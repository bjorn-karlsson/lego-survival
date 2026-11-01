namespace Brickblade;

/* =============================== ENEMIES (JS 6974–7215) ===============================
   A death, and what a body leaves behind. */
public sealed partial class Prop
{
    public string rar; public bool mimic, wild;
}
public static partial class G
{
    public static void killEnemy(Ent e, string source)
    {
        e.dead = true; player.kills++;
        logKill(e);
        if (rpgOn()) rpgOnKill(e);
        /* a death INSIDE an open breach feeds its clock; a breach body pays its lord's splinters */
        if (e.breach != null) breachOnKill(e);
        if (e.abyss != null) abyssOnKill(e);
        if (e.sbox != null) sboxOnKill(e);
        if (e.ritual != null || e.ritualGuard != null) ritualOnKill(e);
        rareModOnKill(e);
        specialOnKill(e);
        if (e.legion != null) legionOnKill(e);
        if (e.breach != null) breachSpoils(e);
        /* A FRENZY CHARGE IS EARNED BY KILLING -- first thing in the death */
        if (player.chargeKill > 0) stackAdd("frenzy", player.chargeKill);
        /* WHAT DIES CARRIES ON DYING */
        if (player.prolifR > 0) proliferate(e);
        /* ABSOLUTE ZERO: a body that dies frozen shatters */
        zeroShatter(e);
        /* BRICKBANE'S LEGENDARY: a body that dies with poison in it comes apart in a cloud */
        var po = e.poison;
        if (po != null && player.bane != null && player.bane.burst > 0 &&
            Js.R() < BANE_BURST[(int)player.bane.burst])
        {
            /* THE PAYOFF, and it is not an explosion: what spreads is the rot */
            double rad = BANE_BURST_R[(int)player.bane.burst];
            addRing(e.x, e.y, 8, 340, rad, "#8ede4a", 8, 0.55);
            addRing(e.x, e.y, 4, 240, rad * 0.6, "#d6ffa0", 5, 0.4);
            addSparks(e.x, e.y - 16, (int)(10 + po.n * 2), "#8ede4a");
            SFX.freeze();
            addHitNum(e.x, e.y - 52, "SPORES ×" + Js.Num(po.n), "#8ede4a", 17);
            /* a straight scan rather than the bucket grid */
            double dps = po.dps * BANE_BURST_KEEP, dur = Math.Max(2, po.t * 0.8);
            foreach (var e2 in enemies.ToList())
            {
                if (e2 == e || e2.dead || e2.state != "active") continue;
                if (dist(e2, e) > rad + Js.Or(e2.r, 12)) continue;
                for (int i = 0; i < po.n; i++) poisonEnemy(e2, dps, dur);
            }
        }
        zombieDeath(e);
        if (e.type == "direbat")
        {
            /* it doesn't die quietly -- it splits into the small ones */
            int n = (int)irnd(3, 5);
            for (int i = 0; i < n; i++)
            {
                double a = (double)i / n * TAU + rnd(-0.3, 0.3);
                spawnEnemy("cryptbat",
                    clamp(e.x + Math.Cos(a) * 52, W.wall + 40, W.w - W.wall - 40),
                    clamp(e.y + Math.Sin(a) * 52, W.wall + 40, W.h - W.wall - 40));
            }
            addRing(e.x, e.y, 14, 320, 120, "#b58fd6", 6, 0.5);
            addDmg(e.x, e.y - 70, "THE SWARM!", "#c9b8e8", 18);
            SFX.roar();
        }
        if (e.golem != null)
        {
            /* it doesn't topple, it comes apart -- the pieces get back up and run */
            var Gc = GOLEM_CONF[e.golem];
            int n = (int)irnd(3, 5);
            for (int i = 0; i < n; i++)
            {
                double a = (double)i / n * TAU + rnd(-0.3, 0.3);
                spawnEnemy(Gc.mini,
                    clamp(e.x + Math.Cos(a) * 46, W.wall + 40, W.w - W.wall - 40),
                    clamp(e.y + Math.Sin(a) * 46, W.wall + 40, W.h - W.wall - 40));
            }
            addRing(e.x, e.y, 16, 340, 140, Gc.ring, 7, 0.5);
            spawnBricks(e.x, e.y, Gc.bricks, 16, 240);
            addDust(e.x, e.y, 8, "rgba(200,200,190,");
            addDmg(e.x, e.y - 70, "IT BREAKS APART!", Gc.ring, 19);
            addShake(0.35);
            SFX.brick(6);
        }
        double fort = player.fortuneT > 0 ? player.fortuneMul : 1;
        var cols = new[] { e.col.head, e.col.torso, e.col.legs, e.col.head };
        spawnBricks(e.x, e.y, cols, e.type == "boss" ? 30 : 10 + (int)irnd(0, 4), e.type == "boss" ? 330 : 200);
        sfxAt(e.x).brick(e.type == "boss" ? 10 : 6);
        addShake(e.type == "boss" ? 0.55 : 0.16);
        double amount = irnd(e.studsDrop.a, e.studsDrop.b);
        amount = Math.Round(amount * player.studMul * (e.elite ? 2 : 1) * fort * studMoreMul());
        /* BANK WHAT YOU COLLECTED, NOT WHAT YOU ARE HOLDING */
        player.studsTotal += amount;
        spawnStuds(e.x, e.y, amount);
        if (e.type == "boss") { bossSpoils(e); respawnOnBoss(); }
        else mobSpoils(e);
        gainXp(e.xp * (e.elite ? 2 : 1) * fort * MOB_XP_SCALE, true);
        if (e.type == "boss")
        {
            if (metaBossKill(e.bkey)) logEvent("FIRST KILL — " + (e.bname ?? e.bkey) + " · +1 skill point", "#ffd76e");
            tScale = 0.25;
            for (int i = 0; i < 3; i++) spawnStuds(e.x + rnd(-40, 40), e.y + rnd(-40, 40), 12);
            showBanner(e.super ? "SUPER BOSS DOWN!" : "BOSS SMASHED!", "claim your reward...", "#ffb900", 2.4);
            SFX.roar(); addRing(e.x, e.y, 20, 600, 320, "#ffb900", 10, 0.7);
            queuePick("boss", null);
        }
    }
    /* =============================== WHAT A BODY LEAVES =============================== */
    public static readonly NumMap DROP = new(
        ("heart", 0.0100),
        ("magnet", 0.0034),
        ("fortune", 0.0034),
        ("rage", 0.0008),
        ("ward", 0.0008),
        ("chest", 0.0017),
        ("gheart", 0.0011),
        ("wild", 0.0006));
    public const double DROP_ELITE = 8;        /* an elite rolls every one of them eight times over */
    public static readonly NumMap FLOOR_CAP = new(("heart", 10), ("magnet", 3), ("fortune", 3), ("rage", 3), ("ward", 3), ("gheart", 3));
    public const int CHEST_CAP = 5;
    public static bool floorRoom(string kind)
    {
        if (!FLOOR_CAP.Has(kind)) return true;
        double cap = FLOOR_CAP[kind];
        int n = 0;
        foreach (var pk in pickups) if (pk.kind == kind && !pk.got && ++n >= cap) return false;
        return true;
    }
    public static bool chestRoom()
    {
        int n = 0;
        foreach (var pr in props) if (pr.type == "chest" && !pr.dead && ++n >= CHEST_CAP) return false;
        return true;
    }
    /* A boss always leaves a heart and a magnet, and then rolls for the rest */
    public const double BOSS_EXTRA = 0.28;
    public const double BOSS_EXTRA_BUFF = 0.12;
    /* A BREACH IS A SWARM: a breach body's chest roll is a twelfth of anybody else's */
    public const double BREACH_CHEST_DROP = 1.0 / 12;
    static Prop Chest(double x, double y, string rar, bool wild = false, bool mimic = false) =>
        new Prop { type = "chest", wild = wild, x = x, y = y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16, rar = rar, mimic = mimic };
    public static void mobSpoils(Ent e)
    {
        var p = player; double m = e.elite ? DROP_ELITE : 1;
        double cm = m * (e.breach != null ? BREACH_CHEST_DROP : 1);
        Pt at(double dx, double dy) => new Pt(e.x + rnd(-dx, dx), e.y + rnd(-dy, dy));
        /* the tree's heart drops are an INCREASE to the chance */
        if (Js.R() < heartDropChance() * m && floorRoom("heart"))
            pickups.Add(makePickup("heart", e.x, e.y));
        if (Js.R() < DROP["magnet"] * m && floorRoom("magnet"))
            pickups.Add(makePickup("magnet", at(10, 10).x, at(10, 10).y));
        if (Js.R() < DROP["gheart"] * m && floorRoom("gheart"))
            pickups.Add(makePickup("gheart", at(14, 14).x, at(14, 14).y));
        if (Js.R() < DROP["fortune"] * m && floorRoom("fortune"))
            pickups.Add(makePickup("fortune", at(18, 18).x, at(18, 18).y));
        if (Js.R() < DROP["rage"] * m && floorRoom("rage"))
            pickups.Add(makePickup("rage", at(22, 22).x, at(22, 22).y));
        if (Js.R() < DROP["ward"] * m && floorRoom("ward"))
            pickups.Add(makePickup("ward", at(26, 26).x, at(26, 26).y));
        if (Js.R() < DROP["chest"] * cm && chestRoom())
        {
            double cx = e.x, cy = e.y; string rar = rollRarity("chest");
            bool mim = wave.n >= MIMIC_FROM && Js.R() < MIMIC_CHANCE;
            props.Add(shoveToFloor(Chest(cx, cy, rar, false, mim), 20));
        }
        if (Js.R() < DROP["wild"] * cm && chestRoom())
            props.Add(shoveToFloor(Chest(e.x + rnd(-12, 12), e.y + rnd(-12, 12), "epic", true), 20));
    }
    public static void bossSpoils(Ent e)
    {
        pickups.Add(makePickup("heart", e.x - 22, e.y));
        pickups.Add(makePickup("magnet", e.x + 22, e.y));
        void maybe(string kind, double dx, double dy)
        {
            if (Js.R() < BOSS_EXTRA)
                pickups.Add(makePickup(kind, e.x + dx, e.y + dy));
        }
        maybe("fortune", -40, 18);
        void rare(string kind, double dx, double dy)
        {
            if (Js.R() < BOSS_EXTRA_BUFF) pickups.Add(makePickup(kind, e.x + dx, e.y + dy));
        }
        rare("rage", 40, 18); rare("ward", 0, 34);
        if (Js.R() < BOSS_EXTRA * 0.6) pickups.Add(makePickup("gheart", e.x, e.y - 26));
        /* a boss always leaves a chest, painted at least rare */
        { double cx = e.x + rnd(-60, 60), cy = e.y + rnd(30, 60); props.Add(shoveToFloor(Chest(cx, cy, rollRarity("boss")), 20)); }
        /* ...and one in six leave the mystery one too */
        if (Js.R() < 0.17 && chestRoom())
        {
            double cx = e.x + rnd(-70, 70), cy = e.y + rnd(30, 70);
            props.Add(shoveToFloor(Chest(cx, cy, "epic", true), 20));
        }
        /* ...and the only gear in the game: one piece, two off a super boss */
        gearBossSpoils(e);
    }
}
