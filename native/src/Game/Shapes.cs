namespace Brickblade;

/* THE SHAPES OF THE JS'S OBJECT LITERALS that more than one section builds or
   reads. Each is a partial class: the section that owns the thing adds its
   fields beside its code, so a type is declared here once and grows as the port
   reaches the sections that use it. */

public sealed partial class Breach : Obj { }
public sealed partial class Upgrade { }
/* a patch of ground that hurts: fire, lava, frost, venom, a lightning coil */
public sealed partial class Hazard : Obj
{
    public string kind; public double life, max, tick, arm, armMax, drift, spin, seed, born, t;
}
/* a stone tooth: a boss's quake (spikes) or the mace's fault (pspikes). The JS
   gives the two the same `hit` key with different meanings -- a boss tooth's is a
   flag (it has struck the hero), a mace tooth's the bodies it has bitten -- so the
   flag is `hitHero` here */
public sealed partial class Spike : Obj
{
    public double a, t, life, h, w, seed, wait; public bool hitHero; public HashSet<object> hit; public SlamRec sl; public string el;
}
/* one mace slam's shared record: what it has bitten, what it dealt, how many teeth stand */
public sealed class SlamRec { public JsMap<Ent, int> hits = new(); public HashSet<Prop> props = new(); public double dealt, left; public string el; public bool propSfx; }
public sealed partial class SlamAct { public double t, aim; public bool fired; public string el; }
/* a swing in flight (startSwing builds it; updatePlayer sweeps it) */
public sealed partial class Swing
{
    public double t, dur, kb = 1, aim, from, to, reach, cur; public bool spin, crit, crited, gained;
    public HashSet<object> hit = new(), hitP = new();
}
public sealed partial class Abyss : Crack { }
public sealed partial class Ritual : Obj { }
public sealed partial class Legion : Obj { }
public sealed partial class Wave { public double n; public bool hold; public string state; }
public sealed partial class Pickup { public bool got; }
