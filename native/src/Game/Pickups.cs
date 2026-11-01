namespace Brickblade;

/* =============================== PICKUPS / DEBRIS / PARTICLES (JS 4939–5411) =============================== */
public sealed class StudTier { public double val, xp, r; public string col, next, label; }
public sealed partial class Pickup : Obj
{
    public string kind; public double z, vx, vy, vz, spin, t, val, xp;
    public bool seeking; public double seekX, seekY;
}
public sealed class Debris { public double x, y, z, vx, vy, vz, rot, vr, w, h, life, max; public Col color; }
public sealed partial class Particle : Obj
{
    public string kind; public double vx, vy, life, max, v, lw, maxLife;
    public Col color, col;
    public double x1, y1, x2, y2, seed, link; public bool hostile;
}
public sealed class DmgText : Obj { public string txt; public Col color; public double size, life, vy; }
public sealed class Corpse : Obj { public double t, a, seed; public bool big; }
public sealed class ExplodeOpts { public bool hostile; public Col[] brickCols; public double shake = double.NaN; public string source; }
public sealed class FxPal
{
    public string ring, ringHot, spark; public string[] bricks; public string zap, zapCore, brick, brickStud, frost, frostDeep;
}
public sealed class FigCols { public string head, torso, legs, hands, hair, hat, cape, acc, eye, belt, arm; }

public static partial class G
{
    public static readonly Dictionary<string, StudTier> STUD_TIERS = new()
    {
        ["stud1"] = new StudTier { val = 1, xp = 1, r = 5.6, col = PAL.gold, next = "stud5" },
        ["stud5"] = new StudTier { val = 5, xp = 3, r = 7, col = PAL.blue, next = "stud20" },
        ["stud20"] = new StudTier { val = 20, xp = 8, r = 8, col = PAL.red, next = "stud50" },
        ["stud50"] = new StudTier { val = 50, xp = 18, r = 9.5, col = "#a45bd6", next = "stud100", label = "PURPLE" },
        ["stud100"] = new StudTier { val = 100, xp = 34, r = 11, col = "#2ea8a0", next = "stud250", label = "TEAL" },
        ["stud250"] = new StudTier { val = 250, xp = 60, r = 12.5, col = "#fe8a18", next = null, label = "ORANGE" },
    };
    public static readonly string[] STUD_ORDER = { "stud1", "stud5", "stud20", "stud50", "stud100", "stud250" };
    public static bool isStud(Pickup pk) => pk.kind != null && STUD_TIERS.ContainsKey(pk.kind);
    /* everything that lands is shoved back onto floor you can walk on */
    public static T shoveToFloor<T>(T o, double pad = double.NaN) where T : Obj
    {
        double r = double.IsNaN(pad) ? 10 : pad;
        o.x = clamp(o.x, W.wall + r + 4, W.w - W.wall - r - 4);
        o.y = clamp(o.y, W.wall + r + 4, W.h - W.wall - r - 4);
        foreach (var sd in solids)
        {
            double d = dist(o, sd), rr = sd.r + r;
            if (d < rr && d > 0.001) { o.x += (o.x - sd.x) / d * (rr - d); o.y += (o.y - sd.y) / d * (rr - d); }
            else if (d <= 0.001) { o.x = sd.x + rr; }
        }
        foreach (var w in waters)
        {
            if (o.x < w.x - r || o.x > w.x + w.w + r || o.y < w.y - r || o.y > w.y + w.h + r) continue;
            double l = o.x - (w.x - r), rt = (w.x + w.w + r) - o.x, t = o.y - (w.y - r), bt = (w.y + w.h + r) - o.y;
            double m = Math.Min(Math.Min(l, rt), Math.Min(t, bt));
            if (m == l) o.x = w.x - r;
            else if (m == rt) o.x = w.x + w.w + r;
            else if (m == t) o.y = w.y - r;
            else o.y = w.y + w.h + r;
        }
        return o;
    }
    public static bool floorClear(double x, double y, double r)
    {
        if (x < W.wall + r + 4 || x > W.w - W.wall - r - 4 || y < W.wall + r + 4 || y > W.h - W.wall - r - 4) return false;
        foreach (var sd in solids) if (!sd.dead && hypot(x - sd.x, y - sd.y) < sd.r + r) return false;
        foreach (var w in waters) if (x > w.x - r && x < w.x + w.w + r && y > w.y - r && y < w.y + w.h + r) return false;
        return true;
    }
    public static Pt floorAt(double x, double y, double r)
    {
        var o = new Pt(x, y);
        for (int i = 0; i < 3 && !floorClear(o.x, o.y, r); i++) shoveToFloor(o, r);
        if (floorClear(o.x, o.y, r)) return o;
        for (int ring = 1; ring <= 12; ring++)
            for (int k = 0; k < ring * 6; k++)
            {
                double a = (double)k / (ring * 6) * TAU, px = x + Math.Cos(a) * ring * 24, py = y + Math.Sin(a) * ring * 24;
                if (floorClear(px, py, r)) return new Pt(px, py);
            }
        return o;
    }
    public static Pickup makePickup(string kind, double x, double y)
    {
        var T = kind != null ? STUD_TIERS.GetValueOrDefault(kind) : null;
        return shoveToFloor(new Pickup
        {
            kind = kind, x = x, y = y, z = 14, vx = rnd(-90, 90), vy = rnd(-90, 90), vz = rnd(140, 300),
            spin = rnd(TAU), t = 0, val = T != null ? T.val : 1, xp = T != null ? T.xp : 0
        });
    }
    public static void spawnStuds(double x, double y, double amount)
    {
        double left = Math.Max(0, Math.Round(amount));
        double bigChance = wave.n > 15 ? 0.30 : 0.20, hugeChance = wave.n > 15 ? 0.07 : 0.04, megaChance = wave.n > 25 ? 0.05 : 0.02;
        int guard = 0;
        while (left > 0 && guard++ < 400)
        {
            double r = Js.R();
            string kind = "stud1";
            if (left >= 100 && r < megaChance * 0.4) kind = "stud100";
            else if (left >= 50 && r < megaChance) kind = "stud50";
            else if (left >= 20 && r < hugeChance) kind = "stud20";
            else if (left >= 5 && r < bigChance) kind = "stud5";
            left -= STUD_TIERS[kind].val;
            var p = makePickup(kind, x, y);
            if (kind != "stud1") { p.vx *= 1.5; p.vy *= 1.5; p.vz += 80; }
            pickups.Add(p);
        }
    }
    public static void spawnBricks(double x, double y, IList<Col> colors, int n, double pow)
    {
        for (int i = 0; i < n; i++)
        {
            if (debris.Count > 240) debris.RemoveAt(0);
            double a = rnd(TAU), sp = rnd(pow * 0.3, pow);
            debris.Add(new Debris
            {
                x = x, y = y, z = 8, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp, vz = rnd(160, 420),
                rot = rnd(TAU), vr = rnd(-9, 9), w = rnd(9, 17), h = rnd(6, 10),
                color = pick(colors), life = rnd(0.9, 1.6), max = 1.6
            });
        }
    }
    public static void spawnBricks(double x, double y, IList<string> colors, int n, double pow) => spawnBricks(x, y, colors.Select(c => (Col)c).ToList(), n, pow);
    public static void addSparks(double x, double y, int n, Col? color = null)
    {
        var c = color ?? (Col)"#fff2b0";
        for (int i = 0; i < n; i++)
        {
            double a = rnd(TAU), sp = rnd(60, 260);
            particles.Add(new Particle { kind = "spark", x = x, y = y, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp, life = rnd(0.15, 0.35), max = 0.35, color = c });
        }
    }
    public static void addSparks(double x, double y, int n, string color) => addSparks(x, y, n, (Col?)Col.Parse(color));
    /* the JS hands a colour PREFIX ('rgba(220,225,210,') and the renderer adds the alpha */
    public static void addDust(double x, double y, int n = 3, string col = "rgba(220,225,210,")
    {
        var c = Col.Parse(col.EndsWith(",") ? col + "1)" : col);
        for (int i = 0; i < n; i++) particles.Add(new Particle
        {
            kind = "dust", x = x + rnd(-6, 6), y = y + rnd(-4, 2),
            vx = rnd(-25, 25), vy = rnd(-30, -6), life = rnd(0.3, 0.55), max = 0.55, r = rnd(3, 6), col = c
        });
    }
    public static void addRing(double x, double y, double r0, double speed, double max, Col color, double lw = 6, double life = 0.5)
    {
        particles.Add(new Particle { kind = "ring", x = x, y = y, r = Math.Max(1, r0), v = Math.Max(1, speed), max = max, color = color, lw = lw, life = life, maxLife = life });
    }
    public static void addZap(double x1, double y1, double x2, double y2, double link = 0, bool hostile = false)
    {
        particles.Add(new Particle
        {
            kind = "zap", x1 = x1, y1 = y1, x2 = x2, y2 = y2, x = (x1 + x2) / 2, y = (y1 + y2) / 2,
            vx = 0, vy = 0, life = 0.16, max = 0.16, seed = rnd(TAU), link = link, hostile = hostile
        });
    }
    /* hearts read as 6, 5.5, 5.25 */
    public static string fmtHp(double v)
    {
        double r = Math.Round(v * 100) / 100;
        if (r == Math.Floor(r)) return N(r);
        return Math.Abs((r * 10) % 1) < 1e-9 ? F(r, 1) : F(r, 2);
    }
    public static void addDmg(double x, double y, string txt, Col? color = null, double size = 17)
    {
        dmgTexts.Add(new DmgText { x = x + rnd(-8, 8), y = y - 30, txt = txt, color = color ?? (Col)"#fff", size = size, life = 0.8, vy = -52 });
    }
    public static void addDmg(double x, double y, string txt, string color, double size = 17) => addDmg(x, y, txt, (Col?)Col.Parse(color), size);
    /* THE COMBAT TEXT, and only that, obeys DAMAGE NUMBERS */
    public static void addHitNum(double x, double y, string txt, Col color, double size)
    {
        if (!OPT.dmgNumbers) return;
        addDmg(x, y, txt, color, size);
    }
    /* ground-merge sweep: clusters of same-kind studs fuse into the next tier */
    public static void mergeSweep()
    {
        if (player != null && player.magnetT > 0)
        {
            foreach (var pk in pickups) pk.seeking = false;
            return;
        }
        if (pickups.Count < 12) return;
        var NEED = new Dictionary<string, int> { ["stud1"] = 10, ["stud5"] = 8, ["stud20"] = 5, ["stud50"] = 4, ["stud100"] = 5 };
        var OUT = new Dictionary<string, int> { ["stud1"] = 2, ["stud5"] = 2, ["stud20"] = 2, ["stud50"] = 2, ["stud100"] = 2 };
        var MERGEABLE = new[] { "stud1", "stud5", "stud20", "stud50", "stud100" };
        var used = new HashSet<Pickup>();
        int merged = 0;
        foreach (var kind in MERGEABLE)
        {
            int need = NEED[kind];
            var arr = new List<Pickup>();
            foreach (var pk in pickups) if (pk.kind == kind && !pk.dead && pk.z < 6) arr.Add(pk);
            if (arr.Count < need) continue;
            for (int i = 0; i < arr.Count && merged < 30; i++)
            {
                if (used.Contains(arr[i])) continue;
                var cluster = new List<Pickup> { arr[i] }; used.Add(arr[i]);
                for (int j = i + 1; j < arr.Count && cluster.Count < need; j++)
                {
                    if (used.Contains(arr[j])) continue;
                    if (hypot(arr[j].x - arr[i].x, arr[j].y - arr[i].y) < MERGE_NEAR) { cluster.Add(arr[j]); used.Add(arr[j]); }
                }
                if (cluster.Count < need) { foreach (var pk in cluster) used.Remove(pk); continue; }
                double cx = 0, cy = 0;
                foreach (var pk in cluster) { pk.dead = true; cx += pk.x; cy += pk.y; }
                cx /= cluster.Count; cy /= cluster.Count;
                string nk = STUD_TIERS[kind].next;
                for (int k2 = 0; k2 < OUT[kind]; k2++)
                {
                    var np = makePickup(nk, cx + rnd(-8, 8), cy + rnd(-8, 8));
                    np.z = 16; np.vz = 240;
                    np.vx = (k2 != 0 ? 1 : -1) * rnd(30, 70); np.vy = rnd(-40, 40);
                    pickups.Add(np);
                }
                addRing(cx, cy, 4, 240, 36, STUD_TIERS[nk].col, 3, 0.35);
                addSparks(cx, cy - 4, 4, sh(STUD_TIERS[nk].col, 0.4));
                merged++;
            }
        }
        foreach (var kind in MERGEABLE)
        {
            var arr = new List<Pickup>();
            foreach (var pk in pickups) if (pk.kind == kind && !pk.dead && pk.z < 6) arr.Add(pk);
            if (arr.Count < 2 || arr.Count > 300) { foreach (var pk in arr) pk.seeking = false; continue; }
            foreach (var pk in arr)
            {
                if (pk.t < MERGE_AGE) { pk.seeking = false; continue; }
                double ax2 = 0, ay2 = 0; int n = 0;
                foreach (var o in arr)
                {
                    if (o == pk) continue;
                    if (hypot(o.x - pk.x, o.y - pk.y) < MERGE_DRIFT) { ax2 += o.x; ay2 += o.y; n++; }
                }
                if (n == 0) { pk.seeking = false; continue; }
                ax2 /= n; ay2 /= n;
                if (hypot(ax2 - pk.x, ay2 - pk.y) < MERGE_NEAR * 0.5) { pk.seeking = false; continue; }
                pk.seeking = true; pk.seekX = ax2; pk.seekY = ay2;
            }
        }
        if (merged > 0)
        {
            pickups = pickups.Where(pk => !pk.dead).ToList();
            SFX.stud(6);
        }
    }
    /* What it takes to HIT a body: its drawn footprint, not its collision radius */
    public static double hitR(Ent e)
    {
        if (e == null) return 0;
        double s = Js.Or(e.scale, 1);
        return Math.Max(e.r, (e.golem != null ? 23 : 15) * s);
    }
    /* THE BODY: a capsule from the hips to the top of the head */
    public static double bodyLo(Ent o) => o.y - 3 * Js.Or(o.scale, 1) - o.z;
    public static double bodyHi(Ent o)
    {
        double s = Js.Or(o.scale, 1);
        double tall = o.golem != null ? 62 : o.type == "boss" ? 54 : 40;
        return o.y - tall * s - o.z;
    }
    public static double bodyRad(Ent o) => Math.Max(9, (o.golem != null ? 20 : 12) * Js.Or(o.scale, 1));
    public static double bodyDist(Ent o, double x, double y)
    {
        double lo = bodyLo(o), hi = bodyHi(o);
        double dx = x - o.x;
        double dy = y > lo ? y - lo : (y < hi ? y - hi : 0);
        return Math.Sqrt(dx * dx + dy * dy);
    }
    public static double segSegDist(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        double ux = bx - ax, uy = by - ay, vx = dx - cx, vy = dy - cy;
        double wx = ax - cx, wy = ay - cy;
        double a = ux * ux + uy * uy, b = ux * vx + uy * vy, c = vx * vx + vy * vy;
        double d = ux * wx + uy * wy, e = vx * wx + vy * wy;
        double D = a * c - b * b;
        double sN, sD = D, tN, tD = D;
        if (D < 1e-9) { sN = 0; sD = 1; tN = e; tD = c; }
        else
        {
            sN = b * e - c * d; tN = a * e - b * d;
            if (sN < 0) { sN = 0; tN = e; tD = c; }
            else if (sN > sD) { sN = sD; tN = e + b; tD = c; }
        }
        if (tN < 0)
        {
            tN = 0;
            if (-d < 0) sN = 0; else if (-d > a) sN = sD; else { sN = -d; sD = a; }
        }
        else if (tN > tD)
        {
            tN = tD;
            double t2 = -d + b;
            if (t2 < 0) sN = 0; else if (t2 > a) sN = sD; else { sN = t2; sD = a; }
        }
        double sc = Math.Abs(sN) < 1e-9 ? 0 : sN / sD;
        double tc = Math.Abs(tN) < 1e-9 ? 0 : tN / tD;
        double qx = wx + sc * ux - tc * vx, qy = wy + sc * uy - tc * vy;
        return Math.Sqrt(qx * qx + qy * qy);
    }
    public static bool hitsBody(Ent o, double x, double y, double r = 0)
    {
        double rr = bodyRad(o) + r;
        double dx = x - o.x;
        if (dx > rr || dx < -rr) return false;
        double lo = bodyLo(o), hi = bodyHi(o);
        if (y > lo + rr || y < hi - rr) return false;
        double dy = y > lo ? y - lo : (y < hi ? y - hi : 0);
        return dx * dx + dy * dy < rr * rr;
    }
    public static bool sweepsBody(Ent o, double x0, double y0, double x1, double y1, double r = 0)
    {
        double rr = bodyRad(o) + r;
        double minx = Math.Min(x0, x1) - rr, maxx = Math.Max(x0, x1) + rr;
        if (o.x < minx || o.x > maxx) return false;
        double lo = bodyLo(o), hi = bodyHi(o);
        double miny = Math.Min(y0, y1) - rr, maxy = Math.Max(y0, y1) + rr;
        if (lo < miny || hi > maxy) return false;
        return segSegDist(x0, y0, x1, y1, o.x, lo, o.x, hi) < rr;
    }
    /* FRIEND OR FOE, AT A GLANCE: the hero's effects pale, the monsters' deep */
    public static readonly FxPal FX_friend = new FxPal
    {
        ring = "#fff2c4", ringHot = "#ffd98a", spark = "#fffaf0", bricks = new[] { "#fff2c4", "#ffd98a", "#ffe89a" },
        zap = CONV_COL["shock"], zapCore = "#fffbe0", brick = "#ffe89a", brickStud = "#ffd166", frost = "#f2fbff", frostDeep = "#cfeaff"
    };
    public static readonly FxPal FX_foe = new FxPal
    {
        ring = "#c2410c", ringHot = "#7c2d12", spark = "#e2620f", bricks = new[] { "#c2410c", "#7c2d12", "#3a2f2a" },
        zap = "#e0a81a", zapCore = "#ffe95a", frost = "#79b4dc", frostDeep = "#3f7ba8"
    };
    public static readonly (FxPal friend, FxPal foe) FX = (FX_friend, FX_foe);
    public static void explode(double x, double y, double r, double dmg, ExplodeOpts opts = null)
    {
        opts ??= new ExplodeOpts();
        var C = opts.hostile ? FX.foe : FX.friend;
        addRing(x, y, 10, r * 4.2, r * 1.35, C.ring, 9, 0.4);
        addRing(x, y, 6, r * 3.4, r, C.ringHot, 5, 0.32);
        addSparks(x, y, 16, C.spark);
        for (int i = 0; i < 3; i++) addDust(x + rnd(-r / 3, r / 3), y + rnd(-r / 3, r / 3), 2, "rgba(90,85,75,");
        if (opts.brickCols != null) spawnBricks(x, y, opts.brickCols, 10, 220); else spawnBricks(x, y, C.bricks, 10, 220);
        SFX.boomSmall(); addShake(double.IsNaN(opts.shake) ? 0.35 : opts.shake);
        if (!opts.hostile)
        {
            var at = new Pt(x, y);
            foreach (var e in enemies.ToList())
            {
                if (e.state != "active" || e.dead) continue;
                if (dist(e, at) < r + hitR(e))
                    hitEnemy(e, dmg, angTo(at, e), 240, opts.source ?? "bomb", new HitOpts { spin = true });
            }
            foreach (var p in props.ToList())
                if (!p.dead && !p.indestructible && dist(p, at) < r + p.r) breakProp(p);
        }
    }
    /* =============================== THE DEAD THAT WALK =============================== */
    public static readonly Dictionary<string, string> ZOMBIE_CRAWL = new() { ["zombie"] = "crawler", ["zrunner"] = "crawler", ["zhulk"] = "bigcrawler" };
    public const double ZOMBIE_CRAWL_CHANCE = 0.4;
    public const double BLOAT_WIND = 0.55, BLOAT_BREATH = 1.1, BLOAT_TICK = 0.25, BLOAT_RANGE = 150, BLOAT_CONE = 0.55;
    public const double BLOAT_PSN = 0.35, BLOAT_CD = 4.5;
    public static readonly int[] BLOAT_GIBS = { 8, 11 };
    public static readonly double[] BLOAT_GIB_SPD = { 230, 330 };
    public const double BLOAT_GIB_LIFE = 0.85, BLOAT_GIB_DMG = 1.2;
    public const double CORPSE_LIFE = 30; public const int CORPSE_MAX = 60;
    public static List<Corpse> corpses = new();
    public static bool isZombieType(string t) => t != null && EDEF.TryGetValue(t, out var d) && d.zombie;
    public static void zombieDeath(Ent e)
    {
        if (!isZombieType(e.type) || e.breach != null) return;
        if (e.type == "bloater")
        {
            int n = irnd(BLOAT_GIBS[0], BLOAT_GIBS[1]);
            for (int i = 0; i < n; i++)
            {
                double a = (double)i / n * TAU + rnd(-0.25, 0.25), sp = rnd(BLOAT_GIB_SPD[0], BLOAT_GIB_SPD[1]);
                projectiles.Add(new Proj
                {
                    kind = "arrow", gib = true, owner = "enemy", x = e.x, y = e.y - 16, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp,
                    dmg = Math.Max(1, Math.Round(Js.Or(e.dmg, 1) * BLOAT_GIB_DMG * 10) / 10), life = BLOAT_GIB_LIFE, spin = rnd(TAU)
                });
            }
            addRing(e.x, e.y, 12, 380, 110, "#9ede5a", 7, 0.45);
            spawnBricks(e.x, e.y, new[] { "#9dbb62", "#7e6b4a", "#6b8e23" }, 12, 260);
            addShake(0.25); SFX.boomSmall();
            return;
        }
        if (ZOMBIE_CRAWL.TryGetValue(e.type, out var crawl) && Js.R() < ZOMBIE_CRAWL_CHANCE)
        {
            int n0 = enemies.Count;
            spawnEnemy(crawl, e.x, e.y, false, false);
            if (enemies.Count > n0)
            {
                var c2 = enemies[^1];
                c2.state = "active"; c2.buildT = 1; c2.x = e.x; c2.y = e.y; c2.hitCd = 0.6;
                addDust(e.x, e.y, 5, "rgba(90,110,60,");
                addDmg(e.x, e.y - 34, "IT CRAWLS", "#9ede5a", 13);
            }
            return;
        }
        layCorpse(e);
    }
    public static void layCorpse(Ent e)
    {
        if (!isSummoner()) return;
        corpses.Add(new Corpse { x = e.x, y = e.y, t = CORPSE_LIFE, big = Js.Or(e.scale, 1) > 1.1, a = rnd(TAU), seed = rnd(1000) });
        if (corpses.Count > CORPSE_MAX) corpses.RemoveAt(0);
    }
    public static void updateCorpses(double dt)
    {
        if (corpses.Count == 0) return;
        foreach (var c in corpses) c.t -= dt;
        corpses = corpses.Where(c => c.t > 0).ToList();
    }
    public static void drawCorpses()
    {
        if (corpses.Count == 0 || !isSummoner()) return;
        var c = ctx;
        foreach (var k in corpses)
        {
            if (!onScreen(k, 60)) continue;
            double fade = clamp(k.t / 3, 0, 1), s = k.big ? 1.2 : 0.85;
            c.save(); c.translate(k.x, k.y); c.rotate(k.a); c.globalAlpha = 0.9 * fade;
            c.fillStyle = "rgba(30,20,20,0.35)"; c.beginPath(); c.ellipse(0, 2, 20 * s, 8 * s, 0, 0, TAU); c.fill();
            c.fillStyle = "#5a5044"; c.strokeStyle = "rgba(20,20,20,0.5)"; c.lineWidth = 1.5;
            roundRect(c, -12 * s, -5 * s, 20 * s, 10 * s, 3 * s); c.fill(); c.stroke();
            c.fillStyle = "#6f8a52";
            c.beginPath(); c.arc(12 * s, 0, 5.5 * s, 0, TAU); c.fill(); c.stroke();
            c.strokeStyle = "#6f8a52"; c.lineWidth = 3.5 * s; c.lineCap = "round";
            c.beginPath(); c.moveTo(5 * s, -5 * s); c.lineTo(12 * s, -11 * s); c.moveTo(3 * s, 5 * s); c.lineTo(10 * s, 11 * s); c.stroke();
            c.globalAlpha = 0.35 * fade * (0.6 + 0.4 * Math.Sin(vtime * 3 + k.seed));
            c.strokeStyle = "#b7a6ff"; c.lineWidth = 1.5;
            c.beginPath(); c.ellipse(0, 1, 22 * s, 9 * s, 0, 0, TAU); c.stroke();
            c.restore();
        }
    }
    public static void drawGib(Ctx c, Proj pr)
    {
        pr.spin = pr.spin + 0.3;
        c.save(); c.translate(pr.x, pr.y); c.rotate(pr.spin);
        c.fillStyle = "#9dbb62"; c.strokeStyle = "rgba(20,30,20,0.6)"; c.lineWidth = 1.5;
        roundRect(c, -5, -3.5, 10, 7, 2); c.fill(); c.stroke();
        c.fillStyle = "#7a2f24"; c.fillRect(-5, -3.5, 3, 7);
        c.restore();
    }
    public static void drawCrawler(Ctx c, Ent e)
    {
        double s = Js.Or(e.scale, 0.84), a = Math.Atan2(Js.Or(e.face.y, 0.001), Js.Or(e.face.x, 0.001));
        double pull = Math.Sin(e.walk) * (e.moving ? 1 : 0.2);
        Col T(Col col) => e.flash > 0 ? mixc(col, "#ffffff", Math.Min(1, e.flash * 0.85)) : col;
        c.save(); c.translate(e.x, e.y - 4 * s);
        c.fillStyle = "rgba(15,30,20,0.3)"; c.beginPath(); c.ellipse(0, 6 * s, 17 * s, 6 * s, 0, 0, TAU); c.fill();
        if (e.elite) { c.strokeStyle = "rgba(255,185,0,0.75)"; c.lineWidth = 2.5; c.beginPath(); c.ellipse(0, 6 * s, 19 * s, 7.5 * s, 0, 0, TAU); c.stroke(); }
        if (e.magic) { c.setLineDash(new double[] { 5, 4 }); c.strokeStyle = MAGIC_RING; c.lineWidth = 2.5; c.beginPath(); c.ellipse(0, 6 * s, 19 * s, 7.5 * s, 0, 0, TAU); c.stroke(); c.setLineDash(Array.Empty<double>()); }
        c.rotate(a);
        const string OL = "rgba(20,30,40,0.55)";
        c.lineWidth = 2; c.strokeStyle = OL;
        c.fillStyle = T("#7a2f24");
        c.beginPath(); c.moveTo(-12 * s, -6 * s); c.lineTo(-16 * s, -3 * s); c.lineTo(-13 * s, 0); c.lineTo(-17 * s, 3 * s); c.lineTo(-12 * s, 6 * s); c.closePath(); c.fill();
        c.fillStyle = T(e.col.torso);
        roundRect(c, -13 * s, -7 * s, 20 * s, 14 * s, 3 * s); c.fill(); c.stroke();
        c.fillStyle = T(sh(e.col.head, -0.12));
        c.beginPath(); c.moveTo(-6 * s, -3 * s); c.lineTo(-1 * s, -1 * s); c.lineTo(-5 * s, 3 * s); c.closePath(); c.fill();
        c.strokeStyle = T(e.col.torso); c.lineWidth = 5 * s; c.lineCap = "round";
        double r1 = 8 + 7 * pull, r2 = 8 - 7 * pull;
        c.beginPath(); c.moveTo(4 * s, -6 * s); c.lineTo((4 + r1) * s, -10 * s); c.stroke();
        c.beginPath(); c.moveTo(4 * s, 6 * s); c.lineTo((4 + r2) * s, 10 * s); c.stroke();
        c.fillStyle = T(e.col.hands); c.strokeStyle = OL; c.lineWidth = 1.5;
        c.beginPath(); c.arc((4 + r1) * s, -10 * s, 3.2 * s, 0, TAU); c.fill(); c.stroke();
        c.beginPath(); c.arc((4 + r2) * s, 10 * s, 3.2 * s, 0, TAU); c.fill(); c.stroke();
        c.fillStyle = T(e.col.head); c.lineWidth = 2;
        c.beginPath(); c.arc(11 * s, 0, 7 * s, 0, TAU); c.fill(); c.stroke();
        c.fillStyle = "#1a1a14";
        c.beginPath(); c.arc(14 * s, -2.4 * s, 1.3 * s, 0, TAU); c.arc(14 * s, 2.4 * s, 1.3 * s, 0, TAU); c.fill();
        c.restore();
    }
}
