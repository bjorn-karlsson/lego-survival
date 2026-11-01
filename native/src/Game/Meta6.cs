// Port of lego-survival.html lines 15718-16309: the skill tree's screen -- the
// SVG map with zoom and pan, the search box, the side panel, presets, the
// tooltip card, the title's tree button and opening/closing the tree.
using System.Text;

namespace Brickblade;

public sealed class TreeView { public double x, y, w, h, cx, cy; }
public sealed class TreeDragSt { public double moved; }

public static partial class G
{
    public static TreeView TREE_VIEW;
    /* run from boot(): the map has to be built first */
    public static void TreeViewInit()
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        foreach (var n in META_NODES)
        {
            if (n.x < x0) x0 = n.x; if (n.x > x1) x1 = n.x;
            if (n.y < y0) y0 = n.y; if (n.y > y1) y1 = n.y;
        }
        const double PAD = 130;
        TREE_VIEW = new TreeView { x = x0 - PAD, y = y0 - PAD, w = (x1 - x0) + PAD * 2, h = (y1 - y0) + PAD * 2, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2 };
    }
    public static bool treeOn = false, treeRefund = false;
    public static string treePick = null, treeHover = null;
    /* one line of feedback after SAVE or LOAD, shown on the next render only */
    public static string treePresetMsg = "";
    /* the three country washes. Remembered like every other preference. */
    public static bool treeSectors = false;
    public static void TreeSectorsLoad() { try { treeSectors = Store.getItem("bb_treesec") == "1"; } catch { } }
    public static bool treeToggleSectors()
    {
        treeSectors = !treeSectors;
        try { Store.setItem("bb_treesec", treeSectors ? "1" : "0"); } catch { }
        renderTree();
        return treeSectors;
    }
    /* ZOOM AND PAN. A map this size at one zoom level is a picture of a tree rather
       than a thing you read. The view is one viewBox attribute, written straight
       onto the <svg> — re-rendering four hundred nodes on every wheel tick would
       make the whole thing feel like mud. */
    /* THE SCALE THE LABEL READS. The whole map is 0% -- it is a picture, not a
       reading distance; three times closer is 100%, where a node's name can be
       read, and it goes on to 200% at nine times. The steps are even on the
       label: each wheel tick adds the same few percent however deep you are. */
    public const double TREE_ZOOM_MIN = 1, TREE_ZOOM_MAX = 9, TREE_ZOOM_HOME = 3;
    public static double treeZoom = 1, treeCx = 0, treeCy = 0;
    public static TreeDragSt treeDrag = null;
    public static double treeZoomPct(double z = double.NaN) => Math.Round(Math.Log(double.IsNaN(z) ? treeZoom : z) / Math.Log(TREE_ZOOM_HOME) * 100);
    public static void treeFit()
    {
        treeZoom = 1; treeCx = TREE_VIEW.x + TREE_VIEW.w / 2;
        treeCy = TREE_VIEW.y + TREE_VIEW.h / 2; treeApplyView();
    }
    /* 100%, on your own starting point: where the tree opens */
    public static void treeHome()
    {
        var n = META_BY_ID.GetValueOrDefault(metaStartId());
        treeZoom = TREE_ZOOM_HOME;
        treeCx = n != null ? n.x : TREE_VIEW.x + TREE_VIEW.w / 2; treeCy = n != null ? n.y : TREE_VIEW.y + TREE_VIEW.h / 2;
        treeApplyView();
    }
    public static void treeApplyView()
    {
        var svg = document.getElementById("treeSvg");
        if (svg == null) return;
        treeZoom = clamp(treeZoom, TREE_ZOOM_MIN, TREE_ZOOM_MAX);
        double w = TREE_VIEW.w / treeZoom, h = TREE_VIEW.h / treeZoom;
        /* you can push the edge of the map to the middle of the view and no further,
           so it is never possible to lose the tree off the side of the screen */
        treeCx = clamp(treeCx, TREE_VIEW.x, TREE_VIEW.x + TREE_VIEW.w);
        treeCy = clamp(treeCy, TREE_VIEW.y, TREE_VIEW.y + TREE_VIEW.h);
        svg.setAttribute("viewBox", $"{S(treeCx - w / 2)} {S(treeCy - h / 2)} {S(w)} {S(h)}");
        var z = document.getElementById("treeZoomLbl");
        if (z != null) z.textContent = S(treeZoomPct()) + "%";
        treeFindSize();
    }
    /* where a screen point lands on the map, letterboxing and all */
    public static DOMPoint treeUserXY(Element svg, DomEvent ev)
    {
        try
        {
            var pt = svg.createSVGPoint(); pt.x = ev.clientX; pt.y = ev.clientY;
            return pt.matrixTransform(svg.getScreenCTM().inverse());
        }
        catch { return new DOMPoint { x = treeCx, y = treeCy }; }
    }
    public static void treeZoomBy(double mul, DomEvent ev = null)
    {
        var svg = document.getElementById("treeSvg");
        if (svg == null) return;
        double was = treeZoom;
        double want = clamp(treeZoom * mul, TREE_ZOOM_MIN, TREE_ZOOM_MAX);
        if (want == was) return;
        /* zoom toward the cursor, not toward the middle — otherwise the thing you
           were looking at is the first thing to leave */
        var p = ev != null ? treeUserXY(svg, ev) : new DOMPoint { x = treeCx, y = treeCy };
        treeZoom = want;
        treeCx = p.x - (p.x - treeCx) * (was / want);
        treeCy = p.y - (p.y - treeCy) * (was / want);
        treeApplyView();
    }
    /* a press that wanders less than this is a CLICK, not a pan — a hand on a
       mouse always moves a pixel or two between pressing and letting go */
    public const double TREE_DRAG_SLOP = 6;
    public static bool treeDragged = false, treeWinBound = false;
    public static void treeBindView(Element svg)
    {
        svg.addEventListener("wheel", ev =>
        {
            ev.preventDefault();
            treeZoomBy(ev.deltaY < 0 ? 1.18 : 1 / 1.18, ev);
        }, new { passive = false });
        /* NO POINTER CAPTURE HERE. Capturing on the <svg> retargets the pointer to
           it, and the click that follows is then delivered to the <svg> and never
           reaches the node that was pressed — which is every node, which is the whole
           tree. The window hears the drag instead, and the nodes keep their clicks. */
        svg.addEventListener("pointerdown", ev =>
        {
            if (ev.button != 0 && ev.button != 1) return;
            treeDrag = new TreeDragSt { moved = 0 };
        });
        treeBindWindow();
    }
    public static void treeBindWindow()
    {
        if (treeWinBound) return;
        treeWinBound = true;
        window.addEventListener("pointermove", ev =>
        {
            if (treeDrag == null) return;
            treeDrag.moved += Math.Abs(Js.Or(ev.movementX, 0)) + Math.Abs(Js.Or(ev.movementY, 0));
            if (treeDrag.moved <= TREE_DRAG_SLOP) return;
            var svg = document.getElementById("treeSvg");
            if (svg == null) return;
            /* the map moves exactly as far as the cursor did, however far you are in */
            var r = svg.getBoundingClientRect();
            if (r.width == 0 || r.height == 0) return;
            double sc = Math.Max((TREE_VIEW.w / treeZoom) / r.width, (TREE_VIEW.h / treeZoom) / r.height);
            treeCx -= Js.Or(ev.movementX, 0) * sc;
            treeCy -= Js.Or(ev.movementY, 0) * sc;
            treeApplyView();
        });
        Action<DomEvent> up = _ =>
        {
            if (treeDrag == null) return;
            treeDragged = treeDrag.moved > TREE_DRAG_SLOP;
            treeDrag = null;
            /* the click lands after pointerup, so the flag is cleared right behind it */
            setTimeout(() => { treeDragged = false; }, 0);
        };
        window.addEventListener("pointerup", up);
        window.addEventListener("pointercancel", up);
    }
    public static (double x, double y) metaNodeXY(MetaNode n) => (n.x, n.y);
    public static string metaSpokeCol(string id)
    {
        var sp = META_SPOKES.Find(x => x.id == id);
        return sp != null ? sp.col : "#9fb0bd";
    }
    public static double metaNodeRadius(MetaNode n) => n.road == "attr" ? ATTR_BREAK_R : Js.Or(META_NODE_R.GetValueOrDefault(n.kind ?? ""), 14);
    public static string metaKindName(MetaNode n) =>
        (n.kind == "start" && Js.T(n.soon)) ? "NOT BUILT YET"
        : n.kind == "start" ? "STARTING POINT"
        : n.kind == "legend" ? "LEGENDARY"
        : n.kind == "keystone" ? "KEYSTONE"
        : n.kind == "notable" ? "NOTABLE"
        : n.kind == "travel" ? (n.road == "attr" ? "ATTRIBUTE" : "PATH") : "MINOR";
    /* the card that follows the cursor — name, what it gives, what it costs */
    public static string metaTipHtml(MetaNode n, MetaRec m)
    {
        string wk = metaWeapon();
        string lines = string.Join("", n.mods.Select(mod =>
        {
            var st = META_STATS.GetValueOrDefault(mod.k ?? "");
            bool dud = st != null && st.only != null && Array.IndexOf(st.only, wk) < 0;
            string col = st != null && Js.T(st.col) ? $" style=\"color:{st.col}\"" : "";
            return $"<li class=\"{(metaModGood(mod) ? "good" : "bad")}{(dud ? " dud" : "")}\"{col}>{esc(metaModText(mod))}" +
                   $"{(dud ? " <em>— nothing for a " + esc(wk) + "</em>" : "")}</li>";
        }));
        var cl = n.cl != null ? META_CLUSTER_BY_ID.GetValueOrDefault(n.cl) : null;
        string foot;
        if (n.kind == "start") foot = Js.T(n.soon) ? n.soon
            : n.id == metaStartId()
            ? "where this weapon begins"
            : "another weapon begins here — you cannot step through it, but what hangs "
              + "off it is yours to walk to the long way";
        else if (metaHas(n.id, m)) foot = treeRefund ? (metaCanRefund(n.id, m) ? "click to refund"
                                                       : "holds up a branch — refund those first")
                                                    : "taken";
        else if (metaLocked(n, m)) foot = $"locked — reach wave {S(n.at)} with this weapon";
        else if (!metaConnected(n.id, m, new List<string> { metaStartId() })) foot = "not connected — path to it first";
        else if (metaPointsLeft(m) < metaCost(n)) foot = $"{S(metaCost(n) - metaPointsLeft(m))} more point(s)";
        else foot = "click to take it";
        return $"<h4>{esc(n.name)}</h4>" +
          (Js.T(n.note) ? $"<p class=\"tnote\">{esc(n.note)}</p>" : "") +
          (lines.Length > 0 ? $"<ul>{lines}</ul>" : "") +
          $"<div class=\"tmeta\">{S(metaCost(n))} point{(metaCost(n) == 1 ? "" : "s")}" +
          (cl != null ? $" · {esc(cl.name)}" : "") + $" · {metaKindName(n)}</div>" +
          $"<div class=\"tmeta tfoot\">{foot}</div>";
    }
    /* THE SEARCH BOX. Made once and carried from render to render, so taking a node
       in the middle of a search does not throw away what was typed. ENTER walks to
       the next thing found (SHIFT+ENTER back), nearest your beginning first; ESC
       empties the box, and a second ESC leaves it. */
    public static string treeFindQ = "";
    public static List<string> treeFound = new();
    public static int treeFoundI = -1;
    public static Element treeFindEl = null;
    public static Element treeFindBox()
    {
        if (treeFindEl != null) return treeFindEl;
        var el = document.createElement("div");
        el.className = "treefind";
        el.innerHTML = "<input id=\"treeSearch\" type=\"text\" spellcheck=\"false\" autocomplete=\"off\" " +
          "placeholder=\"SEARCH — minion, fire res, attr, sword…\" aria-label=\"search the tree\"><span id=\"treeFindN\"></span>";
        var inp = el.querySelector("input");
        inp.addEventListener("input", _ => { treeFindQ = inp.value; treeFoundI = -1; treeFindApply(); });
        inp.addEventListener("keydown", ev =>
        {
            if (ev.key == "Enter") { ev.preventDefault(); treeFindNext(ev.shiftKey ? -1 : 1); }
            else if (ev.key == "Escape" && inp.value.Length > 0)
            {
                ev.preventDefault(); ev.stopPropagation();
                inp.value = ""; treeFindQ = ""; treeFoundI = -1; treeFindApply();
            }
        });
        return treeFindEl = el;
    }
    public static int treeFindSet(string q)
    {
        var inp = treeFindBox().querySelector("input");
        inp.value = q; treeFindQ = q; treeFoundI = -1; treeFindApply();
        return treeFound.Count;
    }
    public static void treeFindApply()
    {
        treeFound = metaSearch(treeFindQ);
        /* nearest your own beginning first: that is the order you would walk them in */
        var st = META_BY_ID.GetValueOrDefault(metaStartId());
        if (st != null)
            treeFound = treeFound.SortJs((a, b) =>
            {
                var A = META_BY_ID[a]; var B = META_BY_ID[b];
                return hypot(A.x - st.x, A.y - st.y) - hypot(B.x - st.x, B.y - st.y);
            });
        var svg = document.getElementById("treeSvg");
        if (svg != null)
        {
            foreach (var r in svg.querySelectorAll(".tfind")) r.remove();
            var at = new Dictionary<string, int>();
            for (int i = 0; i < treeFound.Count; i++) at[treeFound[i]] = i;
            foreach (var g in svg.querySelectorAll(".tnode"))
            {
                string id = g.dataset["node"];
                bool hit = at.TryGetValue(id ?? "", out int i);
                g.classList.toggle("found", hit);
                if (!hit) continue;
                var n = META_BY_ID[id];
                var c = document.createElementNS("http://www.w3.org/2000/svg", "circle");
                c.setAttribute("class", "tfind" + (i == treeFoundI ? " cur" : ""));
                c.setAttribute("cx", n.x); c.setAttribute("cy", n.y);
                c.dataset["r"] = S(metaNodeRadius(n));
                g.appendChild(c);
            }
            treeFindSize();
        }
        var lbl = document.getElementById("treeFindN");
        if (lbl != null) lbl.textContent = treeFindQ.Trim().Length == 0 ? ""
          : treeFound.Count == 0 ? "none"
          : (treeFoundI >= 0 ? S(treeFoundI + 1) + " / " : "") + S(treeFound.Count) + " found";
    }
    /* the ring is drawn in map units and the map zooms twenty-fold, so its size is
       worked out again whenever the view moves: never under a few pixels across */
    public static void treeFindSize()
    {
        var svg = document.getElementById("treeSvg");
        if (svg == null) return;
        var rings = svg.querySelectorAll(".tfind");
        if (rings.Count == 0) return;
        var r = svg.getBoundingClientRect();
        double px = (r.width != 0 && r.height != 0)
          ? Math.Max((TREE_VIEW.w / treeZoom) / r.width, (TREE_VIEW.h / treeZoom) / r.height) : 10;
        foreach (var c in rings) c.setAttribute("r", Math.Round(Math.Max(Js.ToNumber(c.dataset["r"]) + 14, 6 * px)));
    }
    public static string treeFindNext(int dir)
    {
        if (treeFound.Count == 0) return null;
        treeFoundI = (treeFoundI + (dir != 0 ? dir : 1) + treeFound.Count) % treeFound.Count;
        var n = META_BY_ID[treeFound[treeFoundI]];
        treeZoom = Math.Max(treeZoom, TREE_ZOOM_HOME);
        treeCx = n.x; treeCy = n.y;
        treeApplyView();
        treeFindApply();
        var info = document.getElementById("treeInfo");
        if (info != null) info.innerHTML = metaTipHtml(n, metaCur());
        return n.id;
    }
    public static void renderTree()
    {
        var body = document.getElementById("treeBody");
        if (body == null) return;
        if (META == null) metaLoad();
        var m = metaCur();
        double left = metaPointsLeft(m), earned = metaPointsEarned(m), spent = metaPointsSpent(m);
        string startId = metaStartId(), wk = metaWeapon();

        document.getElementById("treeSub").textContent =
          $"{difficulty.name} · {weapon.name} · {S(spent)} of {S(earned)} points spent · " +
          $"{S(left)} to place · best wave {S(m.best.wave)} · " +
          $"the tree costs {S(metaTotalCost())}, the ceiling is {S(META_POINT_MAX)}";

        var parts = new StringBuilder();
        parts.Append($"<svg id=\"treeSvg\" viewBox=\"{S(TREE_VIEW.x)} {S(TREE_VIEW.y)} {S(TREE_VIEW.w)} {S(TREE_VIEW.h)}\">");
        /* THE THREE COUNTRIES, painted first and very faintly. A player should be
           able to tell where they are on the map without reading a single node — the
           red half is the sword and the mace, the green is the axe, the blue is the
           staff, and the roads through each pay that attribute. */
        /* AND IT IS A TOGGLE NOW, OFF BY DEFAULT. Three coloured washes and three
           dashed borders across the whole map is a lot of ink for a thing you need to
           know once, and once you know it the map reads better without them. */
        if (treeSectors)
        {
            /* out to the furthest NODE and no further: painted to the corners of the
               viewBox, the countries bleed into the letterbox and the map looks like it
               is floating in the wrong place */
            double far = 0;
            foreach (var n in META_NODES)
                far = Math.Max(far, hypot(n.x - META_MAP.cx, n.y - META_MAP.cy));
            far = Math.Round(far + 150);
            foreach (var k in new[] { "str", "int", "dex" })
            {
                var sec = META_MAP.sector[k]; double f = sec[0], t = sec[1];
                double span = ((t - f) + 360) % 360;
                var p = new List<(double, double)> { (META_MAP.cx, META_MAP.cy) };
                for (int i = 0; i <= 24; i++)
                {
                    double a = (f + span * (i / 24.0)) * Math.PI / 180;
                    p.Add((Math.Round(META_MAP.cx + Math.Cos(a) * far), Math.Round(META_MAP.cy + Math.Sin(a) * far)));
                }
                parts.Append($"<path d=\"M{string.Join("L", p.Select(q => S(q.Item1) + "," + S(q.Item2)))}Z\" fill=\"{metaSpokeCol(k)}\" " +
                             "opacity=\"0.10\" pointer-events=\"none\"/>");
                /* and a line on the border, because a gradient between two dim washes is
                   not a border anybody can point at */
                (double, double) e(double f2) { double a = f2 * Math.PI / 180; return (Math.Round(META_MAP.cx + Math.Cos(a) * far), Math.Round(META_MAP.cy + Math.Sin(a) * far)); }
                var b0 = e(f);
                parts.Append($"<line x1=\"{S(META_MAP.cx)}\" y1=\"{S(META_MAP.cy)}\" x2=\"{S(b0.Item1)}\" y2=\"{S(b0.Item2)}\" " +
                             $"stroke=\"{metaSpokeCol(k)}\" stroke-width=\"5\" opacity=\"0.22\" " +
                             "stroke-dasharray=\"26 26\" pointer-events=\"none\"/>");
            }
        }
        /* a faint blot behind each cluster, so a GROUP of nodes reads as one thing.
           Its middle and its size are measured off where the nodes ended up and
           pulled in clear of everything else, so it never shades a stretch of road. */
        foreach (var c in META_CLUSTERS)
        {
            if (!(c.hr > 26)) continue;
            parts.Append($"<circle cx=\"{S(c.hx)}\" cy=\"{S(c.hy)}\" r=\"{S(c.hr)}\" fill=\"{metaSpokeCol(c.sp)}\" " +
                         "opacity=\"0.06\" pointer-events=\"none\"/>");
        }
        /* EVERY EDGE IS DRAWN, so you can see the whole walk before you pay for it */
        foreach (var n in META_NODES)
        {
            foreach (var rid in n.req)
            {
                var r = META_BY_ID.GetValueOrDefault(rid);
                if (r == null) continue;
                if (r.kind != "start" && string.CompareOrdinal(r.id, n.id) > 0) continue;       /* draw each edge once */
                /* A DOOR THAT IS NOT YOURS OPENS FOR NOBODY, so the step it leads to is
                   not drawn joined to it — the only way into somebody else's beginning is
                   from the far end, and the picture has to say so. Everything else about
                   those paths is drawn like any other part of the map. */
                if (r.kind == "start" && r.id != startId) continue;
                bool live = metaHas(n.id, m) && (metaHas(rid, m) || rid == startId);
                parts.Append($"<line x1=\"{S(r.x)}\" y1=\"{S(r.y)}\" x2=\"{S(n.x)}\" y2=\"{S(n.y)}\" " +
                  $"stroke=\"{(live ? metaSpokeCol(n.spoke) : "rgba(255,255,255,.20)")}\" " +
                  $"stroke-width=\"{(live ? 10 : 5)}\" stroke-linecap=\"round\" opacity=\"{(live ? "0.9" : "1")}\" " +
                  "pointer-events=\"none\"/>");
            }
        }
        foreach (var n in META_NODES)
        {
            string col = metaSpokeCol(n.spoke);
            bool isStart = n.kind == "start";
            bool mine = isStart && n.id == startId;
            bool has = mine || metaHas(n.id, m);
            bool lck = !isStart && metaLocked(n, m);
            bool can = !isStart && metaCanBuy(n, m);
            double rr = metaNodeRadius(n);
            /* somebody else's door is drawn, dimmed: you can see where the axe begins
               and what hangs off it, and you can walk to those nodes the long way — you
               simply cannot start there or step through it */
            bool shut = isStart && !mine;
            string fill = has ? (isStart ? "#ffd76e" : col) : "rgba(16,21,26,.95)";
            bool refundable = treeRefund && has && !isStart && metaCanRefund(n.id, m);
            /* an unowned node still wears the colour of the ground it stands on, faintly
               — that is how eight regions read as eight regions from across the map */
            string stroke = refundable ? "#ff8a6a" : has ? "#fff" : can ? col : col;
            double sop = has || can ? 1 : lck ? 0.3 : 0.45;
            /* WHAT IS ON IT, drawn rather than described. The road wears a plus in the
               colour of the country it runs through and nothing else — that is all a
               travel node does. Everything else wears the picture of the thing it
               gives, and it is the same drawing the brick or the card uses, so a
               flame in the tree and a flame on a card are the same flame.
               Rank is the frame: a keystone gets a broken ring round it, a legendary a
               whole second one. */
            bool big = n.road == "attr"; double pz = big ? 13 : 8;
            string glyph;
            if (n.kind == "travel")
                glyph = $"<path d=\"M{S(n.x - pz)} {S(n.y)} h{S(pz * 2)} M{S(n.x)} {S(n.y - pz)} v{S(pz * 2)}\" fill=\"none\" " +
                  $"stroke=\"{(has ? "#0d1218" : (big ? ATTR_COL.GetValueOrDefault(n.mods[0].k) : col))}\" stroke-width=\"{(big ? 7 : 5)}\" stroke-linecap=\"round\" " +
                  $"opacity=\"{(has ? "1" : "0.9")}\" pointer-events=\"none\"/>";
            else if (isStart)
                glyph = $"<text x=\"{S(n.x)}\" y=\"{S(n.y + 10)}\" text-anchor=\"middle\" font-size=\"30\" " +
                  $"fill=\"{(mine ? "#20160a" : "#8e9aa4")}\" font-family=\"Titan One\" " +
                  $"opacity=\"{(mine ? "1" : "0.5")}\" pointer-events=\"none\">✦</text>";
            else
            {
                double z = n.kind == "legend" ? 40 : n.kind == "keystone" ? 36 : n.kind == "notable" ? 32 : 26, k = z / 24;
                /* a GROUP with a transform and not a nested <svg>: the icons are
                   drawn on a 24-square like every other icon in the game, and a
                   nested viewport inside the one the tree is already scaling comes
                   out of Chrome the size of a county */
                glyph = $"<g transform=\"translate({Js.ToFixed(n.x - z / 2, 1)},{Js.ToFixed(n.y - z / 2, 1)}) " +
                  $"scale({Js.ToFixed(k, 4)})\" fill=\"none\" stroke=\"{(has ? "#0d1218" : col)}\" " +
                  "stroke-width=\"2.3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" " +
                  $"opacity=\"{(lck ? "0.7" : "1")}\" pointer-events=\"none\">" +
                  $"{ICONS.GetValueOrDefault(metaIconKey(n)) ?? ""}</g>";
            }
            string frame = n.kind == "legend"
              ? $"<circle cx=\"{S(n.x)}\" cy=\"{S(n.y)}\" r=\"{S(rr + 10)}\" fill=\"none\" stroke=\"{stroke}\" " +
                $"stroke-width=\"3\" stroke-opacity=\"{S(sop * 0.75)}\" pointer-events=\"none\"/>"
              : n.kind == "keystone"
                ? $"<circle cx=\"{S(n.x)}\" cy=\"{S(n.y)}\" r=\"{S(rr + 8)}\" fill=\"none\" stroke=\"{stroke}\" " +
                  $"stroke-width=\"3\" stroke-dasharray=\"7 9\" stroke-opacity=\"{S(sop * 0.75)}\" " +
                  "pointer-events=\"none\"/>" : "";
            /* A DOOR THAT IS NOT BUILT YET is hollow and dashed: a place on the map that
               is plainly a place, and plainly not open. Nothing else in the tree looks
               like this, which is the point -- it should not be mistaken for a node you
               have failed to unlock. */
            string soon = n.kind == "start" && Js.T(n.soon)
              ? $"<circle cx=\"{S(n.x)}\" cy=\"{S(n.y)}\" r=\"{S(rr + 13)}\" fill=\"none\" stroke=\"#9fb0bd\" " +
                "stroke-width=\"3\" stroke-dasharray=\"9 11\" stroke-opacity=\"0.5\" pointer-events=\"none\"/>"
              : "";
            parts.Append($"<g class=\"tnode{(lck ? " locked" : "")}\" data-node=\"{n.id}\">" +
              $"<circle cx=\"{S(n.x)}\" cy=\"{S(n.y)}\" r=\"{S(rr + 22)}\" fill=\"transparent\"/>" +
              $"<circle cx=\"{S(n.x)}\" cy=\"{S(n.y)}\" r=\"{S(rr)}\" " +
              $"fill=\"{(Js.T(n.soon) ? "rgba(20,26,32,0.85)" : fill)}\" " +
              $"stroke=\"{(Js.T(n.soon) ? "#6d7b86" : stroke)}\" " +
              $"stroke-width=\"{(refundable ? 9 : has ? 7 : 4)}\" stroke-opacity=\"{(Js.T(n.soon) ? "0.7" : S(sop))}\" " +
              $"opacity=\"{(Js.T(n.soon) ? "0.62" : lck ? "0.55" : shut ? "0.38" : "1")}\"/>" + soon + frame + glyph +
              "</g>");
        }
        /* cluster names, in whatever clear spot the build found for them */
        foreach (var c in META_CLUSTERS)
        {
            if (double.IsNaN(c.lx)) continue;
            parts.Append($"<text x=\"{S(c.lx)}\" y=\"{S(c.ly)}\" fill=\"{metaSpokeCol(c.sp)}\" " +
              "font-size=\"30\" font-family=\"Titan One\" opacity=\"0.92\" pointer-events=\"none\"" +
              " stroke=\"rgba(4,7,10,.92)\" stroke-width=\"7\" paint-order=\"stroke\"" +
              $" text-anchor=\"{c.lanch}\">{esc(c.name)}</text>");
        }
        parts.Append("</svg>");

        var stats = metaStatLines(m);
        double nextWave = metaNextPoint(m);
        string presets = string.Join("", (m.presets ?? new()).Select((pr, i) =>
        {
            double cost = metaPresetCost(pr);
            bool same = pr.nodes.Count == m.nodes.Count && pr.nodes.All(id => m.nodes.IndexOf(id) >= 0);
            return $@"<div class=""tprow"">
          <input class=""tpname"" data-i=""{i}"" maxlength=""{META_PRESET_NAME_MAX}""
            value=""{esc(pr.name)}"" spellcheck=""false"" aria-label=""preset {i + 1} name"">
          <button class=""tbtn gray tpsave"" data-i=""{i}"">SAVE</button>
          <button class=""tbtn tpload"" data-i=""{i}""{((pr.nodes.Count == 0 || state == "tree") ? " disabled" : "")}>LOAD</button>
          <i>{(pr.nodes.Count > 0 ? S(cost) + " point" + (cost == 1 ? "" : "s") + (same ? " · what you have now" : "") : "empty")}</i>
        </div>";
        }));
        string side = $@"
    {(META_REFUNDED > 0 ? $@"<div class=""tnews""><b>THE TREE CHANGED</b>
      Your {S(META_REFUNDED)} spent point{(META_REFUNDED == 1 ? " is" : "s are")} back — every stud,
      best wave and boss you had is untouched. Spend them again.</div>" : "")}
    <div class=""tpts""><b>{S(left)}</b> to place <i>{S(spent)} of {S(earned)} spent</i></div>
    <div class=""tinfo"" id=""treeInfo"">{(treePick != null && META_BY_ID.ContainsKey(treePick)
      ? metaTipHtml(META_BY_ID[treePick], m)
      : $@"<h4>{esc(weapon.name)} · {esc(difficulty.name)}</h4>
         <p>Hover a node and its card follows your cursor; click to take it. You
         can only take what TOUCHES something you already hold, so where you
         start decides what is cheap and what is a long walk.</p>
         <div class=""tmeta"">Every weapon has its own tree on every difficulty — twelve
         in all, one map, six doors set far apart. Two of them are for weapons
         that are not built yet.</div>")}</div>
    <div class=""tbar""><b>{S(metaWavePoints(m))}</b> points from waves
      <i>{(double.IsNaN(nextWave) ? "the ceiling of " + S(META_POINT_MAX) + " is reached"
                             : "your best is wave " + S(m.best.wave) + " — clear wave " +
                               S(nextWave) + " for the next one")}</i></div>
    <div class=""tbar""><b>{S(metaMilestonePoints(m))}</b> points from achievements
      <i>{S(metaLevelPoints(m))} from level {S(m.best.level)} ({S(META_POINTS_PER_LEVEL)} a level)
      · {(m.bosses?.Count ?? 0)} bosses · {(m.mastery?.Count ?? 0)}/{META_MASTERY.Count} mastered
      · all with THIS weapon</i></div>
    <div class=""tpresets"">
      <h4>PRESETS</h4>
      {presets}
      <div class=""tmeta"">{(state == "tree"
        ? "mid-run you can SAVE; LOADING is a respec and waits for the run to end"
        : "SAVE keeps what is placed now. LOAD takes it all back and places the preset")}</div>
      {(treePresetMsg.Length > 0 ? $"<div class=\"tpmsg\">{esc(treePresetMsg)}</div>" : "")}
    </div>
    <div class=""tbar""><b>{S(m.studsEarned)}</b> studs banked
      <i>a lifetime count. Studs buy bricks inside a run and nothing at all in here</i></div>
    <div class=""tstats"">
      <h4>WHAT THE TREE IS GIVING YOU</h4>
      {(stats.Count > 0 ? $"<ul>{string.Join("", stats.Select(l => $"<li class=\"{(l.good ? "good" : "bad")}\"" + (Js.T(l.col) ? $" style=\"color:{l.col}\"" : "") + $">{esc(l.text)}</li>"))}</ul>"
                     : "<div class=\"tmeta\">nothing yet — place a point</div>")}
    </div>";
        body.innerHTML = $"<div class=\"treewrap\">{parts}</div><div class=\"treeside\">{side}</div>";

        var sideEl = body.querySelector(".treeside");
        if (sideEl != null) sideEl.insertBefore(treeFindBox(), sideEl.firstChild);
        var svg = document.getElementById("treeSvg");
        if (svg != null)
        {
            if (treeCx == 0 && treeCy == 0) treeFit(); else treeApplyView();
            treeBindView(svg);
        }
        treeFindApply();
        var info = document.getElementById("treeInfo");
        var tip = document.getElementById("treeTip");
        /* KEPT INSIDE THE WINDOW. A card that runs off the right edge is a card you
           scroll the page to read, and this one is fixed -- so it flips to the other
           side of the cursor rather than growing a scrollbar. */
        void place(DomEvent ev)
        {
            if (tip == null) return;
            double w = Js.Or(tip.offsetWidth, 300), h = Js.Or(tip.offsetHeight, 140), pad = 14;
            double x = ev.clientX + 18, y = ev.clientY + 18;
            if (x + w + pad > window.innerWidth) x = ev.clientX - w - 18;
            if (y + h + pad > window.innerHeight) y = Math.Max(pad, ev.clientY - h - 18);
            tip.style.left = S(Math.Max(pad, x)) + "px";
            tip.style.top = S(Math.Max(pad, y)) + "px";
        }
        foreach (var g in body.querySelectorAll(".tnode"))
        {
            string id = g.dataset["node"]; var n = META_BY_ID[id];
            g.addEventListener("mouseenter", ev =>
            {
                treeHover = id;
                if (tip != null) { tip.innerHTML = metaTipHtml(n, m); tip.classList.add("on"); place(ev); }
                if (info != null) info.innerHTML = metaTipHtml(n, m);
            });
            g.addEventListener("mousemove", place);
            g.addEventListener("mouseleave", _ =>
            {
                treeHover = null;
                if (tip != null) tip.classList.remove("on");
                if (info != null && treePick != null && META_BY_ID.ContainsKey(treePick)) info.innerHTML = metaTipHtml(META_BY_ID[treePick], m);
            });
            g.addEventListener("click", ev =>
            {
                ev.stopPropagation();
                if (treeDragged) return;          /* that was a pan, not a purchase */
                treePick = id;
                if (treeRefund && state != "tree") { if (metaRefund(id)) SFX.click(); else SFX.clank(); }
                else if (metaBuyLive(id)) SFX.pick();
                else SFX.click();
                renderTree(); refreshTreeBtn();
            });
        }
        /* MID-RUN THE TREE ONLY GROWS. A point placed lands on the hero at once;
           taking one back would mean unpicking a node from a live hero, and the
           switches some nodes throw do not unpick cleanly -- so refunds, CLEAR ALL
           and the on/off switch wait for the run to end, and say so. */
        bool liveRun = state == "tree";
        foreach (var bid in new[] { "respecBtn", "clearBtn", "treeOffBtn" })
        {
            var b = document.getElementById(bid);
            if (b != null) b.style.display = liveRun ? "none" : "";
        }
        /* the three preset rows */
        foreach (var inp in body.querySelectorAll(".tpname"))
        {
            inp.addEventListener("change", _ => { metaPresetRename((int)Js.ToNumber(inp.dataset["i"]), inp.value); });
            inp.addEventListener("keydown", ev => { if (ev.key == "Enter") inp.blur(); });
        }
        foreach (var b2 in body.querySelectorAll(".tpsave"))
            b2.addEventListener("click", _ =>
            {
                int i = (int)Js.ToNumber(b2.dataset["i"]); var inp = body.querySelector(".tpname[data-i=\"" + i + "\"]");
                if (inp != null) metaPresetRename(i, inp.value);
                metaPresetSave(i); SFX.pick();
                treePresetMsg = "saved to " + metaCur().presets[i].name;
                renderTree();
            });
        foreach (var b2 in body.querySelectorAll(".tpload"))
            b2.addEventListener("click", _ =>
            {
                int i = (int)Js.ToNumber(b2.dataset["i"]); var r = metaPresetLoad(i);
                if (r == null) { SFX.click(); return; }
                SFX.pick(); treePick = null; treeRefund = false;
                treePresetMsg = "loaded " + metaCur().presets[i].name + (r.placed < r.of
                  ? " — placed " + S(r.placed) + " of " + S(r.of) + "; the rest need points or waves you do not have yet"
                  : "");
                renderTree(); refreshTreeBtn();
            });
        treePresetMsg = "";
        var rb = document.getElementById("respecBtn");
        if (rb != null)
        {
            rb.textContent = treeRefund ? "REFUNDING — CLICK A NODE" : "REFUND MODE";
            rb.classList.toggle("on", treeRefund);
        }
        var off = document.getElementById("treeOffBtn");
        if (off != null) off.textContent = META.on ? "APPLIED TO RUNS" : "NOT APPLIED";
        var note = document.getElementById("treeNote");
        if (note != null && liveRun) note.textContent = META.on
          ? "MID-RUN — a point you place lands on your hero now. Refunds wait for the run to end."
          : "the tree is switched off for this run — points placed now wait for the next one";
        else if (note != null) note.textContent = treeRefund
          ? "click any node you hold to take the point back — free, and one at a time"
          : (META.on ? "applied when a run starts — never to the test bench’s preview heroes"
                     : "switched off: runs are played on the raw numbers");
    }
    /* THE BUTTON HAS TO SAY WHICH NUMBER IT IS SHOWING. It printed points LEFT, so
       a hero who had spent all twelve read "SKILL TREE — 0 POINTS", which looks
       exactly like a hero who has never earned one. And it was refreshed when the
       DIFFICULTY changed but not when the WEAPON did, so it went on showing the
       last weapon's count — twelve trees, one label, and no way to tell. */
    public static void refreshTreeBtn()
    {
        var btn = document.getElementById("treeBtn");
        if (btn == null) return;
        if (META == null) metaLoad();
        var m = metaCur(); double left = metaPointsLeft(m), spent = metaPointsSpent(m);
        btn.innerHTML = $"SKILL TREE — <b id=\"treePts\">{S(left)}</b> TO PLACE" +
          (spent > 0 ? $" <i class=\"tspent\">{S(spent)} spent</i>" : "") +
          $"<i class=\"twho\">{esc(weapon.name)} · {esc(difficulty.name)}</i>";
    }
    public static void treeTipHide()
    {
        var t = document.getElementById("treeTip");
        if (t != null) t.classList.remove("on");
    }
    /* THREE DOORS NOW: the title, T in a run, and the pause menu. From a run it
       takes `state` to 'tree', which freezes the world exactly like the character
       sheet does (only 'play' updates anything), and closing puts you back where
       you came from -- a paused run stays paused. */
    public static string treeFrom = "title";
    public static void toggleTree()
    {
        treeTipHide();
        var el = document.getElementById("treeScreen");
        if (el == null) return;
        treeOn = !treeOn;
        if (treeOn)
        {
            treeFrom = (state == "play" || state == "pause") ? state : "title";
            if (treeFrom != "title")
            {
                document.getElementById("pauseScreen").classList.add("hidden");
                state = "tree"; cvEl.style.cursor = "default";
            }
            treePick = null; treeRefund = false; renderTree(); treeHome();
            el.classList.remove("hidden");
        }
        else
        {
            el.classList.add("hidden");
            if (state == "tree")
            {
                if (treeFrom == "pause")
                {
                    state = "pause";
                    document.getElementById("pauseScreen").classList.remove("hidden");
                }
                else
                {
                    state = "play"; cvEl.style.cursor = "none";
                }
            }
            treeFrom = "title";
        }
    }
    /* the game's <canvas> element, for its cursor (the drawing itself goes to the window) */
    public static Element cvEl => document.getElementById("game");
}
