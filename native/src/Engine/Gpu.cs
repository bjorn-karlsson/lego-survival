using System.Numerics;
using Raylib_cs;

namespace Brickblade;

public enum BlendOp { SourceOver, Lighter, DestinationOut, DestinationIn, SourceIn, Copy }

/* Where drawing lands: the window, or an offscreen canvas's texture. */
public sealed class Target
{
    public uint fbo, tex, depth;
    public int w, h;
    public bool screen;
    public RenderTexture2D rt;
    public int filter = -1;      /* the sampling it was last set to (-1: never) */
    public bool dirty, mips;     /* drawn into since its mip levels were made; has mip levels */
}

/* The one place that talks to OpenGL. Every shape the canvas shim makes becomes
   triangles in ONE vertex buffer (position, texcoord, premultiplied colour);
   the buffer goes to the GPU when something that cannot share a draw call
   changes: the texture, the blend, the target, the clip mask, or the gradient.
   Everything is premultiplied alpha, so offscreen canvases composite onto each
   other exactly the way a browser's do. */
/* what a frame asked of the renderer (F3, --script 'gfx'): plain counters, reset each frame */
public static class GfxStats
{
    /* the per-call timers run only while someone is looking (F3, --script) */
    public static bool On;
    public static int Fills, FillsFan, FillsMulti, FillsTess, FillsEar, Strokes, Texts, Glyphs, Clips, Images, GradFills, Shadows, NewTargets;
    public static int BrkTarget, BrkBlend, BrkClip, BrkTex, BrkGrad, BrkFlushDirect;

    public static long TFill, TStroke, TImage, TText, TClip, TPath, TTess;
    static double Ms(long t) => t * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    public static string Times() => $"fill {Ms(TFill):0.0}ms (tess {Ms(TTess):0.0}ms) stroke {Ms(TStroke):0.0}ms image {Ms(TImage):0.0}ms text {Ms(TText):0.0}ms clip {Ms(TClip):0.0}ms arcs {Ms(TPath):0.0}ms";
    public static string Line() => $"fills {Fills} (fan {FillsFan}, multi {FillsMulti}, ear {FillsEar}, tess {FillsTess}, grad {GradFills}) strokes {Strokes} texts {Texts}/{Glyphs} clips {Clips} images {Images} shadows {Shadows} newTargets {NewTargets} | {Times()} | breaks: target {BrkTarget} blend {BrkBlend} clip {BrkClip} tex {BrkTex} grad {BrkGrad} direct {BrkFlushDirect}";
    public static void Reset() { TFill = TStroke = TImage = TText = TClip = TPath = TTess = 0; Fills = FillsFan = FillsMulti = FillsTess = FillsEar = Strokes = Texts = Glyphs = Clips = Images = GradFills = Shadows = NewTargets = 0; BrkTarget = BrkBlend = BrkClip = BrkTex = BrkGrad = BrkFlushDirect = 0; }
}

/* one vertex as the GPU reads it: device position, texture coordinate (or the
   user-space position a gradient is evaluated at), and the premultiplied colour
   as four 16-bit parts -- 24 bytes, where four floats of colour made it 32, and
   still exact for the faintest translucent passes */
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
public struct Vtx { public float x, y, u, v; public ulong c; }

public static unsafe class Gpu
{
    /* A BATCH IS INDEXED: shapes share their corners (a disc is its rim and its
       centre, not three corners per sliver), and the indices say which corners make
       each triangle. 16-bit indices, so a batch holds up to 65,532 corners. */
    public const int MAXV = 65532;
    const int MAXI = MAXV * 3;
    static readonly Vtx[] vb = new Vtx[MAXV];
    static readonly ushort[] ib = new ushort[MAXI];
    static int nv, ni;
    static uint vao, vbo, ebo;
    static Shader progTex, progGrad;
    static int tSize, tUseClip, tTex, tClip;
    static int gSize, gUseClip, gClip, gKind, gP, gN, gOff, gCol;
    static uint whiteTex;
    public static uint WhiteTex => whiteTex;

    /* the state the pending triangles were made under */
    static bool useGrad; static Grad curGrad;
    static uint curTex;
    static BlendOp curBlend = BlendOp.SourceOver;
    static Target curTarget, boundTarget;
    static Target curClip;
    public static Target Screen;
    /* an axis-aligned clip in device pixels (the UI's overflow clipping); null is none */
    static (int x, int y, int w, int h)? scissor;
    /* drawing FROM a canvas: what is still pending INTO it has to land first */
    public static void ReadingFrom(Target t) { if (nv > 0 && curTarget == t) Flush(); }
    /* a canvas's sampling (smooth or nearest): a change waits for what already uses it */
    public static void Filter(Target t, bool smooth)
    {
        int f = smooth ? (t.mips ? 0x2703 : 0x2601) : 0x2600;
        if (t.filter == f) return;
        if (nv > 0 && curTex == t.tex) Flush();
        Rlgl.TextureParameters(t.tex, 0x2800, smooth ? 0x2601 : 0x2600);
        Rlgl.TextureParameters(t.tex, 0x2801, f);
        t.filter = f;
    }
    /* A SUPERSAMPLED CANVAS IS SHRUNK, not just sampled: its mip levels are made
       again whenever it was drawn into since, so drawing it at any smaller size
       averages all the pixels under each one (trilinear), not four of them */
    public static void Mips(Target t)
    {
        if (!t.dirty || t.screen || t.tex == 0) return;
        if (nv > 0 && curTex == t.tex) Flush();
        int levels = 0;
        Rlgl.GenTextureMipmaps(t.tex, t.w, t.h, PixelFormat.UncompressedR8G8B8A8, &levels);
        /* a little towards the larger level: smooth, without the softness of an even blend */
        Rlgl.TextureParameters(t.tex, 0x4000, -50);
        t.mips = true; t.dirty = false; t.filter = -1;
    }
    public static void Scissor((int x, int y, int w, int h)? r)
    {
        if (r == scissor) return;
        Flush();
        scissor = r;
    }
    public static int DrawCalls, Tris, Verts;

    const string VS = @"#version 330
layout(location=0) in vec2 aPos; layout(location=1) in vec2 aUv; layout(location=2) in vec4 aCol;
uniform vec2 uSize;
out vec2 vUv; out vec4 vCol;
void main(){ vUv = aUv; vCol = aCol; gl_Position = vec4(aPos.x*2.0/uSize.x - 1.0, 1.0 - aPos.y*2.0/uSize.y, 0.0, 1.0); }";
    const string FS_TEX = @"#version 330
in vec2 vUv; in vec4 vCol;
uniform sampler2D uTex; uniform sampler2D uClip; uniform int uUseClip; uniform vec2 uSize;
out vec4 o;
void main(){
  vec4 c = texture(uTex, vUv) * vCol;
  if(uUseClip != 0) c *= texture(uClip, gl_FragCoord.xy / uSize).a;
  o = c;
}";
    const string FS_GRAD = @"#version 330
in vec2 vUv; in vec4 vCol;
uniform int uKind; uniform vec4 uP; uniform int uN; uniform float uOff[8]; uniform vec4 uCol[8];
uniform sampler2D uClip; uniform int uUseClip; uniform vec2 uSize;
out vec4 o;
void main(){
  float t;
  if(uKind == 0){ vec2 d = uP.zw - uP.xy; t = dot(vUv - uP.xy, d) / max(dot(d, d), 1e-9); }
  else { float r = length(vUv - uP.xy); t = (r - uP.z) / max(uP.w - uP.z, 1e-9); }
  t = clamp(t, 0.0, 1.0);
  vec4 c = uCol[0];
  for(int i = 1; i < 8; i++){
    if(i >= uN) break;
    if(t <= uOff[i]){ float f = (t - uOff[i-1]) / max(uOff[i] - uOff[i-1], 1e-9); c = mix(uCol[i-1], uCol[i], clamp(f, 0.0, 1.0)); break; }
    c = uCol[i];
  }
  vec4 pm = vec4(c.rgb * c.a, c.a) * vCol.a;
  if(uUseClip != 0) pm *= texture(uClip, gl_FragCoord.xy / uSize).a;
  o = pm;
}";

    public static void Init(int w, int h)
    {
        progTex = Raylib.LoadShaderFromMemory(VS, FS_TEX);
        progGrad = Raylib.LoadShaderFromMemory(VS, FS_GRAD);
        tSize = Raylib.GetShaderLocation(progTex, "uSize"); tUseClip = Raylib.GetShaderLocation(progTex, "uUseClip");
        tTex = Raylib.GetShaderLocation(progTex, "uTex"); tClip = Raylib.GetShaderLocation(progTex, "uClip");
        gSize = Raylib.GetShaderLocation(progGrad, "uSize"); gUseClip = Raylib.GetShaderLocation(progGrad, "uUseClip");
        gClip = Raylib.GetShaderLocation(progGrad, "uClip"); gKind = Raylib.GetShaderLocation(progGrad, "uKind");
        gP = Raylib.GetShaderLocation(progGrad, "uP"); gN = Raylib.GetShaderLocation(progGrad, "uN");
        gOff = Raylib.GetShaderLocation(progGrad, "uOff"); gCol = Raylib.GetShaderLocation(progGrad, "uCol");
        whiteTex = Rlgl.GetTextureIdDefault();
        vao = Rlgl.LoadVertexArray();
        Rlgl.EnableVertexArray(vao);
        fixed (Vtx* p = vb) vbo = Rlgl.LoadVertexBuffer(p, MAXV * sizeof(Vtx), true);
        Rlgl.SetVertexAttribute(0, 2, 0x1406, false, sizeof(Vtx), 0); Rlgl.EnableVertexAttribute(0);
        Rlgl.SetVertexAttribute(1, 2, 0x1406, false, sizeof(Vtx), 8); Rlgl.EnableVertexAttribute(1);
        Rlgl.SetVertexAttribute(2, 4, 0x1403, true, sizeof(Vtx), 16); Rlgl.EnableVertexAttribute(2);
        fixed (ushort* q = ib) ebo = Rlgl.LoadVertexBufferElement(q, MAXI * 2, true);
        Rlgl.DisableVertexArray();
        Screen = new Target { screen = true, w = w, h = h };
        curTarget = Screen;
    }
    public static void ResizeScreen(int w, int h) { Flush(); Screen.w = w; Screen.h = h; boundTarget = null; }

    public static Target NewTarget(int w, int h)
    {
        GfxStats.NewTargets++;
        w = Math.Max(1, w); h = Math.Max(1, h);
        var rt = Raylib.LoadRenderTexture(w, h);
        Raylib.SetTextureFilter(rt.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureWrap(rt.Texture, TextureWrap.Clamp);
        var t = new Target { rt = rt, fbo = rt.Id, tex = rt.Texture.Id, w = w, h = h, filter = 0x2601 };
        Clear(t);
        return t;
    }
    public static void FreeTarget(Target t)
    {
        if (t == null || t.screen) return;
        Flush();
        if (boundTarget == t) boundTarget = null;
        if (curTarget == t) curTarget = Screen;
        Raylib.UnloadRenderTexture(t.rt);
        t.fbo = 0; t.tex = 0;
    }
    public static void Clear(Target t)
    {
        Flush();
        Bind(t);
        t.dirty = true;
        Rlgl.ClearColor(0, 0, 0, 0);
        Rlgl.ClearScreenBuffers();
    }
    public static void ClearScreen(Col c)
    {
        Flush();
        Bind(Screen);
        Rlgl.ClearColor((byte)c.r, (byte)c.g, (byte)c.b, (byte)(c.a * 255));
        Rlgl.ClearScreenBuffers();
    }
    static void Bind(Target t)
    {
        /* always rebind: raylib binds framebuffers itself when it makes a texture */
        if (t.screen) Rlgl.DisableFramebuffer(); else Rlgl.EnableFramebuffer(t.fbo);
        Rlgl.Viewport(0, 0, t.w, t.h);
        boundTarget = t;
    }
    /* a region of one target copied, pixel for pixel, into the top-left of another
       (x, y from the top, as the canvas counts) -- what is behind a backdrop blur */
    public static void Copy(Target src, int x, int y, int w, int h, Target dst)
    {
        Flush();
        const int READ = 0x8CA8, DRAW = 0x8CA9, COLOR = 0x4000;
        w = Math.Min(w, dst.w); h = Math.Min(h, dst.h);
        if (w <= 0 || h <= 0) return;
        Rlgl.BindFramebuffer(READ, src.screen ? 0 : src.fbo);
        Rlgl.BindFramebuffer(DRAW, dst.fbo);
        int gy = src.h - (y + h);
        Rlgl.BlitFramebuffer(x, gy, w, h, 0, dst.h - h, w, h, COLOR);
        Rlgl.BindFramebuffer(READ, 0); Rlgl.BindFramebuffer(DRAW, 0);
        boundTarget = null;
    }
    /* the frame ends: whatever raylib does next starts from a clean slate */
    public static void EndFrame()
    {
        Flush();
        Rlgl.DisableFramebuffer();
        boundTarget = null;
        Rlgl.Viewport(0, 0, Screen.w, Screen.h);
        Rlgl.SetBlendMode(BlendMode.Alpha);
    }

    /* ---- state the next triangles need; a change sends what is pending ---- */
    public static void Use(Target t, BlendOp op, Target clip, uint tex)
    {
        if (t != curTarget || op != curBlend || clip != curClip || tex != curTex || useGrad)
        {
            if (nv > 0) { if (t != curTarget) GfxStats.BrkTarget++; else if (op != curBlend) GfxStats.BrkBlend++; else if (clip != curClip) GfxStats.BrkClip++; else if (useGrad) GfxStats.BrkGrad++; else GfxStats.BrkTex++; }
            Flush();
            curTarget = t; curBlend = op; curClip = clip; curTex = tex; useGrad = false; curGrad = null;
        }
    }
    public static void UseGrad(Target t, BlendOp op, Target clip, Grad g)
    {
        if (t != curTarget || op != curBlend || clip != curClip || !useGrad || g != curGrad)
        {
            if (nv > 0) { if (t != curTarget) GfxStats.BrkTarget++; else if (op != curBlend) GfxStats.BrkBlend++; else if (clip != curClip) GfxStats.BrkClip++; else GfxStats.BrkGrad++; }
            Flush();
            curTarget = t; curBlend = op; curClip = clip; useGrad = true; curGrad = g; curTex = whiteTex;
        }
    }
    static void ApplyBlend(BlendOp op)
    {
        const int ZERO = 0, ONE = 1, SA = 0x0302, ISA = 0x0303, DA = 0x0304, ADD = 0x8006;
        int s = ONE, d = ISA;
        switch (op)
        {
            case BlendOp.SourceOver: s = ONE; d = ISA; break;
            case BlendOp.Lighter: s = ONE; d = ONE; break;
            case BlendOp.DestinationOut: s = ZERO; d = ISA; break;
            case BlendOp.DestinationIn: s = ZERO; d = SA; break;
            case BlendOp.SourceIn: s = DA; d = ZERO; break;
            case BlendOp.Copy: s = ONE; d = ZERO; break;
        }
        Rlgl.SetBlendFactorsSeparate(s, d, s, d, ADD, ADD);
        Rlgl.SetBlendMode(BlendMode.CustomSeparate);
    }

    public static long GlTicks;          /* time spent handing batches to the driver this frame */
    public static void Flush()
    {
        if (nv == 0) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var t = curTarget ?? Screen;
        t.dirty = true;
        Bind(t);
        ApplyBlend(curBlend);
        Rlgl.DisableBackfaceCulling();
        Rlgl.DisableDepthTest();
        if (scissor is { } sc) { Rlgl.EnableScissorTest(); Rlgl.Scissor(sc.x, t.h - (sc.y + sc.h), Math.Max(0, sc.w), Math.Max(0, sc.h)); }
        var sz = new Vector2(t.w, t.h);
        int useClip = curClip != null && curClip.tex != 0 ? 1 : 0;
        if (!useGrad)
        {
            Rlgl.EnableShader(progTex.Id);
            Rlgl.SetUniform(tSize, &sz, (int)ShaderUniformDataType.Vec2, 1);
            Rlgl.SetUniform(tUseClip, &useClip, (int)ShaderUniformDataType.Int, 1);
            int u0 = 0, u1 = 1;
            Rlgl.SetUniform(tTex, &u0, (int)ShaderUniformDataType.Int, 1);
            Rlgl.SetUniform(tClip, &u1, (int)ShaderUniformDataType.Int, 1);
        }
        else
        {
            Rlgl.EnableShader(progGrad.Id);
            Rlgl.SetUniform(gSize, &sz, (int)ShaderUniformDataType.Vec2, 1);
            Rlgl.SetUniform(gUseClip, &useClip, (int)ShaderUniformDataType.Int, 1);
            int u1 = 1;
            Rlgl.SetUniform(gClip, &u1, (int)ShaderUniformDataType.Int, 1);
            var g = curGrad;
            int kind = g.radial ? 1 : 0;
            Rlgl.SetUniform(gKind, &kind, (int)ShaderUniformDataType.Int, 1);
            var p = g.radial ? new Vector4((float)g.x1, (float)g.y1, (float)g.r0, (float)g.r1)
                             : new Vector4((float)g.x0, (float)g.y0, (float)g.x1, (float)g.y1);
            Rlgl.SetUniform(gP, &p, (int)ShaderUniformDataType.Vec4, 1);
            int n = Math.Min(8, Math.Max(1, g.stops.Count));
            Rlgl.SetUniform(gN, &n, (int)ShaderUniformDataType.Int, 1);
            float* offs = stackalloc float[8]; Vector4* cols = stackalloc Vector4[8];
            for (int i = 0; i < 8; i++)
            {
                var st = g.stops.Count == 0 ? (0.0, new Col(0, 0, 0, 0)) : g.stops[Math.Min(i, g.stops.Count - 1)];
                offs[i] = (float)st.Item1;
                cols[i] = new Vector4(st.Item2.r / 255f, st.Item2.g / 255f, st.Item2.b / 255f, st.Item2.a);
            }
            Rlgl.SetUniform(gOff, offs, (int)ShaderUniformDataType.Float, 8);
            Rlgl.SetUniform(gCol, cols, (int)ShaderUniformDataType.Vec4, 8);
        }
        Rlgl.ActiveTextureSlot(0); Rlgl.EnableTexture(useGrad ? whiteTex : curTex);
        Rlgl.ActiveTextureSlot(1); Rlgl.EnableTexture(useClip == 1 ? curClip.tex : whiteTex);
        Rlgl.ActiveTextureSlot(0);
        Rlgl.EnableVertexArray(vao);
        fixed (Vtx* p = vb) Rlgl.UpdateVertexBuffer(vbo, p, nv * sizeof(Vtx), 0);
        fixed (ushort* q = ib) Rlgl.UpdateVertexBufferElements(ebo, q, ni * 2, 0);
        Rlgl.DrawVertexArrayElements(0, ni, null);
        Rlgl.DisableVertexArray();
        if (scissor != null) Rlgl.DisableScissorTest();
        Rlgl.DisableShader();
        DrawCalls++; Tris += ni / 3; Verts += nv;
        nv = 0; ni = 0;
        GlTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
    }

    /* a premultiplied colour (each part 0..1) as the four 16-bit parts a vertex carries */
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong Pack(float r, float g, float b, float a)
    {
        static ulong B(float f) => f <= 0 ? 0ul : f >= 1 ? 65535ul : (ulong)(f * 65535f + 0.5f);
        return B(r) | B(g) << 16 | B(b) << 32 | B(a) << 48;
    }
    /* one corner of a triangle in a list of separate triangles (three V calls make one) */
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void V(float x, float y, float u, float v, float r, float g, float b, float a)
    {
        ref var t = ref vb[nv]; t.x = x; t.y = y; t.u = u; t.v = v; t.c = Pack(r, g, b, a);
        ib[ni++] = (ushort)nv; nv++;
    }
    /* room for n more separate-triangle corners (a multiple of 3) */
    public static void Need(int n)
    {
        if (nv + n > MAXV || ni + n > MAXI) Flush();
    }
    /* SHARED CORNERS: room for nVerts corners and nIdx indices; returns the index
       the first corner added next will have. False when the shape cannot fit in
       one batch at all (then it goes as separate triangles). */
    public static bool Begin(int nVerts, int nIdx, out int baseV)
    {
        baseV = 0;
        if (nVerts > MAXV || nIdx > MAXI) return false;
        if (nv + nVerts > MAXV || ni + nIdx > MAXI) Flush();
        baseV = nv;
        return true;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void VX(float x, float y, float u, float v, ulong c) { ref var t = ref vb[nv++]; t.x = x; t.y = y; t.u = u; t.v = v; t.c = c; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void I(int i) { ib[ni++] = (ushort)i; }
}
