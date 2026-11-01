// lego-survival.html, JS 22253-23086: THE LEGION — the scepter's skeletons, golems,
// zombies and overseer: what they are, raising them, Convocation, how they pick a
// fight and land a blow, one frame of all of them, and how they are drawn.
namespace Brickblade;

/* one kind of minion's numbers: `dmg` and `rate` scale the legion's own, `hp` its life */
public sealed class MinionKind
{
    public string name; public double dmg, rate, reach, range, keep, hp, spd, r, aoe;
    public MinionKind With(NumMap k)
    {
        var o = (MinionKind)MemberwiseClone();
        if (k != null) foreach (var (key, v) in k)
            switch (key)
            {
                case "dmg": o.dmg = v; break; case "rate": o.rate = v; break; case "reach": o.reach = v; break;
                case "range": o.range = v; break; case "keep": o.keep = v; break; case "hp": o.hp = v; break;
                case "spd": o.spd = v; break; case "r": o.r = v; break; case "aoe": o.aoe = v; break;
            }
        return o;
    }
}
public sealed class HordeKind { public string name; public double w, s; public NumMap k; public bool crawl, fat; }
public sealed class MinionLeap { public double t, fx, fy, tx, ty; }
public sealed class MinionCharge { public Obj tgt; public double t; }

/* the fields a minion carries (minions are Ents, as the JS's are plain objects) */
public sealed partial class Ent
{
    public string kind, gt, zk, el; public bool pike, mage, buffed;
    public double rise, born, strikeA, drawT, repoT, pulseT, leapCd, chargeCd, regenT, lx, ly, ldt;
    public int slot; public Obj strikeTgt; public Pt shotAt; public MinionLeap leap; public MinionCharge charge; public Ent gfx;
    /* the hero's side of Convocation */
    public List<Ent> convoQ; public double convoN, convoI, convoT, convoB; public string golemBuffKey;
}

public static partial class G
{
    /* =============================== THE LEGION ===============================
       A SUMMONER'S HERO HITS NOTHING. The scepter stands skeletons up out of the
       floor where you point; they walk to the nearest body in reach of you and cut
       it down, and they stand between you and the crowd while they do -- a monster
       touching a skeleton is held there, half-speed, and fights it instead of
       walking through it. They die, and you raise more.
       - RAISING: one a cast (two with Twin Raising) up to the legion's size -- a
         spearman, a swordsman or an archer. At the cap the OLDEST crumbles and the
         new one takes its place. Dead, you raise nothing.
       - THEIR NUMBERS ARE THEIR OWN. Minion damage reads the level pool you gain
         by levelling (so a legion keeps up with the run) plus the minion pool the
         scepter's bricks and nodes fill -- and nothing you buy for your own hits.
         MIRRORED LEGION is the keystone that says otherwise: three quarters of
         your own increases, and your conversions, ride along.
       - SKELETON MAGES, the legendary, turns every one of them into a caster that
         stands off and throws fast bolts of fire, cold or lightning -- a third of
         the legion each -- for 80% of a blade's hit.
       - MINION INSTABILITY, the other keystone: a skeleton that falls bursts, and
         everything close takes a share of its life as fire. */
    public const double MINION_MAX_BASE = 4, MINION_MAX_CAP = 15;
    public const double MINION_PERCAST_MAX = 5;          /* skeletons a single raising stands up, at most */
    public const double MINION_HP_BASE = 14, MINION_HP_LVL = 0.12;   /* life, and +12% of it a hero level */
    public const double MINION_SPD = 175, MINION_REACH = 30, MINION_RATE = 0.85;
    public const double MINION_SEEK = 460, MINION_LEASH = 560, MINION_RISE = 0.45;
    public const double MINION_GUARD = 260;            /* anything this close to you comes first */
    public const double MINION_WIND = 0.2;             /* a skeleton's raised-weapon beat */
    public const double MINION_CONTACT = 0.9;          /* a boss's body lands on a skeleton this often */
    public const double MAGE_RANGE = 820, MAGE_KEEP = 300, MAGE_RATE = 0.75, MAGE_SHARE = 0.8, MAGE_BOLT_SPD = 960;
    /* A MAGE DOES NOT MOVE. It stands where it rose and casts; Convocation is how it
       is brought anywhere else. Its bolts fly far, fast and wide. */
    public const double MAGE_BOLT_FLY = 1.3, MAGE_FAN = 0.24, MAGE_FAN_MAX = 2.8;
    public static readonly string[] MAGE_ELEMS = { "fire", "frost", "shock" };
    public static readonly Dictionary<string, string> MAGE_COL = new() { ["fire"] = "#ff8a3c", ["frost"] = "#8fd8ff", ["shock"] = "#ffe95a", ["chaos"] = "#b45cff" };
    public const double MIRROR_SHARE = 0.75;
    public const double INSTAB_SHARE = 0.30, INSTAB_R = 110;
    /* WHAT RISES. A plain raising stands up one of three skeletons, for variety and
       for shape: a SPEARMAN (reach), a SWORDSMAN (quicker, harder, shorter), or an
       ARCHER that stands off and looses bone arrows. The legendary makes every one
       a MAGE; the first epic makes some of the melee ones GOLEMS; the second puts an
       OVERSEER at your shoulder. `dmg` and `rate` scale the legion's own numbers;
       `hp` its life. */
    public static readonly Dictionary<string, MinionKind> MINION_KINDS = new()
    {
        ["spear"] = new MinionKind { name = "Skeleton Spearman", dmg = 1.25, rate = 0.85, reach = 34, range = 0, hp = 1.0, spd = 1.00, r = 11 },
        ["sword"] = new MinionKind { name = "Skeleton Swordsman", dmg = 1.35, rate = 0.72, reach = 26, range = 0, hp = 1.1, spd = 1.25, r = 11 },
        ["bow"] = new MinionKind { name = "Skeleton Archer", dmg = 0.90, rate = 1.00, reach = 0, range = 480, keep = 240, hp = 0.8, spd = 1.0, r = 11 },
        ["mage"] = new MinionKind { name = "Skeleton Mage", dmg = MAGE_SHARE, rate = MAGE_RATE, reach = 0, range = MAGE_RANGE, keep = MAGE_KEEP, hp = 0.8, spd = 1.0, r = 11 },
        ["golem"] = new MinionKind { name = "Bone Golem", dmg = 2.75, rate = 1.25, reach = 42, range = 0, hp = 3.5, spd = 0.80, r = 17, aoe = 62 },
        ["zombie"] = new MinionKind { name = "Raised Zombie", dmg = 1.15, rate = 0.95, reach = 28, range = 0, hp = 2.2, spd = 0.70, r = 12 },
        ["overseer"] = new MinionKind { name = "Bone Overseer", dmg = 0, rate = 1, reach = 0, range = 0, hp = 4.0, spd = 1.05, r = 13 },
    };
    public static readonly (string k, double w)[] MINION_MIX = { ("spear", 1.0 / 3), ("sword", 1.0 / 3), ("bow", 1.0 / 3) };
    /* BONE GOLEMS ARE THEIR OWN POOL. They are not skeletons and take no place in
       the legion: five golems AND fifteen skeletons. Each Bone Golem is one more
       golem; they rise at your side on their own, and one that falls is back
       GOLEM_BACK seconds later. */
    /* (the plain Bone Golem card is gone: the golems are the six kinds above) */
    /* RAISE ZOMBIE. With the tree's notable or the epic, the summon aura also raises
       ZOMBIES -- a pool of their own, up to twelve -- out of the corpses zombies
       leave behind: the corpse nearest your cursor, within ZOMBIE_REACH of you,
       stands up for you, on the same clock the skeletons rise on. */
    public const double ZOMBIE_MINION_MAX = 12, ZOMBIE_REACH = 700;
    /* THE ZOMBIE HORDE (a legendary, on top of Raise Zombie): the cap doubles and
       what rises is any of the family, not only the plain walker -- each with its
       own numbers against the Raised Zombie's */
    public const double ZOMBIE_HORDE_MUL = 2;
    public static readonly Dictionary<string, HordeKind> ZOMBIE_HORDE_KINDS = new()
    {
        ["zombie"] = new HordeKind { name = "Raised Zombie", w = 3, k = new NumMap(), s = 0.84 },
        ["runner"] = new HordeKind { name = "Raised Runner", w = 2, k = new NumMap(("spd", 1.45), ("hp", 0.75), ("dmg", 0.9), ("rate", 0.8)), s = 0.84 },
        ["crawler"] = new HordeKind { name = "Raised Crawler", w = 2, k = new NumMap(("spd", 1.1), ("hp", 0.55), ("dmg", 0.8), ("rate", 0.7), ("r", 11)), s = 0.84, crawl = true },
        ["bloater"] = new HordeKind { name = "Raised Bloater", w = 1, k = new NumMap(("spd", 0.6), ("hp", 2.2), ("dmg", 0.9), ("r", 17)), s = 1.1, fat = true },
        ["hulk"] = new HordeKind { name = "Raised Hulk", w = 1, k = new NumMap(("spd", 0.75), ("hp", 1.8), ("dmg", 1.8), ("rate", 1.15), ("r", 16)), s = 1.2 },
    };
    public static double zombieCap() => ZOMBIE_MINION_MAX * ((player != null && player.minions.horde) ? ZOMBIE_HORDE_MUL : 1);
    public static string zombieHordeKind()
    {
        double t = 0; foreach (var k in ZOMBIE_HORDE_KINDS.Keys) t += ZOMBIE_HORDE_KINDS[k].w;
        var r = Js.R() * t;
        foreach (var k in ZOMBIE_HORDE_KINDS.Keys) { r -= ZOMBIE_HORDE_KINDS[k].w; if (r <= 0) return k; }
        return "zombie";
    }
    /* A SPEARMAN'S THRUST GOES THROUGH: everything on the line of the spear, not one
       body and not a circle -- and some carry a PIKE, half again as long */
    public const double SPEAR_PIKE_CHANCE = 0.4, SPEAR_PIKE_REACH = 58, SPEAR_LINE_W = 16;
    /* A SWORDSMAN CHARGES, sometimes: from a little way off it runs the last of the
       ground at CHARGE_SPD times its pace and strikes on arrival */
    public const double SWORD_CHARGE_MIN = 90, SWORD_CHARGE_MAX = 280, SWORD_CHARGE_SPD = 2.6, SWORD_CHARGE_T = 0.45, SWORD_CHARGE_CHANCE = 0.5;
    public static readonly Tbl SWORD_CHARGE_CD = new double[] { 3, 6 };
    /* A MELEE GOLEM LEAPS onto what it is after, when it is a few strides off: up,
       across, and down on it -- a slam where it lands */
    public const double GOLEM_LEAP_MIN = 120, GOLEM_LEAP_MAX = 340, GOLEM_LEAP_T = 0.5, GOLEM_LEAP_CD = 6, GOLEM_LEAP_H = 70;
    /* MINIONS MOVE FASTER TOO: the minion speed stat speeds their feet as well as
       their hands, and half of your own increased movement speed is theirs, so a
       quick hero is not a hero with no legion */
    public static double minionMoveMul()
    {
        var p = player;
        if (p == null) return 1;
        var own = Math.max(0, Js.Or(p.speed, SPEED_BASE) / SPEED_BASE - 1);
        return (1 + p.minions.spd) * (1 + 0.5 * own);
    }
    public static readonly FigCols ZOMBIE_MINION_COLORS = new() { head = "#86b25e", torso = "#4a3d6a", legs = "#2e2744", hands = "#86b25e", hair = "#3d5230" };
    public const double GOLEM_BACK = 10;
    /* GREATER MULTIPLE PROJECTILES, for the legion. An archer's arrow and a mage's
       bolt become a fan of them: +1 a Bone Volley (six at most), +4 from the
       legendary, +2 from the tree's Bone Rain -- thirteen at the far end, fourteen
       extra the hard cap. The fan widens with the count; a body close enough to
       stand in the middle of it takes several. */
    public const double MINION_PROJ_CAP = 14, MINION_VOLLEY_MAX = 8, MINION_GMP_N = 4, MINION_GMP_LESS = 0.25;
    public const double MINION_PROJ_HIT = 0.4;              /* the share of a fan the sheet counts as landing on one body */
    /* the aura reaches as far as the legion fights from you (MINION_LEASH): at 320
       it covered half of a fighting legion at any moment */
    public const double OVERSEER_R = 520, OVERSEER_MORE = 1.30, OVERSEER_SPD = 1.25, OVERSEER_BACK = 15;
    public static readonly HashSet<string> MINION_SOURCES = new() { "minion", "mfire", "mfrost", "mshock", "mburst", "mchaos" };
    public static readonly FigCols MINION_COLORS = new() { head = "#ede8ff", torso = "#5b4a8f", legs = "#3a2f5e", hands = "#ede8ff", hair = "#7d6ab8" };
    public static readonly FigCols OVERSEER_COLORS = new() { head = "#ede8ff", torso = "#3a2f5e", legs = "#2a2140", hands = "#ffd76e", hair = "#b7a6ff" };
    public static double minionMax() => Math.min(MINION_MAX_CAP, Js.Or(player != null ? player.minions.max : 0, MINION_MAX_BASE));
    public static MinionKind kindOf(string k) => MINION_KINDS.GetValueOrDefault(k ?? "") ?? MINION_KINDS["spear"];
    public static double minionMaxHp(string kind = null, string gt = null)
    {
        var p = player; var M = p.minions;
        return MINION_HP_BASE * (1 + MINION_HP_LVL * (p.level - 1)) * (1 + M.life) * (!string.IsNullOrEmpty(gt) ? golemK(gt) : kindOf(kind ?? "spear")).hp;
    }
    /* WHAT A MINION HIT IS WORTH: (the scepter's base + the minion flat) x (1 + the
       level pool + the minion pool [+ 3/4 of yours, mirrored]) x the minion MORE x
       what kind of skeleton it is [x the overseer's MORE, standing in its aura] */
    public static double minionInc()
    {
        var p = player; var M = p.minions;
        var inc = p.incAll + M.inc;
        if (M.mirror) inc += MIRROR_SHARE * (incMeleeAll() + incSpellAll() + p.inc["elem"]);
        return Math.min(INC_DMG_CAP, inc);
    }
    /* `k` is a kind name, or -- as the cards and the sheet ask it -- true for a mage
       and false for a plain skeleton */
    public static string minionKindArg(string k) => k;
    public static string minionKindArg(bool k) => k ? "mage" : "spear";
    public static double minionDamage(bool mage) => minionDamage(minionKindArg(mage));
    public static double minionDamage(string k, Ent m = null)
    {
        var M = player.minions; var kind = m != null ? m.kind : minionKindArg(k); var K = m != null ? minionK(m) : kindOf(kind);
        var golem = kind == "golem";
        /* the Carrion Golem's added physical, for everything that is not a golem */
        var carrion = !golem && carrionOn() ? weapon.dmg * CARRION_FLAT : 0;
        /* RAGE is yours and theirs: a raging summoner's legion hits harder too */
        return (weapon.dmg + M.flat + carrion) * (1 + minionInc()) * Js.Or(M.more, 1) * K.dmg
          * (m != null && m.buffed ? OVERSEER_MORE : 1) * (golem && m != null && m.gt == "carrion" ? carrionMul(m) : 1) * rageMul();
    }
    public static double minionRate(bool mage) => minionRate(minionKindArg(mage));
    public static double minionRate(string k, Ent m = null) =>
        (m != null ? minionK(m) : kindOf(minionKindArg(k))).rate / (1 + player.minions.spd) / (m != null && m.buffed ? OVERSEER_SPD : 1);
    /* everything a full raising stands up: the golems (their own pool), then the
       skeletons -- mages, or the mix */
    public static List<string> legionPlan()
    {
        var M = player.minions; var n = minionMax(); var g = golemMax();
        var outp = new List<string>(); for (int i = 0; i < g; i++) outp.Add("golem");
        for (int i = 0; i < n; i++) outp.Add(M.mages ? "mage" : null);
        for (int i = 0; i < zombieMinions().Count; i++) outp.Add("zombie");
        return outp;
    }
    public static double golemCap() => clamp(GOLEM_BASE_CAP + (player != null ? player.minions.golemCap : 0), 0, GOLEM_CAP_MAX);
    public static List<string> golemTypesOwned() => (player != null ? player.minions.golemTypes : null) ?? new List<string>();
    public static double golemMax() => Math.min(golemTypesOwned().Count, golemCap());
    /* a golem's numbers: the plain golem's, with its kind's on top */
    public static MinionKind golemK(string gt) { var T = GOLEM_TYPES.GetValueOrDefault(gt ?? ""); return T != null ? MINION_KINDS["golem"].With(T.k) : MINION_KINDS["golem"]; }
    public static MinionKind minionK(Ent m)
    {
        if (m != null && m.kind == "golem") return golemK(m.gt);
        if (m != null && m.kind == "zombie" && !string.IsNullOrEmpty(m.zk) && ZOMBIE_HORDE_KINDS.ContainsKey(m.zk)) return MINION_KINDS["zombie"].With(ZOMBIE_HORDE_KINDS[m.zk].k);
        if (m != null && m.kind == "spear" && m.pike) return MINION_KINDS["spear"].With(new NumMap(("reach", SPEAR_PIKE_REACH)));
        return kindOf(m != null ? m.kind : "spear");
    }
    /* the kind owed a body: the first one you keep that has none standing */
    public static string golemOwed() { var have = golems().Select(g => g.gt).ToList(); return golemTypesOwned().Take((int)golemCap()).FirstOrDefault(t => !have.Contains(t)); }
    public static string golemSrc(Ent m) { var el = GOLEM_TYPES.GetValueOrDefault(m.gt ?? "")?.el; return !string.IsNullOrEmpty(el) ? "m" + el : "minion"; }
    /* THE CARRION GOLEM: added physical for everything that is not a golem, and it
       feeds on the company it keeps */
    public static bool carrionOn() => minions.Any(g => !g.gone && g.kind == "golem" && g.gt == "carrion" && g.rise >= 1);
    public static double carrionMul(Ent m)
    {
        double n = 0;
        foreach (var q in minions) if (!q.gone && q.rise >= 1 && q.kind != "golem" && q.kind != "overseer" && Math.hypot(q.x - m.x, q.y - m.y) < CARRION_R) n++;
        return 1 + Math.min(CARRION_MAX, n * CARRION_PER);
    }
    /* WHAT THE GOLEMS GRANT, while they stand: written through the tree's own stat
       writers and backed out when one falls, then the sheet rebuilt */
    public static void golemBuffSync()
    {
        var p = player;
        if (p == null) return;
        var on = new HashSet<string>(golems().Where(g => g.rise >= 1 && GOLEM_TYPES.ContainsKey(g.gt ?? "")).Select(g => g.gt));
        var key = string.Join(",", on.OrderBy(x => x, StringComparer.Ordinal));
        if (key == (p.golemBuffKey ?? "")) return;
        var was = new HashSet<string>((p.golemBuffKey ?? "").Split(',').Where(x => x != ""));
        foreach (var t in was) if (!on.Contains(t)) foreach (var (k, v) in GOLEM_TYPES[t].buff) META_STATS[k].w(p, -v);
        foreach (var t in on) if (!was.Contains(t)) foreach (var (k, v) in GOLEM_TYPES[t].buff) META_STATS[k].w(p, v);
        p.golemBuffKey = key;
        syncStats();
    }
    public static List<Ent> golems() => minions.Where(m => !m.gone && m.kind == "golem").ToList();
    /* how many an archer or a mage looses at once */
    public static double minionProj() => 1 + clamp(Math.round(player != null ? player.minions.proj : 0), 0, MINION_PROJ_CAP);
    /* what one volley is worth on one body, relative to a single shot */
    public static double minionVolleyWorth()
    {
        var n = minionProj();
        return (1 + (n - 1) * MINION_PROJ_HIT) * Js.Or(player != null ? player.minions.projMore : 0, 1);
    }
    public static double minionDps()
    {
        if (!isSummoner()) return 0;
        var crit = 1 + critChance() * (critMult() - 1);
        double shot(string k) => (k == "bow" || k == "mage") ? minionVolleyWorth() : 1;
        double dps = 0;
        foreach (var k in legionPlan())
        {
            if (k != null) dps += minionDamage(k) / minionRate(k) * shot(k);
            else foreach (var (mk, w) in MINION_MIX) dps += w * minionDamage(mk) / minionRate(mk) * shot(mk);
        }
        return dps * crit;
    }
    /* the skeletons: what the scepter raises and the legion cap counts */
    public static List<Ent> legion() => minions.Where(m => !m.gone && m.kind != "overseer" && m.kind != "golem" && m.kind != "zombie").ToList();
    public static List<Ent> zombieMinions() => minions.Where(m => !m.gone && m.kind == "zombie").ToList();
    /* the corpse the aura would raise next: nearest the cursor, within reach of you */
    public static Corpse corpseToRaise()
    {
        Obj h = heroObj ?? player;
        Corpse best = null; double bd = double.PositiveInfinity;
        foreach (var k in corpses)
        {
            if (Math.hypot(k.x - h.x, k.y - h.y) > ZOMBIE_REACH) continue;
            var d = Math.hypot(k.x - mouse.wx, k.y - mouse.wy);
            if (d < bd) { bd = d; best = k; }
        }
        return best;
    }
    public static Ent raiseZombie()
    {
        var k = corpseToRaise();
        if (k == null) return null;
        corpses = corpses.Where(x => x != k).ToList();
        var z = raiseAt(k.x, k.y, "zombie");
        /* the horde: any of the family */
        if (player.minions.horde)
        {
            z.zk = zombieHordeKind();
            var K = minionK(z); z.r = K.r; z.maxHp = z.hp = z.maxHp * Js.Or(ZOMBIE_HORDE_KINDS[z.zk].k["hp"], 1);
        }
        addRing(z.x, z.y, 6, 220, 40, "#9ede5a", 4, 0.35);
        addDust(z.x, z.y, 4, "rgba(90,110,60,");
        return z;
    }
    /* AN EVEN LEGION. The kind that rises is whichever of spear, sword and bow the
       legion has fewest of (a tie is broken at random): nine skeletons are three of
       each. Mages are all mages. */
    public static string nextKind()
    {
        var M = player.minions;
        if (M.mages) return "mage";
        var have = new Dictionary<string, int> { ["spear"] = 0, ["sword"] = 0, ["bow"] = 0 };
        foreach (var m in legion()) if (m.kind != null && have.ContainsKey(m.kind)) have[m.kind]++;
        var least = System.Math.Min(have["spear"], System.Math.Min(have["sword"], have["bow"]));
        var pk = MINION_MIX.Select(x => x.k).Where(k => have[k] == least).ToList();
        return pk[Js.I32(Js.R() * pk.Count)];
    }
    /* RAISING. The SUMMON AURA calls this on its own clock (`fresh` false): it only
       ever fills the legion to its cap. Called any other way -- the bench, a test --
       a full legion's FIRST skeleton crumbles and the new one takes its place. The
       clock is the scepter's rate, sped by attack speed like any weapon's. */
    public static void raiseMinion(double aim, bool? fresh = null)
    {
        var p = player;
        if (p == null || p.dead) return;
        var M = p.minions;
        var n = Js.Or(M.perCast, 1);
        if (fresh == false) n = Math.min(n, minionMax() - legion().Count);
        if (n <= 0) return;
        p.raiseCd = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
        p.castT = 0.2; retPulse = 1;
        var d = Math.min(150, Math.hypot(mouse.wx - p.x, mouse.wy - p.y));
        for (int i = 0; i < n; i++)
        {
            var L = legion();
            if (L.Count >= minionMax())
            {
                var old = L.Aggregate(L[0], (a, z) => z.born < a.born ? z : a);
                old.gone = true;
                spawnBricks(old.x, old.y, new[] { "#ede8ff", "#5b4a8f", "#3a2f5e" }, 5, 130);
                addDust(old.x, old.y, 4);
            }
            double a2 = aim + (i != 0 ? rnd(-0.8, 0.8) : 0), rr = Math.max(40, d - i * 26);
            var x = clamp(p.x + Math.cos(a2) * rr, W.wall + 40, W.w - W.wall - 40);
            var y = clamp(p.y + Math.sin(a2) * rr, W.wall + 40, W.h - W.wall - 40);
            raiseAt(x, y);
        }
        minions = minions.Where(m => !m.gone).ToList();
        SFX.build();
    }
    /* CONVOCATION, the scepter's left click (PoE's gem, built in): every minion you
       own is pulled back to your side at once and regenerates CONVO_REGEN of its
       life a second for CONVO_REGEN_T seconds. On a CONVO_CD cooldown. */
    public const double CONVO_CD = 3, CONVO_REGEN = 0.25, CONVO_REGEN_T = 3, CONVO_R0 = 50, CONVO_R1 = 100;
    public const double CONVO_STEP = 0.14;               /* seconds between one group arriving and the next */
    /* A BIG LEGION COMES HOME IN BIGGER GROUPS. One at a time, thirty skeletons took
       four seconds -- longer than the cooldown. The call now lands in at most this
       many beats, each bringing ceil(legion / beats): six or fewer still come one by
       one, twelve come two at a time, thirty come five at a time -- and every legion
       is home inside a second. */
    public const double CONVO_BEATS = 6;
    public static double convoBatch(double n) => Math.max(1, Math.ceil(n / CONVO_BEATS));
    public static bool convocation()
    {
        var p = player;
        if (p == null || p.dead || p.convoCd > 0) return false;
        var all = minions.Where(m => !m.gone && m.kind != "overseer").ToList();
        p.atkCd = 0.2;
        if (all.Count == 0) return false;
        /* NEAREST FIRST, a beat apart, so the legion is seen to answer -- in groups
           that grow with it, so a big one is home as quickly as a small one */
        all = all.OrderBy(a => Math.hypot(a.x - p.x, a.y - p.y)).ToList();
        p.convoQ = all; p.convoN = all.Count; p.convoI = 0; p.convoT = 0; p.convoB = convoBatch(all.Count);
        addRing(p.x, p.y, 10, 420, CONVO_R1 + 30, "#b7a6ff", 6, 0.5);
        p.convoCd = CONVO_CD; p.castT = 0.3; retPulse = 1;
        SFX.build();
        logEvent("convocation — " + S(all.Count) + " called home", "#b7a6ff");
        return true;
    }
    /* one of them arrives: lifted out where it stood, set down in the ring round you */
    public static void convoPull(Ent m, double i, double n)
    {
        var p = player;
        if (m == null || m.gone || p == null) return;
        Col col = !string.IsNullOrEmpty(m.el) ? (MAGE_COL.GetValueOrDefault(m.el) ?? "#b7a6ff") : "#b7a6ff";
        addSparks(m.x, m.y - 14, 5, col);
        double a = i / Math.max(1, n) * TAU + rnd(-0.2, 0.2), rr = CONVO_R0 + (i % 3) * (CONVO_R1 - CONVO_R0) / 2;
        var at = floorAt(p.x + Math.cos(a) * rr, p.y + Math.sin(a) * rr, m.r + 2);
        m.x = at.x; m.y = at.y; m.lx = m.x; m.ly = m.y;
        m.spot = null; m.strikeT = 0; m.drawT = 0; m.strikeTgt = null;
        m.regenT = CONVO_REGEN_T;
        addSparks(m.x, m.y - 14, 5, col); addRing(m.x, m.y, 4, 200, 24, col, 3, 0.25);
        SFX.pick();
    }
    public static void convoTick(double dt)
    {
        var p = player;
        if (p == null || p.convoQ == null || p.convoQ.Count == 0) return;
        p.convoT -= dt;
        while (p.convoT <= 0 && p.convoQ.Count > 0)
        {
            for (int k = 0; k < Js.Or(p.convoB, 1) && p.convoQ.Count > 0; k++)
            {
                var m = p.convoQ[0]; p.convoQ.RemoveAt(0);
                convoPull(m, p.convoI++, p.convoN);
            }
            p.convoT += CONVO_STEP;
        }
    }
    public static double minionBorn = 0;
    public static Ent raiseAt(double x, double y, string kind = null, string gt = null)
    {
        var k = kind ?? nextKind(); var K = !string.IsNullOrEmpty(gt) ? golemK(gt) : kindOf(k); var hp = minionMaxHp(k, gt);
        /* never out of a lake or a boulder: the nearest floor you could stand on */
        var at = k == "mage" ? mageSpot(x, y, K.r, null) : floorAt(x, y, K.r + 4); x = at.x; y = at.y;
        var m = new Ent { x = x, y = y, r = K.r, kind = k, hp = hp, maxHp = hp, rise = 0, atkCd = rnd(0.1, 0.4), face = new Pt(0, 1), walk = rnd(TAU),
            moving = false, flash = 0, phase = rnd(TAU), slot = minionSlot(), castT = 0, strikeT = 0, drawT = 0,
            born = ++minionBorn, scale = k == "golem" ? 0.62 : 0.82 };
        if (!string.IsNullOrEmpty(gt)) m.gt = gt;
        if (k == "spear" && Js.R() < SPEAR_PIKE_CHANCE) m.pike = true;
        m.mage = k == "mage";
        m.el = m.mage ? MAGE_ELEMS[m.slot % MAGE_ELEMS.Length] : null;
        minions.Add(m);
        addRing(x, y, 6, 220, k == "golem" ? 70 : 46, "#b7a6ff", 4, 0.4);
        spawnBricks(x, y, new[] { "#ede8ff", "#5b4a8f", "#3a2f5e" }, 5, 120);
        return m;
    }
    /* A MAGE NEVER MOVES, so nothing may shove it either -- which means it has to
       RISE somewhere with room: the floor nearest (x,y) that no other minion is
       standing on */
    public static Pt mageSpot(double x, double y, double r, Ent self)
    {
        bool free(double px, double py) => floorClear(px, py, r + 4) &&
          minions.All(o => o == self || o.gone || minionTribe(o) != "skel" || Math.hypot(o.x - px, o.y - py) >= o.r + r);
        if (free(x, y)) return new Pt(x, y);
        for (int ring = 1; ring <= 10; ring++)
            for (int k = 0; k < ring * 6; k++)
            {
                double a = (double)k / (ring * 6) * TAU + ring, px = x + Math.cos(a) * ring * (r + 6), py = y + Math.sin(a) * ring * (r + 6);
                if (px < W.wall + 30 || py < W.wall + 30 || px > W.w - W.wall - 30 || py > W.h - W.wall - 30) continue;
                if (free(px, py)) return new Pt(px, py);
            }
        return floorAt(x, y, r + 4);
    }
    /* a stable place in the formation, and a stable element for a mage */
    public static int minionSlot()
    {
        var used = new HashSet<int>(minions.Select(m => m.slot));
        int i = 0; while (used.Contains(i)) i++;
        return i;
    }
    /* SKELETON MAGES: every skeleton standing turns -- a golem is not a skeleton,
       and the overseer is not one of the rank and file */
    public static void minionsToMages()
    {
        foreach (var m in minions.Live())
        {
            if (m.kind == "golem" || m.kind == "overseer" || m.kind == "zombie") continue;
            m.kind = "mage"; m.mage = true; m.el = MAGE_ELEMS[m.slot % MAGE_ELEMS.Length];
            var at = mageSpot(m.x, m.y, m.r, m); m.x = at.x; m.y = at.y;     /* stood where it can stay */
        }
    }
    /* BONE GOLEMS: one rises at your side for every golem owed and not standing */
    public static Ent raiseGolem(string gt = null)
    {
        Ent h = heroObj ?? player;
        if (h == null || h.dead) return null;
        gt = !string.IsNullOrEmpty(gt) ? gt : golemOwed();
        if (string.IsNullOrEmpty(gt) || !GOLEM_TYPES.ContainsKey(gt)) return null;
        var a = rnd(TAU); var g = raiseAt(h.x + Math.cos(a) * 70, h.y + Math.sin(a) * 70, "golem", gt);
        addRing(g.x, g.y, 8, 260, 80, GOLEM_TYPES[gt].col, 6, 0.5);
        return g;
    }
    public static readonly List<Ent> _minBuf = new();
    /* WHAT A SKELETON GOES FOR. Whatever is closest to YOU first, if anything is
       close enough to matter; otherwise the nearest thing to itself, never far
       from you. With nothing to fight, the nearest chest or crate -- a summoner has
       no other way to open one. */
    public static Ent minionTarget(Ent m)
    {
        Ent h = heroObj ?? player;
        Ent best = null; double bd = MINION_GUARD;
        foreach (var e in enemiesNear(h.x, h.y, MINION_GUARD, _minBuf).Live())
        {
            if (e.dead || e.state != "active" || !breachSees(e) || e.dormant) continue;
            var d = Math.hypot(e.x - h.x, e.y - h.y);
            if (d < bd) { bd = d; best = e; }
        }
        if (best != null) return best;
        /* an archer or a mage looks as far as it can shoot */
        var seek = Math.max(MINION_SEEK, kindOf(m.kind).range);
        /* ...and a caster that throws 800px may fight that far from you */
        var leash = Math.max(MINION_LEASH, kindOf(m.kind).range);
        bd = seek;
        foreach (var e in enemiesNear(m.x, m.y, seek, _minBuf).Live())
        {
            if (e.dead || e.state != "active" || !breachSees(e) || e.dormant) continue;
            if (Math.hypot(e.x - h.x, e.y - h.y) > leash) continue;   /* never far from you */
            var d = Math.hypot(e.x - m.x, e.y - m.y);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }
    public static Prop minionProp(Ent m)
    {
        Ent h = heroObj ?? player;
        Prop best = null; double bd = MINION_SEEK;
        foreach (var pr in props)
        {
            if (pr.dead || pr.indestructible || (pr.type != "chest" && pr.type != "crate")) continue;
            if (Math.hypot(pr.x - h.x, pr.y - h.y) > MINION_LEASH) continue;
            var d = Math.hypot(pr.x - m.x, pr.y - m.y);
            if (d < bd) { bd = d; best = pr; }
        }
        return best;
    }
    public static void knockProp(Prop pr)
    {
        if (pr.dead || pr.indestructible) return;
        pr.hp--; pr.flash = 1;
        addSparks(pr.x, pr.y - 10, 3); SFX.hit();
        if (pr.hp <= 0) breakProp(pr);
    }
    /* a skeleton walks the same floor you do: round the rocks, along the shore,
       never across the water */
    public static void minionStep(Ent m, double tx, double ty, double dt, double spd)
    {
        double dx = tx - m.x, dy = ty - m.y, d = Math.hypot(dx, dy);
        if (d < 2) { m.moving = false; return; }
        var v = Math.min(spd, d / Math.max(dt, 1e-4));
        double vx = dx / d * v, vy = dy / d * v;
        var slid = slideAroundSolids(m, vx, vy, dt);
        if (slid != null) { vx = slid.x; vy = slid.y; }
        m.x = clamp(m.x + vx * dt, W.wall + 30, W.w - W.wall - 30);
        m.y = clamp(m.y + vy * dt, W.wall + 30, W.h - W.wall - 30);
        collideWorld(m);
        m.moving = true; m.walk += dt * 10; m.face = new Pt(dx / d, dy / d);
    }
    /* A LEAPING GOLEM COMES DOWN: a slam round where it lands, a little harder than
       its blow, in its own element */
    public static void golemLand(Ent m)
    {
        var K = minionK(m); var T = GOLEM_TYPES.GetValueOrDefault(m.gt ?? ""); var r = Js.Or(K.aoe, 62) * 1.25;
        addRing(m.x, m.y, 8, 320, r, T != null ? T.col : "#b7a6ff", 6, 0.4); addShake(0.12); addDust(m.x, m.y, 8);
        var src = golemSrc(m); var dmg = minionDamage("golem", m) * 1.2;
        foreach (var e in enemiesNear(m.x, m.y, r + 30, _minBuf).ToList())
        {
            if (e.dead || e.state != "active" || !breachSees(e)) continue;
            if (Math.hypot(e.x - m.x, e.y - m.y) > r + Js.Or(e.r, 12)) continue;
            hitEnemy(e, dmg, Math.atan2(e.y - m.y, e.x - m.x), 220, src, new HitOpts());
        }
    }
    /* A MELEE BLOW LANDS: on its target, or -- a golem's -- on everything in front */
    public static void minionBlow(Ent m)
    {
        var K = minionK(m); var t = m.strikeTgt;
        double cx = m.x + Math.cos(m.strikeA) * (K.reach * 0.8), cy = m.y + Math.sin(m.strikeA) * (K.reach * 0.8);
        slashFx(m.x, m.y - 14 * (m.kind == "golem" ? 1.4 : 1), m.strikeA, (K.reach + 6), "#c9b8ff");
        if (m.kind == "golem")
        {
            var T = GOLEM_TYPES.GetValueOrDefault(m.gt ?? "");
            addRing(cx, cy, 6, 300, K.aoe, T != null ? T.col : "#b7a6ff", 5, 0.35); addShake(0.08);
            var src = golemSrc(m); var dmg = minionDamage("golem", m);
            foreach (var e in enemiesNear(cx, cy, K.aoe + 30, _minBuf).ToList())
            {
                if (e.dead || e.state != "active" || !breachSees(e)) continue;
                if (Math.hypot(e.x - cx, e.y - cy) > K.aoe + Js.Or(e.r, 12)) continue;
                hitEnemy(e, dmg, Math.atan2(e.y - m.y, e.x - m.x), 160, src, new HitOpts());
            }
            if (t is Prop tp && !string.IsNullOrEmpty(tp.type) && !tp.dead && props.Contains(tp)) knockProp(tp);
            return;
        }
        if (t == null || t.dead) return;
        if (t is Prop pr) { if (props.Contains(pr) && Math.hypot(pr.x - m.x, pr.y - m.y) < K.reach + Js.Or(pr.r, 14) + 12) knockProp(pr); return; }
        if (t is not Ent te) return;
        /* THE SPEAR: through everything on its line, as far as it reaches */
        if (m.kind == "spear")
        {
            double len = K.reach + 18, ux = Math.cos(m.strikeA), uy = Math.sin(m.strikeA); var dmg = minionDamage("spear", m);
            bool hitT = false;
            foreach (var e in enemiesNear(m.x, m.y, len + 40, _minBuf).ToList())
            {
                if (e.dead || e.state != "active" || !breachSees(e)) continue;
                double along = (e.x - m.x) * ux + (e.y - m.y) * uy, off = Math.abs((e.x - m.x) * uy - (e.y - m.y) * ux);
                if (along < -6 || along > len + Js.Or(e.r, 12) || off > SPEAR_LINE_W + Js.Or(e.r, 12)) continue;
                hitEnemy(e, dmg, m.strikeA, 90, "minion", new HitOpts());
                if (e == te) hitT = true;
            }
            /* the one it was aimed at is hit even if it stepped a hair off the line */
            if (!hitT && te.state == "active" && Math.hypot(te.x - m.x, te.y - m.y) < K.reach + Js.Or(te.r, 12) + 12)
                hitEnemy(te, dmg, m.strikeA, 90, "minion", new HitOpts());
            return;
        }
        if (te.state == "active" && Math.hypot(te.x - m.x, te.y - m.y) < K.reach + Js.Or(te.r, 12) + 12)
            hitEnemy(te, minionDamage(m.kind, m), m.strikeA, 90, "minion", new HitOpts());
    }
    public static void minionShoot(Ent m, double tx, double ty)
    {
        double sx = m.x + (m.face != null ? m.face.x : 0) * 10, sy = m.y - 18;
        var ta = Math.atan2(ty - 14 - sy, tx - sx);
        /* a golem that throws: one bolt of its own element, at its own weight */
        if (m.kind == "golem")
        {
            var T = GOLEM_TYPES.GetValueOrDefault(m.gt ?? ""); var el = T != null && !string.IsNullOrEmpty(T.el) ? T.el : "fire";
            projectiles.Add(new Proj { kind = "mbolt", owner = "player", x = sx, y = sy - 8, px = sx, py = sy - 8,
                vx = Math.cos(ta) * MAGE_BOLT_SPD, vy = Math.sin(ta) * MAGE_BOLT_SPD, life = minionK(m).range * 1.15 / MAGE_BOLT_SPD,
                dmg = minionDamage("golem", m), el = el, srcKey = "m" + el, big = true });
            return;
        }
        var n = minionProj(); var mage = m.kind == "mage";
        var arc = n > 1 ? (mage ? Math.min(MAGE_FAN_MAX, MAGE_FAN * (n - 1)) : Math.min(1.5, 0.11 * (n - 1))) : 0;
        var more = Js.Or(player.minions.projMore, 1);
        for (int i = 0; i < n; i++)
        {
            var a = ta + (n > 1 ? (i / (n - 1) - 0.5) * arc : 0);
            if (m.kind == "mage")
            {
                projectiles.Add(new Proj { kind = "mbolt", owner = "player", x = sx, y = sy, px = sx, py = sy,
                    vx = Math.cos(a) * MAGE_BOLT_SPD, vy = Math.sin(a) * MAGE_BOLT_SPD, life = MAGE_RANGE * MAGE_BOLT_FLY / MAGE_BOLT_SPD,
                    dmg = minionDamage("mage", m) * more, el = m.el, srcKey = "m" + m.el });
            }
            else
            {
                projectiles.Add(new Proj { kind = "mbolt", arrow = true, owner = "player", x = sx, y = sy, px = sx, py = sy,
                    vx = Math.cos(a) * 640, vy = Math.sin(a) * 640, life = kindOf("bow").range * 1.15 / 640,
                    dmg = minionDamage("bow", m) * more, el = null, srcKey = "minion" });
            }
        }
        if (m.kind != "mage") SFX.whoosh();
    }
    public static void updateMinions(double dt)
    {
        var p = player;
        if (p == null) return;
        var M = p.minions;
        Ent h = heroObj ?? p;
        /* THE OVERSEER: while it is owed and not standing, it comes back on a clock */
        if (M.overseer && !minions.Any(m => m.kind == "overseer" && !m.gone))
        {
            M.overseerCd = Math.max(0, M.overseerCd - dt);
            if (M.overseerCd <= 0 && !h.dead) { raiseAt(h.x - 50, h.y + 20, "overseer"); }
        }
        /* THE GOLEMS: one short, and the next is back on its clock -- and what they
           grant follows who is standing */
        golemBuffSync();
        if (golems().Count < golemMax())
        {
            M.golemCd = Math.max(0, M.golemCd - dt);
            if (M.golemCd <= 0 && !h.dead) { raiseGolem(); if (golems().Count < golemMax()) M.golemCd = 0.6; }
        }
        var ov = minions.Find(m => m.kind == "overseer" && !m.gone && m.rise >= 1);
        foreach (var m in minions.Live())
        {
            m.lx = m.x; m.ly = m.y; m.ldt = dt;
            /* Convocation's mending */
            if (m.regenT > 0) { m.regenT -= dt; m.hp = Math.min(m.maxHp, m.hp + m.maxHp * CONVO_REGEN * dt); }
            m.flash = Math.max(0, m.flash - dt * 4);
            m.castT = Math.max(0, m.castT - dt);
            m.buffed = ov != null && m != ov && Math.hypot(m.x - ov.x, m.y - ov.y) < OVERSEER_R;
            if (m.rise < 1) { m.rise += dt / MINION_RISE; continue; }
            /* lost: too far from you, and it simply rises again at your side -- all but
               a mage, which stays where it stands until Convocation calls it */
            if (m.kind != "mage" && Math.hypot(m.x - h.x, m.y - h.y) > MINION_LEASH * 1.6)
            {
                var a0 = rnd(TAU); var at = floorAt(h.x + Math.cos(a0) * 60, h.y + Math.sin(a0) * 60, m.r + 4);
                m.x = at.x; m.y = at.y; m.rise = 0.4;
            }
            var K = minionK(m); var spd = MINION_SPD * K.spd * minionMoveMul();
            /* A GOLEM IN THE AIR finishes its leap before anything else */
            if (m.leap != null)
            {
                var L = m.leap; L.t += dt;
                var k = clamp(L.t / GOLEM_LEAP_T, 0, 1);
                m.x = L.fx + (L.tx - L.fx) * k; m.y = L.fy + (L.ty - L.fy) * k; m.z = Math.sin(k * Math.PI) * GOLEM_LEAP_H;
                m.moving = true;
                if (k >= 1) { m.leap = null; m.z = 0; collideWorld(m); golemLand(m); m.atkCd = minionRate(m.kind, m) * 0.5; }
                continue;
            }
            /* A SWORDSMAN'S CHARGE runs its course */
            if (m.charge != null)
            {
                var C = m.charge; var t0 = C.tgt; C.t -= dt;
                var te0 = t0 as Ent;
                if (t0 == null || t0.dead || (te0 != null && !string.IsNullOrEmpty(te0.state) && te0.state != "active") || C.t <= 0) { m.charge = null; }
                else
                {
                    double d0 = Math.hypot(t0.x - m.x, t0.y - m.y), reach0 = K.reach + Js.Or(t0.r, 12);
                    if (d0 > reach0) { minionStep(m, t0.x, t0.y, dt, spd * SWORD_CHARGE_SPD); if (Js.R() < 0.5) addDust(m.x, m.y + 2, 1); continue; }
                    m.charge = null; m.atkCd = 0;
                }
            }
            if (m.kind == "overseer")
            {
                /* it keeps to your shoulder and keeps the legion's blood up */
                double fx0 = h.x - 55, fy0 = h.y + 25;
                if (Math.hypot(fx0 - m.x, fy0 - m.y) > 20) minionStep(m, fx0, fy0, dt, spd * 1.2); else m.moving = false;
                m.pulseT = m.pulseT - dt;
                if (m.pulseT <= 0) { m.pulseT = 3; addRing(m.x, m.y, 10, 260, OVERSEER_R, "#ffd76e", 3, 0.6); m.castT = 0.4; }
                continue;
            }
            m.atkCd -= dt;
            m.leapCd = Math.max(0, m.leapCd - dt); m.chargeCd = Math.max(0, m.chargeCd - dt);
            /* A BLOW IN THE AIR finishes before anything else */
            if (m.strikeT > 0)
            {
                m.strikeT -= dt; m.moving = false;
                if (m.strikeT <= 0) { m.strikeT = 0; minionBlow(m); m.atkCd = minionRate(m.kind, m); }
                continue;
            }
            if (m.drawT > 0)
            {
                m.drawT -= dt; m.moving = false;
                if (m.drawT <= 0) { m.drawT = 0; if (m.shotAt != null) minionShoot(m, m.shotAt.x, m.shotAt.y); m.atkCd = minionRate(m.kind, m); }
                continue;
            }
            var e = minionTarget(m);
            var prop = e != null ? null : minionProp(m);
            Obj t = (Obj)e ?? prop;
            if (t != null)
            {
                double d = Math.hypot(t.x - m.x, t.y - m.y), a = Math.atan2(t.y - m.y, t.x - m.x);
                if (m.kind == "mage")
                {
                    /* THE MAGE STANDS AND CASTS. It never moves on its own -- not a step,
                       not a blink: it throws at whatever is in reach from where it rose,
                       and Convocation is how it is brought anywhere else. */
                    m.moving = false;
                    m.face = new Pt(Math.cos(a), Math.sin(a));
                    if (d <= K.range && m.atkCd <= 0)
                    {
                        m.castT = 0.18; minionShoot(m, t.x, t.y); m.atkCd = minionRate("mage", m);
                    }
                }
                else if (K.range != 0)
                {
                    /* THE BACK LINE. It does not plant its feet: in range it keeps a spot
                       in a loose arc on your side of the fight, and picks a new one every
                       second or two, shooting as it goes; anything that gets inside its
                       comfort, it steps back from; out of range, it closes. */
                    var want = prop != null ? 150 : K.range * 0.9;
                    if (prop != null)
                    {
                        if (d > want) minionStep(m, t.x, t.y, dt, spd); else { m.moving = false; m.face = new Pt(Math.cos(a), Math.sin(a)); }
                    }
                    else if (d > want)
                    {
                        minionStep(m, t.x, t.y, dt, spd); m.spot = null;
                    }
                    else if (d < K.keep * 0.6)
                    {
                        minionStep(m, m.x - Math.cos(a) * 40, m.y - Math.sin(a) * 40, dt, spd * 0.8); m.spot = null;
                    }
                    else
                    {
                        m.repoT = m.repoT - dt;
                        if (m.spot == null || m.repoT <= 0)
                        {
                            /* a fresh spot, never one it is already standing on */
                            var aT = Math.atan2(t.y - h.y, t.x - h.x);
                            double sx = m.x, sy = m.y;
                            for (int tries = 0; tries < 6 && Math.hypot(sx - m.x, sy - m.y) < 50; tries++)
                            {
                                var side = ((m.slot % 2) != 0 ? 1 : -1) * (0.35 + Js.R() * 0.9) * (tries % 2 != 0 ? -1 : 1);
                                var rr = 70 + Js.R() * 90;
                                sx = h.x - Math.cos(aT + side) * rr; sy = h.y - Math.sin(aT + side) * rr;
                                var ds = Math.hypot(sx - t.x, sy - t.y);
                                if (ds > K.range * 0.85) { var kk = K.range * 0.8 / ds; sx = t.x + (sx - t.x) * kk; sy = t.y + (sy - t.y) * kk; }
                            }
                            m.spot = floorAt(sx, sy, m.r + 2); m.repoT = 0.9 + Js.R() * 0.7;
                        }
                        if (Math.hypot(m.spot.x - m.x, m.spot.y - m.y) > 14) minionStep(m, m.spot.x, m.spot.y, dt, spd * 0.8);
                        else m.moving = false;
                        m.face = new Pt(Math.cos(a), Math.sin(a));
                    }
                    if (d <= Math.max(want, K.range) && m.atkCd <= 0)
                    {
                        if (m.kind == "mage") { m.castT = 0.18; minionShoot(m, t.x, t.y); m.atkCd = minionRate("mage", m); }
                        else { m.drawT = 0.3; m.shotAt = new Pt(t.x, t.y); }
                    }
                }
                else
                {
                    var reach = K.reach + Js.Or(t.r, 12);
                    /* a melee golem a few strides off LEAPS onto it */
                    if (m.kind == "golem" && prop == null && d > GOLEM_LEAP_MIN && d < GOLEM_LEAP_MAX && m.leapCd <= 0 && m.leap == null)
                    {
                        var land = floorAt(t.x - Math.cos(a) * (reach * 0.6), t.y - Math.sin(a) * (reach * 0.6), m.r + 2);
                        m.leap = new MinionLeap { t = 0, fx = m.x, fy = m.y, tx = land.x, ty = land.y }; m.leapCd = GOLEM_LEAP_CD; m.face = new Pt(Math.cos(a), Math.sin(a));
                        addDust(m.x, m.y, 4);
                        continue;
                    }
                    /* a swordsman some way off may CHARGE the last of the ground */
                    if (m.kind == "sword" && prop == null && d > SWORD_CHARGE_MIN && d < SWORD_CHARGE_MAX && m.chargeCd <= 0)
                    {
                        m.chargeCd = rnd(SWORD_CHARGE_CD[0], SWORD_CHARGE_CD[1]);
                        if (Js.R() < SWORD_CHARGE_CHANCE) { m.charge = new MinionCharge { tgt = t, t = SWORD_CHARGE_T }; addSparks(m.x, m.y - 14, 4, "#ede8ff"); continue; }
                    }
                    /* each comes at it from its own side, so a legion SURROUNDS a body
                       instead of queueing behind one another on the same line */
                    var fa = a + ((m.slot % 5) - 2) * 0.55;
                    if (d > reach) minionStep(m, t.x - Math.cos(fa) * (reach - 6), t.y - Math.sin(fa) * (reach - 6), dt, spd);
                    else { m.moving = false; m.face = new Pt(Math.cos(a), Math.sin(a)); }
                    if (d <= reach + 6 && m.atkCd <= 0)
                    {
                        m.strikeT = MINION_WIND * (m.kind == "golem" ? 1.8 : 1); m.strikeA = a; m.strikeTgt = t;
                    }
                }
            }
            else
            {
                /* nothing to fight: a place in a loose ring round you */
                double n = Math.max(1, minions.Count), ang = m.slot / n * TAU + vtime * 0.15;
                double r = K.range != 0 ? 90 : 62;
                double fx = h.x + Math.cos(ang) * r, fy = h.y + Math.sin(ang) * r * 0.75;
                if (m.kind == "mage") m.moving = false;
                else if (Math.hypot(fx - m.x, fy - m.y) > 18) minionStep(m, fx, fy, dt, spd * 1.1);
                else m.moving = false;
            }
        }
        /* A BOSS'S BODY. A boss does not stop to swing at bones; walking through a
           skeleton simply grinds it down */
        foreach (var m in minions.Live())
        {
            if (m.rise < 1 || m.gone) continue;
            foreach (var en in enemiesNear(m.x, m.y, 80, _minBuf).Live())
            {
                if (en.dead || en.state != "active" || en.type != "boss") continue;
                if (Math.hypot(en.x - m.x, en.y - m.y) > Js.Or(en.r, 30) + m.r) continue;
                m.hp -= Js.Or(en.dmg, 1) * dt / MINION_CONTACT; m.flash = Math.max(m.flash, 0.3);
            }
        }
        /* and they keep a little room between them */
        for (int i = 0; i < minions.Count; i++) for (int j = i + 1; j < minions.Count; j++)
            {
                var A = minions[i]; var B = minions[j];
                /* only a minion's OWN kind is in its way: skeletons walk through zombies and
                   golems, golems through skeletons, and so on */
                if (minionTribe(A) != minionTribe(B)) continue;
                double dx = B.x - A.x, dy = B.y - A.y, d = Math.hypot(dx, dy), want = A.r + B.r;
                /* a mage is a post: the other one steps round it, and two mages never shove */
                double fa = A.kind == "mage" ? 0 : B.kind == "mage" ? 1 : 0.5, fb = B.kind == "mage" ? 0 : A.kind == "mage" ? 1 : 0.5;
                if (d > 0.01 && d < want) { var k = (want - d) / d; A.x -= dx * k * fa; A.y -= dy * k * fa; B.x += dx * k * fb; B.y += dy * k * fb; }
                else if (d <= 0.01) { A.x -= 6 * fa; B.x += 6 * fb; }
            }
        foreach (var m in minions.Live()) if (m.hp <= 0 && !m.gone) minionDies(m);
        minions = minions.Where(m => !m.gone).ToList();
        /* a golem that just fell takes what it granted with it, this frame */
        golemBuffSync();
    }
    /* which minions keep room from each other: every skeleton (spear, sword, bow,
       mage) is one tribe; golems, zombies and the overseer are each their own */
    public static string minionTribe(Ent m) => (m.kind == "golem" || m.kind == "zombie" || m.kind == "overseer") ? m.kind : "skel";
    public static void minionDies(Ent m)
    {
        m.gone = true;
        spawnBricks(m.x, m.y, new[] { "#ede8ff", "#5b4a8f", "#3a2f5e" }, 6, 150);
        if (m.kind == "overseer") { player.minions.overseerCd = OVERSEER_BACK; return; }
        if (m.kind == "golem") player.minions.golemCd = Math.max(player.minions.golemCd, GOLEM_BACK);
        if (player.minions.instab)
        {
            var dmg = m.maxHp * INSTAB_SHARE * (1 + minionInc());
            addRing(m.x, m.y, 8, 360, INSTAB_R, "#ff8a3c", 6, 0.4);
            addSparks(m.x, m.y - 12, 10, "#ffb070");
            foreach (var e in enemiesNear(m.x, m.y, INSTAB_R + 30, _minBuf).ToList())
            {
                if (e.dead || e.state != "active") continue;
                if (Math.hypot(e.x - m.x, e.y - m.y) > INSTAB_R + Js.Or(e.r, 12)) continue;
                hitEnemy(e, dmg, Math.atan2(e.y - m.y, e.x - m.x), 160, "mburst", new HitOpts());
            }
        }
    }
    /* the zombies' badge: a green head, and the count */
    public static void drawZombieBadge(double x, double y, double n)
    {
        ctx.save(); ctx.translate(x, y - 2);
        ctx.fillStyle = "#86b25e"; ctx.strokeStyle = rgba(20, 20, 20, 0.6); ctx.lineWidth = 1.4;
        ctx.beginPath(); ctx.arc(0, -1, 9, 0, TAU); ctx.fill(); ctx.stroke();
        ctx.fillStyle = "#1a1a14";
        ctx.fillRect(-5, -4, 3, 3); ctx.fillRect(2, -4, 3, 3); ctx.fillRect(-4, 3, 8, 1.6);
        ctx.fillStyle = "#b7a6ff"; ctx.font = "800 11px Fredoka"; ctx.textAlign = "center";
        ctx.strokeStyle = rgba(15, 20, 25, 0.9); ctx.lineWidth = 3;
        ctx.strokeText(S(n), 11, 12); ctx.fillText(S(n), 11, 12);
        ctx.restore();
    }
    /* Convocation's badge: arrows turning in on a point */
    public static void drawConvoIcon(double x, double y)
    {
        ctx.save(); ctx.translate(x, y - 1);
        ctx.strokeStyle = "#b7a6ff"; ctx.fillStyle = "#b7a6ff"; ctx.lineWidth = 2.4; ctx.lineCap = "round";
        for (int k = 0; k < 3; k++)
        {
            var a = k / 3.0 * TAU + vtime * 0.8;
            double ox = Math.cos(a) * 11, oy = Math.sin(a) * 11, ix = Math.cos(a) * 4, iy = Math.sin(a) * 4;
            ctx.beginPath(); ctx.moveTo(ox, oy); ctx.lineTo(ix, iy); ctx.stroke();
            var ha = a + Math.PI;
            ctx.beginPath(); ctx.moveTo(ix, iy);
            ctx.lineTo(ix + Math.cos(ha + 2.5) * 4, iy + Math.sin(ha + 2.5) * 4); ctx.lineTo(ix + Math.cos(ha - 2.5) * 4, iy + Math.sin(ha - 2.5) * 4);
            ctx.closePath(); ctx.fill();
        }
        ctx.beginPath(); ctx.arc(0, 0, 2.2, 0, TAU); ctx.fill();
        ctx.restore();
    }
    /* the golems' badge: a square bone head, and the count */
    public static void drawGolemBadge(double x, double y, double n)
    {
        ctx.save(); ctx.translate(x, y - 2);
        ctx.fillStyle = "#d9d2f5"; ctx.strokeStyle = rgba(20, 20, 40, 0.6); ctx.lineWidth = 1.4;
        roundRect(ctx, -10, -11, 20, 18, 3); ctx.fill(); ctx.stroke();
        ctx.fillStyle = "#3a2f5e";
        ctx.fillRect(-6, -6, 4, 3); ctx.fillRect(2, -6, 4, 3); ctx.fillRect(-5, 1, 10, 2);
        ctx.fillStyle = "#b7a6ff"; ctx.font = "800 11px Fredoka"; ctx.textAlign = "center";
        ctx.strokeStyle = rgba(15, 20, 25, 0.9); ctx.lineWidth = 3;
        ctx.strokeText(S(n), 11, 12); ctx.fillText(S(n), 11, 12);
        ctx.restore();
    }
    /* the ability-row badge: a little skull, and the count across its brow */
    public static void drawLegionIcon(double x, double y, double n)
    {
        ctx.save(); ctx.translate(x, y - 2);
        ctx.fillStyle = "#ede8ff"; ctx.strokeStyle = rgba(20, 20, 40, 0.6); ctx.lineWidth = 1.4;
        ctx.beginPath(); ctx.arc(0, -2, 9, 0, TAU); ctx.fill(); ctx.stroke();
        roundRect(ctx, -5, 4, 10, 6, 2); ctx.fill(); ctx.stroke();
        ctx.fillStyle = "#3a2f5e";
        ctx.beginPath(); ctx.arc(-3.3, -2, 2.4, 0, TAU); ctx.arc(3.3, -2, 2.4, 0, TAU); ctx.fill();
        ctx.fillStyle = "#b7a6ff"; ctx.font = "800 11px Fredoka"; ctx.textAlign = "center";
        ctx.strokeStyle = rgba(15, 20, 25, 0.9); ctx.lineWidth = 3;
        ctx.strokeText(S(n), 11, 12); ctx.fillText(S(n), 11, 12);
        ctx.restore();
    }
    public static void drawMinion(Ctx c, Ent m)
    {
        if (m.rise < 1)
        {
            /* bones climbing out of the floor */
            var t = clamp(m.rise, 0, 1);
            for (int k = 0; k < 6; k++)
            {
                double a = k / 6.0 * TAU + t * 4, r = (1 - t) * 26;
                c.save(); c.translate(m.x + Math.cos(a) * r, m.y - 8 + Math.sin(a) * r * 0.5 - t * 6); c.rotate(a);
                c.globalAlpha = 0.4 + t * 0.6; c.fillStyle = k % 2 != 0 ? "#ede8ff" : "#5b4a8f";
                c.fillRect(-5, -3, 10, 6); c.restore();
            }
            c.globalAlpha = 1;
            return;
        }
        /* an ALLY: a violet ring at its feet, so it is never read as one of theirs */
        c.strokeStyle = rgba(183, 166, 255, 0.75); c.lineWidth = 2;
        c.beginPath(); c.ellipse(m.x, m.y + 2, m.r * 1.35, m.r * 0.55, 0, 0, TAU); c.stroke();
        if (m.buffed)
        {
            c.strokeStyle = rgba(255, 215, 110, 0.55); c.lineWidth = 1.5;
            c.beginPath(); c.ellipse(m.x, m.y + 2, m.r * 1.6, m.r * 0.68, 0, 0, TAU); c.stroke();
        }
        if (m.kind == "golem")
        {
            var g = m.gfx ??= new Ent { golem = GOLEM_TYPES.GetValueOrDefault(m.gt ?? "")?.skin ?? "bone", type = "minion" };
            if (m.z > 0) { c.fillStyle = rgba(0, 0, 0, 0.25); c.beginPath(); c.ellipse(m.x, m.y + 3, m.r * 1.3, m.r * 0.5, 0, 0, TAU); c.fill(); }
            g.x = m.x; g.y = m.y - m.z; g.scale = m.scale; g.moving = m.moving; g.walk = m.walk; g.phase = m.phase;
            g.face = m.face; g.flash = m.flash; g.mode = m.strikeT > 0 ? "stompWind" : "chase"; g.hp = m.hp; g.maxHp = m.maxHp; g.r = m.r;
            drawGolem(c, g);
        }
        else
        {
            var o = new Fig { x = m.x, y = m.y, s = m.kind == "overseer" ? 0.95 : 0.82, face = m.face, walk = m.walk, moving = m.moving, flash = m.flash,
                colors = m.kind == "overseer" ? OVERSEER_COLORS : MINION_COLORS, style = "skel", phase = m.phase, angry = false };
            if (m.kind == "mage")
            {
                o.weapon = "staff"; o.orbCol = MAGE_COL.GetValueOrDefault(m.el ?? "") ?? "#c9b8ff"; o.hoodCol = "#3a2f5e";
                if (m.castT > 0) { o.armsUp = true; o.castGlow = true; }
            }
            else if (m.kind == "overseer")
            {
                o.weapon = "staff"; o.orbCol = "#ffd76e"; o.hoodCol = "#2a2140"; o.lord = true;
                if (m.castT > 0) { o.armsUp = true; o.castGlow = true; }
            }
            else if (m.kind == "bow") { o.bowPull = m.drawT > 0 ? clamp(1 - m.drawT / 0.3, 0, 1) : 0; }
            else if (m.kind == "zombie")
            {
                o.colors = ZOMBIE_MINION_COLORS; o.style = "zombie"; o.noHelm = true;
                o.zombieArms = true; o.s = 0.84; if (m.strikeT > 0) o.armsUp = true;
                var Z = !string.IsNullOrEmpty(m.zk) ? ZOMBIE_HORDE_KINDS.GetValueOrDefault(m.zk) : null;
                if (Z != null)
                {
                    o.s = Z.s; if (Z.fat) o.fat = true;
                    if (Z.crawl)
                    {
                        drawCrawler(c, new Ent { x = m.x, y = m.y, scale = Z.s, walk = m.walk, face = m.face, flash = m.flash, moving = m.moving, col = ZOMBIE_MINION_COLORS });
                        o.skip = true;
                    }
                }
            }
            else if (m.kind == "sword") { o.weapon = "greatsword"; o.wScale = 0.7; if (m.strikeT > 0) o.armsUp = true; }
            else { o.weapon = "spear"; if (m.pike) o.wScale = 1.35; if (m.strikeT > 0) o.armsUp = true; }
            if (!o.skip) drawFigure(c, o);
        }
        if (m.hp < m.maxHp)
        {
            double w5 = 20, top = m.kind == "golem" ? 56 : 42;
            c.fillStyle = rgba(15, 20, 25, 0.6); c.fillRect(m.x - w5 / 2, m.y - top, w5, 3.5);
            c.fillStyle = "#b7a6ff"; c.fillRect(m.x - w5 / 2, m.y - top, w5 * clamp(m.hp / m.maxHp, 0, 1), 3.5);
        }
    }
    public static void drawScepterArt(Ctx c, double s, bool bright)
    {
        c.lineCap = "round";
        c.strokeStyle = rgba(20, 30, 40, 0.55); c.lineWidth = 4.6 * s;
        c.beginPath(); c.moveTo(0, 12 * s); c.lineTo(0, -12 * s); c.stroke();
        c.strokeStyle = bright ? "#d8ccff" : "#5b4a8f"; c.lineWidth = 3 * s;
        c.beginPath(); c.moveTo(0, 12 * s); c.lineTo(0, -12 * s); c.stroke();
        /* the skull on top, and the stone in its jaw */
        c.fillStyle = "#ede8ff"; c.strokeStyle = rgba(20, 30, 40, 0.6); c.lineWidth = 1.4 * s;
        c.beginPath(); c.arc(0, -16 * s, 5.2 * s, 0, TAU); c.fill(); c.stroke();
        c.fillStyle = "#2a2140";
        c.beginPath(); c.arc(-1.9 * s, -16.6 * s, 1.3 * s, 0, TAU); c.arc(1.9 * s, -16.6 * s, 1.3 * s, 0, TAU); c.fill();
        c.fillStyle = "#b7a6ff";
        c.beginPath(); c.arc(0, -12.4 * s, 1.6 * s, 0, TAU); c.fill();
    }
}

/* a minion's bolt names its damage source as a STRING in `src` ('mfire', 'minion'),
   where other projectiles keep the Ent that threw them there: the string lives here */
public sealed partial class Proj { public string srcKey; }
