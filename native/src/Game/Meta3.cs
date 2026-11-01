namespace Brickblade;

/* ========================= THE SKILL TREE: the build (JS 13958–15110) =========================
   buildTree, by hand. Saves, presets and run links store NODE IDS, so the map
   this lays out has to be the JS's to the node: the same hash, the same order
   of every walk and every stable sort, the same rounding. */
public sealed class MetaNode
{
    public string id, kind, spoke, cl, road, name, note, weap, soon, start, inward;
    public double x, y, at, cost = double.NaN, deg;
    public List<Mod> mods = new(); public List<string> req = new();
    public Action<Ent> apply;
    public string _tags, _hay;   /* the search's words, read once (metaNodeTags / metaNodeHay) */
}
public sealed class MetaHub { public int ring, j; public double deg, r, x, y; public string sec; public MetaNode node; }
public sealed class MetaSite
{
    public int ring, j; public double deg, x, y, gap = double.NaN, wall = double.NaN;
    public bool rim, late, sat, after, heart; public string sec; public MetaTpl site; public MetaCluster cluster; public List<string> ways;
}
public sealed class MetaCluster
{
    public string id, tpl, name, sp, shape, centre, lanch; public int ring; public bool rim, late;
    public double x, y, deg, r, at, hr, hx, hy, lx = double.NaN, ly; public int ways;
    public List<string> nodes = new(), heart;
}
public sealed class MetaStart { public string id, weap, name, soon; public double deg, x, y; }

public static partial class G
{
    static double TreeHash(double n)
    {
        int h = Js.Imul(Js.I32(n) ^ unchecked((int)0x9e3779b9), unchecked((int)0x85ebca6b));
        h ^= (int)((uint)h >> 13); h = Js.Imul(h, unchecked((int)0xc2b2ae35));
        return (uint)(h ^ (int)((uint)h >> 16));
    }
    static double TreeJit(double n, double amt) => ((TreeHash(n) % 2000) / 1000 - 1) * amt;
    sealed class Layout { public List<double[]> P = new(); public List<int[]> L = new(); public double[] hub = { 0, 0 }; public List<int> toHub = new(); }
    static readonly Dictionary<string, int[]> SHAPE_N = new()
    {
        ["vee"] = new[] { 4, 4 }, ["line"] = new[] { 4, 5 }, ["chain"] = new[] { 4, 5 }, ["hook"] = new[] { 4, 5 }, ["arc"] = new[] { 4, 6 },
        ["cresc"] = new[] { 4, 5 }, ["ring"] = new[] { 5, 6 }, ["wheel"] = new[] { 4, 6 }, ["diamond"] = new[] { 4, 4 }, ["star"] = new[] { 5, 6 },
    };
    static readonly string[] SHAPE_RING = { "ring", "arc", "wheel", "line", "diamond", "cresc", "chain", "hook", "star", "vee" };
    static readonly Dictionary<string, string> SHAPE_ALIAS = new() { ["fan"] = "star", ["pair"] = "vee" };
    /* WHERE THE NODES OF A FIGURE GO, in cluster feet: u straight out from the middle, v along the ring */
    static Layout TreeLayout(string shape, int n, double r, double seed)
    {
        var G0 = new Layout();
        void put(double u, double v) => G0.P.Add(new[] { u, v });
        void run(bool closed) { for (int i = 0; i < n - 1; i++) G0.L.Add(new[] { i, i + 1 }); if (closed && n > 2) G0.L.Add(new[] { n - 1, 0 }); }
        void bow(double half, Func<int, double> rad)
        {
            for (int i = 0; i < n; i++)
            {
                double a = -half + 2 * half * (n == 1 ? 0.5 : (double)i / (n - 1));
                put(Math.Cos(a) * rad(i), Math.Sin(a) * rad(i));
            }
        }
        if (shape == "vee")
        {
            /* a chevron: two arms meeting at a point, the prize in the notch */
            put(-r * 0.55, -r * 0.95); put(r * 0.45, -r * 0.45);
            put(r * 0.45, r * 0.45); put(-r * 0.55, r * 0.95);
            run(false);
            G0.hub = new[] { -r * 1.05, 0 }; G0.toHub.AddRange(new[] { 0, 3 });
        }
        else if (shape == "line" || shape == "chain")
        {
            bool radial = ((long)seed & 1) != 0; double amp = shape == "chain" ? r * 0.34 : 0;
            for (int i = 0; i < n; i++)
            {
                double t = (n == 1 ? 0 : ((double)i / (n - 1) - 0.5) * 2) * r, w = (i % 2 != 0 ? amp : -amp);
                if (radial) put(t, w); else put(w, t);
            }
            run(false);
            G0.hub = radial ? new[] { r * 1.62, 0 } : new[] { 0, r * 1.62 };
            G0.toHub.Add(n - 1);
        }
        else if (shape == "hook")
        {
            int k = (int)Math.Ceiling(n / 2.0);
            for (int i = 0; i < k; i++) put(-r * 0.6, -r + r * (k < 2 ? 0 : (double)i / (k - 1)));
            for (int i = k; i < n; i++) put(-r * 0.6 + r * 1.25 * ((double)(i - k + 1) / (n - k)), r * 0.06);
            run(false);
            G0.hub = new[] { r * 0.95, -r * 0.95 }; G0.toHub.AddRange(new[] { 0, n - 1 });
        }
        else if (shape == "arc")
        {
            bow(Math.PI * 0.42, _ => r); run(false);
            G0.hub = new[] { -r * 0.42, 0 }; G0.toHub.AddRange(new[] { 0, n - 1 });
        }
        else if (shape == "cresc")
        {
            bow(Math.PI * 0.64, i => (i == 0 || i == n - 1) ? r * 0.72 : r); run(false);
            G0.hub = new[] { -r * 0.46, 0 }; G0.toHub.AddRange(new[] { 0, n >> 1, n - 1 });
        }
        else if (shape == "diamond")
        {
            put(r, 0); put(0, r); put(-r, 0); put(0, -r); run(true);
            G0.toHub.AddRange(new[] { 0, 1, 2, 3 });
        }
        else if (shape == "star")
        {
            for (int i = 0; i < n; i++)
            {
                double a = ((double)i / n) * TAU + (seed % 7) / 7;
                double rr = r * (0.84 + 0.17 * (i % 3)); put(Math.Cos(a) * rr, Math.Sin(a) * rr);
            }
            for (int i = 0; i < n; i++) G0.toHub.Add(i);
        }
        else
        {                                        /* ring and wheel */
            for (int i = 0; i < n; i++) { double a = ((double)i / n) * TAU + (seed % 9) / 9; put(Math.Cos(a) * r, Math.Sin(a) * r); }
            run(true);
            if (shape == "wheel") { for (int i = 0; i < n; i++) G0.toHub.Add(i); }
            else for (int i = 0; i < n; i += 2) G0.toHub.Add(i);
        }
        return G0;
    }
    static bool SegCross(double ax, double ay, double bx, double by, MetaNode P1, MetaNode P2)
    {
        double d = (P2.y - P1.y) * (bx - ax) - (P2.x - P1.x) * (by - ay);
        if (Math.Abs(d) < 1e-9) return false;
        double ua = ((P2.x - P1.x) * (ay - P1.y) - (P2.y - P1.y) * (ax - P1.x)) / d;
        double ub = ((bx - ax) * (ay - P1.y) - (by - ay) * (ax - P1.x)) / d;
        return ua > 0.001 && ua < 0.999 && ub > 0.001 && ub < 0.999;
    }
    static readonly System.Text.RegularExpressions.Regex DMG_STAT =
        new(@"^(incMelee|incSpell|incAll|moreAll|flatPhys|flatElem|incElem|incFire|incFrost|incShock|incChaos|incAtk|incCast|incCrit|critMult|incCritMult|dmg_|inc_|slamDmg|arrowFlat|stormDmg|orbitDmg|chainDmg|vortexDmg|bombDmg|baneDmg)");

    public static void buildTree()
    {
        var edges = new List<(string a, string b)>();
        void add(string a, string b) { if (a != null && b != null && a != b) edges.Add((a, b)); }
        MetaNode push(MetaNode n) { META_NODES.Add(n); META_BY_ID[n.id] = n; return n; }
        double CX = META_MAP.cx, CY = META_MAP.cy;
        Func<double, double> hash = TreeHash;
        List<Mod> one(string stat, double v) => new List<Mod> { new Mod(stat, v) };

        /* ---- 1. THE HUBS, AND THEY ARE ALL LANE ---- */
        var hubs = new List<MetaHub>();
        for (int ri = 0; ri < META_RINGS.Count; ri++)
        {
            var R = META_RINGS[ri];
            for (int j = 0; j < R.n; j++)
            {
                double step = 360 / R.n;
                double deg = R.phase + j * step + TreeJit(ri * 997 + j * 31 + 7, HUB_SWING * step);
                double rad = R.r * (1 + TreeJit(ri * 613 + j * 57 + 3, HUB_JIT));
                double a = deg * Math.PI / 180;
                double x = Math.Round(CX + Math.Cos(a) * rad), y = Math.Round(CY + Math.Sin(a) * rad);
                string sec = metaSectorOf(deg); var use = META_TRAVEL.GetValueOrDefault(sec, META_TRAVEL["core"]);
                hubs.Add(new MetaHub
                {
                    ring = ri, j = j, deg = ((deg % 360) + 360) % 360, r = rad, x = x, y = y, sec = sec,
                    node = push(new MetaNode { id = "h" + ri + "_" + j, kind = "travel", spoke = sec, cl = null, road = "hub",
                        name = use.name, x = x, y = y, at = 0, cost = 1, mods = one(use.stat, use.v) })
                });
            }
        }
        List<MetaHub> ringHubs(int ri) => hubs.Where(h => h.ring == ri).ToList();

        /* ---- 2. THE LANES ---- */
        var laneAt = new List<MetaNode>();
        var laid = new HashSet<string>();
        void lane(MetaNode an, MetaNode bn, string key)
        {
            if (an == null || bn == null || an == bn || laid.Contains(key)) return;
            laid.Add(key);
            double dx = bn.x - an.x, dy = bn.y - an.y, len = hypot(dx, dy);
            double vary = 1 + TreeJit(an.x * 7 + an.y * 13 + bn.x * 3, LANE_VARY);
            /* FOUR BETWEEN TWO JUNCTIONS AND NO MORE */
            int steps = (int)Math.Min(4, Math.Max(1, Math.Round(len / (LANE_STEP * vary))));
            var prev = an;
            for (int i = 1; i < steps; i++)
            {
                double t = (double)i / steps, ang = Math.Atan2(dy, dx) + Math.PI / 2;
                double px = an.x + dx * t + Math.Cos(ang) * TreeJit(an.x + i * 71, 26);
                double py = an.y + dy * t + Math.Sin(ang) * TreeJit(an.y + i * 97, 26);
                string sec = metaSectorAt(px, py); var use = META_TRAVEL.GetValueOrDefault(sec, META_TRAVEL["core"]);
                var n = push(new MetaNode { id = "ln" + laneAt.Count, kind = "travel", spoke = sec, cl = null, road = key,
                    name = use.name, x = Math.Round(px), y = Math.Round(py), at = 0, cost = 1, mods = one(use.stat, use.v) });
                laneAt.Add(n);
                add(prev.id, n.id); prev = n;
            }
            add(prev.id, bn.id);
        }
        for (int ri = 0; ri < META_RINGS.Count; ri++)
        {
            var hs = ringHubs(ri);
            for (int j = 0; j < hs.Count; j++) lane(hs[j].node, hs[(j + 1) % hs.Count].node, "r" + ri + "_" + j);
        }
        MetaHub nearestOn(int ri, double deg)
        {
            MetaHub best = null; double bd = double.PositiveInfinity;
            foreach (var h in ringHubs(ri))
            {
                double d = Math.Abs(((h.deg - deg + 540) % 360) - 180);
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }
        for (int ri = 0; ri < META_RINGS.Count - 1; ri++)
        {
            foreach (var h in ringHubs(ri))
            {
                var o = nearestOn(ri + 1, h.deg);
                if (o != null) lane(h.node, o.node, "x" + ri + "_" + h.j + "_" + o.j);
            }
            foreach (var h in ringHubs(ri + 1))
            {
                var o = nearestOn(ri, h.deg);
                if (o != null) lane(o.node, h.node, "x" + ri + "_" + o.j + "_" + h.j);
            }
        }

        /* ---- 3. THE SITES: a cluster goes in the EMPTY MIDDLE of a quad ---- */
        var sites = new List<MetaSite>();
        for (int ri = 0; ri < META_RINGS.Count; ri++)
        {
            var R = META_RINGS[ri]; var hs = ringHubs(ri); var outR = ri + 1 < META_RINGS.Count ? META_RINGS[ri + 1] : null;
            for (int j = 0; j < hs.Count; j++)
            {
                double a = hs[j].deg, b = hs[(j + 1) % hs.Count].deg;
                double mid = a + (((b - a) + 360) % 360) / 2;
                double rad = (outR != null ? (R.r + outR.r) / 2 : R.r + 470) * (1 + TreeJit(ri * 331 + j * 47, 0.05));
                double ang = (mid + TreeJit(ri * 77 + j * 911, 4)) * Math.PI / 180;
                sites.Add(new MetaSite
                {
                    ring = ri, j = j, deg = ((mid % 360) + 360) % 360, rim = outR == null,
                    x = Math.Round(CX + Math.Cos(ang) * rad), y = Math.Round(CY + Math.Sin(ang) * rad),
                    sec = metaSectorOf(mid), site = null
                });
            }
        }

        /* ---- 4. WHAT SITS ON THEM: one-of-a-kind first, deepest and dearest before the rest ---- */
        bool claim(MetaTpl tpl)
        {
            MetaSite best = null; double bh = double.PositiveInfinity;
            foreach (var s in sites)
            {
                if (s.site != null || s.sec != tpl.home) continue;
                if (s.ring < tpl.d[0] || s.ring > tpl.d[1]) continue;
                double k = hash(s.ring * 7919 + s.j * 104729 + tpl.id.Length * 31);
                if (k < bh) { bh = k; best = s; }
            }
            if (best != null) best.site = tpl;
            return best != null;
        }
        var unplaced = new List<string>();
        foreach (var t in META_LEGENDS) if (!claim(t)) unplaced.Add(t.id);
        foreach (var t in META_KEYSTONES) if (!claim(t)) unplaced.Add(t.id);
        foreach (var t in META_NOTABLES) if (!claim(t)) unplaced.Add(t.id);
        /* ...and everything marked MUST gets at least one place of its own */
        foreach (var t in META_GROUPS) if (t.must && !claim(t)) unplaced.Add(t.id);
        int gi = 0;
        foreach (var s in sites)
        {
            if (s.site != null) continue;
            if (hash(s.ring * 31 + s.j * 17 + 5) % 100 >= 62) continue;   /* the quad stays empty */
            var fits = META_GROUPS.Where(g => !g.lateOnly && g.home == s.sec && s.ring >= g.d[0] && s.ring <= g.d[1]).ToList();
            if (fits.Count == 0) continue;
            /* NOT THE ONE NEXT DOOR */
            var near = fits.Where(g => !sites.Any(o => o.site != null && o.site.id == g.id && hypot(o.x - s.x, o.y - s.y) < 1500)).ToList();
            if (near.Count > 0) fits = near;
            s.site = fits[(int)(hash(s.ring * 911 + s.j * 131 + (gi++)) % fits.Count)];
        }
        /* WHAT THE NEW GROUND MAY HOLD: filler, not raw damage */
        bool lateOk(MetaTpl g) => !g.must && g.minors.Count(m => DMG_STAT.IsMatch(m.stat)) * 2 < g.minors.Count;
        /* THE EMPTY QUADS, FILLED -- LATE, so nothing that was already there moves */
        foreach (var s in sites)
        {
            if (s.site != null) continue;
            var fits = META_GROUPS.Where(g => g.home == s.sec && lateOk(g) && s.ring >= g.d[0] && s.ring <= g.d[1]).ToList();
            if (fits.Count == 0) fits = META_GROUPS.Where(g => g.home == s.sec && lateOk(g)).ToList();
            if (fits.Count == 0) continue;
            var near = fits.Where(g => !sites.Any(o => o.site != null && o.site.id == g.id && hypot(o.x - s.x, o.y - s.y) < 1500)).ToList();
            if (near.Count > 0) fits = near;
            s.site = fits[(int)(hash(s.ring * 577 + s.j * 9173 + 3) % fits.Count)];
            s.late = true;
        }
        /* THE LATE ARRIVALS, on a site of their own */
        foreach (var t in META_LATE)
        {
            if (t.after) continue;
            double a = t.spot.deg * Math.PI / 180;
            sites.Add(new MetaSite
            {
                ring = (int)t.spot.ring, j = 1000 + sites.Count, deg = t.spot.deg, rim = false,
                x = Math.Round(CX + Math.Cos(a) * (t.spot.rad + CORE_ROOM)), y = Math.Round(CY + Math.Sin(a) * (t.spot.rad + CORE_ROOM)),
                sec = t.home, site = t
            });
        }

        /* ---- 5. THE CLUSTERS THEMSELVES ---- */
        var taken = sites.Where(o => o.site != null && !o.late).ToList();
        /* HOW FAR THE ROAD IS */
        (double d, double x, double y) wallAt(double x, double y)
        {
            double w = double.PositiveInfinity, wx = x, wy = y;
            foreach (var e in edges)
            {
                var A = META_BY_ID.GetValueOrDefault(e.a); var B = META_BY_ID.GetValueOrDefault(e.b);
                if (A == null || B == null) continue;
                double ex = B.x - A.x, ey = B.y - A.y, L2 = ex * ex + ey * ey;
                double t = L2 < 1 ? 0 : ((x - A.x) * ex + (y - A.y) * ey) / L2;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                double px = A.x + ex * t, py = A.y + ey * t, d = hypot(x - px, y - py);
                if (d < w) { w = d; wx = px; wy = py; }
            }
            return (double.IsFinite(w) ? w : 900, wx, wy);
        }
        /* AND IF THE MIDDLE OF THE QUAD IS NOT THE ROOMIEST PART OF IT, MOVE */
        void walkOff(MetaSite s)
        {
            double sx = s.x, sy = s.y;
            for (int k = 0; k < 11; k++)
            {
                var w = wallAt(s.x, s.y);
                if (w.d > 300) break;
                double dx = s.x - w.x, dy = s.y - w.y, d = Js.Or(hypot(dx, dy), 1);
                double nx = Math.Round(s.x + dx / d * 34), ny = Math.Round(s.y + dy / d * 34);
                if (hypot(nx - sx, ny - sy) > 270) break;
                if (wallAt(nx, ny).d <= w.d) break;
                s.x = nx; s.y = ny;
            }
        }
        foreach (var s in taken) walkOff(s);
        foreach (var s in taken)
        {
            double g = double.PositiveInfinity;
            foreach (var o in taken) if (o != s) g = Math.Min(g, hypot(o.x - s.x, o.y - s.y));
            s.gap = double.IsFinite(g) ? g : 900;
            s.wall = wallAt(s.x, s.y).d;
        }
        /* the filled quads find their room the same way, against everything */
        var fillers = sites.Where(o => o.late).ToList();
        foreach (var s in fillers) walkOff(s);
        /* AND THE ROOM LEFT INSIDE A QUAD */
        double clearOf(double x, double y, List<MetaSite> list)
        {
            double d = wallAt(x, y).d - SAT_WALL;
            foreach (var o in list) d = Math.Min(d, hypot(o.x - x, o.y - y) - SAT_GAP);
            return d;
        }
        var baseSites = sites.Where(o => o.j < 1000).ToList();
        foreach (var q in baseSites)
        {
            if (q.ring < 1 || q.rim) continue;
            var R = META_RINGS[q.ring]; var outR = META_RINGS[q.ring + 1];
            double step = 360 / R.n, midR = (R.r + outR.r) / 2, depth = outR.r - R.r;
            var cands = new List<(double dg, double rr)>();
            foreach (var fa in new[] { -0.34, -0.17, 0, 0.17, 0.34 }) foreach (var fr in new[] { -0.30, -0.15, 0, 0.15, 0.30 })
                    cands.Add((q.deg + step * fa, midR + depth * fr));
            var occupied = sites.Where(o => o.site != null).ToList();
            (double x, double y, double deg)? best = null; double bc = double.NegativeInfinity;
            foreach (var (dg, rr) in cands)
            {
                double a = dg * Math.PI / 180, x = Math.Round(CX + Math.Cos(a) * rr), y = Math.Round(CY + Math.Sin(a) * rr);
                double cl = clearOf(x, y, occupied);
                if (cl > bc) { bc = cl; best = (x, y, ((dg % 360) + 360) % 360); }
            }
            if (best == null || bc < 0) continue;
            var bb = best.Value;
            string sec = metaSectorAt(bb.x, bb.y);
            var fits = META_GROUPS.Where(g => g.home == sec && lateOk(g) && q.ring >= g.d[0] && q.ring <= g.d[1]).ToList();
            if (fits.Count == 0) continue;
            var near = fits.Where(g => !sites.Any(o => o.site != null && o.site.id == g.id && hypot(o.x - bb.x, o.y - bb.y) < 1500)).ToList();
            if (near.Count > 0) fits = near;
            sites.Add(new MetaSite
            {
                ring = q.ring, j = 2000 + sites.Count, deg = bb.deg, rim = false, x = bb.x, y = bb.y, sec = sec,
                site = fits[(int)(hash(q.ring * 887 + q.j * 6151 + 17) % fits.Count)], late = true, sat = true
            });
        }
        fillers.AddRange(sites.Where(o => o.sat));
        foreach (var s in fillers)
        {
            double g = double.PositiveInfinity;
            foreach (var o in taken.Concat(fillers)) if (o != s) g = Math.Min(g, hypot(o.x - s.x, o.y - s.y));
            s.gap = double.IsFinite(g) ? g : 900;
            s.wall = wallAt(s.x, s.y).d;
        }
        /* LATER STILL: a template marked `after` arrives once every other site is numbered and measured */
        {
            int i = 0;
            foreach (var t in META_LATE.Where(t0 => t0.after).ToList())
            {
                double a = t.spot.deg * Math.PI / 180;
                var s = new MetaSite
                {
                    ring = (int)t.spot.ring, j = 3000 + i, deg = t.spot.deg, rim = false,
                    x = Math.Round(CX + Math.Cos(a) * (t.spot.rad + CORE_ROOM)), y = Math.Round(CY + Math.Sin(a) * (t.spot.rad + CORE_ROOM)),
                    sec = t.home, site = t, after = true
                };
                double g = double.PositiveInfinity;
                foreach (var o in taken.Concat(fillers)) g = Math.Min(g, hypot(o.x - s.x, o.y - s.y));
                s.gap = double.IsFinite(g) ? g : 900;
                s.wall = wallAt(s.x, s.y).d;
                sites.Add(s); fillers.Add(s);
                i++;
            }
        }
        int cn = 0;
        var drawn = new List<(double x, double y, string shape)>();
        void drawSite(MetaSite s)
        {
            var t = s.site; bool uniq = t.notable != null || t.keystone != null || t.legend != null;
            var mid = t.legend ?? t.keystone ?? t.notable;
            double baseR = !double.IsNaN(t.r) && t.r != 0 ? t.r : (s.ring <= 1 ? 84 : s.ring == 2 ? 94 : 106);
            double seed = hash(s.ring * 31 + s.j * 7 + 11);
            /* HOW WIDE A FIGURE HAS TO BE, and HOW FAR IT REACHES, both asked of the figure itself */
            double roomFor(string sh, int k)
            {
                var g = TreeLayout(sh, k, 100, seed); var pts = g.P.ToList();
                if (mid != null) pts.Add(g.hub);
                double mn = double.PositiveInfinity;
                for (int a = 0; a < pts.Count; a++) for (int b = a + 1; b < pts.Count; b++)
                        mn = Math.Min(mn, hypot(pts[a][0] - pts[b][0], pts[a][1] - pts[b][1]));
                return double.IsFinite(mn) && mn > 1 ? 100 * 82 / mn : 60;
            }
            double roomAt(string sh, int k)
            {
                var g = TreeLayout(sh, k, 100, seed);
                double f = 0;
                foreach (var p in g.P) f = Math.Max(f, hypot(p[0], p[1]));
                if (mid != null) f = Math.Max(f, hypot(g.hub[0], g.hub[1]));
                f = Math.Max(0.2, f / 100);
                return Math.Min(152, Math.Min(Js.Or(s.gap, 900) * 0.46 / f, (Js.Or(s.wall, 900) - 62) / f));
            }
            /* WHICH FIGURE, AND HOW BIG */
            var nearby = drawn.Where(o => hypot(o.x - s.x, o.y - s.y) < 1500).ToList();
            double k0 = hash(s.ring * 577 + s.j * 8191 + t.id.Length * 13);
            string alias = SHAPE_ALIAS.GetValueOrDefault(t.shape ?? "") ?? t.shape;
            string first = alias != null && SHAPE_N.ContainsKey(alias) ? alias : "ring";
            var order = new List<string> { first };
            for (int k = 0; k < SHAPE_RING.Length; k++)
            {
                string sh = SHAPE_RING[(int)((k0 + k) % SHAPE_RING.Length)];
                if (!order.Contains(sh)) order.Add(sh);
            }
            int sizeOf(string sh)
            {
                var sp = SHAPE_N[sh];
                int k = sp[0] + (int)(hash(s.ring * 101 + s.j * 97 + sh.Length * 31) % (sp[1] - sp[0] + 1));
                while (k > sp[0] && roomFor(sh, k) > roomAt(sh, k)) k--;
                return k;
            }
            string shape = first; int n0 = 0; bool taboo = true;
            foreach (var relax in new[] { 0, 1 })
            {       /* fussy sweep, then take what fits */
                foreach (var sh in order)
                {
                    /* a star with nothing in the middle is leaves joined to nobody */
                    if (sh == "star" && mid == null) continue;
                    if (relax == 0 && nearby.Any(o => o.shape == sh)) continue;
                    int k = sizeOf(sh);
                    if (roomFor(sh, k) > roomAt(sh, k)) continue;
                    shape = sh; n0 = k; taboo = false; break;
                }
                if (!taboo) break;
            }
            if (taboo)
            {
                /* NOWHERE NEAR ENOUGH ROOM: whichever figure overflows its site by the least */
                double bestOver = double.PositiveInfinity;
                foreach (var sh in order)
                {
                    if (sh == "star" && mid == null) continue;
                    int k = sizeOf(sh); double over = roomFor(sh, k) / Math.Max(1, roomAt(sh, k));
                    if (over < bestOver) { bestOver = over; shape = sh; n0 = k; }
                }
                if (n0 == 0) { shape = (first == "star" && mid == null) ? "ring" : first; n0 = SHAPE_N[shape][0]; }
            }
            double want = roomFor(shape, n0), roomHas = roomAt(shape, n0);
            var c = new MetaCluster
            {
                id = t.id + (uniq ? "" : "@" + (cn++)), tpl = t.id, name = t.name, sp = t.sp,
                x = s.x, y = s.y, deg = s.deg, ring = s.ring, rim = s.rim, shape = shape,
                r = Math.Round(Math.Max(want, Math.Min(152, Math.Min(baseR * (0.56 + 0.135 * n0), roomHas)))),
                at = RING_AT[s.ring], centre = null
            };
            META_CLUSTERS.Add(c); META_CLUSTER_BY_ID[c.id] = c;
            s.cluster = c;
            drawn.Add((s.x, s.y, shape));
            double outA = Math.Atan2(c.y - CY, c.x - CX), tan = outA + Math.PI / 2;
            var G0 = TreeLayout(shape, n0, c.r, seed);
            /* THE FIGURE DECIDES, not the number that asked for it */
            int cnt = G0.P.Count;
            double[] Wp(double[] p) => new[] { Math.Round(c.x + Math.Cos(outA) * p[0] + Math.Cos(tan) * p[1]),
                                               Math.Round(c.y + Math.Sin(outA) * p[0] + Math.Sin(tan) * p[1]) };
            for (int i = 0; i < cnt; i++)
            {
                /* the stats go round the figure, repeating if the figure is the longer of the two */
                var row = t.minors[i % t.minors.Count]; var q = Wp(G0.P[i]);
                c.nodes.Add(push(new MetaNode { id = c.id + "_" + i, kind = "minor", spoke = c.sp, cl = c.id, name = row.name,
                    x = q[0], y = q[1], at = c.at, cost = 1, mods = one(row.stat, row.v) }).id);
            }
            foreach (var e in G0.L) if (e[0] < c.nodes.Count && e[1] < c.nodes.Count) add(c.nodes[e[0]], c.nodes[e[1]]);
            if (mid != null)
            {
                string kind = t.legend != null ? "legend" : t.keystone != null ? "keystone" : "notable";
                var q = Wp(G0.hub);
                var n = push(new MetaNode
                {
                    id = c.id + "_C", kind = kind, spoke = c.sp, cl = c.id, name = mid.name, note = mid.note,
                    x = q[0], y = q[1], at = c.at,
                    /* A KEYSTONE COSTS FIVE */
                    cost = !double.IsNaN(mid.cost) && mid.cost != 0 ? mid.cost : (kind == "legend" ? 5 : kind == "keystone" ? 5 : 2),
                    mods = mid.mods
                });
                c.centre = n.id;
                foreach (var i in G0.toHub) if (i < c.nodes.Count) add(c.nodes[i], n.id);
            }
        }
        foreach (var s in sites.ToList()) if (s.site != null && !s.late && !s.after) drawSite(s);
        /* the filled quads last, so their numbers follow everything already known */
        foreach (var s in sites.ToList()) if (s.late) { drawSite(s); s.cluster.late = true; }
        /* ...and after them the `after` templates */
        foreach (var s in sites.ToList()) if (s.after) drawSite(s);
        META_SITES = sites;

        var byHome = new Dictionary<string, List<MetaDoor>>(); var homeOrder = new List<string>();
        foreach (var d in META_DOORS) { if (!byHome.ContainsKey(d.home)) { byHome[d.home] = new(); homeOrder.Add(d.home); } byHome[d.home].Add(d); }
        /* where the i-th of n doors in a country stands, in degrees */
        double doorDeg(string sec, int i, int n)
        {
            double f = META_MAP.sector[sec][0], t = META_MAP.sector[sec][1], span = ((t - f) + 360) % 360;
            return f + span * (0.5 + (((i + 0.5) / n) - 0.5) * DOOR_SPREAD);
        }
        /* ---- 6b. THE HEART: a ring of plain stops round the middle ---- */
        var heart = new List<MetaNode>();
        for (int j = 0; j < HEART_N; j++)
        {
            double deg = j * 360 / HEART_N, a = deg * Math.PI / 180;
            double x = Math.Round(CX + Math.Cos(a) * HEART_R), y = Math.Round(CY + Math.Sin(a) * HEART_R);
            string sec = metaSectorAt(x, y); var use = META_TRAVEL.GetValueOrDefault(sec, META_TRAVEL["core"]);
            heart.Add(push(new MetaNode { id = "hc" + j, kind = "travel", spoke = sec, cl = null, road = "heart", deg = deg,
                name = use.name, x = x, y = y, at = 0, cost = 1, mods = one(use.stat, use.v) }));
        }
        for (int j = 0; j < HEART_N; j++) add(heart[j].id, heart[(j + 1) % (int)HEART_N].id);
        /* a road of exactly k stops between two nodes, each paying its own ground */
        void road(MetaNode an, MetaNode bn, double k, string key)
        {
            double dx = bn.x - an.x, dy = bn.y - an.y;
            var prev = an;
            for (int i = 1; i <= k; i++)
            {
                /* STRAIGHT */
                double t = i / (k + 1);
                double px = Math.Round(an.x + dx * t), py = Math.Round(an.y + dy * t);
                string sec = metaSectorAt(px, py); var use = META_TRAVEL.GetValueOrDefault(sec, META_TRAVEL["core"]);
                var n = push(new MetaNode { id = key + "_" + i, kind = "travel", spoke = sec, cl = null, road = key,
                    name = use.name, x = px, y = py, at = 0, cost = 1, mods = one(use.stat, use.v) });
                add(prev.id, n.id); prev = n;
            }
            add(prev.id, bn.id);
        }
        double gapDeg(double a, double b) => Math.Abs(((a - b + 540) % 360) - 180);
        var heartUsed = new HashSet<string>();
        var doors = new List<(string weap, double deg)>();
        foreach (var sec in homeOrder)
        {
            var lst = byHome[sec];
            for (int i = 0; i < lst.Count; i++)
                if (lst[i].weap != null) doors.Add((lst[i].weap, ((doorDeg(sec, i, lst.Count) % 360) + 360) % 360));
        }
        doors = doors.ToList().SortJs((a, b) => a.deg - b.deg);
        var ways = new List<(double mid, (string weap, double deg) A, (string weap, double deg) B, string key, double span, double hwDeg)>();
        /* THE HIGHWAYS: between each pair of neighbouring doors, from the Heart out to the inner ring */
        for (int i = 0; i < doors.Count; i++)
        {
            var A = doors[i]; var B = doors[(i + 1) % doors.Count];
            double span = ((B.deg - A.deg) + 360) % 360, mid = (A.deg + span / 2) % 360;
            var hs2 = heart.ToList().SortJs((p, q) => gapDeg(p.deg, mid) - gapDeg(q.deg, mid)).Take(2).ToList();
            var os2 = ringHubs(0).ToList().SortJs((p, q) => gapDeg(p.deg, mid) - gapDeg(q.deg, mid)).Take(2).ToList();
            bool clean(MetaNode a, MetaNode b)
            {
                foreach (var e in edges)
                {
                    var P1 = META_BY_ID.GetValueOrDefault(e.a); var P2 = META_BY_ID.GetValueOrDefault(e.b);
                    if (P1 == null || P2 == null || P1 == a || P2 == a || P1 == b || P2 == b) continue;
                    if (SegCross(a.x, a.y, b.x, b.y, P1, P2)) return false;
                }
                return true;
            }
            var h = hs2[0]; var o = os2[0]; double bestK = double.PositiveInfinity;
            foreach (var hh in hs2) foreach (var oo in os2)
                {
                    double k = gapDeg(hh.deg, mid) + gapDeg(oo.deg, mid) + (clean(hh, oo.node) ? 0 : 1000);
                    if (k < bestK) { bestK = k; h = hh; o = oo; }
                }
            double len = hypot(o.x - h.x, o.y - h.y);
            road(h, o.node, Math.Max(2, Math.Min(5, Math.Round(len / HEART_STRIDE) - 1)), "hw" + i);
            /* where the highway actually crosses the groups' circle */
            double hwDeg = mid;
            for (double t = 0; t <= 1; t += 0.01)
            {
                double px = h.x + (o.x - h.x) * t, py = h.y + (o.y - h.y) * t;
                if (hypot(px - CX, py - CY) >= HEART_SITE_R) { hwDeg = ((Math.Atan2(py - CY, px - CX) * 180 / Math.PI) + 360) % 360; break; }
            }
            ways.Add((mid, A, B, "hw" + i, span, hwDeg));
        }
        /* ---- 6. THE DOORS: one per weapon, in its own country ---- */
        foreach (var sec in homeOrder)
        {
            var list = byHome[sec];
            for (int i = 0; i < list.Count; i++)
            {
                var D = list[i]; string wk = D.weap;
                double deg = doorDeg(sec, i, list.Count);
                double a = deg * Math.PI / 180, R = DOOR_R;
                var st = new MetaStart
                {
                    id = "st_" + D.id, weap = wk, name = D.name, soon = null,
                    deg = ((deg % 360) + 360) % 360,
                    x = Math.Round(CX + Math.Cos(a) * R), y = Math.Round(CY + Math.Sin(a) * R)
                };
                /* ONLY A REAL WEAPON IS A PLACE YOU CAN BEGIN */
                if (wk != null) META_STARTS.Add(st);
                var door = push(new MetaNode { id = st.id, kind = "start", weap = wk, name = st.name, soon = st.soon,
                    spoke = "core", cl = null, x = st.x, y = st.y, at = 0, cost = 0 });
                var sorted = ringHubs(0).ToList().SortJs((p, q) =>
                    Math.Abs(((p.deg - deg + 540) % 360) - 180) - Math.Abs(((q.deg - deg + 540) % 360) - 180));
                /* THE FOUR NEAREST THAT DO NOT CROSS */
                bool crossesAny(double ax, double ay, double bx, double by)
                {
                    foreach (var e in edges)
                    {
                        var P1 = META_BY_ID.GetValueOrDefault(e.a); var P2 = META_BY_ID.GetValueOrDefault(e.b);
                        if (P1 == null || P2 == null) continue;
                        if (SegCross(ax, ay, bx, by, P1, P2)) return true;
                    }
                    return false;
                }
                double bearing(MetaHub h0) => Math.Atan2(h0.y - st.y, h0.x - st.x);
                double apart(double a2, double b2) => Math.Abs(((a2 - b2 + Math.PI * 3) % (Math.PI * 2)) - Math.PI);
                /* TWO OUT AND ONE IN, since the Heart */
                const int WAYS = 2;
                var near = new List<MetaHub>();
                foreach (var gap in new[] { 0.72, 0.52, 0.34, 0 })
                {
                    foreach (var pass in new[] { 0, 1 })
                    {
                        foreach (var h0 in sorted)
                        {
                            if (near.Count >= WAYS) break;
                            if (near.Contains(h0)) continue;
                            if (gap > 0 && near.Any(o => apart(bearing(o), bearing(h0)) < gap)) continue;
                            if (pass == 0 && crossesAny(st.x, st.y, h0.x, h0.y)) continue;
                            near.Add(h0);
                        }
                        if (near.Count >= WAYS) break;
                    }
                }
                /* AN UNFINISHED DOOR IS A SIGN, NOT A ROAD */
                if (wk == null)
                {
                    var pk = sorted.FirstOrDefault(h0 => !crossesAny(st.x, st.y, h0.x, h0.y)) ?? sorted.FirstOrDefault();
                    if (pk != null) lane(door, pk.node, "soon_" + D.id);
                    continue;
                }
                var flavour = META_BRANCH.GetValueOrDefault(wk) ?? META_BRANCH["_"];
                /* the way in: to the stop of the Heart nearest the door's own bearing */
                var hin = heart.Where(o => !heartUsed.Contains(o.id)).ToList()
                               .SortJs((p, q) => gapDeg(p.deg, st.deg) - gapDeg(q.deg, st.deg))[0];
                heartUsed.Add(hin.id);
                var targets = near.Take(WAYS).Select(h0 => (node: h0.node, x: h0.x, y: h0.y, inward: false)).ToList();
                targets.Add((hin, hin.x, hin.y, true));
                for (int k = 0; k < targets.Count; k++)
                {
                    var h0 = targets[k];
                    /* THREE STEPS, not one */
                    var path = flavour[k % flavour.Count];
                    double fa = Math.Atan2(h0.y - st.y, h0.x - st.x);
                    /* SHORT OF THE RING, ALWAYS */
                    double span = Math.Min(420, hypot(h0.x - st.x, h0.y - st.y) * 0.58);
                    var prev = door;
                    for (int j = 0; j < path.Count; j++)
                    {
                        var row = path[j];
                        double t = (j + 1) / (path.Count + 0.35);
                        double off = ((j % 2) != 0 ? 1 : -1) * (j == 0 ? 0 : 26);
                        double fx = Math.Round(st.x + Math.Cos(fa) * span * t + Math.Cos(fa + Math.PI / 2) * off);
                        double fy = Math.Round(st.y + Math.Sin(fa) * span * t + Math.Sin(fa + Math.PI / 2) * off);
                        var n = push(new MetaNode { id = st.id + "_s" + k + "_" + j, kind = "minor", spoke = metaSectorAt(fx, fy),
                            cl = null, start = wk, name = row.name, x = fx, y = fy, at = 0, cost = 1, mods = one(row.stat, row.v) });
                        if (h0.inward) n.inward = wk;
                        add(prev.id, n.id);
                        prev = n;
                    }
                    lane(prev, h0.node, "door_" + wk + "_" + k);
                }
            }
        }
        /* ---- 6c. THE HEART'S GROUPS, now the doors and their ways in stand ---- */
        {
            int gi2 = 0;
            var dealt = new NumMap();
            foreach (var w in ways)
            {
                double span = w.span;
                if (span < HEART_GROUP_GAP) continue;
                foreach (var f in new[] { 0.27, 0.73 })
                {
                    /* halfway round from the door's passage to where the highway runs */
                    double from = f < 0.5 ? w.A.deg : w.B.deg;
                    double turn = ((w.hwDeg - from + 540) % 360) - 180;
                    double deg = ((from + turn / 2) % 360 + 360) % 360, a = deg * Math.PI / 180;
                    double x = Math.Round(CX + Math.Cos(a) * HEART_SITE_R), y = Math.Round(CY + Math.Sin(a) * HEART_SITE_R);
                    string sec = metaSectorAt(x, y); var mine = META_HEART_GROUPS.Where(g => g.home == sec).ToList();
                    dealt[sec] = dealt[sec] + 1; gi2++;
                    var tpl = mine[((int)dealt[sec] - 1) % mine.Count];
                    /* it hangs between the way in of the door it is nearer and this highway */
                    var hs = new MetaSite
                    {
                        ring = 0, j = 3000 + gi2, deg = deg, rim = false, x = x, y = y, sec = sec, site = tpl, heart = true,
                        ways = new List<string> { "in:" + (f < 0.5 ? w.A : w.B).weap, w.key }
                    };
                    double g0 = double.PositiveInfinity;
                    foreach (var o in META_NODES) if (o.kind == "start" || o.road == "heart") g0 = Math.Min(g0, hypot(o.x - x, o.y - y));
                    hs.gap = Math.Min(560, g0 * 1.4); hs.wall = wallAt(x, y).d;
                    sites.Add(hs);
                    drawSite(hs);
                    hs.cluster.heart = hs.ways;
                }
            }
        }
        /* ---- 7. THE NUDGE ---- */
        {
            double R(MetaNode n) => META_NODE_R.GetValueOrDefault(n.kind ?? "", 14);
            var home = new Dictionary<string, double[]>();
            /* EVERYTHING BUT A DOOR MAY GIVE A LITTLE */
            foreach (var n in META_NODES) if (n.kind != "start") home[n.id] = new[] { n.x, n.y };
            var seg = new List<(MetaNode A, MetaNode B)>();
            foreach (var e in edges)
            {
                var A = META_BY_ID.GetValueOrDefault(e.a); var B = META_BY_ID.GetValueOrDefault(e.b);
                if (A != null && B != null && A != B) seg.Add((A, B));
            }
            /* AND ONLY WHAT IS NEAR: gathered once off a grid */
            const double CELL = 300; var grid = new Dictionary<string, List<MetaNode>>();
            foreach (var n in META_NODES)
            {
                string k = Js.I32(n.x / CELL) + "," + Js.I32(n.y / CELL);
                if (!grid.TryGetValue(k, out var gl)) { gl = new(); grid[k] = gl; }
                gl.Add(n);
            }
            var nearM = new Dictionary<string, List<MetaNode>>();
            foreach (var n in META_NODES)
            {
                int gx = Js.I32(n.x / CELL), gy = Js.I32(n.y / CELL); var list = new List<MetaNode>();
                for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
                    {
                        if (!grid.TryGetValue((gx + i) + "," + (gy + j), out var cell)) continue;
                        foreach (var o in cell) if (o != n && hypot(o.x - n.x, o.y - n.y) < 260) list.Add(o);
                    }
                nearM[n.id] = list;
            }
            var cand = new Dictionary<string, List<(MetaNode A, MetaNode B)>>();
            foreach (var n in META_NODES)
            {
                var list = new List<(MetaNode A, MetaNode B)>();
                foreach (var sg in seg)
                {
                    if (sg.A == n || sg.B == n) continue;
                    if (n.x < Math.Min(sg.A.x, sg.B.x) - 170 || n.x > Math.Max(sg.A.x, sg.B.x) + 170) continue;
                    if (n.y < Math.Min(sg.A.y, sg.B.y) - 170 || n.y > Math.Max(sg.A.y, sg.B.y) + 170) continue;
                    list.Add(sg);
                }
                cand[n.id] = list;
            }
            for (int pass = 0; pass < 40; pass++)
                foreach (var n in META_NODES)
                {
                    if (!home.ContainsKey(n.id)) continue;
                    double mx = 0, my = 0;
                    foreach (var o in nearM[n.id])
                    {
                        /* TWO NODES OF THE SAME GROUP WANT MORE AIR THAN TWO STRANGERS */
                        double dx = n.x - o.x, dy = n.y - o.y, d = hypot(dx, dy),
                               need = R(n) + R(o) + 18 + ((n.cl != null && n.cl == o.cl) ? 22 : 0);
                        if (d >= need) continue;
                        double a = d < 0.01 ? ((n.x * 7 + n.y * 13) % 628) / 100 : Math.Atan2(dy, dx);
                        mx += Math.Cos(a) * (need - d) * 0.5; my += Math.Sin(a) * (need - d) * 0.5;
                    }
                    foreach (var sg in cand[n.id])
                    {
                        var a = sg.A; var b = sg.B;
                        double ex = b.x - a.x, ey = b.y - a.y, L2 = ex * ex + ey * ey;
                        if (L2 < 1) continue;
                        double t = ((n.x - a.x) * ex + (n.y - a.y) * ey) / L2;
                        if (t <= 0.02 || t >= 0.98) continue;            /* past an end is not through it */
                        double dx = n.x - (a.x + ex * t), dy = n.y - (a.y + ey * t);
                        double d = hypot(dx, dy), need = R(n) + 26;
                        if (d >= need) continue;
                        double ang = d < 0.01 ? Math.Atan2(-ey, ex) : Math.Atan2(dy, dx);
                        mx += Math.Cos(ang) * (need - d) * 0.7; my += Math.Sin(ang) * (need - d) * 0.7;
                        /* AND THE LINE GIVES WAY TOO */
                        foreach (var end in new[] { sg.A, sg.B })
                        {
                            if (!home.ContainsKey(end.id)) continue;
                            double bx = Math.Cos(ang) * (need - d) * -0.22, by = Math.Sin(ang) * (need - d) * -0.22;
                            end.x = Math.Round(end.x + bx); end.y = Math.Round(end.y + by);
                            var eh = home[end.id]; double ex2 = end.x - eh[0], ey2 = end.y - eh[1];
                            double ed = hypot(ex2, ey2), ecap = end.kind == "travel" ? 70 : end.kind == "minor" ? 48 : 34;
                            if (ed > ecap) { end.x = Math.Round(eh[0] + ex2 / ed * ecap); end.y = Math.Round(eh[1] + ey2 / ed * ecap); }
                        }
                    }
                    if (mx == 0 && my == 0) continue;
                    n.x = Math.Round(n.x + mx); n.y = Math.Round(n.y + my);
                    double cap = n.kind == "travel" ? 70 : n.kind == "minor" ? 48 : 34;
                    var h = home[n.id]; double ox = n.x - h[0], oy = n.y - h[1], od = hypot(ox, oy);
                    if (od > cap) { n.x = Math.Round(h[0] + ox / od * cap); n.y = Math.Round(h[1] + oy / od * cap); }
                }
            /* AND THEN READ THE GROUND AGAIN */
            foreach (var n in META_NODES)
            {
                if (n.kind != "travel") continue;
                string sec = metaSectorAt(n.x, n.y);
                if (n.spoke == sec) continue;
                var use = META_TRAVEL.GetValueOrDefault(sec, META_TRAVEL["core"]);
                n.spoke = sec; n.name = use.name; n.mods = one(use.stat, use.v);
            }
        }
        /* ---- 8. AND HOW YOU GET OFF THE HIGHWAY INTO ONE ---- */
        {
            var lanePool = META_NODES.Where(n => n.kind == "travel").ToList();
            double RAD(MetaNode n) => META_NODE_R.GetValueOrDefault(n.kind ?? "", 14);
            var seg = new List<(MetaNode A, MetaNode B)>();
            foreach (var e in edges)
            {
                var A = META_BY_ID.GetValueOrDefault(e.a); var B = META_BY_ID.GetValueOrDefault(e.b);
                if (A != null && B != null) seg.Add((A, B));
            }
            bool crosses(MetaNode a, MetaNode b)
            {
                foreach (var sg in seg)
                {
                    var P1 = sg.A; var P2 = sg.B;
                    if (P1 == a || P1 == b || P2 == a || P2 == b) continue;
                    if (Math.Min(P1.x, P2.x) > Math.Max(a.x, b.x) || Math.Max(P1.x, P2.x) < Math.Min(a.x, b.x)) continue;
                    if (Math.Min(P1.y, P2.y) > Math.Max(a.y, b.y) || Math.Max(P1.y, P2.y) < Math.Min(a.y, b.y)) continue;
                    if (SegCross(a.x, a.y, b.x, b.y, P1, P2)) return true;
                }
                return false;
            }
            bool grazes(MetaNode a, MetaNode b)
            {
                double ex = b.x - a.x, ey = b.y - a.y, L2 = ex * ex + ey * ey;
                if (L2 < 1) return true;
                foreach (var o in META_NODES)
                {
                    if (o == a || o == b) continue;
                    double t = ((o.x - a.x) * ex + (o.y - a.y) * ey) / L2;
                    if (t <= 0.02 || t >= 0.98) continue;
                    if (hypot(o.x - (a.x + ex * t), o.y - (a.y + ey * t)) < RAD(o) + 22) return true;
                }
                return false;
            }
            foreach (var c in META_CLUSTERS)
            {
                /* A GROUP OF THE HEART is joined to the two roads either side of it and to nothing else */
                if (c.heart != null)
                {
                    var mine0 = c.nodes.Select(id => META_BY_ID.GetValueOrDefault(id)).Where(n => n != null).ToList();
                    int got = 0;
                    foreach (var key in c.heart)
                    {
                        (MetaNode ln, MetaNode n)? best = null; double bd = double.PositiveInfinity;
                        bool isIn = key.StartsWith("in:"); string wk = key.Substring(3);
                        foreach (var ln in META_NODES)
                        {
                            if (isIn ? ln.inward != wk : (ln.kind != "travel" || ln.road != key)) continue;
                            foreach (var n in mine0) { double d = hypot(n.x - ln.x, n.y - ln.y); if (d < bd) { bd = d; best = (ln, n); } }
                        }
                        if (best != null) { add(best.Value.ln.id, best.Value.n.id); seg.Add((best.Value.ln, best.Value.n)); got++; }
                    }
                    c.ways = got;
                    continue;
                }
                int want = c.rim ? 1 : 2 + (int)(hash(c.x * 13 + c.y * 7) % 2);
                var mine = c.nodes.Select(id => META_BY_ID.GetValueOrDefault(id)).Where(n => n != null).ToList();
                /* every lane node near enough, against every node of the cluster, cheapest clean pair first */
                var pairs = new List<(MetaNode ln, MetaNode n, double d, double bear)>();
                foreach (var ln in lanePool)
                {
                    double dc = hypot(ln.x - c.x, ln.y - c.y);
                    if (dc > 1300) continue;
                    foreach (var n in mine) pairs.Add((ln, n, hypot(n.x - ln.x, n.y - ln.y), Math.Atan2(ln.y - c.y, ln.x - c.x)));
                }
                pairs = pairs.SortJs((a, b) => a.d - b.d);
                var picked = new List<(MetaNode ln, MetaNode n, double d, double bear)>();
                /* A CLUSTER IS A PLACE YOU WALK THROUGH: its second way in comes from the OTHER side */
                bool apartBy((MetaNode ln, MetaNode n, double d, double bear) o, double lim) =>
                    picked.All(p2 => Math.Abs(((o.bear - p2.bear + Math.PI * 3) % (Math.PI * 2)) - Math.PI) >= lim);
                foreach (var lim in new[] { THROUGH_ANGLE, THROUGH_ANGLE * 0.64 })
                {
                    if (c.rim || picked.Count >= 2) break;
                    foreach (var o in pairs)
                    {
                        if (picked.Count >= 2) break;
                        if (o.d > 950 && picked.Count == 0) continue;
                        if (picked.Any(p2 => p2.ln == o.ln)) continue;
                        if (picked.Count > 0 && !apartBy(o, lim)) continue;
                        if (grazes(o.ln, o.n) || crosses(o.ln, o.n)) continue;
                        picked.Add(o);
                        seg.Add((o.ln, o.n));
                    }
                }
                /* THREE SWEEPS, EACH WORSE THAN THE LAST */
                foreach (var pass in new[] { 0, 1, 2 })
                {
                    if (picked.Count >= want) break;
                    int cap = pass != 0 ? Math.Min(want, 2) : want;
                    foreach (var o in pairs)
                    {
                        if (picked.Count >= cap) break;
                        if (picked.Any(p2 => hypot(p2.ln.x - o.ln.x, p2.ln.y - o.ln.y) < 420)) continue;
                        if (picked.Any(p2 => p2.ln == o.ln)) continue;
                        if (hypot(o.ln.x - c.x, o.ln.y - c.y) > 950) continue;
                        if (pass < 2 && grazes(o.ln, o.n)) continue;
                        if (pass < 1 && crosses(o.ln, o.n)) continue;
                        picked.Add(o);
                        seg.Add((o.ln, o.n));
                    }
                    if (picked.Count >= cap) break;
                }
                c.ways = picked.Count;
                foreach (var o in picked) add(o.ln.id, o.n.id);
            }
        }
        /* ---- 8b. THE BALANCING BREAKS ---- */
        {
            var ATTR_NAME = new Dictionary<string, string> { ["str"] = "Strength", ["dex"] = "Dexterity", ["int"] = "Intelligence" };
            double RR(MetaNode n) => META_NODE_R.GetValueOrDefault(n.kind ?? "", 14);
            var segs = new List<(MetaNode A, MetaNode B)>();
            foreach (var e in edges) { var A = META_BY_ID.GetValueOrDefault(e.a); var B = META_BY_ID.GetValueOrDefault(e.b); if (A != null && B != null) segs.Add((A, B)); }
            foreach (var h in hubs)
            {
                if (h.ring < 1 || h.ring > 3 || h.j % 2 != 0) continue;
                var hn = h.node; string own = META_TRAVEL.ContainsKey(hn.spoke) ? META_TRAVEL[hn.spoke].stat : "str";
                var others = new[] { "str", "dex", "int" }.Where(k => k != own).ToList();
                var nearN = META_NODES.Where(n => n != hn && hypot(n.x - hn.x, n.y - hn.y) < ATTR_BREAK_D + 260).ToList();
                var nearS = segs.Where(sg => segDist(sg.A.x, sg.A.y, sg.B.x, sg.B.y, hn.x, hn.y) < ATTR_BREAK_D + 260).ToList();
                List<(double x, double y)> best = null; double bc = double.NegativeInfinity;
                for (int k = 0; k < 36; k++)
                {
                    double a = k / 36.0 * TAU;
                    var pts = new[] { a - 0.42, a + 0.42 }.Select(b => (x: Math.Round(hn.x + Math.Cos(b) * ATTR_BREAK_D), y: Math.Round(hn.y + Math.Sin(b) * ATTR_BREAK_D))).ToList();
                    double cl = double.PositiveInfinity;
                    foreach (var q in pts)
                    {
                        foreach (var n in nearN)
                        {
                            cl = Math.Min(cl, hypot(n.x - q.x, n.y - q.y) - RR(n) - ATTR_BREAK_R);
                            cl = Math.Min(cl, segDist(hn.x, hn.y, q.x, q.y, n.x, n.y) - RR(n) - 8);
                        }
                        foreach (var (A, B) in nearS)
                        {
                            if (A != hn && B != hn && SegCross(hn.x, hn.y, q.x, q.y, A, B)) { cl = double.NegativeInfinity; break; }
                            cl = Math.Min(cl, segDist(A.x, A.y, B.x, B.y, q.x, q.y) - ATTR_BREAK_R - 6);
                        }
                        if (cl == double.NegativeInfinity) break;
                    }
                    if (cl > bc) { bc = cl; best = pts; }
                }
                if (best == null || bc < 10) continue;
                for (int i = 0; i < best.Count; i++)
                {
                    var q = best[i]; string at = others[i];
                    var n = push(new MetaNode { id = "ab_" + hn.id + "_" + at, kind = "travel", road = "attr", spoke = hn.spoke, cl = null,
                        name = ATTR_NAME[at], x = q.x, y = q.y, at = 0, cost = 1, mods = one(at, ATTR_BREAK_N) });
                    add(hn.id, n.id);
                }
            }
        }
        /* ---- 9. THE BLOT AND THE NAME ---- */
        {
            double R(MetaNode n) => META_NODE_R.GetValueOrDefault(n.kind ?? "", 14);
            foreach (var c in META_CLUSTERS)
            {
                var ns = c.nodes.Concat(c.centre != null ? new[] { c.centre } : new string[0])
                                .Select(id => META_BY_ID.GetValueOrDefault(id)).Where(n => n != null).ToList();
                if (ns.Count == 0) { c.hr = 0; c.hx = c.x; c.hy = c.y; continue; }
                double sx = 0, sy = 0;
                foreach (var n in ns) { sx += n.x; sy += n.y; }
                c.hx = Math.Round(sx / ns.Count); c.hy = Math.Round(sy / ns.Count);
                double rr = 0;
                foreach (var n in ns) rr = Math.Max(rr, hypot(n.x - c.hx, n.y - c.hy) + R(n));
                rr += 24;
                foreach (var n in META_NODES)
                {
                    if (n.cl == c.id) continue;
                    rr = Math.Min(rr, hypot(n.x - c.hx, n.y - c.hy) - R(n) - 10);
                }
                c.hr = Math.Round(rr);
            }
            /* shrinking only ever takes away, so one sweep settles it */
            for (int i = 0; i < META_CLUSTERS.Count; i++)
                for (int j = i + 1; j < META_CLUSTERS.Count; j++)
                {
                    var a = META_CLUSTERS[i]; var b = META_CLUSTERS[j];
                    if (a.hr <= 0 || b.hr <= 0) continue;
                    double d = hypot(a.hx - b.hx, a.hy - b.hy) - 10;
                    if (a.hr + b.hr <= d) continue;
                    double k = Math.Max(0, d) / (a.hr + b.hr);
                    a.hr = Math.Floor(a.hr * k); b.hr = Math.Floor(b.hr * k);
                }
            const double CH = 17.5; var boxes = new List<double[]>();
            foreach (var c in META_CLUSTERS)
            {
                double w = Math.Max(60, c.name.Length * CH);
                double bse = Math.Atan2(c.hy - CY, c.hx - CX);
                var local = META_NODES.Where(n => hypot(n.x - c.hx, n.y - c.hy) < Math.Max(c.hr, 60) + 520).ToList();
                (double lx, double ly, string anch, double[] box)? best = null;
                for (int k = 0; k < 24 && best == null; k++)
                {
                    double a = bse + Math.Ceiling(k / 2.0) * (k % 2 != 0 ? 1 : -1) * (Math.PI / 12);
                    double u = Math.Cos(a), v = Math.Sin(a);
                    string anch = u > 0.3 ? "start" : u < -0.3 ? "end" : "middle";
                    foreach (var off in new[] { Math.Max(c.hr, 40) + 48, Math.Max(c.hr, 40) + 92 })
                    {
                        double lx = c.hx + u * off;
                        double ly = c.hy + v * off + (v > 0.3 ? 26 : v < -0.3 ? -6 : 10);
                        double x0 = anch == "start" ? lx : anch == "end" ? lx - w : lx - w / 2;
                        var box = new[] { x0 - 10, ly - 27, x0 + w + 10, ly + 9 };
                        bool bad = false;
                        foreach (var n in local)
                        {
                            double rr = R(n);
                            if (n.x + rr > box[0] && n.x - rr < box[2] && n.y + rr > box[1] && n.y - rr < box[3]) { bad = true; break; }
                        }
                        if (!bad) foreach (var o in boxes)
                                if (o[0] < box[2] && o[2] > box[0] && o[1] < box[3] && o[3] > box[1]) { bad = true; break; }
                        if (bad) continue;
                        best = (Math.Round(lx), Math.Round(ly), anch, box);
                        break;
                    }
                }
                if (best == null) { c.lx = double.NaN; continue; }
                c.lx = best.Value.lx; c.ly = best.Value.ly; c.lanch = best.Value.anch;
                boxes.Add(best.Value.box);
            }
        }
        /* ---- 10. REQ IS THE ADJACENCY, BOTH WAYS ---- */
        foreach (var e in edges)
        {
            var A = META_BY_ID.GetValueOrDefault(e.a); var B = META_BY_ID.GetValueOrDefault(e.b);
            if (A == null || B == null) continue;
            if (A.kind != "start" && !A.req.Contains(e.b)) A.req.Add(e.b);
            if (B.kind != "start" && !B.req.Contains(e.a)) B.req.Add(e.a);
        }
        META_TREE_UNPLACED = unplaced;
        /* ---- 10b. PoE'S NUMBERS: 16% / 30% on damage, +10 / +20 on an attribute ---- */
        foreach (var n in META_NODES)
        {
            bool minor = n.kind == "minor", notable = n.kind == "notable";
            if (!minor && !notable) continue;
            n.mods = n.mods.Select(m =>
            {
                if (!(m.num && m.v > 0)) return m;
                if (META_INC_DMG.IsMatch(m.k)) return new Mod(m.k, minor ? META_INC_MINOR : META_INC_NOTABLE);
                if (m.k == "str" || m.k == "dex" || m.k == "int") return new Mod(m.k, minor ? META_ATTR_MINOR : META_ATTR_NOTABLE);
                return m;
            }).ToList();
        }
        /* ---- 11. INTELLIGENCE FEEDS THE LEGION ---- */
        foreach (var n in META_NODES)
        {
            if (n.kind != "minor" || n.mods.Count != 1) continue;
            if (metaSectorAt(n.x, n.y) != "int") continue;
            var sis = n.mods[0].num ? minionSister(n.mods[0].k, n.mods[0].v) : null;
            if (sis != null) n.mods.Add(sis);
        }
    }
}
