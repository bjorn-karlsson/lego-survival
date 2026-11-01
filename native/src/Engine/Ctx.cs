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
        public FontSpec font; public int align, baseline;
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
    readonly List<Sub> subs = new();
    Sub cur;
    double ux, uy;            /* the current point, user space */
    bool hasPt;
    public void beginPath() { subs.Clear(); cur = null; hasPt = false; }
    void NewSub(double x, double y)
    {
        cur = new Sub(); subs.Add(cur);
        cur.p.Add(X(x, y)); ux = x; uy = y; hasPt = true;
    }
    public void moveTo(double x, double y) { if (!Js.IsFinite(x) || !Js.IsFinite(y)) return; NewSub(x, y); }
    public void lineTo(double x, double y)
    {
        if (!Js.IsFinite(x) || !Js.IsFinite(y)) return;
        if (cur == null || cur.closed) { if (cur != null && cur.closed) { var st = cur.p[0]; cur = new Sub(); subs.Add(cur); cur.p.Add(st); } else { NewSub(x, y); return; } }
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
        if (cur != null && cur.closed) { var st = cur.p[0]; cur = new Sub(); subs.Add(cur); cur.p.Add(st); }
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
        if (!Js.IsFinite(x) || !Js.IsFinite(y) || !Js.IsFinite(r) || !Js.IsFinite(a0) || !Js.IsFinite(a1)) return;
        r = Math.Abs(r);
        double sw = Sweep(a0, a1, ccw);
        double sx = x + Math.Cos(a0) * r, sy = y + Math.Sin(a0) * r;
        if (cur == null) NewSub(sx, sy); else { EnsureOpen(); cur.p.Add(X(sx, sy)); }
        int n = ArcSegs(r, sw);
        for (int i = 1; i <= n; i++)
        {
            double t = a0 + sw * i / n;
            cur.p.Add(X(x + Math.Cos(t) * r, y + Math.Sin(t) * r));
        }
        ux = x + Math.Cos(a0 + sw) * r; uy = y + Math.Sin(a0 + sw) * r; hasPt = true;
    }
    public void ellipse(double x, double y, double rx, double ry, double rot, double a0, double a1, bool ccw = false)
    {
        if (!Js.IsFinite(x) || !Js.IsFinite(y) || !Js.IsFinite(rx) || !Js.IsFinite(ry)) return;
        rx = Math.Abs(rx); ry = Math.Abs(ry);
        double sw = Sweep(a0, a1, ccw);
        double cr = Math.Cos(rot), sr = Math.Sin(rot);
        (double, double) P(double t) { double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t); return (x + cr * ex - sr * ey, y + sr * ex + cr * ey); }
        var (sx, sy) = P(a0);
        if (cur == null) NewSub(sx, sy); else { EnsureOpen(); cur.p.Add(X(sx, sy)); }
        int n = ArcSegs(Math.Max(rx, ry), sw);
        for (int i = 1; i <= n; i++) { var (px, py) = P(a0 + sw * i / n); cur.p.Add(X(px, py)); }
        (ux, uy) = P(a0 + sw); hasPt = true;
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
    static Col Pm(Col c, double alpha) { float a = (float)(c.a * alpha); return new Col(c.r / 255f * a, c.g / 255f * a, c.b / 255f * a, a); }

    /* the triangles of a fill, in device space */
    readonly List<Vector2> tri = new();
    void TessFill(bool evenodd, List<Sub> list, List<Vector2> outTri)
    {
        outTri.Clear();
        int live = 0; Sub only = null;
        foreach (var sb in list) if (sb.p.Count >= 3) { live++; only = sb; }
        if (live == 0) return;
        if (live == 1 && Convex(only.p))
        {
            var p = only.p; int n = p.Count;
            if (n > 2 && p[n - 1] == p[0]) n--;
            for (int i = 1; i < n - 1; i++) { outTri.Add(p[0]); outTri.Add(p[i]); outTri.Add(p[i + 1]); }
            return;
        }
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
        for (int i = 0; i < tess.ElementCount * 3; i++)
        {
            int k = tess.Elements[i]; if (k < 0) continue;
            var v = tess.Vertices[k].Position; outTri.Add(new Vector2(v.X, v.Y));
        }
        if (outTri.Count % 3 != 0) outTri.RemoveRange(outTri.Count - outTri.Count % 3, outTri.Count % 3);
    }
    static bool Convex(List<Vector2> p)
    {
        int n = p.Count;
        if (n > 2 && p[n - 1] == p[0]) n--;
        if (n < 3) return false;
        int sign = 0; double turn = 0;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n]; var c = p[(i + 2) % n];
            float cx = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cx) < 1e-4f) continue;
            int sg = cx > 0 ? 1 : -1;
            if (sign == 0) sign = sg; else if (sg != sign) return false;
            double a1 = Math.Atan2(b.Y - a.Y, b.X - a.X), a2 = Math.Atan2(c.Y - b.Y, c.X - b.X);
            double d = a2 - a1; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
            turn += d;
        }
        return Math.Abs(turn) < 2 * Math.PI + 0.5;
    }
    /* send triangles with a paint */
    void Emit(List<Vector2> t, Paint paint, double extraAlpha = 1)
    {
        if (t.Count == 0) return;
        var tg = Tgt;
        if (paint.grad != null)
        {
            if (!Inv(out var ia, out var ib, out var ic, out var id, out var ie, out var iff)) return;
            Gpu.UseGrad(tg, s.op, Clip, paint.grad);
            float ga = (float)(s.alpha * extraAlpha);
            for (int i = 0; i < t.Count; i += 3)
            {
                Gpu.Need(3);
                for (int k = 0; k < 3; k++)
                {
                    var v = t[i + k];
                    float uxu = (float)(ia * v.X + ic * v.Y + ie), uyu = (float)(ib * v.X + id * v.Y + iff);
                    Gpu.V(v.X, v.Y, uxu, uyu, 0, 0, 0, ga);
                }
            }
            return;
        }
        var c = Pm(paint.col, s.alpha * extraAlpha);
        if (c.a <= 0 && s.op == BlendOp.SourceOver) return;
        Gpu.Use(tg, s.op, Clip, Gpu.WhiteTex);
        for (int i = 0; i < t.Count; i += 3)
        {
            Gpu.Need(3);
            for (int k = 0; k < 3; k++) { var v = t[i + k]; Gpu.V(v.X, v.Y, 0.5f, 0.5f, c.r, c.g, c.b, c.a); }
        }
    }
    void Shadow(List<Vector2> t)
    {
        if (s.shCol.a <= 0 || (s.shBlur <= 0 && s.shX == 0 && s.shY == 0) || t.Count == 0) return;
        /* a blur, roughly: the shape again in soft rings around the offset */
        double blur = s.shBlur, ox = s.shX, oy = s.shY;
        var copy = new List<Vector2>(t.Count);
        int rings = blur > 0 ? 3 : 0, dirs = 8;
        double total = 1 + rings * dirs;
        var col = s.shCol;
        void Pass(double dx, double dy, double w)
        {
            copy.Clear(); foreach (var v in t) copy.Add(new Vector2((float)(v.X + dx), (float)(v.Y + dy)));
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
        TessFill(rule == "evenodd", subs, tri);
        Shadow(tri);
        Emit(tri, s.fill);
    }
    public void fill(Path2D p, string rule = "nonzero")
    {
        var saveSubs = new List<Sub>(subs); var saveCur = cur; double sx = ux, sy = uy; bool sh = hasPt;
        beginPath(); p.Replay(this);
        fill(rule);
        subs.Clear(); subs.AddRange(saveSubs); cur = saveCur; ux = sx; uy = sy; hasPt = sh;
    }
    public void stroke()
    {
        BuildStroke(subs, tri);
        Shadow(tri);
        Emit(tri, s.stroke, strokeAlphaK);
    }
    public void stroke(Path2D p)
    {
        var saveSubs = new List<Sub>(subs); var saveCur = cur; double sx = ux, sy = uy; bool sh = hasPt;
        beginPath(); p.Replay(this);
        stroke();
        subs.Clear(); subs.AddRange(saveSubs); cur = saveCur; ux = sx; uy = sy; hasPt = sh;
    }
    public void fillRect(double x, double y, double w, double h)
    {
        if (!Js.IsFinite(x + y + w + h)) return;
        tri.Clear();
        var a = X(x, y); var b = X(x + w, y); var c = X(x + w, y + h); var d = X(x, y + h);
        tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d);
        Shadow(tri);
        Emit(tri, s.fill);
    }
    public void strokeRect(double x, double y, double w, double h)
    {
        var keep = new List<Sub>(subs); var kc = cur;
        subs.Clear(); cur = null;
        rect(x, y, w, h);
        stroke();
        subs.Clear(); subs.AddRange(keep); cur = kc;
    }
    public void clearRect(double x, double y, double w, double h)
    {
        var op = s.op; var al = s.alpha;
        s.op = BlendOp.DestinationOut; s.alpha = 1;
        tri.Clear();
        var a = X(x, y); var b = X(x + w, y); var c = X(x + w, y + h); var d = X(x, y + h);
        tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d);
        Emit(tri, new Col(0, 0, 0, 1));
        s.op = op; s.alpha = al;
    }
    /* clip: the path is drawn into a mask the size of the target; later draws
       are multiplied by it (an intersection with any clip already in force) */
    public void clip(string rule = "nonzero")
    {
        var tg = Tgt;
        var mask = Gpu.NewTarget(tg.w, tg.h);
        TessFill(rule == "evenodd", subs, tri);
        Gpu.Use(mask, BlendOp.SourceOver, s.clip, Gpu.WhiteTex);
        foreach (var v in tri) { }
        for (int i = 0; i < tri.Count; i += 3)
        {
            Gpu.Need(3);
            for (int k = 0; k < 3; k++) { var v = tri[i + k]; Gpu.V(v.X, v.Y, 0.5f, 0.5f, 1, 1, 1, 1); }
        }
        Gpu.Flush();
        MaskPool.Track(mask);
        s.clip = mask;
    }
    public void clip(Path2D p, string rule = "nonzero")
    {
        var saveSubs = new List<Sub>(subs); var saveCur = cur;
        beginPath(); p.Replay(this);
        clip(rule);
        subs.Clear(); subs.AddRange(saveSubs); cur = saveCur;
    }

    /* ================= strokes ================= */
    readonly List<Vector2> pl = new();
    void BuildStroke(List<Sub> list, List<Vector2> outTri)
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
                else if (s.cap == Cap.Square) { var p = pl[0]; Quad(p + new Vector2(-hw, -hw), p + new Vector2(hw, -hw), p + new Vector2(hw, hw), p + new Vector2(-hw, hw), outTri); }
                continue;
            }
            if (s.dash != null) { Dashed(pl, closed, hw, sc, outTri); continue; }
            Poly(pl, closed, hw, outTri);
        }
    }
    double strokeAlphaK = 1;
    void Dashed(List<Vector2> pts, bool closed, float hw, double sc, List<Vector2> outTri)
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
    static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, List<Vector2> o) { o.Add(a); o.Add(b); o.Add(c); o.Add(a); o.Add(c); o.Add(d); }
    static void Disc(Vector2 c, float r, List<Vector2> o)
    {
        int n = Math.Clamp((int)(r * 1.2f) + 8, 8, 48);
        for (int i = 0; i < n; i++)
        {
            double a0 = i * Math.PI * 2 / n, a1 = (i + 1) * Math.PI * 2 / n;
            o.Add(c); o.Add(c + new Vector2((float)Math.Cos(a0) * r, (float)Math.Sin(a0) * r)); o.Add(c + new Vector2((float)Math.Cos(a1) * r, (float)Math.Sin(a1) * r));
        }
    }
    static void Fan(Vector2 c, Vector2 from, Vector2 to, float r, bool cw, List<Vector2> o)
    {
        double a0 = Math.Atan2(from.Y - c.Y, from.X - c.X), a1 = Math.Atan2(to.Y - c.Y, to.X - c.X);
        double d = a1 - a0;
        while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
        int n = Math.Clamp((int)(Math.Abs(d) * r / 2) + 1, 1, 32);
        var prev = from;
        for (int i = 1; i <= n; i++)
        {
            double a = a0 + d * i / n;
            var p = c + new Vector2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
            o.Add(c); o.Add(prev); o.Add(p); prev = p;
        }
    }
    void Poly(List<Vector2> p, bool closed, float hw, List<Vector2> o)
    {
        int n = p.Count;
        int segs = closed ? n : n - 1;
        if (segs < 1) return;
        /* each segment as a quad */
        for (int i = 0; i < segs; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n];
            var d = b - a; float l = d.Length(); if (l < 1e-6f) continue;
            var nn = new Vector2(-d.Y / l, d.X / l) * hw;
            var ea = a; var eb = b;
            if (!closed && s.cap == Cap.Square)
            {
                var ext = d / l * hw;
                if (i == 0) ea -= ext;
                if (i == segs - 1) eb += ext;
            }
            Quad(ea + nn, eb + nn, eb - nn, ea - nn, o);
        }
        /* joins: the wedge on the outer side of each corner */
        int j0 = closed ? 0 : 1, j1 = closed ? n : n - 1;
        for (int i = j0; i < j1; i++)
        {
            var c = p[i]; var pa = p[(i - 1 + n) % n]; var pb = p[(i + 1) % n];
            var d0 = c - pa; var d1 = pb - c;
            float l0 = d0.Length(), l1 = d1.Length(); if (l0 < 1e-6f || l1 < 1e-6f) continue;
            d0 /= l0; d1 /= l1;
            float cross = d0.X * d1.Y - d0.Y * d1.X;
            if (Math.Abs(cross) < 1e-5f && Vector2.Dot(d0, d1) > 0) continue;
            var n0 = new Vector2(-d0.Y, d0.X) * hw; var n1 = new Vector2(-d1.Y, d1.X) * hw;
            /* outer side: the side the path turns away from */
            if (cross > 0) { n0 = -n0; n1 = -n1; }
            var o0 = c + n0; var o1 = c + n1;
            if (s.join == Join.Round) { Fan(c, o0, o1, hw, cross > 0, o); continue; }
            o.Add(c); o.Add(o0); o.Add(o1);
            if (s.join == Join.Miter)
            {
                var mid = Vector2.Normalize(n0 + n1);
                float cosHalf = Vector2.Dot(mid, Vector2.Normalize(n0));
                if (cosHalf > 1e-4f)
                {
                    float ml = hw / cosHalf;
                    if (ml / hw <= s.miter) { var mp = c + mid * ml; o.Add(o0); o.Add(mp); o.Add(o1); }
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
    public TextMetrics measureText(string t) => new TextMetrics { width = Measure(t, s.font) };
    public static double Measure(string t, FontSpec f)
    {
        if (string.IsNullOrEmpty(t)) return 0;
        const int REF = 64;
        double w = 0;
        foreach (var rune in t.EnumerateRunes())
        {
            var face = Fonts.For(f.face, rune.Value);
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
        if (string.IsNullOrEmpty(t) || !Js.IsFinite(x) || !Js.IsFinite(y)) return;
        var f = s.font;
        double sc = Scale;
        if (sc <= 0) return;
        int dsize = (int)Math.Round(f.px * sc);
        if (dsize < 2) return;
        double width = Measure(t, f);
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
                    Gpu.Need(6);
                    Gpu.V(p0.X, p0.Y, u0, v0, pm.r, pm.g, pm.b, pm.a); Gpu.V(p1.X, p1.Y, u1, v0, pm.r, pm.g, pm.b, pm.a); Gpu.V(p2.X, p2.Y, u1, v1, pm.r, pm.g, pm.b, pm.a);
                    Gpu.V(p0.X, p0.Y, u0, v0, pm.r, pm.g, pm.b, pm.a); Gpu.V(p2.X, p2.Y, u1, v1, pm.r, pm.g, pm.b, pm.a); Gpu.V(p3.X, p3.Y, u0, v1, pm.r, pm.g, pm.b, pm.a);
                }
                /* advance at the reference size so measureText and drawing agree */
                var atR = Fonts.Get(face, 64);
                if (atR.idx.TryGetValue(rune.Value, out int gr)) { var g2 = atR.font.Glyphs[gr]; pen += (g2.AdvanceX != 0 ? g2.AdvanceX : atR.font.Recs[gr].Width) * f.px / 64; }
                else pen += adv / sc;
            }
        }
    }

    /* ================= images ================= */
    public void drawImage(OffCanvas img, double dx, double dy) => drawImage(img, 0, 0, img.width, img.height, dx, dy, img.width, img.height);
    public void drawImage(OffCanvas img, double dx, double dy, double dw, double dh) => drawImage(img, 0, 0, img.width, img.height, dx, dy, dw, dh);
    public void drawImage(OffCanvas img, double sx, double sy, double sw, double sh, double dx, double dy, double dw, double dh)
    {
        if (img == null) return;
        var T = img.T;
        if (T == Tgt) return;
        Gpu.Flush();
        var p0 = X(dx, dy); var p1 = X(dx + dw, dy); var p2 = X(dx + dw, dy + dh); var p3 = X(dx, dy + dh);
        /* a canvas's texture is stored bottom row first */
        float u0 = (float)(sx / T.w), u1 = (float)((sx + sw) / T.w);
        float v0 = 1 - (float)(sy / T.h), v1 = 1 - (float)((sy + sh) / T.h);
        float a = (float)s.alpha;
        Gpu.Use(Tgt, s.op, Clip, T.tex);
        Raylib_cs.Rlgl.TextureParameters(T.tex, 0x2800, s.smooth ? 0x2601 : 0x2600);
        Raylib_cs.Rlgl.TextureParameters(T.tex, 0x2801, s.smooth ? 0x2601 : 0x2600);
        Gpu.Need(6);
        Gpu.V(p0.X, p0.Y, u0, v0, a, a, a, a); Gpu.V(p1.X, p1.Y, u1, v0, a, a, a, a); Gpu.V(p2.X, p2.Y, u1, v1, a, a, a, a);
        Gpu.V(p0.X, p0.Y, u0, v0, a, a, a, a); Gpu.V(p2.X, p2.Y, u1, v1, a, a, a, a); Gpu.V(p3.X, p3.Y, u0, v1, a, a, a, a);
        Gpu.Flush();
    }

    /* ================= gradients ================= */
    public Grad createLinearGradient(double x0, double y0, double x1, double y1) => new Grad { x0 = x0, y0 = y0, x1 = x1, y1 = y1 };
    public Grad createRadialGradient(double x0, double y0, double r0, double x1, double y1, double r1) => new Grad { radial = true, x0 = x0, y0 = y0, r0 = r0, x1 = x1, y1 = y1, r1 = r1 };
}

/* the masks clip() makes are freed at the end of the frame */
public static class MaskPool
{
    static readonly List<Target> live = new();
    public static void Track(Target t) => live.Add(t);
    public static void EndFrame() { foreach (var t in live) Gpu.FreeTarget(t); live.Clear(); }
}

/* new Path2D(): a list of path commands, replayed into a context */
public sealed class Path2D
{
    readonly List<Action<Ctx>> ops = new();
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
