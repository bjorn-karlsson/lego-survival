// The native UI engine, part 7: inline SVG. The brick icons and the skill-tree
// map are SVG; this draws <svg>, <g>, <path> (every command, arcs included),
// <circle>, <ellipse>, <rect>, <line>, <polyline>, <polygon> and <text> with
// their CSS-computed fill and stroke, honours viewBox and transforms, and finds
// the shape under the pointer for clicks and hovers.
using System.Globalization;

namespace Brickblade;

internal static class Svg
{
    public static double[] ViewBox(Element e)
    {
        var v = e.getAttribute("viewBox");
        if (string.IsNullOrEmpty(v)) return null;
        var p = v.Split(new[] { ' ', ',', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => Js.ParseFloat(s)).ToArray();
        if (p.Length < 4 || p.Any(double.IsNaN) || p[2] <= 0 || p[3] <= 0) return null;
        return p;
    }
    /* the user-space -> viewport transform for an <svg> of size w x h */
    internal static Mat ViewMat(Element svg, double w, double h)
    {
        var vb = ViewBox(svg);
        if (vb == null) return Mat.I;
        string par = svg.getAttribute("preserveAspectRatio") ?? "xMidYMid meet";
        double sx = w / vb[2], sy = h / vb[3];
        if (par.StartsWith("none")) return Mat.S(sx, sy).Mul(Mat.T(-vb[0], -vb[1]));
        bool slice = par.Contains("slice");
        double s = slice ? Math.Max(sx, sy) : Math.Min(sx, sy);
        double tx = 0, ty = 0;
        if (par.Contains("xMid")) tx = (w - vb[2] * s) / 2; else if (par.Contains("xMax")) tx = w - vb[2] * s;
        if (par.Contains("YMid")) ty = (h - vb[3] * s) / 2; else if (par.Contains("YMax")) ty = h - vb[3] * s;
        return Mat.T(tx, ty).Mul(Mat.S(s, s)).Mul(Mat.T(-vb[0], -vb[1]));
    }
    static readonly Dictionary<string, Mat> tfCache = new();
    static Mat TransformAttr(string t)
    {
        if (string.IsNullOrEmpty(t)) return Mat.I;
        if (tfCache.TryGetValue(t, out var m0)) return m0;
        var m = Mat.I;
        int i = 0;
        while (i < t.Length)
        {
            int p = t.IndexOf('(', i); if (p < 0) break;
            int q = t.IndexOf(')', p); if (q < 0) break;
            string fn = t.Substring(i, p - i).Trim().TrimStart(',').Trim();
            var a = t.Substring(p + 1, q - p - 1).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => Js.ParseFloat(s)).ToArray();
            i = q + 1;
            switch (fn)
            {
                case "translate": m = m.Mul(Mat.T(a[0], a.Length > 1 ? a[1] : 0)); break;
                case "scale": m = m.Mul(Mat.S(a[0], a.Length > 1 ? a[1] : a[0])); break;
                case "rotate":
                    if (a.Length >= 3) m = m.Mul(Mat.T(a[1], a[2])).Mul(Mat.R(a[0] * Math.PI / 180)).Mul(Mat.T(-a[1], -a[2]));
                    else m = m.Mul(Mat.R(a[0] * Math.PI / 180));
                    break;
                case "skewX": m = m.Mul(new Mat { a = 1, d = 1, c = Math.Tan(a[0] * Math.PI / 180) }); break;
                case "skewY": m = m.Mul(new Mat { a = 1, d = 1, b = Math.Tan(a[0] * Math.PI / 180) }); break;
                case "matrix": if (a.Length >= 6) m = m.Mul(new Mat { a = a[0], b = a[1], c = a[2], d = a[3], e = a[4], f = a[5] }); break;
            }
        }
        if (tfCache.Count > 5000) tfCache.Clear();
        tfCache[t] = m;
        return m;
    }

    /* ---- path data -> polylines (in user space) ---- */
    sealed class Poly { public List<(double x, double y)> pts = new(); public bool closed; }
    static readonly Dictionary<string, List<Poly>> pathCache = new();
    static List<Poly> PathPolys(string d)
    {
        if (d == null) return new();
        if (pathCache.TryGetValue(d, out var c)) return c;
        var l = new List<Poly>();
        try { ParsePath(d, l); } catch { }
        if (pathCache.Count > 20000) pathCache.Clear();
        pathCache[d] = l;
        return l;
    }
    static void ParsePath(string d, List<Poly> outl)
    {
        int i = 0; char cmd = ' ';
        double x = 0, y = 0, sx = 0, sy = 0, lcx = 0, lcy = 0; char lastCmd = ' ';
        Poly cur = null;
        void Ws() { while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++; }
        bool NumNext() { Ws(); return i < d.Length && (char.IsDigit(d[i]) || d[i] == '-' || d[i] == '+' || d[i] == '.'); }
        double N()
        {
            Ws();
            int st = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.' && !dot)) { if (d[i] == '.') dot = true; i++; }
            if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) { i++; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++; while (i < d.Length && char.IsDigit(d[i])) i++; }
            return double.Parse(d.AsSpan(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        double Flag() { Ws(); char c = d[i++]; return c == '1' ? 1 : 0; }
        void Move(double nx, double ny) { cur = new Poly(); cur.pts.Add((nx, ny)); outl.Add(cur); x = sx = nx; y = sy = ny; }
        void Line(double nx, double ny) { if (cur == null) Move(x, y); cur.pts.Add((nx, ny)); x = nx; y = ny; }
        void Cubic(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            if (cur == null) Move(x, y);
            double x0 = x, y0 = y;
            int n = Math.Clamp((int)(Math.Sqrt(Math.Abs(x3 - x0) + Math.Abs(y3 - y0) + Math.Abs(x1 - x0) + Math.Abs(y2 - y3)) * 2), 6, 24);
            for (int k = 1; k <= n; k++)
            {
                double t = (double)k / n, u = 1 - t;
                cur.pts.Add((u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3, u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3));
            }
            x = x3; y = y3;
        }
        void Quad(double x1, double y1, double x2, double y2)
        {
            if (cur == null) Move(x, y);
            double x0 = x, y0 = y;
            int n = Math.Clamp((int)(Math.Sqrt(Math.Abs(x2 - x0) + Math.Abs(y2 - y0)) * 2), 5, 20);
            for (int k = 1; k <= n; k++) { double t = (double)k / n, u = 1 - t; cur.pts.Add((u * u * x0 + 2 * u * t * x1 + t * t * x2, u * u * y0 + 2 * u * t * y1 + t * t * y2)); }
            x = x2; y = y2;
        }
        void Arc(double rx, double ry, double rot, bool large, bool sweep, double x2, double y2)
        {
            if (cur == null) Move(x, y);
            double x1 = x, y1 = y;
            if (rx == 0 || ry == 0 || x1 == x2 && y1 == y2) { Line(x2, y2); return; }
            rx = Math.Abs(rx); ry = Math.Abs(ry);
            double phi = rot * Math.PI / 180, cp = Math.Cos(phi), sp = Math.Sin(phi);
            double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2;
            double x1p = cp * dx + sp * dy, y1p = -sp * dx + cp * dy;
            double lam = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lam > 1) { double s = Math.Sqrt(lam); rx *= s; ry *= s; }
            double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            double co = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
            double cxp = co * rx * y1p / ry, cyp = -co * ry * x1p / rx;
            double cx = cp * cxp - sp * cyp + (x1 + x2) / 2, cy = sp * cxp + cp * cyp + (y1 + y2) / 2;
            double Ang(double ux, double uy, double vx, double vy) { double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy); return a; }
            double th1 = Ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
            double dth = Ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
            if (!sweep && dth > 0) dth -= 2 * Math.PI; else if (sweep && dth < 0) dth += 2 * Math.PI;
            int n = Math.Clamp((int)(Math.Abs(dth) * Math.Max(rx, ry) / 2), 6, 48);
            for (int k = 1; k <= n; k++)
            {
                double t = th1 + dth * k / n;
                double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
                cur.pts.Add((cp * ex - sp * ey + cx, sp * ex + cp * ey + cy));
            }
            x = x2; y = y2;
        }
        while (true)
        {
            Ws();
            if (i >= d.Length) break;
            char ch = d[i];
            if (char.IsLetter(ch) && ch != 'e' && ch != 'E') { cmd = ch; i++; }
            else if (cmd == ' ') { i++; continue; }
            bool rel = char.IsLower(cmd);
            double ox = rel ? x : 0, oy = rel ? y : 0;
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M': { double a = N() + ox, b = N() + oy; Move(a, b); cmd = rel ? 'l' : 'L'; lastCmd = 'M'; continue; }
                case 'L': { double a = N() + ox, b = N() + oy; Line(a, b); break; }
                case 'H': { double a = N() + (rel ? x : 0); Line(a, y); break; }
                case 'V': { double b = N() + (rel ? y : 0); Line(x, b); break; }
                case 'C':
                    {
                        double a1 = N() + ox, b1 = N() + oy, a2 = N() + ox, b2 = N() + oy, a3 = N() + ox, b3 = N() + oy;
                        Cubic(a1, b1, a2, b2, a3, b3); lcx = a2; lcy = b2; lastCmd = 'C'; continue;
                    }
                case 'S':
                    {
                        double a1 = lastCmd == 'C' ? 2 * x - lcx : x, b1 = lastCmd == 'C' ? 2 * y - lcy : y;
                        double a2 = N() + ox, b2 = N() + oy, a3 = N() + ox, b3 = N() + oy;
                        Cubic(a1, b1, a2, b2, a3, b3); lcx = a2; lcy = b2; lastCmd = 'C'; continue;
                    }
                case 'Q':
                    {
                        double a1 = N() + ox, b1 = N() + oy, a2 = N() + ox, b2 = N() + oy;
                        Quad(a1, b1, a2, b2); lcx = a1; lcy = b1; lastCmd = 'Q'; continue;
                    }
                case 'T':
                    {
                        double a1 = lastCmd == 'Q' ? 2 * x - lcx : x, b1 = lastCmd == 'Q' ? 2 * y - lcy : y;
                        double a2 = N() + ox, b2 = N() + oy;
                        Quad(a1, b1, a2, b2); lcx = a1; lcy = b1; lastCmd = 'Q'; continue;
                    }
                case 'A':
                    {
                        double rx = N(), ry = N(), rot = N(); bool la = Flag() == 1, sw = Flag() == 1;
                        double a = N() + ox, b = N() + oy;
                        Arc(rx, ry, rot, la, sw, a, b); break;
                    }
                case 'Z':
                    if (cur != null) { cur.closed = true; x = sx; y = sy; cur = null; }
                    lastCmd = 'Z';
                    /* a command after Z starts at the subpath's start */
                    continue;
                default: i++; continue;
            }
            lastCmd = char.ToUpperInvariant(cmd);
            if (!NumNext()) { }
        }
    }
    static double A(Element e, string k, double def = 0) { var v = e.getAttribute(k); if (v == null) return def; var d = Js.ParseFloat(v); return double.IsNaN(d) ? def : d; }
    /* the geometry of one shape element, in its user space */
    static List<Poly> Shape(Element e, out bool closedShape)
    {
        closedShape = true;
        switch (e.localName)
        {
            case "path": closedShape = false; return PathPolys(e.getAttribute("d"));
            case "circle": return Ellipse(A(e, "cx"), A(e, "cy"), A(e, "r"), A(e, "r"));
            case "ellipse": return Ellipse(A(e, "cx"), A(e, "cy"), A(e, "rx"), A(e, "ry"));
            case "rect":
                {
                    double x = A(e, "x"), y = A(e, "y"), w = A(e, "width"), h = A(e, "height");
                    double rx = A(e, "rx", double.NaN), ry = A(e, "ry", double.NaN);
                    if (double.IsNaN(rx)) rx = double.IsNaN(ry) ? 0 : ry; if (double.IsNaN(ry)) ry = rx;
                    rx = Math.Min(rx, w / 2); ry = Math.Min(ry, h / 2);
                    var p = new Poly { closed = true };
                    if (rx <= 0) { p.pts.Add((x, y)); p.pts.Add((x + w, y)); p.pts.Add((x + w, y + h)); p.pts.Add((x, y + h)); }
                    else
                    {
                        void C(double cx, double cy, double a0) { for (int k = 0; k <= 6; k++) { double a = a0 + Math.PI / 2 * k / 6; p.pts.Add((cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry)); } }
                        C(x + rx, y + ry, Math.PI); C(x + w - rx, y + ry, -Math.PI / 2); C(x + w - rx, y + h - ry, 0); C(x + rx, y + h - ry, Math.PI / 2);
                    }
                    return new List<Poly> { p };
                }
            case "line":
                {
                    closedShape = false;
                    var p = new Poly(); p.pts.Add((A(e, "x1"), A(e, "y1"))); p.pts.Add((A(e, "x2"), A(e, "y2")));
                    return new List<Poly> { p };
                }
            case "polyline": case "polygon":
                {
                    var nums = (e.getAttribute("points") ?? "").Split(new[] { ' ', ',', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => Js.ParseFloat(s)).ToArray();
                    var p = new Poly { closed = e.localName == "polygon" };
                    for (int k = 0; k + 1 < nums.Length; k += 2) p.pts.Add((nums[k], nums[k + 1]));
                    closedShape = p.closed;
                    return new List<Poly> { p };
                }
        }
        return null;
    }
    static List<Poly> Ellipse(double cx, double cy, double rx, double ry)
    {
        var p = new Poly { closed = true };
        int n = Math.Clamp((int)(Math.Max(rx, ry) * 1.2), 16, 64);
        for (int k = 0; k < n; k++) { double a = k * Math.PI * 2 / n; p.pts.Add((cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry)); }
        return new List<Poly> { p };
    }

    /* ---- painting ---- */
    static Ctx g;
    public static void Paint(Ctx ctx, Element svg, Mat m, double w, double h, double alpha, CStyle cs)
    {
        g = ctx;
        var vm = m.Mul(ViewMat(svg, w, h));
        foreach (var n in svg.childNodes) if (n is Element e) Draw(e, vm, alpha);
    }
    static void Draw(Element e, Mat m, double alpha)
    {
        var cs = e.cs;
        if (cs == null || cs.display == Disp.None) return;
        string ln = e.localName;
        if (ln is "defs" or "title" or "desc" or "style" or "script" or "clipPath" or "mask" or "linearGradient" or "radialGradient" or "symbol" or "marker" or "filter") return;
        var t = e.getAttribute("transform");
        if (t != null) m = m.Mul(TransformAttr(t));
        if (cs.transform != null) { /* CSS transforms on SVG elements: origin at 0,0 of user space */
            var tm = Mat.I; foreach (var op in cs.transform) tm = op.f switch { "t" => tm.Mul(Mat.T(op.a.R(0), op.b.R(0))), "s" => tm.Mul(Mat.S(op.v1, op.v2)), "r" => tm.Mul(Mat.R(op.v1)), _ => tm }; m = m.Mul(tm); }
        double a = alpha * cs.opacity;
        if (a <= 0) return;
        if (ln is "g" or "svg" or "a")
        {
            if (ln == "svg") { double w = A(e, "width", 0), h = A(e, "height", 0); m = m.Mul(Mat.T(A(e, "x"), A(e, "y"))).Mul(ViewMat(e, w, h)); }
            foreach (var n in e.childNodes) if (n is Element k) Draw(k, m, a);
            return;
        }
        if (cs.hidden) return;
        if (ln is "text") { DrawText(e, m, a); return; }
        var polys = Shape(e, out bool closedShape);
        if (polys == null || polys.Count == 0) return;
        g.setTransform(m.a, m.b, m.c, m.d, m.e, m.f);
        /* fill (paths fill too: an open subpath fills as if closed) */
        if (cs.fill.kind == 1 && cs.fill.col.a > 0 && ln != "line" && ln != "polyline" || cs.fill.kind == 1 && ln == "polyline" && cs.fill.col.a > 0)
        {
            g.beginPath();
            foreach (var p in polys) { if (p.pts.Count < 2) continue; g.moveTo(p.pts[0].x, p.pts[0].y); for (int k = 1; k < p.pts.Count; k++) g.lineTo(p.pts[k].x, p.pts[k].y); g.closePath(); }
            g.globalAlpha = Math.Clamp(a * cs.fillOp, 0, 1);
            g.fillStyle = cs.fill.col;
            g.fill(cs.fillRule == "evenodd" ? "evenodd" : "nonzero");
        }
        if (cs.stroke.kind == 1 && cs.stroke.col.a > 0)
        {
            double sw = cs.strokeW.R(100);
            if (sw <= 0) return;
            if (cs.nonScaling) { double k = m.Scale; if (k > 0) sw = sw * Paint2DPR / k; }
            g.beginPath();
            foreach (var p in polys) { if (p.pts.Count < 2) continue; g.moveTo(p.pts[0].x, p.pts[0].y); for (int k = 1; k < p.pts.Count; k++) g.lineTo(p.pts[k].x, p.pts[k].y); if (p.closed) g.closePath(); }
            g.globalAlpha = Math.Clamp(a * cs.strokeOp, 0, 1);
            g.strokeStyle = cs.stroke.col; g.lineWidth = sw;
            g.lineCap = cs.cap; g.lineJoin = cs.join; g.miterLimit = cs.strokeMiter;
            g.setLineDash(cs.dash ?? Array.Empty<double>()); g.lineDashOffset = cs.dashOff;
            g.stroke();
            g.setLineDash(Array.Empty<double>());
        }
    }
    public static double Paint2DPR => Dom.Scale;
    static void DrawText(Element e, Mat m, double alpha)
    {
        var cs = e.cs;
        string s = e.textContent;
        if (string.IsNullOrEmpty(s)) return;
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        double x = A(e, "x"), y = A(e, "y") + A(e, "dy");
        x += A(e, "dx");
        g.setTransform(m.a, m.b, m.c, m.d, m.e, m.f);
        var f = cs.Font;
        g.font = f.src;
        g.textAlign = cs.textAnchor == "middle" ? "center" : cs.textAnchor == "end" ? "right" : "left";
        g.textBaseline = cs.baseline is "middle" or "central" ? "middle" : cs.baseline is "hanging" or "text-before-edge" ? "top" : "alphabetic";
        if (cs.stroke.kind == 1 && cs.stroke.col.a > 0)
        {
            g.globalAlpha = Math.Clamp(alpha * cs.strokeOp, 0, 1);
            g.strokeStyle = cs.stroke.col; g.lineWidth = cs.strokeW.R(0); g.lineJoin = "round";
            g.strokeText(s, x, y);
        }
        if (cs.fill.kind == 1 && cs.fill.col.a > 0)
        {
            g.globalAlpha = Math.Clamp(alpha * cs.fillOp, 0, 1);
            g.fillStyle = cs.fill.col;
            g.fillText(s, x, y);
        }
    }

    /* ---- hit testing: the topmost shape under (hx, hy) device px ---- */
    public static Element Hit(Element svg, Mat m, double w, double h, double hx, double hy)
    {
        if (svg.cs != null && svg.cs.noPointer) return null;
        var vm = m.Mul(ViewMat(svg, w, h));
        Element found = null;
        foreach (var n in svg.childNodes) if (n is Element e) { var r = HitEl(e, vm, hx, hy); if (r != null) found = r; }
        if (found != null) return found;
        /* the svg's own box */
        if (m.Inv(out var inv)) { var (lx, ly) = inv.Ap(hx, hy); if (lx >= 0 && ly >= 0 && lx < w && ly < h) return svg; }
        return null;
    }
    static Element HitEl(Element e, Mat m, double hx, double hy)
    {
        var cs = e.cs;
        if (cs == null || cs.display == Disp.None) return null;
        string ln = e.localName;
        if (ln is "defs" or "title" or "desc" or "style") return null;
        var t = e.getAttribute("transform");
        if (t != null) m = m.Mul(TransformAttr(t));
        if (ln is "g" or "svg" or "a")
        {
            Element found = null;
            foreach (var n in e.childNodes) if (n is Element k) { var r = HitEl(k, m, hx, hy); if (r != null) found = r; }
            return found;
        }
        if (cs.noPointer || cs.hidden) return null;
        if (!m.Inv(out var inv)) return null;
        var (lx, ly) = inv.Ap(hx, hy);
        if (ln == "text")
        {
            var f = cs.Font; string s = e.textContent ?? ""; double tw = Ctx.Measure(s, f);
            double x = A(e, "x"), y = A(e, "y");
            double x0 = cs.textAnchor == "middle" ? x - tw / 2 : cs.textAnchor == "end" ? x - tw : x;
            double y0 = cs.baseline is "middle" or "central" ? y - f.px / 2 : y - f.Ascent;
            return lx >= x0 && lx <= x0 + tw && ly >= y0 && ly <= y0 + f.px * 1.2 ? e : null;
        }
        var polys = Shape(e, out _);
        if (polys == null) return null;
        bool fillable = ln != "line";
        if (fillable && (cs.fill.kind != 0 || e.getAttribute("pointer-events") == "all" || cs.spec?.GetValueOrDefault("pointer-events") is "all" or "fill" or "visible") && Inside(polys, lx, ly)) return e;
        if (cs.stroke.kind != 0 || ln == "line")
        {
            double sw = Math.Max(1, cs.strokeW.R(0));
            if (cs.nonScaling) { double k = m.Scale; if (k > 0) sw = sw * Dom.Scale / k; }
            double r2 = sw / 2 + 0.5 / Math.Max(1e-6, m.Scale);
            foreach (var p in polys)
                for (int k = 0; k + 1 < p.pts.Count + (p.closed ? 1 : 0); k++)
                {
                    var a = p.pts[k]; var b = p.pts[(k + 1) % p.pts.Count];
                    if (SegDist(lx, ly, a.x, a.y, b.x, b.y) <= r2) return e;
                }
        }
        return null;
    }
    static bool Inside(List<Poly> polys, double x, double y)
    {
        int wind = 0;
        foreach (var p in polys)
        {
            int n = p.pts.Count;
            if (n < 3) continue;
            for (int i = 0; i < n; i++)
            {
                var a = p.pts[i]; var b = p.pts[(i + 1) % n];
                if (a.y <= y) { if (b.y > y && (b.x - a.x) * (y - a.y) - (x - a.x) * (b.y - a.y) > 0) wind++; }
                else if (b.y <= y && (b.x - a.x) * (y - a.y) - (x - a.x) * (b.y - a.y) < 0) wind--;
            }
        }
        return wind != 0;
    }
    static double SegDist(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 > 0 ? Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1) : 0;
        double qx = ax + dx * t - px, qy = ay + dy * t - py;
        return Math.Sqrt(qx * qx + qy * qy);
    }
}
