// lego-survival.html, JS 21305-21483: POINT AND CLICK — what the hero means to do
// each frame, from WASD or the mouse, and the marks a click leaves.
namespace Brickblade;

public sealed class ClickMark { public double x, y, t; }
public sealed class Ctl
{
    public Pt dest; public Obj target; public string mode, btn; public bool fired, free;
    public List<Pt> path; public int pathI; public double pathT; public Pt goal; public ClickMark mark;
}
/* ONE FRAME OF INTENT (see heroControl) */
public sealed class Intent { public double ax, ay, aim, face; public bool attack, held, press, engaged, stand; }

public static partial class G
{
    /* =============================== UPDATE =============================== */
    /* ============================ POINT AND CLICK ============================
       PoE's movement, as an option (OPT.moveMode) -- and the movement the RPG will
       use. Everything the hero does each frame is asked of heroControl(): which way
       to walk, where to aim, whether to attack. WASD answers the way it always did;
       POINT & CLICK answers from the mouse:
         - the left button on the GROUND walks there, routed round what is in the
           way; held, the hero follows the cursor;
         - the attack button on a MONSTER (or anything breakable) walks into range
           and attacks it once; held, it keeps attacking that one;
         - SHIFT with the attack button attacks at the cursor WITHOUT stopping the
           walk (a hero can cross the room shooting), and holding SHIFT while the
           left button walks attacks as it goes;
         - with the attack on the RIGHT button, the left only ever walks and the
           right attacks at the cursor while the walk goes on -- hold both to walk
           and fight at once.
       A press decides what a hold means, as in PoE: pressed on the ground, a held
       button walks even when the cursor passes over a monster; pressed on a monster,
       it keeps hitting that monster wherever the cursor goes. */
    public const double CLICK_STOP = 10;          /* close enough to a clicked spot */
    public const double CLICK_PICK = 20;          /* slack round a body that still counts as clicking it */
    public const double CLICK_RANGE_ARROW = 520;  /* how close a bow walks before it shoots (arrows fly ~740) */
    public const double CLICK_RANGE_BOLT = 470;   /* ...and a staff (bolts fly ~700) */
    public static readonly Ctl CTL = new();
    public static bool clickMode() => OPT.moveMode == "click";
    public static bool attackOnRight() => OPT.attackBtn == "right";
    public static bool shiftHeld() => key("ShiftLeft") || key("ShiftRight");
    public static void ctlReset() { CTL.dest = null; CTL.target = null; CTL.mode = null; CTL.btn = null; CTL.fired = false; CTL.free = false; CTL.path = null; CTL.mark = null; }
    /* how close the weapon has to be to its target: a swing's reach, a slam's
       length, a comfortable bowshot. The scepter attacks nothing itself: 0. */
    public static double heroAttackRange()
    {
        var p = player;
        if (p == null || weapon == null) return 0;
        if (isSummoner()) return 0;
        if (isArcher()) return CLICK_RANGE_ARROW;
        if (isCaster()) return CLICK_RANGE_BOLT;
        if (isSlammer()) return Math.max(90, Js.Or(p.slamRange, 220) * 0.7);
        return Math.max(30, (weapon.reach * (1 + stackMod("reach")) + p.reach) * 0.85);
    }
    /* what is under the cursor: a monster first, then anything breakable. A body is
       drawn standing UP from its feet, so it is picked round its middle, not its feet */
    public static Obj clickTargetAt(double wx, double wy)
    {
        Obj best = null; double bd = double.PositiveInfinity;
        foreach (var e in enemies.Live())
        {
            if (e.dead || e.state != "active" || e.docile) continue;
            var s = Js.Or(e.scale, 1); var cy = e.y - 18 * s;
            var d = Math.min(Math.hypot(e.x - wx, cy - wy), Math.hypot(e.x - wx, e.y - wy)) - hitR(e) - CLICK_PICK;
            if (d < 0 && d < bd) { bd = d; best = e; }
        }
        if (best != null) return best;
        foreach (var pr in props.Live())
        {
            if (pr.dead || pr.indestructible || !(pr.hp > 0)) continue;
            var d = Math.hypot(pr.x - wx, pr.y - 10 - wy) - Js.Or(pr.r, 14) - CLICK_PICK * 0.5;
            if (d < 0 && d < bd) { bd = d; best = pr; }
        }
        return best;
    }
    public static bool clickTargetLive(Obj t) =>
        t != null && !t.dead && (t is Prop pr ? props.Contains(pr) && pr.hp > 0 : t is Ent e && e.state == "active");
    public static double clickTargetR(Obj t) => t is Prop pr ? Js.Or(pr.r, 14) : hitR(t as Ent);
    /* which way to walk towards a point: straight while the line is open, else the
       same A* the monsters use, string-pulled, repathed when the goal moves */
    public static Pt heroSteer(Ent p, double tx, double ty, double dt)
    {
        var d = Math.hypot(tx - p.x, ty - p.y);
        if (d < 1) return null;
        if (NAV.blocked == null || (navLineOpen(p.x, p.y, tx, ty) && !segHitsWater(p.x, p.y, tx, ty, Js.Or(p.r, 12) * 0.8)))
        {
            CTL.path = null; return new Pt((tx - p.x) / d, (ty - p.y) / d);
        }
        CTL.pathT -= dt;
        if (CTL.path == null || CTL.pathT <= 0 || CTL.goal == null || Math.hypot(CTL.goal.x - tx, CTL.goal.y - ty) > 60)
        {
            CTL.path = navFindPath(p.x, p.y, tx, ty); CTL.pathI = 0; CTL.goal = new Pt(tx, ty); CTL.pathT = 0.5;
        }
        if (CTL.path == null || CTL.path.Count == 0) return new Pt((tx - p.x) / d, (ty - p.y) / d);
        int best = CTL.pathI;
        for (int i = System.Math.Min(CTL.path.Count - 1, CTL.pathI + 6); i > CTL.pathI; i--)
            if (navLineOpen(p.x, p.y, CTL.path[i].x, CTL.path[i].y)) { best = i; break; }
        CTL.pathI = best;
        var wp = CTL.path[CTL.pathI];
        if (Math.hypot(wp.x - p.x, wp.y - p.y) < NAV.cs * 0.5 && CTL.pathI < CTL.path.Count - 1) { CTL.pathI++; wp = CTL.path[CTL.pathI]; }
        var dw = Js.Or(Math.hypot(wp.x - p.x, wp.y - p.y), 1);
        return new Pt((wp.x - p.x) / dw, (wp.y - p.y) / dw);
    }
    /* ONE FRAME OF INTENT: { ax, ay } to walk, aim, attack (fire the weapon if it
       can), held (the attack button is being held -- a whirl spins), press (the
       attack was pressed this frame -- a Convocation) and face (where the body looks) */
    public static Intent heroControl(Ent p, double dt)
    {
        var mouseA = Math.atan2(mouse.wy - p.y, mouse.wx - p.x);
        var outp = new Intent { ax = 0, ay = 0, aim = mouseA, face = mouseA };
        var right = attackOnRight();
        if (!clickMode())
        {
            double ax = (key("KeyD") || key("ArrowRight") ? 1 : 0) - (key("KeyA") || key("ArrowLeft") ? 1 : 0);
            double ay = (key("KeyS") || key("ArrowDown") ? 1 : 0) - (key("KeyW") || key("ArrowUp") ? 1 : 0);
            var al = Js.Or(Math.hypot(ax, ay), 1); outp.ax = ax / al; outp.ay = ay / al;
            outp.attack = outp.held = right ? mouse.rdown : mouse.down;
            outp.press = right ? mouse.rpress : mouse.fresh;
            if (!right) mouse.fresh = false;
            mouse.lpress = mouse.rpress = false;
            ctlReset();
            return outp;
        }
        var sh = shiftHeld(); var range = heroAttackRange();
        var lp = mouse.lpress; var rp = mouse.rpress;
        mouse.lpress = mouse.rpress = false; mouse.fresh = false;
        var atkDown = right ? mouse.rdown : mouse.down; var atkPress = right ? rp : lp;
        /* TWO CHANNELS, NOT ONE. Walking and attacking are separate: a FREE ATTACK --
           SHIFT with the left button, or the right button when the attack lives there
           -- fires at the cursor and leaves the walk alone, so a hero can cross the
           room shooting, or hold both buttons and walk and swing at once. Only a
           click ON a monster (without SHIFT) turns the walk into an approach. */
        void mark() { CTL.mark = new ClickMark { x = mouse.wx, y = mouse.wy, t = 0.45 }; }
        if (atkPress)
        {
            var under = clickTargetAt(mouse.wx, mouse.wy);
            var free = right ? !(under != null && range > 0 && CTL.dest == null && !mouse.down) : sh;
            if (free) { CTL.free = true; CTL.fired = false; outp.press = true; }
            else if (under != null && range > 0) { CTL.mode = "attack"; CTL.target = under; CTL.fired = false; CTL.dest = null; }
            else { CTL.mode = "move"; CTL.target = null; CTL.dest = new Pt(mouse.wx, mouse.wy); mark(); }
            /* the scepter attacks nothing itself: a click walks, a free attack is Convocation */
            if (range <= 0 && !free) outp.press = false;
        }
        /* THE WALK BUTTON when the attack is on the right: it only ever walks */
        if (right && lp) { CTL.mode = "move"; CTL.target = null; CTL.dest = new Pt(mouse.wx, mouse.wy); mark(); }
        /* a held walk follows the cursor -- but not while SHIFT holds the left button
           for an attack: then the walk goes on to where it was going */
        var walkDown = right ? mouse.down : (mouse.down && !sh && CTL.mode == "move");
        if (walkDown) CTL.dest = new Pt(mouse.wx, mouse.wy);
        /* HOLDING ON A MONSTER: a dead target hands the hold to whatever is under the cursor */
        var engageDown = CTL.mode == "attack" && atkDown && !(right ? false : sh);
        if (engageDown && !clickTargetLive(CTL.target))
        {
            var under = clickTargetAt(mouse.wx, mouse.wy);
            if (under != null) { CTL.target = under; CTL.fired = false; } else { CTL.mode = "move"; CTL.target = null; }
        }
        /* THE FREE ATTACK: while its button is held (SHIFT held with the left, or the
           right button), and once for a tap too quick to be held for a frame */
        var freeDown = right ? (mouse.rdown && CTL.mode != "attack") : (mouse.down && sh);
        if (freeDown && !CTL.free)
        {
            CTL.free = true; CTL.fired = false;
            /* the scepter's attack is a PRESS (Convocation): SHIFT coming down on a held
               walk is that press too, or nothing happened until you let go and clicked */
            if (range <= 0) outp.press = true;
        }
        if (CTL.free)
        {
            if (freeDown || !CTL.fired) { outp.attack = true; outp.held = freeDown || !CTL.fired; outp.stand = true; outp.aim = outp.face = mouseA; }
            else CTL.free = false;
        }
        /* ENGAGING: into range, then the weapon -- once, or for as long as it is held */
        Pt steer = null;
        if (CTL.target != null)
        {
            var t = CTL.target;
            if (!clickTargetLive(t) || (CTL.fired && !engageDown && !CTL.free)) { CTL.target = null; if (CTL.mode == "attack" && !atkDown) CTL.mode = null; }
            else
            {
                var gap = Math.hypot(t.x - p.x, t.y - p.y) - clickTargetR(t);
                if (!outp.attack) outp.aim = outp.face = Math.atan2(t.y - p.y, t.x - p.x);
                if (gap > range) steer = heroSteer(p, t.x, t.y, dt);
                else if (!outp.attack) { outp.attack = true; outp.held = engageDown || !CTL.fired; outp.engaged = true; }
            }
        }
        if (CTL.target == null && CTL.dest != null)
        {
            if (Math.hypot(CTL.dest.x - p.x, CTL.dest.y - p.y) <= CLICK_STOP) { if (!walkDown) CTL.dest = null; }
            else steer = heroSteer(p, CTL.dest.x, CTL.dest.y, dt);
        }
        if (!walkDown && CTL.mode == "move" && CTL.dest == null) CTL.mode = null;
        if (steer != null) { outp.ax = steer.x; outp.ay = steer.y; if (!outp.attack) outp.face = Math.atan2(steer.y, steer.x); }
        if (CTL.mark != null) { CTL.mark.t -= dt; if (CTL.mark.t <= 0) CTL.mark = null; }
        return outp;
    }
    /* the little ring where you clicked the ground, and a ring under what you are after */
    public static void drawClickMarks(Ctx c)
    {
        if (!clickMode() || player == null) return;
        if (CTL.mark != null)
        {
            var k = clamp(CTL.mark.t / 0.45, 0, 1);
            c.strokeStyle = rgba(255, 236, 160, 0.8 * k); c.lineWidth = 2.5;
            c.beginPath(); c.ellipse(CTL.mark.x, CTL.mark.y, 16 + 10 * (1 - k), 7 + 4 * (1 - k), 0, 0, TAU); c.stroke();
        }
        var t = CTL.target;
        if (t != null && clickTargetLive(t))
        {
            var r = clickTargetR(t) + 8;
            c.strokeStyle = rgba(255, 90, 74, 0.8); c.lineWidth = 2.5; c.setLineDash(new double[] { 6, 5 });
            c.beginPath(); c.ellipse(t.x, t.y + 2, r, r * 0.45, 0, 0, TAU); c.stroke(); c.setLineDash(new double[0]);
        }
    }
}
