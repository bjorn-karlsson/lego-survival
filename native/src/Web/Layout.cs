// The native UI engine, part 5: layout. Block flow with sibling margin
// collapsing, inline text with wrapping and baselines, inline-blocks, flexbox
// (rows, columns, wrapping, grow/shrink, gaps, alignment), grid (fixed, auto,
// fr, minmax, repeat(auto-fit/auto-fill)), absolute/fixed/relative/sticky
// positioning, overflow scrolling and zoom. The game's stylesheet is the test:
// it only has to do what that CSS asks of a browser, the way Chrome does it.
namespace Brickblade;

internal sealed class TextPiece { public string text; public double x, y, w; public CStyle cs; public FontSpec font; public bool space; }
internal sealed class Frag { public double x, y, w, h; public bool first, last; }

internal sealed class Box
{
    public Element el; public CStyle cs; public Box parent; public readonly List<Box> kids = new();
    public bool isText, isPseudo, anon, replaced;
    public TextNode tnode; public string text;
    public List<TextPiece> pieces;          /* a text box's placed words (container coordinates) */
    public List<Frag> frags;                /* an inline box's rectangles, one per line it crosses */
    public double x, y, w, h;               /* border box, relative to the parent box (parent's local units) */
    public double ml, mr, mt, mb, bl, br, bt, bb, pl, pr, pt, pb;
    public double contentW, contentH;       /* scrollable extent, from the padding-box origin */
    public double baseline = double.NaN;    /* first baseline, from the border-box top */
    public double zoom = 1, relX, relY, scrollbarW;
    public double fixedW = double.NaN, fixedH = double.NaN;   /* a size a flex/grid container imposed */
    public bool inlineBox;                  /* a non-atomic inline (span, b, ...): placed by frags */
    public double staticX, staticY;         /* where an absolutely positioned box would have been */
    public Box cbBox;                       /* containing block of a positioned box */
    public double zoomOut => 1;
    public double PadW => w / zoom - bl - br;
    public double PadH => h / zoom - bt - bb;
    public double CW => w / zoom - bl - br - pl - pr;
    public double CH => h / zoom - bt - bb - pt - pb;
    public string Text => isText ? (tnode?.data ?? text ?? "") : null;

    /* the border box on the screen (CSS px), after scrolls, zooms and translations */
    public DOMRect ClientRect()
    {
        if (inlineBox)
        {
            if (frags == null || frags.Count == 0) { var o = Origin(); return new DOMRect { x = o.x, y = o.y }; }
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var f in frags) { x0 = Math.Min(x0, f.x); y0 = Math.Min(y0, f.y); x1 = Math.Max(x1, f.x + f.w); y1 = Math.Max(y1, f.y + f.h); }
            var (ox, oy, k) = Origin();
            return new DOMRect { x = ox + x0 * k, y = oy + y0 * k, width = (x1 - x0) * k, height = (y1 - y0) * k };
        }
        var (ax, ay, sc) = Origin();
        double ww = w * sc / zoom * zoom, hh = h * sc;
        /* a transform's translate and scale move the rectangle (rotation is ignored) */
        if (cs?.transform != null)
        {
            var m = Paint2.TransformMatrix(this);
            double lx = 0, ly = 0, lw = w, lh = h;
            double X0 = m.a * lx + m.c * ly + m.e, Y0 = m.b * lx + m.d * ly + m.f;
            double X1 = m.a * (lx + lw) + m.c * (ly + lh) + m.e, Y1 = m.b * (lx + lw) + m.d * (ly + lh) + m.f;
            return new DOMRect { x = ax + Math.Min(X0, X1) * sc, y = ay + Math.Min(Y0, Y1) * sc, width = Math.Abs(X1 - X0) * sc, height = Math.Abs(Y1 - Y0) * sc };
        }
        return new DOMRect { x = ax, y = ay, width = ww, height = hh };
    }
    /* this box's border-box origin in client px, and the scale its parent's units have there */
    public (double x, double y, double k) Origin()
    {
        var chain = new List<Box>();
        for (var b = this; b != null; b = b.parent) chain.Add(b);
        double X = 0, Y = 0, K = 1;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var b = chain[i];
            if (b.cs != null && b.cs.position == Pos.Fixed && !b.inlineBox) { X = 0; Y = 0; K = Dom.ZoomRoot; }
            X += (b.x + b.relX + Paint2.StickyX(b)) * K; Y += (b.y + b.relY + Paint2.StickyY(b)) * K;
            if (i == 0) break;
            /* entering b: its children are in b's local units, offset by b's scroll */
            if (b.cs != null && b.cs.transform != null && !b.inlineBox)
            {
                var m = Paint2.TransformMatrix(b); X += m.e * K; Y += m.f * K; K *= Math.Sqrt(Math.Abs(m.a * m.d - m.b * m.c));
            }
            K *= b.zoom;
            if (b.el != null && !b.isPseudo && !b.inlineBox) { X -= b.el.scrollX * K; Y -= b.el.scrollY * K; }
        }
        return (X, Y, K);
    }
}

internal static class Layout
{
    static Dictionary<Box, (double min, double max)> intr = new();
    public static Box Root;
    static double VW, VH;

    /* ---- the box tree, from the DOM ---- */
    public static Box Build(Element e, Box parent)
    {
        var cs = e.cs;
        if (cs == null || cs.display == Disp.None) { e.box = null; ClearBoxes(e); return null; }
        var b = new Box { el = e, cs = cs, parent = parent };
        e.box = b;
        string ln = e.localName;
        if (ln == "canvas" || (ln == "svg" && !(e.parentNode?.isSvg ?? false)) || ln == "input" || ln == "img" || ln == "textarea") b.replaced = true;
        if (b.replaced) { foreach (var c in e.childNodes) if (c is Element ce) ClearBoxes(ce); return b; }
        if (e.csBefore != null && e.csBefore.display != Disp.None) b.kids.Add(PseudoBox(e, e.csBefore, b));
        foreach (var n in e.childNodes)
        {
            if (n is TextNode t) { if (t.data.Length > 0) b.kids.Add(new Box { isText = true, tnode = t, cs = cs, parent = b }); }
            else if (n is Element ce)
            {
                var k = Build(ce, b);
                if (k == null) continue;
                if (ce.cs.display == Disp.Contents) { foreach (var kk in k.kids) { kk.parent = b; b.kids.Add(kk); } continue; }
                b.kids.Add(k);
            }
        }
        if (e.csAfter != null && e.csAfter.display != Disp.None) b.kids.Add(PseudoBox(e, e.csAfter, b));
        if (cs.display is Disp.Flex or Disp.InlineFlex or Disp.Grid or Disp.InlineGrid) WrapText(b);
        return b;
    }
    /* text directly in a flex or grid container is an anonymous block item; whitespace is nothing */
    static void WrapText(Box b)
    {
        for (int i = 0; i < b.kids.Count; i++)
        {
            var k = b.kids[i];
            if (!k.isText) continue;
            if (string.IsNullOrWhiteSpace(k.Text) && k.cs.whiteSpace is "normal" or "nowrap" or "pre-line") { b.kids.RemoveAt(i--); continue; }
            var a = new Box { anon = true, cs = AnonCs(b.cs), parent = b };
            /* consecutive text runs share one anonymous block */
            while (i < b.kids.Count && b.kids[i].isText) { var t = b.kids[i]; t.parent = a; a.kids.Add(t); b.kids.RemoveAt(i); }
            b.kids.Insert(i, a);
        }
    }
    static void ClearBoxes(Element e) { e.box = null; foreach (var c in e.childNodes) if (c is Element ce) ClearBoxes(ce); }
    static Box PseudoBox(Element e, CStyle cs, Box parent)
    {
        var p = new Box { el = e, cs = cs, parent = parent, isPseudo = true };
        if (!string.IsNullOrEmpty(cs.content)) p.kids.Add(new Box { isText = true, text = cs.content, cs = cs, parent = p });
        return p;
    }

    public static void Run(Box root, double vw, double vh)
    {
        Root = root; VW = vw; VH = vh;
        intr = new();
        var pend = new List<Box>();
        root.x = 0; root.y = 0;
        LayoutBlockish(root, vw, vh, pend, false);
        root.h = Math.Max(root.h, vh);
        PlaceAbs(pend);
    }

    /* ---- shared sizing ---- */
    static bool IsBlockLevel(Box b) => !b.isText && b.cs.display is Disp.Block or Disp.Flex or Disp.Grid or Disp.ListItem && !b.cs.OutOfFlow;
    static bool InFlow(Box b) => b.isText || !b.cs.OutOfFlow;
    static bool Atomic(Box b) => !b.isText && (b.replaced || b.cs.display is Disp.InlineBlock or Disp.InlineFlex or Disp.InlineGrid);
    static void Edges(Box b, double cbW)
    {
        var c = b.cs;
        b.bl = c.bL; b.br = c.bR; b.bt = c.bT; b.bb = c.bB;
        b.pl = Math.Max(0, c.pL.R(cbW)); b.pr = Math.Max(0, c.pR.R(cbW)); b.pt = Math.Max(0, c.pT.R(cbW)); b.pb = Math.Max(0, c.pB.R(cbW));
        b.ml = c.mL.R(cbW); b.mr = c.mR.R(cbW); b.mt = c.mT.R(cbW); b.mb = c.mB.R(cbW);
    }
    static double Frame(Box b) => b.bl + b.br + b.pl + b.pr;
    static double FrameV(Box b) => b.bt + b.bb + b.pt + b.pb;
    /* a CSS width/height as a border-box size (box-sizing), NaN when auto or unresolvable */
    static double SpecW(Box b, Len L, double cbW)
    {
        double v = L.Def(cbW);
        if (double.IsNaN(v)) return double.NaN;
        return b.cs.borderBox ? Math.Max(v, Frame(b)) : v + Frame(b);
    }
    static double SpecH(Box b, Len L, double cbH)
    {
        double v = L.Def(cbH);
        if (double.IsNaN(v)) return double.NaN;
        return b.cs.borderBox ? Math.Max(v, FrameV(b)) : v + FrameV(b);
    }
    static double ClampW(Box b, double w, double cbW)
    {
        double mx = b.cs.maxW.IsNone ? double.NaN : SpecW(b, b.cs.maxW, cbW);
        double mn = b.cs.minW.IsVal ? SpecW(b, b.cs.minW, cbW) : double.NaN;
        if (!double.IsNaN(mx)) w = Math.Min(w, mx);
        if (!double.IsNaN(mn)) w = Math.Max(w, mn);
        return Math.Max(w, Frame(b));
    }
    static double ClampH(Box b, double h, double cbH)
    {
        double mx = b.cs.maxH.IsNone ? double.NaN : SpecH(b, b.cs.maxH, cbH);
        double mn = b.cs.minH.IsVal ? SpecH(b, b.cs.minH, cbH) : double.NaN;
        if (!double.IsNaN(mx)) h = Math.Min(h, mx);
        if (!double.IsNaN(mn)) h = Math.Max(h, mn);
        return Math.Max(h, FrameV(b));
    }

    /* ---- intrinsic widths (border box, without margins) ---- */
    public static (double min, double max) Intrinsic(Box b)
    {
        if (intr.TryGetValue(b, out var r)) return r;
        var saved = (b.ml, b.mr, b.mt, b.mb, b.bl, b.br, b.bt, b.bb, b.pl, b.pr, b.pt, b.pb);
        try { return intr[b] = IntrinsicOf(b); }
        finally { (b.ml, b.mr, b.mt, b.mb, b.bl, b.br, b.bt, b.bb, b.pl, b.pr, b.pt, b.pb) = saved; }
    }
    static (double min, double max) IntrinsicOf(Box b)
    {
        (double min, double max) r;
        Edges(b, 0);
        double fr = Frame(b);
        double sw = SpecW(b, b.cs.width, double.NaN);
        if (!double.IsNaN(sw)) return (ClampW(b, sw, double.NaN), ClampW(b, sw, double.NaN));
        if (b.replaced) { var (iw, _) = ReplacedSize(b, double.NaN, double.NaN); return (iw, iw); }
        double mn = 0, mx = 0;
        var d = b.cs.display;
        if (d is Disp.Flex or Disp.InlineFlex)
        {
            bool row = b.cs.flexDir is 0 or 2;
            double gap = b.cs.colGap.R(0); int n = 0;
            foreach (var k in FlexItems(b))
            {
                var (a, z) = Contribution(k);
                if (row) { mx += z; mn = b.cs.wrap ? Math.Max(mn, a) : mn + a; }
                else { mx = Math.Max(mx, z); mn = Math.Max(mn, a); }
                n++;
            }
            if (row && n > 1) { mx += gap * (n - 1); if (!b.cs.wrap) mn += gap * (n - 1); }
        }
        else if (d is Disp.Grid or Disp.InlineGrid)
        {
            /* the columns' fixed sizes, or each item's contribution */
            double gap = b.cs.colGap.R(0);
            var items = FlexItems(b).ToList();
            int cols = Math.Max(1, CountTracks(b.cs.gridCols, double.NaN, gap, items.Count));
            double rowMax = 0, rowMin = 0;
            for (int i = 0; i < items.Count; i++) { var (a, z) = Contribution(items[i]); rowMax = Math.Max(rowMax, z); rowMin = Math.Max(rowMin, a); }
            mx = rowMax * cols + gap * (cols - 1); mn = rowMin * cols + gap * (cols - 1);
            if (b.cs.gridCols != null && b.cs.gridCols.All(t => t.kind == "len")) { mx = mn = b.cs.gridCols.Sum(t => t.len.R(0)) + gap * (b.cs.gridCols.Count - 1); }
        }
        else
        {
            /* block flow: the widest child; inline flow: the longest line / the longest word */
            double lineMax = 0, lineMin = 0;
            void Flush() { mx = Math.Max(mx, lineMax); lineMax = 0; }
            lastSpace = true;
            InlineWalk(b, piece =>
            {
                switch (piece.kind)
                {
                    case 0:
                        lineMax += piece.w;
                        if (piece.space) { if (piece.canBreak) lineMin = 0; else lineMin += piece.w; }
                        else { lineMin += piece.w; mn = Math.Max(mn, lineMin); if (piece.canBreak) lineMin = 0; }
                        break;
                    case 2: Flush(); lineMin = 0; lastSpace = true; break;
                    case 3: Flush(); lineMin = 0; { var (a, z) = Contribution(piece.box); mn = Math.Max(mn, a); mx = Math.Max(mx, z); } lastSpace = true; break;
                    case 4: case 5: lineMax += piece.w; lineMin += piece.w; mn = Math.Max(mn, lineMin); break;
                    case 6: lineMax += piece.w; mn = Math.Max(mn, piece.wmin); lineMin = 0; lastSpace = false; break;
                }
            });
            Flush();
        }
        r = (ClampW(b, mn + fr, double.NaN), ClampW(b, mx + fr, double.NaN));
        if (r.min > r.max) r.max = r.min;
        return r;
    }
    /* a child's contribution to its parent's intrinsic width: border box plus margins */
    static (double min, double max) Contribution(Box k)
    {
        if (k.isText) return (0, 0);
        var (a, z) = Intrinsic(k);
        double m = Math.Max(0, k.cs.mL.R(0)) + Math.Max(0, k.cs.mR.R(0));
        return (a + m, z + m);
    }

    /* ---- the inline walk: a container's inline content as a stream of pieces ---- */
    internal struct Piece { public int kind; public double w, wmin; public int idx; public string text; public bool space, canBreak; public Box box; public Box owner; public CStyle cs; public FontSpec font; }
    /* kinds: 0 text word or space, 1 (unused), 2 forced break, 3 block-level child, 4 inline open, 5 inline close, 6 atom, 7 out-of-flow */
    static void InlineWalk(Box b, Action<Piece> emit)
    {
        foreach (var k in b.kids)
        {
            if (k.isText) { TextPieces(k, k.parent?.cs ?? b.cs, emit); continue; }
            if (k.cs.OutOfFlow) { emit(new Piece { kind = 7, box = k }); continue; }
            if (k.el != null && !k.isPseudo && k.el.localName == "br") { emit(new Piece { kind = 2, box = k }); continue; }
            if (Atomic(k)) { var (a, z) = Contribution(k); emit(new Piece { kind = 6, w = z, wmin = a, box = k, canBreak = true }); continue; }
            if (IsBlockLevel(k)) { emit(new Piece { kind = 3, box = k }); continue; }
            /* a plain inline: its left edge, its content, its right edge */
            Edges(k, 0);
            emit(new Piece { kind = 4, w = k.ml + k.bl + k.pl, box = k });
            InlineWalk(k, emit);
            emit(new Piece { kind = 5, w = k.mr + k.br + k.pr, box = k });
        }
    }
    static string Transform(string s, CStyle cs) => cs.textTransform switch
    {
        "uppercase" => s.ToUpperInvariant(), "lowercase" => s.ToLowerInvariant(),
        "capitalize" => System.Text.RegularExpressions.Regex.Replace(s, @"\b\w", m => m.Value.ToUpperInvariant()), _ => s,
    };
    public static double TextW(string s, CStyle cs, FontSpec f)
    {
        if (s.Length == 0) return 0;
        double w = Ctx.Measure(s, f);
        if (cs.letter != 0) w += cs.letter * new System.Globalization.StringInfo(s).LengthInTextElements;
        return w;
    }
    /* collapsed-space state across text boxes in one container */
    [ThreadStatic] static bool lastSpace;
    static void TextPieces(Box t, CStyle cs, Action<Piece> emit)
    {
        string s = Transform(t.Text, cs);
        var f = cs.Font;
        string ws = cs.whiteSpace;
        bool collapse = ws is "normal" or "nowrap" or "pre-line", wrap = ws is "normal" or "pre-wrap" or "pre-line" or "break-spaces", keepNl = ws is "pre" or "pre-wrap" or "pre-line" or "break-spaces";
        var sb = new System.Text.StringBuilder();
        void Word()
        {
            if (sb.Length == 0) return;
            string w = sb.ToString(); sb.Clear();
            /* a word may break after a hyphen, the way Chrome breaks "auto-fit" */
            int st = 0;
            if (wrap)
                for (int i = 0; i < w.Length - 1; i++)
                    if ((w[i] == '-' || w[i] == '—' || w[i] == '/') && i > st && char.IsLetterOrDigit(w[i + 1]))
                    {
                        string part = w.Substring(st, i + 1 - st);
                        emit(new Piece { kind = 0, text = part, w = TextW(part, cs, f), cs = cs, font = f, canBreak = true, owner = t });
                        st = i + 1;
                    }
            string rest = w.Substring(st);
            emit(new Piece { kind = 0, text = rest, w = TextW(rest, cs, f), cs = cs, font = f, owner = t });
            lastSpace = false;
        }
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\n' && keepNl) { Word(); emit(new Piece { kind = 2 }); lastSpace = true; continue; }
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f')
            {
                if (collapse)
                {
                    Word();
                    if (!lastSpace) emit(new Piece { kind = 0, text = " ", w = TextW(" ", cs, f), space = true, canBreak = wrap, cs = cs, font = f, owner = t });
                    lastSpace = true; continue;
                }
                Word();
                string sp = c == '\t' ? "        " : " ";
                emit(new Piece { kind = 0, text = sp, w = TextW(sp, cs, f), space = true, canBreak = wrap, cs = cs, font = f, owner = t, box = t });
                lastSpace = false; continue;
            }
            if (c == ' ') { sb.Append(' '); continue; }
            sb.Append(c);
            /* CJK and the like could break anywhere; the game has none */
        }
        Word();
    }

    /* ---- block containers ---- */
    /* lays out b with the given containing-block width; sets b.w, b.h (b.x/b.y are the caller's) */
    static void LayoutBlockish(Box b, double cbW, double cbH, List<Box> pend, bool shrink)
    {
        Edges(b, cbW);
        b.baseline = double.NaN;
        var cs = b.cs;
        double z = cs.zoom;
        if (z != 1) { cbW /= z; if (!double.IsNaN(cbH)) cbH /= z; Edges(b, cbW); }
        double W;
        if (!double.IsNaN(b.fixedW)) W = b.fixedW / z;
        else
        {
            double sw = SpecW(b, cs.width, cbW);
            if (!double.IsNaN(sw)) W = sw;
            else if (b.replaced) W = ReplacedSize(b, cbW, cbH).w;
            else if (shrink)
            {
                var (mn, mx) = Intrinsic(b);
                double avail = cbW - (cs.mL.IsAuto ? 0 : b.ml) - (cs.mR.IsAuto ? 0 : b.mr);
                W = Math.Min(Math.Max(mn, avail), mx);
            }
            else
            {
                double ml = cs.mL.IsAuto ? 0 : b.ml, mr = cs.mR.IsAuto ? 0 : b.mr;
                W = cbW - ml - mr;
            }
            W = ClampW(b, W, cbW);
        }
        /* auto horizontal margins centre a block with a width */
        if (!shrink && double.IsNaN(b.fixedW))
        {
            double free = cbW - W - (cs.mL.IsAuto ? 0 : b.ml) - (cs.mR.IsAuto ? 0 : b.mr);
            if (cs.mL.IsAuto && cs.mR.IsAuto) { b.ml = b.mr = Math.Max(0, free / 2); }
            else if (cs.mL.IsAuto) b.ml = Math.Max(0, free);
            else if (cs.mR.IsAuto) b.mr = Math.Max(0, free);
        }
        else { if (cs.mL.IsAuto) b.ml = 0; if (cs.mR.IsAuto) b.mr = 0; }
        if (cs.mT.IsAuto) b.mt = 0; if (cs.mB.IsAuto) b.mb = 0;
        b.w = W;
        double specH = !double.IsNaN(b.fixedH) ? b.fixedH / z : SpecH(b, cs.height, cbH);
        if (b.replaced && double.IsNaN(specH)) specH = ReplacedSize(b, cbW, cbH, W).h;
        double contentHDef = double.NaN;
        if (!double.IsNaN(specH)) contentHDef = ClampH(b, specH, cbH) - FrameV(b);
        var myPend = cs.Positioned || b.parent == null ? new List<Box>() : pend;
        double scrollbar = cs.ovY == Ovf.Scroll ? Paint2.ScrollbarW(b) : 0;
        bool flexy = cs.display is Disp.Flex or Disp.InlineFlex;
        for (int pass = 0; pass < 3; pass++)
        {
            double cw = Math.Max(0, W - Frame(b) - scrollbar);
            double inner;
            if (b.replaced) inner = 0;
            else if (flexy) inner = LayoutFlex(b, cw, contentHDef, myPend);
            else if (cs.display is Disp.Grid or Disp.InlineGrid) inner = LayoutGrid(b, cw, contentHDef, myPend);
            else inner = LayoutFlow(b, cw, contentHDef, myPend);
            double H0 = !double.IsNaN(specH) ? specH : inner + FrameV(b);
            double H = ClampH(b, H0, cbH);
            b.h = H;
            /* a flex container held by max-height (or pushed by min-height) lays its items out again in that size */
            if (flexy && double.IsNaN(contentHDef) && Math.Abs(H - H0) > 0.5) { contentHDef = H - FrameV(b); continue; }
            /* a vertical scroller takes a scrollbar's width once it overflows (classic scrollbars) */
            if (scrollbar == 0 && cs.ovY == Ovf.Auto && b.contentH > b.PadH + 0.5 && Paint2.ScrollbarW(b) > 0) { scrollbar = Paint2.ScrollbarW(b); continue; }
            break;
        }
        b.scrollbarW = scrollbar;
        if (b.parent == null || cs.Positioned) { b.cbBox = b; PlaceAbsFor(b, myPend); }
        if (z != 1) { b.zoom = z; b.w *= z; b.h *= z; b.ml *= z; b.mr *= z; b.mt *= z; b.mb *= z; if (!double.IsNaN(b.baseline)) b.baseline *= z; }
        Relative(b, cbW, cbH);
    }
    static void Relative(Box b, double cbW, double cbH)
    {
        b.relX = b.relY = 0;
        if (b.cs.position != Pos.Relative) return;
        if (!b.cs.left.IsAuto) b.relX = b.cs.left.R(cbW); else if (!b.cs.right.IsAuto) b.relX = -b.cs.right.R(cbW);
        if (!b.cs.top.IsAuto) b.relY = b.cs.top.R(cbH); else if (!b.cs.bottom.IsAuto) b.relY = -b.cs.bottom.R(cbH);
    }

    /* normal flow inside b's content box; returns the content height */
    static double LayoutFlow(Box b, double cw, double chDef, List<Box> pend)
    {
        double ox = b.bl + b.pl, oy = b.bt + b.pt;
        double y = 0, prevMB = 0; bool havePrev = false;
        double maxX = cw;
        var run = new List<Box>();
        bool anyBlock = b.kids.Any(k => IsBlockLevel(k));
        if (!anyBlock)
        {
            double h = LayoutInline(b, b.kids, ox, oy, cw, pend, out double ilw);
            b.contentW = Math.Max(cw, ilw) + b.pl + b.pr; b.contentH = h + b.pt + b.pb;
            if (b.cs.display == Disp.ListItem) Marker(b, oy);
            return h;
        }
        void FlushRun()
        {
            if (run.Count == 0) return;
            /* whitespace-only runs between blocks make no line */
            bool blank = run.All(k => k.isText && string.IsNullOrWhiteSpace(k.Text) && k.cs.whiteSpace is "normal" or "nowrap" || !k.isText && k.cs.OutOfFlow);
            if (blank)
            {
                foreach (var k in run) if (!k.isText) { k.staticX = ox; k.staticY = oy + y + (havePrev ? prevMB : 0); pend.Add(k); }
                foreach (var k in run) if (k.isText) k.pieces = null;
                run.Clear(); return;
            }
            if (havePrev) { y += prevMB; havePrev = false; prevMB = 0; }
            double h = LayoutInline(b, run, ox, oy + y, cw, pend, out double lw);
            maxX = Math.Max(maxX, lw);
            y += h; run.Clear();
        }
        foreach (var k in b.kids)
        {
            if (!IsBlockLevel(k)) { run.Add(k); continue; }
            FlushRun();
            Edges(k, cw);
            k.fixedW = double.NaN; k.fixedH = double.NaN;
            LayoutBlockish(k, cw, chDef, pend, false);
            double mt = k.mt;
            double gap = havePrev ? Collapse(prevMB, mt) : mt;
            y += gap;
            k.x = ox + k.ml; k.y = oy + y;
            if (double.IsNaN(b.baseline) && !double.IsNaN(k.baseline)) b.baseline = k.y + k.baseline;
            y += k.h;
            prevMB = k.mb; havePrev = true;
            maxX = Math.Max(maxX, k.ml + k.w + k.mr);
        }
        FlushRun();
        if (havePrev) y += prevMB;
        b.contentW = maxX + b.pl + b.pr; b.contentH = y + b.pt + b.pb;
        if (b.cs.display == Disp.ListItem) Marker(b, oy);
        return y;
    }
    static double Collapse(double a, double b) => a >= 0 && b >= 0 ? Math.Max(a, b) : a < 0 && b < 0 ? Math.Min(a, b) : a + b;
    /* list-item markers are painted, not laid out: remember where the first line is */
    static void Marker(Box b, double oy) { }

    /* ---- inline formatting: line boxes ---- */
    sealed class LItem { public Piece p; public double x; public int idx; }
    static double LayoutInline(Box b, List<Box> kids, double ox, double oy, double cw, List<Box> pend, out double usedW)
    {
        var items = new List<Piece>();
        lastSpace = true;
        foreach (var k in kids) ResetInline(k);
        var tmp = new Box { cs = b.cs, el = b.el };
        tmp.kids.AddRange(kids);
        InlineWalk(tmp, p => items.Add(p));
        foreach (var k in kids) k.parent = b;
        /* lay atoms out first: their widths and baselines */
        foreach (var p in items)
            if (p.kind == 6)
            {
                var a = p.box;
                Edges(a, cw);
                a.fixedW = double.NaN; a.fixedH = double.NaN;
                LayoutBlockish(a, cw, double.NaN, pend, true);
            }
        string align = b.cs.textAlign;
        var strut = b.cs.Font;
        double strutLH = b.cs.LineH(strut);
        double y = 0; usedW = 0;
        int i = 0, n = items.Count;
        bool firstLine = true;
        /* open inline boxes carried across lines */
        var open = new List<Box>();
        while (i < n)
        {
            /* gather one line */
            var line = new List<LItem>();
            double x = 0; int lastBreak = -1; double lastBreakX = 0;
            int j = i;
            bool forced = false;
            /* leading collapsible spaces vanish */
            while (j < n && items[j].kind == 0 && items[j].space && items[j].cs.whiteSpace is "normal" or "nowrap" or "pre-line") j++;
            bool contentNow = false;
            for (; j < n; j++)
            {
                var p = items[j];
                if (p.kind == 2) { forced = true; j++; break; }
                if (p.kind == 7) { line.Add(new LItem { p = p, x = x, idx = j }); continue; }
                double w = p.w;
                /* an inline-block can always wrap before it (Chrome treats atoms like ideographs) */
                if (p.kind == 6 && contentNow && AllowWrap(p, b) && line.Count > 0) lastBreak = line.Count - 1;
                if ((p.kind == 0 && !p.space || p.kind == 6) && x + w > cw + 0.01 && contentNow && lastBreak >= 0)
                {
                    /* back up to the last break opportunity */
                    j = line[lastBreak].idx + 1;
                    line.RemoveRange(lastBreak + 1, line.Count - lastBreak - 1);
                    break;
                }
                line.Add(new LItem { p = p, x = x, idx = j });
                x += w;
                if (p.kind == 6 || p.kind == 0 && !p.space) contentNow = true;
                bool canBreakAfter = (p.kind == 0 && p.canBreak) || p.kind == 6 && AllowWrap(p, b);
                if (canBreakAfter) { lastBreak = line.Count - 1; lastBreakX = x; }
            }
            if (j <= i) j = i + 1;
            /* trailing collapsible spaces hang */
            while (line.Count > 0 && line[^1].p.kind == 0 && line[^1].p.space && line[^1].p.cs.whiteSpace is "normal" or "nowrap" or "pre-line") line.RemoveAt(line.Count - 1);
            double lineW = 0; foreach (var li in line) if (li.p.kind != 7) lineW = Math.Max(lineW, li.x + li.p.w);
            usedW = Math.Max(usedW, lineW);
            /* vertical: baseline-aligned boxes around the strut */
            double asc = strut.Ascent, desc = strut.Descent;
            double half = (strutLH - (asc + desc)) / 2;
            double top = -(asc + half), bot = desc + half;
            foreach (var li in line)
            {
                var p = li.p;
                if (p.kind == 0)
                {
                    double lh = p.cs.LineH(p.font), a2 = p.font.Ascent, d2 = p.font.Descent, h2 = (lh - (a2 + d2)) / 2;
                    double shift = VShift(p.cs, b.cs, strut);
                    top = Math.Min(top, shift - (a2 + h2)); bot = Math.Max(bot, shift + d2 + h2);
                }
                else if (p.kind == 6)
                {
                    var a = p.box; double mh = a.mt + a.h + a.mb;
                    double bl = AtomBaseline(a);
                    string va = a.cs.vAlign;
                    if (va == "middle") { double mid = -strut.px * 0.25; top = Math.Min(top, mid - mh / 2); bot = Math.Max(bot, mid + mh / 2); }
                    else if (va is "top" or "bottom") { }
                    else { double shift = VShift(a.cs, b.cs, strut); top = Math.Min(top, shift - bl); bot = Math.Max(bot, shift + mh - bl); }
                }
            }
            double lineH = bot - top;
            foreach (var li in line) if (li.p.kind == 6 && li.p.box.cs.vAlign is "top" or "bottom") lineH = Math.Max(lineH, li.p.box.mt + li.p.box.h + li.p.box.mb);
            if (line.Count == 0 && !forced) lineH = 0;
            if (line.All(li => li.p.kind == 7) && !forced) lineH = 0;
            double baseY = oy + y - top;
            /* horizontal alignment */
            double shiftX = 0;
            string al = align;
            if (al is "center" or "-webkit-center") shiftX = (cw - lineW) / 2;
            else if (al is "right" or "end" or "-webkit-right") shiftX = cw - lineW;
            if (shiftX < 0 && al != "center") shiftX = 0;
            /* place */
            foreach (var ob in open) StartFrag(ob, ox + shiftX, oy + y, lineH, true);
            foreach (var li in line)
            {
                var p = li.p; double px = ox + shiftX + li.x;
                switch (p.kind)
                {
                    case 0:
                        {
                            var tb = p.owner;
                            tb.pieces ??= new List<TextPiece>();
                            double shift = VShift(p.cs, b.cs, strut);
                            tb.pieces.Add(new TextPiece { text = p.text, x = px, y = baseY + shift, w = p.w, cs = p.cs, font = p.font, space = p.space });
                            if (firstLine && double.IsNaN(b.baseline)) b.baseline = baseY;
                            foreach (var ob in open) GrowFrag(ob, px + p.w, p.font, baseY + shift);
                            break;
                        }
                    case 4:
                        {
                            var ib = p.box; ib.inlineBox = true; ib.x = 0; ib.y = 0; ib.w = ib.h = 0;
                            ib.frags ??= new List<Frag>();
                            open.Add(ib);
                            StartFrag(ib, px + ib.ml, oy + y, lineH, false);
                            var f = ib.cs.Font;
                            GrowFrag(ib, px + p.w, f, baseY + VShift(ib.cs, b.cs, strut));
                            ib.frags[^1].first = true;
                            break;
                        }
                    case 5:
                        {
                            var ib = p.box;
                            if (ib.frags != null && ib.frags.Count > 0) { var fr = ib.frags[^1]; fr.w = Math.Max(fr.w, px + p.w - ib.mr - fr.x); fr.last = true; }
                            open.Remove(ib);
                            foreach (var ob in open) GrowFrag(ob, px + p.w, null, 0);
                            break;
                        }
                    case 6:
                        {
                            var a = p.box;
                            double bl = AtomBaseline(a);
                            string va = a.cs.vAlign;
                            double ty;
                            if (va == "middle") ty = baseY - strut.px * 0.25 - (a.mt + a.h + a.mb) / 2;
                            else if (va == "top") ty = oy + y;
                            else if (va == "bottom") ty = oy + y + lineH - (a.mt + a.h + a.mb);
                            else ty = baseY + VShift(a.cs, b.cs, strut) - bl;
                            a.x = px + a.ml; a.y = ty + a.mt;
                            if (a.parent != b) { /* inside an inline: inline boxes sit at 0,0, so container coordinates hold */ }
                            if (firstLine && double.IsNaN(b.baseline)) b.baseline = baseY;
                            foreach (var ob in open) GrowFrag(ob, px + p.w, null, 0);
                            break;
                        }
                    case 7: p.box.staticX = px; p.box.staticY = oy + y; pend.Add(p.box); break;
                }
            }
            foreach (var ob in open) { var fr = ob.frags[^1]; fr.w = Math.Max(fr.w, ox + shiftX + lineW - fr.x); }
            if (lineH > 0) firstLine = false;
            y += lineH;
            i = j;
        }
        return y;
    }
    static bool AllowWrap(Piece p, Box b) => b.cs.whiteSpace is "normal" or "pre-wrap" or "pre-line";
    static void ResetInline(Box k)
    {
        if (k.isText) { k.pieces = null; return; }
        if (!Atomic(k) && !IsBlockLevel(k)) { k.frags = null; foreach (var c in k.kids) ResetInline(c); }
    }
    static void StartFrag(Box ib, double x, double lineTop, double lineH, bool cont)
    {
        var f = ib.cs.Font;
        ib.frags.Add(new Frag { x = x, y = double.NaN, w = 0, h = 0 });
    }
    static void GrowFrag(Box ib, double right, FontSpec f, double baseY)
    {
        var fr = ib.frags[^1];
        fr.w = Math.Max(fr.w, right - fr.x);
        if (double.IsNaN(fr.y) || f != null)
        {
            var ff = ib.cs.Font;
            double by = f != null ? baseY : double.IsNaN(fr.y) ? 0 : fr.y + ib.bt + ib.pt + ff.Ascent;
            if (double.IsNaN(fr.y) && f == null) return;
            double top = by - ff.Ascent - ib.pt - ib.bt;
            double h = ff.Ascent + ff.Descent + ib.pt + ib.pb + ib.bt + ib.bb;
            if (double.IsNaN(fr.y)) { fr.y = top; fr.h = h; }
        }
    }
    /* vertical-align that shifts the baseline (sub, super, lengths) */
    static double VShift(CStyle c, CStyle parent, FontSpec strut)
    {
        switch (c.vAlign)
        {
            case "baseline": case "middle": case "top": case "bottom": case "text-top": case "text-bottom": return 0;
            case "sub": return strut.px * 0.2;
            case "super": return -strut.px * 0.34;
            default: { var L = Cascade.ParseLen(c.vAlign, c.fontSize, 16); return L.IsVal ? -L.R(c.LineH(c.Font)) : 0; }
        }
    }
    /* an inline-block's baseline: its last line's, or its bottom margin edge when it has none or clips */
    static double AtomBaseline(Box a)
    {
        if (a.replaced || a.cs.Clips || double.IsNaN(a.baseline)) return a.mt + a.h;
        return a.mt + a.baseline;
    }

    /* ---- replaced elements: canvas, svg, inputs ---- */
    static (double w, double h) ReplacedSize(Box b, double cbW, double cbH, double knownW = double.NaN)
    {
        var e = b.el; double iw, ih;
        if (e.canvas != null) { iw = e.canvas.width; ih = e.canvas.height; }
        else if (e.localName == "svg")
        {
            double aw = Js.ToNumber(e.getAttribute("width") ?? ""), ah = Js.ToNumber(e.getAttribute("height") ?? "");
            var vb = Svg.ViewBox(e);
            if (double.IsNaN(aw) && double.IsNaN(ah) && vb != null) { iw = 300; ih = vb[3] > 0 ? 300 * vb[3] / vb[2] : 150; if (double.IsNaN(cbW)) { } }
            else { iw = double.IsNaN(aw) ? (vb != null && !double.IsNaN(ah) ? ah * vb[2] / vb[3] : 300) : aw; ih = double.IsNaN(ah) ? (vb != null ? iw * vb[3] / vb[2] : 150) : ah; }
        }
        else if (e.localName == "input" && e.type == "range") { iw = 129; ih = 16; }
        else if (e.localName == "input")
        {
            var f = b.cs.Font; double sz = Js.ToNumber(e.getAttribute("size") ?? "20"); if (double.IsNaN(sz)) sz = 20;
            iw = Math.Round(f.px * 0.55 * sz) + 1; ih = b.cs.LineH(f);
            if (b.cs.lineHeight < 0) ih = Math.Max(ih, f.Ascent + f.Descent);
            double sw0 = SpecW(b, b.cs.width, cbW), sh0 = SpecH(b, b.cs.height, cbH);
            if (!double.IsNaN(knownW)) sw0 = knownW;
            return (ClampW(b, double.IsNaN(sw0) ? iw + Frame(b) : sw0, cbW), ClampH(b, double.IsNaN(sh0) ? ih + FrameV(b) : sh0, cbH));
        }
        else { iw = 300; ih = 150; }
        double ratio = b.cs.aspect > 0 ? b.cs.aspect : (ih > 0 ? iw / ih : 1);
        double sw = SpecW(b, b.cs.width, cbW), sh = SpecH(b, b.cs.height, cbH);
        if (!double.IsNaN(knownW)) sw = knownW;
        double cwid, chei;
        if (!double.IsNaN(sw) && !double.IsNaN(sh)) { cwid = sw; chei = sh; }
        else if (!double.IsNaN(sw)) { cwid = sw; chei = (sw - Frame(b)) / ratio + FrameV(b); }
        else if (!double.IsNaN(sh)) { chei = sh; cwid = (sh - FrameV(b)) * ratio + Frame(b); }
        else { cwid = iw + Frame(b); chei = ih + FrameV(b); }
        cwid = ClampW(b, cwid, cbW);
        return (cwid, ClampH(b, chei, cbH));
    }

    /* ---- flexbox ---- */
    static IEnumerable<Box> FlexItems(Box b)
    {
        foreach (var k in b.kids)
        {
            if (k.isText) continue;
            if (k.cs.OutOfFlow) continue;
            yield return k;
        }
    }
    static readonly Dictionary<CStyle, CStyle> anonCs = new();
    static CStyle AnonCs(CStyle p)
    {
        if (anonCs.TryGetValue(p, out var c)) return c;
        c = new CStyle
        {
            display = Disp.Block, color = p.color, family = p.family, fontSize = p.fontSize, weight = p.weight, italic = p.italic, lineHeight = p.lineHeight, lhNum = p.lhNum,
            letter = p.letter, textAlign = p.textAlign, textTransform = p.textTransform, whiteSpace = p.whiteSpace, cursor = p.cursor, hidden = p.hidden, noPointer = p.noPointer,
            textShadow = p.textShadow, wordBreak = p.wordBreak, deco = p.deco, vars = p.vars, fill = p.fill, stroke = p.stroke,
        };
        if (anonCs.Count > 4000) anonCs.Clear();
        anonCs[p] = c;
        return c;
    }
    sealed class FItem
    {
        public Box b; public double basis, hyp, main, cross, minMain, maxMain, outerMainExtra, outerCrossExtra; public bool frozen;
        public double grow, shrink; public string align;
    }
    static double LayoutFlex(Box b, double cw, double chDef, List<Box> pend)
    {
        var cs = b.cs;
        bool row = cs.flexDir is 0 or 2, rev = cs.flexDir is 2 or 3;
        double ox = b.bl + b.pl, oy = b.bt + b.pt;
        double mainAvail = row ? cw : chDef;
        double gapMain = row ? cs.colGap.R(cw) : cs.rowGap.R(cw), gapCross = row ? cs.rowGap.R(cw) : cs.colGap.R(cw);
        var items = new List<FItem>();
        int ord = 0;
        var src = FlexItems(b).Select(k => (k, ord++)).OrderBy(t => t.k.cs.order).ThenBy(t => t.Item2).Select(t => t.k).ToList();
        foreach (var k in src)
        {
            k.fixedW = double.NaN; k.fixedH = double.NaN;
            Edges(k, cw);
            var it = new FItem { b = k, grow = k.cs.grow, shrink = k.cs.shrink };
            it.align = k.cs.alignSelf is "auto" or "normal" ? (cs.alignItems == "normal" ? "stretch" : cs.alignItems) : k.cs.alignSelf;
            double mlr = (k.cs.mL.IsAuto ? 0 : k.ml) + (k.cs.mR.IsAuto ? 0 : k.mr), mtb = (k.cs.mT.IsAuto ? 0 : k.mt) + (k.cs.mB.IsAuto ? 0 : k.mb);
            it.outerMainExtra = row ? mlr : mtb; it.outerCrossExtra = row ? mtb : mlr;
            /* the flex base size */
            double basis = double.NaN;
            var bl = k.cs.basis;
            if (bl.IsVal) basis = row ? SpecW(k, bl, mainAvail) : SpecH(k, bl, mainAvail);
            if (double.IsNaN(basis) && (bl.IsAuto || bl.IsVal)) basis = row ? SpecW(k, k.cs.width, cw) : SpecH(k, k.cs.height, chDef);
            if (double.IsNaN(basis))
            {
                if (row) basis = k.replaced ? ReplacedSize(k, cw, chDef).w : Intrinsic(k).max;
                else
                {
                    double crossW = CrossWidthForColumn(k, it, cw);
                    k.fixedW = crossW;
                    LayoutBlockish(k, cw, double.NaN, pend, false);
                    basis = k.h;
                    k.fixedW = double.NaN;
                }
            }
            it.basis = basis;
            /* min/max main */
            double mn, mx;
            if (row)
            {
                mn = k.cs.minW.IsVal ? SpecW(k, k.cs.minW, cw) : double.NaN;
                if (double.IsNaN(mn))
                {
                    /* min-width:auto is the content's min-content size (unless it clips) */
                    if (k.cs.Clips) mn = Frame(k);
                    else { mn = k.replaced ? ReplacedSize(k, cw, chDef).w : Intrinsic(k).min; double sw = SpecW(k, k.cs.width, cw); if (!double.IsNaN(sw)) mn = Math.Min(mn, sw); }
                }
                mx = k.cs.maxW.IsNone ? double.PositiveInfinity : SpecW(k, k.cs.maxW, cw);
            }
            else
            {
                mn = k.cs.minH.IsVal ? SpecH(k, k.cs.minH, chDef) : double.NaN;
                if (double.IsNaN(mn))
                {
                    if (k.cs.Clips) mn = FrameV(k);
                    else
                    {
                        double crossW = CrossWidthForColumn(k, it, cw);
                        k.fixedW = crossW; LayoutBlockish(k, cw, double.NaN, pend, false); mn = k.h; k.fixedW = double.NaN;
                        double sh = SpecH(k, k.cs.height, chDef); if (!double.IsNaN(sh)) mn = Math.Min(mn, sh);
                    }
                }
                mx = k.cs.maxH.IsNone ? double.PositiveInfinity : SpecH(k, k.cs.maxH, chDef);
            }
            if (double.IsNaN(mx)) mx = double.PositiveInfinity; if (double.IsNaN(mn)) mn = 0;
            it.minMain = mn; it.maxMain = Math.Max(mn, mx);
            it.hyp = Math.Max(mn, Math.Min(it.maxMain, basis));
            items.Add(it);
        }
        /* lines */
        var lines = new List<List<FItem>>();
        double avail = double.IsNaN(mainAvail) ? double.PositiveInfinity : mainAvail;
        {
            var cur = new List<FItem>(); double used = 0;
            foreach (var it in items)
            {
                double ow = it.hyp + it.outerMainExtra;
                if (cs.wrap && cur.Count > 0 && used + gapMain + ow > avail + 0.01) { lines.Add(cur); cur = new List<FItem>(); used = 0; }
                used += (cur.Count > 0 ? gapMain : 0) + ow; cur.Add(it);
            }
            if (cur.Count > 0 || lines.Count == 0) lines.Add(cur);
        }
        /* resolve flexible lengths per line */
        double contentMain = 0;
        foreach (var line in lines)
        {
            double sumHyp = line.Sum(t => t.hyp + t.outerMainExtra) + gapMain * Math.Max(0, line.Count - 1);
            double space = double.IsInfinity(avail) ? 0 : avail - sumHyp;
            bool growing = space > 0;
            foreach (var t in line) { t.main = t.hyp; t.frozen = growing ? t.grow == 0 || t.basis > t.hyp && false : t.shrink == 0; }
            if (!double.IsInfinity(avail))
                for (int guard = 0; guard < 10; guard++)
                {
                    var live = line.Where(t => !t.frozen).ToList();
                    if (live.Count == 0) break;
                    double used = line.Sum(t => (t.frozen ? t.main : t.basis) + t.outerMainExtra) + gapMain * Math.Max(0, line.Count - 1);
                    double free = avail - used;
                    if (growing)
                    {
                        double sg = live.Sum(t => t.grow); if (sg <= 0) break;
                        if (sg < 1) free *= sg;
                        foreach (var t in live) t.main = t.basis + free * t.grow / sg;
                    }
                    else
                    {
                        double ss = live.Sum(t => t.shrink * t.basis); if (ss <= 0) break;
                        foreach (var t in live) t.main = t.basis + free * (t.shrink * t.basis) / ss;
                    }
                    double viol = 0;
                    foreach (var t in live) { double cl = Math.Max(t.minMain, Math.Min(t.maxMain, t.main)); viol += cl - t.main; }
                    if (Math.Abs(viol) < 0.01) { foreach (var t in live) t.main = Math.Max(t.minMain, Math.Min(t.maxMain, t.main)); break; }
                    foreach (var t in live)
                    {
                        double cl = Math.Max(t.minMain, Math.Min(t.maxMain, t.main));
                        if (viol > 0 && cl > t.main || viol < 0 && cl < t.main) { t.main = cl; t.frozen = true; }
                    }
                }
            contentMain = Math.Max(contentMain, line.Sum(t => t.main + t.outerMainExtra) + gapMain * Math.Max(0, line.Count - 1));
        }
        /* cross sizes: lay each item out at its main size */
        var lineCross = new List<double>();
        double crossAvail = row ? chDef : cw;
        foreach (var line in lines)
        {
            double lc = 0, maxAsc = 0, maxDesc = 0;
            foreach (var t in line)
            {
                var k = t.b;
                if (row)
                {
                    k.fixedW = t.main;
                    k.fixedH = double.NaN;
                    LayoutBlockish(k, cw, chDef, pend, false);
                    t.cross = k.h;
                }
                else
                {
                    double crossW = CrossWidthForColumn(k, t, cw);
                    k.fixedW = crossW; k.fixedH = t.main;
                    LayoutBlockish(k, cw, chDef, pend, false);
                    t.cross = k.w;
                }
                if (t.align == "baseline" && row)
                {
                    double bl = double.IsNaN(k.baseline) ? k.h : k.baseline;
                    maxAsc = Math.Max(maxAsc, k.mt + bl); maxDesc = Math.Max(maxDesc, k.h - bl + k.mb);
                }
                lc = Math.Max(lc, t.cross + t.outerCrossExtra);
            }
            lc = Math.Max(lc, maxAsc + maxDesc);
            lineCross.Add(lc);
        }
        double totalCross = lineCross.Sum() + gapCross * Math.Max(0, lines.Count - 1);
        /* a single line in a container with a definite cross size fills it */
        if (!double.IsNaN(crossAvail) && lines.Count == 1 && (!cs.wrap || true) && lines.Count == 1) { if (!cs.wrap) lineCross[0] = crossAvail; }
        double crossExtra = !double.IsNaN(crossAvail) ? crossAvail - totalCross : 0;
        double crossStart = 0, crossBetween = gapCross;
        if (cs.wrap && !double.IsNaN(crossAvail) && crossExtra > 0)
        {
            string ac = cs.alignContent;
            if (ac is "normal" or "stretch") { double add = crossExtra / lines.Count; for (int q = 0; q < lineCross.Count; q++) lineCross[q] += add; }
            else if (ac is "center") crossStart = crossExtra / 2;
            else if (ac is "flex-end" or "end") crossStart = crossExtra;
            else if (ac == "space-between" && lines.Count > 1) crossBetween += crossExtra / (lines.Count - 1);
            else if (ac == "space-around") { crossBetween += crossExtra / lines.Count; crossStart = crossExtra / lines.Count / 2; }
        }
        /* place */
        double cpos = crossStart;
        double maxMainExtent = 0, maxCrossExtent = 0;
        for (int li = 0; li < lines.Count; li++)
        {
            var line = lines[li]; double lc = lineCross[li];
            /* stretch */
            foreach (var t in line)
            {
                var k = t.b;
                bool autoCross = row ? k.cs.height.IsAuto && !k.cs.mT.IsAuto && !k.cs.mB.IsAuto : k.cs.width.IsAuto && !k.cs.mL.IsAuto && !k.cs.mR.IsAuto;
                if (t.align == "stretch" && autoCross)
                {
                    double target = Math.Max(0, lc - t.outerCrossExtra);
                    if (row)
                    {
                        target = ClampH(k, target, chDef);
                        if (Math.Abs(target - k.h) > 0.01) { k.fixedW = t.main; k.fixedH = target; LayoutBlockish(k, cw, chDef, pend, false); }
                        t.cross = k.h;
                    }
                    else t.cross = k.w;
                }
            }
            double used = line.Sum(t => t.main + t.outerMainExtra) + gapMain * Math.Max(0, line.Count - 1);
            double free = (double.IsInfinity(avail) || double.IsNaN(mainAvail) ? 0 : mainAvail - used);
            /* auto margins on the main axis take the free space first */
            int autoM = 0;
            foreach (var t in line) { var k = t.b; if (row) { if (k.cs.mL.IsAuto) autoM++; if (k.cs.mR.IsAuto) autoM++; } else { if (k.cs.mT.IsAuto) autoM++; if (k.cs.mB.IsAuto) autoM++; } }
            double perAuto = autoM > 0 && free > 0 ? free / autoM : 0;
            double start = 0, between = gapMain;
            if (autoM == 0 || free <= 0)
            {
                string jc = cs.justify;
                double f2 = free;
                if (cs.justifySafe && f2 < 0) f2 = 0;
                switch (jc)
                {
                    case "center": start = f2 / 2; break;
                    case "flex-end": case "end": case "right": start = f2; break;
                    case "space-between": if (line.Count > 1 && free > 0) between += free / (line.Count - 1); break;
                    case "space-around": if (free > 0) { between += free / line.Count; start = free / line.Count / 2; } break;
                    case "space-evenly": if (free > 0) { between += free / (line.Count + 1); start = free / (line.Count + 1); } break;
                }
                if (rev && jc is "normal" or "flex-start" or "start") start = free;
            }
            double mpos = start;
            var seq = rev ? Enumerable.Reverse(line).ToList() : line;
            double lineAsc = 0;
            if (row) foreach (var t in line) if (t.align == "baseline") { double bl = double.IsNaN(t.b.baseline) ? t.b.h : t.b.baseline; lineAsc = Math.Max(lineAsc, t.b.mt + bl); }
            foreach (var t in seq)
            {
                var k = t.b;
                double mStart, mEnd, cStart, cEnd;
                if (row) { mStart = k.cs.mL.IsAuto ? perAuto : k.ml; mEnd = k.cs.mR.IsAuto ? perAuto : k.mr; cStart = k.cs.mT.IsAuto ? 0 : k.mt; cEnd = k.cs.mB.IsAuto ? 0 : k.mb; }
                else { mStart = k.cs.mT.IsAuto ? perAuto : k.mt; mEnd = k.cs.mB.IsAuto ? perAuto : k.mb; cStart = k.cs.mL.IsAuto ? 0 : k.ml; cEnd = k.cs.mR.IsAuto ? 0 : k.mr; }
                double itemMain = row ? k.w : k.h, itemCross = row ? k.h : k.w;
                double cOff;
                bool autoC1 = row ? k.cs.mT.IsAuto : k.cs.mL.IsAuto, autoC2 = row ? k.cs.mB.IsAuto : k.cs.mR.IsAuto;
                double cFree = lc - itemCross - cStart - cEnd;
                if (autoC1 && autoC2) cOff = cStart + Math.Max(0, cFree) / 2;
                else if (autoC1) cOff = cStart + Math.Max(0, cFree);
                else switch (t.align)
                    {
                        case "center": cOff = cStart + cFree / 2; break;
                        case "flex-end": case "end": case "self-end": cOff = cStart + cFree; break;
                        case "baseline": cOff = row ? lineAsc - (double.IsNaN(k.baseline) ? k.h : k.baseline) : cStart; break;
                        default: cOff = cStart; break;
                    }
                double mm = mpos + mStart;
                if (row) { k.x = ox + mm; k.y = oy + cpos + cOff; } else { k.x = ox + cpos + cOff; k.y = oy + mm; }
                if (row && rev == false && double.IsNaN(b.baseline) && li == 0 && !double.IsNaN(k.baseline)) b.baseline = k.y + k.baseline;
                if (!row && double.IsNaN(b.baseline) && !double.IsNaN(k.baseline)) b.baseline = k.y + k.baseline;
                mpos = mm + itemMain + mEnd + between;
                maxMainExtent = Math.Max(maxMainExtent, mm + itemMain + mEnd);
                maxCrossExtent = Math.Max(maxCrossExtent, cpos + cOff + itemCross + cEnd);
            }
            if (row && double.IsNaN(b.baseline) && li == 0 && line.Count > 0) { var f0 = line[0].b; b.baseline = f0.y + (double.IsNaN(f0.baseline) ? f0.h : f0.baseline); }
            cpos += lc + crossBetween;
        }
        foreach (var k in b.kids) if (!k.isText && k.cs.OutOfFlow) { k.staticX = ox; k.staticY = oy; pend.Add(k); }
        double contentCross = Math.Max(totalCross, maxCrossExtent);
        if (row) { b.contentW = Math.Max(cw, maxMainExtent) + b.pl + b.pr; b.contentH = contentCross + b.pt + b.pb; return contentCross; }
        double mainUsed = lines.Count > 0 ? contentMain : 0;
        b.contentW = Math.Max(cw, maxCrossExtent) + b.pl + b.pr; b.contentH = Math.Max(mainUsed, maxMainExtent) + b.pt + b.pb;
        return mainUsed;
    }
    /* a column item's width: its own, stretched to the container, or shrink-to-fit */
    static double CrossWidthForColumn(Box k, FItem it, double cw)
    {
        double sw = SpecW(k, k.cs.width, cw);
        if (!double.IsNaN(sw)) return ClampW(k, sw, cw);
        double avail = Math.Max(0, cw - it.outerCrossExtra);
        if (it.align == "stretch" && !k.cs.mL.IsAuto && !k.cs.mR.IsAuto) return ClampW(k, avail, cw);
        if (k.replaced) return ReplacedSize(k, cw, double.NaN).w;
        var (mn, mx) = Intrinsic(k);
        return ClampW(k, Math.Min(Math.Max(mn, avail), mx), cw);
    }

    /* ---- grid ---- */
    static int CountTracks(List<GTrack> t, double W, double gap, int nItems)
    {
        if (t == null) return 1;
        int n = 0;
        foreach (var x in t)
        {
            if (x.kind == "repeat")
            {
                if (x.autoFit || x.autoFill)
                {
                    double each = x.inner.Sum(q => TrackMin(q, W)) + gap * Math.Max(0, x.inner.Count - 1);
                    int cnt = double.IsNaN(W) || each <= 0 ? 1 : Math.Max(1, (int)Math.Floor((W + gap) / (each + gap)));
                    n += cnt * x.inner.Count;
                }
                else n += x.repeat * x.inner.Count;
            }
            else n++;
        }
        return n;
    }
    static double TrackMin(GTrack t, double W) => t.kind switch { "len" => t.len.R(W), "minmax" => TrackMin(t.min, W), _ => 0 };
    static List<GTrack> Expand(List<GTrack> t, double W, double gap, out bool autoFit, out int fitPer)
    {
        autoFit = false; fitPer = 0;
        var l = new List<GTrack>();
        if (t == null) { l.Add(new GTrack { kind = "fr", v = 1 }); return l; }
        double fixedOther = 0; int otherCount = 0;
        foreach (var x in t) if (x.kind != "repeat") { fixedOther += TrackMin(x, W); otherCount++; }
        foreach (var x in t)
        {
            if (x.kind != "repeat") { l.Add(x); continue; }
            int cnt = x.repeat;
            if (x.autoFit || x.autoFill)
            {
                double each = x.inner.Sum(q => TrackMin(q, W)) + gap * Math.Max(0, x.inner.Count - 1);
                double room = W - fixedOther - gap * otherCount;
                cnt = double.IsNaN(W) || each <= 0 ? 1 : Math.Max(1, (int)Math.Floor((room + gap) / (each + gap)));
                autoFit = x.autoFit; fitPer = x.inner.Count;
            }
            for (int r = 0; r < cnt; r++) l.AddRange(x.inner);
        }
        return l;
    }
    static double LayoutGrid(Box b, double cw, double chDef, List<Box> pend)
    {
        var cs = b.cs;
        double ox = b.bl + b.pl, oy = b.bt + b.pt;
        double gapC = cs.colGap.R(cw), gapR = cs.rowGap.R(cw);
        var items = FlexItems(b).ToList();
        var tracks = Expand(cs.gridCols, cw, gapC, out bool autoFit, out _);
        int nc = Math.Max(1, tracks.Count);
        /* placement: row-major auto placement with column spans */
        var place = new List<(Box k, int c, int span, int r)>();
        int col = 0, rowI = 0;
        foreach (var k in items)
        {
            Edges(k, cw);
            int span = 1, start = -1;
            string s = k.cs.gcStart, e = k.cs.gcEnd;
            if (s != null && s.StartsWith("span")) span = Math.Max(1, (int)Js.Or(Js.ParseFloat(s.Substring(4)), 1));
            else if (s != null && s != "auto") { int si = (int)Js.Or(Js.ParseFloat(s), 1); start = si > 0 ? si - 1 : nc + si; }
            if (e != null && e.StartsWith("span")) span = Math.Max(1, (int)Js.Or(Js.ParseFloat(e.Substring(4)), 1));
            else if (e != null && e != "auto" && start >= 0) { int ei = (int)Js.Or(Js.ParseFloat(e), start + 2); int end = ei > 0 ? ei - 1 : nc + 1 + ei; span = Math.Max(1, end - start); }
            span = Math.Min(span, nc);
            if (start >= 0) { if (start < col) rowI++; col = start; }
            if (col + span > nc) { col = 0; rowI++; }
            place.Add((k, col, span, rowI));
            col += span;
            if (col >= nc) { col = 0; rowI++; }
        }
        int nr = place.Count == 0 ? 0 : place.Max(p => p.r) + 1;
        /* auto-fit collapses the tracks nothing landed in */
        int usedCols = place.Count == 0 ? 0 : place.Max(p => p.c + p.span);
        var size = new double[nc];
        var isFr = new bool[nc]; var fr = new double[nc];
        double fixedSum = 0;
        for (int i = 0; i < nc; i++)
        {
            var t = tracks[i];
            if (autoFit && i >= usedCols) { size[i] = 0; continue; }
            switch (t.kind)
            {
                case "len": size[i] = t.len.R(cw); break;
                case "fr": isFr[i] = true; fr[i] = t.v; break;
                case "minmax":
                    size[i] = TrackMin(t.min, cw);
                    if (t.max.kind == "fr") { isFr[i] = true; fr[i] = t.max.v; }
                    else if (t.max.kind == "len") size[i] = Math.Max(size[i], t.max.len.R(cw));
                    break;
                default:
                    {
                        double m = 0;
                        foreach (var p in place) if (p.c == i && p.span == 1) m = Math.Max(m, Contribution(p.k).max);
                        size[i] = m; break;
                    }
            }
            if (!isFr[i]) fixedSum += size[i];
        }
        int liveCols = autoFit ? Math.Max(1, usedCols) : nc;
        double gaps = gapC * Math.Max(0, liveCols - 1);
        double sumFr = 0; for (int i = 0; i < nc; i++) if (isFr[i] && !(autoFit && i >= usedCols)) sumFr += fr[i];
        if (sumFr > 0)
        {
            double rem = Math.Max(0, cw - gaps - fixedSum);
            for (int i = 0; i < nc; i++) if (isFr[i] && !(autoFit && i >= usedCols)) { double minB = tracks[i].kind == "minmax" ? TrackMin(tracks[i].min, cw) : 0; size[i] = Math.Max(minB, rem * fr[i] / Math.Max(1, sumFr)); }
        }
        else
        {
            /* auto tracks stretch into leftover space (justify-content: normal) */
            int autos = 0; for (int i = 0; i < nc; i++) if (tracks[i].kind == "auto" && !(autoFit && i >= usedCols)) autos++;
            double rem = cw - gaps - fixedSum;
            if (autos > 0 && rem > 0) for (int i = 0; i < nc; i++) if (tracks[i].kind == "auto") size[i] += rem / autos;
        }
        var colX = new double[nc + 1];
        int lastLive = (autoFit ? Math.Max(1, usedCols) : nc) - 1;
        for (int i = 0; i < nc; i++) colX[i + 1] = colX[i] + (i > lastLive ? 0 : size[i] + (i < lastLive ? gapC : 0));
        double SpanW(int c, int span) => colX[c + span] - colX[c] - (c + span - 1 < lastLive ? gapC : 0);
        /* rows: the tallest item in each */
        var rowH = new double[Math.Max(1, nr)];
        foreach (var p in place)
        {
            var k = p.k;
            double w = SpanW(p.c, p.span);
            double ww = w - (k.cs.mL.IsAuto ? 0 : k.ml) - (k.cs.mR.IsAuto ? 0 : k.mr);
            double sw = SpecW(k, k.cs.width, w);
            k.fixedW = !double.IsNaN(sw) ? ClampW(k, sw, w) : ClampW(k, Math.Max(0, ww), w);
            k.fixedH = double.NaN;
            LayoutBlockish(k, w, double.NaN, pend, false);
            rowH[p.r] = Math.Max(rowH[p.r], k.h + k.mt + k.mb);
        }
        var rowY = new double[rowH.Length + 1];
        for (int r = 0; r < rowH.Length; r++) rowY[r + 1] = rowY[r] + rowH[r] + (r < rowH.Length - 1 ? gapR : 0);
        foreach (var p in place)
        {
            var k = p.k;
            string al = k.cs.alignSelf is "auto" or "normal" ? (cs.alignItems == "normal" ? "stretch" : cs.alignItems) : k.cs.alignSelf;
            double rh = rowH[p.r];
            if (al == "stretch" && k.cs.height.IsAuto && Math.Abs(k.h + k.mt + k.mb - rh) > 0.01) { k.fixedH = rh - k.mt - k.mb; LayoutBlockish(k, SpanW(p.c, p.span), double.NaN, pend, false); }
            double dy = al == "center" ? (rh - k.h - k.mt - k.mb) / 2 : al is "end" or "flex-end" ? rh - k.h - k.mt - k.mb : 0;
            if (al == "baseline") dy = 0;
            k.x = ox + colX[p.c] + k.ml; k.y = oy + rowY[p.r] + k.mt + dy;
            if (double.IsNaN(b.baseline) && !double.IsNaN(k.baseline)) b.baseline = k.y + k.baseline;
        }
        foreach (var k in b.kids) if (!k.isText && k.cs.OutOfFlow) { k.staticX = ox; k.staticY = oy; pend.Add(k); }
        double H = rowY[rowH.Length];
        b.contentW = Math.Max(cw, colX[nc]) + b.pl + b.pr; b.contentH = H + b.pt + b.pb;
        return H;
    }

    /* ---- absolutely positioned boxes, once their containing block's size is known ---- */
    static void PlaceAbsFor(Box cb, List<Box> pend)
    {
        var mine = pend.Distinct().ToList(); pend.Clear();
        foreach (var k in mine) PlaceOne(k, cb);
    }
    static void PlaceAbs(List<Box> pend) { foreach (var k in pend.Distinct().ToList()) PlaceOne(k, Root); }
    static void PlaceOne(Box k, Box cb)
    {
        bool fix = k.cs.position == Pos.Fixed;
        double cbW, cbH, cbX, cbY;   /* the CB's padding box, in the CB's local coordinates */
        if (fix) { cbW = VW; cbH = VH; cbX = 0; cbY = 0; }
        else { cbW = cb.PadW; cbH = cb.PadH; cbX = cb.bl; cbY = cb.bt; }
        k.cbBox = fix ? null : cb;
        var c = k.cs;
        Edges(k, cbW);
        double L = c.left.IsAuto ? double.NaN : c.left.R(cbW), R = c.right.IsAuto ? double.NaN : c.right.R(cbW);
        double T = c.top.IsAuto ? double.NaN : c.top.R(cbH), B = c.bottom.IsAuto ? double.NaN : c.bottom.R(cbH);
        double ml = c.mL.IsAuto ? 0 : k.ml, mr = c.mR.IsAuto ? 0 : k.mr, mt = c.mT.IsAuto ? 0 : k.mt, mb = c.mB.IsAuto ? 0 : k.mb;
        double sw = SpecW(k, c.width, cbW);
        k.fixedW = double.NaN; k.fixedH = double.NaN;
        if (double.IsNaN(sw) && !double.IsNaN(L) && !double.IsNaN(R)) k.fixedW = ClampW(k, Math.Max(0, cbW - L - R - ml - mr), cbW);
        double sh = SpecH(k, c.height, cbH);
        if (double.IsNaN(sh) && !double.IsNaN(T) && !double.IsNaN(B)) k.fixedH = ClampH(k, Math.Max(0, cbH - T - B - mt - mb), cbH);
        /* shrink-to-fit against the room the insets leave */
        double room = cbW - (double.IsNaN(L) ? 0 : L) - (double.IsNaN(R) ? 0 : R);
        if (double.IsNaN(sw) && double.IsNaN(k.fixedW) && !k.replaced)
        {
            var (mn, mx) = Intrinsic(k);
            double avail = (double.IsNaN(L) && double.IsNaN(R)) ? cbW - (k.staticX - cbX) : room;
            k.fixedW = ClampW(k, Math.Min(Math.Max(mn, avail - ml - mr), mx), cbW);
        }
        LayoutBlockish(k, cbW, cbH, new List<Box>(), true);
        ml = k.ml; mr = k.mr; mt = k.mt; mb = k.mb;
        /* auto margins centre when both insets are set */
        if (!double.IsNaN(L) && !double.IsNaN(R) && c.mL.IsAuto && c.mR.IsAuto) { double fr = cbW - L - R - k.w; ml = mr = Math.Max(0, fr / 2); }
        if (!double.IsNaN(T) && !double.IsNaN(B) && c.mT.IsAuto && c.mB.IsAuto) { double fr = cbH - T - B - k.h; mt = mb = Math.Max(0, fr / 2); }
        double x, y;
        if (!double.IsNaN(L)) x = cbX + L + ml;
        else if (!double.IsNaN(R)) x = cbX + cbW - R - mr - k.w;
        else x = StaticIn(k, cb, true) + ml;
        if (!double.IsNaN(T)) y = cbY + T + mt;
        else if (!double.IsNaN(B)) y = cbY + cbH - B - mb - k.h;
        else y = StaticIn(k, cb, false) + mt;
        /* the box's x/y are relative to its parent box: convert from the CB's frame */
        if (fix) { k.x = x; k.y = y; }
        else
        {
            var (dx, dy) = Offset(k.parent, cb);
            k.x = x - dx; k.y = y - dy;
        }
    }
    /* the static position, expressed in the CB's frame */
    static double StaticIn(Box k, Box cb, bool horiz)
    {
        if (k.parent == null) return 0;
        var (dx, dy) = Offset(k.parent, cb);
        return horiz ? k.staticX + dx : k.staticY + dy;
    }
    /* where `from`'s origin sits in `to`'s coordinates (to an ancestor of from) */
    static (double, double) Offset(Box from, Box to)
    {
        double x = 0, y = 0;
        for (var b = from; b != null && b != to; b = b.parent) { x += b.x; y += b.y; }
        return (x, y);
    }
}
