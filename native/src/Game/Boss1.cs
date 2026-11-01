// lego-survival.html, JS 24795-25142: what bosses and golems do with their hands —
// the pull, the shockwave ring, hellfire, bomb patterns, the golem stomp, coils and
// throws, the chase, the mace spin, and calling for help.
namespace Brickblade;

public static partial class G
{
public const double BOSS_PULL_T = 0.85;
public static void bossPull(Ent b, double dt){
  var p = player;
  if(p.dead || p.dash.t > 0) return;        /* rolling slips the grip */
  var a = Math.atan2(b.y-p.y, b.x-p.x);
  var d = dist(p, b);
  var stop = b.r + p.r + 26;
  if(d <= stop) return;
  var drag = Math.min((d-stop)/dt, 430);  /* never overshoot into the boss */
  moveCollide(p, Math.cos(a)*drag*dt, Math.sin(a)*drag*dt);
  if(Js.R()<0.7)
    particles.Add(new Particle {kind = "spark", x = p.x+rnd(-20,20), y = p.y-rnd(0,40),
      vx = Math.cos(a)*160, vy = Math.sin(a)*160, life = 0.25, max = 0.25, color = "#ffb066"});
}
public static void spawnBossRing(Ent b){
  b.ring = new BossRing { r = 30, hit = false };
  var col = "#ff6a2a";
  addRing(b.x, b.y, 26, 460, RING_MAX, col, 13, 0.5);
  addRing(b.x, b.y, 14, 340, RING_MAX*0.7, "#ffffff", 6, 0.35);
  addSparks(b.x, b.y-10, 20, col);
  spawnBricks(b.x, b.y+8, new[]{"#6d6e68","#9ba19d"}, 12, 190);
  addShake(0.6);
  addDmg(b.x, b.y-70*(Js.Or(b.scale, 1)), "SHOCKWAVE!", col, 23);
  SFX.bomb(); SFX.slam();
}
/* HELLFIRE: a sustained rain from above, not a single burst. The boss plants
   himself, throws his arms up, and the sky keeps opening for a few seconds —
   so you have to keep moving through it instead of side-stepping one volley. */
public const double HELL_WIND = 0.75, HELL_DUR = 3.4;
public static bool bossHellfire(Ent b, double dt, double tier, bool enr){
  b.modeT += dt; b.moving = false;
  if(b.modeT < HELL_WIND){
    /* wind-up: embers gather over his head so the rain is never a surprise */
    if(Js.R()<0.8)
      particles.Add(new Particle {kind = "spark", x = b.x+rnd(-34,34), y = b.y-rnd(40,90),
        vx = rnd(-14,14), vy = -110, life = 0.45, max = 0.45, color = "#ffb066"});
    return false;
  }
  var rate = 7 + tier*1.2 + (enr?4:0);            /* meteors per second */
  b.hellAcc = b.hellAcc + rate*dt;
  while(b.hellAcc >= 1){
    b.hellAcc -= 1;
    var lead = leadPlayer(b.x, b.y, 0.95, 0.5);
    double a = rnd(TAU), rr = Math.sqrt(Js.R())*310;
    var tx = clamp(lead.x + Math.cos(a)*rr, W.wall+60, W.w-W.wall-60);
    var ty = clamp(lead.y + Math.sin(a)*rr, W.wall+60, W.h-W.wall-60);
    projectiles.Add(new Proj {kind = "meteor", sx = tx-80, sy = ty-760, tx = tx, ty = ty, dmg = shotPow(b,"meteor"),
      t = 0, dur = rnd(0.75,1.0), spin = rnd(TAU), delay = 0, fr = fireRadius(b, 31)});
    if(Js.R()<0.14) SFX.fireball();
  }
  if(b.modeT >= HELL_WIND + HELL_DUR){ b.hellAcc = 0; return true; }
  return false;
}
/* Boss bomb volleys: concentric rings instead of a random blob. One bomb dead
   centre, then rings packed at the minimum separation so no two blasts sit on
   top of each other — the inner rings fill first, so the middle of the pattern
   is where the bombs are thickest, and the last ring is pushed all the way out
   to the full radius so the whole thing covers far more ground than before. */
public static List<Pt> bombSpread(double cx, double cy, double n, double R, double minSep){
  if(n <= 1) return new List<Pt>{new Pt(cx, cy)};
  /* how many rings it takes to hold them all, packed at minSep */
  var caps = new List<double>();
  double total = 1;
  while(total < n && caps.Count() < 8){
    var rr = (caps.Count()+1) * minSep;
    var cap = Math.max(1, Math.floor(TAU*rr/minSep));
    caps.Add(cap); total += cap;
  }
  /* then stretch those rings across the whole radius, biased outward so the
     inner ring stays tight around the centre bomb */
  var outp = new List<Pt>{new Pt(cx, cy)};
  double rings = caps.Count(), a0 = rnd(TAU);
  for(var i=0; i<rings && outp.Count()<n; i++){
    var rr = R * Math.pow((i+1)/rings, 1.3);
    var k = Math.min(caps[i], n - outp.Count());
    for(var j=0;j<k;j++){
      var a = a0 + i*0.8 + (j + rnd(-0.18,0.18))*TAU/k;
      var jr = rr * rnd(0.93, 1.07);
      outp.Add(new Pt(cx + Math.cos(a)*jr, cy + Math.sin(a)*jr));
    }
  }
  while(outp.Count() < n) outp.Add(new Pt(cx + rnd(-R*0.4,R*0.4), cy + rnd(-R*0.4,R*0.4)));
  return outp;
}
/* The golem stomp: a shockwave ring plus a lingering elemental scar on the ground. */
public static void golemStomp(Ent e, GolemConf G){
  e.ring = new BossRing { r = 26, hit = false, max = G.stompR, speed = 300, dmg = G.dmg, golem = e.golem };
  addRing(e.x, e.y, 22, 340, G.stompR, G.ring, 10, 0.5);
  addRing(e.x, e.y, 12, 250, G.stompR*0.6, "#ffffff", 5, 0.35);
  addSparks(e.x, e.y-8, 16, G.dust);
  spawnBricks(e.x, e.y+8, G.bricks, 10, 200);
  addDust(e.x, e.y, 7, "rgba(200,200,190,");
  addShake(0.4);
  SFX.slam();
  if(e.golem=="fire"){
    /* a ring of burning ground you have to path around */
    for(var i=0;i<7;i++){
      var a = i/7.0*TAU + rnd(-0.2,0.2);
      hazards.Add(new Hazard {kind = "fire", dmg = Math.max(1, Math.round((Js.Or(e.dmg, 2))*0.6)),
        x = e.x+Math.cos(a)*G.stompR*0.62, y = e.y+Math.sin(a)*G.stompR*0.62,
        r = fireRadius(e, 44), life = 5, max = 5});
    }
    SFX.fireball();
  } else if(e.golem=="ice"){
    /* a wide frost field — no damage, but you crawl through it */
    hazards.Add(new Hazard {kind = "frost", x = e.x, y = e.y, r = G.stompR*0.85, life = 6, max = 6});
    SFX.freeze();
  } else {
    /* stone rips chunks out of the floor and flings them */
    for(var i=0;i<8;i++){
      var a = i/8.0*TAU;
      projectiles.Add(new Proj {kind = "jrock", dmg = shotPow(e,"jrock"), sx = e.x, sy = e.y-24,
        tx = clamp(e.x+Math.cos(a)*G.stompR*0.9, W.wall+40, W.w-W.wall-40),
        ty = clamp(e.y+Math.sin(a)*G.stompR*0.9, W.wall+40, W.h-W.wall-40),
        t = 0, dur = 0.7, spin = rnd(TAU), delay = i*0.04});
    }
  }
}
/* The frost golem's boulder and the ember golem's bomb cluster. Neither of them
   stomps: stone owns that. */
/* A COIL. It is planted, it is not thrown — it appears where the hero was, it
   drifts, and anything standing in its circle takes lightning every half second.
   Twelve seconds is long enough that the floor you cleared stops being cleared. */
public static void plantCoil(Ent e, double x, double y){
  var live = 0;
  foreach (var h in hazards.Live()) if(h.kind=="coil") live++;
  if(live >= COIL_MAX) return;
  /* the twelve seconds it is DANGEROUS start when it wakes, not when it lands */
  var arm = rnd(COIL_ARM_MIN, COIL_ARM_MAX);
  hazards.Add(new Hazard { kind = "coil", x = clamp(x, W.wall+40, W.w-W.wall-40),
    y = clamp(y, W.wall+40, W.h-W.wall-40),
    r = COIL_R, life = COIL_LIFE+arm, max = COIL_LIFE+arm, tick = rnd(0.25,0.85),
    arm = arm, armMax = arm,
    dmg = Math.max(1, Math.round((e != null ? Js.Or(e.dmg, 2) : 2) * 0.55)),
    drift = rnd(TAU), spin = rnd(TAU), seed = rnd(TAU), born = 0 });
  addRing(x, y, 10, 300, COIL_R, "#ffe95a", 6, 0.5);
}
public static void golemCoils(Ent e, GolemConf G){
  /* an even ring AROUND you rather than a pile on top of you: the angles are
     spread so two never land in the same place, and none of them lands where
     you are standing. Walking out of one is a direction you can choose. */
  var n = e.elite ? 4 : 3;
  var lead = leadPlayer(e.x, e.y, 0.7, 0.8);
  var a0 = rnd(TAU);
  for(var i=0;i<n;i++){
    var a  = a0 + i*TAU/n + rnd(-0.32, 0.32);
    var rr = rnd(COIL_SPREAD_MIN, COIL_SPREAD_MAX);
    schedule(i*0.18, ()=> plantCoil(e, lead.x+Math.cos(a)*rr, lead.y+Math.sin(a)*rr*0.85));
  }
  addSparks(e.x, e.y-24, 14, "#ffe95a");
  SFX.thunder();
}
public static void golemThrow(Ent e, GolemConf G){
  var a = e.lockA;
  if(e.golem=="ice"){
    /* LEAD IT. The boulder crosses the arena at 660px/s and was aimed at where
       you were standing when the wind-up began — by the time it arrived you had
       walked out of a 25px-wide rock. It now aims where you are going. */
    var lead = leadPlayer(e.x, e.y - 22, 0.42, 0.85);
    a = Math.atan2(lead.y - (e.y-22), lead.x - e.x);
    /* a boulder that skates the length of the field and freezes the lane it
       crossed — the trail is the real attack, the rock is just the delivery */
    projectiles.Add(new Proj {kind = "frostrock", x = e.x, y = e.y-22, owner = "enemy",
      vx = Math.cos(a)*620, vy = Math.sin(a)*620, life = 2.1, dmg = shotPow(e,"frostrock"),
      spin = rnd(TAU), trailD = 0, trailLife = rnd(5,8), tr = 48,
      fr = 0, big = true, hitR = 26});
    addSparks(e.x, e.y-20, 12, "#dff2ff");
    SFX.freeze();
  } else {
    /* fire golem: the boss bomb pattern, but a handful instead of a barrage */
    var n = e.elite ? 3 : 2;
    var lead = leadPlayer(e.x, e.y, 0.95, 0.8);
    var pts = bombSpread(lead.x, lead.y, n, 130, 74);
    for(var i=0;i<n;i++){
      projectiles.Add(new Proj {kind = "molten", sx = e.x, sy = e.y-24, dmg = shotPow(e,"molten"),
        tx = clamp(pts[i].x, W.wall+50, W.w-W.wall-50),
        ty = clamp(pts[i].y, W.wall+50, W.h-W.wall-50),
        t = 0, dur = rnd(0.8,1.0), spin = rnd(TAU), delay = i*0.14, fr = fireRadius(e, 36)});
    }
    addSparks(e.x, e.y-20, 12, "#ffb066");
    SFX.fireball();
  }
  spawnBricks(e.x, e.y+6, G.bricks, 5, 150);
  addShake(0.2);
}
/* How wide a patch of fire this caster leaves. Elites, magic-touched, lords,
   bosses and anything an overseer has buffed all burn a bigger hole. */
public static double fireRadius(Ent e, double @base){
  double m = 1;
  if(e != null){
    if(e.elite) m *= 1.35;
    if(e.magic) m *= 1.06;
    if(e.type == "boss") m *= e.super ? 1.6 : 1.3;
    else if(e.type != null && e.type.EndsWith("lord")) m *= 1.25;
    m *= 1 + 0.08*e.buffStacks;
  }
  return Math.round(@base * m * rnd(0.88, 1.16));
}
/* Bosses use the same grid, but only while walking the player down. */
public static void bossChase(Ent b, double toP, double spd, double dt, double mul = double.NaN){
  var m = Js.Or(mul, 1);
  double cx = Math.cos(toP), cy = Math.sin(toP);
  var dir = navSteer(b, dt);
  if(dir != null){ cx = dir.x; cy = dir.y; }
  var slid = slideAroundSolids(b, cx*spd*m, cy*spd*m, dt);
  if(slid != null){ var l = Js.Or(Math.hypot(slid.x, slid.y), 1); cx = slid.x/l; cy = slid.y/l; }
  b.x += cx*spd*m*dt; b.y += cy*spd*m*dt;
  b.face = new Pt(cx, cy);
}
public static void bossRingHit(Ent b){
  /* the shockwave takes every skeleton it crosses, whoever it was thrown at */
  minionsOnRing(b.ring, b.x, b.y, 30, 2 + Math.round((Js.Or(b.dmg, 2))*0.5), null);
  if(!b.ring.hit && !player.dead){
    /* A TRUE CIRCLE, drawn and measured. The squashed version reached a third
       further north and south than the picture showed, which on the attack that
       drags you into the middle first is the worst place to be lying about. */
    var d = dist(player, b);
    if(Math.abs(d - b.ring.r) < 30){
      if(player.dash.t>0){ b.ring.hit = true; perfectDodge(player.x, player.y); }
      else if(player.invuln<=0){ playerDamage(2, b); b.ring.hit = true; }
    }
  }
}
/* One place for every boss summon, so "call for help" means the same thing
   whoever is doing it — and so the crowd scales with the wave instead of being
   the same four bodies at wave 5 and wave 45. */
/* THE MACE SPIN. A boss with a mace should use it: it plants itself, winds up,
   then whirls on the spot with the whole thing extended, walking slowly at you
   the entire time. Anything the head passes through gets hit. */
/* THE WIND-UP IS THE WHOLE TELL. Half a second was not a warning, it was a
   formality — the boss was already spinning and already hitting before you had
   read that it had started. It braces for two and a half seconds now, with the
   reach painted on the floor the whole time, and NOTHING it does in that window
   can hurt you: the cast lands first, the spin second. */
public const double MACE_WIND = 2.4, MACE_SPIN_T = 4.2, MACE_SPIN_RATE = 11, MACE_TICK = 0.4;
/* A whirling boss used to creep at under half speed, which made the whole
   attack something you walked away from. It now charges at two and a half
   times its normal speed for the whole spin — the spin IS the chase. */
public const double MACE_SPIN_SPD = 2.5;
/* half the thickness of the swung head, in pixels — the blade is a line, but a
   line with no width would be impossible to be hit by */
public const double MACE_BLADE_HALF = 22;
public static readonly Tbl SKULL_VOLLEYS = new double[] {5, 10};     /* rings per barrage, by tier */
public const double SPIRAL_ARMS = 5;             /* sweeps per spiral */
public const double SPIRAL_GAP = 0.45;           /* seconds between them */
public const double SPIRAL_STEP = 0.30;          /* radians between skulls along one arm */
public const double SKULL_VOLLEY_GAP = 0.5;      /* seconds between them */
public static bool maceSpin(Ent b, double dt, double tier, bool enr, double toP){
  b.modeT += dt;
  if(b.modeT < MACE_WIND){
    /* braced, mace back, the reach painted on the ground by the renderer */
    b.moving = false;
    b.whirlA = -1.2; b.whirlPrev = double.NaN;
    /* the CAST, once, on the last beat of the wind — the roar and the ring go
       out before a single frame of the spin can land, so what you hear is the
       thing starting rather than the thing already having hit you */
    if(!Js.T(b.maceCast) && b.modeT > MACE_WIND - 0.45){
      b.maceCast = 1;
      addRing(b.x, b.y, 24, 420, maceReach(b), "#ffd166", 9, 0.55);
      addShake(0.25); SFX.roar();
    }
    return false;
  }
  b.whirlA = b.whirlA + MACE_SPIN_RATE*dt;
  b.moving = true; b.walk += dt*14;
  /* it comes at you the whole time it spins, faster than it can ever walk */
  var creep = b.speed*MACE_SPIN_SPD*(enr?1.2:1);
  b.x += Math.cos(toP)*creep*dt; b.y += Math.sin(toP)*creep*dt;
  collideWorld(b);
  var reach = maceReach(b);
  b.maceCd = b.maceCd - dt;
  /* THE BLADE HITS, NOT THE CIRCLE. Standing inside the sweep used to be enough;
     the whole disc was live, so the attack was "do not be near it" rather than
     "do not be where the sword is". Now the hit is the arc the head actually
     swept THIS FRAME -- at eleven radians a second the tip crosses forty-odd
     pixels between frames, so testing only its current angle would let you slip
     through the gap. We test the wedge from last frame's angle to this one. */
  var prevA = double.IsNaN(b.whirlPrev) ? b.whirlA : b.whirlPrev;
  b.whirlPrev = b.whirlA;
  if(!player.dead && player.dash.t<=0 && b.maceCd<=0){
    var d = dist(b, player);
    if(d < reach + player.r){
      /* how wide the hero looks from the boss at this range: close in, a body
         subtends most of the circle, so the blade cannot miss it */
      var half = Math.atan2(player.r + MACE_BLADE_HALF, Math.max(8, d));
      var pa = angTo(b, player);
      /* did the head sweep past the hero's bearing between the two frames? */
      double from = angDiff(pa, prevA), to = angDiff(pa, b.whirlA);
      var swept = (from <= half && from >= -half) || (to <= half && to >= -half) ||
                    (from > 0 && to < 0 && (from - to) < Math.PI);
      if(swept){
        b.maceCd = MACE_TICK;
        playerDamage(b.dmg+1, b);
      }
    }
  }
  /* THE SWING TAKES WHATEVER IT CROSSES, skeletons included -- each on its own
     short clock so one pass of the head is one hit */
  foreach (var m in minions.Live()){
    if(m.rise < 1 || m.gone || m.maceCd > vtime) continue;
    var dm = Math.hypot(m.x - b.x, m.y - b.y);
    if(dm >= reach + m.r) continue;
    double half = Math.atan2(m.r + MACE_BLADE_HALF, Math.max(8, dm)), pa = Math.atan2(m.y - b.y, m.x - b.x);
    double from = angDiff(pa, prevA), to = angDiff(pa, b.whirlA);
    if((from <= half && from >= -half) || (to <= half && to >= -half) || (from > 0 && to < 0 && (from - to) < Math.PI)){
      m.maceCd = vtime + MACE_TICK; minionHurt(m, b.dmg+1, null, b);
    }
  }
  if(Js.R()<0.7){
    double hx = b.x + Math.cos(b.whirlA)*reach, hy = b.y + Math.sin(b.whirlA)*reach;
    particles.Add(new Particle {kind = "spark", x = hx, y = hy, vx = rnd(-40,40), vy = rnd(-40,10),
      life = 0.22, max = 0.22, color = "#ffd166"});
  }
  if(b.modeT >= MACE_WIND + MACE_SPIN_T*(enr?1.35:1)){
    b.whirlA = double.NaN; b.whirlPrev = double.NaN; b.maceCast = 0;
    addShake(0.3); SFX.slam();
    return true;
  }
  return false;
}
/* The sweep is the whole point of the attack — at the old radius you could
   stand a step back and watch it. It now reaches roughly twice as far. */
/* The whirl grows with the boss. The first one you meet at wave 5 was sweeping
   the same circle as the last one at wave 35, which on a hero with three bricks
   to their name is not a fight, it is weather. Half the size at tier one, full
   size by tier four. */
public static double maceReach(Ent b){
  var t = Math.max(1, Js.Or(b.btier, 1));
  return (126*(Js.Or(b.scale, 1)) + 40) * Math.min(1, 0.5 + (t-1)*0.17);
}
public static void bossSummon(Ent b, double count, IList<string> kinds, double elites = 0){
  if(b != null && b.trLegion) count = Math.round(count*1.6) + 1;   /* LEGION */
  var room = Math.max(0, MAX_ALIVE - enemies.Count() - 4);
  var n = Math.min(count, room);
  for(var i=0;i<n;i++){
    var a = i/Math.max(1,n)*TAU + rnd(-0.25,0.25);
    var rr = 92 + rnd(0,70) + (i%3)*26;
    spawnEnemy(pick(kinds),
      clamp(b.x+Math.cos(a)*rr, 120, W.w-120),
      clamp(b.y+Math.sin(a)*rr, 120, W.h-120),
      i < elites);
  }
  if(n>0){
    addRing(b.x, b.y, 18, 320, 170, "#c9b8e8", 6, 0.5);
    addDmg(b.x, b.y-80, "TO ME!", "#c9b8e8", 20);
    SFX.roar();
  }
}
}

/* a boss's own clocks: hellfire's meteor count, the mace spin (whirlA is undefined
   between spins in the JS: NaN) */
public sealed partial class Ent
{
    public double hellAcc, whirlA = double.NaN, whirlPrev = double.NaN, maceCast, maceCd;
}
