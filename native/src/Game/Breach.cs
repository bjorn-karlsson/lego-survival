namespace Brickblade;

/* ================================== BREACH (JS 9267–9827) ==================================
   A hand of void bricks; walk into it and a circle of another layer of the same
   ground grows out of it, full of its lord's monsters that exist only inside it.
   On a clock that kills buy back; pays in splinters that fuse into the lord's ring. */
public sealed class BreachLordDef { public string name, title, el, col, dim, ring; }
public sealed class BreachRec { public double x, y, d; public string type; public bool magic; }
public sealed partial class Breach
{
    public string lord, state;
    public double delay, rClose, age, t, kills, spawnT, closeT, closeMax, spl, depth, rEnd, hold, pulled;
    public bool lordOut, lord2Out;
    public List<BreachRec> layer; public int li;
    public Ent hand, lord2;
}
public sealed partial class Ent { public bool breachLord, breachLord2, pulled; }
public sealed partial class Prop { public string hoard; }

public static partial class G
{
    public const double BREACH_FROM = 7;
    public static readonly Tbl BREACH_HAND_DELAY = new double[] { 3, 8 };
    public static readonly Tbl BREACH_HAND_DIST = new double[] { 380, 760 };
    public const double BREACH_TOUCH_R = 44;
    public const double BREACH_R0 = 60;
    public const double BREACH_SPD = 110;
    public const double BREACH_TIME = 8;
    /* WHAT A KILL BUYS, and how that shrinks */
    public const double BREACH_KILL_T0 = 0.7, BREACH_KILL_REF = 250, BREACH_KILL_EXP = 1.25;
    public const double BREACH_T_MAX = 12;
    /* THE OTHER LAYER: packs of three to six, one kind to a pack */
    public const double BREACH_LAYER_MIN = 600, BREACH_LAYER_STEP = 40, BREACH_LAYER_MAX = 2000;
    public const double BREACH_LAYER_EXP = 0.8;
    public static readonly Tbl BREACH_PACK = new double[] { 3, 6 };
    public const double BREACH_CORE = 0.3, BREACH_OUTER_EXP = 0.36;
    /* revealed and standing at once */
    public const double BREACH_ALIVE_MAX = 90, BREACH_ALIVE_TOP = 180, BREACH_ALIVE_PER = 50;
    public static double breachAliveMax(double r) => Math.Min(BREACH_ALIVE_TOP, BREACH_ALIVE_MAX + Math.Floor(r / BREACH_ALIVE_PER));
    /* THE DEEPER YOU GO */
    public const double BREACH_DEPTH_HP = 0.12, BREACH_DEPTH_HP_MAX = 3, BREACH_DEPTH_MAGIC = 0.015, BREACH_MAGIC_MAX = 0.3;
    public const double BREACH_LAYER_FROM = 6;
    public static double breachLayerSize(double n) =>
        clamp(BREACH_LAYER_MIN + (n - BREACH_LAYER_FROM) * BREACH_LAYER_STEP, BREACH_LAYER_MIN, BREACH_LAYER_MAX);
    public const double BREACH_HP = 0.7, BREACH_XP = 0.6;
    public const double BREACH_MAGIC = 0.08;
    public const double BREACH_LORD_AT = 34;
    public const double BREACH_LORD_HP = 4;
    public const double BREACH_LORD_HOLD = 20;
    /* IT CLOSES LIKE PoE's */
    public const double BREACH_CLOSE_T = 1.2, BREACH_CLOSE_SPD = 1100, BREACH_CLOSE_MAX = 4.5;
    public const double BREACH_STONE = 60;
    /* RANKS IV AND V ARE BLESSINGS, and cost more */
    public static readonly Tbl BREACH_STONE_COST = new double[] { 60, 60, 60, 100, 150 };
    public static double breachStoneCost(double rank) => BREACH_STONE_COST[(int)Math.Min(rank, BREACH_STONE_COST.Length - 1)];
    public const double SPLINTER_MAGIC = 3, SPLINTER_LORD = 15;
    public const double LORD_RING_MAX = 5, LORD_RING_PLAIN = 3;
    /* THE BREACHLORD */
    public const double BREACH_LORD2_AT = 110, BREACH_LORD2_HP = 14, BREACH_LORD2_HOLD = 35, SPLINTER_LORD2 = 40;
    /* A BREACH PAYS IN RINGS AND SPLINTERS, NOT IN BRICKS */
    public const string BREACH_CHEST_MAX = "rare";
    public static readonly (double need, string rar)[] BREACH_HOARD = { (25, "common"), (60, "uncommon"), (110, "rare") };
    public static string breachChestRar(string r) => Array.IndexOf(RAR_ORDER, r) > Array.IndexOf(RAR_ORDER, BREACH_CHEST_MAX) ? BREACH_CHEST_MAX : r;
    public static readonly JsMap<string, BreachLordDef> BREACH_LORDS = new()
    {
        ["xoph"] = new BreachLordDef { name = "XOPH", title = "Dark Embers", el = "fire", col = "#ff6a2a", dim = "#6e1c0a", ring = "Xoph's Ring" },
        ["tul"] = new BreachLordDef { name = "TUL", title = "Creeping Avalanche", el = "frost", col = "#8fd8ff", dim = "#1d4a66", ring = "Tul's Ring" },
        ["esh"] = new BreachLordDef { name = "ESH", title = "Forked Thought", el = "shock", col = "#ffe14a", dim = "#6b5a0a", ring = "Esh's Ring" },
        ["uul"] = new BreachLordDef { name = "UUL-NETOL", title = "Unburdened Flesh", el = "phys", col = "#e8c9a0", dim = "#5a4030", ring = "Uul-Netol's Ring" },
        ["chayula"] = new BreachLordDef { name = "CHAYULA", title = "Who Dreamt", el = "chaos", col = "#b77cff", dim = "#3a1a5e", ring = "Chayula's Ring" },
    };
    public static readonly List<string> BREACH_LORD_KEYS = BREACH_LORDS.Keys.ToList();
    /* ONE RANK OF A RING -- AND ONLY INSIDE A BREACH */
    public static readonly (double more, double less, double lessCap) RING_VS = (0.08, 0.05, 0.6);
    public static readonly string[] RING_ROMAN = { "", "I", "II", "III", "IV", "V" };
    public static string ringText(string k, double n)
    {
        var L = BREACH_LORDS[k]; string pc(double v) => Js.Num(Math.Round(v * n * 100)) + "%";
        return "against breach monsters only: " + pc(RING_VS.more) + " more damage dealt, " + pc(RING_VS.less) + " less taken " +
            "(twice that against " + L.name + "'s own)";
    }
    /* the ranks that count against a body of lord `k`'s breach */
    public static double ringRanksVs(string k)
    {
        var R = player?.rings ?? new NumMap();
        double n = 0; foreach (var (_, v) in R) n += v;
        return n + (k != null ? R[k] : 0);
    }
    public static double ringMoreVs(Ent e) => (e != null && e.breach != null) ? 1 + RING_VS.more * ringRanksVs(e.breach) : 1;
    public static double ringLessFrom(Obj src)
    {
        /* the blow's owner: the body itself, or whoever threw the thing that landed */
        Ent who = src is Ent se && se.breach != null ? se : (src is Proj pr && pr.src != null && pr.src.breach != null ? pr.src : null);
        return who != null ? 1 - Math.Min(RING_VS.lessCap, RING_VS.less * ringRanksVs(who.breach)) : 1;
    }
    /* MORE THAN ONE, LATE IN A RUN: every per-breach function runs with `breach` set to the one it is working on */
    public static List<Breach> breachAllNow = null;
    public static List<Breach> allBreaches()
    {
        if (breachAllNow != null) return breachAllNow.ToList();
        var l = new List<Breach>(); if (breach != null) l.Add(breach); l.AddRange(breachExtra); return l;
    }
    public static T withBreach<T>(Breach B, Func<T> fn) { var keep = breach; breach = B; try { return fn(); } finally { breach = keep; } }
    public static void withBreach(Breach B, Action fn) { var keep = breach; breach = B; try { fn(); } finally { breach = keep; } }
    public static Breach breachOf(Ent e) => e != null && e.breach != null ? (e.breachB ?? breach) : null;
    public static bool breachOpenNow() => allBreaches().Any(B => B.state == "open" || B.state == "closing");
    public static Breach breachNewAt(string lord) => new Breach
    {
        lord = lord, state = "wait", delay = rnd(BREACH_HAND_DELAY[0], BREACH_HAND_DELAY[1]),
        x = 0, y = 0, r = 0, rClose = 0, age = 0, t = 0, kills = 0, spawnT = 0, closeT = 0, lordOut = false, spl = 0
    };
    /* HOW MANY BREACHES THIS WAVE: `k`, which the league roll decides */
    public static void breachRoll(double n, double k = 1)
    {
        breach = null; breachExtra = new List<Breach>();
        if (sandbox || n < BREACH_FROM || n % 5 == 0 || k <= 0) return;
        string lord = BREACH_LORD_KEYS[(int)(Js.R() * BREACH_LORD_KEYS.Count)];
        breach = breachNewAt(lord);
        for (int i = 1; i < k; i++)
        {
            var free = BREACH_LORD_KEYS.Where(q => !allBreaches().Any(B => B.lord == q)).ToList();
            if (free.Count == 0) break;
            var B = breachNewAt(free[(int)(Js.R() * free.Count)]);
            B.delay += rnd(3, 9) * i;
            breachExtra.Add(B);
        }
    }
    /* somewhere off to one side: far enough to be a decision, near enough to see */
    public static Pt breachSpot()
    {
        double lo = W.wall + 120, hiX = W.w - W.wall - 120, hiY = W.h - W.wall - 120;
        for (int i = 0; i < 60; i++)
        {
            double a = rnd(TAU), rr = rnd(BREACH_HAND_DIST[0], BREACH_HAND_DIST[1]);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            if (x < lo || x > hiX || y < lo || y > hiY) continue;
            bool bad = false; var pt = new Pt(x, y);
            foreach (var so in solids) if (dist(pt, so) < so.r + 40) { bad = true; break; }
            foreach (var w in waters) if (x > w.x - 40 && x < w.x + w.w + 40 && y > w.y - 40 && y < w.y + w.h + 40) bad = true;
            /* not on top of another breach's hand */
            foreach (var o in allBreaches()) if (o != breach && o.state != "wait" && hypot(o.x - x, o.y - y) < 600) bad = true;
            if (!bad) return pt;
        }
        return randomSpawnPos();
    }
    public static void breachOpen()
    {
        var B = breach; var L = BREACH_LORDS[B.lord];
        B.state = "open"; B.age = 0; B.t = BREACH_TIME; B.r = BREACH_R0; B.kills = 0;
        B.depth = player.breachDepth; player.breachDepth = B.depth + 1;
        breachLayer(B);
        showBanner("BREACH", L.name + " — " + L.title.ToUpperInvariant(), L.col, 1.8, 50);
        logEvent("   THE BREACH OPENS — " + L.name + ", " + L.title, L.col);
        addRing(B.x, B.y, 16, 520, 220, L.col, 8, 0.6);
        addShake(0.4); SFX.roar();
    }
    /* WHAT COMES THROUGH: walkers and runners, never a caster or a siege body */
    public static List<string> breachTypes(double n)
    {
        var t = new List<string> { "imp", "skeleton", "stalker" };
        if (n >= 10) t.AddRange(new[] { "lancer", "knight" });
        if (n >= 16) t.Add("brute");
        if (n >= 24) t.Add("superimp");
        return t;
    }
    public static Pt breachNear(Breach B)
    {
        for (int i = 0; i < 20; i++)
        {
            double a = rnd(TAU), rr = rnd(200, 320);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            double dx = x - B.x, dy = y - B.y, d = hypot(dx, dy), lim = Math.Max(0, B.r - 40);
            if (d > lim && d > 0) { x = B.x + dx / d * lim; y = B.y + dy / d * lim; }
            x = clamp(x, W.wall + 50, W.w - W.wall - 50); y = clamp(y, W.wall + 50, W.h - W.wall - 50);
            if (breachFloorOk(x, y)) return new Pt(x, y);
        }
        return new Pt(B.x, B.y);
    }
    public static void breachify(Ent e, string lord)
    {
        var L = BREACH_LORDS[lord];
        e.breach = lord; e.breachB = breach;
        /* the league tree can drive a breach's monsters deeper */
        double lvUp = leagueStat("breachMobLvl");
        if (lvUp > 0) { double k = mobRankMul(e.mlvl + lvUp) / mobRankMul(e.mlvl); e.mlvl = e.mlvl + lvUp; e.maxHp *= k; e.hp = e.maxHp; e.dmg *= k; }
        double deep = Math.Min(BREACH_DEPTH_HP_MAX, 1 + BREACH_DEPTH_HP * (breach != null ? breach.depth : 0));
        e.hp = e.maxHp = Math.Max(1, Math.Ceiling(e.maxHp * BREACH_HP * deep));
        e.xp *= BREACH_XP; e.studsDrop = new DropRange { a = 0, b = 0 };
        e.col = new FigCols { head = L.col, torso = L.dim, legs = "#17121f", hands = L.col, hair = L.dim };
        if (L.el == "phys") e.dmg = Math.Ceiling(e.dmg * 1.15);   /* the flesh hits harder instead */
        else e.touchElem = L.el;
        /* it STEPS THROUGH, half made already */
        e.buildT = Math.Max(e.buildT, 0.5);
    }
    /* THE LAYER: laid across the whole floor the moment the breach opens, sorted by distance */
    public static bool breachFloorOk(double x, double y)
    {
        foreach (var so in solids) if (hypot(x - so.x, y - so.y) < so.r + 16) return false;
        foreach (var w in waters) if (x > w.x - 24 && x < w.x + w.w + 24 && y > w.y - 24 && y < w.y + w.h + 24) return false;
        return true;
    }
    public static void breachLayer(Breach B)
    {
        double lo = W.wall + 60, hiX = W.w - W.wall - 60, hiY = W.h - W.wall - 60;
        var types = breachTypes(wave.n); double want = breachLayerSize(wave.n);
        /* the circle's last size: the farthest corner of the floor from the hand */
        B.rEnd = Math.Max(Math.Max(hypot(W.wall - B.x, W.wall - B.y), hypot(W.w - W.wall - B.x, W.wall - B.y)),
                          Math.Max(hypot(W.wall - B.x, W.h - W.wall - B.y), hypot(W.w - W.wall - B.x, W.h - W.wall - B.y)));
        var L = new List<BreachRec>();
        for (int tries = 0; L.Count < want && tries < want * 4; tries++)
        {
            bool core = Js.R() < BREACH_CORE;
            double a0 = rnd(TAU), d0 = B.rEnd * Math.Pow(Js.R(), core ? BREACH_LAYER_EXP : BREACH_OUTER_EXP);
            double cx = B.x + Math.Cos(a0) * d0, cy = B.y + Math.Sin(a0) * d0;
            if (cx < lo || cx > hiX || cy < lo || cy > hiY) continue;  /* that part of the circle is off the floor */
            if (d0 < 150) continue;                                    /* not on top of the hand */
            double n = Math.Min(irnd(BREACH_PACK[0], BREACH_PACK[1]), want - L.Count);
            string type = types[(int)(Js.R() * types.Count)];
            bool magic = Js.R() < Math.Min(BREACH_MAGIC_MAX, BREACH_MAGIC + BREACH_DEPTH_MAGIC * B.depth);
            for (int k = 0; k < n; k++)
            {
                double a = rnd(TAU), rr = rnd(0, 26 + n * 9);
                double x = clamp(cx + Math.Cos(a) * rr, lo, hiX), y = clamp(cy + Math.Sin(a) * rr, lo, hiY);
                if (!breachFloorOk(x, y)) continue;
                L.Add(new BreachRec { x = x, y = y, type = type, magic = magic, d = hypot(x - B.x, y - B.y) });
            }
        }
        L.SortJs((a, z) => a.d - z.d);
        B.layer = L; B.li = 0;
    }
    public static void breachReveal(Breach B, BreachRec rec)
    {
        int n0 = enemies.Count;
        spawnEnemy(rec.type, rec.x, rec.y, false, rec.magic);
        if (enemies.Count > n0) breachify(enemies[^1], B.lord);
    }
    public static double breachKillT(double r) => BREACH_KILL_T0 * Math.Pow(BREACH_KILL_REF / Math.Max(BREACH_KILL_REF, r), BREACH_KILL_EXP);
    /* IS IT HERE? A breach body exists only inside its own circle. */
    public static bool breachSees(Ent e)
    {
        var B = breachOf(e);
        if (e.breach == null) return true;
        if (B == null || !(B.state == "open" || B.state == "closing")) return false;
        return hypot(e.x - B.x, e.y - B.y) <= B.r + Js.Or(e.r, 12);
    }
    public static void breachLord(Breach B)
    {
        var L = BREACH_LORDS[B.lord];
        B.lordOut = true;
        /* it steps through NEAR YOU */
        var at = breachNear(B); int n0 = enemies.Count;
        spawnEnemy(wave.n >= 14 ? "jugg" : "brute", at.x, at.y, true, false);
        if (enemies.Count == n0) return;
        var e = enemies[^1];
        breachify(e, B.lord);
        e.breachLord = true;
        e.hp = e.maxHp = Math.Ceiling(e.maxHp * BREACH_LORD_HP);
        e.scale *= 1.2; e.r *= 1.12; e.xp *= 6;
        B.hand = e; B.hold = BREACH_LORD_HOLD;
        showBanner("THE HAND OF " + L.name, "it carries " + L.ring, L.col, 2.0, 46);
        logEvent("   THE HAND OF " + L.name + " steps through", L.col);
        addRing(at.x, at.y, 14, 420, 160, L.col, 7, 0.5);
        SFX.roar(); addShake(0.35);
    }
    public static void breachLord2(Breach B)
    {
        var L = BREACH_LORDS[B.lord];
        B.lord2Out = true;
        var at = breachNear(B); int n0 = enemies.Count;
        spawnEnemy("revenant", at.x, at.y, true, false);
        if (enemies.Count == n0) return;
        var e = enemies[^1];
        breachify(e, B.lord);
        e.breachLord = true; e.breachLord2 = true;
        e.hp = e.maxHp = Math.Ceiling(e.maxHp * BREACH_LORD2_HP);
        e.scale *= 1.45; e.r *= 1.3; e.xp *= 20; e.dmg = Math.Ceiling(e.dmg * 1.5);
        B.lord2 = e; B.hold = Math.Max(B.hold, BREACH_LORD2_HOLD);
        showBanner(L.name + " COMES THROUGH", L.title.ToUpperInvariant() + " — THE BREACHLORD", L.col, 2.6, 52);
        logEvent("   " + L.name + ", " + L.title + ", steps through the breach itself", L.col);
        addRing(at.x, at.y, 20, 520, 260, L.col, 9, 0.7);
        SFX.roar(); addShake(0.6);
    }
    public static void breachClose(Breach B)
    {
        var L = BREACH_LORDS[B.lord];
        B.state = "closing"; B.rClose = B.r;
        B.closeT = B.closeMax = clamp(B.r / BREACH_CLOSE_SPD, BREACH_CLOSE_T, BREACH_CLOSE_MAX);
        B.pulled = 0;
        logEvent("   the breach closes — " + Js.Num(B.kills) + " killed inside, " + Js.Num(B.spl) + " splinters", L.col);
    }
    /* THE HOARD: left at the hand once the ring has fallen all the way in */
    public static Prop breachHoard(Breach B)
    {
        double score = B.kills + (B.hand != null && B.hand.dead ? 60 : 0);
        string rar = null;
        foreach (var (need, r) in BREACH_HOARD) if (score >= need) rar = r;
        if (B.lord2 != null && B.lord2.dead) rar = BREACH_CHEST_MAX;     /* the lord's own hoard */
        if (rar == null) return null;
        rar = breachChestRar(rar);
        var L = BREACH_LORDS[B.lord];
        var ch = shoveToFloor(new Prop { type = "chest", x = B.x, y = B.y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16, rar = rar, hoard = B.lord }, 20);
        props.Add(ch);
        showBanner("BREACH HOARD", (RARITY.GetValueOrDefault(rar) ?? RARITY["common"]).name + " — " + L.name, L.col, 1.8, 40);
        logEvent("   " + L.name + "'s hoard is left at the hand: " + rar, L.col);
        addRing(ch.x, ch.y, 10, 380, 120, L.col, 7, 0.5);
        return ch;
    }
    public static void breachTickOne(double dt)
    {
        var B = breach;
        if (B == null || player == null) return;
        B.age += dt;
        if (B.state == "wait" || B.state == "hand")
        {
            /* a wave that ends with the hand untouched takes the hand with it */
            if (wave.state == "cleared" && (B.state == "wait" || !wave.hold))
            {
                if (B.state == "hand") logEvent("   the breach hand sinks back, unopened", BREACH_LORDS[B.lord].col);
                breach = null; return;
            }
            if (B.state == "wait")
            {
                if (wave.state != "fight") return;
                B.delay -= dt;
                if (B.delay > 0) return;
                var at = breachSpot();
                B.x = at.x; B.y = at.y; B.state = "hand"; B.age = 0;
                var L = BREACH_LORDS[B.lord];
                logEvent("   A BREACH HAND reaches through — " + L.name + ", " + L.title, L.col);
                addDmg(B.x, B.y - 64, "BREACH", L.col, 20);
                return;
            }
            if (!player.dead && hypot(player.x - B.x, player.y - B.y) < BREACH_TOUCH_R + 12) breachOpen();
            return;
        }
        if (B.state == "open")
        {
            B.r = Math.Min(Js.Or(B.rEnd, BREACH_R0), B.r + BREACH_SPD * dt);
            B.t -= dt;
            /* the circle reaches them, and they are there */
            int alive = 0; foreach (var e in enemies) if (breachOf(e) == B && !e.dead) alive++;
            double cap = breachAliveMax(B.r);
            while (B.layer != null && B.li < B.layer.Count && B.layer[B.li].d <= B.r && alive < cap)
            {
                breachReveal(B, B.layer[B.li++]); alive++;
            }
            breachHold(B);
            if (!B.lordOut && B.kills >= BREACH_LORD_AT) breachLord(B);
            if (!B.lord2Out && B.lordOut && B.hand != null && B.hand.dead && B.kills >= BREACH_LORD2_AT &&
                player.rings[B.lord] >= LORD_RING_PLAIN) breachLord2(B);
            /* THE HAND HOLDS IT OPEN */
            var holder = (B.lord2 != null && !B.lord2.dead) ? B.lord2 : (B.hand != null && !B.hand.dead) ? B.hand : null;
            if (holder != null && B.hold > 0) { B.hold -= dt; B.t = Math.Max(B.t, 1); }
            if (B.t <= 0) breachClose(B);
            return;
        }
        if (B.state == "closing")
        {
            B.closeT -= dt;
            B.r = B.rClose * clamp(B.closeT / Js.Or(B.closeMax, BREACH_CLOSE_T), 0, 1);
            /* the edge passes over them, and they go back through -- paying nothing */
            var L = BREACH_LORDS[B.lord];
            foreach (var e in enemies)
            {
                if (breachOf(e) != B || e.dead) continue;
                if (hypot(e.x - B.x, e.y - B.y) <= B.r + Js.Or(e.r, 12) * 0.5) continue;
                e.dead = true; e.pulled = true; B.pulled = B.pulled + 1;
                addSparks(e.x, e.y - 14, 5, L.col);
            }
            if (B.closeT <= 0)
            {
                if (Js.T(B.pulled)) logEvent("   " + Js.Num(B.pulled) + " pulled back through as the ring passed", L.col);
                breachHoard(B);
                breach = null;
            }
        }
    }
    public static void updateBreach(double dt)
    {
        var l = new List<Breach>(); if (breach != null) l.Add(breach); l.AddRange(breachExtra);
        breachAllNow = l;
        try { updateBreachAll(dt); } finally { breachAllNow = null; }
    }
    public static void updateBreachAll(double dt)
    {
        if (breach != null) breachTickOne(dt);
        breachExtra = breachExtra.Select(B => { var keep = breach; breach = B; try { breachTickOne(dt); return breach; } finally { breach = keep; } })
                                 .Where(b => b != null).ToList();
        /* the first of the others takes the lead once the lead is gone */
        if (breach == null && breachExtra.Count > 0) breach = breachExtra.Shift();
    }
    /* A BREACH BODY CANNOT LEAVE ITS CIRCLE */
    public static void breachHold(Breach B)
    {
        foreach (var e in enemies)
        {
            if (breachOf(e) != B || e.dead) continue;
            double dx = e.x - B.x, dy = e.y - B.y, d = hypot(dx, dy), lim = Math.Max(0, B.r - Js.Or(e.r, 12));
            if (d > lim && d > 0) { e.x = B.x + dx / d * lim; e.y = B.y + dy / d * lim; }
        }
    }
    public static void breachOnKill(Ent e)
    {
        var B = breachOf(e);
        if (B == null || B.state != "open") return;
        if (hypot(e.x - B.x, e.y - B.y) > B.r + Js.Or(e.r, 12)) return;
        B.kills++;
        B.t = Math.Min(BREACH_T_MAX, B.t + breachKillT(B.r));
    }
    public static void breachSpoils(Ent e)
    {
        var p = player; string k = e.breach; var L = k != null ? BREACH_LORDS.GetValueOrDefault(k) : null;
        if (L == null) return;
        double n = e.breachLord2 ? SPLINTER_LORD2 : e.breachLord ? SPLINTER_LORD : e.magic ? SPLINTER_MAGIC : 1;
        p.splinters[k] = p.splinters[k] + n;
        var B = breachOf(e);
        if (B != null && B.lord == k) B.spl += n;
        addSparks(e.x, e.y - 18, (int)(2 + n), L.col);
        if (e.breachLord2)
        {
            addDmg(e.x, e.y - 90, L.name + " FALLS", L.col, 22);
            showBanner(L.name + " FALLS", "the hoard will be " + RARITY[BREACH_CHEST_MAX].name.ToLowerInvariant(), L.col, 2.2, 48);
            ringGain(k, "lord");
        }
        else if (e.breachLord)
        {
            addDmg(e.x, e.y - 70, L.ring.ToUpperInvariant() + "!", L.col, 20);
            ringGain(k, "hand");
        }
        for (double cost = breachStoneCost(p.rings[k]); p.splinters[k] >= cost; cost = breachStoneCost(p.rings[k]))
        {
            p.splinters[k] -= cost;
            logEvent("   " + Js.Num(cost) + " " + L.name + " splinters fuse into a BREACHSTONE", L.col);
            ringGain(k, "stone");
        }
    }
    public static void ringGain(string k, string why)
    {
        var p = player; var L = BREACH_LORDS[k];
        double have = p.rings[k];
        if (have >= LORD_RING_MAX)
        {
            /* a ring that is fully blessed turns every further stone into a hoard */
            double cx = p.x + rnd(-50, 50), cy = p.y + rnd(30, 60);
            props.Add(shoveToFloor(new Prop { type = "chest", x = cx, y = cy, flash = 0, seed = rnd(1000),
                dead = false, hp = 3, r = 16, rar = breachChestRar(why == "lord" ? "legendary" : "uncommon"), hoard = k }, 20));
            showBanner(L.ring.ToUpperInvariant() + " IS WHOLE", "a BLESSED HOARD instead", L.col, 1.6, 40);
            logEvent("   " + L.ring + " is fully blessed — a hoard instead", L.col);
            return;
        }
        p.rings[k] = have + 1;
        int h1 = (int)have + 1;
        showBanner(L.ring.ToUpperInvariant() + (Js.T(have) ? (have + 1 > LORD_RING_PLAIN ? " — BLESSED " : " — RANK ") + RING_ROMAN[h1] : ""),
            ringText(k, 1) + (Js.T(have) ? " more" : ""), L.col, 2.4, 44);
        logEvent("   " + L.ring + " " + RING_ROMAN[h1] + (why == "hand" ? " — from the Hand" : why == "lord" ? " — from " + L.name + " itself" : " — from a breachstone") +
                 ": " + ringText(k, have + 1), L.col);
        SFX.levelup();
    }
    /* ---- drawn ---- */
    public static void drawBreachHand(Ctx c, double x, double y, Col col, double pulse, bool open, double s)
    {
        c.save(); c.translate(x, y); c.scale(Js.Or(s, 1), Js.Or(s, 1));
        c.fillStyle = col.WithAlpha(0.14 + 0.12 * pulse);
        c.beginPath(); c.arc(0, -26, 36, 0, TAU); c.fill();
        double spread = open ? 0.62 : 0.2;
        for (int i = 0; i < 4; i++)
        {
            c.save(); c.rotate((i - 1.5) * spread);
            c.fillStyle = "#241a36"; c.strokeStyle = "rgba(8,4,16,0.85)"; c.lineWidth = 2;
            roundRect(c, -4.5, -46 + Math.Abs(i - 1.5) * 5, 9, 42 - Math.Abs(i - 1.5) * 5, 4); c.fill(); c.stroke();
            c.fillStyle = col;
            double ty = -46 + Math.Abs(i - 1.5) * 5;
            c.beginPath(); c.moveTo(-4, ty + 1); c.lineTo(4, ty + 1); c.lineTo(0, ty - 9); c.closePath(); c.fill();
            c.restore();
        }
        c.save(); c.rotate(open ? -1.1 : -0.8);
        c.fillStyle = "#241a36"; c.strokeStyle = "rgba(8,4,16,0.85)"; c.lineWidth = 2;
        roundRect(c, -4.5, -30, 9, 26, 4); c.fill(); c.stroke();
        c.fillStyle = col; c.beginPath(); c.moveTo(-4, -29); c.lineTo(4, -29); c.lineTo(0, -37); c.closePath(); c.fill();
        c.restore();
        c.fillStyle = "#2e2244"; c.strokeStyle = "rgba(8,4,16,0.85)"; c.lineWidth = 2;
        roundRect(c, -17, -12, 34, 15, 5); c.fill(); c.stroke();
        drawStudG(c, -8, -5, 2.6, col); drawStudG(c, 0, -5, 2.6, col); drawStudG(c, 8, -5, 2.6, col);
        c.restore();
    }
    public static void drawBreach()
    {
        foreach (var B in allBreaches()) withBreach(B, drawBreachOne);   /* allBreaches() is read once, before any swap */
        var c = ctx;
        /* a stain of its lord under every breach body */
        foreach (var e in enemies)
        {
            if (e.breach == null || e.dead || !onScreen(e, 40) || !breachSees(e)) continue;
            Col lc = BREACH_LORDS[e.breach].col;
            c.fillStyle = lc.WithAlpha(e.breachLord ? 0.38 : 0.24);
            c.beginPath(); c.ellipse(e.x, e.y + 2, Js.Or(e.r, 12) * 1.5, Js.Or(e.r, 12) * 0.62, 0, 0, TAU); c.fill();
        }
    }
    public static void drawBreachOne()
    {
        var B = breach; var c = ctx;
        if (B != null && (B.state == "hand" || B.state == "open" || B.state == "closing"))
        {
            var L = BREACH_LORDS[B.lord]; Col lc = L.col;
            double pulse = 0.5 + 0.5 * Math.Sin(vtime * 4);
            if (B.state == "hand")
            {
                c.fillStyle = "rgba(18,8,32,0.62)";
                c.beginPath(); c.ellipse(B.x, B.y, 44, 19, 0, 0, TAU); c.fill();
                c.strokeStyle = lc.WithAlpha(0.45 + 0.4 * pulse); c.lineWidth = 3;
                c.beginPath(); c.ellipse(B.x, B.y, 44 + 8 * pulse, 19 + 3.5 * pulse, 0, 0, TAU); c.stroke();
                drawBreachHand(c, B.x, B.y, lc, pulse, false, 1);
            }
            else
            {
                double R = Math.Max(2, B.r);
                var gr = c.createRadialGradient(B.x, B.y, R * 0.08, B.x, B.y, R);
                gr.addColorStop(0, "rgba(12,5,24,0.46)");
                gr.addColorStop(0.78, "rgba(26,10,46,0.30)");
                gr.addColorStop(1, lc.WithAlpha(0.24));
                c.fillStyle = gr;
                c.beginPath(); c.arc(B.x, B.y, R, 0, TAU); c.fill();
                c.strokeStyle = lc.WithAlpha(0.85); c.lineWidth = 4;
                c.beginPath(); c.arc(B.x, B.y, R, 0, TAU); c.stroke();
                c.setLineDash(new double[] { 18, 14 }); c.lineDashOffset = -vtime * 40;
                c.strokeStyle = lc.WithAlpha(0.4); c.lineWidth = 2;
                c.beginPath(); c.arc(B.x, B.y, R + 9, 0, TAU); c.stroke();
                c.setLineDash(new double[0]); c.lineDashOffset = 0;
                /* the void drifting in it */
                c.fillStyle = lc.WithAlpha(0.5);
                for (int i = 0; i < 30; i++)
                {
                    double a = i * 2.399 + vtime * 0.25 * (i % 2 != 0 ? 1 : -1);
                    double rr = R * (((i * 0.137) + vtime * 0.04) % 1);
                    c.beginPath(); c.arc(B.x + Math.Cos(a) * rr, B.y + Math.Sin(a) * rr, 1.6 + (i % 3), 0, TAU); c.fill();
                }
                c.globalAlpha = B.state == "closing" ? clamp(B.closeT / BREACH_CLOSE_T, 0, 1) : 0.85;
                drawBreachHand(c, B.x, B.y, lc, pulse, true, 1.15);
                c.globalAlpha = 1;
            }
        }
    }
    /* the clock and the count, under the wave block, and the splinters in the corner */
    public static void drawBreachHud()
    {
        var p = player;
        var openB = allBreaches().Where(o => o.state == "open").ToList();
        var B = openB.FirstOrDefault();
        if (B != null && B.state == "open")
        {
            var L = BREACH_LORDS[B.lord];
            double bw = Math.Min(360, cw - 80), bx = cw / 2 - bw / 2, by = hudWaveBottom() + 8;
            ctx.font = "700 12px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
            ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
            bool held = B.hand != null && !B.hand.dead && B.hold > 0;
            bool lordUp = B.lord2 != null && !B.lord2.dead;
            bool lord2Due = p.rings[B.lord] >= LORD_RING_PLAIN && !B.lord2Out;
            string head = "BREACH" + (openB.Count > 1 ? " ×" + openB.Count : "") + " · " + L.name + (Js.T(B.depth) ? " · DEPTH " + Js.Num(B.depth + 1) : "") + " · " +
                (lordUp ? L.name + " HOLDS IT OPEN" : held ? "THE HAND HOLDS IT OPEN"
                : B.lordOut && lord2Due ? Js.Num(B.kills) + " / " + Js.Num(BREACH_LORD2_AT) + " for " + L.name
                : B.lordOut ? "THE HAND IS DOWN"
                : Js.Num(B.kills) + " / " + Js.Num(BREACH_LORD_AT) + " for the Hand") + " · +" + Js.ToFixed(breachKillT(B.r), 2) + "s a kill";
            ctx.strokeText(head, cw / 2, by); ctx.fillStyle = L.col; ctx.fillText(head, cw / 2, by);
            ctx.fillStyle = "rgba(15,20,25,0.55)"; roundRect(ctx, bx, by + 10, bw, 8, 4); ctx.fill();
            ctx.fillStyle = L.col; roundRect(ctx, bx, by + 10, Math.Max(3, bw * clamp(B.t / BREACH_T_MAX, 0, 1)), 8, 4); ctx.fill();
        }
        /* splinters and rings, top right under the studs -- only the lords you have met */
        double y = 66;
        ctx.textAlign = "right"; ctx.textBaseline = "middle"; ctx.font = "700 12px Fredoka";
        foreach (var k in BREACH_LORD_KEYS)
        {
            double sp = p.splinters != null ? p.splinters[k] : 0, rk = p.rings != null ? p.rings[k] : 0;
            if (!Js.T(sp) && !Js.T(rk)) continue;
            var L = BREACH_LORDS[k];
            string txt = L.name + "  " + Js.Num(sp) + "/" + Js.Num(breachStoneCost(rk)) + (Js.T(rk) ? "  ◆" + RING_ROMAN[(int)rk] : "");
            ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
            ctx.strokeText(txt, cw - 24, y); ctx.fillStyle = L.col; ctx.fillText(txt, cw - 24, y);
            y += 17;
        }
    }
}
