// lego-survival.html, JS 22180-22252: THE ARROW — the bow's shot, the two
// legendaries that do something where it lands, and the ricochet.
namespace Brickblade;

public sealed partial class Proj { public double pierce; }

public static partial class G
{
    /* ================================ THE ARROW ================================ */
    public static double arrowDamage()
    {
        var b = player.bow;
        return calcDamage(physBase() + b.flat, "melee", b.more != 1 ? new[] { b.more } : null);
    }
    public static void fireArrow(double aim)
    {
        var p = player; var b = p.bow;
        p.combo = 0; p.comboTimer = 0;
        p.atkCd = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
        p.castT = 0.16;
        retPulse = 1;
        double n = clamp(Js.I32(b.n), 1, ARROW_MAX_N);
        var spd = Js.Or(weapon.arrowSpd, 780);
        var dmg = arrowDamage();
        double pierce = b.kind == "chaos" ? ARROW_PIERCE_ALL : b.pierce;
        for (int i = 0; i < n; i++)
        {
            var a = aim + (i - (n - 1) / 2) * ARROW_SPREAD;
            double sx = p.x + Math.cos(a) * 14, sy = p.y - 16 + Math.sin(a) * 10;
            projectiles.Add(new Proj { kind = "parrow", x = sx, y = sy, px = sx, py = sy,
                vx = Math.cos(a) * spd, vy = Math.sin(a) * spd, life = ARROW_LIFE, dmg = dmg, pierce = pierce,
                pfall = b.kind == "chaos" ? 0 : pierceFall(b.pierce), hit = new HashSet<object>(), el = b.kind, rico = 0 });
        }
        SFX.whoosh();
    }
    /* WHAT A LANDING DOES, beyond the hit itself -- the two legendaries that do
       something at the body: Fire Arrow bursts, Lightning Arrow forks. Both are
       arrow sources, so they carry the arrow's conversion and its ailment chance. */
    /* the tree's TEMPEST QUIVER on top of the two legendaries' own numbers */
    public static double arrowBlastR() => ARROW_BLAST_R * (1 + (player != null && player.bow != null ? player.bow.blastInc : 0));
    public static double arrowForkN() => ARROW_FORK_N + (player != null && player.bow != null ? player.bow.forkPlus : 0);
    public static void arrowOnHit(Proj pr, Ent e, double dmg)
    {
        if (pr.el == "fire")
        {
            var R = arrowBlastR();
            foreach (var o in enemiesNear(e.x, e.y, R + 60).Live())
            {
                if (o == e || o.dead || o.state != "active") continue;
                if (dist(o, e) > R + hitR(o)) continue;
                hitEnemy(o, dmg * (1 - ARROW_BLAST_LESS), angTo(e, o), 60, "arrowblast", new HitOpts());
            }
            addRing(e.x, e.y - 10, 8, 320, R, ARROW_KINDS["fire"].col, 5, 0.35);
            addSparks(e.x, e.y - 16, 8, "#ffb07c");
        }
        else if (pr.el == "light")
        {
            /* ONE TO MANY, never onward: the body the arrow found throws a bolt at each
               of its nearest neighbours, and none of those throws one again */
            var near = new List<(double d, Ent o)>();
            foreach (var o in enemies.Live())
            {
                if (o == e || o.dead || o.state != "active" || pr.hit.Contains(o)) continue;
                var d = dist(o, e);
                if (d <= ARROW_FORK_R) near.Add((d, o));
            }
            near = near.OrderBy(q => q.d).ToList();
            foreach (var (_, o) in near.Take(Js.I32(arrowForkN())).ToList())
            {
                addZap(e.x, e.y - 18, o.x, o.y - 18, 0, false);
                hitEnemy(o, dmg * ARROW_FORK_SHARE, angTo(e, o), 40, "arrowfork", new HitOpts());
            }
        }
    }
    /* THE RICOCHET KEYSTONE: an arrow about to stop may find one more body */
    public static bool arrowRicochet(Proj pr)
    {
        var ch = player != null ? player.ricochet : 0;
        if (!(ch > 0) || pr.rico >= RICO_MAX || Js.R() >= ch) return false;
        Ent best = null; double bd = RICO_R;
        foreach (var o in enemies.Live())
        {
            if (o.dead || o.state != "active" || pr.hit.Contains(o)) continue;
            var d = Math.hypot(o.x - pr.x, (o.y - 16) - pr.y);
            if (d < bd) { bd = d; best = o; }
        }
        if (best == null) return false;
        double a = Math.atan2((best.y - 16) - pr.y, best.x - pr.x), sp = Math.hypot(pr.vx, pr.vy);
        pr.vx = Math.cos(a) * sp; pr.vy = Math.sin(a) * sp;
        pr.pierce = 0; pr.rico++; pr.life = ARROW_LIFE * 0.7;
        addSparks(pr.x, pr.y, 4, "#e8f0c8");
        return true;
    }
}
