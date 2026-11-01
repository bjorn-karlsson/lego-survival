namespace Brickblade;

/* ================================== RITUAL (JS 10432–10989) ==================================
   A set of three altars, each ringed by a sleeping pack. Kill a pack, step onto
   its altar, and a circle closes: the altar calls monsters in pulses and
   strikes at you itself. Every monster it calls pays tribute, spent at the
   ritual shop (U) on gear or chests -- or, in the arcade, on cards. */
public sealed class RitualPending { public double delay; }
public sealed class RitualFx : Obj { public string kind; public Ritual A; public double t, T, wait; public bool hit, done; }
public sealed class RitualOffer
{
    public int id; public string kind, key, rar, uid; public GearItem it; public double cost, defers; public bool carried;
}
public sealed class RitualState
{
    public List<Ritual> altars = new(); public RitualPending pending; public double tribute;
    public List<RitualOffer> offers = new(), deferred = new(); public double rerollN;
    public List<RitualFx> fx = new(); public Ritual active; public double set; public int uid = 1; public bool closed;
}
/* one altar */
public sealed partial class Ritual { public string state; public double guards, seed, left, pulseT, abilT, t, earned, set; public bool woke; }
public sealed partial class Ent { public double ritualSide = double.NaN; public bool gone; }

public static partial class G
{
    public const double RITUAL_FROM = 7;
    public const double RITUAL_N = 3;
    public static readonly double[] RITUAL_DELAY = { 4, 10 };
    public const double RITUAL_R = 450;
    public const double RITUAL_TOUCH = 40;
    public const double RITUAL_WAKE_R = 210;
    public const double RITUAL_GAP = 1100;
    public static readonly double[] RITUAL_GUARD = { 5, 7 };
    /* A RITUAL IS A HORDE */
    public static double ritualBudget(double n) => 24 + n;
    public const double RITUAL_PULSE_T = 1.6, RITUAL_ALIVE = 26;
    public static readonly double[] RITUAL_PACK = { 6, 9 };
    public const double RITUAL_MAGIC = 0.14, RITUAL_ELITE = 0.06;
    public static readonly double[] RITUAL_ABIL_T = { 3.2, 4.8 };
    public const double RITUAL_MARK_R = 72, RITUAL_MARK_T = 1.15;
    public const double RITUAL_RING_WAIT = 0.7, RITUAL_RING_SPD = 250, RITUAL_RING_BAND = 14;
    public const double RITUAL_TRIB = 4;
    public const string RITUAL_COL = "#ff3a4a";
    public const int RITUAL_OFFERS = 8, RITUAL_DEFER_MAX = 6;
    public const double RITUAL_DEFER_MUL = 0.9;
    public const double RITUAL_REROLL = 60, RITUAL_REROLL_GROW = 1.5;
    /* what an offer costs at MOB LV 0, before the level's worth */
    public static readonly NumMap RITUAL_COST = new(("magic", 90), ("rare", 260), ("chest:rare", 200), ("chest:epic", 600), ("chest:legendary", 2800),
        /* ARCADE sells CARDS where the RPG sells gear */
        ("card:uncommon", 110), ("card:rare", 260), ("card:epic", 760), ("card:legendary", 3600));
    public static readonly (string k, double w)[] RITUAL_CARD_RAR_W = { ("uncommon", 0.34), ("rare", 0.36), ("epic", 0.22), ("legendary", 0.08) };
    public static readonly (string k, double w)[] RITUAL_OFFER_W = { ("magic", 0.24), ("rare", 0.28), ("chest:rare", 0.24), ("chest:epic", 0.16), ("chest:legendary", 0.08) };
    /* THE RING IS A WALL for every missile, both ways -- except what stands still */
    public static readonly string[] RITUAL_FOG_STILL = { "vtotem", "gas" };
    public static RitualState ritualBlank() => new RitualState();
    public static RitualState ritualS = ritualBlank();
    public static bool ritualActiveNow() => ritualS.active != null;
    /* THE RING IS A WALL FOR HARM, not only for missiles */
    public static bool ritualOutside(Obj o)
    {
        var A = ritualS.active;
        if (A == null || o == null || !double.IsFinite(o.x) || !double.IsFinite(o.y)) return false;
        if (o is Ent oe && !double.IsNaN(oe.ritualSide)) return oe.ritualSide < 0;
        return hypot(o.x - A.x, o.y - A.y) >= RITUAL_R;
    }
    public static List<Ritual> ritualLive() => ritualS.altars.Where(A => A.state != "done").ToList();
    /* one set at a time */
    public static bool ritualCanRise() => ritualS.pending == null && ritualLive().Count == 0;
    public static void ritualRoll(double n, double k = 1)
    {
        if (sandbox || n < RITUAL_FROM || n % 5 == 0 || k <= 0) return;
        if (!ritualCanRise()) return;
        ritualS.pending = new RitualPending { delay = rnd(RITUAL_DELAY[0], RITUAL_DELAY[1]) };
    }
    /* a spot for an altar: open floor under most of its circle */
    public static Pt ritualSpot(List<Pt> taken)
    {
        double pad = RITUAL_R + 60;
        for (int i = 0; i < 160; i++)
        {
            double a = rnd(TAU), rr = rnd(520, 1700);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            if (!abyssFloorOk(x, y, pad)) continue;
            if (taken.Any(q => hypot(q.x - x, q.y - y) < RITUAL_GAP)) continue;
            if (props.Any(q => !q.dead && q.type == "strongbox" && hypot(q.x - x, q.y - y) < 160)) continue;
            int ok = 0;
            for (int k = 0; k < 8; k++) if (breachFloorOk(x + Math.Cos(k / 8.0 * TAU) * RITUAL_R * 0.6, y + Math.Sin(k / 8.0 * TAU) * RITUAL_R * 0.6)) ok++;
            if (ok >= 6) return new Pt(x, y);
        }
        return null;
    }
    public static bool ritualPlaceSet()
    {
        var taken = new List<Pt>();
        for (int i = 0; i < ritualAltarCount(); i++) { var at = ritualSpot(taken); if (at != null) taken.Add(at); }
        if (taken.Count == 0) return false;
        ritualS.set++;
        ritualS.altars = taken.Select(at => new Ritual
        {
            x = at.x, y = at.y, state = "guarded", guards = 0, woke = false, seed = rnd(1000),
            left = 0, pulseT = 0, abilT = 0, t = 0, earned = 0, set = ritualS.set
        }).ToList();
        foreach (var A in ritualS.altars) ritualGuards(A);
        ritualOffersNew();
        showBanner("RITUAL", ritualS.altars.Count + " ALTARS HAVE RISEN — KILL THEIR GUARDS", RITUAL_COL, 2.2, 46);
        logEvent("   RITUAL — " + ritualS.altars.Count + " altars have risen; their guards sleep until you come for them", RITUAL_COL);
        SFX.roar();
        return true;
    }
    /* the sleeping pack round an altar */
    public static void ritualGuards(Ritual A)
    {
        var types = abyssTypes(wave.n); string type = types[(int)(Js.R() * types.Count)];
        double n = irnd(RITUAL_GUARD[0], RITUAL_GUARD[1]);
        for (int k = 0; k < n; k++)
        {
            double a = k / n * TAU + rnd(-0.2, 0.2), rr = rnd(70, 130);
            var at = floorAt(A.x + Math.Cos(a) * rr, A.y + Math.Sin(a) * rr, 14);
            int n0 = enemies.Count;
            spawnEnemy(type, at.x, at.y, false, k == 0);
            if (enemies.Count == n0) continue;
            var e = enemies[^1];
            e.dormant = true; e.ritualGuard = A; e.faceA = a + Math.PI;
            A.guards++;
        }
    }
    /* THE PACK WAKES AS ONE */
    public static void ritualWake(Ritual A)
    {
        if (A == null || A.woke) return;
        A.woke = true;
        foreach (var e in enemies) if (e.ritualGuard == A && e.dormant) { e.dormant = false; e.atkCd = Math.Max(e.atkCd, 0.35); }
        addRing(A.x, A.y, 20, 320, 160, RITUAL_COL, 4, 0.45);
        logEvent("   the altar's guards wake", RITUAL_COL);
    }
    /* a sleeping guard does nothing at all, until something wakes its pack */
    public static bool ritualDormantTick(Ent e)
    {
        var h = heroObj ?? player; var A = e.ritualGuard;
        /* a pack in an RPG zone wakes all together, at the zone's own distance */
        if (Js.T(e.rpgPack))
        {
            if (h != null && !h.dead && hypot(h.x - e.x, h.y - e.y) < RPG_AGGRO_R) { rpgWakePack(e.rpgPack); return false; }
            e.moving = false; collideWorld(e); return true;
        }
        if (h != null && !h.dead && (hypot(h.x - e.x, h.y - e.y) < RITUAL_WAKE_R ||
                                     (A != null && hypot(h.x - A.x, h.y - A.y) < RITUAL_WAKE_R)))
        {
            if (A != null) ritualWake(A); else e.dormant = false;
            return false;
        }
        e.moving = false;
        collideWorld(e);
        return true;
    }
    public static double ritualTribute(Ent e) => Math.Max(1, Math.Round(RITUAL_TRIB * mobRankMul(e.mlvl) * (e.elite ? 4 : e.magic ? 2 : 1)));
    public static void ritualOnKill(Ent e)
    {
        if (e.ritualGuard != null)
        {
            var A = e.ritualGuard;
            A.guards = Math.Max(0, A.guards - 1);
            return;
        }
        if (e.ritual == null) return;
        double t = ritualTribute(e);
        ritualS.tribute += t; e.ritual.earned += t;
        addDmg(e.x, e.y - 50 * Js.Or(e.scale, 1), "+" + Js.Num(t) + " TRIBUTE", RITUAL_COL, 13);
    }
    /* STEP ONTO A FREE ALTAR: the circle closes */
    public static void ritualStart(Ritual A)
    {
        A.state = "active"; A.t = 0; A.left = ritualBudget(wave.n); A.pulseT = 0.9; A.abilT = rnd(RITUAL_ABIL_T[0], RITUAL_ABIL_T[1]); A.earned = 0;
        ritualS.active = A;
        /* what is inside now stays inside, and what is outside stays out */
        foreach (var e in enemies) e.ritualSide = hypot(e.x - A.x, e.y - A.y) < RITUAL_R ? 1 : -1;
        /* the legion comes in with you */
        foreach (var m in minions)
        {
            double d = hypot(m.x - A.x, m.y - A.y);
            if (d > RITUAL_R - 30) { double a = rnd(TAU), rr = rnd(40, RITUAL_R * 0.5); m.x = A.x + Math.Cos(a) * rr; m.y = A.y + Math.Sin(a) * rr; }
        }
        showBanner("THE RITUAL BEGINS", "SURVIVE WHAT THE ALTAR CALLS", RITUAL_COL, 1.8, 44);
        logEvent("   the ritual begins — the circle holds you until it is done", RITUAL_COL);
        addRing(A.x, A.y, 10, 700, RITUAL_R, RITUAL_COL, 6, 0.6); addShake(0.3); SFX.roar();
    }
    public static Pt ritualInsideSpot(Ritual A, double pad)
    {
        var h = heroObj ?? player;
        for (int i = 0; i < 20; i++)
        {
            double a = rnd(TAU), rr = rnd(RITUAL_R * 0.3, RITUAL_R - pad);
            var at = floorAt(A.x + Math.Cos(a) * rr, A.y + Math.Sin(a) * rr, 14);
            if (hypot(at.x - A.x, at.y - A.y) > RITUAL_R - pad) continue;
            if (h != null && hypot(at.x - h.x, at.y - h.y) < 110) continue;
            return at;
        }
        double a2 = rnd(TAU);
        return new Pt(A.x + Math.Cos(a2) * RITUAL_R * 0.6, A.y + Math.Sin(a2) * RITUAL_R * 0.6);
    }
    /* the altar calls a pulse of its own monsters into the circle */
    public static void ritualPulse(Ritual A)
    {
        var types = abyssTypes(wave.n); string type = types[(int)(Js.R() * types.Count)];
        double n = Math.Min(A.left, irnd(RITUAL_PACK[0], RITUAL_PACK[1]));
        for (int k = 0; k < n; k++)
        {
            var at = ritualInsideSpot(A, 24); int n0 = enemies.Count;
            bool elite = Js.R() < RITUAL_ELITE, magic = !elite && Js.R() < RITUAL_MAGIC;
            spawnEnemy(type, at.x, at.y, elite, magic);
            A.left--;
            if (enemies.Count == n0) continue;
            var e = enemies[^1];
            e.ritual = A; e.ritualSide = 1;
            addRing(at.x, at.y, 4, 180, 44, RITUAL_COL, 3, 0.4);
        }
    }
    /* WHAT ONE OF THE ALTAR'S STRIKES HITS FOR */
    public static double ritualHit(double k)
    {
        double lvl = mobLevel(), rankMul = mobRankMul();
        double dmgB = ((lvl - 1) * 0.55 + Math.Pow(Math.Max(0, lvl - 8), 1.45) * 0.14) * rankMul;
        return Math.Max(1, Math.Round((10 + dmgB) * Js.Or(difficulty.dmg, 1) * k));
    }
    /* THE ALTAR STRIKES: a mark under you that bursts, or a ring out of the altar */
    public static void ritualCast(Ritual A, string kind = null)
    {
        var h = heroObj ?? player;
        kind ??= Js.R() < 0.6 ? "mark" : "ring";
        if (kind == "mark")
        {
            int n = wave.n >= 30 ? 3 : wave.n >= 20 ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                double a = rnd(TAU), off = i != 0 ? rnd(70, 140) : 0;
                double x = h.x + Math.Cos(a) * off, y = h.y + Math.Sin(a) * off;
                double d = hypot(x - A.x, y - A.y), lim = RITUAL_R - RITUAL_MARK_R * 0.5;
                if (d > lim) { x = A.x + (x - A.x) * lim / d; y = A.y + (y - A.y) * lim / d; }
                ritualS.fx.Add(new RitualFx { kind = "mark", A = A, x = x, y = y, r = RITUAL_MARK_R, t = -i * 0.25, T = RITUAL_MARK_T, dmg = ritualHit(1.6) });
            }
        }
        else
        {
            ritualS.fx.Add(new RitualFx { kind = "ring", A = A, x = A.x, y = A.y, r = 18, t = 0, wait = RITUAL_RING_WAIT, hit = false, dmg = ritualHit(1.1) });
        }
        addRing(A.x, A.y, 6, 200, 50, RITUAL_COL, 4, 0.35);
    }
    public static void ritualFxTick(double dt)
    {
        var h = heroObj ?? player;
        foreach (var f in ritualS.fx.ToList())
        {
            f.t += dt;
            if (f.kind == "mark")
            {
                if (f.t >= f.T && !f.done)
                {
                    f.done = true;
                    if (h != null && !h.dead && hypot(h.x - f.x, h.y - f.y) < f.r + Js.Or(h.r, 12))
                        playerDamage(f.dmg, new Obj { x = f.x, y = f.y, type = "ritual", bname = "the ritual altar" }, "fire", true);
                    addRing(f.x, f.y, 10, 520, f.r + 10, "#ff6a3a", 6, 0.35);
                    addSparks(f.x, f.y - 6, 10, "#ff8a4a"); addShake(0.12);
                    SFX.boomSmall();
                }
            }
            else if (f.kind == "ring")
            {
                if (f.t < f.wait) continue;
                f.r += RITUAL_RING_SPD * dt;
                if (!f.hit && h != null && !h.dead && Math.Abs(hypot(h.x - f.x, h.y - f.y) - f.r) < RITUAL_RING_BAND + Js.Or(h.r, 12))
                {
                    f.hit = true; playerDamage(f.dmg, new Obj { x = f.x, y = f.y, type = "ritual", bname = "the ritual altar" }, "chaos", true);
                }
                if (f.r >= RITUAL_R) f.done = true;
            }
        }
        ritualS.fx = ritualS.fx.Filter(f => !(f.done && (f.kind != "mark" || f.t > f.T + 0.05)));
    }
    public static void ritualDone(Ritual A)
    {
        A.state = "done"; ritualS.active = null; ritualS.fx = ritualS.fx.Filter(f => f.A != A);
        foreach (var e in enemies) e.ritualSide = double.NaN;
        int left = ritualLive().Count;
        showBanner(left != 0 ? "RITUAL COMPLETE" : "ALL RITUALS COMPLETE",
            "+" + Js.Num(A.earned) + " TRIBUTE · " + Js.Num(ritualS.tribute) + " TO SPEND · U", RITUAL_COL, 2.0, 44);
        logEvent("   ritual complete: +" + Js.Num(A.earned) + " tribute (" + Js.Num(ritualS.tribute) + " held) — U to spend it" + (left != 0 ? ", or save it for the other " + left : ""), RITUAL_COL);
        addRing(A.x, A.y, 10, 600, RITUAL_R, "#ffd76e", 6, 0.6);
        SFX.fortune();
    }
    /* THE CIRCLE HOLDS */
    public static void ritualClamp()
    {
        var A = ritualS.active;
        if (A == null) return;
        void inside(Obj o, double pad)
        {
            double dx = o.x - A.x, dy = o.y - A.y, d = hypot(dx, dy), lim = RITUAL_R - pad;
            if (d > lim && d > 0.01) { o.x = A.x + dx * lim / d; o.y = A.y + dy * lim / d; }
        }
        void outside(Obj o, double pad)
        {
            double dx = o.x - A.x, dy = o.y - A.y, d = hypot(dx, dy), lim = RITUAL_R + pad;
            if (d < lim) { double k = d > 0.01 ? lim / d : 0; o.x = d > 0.01 ? A.x + dx * k : A.x + lim; o.y = d > 0.01 ? A.y + dy * k : A.y; }
        }
        var h = heroObj ?? player;
        if (h != null && !h.dead) inside(h, Js.Or(h.r, 12) + 2);
        foreach (var m in minions) if (!m.gone) inside(m, Js.Or(m.r, 10) + 2);
        foreach (var e in enemies)
        {
            if (e.dead) continue;
            if (double.IsNaN(e.ritualSide)) e.ritualSide = e.ritual == A || hypot(e.x - A.x, e.y - A.y) < RITUAL_R ? 1 : -1;
            if (e.ritualSide > 0) inside(e, Js.Or(e.r, 12)); else outside(e, Js.Or(e.r, 12));
        }
        foreach (var o in bloodOrbs)
        {
            if ((hypot(o.px - A.x, o.py - A.y) >= RITUAL_R) != (hypot(o.x - A.x, o.y - A.y) >= RITUAL_R)) { o.dead = true; addDust(o.x, o.y, 3, "rgba(120,40,50,"); }
        }
        bloodOrbs = bloodOrbs.Filter(o => !o.dead);
        foreach (var pr in projectiles)
        {
            if (pr.dead || Array.IndexOf(RITUAL_FOG_STILL, pr.kind) >= 0 || double.IsNaN(pr.px)) continue;
            double was = hypot(pr.px - A.x, pr.py - A.y), now = hypot(pr.x - A.x, pr.y - A.y);
            if ((was >= RITUAL_R) != (now >= RITUAL_R)) { pr.dead = true; addDust(pr.x, pr.y, 3, "rgba(120,40,50,"); }
        }
    }
    public static void updateRitual(double dt)
    {
        if (player == null) return;
        var S = ritualS;
        if (S.pending != null && wave.state == "fight")
        {
            S.pending.delay -= dt;
            if (S.pending.delay <= 0) { S.pending = null; ritualPlaceSet(); }
        }
        foreach (var A0 in S.altars)
        {
            if (A0.state == "guarded")
            {
                A0.guards = enemies.Count(e => e.ritualGuard == A0 && !e.dead);
                if (A0.guards <= 0)
                {
                    A0.state = "ready"; A0.woke = true;
                    addRing(A0.x, A0.y, 10, 400, 120, RITUAL_COL, 5, 0.5);
                    logEvent("   an altar is free — step onto it to begin the ritual", RITUAL_COL);
                }
            }
        }
        var h = heroObj ?? player;
        if (S.active == null && h != null && !h.dead)
        {
            foreach (var A0 in S.altars) if (A0.state == "ready" && hypot(h.x - A0.x, h.y - A0.y) < RITUAL_TOUCH + Js.Or(h.r, 12)) { ritualStart(A0); break; }
        }
        var A = S.active;
        if (A != null)
        {
            A.t += dt;
            int alive = enemies.Count(e => e.ritual == A && !e.dead);
            if (A.left > 0)
            {
                A.pulseT -= dt;
                if (A.pulseT <= 0 && alive < RITUAL_ALIVE) { ritualPulse(A); A.pulseT = RITUAL_PULSE_T; }
            }
            A.abilT -= dt;
            if (A.abilT <= 0) { ritualCast(A); A.abilT = rnd(RITUAL_ABIL_T[0], RITUAL_ABIL_T[1]) * (wave.n >= 30 ? 0.8 : 1); }
            if (A.left <= 0 && alive == 0) ritualDone(A);
        }
        ritualFxTick(dt);
    }
    /* ---- THE OFFER ---- */
    public static double ritualCostOf(string key) => Math.Round(RITUAL_COST[key] * mobRankMul() * rnd(0.88, 1.12));
    public static RitualOffer ritualOffer()
    {
        double r = Js.R() * RITUAL_OFFER_W.Sum(q => q.w); string key = RITUAL_OFFER_W[0].k;
        foreach (var (k, w) in RITUAL_OFFER_W) { r -= w; if (r <= 0) { key = k; break; } }
        /* an arcade ritual sells no gear: its gear offers are cards instead */
        if ((key == "magic" || key == "rare") && !modeHasGear()) { var c = ritualCard(); if (c != null) return c; key = "chest:rare"; }
        if (key == "magic" || key == "rare")
        {
            double lv = gearItemLevel() + 2; var B = gearRollBase(lv);
            if (B != null) return new RitualOffer { id = ritualS.uid++, kind = "gear", key = key, it = gearMake(B.id, key, lv), cost = ritualCostOf(key) };
            key = "chest:rare";
        }
        return new RitualOffer { id = ritualS.uid++, kind = "chest", key = key, rar = key.Substring(6), cost = ritualCostOf(key) };
    }
    /* A CARD ON THE ALTAR */
    public static bool ritualCardUsable(RitualOffer o)
    {
        var u = UPGRADES.FirstOrDefault(x => x.id == o.uid);
        if (u == null) return false;
        try { return (u.req == null || u.req()) && rarAllows(u, o.rar); } catch { return false; }
    }
    public static bool ritualOfferUsable(RitualOffer o) => o.kind != "card" || ritualCardUsable(o);
    public static RitualOffer ritualCard()
    {
        double r = Js.R() * RITUAL_CARD_RAR_W.Sum(q => q.w); string rar = RITUAL_CARD_RAR_W[0].k;
        foreach (var (k, w) in RITUAL_CARD_RAR_W) { r -= w; if (r <= 0) { rar = k; break; } }
        var shown = new HashSet<string>(ritualS.offers.Concat(ritualS.deferred).Where(q => q.kind == "card").Select(q => q.uid));
        List<Upgrade> fits(string R) => UPGRADES.Where(u => !shown.Contains(u.id) && ritualCardUsable(new RitualOffer { uid = u.id, rar = R })).ToList();
        var cands = fits(rar);
        for (int rk = rarRank(rar) - 1; cands.Count == 0 && rk >= rarRank("uncommon"); rk--) { rar = RAR_ORDER[rk]; cands = fits(rar); }
        if (cands.Count == 0) return null;
        double wOf(Upgrade u) => shunMul(u.id) * cardWeight(u);
        double total = 0; foreach (var u in cands) total += wOf(u);
        double x = Js.R() * total; var t = cands[^1];
        foreach (var u in cands) { x -= wOf(u); if (x < 0) { t = u; break; } }
        string key = "card:" + rar;
        return new RitualOffer { id = ritualS.uid++, kind = "card", key = key, uid = t.id, rar = rar, cost = ritualCostOf(key) };
    }
    /* a new set's offer: what you deferred last time first, then fresh ones */
    public static void ritualOffersNew()
    {
        var carry = ritualS.deferred.Select(o => { o.carried = true; return o; }).ToList();
        ritualS.deferred = new List<RitualOffer>();
        ritualS.rerollN = 0;
        ritualS.closed = false;
        ritualS.offers = carry.Take(RITUAL_OFFERS).ToList();
        while (ritualS.offers.Count < RITUAL_OFFERS) ritualS.offers.Add(ritualOffer());
    }
    public static bool ritualCanShop() => !ritualS.closed && ritualS.altars.Any(A => A.state == "done") && ritualS.offers.Count > 0;
    /* NOTHING LEFT TO DO */
    public static bool ritualStuck()
    {
        var S = ritualS;
        if (S.offers.Count == 0) return true;
        return S.offers.All(o => o.cost > S.tribute || !ritualOfferUsable(o)) && S.deferred.Count >= RITUAL_DEFER_MAX && S.tribute < ritualRerollCost();
    }
    /* DONE: the window closes, and once every altar is finished the set's offer ends */
    public static void ritualFinish()
    {
        var S = ritualS;
        if (S.altars.Count > 0 && S.altars.All(A => A.state == "done")) { S.offers = new List<RitualOffer>(); S.closed = true; }
        if (ritualOn) toggleRitual();
    }
    public static double ritualRerollCost() => Math.Round(RITUAL_REROLL * mobRankMul() * Math.Pow(RITUAL_REROLL_GROW, ritualS.rerollN));
    public static string ritualBuy(int id)
    {
        var o = ritualS.offers.FirstOrDefault(q => q.id == id);
        if (o == null) return "gone";
        if (!ritualOfferUsable(o)) return "maxed";   /* a card you have since capped out of */
        if (ritualS.tribute < o.cost) return "poor";
        ritualS.tribute -= o.cost;
        ritualS.offers = ritualS.offers.Filter(q => q != o);
        if (o.kind == "card")
        {
            applyPick(UPGRADES.FirstOrDefault(x => x.id == o.uid), o.rar);
            addRing(player.x, player.y, 10, 400, 130, RARITY[o.rar].col, 6, 0.5);
            return "card";
        }
        if (o.kind == "gear")
        {
            if (!gearInvAdd(o.it)) { gearDropAt(o.it, player.x, player.y); return "floor"; }
            logEvent("RITUAL — " + gearName(o.it), GEAR_RAR[o.it.r].col);
            return "bag";
        }
        queuePick("chest", o.rar);
        logEvent("RITUAL — a " + o.rar + " chest, waiting on R", (RARITY.GetValueOrDefault(o.rar ?? "") ?? RARITY["common"]).col);
        return "chest";
    }
    public static bool ritualDefer(int id)
    {
        var o = ritualS.offers.FirstOrDefault(q => q.id == id);
        if (o == null || ritualS.deferred.Count >= RITUAL_DEFER_MAX) return false;
        ritualS.offers = ritualS.offers.Filter(q => q != o);
        o.cost = Math.Round(o.cost * RITUAL_DEFER_MUL);
        o.defers = o.defers + 1;
        o.carried = false;
        ritualS.deferred.Add(o);
        return true;
    }
    public static bool ritualReroll()
    {
        double c = ritualRerollCost();
        if (ritualS.tribute < c) return false;
        ritualS.tribute -= c; ritualS.rerollN++;
        var keep = ritualS.offers.Where(o => o.carried).ToList();
        ritualS.offers = keep;
        while (ritualS.offers.Count < RITUAL_OFFERS) ritualS.offers.Add(ritualOffer());
        return true;
    }
    /* ---- DRAWN ---- */
    static readonly (double, double)[] ALTAR_HORNS = { (-30, -4), (30, -4), (-16, 8), (16, 8) };
    public static void drawRitualAltar(Ctx c, Ritual A)
    {
        double pulse = 0.5 + 0.5 * Math.Sin(vtime * 3 + A.seed);
        bool lit = A.state == "ready" || A.state == "active", done = A.state == "done";
        /* the circle it will close, faint until it does */
        if (!done && A.state != "active")
        {
            c.save(); c.setLineDash(new double[] { 14, 12 }); c.lineDashOffset = -vtime * 18;
            c.strokeStyle = rgba(255, 58, 74, A.state == "ready" ? 0.35 + 0.2 * pulse : 0.16); c.lineWidth = 2.5;
            c.beginPath(); c.arc(A.x, A.y, RITUAL_R, 0, TAU); c.stroke(); c.restore();
        }
        /* the stone */
        c.fillStyle = done ? "rgba(40,40,44,0.7)" : "rgba(30,8,14,0.72)";
        c.beginPath(); c.ellipse(A.x, A.y, 46, 22, 0, 0, TAU); c.fill();
        c.strokeStyle = done ? (Col)"#5a5f66" : rgba(255, 58, 74, lit ? 0.6 + 0.4 * pulse : 0.35); c.lineWidth = 3;
        c.beginPath(); c.ellipse(A.x, A.y, 46, 22, 0, 0, TAU); c.stroke();
        /* runes round the rim */
        if (!done)
        {
            c.fillStyle = rgba(255, 120, 120, lit ? 0.8 : 0.35);
            for (int i = 0; i < 8; i++)
            {
                double a = i / 8.0 * TAU + vtime * (lit ? 0.8 : 0.2);
                c.fillRect(A.x + Math.Cos(a) * 38 - 2, A.y + Math.Sin(a) * 17 - 2, 4, 4);
            }
        }
        /* four horns of stone */
        c.fillStyle = done ? "#4a4e54" : "#3a2a2e";
        foreach (var (dx, dy) in ALTAR_HORNS)
        {
            c.beginPath(); c.moveTo(A.x + dx - 6, A.y + dy); c.lineTo(A.x + dx, A.y + dy - 30); c.lineTo(A.x + dx + 6, A.y + dy); c.closePath(); c.fill();
        }
        /* the shard, floating over it, while it lives */
        if (!done)
        {
            double bob = Math.Sin(vtime * 2 + A.seed) * 4, y0 = A.y - 44 + bob;
            c.fillStyle = rgba(255, 58, 74, lit ? 0.25 + 0.2 * pulse : 0.12);
            c.beginPath(); c.arc(A.x, y0, 20 + 6 * pulse, 0, TAU); c.fill();
            c.fillStyle = lit ? "#ff3a4a" : "#8a2a34"; c.strokeStyle = "rgba(20,4,8,0.8)"; c.lineWidth = 1.5;
            c.beginPath(); c.moveTo(A.x, y0 - 16); c.lineTo(A.x + 8, y0); c.lineTo(A.x, y0 + 14); c.lineTo(A.x - 8, y0); c.closePath(); c.fill(); c.stroke();
        }
        c.font = "700 10px Fredoka"; c.textAlign = "center"; c.textBaseline = "middle";
        c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 3;
        string label = A.state == "guarded" ? "RITUAL ALTAR · GUARDED" : A.state == "ready" ? "STEP IN TO BEGIN" : A.state == "done" ? "COMPLETE" : "";
        if (label != "") { c.strokeText(label, A.x, A.y + 34); c.fillStyle = done ? "#9ba19d" : RITUAL_COL; c.fillText(label, A.x, A.y + 34); }
    }
    public static void drawRitual()
    {
        var c = ctx;
        foreach (var A in ritualS.altars) if (onScreen(A, RITUAL_R + 40)) drawRitualAltar(c, A);
        /* a dark stain under a sleeping guard, and a red one under a woken one */
        foreach (var e in enemies)
        {
            if (!(e.ritualGuard != null || e.ritual != null) || e.dead || !onScreen(e, 40)) continue;
            c.fillStyle = e.dormant ? "rgba(60,10,20,0.35)" : "rgba(255,58,74,0.22)";
            c.beginPath(); c.ellipse(e.x, e.y + 2, Js.Or(e.r, 12) * 1.45, Js.Or(e.r, 12) * 0.6, 0, 0, TAU); c.fill();
        }
        /* the altar's strikes */
        foreach (var f in ritualS.fx)
        {
            if (f.kind == "mark")
            {
                if (f.t < 0) continue;
                double k = clamp(f.t / f.T, 0, 1);
                c.fillStyle = rgba(255, 70, 50, 0.10 + 0.22 * k);
                c.beginPath(); c.arc(f.x, f.y, f.r, 0, TAU); c.fill();
                c.fillStyle = rgba(255, 120, 60, 0.25 + 0.3 * k);
                c.beginPath(); c.arc(f.x, f.y, f.r * k, 0, TAU); c.fill();
                c.strokeStyle = rgba(255, 90, 60, 0.6 + 0.4 * k); c.lineWidth = 2.5;
                c.beginPath(); c.arc(f.x, f.y, f.r, 0, TAU); c.stroke();
            }
            else if (f.kind == "ring")
            {
                if (f.t < f.wait)
                {
                    double k = f.t / f.wait;
                    c.fillStyle = rgba(160, 60, 255, 0.2 + 0.3 * k);
                    c.beginPath(); c.arc(f.x, f.y, 30 + 20 * k, 0, TAU); c.fill();
                    continue;
                }
                c.strokeStyle = "rgba(170,80,255,0.85)"; c.lineWidth = RITUAL_RING_BAND;
                c.beginPath(); c.arc(f.x, f.y, f.r, 0, TAU); c.stroke();
                c.strokeStyle = "rgba(240,210,255,0.7)"; c.lineWidth = 3;
                c.beginPath(); c.arc(f.x, f.y, f.r, 0, TAU); c.stroke();
            }
        }
    }
    /* THE FOG, over everything outside the circle while a ritual runs */
    public static void drawRitualFog()
    {
        var A = ritualS.active;
        if (A == null) return;
        var c = ctx;
        c.save();
        c.beginPath();
        c.rect(VIEW.x0 - 50, VIEW.y0 - 50, VIEW.x1 - VIEW.x0 + 100, VIEW.y1 - VIEW.y0 + 100);
        c.arc(A.x, A.y, RITUAL_R, 0, TAU, true);
        c.fillStyle = "rgba(26,6,12,0.52)";
        c.fill("evenodd");
        /* rolling banks of it just outside the ring */
        for (int i = 0; i < 22; i++)
        {
            double a = i / 22.0 * TAU + vtime * 0.12 + Math.Sin(vtime * 0.5 + i) * 0.05;
            double rr = RITUAL_R + 40 + 22 * Math.Sin(vtime * 0.8 + i * 1.7);
            double x = A.x + Math.Cos(a) * rr, y = A.y + Math.Sin(a) * rr;
            var g = c.createRadialGradient(x, y, 4, x, y, 70);
            g.addColorStop(0, "rgba(90,30,40,0.30)"); g.addColorStop(1, "rgba(90,30,40,0)");
            c.fillStyle = g; c.beginPath(); c.arc(x, y, 70, 0, TAU); c.fill();
        }
        /* the ring */
        double pulse = 0.5 + 0.5 * Math.Sin(vtime * 4);
        c.strokeStyle = rgba(255, 58, 74, 0.7 + 0.3 * pulse); c.lineWidth = 5;
        c.beginPath(); c.arc(A.x, A.y, RITUAL_R, 0, TAU); c.stroke();
        c.setLineDash(new double[] { 6, 22 }); c.lineDashOffset = vtime * 30; c.strokeStyle = "rgba(255,200,200,0.6)"; c.lineWidth = 3;
        c.beginPath(); c.arc(A.x, A.y, RITUAL_R - 9, 0, TAU); c.stroke();
        c.restore();
    }
    public static void drawRitualHud()
    {
        var S = ritualS;
        if (S.altars.Count == 0) return;
        var A = S.active; int live = ritualLive().Count, done = S.altars.Count - live;
        if (A == null && done == 0) return;
        double by = hudWaveBottom() + 8 + (breachOpenNow() ? 30 : 0) + (abyssOpenNow() ? 30 : 0) + (sboxActiveNow() ? 20 : 0);
        string head;
        if (A != null)
        {
            int alive = enemies.Count(e => e.ritual == A && !e.dead);
            head = "RITUAL · " + (A.left > 0 ? Js.Num(A.left) + " to come · " : "") + alive + " standing · " + Js.Num(S.tribute) + " tribute";
        }
        else if (!ritualCanShop()) return;
        else head = "RITUAL · " + done + "/" + S.altars.Count + " done · " + Js.Num(S.tribute) + " tribute · U to spend";
        ctx.font = "700 12px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
        ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
        ctx.strokeText(head, cw / 2, by); ctx.fillStyle = RITUAL_COL; ctx.fillText(head, cw / 2, by);
    }
}
