namespace Brickblade;

/* =============================== THE ABYSS (JS 9828–10214) ===============================
   A green eye on the floor; walk onto it and the ground cracks on towards
   wherever it is going, only while you follow it. Monsters climb out behind the
   front, pits tear open and hold it, it may fork, and at its end the Stygian. */
public sealed class AbyssPit { public double d, alive, x, y; public bool open, cleared; }
public sealed class JagPt { public double d, x, y; }
public sealed class AbyssAt { public double x, y, nx, ny; }
/* one crack: the main line of an abyss, or a branch off it */
public class Crack : Obj
{
    public List<Pt> pts = new(); public List<double> cum = new(); public List<JagPt> jag = new();
    public double len, f, nextSpawn; public List<AbyssPit> pits = new(); public AbyssPit pit;
    /* a branch's own */
    public double fork; public bool active, done;
}
public sealed partial class Abyss
{
    public string state; public double delay, age, idleT, kills, fadeT;
    public Ent lord; public List<Crack> branches = new();
}
public sealed partial class Ent { public AbyssPit abyssPit; public bool abyssLord; }
public sealed partial class Prop { public bool abyss; }

public static partial class G
{
    public static Abyss abyss = null; public static List<Abyss> abyssExtra = new();
    public const double ABYSS_FROM = 7;
    public static readonly double[] ABYSS_DELAY = { 4, 10 };
    public static readonly double[] ABYSS_DIST = { 320, 620 };
    public const int ABYSS_SEGS = 8;
    public static readonly double[] ABYSS_SEG = { 180, 230 };
    public const double ABYSS_TOUCH = 56;
    public const double ABYSS_SPD = 150;
    public const double ABYSS_FOLLOW = 420;
    public const double ABYSS_ABANDON = 18;
    public const double ABYSS_SPAWN_EVERY = 150;
    public static readonly double[] ABYSS_PACK = { 2, 4 };
    public static readonly double[] ABYSS_PITS = { 0.34, 0.68 }, ABYSS_PIT_PACK = { 6, 9 };
    public const double ABYSS_ALIVE_MAX = 45;
    public const double ABYSS_HP = 0.9, ABYSS_MAGIC = 0.1;
    public const double ABYSS_LORD_HP = 6;
    public const string ABYSS_CHEST_MAX = "rare";
    public const double ABYSS_PIT_CHEST = 0.4;
    public const double ABYSS_LORD_RARE = 0.4;
    /* THE FORK */
    public const double ABYSS_FORK = 0.45, ABYSS_FORK2 = 0.25, ABYSS_FORK2_FROM = 20;
    public static readonly double[] ABYSS_FORK_AT = { 0.2, 0.6 }, ABYSS_FORK_TURN = { 0.7, 1.3 }, ABYSS_FORK_SEGS = { 3, 5 };
    public const string ABYSS_COL = "#6cff9a", ABYSS_DIM = "#0f2a1a";
    public static List<Abyss> allAbysses() { var l = new List<Abyss>(); if (abyss != null) l.Add(abyss); l.AddRange(abyssExtra); return l; }
    public static bool abyssOpenNow() => allAbysses().Any(A => A.state == "open" || A.state == "lord");
    public static Abyss abyssNew() => new Abyss
    {
        state = "wait", delay = rnd(ABYSS_DELAY[0], ABYSS_DELAY[1]), age = 0, f = 0, len = 0,
        nextSpawn = ABYSS_SPAWN_EVERY, pit = null, idleT = 0, kills = 0, lord = null, fadeT = 0
    };
    /* HOW MANY ABYSSES THIS WAVE: `k`, from the league roll */
    public static void abyssRoll(double n, double k = 1)
    {
        abyss = null; abyssExtra = new List<Abyss>();
        if (sandbox || n < ABYSS_FROM || n % 5 == 0 || k <= 0) return;
        abyss = abyssNew();
        for (int i = 1; i < k; i++) { var A = abyssNew(); A.delay += rnd(3, 9) * i; abyssExtra.Add(A); }
    }
    public static bool abyssFloorOk(double x, double y, double pad)
    {
        double lo = W.wall + pad, hiX = W.w - W.wall - pad, hiY = W.h - W.wall - pad;
        if (x < lo || x > hiX || y < lo || y > hiY) return false;
        return breachFloorOk(x, y);
    }
    /* a line of `segs` segments from `start`, heading `h` and wandering */
    public static List<Pt> abyssLine(Pt start, double h, double segs, bool centre)
    {
        var pts = new List<Pt> { start };
        for (int s = 0; s < segs; s++)
        {
            var prev = pts[^1];
            if (centre) h += clamp(angDiff(Math.Atan2(W.h / 2 - prev.y, W.w / 2 - prev.x), h), -0.35, 0.35);
            Pt next = null;
            for (int t = 0; t < 14 && next == null; t++)
            {
                double hh = h + rnd(-0.55, 0.55) + (t != 0 ? (t % 2 != 0 ? 1 : -1) * t * 0.25 : 0);
                double len = rnd(ABYSS_SEG[0], ABYSS_SEG[1]);
                double x = prev.x + Math.Cos(hh) * len, y = prev.y + Math.Sin(hh) * len;
                /* the middle of the segment has to be floor too, not just its end */
                if (abyssFloorOk(x, y, 90) && abyssFloorOk((x + prev.x) / 2, (y + prev.y) / 2, 90)) { next = new Pt(x, y); h = hh; }
            }
            if (next == null) break;
            pts.Add(next);
        }
        return pts;
    }
    public static List<Pt> abyssPath()
    {
        Pt start = null;
        for (int i = 0; i < 80 && start == null; i++)
        {
            double a = rnd(TAU), rr = rnd(ABYSS_DIST[0], ABYSS_DIST[1]);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            if (abyssFloorOk(x, y, 140) && allAbysses().All(o => o.pts.Count == 0 || hypot(o.x - x, o.y - y) > 500)) start = new Pt(x, y);
        }
        start ??= randomSpawnPos();
        return abyssLine(start, Math.Atan2(W.h / 2 - start.y, W.w / 2 - start.x) + rnd(-0.8, 0.8), ABYSS_SEGS, true);
    }
    /* the measured line of a crack: lengths, a jagged edge, and where its pits are */
    public static void crackMeasure(Crack c, double[] pitFracs)
    {
        c.cum = new List<double> { 0 };
        for (int i = 1; i < c.pts.Count; i++) c.cum.Add(c.cum[i - 1] + hypot(c.pts[i].x - c.pts[i - 1].x, c.pts[i].y - c.pts[i - 1].y));
        c.len = c.cum[^1];
        c.jag = new List<JagPt>();
        for (double d = 0; d <= c.len; d += 16) { var q = abyssAt(c, d); c.jag.Add(new JagPt { d = d, x = q.x + q.nx * rnd(-6, 6), y = q.y + q.ny * rnd(-6, 6) }); }
        c.pits = pitFracs.Select(fr => new AbyssPit { d = fr * c.len, open = false, cleared = false, alive = 0, x = 0, y = 0 }).ToList();
        c.f = 0; c.nextSpawn = ABYSS_SPAWN_EVERY; c.pit = null;
    }
    public static void abyssBuild(Abyss A)
    {
        A.pts = abyssPath();
        crackMeasure(A, ABYSS_PITS);
        A.x = A.pts[0].x; A.y = A.pts[0].y;
        /* THE FORKS, decided now and hidden like the rest */
        A.branches = new List<Crack>();
        int forks = (Js.R() < abyssForkChance() ? 1 : 0) + (wave.n >= ABYSS_FORK2_FROM && Js.R() < ABYSS_FORK2 ? 1 : 0);
        for (int i = 0; i < forks; i++)
        {
            /* a fork that runs straight into a wall is tried again somewhere else */
            for (int t = 0; t < 6; t++)
            {
                double fork = rnd(ABYSS_FORK_AT[0], ABYSS_FORK_AT[1]) * A.len; var q = abyssAt(A, fork);
                double along = Math.Atan2(-q.nx, q.ny);                  /* the tangent is (ny, -nx) */
                double side = (t % 2 != 0 ? -1 : 1) * (Js.R() < 0.5 ? 1 : -1);
                var pts = abyssLine(new Pt(q.x, q.y), along + side * rnd(ABYSS_FORK_TURN[0], ABYSS_FORK_TURN[1]),
                                    irnd(ABYSS_FORK_SEGS[0], ABYSS_FORK_SEGS[1]), false);
                if (pts.Count < 3) continue;
                var b = new Crack { pts = pts, fork = fork, active = false, done = false };
                crackMeasure(b, new double[] { 1 });                    /* its pit is at its very end */
                if (b.len < 250) continue;
                A.branches.Add(b);
                break;
            }
        }
    }
    /* a point `d` along a crack, and the unit normal there */
    public static AbyssAt abyssAt(Crack c, double d)
    {
        d = clamp(d, 0, c.len);
        int i = 1; while (i < c.cum.Count - 1 && c.cum[i] < d) i++;
        var a = c.pts[i - 1]; var b = c.pts[i]; double seg = Js.Or(c.cum[i] - c.cum[i - 1], 1), t = (d - c.cum[i - 1]) / seg;
        double dx = (b.x - a.x) / seg, dy = (b.y - a.y) / seg;
        return new AbyssAt { x = a.x + (b.x - a.x) * t, y = a.y + (b.y - a.y) * t, nx = -dy, ny = dx };
    }
    public static List<string> abyssTypes(double n)
    {
        var t = new List<string> { "imp", "zombie", "skeleton" };
        if (n >= 7) t.AddRange(new[] { "stalker", "cryptbat" });
        if (n >= 12) t.AddRange(new[] { "zrunner", "knight" });
        if (n >= 18) t.AddRange(new[] { "zhulk", "revenant" });
        return t;
    }
    public static Ent abyssify(Ent e, Abyss A)
    {
        e.abyss = true; e.abyssA = A;
        e.hp = e.maxHp = Math.Max(1, Math.Ceiling(e.maxHp * ABYSS_HP));
        e.col = new FigCols { head = "#243a2c", torso = ABYSS_DIM, legs = "#0a120d", hands = ABYSS_COL, hair = "#0a120d" };
        e.buildT = Math.Max(e.buildT, 0.55);            /* it climbs out, it is not assembled */
        return e;
    }
    public static List<Ent> abyssSpawn(Abyss A, Crack c, double d, double n, bool magicOne, AbyssPit pit)
    {
        var types = abyssTypes(wave.n); string type = types[(int)(Js.R() * types.Count)];
        var q = abyssAt(c, d); var outL = new List<Ent>();
        for (int k = 0; k < n; k++)
        {
            double side = (k % 2 != 0 ? 1 : -1) * rnd(18, 46), along = rnd(-40, 40);
            /* either side of the crack, spread a little along it */
            var at = floorAt(q.x + q.nx * side + q.ny * along, q.y + q.ny * side - q.nx * along, 14);
            int n0 = enemies.Count;
            spawnEnemy(type, at.x, at.y, false, (magicOne && k == 0) || Js.R() < ABYSS_MAGIC);
            if (enemies.Count > n0)
            {
                var e = abyssify(enemies[^1], A);
                if (pit != null) { e.abyssPit = pit; pit.alive++; }
                outL.Add(e);
            }
        }
        addDust(q.x, q.y, 6, "rgba(40,90,60,");
        return outL;
    }
    public static void abyssOpen(Abyss A)
    {
        A.state = "open"; A.age = 0; A.f = 0; A.idleT = 0;
        showBanner("ABYSS", "follow the crack", ABYSS_COL, 1.8, 50);
        logEvent("   THE ABYSS OPENS — follow it down", ABYSS_COL);
        addRing(A.x, A.y, 12, 420, 140, ABYSS_COL, 7, 0.55);
        addShake(0.3); SFX.roar();
    }
    public static void abyssSeal(Abyss A, string why = null)
    {
        A.state = "sealed"; A.fadeT = 1.5;
        logEvent("   the abyss seals" + (!string.IsNullOrEmpty(why) ? " — " + why : ""), ABYSS_COL);
    }
    /* the champion of the depths */
    public static void abyssLord(Abyss A)
    {
        var q = abyssAt(A, A.len); double n = wave.n;
        string type = n >= 16 ? "revenant" : n >= 8 ? "brute" : "skeleton";
        int n0 = enemies.Count;
        spawnEnemy(type, q.x, q.y, true, false);
        if (enemies.Count == n0) { abyssReward(A); return; }
        var e = abyssify(enemies[^1], A);
        e.abyssLord = true; e.state = "active"; e.buildT = 1;
        e.hp = e.maxHp = Math.Ceiling(e.maxHp * ABYSS_LORD_HP);
        e.scale = Js.Or(e.scale, 1) * 1.2; e.r = Math.Round(Js.Or(e.r, 12) * 1.2);
        A.lord = e; A.state = "lord";
        showBanner("THE STYGIAN", "climbs out of the depths", ABYSS_COL, 1.8, 46);
        logEvent("   the Stygian climbs out of the abyss", ABYSS_COL);
        addRing(q.x, q.y, 16, 520, 200, ABYSS_COL, 8, 0.6); addShake(0.35); SFX.roar();
    }
    public static Prop abyssChest(double x, double y, string rar)
    {
        string r = Array.IndexOf(RAR_ORDER, rar) > Array.IndexOf(RAR_ORDER, ABYSS_CHEST_MAX) ? ABYSS_CHEST_MAX : rar;
        var ch = shoveToFloor(new Prop { type = "chest", x = x, y = y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16, rar = r, abyss = true }, 20);
        props.Add(ch);
        return ch;
    }
    public static void abyssReward(Abyss A)
    {
        var q = abyssAt(A, A.len);
        string rar = Js.R() < ABYSS_LORD_RARE ? "rare" : "uncommon";
        abyssChest(q.x, q.y + 30, rar);
        spawnStuds(q.x, q.y, 20 + wave.n * 3);
        showBanner("ABYSS CLEARED", (RARITY.GetValueOrDefault(rar) ?? RARITY["common"]).name + " CHEST", ABYSS_COL, 1.8, 42);
        logEvent("   the abyss is spent: a " + rar + " chest at its depth", ABYSS_COL);
        A.state = "done"; A.fadeT = 4;
    }
    public static void abyssOnKill(Ent e)
    {
        var A = e.abyssA ?? abyss;
        if (A == null || !e.abyss) return;
        A.kills++;
        var pit = e.abyssPit;
        if (pit != null && !pit.cleared && --pit.alive <= 0)
        {
            pit.cleared = true;
            foreach (var c in new List<Crack> { A }.Concat(A.branches)) if (c.pit == pit) c.pit = null;
            spawnStuds(pit.x, pit.y, 6 + wave.n);
            if (Js.R() < ABYSS_PIT_CHEST) abyssChest(pit.x, pit.y + 24, Js.R() < 0.5 ? "uncommon" : "common");
            addRing(pit.x, pit.y, 8, 300, 90, ABYSS_COL, 5, 0.45);
            logEvent("   an abyssal pit is cleared", ABYSS_COL);
        }
        if (e.abyssLord && A.lord == e) abyssReward(A);
    }
    /* ONE CRACK'S FRONT, for a frame: it runs on while you follow it. Returns whether it moved. */
    public static bool crackTick(Abyss A, Crack c, double dt, double crowd)
    {
        if (c.done) return false;
        var front = abyssAt(c, c.f);
        bool near = !player.dead && hypot(player.x - front.x, player.y - front.y) <= ABYSS_FOLLOW;
        bool held = c.pit != null || crowd >= ABYSS_ALIVE_MAX;
        bool moved = false;
        if (near && !held && c.f < c.len)
        {
            c.f = Math.Min(c.len, c.f + ABYSS_SPD * dt); moved = true;
            if (Js.R() < 0.5) addSparks(front.x, front.y - 4, 1, ABYSS_COL);
        }
        /* what climbs out behind the front */
        while (c.nextSpawn <= c.f && c.nextSpawn < c.len - 40)
        {
            abyssSpawn(A, c, c.nextSpawn, irnd(ABYSS_PACK[0], ABYSS_PACK[1]), false, null);
            c.nextSpawn += ABYSS_SPAWN_EVERY;
        }
        /* a pit tears open where the front reaches it, and holds this crack there */
        foreach (var pit in c.pits)
        {
            if (pit.open || c.f < pit.d - 0.5) continue;
            pit.open = true; c.f = Math.Min(c.f, pit.d);
            var q = abyssAt(c, pit.d); pit.x = q.x; pit.y = q.y;
            c.pit = pit;
            abyssSpawn(A, c, pit.d, irnd(ABYSS_PIT_PACK[0], ABYSS_PIT_PACK[1]), true, pit);
            if (pit.alive <= 0) { pit.cleared = true; c.pit = null; }
            addRing(q.x, q.y, 10, 380, 110, ABYSS_COL, 7, 0.5); addShake(0.2);
            addDmg(q.x, q.y - 40, "ABYSSAL PIT", ABYSS_COL, 16);
            logEvent("   an abyssal pit tears open", ABYSS_COL);
            break;
        }
        /* a branch is finished when it has run its length and its end pit is cleared */
        if (c != A && c.f >= c.len && c.pits.All(p => p.cleared)) c.done = true;
        return moved;
    }
    /* one abyss, for a frame. Returns false when it is finished with. */
    public static bool abyssTick(Abyss A, double dt)
    {
        A.age += dt;
        if (A.state == "wait")
        {
            if (wave.state == "cleared") return false;
            if (wave.state != "fight") return true;
            A.delay -= dt;
            if (A.delay > 0) return true;
            abyssBuild(A);
            if (A.len < 300) return false;                     /* nowhere to run: no abyss this wave */
            A.state = "idle"; A.age = 0;
            logEvent("   AN ABYSS opens its eye", ABYSS_COL);
            addDmg(A.x, A.y - 50, "ABYSS", ABYSS_COL, 20);
            return true;
        }
        if (A.state == "idle")
        {
            if (wave.state == "cleared" && !wave.hold) { logEvent("   the abyss closes, untouched", ABYSS_COL); return false; }
            if (!player.dead && hypot(player.x - A.x, player.y - A.y) < ABYSS_TOUCH + Js.Or(player.r, 12)) abyssOpen(A);
            return true;
        }
        if (A.state == "sealed" || A.state == "done")
        {
            A.fadeT -= dt;
            return A.fadeT > 0;
        }
        double crowd = 0; foreach (var e in enemies) if (e.abyss && !e.dead && (e.abyssA ?? abyss) == A) crowd++;
        /* the forks wake as the main front passes them */
        foreach (var b in A.branches)
            if (!b.active && A.f >= b.fork)
            {
                b.active = true;
                var q = abyssAt(b, 0);
                addRing(q.x, q.y, 8, 320, 90, ABYSS_COL, 5, 0.45);
                addDmg(q.x, q.y - 40, "THE ABYSS FORKS", ABYSS_COL, 15);
                logEvent("   the abyss forks", ABYSS_COL);
            }
        bool moved = false, holding = false;
        if (A.state == "open")
        {
            moved = crackTick(A, A, dt, crowd) || moved;
            holding = holding || A.pit != null;
        }
        foreach (var b in A.branches) if (b.active && !b.done) { moved = crackTick(A, b, dt, crowd) || moved; holding = holding || b.pit != null; }
        if (A.state == "lord")
        {
            if (A.lord != null && A.lord.dead && A.state == "lord") abyssReward(A);   /* in case it left without a kill call */
            return true;
        }
        if (moved || holding) A.idleT = 0;
        else
        {
            A.idleT += dt;
            if (A.idleT >= ABYSS_ABANDON) { abyssSeal(A, "nobody followed it"); return true; }
        }
        if (A.f >= A.len && A.pit == null) abyssLord(A);
        return true;
    }
    public static void updateAbyss(double dt)
    {
        if (player == null) return;
        if (abyss != null && !abyssTick(abyss, dt)) abyss = null;
        abyssExtra = abyssExtra.Filter(A => abyssTick(A, dt));
        /* the first of the others takes the lead once the lead is gone */
        if (abyss == null && abyssExtra.Count > 0) abyss = abyssExtra.Shift();
    }
    /* the crack on the floor -- only what has opened */
    public static void drawAbyssOne(Abyss A)
    {
        var c = ctx;
        if (A.state == "wait" || A.jag.Count == 0) return;
        double pulse = 0.5 + 0.5 * Math.Sin(vtime * 4);
        double fade = (A.state == "sealed" || A.state == "done") ? clamp(A.fadeT / (A.state == "done" ? 4 : 1.5), 0, 1) : 1;
        void line(Crack k, double to)
        {
            c.beginPath(); bool first = true;
            foreach (var q0 in k.jag) { if (q0.d > to) break; if (first) { c.moveTo(q0.x, q0.y); first = false; } else c.lineTo(q0.x, q0.y); }
            var q = abyssAt(k, to); if (first) c.moveTo(q.x, q.y); c.lineTo(q.x, q.y);
        }
        c.save(); c.globalAlpha = fade; c.lineCap = "round"; c.lineJoin = "round";
        var cracks = new List<Crack> { A }; cracks.AddRange(A.branches.Where(b => b.active));
        foreach (var k in cracks)
        {
            double opened = (A.state == "idle") ? 0 : k.f;
            if (opened <= 0) continue;
            c.strokeStyle = "rgba(108,255,154,0.28)"; c.lineWidth = 22; line(k, opened); c.stroke();
            c.strokeStyle = "#06110a"; c.lineWidth = 13; line(k, opened); c.stroke();
            c.strokeStyle = rgba(108, 255, 154, 0.55 + 0.3 * pulse); c.lineWidth = 2.5; line(k, opened); c.stroke();
            /* the tip: where it is tearing, and nothing past it */
            if (opened < k.len && (A.state == "open" || A.state == "lord"))
            {
                var q = abyssAt(k, opened);
                c.fillStyle = rgba(108, 255, 154, 0.35 + 0.35 * pulse);
                c.beginPath(); c.arc(q.x, q.y, 9 + 4 * pulse, 0, TAU); c.fill();
            }
            foreach (var pit in k.pits)
            {
                if (!pit.open) continue;
                c.fillStyle = pit.cleared ? "rgba(6,17,10,0.55)" : "rgba(4,12,7,0.85)";
                c.beginPath(); c.ellipse(pit.x, pit.y, 40, 20, 0, 0, TAU); c.fill();
                c.strokeStyle = pit.cleared ? "rgba(108,255,154,0.25)" : rgba(108, 255, 154, 0.55 + 0.35 * pulse); c.lineWidth = 3;
                c.beginPath(); c.ellipse(pit.x, pit.y, 40 + (pit.cleared ? 0 : 4 * pulse), 20 + (pit.cleared ? 0 : 2 * pulse), 0, 0, TAU); c.stroke();
            }
        }
        /* the eye at its head: what you walk onto to open it */
        if (A.state == "idle" || A.state == "open")
        {
            double s = A.state == "idle" ? 1 : 0.7;
            c.fillStyle = "rgba(4,12,7,0.8)";
            c.beginPath(); c.ellipse(A.x, A.y, 30 * s, 14 * s, 0, 0, TAU); c.fill();
            c.strokeStyle = rgba(108, 255, 154, 0.5 + 0.4 * pulse); c.lineWidth = 2.5;
            c.beginPath(); c.ellipse(A.x, A.y, (30 + 6 * pulse) * s, (14 + 3 * pulse) * s, 0, 0, TAU); c.stroke();
            c.fillStyle = ABYSS_COL;
            c.beginPath(); c.ellipse(A.x, A.y, 8 * s, (3 + 3 * pulse) * s, 0, 0, TAU); c.fill();
        }
        c.restore();
    }
    public static void drawAbyss()
    {
        foreach (var A in allAbysses()) drawAbyssOne(A);
        /* a green stain under everything that climbed out of one */
        var c = ctx;
        foreach (var e in enemies)
        {
            if (!e.abyss || e.dead || !onScreen(e, 40)) continue;
            c.fillStyle = rgba(108, 255, 154, e.abyssLord ? 0.34 : 0.2);
            c.beginPath(); c.ellipse(e.x, e.y + 2, Js.Or(e.r, 12) * 1.5, Js.Or(e.r, 12) * 0.62, 0, 0, TAU); c.fill();
        }
    }
    public static void drawAbyssHud()
    {
        var open = allAbysses().Where(A0 => A0.state == "open" || A0.state == "lord").ToList();
        if (open.Count == 0) return;
        var A = open[0];
        double bw = Math.Min(320, cw - 80), bx = cw / 2 - bw / 2;
        double by = hudWaveBottom() + 8 + (breachOpenNow() ? 30 : 0);
        var pits = new List<Crack> { A }.Concat(A.branches).Select(c0 => c0.pit).Where(p0 => p0 != null).ToList();
        int alive = 0; foreach (var e in enemies) if (e.abyssPit != null && pits.Contains(e.abyssPit) && !e.dead) alive++;
        int forks = A.branches.Count(b => b.active && !b.done);
        string head = "ABYSS" + (open.Count > 1 ? " ×" + open.Count : "") + " · " + (A.state == "lord" ? "THE STYGIAN"
            : pits.Count > 0 ? "ABYSSAL PIT — " + alive + " left"
            : A.idleT > 0 ? "FOLLOW THE CRACK · seals in " + Js.Num(Math.Ceiling(ABYSS_ABANDON - A.idleT)) + "s"
            : Js.Num(Math.Round(100 * A.f / A.len)) + "% down") + (forks != 0 ? " · " + forks + " fork" + (forks > 1 ? "s" : "") : "");
        ctx.font = "700 12px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
        ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
        ctx.strokeText(head, cw / 2, by); ctx.fillStyle = ABYSS_COL; ctx.fillText(head, cw / 2, by);
        ctx.fillStyle = "rgba(15,20,25,0.55)"; roundRect(ctx, bx, by + 10, bw, 6, 3); ctx.fill();
        ctx.fillStyle = ABYSS_COL; roundRect(ctx, bx, by + 10, Math.Max(3, bw * clamp(A.f / Js.Or(A.len, 1), 0, 1)), 6, 3); ctx.fill();
    }
}
