// lego-survival.html, JS 25656-25966: THE QUAKE (waves of stone spikes) and one frame
// of a SUPER BOSS (updateSuperBoss).
namespace Brickblade;

public sealed class Quake { public double r, max, idx, next, a; public bool dead; }
public sealed class QuakeQueue { public double left, t, a; }
public sealed partial class Spike { public HashSet<Ent> mHit; }
public sealed partial class Ent { public List<Quake> quakes; public QuakeQueue quakeQueue; public bool quake; public double bless; }

public static partial class G
{
    /* NEW: SUPER BOSSES — bigger, meaner, signature mechanic */
    /* THE QUAKE. The Omega heaves its mace overhead and drives it into the floor.
       Three shockwaves roll out of the impact, each one further than the last, and
       riding each wave is an arc of STONE SPIKES that tear up out of the ground in
       a 180-degree fan pointed at the hero — the Path of Exile slam. The spikes are
       what actually hits you; the rings are the tell that tells you where they are
       about to come up. */
    public const double QUAKE_WIND = 0.9;           /* overhead, before the hammer falls */
    public const double QUAKE_WAVES = 3;
    /* The slam used to die out well inside the arena, so stepping back two paces
       was the whole counterplay. The mountains ride a lot further out now. */
    public static readonly Tbl QUAKE_RADII = new double[] { 190, 350, 520 };   /* each wave reaches further than the last */
    public static readonly double QUAKE_MAX = QUAKE_RADII[QUAKE_RADII.Length - 1];   /* how far the last wave reaches */
    public const double QUAKE_GAP = 0.42;           /* seconds between one wave and the next */
    public const double QUAKE_SPEED = 430;
    public const double QUAKE_W0 = 14, QUAKE_W1 = 46;
    public const double QUAKE_ARC = Math.PI;        /* the fan the spikes erupt in: 180 degrees */
    public const double QUAKE_SPIKES = 13;          /* per wave */
    public const double SPIKE_RISE = 0.16, SPIKE_HOLD = 0.2, SPIKE_SINK = 0.3;
    public static double quakeBand(Quake q) => QUAKE_W0 + (QUAKE_W1 - QUAKE_W0) * clamp(q.r / q.max, 0, 1);
    public static void startQuake(Ent b, double toP)
    {
        /* one entry per wave; they are launched one after another, not all at once */
        b.quakes = new List<Quake>();
        b.quakeQueue = new QuakeQueue { left = QUAKE_WAVES, t = 0, a = toP };
        b.slamHold = 0.55;              /* the mace stays buried while the floor breaks */
        addShake(0.8);
        spawnBricks(b.x, b.y + 8, new[] { "#3a4045", "#2c3136", "#8b8f8a" }, 18, 230);
        addDust(b.x, b.y, 11, "rgba(150,150,145,");
        addDmg(b.x, b.y - 86 * Js.Or(b.scale, 1), "QUAKE!", "#cfd6dd", 24);
        SFX.slam(); SFX.bomb();
    }
    /* A spike: it bursts up out of the floor, stands for a beat, then sinks back. */
    public static void addSpike(double x, double y, double a, double dmg)
    {
        spikes.Add(new Spike { x = x, y = y, a = a, dmg = dmg, t = 0, life = SPIKE_RISE + SPIKE_HOLD + SPIKE_SINK, hitHero = false,
            h = rnd(30, 46), w = rnd(8, 13), seed = rnd(TAU) });
    }
    public static double spikeHeight(Spike sp)
    {
        if (sp.t < SPIKE_RISE) return sp.h * (sp.t / SPIKE_RISE);
        if (sp.t < SPIKE_RISE + SPIKE_HOLD) return sp.h;
        return sp.h * Math.max(0, 1 - (sp.t - SPIKE_RISE - SPIKE_HOLD) / SPIKE_SINK);
    }
    public static void updateSpikes(double dt)
    {
        foreach (var sp in spikes.Live())
        {
            sp.t += dt;
            /* it only bites on the way up, while it is actually tearing through */
            if (!sp.hitHero && sp.t >= SPIKE_RISE * 0.45 && sp.t < SPIKE_RISE + SPIKE_HOLD
               && !player.dead && dist(sp, player) < 20 + player.r)
            {
                if (player.dash.t > 0) { sp.hitHero = true; perfectDodge(sp.x, sp.y); }
                else if (player.invuln <= 0) { playerDamage(sp.dmg, sp); sp.hitHero = true; }
            }
            if (sp.t >= SPIKE_RISE * 0.45 && sp.t < SPIKE_RISE + SPIKE_HOLD && minions.Count > 0)
            {
                sp.mHit ??= new HashSet<Ent>();
                minionsIn(sp.x, sp.y, 20, m => { if (!sp.mHit.Contains(m)) { sp.mHit.Add(m); minionHurt(m, sp.dmg, null, sp); } });
            }
            if (sp.t >= sp.life) sp.dead = true;
        }
        spikes = spikes.Where(sp => !sp.dead).ToList();
    }
    public static void quakeTick(Ent b, double dt)
    {
        /* release the waves in sequence */
        var q0 = b.quakeQueue;
        if (q0 != null)
        {
            q0.t -= dt;
            if (q0.t <= 0)
            {
                var idx = QUAKE_WAVES - q0.left;
                b.quakes.Add(new Quake { r = 10, max = QUAKE_RADII[idx], idx = idx, next = 0, a = q0.a });
                addRing(b.x, b.y, 14, 260, 80 + idx * 30, "#cfd6dd", 9 - idx * 2, 0.4);
                addShake(0.35);
                SFX.slam();
                q0.left--; q0.t = QUAKE_GAP;
                if (q0.left <= 0) b.quakeQueue = null;
            }
        }
        foreach (var q in b.quakes.Live())
        {
            q.r += QUAKE_SPEED * dt;
            /* spikes erupt along the crest as it passes, inside the 180 fan */
            var step = q.max / QUAKE_SPIKES;
            while (q.next * step <= q.r && q.next <= QUAKE_SPIKES)
            {
                var rr = Math.max(18, q.next * step);
                var count = 3 + q.idx * 2;
                for (int i = 0; i < count; i++)
                {
                    var off = (i / (count - 1) - 0.5) * QUAKE_ARC + rnd(-0.08, 0.08);
                    var a = q.a + off;
                    addSpike(b.x + Math.cos(a) * rr, b.y + Math.sin(a) * rr, a, b.dmg + 1);
                }
                q.next++;
            }
            if (Js.R() < 0.9)
            {
                var a = q.a + rnd(-QUAKE_ARC / 2.0, QUAKE_ARC / 2.0);
                particles.Add(new Particle { kind = "spark", x = b.x + Math.cos(a) * q.r, y = b.y + Math.sin(a) * q.r,
                    vx = rnd(-40, 40), vy = rnd(-110, -40), life = 0.34, max = 0.34, color = "#b9c0c6" });
            }
            if (q.r > q.max) q.dead = true;
        }
        b.quakes = b.quakes.Where(q => !q.dead).ToList();
        if (b.quakes.Count == 0 && b.quakeQueue == null) b.quake = false;
    }
public static void updateSuperBoss(Ent b, double dt, double toP, double dP){
  var enr = b.hp < b.maxHp*0.5;
  var spd = b.speed*(enr?1.3:1);
  var tier = Math.max(2, Math.floor(wave.n/5.0));
  if(b.mode=="chase"){
    bossChase(b, toP, spd, dt, 0.9);
    b.moving = true; b.walk += dt*7;
    b.nextAtk -= dt;
    if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
    if(b.nextAtk<=0){
      /* the Omega swaps one step of its cycle for the quake — that mace is
         finally load-bearing. The Mega Bone Baron trades reach for COMMAND: it
         hexes you and it blesses everything of its own that is still standing. */
      var order = b.bkey=="omegathane"
        ? new string[] {"quakeWind","missileWind","volleyWind","summonWind"}
        : b.bkey=="megabaron"
        ? new string[] {"slamWind","hexWind","summonWind","boneCallWind"}
        : new string[] {"slamWind","missileWind","volleyWind","summonWind"};
      var len = order.Count() + 1;                 /* the signature is the last step */
      b.cycle = (b.cycle+1) % len;
      b.mode = b.cycle==order.Count() ? b.sig+"Wind" : order[(int)b.cycle];
      b.modeT = 0;
      SFX.roar();
      if(b.trRest) b.nextAtk = (Js.Or(b.nextAtk, 2)) * 0.7;
    }
  }
  /* THE HEX. A fan of curse bolts, each carrying a different weakness — the
     hero it lands on fights the rest of the cycle a little more poorly. */
  else if(b.mode=="hexWind"){
    b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
    if(Js.R()<0.6)
      particles.Add(new Particle {kind = "spark", x = b.x+rnd(-34,34), y = b.y-rnd(20,90),
        vx = rnd(-24,24), vy = rnd(-30,10), life = 0.4, max = 0.4, color = "#c9b8e8"});
    if(b.modeT>=0.65){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.6:2.4;
      var n = 6 + Math.min(4, tier);
      for(var i=0;i<n;i++){
        var a2 = toP + (i-(n-1)/2.0)*0.20;
        projectiles.Add(new Proj {kind = "hexbolt", x = b.x, y = b.y-24,
          vx = Math.cos(a2)*275, vy = Math.sin(a2)*275,
          owner = "enemy", dmg = shotPow(b,"hexbolt"), life = 4,
          curse = CURSE_KEYS[i % CURSE_KEYS.Count()], src = b, seed = rnd(TAU)});
      }
      addRing(b.x, b.y, 18, 300, 150, "#c9b8e8", 6, 0.5);
      SFX.grunt();
    }
  }
  /* THE CALL. Every skeleton still on its feet stands a little taller, and so
     does the Baron — armour, damage and swing rate, on itself and on its court. */
  else if(b.mode=="boneCallWind"){
    b.modeT += dt; b.moving=false;
    if(Js.R()<0.8)
      particles.Add(new Particle {kind = "spark", x = b.x+rnd(-40,40), y = b.y-rnd(10,100),
        vx = rnd(-10,10), vy = rnd(-70,-20), life = 0.5, max = 0.5,
        color = Js.R()<0.5 ? "#f6f2e4" : "#c9b8e8"});
    if(b.modeT>=0.85){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.0:2.8;
      showBanner("RISE!", "the court is blessed", "#f6f2e4", 1.2, 34);
      addRing(b.x, b.y, 26, 420, 520, "#f6f2e4", 12, 0.6);
      addRing(b.x, b.y, 14, 320, 380, "#c9b8e8", 7, 0.5);
      SFX.roar();
      /* itself first */
      b.bless = b.bless + 1;
      b.armour = Math.round(b.armour * 1.18);
      b.dmg = Math.round(b.dmg * 1.12);
      b.atkS = Math.max(0.3, b.atkS * 0.92);
      b.suppress = Math.min(0.8, b.suppress + 0.05);
      /* then everything of its own within the call */
      var cnt = 0;
      foreach (var e2 in enemies){
        if(e2==b || e2.dead || e2.state!="active" || e2.type=="boss") continue;
        if(dist(e2,b) > 520) continue;
        if(!addMobBuff(e2, "empower", OVERSEER_MAX_STACKS)) continue;
        e2.buffStacks = buffStacksOf(e2, "empower");
        var oldMax = e2.maxHp;
        e2.maxHp = Math.round(e2.maxHp*1.18);
        e2.hp += e2.maxHp - oldMax;
        e2.armour = e2.armour + 12;
        e2.suppress = Math.min(0.6, e2.suppress + 0.08);
        if(e2.buffDmg < 6){ e2.buffDmg = e2.buffDmg+1; e2.dmg += 1; }
        e2.speed *= 1.06;
        addRing(e2.x, e2.y, 6, 200, 40, "#f6f2e4", 3, 0.35);
        cnt++;
      }
      /* and it raises a fresh rank while it is at it */
      bossSummon(b, 4 + Math.min(6, tier), ["skeleton","boneling"], enr ? 1 : 0);
      logEvent("MEGA BONE BARON blessed "+cnt+" of its court", "#f6f2e4");
    }
  }
  else if(b.mode=="quakeWind"){
    b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
    if(Js.R()<0.5)
      particles.Add(new Particle {kind = "spark", x = b.x+rnd(-30,30), y = b.y-rnd(50,110),
        vx = rnd(-14,14), vy = -40, life = 0.35, max = 0.35, color = "#cfd6dd"});
    if(b.modeT>=QUAKE_WIND){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.2:3.2;
      b.quake = true;
      startQuake(b, toP);
    }
  }
  else if(b.mode=="slamWind"){
    b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
    if(b.modeT < BOSS_PULL_T) bossPull(b, dt);
    else if(b.modeT>=BOSS_PULL_T+0.5){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.5:2.2;
      spawnBossRing(b);
      /* radial arrow burst on super slam */
      var na = 10;
      for(var i=0;i<na;i++){
        var a2 = (double)i/na*TAU;
        projectiles.Add(new Proj {kind = "arrow", x = b.x, y = b.y-14,
          vx = Math.cos(a2)*280, vy = Math.sin(a2)*280,
          owner = "enemy", dmg = shotPow(b,"arrow"), life = 3});
      }
    }
  }
  else if(b.mode=="volleyWind"){
    b.modeT += dt; b.moving=false;
    if(b.modeT>=0.7){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.6:2.4;
      var n = 8 + Math.min(6,tier);
      var lead = leadPlayer(b.x, b.y, 1.0, 0.95);
      var pts = bombSpread(lead.x, lead.y, n, 290, 86);
      for(var i=0;i<n;i++){
        projectiles.Add(new Proj {kind = "vbrick", dmg = shotPow(b,"vbrick"), sx = b.x, sy = b.y-30,
          tx = clamp(pts[i].x, 120, W.w-120),
          ty = clamp(pts[i].y, 120, W.h-120),
          t = 0, dur = rnd(0.75,0.95), spin = rnd(TAU), delay = i*0.08});
      }
      SFX.grunt();
    }
  }
  else if(b.mode=="summonWind"){
    b.modeT += dt; b.moving=false;
    if(b.modeT>=0.8){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
      /* an ultra calls down a warband, and the first few come through promoted */
      bossSummon(b, 14 + tier*3, ["imp","archer","boneling","knight","brute"], 3);
      addRing(b.x,b.y,20,300,150,"#ffd166",6,0.5);
    }
  }
  else if(b.mode=="missileWind"){
    /* a salvo of seeking bricks — they turn, so running in a straight line
       is not enough; you have to break their line of sight or out-turn them */
    b.modeT += dt; b.moving=false;
    if(b.modeT>=0.75){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.5:2.2;
      var n = enr ? 7 : 5;
      for(var i=0;i<n;i++){
        var a2 = Math.atan2(player.y-b.y, player.x-b.x) + (i-(n-1)/2.0)*0.4;
        projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-24,
          vx = Math.cos(a2)*215, vy = Math.sin(a2)*215,
          owner = "enemy", dmg = shotPow(b,"skull"), life = 6, homing = true, turn = 1.5+0.15*i,
          missile = true, spin = rnd(TAU), delay = i*0.08});
      }
      addRing(b.x,b.y,20,360,170,"#ffd166",7,0.5);
      SFX.roar();
    }
  }
  else if(b.mode=="novaWind"){
    b.modeT += dt; b.moving=false;
    if(b.modeT>=0.8){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.5;
      var n = 16;
      for(var i=0;i<n;i++){
        var a2 = (double)i/n*TAU;
        projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-20,
          vx = Math.cos(a2)*250, vy = Math.sin(a2)*250,
          owner = "enemy", dmg = shotPow(b,"skull"), life = 4.5, homing = false});
      }
      addRing(b.x,b.y,24,420,200,"#ffd166",8,0.5);
      SFX.bomb();
    }
  }
  else if(b.mode=="spiralWind"){
    b.modeT += dt; b.moving=false;
    if(b.modeT>=0.9){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.5;
      double n = 20, @base = rnd(TAU);
      for(var i=0;i<n;i++){
        var a2 = @base + i*0.32;
        projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-20,
          vx = Math.cos(a2)*260, vy = Math.sin(a2)*260,
          owner = "enemy", dmg = shotPow(b,"skull"), life = 4.5, homing = false});
      }
      SFX.grunt();
    }
  }
  else if(b.mode=="meteorWind"){
    if(b.modeT==0) showBanner("HELLFIRE!","THE SKY IS FALLING", "#ff6a3a", 1.2, 36);
    if(bossHellfire(b, dt, tier+2, enr)){
      b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.0:2.9;
    }
  }
  else if(b.mode=="recover"){
    b.modeT += dt;
    if(b.modeT>0.4){ b.mode="chase"; }
  }
  collideWorld(b);
  if(b.quake || (b.quakes != null && b.quakes.Count > 0)) quakeTick(b, dt);
  if(b.ring != null){
    b.ring.r += 380*dt;
    bossRingHit(b);
    if(b.ring.r > RING_MAX) b.ring = null;
  }
}
/* Returning Blades: a storm blade with legs left over doesn't fade when its
   flight runs out — it turns around and comes home, and at the second rank the
   hero catches it and hurls it straight back out the far side. */
/* THROW IT. The nearest body that is not the one that threw it, and nothing at
   all if there is none — a blade with nowhere to go is a blade you have thrown
   away for nothing. */
}
