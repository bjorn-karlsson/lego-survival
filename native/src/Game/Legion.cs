using System.Text.Json.Nodes;

namespace Brickblade;

/* ================================== LEGION (JS 10990–11186) ==================================
   A monolith; touch it and a battle stopped in time comes into being round it.
   What you break in the freeze stands as ice and gets up AWAKENED when time
   starts again. Then THE LEAGUE TREE (JS 11187–11356), pre-built and switched off. */
public sealed class LegionFaction { public string name, col, sergeant, general; public string[] ranks; }
public sealed class LegionSquad { public double x, y, spread, face, w; }
public sealed partial class Legion
{
    public string state, faction; public double delay, n, t, seed, T0, kills;
    public List<Pt> squads; public List<Ent> held;
}
public sealed partial class Ent
{
    public bool legionFrozen, legionGeneral, legionDown, awakened; public double lgx, lgy; public string legionChest, legionSrc;
}
public sealed partial class Prop { public bool war; }
public sealed class LtStat { public Func<double, string> txt; }
public sealed class LtNode { public string id, name, league; public bool hub, notable; public NumMap stats; public string[] links; }
public sealed class LtreeState { public double pts; public List<string> nodes = new() { "lt_root" }; }

public static partial class G
{
    /* LEGION_R is how far out the battlefield reaches */
    public const double LEGION_R = 620, LEGION_TOUCH = 50, LEGION_T = 9;
    /* THE ARMY STANDS IN SQUADS, not in a ring */
    public static readonly Tbl LEGION_SQUADS = new double[] { 3, 6 };
    public static readonly Tbl LEGION_DELAY = new double[] { 3, 8 };
    public const double LEGION_GENERAL_FROM = 15;
    public const string LEGION_COL = "#c3a6ff";
    public static readonly (int r, int g, int b) LEGION_ICE = (215, 200, 255);
    public static readonly JsMap<string, LegionFaction> LEGION_FACTIONS = new()
    {
        ["empire"] = new LegionFaction { name = "THE ETERNAL EMPIRE", col = "#ffd76e", ranks = new[] { "knight", "lancer", "archer", "skeleton" }, sergeant = "bastion", general = "jugg" },
        ["horde"] = new LegionFaction { name = "THE KARUI HORDE", col = "#ff8a3c", ranks = new[] { "brute", "stalker", "imp", "superimp" }, sergeant = "zhulk", general = "jugg" },
    };
    public static Legion legionEv = null;
    public static double legionSize(double n) => Math.Min(60, 22 + n);
    public static double legionSergeants(double n) => 2 + (n >= 20 ? 1 : 0) + (n >= 35 ? 1 : 0);
    public static bool legionActiveNow() => legionEv != null && (legionEv.state == "frozen" || legionEv.state == "fighting");
    public static bool legionRoll(double n, double k)
    {
        if (legionEv != null && legionEv.state == "idle" && !Js.T(k)) legionEv = null;      /* an untouched monolith goes with its wave */
        if (!Js.T(k) || legionActiveNow()) return false;
        legionEv = new Legion { state = "pending", delay = rnd(LEGION_DELAY[0], LEGION_DELAY[1]), n = n, t = 0, seed = rnd(1000) };
        return true;
    }
    public static Pt legionSpot()
    {
        double pad = LEGION_R * 0.55 + 40;
        for (int i = 0; i < 160; i++)
        {
            double a = rnd(TAU), rr = rnd(520, 1500);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            if (!abyssFloorOk(x, y, pad)) continue;
            int ok = 0;
            for (int k = 0; k < 8; k++) if (breachFloorOk(x + Math.Cos(k / 8.0 * TAU) * LEGION_R * 0.6, y + Math.Sin(k / 8.0 * TAU) * LEGION_R * 0.6)) ok++;
            if (ok >= 6) return new Pt(x, y);
        }
        return null;
    }
    public static bool legionPlace(Pt at = null)
    {
        at ??= legionSpot();
        if (at == null) { legionEv = null; return false; }
        legionEv.state = "idle"; legionEv.x = at.x; legionEv.y = at.y;
        showBanner("LEGION", "A MONOLITH HAS RISEN — TOUCH IT", LEGION_COL, 2.0, 44);
        logEvent("   LEGION — a monolith has risen; touch it to stop the battle in time", LEGION_COL);
        SFX.roar();
        return true;
    }
    /* the battlefield, stopped: an army in its ranks around the stone */
    public static void legionStart()
    {
        var L = legionEv; double n = wave.n;
        string fk = Js.R() < 0.5 ? "empire" : "horde"; var FAC = LEGION_FACTIONS[fk];
        L.faction = fk; L.state = "frozen"; L.t = L.T0 = legionTimeNow(); L.kills = 0;
        /* the squads: squared randomness, so a couple are big and the rest are knots */
        var squads = new List<LegionSquad>(); double nS = irnd(LEGION_SQUADS[0], LEGION_SQUADS[1]);
        for (int i = 0; i < nS; i++)
        {
            double x = L.x, y = L.y;
            for (int k = 0; k < 12; k++)
            {
                double a = rnd(TAU), rr = rnd(170, LEGION_R - 110);
                x = L.x + Math.Cos(a) * rr; y = L.y + Math.Sin(a) * rr;
                if (abyssFloorOk(x, y, 60)) break;
            }
            squads.Add(new LegionSquad { x = x, y = y, spread = rnd(70, 150), face = rnd(TAU), w = 0.25 + Math.Pow(Js.R(), 2) * 2.5 });
        }
        double wSum = squads.Aggregate(0.0, (t, q) => t + q.w);
        LegionSquad pickSquad() { double r = Js.R() * wSum; foreach (var q in squads) { r -= q.w; if (r <= 0) return q; } return squads[0]; }
        L.squads = squads.Select(q => new Pt(Math.Round(q.x), Math.Round(q.y))).ToList();
        /* one soldier, in a squad (or, for the general, beside the stone) */
        Ent put(string type, bool elite, bool magic, LegionSquad sq)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                double x, y, fa;
                if (sq != null && tries >= 25) { double a = rnd(TAU), rr = rnd(80, LEGION_R - 20); x = L.x + Math.Cos(a) * rr; y = L.y + Math.Sin(a) * rr; fa = sq.face; }
                else if (sq != null) { double a = rnd(TAU), rr = sq.spread * Math.Sqrt(Js.R()); x = sq.x + Math.Cos(a) * rr; y = sq.y + Math.Sin(a) * rr; fa = sq.face + rnd(-0.5, 0.5); }
                else { double a = rnd(TAU), rr = rnd(70, 130); x = L.x + Math.Cos(a) * rr; y = L.y + Math.Sin(a) * rr; fa = a + Math.PI; }
                if (hypot(x - L.x, y - L.y) > LEGION_R || hypot(x - L.x, y - L.y) < 60) continue;
                if (!abyssFloorOk(x, y, 40)) continue;
                spawnEnemy(type, x, y, elite, magic);
                var e = enemies[^1];
                e.state = "active"; e.legion = L; e.legionFrozen = true; e.lgx = x; e.lgy = y;
                e.face = new Pt(Math.Cos(fa), Math.Sin(fa)); e.walk = rnd(TAU);
                return e;
            }
            return null;
        }
        double N = legionSize(n);
        for (int i = 0; i < N; i++)
        {
            var sq = pickSquad();
            if (put(FAC.ranks[(int)(Js.R() * FAC.ranks.Length)], false, Js.R() < 0.15, sq) == null)
                put(FAC.ranks[(int)(Js.R() * FAC.ranks.Length)], false, Js.R() < 0.15, pickSquad());
        }
        /* a sergeant leads a squad -- the biggest ones first */
        var bySize = squads.ToList().SortJs((a, b) => b.w - a.w);
        for (int i = 0; i < legionSergeants(n); i++)
        {
            var e = put(FAC.sergeant, true, false, bySize[i % bySize.Count]);
            if (e != null) e.legionChest = Js.R() < 0.05 ? "legendary" : Js.R() < 0.37 ? "epic" : "rare";
        }
        if (n >= LEGION_GENERAL_FROM)
        {
            var g = put(FAC.general, true, false, null);
            if (g != null) { g.legionGeneral = true; g.maxHp *= 2.5; g.hp = g.maxHp; g.scale *= 1.3; g.r *= 1.3; }
        }
        addSparks(L.x, L.y - 60, 30, LEGION_COL); addShake(0.3);
        showBanner(FAC.name, "FROZEN IN TIME — " + Js.Num(L.T0) + "s", LEGION_COL, 2.0, 42);
        logEvent("   LEGION — " + FAC.name + " stands frozen; what you break shatters when time starts again", LEGION_COL);
        SFX.freeze();
    }
    /* A FROZEN SOLDIER BROUGHT TO NOTHING IS HELD, NOT KILLED */
    public const double LEGION_WAKE_HP = 1.6, LEGION_WAKE_DMG = 1.35, LEGION_WAKE_SPD = 1.15, LEGION_WAKE_SCALE = 1.15;
    public static bool legionHolds(Ent e, string source)
    {
        var L = e.legion;
        if (L == null || L != legionEv || !e.legionFrozen || L.state != "frozen" || e.dead) return false;
        e.hp = 0; e.dead = true; e.legionDown = true; e.legionSrc = source;
        (L.held ??= new List<Ent>()).Add(e);
        L.kills++;
        addSparks(e.x, e.y - 18, 10, "#e6dcff");
        return true;
    }
    public static void legionRelease()
    {
        var L = legionEv;
        L.state = "fighting";
        /* THE BROKEN GET UP, AWAKENED */
        var held = L.held ?? new List<Ent>(); L.held = new List<Ent>();
        foreach (var e in held)
        {
            e.dead = false; e.legionDown = false; e.legionFrozen = false; e.awakened = true;
            e.maxHp *= LEGION_WAKE_HP; e.hp = e.maxHp; e.dmg *= LEGION_WAKE_DMG; e.speed *= LEGION_WAKE_SPD;
            e.scale = Js.Or(e.scale, 1) * LEGION_WAKE_SCALE; e.r = Js.Or(e.r, 12) * 1.08;
            e.mode = "chase"; e.atkCd = rnd(0.3, 1.0);
            addSparks(e.x, e.y - 22 * Js.Or(e.scale, 1), 14, "#e6dcff");
            addRing(e.x, e.y, 4, 260, 46 * Js.Or(e.scale, 1), "#c3a6ff", 4, 0.35);
            /* back into the fight -- once */
            if (!enemies.Contains(e)) enemies.Add(e);
        }
        foreach (var e in enemies) if (e.legion == L && !e.dead) { e.legionFrozen = false; e.mode = "chase"; e.atkCd = rnd(0.2, 1.0); }
        int woke = enemies.Count(e => e.legion == L && !e.dead);
        showBanner("TIME STARTS AGAIN", held.Count > 0 ? held.Count + " RISE AWAKENED · " + woke + " IN ALL"
                                                      : "THE LEGION BREAKS FREE", LEGION_FACTIONS[L.faction].col, 1.8, 40);
        addSparks(L.x, L.y - 60, 30, "#ffffff");
        SFX.roar();
    }
    public static void legionDone()
    {
        var L = legionEv;
        L.state = "done";
        showBanner("LEGION DEFEATED", Js.Num(L.kills) + " ROSE AWAKENED AND FELL", LEGION_COL, 1.8, 40);
        logEvent("   LEGION defeated — " + Js.Num(L.kills) + " were broken in the freeze and rose awakened", LEGION_COL);
        legionEv = null;
    }
    public static void legionOnKill(Ent e)
    {
        var L = e.legion;
        if (L == null) return;
        if (e.legionChest != null)
            props.Add(shoveToFloor(new Prop { type = "chest", x = e.x, y = e.y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16, rar = e.legionChest, war = true }, 20));
        if (e.legionGeneral)
        {
            queuePick("chest", Js.R() < 0.25 ? "legendary" : "epic");
            showBanner("THE GENERAL FALLS", "ITS WAR CHEST IS YOURS · ON R", LEGION_FACTIONS[L.faction].col, 1.8, 40);
        }
    }
    public static void updateLegion(double dt)
    {
        if (legionEv == null || player == null) return;
        var L = legionEv; var h = heroObj ?? player;
        if (L.state == "pending")
        {
            if (wave.state != "fight") return;
            L.delay -= dt;
            if (L.delay <= 0) legionPlace();
            return;
        }
        if (L.state == "idle")
        {
            if (h != null && !h.dead && hypot(h.x - L.x, h.y - L.y) < LEGION_TOUCH + Js.Or(h.r, 12)) legionStart();
            return;
        }
        var left = enemies.Where(e => e.legion == L && !e.dead).ToList();
        /* THE FREEZE RUNS ITS FULL LENGTH, however many are broken */
        if (L.state == "frozen")
        {
            L.t -= dt;
            if (L.t <= 0) legionRelease();
            return;
        }
        if (L.state == "fighting" && left.Count == 0) legionDone();
    }
    /* ---- DRAWN ---- */
    static Col LIce(double a) => rgba(LEGION_ICE.r, LEGION_ICE.g, LEGION_ICE.b, a);
    public static void drawLegion()
    {
        var L = legionEv;
        if (L == null || L.state == "pending") return;
        var c = ctx;
        /* the monolith */
        double glow = 0.5 + 0.5 * Math.Sin(vtime * 2.5 + L.seed);
        c.save(); c.translate(L.x, L.y);
        c.fillStyle = "rgba(0,0,0,0.3)"; c.beginPath(); c.ellipse(0, 4, 26, 9, 0, 0, TAU); c.fill();
        c.fillStyle = "#4a5058"; c.strokeStyle = "rgba(15,20,25,0.8)"; c.lineWidth = 2.5;
        c.beginPath(); c.moveTo(-16, 2); c.lineTo(-11, -96); c.lineTo(0, -110); c.lineTo(11, -96); c.lineTo(16, 2); c.closePath(); c.fill(); c.stroke();
        double lit = L.state == "frozen" ? clamp(L.t / Js.Or(L.T0, LEGION_T), 0, 1) * 4 : L.state == "idle" ? 4 : 0;
        for (int i = 0; i < 4; i++)
        {
            /* bottom rune last to go out */
            double on = clamp(lit - (3 - i), 0, 1);
            c.fillStyle = L.state == "idle" ? LIce(0.45 + 0.5 * glow) : LIce(0.18 + 0.8 * on);
            c.fillRect(-4, -86 + i * 20, 8, 10);
        }
        if (L.state == "frozen")
        {
            /* and a faint violet haze off the stone while time is stopped */
            c.fillStyle = LIce(0.10 + 0.06 * Math.Sin(vtime * 5));
            c.beginPath(); c.ellipse(0, -50, 30, 70, 0, 0, TAU); c.fill();
        }
        drawStudG(c, 0, -108, 3.5, "#6d747c");
        c.restore();
    }
    /* frozen soldiers wear a shell of ice -- drawn over them */
    public static void drawLegionIce()
    {
        var L = legionEv; var c = ctx;
        /* the AWAKENED: a violet flame round the feet and the word over the head */
        foreach (var e in enemies)
        {
            if (!e.awakened || e.dead || !onScreen(e, 80)) continue;
            double s = Js.Or(e.scale, 1), fl = 0.5 + 0.5 * Math.Sin(vtime * 7 + e.phase);
            c.strokeStyle = rgba(195, 166, 255, 0.55 + 0.35 * fl); c.lineWidth = 3;
            c.beginPath(); c.ellipse(e.x, e.y + 2, 20 * s, 8 * s, 0, 0, TAU); c.stroke();
            if (Js.R() < 0.3) particles.Add(new Particle { kind = "spark", x = e.x + rnd(-12, 12) * s, y = e.y - rnd(0, 30) * s, vx = rnd(-8, 8), vy = rnd(-50, -20), life = 0.5, max = 0.5, color = "#c3a6ff" });
            c.font = "700 10px Fredoka"; c.textAlign = "center"; c.lineWidth = 3; c.strokeStyle = "rgba(15,20,25,0.8)";
            c.strokeText("AWAKENED", e.x, e.y - 62 * s); c.fillStyle = "#c3a6ff"; c.fillText("AWAKENED", e.x, e.y - 62 * s);
        }
        if (L != null && L.state == "frozen")
        {
            foreach (var e in enemies)
            {
                if (e.dead || e.legion != L || !e.legionFrozen || !onScreen(e, 80)) continue;
                double s = Js.Or(e.scale, 1);
                c.fillStyle = LIce(0.34); c.strokeStyle = "rgba(232,222,255,0.8)"; c.lineWidth = 1.5;
                c.beginPath(); c.ellipse(e.x, e.y - 26 * s, 17 * s, 32 * s, 0, 0, TAU); c.fill(); c.stroke();
            }
            /* ...and the broken ones stand as cracked ice until time starts again */
            foreach (var e in L.held ?? new List<Ent>())
            {
                if (!onScreen(e, 80)) continue;
                double s = Js.Or(e.scale, 1), cy = e.y - 26 * s;
                c.fillStyle = "rgba(230,220,255,0.62)"; c.strokeStyle = "#ffffff"; c.lineWidth = 2;
                c.beginPath(); c.moveTo(e.x - 15 * s, e.y); c.lineTo(e.x - 18 * s, cy - 14 * s); c.lineTo(e.x - 6 * s, cy - 34 * s);
                c.lineTo(e.x + 8 * s, cy - 30 * s); c.lineTo(e.x + 17 * s, cy - 8 * s); c.lineTo(e.x + 14 * s, e.y); c.closePath(); c.fill(); c.stroke();
                c.strokeStyle = "rgba(130,95,210,0.85)"; c.lineWidth = 1.5;
                c.beginPath(); c.moveTo(e.x - 2 * s, cy - 30 * s); c.lineTo(e.x + 3 * s, cy - 12 * s); c.lineTo(e.x - 5 * s, cy + 2 * s); c.lineTo(e.x + 2 * s, e.y - 4 * s);
                c.moveTo(e.x + 3 * s, cy - 12 * s); c.lineTo(e.x + 12 * s, cy - 4 * s); c.moveTo(e.x - 5 * s, cy + 2 * s); c.lineTo(e.x - 13 * s, cy - 6 * s); c.stroke();
            }
        }
    }
    public static void drawLegionHud()
    {
        var L = legionEv;
        if (L == null || player == null) return;
        if (L.state == "idle")
            edgeMarker(L.x, L.y, LEGION_COL, (x, y) =>
            {
                ctx.fillStyle = "#4a5058"; ctx.strokeStyle = LEGION_COL; ctx.lineWidth = 1.5;
                ctx.beginPath(); ctx.moveTo(x - 5, y + 8); ctx.lineTo(x - 3, y - 8); ctx.lineTo(x, y - 11); ctx.lineTo(x + 3, y - 8); ctx.lineTo(x + 5, y + 8); ctx.closePath(); ctx.fill(); ctx.stroke();
            }, true);
        if (!legionActiveNow()) return;
        int left = enemies.Count(e => e.legion == L && !e.dead);
        double by = hudWaveBottom() + 8 + (breachOpenNow() ? 30 : 0) + (abyssOpenNow() ? 30 : 0) + (sboxActiveNow() ? 20 : 0) + (ritualActiveNow() ? 20 : 0);
        string head = L.state == "frozen" ? "LEGION · FROZEN " + Js.ToFixed(L.t, 1) + "s · " + Js.Num(L.kills) + " broken · " + left + " standing"
                                          : "LEGION · " + left + " still fighting";
        ctx.font = "700 12px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
        ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
        ctx.strokeText(head, cw / 2, by); ctx.fillStyle = LEGION_COL; ctx.fillText(head, cw / 2, by);
    }

    /* ============================ THE LEAGUE TREE (pre-built) ============================
       NOT LIVE YET: leagueStat() answers 0 while LEAGUE_TREE_ON is off. */
    public static bool LEAGUE_TREE_ON = false;
    static string P(double v) => Js.Num(Math.Round(v * 100));
    public static readonly JsMap<string, LtStat> LT_STATS = new()
    {
        ["leagueChance"] = new LtStat { txt = v => "+" + P(v) + "% chance a wave carries a league" },
        ["breachWeight"] = new LtStat { txt = v => "Breach " + P(v) + "% more likely to be the league" },
        ["abyssWeight"] = new LtStat { txt = v => "Abyss " + P(v) + "% more likely to be the league" },
        ["ritualWeight"] = new LtStat { txt = v => "Ritual " + P(v) + "% more likely to be the league" },
        ["legionWeight"] = new LtStat { txt = v => "Legion " + P(v) + "% more likely to be the league" },
        ["breachMobLvl"] = new LtStat { txt = v => "+" + Js.Num(v) + " monster level for breach monsters" },
        ["ritualExtra"] = new LtStat { txt = v => "+" + Js.Num(v) + " altar in every ritual set" },
        ["abyssFork"] = new LtStat { txt = v => "+" + P(v) + "% chance an abyss forks" },
        ["legionTime"] = new LtStat { txt = v => "+" + Js.Num(v) + "s frozen in a legion" },
        ["sboxChance"] = new LtStat { txt = v => P(v) + "% more strongboxes" },
        ["goblinChance"] = new LtStat { txt = v => P(v) + "% more treasure goblins" },
        ["studMore"] = new LtStat { txt = v => P(v) + "% more studs from monsters" },
    };
    static LtNode LT(string id, string league, string name, NumMap stats, string[] links, bool notable = false, bool hub = false) =>
        new LtNode { id = id, league = league, name = name, stats = stats, links = links, notable = notable, hub = hub };
    public static readonly List<LtNode> LEAGUE_TREE = new()
    {
        LT("lt_root", null, "THE ATLAS", new NumMap(), new string[0], hub: true),
        /* breach */
        LT("lt_br1", "breach", "Thinning Veil", new NumMap(("breachWeight", 0.5)), new[] { "lt_root" }),
        LT("lt_br2", "breach", "Deeper Hands", new NumMap(("breachMobLvl", 1)), new[] { "lt_br1" }),
        LT("lt_br3", "breach", "Through the Tear", new NumMap(("breachWeight", 0.5), ("breachMobLvl", 1)), new[] { "lt_br2" }, true),
        /* abyss */
        LT("lt_ab1", "abyss", "Cracked Earth", new NumMap(("abyssWeight", 0.5)), new[] { "lt_root" }),
        LT("lt_ab2", "abyss", "Branching Rift", new NumMap(("abyssFork", 0.15)), new[] { "lt_ab1" }),
        LT("lt_ab3", "abyss", "The Deep Split", new NumMap(("abyssWeight", 0.5), ("abyssFork", 0.20)), new[] { "lt_ab2" }, true),
        /* ritual */
        LT("lt_ri1", "ritual", "Blood Calling", new NumMap(("ritualWeight", 0.5)), new[] { "lt_root" }),
        LT("lt_ri2", "ritual", "A Fourth Altar", new NumMap(("ritualExtra", 1)), new[] { "lt_ri1" }, true),
        /* legion */
        LT("lt_lg1", "legion", "Stopped Clocks", new NumMap(("legionWeight", 0.5)), new[] { "lt_root" }),
        LT("lt_lg2", "legion", "Longer Moment", new NumMap(("legionTime", 2)), new[] { "lt_lg1" }),
        LT("lt_lg3", "legion", "Eternal Stand", new NumMap(("legionWeight", 0.5), ("legionTime", 2)), new[] { "lt_lg2" }, true),
        /* the loot roads */
        LT("lt_lo1", "loot", "League Veteran", new NumMap(("leagueChance", 0.25)), new[] { "lt_root" }),
        LT("lt_lo2", "loot", "Heavy Pockets", new NumMap(("studMore", 0.15)), new[] { "lt_lo1" }),
        LT("lt_lo3", "loot", "Locksmith", new NumMap(("sboxChance", 0.30)), new[] { "lt_lo1" }),
        LT("lt_lo4", "loot", "Goblin Bait", new NumMap(("goblinChance", 1.0)), new[] { "lt_lo2" }, true),
    };
    public static readonly JsMap<string, LtNode> LEAGUE_TREE_BY_ID = LEAGUE_TREE.ToJsMap(n => n.id, n => n);
    public static LtreeState LTREE = new();
    public static NumMap ltreeCache = new();
    public static void ltreeRebuild()
    {
        ltreeCache = new NumMap();
        foreach (var id in LTREE.nodes)
        {
            var n = LEAGUE_TREE_BY_ID.GetValueOrDefault(id); if (n == null) continue;
            foreach (var (k, v) in n.stats) ltreeCache[k] = ltreeCache[k] + v;
        }
    }
    public static void ltreeLoad()
    {
        try
        {
            var o = JsonNode.Parse(Store.getItem("bb_ltree") ?? "null") as JsonObject;
            if (o != null && o["nodes"] is JsonArray arr)
            {
                var ids = arr.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)
                             .Where(id => id != null && id != "lt_root" && LEAGUE_TREE_BY_ID.ContainsKey(id));
                double pts = o["pts"] is JsonValue pv && pv.TryGetValue<double>(out var pd) ? pd : 0;
                LTREE = new LtreeState { pts = Math.Max(0, Js.I32(pts)), nodes = new[] { "lt_root" }.Concat(ids).ToList() };
            }
        }
        catch { }
        ltreeRebuild();
    }
    public static void ltreeSave()
    {
        try { Store.setItem("bb_ltree", new JsonObject { ["pts"] = LTREE.pts, ["nodes"] = new JsonArray(LTREE.nodes.Select(n => (JsonNode)n).ToArray()) }.ToJsonString()); } catch { }
    }
    /* a node is taken next to one you have, for a point */
    public static bool ltreeCanTake(string id)
    {
        var n = LEAGUE_TREE_BY_ID.GetValueOrDefault(id ?? "");
        if (n == null || LTREE.nodes.Contains(id) || LTREE.pts < 1) return false;
        return n.links.Any(l => LTREE.nodes.Contains(l));
    }
    public static bool ltreeTake(string id)
    {
        if (!ltreeCanTake(id)) return false;
        LTREE.nodes.Add(id); LTREE.pts--; ltreeRebuild(); ltreeSave();
        return true;
    }
    /* ...and given back only from the end of a road, so nothing is left hanging */
    public static bool ltreeCanRefund(string id)
    {
        if (id == "lt_root" || !LTREE.nodes.Contains(id)) return false;
        return !LTREE.nodes.Any(o => o != id && (LEAGUE_TREE_BY_ID.GetValueOrDefault(o)?.links ?? new string[0]).Contains(id));
    }
    public static bool ltreeRefund(string id)
    {
        if (!ltreeCanRefund(id)) return false;
        LTREE.nodes = LTREE.nodes.Where(o => o != id).ToList(); LTREE.pts++; ltreeRebuild(); ltreeSave();
        return true;
    }
    public static double leagueStat(string k) => LEAGUE_TREE_ON ? ltreeCache[k] : 0;
    /* THE HOOKS, one per knob */
    public static double leagueChanceNow() => LEAGUE_CHANCE * (1 + leagueStat("leagueChance"));
    public static double leagueKindWeight(string t) => 1 + leagueStat(t + "Weight");
    public static double ritualAltarCount() => RITUAL_N + leagueStat("ritualExtra");
    public static double abyssForkChance() => ABYSS_FORK + leagueStat("abyssFork");
    public static double legionTimeNow() => LEGION_T + leagueStat("legionTime");
    public static double sboxChanceNow() => SBOX_CHANCE * (1 + leagueStat("sboxChance"));
    public static double goblinChanceNow() => GOBLIN_CHANCE * (1 + leagueStat("goblinChance"));
    public static double studMoreMul() => 1 + leagueStat("studMore");
}
