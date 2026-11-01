namespace Brickblade;

/* =============================== CONSTANTS, part 2 (JS 1773–2400) =============================== */
public sealed class Weapon
{
    public string id, name, col, blurb;
    public double dmg, rate, reach, spinReach, crit, critMult, bleed, spin, kb;
    public string hold, @throw;
    public double lessTaken, suppress, leech, leechChance, acc;
    public NumMap attr;
    public bool slam, cast, shoot, summon;
    public string elem;
    public double incSpell, incElem, incAtk, res, boltSpd, boltR, evaBase, arrowSpd;
}
public sealed class Diff
{
    public string id, name, col, blurb;
    public double lvl, hp, dmg, spd, atk, count, pack, rar;
}
public sealed class Band { public double from, every; }
/* a hero's conversion table: conv[from][to] and gain[from][to] */
public sealed class Conv
{
    public Dictionary<string, NumMap> conv = new(), gain = new();
    public bool on;
}

public static partial class G
{
    public static readonly string[] ELITE_MINIONS = { "imp", "boneling", "skeleton", "archer", "cryptbat", "spitter" };
    public static string eliteMinion(string type)
    {
        var d = EDEF.GetValueOrDefault(type);
        if (d != null && d.golem != null && GOLEM_CONF.ContainsKey(d.golem)) return GOLEM_CONF[d.golem].mini;
        var ok = ELITE_MINIONS.Where(k =>
        {
            var r = ROSTER.FirstOrDefault(x => x.k == k);
            return r == null || r.at <= Math.Max(1, wave.n);
        }).ToList();
        var src = ok.Count > 0 ? ok : ELITE_MINIONS.ToList();
        return src.Count > 0 ? pick(src) : "imp";
    }
    public const string MAGIC_COL = "#3f8ae0";
    public const string MAGIC_RING = "rgba(63,138,224,0.9)";
    public const int OVERSEER_MAX_STACKS = 3;
    /* NAMED BUFFS ON A MONSTER: a map keyed by what granted them */
    public const int MOB_BUFF_UNIQUE = 9;
    public static int buffCount(Ent e) => e.buffs != null ? e.buffs.Count : 0;
    public static double buffStacksOf(Ent e, string k) => e.buffs != null ? e.buffs[k] : 0;
    public static bool addMobBuff(Ent e, string key, double max = 0)
    {
        if (e == null || e.dead) return false;
        if (e.buffs == null) e.buffs = new NumMap();
        double have = e.buffs[key];
        if (have >= Js.Or(max, 3)) return false;
        if (!Js.T(have) && buffCount(e) >= MOB_BUFF_UNIQUE) return false;
        e.buffs[key] = have + 1;
        return true;
    }
    /* THE ELEMENTAL LORDS WARD THEIR OWN */
    public static readonly Dictionary<string, string> LORD_WARD = new() { ["pyrolord"] = "fire", ["frostlord"] = "frost", ["stormlord"] = "shock" };
    public const double LORD_WARD_R = 320, LORD_WARD_STEP = 0.25;
    public const int LORD_WARD_MAX = 3;
    public static void lordWardTick(Ent e, double dt)
    {
        if (!LORD_WARD.TryGetValue(e.type ?? "", out var el)) return;
        e.wardT = e.wardT - dt;
        if (e.wardT > 0) return;
        e.wardT = 1.4;
        string key = "res_" + el;
        int n = 0;
        foreach (var e2 in enemiesNear(e.x, e.y, LORD_WARD_R + 60))
        {
            if (e2 == e || e2.dead || e2.state != "active") continue;
            if (dist(e2, e) > LORD_WARD_R) continue;
            if (e2.res == null) continue;
            if (!addMobBuff(e2, key, LORD_WARD_MAX)) continue;
            e2.res[el] = e2.res[el] + LORD_WARD_STEP;
            n++;
            addSparks(e2.x, e2.y - 18, 2, BRAND_COL.GetValueOrDefault(el) ?? "#ffd166");
        }
        if (n > 0) { e.wardLit = 0.5; addRing(e.x, e.y, 16, 260, LORD_WARD_R, BRAND_COL.GetValueOrDefault(el) ?? "#ffd166", 4, 0.45); }
    }
    public const int AIL_BREAK = 5;
    public const double BRAND_STEP = 0.11, BRAND_T = 22, RES_FLOOR = -1.0, IFRAME_T = 0.1;
    public static readonly Dictionary<string, string> BRAND_NAME = new() { ["fire"] = "BURNT", ["frost"] = "FROSTBITTEN", ["shock"] = "CONDUCTIVE" };
    public static readonly Dictionary<string, string> BRAND_COL = new() { ["fire"] = "#ff6a2a", ["frost"] = "#7fb9e0", ["shock"] = "#e0a81a" };
    public const double STUN_T = 3, BURN_DUR = 12;
    public static List<Dot> burnList() => player.burn?.list ?? new List<Dot>();
    public static int burnStacks() => burnList().Count;
    public static double burnDps() { double d = 0; foreach (var f in burnList()) d += f.dps; return d; }
    public static double burnLeft() { double d = 0; foreach (var f in burnList()) d += f.dps * f.t; return d; }
    public const double PSN_DUR = 5;
    public static List<Dot> psnList() => player.psn?.list ?? new List<Dot>();
    public static int psnStacks() => psnList().Count;
    public static double psnDps() { double d = 0; foreach (var f in psnList()) d += f.dps; return d; }
    public static double psnTime() { double t = 0; foreach (var f in psnList()) t = Math.Max(t, f.t); return t; }
    /* BLEEDING, on you */
    public const double PBLEED_T = 4, PBLEED_SHARE = 0.35, PBLEED_MOVE = 2;
    public const int PBLEED_MAX = 5;
    public static List<Dot> pbleedList() => player.pbleed?.list ?? new List<Dot>();
    public static int pbleedStacks() => pbleedList().Count;
    public static double pbleedDps() { double d = 0; foreach (var f in pbleedList()) d += f.dps; return d; }
    public static double pbleedTime() { double t = 0; foreach (var f in pbleedList()) t = Math.Max(t, f.t); return t; }
    public static void bleedPlayer(double hit)
    {
        var p = player;
        if (p == null || p.dead || !(hit > 0)) return;
        if (p.pbleed == null) p.pbleed = new DotList();
        double dps = hit * PBLEED_SHARE;
        var L = p.pbleed.list;
        if (L.Count >= PBLEED_MAX)
        {
            int w = 0;
            for (int i = 1; i < L.Count; i++) if (L[i].dps < L[w].dps) w = i;
            if (dps <= L[w].dps) return;
            L.RemoveAt(w);
        }
        L.Add(new Dot { dps = dps, t = PBLEED_T });
    }
    public const int PSN_MAX_STACKS = 8;
    public static void poisonPlayer(double dps, double dur = 0)
    {
        var p = player;
        if (p == null || p.dead || !(dps > 0)) return;
        if (p.chaosInoc) return;
        if (p.psn == null) p.psn = new DotList();
        var L = p.psn.list;
        if (L.Count >= PSN_MAX_STACKS)
        {
            int w = 0;
            for (int i = 1; i < L.Count; i++) if (L[i].dps < L[w].dps) w = i;
            if (dps <= L[w].dps) return;
            L.RemoveAt(w);
        }
        L.Add(new Dot { dps = dps, t = Js.Or(dur, PSN_DUR) });
    }
    public static double burnTime() { double t = 0; foreach (var f in burnList()) t = Math.Max(t, f.t); return t; }
    public const double RES_CAP = 0.8;
    /* THE GROUND SLAM — every ceiling it has */
    public static readonly double SLAM_ARC_BASE = Math.PI * 0.55, SLAM_ARC_MAX = Math.PI, SLAM_ARC_FULL = TAU;
    public const double SLAM_RANGE_BASE = 200, SLAM_RANGE_MAX = 430, SLAM_SPEED_BASE = 620, SLAM_SPEED_MAX = 1400;
    public const double SLAM_WIND = 0.16, SLAM_RAY_GAP = 0.27;
    public const int SLAM_RAYS_MAX = 26;
    public const double SLAM_STEP = 52, SLAM_SHARE = 0.36;
    public const int SLAM_MAX_HITS = 3;
    public const double SLAM_ELEM_SHARE = 0.5;
    public static readonly string[] SLAM_ELEMS = { "fire", "shock", "frost" };
    public const int SLAM_ELEM_MAX = 2;
    public static readonly Dictionary<string, string> SLAM_COL = new() { ["fire"] = "#ff9a3c", ["shock"] = "#ffe95a", ["frost"] = "#bfe3ff" };
    public const double SLAM_SPIKE_A = 0.52;
    public static readonly Dictionary<string, string> SLAM_AIL = new() { ["fire"] = "BURN", ["shock"] = "SHOCK", ["frost"] = "CHILL" };
    public const double LEECH_MAX = 0.25, LEECH_CHANCE_MAX = 0.60, LEECH_HIT_CAP = 0.03;
    /* Difficulty scales the monsters and the loot, not the rules */
    public static readonly Dictionary<string, Diff> DIFFS = new()
    {
        ["easy"] = new Diff { id = "easy", name = "EASY", col = "#4b9b41", lvl = -2, hp = 0.70, dmg = 0.75, spd = 0.88, atk = 1.20, count = 0.80, pack = 0.6, rar = 0.85,
            blurb = "Fewer, slower, softer monsters. Rewards roll a touch worse." },
        ["hard"] = new Diff { id = "hard", name = "HARD", col = "#ffb900", lvl = 0, hp = 1.00, dmg = 1.00, spd = 1.00, atk = 1.00, count = 1.00, pack = 1.0, rar = 1.00,
            blurb = "The game as built. Every number is the baseline." },
        ["nightmare"] = new Diff { id = "nightmare", name = "NIGHTMARE", col = "#d3242b", lvl = 4, hp = 1.85, dmg = 1.55, spd = 1.16, atk = 0.78, count = 1.45, pack = 1.9, rar = 1.25,
            blurb = "Monsters arrive earlier, in packs, and hit far harder. Rewards roll better." }
    };
    public static Diff difficulty = DIFFS["hard"];
    public static double mobLevel() => Math.Max(1, wave.n + difficulty.lvl);
    /* THE MONSTER'S OWN LEVEL, in bands that come faster as the run goes on */
    public static readonly Band[] MOB_LEVEL_BANDS = { new Band { from = 5, every = 4 }, new Band { from = 20, every = 3 }, new Band { from = 30, every = 2 }, new Band { from = 40, every = 1 } };
    public static readonly double MOB_LEVEL_FROM = MOB_LEVEL_BANDS[0].from;
    public const double MOB_LEVEL_STEP = 0.11, MOB_LEVEL_MAX = 100;
    public static double mobRank(double w = double.NaN)
    {
        double n = double.IsNaN(w) ? wave.n : w;
        if (n < MOB_LEVEL_FROM) return 0;
        double r = 1;
        for (int i = 0; i < MOB_LEVEL_BANDS.Length; i++)
        {
            var b = MOB_LEVEL_BANDS[i];
            if (n < b.from) break;
            var next = i + 1 < MOB_LEVEL_BANDS.Length ? MOB_LEVEL_BANDS[i + 1] : null;
            double end = next != null ? Math.Min(n, next.from) : n;
            r += Math.Floor((end - b.from) / b.every);
        }
        return Math.Min(MOB_LEVEL_MAX, r);
    }
    public static double mobLevelEvery(double w = double.NaN)
    {
        double n = double.IsNaN(w) ? wave.n : w;
        double e = MOB_LEVEL_BANDS[0].every;
        foreach (var b in MOB_LEVEL_BANDS) if (n >= b.from) e = b.every;
        return e;
    }
    public static double mobRankMul(double r = double.NaN) => Math.Pow(1 + MOB_LEVEL_STEP, double.IsNaN(r) ? mobRank() : r);
    /* ============================== WEAPONS ============================== */
    public static readonly Dictionary<string, Weapon> WEAPONS = new()
    {
        ["sword"] = new Weapon { id = "sword", name = "SWORD", col = "#cfd6dd",
            blurb = "Balanced. Nothing it does is the best, and nothing it does is bad.",
            dmg = 1.0, rate = 0.34, reach = 56, spinReach = 64, crit = 0.05, critMult = 150,
            bleed = 1.0, spin = 1.40, kb = 1.00, hold = "sword", @throw = "blade",
            lessTaken = 0.40, suppress = 0.25, leech = 0.06, leechChance = 0.15, acc = 1.10,
            attr = new NumMap(("str", 20), ("int", 8), ("dex", 16)) },
        ["axe"] = new Weapon { id = "axe", name = "AXE", col = "#e0a81a",
            blurb = "Crits far more often and far harder — but a clean cut barely bleeds.",
            dmg = 1.15, rate = 0.40, reach = 54, spinReach = 68, crit = 0.10, critMult = 210,
            bleed = 0.45, spin = 1.55, kb = 1.15, hold = "axe", @throw = "axe",
            lessTaken = 0.45, suppress = 0.25, leech = 0.09, leechChance = 0.20, acc = 0.88,
            attr = new NumMap(("str", 16), ("int", 0), ("dex", 32)) },
        ["mace"] = new Weapon { id = "mace", name = "MACE", col = "#b6532b",
            blurb = "Does not swing — it SLAMS. A fault of stone teeth opens in front of you.",
            dmg = 1.70, rate = 0.54, reach = 52, spinReach = 62, crit = 0.03, critMult = 120,
            bleed = 1.90, spin = 1.30, kb = 1.55, hold = "mace", @throw = "mace",
            lessTaken = 0.60, suppress = 0.30, leech = 0.035, leechChance = 0.10, acc = 0.66,
            attr = new NumMap(("str", 40), ("int", 0), ("dex", 4)), slam = true },
        ["staff"] = new Weapon { id = "staff", name = "STAFF", col = "#9b45d6",
            blurb = "Casts fireballs instead of swinging. Burns what it hits.",
            cast = true, elem = "fire",
            dmg = 1.7, rate = 0.68, reach = 0, spinReach = 0, crit = 0.06, critMult = 160,
            bleed = 0, spin = 1.00, kb = 1.00, hold = "pstaff", @throw = "blade",
            incSpell = 0.25, incElem = 0.30, incAtk = 0.20, res = 0.08, acc = 0.72,
            attr = new NumMap(("str", 0), ("int", 44), ("dex", 8)), boltSpd = 520, boltR = 46 },
        ["bow"] = new Weapon { id = "bow", name = "BOW", col = "#6ee06e",
            blurb = "Looses arrows instead of swinging. Physical, precise, and never close.",
            shoot = true,
            dmg = 1.25, rate = 0.72, reach = 0, spinReach = 0, crit = 0.07, critMult = 165,
            bleed = 0.8, spin = 1.00, kb = 0.60, hold = "bow", @throw = "blade",
            lessTaken = 0, suppress = 0.10, leech = 0, leechChance = 0, acc = 1.25, evaBase = 120,
            attr = new NumMap(("str", 4), ("int", 0), ("dex", 44)), arrowSpd = 780 },
        ["scepter"] = new Weapon { id = "scepter", name = "SCEPTER", col = "#b7a6ff",
            blurb = "A summon aura raises skeletons where you point; left click calls them home (Convocation).",
            summon = true,
            dmg = 2.1, rate = 0.70, reach = 0, spinReach = 0, crit = 0.05, critMult = 150,
            bleed = 0, spin = 1.00, kb = 0.80, hold = "scepter", @throw = "blade",
            lessTaken = 0, suppress = 0.15, leech = 0, leechChance = 0, acc = 0.80,
            attr = new NumMap(("str", 4), ("int", 44), ("dex", 0)) },
    };
    public static double weaponLessTaken() => 1 - (weapon != null ? weapon.lessTaken : 0);
    public static double weaponSuppress() => weapon != null ? weapon.suppress : 0;
    public static double weaponLeech() => weapon != null ? weapon.leech : 0;
    public static double weaponLeechChance() => weapon != null ? weapon.leechChance : 0;
    public static double weaponRes() => weapon != null ? weapon.res : 0;
    public static readonly List<string> WEAPON_KEYS = WEAPONS.Keys.ToList();
    public static Weapon weapon = WEAPONS["sword"];
    public static void setWeapon(string id)
    {
        weapon = WEAPONS.GetValueOrDefault(id ?? "") ?? WEAPONS["sword"];
        try { Store.setItem("bb_weapon", weapon.id); } catch { }
        /* every hero wears its own gear: the equipment screen follows the weapon */
        if (GEAR != null) { gearBindClass(); if (gearOn) renderGear(); }
        if (player != null) syncStats();
        /* the favourite list is weapon-dependent: drop a favourite this weapon cannot use */
        buildFavRow();
        if (!favAllowed(favourite)) setFavourite("none");
        Ui.TitleDirty();
        refreshTreeBtn();
        if (treeOn) renderTree();
        buildComboPanel();
    }
    /* THE BARE HAND: the zero every weapon is measured against */
    public static readonly Weapon BARE_HANDS = new Weapon { id = "bare", name = "BARE HANDS", col = "#8a9299",
        blurb = "Nothing in your hands. Every weapon is written against this.",
        dmg = 1.0, rate = 0.50, reach = 40, spinReach = 40, crit = 0.05, critMult = 100,
        bleed = 1.0, spin = 1.00, kb = 1.00, hold = "sword", @throw = "blade",
        lessTaken = 0, suppress = 0, leech = 0, leechChance = 0, attr = new NumMap(("str", 0), ("int", 0), ("dex", 0)) };
    public static readonly double CRIT_BASE = WEAPONS["sword"].crit;
    public static readonly double CRIT_MULT_BASE = WEAPONS["sword"].critMult;
    public const double CRIT_CHANCE_CAP = 0.80, CRIT_MULT_CAP = 300;
    public const double INC_DMG_CAP = 4.00, MORE_DMG_CAP = 3.00;
    public const double ARMOUR_DR_CAP = 0.85, ARMOUR_FLAT_MAX = 520, ARMOUR_INC_MAX = 2.0;
    public const double ARMOUR_VALUE_MAX = ARMOUR_FLAT_MAX * (1 + ARMOUR_INC_MAX);
    public const double SUPPRESS_TAKEN = 0.5;
    public static readonly HashSet<string> SPELL_SOURCES = new() { "orbit", "chain", "bomb", "bane" };
    public static readonly HashSet<string> MELEE_SOURCES = new() { "sword", "storm", "vortex", "slam" };
    public static readonly HashSet<string> LEECH_SOURCES = new() { "sword" };
    /* BLEED */
    public const double BLEED_T = 11, BLEED_SHARE_BASE = 0.40, BLEED_SHARE_MAX = 0.70;
    public const double AIL_CHANCE_MAX = 0.75;
    public static readonly string[] AIL_TYPE_KEYS = { "fire", "frost", "shock", "chaos" };
    public const double AIL_TYPE_MAX = 0.5;
    public static readonly double[] AIL_TYPE_STEP = { 0.03, 0.05, 0.08, 0.11, 0.15 };
    public const double AIL_CARD_W = 0.5;
    public static double ailTypeChance(string type) { var p = player; return p != null && p.ailType != null ? p.ailType[type] : 0; }
    public const double BLEED_STILL_LESS = 0.40, BLEED_STILL_SPD = 18, BLEED_TICK = 0.4;
    public const int BLEED_STACK_MAX = 8;
    /* ENEMY CHILL / FROZEN / SHOCK */
    public const int ECHILL_MAX = 5;
    public const double ECHILL_STEP = 0.13, ECHILL_FLOOR = 0.32, ECHILL_T = 7;
    public const double FROZEN_RES_STEP = 0.12;
    public const int FROZEN_MAX = 4;
    public const double FROZEN_T = 14;
    public const int ESHOCK_MAX = 5;
    public const double ESHOCK_STEP = 0.07, ESHOCK_T = 7;
    public const int ESHOCK_BRAND_AT = 5;
    public const double SHOCKED_CHANCE = 0.32, SHOCKED_CHANCE_PER = 0.07, SHOCKED_STEP = 0.07;
    public const int SHOCKED_MAX = 3;
    public const double SHOCKED_T = 14;
    /* BLIGHT — the pool a blightspitter leaves */
    public const double VENOM_LIFE = 5.5, VENOM_TICK = 0.55, VENOM_SHARE = 0.45;
    /* ======================= DAMAGE CONVERSION ======================= */
    public static readonly string[] CONV_ORDER = { "phys", "shock", "frost", "fire", "chaos" };
    public static readonly Dictionary<string, string> CONV_NAME = new() { ["phys"] = "PHYSICAL", ["shock"] = "LIGHTNING", ["frost"] = "COLD", ["fire"] = "FIRE", ["chaos"] = "CHAOS" };
    public static readonly Dictionary<string, string> CONV_COL = new() { ["phys"] = "#ffffff", ["shock"] = "#ffe23d", ["frost"] = "#6fb7ff", ["fire"] = "#ff4a3c", ["chaos"] = "#b46aff" };
    public static readonly (string body, string stud, string spark, string glow) STORM_BRICK = ("#f2c000", "#9a7400", "#fff3a8", CONV_COL["shock"]);
    public static readonly Dictionary<string, int> CONV_RANK = CONV_ORDER.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i);
    public static bool convAdd(Conv c, string from, string to, double v)
    {
        if (c == null || !convLegal(from, to) || !(v > 0)) return false;
        c.conv[from][to] = c.conv[from][to] + v;
        c.on = true;
        return true;
    }
    public static bool convGain(Conv c, string from, string to, double v)
    {
        if (c == null || !convLegal(from, to) || !(v > 0)) return false;
        c.gain[from][to] = c.gain[from][to] + v;
        c.on = true;
        return true;
    }
    public static Conv blankConv()
    {
        var o = new Conv();
        foreach (var a in CONV_ORDER) { o.conv[a] = new NumMap(); o.gain[a] = new NumMap(); }
        return o;
    }
    public static bool convLegal(string from, string to) =>
        from != null && to != null && CONV_RANK.ContainsKey(from) && CONV_RANK.ContainsKey(to) && CONV_RANK[to] > CONV_RANK[from];
    public static NumMap convShares(Conv c, string from)
    {
        var row = c?.conv?.GetValueOrDefault(from) ?? new NumMap();
        double total = 0;
        foreach (var to in CONV_ORDER) if (convLegal(from, to)) total += Math.Max(0, row[to]);
        var o = new NumMap();
        double scale = total > 1 ? 1 / total : 1;
        foreach (var to in CONV_ORDER)
            if (convLegal(from, to) && row[to] > 0) o[to] = row[to] * scale;
        return o;
    }
}
