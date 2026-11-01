namespace Brickblade;

/* =============================== WAVES / THE ROSTER (JS 8648–9266) ===============================
   A wave is a BUDGET: every monster has a price in bricks. The roster is banded
   by era and permuted within each band every run; wave modifiers are rolled
   once per wave; the wave state machine runs ready -> fight -> cleared. */
public sealed class RosterEntry { public string k; public double at, cost, flavour, baseAt; }
public sealed class WaveMod
{
    public string id, name, col, tip; public Action<Ent> apply; public Func<double, double> budget; public double elite;
}
public sealed class PackCaps { public double magic, elite; }
public sealed class LeaguePlan { public int breach, abyss, ritual, legion; }
public sealed partial class Wave
{
    public List<string> queue = new();
    public double magicN, eliteN, t, spawnT, raidIdx, readyT, readyMax, callT, callIn, merged, owed, done;
    public bool callLatch;
}

public static partial class G
{
    public static readonly List<RosterEntry> ROSTER = new()
    {
        new() { k = "imp", at = 1, cost = 1, flavour = 1 },
        new() { k = "zombie", at = 1, cost = 1, flavour = 1 },
        new() { k = "bloater", at = 6, cost = 5, flavour = 0.9 },
        new() { k = "zrunner", at = 13, cost = 9, flavour = 1 },
        new() { k = "zhulk", at = 18, cost = 14, flavour = 0.9 },
        new() { k = "boneling", at = 2, cost = 1, flavour = 1 },
        new() { k = "archer", at = 3, cost = 2, flavour = 1 },
        new() { k = "skeleton", at = 5, cost = 3, flavour = 1.2 },
        new() { k = "spitter", at = 4, cost = 3, flavour = 1 },
        new() { k = "shade", at = 6, cost = 4, flavour = 0.9 },
        new() { k = "cryptbat", at = 7, cost = 5, flavour = 1 },
        new() { k = "brute", at = 8, cost = 6, flavour = 1 },
        new() { k = "knight", at = 9, cost = 8, flavour = 1 },
        new() { k = "stalker", at = 11, cost = 9, flavour = 1 },
        new() { k = "lancer", at = 12, cost = 10, flavour = 1 },
        new() { k = "bannerman", at = 12, cost = 9, flavour = 0.8 },
        new() { k = "superimp", at = 15, cost = 11, flavour = 1 },
        new() { k = "hexer", at = 14, cost = 12, flavour = 0.8 },
        new() { k = "revenant", at = 16, cost = 14, flavour = 1 },
        new() { k = "lobber", at = 17, cost = 15, flavour = 0.9 },
        new() { k = "witchdoctor", at = 16, cost = 13, flavour = 0.85 },
        new() { k = "blightspitter", at = 12, cost = 11, flavour = 0.85 },
        new() { k = "pyromancer", at = 19, cost = 16, flavour = 0.9 },
        new() { k = "frostmage", at = 21, cost = 16, flavour = 0.9 },
        new() { k = "necromancer", at = 22, cost = 18, flavour = 0.8 },
        new() { k = "stormcaller", at = 24, cost = 18, flavour = 0.9 },
        new() { k = "jugg", at = 26, cost = 20, flavour = 1 },
        new() { k = "marksman", at = 27, cost = 20, flavour = 0.9 },
        new() { k = "bastion", at = 29, cost = 22, flavour = 1 },
        new() { k = "bomber", at = 13, cost = 25, flavour = 1 },
        new() { k = "direbat", at = 31, cost = 26, flavour = 0.9 },
        new() { k = "pumpkinlord", at = 32, cost = 28, flavour = 0.8 },
        new() { k = "shadestorm", at = 34, cost = 30, flavour = 0.8 },
        new() { k = "pyrolord", at = 36, cost = 40, flavour = 0.7 },
        new() { k = "frostlord", at = 37, cost = 40, flavour = 0.7 },
        new() { k = "stormlord", at = 38, cost = 42, flavour = 0.7 },
        new() { k = "necrolord", at = 39, cost = 45, flavour = 0.7 },
        new() { k = "greenwarden", at = 23, cost = 40, flavour = 0.65 },
        new() { k = "impmaster", at = 33, cost = 50, flavour = 0.7 },
        new() { k = "overseer", at = 39, cost = 55, flavour = 0.6 },
    };
    /* SANDBOX: waves stop coming and nothing spawns on its own */
    public static bool sandbox = false;
    public static readonly NumMap MONSTER_COST = BuildMonsterCost();
    static NumMap BuildMonsterCost()
    {
        var m = new NumMap();
        foreach (var r in ROSTER) m[r.k] = r.cost;
        /* siege units never travel through the roster, so they need their price set by hand */
        m["stonegolem"] = 30; m["icegolem"] = 32;
        m["firegolem"] = 34; m["stormgolem"] = 36;
        foreach (var r in ROSTER) r.baseAt = r.at;
        return m;
    }
    /* The roster is banded by ERA, and each run permutes who arrives when WITHIN its band */
    public static readonly string[][] ROSTER_BANDS =
    {
        new[] { "imp", "boneling", "archer", "spitter", "skeleton" },
        new[] { "shade", "cryptbat", "brute", "knight", "bloater" },
        new[] { "stalker", "lancer", "superimp", "hexer", "bomber", "zrunner", "bannerman" },
        new[] { "revenant", "lobber", "pyromancer", "frostmage", "zhulk", "witchdoctor" },
        new[] { "necromancer", "stormcaller", "jugg", "marksman", "greenwarden" },
        new[] { "bastion", "direbat", "pumpkinlord", "impmaster" },
        new[] { "shadestorm", "pyrolord", "frostlord", "stormlord", "necrolord", "overseer" },
    };
    /* The unlock waves a band owns are fixed; only WHO fills each one is rolled */
    public static void rollRoster()
    {
        foreach (var band in ROSTER_BANDS)
        {
            var members = new List<RosterEntry>();
            foreach (var k in band) { var r = ROSTER.FirstOrDefault(x => x.k == k); if (r != null) members.Add(r); }
            if (members.Count < 2) continue;
            var slots = members.Select(r => r.baseAt).ToList().SortJs((a, b) => a - b);
            shuffleArr(members);
            for (int i = 0; i < members.Count; i++) members[i].at = slots[i];
        }
    }
    /* Rank order for the pivot: sorted by cost for the walk-up */
    public static readonly List<RosterEntry> RANKS = ROSTER.ToList().SortJs((a, b) => Js.Or(a.cost - b.cost, a.at - b.at));
    public const int MAX_WAVE_BODIES = 320;
    /* Every wave now opens with a breath */
    public const double WAVE_BREAK = 6;
    public const double WAVE_BREAK_BOSS = 30;
    /* CALLING THE NEXT WAVE DOWN ON YOURSELF */
    public const double CALL_HOLD = 0.7, CALL_DELAY = 3, CALL_MAX = 3;
    /* Where the run turns into the grand raid */
    public const double RAID_WAVE = 50;
    public const double PIVOT_HOLD = 13, PIVOT_EVERY = 3, PIVOT_KEEP = 5;
    /* How far up the rank list the floor has climbed */
    public static int wavePivot(double n)
    {
        if (n < PIVOT_HOLD) return 0;
        return (int)Math.Min(RANKS.Count - PIVOT_KEEP, Math.Floor((n - PIVOT_HOLD) / PIVOT_EVERY) + 1);
    }
    /* Bricks to spend: the quadratic term has to outrun the pivot */
    public static double waveBudget(double n) => Math.Round((14 + n * 10 + n * n * 1.7) * difficulty.count);
    /* The weight is set from the price so every rank gets roughly the same slice of the BUDGET */
    public static List<(string k, double w)> wavePool(double n)
    {
        int pivot = wavePivot(n);
        var outL = new List<(string, double)>();
        for (int i = 0; i < RANKS.Count; i++)
        {
            var r = RANKS[i];
            if (n < r.at) continue;
            if (i < pivot) continue;                        /* this rank has aged out */
            double fade = i == pivot ? 0.35 : i == pivot + 1 ? 0.7 : 1;
            double w = Math.Max(1, Math.Round(64 / r.cost * r.flavour * fade));
            outL.Add((r.k, w));
        }
        if (outL.Count == 0) outL.Add(("imp", 1));
        return outL;
    }
    public static string pickFrom(List<(string k, double w)> p)
    {
        double t = 0; foreach (var (_, w) in p) t += w;
        double r = Js.R() * t;
        foreach (var (k, w) in p) { if ((r -= w) < 0) return k; }
        return p[0].k;
    }
    /* Golems are siege units, not rank and file */
    public const double GOLEM_THROW_WIND = 0.42;
    public const double GOLEM_MAX = 36;
    public static double golemCount(double n)
    {
        if (n < 16) return 0;
        if (n < 19) return 1;
        if (n < 23) return 2;
        double avg = 3.5 + (n - 23) * 0.95;
        return clamp(Math.Round(avg * rnd(0.72, 1.28)), 1, GOLEM_MAX);
    }
    public static List<string> golemKinds(double n)
    {
        var k = new List<string> { "stonegolem" };
        if (n >= 20) k.Add("icegolem");
        if (n >= 24) k.Add("firegolem");
        if (n >= 28) k.Add("stormgolem");
        return k;
    }
    /* Drop `count` golems into a queue, replacing rank-and-file rather than adding to it */
    public static void seedGolems(List<string> q, double n)
    {
        double want = Math.Min(golemCount(n), Math.Max(1, Math.Floor(q.Count / 3.0)));
        if (!Js.T(want)) return;
        var kinds = golemKinds(n);
        var used = new HashSet<int>();
        for (int i = 0; i < want; i++)
        {
            string g = pick(kinds);
            int at = -1;
            for (int tries = 0; tries < 24; tries++)
            {
                int k = (int)(Js.R() * q.Count);
                if (q.Count > 0 && !used.Contains(k) && q[k] != "boss") { at = k; break; }
            }
            if (at >= 0) { q[at] = g; used.Add(at); }
            else q.Add(g);
        }
    }
    /* Spend `budget` bricks out of `pool`, cheapest-affordable last */
    public static List<string> spendBudget(List<(string k, double w)> pool, double budget)
    {
        var q = new List<string>();
        double spent = 0; int guard = 0;
        string cheapest = pool[0].k;
        foreach (var (k, _) in pool) if (Js.Or(MONSTER_COST[k], 1) < Js.Or(MONSTER_COST[cheapest], 1)) cheapest = k;
        double cheap = Js.Or(MONSTER_COST[cheapest], 1);
        while (spent + cheap <= budget && q.Count < MAX_WAVE_BODIES && guard++ < 8000)
        {
            string k = pickFrom(pool);
            double c = Js.Or(MONSTER_COST[k], 1);
            if (spent + c > budget) { q.Add(cheapest); spent += cheap; continue; }
            q.Add(k); spent += c;
        }
        return q;
    }
    /* ================= WAVE MODIFIERS -- the map affixes ================== */
    public const double WAVE_MOD_FROM = 6, WAVE_MOD_STEP = 12, WAVE_MOD_MAX = 4;
    public static readonly List<WaveMod> WAVE_MODS = new()
    {
        new WaveMod { id = "hardy", name = "HARDY", col = "#ff8a6a", tip = "monsters have 30% more health",
            apply = e => { e.maxHp = Math.Ceiling(e.maxHp * 1.30); e.hp = e.maxHp; } },
        new WaveMod { id = "brutal", name = "BRUTAL", col = "#ff5a4a", tip = "monsters deal 25% more damage",
            apply = e => { e.dmg = Math.Max(1, Math.Round(e.dmg * 1.25)); } },
        /* THE ONE THING THAT GIVES A MONSTER RAGE */
        new WaveMod { id = "wrathful", name = "WRATHFUL", col = "#ff4a3c",
            tip = "hurting a monster enrages it — up to +30% damage, and it cools off if you leave it",
            apply = e =>
            {
                e.stackOn = new HashSet<string>(e.stackOn ?? new HashSet<string>()) { "rage" };
                e.stacks ??= new Dictionary<string, StackSt>();
                e.stackMax ??= new NumMap();
                e.rageOnHurt = e.rageOnHurt + 3;
            } },
        new WaveMod { id = "swift", name = "SWIFT", col = "#9fe8ff", tip = "monsters move 18% faster",
            apply = e => { e.speed *= 1.18; } },
        new WaveMod { id = "frenzied", name = "FRENZIED", col = "#ffd166", tip = "monsters attack 25% faster",
            apply = e => { e.atkS *= 0.75; if (Js.T(e.fire)) e.fire *= 0.75; e.touchCd *= 0.75; } },
        new WaveMod { id = "plated", name = "PLATED", col = "#cfd6dd", tip = "monsters have double armour",
            apply = e => { e.armour = e.armour * 2; } },
        new WaveMod { id = "warded", name = "WARDED", col = "#b98cff", tip = "monsters suppress 30% of spell damage",
            apply = e => { e.suppress = Math.Min(0.75, e.suppress + 0.30); } },
        new WaveMod { id = "resist", name = "RESISTANT", col = "#8ac24a", tip = "monsters gain 35% resistance to every element",
            apply = e => { foreach (var el in ELEMENTS) e.res[el] = e.res[el] + 0.35; } },
        new WaveMod { id = "slippery", name = "SLIPPERY", col = "#c9b8e8", tip = "monsters are twice as hard to hit",
            apply = e => { e.evasion = Math.Round(e.evasion * 2); } },
        new WaveMod { id = "horde", name = "HORDE", col = "#ffb900", tip = "40% more monsters, each with 15% less health",
            budget = b => Math.Round(b * 1.40),
            apply = e => { e.maxHp = Math.Max(1, Math.Ceiling(e.maxHp * 0.85)); e.hp = e.maxHp; } },
        new WaveMod { id = "rich", name = "OPULENT", col = "#ffe95a", tip = "monsters are tougher, and pay double",
            apply = e =>
            {
                e.maxHp = Math.Ceiling(e.maxHp * 1.20); e.hp = e.maxHp;
                e.xp = Math.Round(e.xp * 2); e.studsDrop.a *= 2; e.studsDrop.b *= 2;
            } },
        new WaveMod { id = "elites", name = "WARBAND", col = "#ffd76e", tip = "one monster in six is an elite",
            elite = 0.17 },
    };
    public static List<WaveMod> waveMods = new();
    public static int waveModCount(double n)
    {
        if (n < WAVE_MOD_FROM) return 0;
        return (int)Math.Min(WAVE_MOD_MAX, 1 + Math.Floor((n - WAVE_MOD_FROM) / WAVE_MOD_STEP));
    }
    public static List<WaveMod> rollWaveMods(double n)
    {
        int want = waveModCount(n); var pool = WAVE_MODS.ToList(); var outL = new List<WaveMod>();
        while (outL.Count < want && pool.Count > 0) outL.Add(pool.Splice((int)irnd(0, pool.Count - 1), 1)[0]);
        return outL;
    }
    /* what the modifiers do to the wave's budget, before a single body is picked */
    public static double waveModBudget(double b)
    {
        double outV = b;
        foreach (var m in waveMods) if (m.budget != null) outV = m.budget(outV);
        return outV;
    }
    /* ...and to one body, the moment it is built */
    public static void applyWaveMods(Ent e)
    {
        foreach (var m in waveMods) if (m.apply != null) try { m.apply(e); } catch { }
    }
    /* the extra chance a body is an elite, from WARBAND */
    public static double waveModElite()
    {
        double p = 0;
        foreach (var m in waveMods) if (Js.T(m.elite)) p = Math.Max(p, m.elite);
        return p;
    }
    public static List<string> composeWave(double n)
    {
        /* rolled here and held for the whole wave */
        waveMods = rollWaveMods(n);
        var pool = wavePool(n);
        List<string> q;
        if (n % 5 == 0)
        {
            /* a boss wave spends a slice of the budget on an escort */
            double escort = Math.Round(waveModBudget(waveBudget(n)) * (n % RAID_WAVE == 0 ? 0.55 : 0.40));
            q = spendBudget(pool, escort);
            shuffleArr(q);
            if (n % RAID_WAVE == 0) q.InsertRange(0, new[] { "boss", "boss", "boss", "boss", "boss", "boss" });  /* the raid */
            else q.Insert(0, "boss");
            seedGolems(q, n);
            return q;
        }
        q = spendBudget(pool, waveModBudget(waveBudget(n)));
        shuffleArr(q);
        seedGolems(q, n);
        return q;
    }
    public static List<T> shuffleArr<T>(List<T> a)
    {
        for (int i = a.Count - 1; i > 0; i--) { int j = (int)(Js.R() * (i + 1)); var t = a[i]; a[i] = a[j]; a[j] = t; }
        return a;
    }
    /* how much of the pack odds a wave gets: a sliver on wave 6, all of it by 30 */
    public const double PACK_RAMP_FROM = 6, PACK_RAMP_FULL = 30, PACK_RAMP_MIN = 0.12;
    public static double packRamp(double n) => clamp(PACK_RAMP_MIN + (1 - PACK_RAMP_MIN) * (n - PACK_RAMP_FROM) / (PACK_RAMP_FULL - PACK_RAMP_FROM), PACK_RAMP_MIN, 1);
    /* ...and how many a wave may hold */
    public static PackCaps packCaps(double n) { double k = Math.Max(0, n - PACK_RAMP_FROM); return new PackCaps { magic = 1 + Math.Floor(k / 4), elite = 2 + Math.Floor(k / 2) }; }
    public static void startWave(double n)
    {
        wave.n = n; wave.queue = composeWave(n); wave.hold = false; wave.magicN = 0; wave.eliteN = 0;
        { var L = leagueRoll(n); breachRoll(n, L.breach); abyssRoll(n, L.abyss); ritualRoll(n, L.ritual); legionRoll(n, L.legion); }
        sboxRoll(n);
        goblinRoll(n);
        logEvent("── WAVE " + Js.Num(n) + " · " + wave.queue.Count + " bodies · " + Js.Num(waveBudget(n)) + " bricks ──", "#ffd76e");
        /* THE TOLL lands ON the wave that charges it */
        if (RES_TAX_WAVES.Contains(n))
        {
            double lost = Math.Round(resTax(n) * 100);
            logEvent("   THE COLD CREEPS IN — all resistances " + Js.Num(lost) + " lower, for good", "#7ecbff");
            showBanner("THE COLD CREEPS IN", "ALL RESISTANCES −" + Js.Num(lost) + "% — PERMANENTLY",
                "#7ecbff", 2.6, 44);
            SFX.roar(); addShake(0.4);
        }
        foreach (var m in waveMods) logEvent("   " + m.name + " — " + m.tip, m.col);
        queueTrackForWave(n);
        /* the wave is BUILT now and spawns when the break runs out */
        bool afterBoss = n > 1 && (n - 1) % 5 == 0;
        wave.state = "ready"; wave.t = 0; wave.spawnT = 1.4; wave.raidIdx = 0;
        wave.readyT = wave.readyMax = afterBoss ? WAVE_BREAK_BOSS : WAVE_BREAK;
        bool boss = n % 5 == 0;
        if (n % RAID_WAVE == 0)
        {
            showBanner("WAVE " + Js.Num(n), "THE GRAND RAID — ALL BOSSES AWAKEN", "#c95bd0", 3.0, 58);
            SFX.roar();
            return;
        }
        string bname = "";
        if (boss)
        {
            var bkeys = BOSSES.Keys.ToList();
            bname = (n % 10 == 0)
                ? SUPERBOSSES[ultraForWave(n)].name
                : BOSSES[bkeys[((int)Math.Max(1, Math.Floor(n / 5)) - 1) % bkeys.Count]].name;
        }
        /* the modifiers go under the wave number */
        string modLine = waveMods.Count > 0
            ? string.Join("  ·  ", waveMods.Select(m => m.name))
            : (n == 1 ? biomeName() + " · " + (isCaster() ? "Cast" : isSlammer() ? "Slam" : isArcher() ? "Shoot" : isSummoner() ? "Raise the dead" : "Slash") +
                       " with LMB · SPACE to roll!" : "");
        showBanner("WAVE " + Js.Num(n),
            boss ? (bname + " approaches..." + (waveMods.Count > 0 ? "   [" + string.Join(" · ", waveMods.Select(m => m.name)) + "]" : ""))
                 : modLine,
            boss ? (n % 10 == 0 ? "#c95bd0" : "#ff5a4a") : "#ffb900", boss ? 2.2 : 1.7,
            boss && n % 10 == 0 ? 60 : 54);
        if (boss) SFX.roar();
    }
    /* WHAT OF THE LEAGUES IS STILL WAITING TO BE DONE on the map */
    public static List<string> leagueLeft()
    {
        var outL = new List<string>();
        foreach (var B in allBreaches()) if (B != null && B.state == "hand") outL.Add("breach");
        foreach (var A in allAbysses()) if (A != null && A.state == "idle") outL.Add("abyss");
        foreach (var b in sboxLive()) if (!b.sprung) outL.Add("strongbox");
        foreach (var A in ritualS.altars) if (A.state == "guarded" || A.state == "ready") outL.Add("ritual");
        if (legionEv != null && legionEv.state == "idle") outL.Add("legion");
        return outL;
    }
    /* league content that is RUNNING right now */
    public static bool leagueRunning() => breachOpenNow() || abyssOpenNow() || sboxActiveNow() || ritualActiveNow() || legionActiveNow();
    public static string leagueLeftText()
    {
        var L = leagueLeft(); var outL = new List<string>();
        int n(string k) => L.Count(x => x == k);
        void say(string k, string one, string many) { int c = n(k); if (c != 0) outL.Add(c == 1 ? one : c + " " + many); }
        say("breach", "a breach", "breaches"); say("abyss", "an abyss", "abysses");
        say("strongbox", "a strongbox", "strongboxes"); say("ritual", "a ritual altar", "ritual altars");
        say("legion", "a legion monolith", "legion monoliths");
        return string.Join(" · ", outL);
    }
    /* F ON A HELD WAVE: leave what is left */
    public static void waveGoOn()
    {
        if (wave.state != "cleared" || !wave.hold) return;
        wave.hold = false;
        SFX.click();
    }
    public static void waveSkip()
    {
        if (wave.state != "ready") return;
        wave.readyT = 0;
        SFX.click();
    }
    public static void beginFight()
    {
        wave.state = "fight"; wave.t = 0; wave.spawnT = 1.4;
        wave.callT = 0; wave.callIn = 0; wave.merged = 0; wave.owed = 0;
        /* if F is still down from skipping the break, it must be RELEASED first */
        wave.callLatch = key("KeyF");
    }
    /* Fold the next wave into this one */
    public static void callWave()
    {
        double n = wave.n + 1;
        var extra = composeWave(n);
        wave.n = n;
        wave.queue = wave.queue.Concat(extra).ToList();
        shuffleArr(wave.queue);
        wave.merged = wave.merged + 1;
        wave.owed = wave.owed + 1;           /* a reward is owed for it either way */
        wave.spawnT = 0;                     /* they come straight through the door */
        queueTrackForWave(n);
        logEvent("── WAVE " + Js.Num(n) + " CALLED IN EARLY · +" + extra.Count + " bodies ──", "#ff8a4a");
        showBanner("WAVE " + Js.Num(n) + " JOINS IN!", extra.Count + " more bodies — double or nothing",
            "#ff8a4a", 2.0, 46);
        SFX.roar(); addShake(0.45);
    }
    public static void updateCall(double dt)
    {
        if (wave.state != "fight") { wave.callT = 0; return; }
        if (wave.callIn > 0)
        {
            wave.callIn -= dt;
            if (wave.callIn <= 0) { wave.callIn = 0; callWave(); }
            return;
        }
        if (wave.merged >= CALL_MAX) return;
        if (!key("KeyF")) wave.callLatch = false;
        if (key("KeyF") && !wave.callLatch && !player.dead)
        {
            wave.callT = wave.callT + dt;
            if (wave.callT >= CALL_HOLD)
            {
                wave.callT = 0;
                wave.callIn = CALL_DELAY;
                SFX.grunt();
                showBanner("CALLING THE NEXT WAVE", "brace — " + Js.Num(CALL_DELAY) + " seconds", "#ff8a4a", 1.2, 38);
            }
        }
        else wave.callT = Math.Max(0, wave.callT - dt * 2.2);
    }
    public const double WAVE_CLEAR_HOLD = 1.0;     /* seconds a cleared wave lingers before the next breather */
    public static void updateWave(double dt)
    {
        /* in the sandbox the clock simply does not run */
        if (sandbox) return;
        wave.t += dt;
        updateCall(dt);
        /* A CLEARED WAVE DOES NOT WAIT FOR ITS REWARD. IT DOES WAIT FOR THE LEAGUES. */
        if (wave.state == "cleared")
        {
            if (wave.hold && leagueLeft().Count == 0) wave.hold = false;
            if (!wave.hold && !leagueRunning() && wave.t >= WAVE_CLEAR_HOLD) startWave(wave.n + 1);
            return;
        }
        if (wave.state == "ready")
        {
            if (!ritualActiveNow()) wave.readyT -= dt;
            if (wave.readyT <= 0) beginFight();
            return;
        }
        if (wave.state == "fight")
        {
            int alive = enemies.Count(e => !e.dead && e.state == "active" && !e.dormant && !isBanner(e));
            double cap = Math.Min(MAX_ALIVE, 30 + wave.n * 16);
            /* a running ritual holds the wave's own bodies back until it is done */
            if (wave.queue.Count > 0 && alive < cap && !ritualActiveNow())
            {
                wave.spawnT -= dt;
                if (wave.spawnT <= 0)
                {
                    int batch = wave.n < 3 ? 1 : (wave.n < 6 ? 2 : (wave.n < 12 ? 3 : (wave.n < 25 ? 4 : 6)));
                    for (int i = 0; i < batch && wave.queue.Count > 0; i++)
                    {
                        string type = wave.queue.Shift();
                        var pos = randomSpawnPos();
                        if (type == "boss")
                        {
                            /* wave-50 raid: first six boss entries get forced distinct keys */
                            if (wave.n % RAID_WAVE == 0)
                            {
                                var raidKeys = BOSSES.Keys.Concat(SUPERBOSSES.Keys).ToList();
                                int idx = (int)(wave.raidIdx = wave.raidIdx);
                                spawnEnemy("boss", pos.x, pos.y, false, false, raidKeys[idx % raidKeys.Count]);
                                wave.raidIdx = idx + 1;
                            }
                            else spawnEnemy("boss", pos.x, pos.y);
                        }
                        else if (type == "boneling")
                        {
                            /* bonelings come in threes */
                            for (int k = 0; k < 3; k++)
                            {
                                bool el = wave.n >= 6 && wave.eliteN < packCaps(wave.n).elite && Js.R() < 0.15 * packRamp(wave.n);
                                if (el) wave.eliteN = wave.eliteN + 1;
                                spawnEnemy("boneling", clamp(pos.x + rnd(-45, 45), W.wall + 40, W.w - W.wall - 40),
                                    clamp(pos.y + rnd(-45, 45), W.wall + 40, W.h - W.wall - 40), el);
                            }
                        }
                        else
                        {
                            bool packable = wave.n >= 6;
                            /* A PACK IS A PACK: nine to twelve bodies */
                            double packSize = MAGIC_PACK[0] +
                                Math.Round((MAGIC_PACK[1] - MAGIC_PACK[0]) * clamp((wave.n - 6) / 34, 0, 1));
                            double roll = Js.R();
                            /* golems are siege units -- they only rarely head a pack */
                            bool isGolem = type.EndsWith("golem");
                            /* THE PACKS COME IN GRADUALLY */
                            double pk = difficulty.pack * (isGolem ? clamp(0.10 + (wave.n - 10) * 0.015, 0.10, 0.45) : 1) * packRamp(wave.n);
                            var caps = packCaps(wave.n);
                            bool magicOk = wave.magicN < caps.magic, eliteOk = wave.eliteN < caps.elite;
                            if (packable && magicOk && roll < 0.09 * pk)
                            {
                                wave.magicN = wave.magicN + 1;
                                /* THE MAGIC GOES ON ALL OF THEM */
                                spawnMagicPack(type, pos, packSize + irnd(0, 3));
                            }
                            else if (packable && eliteOk && roll < 0.09 * pk + 0.12 * pk)
                            {
                                wave.eliteN = wave.eliteN + 1;
                                /* AN ELITE IS ONE BODY WITH A COURT */
                                spawnEnemy(type, pos.x, pos.y, true, false);
                                double court = ELITE_COURT[0] +
                                    Math.Round((ELITE_COURT[1] - ELITE_COURT[0]) * clamp((wave.n - 6) / 34, 0, 1));
                                string minion = eliteMinion(type);
                                for (int k = 0; k < court; k++)
                                {
                                    double a = k / court * TAU + rnd(-0.18, 0.18), rr = rnd(56, 96);
                                    spawnEnemy(minion, clamp(pos.x + Math.Cos(a) * rr, W.wall + 50, W.w - W.wall - 50),
                                        clamp(pos.y + Math.Sin(a) * rr, W.wall + 50, W.h - W.wall - 50), false, false);
                                }
                            }
                            else
                            {
                                /* a lone elite counts against the cap too */
                                bool lone = packable && eliteOk && Js.R() < 0.15 * pk;
                                if (lone) wave.eliteN = wave.eliteN + 1;
                                spawnEnemy(type, pos.x, pos.y, lone || Js.R() < waveModElite());
                            }
                        }
                    }
                    wave.spawnT = 0.6;
                }
            }
            /* a sleeping ritual pack is not the wave's: it waits for you across waves */
            if (wave.queue.Count == 0 && !enemies.Any(e => !e.dead && !e.dormant && e.type != "goblin" && !isBanner(e)) && wave.t > 2 && !Js.T(wave.callIn)
                && !breachOpenNow() && !abyssOpenNow() && !sboxActiveNow() && !ritualActiveNow() && !legionActiveNow())
            {
                wave.state = "cleared"; wave.t = 0;
                foreach (var b in enemies) if (isBanner(b) && !b.dead) { b.dead = true; addSparks(b.x, b.y - 60, 8, "#fff0b0"); }
                wave.hold = leagueLeft().Count > 0;
                /* the highest wave actually FINISHED */
                wave.done = Math.Max(wave.done, wave.n);
                metaLiveBank();
                double folded = wave.merged;
                double bonus = (4 + wave.n * 2) * (1 + folded);
                showBanner(Js.T(folded) ? "DOUBLE WAVE CLEARED!" : "WAVE CLEARED!",
                    "+" + Js.Num(bonus) + " stud bonus" + (Js.T(folded) ? " · " + Js.Num(folded + 1) + " rewards" : ""), "#a5e06e", 1.6, 44);
                spawnStuds(player.x, player.y - 20, bonus);
                SFX.levelup();
                /* every wave folded in is paid for */
                for (int i = 0; i < folded; i++) queuePick("bonus", null);
                queuePick("wave", null);
                wave.merged = 0; wave.owed = 0;
            }
        }
    }
    /* ============================ THE LEAGUE ROLL ============================ */
    public const double LEAGUE_FROM = 7;
    public const double LEAGUE_CHANCE = 0.22;
    public const double LEAGUE_PITY = 6;
    public const double LEAGUE_MORE_PER_LVL = 0.025, LEAGUE_MORE_MAX = 0.45, LEAGUE_MAX = 3;
    public static readonly string[] LEAGUE_KINDS = { "breach", "abyss", "ritual", "legion" };
    public static double leagueMiss = 0;
    public static double leagueMoreChance(double n) => Math.Min(LEAGUE_MORE_MAX, LEAGUE_MORE_PER_LVL * mobRank(n));
    public static LeaguePlan leagueRoll(double n)
    {
        var plan = new LeaguePlan();
        if (sandbox || n < LEAGUE_FROM || n % 5 == 0) return plan;
        if (leagueMiss < LEAGUE_PITY - 1 && Js.R() >= leagueChanceNow()) { leagueMiss++; return plan; }
        leagueMiss = 0;
        int k = 1;
        double more = leagueMoreChance(n);
        while (k < LEAGUE_MAX && Js.R() < more) k++;
        for (int i = 0; i < k; i++)
        {
            var ok = LEAGUE_KINDS.Where(t => t == "ritual" ? plan.ritual == 0 && ritualCanRise()
                                          : t == "legion" ? plan.legion == 0 && !legionActiveNow()
                                          : t == "breach" ? plan.breach < BREACH_LORD_KEYS.Count : true).ToList();
            /* weighted by the league tree */
            var ws = ok.Select(leagueKindWeight).ToList(); double tot = ws.Sum();
            double r = Js.R() * tot; string pk = ok[^1];
            for (int j = 0; j < ok.Count; j++) { r -= ws[j]; if (r < 0) { pk = ok[j]; break; } }
            switch (pk) { case "breach": plan.breach++; break; case "abyss": plan.abyss++; break; case "ritual": plan.ritual++; break; case "legion": plan.legion++; break; }
        }
        return plan;
    }
}
