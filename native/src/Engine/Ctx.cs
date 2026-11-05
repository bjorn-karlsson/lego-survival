using System.Numerics;
using System.Runtime.CompilerServices;
using LibTessDotNet;

namespace Brickblade;

/* An offscreen canvas: document.createElement('canvas'). Its 2D context draws
   into a GPU texture; ctx.drawImage puts that texture on another canvas. */
public sealed class OffCanvas
{
    int _w = 300, _h = 150;
    public Target target;
    Ctx _ctx;
    public int width { get => _w; set { if (value != _w) { _w = Math.Max(1, value); Realloc(); } else Gpu.Clear(T); } }
    public int height { get => _h; set { if (value != _h) { _h = Math.Max(1, value); Realloc(); } else Gpu.Clear(T); } }
    void Realloc() { if (target != null) { Gpu.FreeTarget(target); target = null; } }
    public Target T { get { if (target == null || target.tex == 0) target = Gpu.NewTarget(_w, _h); return target; } }
    public Ctx getContext(string kind = "2d") { if (_ctx == null) _ctx = new Ctx(this); return _ctx; }
    public void Free() { Realloc(); }
    /* A browser frees a canvas's pixels when nothing holds the canvas any more. A
       texture is not the garbage collector's to free (and its finalizer thread may
       not touch GL), so a canvas made for something that dies -- a golem's baked
       body -- is OWNED by it, and the frame loop frees the canvases whose owners
       have been collected. */
    static readonly List<(WeakReference<object> owner, OffCanvas cv)> owned = new();
    public OffCanvas Own(object owner) { owned.Add((new WeakReference<object>(owner), this)); return this; }
    public static void SweepOwned()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (!owned[i].owner.TryGetTarget(out _)) { owned[i].cv.Free(); owned.RemoveAt(i); }
    }
}

public enum Cap { Butt, Round, Square }
public enum Join { Miter, Round, Bevel }

public sealed class TextMetrics { public double width; }

/* CanvasRenderingContext2D, the parts the game uses, on the GPU batcher. Path
   points are transformed when they are added (as the canvas does), so a path
   lives in device pixels; fills and strokes are tessellated there. */
public sealed class Ctx
{
    readonly OffCanvas owner;          /* null: the window */
    public Target Tgt => owner != null ? owner.T : (MainTarget ?? Gpu.Screen);
    /* the window's canvas can be redirected to an offscreen target (render scale) */
    public static Target MainTarget;
    public OffCanvas canvas => owner;

    struct St
    {
        public double a, b, c, d, e, f;
        public Paint fill, stroke;
        public double lw; public Cap cap; public Join join; public double miter;
        public double alpha; public BlendOp op;
        public FontSpec font; public int align, baseline; public double ls;
        public double[] dash; public double dashOff;
        public double shBlur, shX, shY; public Col shCol;
        public Target clip; public bool smooth;
    }
    St s;
    readonly Stack<St> stack = new();

    public Ctx(OffCanvas owner)
    {
        this.owner = owner;
        Reset();
    }
    public void Reset()
    {
        s = new St
        {
            a = 1, d = 1, fill = "#000", stroke = "#000", lw = 1, cap = Cap.Butt, join = Join.Miter, miter = 10,
            alpha = 1, op = BlendOp.SourceOver, font = FontSpec.Parse("10px sans-serif"), align = 0, baseline = 0,
            dash = null, smooth = true
        };
        stack.Clear();
        beginPath();
    }

    /* ================= state ================= */
    public Paint fillStyle { set => s.fill = value; get => s.fill; }
    public Paint strokeStyle { set => s.stroke = value; get => s.stroke; }
    public double lineWidth { set { if (value > 0 && Js.IsFinite(value)) s.lw = value; } get => s.lw; }
    public string lineCap { set => s.cap = value == "round" ? Cap.Round : value == "square" ? Cap.Square : Cap.Butt; get => s.cap == Cap.Round ? "round" : s.cap == Cap.Square ? "square" : "butt"; }
    public string lineJoin { set => s.join = value == "round" ? Join.Round : value == "bevel" ? Join.Bevel : Join.Miter; get => s.join == Join.Round ? "round" : s.join == Join.Bevel ? "bevel" : "miter"; }
    public double miterLimit { set => s.miter = value; get => s.miter; }
    public double globalAlpha { set { if (value >= 0 && value <= 1) s.alpha = value; else if (value > 1) s.alpha = 1; } get => s.alpha; }
    public string globalCompositeOperation
    {
        set => s.op = value switch { "lighter" => BlendOp.Lighter, "destination-out" => BlendOp.DestinationOut, "destination-in" => BlendOp.DestinationIn, "source-in" => BlendOp.SourceIn, "copy" => BlendOp.Copy, _ => BlendOp.SourceOver };
        get => s.op switch { BlendOp.Lighter => "lighter", BlendOp.DestinationOut => "destination-out", BlendOp.DestinationIn => "destination-in", BlendOp.SourceIn => "source-in", BlendOp.Copy => "copy", _ => "source-over" };
    }
    public string font { set => s.font = FontSpec.Parse(value); get => s.font.src; }
    /* a font already resolved (the page's CSS fonts): TEXT SIZE is in it already, so it
       must not go through the string setter, which scales the game's canvas fonts */
    public FontSpec fontSpec { set => s.font = value; get => s.font; }
    /* ctx.letterSpacing = '1.5px': added after every character, the last included */
    public string letterSpacing { set => s.ls = Js.ParseFloat(value ?? "0") is var v && Js.IsFinite(v) ? v : 0; get => Js.Num(s.ls) + "px"; }
    public string textAlign { set => s.align = value == "center" ? 1 : value == "right" || value == "end" ? 2 : 0; get => s.align == 1 ? "center" : s.align == 2 ? "right" : "left"; }
    public string textBaseline { set => s.baseline = value switch { "middle" => 1, "top" => 2, "bottom" => 3, "hanging" => 4, "ideographic" => 3, _ => 0 }; get => s.baseline switch { 1 => "middle", 2 => "top", 3 => "bottom", 4 => "hanging", _ => "alphabetic" }; }
    public double shadowBlur { set => s.shBlur = value; get => s.shBlur; }
    public double shadowOffsetX { set => s.shX = value; get => s.shX; }
    public double shadowOffsetY { set => s.shY = value; get => s.shY; }
    public Col shadowColor { set => s.shCol = value; get => s.shCol; }
    public bool imageSmoothingEnabled { set => s.smooth = value; get => s.smooth; }
    public double lineDashOffset { set => s.dashOff = value; get => s.dashOff; }
    public void setLineDash(double[] d)
    {
        if (d == null || d.Length == 0) { s.dash = null; return; }
        foreach (var v in d) if (v < 0 || !Js.IsFinite(v)) return;
        s.dash = d.Length % 2 == 1 ? d.Concat(d).ToArray() : (double[])d.Clone();
        if (s.dash.All(v => v == 0)) s.dash = null;
    }

    public void save() => stack.Push(s);
    public void restore() { if (stack.Count > 0) s = stack.Pop(); }

    /* ================= transform ================= */
    public void setTransform(double a, double b, double c, double d, double e, double f) { s.a = a; s.b = b; s.c = c; s.d = d; s.e = e; s.f = f; }
    public void resetTransform() => setTransform(1, 0, 0, 1, 0, 0);
    public void transform(double a, double b, double c, double d, double e, double f)
    {
        var (A, B, C, D, E, F) = (s.a, s.b, s.c, s.d, s.e, s.f);
        s.a = A * a + C * b; s.b = B * a + D * b;
        s.c = A * c + C * d; s.d = B * c + D * d;
        s.e = A * e + C * f + E; s.f = B * e + D * f + F;
    }
    public void translate(double x, double y) { s.e += s.a * x + s.c * y; s.f += s.b * x + s.d * y; }
    public void scale(double x, double y) { s.a *= x; s.b *= x; s.c *= y; s.d *= y; }
    public void rotate(double r)
    {
        double cs = Math.Cos(r), sn = Math.Sin(r);
        var (A, B, C, D) = (s.a, s.b, s.c, s.d);
        s.a = A * cs + C * sn; s.b = B * cs + D * sn;
        s.c = -A * sn + C * cs; s.d = -B * sn + D * cs;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Vector2 X(double x, double y) => new((float)(s.a * x + s.c * y + s.e), (float)(s.b * x + s.d * y + s.f));
    double Scale => Math.Sqrt(Math.Abs(s.a * s.d - s.b * s.c));
    /* device -> user, for gradients */
    bool Inv(out double ia, out double ib, out double ic, out double id, out double ie, out double iff)
    {
        double det = s.a * s.d - s.b * s.c;
        if (Math.Abs(det) < 1e-12) { ia = ib = ic = id = ie = iff = 0; return false; }
        ia = s.d / det; ib = -s.b / det; ic = -s.c / det; id = s.a / det;
        ie = (s.c * s.f - s.d * s.e) / det; iff = (s.b * s.e - s.a * s.f) / det;
        return true;
    }

    /* ================= path ================= */
    sealed class Sub { public readonly List<Vector2> p = new(); public bool closed; }
    /* sub-paths and their point lists are used again, path after path: a frame
       builds thousands of paths, and making each from new is most of the garbage */
    List<Sub> subs = new();
    readonly Stack<Sub> subPool = new();
    readonly Stack<List<Sub>> listPool = new();
    Sub cur;
    double ux, uy;            /* the current point, user space */
    bool hasPt;
    Sub Fresh() { var sb = subPool.Count > 0 ? subPool.Pop() : new Sub(); sb.p.Clear(); sb.closed = false; return sb; }
    void Release(List<Sub> l) { foreach (var sb in l) subPool.Push(sb); l.Clear(); }
    public void beginPath() { Release(subs); cur = null; hasPt = false; }
    /* a path drawn in passing (fill(path2d), strokeRect...) leaves the current path alone */
    List<Sub> Aside() { var keep = subs; subs = listPool.Count > 0 ? listPool.Pop() : new List<Sub>(); return keep; }
    void Back(List<Sub> keep) { Release(subs); listPool.Push(subs); subs = keep; }
    void NewSub(double x, double y)
    {
        cur = Fresh(); subs.Add(cur);
        cur.p.Add(X(x, y)); ux = x; uy = y; hasPt = true;
    }
    public void moveTo(double x, double y) { if (!Js.IsFinite(x) || !Js.IsFinite(y)) return; NewSub(x, y); }
    public void lineTo(double x, double y)
    {
        if (!Js.IsFinite(x) || !Js.IsFinite(y)) return;
        if (cur == null || cur.closed) { if (cur != null && cur.closed) { var st = cur.p[0]; cur = Fresh(); subs.Add(cur); cur.p.Add(st); } else { NewSub(x, y); return; } }
        cur.p.Add(X(x, y)); ux = x; uy = y; hasPt = true;
    }
    public void closePath()
    {
        if (cur == null || cur.p.Count == 0) return;
        cur.closed = true;
        /* the current point goes back to where the sub-path began */
    }
    void EnsureOpen()
    {
        if (cur != null && cur.closed) { var st = cur.p[0]; cur = Fresh(); subs.Add(cur); cur.p.Add(st); }
    }
    public void rect(double x, double y, double w, double h)
    {
        NewSub(x, y); cur.p.Add(X(x + w, y)); cur.p.Add(X(x + w, y + h)); cur.p.Add(X(x, y + h)); cur.closed = true; ux = x; uy = y;
    }
    int ArcSegs(double r, double sweep)
    {
        double rd = Math.Abs(r) * Scale;
        if (rd < 0.5) return Math.Max(3, (int)Math.Ceiling(Math.Abs(sweep) / 1.6));
        double tol = 0.22;
        double step = 2 * Math.Acos(Math.Clamp(1 - tol / rd, -1, 1));
        step = Math.Clamp(step, 0.02, 0.9);
        return Math.Clamp((int)Math.Ceiling(Math.Abs(sweep) / step), 2, 720);
    }
    /* Chromium's rule (adjustEndAngle): a full turn either way is the whole
       circle, and arc(x, y, r, 0, 2*PI, true) is a whole circle too */
    static double Sweep(double a0, double a1, bool ccw)
    {
        const double TAU = Math.PI * 2;
        if (!ccw && a1 - a0 >= TAU) return TAU;
        if (ccw && a0 - a1 >= TAU) return -TAU;
        if (!ccw && a0 > a1) return TAU - ((a0 - a1) % TAU);
        if (ccw && a0 < a1) return -(TAU - ((a1 - a0) % TAU));
        return a1 - a0;
    }
    public void arc(double x, double y, double r, double a0, double a1, bool ccw = false)
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try {
        if (!Js.IsFinite(x) || !Js.IsFinite(y) || !Js.IsFinite(r) || !Js.IsFinite(a0) || !Js.IsFinite(a1)) return;
        r = Math.Abs(r);
        double sw = Sweep(a0, a1, ccw);
        double sx = x + Math.Cos(a0) * r, sy = y + Math.Sin(a0) * r;
        if (cur == null) NewSub(sx, sy); else { EnsureOpen(); cur.p.Add(X(sx, sy)); }
        int n = ArcSegs(r, sw);
        /* the points by rotation: one cos and sin for the step, not one per point */
        double cs = Math.Cos(sw / n), sn = Math.Sin(sw / n), ca = Math.Cos(a0), sa = Math.Sin(a0);
        for (int i = 1; i <= n; i++)
        {
            double nca = ca * cs - sa * sn; sa = sa * cs + ca * sn; ca = nca;
            cur.p.Add(X(x + ca * r, y + sa * r));
        }
        ux = x + Math.Cos(a0 + sw) * r; uy = y + Math.Sin(a0 + sw) * r; hasPt = true;
            } finally { if (GfxStats.On) GfxStats.TPath += System.Diagnostics.Stopwatch.GetTimestamp() - __t0; }
    }
    public void ellipse(double x, double y, double rx, double ry, double rot, double a0, double a1, bool ccw = false)
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try {
        if (!Js.IsFinite(x) || !Js.IsFinite(y) || !Js.IsFinite(rx) || !Js.IsFinite(ry)) return;
        rx = Math.Abs(rx); ry = Math.Abs(ry);
        double sw = Sweep(a0, a1, ccw);
        double cr = Math.Cos(rot), sr = Math.Sin(rot);
        (double, double) P(double t) { double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t); return (x + cr * ex - sr * ey, y + sr * ex + cr * ey); }
        var (sx, sy) = P(a0);
        if (cur == null) NewSub(sx, sy); else { EnsureOpen(); cur.p.Add(X(sx, sy)); }
        int n = ArcSegs(Math.Max(rx, ry), sw);
        double cs = Math.Cos(sw / n), sn = Math.Sin(sw / n), ct = Math.Cos(a0), st = Math.Sin(a0);
        for (int i = 1; i <= n; i++)
        {
            double nct = ct * cs - st * sn; st = st * cs + ct * sn; ct = nct;
            double ex = rx * ct, ey = ry * st;
            cur.p.Add(X(x + cr * ex - sr * ey, y + sr * ex + cr * ey));
        }
        (ux, uy) = P(a0 + sw); hasPt = true;
            } finally { if (GfxStats.On) GfxStats.TPath += System.Diagnostics.Stopwatch.GetTimestamp() - __t0; }
    }
    public void arcTo(double x1, double y1, double x2, double y2, double r)
    {
        if (cur == null) { moveTo(x1, y1); return; }
        EnsureOpen();
        double x0 = ux, y0 = uy;
        double dx0 = x0 - x1, dy0 = y0 - y1, dx2 = x2 - x1, dy2 = y2 - y1;
        double l0 = Math.Sqrt(dx0 * dx0 + dy0 * dy0), l2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
        if (l0 < 1e-9 || l2 < 1e-9 || r <= 0) { lineTo(x1, y1); return; }
        dx0 /= l0; dy0 /= l0; dx2 /= l2; dy2 /= l2;
        double cos = dx0 * dx2 + dy0 * dy2;
        if (Math.Abs(cos) > 0.999999) { lineTo(x1, y1); return; }
        double ang = Math.Acos(cos), tdist = r / Math.Tan(ang / 2);
        double tx0 = x1 + dx0 * tdist, ty0 = y1 + dy0 * tdist, tx2 = x1 + dx2 * tdist, ty2 = y1 + dy2 * tdist;
        double bx = dx0 + dx2, by = dy0 + dy2, bl = Math.Sqrt(bx * bx + by * by);
        double cd = r / Math.Sin(ang / 2);
        double cx = x1 + bx / bl * cd, cy = y1 + by / bl * cd;
        double a0 = Math.Atan2(ty0 - cy, tx0 - cx), a1 = Math.Atan2(ty2 - cy, tx2 - cx);
        double cross = dx0 * dy2 - dy0 * dx2;
        lineTo(tx0, ty0);
        arc(cx, cy, r, a0, a1, cross > 0);
    }
    public void quadraticCurveTo(double cx, double cy, double x, double y)
    {
        if (cur == null) NewSub(cx, cy);
        EnsureOpen();
        double x0 = ux, y0 = uy;
        var d0 = X(x0, y0); var d1 = X(cx, cy); var d2 = X(x, y);
        float len = Vector2.Distance(d0, d1) + Vector2.Distance(d1, d2);
        int n = Math.Clamp((int)(len / 4), 3, 64);
        for (int i = 1; i <= n; i++)
        {
            double t = (double)i / n, mt = 1 - t;
            cur.p.Add(X(mt * mt * x0 + 2 * mt * t * cx + t * t * x, mt * mt * y0 + 2 * mt * t * cy + t * t * y));
        }
        ux = x; uy = y;
    }
    public void bezierCurveTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
    {
        if (cur == null) NewSub(c1x, c1y);
        EnsureOpen();
        double x0 = ux, y0 = uy;
        float len = Vector2.Distance(X(x0, y0), X(c1x, c1y)) + Vector2.Distance(X(c1x, c1y), X(c2x, c2y)) + Vector2.Distance(X(c2x, c2y), X(x, y));
        int n = Math.Clamp((int)(len / 4), 4, 96);
        for (int i = 1; i <= n; i++)
        {
            double t = (double)i / n, mt = 1 - t;
            double a = mt * mt * mt, b = 3 * mt * mt * t, c = 3 * mt * t * t, d = t * t * t;
            cur.p.Add(X(a * x0 + b * c1x + c * c2x + d * x, a * y0 + b * c1y + c * c2y + d * y));
        }
        ux = x; uy = y;
    }
    /* Path2D: a recorded path, replayed into the context with the transform of
       the moment it is used (as the canvas does) */
    public void AddRecorded(Path2D p) { p.Replay(this); }

    /* ================= painting ================= */
    Target Clip => s.clip;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Col Pm(Col c, double alpha) { if (Filter != null) c = Filter(c); float a = (float)(c.a * alpha); return new Col(c.r / 255f * a, c.g / 255f * a, c.b / 255f * a, a); }
    /* a CSS filter (saturate, grayscale, brightness) on every colour drawn while it is set */
    public static Func<Col, Col> Filter;
    public static Col Premul(Col c, double alpha) => Pm(c, alpha);

    /* A SHAPE'S TRIANGLES, in device space, sharing their corners: a fill's polygon
       is its points and a fan of indices, a stroke's strip shares the corners its
       quads and joins meet at. The GPU gets each corner once. */
    sealed class Mesh
    {
        public readonly List<Vector2> v = new(); public readonly List<int> i = new();
        public void Clear() { v.Clear(); i.Clear(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public int P(Vector2 p) { v.Add(p); return v.Count - 1; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void T(int a, int b, int c) { i.Add(a); i.Add(b); i.Add(c); }
        public void Tri(Vector2 a, Vector2 b, Vector2 c) { int k = v.Count; v.Add(a); v.Add(b); v.Add(c); i.Add(k); i.Add(k + 1); i.Add(k + 2); }
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d) { int k = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); i.Add(k); i.Add(k + 1); i.Add(k + 2); i.Add(k); i.Add(k + 2); i.Add(k + 3); }
        public void Fan(List<Vector2> poly, int n) { int k = v.Count; for (int q = 0; q < n; q++) v.Add(poly[q]); for (int q = 1; q < n - 1; q++) { i.Add(k); i.Add(k + q); i.Add(k + q + 1); } }
        public void Rollback(int vc, int ic) { v.RemoveRange(vc, v.Count - vc); i.RemoveRange(ic, i.Count - ic); }
    }
    readonly Mesh mesh = new(), shMesh = new();
    void TessFill(bool evenodd, List<Sub> list, Mesh outTri, bool opaque = false)
    {
        outTri.Clear();
        int live = 0; Sub only = null;
        foreach (var sb in list) if (sb.p.Count >= 3) { live++; only = sb; }
        if (live == 0) return;
        if (live == 1 && Convex(only.p))
        {
            GfxStats.FillsFan++;
            var p = only.p; int n = p.Count;
            if (n > 2 && p[n - 1] == p[0]) n--;
            outTri.Fan(p, n);
            return;
        }
        /* A SINGLE OUTLINE that is merely concave -- a horn, a helmet, a hand -- is cut
           into triangles by ear clipping, many times faster than the general
           tessellator; anything it cannot prove simple goes on to the tessellator */
        if (live == 1 && !evenodd && only.p.Count <= 256 && TwoLoops(only.p, outTri, opaque)) { GfxStats.FillsMulti++; return; }
        if (live == 1 && !evenodd && only.p.Count <= 256 && EarClip(only.p, outTri)) { GfxStats.FillsEar++; return; }
        outTri.Clear();
        GfxStats.FillsTess++;
        long __tt = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try {
        var tess = new Tess();
        foreach (var sb in list)
        {
            if (sb.p.Count < 3) continue;
            var cv = new ContourVertex[sb.p.Count];
            for (int i = 0; i < sb.p.Count; i++) cv[i].Position = new Vec3(sb.p[i].X, sb.p[i].Y, 0);
            tess.AddContour(cv, ContourOrientation.Original);
        }
        try { tess.Tessellate(evenodd ? WindingRule.EvenOdd : WindingRule.NonZero, ElementType.Polygons, 3); }
        catch { return; }
        for (int i = 0; i < tess.VertexCount; i++) { var v = tess.Vertices[i].Position; outTri.v.Add(new Vector2(v.X, v.Y)); }
        for (int i = 0; i + 2 < tess.ElementCount * 3; i += 3)
        {
            int a = tess.Elements[i], b = tess.Elements[i + 1], c = tess.Elements[i + 2];
            if (a < 0 || b < 0 || c < 0) continue;
            outTri.T(a, b, c);
        }
        } finally { if (GfxStats.On) GfxStats.TTess += System.Diagnostics.Stopwatch.GetTimestamp() - __tt; }
    }
    /* TWO LOOPS IN ONE OUTLINE: arc(); arc(); fill() -- a pair of eyes. Each circle
       closes on its own start and the line between them is walked there and back,
       so it adds nothing: the fill is the two loops. Filled one by one, that is
       exact when they do not overlap, or overlap painted opaque and wound the
       same way (nonzero would fill the overlap once either way). */
    readonly List<Vector2> la = new(), lb = new();
    bool TwoLoops(List<Vector2> p, Mesh outTri, bool opaque)
    {
        int n = p.Count, k = -1;
        if (n < 8) return false;
        for (int i = 3; i < n - 3; i++) if (Vector2.DistanceSquared(p[i], p[0]) < 1e-4f) { k = i; break; }
        if (k < 0 || Vector2.DistanceSquared(p[n - 1], p[k + 1]) >= 1e-4f) return false;
        la.Clear(); for (int i = 0; i < k; i++) la.Add(p[i]);
        lb.Clear(); for (int i = k + 1; i < n - 1; i++) lb.Add(p[i]);
        if (la.Count < 3 || lb.Count < 3) return false;
        static (float x0, float y0, float x1, float y1) Box(List<Vector2> l) { float a = float.MaxValue, b = float.MaxValue, c = float.MinValue, d = float.MinValue; foreach (var v in l) { a = Math.Min(a, v.X); b = Math.Min(b, v.Y); c = Math.Max(c, v.X); d = Math.Max(d, v.Y); } return (a, b, c, d); }
        static double Area(List<Vector2> l) { double s2 = 0; for (int i = 0, j = l.Count - 1; i < l.Count; j = i++) s2 += (double)l[j].X * l[i].Y - (double)l[i].X * l[j].Y; return s2; }
        var A = Box(la); var B = Box(lb);
        bool apart = A.x1 < B.x0 || B.x1 < A.x0 || A.y1 < B.y0 || B.y1 < A.y0;
        if (!apart && !(opaque && Area(la) * Area(lb) > 0)) return false;
        int sv = outTri.v.Count, si = outTri.i.Count;
        foreach (var l in new[] { la, lb })
        {
            if (Convex(l)) outTri.Fan(l, l.Count);
            else if (!EarClip(l, outTri)) { outTri.Rollback(sv, si); return false; }
        }
        return true;
    }
    /* EAR CLIPPING a simple polygon (no edge crossing another). False -- and
       nothing kept -- when it gets stuck, or when the triangles it made do not add
       up to the polygon's own area: a crossing outline, which nonzero filling
       must handle as the tessellator does. */
    readonly List<Vector2> ec = new(); readonly List<int> ei = new();
    bool EarClip(List<Vector2> src, Mesh outTri)
    {
        ec.Clear();
        foreach (var v in src) if (ec.Count == 0 || Vector2.DistanceSquared(ec[^1], v) > 1e-8f) ec.Add(v);
        while (ec.Count > 1 && Vector2.DistanceSquared(ec[0], ec[^1]) <= 1e-8f) ec.RemoveAt(ec.Count - 1);
        int n = ec.Count;
        if (n < 3) return false;
        double area = 0;
        for (int i = 0, j = n - 1; i < n; j = i++) area += (double)ec[j].X * ec[i].Y - (double)ec[i].X * ec[j].Y;
        area *= 0.5;
        if (Math.Abs(area) < 1e-6) return false;
        float orient = area > 0 ? 1 : -1;
        ei.Clear(); for (int i = 0; i < n; i++) ei.Add(i);
        int sv = outTri.v.Count, si = outTri.i.Count; double got = 0;
        foreach (var v in ec) outTri.v.Add(v);
        int guard = 0, k = 0;
        while (ei.Count > 3)
        {
            if (guard++ > ei.Count * 2 + 8) { outTri.Rollback(sv, si); return false; }
            int m = ei.Count;
            int ip = ei[(k + m - 1) % m], ic = ei[k % m], inx = ei[(k + 1) % m];
            Vector2 a = ec[ip], b = ec[ic], c = ec[inx];
            float cr = ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * orient;
            if (cr <= 1e-7f)
            {
                /* collinear: the middle point adds nothing */
                if (cr > -1e-7f) { ei.RemoveAt(k % m); guard = 0; k = k % Math.Max(1, ei.Count); continue; }
                k = (k + 1) % m; continue;
            }
            bool ear = true;
            for (int q = 0; q < m && ear; q++)
            {
                int iv = ei[q];
                if (iv == ip || iv == ic || iv == inx) continue;
                var pv = ec[iv];
                if (pv == a || pv == b || pv == c) continue;
                float d1 = ((b.X - a.X) * (pv.Y - a.Y) - (b.Y - a.Y) * (pv.X - a.X)) * orient;
                float d2 = ((c.X - b.X) * (pv.Y - b.Y) - (c.Y - b.Y) * (pv.X - b.X)) * orient;
                float d3 = ((a.X - c.X) * (pv.Y - c.Y) - (a.Y - c.Y) * (pv.X - c.X)) * orient;
                if (d1 >= -1e-7f && d2 >= -1e-7f && d3 >= -1e-7f) ear = false;
            }
            if (!ear) { k = (k + 1) % m; continue; }
            outTri.T(sv + ip, sv + ic, sv + inx); got += cr * 0.5;
            ei.RemoveAt(k % m); guard = 0;
            k = k % ei.Count;
        }
        {
            Vector2 a = ec[ei[0]], b = ec[ei[1]], c = ec[ei[2]];
            float cr = ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * orient;
            if (cr > 1e-7f) { outTri.T(sv + ei[0], sv + ei[1], sv + ei[2]); got += cr * 0.5; }
        }
        if (Math.Abs(got - Math.Abs(area)) > Math.Abs(area) * 0.002 + 0.01) { outTri.Rollback(sv, si); return false; }
        return true;
    }
    /* CONVEX: every turn the same way, and the edges sweep round once -- their
       direction's x and y each change sign at most twice going round (a star
       turns the same way at every corner but goes round more than once) */
    static bool Convex(List<Vector2> p)
    {
        int n = p.Count;
        if (n > 2 && p[n - 1] == p[0]) n--;
        if (n < 3) return false;
        int sign = 0, xs = 0, ys = 0, xFirst = 0, yFirst = 0, xFlips = 0, yFlips = 0;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[i + 1 < n ? i + 1 : i + 1 - n]; var c = p[i + 2 < n ? i + 2 : i + 2 - n];
            float ex = b.X - a.X, ey = b.Y - a.Y;
            float cx = ex * (c.Y - b.Y) - ey * (c.X - b.X);
            if (cx > 1e-4f || cx < -1e-4f)
            {
                int sg = cx > 0 ? 1 : -1;
                if (sign == 0) sign = sg; else if (sg != sign) return false;
            }
            int sx = ex > 1e-6f ? 1 : ex < -1e-6f ? -1 : 0, sy = ey > 1e-6f ? 1 : ey < -1e-6f ? -1 : 0;
            if (sx != 0) { if (xs != 0 && sx != xs) xFlips++; if (xFirst == 0) xFirst = sx; xs = sx; }
            if (sy != 0) { if (ys != 0 && sy != ys) yFlips++; if (yFirst == 0) yFirst = sy; ys = sy; }
        }
        /* the wrap from the last edge back to the first */
        if (xs != 0 && xFirst != 0 && xs != xFirst) xFlips++;
        if (ys != 0 && yFirst != 0 && ys != yFirst) yFlips++;
        return xFlips <= 2 && yFlips <= 2;
    }
    /* send triangles with a paint */
    void Emit(Mesh m, Paint paint, double extraAlpha = 1)
    {
        if (m.i.Count == 0) return;
        var tg = Tgt;
        var mv = m.v; var mi = m.i;
        if (paint.grad != null)
        {
            if (!Inv(out var ia, out var ib, out var ic, out var id, out var ie, out var iff)) return;
            Gpu.UseGrad(tg, s.op, Clip, paint.grad);
            float ga = (float)(s.alpha * extraAlpha);
            if (Gpu.Begin(mv.Count, mi.Count, out int b0))
            {
                ulong gc = Gpu.Pack(0, 0, 0, ga);
                foreach (var v in mv) Gpu.VX(v.X, v.Y, (float)(ia * v.X + ic * v.Y + ie), (float)(ib * v.X + id * v.Y + iff), gc);
                foreach (var k in mi) Gpu.I(b0 + k);
                return;
            }
            for (int i = 0; i + 2 < mi.Count; i += 3)
            {
                Gpu.Need(3);
                for (int q = 0; q < 3; q++) { var v = mv[mi[i + q]]; Gpu.V(v.X, v.Y, (float)(ia * v.X + ic * v.Y + ie), (float)(ib * v.X + id * v.Y + iff), 0, 0, 0, ga); }
            }
            return;
        }
        var c = Pm(paint.col, s.alpha * extraAlpha);
        if (c.a <= 0 && s.op == BlendOp.SourceOver) return;
        Gpu.Use(tg, s.op, Clip, Gpu.WhiteTex);
        if (Gpu.Begin(mv.Count, mi.Count, out int b))
        {
            ulong pc = Gpu.Pack(c.r, c.g, c.b, c.a);
            foreach (var v in mv) Gpu.VX(v.X, v.Y, 0.5f, 0.5f, pc);
            foreach (var k in mi) Gpu.I(b + k);
            return;
        }
        for (int i = 0; i + 2 < mi.Count; i += 3)
        {
            Gpu.Need(3);
            for (int q = 0; q < 3; q++) { var v = mv[mi[i + q]]; Gpu.V(v.X, v.Y, 0.5f, 0.5f, c.r, c.g, c.b, c.a); }
        }
    }
    void Shadow(Mesh t)
    {
        if (s.shCol.a <= 0 || (s.shBlur <= 0 && s.shX == 0 && s.shY == 0) || t.i.Count == 0) return;
        GfxStats.Shadows++;
        /* a blur, roughly: the shape again in soft rings around the offset */
        double blur = s.shBlur, ox = s.shX, oy = s.shY;
        var copy = shMesh;
        int rings = blur > 0 ? 3 : 0, dirs = 8;
        double total = 1 + rings * dirs;
        var col = s.shCol;
        void Pass(double dx, double dy, double w)
        {
            copy.Clear(); foreach (var v in t.v) copy.v.Add(new Vector2((float)(v.X + dx), (float)(v.Y + dy))); copy.i.AddRange(t.i);
            Emit(copy, col.WithAlpha(col.a), w);
        }
        Pass(ox, oy, 1.0 / total * 2.2);
        for (int r = 1; r <= rings; r++)
            for (int k = 0; k < dirs; k++)
            {
                double a = k * Math.PI * 2 / dirs + r * 0.4, rr = blur * 0.5 * r / rings;
                Pass(ox + Math.Cos(a) * rr, oy + Math.Sin(a) * rr, 1.0 / total * 1.6);
            }
    }
    public void fill(string rule = "nonzero")
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0, __g0 = Gpu.GlTicks;
        try {
        GfxStats.Fills++; if (s.fill.grad != null) GfxStats.GradFills++;
        bool opaque = s.fill.grad == null && s.fill.col.a * s.alpha >= 0.999 && s.op == BlendOp.SourceOver && Filter == null;
        TessFill(rule == "evenodd", subs, mesh, opaque);
        Shadow(mesh);
        Emit(mesh, s.fill);
            } finally { if (GfxStats.On) GfxStats.TFill += System.Diagnostics.Stopwatch.GetTimestamp() - __t0 - (Gpu.GlTicks - __g0); }
    }
    public void fill(Path2D p, string rule = "nonzero")
    {
        var keep = Aside(); var saveCur = cur; double sx = ux, sy = uy; bool sh = hasPt;
        cur = null; hasPt = false; p.Replay(this);
        fill(rule);
        Back(keep); cur = saveCur; ux = sx; uy = sy; hasPt = sh;
    }
    public void stroke()
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0, __g0 = Gpu.GlTicks;
        try {
        GfxStats.Strokes++;
        BuildStroke(subs, mesh);
        Shadow(mesh);
        Emit(mesh, s.stroke, strokeAlphaK);
            } finally { if (GfxStats.On) GfxStats.TStroke += System.Diagnostics.Stopwatch.GetTimestamp() - __t0 - (Gpu.GlTicks - __g0); }
    }
    public void stroke(Path2D p)
    {
        var keep = Aside(); var saveCur = cur; double sx = ux, sy = uy; bool sh = hasPt;
        cur = null; hasPt = false; p.Replay(this);
        stroke();
        Back(keep); cur = saveCur; ux = sx; uy = sy; hasPt = sh;
    }
    public void fillRect(double x, double y, double w, double h)
    {
        if (!Js.IsFinite(x + y + w + h)) return;
        mesh.Clear();
        mesh.Quad(X(x, y), X(x + w, y), X(x + w, y + h), X(x, y + h));
        Shadow(mesh);
        Emit(mesh, s.fill);
    }
    public void strokeRect(double x, double y, double w, double h)
    {
        var keep = Aside(); var kc = cur;
        cur = null;
        rect(x, y, w, h);
        stroke();
        Back(keep); cur = kc;
    }
    public void clearRect(double x, double y, double w, double h)
    {
        var op = s.op; var al = s.alpha;
        s.op = BlendOp.DestinationOut; s.alpha = 1;
        mesh.Clear();
        mesh.Quad(X(x, y), X(x + w, y), X(x + w, y + h), X(x, y + h));
        Emit(mesh, new Col(0, 0, 0, 1));
        s.op = op; s.alpha = al;
    }
    /* clip: the path is drawn into a mask the size of the target; later draws
       are multiplied by it (an intersection with any clip already in force) */
    public void clip(string rule = "nonzero")
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0, __g0 = Gpu.GlTicks;
        try {
        GfxStats.Clips++;
        var tg = Tgt;
        var mask = MaskPool.Get(tg.w, tg.h);
        TessFill(rule == "evenodd", subs, mesh, true);
        Gpu.Use(mask, BlendOp.SourceOver, s.clip, Gpu.WhiteTex);
        if (Gpu.Begin(mesh.v.Count, mesh.i.Count, out int mb))
        {
            ulong w1 = Gpu.Pack(1, 1, 1, 1);
            foreach (var v in mesh.v) Gpu.VX(v.X, v.Y, 0.5f, 0.5f, w1);
            foreach (var k in mesh.i) Gpu.I(mb + k);
        }
        else
            for (int i = 0; i + 2 < mesh.i.Count; i += 3)
            {
                Gpu.Need(3);
                for (int k = 0; k < 3; k++) { var v = mesh.v[mesh.i[i + k]]; Gpu.V(v.X, v.Y, 0.5f, 0.5f, 1, 1, 1, 1); }
            }
        Gpu.Flush();
        s.clip = mask;
            } finally { if (GfxStats.On) GfxStats.TClip += System.Diagnostics.Stopwatch.GetTimestamp() - __t0 - (Gpu.GlTicks - __g0); }
    }
    public void clip(Path2D p, string rule = "nonzero")
    {
        var keep = Aside(); var saveCur = cur;
        cur = null; hasPt = false; p.Replay(this);
        clip(rule);
        Back(keep); cur = saveCur;
    }

    /* ================= strokes ================= */
    readonly List<Vector2> pl = new();
    void BuildStroke(List<Sub> list, Mesh outTri)
    {
        outTri.Clear();
        double sc = Scale;
        double w = s.lw * sc;
        double alphaK = 1;
        if (w < 1) { alphaK = Math.Max(w, 0.05); w = 1; }
        strokeAlphaK = alphaK;
        float hw = (float)(w / 2);
        foreach (var sb in list)
        {
            if (sb.p.Count == 0) continue;
            pl.Clear();
            foreach (var v in sb.p) if (pl.Count == 0 || Vector2.DistanceSquared(pl[^1], v) > 1e-6f) pl.Add(v);
            bool closed = sb.closed;
            if (closed && pl.Count > 1 && Vector2.DistanceSquared(pl[0], pl[^1]) < 1e-6f) pl.RemoveAt(pl.Count - 1);
            if (pl.Count == 1)
            {
                /* a dot: only round and square caps draw anything */
                if (s.cap == Cap.Round) Disc(pl[0], hw, outTri);
                else if (s.cap == Cap.Square) { var p = pl[0]; outTri.Quad(p + new Vector2(-hw, -hw), p + new Vector2(hw, -hw), p + new Vector2(hw, hw), p + new Vector2(-hw, hw)); }
                continue;
            }
            if (s.dash != null) { Dashed(pl, closed, hw, sc, outTri); continue; }
            Poly(pl, closed, hw, outTri);
        }
    }
    double strokeAlphaK = 1;
    void Dashed(List<Vector2> pts, bool closed, float hw, double sc, Mesh outTri)
    {
        var all = new List<Vector2>(pts); if (closed) all.Add(pts[0]);
        double[] dash = s.dash; int di = 0; double pos = (s.dashOff * sc) % dash.Sum(x => x * sc); if (pos < 0) pos += dash.Sum(x => x * sc);
        while (pos > dash[di] * sc) { pos -= dash[di] * sc; di = (di + 1) % dash.Length; }
        double left = dash[di] * sc - pos; bool on = di % 2 == 0;
        var piece = new List<Vector2>();
        if (on) piece.Add(all[0]);
        for (int i = 0; i < all.Count - 1; i++)
        {
            var a = all[i]; var b = all[i + 1];
            double seg = Vector2.Distance(a, b), t0 = 0;
            while (seg - t0 > left)
            {
                t0 += left;
                var p = Vector2.Lerp(a, b, (float)(t0 / seg));
                if (on) { piece.Add(p); if (piece.Count > 1) Poly(piece, false, hw, outTri); piece.Clear(); }
                else { piece.Clear(); piece.Add(p); }
                on = !on; di = (di + 1) % dash.Length; left = dash[di] * sc;
                if (left <= 0) left = 1e-3;
            }
            left -= seg - t0;
            if (on) piece.Add(b);
        }
        if (on && piece.Count > 1) Poly(piece, false, hw, outTri);
    }
    static void Disc(Vector2 c, float r, Mesh o)
    {
        int n = Math.Clamp((int)(r * 1.2f) + 8, 8, 48);
        int k0 = o.P(c), k1 = o.v.Count;
        for (int i = 0; i < n; i++) { double a0 = i * Math.PI * 2 / n; o.v.Add(c + new Vector2((float)Math.Cos(a0) * r, (float)Math.Sin(a0) * r)); }
        for (int i = 0; i < n; i++) o.T(k0, k1 + i, k1 + (i + 1) % n);
    }
    static void Fan(Vector2 c, Vector2 from, Vector2 to, float r, bool cw, Mesh o)
    {
        /* the wedge between two radii of length r: one atan2 for its angle, then the
           steps by rotation; a wedge too small to need a second step is one triangle */
        var u0 = from - c; var u1 = to - c;
        double rr = (double)r * r; if (rr < 1e-12) return;
        double dot = (u0.X * (double)u1.X + u0.Y * (double)u1.Y) / rr, crs = (u0.X * (double)u1.Y - u0.Y * (double)u1.X) / rr;
        if (dot > 0 && Math.Abs(crs) * 1.5708 * r < 2) { o.Tri(c, from, to); return; }
        double d = Math.Atan2(crs, dot);
        int n = Math.Clamp((int)(Math.Abs(d) * r / 2) + 1, 1, 32);
        if (n == 1) { o.Tri(c, from, to); return; }
        double cs = Math.Cos(d / n), sn = Math.Sin(d / n), vx = u0.X, vy = u0.Y;
        int kc = o.P(c), kp = o.P(from);
        for (int i = 1; i <= n; i++)
        {
            double nx = vx * cs - vy * sn, ny = vx * sn + vy * cs; vx = nx; vy = ny;
            int kn = o.P(i == n ? to : c + new Vector2((float)vx, (float)vy));
            o.T(kc, kp, kn); kp = kn;
        }
    }
    /* A GENTLE OUTLINE AS ONE STRIP: when every corner turns less than about 45
       degrees (a circle, an ellipse, a rounded box), the stroke's two edges are the
       offset lines meeting at their miter points, and the corners each quad and its
       joins shared become one pair of strip points per vertex. Against a round join
       the outer edge is off by under a tenth of the line's width; nothing in the
       strip overlaps, so a translucent outline no longer darkens at every joint. */
    bool Strip(List<Vector2> p, bool closed, float hw, Mesh o)
    {
        int n = p.Count, segs = closed ? n : n - 1;
        if (n < 3 || segs < 2) return false;
        for (int i = 0; i < segs; i++)
        {
            var d = p[i + 1 < n ? i + 1 : 0] - p[i]; float l = d.Length();
            if (l < 1e-4f) return false;
            dirBuf[i] = d / l;
        }
        int j0 = closed ? 0 : 1, j1 = closed ? n : n - 1;
        for (int i = j0; i < j1; i++)
        {
            var d0 = dirBuf[i - 1 < 0 ? segs - 1 : i - 1]; var d1 = dirBuf[i < segs ? i : 0];
            if (d0.X * d1.X + d0.Y * d1.Y < 0.71f) return false;
        }
        int b = o.v.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 c = p[i], off;
            if (!closed && i == 0)
            {
                var d = dirBuf[0]; off = new Vector2(-d.Y, d.X) * hw;
                if (s.cap == Cap.Square) c -= d * hw;
            }
            else if (!closed && i == n - 1)
            {
                var d = dirBuf[segs - 1]; off = new Vector2(-d.Y, d.X) * hw;
                if (s.cap == Cap.Square) c += d * hw;
            }
            else
            {
                var d0 = dirBuf[i - 1 < 0 ? segs - 1 : i - 1]; var d1 = dirBuf[i < segs ? i : 0];
                var n0 = new Vector2(-d0.Y, d0.X); var n1 = new Vector2(-d1.Y, d1.X);
                var m = n0 + n1; float ml = m.Length();
                if (ml < 1e-4f) return false;
                m /= ml;
                float cosHalf = m.X * n1.X + m.Y * n1.Y;
                off = m * (hw / Math.Max(cosHalf, 0.5f));
            }
            o.v.Add(c + off); o.v.Add(c - off);
        }
        for (int i = 0; i < segs; i++)
        {
            int a = b + 2 * i, q = b + 2 * (i + 1 < n ? i + 1 : 0);
            o.T(a, q, q + 1); o.T(a, q + 1, a + 1);
        }
        if (!closed && s.cap == Cap.Round)
            for (int e = 0; e < 2; e++)
            {
                var c = e == 0 ? p[0] : p[n - 1]; var d = e == 0 ? -dirBuf[0] : dirBuf[segs - 1];
                var nn = new Vector2(-d.Y, d.X) * hw;
                Fan(c, c + nn, c + d * hw, hw, false, o);
                Fan(c, c + d * hw, c - nn, hw, false, o);
            }
        return true;
    }
    /* each segment's unit direction, worked out once for its quad and both joins */
    Vector2[] dirBuf = new Vector2[64]; bool[] dirOk = new bool[64];
    void Poly(List<Vector2> p, bool closed, float hw, Mesh o)
    {
        int n = p.Count;
        int segs = closed ? n : n - 1;
        if (segs < 1) return;
        if (dirBuf.Length < segs) { dirBuf = new Vector2[segs * 2]; dirOk = new bool[segs * 2]; }
        if (Strip(p, closed, hw, o)) return;
        /* each segment as a quad */
        for (int i = 0; i < segs; i++)
        {
            var a = p[i]; var b = p[i + 1 < n ? i + 1 : 0];
            var d = b - a; float l = d.Length();
            if (l < 1e-6f) { dirOk[i] = false; continue; }
            dirOk[i] = true; var u = d / l; dirBuf[i] = u;
            var nn = new Vector2(-u.Y, u.X) * hw;
            var ea = a; var eb = b;
            if (!closed && s.cap == Cap.Square)
            {
                var ext = u * hw;
                if (i == 0) ea -= ext;
                if (i == segs - 1) eb += ext;
            }
            o.Quad(ea + nn, eb + nn, eb - nn, ea - nn);
        }
        /* joins: the wedge on the outer side of each corner (segment i-1 into segment i) */
        int j0 = closed ? 0 : 1, j1 = closed ? n : n - 1;
        for (int i = j0; i < j1; i++)
        {
            int sp = i - 1 < 0 ? segs - 1 : i - 1, sn = i;
            if (!dirOk[sp] || !dirOk[sn]) continue;
            var c = p[i];
            var d0 = dirBuf[sp]; var d1 = dirBuf[sn];
            float cross = d0.X * d1.Y - d0.Y * d1.X;
            if (Math.Abs(cross) < 1e-5f && Vector2.Dot(d0, d1) > 0) continue;
            var n0 = new Vector2(-d0.Y, d0.X) * hw; var n1 = new Vector2(-d1.Y, d1.X) * hw;
            /* outer side: the side the path turns away from */
            if (cross > 0) { n0 = -n0; n1 = -n1; }
            var o0 = c + n0; var o1 = c + n1;
            if (s.join == Join.Round) { Fan(c, o0, o1, hw, cross > 0, o); continue; }
            o.Tri(c, o0, o1);
            if (s.join == Join.Miter)
            {
                var mid = Vector2.Normalize(n0 + n1);
                float cosHalf = Vector2.Dot(mid, n0 / hw);
                if (cosHalf > 1e-4f)
                {
                    float ml = hw / cosHalf;
                    if (ml / hw <= s.miter) { var mp = c + mid * ml; o.Tri(o0, mp, o1); }
                }
            }
        }
        /* round caps */
        if (!closed && s.cap == Cap.Round)
        {
            for (int e = 0; e < 2; e++)
            {
                var c = e == 0 ? p[0] : p[n - 1]; var nb = e == 0 ? p[1] : p[n - 2];
                var d = c - nb; float l = d.Length(); if (l < 1e-6f) continue;
                d /= l;
                var nn = new Vector2(-d.Y, d.X) * hw;
                Fan(c, c + nn, c + d * hw, hw, false, o);
                Fan(c, c + d * hw, c - nn, hw, false, o);
            }
        }
    }

    /* ================= text ================= */
    public TextMetrics measureText(string t) => new TextMetrics { width = Measure(t, s.font, s.ls) };
    public static double Measure(string t, FontSpec f, double ls = 0)
    {
        if (string.IsNullOrEmpty(t)) return 0;
        const int REF = 64;
        double w = 0;
        foreach (var rune in t.EnumerateRunes())
        {
            w += ls;
            var face = Fonts.For(f.face, rune.Value);
            double a = face.Adv(rune.Value);
            if (a >= 0) { w += a * f.px / face.upm; continue; }
            var at = Fonts.Get(face, REF);
            if (!at.idx.TryGetValue(rune.Value, out int gi)) { if (!at.idx.TryGetValue('?', out gi)) continue; }
            unsafe
            {
                var g = at.font.Glyphs[gi];
                float adv = g.AdvanceX != 0 ? g.AdvanceX : at.font.Recs[gi].Width;
                w += adv * f.px / REF;
            }
        }
        return w;
    }
    public void fillText(string t, double x, double y, double maxWidth = 0) => DrawText(t, x, y, maxWidth, false);
    public void strokeText(string t, double x, double y, double maxWidth = 0) => DrawText(t, x, y, maxWidth, true);
    unsafe void DrawText(string t, double x, double y, double maxWidth, bool stroke)
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0, __g0 = Gpu.GlTicks;
        try {
        GfxStats.Texts++; GfxStats.Glyphs += t?.Length ?? 0;
        if (string.IsNullOrEmpty(t) || !Js.IsFinite(x) || !Js.IsFinite(y)) return;
        var f = s.font;
        double sc = Scale;
        if (sc <= 0) return;
        int dsize = (int)Math.Round(f.px * sc);
        if (dsize < 2) return;
        double width = Measure(t, f, s.ls);
        double sx = 1;
        if (maxWidth > 0 && width > maxWidth) { sx = maxWidth / width; width = maxWidth; }
        double x0 = s.align == 1 ? x - width / 2 : s.align == 2 ? x - width : x;
        double A = f.face.Ascent((float)f.px), D = f.face.Descent((float)f.px);
        double baseY = s.baseline switch { 1 => y + (A - D) / 2, 2 => y + A, 3 => y - D, 4 => y + A * 0.8, _ => y };
        Paint paint = stroke ? s.stroke : s.fill;
        Col col = paint.grad != null ? (paint.grad.stops.Count > 0 ? paint.grad.stops[^1].c : new Col(0, 0, 0, 1)) : paint.col;
        double alpha = s.alpha;
        /* an outline, the way a stroke round the letters looks: the glyphs again
           in a ring of offsets, lineWidth wide */
        List<(double dx, double dy)> offs = new() { (0, 0) };
        if (stroke)
        {
            offs.Clear();
            double r = s.lw / 2;
            int k = r < 1.2 ? 8 : 12;
            for (int i = 0; i < k; i++) offs.Add((Math.Cos(i * Math.PI * 2 / k) * r, Math.Sin(i * Math.PI * 2 / k) * r));
            if (r > 2) for (int i = 0; i < 8; i++) offs.Add((Math.Cos(i * Math.PI / 4 + 0.4) * r * 0.55, Math.Sin(i * Math.PI / 4 + 0.4) * r * 0.55));
            /* the passes overlap; keep the outline about as dense as one stroke would be */
            alpha = 1 - Math.Pow(1 - Math.Min(0.999, col.a * s.alpha), 1.0 / 3.0);
            col = col.WithAlpha(1);
        }
        var pm = Pm(col, alpha);
        ulong pmc = Gpu.Pack(pm.r, pm.g, pm.b, pm.a);
        foreach (var (odx, ody) in offs)
        {
            double pen = 0;
            foreach (var rune in t.EnumerateRunes())
            {
                var face = Fonts.For(f.face, rune.Value);
                var at = Fonts.Get(face, dsize);
                if (!at.idx.TryGetValue(rune.Value, out int gi)) { if (!at.idx.TryGetValue('?', out gi)) continue; }
                var g = at.font.Glyphs[gi]; var rc = at.font.Recs[gi];
                float adv = g.AdvanceX != 0 ? g.AdvanceX : rc.Width;
                double k = 1 / sc;           /* device px -> user units */
                if (rc.Width > 0 && rc.Height > 0)
                {
                    double gx = x0 + (pen + g.OffsetX * k) * sx + odx;
                    double gy = baseY - (at.asc - g.OffsetY) * k + ody;
                    double gw = rc.Width * k * sx, gh = rc.Height * k;
                    var p0 = X(gx, gy); var p1 = X(gx + gw, gy); var p2 = X(gx + gw, gy + gh); var p3 = X(gx, gy + gh);
                    float tw = at.font.Texture.Width, th = at.font.Texture.Height;
                    float u0 = rc.X / tw, v0 = rc.Y / th, u1 = (rc.X + rc.Width) / tw, v1 = (rc.Y + rc.Height) / th;
                    Gpu.Use(Tgt, s.op, Clip, at.tex);
                    Gpu.Begin(4, 6, out int gb);
                    Gpu.VX(p0.X, p0.Y, u0, v0, pmc); Gpu.VX(p1.X, p1.Y, u1, v0, pmc); Gpu.VX(p2.X, p2.Y, u1, v1, pmc); Gpu.VX(p3.X, p3.Y, u0, v1, pmc);
                    Gpu.I(gb); Gpu.I(gb + 1); Gpu.I(gb + 2); Gpu.I(gb); Gpu.I(gb + 2); Gpu.I(gb + 3);
                }
                /* advance at the reference size so measureText and drawing agree */
                pen += s.ls;
                double ua = face.Adv(rune.Value);
                if (ua >= 0) { pen += ua * f.px / face.upm; continue; }
                var atR = Fonts.Get(face, 64);
                if (atR.idx.TryGetValue(rune.Value, out int gr)) { var g2 = atR.font.Glyphs[gr]; pen += (g2.AdvanceX != 0 ? g2.AdvanceX : atR.font.Recs[gr].Width) * f.px / 64; }
                else pen += adv / sc;
            }
        }
            } finally { if (GfxStats.On) GfxStats.TText += System.Diagnostics.Stopwatch.GetTimestamp() - __t0 - (Gpu.GlTicks - __g0); }
    }

    /* ================= images ================= */
    public void drawImage(OffCanvas img, double dx, double dy) => drawImage(img, 0, 0, img.width, img.height, dx, dy, img.width, img.height);
    public void drawImage(OffCanvas img, double dx, double dy, double dw, double dh) => drawImage(img, 0, 0, img.width, img.height, dx, dy, dw, dh);
    public void drawImage(OffCanvas img, double sx, double sy, double sw, double sh, double dx, double dy, double dw, double dh)
    {
        long __t0 = GfxStats.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0, __g0 = Gpu.GlTicks;
        try {
        if (img == null) return;
        GfxStats.Images++;
        var T = img.T;
        if (T == Tgt) return;
        Gpu.ReadingFrom(T);
        var p0 = X(dx, dy); var p1 = X(dx + dw, dy); var p2 = X(dx + dw, dy + dh); var p3 = X(dx, dy + dh);
        /* a canvas's texture is stored bottom row first */
        float u0 = (float)(sx / T.w), u1 = (float)((sx + sw) / T.w);
        float v0 = 1 - (float)(sy / T.h), v1 = 1 - (float)((sy + sh) / T.h);
        float a = (float)s.alpha;
        Gpu.Use(Tgt, s.op, Clip, T.tex);
        Gpu.Filter(T, s.smooth);
        Gpu.Begin(4, 6, out int ib0);
        ulong ac = Gpu.Pack(a, a, a, a);
        Gpu.VX(p0.X, p0.Y, u0, v0, ac); Gpu.VX(p1.X, p1.Y, u1, v0, ac); Gpu.VX(p2.X, p2.Y, u1, v1, ac); Gpu.VX(p3.X, p3.Y, u0, v1, ac);
        Gpu.I(ib0); Gpu.I(ib0 + 1); Gpu.I(ib0 + 2); Gpu.I(ib0); Gpu.I(ib0 + 2); Gpu.I(ib0 + 3);
            } finally { if (GfxStats.On) GfxStats.TImage += System.Diagnostics.Stopwatch.GetTimestamp() - __t0 - (Gpu.GlTicks - __g0); }
    }

    /* ================= gradients ================= */
    public Grad createLinearGradient(double x0, double y0, double x1, double y1) => new Grad { x0 = x0, y0 = y0, x1 = x1, y1 = y1 };
    public Grad createRadialGradient(double x0, double y0, double r0, double x1, double y1, double r1) => new Grad { radial = true, x0 = x0, y0 = y0, r0 = r0, x1 = x1, y1 = y1, r1 = r1 };
}

/* THE MASKS clip() DRAWS INTO are kept and used again. A mask lives for the rest
   of the frame it was made in; at the end of the frame it goes back to the pool,
   and the next clip of the same size clears it rather than asking the driver for
   a new texture -- which costs far more than drawing the clip itself. Masks no
   clip has wanted for a few seconds are given back. */
public static class MaskPool
{
    static readonly List<Target> live = new();
    static readonly List<(Target t, int frame)> free = new();
    static int frame;
    public static Target Get(int w, int h)
    {
        for (int i = free.Count - 1; i >= 0; i--)
        {
            var t = free[i].t;
            if (t.w != w || t.h != h || t.tex == 0) continue;
            free.RemoveAt(i);
            Gpu.Clear(t);
            live.Add(t);
            return t;
        }
        var n = Gpu.NewTarget(w, h);
        live.Add(n);
        return n;
    }
    public static void EndFrame()
    {
        frame++;
        foreach (var t in live) free.Add((t, frame));
        live.Clear();
        for (int i = free.Count - 1; i >= 0; i--)
            if (frame - free[i].frame > 240 || free.Count > 24) { Gpu.FreeTarget(free[i].t); free.RemoveAt(i); }
    }
}

/* new Path2D(): a list of path commands, replayed into a context */
public sealed class Path2D
{
    readonly List<Action<Ctx>> ops = new();
    public Path2D() { }
    /* new Path2D(svgPathData): curves and arcs arrive already flattened */
    public Path2D(string d)
    {
        foreach (var (pts, closed) in Svg.Polylines(d))
        {
            if (pts.Count == 0) continue;
            moveTo(pts[0].x, pts[0].y);
            for (int i = 1; i < pts.Count; i++) lineTo(pts[i].x, pts[i].y);
            if (closed) closePath();
        }
    }
    public void moveTo(double x, double y) => ops.Add(c => c.moveTo(x, y));
    public void lineTo(double x, double y) => ops.Add(c => c.lineTo(x, y));
    public void closePath() => ops.Add(c => c.closePath());
    public void rect(double x, double y, double w, double h) => ops.Add(c => c.rect(x, y, w, h));
    public void arc(double x, double y, double r, double a0, double a1, bool ccw = false) => ops.Add(c => c.arc(x, y, r, a0, a1, ccw));
    public void ellipse(double x, double y, double rx, double ry, double rot, double a0, double a1, bool ccw = false) => ops.Add(c => c.ellipse(x, y, rx, ry, rot, a0, a1, ccw));
    public void quadraticCurveTo(double cx, double cy, double x, double y) => ops.Add(c => c.quadraticCurveTo(cx, cy, x, y));
    public void addPath(Path2D p) => ops.AddRange(p.ops);
    public void Replay(Ctx c) { foreach (var o in ops) o(c); }
}
