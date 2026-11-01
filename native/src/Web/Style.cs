// The native UI engine, part 4: the cascade and computed values. Each element's
// matched rules, inline style and (for SVG) presentation attributes are sorted
// the way a browser sorts them, var() is substituted, shorthands are expanded,
// and the result is a CStyle of numbers the layout and the painter read.
// Identical results are shared, so a map of two thousand identical nodes
// computes its style a handful of times, not two thousand.
using System.Text;

namespace Brickblade;

public enum Disp : byte { None, Block, Inline, InlineBlock, Flex, InlineFlex, Grid, InlineGrid, ListItem, Contents }
public enum Pos : byte { Static, Relative, Absolute, Fixed, Sticky }
public enum Ovf : byte { Visible, Hidden, Auto, Scroll, Clip }

/* a length: auto, none, px + %, or an expression of the percentage basis */
public struct Len
{
    public byte kind;            /* 0 auto, 1 value, 2 none, 3 content/min/max-content keywords */
    public double px, pct; public Func<double, double> fn; public string kw;
    public static readonly Len Auto = new() { kind = 0 }, None = new() { kind = 2 }, Zero = new() { kind = 1 };
    public static Len Px(double v) => new() { kind = 1, px = v };
    public static Len Pct(double v) => new() { kind = 1, pct = v };
    public bool IsAuto => kind == 0; public bool IsNone => kind == 2; public bool IsVal => kind == 1;
    public bool HasPct => kind == 1 && (pct != 0 || fn != null);
    public double R(double basis)
    {
        if (kind != 1) return 0;
        if (fn != null) return fn(double.IsNaN(basis) ? 0 : basis);
        return pct == 0 ? px : px + pct * (double.IsNaN(basis) ? 0 : basis) / 100;
    }
    /* resolved, or NaN when it cannot be (auto, or a % of an unknown basis) */
    public double Def(double basis) => kind != 1 ? double.NaN : (HasPct && double.IsNaN(basis)) ? double.NaN : R(basis);
}
public sealed class Shadow { public bool inset; public Len x, y; public double blur, spread; public Col col; public bool curCol; }
public sealed class GStop { public Col col; public Len pos; public bool hasPos; }
public sealed class BgImage
{
    public bool radial, repeating, circle; public double angle = 180; public string toSide;
    public string size = "farthest-corner"; public Len atX = Len.Pct(50), atY = Len.Pct(50);
    public List<GStop> stops = new(); public string url;
}
public sealed class TOp { public string f; public Len a, b; public double v1, v2; public double[] mat; }
public sealed class GTrack { public string kind; public double v; public Len len; public GTrack min, max; public int repeat; public bool autoFit, autoFill; public List<GTrack> inner; }
public sealed class Trans { public string prop; public double dur, delay; public string ease; }
public sealed class SvgPaint { public byte kind; public Col col; public string url; }   /* 0 none, 1 color, 2 currentColor, 3 url */

public sealed class CStyle
{
    static int uids; public readonly int uid = ++uids;
    public Disp display = Disp.Inline; public Pos position; public bool borderBox;
    public Len top = Len.Auto, right = Len.Auto, bottom = Len.Auto, left = Len.Auto;
    public Len width = Len.Auto, height = Len.Auto, minW = Len.Auto, minH = Len.Auto, maxW = Len.None, maxH = Len.None;
    public Len mT = Len.Zero, mR = Len.Zero, mB = Len.Zero, mL = Len.Zero, pT = Len.Zero, pR = Len.Zero, pB = Len.Zero, pL = Len.Zero;
    public double bT, bR, bB, bL; public Col cT, cR, cB, cL; public byte sT, sR, sB, sL;   /* style: 0 none 1 solid 2 dotted 3 dashed 4 double */
    public Len rTL = Len.Zero, rTR = Len.Zero, rBR = Len.Zero, rBL = Len.Zero;
    public double outlineW, outlineOff; public Col outlineCol; public byte outlineS;
    public byte flexDir; /* 0 row 1 column 2 row-reverse 3 column-reverse */
    public bool wrap; public string justify = "normal", alignItems = "normal", alignSelf = "auto", alignContent = "normal", justifyItems = "normal";
    public bool justifySafe;
    public double grow, shrink = 1; public Len basis = Len.Auto; public int order;
    public Len rowGap = Len.Zero, colGap = Len.Zero;
    public List<GTrack> gridCols, gridRows; public string gcStart, gcEnd, grStart, grEnd; public string gridFlow = "row";
    public Ovf ovX, ovY; public int z; public bool zAuto = true;
    public double opacity = 1; public bool hidden; public bool noPointer;
    public List<TOp> transform; public Len toX = Len.Pct(50), toY = Len.Pct(50);
    public Col bg; public List<BgImage> bgImg; public List<(Len w, Len h, string kw)> bgSize; public List<(Len x, Len y)> bgPos; public List<string> bgRepeat; public string bgClip = "border-box";
    public List<Shadow> boxShadow, textShadow;
    public Col color = new Col(0, 0, 0); public string family = "sans-serif"; public double fontSize = 16; public int weight = 400; public bool italic;
    public double lineHeight = -1; public bool lhNum; public double letter; public string textAlign = "start", textTransform = "none", whiteSpace = "normal";
    public string deco = "none", vAlign = "baseline", wordBreak = "normal", textOverflow = "clip";
    public string cursor = "auto", listStyle = "disc", listPos = "outside";
    public string content; public string boxSizingRaw;
    public List<(string f, double v)> filter; public double backdropBlur;
    public List<Trans> transition; public string anim; public double animDur, animDelay; public bool animInfinite, animAlternate; public double animCount = 1; public string animEase = "ease";
    public SvgPaint fill = new() { kind = 1, col = new Col(0, 0, 0) }, stroke = new() { kind = 0 };
    public Len strokeW = Len.Px(1); public double fillOp = 1, strokeOp = 1, strokeMiter = 4; public string cap = "butt", join = "miter", fillRule = "nonzero";
    public double[] dash; public double dashOff; public bool nonScaling; public string textAnchor = "start", baseline = "auto";
    public double zoom = 1; public string objectFit; public double aspect;
    public Dictionary<string, string> vars;
    public Dictionary<string, string> spec;   /* the substituted declared values, for getComputedStyle */
    public bool hasBefore, hasAfter;
    internal FontSpec Font => FontSpec.Of(family, weight, fontSize);
    public bool IsBlockish => display is Disp.Block or Disp.Flex or Disp.Grid or Disp.ListItem;
    public bool IsInlineLevel => display is Disp.Inline or Disp.InlineBlock or Disp.InlineFlex or Disp.InlineGrid;
    public bool Positioned => position != Pos.Static;
    public bool OutOfFlow => position is Pos.Absolute or Pos.Fixed;
    public bool Scrolls => ovX is Ovf.Auto or Ovf.Scroll || ovY is Ovf.Auto or Ovf.Scroll;
    public bool Clips => ovX != Ovf.Visible || ovY != Ovf.Visible;
    public double LineH(FontSpec f) => lineHeight < 0 ? f.LineNormal : lhNum ? lineHeight * fontSize : lineHeight;
}

public sealed class CStyleView
{
    readonly Element e;
    public CStyleView(Element e) { this.e = e; }
    public string getPropertyValue(string k) { Dom.EnsureLayout(); return e.cs?.spec?.GetValueOrDefault(k) ?? ""; }
    public string display => getPropertyValue("display");
    public string fontSize { get { Dom.EnsureLayout(); return e.cs != null ? Js.Num(e.cs.fontSize) + "px" : ""; } }
}

public static class Cascade
{
    static readonly HashSet<string> Inherited = new()
    {
        "color", "font-family", "font-size", "font-weight", "font-style", "line-height", "letter-spacing", "text-align", "text-transform",
        "white-space", "cursor", "visibility", "pointer-events", "list-style-type", "list-style-position", "text-shadow", "word-break", "overflow-wrap",
        "fill", "stroke", "stroke-width", "stroke-opacity", "fill-opacity", "stroke-linecap", "stroke-linejoin", "stroke-dasharray", "stroke-dashoffset",
        "stroke-miterlimit", "fill-rule", "text-anchor", "dominant-baseline", "font-variant", "direction", "text-decoration", "user-select",
    };
    /* the user-agent stylesheet: what a browser gives every page before the game's own CSS */
    public const string UA = @"
html,body,div,section,aside,header,footer,main,nav,article,p,ul,ol,li,h1,h2,h3,h4,h5,h6,form,hr,pre,blockquote,figure,details,summary,fieldset,table{display:block}
li{display:list-item} head,style,script,title,template,meta,link{display:none}
body{margin:8px} p{margin:1em 0} ul,ol{margin:1em 0;padding-left:40px} ol{list-style-type:decimal}
h1{font-size:2em;margin:.67em 0;font-weight:bold} h2{font-size:1.5em;margin:.83em 0;font-weight:bold} h3{font-size:1.17em;margin:1em 0;font-weight:bold}
h4{margin:1.33em 0;font-weight:bold} h5{font-size:.83em;margin:1.67em 0;font-weight:bold} h6{font-size:.67em;margin:2.33em 0;font-weight:bold}
b,strong{font-weight:bold} i,em{font-style:italic} code,kbd,pre{font-family:monospace} small{font-size:smaller} u{text-decoration:underline}
hr{border:1px inset #888;margin:.5em auto} pre{white-space:pre}
button{display:inline-block;padding:1px 6px;border:2px outset #ddd;background:#efefef;color:#000;font:13.33px sans-serif;text-align:center;cursor:default}
input{display:inline-block;padding:1px 2px;border:2px inset #ddd;background:#fff;color:#000;font:13.33px sans-serif;cursor:text}
input[type=range]{width:129px;height:16px;padding:0;border:0;background:transparent;cursor:default}
canvas,img,svg{display:inline-block} svg{overflow:hidden} [hidden]{display:none}
svg *{display:inline}
";
    static readonly Dictionary<string, CStyle> share = new();
    static int shareGen;
    public static void ClearShare() { share.Clear(); shareGen++; }
    static readonly List<CssRule> scratch = new();
    static readonly List<(string k, string v, bool imp, int spec, int order)> decl = new();
    /* origin: the user-agent sheet below everything the page writes */
    static int Origin(int order, int spec) => spec >= 1000000 || spec < 0 ? 1 : order < Sheet.uaCount ? 0 : 1;

    /* the computed style of e (and its ::before / ::after) given its parent's */
    public static void Compute(Element e, CStyle parent, CStyle root)
    {
        Sheet.Matching(e, scratch);
        e.cs = Build(e, parent, root, null);
        e.csBefore = e.cs.hasBefore ? Build(e, e.cs, root, "before") : null;
        e.csAfter = e.cs.hasAfter ? Build(e, e.cs, root, "after") : null;
    }
    static CStyle Build(Element e, CStyle parent, CStyle root, string pseudo)
    {
        decl.Clear();
        bool hasB = false, hasA = false;
        var key = new StringBuilder();
        key.Append(pseudo).Append('|').Append(e.localName).Append(e.isSvg ? 's' : 'h').Append(e.parentNode?.isSvg == true ? 's' : 'h').Append('|').Append(parent?.uid ?? 0).Append('|');
        foreach (var r in scratch)
        {
            if (r.sel.pseudoEl != pseudo)
            {
                if (pseudo == null && r.sel.pseudoEl == "before") hasB = true;
                if (pseudo == null && r.sel.pseudoEl == "after") hasA = true;
                continue;
            }
            key.Append(r.order).Append(',');
            foreach (var (k, v, imp) in r.decls) decl.Add((k, v, imp, r.sel.spec, r.order));
        }
        if (pseudo == null)
        {
            /* SVG presentation attributes: the weakest author style there is */
            if (e.isSvg)
                foreach (var a in e.attrOrder)
                    if (SvgPresentation.Contains(a)) { decl.Insert(0, (a, e.attrs[a], false, -1, -1)); key.Append(a).Append('=').Append(e.attrs[a]).Append(';'); }
            foreach (var (k, v, imp) in e.style.decls) decl.Add((k, v, imp, 1000000, 0));
            key.Append('|').Append(e.style.cssText);
            /* attr() in content reads attributes, and so does a canvas's intrinsic size */
            if (hasB || hasA) foreach (var a in e.attrOrder) if (a.StartsWith("data-")) key.Append(a).Append('=').Append(e.attrs[a]).Append(';');
            if (e.canvas != null) key.Append(e.canvas.width).Append('x').Append(e.canvas.height);
        }
        else foreach (var a in e.attrOrder) if (a.StartsWith("data-")) key.Append(a).Append('=').Append(e.attrs[a]).Append(';');
        key.Append(hasB ? 'B' : '-').Append(hasA ? 'A' : '-');
        string skey = key.ToString();
        if (share.TryGetValue(skey, out var hit)) return hit;
        /* the cascade: importance, then specificity, then order of appearance */
        decl.Sort((x, y) =>
        {
            if (x.imp != y.imp) return x.imp ? 1 : -1;
            int ox = Origin(x.order, x.spec), oy = Origin(y.order, y.spec);
            if (ox != oy) return ox.CompareTo(oy);
            return x.spec != y.spec ? x.spec.CompareTo(y.spec) : x.order.CompareTo(y.order);
        });
        var spec = new Dictionary<string, string>();
        foreach (var d in decl) spec[d.k] = d.v;
        var cs = new CStyle { hasBefore = hasB, hasAfter = hasA };
        /* custom properties inherit */
        Dictionary<string, string> vars = parent?.vars;
        foreach (var kv in spec) if (kv.Key.StartsWith("--")) { if (vars == parent?.vars) vars = vars != null ? new(vars) : new(); vars[kv.Key] = kv.Value.Trim(); }
        cs.vars = vars;
        /* substitute var(), expand shorthands into longhands, in cascade order */
        var longs = new Dictionary<string, string>();
        foreach (var d in decl)
        {
            if (d.k.StartsWith("--")) continue;
            string v = d.v.Contains("var(") ? SubVars(d.v, vars) : d.v;
            Expand(d.k, v, longs, d.spec >= 0 && d.spec < 1000000 && d.order >= Sheet.uaCount);
        }
        cs.spec = longs;
        Fill(cs, longs, parent ?? Root, root ?? cs, e, pseudo);
        if (share.Count > 20000) share.Clear();
        share[skey] = cs;
        return cs;
    }
    static readonly CStyle Root = new() { display = Disp.Block };
    static readonly HashSet<string> SvgPresentation = new()
    {
        "fill", "stroke", "stroke-width", "stroke-opacity", "fill-opacity", "stroke-linecap", "stroke-linejoin", "stroke-dasharray", "stroke-dashoffset",
        "opacity", "font-size", "font-family", "font-weight", "text-anchor", "dominant-baseline", "fill-rule", "visibility", "display", "color",
        "transform-origin", "vector-effect", "pointer-events", "letter-spacing", "stroke-miterlimit",
    };

    public static string SubVars(string v, Dictionary<string, string> vars, int depth = 0)
    {
        if (depth > 8) return v;
        var sb = new StringBuilder();
        int i = 0;
        while (i < v.Length)
        {
            int p = v.IndexOf("var(", i, StringComparison.Ordinal);
            if (p < 0) { sb.Append(v, i, v.Length - i); break; }
            sb.Append(v, i, p - i);
            int d = 0, j = p + 3;
            for (; j < v.Length; j++) { if (v[j] == '(') d++; else if (v[j] == ')') { d--; if (d == 0) break; } }
            string inner = v.Substring(p + 4, Math.Max(0, j - p - 4));
            int comma = inner.IndexOf(',');
            string name = (comma >= 0 ? inner.Substring(0, comma) : inner).Trim();
            string fb = comma >= 0 ? inner.Substring(comma + 1).Trim() : "";
            string val = vars != null && vars.TryGetValue(name, out var x) ? x : fb;
            sb.Append(val.Contains("var(") ? SubVars(val, vars, depth + 1) : val);
            i = j + 1;
        }
        return sb.ToString();
    }

    static readonly string[] Sides = { "top", "right", "bottom", "left" };
    static List<string> Words(string v) => CssParser.SplitTop(v.Trim(), ' ').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    static string[] Four(string v)
    {
        var w = Words(v);
        if (w.Count == 0) return new[] { "0", "0", "0", "0" };
        if (w.Count == 1) return new[] { w[0], w[0], w[0], w[0] };
        if (w.Count == 2) return new[] { w[0], w[1], w[0], w[1] };
        if (w.Count == 3) return new[] { w[0], w[1], w[2], w[1] };
        return new[] { w[0], w[1], w[2], w[3] };
    }
    static bool IsBorderStyle(string w) => w is "none" or "solid" or "dotted" or "dashed" or "double" or "groove" or "ridge" or "inset" or "outset" or "hidden";
    static bool IsWidth(string w) => w is "thin" or "medium" or "thick" || (w.Length > 0 && (char.IsDigit(w[0]) || w[0] == '.' || w[0] == '-') ) || w.StartsWith("calc(");
    static void Border(string side, string v, Dictionary<string, string> o)
    {
        string wd = "medium", st = "none", col = "currentcolor";
        if (v.Trim() == "none" || v.Trim() == "0") { wd = "0"; st = "none"; }
        else foreach (var w in Words(v)) { if (IsBorderStyle(w)) st = w; else if (IsWidth(w)) wd = w; else col = w; }
        foreach (var s in side == null ? Sides : new[] { side })
        { o["border-" + s + "-width"] = wd; o["border-" + s + "-style"] = st; o["border-" + s + "-color"] = col; }
    }
    static void Expand(string k, string v, Dictionary<string, string> o, bool sheetRule)
    {
        v = v.Trim();
        switch (k)
        {
            case "margin": case "padding": { var f = Four(v); for (int i = 0; i < 4; i++) o[k + "-" + Sides[i]] = f[i]; return; }
            case "inset": { var f = Four(v); for (int i = 0; i < 4; i++) o[Sides[i]] = f[i]; return; }
            case "border": Border(null, v, o); return;
            case "border-top": case "border-right": case "border-bottom": case "border-left": Border(k.Substring(7), v, o); return;
            case "border-width": case "border-style": case "border-color":
                { var f = Four(v); string part = k.Substring(7); for (int i = 0; i < 4; i++) o["border-" + Sides[i] + "-" + part] = f[i]; return; }
            case "border-radius":
                {
                    string h = v; int sl = v.IndexOf('/'); if (sl >= 0) h = v.Substring(0, sl);
                    var f = Four(h);
                    o["border-top-left-radius"] = f[0]; o["border-top-right-radius"] = f[1]; o["border-bottom-right-radius"] = f[2]; o["border-bottom-left-radius"] = f[3];
                    return;
                }
            case "outline":
                {
                    string wd = "medium", st = "none", col = "currentcolor";
                    if (v == "none" || v == "0") { wd = "0"; }
                    else foreach (var w in Words(v)) { if (IsBorderStyle(w)) st = w; else if (IsWidth(w)) wd = w; else col = w; }
                    o["outline-width"] = wd; o["outline-style"] = st; o["outline-color"] = col; return;
                }
            case "background":
                {
                    /* the colour is the bare colour among the layers; the images are the gradients and urls */
                    var layers = CssParser.SplitTop(v, ',');
                    var imgs = new List<string>(); string col = "transparent"; string size = null, pos = null, rep = null;
                    foreach (var layer in layers)
                    {
                        string L = layer.Trim(); string img = null;
                        var rest = new List<string>();
                        foreach (var w in Words(L))
                        {
                            if (w.Contains("gradient(") || w.StartsWith("url(")) img = w;
                            else rest.Add(w);
                        }
                        imgs.Add(img ?? "none");
                        /* position / size: "center / 44px 44px" */
                        string r = string.Join(" ", rest);
                        int sl = r.IndexOf('/');
                        if (sl >= 0) { pos = r.Substring(0, sl).Trim(); size = r.Substring(sl + 1).Trim(); r = pos; }
                        foreach (var w in Words(r))
                        {
                            if (w is "repeat" or "no-repeat" or "repeat-x" or "repeat-y" or "space" or "round") rep = w;
                            else if (w is "center" or "top" or "left" or "right" or "bottom" || char.IsDigit(w[0]) || w[0] == '-' || w[0] == '.') pos = (pos == null ? "" : pos + " ") + w;
                            else if (w is "padding-box" or "border-box" or "content-box" or "fixed" or "scroll" or "local") { }
                            else col = w;
                        }
                    }
                    o["background-color"] = col;
                    o["background-image"] = string.Join(",", imgs);
                    o["background-size"] = size ?? "auto"; o["background-position"] = pos ?? "0% 0%"; o["background-repeat"] = rep ?? "repeat";
                    return;
                }
            case "flex":
                {
                    var w = Words(v);
                    if (v == "none") { o["flex-grow"] = "0"; o["flex-shrink"] = "0"; o["flex-basis"] = "auto"; return; }
                    if (v == "auto") { o["flex-grow"] = "1"; o["flex-shrink"] = "1"; o["flex-basis"] = "auto"; return; }
                    if (v == "initial") { o["flex-grow"] = "0"; o["flex-shrink"] = "1"; o["flex-basis"] = "auto"; return; }
                    string g = "1", s = "1", b = "0%"; int n = 0;
                    foreach (var x in w)
                    {
                        bool num = double.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                        if (num && n == 0) { g = x; n = 1; }
                        else if (num && n == 1) { s = x; n = 2; }
                        else b = x;
                    }
                    o["flex-grow"] = g; o["flex-shrink"] = s; o["flex-basis"] = b; return;
                }
            case "flex-flow":
                foreach (var w in Words(v)) { if (w.Contains("wrap")) o["flex-wrap"] = w; else o["flex-direction"] = w; }
                return;
            case "gap": case "grid-gap": { var w = Words(v); o["row-gap"] = w[0]; o["column-gap"] = w.Count > 1 ? w[1] : w[0]; return; }
            case "overflow": { var w = Words(v); o["overflow-x"] = w[0]; o["overflow-y"] = w.Count > 1 ? w[1] : w[0]; return; }
            case "place-items": { var w = Words(v); o["align-items"] = w[0]; o["justify-items"] = w.Count > 1 ? w[1] : w[0]; return; }
            case "place-content": { var w = Words(v); o["align-content"] = w[0]; o["justify-content"] = w.Count > 1 ? w[1] : w[0]; return; }
            case "list-style": foreach (var w in Words(v)) { if (w is "inside" or "outside") o["list-style-position"] = w; else o["list-style-type"] = w; } return;
            case "grid-column": case "grid-row":
                { int sl = v.IndexOf('/'); o[k + "-start"] = (sl >= 0 ? v.Substring(0, sl) : v).Trim(); o[k + "-end"] = sl >= 0 ? v.Substring(sl + 1).Trim() : "auto"; return; }
            case "text-decoration": case "text-decoration-line":
                { string line = "none"; foreach (var w in Words(v)) if (w is "underline" or "line-through" or "overline" or "none") line = w; o["text-decoration"] = line; return; }
            case "font":
                {
                    /* [style] [variant] [weight] size[/line-height] family */
                    var w = Words(v);
                    o["font-style"] = "normal"; o["font-weight"] = "400"; o["line-height"] = "normal";
                    int i = 0;
                    for (; i < w.Count; i++)
                    {
                        var t = w[i];
                        if (t is "italic" or "oblique") o["font-style"] = "italic";
                        else if (t is "normal" or "small-caps") { }
                        else if (t is "bold" or "bolder" or "lighter" || (t.Length == 3 && char.IsDigit(t[0]) && t.EndsWith("00"))) o["font-weight"] = t;
                        else break;
                    }
                    if (i < w.Count)
                    {
                        var sz = w[i]; int sl = sz.IndexOf('/');
                        if (sl >= 0) { o["line-height"] = sz.Substring(sl + 1); sz = sz.Substring(0, sl); }
                        else if (i + 1 < w.Count && w[i + 1].StartsWith("/")) { o["line-height"] = w[i + 1].Substring(1).Length > 0 ? w[i + 1].Substring(1) : (i + 2 < w.Count ? w[++i + 1] : "normal"); i++; }
                        o["font-size"] = ScaleFont(sz, sheetRule);
                        o["font-family"] = string.Join(" ", w.Skip(i + 1));
                    }
                    return;
                }
            case "font-size": o[k] = ScaleFont(v, sheetRule); return;
            case "transition": o["transition"] = v; return;
            case "animation": o["animation"] = v; return;
            case "-webkit-backdrop-filter": o["backdrop-filter"] = v; return;
            default: o[k] = v; return;
        }
    }
    /* TEXT SIZE scales every stylesheet font-size written in px (textScaleRules) */
    static string ScaleFont(string v, bool sheetRule)
    {
        if (!sheetRule || Sheet.TextK == 1) return v;
        var m = System.Text.RegularExpressions.Regex.Match(v, @"^([\d.]+)px$");
        return m.Success ? Js.ToFixed(Js.ToNumber(m.Groups[1].Value) * Sheet.TextK, 2) + "px" : v;
    }

    /* ---- turning declared strings into numbers ---- */
    static void Fill(CStyle c, Dictionary<string, string> d, CStyle p, CStyle root, Element e, string pseudo)
    {
        string G(string k) { if (d.TryGetValue(k, out var v)) { v = v.Trim(); if (v == "inherit") return PV(p, k); if (v == "initial" || v == "unset" && !Inherited.Contains(k)) return null; if (v == "unset") return PV(p, k); return v; } return null; }
        /* inherited first, from the parent */
        c.color = p.color; c.family = p.family; c.fontSize = p.fontSize; c.weight = p.weight; c.italic = p.italic;
        c.lineHeight = p.lineHeight; c.lhNum = p.lhNum; c.letter = p.letter; c.textAlign = p.textAlign; c.textTransform = p.textTransform;
        c.whiteSpace = p.whiteSpace; c.cursor = p.cursor; c.hidden = p.hidden; c.noPointer = p.noPointer; c.listStyle = p.listStyle; c.listPos = p.listPos;
        c.textShadow = p.textShadow; c.wordBreak = p.wordBreak; c.fill = p.fill; c.stroke = p.stroke; c.strokeW = p.strokeW; c.fillOp = p.fillOp; c.strokeOp = p.strokeOp;
        c.cap = p.cap; c.join = p.join; c.dash = p.dash; c.dashOff = p.dashOff; c.strokeMiter = p.strokeMiter; c.fillRule = p.fillRule; c.textAnchor = p.textAnchor;
        c.baseline = p.baseline; c.deco = p.deco;
        string v;
        /* font-size before everything that is measured in em */
        if ((v = G("font-size")) != null)
        {
            if (v.EndsWith("%")) c.fontSize = p.fontSize * Js.ParseFloat(v) / 100;
            else if (v == "smaller") c.fontSize = p.fontSize / 1.2;
            else if (v == "larger") c.fontSize = p.fontSize * 1.2;
            else if (v is "small") c.fontSize = 13; else if (v is "medium") c.fontSize = 16; else if (v is "large") c.fontSize = 18; else if (v is "x-small") c.fontSize = 10;
            else { var L = ParseLen(v, p.fontSize, root.fontSize); if (L.IsVal) c.fontSize = Math.Max(0, L.R(p.fontSize)); }
        }
        double em = c.fontSize, rem = e == Dom.document.documentElement ? c.fontSize : (Dom.RootFont > 0 ? Dom.RootFont : 16);
        Len Ln(string k, Len def) { var s = G(k); if (s == null) return def; var L = ParseLen(s, em, rem); return L.kind == 255 ? def : L; }
        if ((v = G("color")) != null) c.color = ColorOf(v, p.color);
        if ((v = G("font-family")) != null) c.family = v;
        if ((v = G("font-weight")) != null) c.weight = v == "bold" ? 700 : v == "normal" ? 400 : v == "bolder" ? Math.Min(900, p.weight + 300) : v == "lighter" ? Math.Max(100, p.weight - 300) : (int)Js.Or(Js.ParseFloat(v), 400);
        if ((v = G("font-style")) != null) c.italic = v is "italic" or "oblique";
        if ((v = G("line-height")) != null)
        {
            if (v == "normal") { c.lineHeight = -1; c.lhNum = false; }
            else if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)) { c.lineHeight = n; c.lhNum = true; }
            else if (v.EndsWith("%")) { c.lineHeight = c.fontSize * Js.ParseFloat(v) / 100; c.lhNum = false; }
            else { var L = ParseLen(v, em, rem); if (L.IsVal) { c.lineHeight = L.R(0); c.lhNum = false; } }
        }
        if ((v = G("letter-spacing")) != null) c.letter = v == "normal" ? 0 : ParseLen(v, em, rem).R(0);
        if ((v = G("text-align")) != null) c.textAlign = v;
        if ((v = G("text-transform")) != null) c.textTransform = v;
        if ((v = G("white-space")) != null) c.whiteSpace = v;
        if ((v = G("cursor")) != null) c.cursor = v.Split(',').Last().Trim();
        if ((v = G("visibility")) != null) c.hidden = v is "hidden" or "collapse";
        if ((v = G("pointer-events")) != null) c.noPointer = v == "none";
        if ((v = G("list-style-type")) != null) c.listStyle = v;
        if ((v = G("list-style-position")) != null) c.listPos = v;
        if ((v = G("text-shadow")) != null) c.textShadow = v == "none" ? null : Shadows(v, em, rem, c.color);
        if ((v = G("word-break")) != null) c.wordBreak = v;
        if ((v = G("overflow-wrap")) != null && v != "normal") c.wordBreak = "break-word";
        if ((v = G("text-decoration")) != null) c.deco = v;
        /* SVG painting */
        if ((v = G("fill")) != null) c.fill = SvgPaintOf(v, c.color);
        if ((v = G("stroke")) != null) c.stroke = SvgPaintOf(v, c.color);
        if ((v = G("stroke-width")) != null) { var L = ParseLen(v.EndsWith("px") || v.EndsWith("%") ? v : v + "px", em, rem); if (L.IsVal) c.strokeW = L; }
        if ((v = G("fill-opacity")) != null) c.fillOp = Num(v, 1);
        if ((v = G("stroke-opacity")) != null) c.strokeOp = Num(v, 1);
        if ((v = G("stroke-linecap")) != null) c.cap = v;
        if ((v = G("stroke-linejoin")) != null) c.join = v;
        if ((v = G("stroke-miterlimit")) != null) c.strokeMiter = Num(v, 4);
        if ((v = G("stroke-dasharray")) != null) c.dash = v == "none" ? null : v.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Js.ParseFloat(x)).Where(x => !double.IsNaN(x)).ToArray();
        if ((v = G("stroke-dashoffset")) != null) c.dashOff = Js.Or(Js.ParseFloat(v), 0);
        if ((v = G("fill-rule")) != null) c.fillRule = v;
        if ((v = G("text-anchor")) != null) c.textAnchor = v;
        if ((v = G("dominant-baseline")) != null) c.baseline = v;
        if ((v = G("vector-effect")) != null) c.nonScaling = v == "non-scaling-stroke";
        /* not inherited */
        if ((v = G("display")) != null) c.display = DispOf(v, e, pseudo);
        else c.display = pseudo != null ? Disp.Inline : (e.isSvg ? (e.localName == "svg" && !(e.parentNode?.isSvg ?? false) ? Disp.InlineBlock : Disp.Inline) : Disp.Inline);
        if ((v = G("position")) != null) c.position = v switch { "relative" => Pos.Relative, "absolute" => Pos.Absolute, "fixed" => Pos.Fixed, "sticky" or "-webkit-sticky" => Pos.Sticky, _ => Pos.Static };
        if ((v = G("box-sizing")) != null) c.borderBox = v == "border-box";
        c.top = Ln("top", Len.Auto); c.right = Ln("right", Len.Auto); c.bottom = Ln("bottom", Len.Auto); c.left = Ln("left", Len.Auto);
        c.width = Ln("width", Len.Auto); c.height = Ln("height", Len.Auto);
        c.minW = Ln("min-width", Len.Auto); c.minH = Ln("min-height", Len.Auto); c.maxW = Ln("max-width", Len.None); c.maxH = Ln("max-height", Len.None);
        c.mT = Ln("margin-top", Len.Zero); c.mR = Ln("margin-right", Len.Zero); c.mB = Ln("margin-bottom", Len.Zero); c.mL = Ln("margin-left", Len.Zero);
        c.pT = Ln("padding-top", Len.Zero); c.pR = Ln("padding-right", Len.Zero); c.pB = Ln("padding-bottom", Len.Zero); c.pL = Ln("padding-left", Len.Zero);
        byte BS(string s) => s switch { "solid" or "inset" or "outset" or "groove" or "ridge" => 1, "dotted" => 2, "dashed" => 3, "double" => 4, _ => 0 };
        double BW(string s, byte st) { if (st == 0) return 0; if (s == null) return 3; if (s == "thin") return 1; if (s == "medium") return 3; if (s == "thick") return 5; var L = ParseLen(s, em, rem); return Math.Max(0, L.R(0)); }
        c.sT = BS(G("border-top-style") ?? "none"); c.sR = BS(G("border-right-style") ?? "none"); c.sB = BS(G("border-bottom-style") ?? "none"); c.sL = BS(G("border-left-style") ?? "none");
        c.bT = BW(G("border-top-width"), c.sT); c.bR = BW(G("border-right-width"), c.sR); c.bB = BW(G("border-bottom-width"), c.sB); c.bL = BW(G("border-left-width"), c.sL);
        c.cT = ColorOf(G("border-top-color") ?? "currentcolor", c.color); c.cR = ColorOf(G("border-right-color") ?? "currentcolor", c.color);
        c.cB = ColorOf(G("border-bottom-color") ?? "currentcolor", c.color); c.cL = ColorOf(G("border-left-color") ?? "currentcolor", c.color);
        c.rTL = Ln("border-top-left-radius", Len.Zero); c.rTR = Ln("border-top-right-radius", Len.Zero); c.rBR = Ln("border-bottom-right-radius", Len.Zero); c.rBL = Ln("border-bottom-left-radius", Len.Zero);
        c.outlineS = BS(G("outline-style") ?? "none"); c.outlineW = BW(G("outline-width"), c.outlineS); c.outlineCol = ColorOf(G("outline-color") ?? "currentcolor", c.color);
        if ((v = G("outline-offset")) != null) c.outlineOff = ParseLen(v, em, rem).R(0);
        if ((v = G("flex-direction")) != null) c.flexDir = v switch { "column" => 1, "row-reverse" => 2, "column-reverse" => 3, _ => 0 };
        if ((v = G("flex-wrap")) != null) c.wrap = v is "wrap" or "wrap-reverse";
        if ((v = G("justify-content")) != null) { c.justifySafe = v.StartsWith("safe "); c.justify = v.Replace("safe ", "").Replace("unsafe ", "").Trim(); }
        if ((v = G("align-items")) != null) c.alignItems = v.Replace("safe ", "").Trim();
        if ((v = G("align-self")) != null) c.alignSelf = v;
        if ((v = G("align-content")) != null) c.alignContent = v;
        if ((v = G("justify-items")) != null) c.justifyItems = v;
        if ((v = G("flex-grow")) != null) c.grow = Num(v, 0);
        if ((v = G("flex-shrink")) != null) c.shrink = Num(v, 1);
        if ((v = G("flex-basis")) != null) c.basis = v == "auto" ? Len.Auto : v == "content" ? new Len { kind = 3, kw = "content" } : ParseLen(v, em, rem);
        if ((v = G("order")) != null) c.order = (int)Num(v, 0);
        c.rowGap = Ln("row-gap", Len.Zero); c.colGap = Ln("column-gap", Len.Zero);
        if (c.rowGap.kind != 1) c.rowGap = Len.Zero; if (c.colGap.kind != 1) c.colGap = Len.Zero;
        if ((v = G("grid-template-columns")) != null && v != "none") c.gridCols = Tracks(v, em, rem);
        if ((v = G("grid-template-rows")) != null && v != "none") c.gridRows = Tracks(v, em, rem);
        c.gcStart = G("grid-column-start"); c.gcEnd = G("grid-column-end"); c.grStart = G("grid-row-start"); c.grEnd = G("grid-row-end");
        if ((v = G("grid-auto-flow")) != null) c.gridFlow = v;
        Ovf OV(string s) => s switch { "hidden" => Ovf.Hidden, "auto" or "overlay" => Ovf.Auto, "scroll" => Ovf.Scroll, "clip" => Ovf.Clip, _ => Ovf.Visible };
        c.ovX = OV(G("overflow-x") ?? "visible"); c.ovY = OV(G("overflow-y") ?? "visible");
        /* one axis scrolling makes a visible other axis auto */
        if (c.ovX != Ovf.Visible && c.ovY == Ovf.Visible) c.ovY = c.ovX == Ovf.Clip ? Ovf.Clip : Ovf.Auto;
        if (c.ovY != Ovf.Visible && c.ovX == Ovf.Visible) c.ovX = c.ovY == Ovf.Clip ? Ovf.Clip : Ovf.Auto;
        if ((v = G("z-index")) != null && v != "auto") { c.zAuto = false; c.z = (int)Num(v, 0); }
        if ((v = G("opacity")) != null) c.opacity = Math.Clamp(v.EndsWith("%") ? Js.ParseFloat(v) / 100 : Num(v, 1), 0, 1);
        if ((v = G("transform")) != null && v != "none") c.transform = Transforms(v, em, rem);
        if ((v = G("transform-origin")) != null) { var w = Words(v); (c.toX, c.toY) = PosPair(w, em, rem); }
        if ((v = G("background-color")) != null) c.bg = ColorOf(v, c.color);
        if ((v = G("background-image")) != null && v != "none") { c.bgImg = Images(v, em, rem, c.color); if (c.bgImg.Count == 0) c.bgImg = null; }
        if ((v = G("background-size")) != null && v != "auto") c.bgSize = CssParser.SplitTop(v, ',').Select(s => { var w = Words(s); if (w.Count == 0) return (Len.Auto, Len.Auto, "auto"); if (w[0] is "cover" or "contain") return (Len.Auto, Len.Auto, w[0]); var a = w[0] == "auto" ? Len.Auto : ParseLen(w[0], em, rem); var b = w.Count > 1 ? (w[1] == "auto" ? Len.Auto : ParseLen(w[1], em, rem)) : Len.Auto; return (a, b, (string)null); }).ToList();
        if ((v = G("background-position")) != null) c.bgPos = CssParser.SplitTop(v, ',').Select(s => PosPair(Words(s), em, rem)).ToList();
        if ((v = G("background-repeat")) != null) c.bgRepeat = CssParser.SplitTop(v, ',').Select(s => s.Trim()).ToList();
        if ((v = G("background-clip")) != null) c.bgClip = v;
        if ((v = G("box-shadow")) != null && v != "none") c.boxShadow = Shadows(v, em, rem, c.color);
        if ((v = G("vertical-align")) != null) c.vAlign = v;
        if ((v = G("text-overflow")) != null) c.textOverflow = v;
        if ((v = G("content")) != null) c.content = ContentOf(v, e);
        if ((v = G("filter")) != null && v != "none") c.filter = Filters(v);
        if ((v = G("backdrop-filter")) != null && v.StartsWith("blur(")) c.backdropBlur = ParseLen(v.Substring(5).TrimEnd(')'), em, rem).R(0);
        if ((v = G("transition")) != null && v != "none") c.transition = Transitions(v);
        if ((v = G("animation")) != null && v != "none") Animation(c, v);
        if ((v = G("zoom")) != null) { double z = v.EndsWith("%") ? Js.ParseFloat(v) / 100 : Num(v, 1); c.zoom = z > 0 ? z : 1; }
        if ((v = G("object-fit")) != null) c.objectFit = v;
        if ((v = G("aspect-ratio")) != null && v != "auto") { var parts = v.Split('/'); double a = Js.ParseFloat(parts[0]), b = parts.Length > 1 ? Js.ParseFloat(parts[1]) : 1; if (a > 0 && b > 0) c.aspect = a / b; }
        /* a pseudo element is nothing without content */
        if (pseudo != null && c.content == null) c.display = Disp.None;
        /* absolutely positioned and flex/grid children are blockified (layout does the flex part) */
        if (c.OutOfFlow && c.display is Disp.Inline or Disp.InlineBlock) c.display = Disp.Block;
        if (c.OutOfFlow && c.display == Disp.InlineFlex) c.display = Disp.Flex;
        if (c.OutOfFlow && c.display == Disp.InlineGrid) c.display = Disp.Grid;
    }
    static string PV(CStyle p, string k) => p.spec != null && p.spec.TryGetValue(k, out var v) ? v : null;
    static Disp DispOf(string v, Element e, string pseudo) => v switch
    {
        "none" => Disp.None, "block" or "table" or "table-row" or "table-cell" or "flow-root" or "table-caption" or "table-row-group" => Disp.Block,
        "inline" => Disp.Inline, "inline-block" or "inline-table" => Disp.InlineBlock,
        "flex" or "-webkit-box" or "-webkit-flex" => Disp.Flex, "inline-flex" => Disp.InlineFlex, "grid" => Disp.Grid, "inline-grid" => Disp.InlineGrid,
        "list-item" => Disp.ListItem, "contents" => Disp.Contents, _ => Disp.Inline,
    };
    static double Num(string v, double def) { var d = Js.ParseFloat(v); return double.IsNaN(d) ? def : d; }
    public static Col ColorOf(string v, Col cur)
    {
        v = v.Trim();
        if (v.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) return cur;
        if (v == "inherit") return cur;
        if (Named.TryGetValue(v.ToLowerInvariant(), out var n)) return Col.Parse(n);
        try { return Col.Parse(v); } catch { return cur; }
    }
    static readonly Dictionary<string, string> Named = new()
    {
        ["silver"] = "#c0c0c0", ["maroon"] = "#800000", ["purple"] = "#800080", ["green"] = "#008000", ["lime"] = "#00ff00", ["olive"] = "#808000",
        ["navy"] = "#000080", ["blue"] = "#0000ff", ["teal"] = "#008080", ["aqua"] = "#00ffff", ["cyan"] = "#00ffff", ["fuchsia"] = "#ff00ff", ["magenta"] = "#ff00ff",
        ["pink"] = "#ffc0cb", ["brown"] = "#a52a2a", ["crimson"] = "#dc143c", ["darkred"] = "#8b0000", ["lightgray"] = "#d3d3d3", ["darkgray"] = "#a9a9a9",
        ["whitesmoke"] = "#f5f5f5", ["gainsboro"] = "#dcdcdc", ["ivory"] = "#fffff0", ["beige"] = "#f5f5dc", ["khaki"] = "#f0e68c", ["violet"] = "#ee82ee",
        ["indigo"] = "#4b0082", ["skyblue"] = "#87ceeb", ["tomato"] = "#ff6347", ["coral"] = "#ff7f50", ["salmon"] = "#fa8072", ["lightblue"] = "#add8e6",
    };
    static SvgPaint SvgPaintOf(string v, Col cur)
    {
        v = v.Trim();
        if (v == "none") return new SvgPaint { kind = 0 };
        /* transparent paints nothing, but it is painted: it still takes the pointer */
        if (v == "transparent") return new SvgPaint { kind = 1, col = new Col(0, 0, 0, 0) };
        if (v.Equals("currentColor", StringComparison.OrdinalIgnoreCase)) return new SvgPaint { kind = 1, col = cur };
        if (v.StartsWith("url(")) return new SvgPaint { kind = 3, url = v.Substring(4).TrimEnd(')').Trim('"', '\'', '#', ' ') };
        return new SvgPaint { kind = 1, col = ColorOf(v, cur) };
    }
    static string ContentOf(string v, Element e)
    {
        v = v.Trim();
        if (v == "none" || v == "normal") return null;
        var sb = new StringBuilder();
        foreach (var w in Words(v))
        {
            if (w.Length >= 2 && (w[0] == '"' || w[0] == '\'')) sb.Append(Unescape(w.Substring(1, w.Length - 2)));
            else if (w.StartsWith("attr(")) sb.Append(e.getAttribute(w.Substring(5).TrimEnd(')').Trim()) ?? "");
        }
        return sb.ToString();
    }
    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
            int j = i + 1; while (j < s.Length && j - i <= 6 && Uri.IsHexDigit(s[j])) j++;
            if (j > i + 1) { sb.Append(char.ConvertFromUtf32(Convert.ToInt32(s.Substring(i + 1, j - i - 1), 16))); i = j - 1; if (i + 1 < s.Length && s[i + 1] == ' ') i++; }
            else { sb.Append(s[i + 1]); i++; }
        }
        return sb.ToString();
    }

    /* ---- lengths: px em rem % vw vh, calc() min() max() clamp() ---- */
    static readonly Dictionary<string, Len> lenCache = new();
    public static Len ParseLen(string v, double em, double rem)
    {
        v = v.Trim();
        if (v.Length == 0) return new Len { kind = 255 };
        if (v == "auto") return Len.Auto;
        if (v == "none") return Len.None;
        if (v is "min-content" or "max-content" or "fit-content" or "content") return new Len { kind = 3, kw = v };
        bool simple = v.IndexOf('(') < 0 && !v.Contains("em") && !v.Contains("v");
        string key = simple ? v : v + "|" + em + "|" + rem + "|" + Dom.ViewW + "x" + Dom.ViewH;
        if (lenCache.TryGetValue(key, out var c)) return c;
        Len r;
        try
        {
            var p = new Calc(v, em, rem);
            var e = p.Expr();
            r = e.fn != null ? new Len { kind = 1, fn = e.fn } : new Len { kind = 1, px = e.px, pct = e.pct };
            if (e.bad) r = new Len { kind = 255 };
        }
        catch { r = new Len { kind = 255 }; }
        if (lenCache.Count > 20000) lenCache.Clear();
        lenCache[key] = r;
        return r;
    }
    struct LV { public double px, pct; public Func<double, double> fn; public bool bad; public bool unitless; public double num;
        public Func<double, double> F { get { if (fn != null) return fn; double pv = px, pc = pct; return b => pv + pc * b / 100; } } }
    sealed class Calc
    {
        readonly string s; int i; readonly double em, rem;
        public Calc(string s, double em, double rem) { this.s = s; this.em = em; this.rem = rem; }
        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        public LV Expr()
        {
            var a = Term();
            for (; ; )
            {
                Ws();
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) { char op = s[i++]; var b = Term(); a = op == '+' ? Add(a, b, 1) : Add(a, b, -1); }
                else return a;
            }
        }
        LV Term()
        {
            var a = Factor();
            for (; ; )
            {
                Ws();
                if (i < s.Length && (s[i] == '*' || s[i] == '/'))
                {
                    char op = s[i++]; var b = Factor();
                    if (op == '*') a = b.unitless ? Mul(a, b.num) : a.unitless ? Mul(b, a.num) : new LV { bad = true };
                    else a = b.unitless && b.num != 0 ? Mul(a, 1 / b.num) : new LV { bad = true };
                }
                else return a;
            }
        }
        static LV Add(LV a, LV b, double sg)
        {
            if (a.unitless && b.unitless) return new LV { unitless = true, num = a.num + sg * b.num };
            if (a.unitless && a.num == 0) a = new LV(); if (b.unitless && b.num == 0) b = new LV();
            if (a.fn == null && b.fn == null) return new LV { px = a.px + sg * b.px, pct = a.pct + sg * b.pct };
            var fa = a.F; var fb = b.F; return new LV { fn = x => fa(x) + sg * fb(x) };
        }
        static LV Mul(LV a, double k)
        {
            if (a.unitless) return new LV { unitless = true, num = a.num * k };
            if (a.fn == null) return new LV { px = a.px * k, pct = a.pct * k };
            var f = a.fn; return new LV { fn = x => f(x) * k };
        }
        LV Factor()
        {
            Ws();
            if (i >= s.Length) return new LV { bad = true };
            if (s[i] == '(') { i++; var e = Expr(); Ws(); if (i < s.Length && s[i] == ')') i++; return e; }
            if (char.IsLetter(s[i]))
            {
                int st = i; while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '-')) i++;
                string fn = s.Substring(st, i - st).ToLowerInvariant();
                Ws();
                if (i < s.Length && s[i] == '(')
                {
                    i++;
                    var args = new List<LV>();
                    for (; ; ) { args.Add(Expr()); Ws(); if (i < s.Length && s[i] == ',') { i++; continue; } if (i < s.Length && s[i] == ')') i++; break; }
                    if (fn == "calc") return args[0];
                    if (fn is "min" or "max" or "clamp")
                    {
                        if (args.All(a => a.fn == null && a.pct == 0 && !a.unitless))
                        {
                            double r = fn == "min" ? args.Min(a => a.px) : fn == "max" ? args.Max(a => a.px) : Math.Max(args[0].px, Math.Min(args[1].px, args[2].px));
                            return new LV { px = r };
                        }
                        if (args.All(a => a.unitless))
                        {
                            double r = fn == "min" ? args.Min(a => a.num) : fn == "max" ? args.Max(a => a.num) : Math.Max(args[0].num, Math.Min(args[1].num, args[2].num));
                            return new LV { unitless = true, num = r };
                        }
                        var fs = args.Select(a => a.unitless ? (Func<double, double>)(_ => a.num) : a.F).ToArray();
                        if (fn == "min") return new LV { fn = x => fs.Min(f => f(x)) };
                        if (fn == "max") return new LV { fn = x => fs.Max(f => f(x)) };
                        return new LV { fn = x => Math.Max(fs[0](x), Math.Min(fs[1](x), fs[2](x))) };
                    }
                    return new LV { bad = true };
                }
                return new LV { bad = true };
            }
            int n0 = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E') && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || s[i + 1] == '-')) { i++; if (s[i] == '-') i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
            if (i == n0) { i++; return new LV { bad = true }; }
            double num = double.Parse(s.Substring(n0, i - n0), System.Globalization.CultureInfo.InvariantCulture);
            int u0 = i; while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '%')) i++;
            string unit = s.Substring(u0, i - u0).ToLowerInvariant();
            switch (unit)
            {
                case "": return new LV { unitless = true, num = num };
                case "px": return new LV { px = num };
                case "%": return new LV { pct = num };
                case "em": return new LV { px = num * em };
                case "rem": return new LV { px = num * rem };
                case "ex": return new LV { px = num * em * 0.5 };
                case "ch": return new LV { px = num * em * 0.55 };
                case "vw": return new LV { px = num * Dom.ViewW / 100 };
                case "vh": return new LV { px = num * Dom.ViewH / 100 };
                case "vmin": return new LV { px = num * Math.Min(Dom.ViewW, Dom.ViewH) / 100 };
                case "vmax": return new LV { px = num * Math.Max(Dom.ViewW, Dom.ViewH) / 100 };
                case "pt": return new LV { px = num * 4 / 3 };
                case "deg": return new LV { unitless = true, num = num };
                default: return new LV { px = num };
            }
        }
    }
    static (Len, Len) PosPair(List<string> w, double em, double rem)
    {
        Len X = Len.Pct(50), Y = Len.Pct(50);
        Len KW(string s) => s switch { "left" or "top" => Len.Pct(0), "right" or "bottom" => Len.Pct(100), "center" => Len.Pct(50), _ => ParseLen(s, em, rem) };
        if (w.Count == 0) return (X, Y);
        if (w.Count == 1) { if (w[0] is "top" or "bottom") Y = KW(w[0]); else X = KW(w[0]); return (X, Y); }
        if (w[0] is "top" or "bottom" || w[1] is "left" or "right") { Y = KW(w[0]); X = KW(w[1]); } else { X = KW(w[0]); Y = KW(w[1]); }
        return (X, Y);
    }
    static List<Shadow> Shadows(string v, double em, double rem, Col cur)
    {
        var l = new List<Shadow>();
        foreach (var part in CssParser.SplitTop(v, ','))
        {
            var sh = new Shadow { col = new Col(0, 0, 0, 1), curCol = true }; var nums = new List<Len>();
            foreach (var w in Words(part))
            {
                if (w == "inset") sh.inset = true;
                else if (char.IsDigit(w[0]) || w[0] == '-' || w[0] == '.' || w.StartsWith("calc(")) nums.Add(ParseLen(w, em, rem));
                else { sh.col = ColorOf(w, cur); sh.curCol = false; }
            }
            if (sh.curCol) sh.col = cur;
            if (nums.Count < 2) continue;
            sh.x = nums[0]; sh.y = nums[1]; sh.blur = nums.Count > 2 ? Math.Max(0, nums[2].R(0)) : 0; sh.spread = nums.Count > 3 ? nums[3].R(0) : 0;
            l.Add(sh);
        }
        return l;
    }
    static List<BgImage> Images(string v, double em, double rem, Col cur)
    {
        var l = new List<BgImage>();
        foreach (var part0 in CssParser.SplitTop(v, ','))
        {
            var part = part0.Trim();
            if (part == "none") { l.Add(null); continue; }
            int p = part.IndexOf('(');
            if (p < 0) continue;
            string fn = part.Substring(0, p).Trim().ToLowerInvariant();
            string inner = part.Substring(p + 1, part.LastIndexOf(')') - p - 1);
            if (fn == "url") { l.Add(new BgImage { url = inner.Trim('"', '\'', ' ') }); continue; }
            var g = new BgImage { radial = fn.Contains("radial"), repeating = fn.StartsWith("repeating") };
            var args = CssParser.SplitTop(inner, ',').Select(a => a.Trim()).ToList();
            int first = 0;
            if (args.Count > 0)
            {
                var a0 = args[0];
                if (!g.radial)
                {
                    if (a0.EndsWith("deg") || a0.EndsWith("turn") || a0.EndsWith("rad")) { g.angle = a0.EndsWith("turn") ? Js.ParseFloat(a0) * 360 : a0.EndsWith("rad") && !a0.EndsWith("grad") ? Js.ParseFloat(a0) * 180 / Math.PI : Js.ParseFloat(a0); first = 1; }
                    else if (a0.StartsWith("to ")) { g.toSide = a0.Substring(3).Trim(); g.angle = SideAngle(g.toSide); first = 1; }
                }
                else if (!LooksLikeColor(a0))
                {
                    var w = Words(a0); int at = w.IndexOf("at");
                    var shape = at >= 0 ? w.Take(at).ToList() : w;
                    foreach (var x in shape) { if (x == "circle") g.circle = true; else if (x == "ellipse") { } else g.size = x; }
                    if (at >= 0) (g.atX, g.atY) = PosPair(w.Skip(at + 1).ToList(), em, rem);
                    first = 1;
                }
            }
            for (int k = first; k < args.Count; k++)
            {
                var w = Words(args[k]);
                if (w.Count == 0) continue;
                var col = ColorOf(w[0], cur);
                if (w.Count == 1) g.stops.Add(new GStop { col = col });
                for (int q = 1; q < w.Count; q++) g.stops.Add(new GStop { col = col, pos = ParseLen(w[q], em, rem), hasPos = true });
            }
            if (g.stops.Count > 0) l.Add(g);
        }
        return l;
    }
    static bool LooksLikeColor(string s) { s = s.Trim(); return s.StartsWith("#") || s.StartsWith("rgb") || s.StartsWith("hsl") || s == "transparent" || Named.ContainsKey(s) || s is "white" or "black" or "red" or "gold" or "yellow" or "orange" or "gray" or "grey"; }
    static double SideAngle(string s) => s switch
    {
        "top" => 0, "right" => 90, "bottom" => 180, "left" => 270,
        "top right" or "right top" => 45, "bottom right" or "right bottom" => 135, "bottom left" or "left bottom" => 225, "top left" or "left top" => 315, _ => 180,
    };
    static List<TOp> Transforms(string v, double em, double rem)
    {
        var l = new List<TOp>();
        int i = 0;
        while (i < v.Length)
        {
            int p = v.IndexOf('(', i); if (p < 0) break;
            int q = v.IndexOf(')', p); if (q < 0) break;
            string fn = v.Substring(i, p - i).Trim().ToLowerInvariant();
            var args = v.Substring(p + 1, q - p - 1).Split(',', ' ').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
            i = q + 1;
            double Ang(string a) => a.EndsWith("turn") ? Js.ParseFloat(a) * Math.PI * 2 : a.EndsWith("grad") ? Js.ParseFloat(a) * Math.PI / 200 : a.EndsWith("rad") ? Js.ParseFloat(a) : Js.ParseFloat(a) * Math.PI / 180;
            switch (fn)
            {
                case "translate": l.Add(new TOp { f = "t", a = ParseLen(args[0], em, rem), b = args.Count > 1 ? ParseLen(args[1], em, rem) : Len.Zero }); break;
                case "translatex": l.Add(new TOp { f = "t", a = ParseLen(args[0], em, rem), b = Len.Zero }); break;
                case "translatey": l.Add(new TOp { f = "t", a = Len.Zero, b = ParseLen(args[0], em, rem) }); break;
                case "translate3d": l.Add(new TOp { f = "t", a = ParseLen(args[0], em, rem), b = args.Count > 1 ? ParseLen(args[1], em, rem) : Len.Zero }); break;
                case "scale": { double sx = Num(args[0], 1); l.Add(new TOp { f = "s", v1 = sx, v2 = args.Count > 1 ? Num(args[1], 1) : sx }); break; }
                case "scalex": l.Add(new TOp { f = "s", v1 = Num(args[0], 1), v2 = 1 }); break;
                case "scaley": l.Add(new TOp { f = "s", v1 = 1, v2 = Num(args[0], 1) }); break;
                case "rotate": case "rotatez": l.Add(new TOp { f = "r", v1 = Ang(args[0]) }); break;
                case "skew": l.Add(new TOp { f = "k", v1 = Ang(args[0]), v2 = args.Count > 1 ? Ang(args[1]) : 0 }); break;
                case "skewx": l.Add(new TOp { f = "k", v1 = Ang(args[0]) }); break;
                case "skewy": l.Add(new TOp { f = "k", v2 = Ang(args[0]) }); break;
                case "matrix": if (args.Count >= 6) l.Add(new TOp { f = "m", a = Len.Px(Num(args[4], 0)), b = Len.Px(Num(args[5], 0)), v1 = 0, v2 = 0, mat = args.Take(4).Select(a => Num(a, 0)).ToArray() }); break;
            }
        }
        return l;
    }
    static List<(string, double)> Filters(string v)
    {
        var l = new List<(string, double)>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(v, @"([a-z-]+)\(([^)]*)\)"))
        {
            string a = m.Groups[2].Value.Trim(); double n = a.EndsWith("%") ? Js.ParseFloat(a) / 100 : a.EndsWith("px") ? Js.ParseFloat(a) : Js.ParseFloat(a);
            l.Add((m.Groups[1].Value, double.IsNaN(n) ? 1 : n));
        }
        return l;
    }
    static double Time(string s) => s.EndsWith("ms") ? Js.ParseFloat(s) / 1000 : Js.ParseFloat(s);
    static List<Trans> Transitions(string v)
    {
        var l = new List<Trans>();
        foreach (var part in CssParser.SplitTop(v, ','))
        {
            var t = new Trans { prop = "all", ease = "ease" }; int times = 0;
            foreach (var w in Words(part))
            {
                if (char.IsDigit(w[0]) || w[0] == '.') { if (times++ == 0) t.dur = Time(w); else t.delay = Time(w); }
                else if (w is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out" || w.StartsWith("cubic-bezier") || w.StartsWith("steps")) t.ease = w;
                else t.prop = w;
            }
            l.Add(t);
        }
        return l;
    }
    static void Animation(CStyle c, string v)
    {
        int times = 0;
        foreach (var w in Words(CssParser.SplitTop(v, ',')[0]))
        {
            if (char.IsDigit(w[0]) || w[0] == '.') { if (w.EndsWith("s")) { if (times++ == 0) c.animDur = Time(w); else c.animDelay = Time(w); } else c.animCount = Js.ParseFloat(w); }
            else if (w == "infinite") c.animInfinite = true;
            else if (w is "alternate" or "alternate-reverse") c.animAlternate = true;
            else if (w is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out" || w.StartsWith("cubic-bezier")) c.animEase = w;
            else if (w is "normal" or "reverse" or "forwards" or "backwards" or "both" or "none" or "running" or "paused") { }
            else c.anim = w;
        }
    }
    static List<GTrack> Tracks(string v, double em, double rem)
    {
        var l = new List<GTrack>();
        foreach (var w in Words(v))
        {
            if (w.StartsWith("repeat("))
            {
                var inner = w.Substring(7, w.Length - 8);
                int c = inner.IndexOf(',');
                string n = inner.Substring(0, c).Trim();
                var t = new GTrack { kind = "repeat", inner = Tracks(inner.Substring(c + 1), em, rem) };
                if (n == "auto-fit") t.autoFit = true; else if (n == "auto-fill") t.autoFill = true; else t.repeat = (int)Num(n, 1);
                l.Add(t);
            }
            else l.Add(OneTrack(w, em, rem));
        }
        return l;
    }
    static GTrack OneTrack(string w, double em, double rem)
    {
        if (w.EndsWith("fr")) return new GTrack { kind = "fr", v = Num(w, 1) };
        if (w is "auto" or "min-content" or "max-content") return new GTrack { kind = w };
        if (w.StartsWith("minmax("))
        {
            var a = CssParser.SplitTop(w.Substring(7, w.Length - 8), ',');
            return new GTrack { kind = "minmax", min = OneTrack(a[0].Trim(), em, rem), max = OneTrack(a[1].Trim(), em, rem) };
        }
        return new GTrack { kind = "len", len = ParseLen(w, em, rem) };
    }
}
