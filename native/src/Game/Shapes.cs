namespace Brickblade;

/* THE SHAPES OF THE JS'S OBJECT LITERALS that more than one section builds or
   reads. Each is a partial class: the section that owns the thing adds its
   fields beside its code, so a type is declared here once and grows as the port
   reaches the sections that use it. */

public sealed partial class Breach : Obj { }
public sealed partial class Choice { }
public sealed partial class Upgrade { }
public sealed partial class Hazard : Obj { }
public sealed partial class Spike : Obj { }
public sealed partial class SlamAct { }
public sealed partial class Swing { }
public sealed partial class Abyss : Obj { }
public sealed partial class Strongbox : Obj { }
public sealed partial class Ritual : Obj { }
public sealed partial class Legion : Obj { }
public sealed partial class Wave { public double n; public bool hold; public string state; }
public sealed partial class Pickup { public bool got; }
