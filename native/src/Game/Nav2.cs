// lego-survival.html, JS 23696-23923: steering a monster along the flow field or
// its own A* path, getting out of a pinch, collision with the world, who a monster
// is after (you or a skeleton) and the stand-in that lets it fight a skeleton,
// keeping bodies apart, and a skeleton taking a hit.
namespace Brickblade;

public sealed partial class Ent
{
    public Pt flowAt; public double flowT; public Ent tgtM; public bool taunted; public Dash proxyDash;
    public JsMap<string, double> hazCd;
    /* the stand-in's copy of the hero: every field as the hero has it this moment */
    public Ent ProxyOf() => (Ent)MemberwiseClone();
}

public static partial class G
{
    /* Steer `e` toward the player, pathing around geometry only when it has to.
       Returns a unit direction, or null to fall back to the caller's own steering. */
    public static Pt navSteer(Ent e, double dt)
    {
        if (NAV.blocked == null) return null;
        /* after the hero: walk the shared field downhill */
        if (player == heroObj && FLOW.dist != null && FLOW.gen == NAV.gen)
        {
            e.path = null;
            if (navLineOpen(e.x, e.y, player.x, player.y) && !navBlockedAt(e.x, e.y) &&
               !segHitsWater(e.x, e.y, player.x, player.y, Js.Or(e.r, 12) * 0.8)) return null;
            e.flowT = e.flowT - dt;
            if (e.flowAt == null || e.flowT <= 0 || Math.hypot(e.flowAt.x - e.x, e.flowAt.y - e.y) < NAV.cs * 0.5)
            {
                e.flowAt = flowAim(e); e.flowT = 0.15;
            }
            if (e.flowAt == null) return null;
            var a0 = Math.atan2(e.flowAt.y - e.y, e.flowAt.x - e.x);
            return new Pt(Math.cos(a0), Math.sin(a0));
        }
        /* after a skeleton: its own A* path, on the budget */
        e.pathT -= dt;
        var losNow = navLineOpen(e.x, e.y, player.x, player.y);
        if (losNow && !navBlockedAt(e.x, e.y))
        {
            e.path = null;                               /* clear shot — no need to path */
            return null;
        }
        /* repath on a timer, when the path runs out, or when the player has moved on */
        var stale = e.path == null || e.pathT <= 0 ||
          (e.pathGoal != null && Math.hypot(e.pathGoal.x - player.x, e.pathGoal.y - player.y) > 160);
        if (stale && navBudget > 0)
        {
            navBudget--;
            var p2 = navFindPath(e.x, e.y, player.x, player.y);
            if (p2 != null)
            {
                e.path = p2; e.pathI = 0;
                e.pathGoal = new Pt(player.x, player.y);
                e.pathT = rnd(0.55, 0.95);                 /* spread repaths across frames */
            }
            else
            {
                e.path = null; e.pathT = rnd(0.4, 0.7);
            }
        }
        if (e.path == null || e.pathI >= e.path.Count) return null;
        /* string-pulling: skip ahead to the furthest waypoint still in sight */
        var best = e.pathI;
        for (int i = System.Math.Min(e.path.Count - 1, e.pathI + 6); i > e.pathI; i--)
        {
            if (navLineOpen(e.x, e.y, e.path[i].x, e.path[i].y)) { best = i; break; }
        }
        e.pathI = best;
        var wp = e.path[e.pathI];
        if (Math.hypot(wp.x - e.x, wp.y - e.y) < NAV.cs * 0.6)
        {
            e.pathI++;
            if (e.pathI >= e.path.Count) { e.path = null; return null; }
        }
        var t2 = e.path[e.pathI];
        var a = Math.atan2(t2.y - e.y, t2.x - e.x);
        return new Pt(Math.cos(a), Math.sin(a));
    }
    /* The open cell centre nearest a point that has ended up INSIDE the padded band
       around a solid, a lake or the arena wall. A body shoved in there has nothing
       sane to path from, so it used to sit and shuffle on the spot — this hands it a
       definite way back out instead of a coin-flip sidestep. */
    public static Pt navEscape(double x, double y)
    {
        if (NAV.blocked == null || !navBlockedAt(x, y)) return null;
        var c = navCell(x, y);
        var open = navNearestOpen(c.ix, c.iy);
        if (open == null) return null;
        double ex = (open.Value.ix + 0.5) * NAV.cs, ey = (open.Value.iy + 0.5) * NAV.cs;
        if (Math.hypot(ex - x, ey - y) < 1) return null;
        return new Pt(ex, ey);
    }
    /* Walk a move in steps no longer than half a body, resolving collisions at each
       one, so nothing can cross a barrier between two frames however fast it is
       travelling. Only the hero needs it — nothing else in the game dashes. */
    public static void moveCollide(Obj e, double dx, double dy)
    {
        var d = Math.hypot(dx, dy);
        var step = Math.max(4, e.r * 0.5);
        var n = d > step ? Math.min(16, Math.ceil(d / step)) : 1;
        for (int i = 0; i < n; i++) { e.x += dx / n; e.y += dy / n; collideWorld(e); }
    }
    public static void collideWorld(Obj e)
    {
        e.x = clamp(e.x, W.wall + e.r + 6, W.w - W.wall - e.r - 6);
        e.y = clamp(e.y, W.wall + e.r + 6, W.h - W.wall - e.r - 6);
        foreach (var s in solids)
        {
            double d = dist(e, s), rr = e.r + s.r;
            if (d < rr && d > 0.001) { e.x += (e.x - s.x) / d * (rr - d); e.y += (e.y - s.y) / d * (rr - d); }
        }
        foreach (var w in waters)
        {
            double cx = clamp(e.x, w.x, w.x + w.w), cy = clamp(e.y, w.y, w.y + w.h);
            double dx = e.x - cx, dy = e.y - cy; var d2 = dx * dx + dy * dy;
            if (d2 < e.r * e.r)
            {
                if (d2 < 0.0001)
                {
                    /* dead inside it — leave by the NEAREST wall, not always the top */
                    double l = e.x - w.x, r2 = (w.x + w.w) - e.x, t = e.y - w.y, b2 = (w.y + w.h) - e.y;
                    var m = Math.min(Math.min(l, r2), Math.min(t, b2));
                    if (m == l) e.x = w.x - e.r;
                    else if (m == r2) e.x = w.x + w.w + e.r;
                    else if (m == t) e.y = w.y - e.r;
                    else e.y = w.y + w.h + e.r;
                }
                else { var d = Math.sqrt(d2); e.x = cx + dx / d * e.r; e.y = cy + dy / d * e.r; }
            }
        }
    }
    public static Pt enemyCasterMove(Ent e, double dt, double toP, double dP, double far, double near)
    {
        e.strafeT -= dt; if (e.strafeT <= 0) { e.strafe *= -1; e.strafeT = rnd(1.5, 3); }
        if (dP > far + 40) { return new Pt(Math.cos(toP) * e.speed, Math.sin(toP) * e.speed); }
        if (dP < near) { return new Pt(-Math.cos(toP) * e.speed * 0.45, -Math.sin(toP) * e.speed * 0.45); }
        return new Pt(Math.cos(toP + Math.PI / 2) * e.speed * e.strafe * 0.7, Math.sin(toP + Math.PI / 2) * e.speed * e.strafe * 0.7);
    }
    /* WHO A MONSTER IS AFTER. A skeleton that is nearer to it than you are is the
       thing it fights -- so a legion really does stand between you and the crowd --
       and you are, when you are the nearer. A little stickiness, so a body between
       the two does not flick its head back and forth every frame. Bosses are after
       you, always; their attacks still land on whatever is standing in them. */
    public const double TARGET_STICK = 30, TARGET_MAX = 700;
    public const double STRIKE_REACH = 12;      /* px past touching that a monster swings from */
    public const double STRIKE_WIND = 0.32;    /* the raised-weapon beat before the swing lands */
    public static void slashFx(double x, double y, double a, double r, Col col)
    {
        particles.Add(new Particle { kind = "slash", x = x, y = y, a = a, r = r, vx = 0, vy = 0, life = 0.16, max = 0.16, color = col });
    }
    public static Ent enemyTarget(Ent e)
    {
        if (minions.Count == 0 || e.type == "boss" || e.docile || heroObj == null) return null;
        /* THE STONE GOLEM TAUNTS: anything near it comes for it, not for you */
        foreach (var g in minions)
        {
            if (g.kind != "golem" || g.gt != "stone" || g.gone || g.rise < 1) continue;
            if (Math.hypot(e.x - g.x, e.y - g.y) < STONE_TAUNT_R) { e.tgtM = g; e.taunted = true; return g; }
        }
        e.taunted = false;
        var dP = heroObj.dead ? 1e9 : Math.hypot(e.x - heroObj.x, e.y - heroObj.y);
        Ent best = null; double bd = TARGET_MAX;
        foreach (var m in minions)
        {
            if (m.rise < 1 || m.gone) continue;
            var d = Math.hypot(e.x - m.x, e.y - m.y) - (e.tgtM == m ? TARGET_STICK : 0);
            if (d < bd) { bd = d; best = m; }
        }
        if (best != null && bd < dP - (e.tgtM != null ? 0 : TARGET_STICK * 0.5)) { e.tgtM = best; return best; }
        e.tgtM = null;
        return null;
    }
    /* THE STAND-IN. A monster's whole mind was written against `player`: where it
       is, how fast it is moving, whether it is rolling. For the length of its own
       update, a monster after a skeleton sees a stand-in that is the skeleton --
       same place, same movement, never rolling, never invulnerable -- and every
       point of damage it deals through playerDamage lands on the skeleton. Reads it
       does not override fall through to the hero; writes stay on the stand-in.
       (The JS builds it with Object.create(heroObj); here it is the hero's fields as
       they stand, copied, with the skeleton's laid over them: the same reads, and
       writes that go nowhere.) */
    public static Ent minionProxy(Ent m)
    {
        var P = heroObj.ProxyOf();
        P.isProxy = true; P.minion = m;
        P.dash = m.proxyDash ??= new Dash { t = 0, cd = 1, cdMax = 1, dir = new Pt(0, 0) };
        P.x = m.x; P.y = m.y; P.r = m.r; P.dead = false; P.invuln = 0; P.stunT = 0;
        P.hp = m.hp; P.maxHp = m.maxHp; P.kx = 0; P.ky = 0; P.slowT = 0;
        P.leadX = m.lx; P.leadY = m.ly; P.leadDt = Js.Or(m.ldt, 0.016);
        P.face = m.face;
        return P;
    }
    public static void updateEnemy(Ent e, double dt)
    {
        var tgt = enemyTarget(e);
        if (tgt == null) { updateEnemyCore(e, dt); }
        else
        {
            var hero = player;
            player = minionProxy(tgt);
            try { updateEnemyCore(e, dt); } finally { player = hero; }
        }
        bodySpace(e);
    }
    /* NOTHING STANDS INSIDE ANYTHING. A monster that has reached you stops at your
       edge instead of walking into your middle; one that has reached a skeleton
       shares the shove with it. */
    public static void bodySpace(Ent e)
    {
        if (e.dead || e.state != "active" || e.docile || e.legionFrozen) return;
        var h = heroObj;
        if (h != null && !h.dead && player == h)
        {
            double dx = e.x - h.x, dy = e.y - h.y, d = Math.hypot(dx, dy), min = Js.Or(e.r, 12) + Js.Or(h.r, 12);
            if (d > 0.01 && d < min) { var k = (min - d) / d; e.x += dx * k; e.y += dy * k; }
        }
        foreach (var m in minions)
        {
            if (m.rise < 1 || m.gone) continue;
            double dx = e.x - m.x, dy = e.y - m.y, d = Math.hypot(dx, dy), min = Js.Or(e.r, 12) + m.r;
            if (d > 0.01 && d < min)
            {
                /* a mage is rooted: a monster steps round it, and a boss walks over it */
                double k = (min - d) / d, share = m.kind == "mage" ? 0 : e.type == "boss" ? 1 : 0.5;
                m.x -= dx * k * share; m.y -= dy * k * share;
                if (e.type != "boss") { e.x += dx * k * (1 - share); e.y += dy * k * (1 - share); }
            }
        }
    }
    /* A SKELETON TAKES A HIT. No armour and no resistances -- it is bones -- so it
       takes what the hit says. */
    public static void minionHurt(Ent m, double n, string elem, Obj src = null)
    {
        if (m == null || m.gone || m.rise < 1 || !(n > 0)) return;
        if (src != null && ritualOutside(src)) return;           /* from beyond a running ritual's wall */
        /* ...except under your WARD: its armour stands in front of every physical
           blow and its resistance in front of fire, cold and lightning, for the
           legion exactly as for you (chaos goes through, as it does on you) */
        if (player != null && player.wardT > 0)
        {
            if (string.IsNullOrEmpty(elem)) n *= 1 - armourDR(wardArmour(), n);
            else if (elem == "fire" || elem == "frost" || elem == "shock") n *= 1 - Math.min(RES_CAP, wardRes());
        }
        m.hp -= n; m.flash = 1;
        addHitNum(m.x, m.y - 34, fmtHp(n), elem == "fire" ? "#ff9a5a" : elem == "frost" ? "#9fdcff"
               : elem == "shock" ? "#fff07a" : "#c9b8ff", 12);
    }
    /* every raised skeleton inside a circle, once each */
    public static void minionsIn(double x, double y, double r, Action<Ent> fn)
    {
        foreach (var m in minions.Live())
        {
            if (m.rise < 1 || m.gone) continue;
            if (Math.hypot(m.x - x, m.y - y) < r + m.r) fn(m);
        }
    }
    /* every raised skeleton on a ring's band, once per ring */
    public static void minionsOnRing(BossRing ring, double cx, double cy, double band, double dmg, string elem)
    {
        if (minions.Count == 0) return;
        ring.mHit ??= new HashSet<Ent>();
        foreach (var m in minions.Live())
        {
            if (m.rise < 1 || m.gone || ring.mHit.Contains(m)) continue;
            if (Math.abs(Math.hypot(m.x - cx, m.y - cy) - ring.r) < band + m.r) { ring.mHit.Add(m); minionHurt(m, dmg, elem); }
        }
    }
    /* a lingering thing -- a pool, a coil -- bites a skeleton standing in it on its own clock */
    public static void minionsInHazard(Obj h, string hkind, double r, double dmg, string elem, double every = 0)
    {
        foreach (var m in minions.Live())
        {
            if (m.rise < 1 || m.gone) continue;
            if (Math.hypot(m.x - h.x, m.y - h.y) >= r + m.r) continue;
            m.hazCd ??= new JsMap<string, double>();
            var k = Js.Or(hkind, "h");
            if (m.hazCd.GetValueOrDefault(k) > vtime) continue;
            m.hazCd[k] = vtime + Js.Or(every, 0.5);
            minionHurt(m, dmg, elem);
        }
    }
}
