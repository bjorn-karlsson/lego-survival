// lego-survival.html, JS 23087-23268: THE GROUND SLAM (the mace's fault of stone
// teeth), and what a weapon's swing combo looks like.
namespace Brickblade;

/* one step of a swing combo: how long, which arc, whose reach, how hard it shoves */
public sealed class SwingStep { public double dur, kb = 1; public double[] arc; public string reach; public bool spin, crit; }

public static partial class G
{
    /* ============================== THE GROUND SLAM =============================
       A mace hero does not swing. They drive the head into the floor and a FAULT
       opens in front of them: lines of stone teeth tearing up out of the ground in
       a fan, running outward from the impact. It is the boss's own slam turned
       around — same spikes, same wave, pointed the other way.
       Every tooth is physical damage, so it bleeds and it reads the same increases
       a swing does. The whole fan is ONE attack: it drinks once, and no body may be
       bitten more than SLAM_MAX_HITS times by it, so standing in the middle of it
       is a reward and not an execution. */
    public static double slamArcCap() =>
        (player != null && player.slam != null && player.slam.elem >= SLAM_ELEM_MAX) ? SLAM_ARC_FULL : SLAM_ARC_MAX;
    public static double slamBase() => weapon.dmg + player.flatPhys + (player.slam != null ? player.slam.flat : 0);
    public static double slamDamage(IEnumerable<double> more = null) => calcDamage(slamBase(), "melee", more);
    /* one tooth, against one whole swing */
    public static double slamSpikeDamage() => slamDamage(new[] { SLAM_SHARE });
    /* how many lines of stone a fan of this width throws — the density never thins,
       so widening it is always more teeth and never the same teeth spread out */
    public static double slamRays(double arc) => clamp(Math.round(arc / SLAM_RAY_GAP) + 1, 3, SLAM_RAYS_MAX);
    /* which element this slam is carrying, or null. The cycle only turns for a hero
       who owns the legendary. */
    public static string slamElement()
    {
        var p = player;
        if (p.slam == null || p.slam.elem <= 0) return null;
        return SLAM_ELEMS[Js.I32(p.slam.cycle) % SLAM_ELEMS.Length];
    }
    public static void startSlam(double aim)
    {
        var p = player;
        p.combo = 0; p.comboTimer = 0;
        p.atkCd = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
        retPulse = 1;
        p.slamAct = new SlamAct { t = 0, aim = aim, fired = false, el = slamElement() };
        p.slamAim = aim;
        SFX.swing();
    }
    /* the floor breaks. Everything the fan is worth is decided here, once. */
    public static void fireSlam(double aim, string el)
    {
        var p = player;
        double arc = p.slamArc, range = p.slamRange, speed = p.slamSpeed;
        double rays = slamRays(arc), rows = Math.max(2, Math.round(range / SLAM_STEP));
        var dmg = slamSpikeDamage();
        /* ONE shared record for the whole fan: what it has bitten, what it has dealt,
           and how many teeth are still standing. The last tooth to leave pays out the
           lifesteal, so a slam drinks once however many teeth landed. */
        var sl = new SlamRec { dealt = 0, left = 0, el = el };
        var full = arc >= TAU - 1e-6;
        for (int row = 0; row < rows; row++)
        {
            var rr = (row + 1) / rows * range;
            var wait = rr / Math.max(1, speed);
            for (int i = 0; i < rays; i++)
            {
                /* a full circle has no edges, so it is spaced round rather than across */
                var off = full ? (i / rays) * TAU
                               : (rays == 1 ? 0 : (i / (rays - 1) - 0.5) * arc) + rnd(-0.05, 0.05);
                var a = aim + off + (full ? rnd(-0.05, 0.05) : 0);
                double x = p.x + Math.cos(a) * rr, y = p.y + Math.sin(a) * rr;
                if (navBlockedAt(x, y)) continue;
                pspikes.Add(new Spike { x = x, y = y, a = a, dmg = dmg, wait = wait, t = 0, hit = new HashSet<object>(), sl = sl, el = el,
                    life = SPIKE_RISE + SPIKE_HOLD + SPIKE_SINK,
                    h = rnd(26, 40), w = rnd(7, 11.5), seed = rnd(TAU) });
                sl.left++;
            }
        }
        addRing(p.x, p.y, 12, Math.min(SLAM_SPEED_MAX, speed), range,
                el != null ? SLAM_COL[el] : "#cfd6dd", 8, 0.34);
        addShake(0.34);
        addDust(p.x, p.y, 7, "rgba(150,150,145,");
        spawnBricks(p.x, p.y + 6, el != null ? new[] { SLAM_COL[el], "#8b8f8a", "#2c3136" } : new[] { "#3a4045", "#2c3136", "#8b8f8a" }, 9, 170);
        SFX.slam();
        /* THE POSE OUTLIVES THE INPUT. The mace stays buried while the fault runs out
           — snapping back upright with teeth still coming up read as two unrelated
           things happening. It is a separate timer from `slamAct` on purpose: that one
           gates the attack, and gating the attack for this long would throttle the
           mace below its own swing rate. */
        p.slamPose = Math.min(0.55, range / Math.max(1, speed)) + 0.14;
        if (el != null) p.slam.cycle = (Js.I32(p.slam.cycle) + 1) % SLAM_ELEMS.Length;
    }
    public static void updateSlam(double dt)
    {
        var p = player; var a = p.slamAct;
        if (p.slamPose > 0) p.slamPose -= dt;
        if (a == null) return;
        a.t += dt;
        if (!a.fired && a.t >= SLAM_WIND) { a.fired = true; fireSlam(a.aim, a.el); }
        if (a.t >= SLAM_WIND + 0.22) p.slamAct = null;
    }
    /* the teeth. They bite on the way up, once per body each, and the fan as a whole
       may only bite one body SLAM_MAX_HITS times. */
    public static void updatePSpikes(double dt)
    {
        foreach (var sp in pspikes.Live())
        {
            if (sp.wait > 0) { sp.wait -= dt; continue; }
            sp.t += dt;
            var sl = sp.sl;
            if (sp.t >= SPIKE_RISE * 0.35 && sp.t < SPIKE_RISE + SPIKE_HOLD)
            {
                foreach (var en in enemiesNear(sp.x, sp.y, 44).Live())
                {
                    if (en.dead || en.state != "active" || sp.hit.Contains(en)) continue;
                    if (dist(sp, en) > 20 + hitR(en)) continue;
                    var n = sl.hits.TryGetValue(en, out var nn) ? nn : 0;
                    if (n >= SLAM_MAX_HITS) { sp.hit.Add(en); continue; }
                    sp.hit.Add(en); sl.hits[en] = n + 1;
                    var before = en.hp;
                    var a2 = Math.atan2(en.y - player.y, en.x - player.x);
                    if (sp.el != null)
                    {
                        /* SLAM OF THE ELEMENTS: half the tooth is converted, and the element
                           it converts into brings its own ailment with it */
                        hitEnemy(en, sp.dmg * (1 - SLAM_ELEM_SHARE), a2, 140 * weapon.kb, "slam", new HitOpts { spin = true });
                        var ed = sp.dmg * SLAM_ELEM_SHARE;
                        /* the element it converts into brings its own ailment, and hitEnemy
                           lays it: a slam source is native to its element, so it is a
                           certainty and not a chance — that is what the legendary bought */
                        hitEnemy(en, ed, a2, 0, "slam" + sp.el, new HitOpts { spin = true, quiet = false });
                    }
                    else
                    {
                        hitEnemy(en, sp.dmg, a2, 160 * weapon.kb, "slam", new HitOpts { spin = true });
                    }
                    sl.dealt += Math.max(0, before - en.hp);
                }
                /* IT TEARS UP THE SCENERY TOO — chests, crates, trees, the lot. One hit
                   per prop per SLAM, the same as one swing, so a wide fault opens every
                   crate it crosses rather than shredding the first one thirty times.
                   It does NOT deflect: a slam is the floor coming up, and nothing about
                   that turns an arrow aside. */
                foreach (var pr in props.Live())
                {
                    if (pr.dead || pr.indestructible || sl.props.Contains(pr)) continue;
                    if (dist(sp, pr) > 22 + pr.r) continue;
                    sl.props.Add(pr);
                    pr.hp--; pr.flash = 1;
                    addSparks(pr.x, pr.y - 10, 3, "#e8eef2");
                    /* one crack per slam, not one per crate — a wide fault crosses a lot of
                       scenery and eight overlapping hits is a noise, not a sound */
                    if (!sl.propSfx) { sl.propSfx = true; SFX.hit(); }
                    if (pr.hp <= 0) breakProp(pr);
                }
            }
            if (sp.t >= sp.life) sp.dead = true;
        }
        if (pspikes.Any(sp => sp.dead))
        {
            foreach (var sp in pspikes)
            {
                if (!sp.dead) continue;
                var sl = sp.sl;
                sl.left--;
                /* the last tooth of the fan settles: the whole slam drinks, once */
                if (sl.left <= 0 && sl.dealt > 0) { leechFrom(sl.dealt); sl.dealt = 0; }
            }
            pspikes = pspikes.Where(sp => !sp.dead).ToList();
        }
    }
    /* ---- WHAT A WEAPON'S COMBO LOOKS LIKE -------------------------------------
       The sword's is three: a sweep out, a sweep back, and a full spin. The AXE
       swings twice — a fast forward chop, then a slow 270-degree BACKSWING on the
       way out of it, which costs more time, reaches further and is ALWAYS a
       critical on the first body it touches. Two hits read as tap-TAP instead of
       tap-tap-TAP, and the guaranteed critical is what Reaving is waiting for: the
       rhythm of the weapon and its legendary are the same thing.
       A row here is a whole combo, so a fifth weapon swings differently by being
       written down rather than by adding a branch to startSwing. */
    public static readonly JsMap<string, SwingStep> SWING_STEPS = new()
    {
        ["lo"] = new SwingStep { dur = 0.20, arc = new[] { -1.50, 1.15 }, reach = "reach", kb = 1.0 },
        ["hi"] = new SwingStep { dur = 0.20, arc = new[] { 1.50, -1.15 }, reach = "reach", kb = 1.0 },
        ["spin"] = new SwingStep { dur = 0.34, arc = new[] { 0, TAU * 0.95 }, reach = "spinReach", kb = 1.43, spin = true },
        /* out of the chop and all the way round behind you */
        ["back"] = new SwingStep { dur = 0.33, arc = new[] { 2.45, -2.25 }, reach = "spinReach", kb = 2.0, spin = true, crit = true },
    };
    public static readonly JsMap<string, string[]> SWING_COMBO = new() { ["_"] = new[] { "lo", "hi", "spin" }, ["axe"] = new[] { "lo", "back" } };
    public static string[] comboSteps() => (weapon != null ? SWING_COMBO.GetValueOrDefault(weapon.id ?? "") : null) ?? SWING_COMBO["_"];
    public static void startSwing(double aim)
    {
        var p = player;
        var steps = comboSteps();
        p.combo = p.comboTimer > 0 ? (p.combo + 1) % steps.Length : 0;
        p.comboTimer = 0.85;
        /* MOMENTUM SHORTENS THE SWING. It is read here rather than poured into
           p.atkRate because a stack that changed a pool would have to be taken back
           out again, and that is where double counting starts. */
        p.atkCd = p.atkRate / Math.max(0.3, chillMul()) / (1 + stackMod("atk")) * curseSwingMul();
        retPulse = 1;
        var step = SWING_STEPS.GetValueOrDefault(steps[Js.I32(p.combo)]) ?? SWING_STEPS["lo"];
        var wr = step.reach == "spinReach" ? weapon.spinReach : weapon.reach;
        var b = Js.Or(wr, weapon.reach) * (1 + stackMod("reach"));
        var s = new Swing { t = 0, dur = step.dur, spin = step.spin, kb = Js.Or(step.kb, 1),
            crit = step.crit, aim = aim,
            from = aim + step.arc[0], to = aim + step.arc[1],
            reach = b + p.reach };
        s.cur = s.from;
        p.swing = s;
        SFX.swing();
    }
}
