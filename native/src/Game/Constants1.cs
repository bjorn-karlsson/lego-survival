namespace Brickblade;

/* =============================== CONSTANTS, part 1 (JS 1085–1773) =============================== */
public sealed class World { public double w, h, wall; }
public sealed class ArrowKind { public string name, to; public double conv, ail; public string col; }

public static partial class G
{
    public static readonly World W = new World { w = 5400, h = 4050, wall = 70 };
    public static readonly double CX = W.w / 2, CY = W.h / 2;
    public const int MAX_ALIVE = 512;
    public const int CS = 40;
    public const double RING_MAX = 320;
    public const double REV_MAX = 156;
    public const double MERGE_NEAR = 62, MERGE_DRIFT = 150, MERGE_CREEP = 42, MERGE_AGE = 3.0;
    public const double REGEN_STALL = 2.0;
    public const double MAGNET_SPEED = 1500;
    public const int HEARTS_PER_ROW = 16;
    public const double HEART_MAX = 999, HEART_POOL_AT = 99;
    public static readonly string[] HEART_ROW_COLS = { "#d3242b", "#ffb900", "#9b45d6", "#2ea8a0", "#e4572e", "#4b9b41", "#2e7dd1", "#c95bd0", "#d3242b" };
    public static readonly Tbl BOMB_DIRS = new double[] { 0, Math.PI / 2, -Math.PI / 2, Math.PI, Math.PI / 4, -Math.PI / 4, 3 * Math.PI / 4, -3 * Math.PI / 4 };
    public static readonly int BOMB_MAX = BOMB_DIRS.Length;
    public const int PUMPKIN_LIVE_CAP = 12, FROST_TRAIL_CAP = 80;
    public const double PUMPKIN_MIN_RATE = 4.0, BOMB_MIN_RATE = 2.0, BOMB_MAX_R = 110;
    public const double REACH_MAX = 62, ORBIT_MAX_R = 190, ORBIT_MAX_SIZE = 20, STORM_MAX_SIZE = 2.2;
    public const double STORM_MAX_BLADES = 150, VORTEX_MAX_R = 210, FREEZE_MAX_R = 290;
    /* THE ARROW */
    public const int ARROW_MAX_N = 9;
    public const double ARROW_SPREAD = 0.14, ARROW_LIFE = 0.95;
    public const int ARROW_MAX_PIERCE = 12, ARROW_PIERCE_ALL = 99, ARROW_FORK_N = 4;
    public const double ARROW_FORK_R = 190, ARROW_FORK_SHARE = 0.5, ARROW_BLAST_R = 80, ARROW_BLAST_LESS = 0.45;
    public static readonly JsMap<string, ArrowKind> ARROW_KINDS = new()
    {
        ["light"] = new ArrowKind { name = "Lightning Arrow", to = "shock", conv = 0.75, ail = 0.25, col = "#ffd84a" },
        ["fire"] = new ArrowKind { name = "Fire Arrow", to = "fire", conv = 0.80, ail = 0.20, col = "#ff7a2a" },
        ["chaos"] = new ArrowKind { name = "Chaos Arrow", to = "chaos", conv = 0.75, ail = 0.25, col = "#8ede4a" },
    };
    public static readonly HashSet<string> ARROW_SOURCES = new() { "arrow", "arrowfork", "arrowblast" };
    public const int RICO_MAX = 2; public const double RICO_R = 260;
    public const double ARROW_SHOOT_R = 6;
    public static double pierceFall(double n)
    {
        if (n <= 0) return 0;
        double t = Math.Min(1, (n - 1) / Math.Max(1, ARROW_MAX_PIERCE - 1));
        return 0.15 + (0.10 - 0.15) * t;
    }
    public const int ORBIT_MAX_N = 15;
    public const double FREEZE_MIN_RATE = 2.2;
    /* ACCURACY AND EVASION: chance to hit = acc / (acc + (eva/4)^0.8), floored */
    public const double HIT_FLOOR = 0.15;
    public static double hitChance(double acc, double eva)
    {
        if (!(eva > 0)) return 1;
        if (!(acc > 0)) return HIT_FLOOR;
        return clamp(acc / (acc + Math.Pow(eva / 4, 0.8)), HIT_FLOOR, 1);
    }
    public const double EVA_BASE = 200, EVA_FROM_SKILL = 3, EVA_ELITE = 1.25, EVA_MAGIC = 1.10;
    public static double mobEvasionOf(EDef d)
    {
        if (d == null) return 0;
        double spd = Js.Or(d.speed, 60), sc = Js.Or(d.scale, 1);
        double b = EVA_BASE * (spd / 80) / Math.Max(0.6, sc);
        return Math.Round(b * (1 + d.evade * EVA_FROM_SKILL));
    }
    public const double ACC_MOB_BASE = 700;
    public static double mobAccuracyOf(EDef d)
    {
        if (d == null) return ACC_MOB_BASE;
        double spd = Js.Or(d.speed, 60);
        double k = (d.type == "boss" || d.boss) ? 2.2 : 1;
        return Math.Round(ACC_MOB_BASE * (0.6 + spd / 140) * k);
    }
    /* ENERGY SHIELD: takes the hit first, comes back by itself, chaos goes through */
    public const double ES_DELAY = 2.5;
    public const double CI_ES_PER_HEART = 2;
    public const double ES_RATE = 0.22;
    public const string ES_COL = "#7fd8ff";
    public static double esAbsorb(Ent o, double n, string type, double chaosShare)
    {
        if (o == null) return n;
        o.esT = ES_DELAY;
        double cs = type == "chaos" ? 1 : clamp(chaosShare, 0, 1);
        double through = n * cs, stopped = n - through;
        double have = o.es;
        if (have <= 0) return n;
        double eaten = Math.Min(have, stopped);
        o.es = Math.Round((have - eaten) * 100) / 100;
        return Math.Max(0, through + (stopped - eaten));
    }
    public static void esTick(Ent o, double dt)
    {
        if (o == null || !(o.esMax > 0)) return;
        if (o.esT > 0) { o.esT -= dt; return; }
        if (o.es >= o.esMax) return;
        o.es = Math.Min(o.esMax, o.es + o.esMax * ES_RATE * dt);
    }
    public static double chaosShareOf(string source, NumMap mix)
    {
        if (mix != null) return mix["chaos"];
        return convFrom(source) == "chaos" ? 1 : 0;
    }
    public const double EVADE_CAP = 0.55;
    public const double ZORB_SPD = 118, ZORB_SPIN = 2.1, ZORB_ARM = 165, ZORB_ZAP_GAP = 0.11;
    public const int ZORB_STACKS = 5;
    public static double chainGap()
    {
        var p = player;
        if (p == null || p.chain == null) return 0.2;
        double z = Math.Max(1, Math.Min(chainMaxZappers(), p.chain.zappers));
        return p.chain.rate / z;
    }
    public const double CHAIN_JUMP_GAP = 0.1;
    public const int CHAIN_MAX_NODES = 40, CHAIN_MAX_LEAPS = 15;
    public static int chainMaxTargets() => CHAIN_MAX_LEAPS + 1;
    public const int CHAIN_FORK_MAX = 2, WHIRL_MAX = 2;
    /* REAVING */
    public const int REAVE_MAX = 2, REAVE_BOUNCES = 3;
    public const double REAVE_MUL = 0.70;
    public static double reaveMul() => REAVE_MUL + (player != null ? player.reavePlus : 0);
    public static double reaveLegs() => REAVE_BOUNCES + (player != null ? player.reaveLegs : 0);
    public const double REAVE_SPEED = 760, REAVE_SEEK = 320, REAVE_SOLO_PAST = 150, REAVE_CD = 0.16;
    public const int REAVE_AIR = 4, REAVE_AIR_R2 = 6;
    public const double REAVE_LIFE = 2.4;
    public const double WHIRL_SPIN = 13, WHIRL_TICK = 0.18, WHIRL_MUL = 0.5;
    public static double whirlShare() => WHIRL_MUL + (player != null ? player.whirlPlus : 0);
    public static readonly Tbl WHIRL_SLOW = new double[] { 1, 1.0, 1.5 };
    public static readonly Tbl WHIRL_DEFLECT = new double[] { 0, 0.35, 0.55 };
    public const double WHIRL_REACH_MUL = 1.5;
    public static double whirlReach()
    {
        var p = player;
        if (p == null || weapon == null) return 0;
        return (weapon.reach * (1 + stackMod("reach")) + p.reach) * WHIRL_REACH_MUL;
    }
    public static readonly Tbl BM_SPEED = new double[] { 0, 0.10, 0.10 };
    public static readonly Tbl BM_REACH = new double[] { 0, 10, 12 };
    public static readonly Tbl BM_ATK = new double[] { 0, 0.10, 0.12 };
    public const int TOTEM_MAX = 2;
    public static readonly Tbl TOTEM_EVERY = new double[] { 0, 6, 3 };
    public static readonly Tbl TOTEM_LIFE = new double[] { 0, 9, 12 };
    public static readonly Tbl TOTEM_MUL = new double[] { 0, 1.0 / 3, 0.55 };
    public const double TOTEM_TICK = 0.3, FREEZE_STUN_T = 4.5;
    /* THE GUARDIAN GUARDS: Aegis Ring and Twin Ring */
    public const double AEGIS_REFORM = 2.4, AEGIS_BLOCK_R = 5, TWIN_GAP_MIN = 24;
    public static double twinRadius(Spell orb) => orb.r + Math.Max(orb.size * 2 + TWIN_GAP_MIN, orb.r * 0.38);
    public static bool aegisBlock(Proj pr)
    {
        var orb = player?.orbit;
        if (orb == null || !Js.T(orb.aegis) || !(orb.n > 0) || orb.pos.Count == 0) return false;
        double px = !double.IsNaN(pr.px) ? pr.px : pr.x, py = !double.IsNaN(pr.py) ? pr.py : pr.y;
        double catchR = orb.size + AEGIS_BLOCK_R + (!double.IsNaN(pr.hitR) ? pr.hitR : 7 + (pr.big ? 5 : 0));
        foreach (var op in orb.pos)
        {
            if (op.broken > 0) continue;
            if (segDist(px, py, pr.x, pr.y, op.x, op.y) > catchR) continue;
            (op.outer ? orb.brk2 : orb.brk)[op.i] = AEGIS_REFORM;
            op.broken = AEGIS_REFORM;
            pr.dead = true;
            orb.blocked = orb.blocked + 1;
            addSparks(pr.x, pr.y, 7, "#ffe08a");
            addRing(op.x, op.y, 4, 220, 26, "#ffd166", 4, 0.3);
            SFX.deflect();
            return true;
        }
        return false;
    }
    public static double heartCap() => Math.Round(HEART_MAX * Js.Or(player.hpMore, 1));
    /* THE POOL: (base + flat) x (1 + increases added together) x each MORE */
    public const double HP_BASE = 6, SPEED_BASE = 215, ATK_BASE = 0.34, DASH_BASE = 0.9, MAGNET_BASE = 95;
    public const double ORBIT_SPD_BASE = 1.9;
    public const double CHAIN_RATE_BASE = 2.0, STORM_RATE_BASE = 6.5;
    public const double VORTEX_RATE_BASE = 9, FREEZE_RATE_BASE = 5.5, BOMB_RATE_BASE = 7;
    /* BRICKBANE */
    public const double BANE_RATE_BASE = 4.2, BANE_MIN_RATE = 1.1, BANE_R_BASE = 150, BANE_R_MAX = 430;
    public static readonly double BANE_ARC_BASE = 35 * Math.PI / 180, BANE_ARC_MAX = TAU;
    public const double BANE_DOT_BASE = 1.2, BANE_DOT_T = 8, BANE_DOT_MAX_T = 16, BANE_TICK = 0.5;
    public const double BANE_DRAIN_TICK = 1.0 / 30;
    public const int BANE_STACK_MAX = 3;
    public static readonly Tbl BANE_BURST = new double[] { 0, 0.80, 1.00 };
    public const int POISON_MAX = 9, POISON_MAX_BOSS = 12;
    public static readonly Tbl POISON_CAP_MUL = new double[] { 1, 2, 4 };
    public static readonly Tbl BANE_BURST_R = new double[] { 0, 130, 210 };
    public const double BANE_BURST_KEEP = 0.7, BANE_WAVE_T = 0.42;
    public const double BOMB_R_BASE = 62;
    public static double pool(double b, double flat, double inc, double more) => (b + flat) * (1 + inc) * Js.Or(more, 1);
    public static double paceOf(double b, double inc, double floor) => Math.Max(floor, b / (1 + inc));
    public static double castInc(double pl)
    {
        return pl + (player?.inc?["cast"] ?? 0) + (weapon != null ? weapon.incAtk : 0) + stackMod("cast");
    }
    public static double dotRate(double b, double flat, bool chaos = false)
    {
        double inc = dotInc() + (chaos && player != null ? player.inc["chaos"] : 0);
        return pool(b, flat, Math.Min(INC_DMG_CAP, inc), player != null ? Js.Or(player.dotMore, 1) : 1);
    }
    public static bool hasAnyDot() => player != null && player.bane != null && player.bane.n > 0;
    public static void syncStats()
    {
        var p = player;
        if (p == null) return;
        var i = p.inc;
        double intHp = p.heartsPerInt * Math.Floor(intOf(p) / (5 * ATTR_K));
        p.maxHp = Math.Max(1, Math.Min(heartCap(),
            Math.Round(pool(HP_BASE, p.hpFlat + intHp, i["hp"] + strOf(p) * ATTR_STR_HP, p.hpMore))));
        if (p.hp > p.maxHp) p.hp = p.maxHp;
        p.speed = Math.Min(SPEED_MAX, pool(SPEED_BASE, 0, i["speed"], 1));
        p.magnetR = Math.Min(MAGNET_MAX, pool(MAGNET_BASE, 0, i["magnet"], 1));
        p.studMul = pool(1, 0, i["studs"], 1);
        p.atkRate = paceOf(weapon.rate, i["atk"] + weapon.incAtk + dexOf(p) * ATTR_DEX_ATK, atkFloor());
        p.critBase = weapon.crit;
        p.dash.cdMax = paceOf(DASH_BASE, i["roll"], DASH_CD_MIN);
        p.orbit.spd = Math.Min(ORBIT_MAX_SPD, pool(ORBIT_SPD_BASE, 0, castInc(i["orbitSpd"]), 1));
        p.chain.rate = paceOf(CHAIN_RATE_BASE, castInc(i["chainRate"]), CHAIN_MIN_RATE);
        p.storm.rate = paceOf(STORM_RATE_BASE, castInc(i["stormRate"]), STORM_MIN_RATE);
        p.vortex.rate = paceOf(VORTEX_RATE_BASE, castInc(i["vortexRate"]), VORTEX_MIN_RATE);
        p.vortex.hitMul = Math.Max(VORTEX_MIN_HITMUL, 1 / (1 + i["vortexRate"] * 0.5));
        p.freeze.rate = paceOf(FREEZE_RATE_BASE, castInc(i["freezeRate"]), FREEZE_MIN_RATE);
        p.bomb.rate = paceOf(BOMB_RATE_BASE, castInc(i["bombRate"]), BOMB_MIN_RATE);
        p.bomb.r = Math.Min(BOMB_MAX_R, pool(BOMB_R_BASE, 0, i["bombR"], 1));
        p.bane.rate = paceOf(BANE_RATE_BASE, castInc(i["baneRate"]), BANE_MIN_RATE);
        p.bane.r = Math.Min(BANE_R_MAX, pool(BANE_R_BASE, 0, i["baneR"], 1));
        p.bane.dot = dotRate(BANE_DOT_BASE, p.baneDotFlat, true);
        p.bleedChance = clamp(p.bleedChanceFlat, 0, 1);
        p.ailChance = clamp(p.ailChanceFlat, 0, AIL_CHANCE_MAX);
        p.ailType = new NumMap();
        foreach (var k in AIL_TYPE_KEYS) p.ailType[k] = clamp(p.ailTypeFlat != null ? p.ailTypeFlat[k] : 0, 0, AIL_TYPE_MAX);
        p.exposeChance = EXPOSE_ON ? clamp(p.exposeChanceFlat, 0, EXPOSE_CHANCE_MAX) : 0;
        {
            double was = p.esMax;
            p.ciEs = 0;
            if (p.chaosInoc)
            {
                p.ciEs = Math.Max(0, p.maxHp - 1) * CI_ES_PER_HEART;
                p.maxHp = 1;
                if (p.hp > 1) p.hp = 1;
            }
            p.esMax = Math.Max(0, (p.esFlat + p.ciEs) * (1 + p.inc["es"] + intOf() * ATTR_INT_ES));
            p.es = was <= 0 ? p.esMax : Math.Min(p.es, p.esMax);
        }
        {
            double was = p.maxMana;
            p.maxMana = Math.Max(0, (MANA_BASE + MANA_PER_LVL * (Js.Or(p.level, 1) - 1) + intOf() * MANA_PER_INT + p.manaFlat)
                                    * (1 + p.inc["mana"]));
            p.mana = was <= 0 ? p.maxMana : Math.Min(p.mana, p.maxMana);
            p.manaRegen = Math.Max(0, (p.maxMana * MANA_REGEN_PCT + p.manaRegenFlat) * (1 + p.inc["manaRegen"]));
        }
        p.bleedShare = Math.Min(BLEED_SHARE_MAX, BLEED_SHARE_BASE + p.bleedShareFlat);
        p.armour = pool(0, p.armourFlat, 0, 1);
        p.reach = Math.Min(REACH_MAX, pool(0, p.reachFlat, 0, 1));
        p.suppress = Math.Min(1, pool(0, p.suppressFlat, 0, 1));
        p.critMultPct = Math.Min(CRIT_MULT_CAP,
            pool(weapon.critMult, p.critMultFlat, p.incCritMult + dexOf(p) * ATTR_DEX_CMULT, 1));
        p.regenRate = p.noRegen ? 0
            : Math.Min(REGEN_MAX, pool(0, p.regenFlat, i["regen"] + intOf(p) * ATTR_INT_REGEN, 1));
        p.heartChance = Math.Min(HEART_CHANCE_MAX, i["heart"]);
        p.xpMul = pool(1, 0, p.xpInc, p.xpMore);
        foreach (var el in RES_KINDS) p.res[el] = pool(0, p.resFlat[el], 0, 1);
        if (p.slam != null)
        {
            p.slamArc = Math.Min(slamArcCap(), SLAM_ARC_BASE + p.slam.arcFlat);
            p.slamRange = Math.Min(SLAM_RANGE_MAX, pool(SLAM_RANGE_BASE, 0, p.slam.rangeInc, 1));
            p.slamSpeed = Math.Min(SLAM_SPEED_MAX, pool(SLAM_SPEED_BASE, 0, p.slam.spdInc, 1));
        }
        p.leech = Math.Min(LEECH_MAX, pool(0, p.leechFlat, 0, 1) + weaponLeech());
        p.leechChance = Math.Min(LEECH_CHANCE_MAX, pool(0, p.leechChanceFlat, 0, 1) + weaponLeechChance());
    }
    public static double addMaxHp(double flat)
    {
        var p = player; double before = p.maxHp;
        p.hpFlat += flat;
        syncStats();
        double gained = p.maxHp - before;
        if (gained > 0) p.hp = Math.Min(p.maxHp, p.hp + gained);
        return gained;
    }
    public const double ATK_RATE_MIN = 0.10, CAST_RATE_MIN = 0.34, BOW_RATE_MIN = 0.22;
    public static double atkFloor() => (isCaster() || isSummoner()) ? CAST_RATE_MIN : isArcher() ? BOW_RATE_MIN : ATK_RATE_MIN;
    public const double SPEED_MAX = 430, DASH_CD_MIN = 0.30, MAGNET_MAX = 420, REGEN_MAX = 0.70;
    public const double REGEN_REF_HP = 6, REGEN_SCALE_POW = 0.55;
    public static double regenScale()
    {
        double hp = player != null ? player.maxHp : REGEN_REF_HP;
        return Math.Max(1, Math.Pow(hp / REGEN_REF_HP, REGEN_SCALE_POW));
    }
    public const double REGEN_BREAK_MUL = 2, HEART_CHANCE_MAX = 2.0, LOW_LIFE_FRAC = 0.10;
    public static bool lowLife() { var p = player; return p != null && p.maxHp > 0 && p.hp <= p.maxHp * LOW_LIFE_FRAC; }
    public const double HEART_PER_LVL = 0.03;
    public static double heartLvlMul() => 1 + HEART_PER_LVL * Math.Max(0, mobLevel() - 1);
    public static double heartDropChance() => DROP["heart"] * (1 + (player != null ? player.heartChance : 0)) * heartLvlMul();
    public const double STORM_MIN_RATE = 2.00, VORTEX_MIN_RATE = 3.00;
    public const int BOOMER_MAX = 2;
    public static readonly Tbl BOOMER_LEGS = new double[] { 0, 1, 3 };
    public const int CLUSTER_MAX = 2;
    public const double CLUSTER_FALLOFF = 0.45;
    public const double FORTUNE_CAP = 2, RAGE_CAP = 2, BUFF_T = 30, BUFF_T_MAX = 150;
    /* a buff picked up while it is already as strong as it gets adds time instead */
    public static double buffExtend(Ent p, string key, bool full)
    {
        string tk = key + "T", mk = key + "Max";
        double had = Math.Max(0, Fields.Num(p, tk));
        if (full && had > 0)
        {
            Fields.SetNum(p, tk, Math.Min(BUFF_T_MAX, had + BUFF_T));
            Fields.SetNum(p, mk, Math.Max(BUFF_T, Fields.Num(p, tk)));
            return Fields.Num(p, tk) - had;
        }
        Fields.SetNum(p, tk, Math.Max(had, BUFF_T));
        Fields.SetNum(p, mk, Math.Max(BUFF_T, Fields.Num(p, tk)));
        return 0;
    }
    public const double HEART_REGEN_T = 30, HEART_REGEN_MUL = 1.5;
    public const int HEART_STACK_MAX = 5;
    public static readonly Tbl HEART_STACK_MUL = new double[] { 1, 1.5, 1.85, 2.10, 2.30, 2.45 };
    public const int CHAIN_MAX_ZAPPERS = 5;
    public static double capUp(string k) => player != null && player.capUp != null ? player.capUp[k] : 0;
    public static double orbitMaxN() => ORBIT_MAX_N + capUp("orbit");
    public static double stormMaxN() => Math.Max(1, Math.Floor(STORM_MAX_BLADES / Math.Max(1, player.storm.count)));
    public static double chainMaxZappers() => CHAIN_MAX_ZAPPERS + capUp("chain");
    public static double bombMaxN() => BOMB_MAX + capUp("bomb");
    public const double CHAIN_MIN_RATE = 1.0;
    /* chain falloff: a curve that buying leaps flattens */
    public const double CHAIN_SPEC_FULL = 9;
    public const double CHAIN_START_MIN = 0.55, CHAIN_START_MAX = 0.85, CHAIN_FLOOR_MIN = 0.40, CHAIN_FLOOR_MAX = 0.75;
    public static double chainSpec(double leaps) => clamp((leaps - 1) / (CHAIN_SPEC_FULL - 1), 0, 1);
    public static double chainLeapStart(double leaps) => CHAIN_START_MIN + (CHAIN_START_MAX - CHAIN_START_MIN) * chainSpec(leaps);
    public static double chainLeapFloor(double leaps) => CHAIN_FLOOR_MIN + (CHAIN_FLOOR_MAX - CHAIN_FLOOR_MIN) * chainSpec(leaps);
    public static double chainLeapFrac(double i, double leaps)
    {
        double start = chainLeapStart(leaps);
        if (leaps <= 0) return start;
        double floor = chainLeapFloor(leaps);
        if (leaps == 1) return floor;
        double t = Math.Pow((i - 1) / (leaps - 1), 0.8);
        return start + (floor - start) * t;
    }
    public const double ORBIT_MAX_SPD = 5.2;
    public static readonly JsMap<string, double> SPELL_FLOOR = new()
    {
        ["chain"] = CHAIN_MIN_RATE, ["storm"] = STORM_MIN_RATE, ["vortex"] = VORTEX_MIN_RATE,
        ["freeze"] = FREEZE_MIN_RATE, ["bomb"] = BOMB_MIN_RATE, ["bane"] = BANE_MIN_RATE
    };
    public static readonly JsMap<string, string> SPELL_NAME = new()
    {
        ["orbit"] = "Guardian Brick", ["chain"] = "Storm Brick", ["storm"] = "Bladestorm", ["vortex"] = "Blade Vortex",
        ["freeze"] = "Block Freeze", ["bomb"] = "Bomb Volley", ["bane"] = "Brickbane"
    };
    public static bool castRoom()
    {
        var p = player;
        if (p == null) return false;
        foreach (var (k, fl) in SPELL_FLOOR)
        {
            var sp = p.Spell(k);
            if (sp != null && sp.n > 0 && sp.rate > fl + 1e-9) return true;
        }
        return p.orbit != null && p.orbit.n > 0 && p.orbit.spd < ORBIT_MAX_SPD - 1e-9;
    }
    public const double VORTEX_MIN_HITMUL = 0.45, VORTEX_MAX_SPIN = 7;
    public const double WARDEN_R = 240, WARDEN_HEAL = 0.055;
    public static readonly Tbl MAGIC_PACK = new double[] { 6, 12 };
    public static readonly Tbl ELITE_COURT = new double[] { 4, 9 };
}
