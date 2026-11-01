// The native UI engine, part 6: painting and hit-testing. The box tree is walked
// in CSS paint order (stacking contexts, positioned layers by z-index) and drawn
// through the Ctx shim and straight into the GPU batcher: rounded rectangles,
// borders and soft box-shadows as triangle strips with per-vertex alpha,
// gradients, text, canvases, SVG, scrollbars and form controls. The same walk
// answers "what is under the pointer", so a click lands where it is drawn.
using System.Numerics;

namespace Brickblade;

public struct Mat
{
    public double a, b, c, d, e, f;
    public static readonly Mat I = new() { a = 1, d = 1 };
    public static Mat T(double x, double y) => new() { a = 1, d = 1, e = x, f = y };
    public static Mat S(double x, double y) => new() { a = x, d = y };
    public static Mat R(double r) { double c = Math.Cos(r), s = Math.Sin(r); return new() { a = c, b = s, c = -s, d = c }; }
    /* this * o: o applied first */
    public Mat Mul(Mat o) => new()
    {
        a = a * o.a + c * o.b, b = b * o.a + d * o.b, c = a * o.c + c * o.d, d = b * o.c + d * o.d,
        e = a * o.e + c * o.f + e, f = b * o.e + d * o.f + f,
    };
    public (double x, double y) Ap(double x, double y) => (a * x + c * y + e, b * x + d * y + f);
    public bool Inv(out Mat r)
    {
        double det = a * d - b * c;
        r = default;
        if (Math.Abs(det) < 1e-12) return false;
        r = new Mat { a = d / det, b = -b / det, c = -c / det, d = a / det, e = (c * f - d * e) / det, f = (b * e - a * f) / det };
        return true;
    }
    public double Scale => Math.Sqrt(Math.Abs(a * d - b * c));
    public bool Axis => Math.Abs(b) < 1e-9 && Math.Abs(c) < 1e-9;
}

internal static class Paint2
{
    /* ---- geometry helpers the layout uses ---- */
    public static double ScrollbarW(Box b)
    {
        if (b.el == null || b.isPseudo) return 0;
        var sb = PseudoStyle(b.el, "-webkit-scrollbar");
        if (sb != null && sb.TryGetValue("width", out var w)) { var L = Cascade.ParseLen(w, b.cs.fontSize, 16); if (L.IsVal) return L.R(0); }
        var sw = b.cs.spec?.GetValueOrDefault("scrollbar-width");
        if (sw == "none") return 0;
        if (sw == "thin") return 11;
        return 15;
    }
    /* declarations of the rules for a pseudo element (scrollbars, range thumbs, placeholders) that match el */
    static readonly Dictionary<(Element, string, int), Dictionary<string, string>> pseudoCache = new();
    static int pseudoGen = -1;
    public static Dictionary<string, string> PseudoStyle(Element el, string pseudo)
    {
        if (pseudoGen != Dom.StyleGen) { pseudoCache.Clear(); pseudoGen = Dom.StyleGen; }
        int hov = Dom.InHover(el) ? 1 : 0;
        if (pseudoCache.TryGetValue((el, pseudo, hov), out var d)) return d;
        d = null;
        var l = new List<(int spec, int order, List<(string k, string v, bool imp)> decls)>();
        foreach (var r in Sheet.rules) if (r.sel.pseudoEl == pseudo && r.sel.Matches(el, null)) l.Add((r.sel.spec, r.order, r.decls));
        if (l.Count > 0)
        {
            d = new Dictionary<string, string>();
            foreach (var x in l.OrderBy(x => x.spec).ThenBy(x => x.order)) foreach (var (k, v, _) in x.decls) d[k] = v.Contains("var(") ? Cascade.SubVars(v, el.cs?.vars) : v;
        }
        pseudoCache[(el, pseudo, hov)] = d;
        return d;
    }
    public static Mat TransformMatrix(Box b)
    {
        var cs = b.cs;
        if (cs == null) return Mat.I;
        var anim = Anim.Transform(b, ops => Ops(b, ops));
        if (anim is { } am) return am;
        if (cs.transform == null) return Mat.I;
        return Ops(b, cs.transform);
    }
    static Mat Ops(Box b, List<TOp> ops)
    {
        if (ops == null) return Mat.I;
        var cs = b.cs;
        double W = b.w, H = b.h;
        double ox = cs.toX.R(W), oy = cs.toY.R(H);
        var m = Mat.T(ox, oy);
        foreach (var t in ops)
        {
            switch (t.f)
            {
                case "t": m = m.Mul(Mat.T(t.a.R(W), t.b.R(H))); break;
                case "s": m = m.Mul(Mat.S(t.v1, t.v2)); break;
                case "r": m = m.Mul(Mat.R(t.v1)); break;
                case "k": m = m.Mul(new Mat { a = 1, d = 1, c = Math.Tan(t.v1), b = Math.Tan(t.v2) }); break;
                case "m": m = m.Mul(new Mat { a = t.mat[0], b = t.mat[1], c = t.mat[2], d = t.mat[3], e = t.a.R(0), f = t.b.R(0) }); break;
            }
        }
        return m.Mul(Mat.T(-ox, -oy));
    }
    /* position:sticky: how far the box is pushed to stay in its scroller's view */
    public static double StickyX(Box b) => 0;
    public static double StickyY(Box b)
    {
        if (b.cs == null || b.cs.position != Pos.Sticky || b.cs.top.IsAuto) return 0;
        /* the nearest scrolling ancestor */
        Box sc = null; double y = b.y;
        for (var p = b.parent; p != null; p = p.parent)
        {
            if (p.el != null && !p.isPseudo && !p.inlineBox && p.cs.Clips) { sc = p; break; }
            y += p.y;
        }
        if (sc == null) return 0;
        double top = b.cs.top.R(sc.PadH);
        double viewTop = sc.el.scrollY + sc.bt;      /* in the scroller's content frame */
        double want = viewTop + top - y;
        if (want <= 0) return 0;
        /* never past the end of its own parent */
        var par = b.parent;
        double parBottom = 0;
        if (par != null && par != sc) { double py = 0; for (var p = par; p != null && p != sc; p = p.parent) py += p.y; parBottom = py + par.h - par.bb - par.pb - (y + b.h); return Math.Max(0, Math.Min(want, parBottom)); }
        return want;
    }

    /* ---- the walk ---- */
    struct St { public Mat m; public (double x0, double y0, double x1, double y1)? clip; public double alpha; public Func<Col, Col> filter; }
    sealed class Layers { public readonly List<(Box b, St st)> z0 = new(); public readonly List<(Box b, St st, int z, int ord)> pos = new(); }
    static Ctx g;
    static bool hitMode; static double hx, hy; static Element hitEl; static Box hitBox;
    static double DPR;
    static int ordCounter;
    public static double Time;

    public static void Paint(Ctx ctx, Box root, double dpr)
    {
        g = ctx; hitMode = false; DPR = dpr;
        var st = new St { m = Mat.S(dpr, dpr), alpha = 1 };
        SC(root, st);
        Gpu.Scissor(null);
        Ctx.Filter = null;
        g.setTransform(1, 0, 0, 1, 0, 0);
    }
    public static Element Hit(Box root, double x, double y, double dpr, out Box box)
    {
        hitMode = true; hx = x * dpr; hy = y * dpr; hitEl = null; hitBox = null; DPR = dpr;
        var st = new St { m = Mat.S(dpr, dpr), alpha = 1 };
        try { SC(root, st); } finally { hitMode = false; }
        box = hitBox;
        return hitEl;
    }
    static bool IsSC(Box b)
    {
        var c = b.cs;
        if (c == null || b.isText) return false;
        if (c.Positioned && !c.zAuto) return true;
        return c.opacity < 1 || c.transform != null || c.filter != null || c.position is Pos.Fixed or Pos.Sticky || Anim.HasTransform(b);
    }
    static void SC(Box b, St st)
    {
        var L = new Layers();
        One(b, st, L);
        for (int i = 0; i < L.z0.Count; i++)
        {
            var (k, s) = L.z0[i];
            if (IsSC(k)) SC(k, s); else One(k, s, L);
        }
        foreach (var (k, s, _, _) in L.pos.OrderBy(p => p.z).ThenBy(p => p.ord)) SC(k, s);
    }
    static St Enter(Box b, St st, out double W, out double H)
    {
        var s = st;
        if (b.cs.position == Pos.Fixed && !b.inlineBox) s.m = Mat.S(DPR, DPR);
        s.m = s.m.Mul(Mat.T(b.x + b.relX + StickyX(b), b.y + b.relY + StickyY(b)));
        if ((b.cs.transform != null || Anim.HasTransform(b)) && !b.inlineBox) s.m = s.m.Mul(TransformMatrix(b));
        if (b.zoom != 1) s.m = s.m.Mul(Mat.S(b.zoom, b.zoom));
        W = b.w / b.zoom; H = b.h / b.zoom;
        s.alpha *= Anim.Opacity(b);
        if (b.cs.filter != null) s.filter = Compose(s.filter, b.cs.filter);
        if (b.cs.position == Pos.Fixed && !b.inlineBox) s.clip = null;
        return s;
    }
    /* paints b and its normal-flow content; positioned descendants go to L */
    static void One(Box b, St parent, Layers L)
    {
        if (b.cs == null) return;
        var st = Enter(b, parent, out double W, out double H);
        /* SCROLLED OUT OF SIGHT, NOT PAINTED. A box (and everything in it) that lies
           wholly outside the clip of the scroller it is in cannot show, and a long
           list -- the test bench's hundreds of cards -- is mostly that. The margin
           keeps a shadow or a badge hanging over the box's edge. */
        if (st.clip is { } cl && !b.inlineBox && Outside(st.m, W, H, cl, 48 * DPR)) return;
        bool vis = !b.cs.hidden;
        Apply(st);
        if (b.inlineBox) { if (vis) InlineDeco(b, st); }
        else if (vis && !b.anon) Decorate(b, st, W, H);
        if (hitMode && vis && !b.cs.noPointer && !b.inlineBox && !b.anon && InClip(st) && InRect(st.m, 0, 0, W, H)) Take(b);
        /* the content frame: clipped and scrolled */
        var inner = st;
        if (b.cs.Clips && !b.inlineBox)
        {
            var (x0, y0) = st.m.Ap(b.bl, b.bt); var (x1, y1) = st.m.Ap(W - b.br, H - b.bb);
            var r = (Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
            inner.clip = Intersect(st.clip, r);
        }
        if (b.el != null && !b.isPseudo && !b.inlineBox && b.cs.Scrolls) inner.m = inner.m.Mul(Mat.T(-b.el.scrollX, -b.el.scrollY));
        if (b.replaced) { if (vis) Replaced(b, st, W, H); }
        else
        {
            if (b.cs.display == Disp.ListItem && vis && b.cs.listStyle != "none") Bullet(b, inner);
            foreach (var k in b.kids)
            {
                if (k.isText) { if (!k.cs.hidden) Texts(k, inner); continue; }
                if (k.cs == null) continue;
                if (k.cs.Positioned)
                {
                    if (IsSC(k) && !k.cs.zAuto && k.cs.z != 0) L.pos.Add((k, inner, k.cs.z, ordCounter++));
                    else L.z0.Add((k, inner));
                    continue;
                }
                if (IsSC(k)) { SC(k, inner); continue; }
                One(k, inner, L);
            }
        }
        if (b.cs.Scrolls && !b.inlineBox && vis) { Apply(st); Scrollbars(b, st, W, H); }
    }
    static bool Outside(Mat m, double W, double H, (double x0, double y0, double x1, double y1) c, double pad)
    {
        var (ax, ay) = m.Ap(0, 0); var (bx, by) = m.Ap(W, 0); var (cx, cy) = m.Ap(W, H); var (dx, dy) = m.Ap(0, H);
        double x0 = Math.Min(Math.Min(ax, bx), Math.Min(cx, dx)), x1 = Math.Max(Math.Max(ax, bx), Math.Max(cx, dx));
        double y0 = Math.Min(Math.Min(ay, by), Math.Min(cy, dy)), y1 = Math.Max(Math.Max(ay, by), Math.Max(cy, dy));
        return x1 + pad < c.x0 || x0 - pad > c.x1 || y1 + pad < c.y0 || y0 - pad > c.y1;
    }
    static Func<Col, Col> Compose(Func<Col, Col> outer, List<(string f, double v)> fs)
    {
        Func<Col, Col> me = c =>
        {
            foreach (var (f, v) in fs)
            {
                switch (f)
                {
                    case "saturate": { double l = 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b; c = new Col(l + (c.r - l) * v, l + (c.g - l) * v, l + (c.b - l) * v, c.a); break; }
                    case "grayscale": { double l = 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b; double k = Math.Clamp(v, 0, 1); c = new Col(c.r + (l - c.r) * k, c.g + (l - c.g) * k, c.b + (l - c.b) * k, c.a); break; }
                    case "brightness": c = new Col(c.r * v, c.g * v, c.b * v, c.a); break;
                    case "opacity": c = c.WithAlpha(c.a * v); break;
                    case "invert": c = new Col(c.r + (255 - 2 * c.r) * v, c.g + (255 - 2 * c.g) * v, c.b + (255 - 2 * c.b) * v, c.a); break;
                }
            }
            return new Col(Math.Clamp(c.r, 0, 255), Math.Clamp(c.g, 0, 255), Math.Clamp(c.b, 0, 255), c.a);
        };
        if (outer == null) return me;
        return c => outer(me(c));
    }
    static (double, double, double, double)? Intersect((double x0, double y0, double x1, double y1)? a, (double x0, double y0, double x1, double y1) b)
    {
        if (a is not { } q) return b;
        return (Math.Max(q.x0, b.x0), Math.Max(q.y0, b.y0), Math.Min(q.x1, b.x1), Math.Min(q.y1, b.y1));
    }
    static void Apply(St st)
    {
        if (hitMode) return;
        if (st.clip is { } c) { int x0 = (int)Math.Floor(c.x0), y0 = (int)Math.Floor(c.y0); Gpu.Scissor((x0, y0, Math.Max(0, (int)Math.Ceiling(c.x1) - x0), Math.Max(0, (int)Math.Ceiling(c.y1) - y0))); }
        else Gpu.Scissor(null);
        Ctx.Filter = st.filter;
        g.setTransform(st.m.a, st.m.b, st.m.c, st.m.d, st.m.e, st.m.f);
        g.globalAlpha = Math.Clamp(st.alpha, 0, 1);
        curAlpha = st.alpha; curM = st.m;
    }
    static double curAlpha = 1; static Mat curM;
    static bool InClip(St st) => st.clip is not { } c || (hx >= c.x0 && hx < c.x1 && hy >= c.y0 && hy < c.y1);
    static bool InRect(Mat m, double x, double y, double w, double h)
    {
        if (!m.Inv(out var inv)) return false;
        var (lx, ly) = inv.Ap(hx, hy);
        return lx >= x && lx < x + w && ly >= y && ly < y + h;
    }
    static void Take(Box b) { hitBox = b; hitEl = b.el ?? FindEl(b); }
    static Element FindEl(Box b) { for (var p = b; p != null; p = p.parent) if (p.el != null) return p.el; return null; }

    /* ---- the box's own paint: shadow, background, border, outline ---- */
    static void Decorate(Box b, St st, double W, double H)
    {
        if (hitMode) return;
        var c = b.cs;
        var rad = Radii(b, W, H);
        if (c.boxShadow != null)
            for (int i = c.boxShadow.Count - 1; i >= 0; i--) { var s = c.boxShadow[i]; if (!s.inset) OuterShadow(s, 0, 0, W, H, rad, W, H); }
        if (c.backdropBlur > 0) BackdropBlur(st, W, H, c.backdropBlur);
        Background(b, W, H, rad);
        if (c.boxShadow != null)
            for (int i = c.boxShadow.Count - 1; i >= 0; i--) { var s = c.boxShadow[i]; if (s.inset) InsetShadow(b, s, W, H, rad, st); }
        Border(b, 0, 0, W, H, rad);
        if (c.outlineS != 0 && c.outlineW > 0)
        {
            double o = c.outlineOff + c.outlineW;
            var col = Anim.OutlineColor(b) ?? c.outlineCol;
            var r2 = (rad.tl + o, rad.tr + o, rad.br + o, rad.bl + o);
            Ring(-o, -o, W + 2 * o, H + 2 * o, r2, -o + c.outlineW, -o + c.outlineW, W + 2 * o - 2 * c.outlineW, H + 2 * o - 2 * c.outlineW, Shrink(r2, c.outlineW), col, col);
        }
    }
    static (double tl, double tr, double br, double bl) Radii(Box b, double W, double H)
    {
        var c = b.cs;
        double tl = c.rTL.R(W), tr = c.rTR.R(W), br = c.rBR.R(W), bl = c.rBL.R(W);
        if (tl == 0 && tr == 0 && br == 0 && bl == 0) return (0, 0, 0, 0);
        /* the CSS rule: scale every radius down by the same factor until adjacent ones fit */
        double f = 1;
        if (tl + tr > W) f = Math.Min(f, W / (tl + tr)); if (bl + br > W) f = Math.Min(f, W / (bl + br));
        if (tl + bl > H) f = Math.Min(f, H / (tl + bl)); if (tr + br > H) f = Math.Min(f, H / (tr + br));
        return (tl * f, tr * f, br * f, bl * f);
    }
    static (double, double, double, double) Shrink((double tl, double tr, double br, double bl) r, double d) => (Math.Max(0, r.tl - d), Math.Max(0, r.tr - d), Math.Max(0, r.br - d), Math.Max(0, r.bl - d));
    static (double, double, double, double) Grow((double tl, double tr, double br, double bl) r, double d) =>
        (r.tl > 0 ? Math.Max(0, r.tl + d) : 0, r.tr > 0 ? Math.Max(0, r.tr + d) : 0, r.br > 0 ? Math.Max(0, r.br + d) : 0, r.bl > 0 ? Math.Max(0, r.bl + d) : 0);
    static (double, double, double, double) GrowAll((double tl, double tr, double br, double bl) r, double d) => (Math.Max(0, r.tl + d), Math.Max(0, r.tr + d), Math.Max(0, r.br + d), Math.Max(0, r.bl + d));

    /* a rounded rectangle's outline, CW from the top-left, the same count of points for any radii */
    const int CSEG = 6;
    static void RRPoints(double x, double y, double w, double h, (double tl, double tr, double br, double bl) r, List<(double, double)> o)
    {
        o.Clear();
        void Corner(double cx, double cy, double rr, double a0)
        {
            for (int i = 0; i <= CSEG; i++)
            {
                double a = a0 + Math.PI / 2 * i / CSEG;
                o.Add((cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr));
            }
        }
        Corner(x + r.tl, y + r.tl, r.tl, Math.PI);
        Corner(x + w - r.tr, y + r.tr, r.tr, -Math.PI / 2);
        Corner(x + w - r.br, y + h - r.br, r.br, 0);
        Corner(x + r.bl, y + h - r.bl, r.bl, Math.PI / 2);
    }
    static readonly List<(double, double)> ptA = new(), ptB = new();
    static Col Pm(Col c, double a) => Ctx.Premul(c, a);
    static void V(double x, double y, Col pm) { var (X, Y) = curM.Ap(x, y); Gpu.V((float)X, (float)Y, 0.5f, 0.5f, pm.r, pm.g, pm.b, pm.a); }
    static void FillRR(double x, double y, double w, double h, (double tl, double tr, double br, double bl) r, Col col)
    {
        if (w <= 0 || h <= 0 || col.a <= 0) return;
        var pm = Pm(col, curAlpha);
        Gpu.Use(g.Tgt, BlendOp.SourceOver, null, Gpu.WhiteTex);
        if (r.tl == 0 && r.tr == 0 && r.br == 0 && r.bl == 0)
        {
            Gpu.Need(6);
            V(x, y, pm); V(x + w, y, pm); V(x + w, y + h, pm); V(x, y, pm); V(x + w, y + h, pm); V(x, y + h, pm);
            return;
        }
        RRPoints(x, y, w, h, r, ptA);
        double cx = x + w / 2, cy = y + h / 2;
        Gpu.Need(ptA.Count * 3);
        for (int i = 0; i < ptA.Count; i++)
        {
            var p = ptA[i]; var q = ptA[(i + 1) % ptA.Count];
            V(cx, cy, pm); V(p.Item1, p.Item2, pm); V(q.Item1, q.Item2, pm);
        }
    }
    /* the band between two rounded rectangles, with a colour at each edge (a soft edge when they differ) */
    static void Ring(double ox, double oy, double ow, double oh, (double, double, double, double) orad,
                     double ix, double iy, double iw, double ih, (double, double, double, double) irad, Col outer, Col inner)
    {
        if (outer.a <= 0 && inner.a <= 0) return;
        RRPoints(ox, oy, ow, oh, orad, ptA);
        RRPoints(ix, iy, Math.Max(0, iw), Math.Max(0, ih), irad, ptB);
        var po = Pm(outer, curAlpha); var pi = Pm(inner, curAlpha);
        Gpu.Use(g.Tgt, BlendOp.SourceOver, null, Gpu.WhiteTex);
        int n = ptA.Count;
        Gpu.Need(n * 6);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var a = ptA[i]; var b2 = ptA[j]; var c = ptB[i]; var d = ptB[j];
            V(a.Item1, a.Item2, po); V(b2.Item1, b2.Item2, po); V(d.Item1, d.Item2, pi);
            V(a.Item1, a.Item2, po); V(d.Item1, d.Item2, pi); V(c.Item1, c.Item2, pi);
        }
    }
    static void OuterShadow(Shadow s, double x, double y, double W, double H, (double tl, double tr, double br, double bl) rad, double bw, double bh)
    {
        double dx = s.x.R(0), dy = s.y.R(0), sp = s.spread, bl = s.blur;
        var col = s.col;
        if (col.a <= 0) return;
        double X = x + dx - sp, Y = y + dy - sp, Ww = W + 2 * sp, Hh = H + 2 * sp;
        var r = GrowAll(rad, sp);
        if (bl <= 0.5) { FillRR(X, Y, Ww, Hh, r, col); return; }
        /* a gaussian edge, roughly: a solid core and a feather blur wide, centred on the edge */
        double h = bl / 2;
        double cw = Ww - 2 * h, ch = Hh - 2 * h;
        var rc = Shrink(r, h);
        if (cw > 0 && ch > 0) FillRR(X + h, Y + h, cw, ch, rc, col);
        var ro = GrowAll(r, h);
        /* two feather bands make the falloff curve instead of a straight ramp */
        var mid = col.WithAlpha(col.a * 0.5);
        Ring(X - h, Y - h, Ww + 2 * h, Hh + 2 * h, ro, X, Y, Ww, Hh, r, col.WithAlpha(0), mid);
        if (cw > 0 && ch > 0) Ring(X, Y, Ww, Hh, r, X + h, Y + h, cw, ch, rc, mid, col);
    }
    static void InsetShadow(Box b, Shadow s, double W, double H, (double tl, double tr, double br, double bl) rad, St st)
    {
        double px = b.bl, py = b.bt, pw = W - b.bl - b.br, ph = H - b.bt - b.bb;
        var pr = (Math.Max(0, rad.tl - Math.Max(b.bl, b.bt)), Math.Max(0, rad.tr - Math.Max(b.br, b.bt)), Math.Max(0, rad.br - Math.Max(b.br, b.bb)), Math.Max(0, rad.bl - Math.Max(b.bl, b.bb)));
        double dx = s.x.R(0), dy = s.y.R(0), sp = s.spread, bl = s.blur;
        /* clip to the padding box: the hole's offset pushes part of it outside */
        var save = st.clip;
        var (x0, y0) = curM.Ap(px, py); var (x1, y1) = curM.Ap(px + pw, py + ph);
        var clip = Intersect(save, (Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1)));
        var keep = st; keep.clip = clip; Apply(keep);
        double ix = px + dx + sp, iy = py + dy + sp, iw = pw - 2 * sp, ih = ph - 2 * sp;
        var ir = Shrink(pr, sp);
        var col = s.col;
        if (bl <= 0.5) Ring(px - Math.Abs(dx) - 1, py - Math.Abs(dy) - 1, pw + 2 * Math.Abs(dx) + 2, ph + 2 * Math.Abs(dy) + 2, GrowAll(pr, Math.Max(Math.Abs(dx), Math.Abs(dy)) + 1), ix, iy, iw, ih, ir, col, col);
        else
        {
            double h = bl / 2;
            Ring(px - Math.Abs(dx) - 1, py - Math.Abs(dy) - 1, pw + 2 * Math.Abs(dx) + 2, ph + 2 * Math.Abs(dy) + 2, GrowAll(pr, Math.Max(Math.Abs(dx), Math.Abs(dy)) + 1), ix - h, iy - h, iw + 2 * h, ih + 2 * h, GrowAll(ir, h), col, col);
            Ring(ix - h, iy - h, iw + 2 * h, ih + 2 * h, GrowAll(ir, h), ix + h, iy + h, iw - 2 * h, ih - 2 * h, Shrink(ir, h), col, col.WithAlpha(0));
        }
        Apply(st);
    }
    static void Border(Box b, double x, double y, double W, double H, (double tl, double tr, double br, double bl) rad)
    {
        var c = b.cs;
        if (c.bT + c.bR + c.bB + c.bL <= 0) return;
        bool uniform = c.cT == c.cR && c.cT == c.cB && c.cT == c.cL && c.sT == c.sR && c.sT == c.sB && c.sT == c.sL && c.sT == 1;
        var inner = (Math.Max(0, rad.tl - Math.Max(c.bL, c.bT)), Math.Max(0, rad.tr - Math.Max(c.bR, c.bT)), Math.Max(0, rad.br - Math.Max(c.bR, c.bB)), Math.Max(0, rad.bl - Math.Max(c.bL, c.bB)));
        if (uniform) { Ring(x, y, W, H, rad, x + c.bL, y + c.bT, W - c.bL - c.bR, H - c.bT - c.bB, inner, c.cT, c.cT); return; }
        /* sides one at a time: a trapezoid each, dots and dashes where asked */
        void Side(byte style, double w, Col col, double ax, double ay, double bx, double by, double cx2, double cy2, double dx2, double dy2, bool horiz, double len)
        {
            if (style == 0 || w <= 0 || col.a <= 0) return;
            var pm = Pm(col, curAlpha);
            Gpu.Use(g.Tgt, BlendOp.SourceOver, null, Gpu.WhiteTex);
            if (style == 1 || style == 4)
            {
                Gpu.Need(6);
                V(ax, ay, pm); V(bx, by, pm); V(cx2, cy2, pm); V(ax, ay, pm); V(cx2, cy2, pm); V(dx2, dy2, pm);
                return;
            }
            /* dotted: squares one border-width apart; dashed: dashes three wide */
            double seg = style == 2 ? w : w * 3, gap = style == 2 ? w : w * 3;
            int n = Math.Max(1, (int)Math.Floor((len + gap) / (seg + gap)));
            double step = n > 1 ? (len - seg) / (n - 1) : 0;
            Gpu.Need(6 * n);
            for (int i = 0; i < n; i++)
            {
                double t0 = i * step, t1 = t0 + seg;
                double X0, Y0, X1, Y1;
                if (horiz) { X0 = ax + t0; X1 = ax + t1; Y0 = Math.Min(ay, dy2); Y1 = Math.Max(ay, dy2); }
                else { Y0 = ay + t0; Y1 = ay + t1; X0 = Math.Min(ax, bx); X1 = Math.Max(ax, bx); }
                V(X0, Y0, pm); V(X1, Y0, pm); V(X1, Y1, pm); V(X0, Y0, pm); V(X1, Y1, pm); V(X0, Y1, pm);
            }
        }
        double L = c.bL, R = c.bR, T = c.bT, B = c.bB;
        Side(c.sT, T, c.cT, x, y, x + W, y, x + W - R, y + T, x + L, y + T, true, W);
        Side(c.sR, R, c.cR, x + W - R, y + T, x + W, y, x + W, y + H, x + W - R, y + H - B, false, H);
        Side(c.sB, B, c.cB, x + L, y + H - B, x + W - R, y + H - B, x + W, y + H, x, y + H, true, W);
        Side(c.sL, L, c.cL, x, y, x + L, y + T, x + L, y + H - B, x, y + H, false, H);
    }
    static void Background(Box b, double W, double H, (double tl, double tr, double br, double bl) rad)
    {
        /* the field is drawn on the window before the page: the boxes it sits in
           (html, body) would paint their backgrounds over it, where the browser has
           them under the canvas, which covers them edge to edge */
        if (Dom.FieldEl != null && b.el != null && b.el != Dom.FieldEl && !b.isPseudo && b.el.contains(Dom.FieldEl)) return;
        var c = b.cs;
        double x = 0, y = 0, w = W, h = H; var r = rad;
        if (c.bgClip == "padding-box") { x = b.bl; y = b.bt; w = W - b.bl - b.br; h = H - b.bt - b.bb; r = Shrink(rad, Math.Max(b.bl, b.bt)); }
        else if (c.bgClip == "content-box") { x = b.bl + b.pl; y = b.bt + b.pt; w = W - b.bl - b.br - b.pl - b.pr; h = H - b.bt - b.bb - b.pt - b.pb; r = Shrink(rad, Math.Max(b.bl + b.pl, b.bt + b.pt)); }
        var bg = Anim.Bg(b) ?? c.bg;
        if (bg.a > 0) FillRR(x, y, w, h, r, bg);
        if (c.bgImg == null) return;
        /* the layers paint last-first */
        for (int i = c.bgImg.Count - 1; i >= 0; i--)
        {
            var img = c.bgImg[i];
            if (img == null || img.url != null) continue;
            /* the positioning area is the padding box */
            double ax = b.bl, ay = b.bt, aw = W - b.bl - b.br, ah = H - b.bt - b.bb;
            (Len w, Len h, string kw) size = c.bgSize != null ? c.bgSize[Math.Min(i, c.bgSize.Count - 1)] : (Len.Auto, Len.Auto, "auto");
            double tw = aw, th = ah;
            if (size.kw == null || size.kw == "auto")
            {
                if (!size.Item1.IsAuto) tw = size.Item1.R(aw);
                if (!size.Item2.IsAuto) th = size.Item2.R(ah);
                else if (!size.Item1.IsAuto) th = tw;
            }
            if (tw <= 0.5 || th <= 0.5) continue;
            string rep = c.bgRepeat != null ? c.bgRepeat[Math.Min(i, c.bgRepeat.Count - 1)] : "repeat";
            bool tiles = (tw < aw - 0.5 || th < ah - 0.5) && rep != "no-repeat";
            if (!tiles) { Gradient(img, x, y, w, h, r, ax, ay, tw, th); continue; }
            /* a small tile repeated: drawn once into a texture, then stamped */
            var tile = Tile(img, tw, th);
            var pos = c.bgPos != null ? c.bgPos[Math.Min(i, c.bgPos.Count - 1)] : (Len.Pct(0), Len.Pct(0));
            double px = ax + pos.Item1.R(aw - tw), py = ay + pos.Item2.R(ah - th);
            double sx = px - Math.Ceiling((px - x) / tw) * tw, sy = py - Math.Ceiling((py - y) / th) * th;
            g.save();
            g.globalAlpha = Math.Clamp(curAlpha, 0, 1);
            /* the tiles start at or before the painting area's edge: each is cut to
               the area, as the browser clips the layer to it */
            double kx = tile.width / tw, ky = tile.height / th;
            for (double ty = sy; ty < y + h; ty += th)
                for (double tx = sx; tx < x + w; tx += tw)
                {
                    double cx0 = Math.Max(tx, x), cy0 = Math.Max(ty, y), cx1 = Math.Min(tx + tw, x + w), cy1 = Math.Min(ty + th, y + h);
                    if (cx1 - cx0 < 0.01 || cy1 - cy0 < 0.01) continue;
                    g.drawImage(tile, (cx0 - tx) * kx, (cy0 - ty) * ky, (cx1 - cx0) * kx, (cy1 - cy0) * ky, cx0, cy0, cx1 - cx0, cy1 - cy0);
                }
            g.restore();
        }
    }
    /* BACKDROP-FILTER: BLUR. What is already drawn behind the box is copied out,
       shrunk twice (each a bilinear 2x2 average) and stretched back under the
       box's own background: a soft blur of about the radius asked for, at the
       cost of one copy and three small draws. */
    static OffCanvas bdFull, bdA, bdB;
    static void BackdropBlur(St st, double W, double H, double radius)
    {
        var (ax, ay) = st.m.Ap(0, 0); var (bx, by) = st.m.Ap(W, H);
        var tgt = g.Tgt;
        int x0 = (int)Math.Floor(Math.Max(0, Math.Min(ax, bx))), y0 = (int)Math.Floor(Math.Max(0, Math.Min(ay, by)));
        int x1 = (int)Math.Ceiling(Math.Min(tgt.w, Math.Max(ax, bx))), y1 = (int)Math.Ceiling(Math.Min(tgt.h, Math.Max(ay, by)));
        if (st.clip is { } cl) { x0 = Math.Max(x0, (int)Math.Floor(cl.x0)); y0 = Math.Max(y0, (int)Math.Floor(cl.y0)); x1 = Math.Min(x1, (int)Math.Ceiling(cl.x1)); y1 = Math.Min(y1, (int)Math.Ceiling(cl.y1)); }
        int w = x1 - x0, h = y1 - y0;
        if (w < 4 || h < 4) return;
        /* how far to shrink: a 3px blur at 1x is about a quarter */
        double k = Math.Clamp(radius * DPR / 1.5, 1, 16);
        int sw = Math.Max(1, (int)Math.Round(w / k)), sh = Math.Max(1, (int)Math.Round(h / k));
        int mw = Math.Max(sw, w / 2), mh = Math.Max(sh, h / 2);
        bdFull ??= new OffCanvas(); bdA ??= new OffCanvas(); bdB ??= new OffCanvas();
        if (bdFull.width < w || bdFull.height < h) { bdFull.width = Math.Max(bdFull.width, w); bdFull.height = Math.Max(bdFull.height, h); }
        if (bdA.width != mw || bdA.height != mh) { bdA.width = mw; bdA.height = mh; }
        if (bdB.width != sw || bdB.height != sh) { bdB.width = sw; bdB.height = sh; }
        Gpu.Copy(tgt, x0, y0, w, h, bdFull.T);
        var keepF = Ctx.Filter; Ctx.Filter = null;
        var ca = bdA.getContext("2d"); ca.Reset(); ca.imageSmoothingEnabled = true; ca.globalCompositeOperation = "copy";
        ca.drawImage(bdFull, 0, 0, w, h, 0, 0, mw, mh);
        var cb = bdB.getContext("2d"); cb.Reset(); cb.imageSmoothingEnabled = true; cb.globalCompositeOperation = "copy";
        cb.drawImage(bdA, 0, 0, mw, mh, 0, 0, sw, sh);
        g.save();
        g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.globalCompositeOperation = "copy";
        g.imageSmoothingEnabled = true;
        g.drawImage(bdB, 0, 0, sw, sh, x0, y0, w, h);
        g.restore();
        Ctx.Filter = keepF;
    }
    static readonly Dictionary<(BgImage, int, int), OffCanvas> tiles = new();
    static OffCanvas Tile(BgImage img, double tw, double th)
    {
        int W = Math.Max(1, (int)Math.Ceiling(tw * DPR)), H = Math.Max(1, (int)Math.Ceiling(th * DPR));
        if (tiles.TryGetValue((img, W, H), out var t) && t.target != null && t.target.tex != 0) return t;
        if (tiles.Count > 64) { foreach (var v in tiles.Values) v.Free(); tiles.Clear(); }
        t = new OffCanvas { width = W, height = H };
        var tg = t.getContext("2d");
        var keepM = curM; var keepA = curAlpha; var keepF = Ctx.Filter;
        Ctx.Filter = null;
        Gpu.Scissor(null);
        tg.setTransform(W / tw, 0, 0, H / th, 0, 0);
        var gsave = g; g = tg; curM = new Mat { a = W / tw, d = H / th }; curAlpha = 1;
        Gradient(img, 0, 0, tw, th, (0, 0, 0, 0), 0, 0, tw, th);
        g = gsave; curM = keepM; curAlpha = keepA; Ctx.Filter = keepF;
        Gpu.Flush();
        tiles[(img, W, H)] = t;
        return t;
    }
    /* a CSS gradient over (x,y,w,h), its geometry from the image box (ix,iy,iw,ih) */
    static void Gradient(BgImage img, double x, double y, double w, double h, (double tl, double tr, double br, double bl) r, double ix, double iy, double iw, double ih)
    {
        Grad gr;
        double len;
        if (!img.radial)
        {
            double ang = img.angle * Math.PI / 180;
            double sx = Math.Sin(ang), sy = -Math.Cos(ang);
            len = Math.Abs(iw * sx) + Math.Abs(ih * sy);
            double cx = ix + iw / 2, cy = iy + ih / 2;
            gr = g.createLinearGradient(cx - sx * len / 2, cy - sy * len / 2, cx + sx * len / 2, cy + sy * len / 2);
        }
        else
        {
            double cx = ix + img.atX.R(iw), cy = iy + img.atY.R(ih);
            double dl = cx - ix, dr = ix + iw - cx, dt = cy - iy, db = iy + ih - cy;
            double rad;
            switch (img.size)
            {
                case "closest-side": rad = Math.Min(Math.Min(dl, dr), Math.Min(dt, db)); break;
                case "farthest-side": rad = Math.Max(Math.Max(dl, dr), Math.Max(dt, db)); break;
                case "closest-corner": rad = Math.Sqrt(Math.Pow(Math.Min(dl, dr), 2) + Math.Pow(Math.Min(dt, db), 2)); break;
                default:
                    {
                        var L = Cascade.ParseLen(img.size, 16, 16);
                        rad = L.IsVal && img.size != "farthest-corner" ? L.R(iw) : Math.Sqrt(Math.Pow(Math.Max(dl, dr), 2) + Math.Pow(Math.Max(dt, db), 2));
                        break;
                    }
            }
            /* an ellipse is a circle here, sized by the larger axis */
            len = Math.Max(1e-3, rad);
            gr = g.createRadialGradient(cx, cy, 0, cx, cy, len);
        }
        /* stops: positions along the line, missing ones spaced evenly */
        int n = img.stops.Count;
        var pos = new double[n];
        for (int i = 0; i < n; i++) pos[i] = img.stops[i].hasPos ? img.stops[i].pos.R(len) / len : double.NaN;
        if (double.IsNaN(pos[0])) pos[0] = 0;
        if (double.IsNaN(pos[n - 1])) pos[n - 1] = n > 1 ? 1 : 0;
        for (int i = 1; i < n; i++) if (!double.IsNaN(pos[i]) && pos[i] < pos[i - 1]) pos[i] = pos[i - 1];
        for (int i = 1; i < n - 1; i++)
            if (double.IsNaN(pos[i]))
            {
                int j = i; while (double.IsNaN(pos[j])) j++;
                for (int k = i; k < j; k++) pos[k] = pos[i - 1] + (pos[j] - pos[i - 1]) * (k - i + 1) / (j - i + 1);
                i = j - 1;
            }
        for (int i = 0; i < n; i++) gr.addColorStop(Math.Clamp(pos[i], 0, 1), img.stops[i].col);
        g.fillStyle = gr;
        g.globalAlpha = Math.Clamp(curAlpha, 0, 1);
        g.beginPath(); RRPath(x, y, w, h, r); g.fill();
    }
    static void RRPath(double x, double y, double w, double h, (double tl, double tr, double br, double bl) r)
    {
        if (r.tl == 0 && r.tr == 0 && r.br == 0 && r.bl == 0) { g.rect(x, y, w, h); return; }
        RRPoints(x, y, w, h, r, ptA);
        g.moveTo(ptA[0].Item1, ptA[0].Item2);
        for (int i = 1; i < ptA.Count; i++) g.lineTo(ptA[i].Item1, ptA[i].Item2);
        g.closePath();
    }
    /* an inline box: background and border on each line it crosses */
    static void InlineDeco(Box b, St st)
    {
        if (b.frags == null) return;
        var c = b.cs;
        foreach (var f in b.frags)
        {
            if (double.IsNaN(f.y)) continue;
            if (hitMode) { if (!c.noPointer && InClip(st) && InRect(st.m, f.x, f.y, f.w, f.h)) Take(b); continue; }
            bool hasBg = c.bg.a > 0 || c.bgImg != null || c.bT + c.bB + c.bL + c.bR > 0 || c.boxShadow != null;
            if (!hasBg) continue;
            var rad = Radii(b, f.w, f.h);
            if (!f.first) rad = (0, rad.Item2, rad.Item3, 0);
            if (!f.last) rad = (rad.Item1, 0, 0, rad.Item4);
            if (c.boxShadow != null) foreach (var s in c.boxShadow) if (!s.inset) OuterShadow(s, f.x, f.y, f.w, f.h, rad, f.w, f.h);
            var bg = Anim.Bg(b) ?? c.bg;
            if (bg.a > 0) FillRR(f.x, f.y, f.w, f.h, rad, bg);
            if (c.bgImg != null) foreach (var img in c.bgImg) if (img != null && img.url == null) Gradient(img, f.x, f.y, f.w, f.h, rad, f.x, f.y, f.w, f.h);
            var save = (b.bl, b.br);
            if (!f.first) b.bl = 0; if (!f.last) b.br = 0;
            var cs2 = c;
            if (c.bT + c.bB + c.bL + c.bR > 0)
            {
                double L = f.first ? c.bL : 0, R = f.last ? c.bR : 0;
                var tmp = new Box { cs = new CStyle { bT = c.bT, bB = c.bB, bL = L, bR = R, cT = c.cT, cB = c.cB, cL = c.cL, cR = c.cR, sT = c.sT, sB = c.sB, sL = L > 0 ? c.sL : (byte)0, sR = R > 0 ? c.sR : (byte)0 } };
                Border(tmp, f.x, f.y, f.w, f.h, rad);
            }
            (b.bl, b.br) = save;
        }
    }

    /* ---- text ---- */
    static void Texts(Box t, St st)
    {
        if (t.pieces == null) return;
        if (hitMode)
        {
            if (!InClip(st)) return;
            var owner = t.parent;
            if (owner == null || owner.cs.noPointer) return;
            foreach (var p in t.pieces) if (InRect(st.m, p.x, p.y - p.font.Ascent, p.w, p.font.Ascent + p.font.Descent)) { hitBox = owner; hitEl = FindEl(owner); return; }
            return;
        }
        Apply(st);
        foreach (var p in t.pieces)
        {
            if (p.space && p.cs.deco == "none") continue;
            var cs = p.cs;
            g.fontSpec = p.font;
            if (cs.textShadow != null)
                for (int i = cs.textShadow.Count - 1; i >= 0; i--)
                {
                    var sh = cs.textShadow[i];
                    double dx = sh.x.R(0), dy = sh.y.R(0);
                    if (sh.blur > 0.5)
                    {
                        var c2 = sh.col.WithAlpha(sh.col.a * 0.35);
                        for (int k = 0; k < 6; k++) { double a = k * Math.PI / 3; DrawRun(p, p.x + dx + Math.Cos(a) * sh.blur * 0.4, p.y + dy + Math.Sin(a) * sh.blur * 0.4, c2); }
                    }
                    else DrawRun(p, p.x + dx, p.y + dy, sh.col);
                }
            DrawRun(p, p.x, p.y, cs.color);
            if (cs.deco is "underline" or "line-through" or "overline")
            {
                double ly = cs.deco == "underline" ? p.y + Math.Max(1, p.font.px * 0.1) : cs.deco == "overline" ? p.y - p.font.Ascent : p.y - p.font.px * 0.3;
                FillRR(p.x, ly, p.w, Math.Max(1, p.font.px / 14), (0, 0, 0, 0), cs.color);
            }
        }
    }
    static void DrawRun(TextPiece p, double x, double y, Col col)
    {
        if (col.a <= 0) return;
        g.fillStyle = col;
        g.textAlign = "left"; g.textBaseline = "alphabetic";
        if (p.cs.letter == 0) { g.fillText(p.text, x, y); return; }
        double pen = x;
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(p.text);
        while (e.MoveNext())
        {
            string ch = (string)e.Current;
            g.fillText(ch, pen, y);
            pen += Ctx.Measure(ch, p.font) + p.cs.letter;
        }
    }
    static void Bullet(Box b, St st)
    {
        if (hitMode) return;
        Apply(st);
        var f = b.cs.Font;
        double y = !double.IsNaN(b.baseline) ? b.baseline : b.bt + b.pt + f.Ascent;
        double x = b.bl + b.pl;
        if (b.cs.listStyle == "decimal")
        {
            int idx = 1; for (var s = b.el.previousElementSibling; s != null; s = s.previousElementSibling) idx++;
            string s2 = idx + ". ";
            g.fontSpec = f; g.fillStyle = b.cs.color; g.textAlign = "right"; g.textBaseline = "alphabetic"; g.fillText(s2, x, y);
            return;
        }
        double r = f.px * 0.18;
        double cx = x - f.px * 0.55, cy = y - f.px * 0.3;
        g.fillStyle = b.cs.color; g.globalAlpha = Math.Clamp(curAlpha, 0, 1);
        g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2);
        if (b.cs.listStyle == "circle") { g.strokeStyle = b.cs.color; g.lineWidth = 1; g.stroke(); } else g.fill();
    }

    /* ---- replaced content ---- */
    static void Replaced(Box b, St st, double W, double H)
    {
        var e = b.el;
        double cx = b.bl + b.pl, cy = b.bt + b.pt, cw = W - b.bl - b.br - b.pl - b.pr, ch = H - b.bt - b.bb - b.pt - b.pb;
        if (e.canvas != null)
        {
            if (hitMode) return;
            Apply(st);
            if (e.canvas.target != null && cw > 0 && ch > 0) g.drawImage(e.canvas, 0, 0, e.canvas.width, e.canvas.height, cx, cy, cw, ch);
            return;
        }
        if (e.localName == "svg")
        {
            var inner = st;
            if (hitMode) { var hit = Svg.Hit(e, st.m.Mul(Mat.T(cx, cy)), cw, ch, hx, hy); if (hit != null && InClip(st)) { hitBox = b; hitEl = hit; } return; }
            Apply(st);
            Svg.Paint(g, e, st.m.Mul(Mat.T(cx, cy)), cw, ch, st.alpha, b.cs);
            Apply(st);
            return;
        }
        if (e.localName == "input") { if (!hitMode) Input(b, st, cx, cy, cw, ch); }
    }
    static void Input(Box b, St st, double x, double y, double w, double h)
    {
        var e = b.el;
        if (e.type == "range")
        {
            double mn = e.RangeMin, mx = e.RangeMax, v = Js.ToNumber(e.value);
            double t = mx > mn ? (v - mn) / (mx - mn) : 0;
            var th = PseudoStyle(e, "-webkit-slider-thumb");
            double tw = 16, thh = 16; var tcol = new Col(255, 255, 255); List<Shadow> tsh = null; double trad = 8;
            if (th != null)
            {
                if (th.TryGetValue("width", out var s1)) tw = Cascade.ParseLen(s1, 16, 16).R(0);
                if (th.TryGetValue("height", out var s2)) thh = Cascade.ParseLen(s2, 16, 16).R(0);
                if (th.TryGetValue("background", out var s3)) tcol = Cascade.ColorOf(s3, b.cs.color);
                if (th.TryGetValue("border-radius", out var s4)) trad = Cascade.ParseLen(s4, 16, 16).R(tw);
                if (th.TryGetValue("box-shadow", out var s5)) { var tmp = new CStyle(); tsh = ShadowsOf(s5, b.cs.color); }
            }
            /* no styled track: the browser's own thin one */
            if (b.cs.bg.a <= 0 && b.cs.bgImg == null) FillRR(x, y + h / 2 - 2, w, 4, (2, 2, 2, 2), new Col(180, 180, 180));
            var acc = b.cs.spec?.GetValueOrDefault("accent-color");
            if (acc != null) { var ac = Cascade.ColorOf(acc, b.cs.color); FillRR(x, y + h / 2 - Math.Min(h, 8) / 2, Math.Max(0, (w - tw) * t + tw / 2), Math.Min(h, 8), (h / 2, 0, 0, h / 2), ac.WithAlpha(ac.a * 0.0)); }
            double tx = x + (w - tw) * t, ty = y + (h - thh) / 2;
            var r = (Math.Min(trad, tw / 2), Math.Min(trad, tw / 2), Math.Min(trad, tw / 2), Math.Min(trad, tw / 2));
            if (tsh != null) for (int i = tsh.Count - 1; i >= 0; i--) if (!tsh[i].inset) OuterShadow(tsh[i], tx, ty, tw, thh, r, tw, thh);
            FillRR(tx, ty, tw, thh, r, tcol);
            return;
        }
        /* a text field: its value (or placeholder), clipped, with a caret when focused */
        var f = b.cs.Font;
        string val = e.value;
        bool ph = val.Length == 0;
        string show = ph ? e.placeholder : val;
        var clip = st; var (x0, y0) = st.m.Ap(x, y); var (x1, y1) = st.m.Ap(x + w, y + h);
        clip.clip = Intersect(st.clip, (Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1)));
        Apply(clip);
        Col col = b.cs.color; double letter = b.cs.letter; FontSpec pf = f;
        if (ph)
        {
            var pst = PseudoStyle(e, "placeholder");
            col = col.WithAlpha(col.a * 0.55);
            if (pst != null) { if (pst.TryGetValue("color", out var pc)) col = Cascade.ColorOf(pc, b.cs.color); if (pst.TryGetValue("letter-spacing", out var pl)) letter = Cascade.ParseLen(pl, f.px, 16).R(0); }
        }
        double baseY = y + (h - (f.Ascent + f.Descent)) / 2 + f.Ascent;
        /* scroll so the caret stays in view */
        double caretX = MeasureL(val.Substring(0, Math.Min(e.caret, val.Length)), f, letter);
        double scroll = Math.Max(0, caretX - w + 2);
        if (Dom.focused == e && e.selFrom >= 0 && e.selFrom != e.caret && !ph)
        {
            int a = Math.Min(e.selFrom, e.caret), z = Math.Max(e.selFrom, e.caret);
            double sx0 = MeasureL(val.Substring(0, a), f, letter), sx1 = MeasureL(val.Substring(0, z), f, letter);
            FillRR(x + sx0 - scroll, baseY - f.Ascent, sx1 - sx0, f.Ascent + f.Descent, (0, 0, 0, 0), new Col(80, 140, 255, 0.45));
        }
        if (show.Length > 0)
        {
            var piece = new TextPiece { text = show, x = x - (ph ? 0 : scroll), y = baseY, font = f, cs = letter == b.cs.letter ? b.cs : new CStyle { letter = letter } };
            g.fontSpec = f;
            DrawRun(piece, piece.x, baseY, col);
        }
        if (Dom.focused == e && ((int)(Time * 2) % 2 == 0 || Time - Dom.lastKeyTime < 0.6))
            FillRR(x + caretX - scroll, baseY - f.Ascent, 1, f.Ascent + f.Descent, (0, 0, 0, 0), b.cs.color);
        Apply(st);
    }
    static double MeasureL(string s, FontSpec f, double letter) => s.Length == 0 ? 0 : Ctx.Measure(s, f) + letter * new System.Globalization.StringInfo(s).LengthInTextElements;
    static List<Shadow> ShadowsOf(string v, Col cur)
    {
        var l = new List<Shadow>();
        foreach (var part in CssParser.SplitTop(v, ','))
        {
            var sh = new Shadow { col = cur }; var nums = new List<double>();
            foreach (var w in CssParser.SplitTop(part.Trim(), ' ').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (w == "inset") sh.inset = true;
                else if (char.IsDigit(w[0]) || w[0] == '-' || w[0] == '.') nums.Add(Cascade.ParseLen(w, 16, 16).R(0));
                else sh.col = Cascade.ColorOf(w, cur);
            }
            if (nums.Count < 2) continue;
            sh.x = Len.Px(nums[0]); sh.y = Len.Px(nums[1]); sh.blur = nums.Count > 2 ? nums[2] : 0; sh.spread = nums.Count > 3 ? nums[3] : 0;
            l.Add(sh);
        }
        return l;
    }

    /* ---- scrollbars ---- */
    public static (double x, double y, double w, double h)? ThumbRect(Box b, double W, double H)
    {
        if (b.el == null || b.cs.ovY is not (Ovf.Auto or Ovf.Scroll)) return null;
        double ch = b.contentH, vh = b.PadH;
        if (ch <= vh + 0.5) return null;
        double sw = b.scrollbarW > 0 ? b.scrollbarW : ScrollbarW(b);
        if (sw <= 0) return null;
        double trackX = W - b.br - sw, trackY = b.bt, trackH = vh;
        double th = Math.Max(18, trackH * vh / ch);
        double ty = trackY + (trackH - th) * (b.el.scrollY / Math.Max(1, ch - vh));
        return (trackX, ty, sw, th);
    }
    static void Scrollbars(Box b, St st, double W, double H)
    {
        if (hitMode) return;
        var t = ThumbRect(b, W, H);
        if (t is not { } r) return;
        var track = PseudoStyle(b.el, "-webkit-scrollbar-track");
        var thumb = PseudoStyle(b.el, "-webkit-scrollbar-thumb");
        Col trackCol = new Col(0, 0, 0, 0), thumbCol = new Col(160, 160, 160, 0.6);
        double rad = r.w / 2;
        string sc = b.cs.spec?.GetValueOrDefault("scrollbar-color");
        if (sc != null) { var parts = CssParser.SplitTop(sc.Trim(), ' '); if (parts.Count >= 2) { thumbCol = Cascade.ColorOf(parts[0], b.cs.color); trackCol = Cascade.ColorOf(parts[1], b.cs.color); } }
        if (track != null && track.TryGetValue("background", out var tb)) trackCol = Cascade.ColorOf(tb, b.cs.color);
        if (thumb != null)
        {
            if (thumb.TryGetValue("background", out var hb)) thumbCol = Cascade.ColorOf(hb, b.cs.color);
            if (thumb.TryGetValue("border-radius", out var hr)) rad = Cascade.ParseLen(hr, 16, 16).R(r.w);
        }
        if (trackCol.a > 0) FillRR(r.x, b.bt, r.w, b.PadH, (rad, rad, rad, rad), trackCol);
        FillRR(r.x, r.y, r.w, r.h, (rad, rad, rad, rad), thumbCol);
    }
}
