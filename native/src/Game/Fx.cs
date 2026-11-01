// lego-survival.html, JS 26618-26802: one frame of everything that is not a body
// (updateFx: debris, sparks, damage numbers, the ground hazards, the spikes, the
// scheduled calls), keeping monsters out of each other, THE VIEW, and the helpers
// that draw on the floor plane.
namespace Brickblade;

public sealed class ViewRect { public double x0, y0, x1, y1; }

public static partial class G
{
    public static void updateFx(double dt)
    {
        foreach (var d in debris.Live())
        {
            d.life -= dt;
            d.vz -= 850 * dt; d.z += d.vz * dt;
            if (d.z < 0) { d.z = 0; d.vz *= -0.45; d.vx *= 0.65; d.vy *= 0.65; if (Math.abs(d.vz) < 50) d.vz = 0; }
            d.x += d.vx * dt; d.y += d.vy * dt;
            d.vx *= Math.max(0, 1 - 2.4 * dt); d.vy *= Math.max(0, 1 - 2.4 * dt);
            d.rot += d.vr * dt;
            if (d.z == 0) d.vr *= Math.max(0, 1 - 4 * dt);
        }
        debris = debris.Where(d => d.life > 0).ToList();
        foreach (var p in particles.Live())
        {
            p.life -= dt;
            if (p.kind == "ring") { p.r += p.v * dt; }
            else { p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.92; p.vy *= 0.92; }
        }
        particles = particles.Where(p => p.life > 0).ToList();
        /* A hard ceiling on sparks. Three hundred bodies burning, freezing and coming
           apart at once could put several thousand of them on the screen, and past a
           point they are not visible as anything but cost — the oldest go first. */
        if (particles.Count > PARTICLE_MAX) particles.RemoveRange(0, particles.Count - PARTICLE_MAX);
        if (debris.Count > DEBRIS_MAX) debris.RemoveRange(0, debris.Count - DEBRIS_MAX);
        foreach (var t in dmgTexts.Live()) { t.life -= dt; t.y += t.vy * dt; t.vy *= 0.94; }
        dmgTexts = dmgTexts.Where(t => t.life > 0).ToList();
        foreach (var h in hazards.Live())
        {
            h.life -= dt;
            if (h.kind == "coil")
            {
                h.born = h.born + dt;
                /* it wanders — slowly, and never far */
                h.drift += dt * 0.6;
                h.x = clamp(h.x + Math.cos(h.drift) * 16 * dt, W.wall + 40, W.w - W.wall - 40);
                h.y = clamp(h.y + Math.sin(h.drift * 0.8) * 16 * dt, W.wall + 40, W.h - W.wall - 40);
                h.spin += dt * 2.4;
                if (h.arm > 0)
                {
                    /* still winding up: it shows you exactly where it will be, and does
                       nothing at all to anything standing in it */
                    h.arm -= dt;
                    if (h.arm <= 0) { addRing(h.x, h.y, 8, 280, h.r, "#ffe95a", 5, 0.4); SFX.zap(); }
                    if (Js.R() < 0.18)
                        particles.Add(new Particle { kind = "spark", x = h.x + rnd(-14, 14), y = h.y - rnd(4, 26),
                            vx = rnd(-8, 8), vy = rnd(-26, -6), life = rnd(0.2, 0.4), max = 0.4, color = "#8d7f3a" });
                    continue;
                }
                h.tick -= dt;
                if (Js.R() < 0.5)
                    particles.Add(new Particle { kind = "spark", x = h.x + rnd(-h.r * 0.8, h.r * 0.8), y = h.y + rnd(-h.r * 0.5, h.r * 0.5),
                        vx = rnd(-20, 20), vy = rnd(-50, -10), life = rnd(0.2, 0.4), max = 0.4, color = "#ffe95a" });
                if (h.tick <= 0)
                {
                    h.tick = COIL_TICK;
                    if (!player.dead && player.dash.t <= 0 && dist(h, player) < h.r)
                    {
                        addZap(h.x, h.y - 26, player.x, player.y - 16, 0, true);
                        playerDamage(h.dmg, h, "shock");
                    }
                    minionsInHazard(h, h.kind, h.r, h.dmg, "shock", COIL_TICK);
                }
                continue;
            }
            if (h.kind == "fire" && Js.R() < 0.25)
                particles.Add(new Particle { kind = "spark", x = h.x + rnd(-h.r * 0.7, h.r * 0.7), y = h.y + rnd(-h.r * 0.4, h.r * 0.4),
                    vx = rnd(-15, 15), vy = rnd(-90, -40), life = rnd(0.3, 0.6), max = 0.6,
                    color = Js.R() < 0.5 ? "#ff9a3c" : "#ffd166" });
            if (h.kind == "frost")
            {
                if (Js.R() < 0.3)
                    particles.Add(new Particle { kind = "spark", x = h.x + rnd(-h.r * 0.8, h.r * 0.8), y = h.y + rnd(-h.r * 0.5, h.r * 0.5),
                        vx = rnd(-12, 12), vy = rnd(-40, -8), life = rnd(0.4, 0.8), max = 0.8, color = FX.foe.frost });
                if (!player.dead && dist(h, player) < h.r * 0.9) applyAilment("frost", null);
                foreach (var en in enemies.Live())
                    if (!en.dead && en.state == "active" && string.IsNullOrEmpty(en.golem) && string.IsNullOrEmpty(en.mini) && dist(h, en) < h.r * 0.9)
                        en.slowT = Math.max(en.slowT, 0.8);   /* the frost field catches its own side too */
                continue;
            }
            if (h.kind == "venom")
            {
                if (Js.R() < 0.45)
                    particles.Add(new Particle { kind = "spark", x = h.x + rnd(-h.r * 0.8, h.r * 0.8), y = h.y + rnd(-h.r * 0.5, h.r * 0.5),
                        vx = rnd(-10, 10), vy = rnd(-34, -6), life = rnd(0.4, 0.9), max = 0.9,
                        color = Js.R() < 0.5 ? "#8ac24a" : "#5f9a2e" });
                h.tick -= dt;
                if (h.tick <= 0)
                {
                    h.tick = VENOM_TICK;
                    /* it does not HIT you, it POISONS you — so the rot keeps working after
                       you have stepped off it, and armour never touches it */
                    if (!player.dead && player.dash.t <= 0 && dist(h, player) < h.r * 0.9)
                        poisonPlayer(Math.max(0.12, Js.Or(h.dmg, 1) * VENOM_SHARE * 0.5), PSN_DUR);
                    /* bones do not carry poison: the pool simply eats at them */
                    minionsInHazard(h, h.kind, h.r * 0.9, Math.max(0.3, Js.Or(h.dmg, 1) * VENOM_SHARE), "chaos", 0.5);
                }
                continue;
            }
            if (!player.dead && player.dash.t <= 0 && dist(h, player) < h.r * 0.88)
                playerDamage(Js.Or(h.dmg, 1), h, "fire");
            if (h.kind == "fire") minionsInHazard(h, h.kind, h.r * 0.88, Js.Or(h.dmg, 1), "fire", 0.5);
        }
        hazards = hazards.Where(h => h.life > 0).ToList();
        updateSpikes(dt);
        updatePSpikes(dt);
        mergeT -= dt;
        if (mergeT <= 0) { mergeT = 1.2; mergeSweep(); }
        foreach (var s in sched.Live()) s.t -= dt;
        var fire = sched.Where(s => s.t <= 0).ToList();
        sched = sched.Where(s => s.t > 0).ToList();
        foreach (var s in fire) s.fn();
    }
    /* KEEPING BODIES OUT OF EACH OTHER. This compared every monster with every other
       monster: three hundred of them is forty-five THOUSAND pairs a frame, and it
       was the single most expensive thing in a big wave. Two bodies can only overlap
       if they are within a bucket of each other, so each bucket is now checked
       against itself and the four buckets after it — which covers every real pair
       exactly once and skips the ninety-nine percent that were never close. */
    public static void sepPair(Ent a, Ent b)
    {
        if (a.state != "active" || b.state != "active") return;
        if (a.type == "boss" || b.type == "boss") return;
        double dx = a.x - b.x, dy = a.y - b.y, rr = a.r + b.r - 2;
        if (dx > rr || dx < -rr || dy > rr || dy < -rr) return;
        var d2 = dx * dx + dy * dy;
        if (d2 >= rr * rr || d2 < 0.0001) return;
        var d = Math.sqrt(d2);
        double push = (rr - d) / 2, ax = dx / d, ay = dy / d;
        a.x += ax * push; a.y += ay * push; b.x -= ax * push; b.y -= ay * push;
    }
    public static readonly int[][] SEP_NEIGHBOURS = { new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { -1, 1 } };
    public static void separate()
    {
        egridBuild();                       /* bodies have moved since the last build */
        if (EGRID.cells == null)
        {
            for (int i = 0; i < enemies.Count; i++) for (int j = i + 1; j < enemies.Count; j++)
                    sepPair(enemies[i], enemies[j]);
            return;
        }
        int w = EGRID.w, h = EGRID.h;
        for (int iy = 0; iy < h; iy++) for (int ix = 0; ix < w; ix++)
            {
                var A = EGRID.cells[iy * w + ix];
                var an = A.Count;
                if (an == 0) continue;
                for (int i = 0; i < an; i++) for (int j = i + 1; j < an; j++) sepPair(A[i], A[j]);
                for (int k = 0; k < 4; k++)
                {
                    int nx = ix + SEP_NEIGHBOURS[k][0], ny = iy + SEP_NEIGHBOURS[k][1];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    var B = EGRID.cells[ny * w + nx];
                    var bn = B.Count;
                    for (int i = 0; i < an; i++) for (int j = 0; j < bn; j++) sepPair(A[i], B[j]);
                }
            }
    }

    /* Ground rings are drawn on the floor plane, squashed the same way every shadow
       and puddle in the game is, so an expanding wave visibly sweeps behind whatever
       is standing in it instead of hooping around them like a sphere. */
    public const double GROUND_SQUASH = 0.55;
    public const int PARTICLE_MAX = 900;    /* sparks on screen before the oldest are dropped */
    public const int DEBRIS_MAX = 260;      /* the same for flying bricks */
    /* =============================== THE VIEW ===============================
       Nothing outside this rectangle is drawn. It is the visible world plus an
       eighth of the screen on every side — a quarter bigger than the window in
       total — so things never pop in at the edge, and a wave of three hundred
       bodies does not pay to draw the two hundred nobody can see. */
    public static readonly ViewRect VIEW = new();
    public static void viewSet(double vx, double vy, double vw, double vh)
    {
        double mx = vw * 0.125, my = vh * 0.125;
        VIEW.x0 = vx - mx; VIEW.y0 = vy - my; VIEW.x1 = vx + vw + mx; VIEW.y1 = vy + vh + my;
    }
    public static bool onScreen(Obj o, double pad = 0)
    {
        var m = Js.Or(pad, 0);
        return o.x >= VIEW.x0 - m && o.x <= VIEW.x1 + m && o.y >= VIEW.y0 - m && o.y <= VIEW.y1 + m;
    }
    public static void groundRing(Ctx c, double x, double y, double r, double lw, Paint style)
    {
        c.save(); c.translate(x, y); c.scale(1, GROUND_SQUASH);
        c.strokeStyle = style; c.lineWidth = lw / GROUND_SQUASH;
        c.beginPath(); c.arc(0, 0, Math.max(1, r), 0, TAU); c.stroke();
        c.restore();
    }
    /* The flat pair. Anything whose HIT TEST is `dist(a,b) < r` has to be drawn as
       a true circle, or its north and south edges reach further than the picture. */
    public static void flatDisc(Ctx c, double x, double y, double r, Paint style)
    {
        c.fillStyle = style;
        c.beginPath(); c.arc(x, y, Math.max(1, r), 0, TAU); c.fill();
    }
    public static void flatRing(Ctx c, double x, double y, double r, double lw, Paint style)
    {
        c.strokeStyle = style; c.lineWidth = lw;
        c.beginPath(); c.arc(x, y, Math.max(1, r), 0, TAU); c.stroke();
    }
    public static void groundDisc(Ctx c, double x, double y, double r, Paint style)
    {
        c.save(); c.translate(x, y); c.scale(1, GROUND_SQUASH);
        c.fillStyle = style;
        c.beginPath(); c.arc(0, 0, Math.max(1, r), 0, TAU); c.fill();
        c.restore();
    }
}
