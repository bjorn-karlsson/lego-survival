// lego-survival.html, JS 21484-22179: UPDATE — one frame of the hero (updatePlayer),
// a swing's sweep, and the staff's bolt.
namespace Brickblade;

public static partial class G
{
public static void updatePlayer(double dt){
  var p = player;
  if(p.dead){ p.deadT += dt; return; }
  if(!Js.IsFinite(p.x)||!Js.IsFinite(p.y)||p.x<W.wall+8||p.x>W.w-W.wall-8||p.y<W.wall+8||p.y>W.h-W.wall-8){
    p.x=CX; p.y=CY-40; p.kx=0; p.ky=0; p.dash.t=0;
  }
  p.atkCd -= dt; p.comboTimer -= dt; p.invuln -= dt; p.flash = Math.max(0, p.flash-4*dt);
  p.dash.cd -= dt; retPulse = Math.max(0, retPulse-dt*4);
  p.magnetT = Math.max(0, p.magnetT - dt);
  /* the axe is back in your hands a breath after the blade dies; the live
     projectile keeps topping this up while it is out */
  p.reaveCd = Math.max(0, p.reaveCd - dt);
  stackTick(dt);
  if(p.fortuneT>0){
    p.fortuneT -= dt;
    if(p.fortuneT<=0) p.fortuneMul = 1;
  }
  if(p.regenBoostT > 0){
    p.regenBoostT -= dt;
    if(p.regenBoostT <= 0) p.heartStacks = 0;
    if(p.regenBoostT > 0 && p.regenRate > 0 && Js.R()<0.12)
      particles.Add(new Particle {kind = "spark", x = p.x+rnd(-16,16), y = p.y-10+rnd(-20,6), vx = 0, vy = -45,
        life = 0.45, max = 0.45, color = "#ff8a8a"});
  }
  if(p.rageT>0){
    p.rageT -= dt;
    if(p.rageT<=0) p.rageMul = 1;
    else if(Js.R()<0.35)
      particles.Add(new Particle {kind = "spark", x = p.x+rnd(-26,26), y = p.y-14+rnd(-26,10), vx = rnd(-20,20), vy = -70,
        life = 0.4, max = 0.4, color = "#ff5a4a"});
  }
  if(p.wardT>0){
    p.wardT -= dt;
    if(p.wardT<=0) p.wardN = 0;
    else if(Js.R()<0.25)
      particles.Add(new Particle {kind = "spark", x = p.x+rnd(-24,24), y = p.y-14+rnd(-26,10), vx = rnd(-12,12), vy = -40,
        life = 0.45, max = 0.45, color = "#8fd4ff"});
  }
  if(p.fortuneT>0 && Js.R()<0.3)
    particles.Add(new Particle {kind = "spark", x = p.x+rnd(-30,30), y = p.y-14+rnd(-24,10), vx = 0, vy = -55,
      life = 0.4, max = 0.4, color = "#ffb066"});
  if(p.slowT>0 && Js.R()<0.25)
    particles.Add(new Particle {kind = "spark", x = p.x+rnd(-14,14), y = p.y-rnd(10,40), vx = rnd(-15,15), vy = -30,
      life = 0.5, max = 0.5, color = "#bfe3ff"});
  /* Regeneration banks in its OWN float and only moves hp once it has a
     hundredth of a heart to give. Adding regen*dt straight into hp and rounding
     hp to hundredths meant anything under 0.6/s produced a per-frame increment
     smaller than the rounding step — it was rounded straight back off and
     nothing below that rate ever healed at all. */
  /* getting hit knocks the wind out of it — no ticking for half a second */
  p.regenStall = Math.max(0, p.regenStall - dt);
  /* the shield comes back on its own clock, which is not the regen clock: one
     is a thing you bought and the other is a thing that heals you slowly */
  esTick(p, dt);
  p.esFlash = Math.max(0, p.esFlash - dt*3);
  /* mana fills on its own clock, always */
  if(p.maxMana > 0 && p.mana < p.maxMana) p.mana = Math.min(p.maxMana, p.mana + p.manaRegen*dt);
  var regen = p.regenStall > 0 ? 0 : cursedRegen();
  /* ZEALOT'S OATH: the same regeneration, into the shield -- and none of it
     into the hearts. It does not wait for the recharge clock: it is regen, not
     recharge, which is the whole difference between the two. */
  if(p.zealot){
    if(regen > 0 && p.esMax > 0 && p.es < p.esMax)
      p.es = Math.min(p.esMax, p.es + regen*dt);
    p.regenAcc = 0;
  }
  else if(regen > 0 && p.hp < p.maxHp){
    p.regenAcc += regen*dt;
    if(p.regenAcc >= 0.01){
      var gain = Math.floor(p.regenAcc*100)/100.0;
      p.regenAcc -= gain;
      p.hp = Math.round(Math.min(p.maxHp, p.hp + gain)*100)/100.0;
    }
  } else p.regenAcc = 0;
  updateAilments(dt);
  p.blinkT -= dt; if(p.blinkT<=0){ p.blink=0.12; p.blinkT=rnd(2,4.5); } p.blink = Math.max(0,p.blink-dt);
  /* WHAT THE PLAYER WANTS THIS FRAME -- from the keys, or from the mouse (see
     POINT AND CLICK above heroControl) */
  var ctl = heroControl(p, dt);
  var aimA = ctl.aim;
  p.face = new Pt(Math.cos(ctl.face), Math.sin(ctl.face));
  double ax = ctl.ax, ay = ctl.ay;
  p.moving = (ax!=0||ay!=0);
  /* Blademaster: holding the swing button spins the hero up into a whirl. It is a
     stance, not an attack — it replaces swinging, costs footing, and locks out
     the roll while it is up. */
  p.whirl = p.blademaster>0 && !isSlammer() && ctl.held && ctl.attack && p.stunT<=0 && p.dash.t<=0 && !p.dead;
  if(p.whirl){
    p.whirlAng += WHIRL_SPIN*dt;
    /* the legs keep working under the crouch, whether or not you are walking */
    p.walk += dt*16;
    p.swing = null;                       /* the whirl and a swing are exclusive */
    /* the whirl READS momentum's reach but never builds it: a spin that fed
       the ramp it benefits from would sit at six stacks for ever */
    var reach = whirlReach();
    var dmg = swordDamage([whirlShare()]);
    foreach (var en in enemiesNear(p.x, p.y, reach + 80).Live()){
      if(en.dead || en.state!="active" || en.whirlCd>0) continue;
      if(dist(p,en) < reach + hitR(en)){
        en.whirlCd = WHIRL_TICK;
        hitEnemy(en, dmg, angTo(p,en), 90, "sword", new HitOpts {spin = true});
        /* RAGE, the swing's rule: a whirl's pass is one hit however many bodies
           it goes through -- once a tick, never once a body */
        if(p.rageOnHit > 0 && p.whirlRageCd <= 0){ stackAdd("rage", p.rageOnHit); p.whirlRageCd = WHIRL_TICK; }
      }
    }
    p.whirlRageCd = Math.max(0, p.whirlRageCd - dt);
    /* THE BLADE IS IN THE WAY. A spinning sword turns aside some of what is
       thrown at it — not everything, or the stance would be a shield, but often
       enough that whirling into a volley is a real answer. Each missile gets
       one roll as it reaches the ring. */
    var defl = Js.Or(WHIRL_DEFLECT[p.blademaster], 0);
    foreach (var pr in projectiles.Live()){
      if(pr.owner!="enemy" || pr.dead || pr.kind=="hexbolt") continue;
      if(pr.whirlRolled) continue;
      if(dist(p, pr) > reach + 8) continue;
      pr.whirlRolled = true;                 /* one roll per missile, not per frame */
      if(Js.R() >= defl) continue;
      /* sent back the way a deflected arrow is: at the nearest thing that is
         not you, and it belongs to you now */
      Ent tgt = null; double best = 1e9;
      foreach (var en in enemies){ if(en.dead||en.state!="active") continue;
        var dd = dist(pr,en); if(dd<best){ best=dd; tgt=en; } }
      var a3 = tgt != null ? angTo(pr,tgt) : p.whirlAng;
      pr.vx = Math.cos(a3)*460; pr.vy = Math.sin(a3)*460;
      pr.owner = "player"; pr.deflected = true; pr.homing = false;
      addSparks(pr.x, pr.y, 5, "#ffe08a");
      SFX.deflect();
    }
    /* and it takes the scenery with it — chests, crates, trees, the lot */
    p.whirlPropCd = p.whirlPropCd - dt;
    if(p.whirlPropCd <= 0){
      p.whirlPropCd = WHIRL_TICK;
      foreach (var pr in props.Live()){
        if(pr.dead || pr.indestructible) continue;
        if(dist(pr, p) < reach + pr.r){
          pr.hp--; pr.flash = 1;
          addSparks(pr.x, pr.y-8, 3, "#e8eef2");
          if(pr.hp <= 0) breakProp(pr);
        }
      }
    }
    /* WHAT A CYCLONE THROWS OFF: dust and chips of floor flung out of the rim
       SIDEWAYS -- along the turn, not away from it -- which is what makes a spin
       read as a spin rather than as a ring that happens to be there */
    for(var i=0;i<3;i++){
      double a2 = p.whirlAng + i*TAU/3.0 + rnd(-0.3, 0.3), rr = reach*rnd(0.75, 1.0);
      double tx = -Math.sin(a2), ty = Math.cos(a2), sp = rnd(160, 320);
      if(Js.R() < 0.55)
        particles.Add(new Particle {kind = "spark", x = p.x+Math.cos(a2)*rr, y = p.y-10+Math.sin(a2)*rr,
          vx = tx*sp + Math.cos(a2)*60, vy = ty*sp + Math.sin(a2)*60, life = 0.28, max = 0.28, color = i != 0 ? "#e8eef2" : "#c9b99a"});
      if(Js.R() < 0.25)
        particles.Add(new Particle {kind = "dust", x = p.x+Math.cos(a2)*rr*0.9, y = p.y+Math.sin(a2)*rr*0.9,
          vx = tx*rnd(60, 140), vy = ty*rnd(60, 140) - 10, life = 0.5, max = 0.5, r = rnd(4, 8), col = "rgba(210,200,180,1)"});
    }
    if(Js.R() < 0.35 && debris.Count() < 220){
      double a2 = p.whirlAng + rnd(TAU), tx = -Math.sin(a2), ty = Math.cos(a2);
      debris.Add(new Debris { x = p.x+Math.cos(a2)*reach*0.9, y = p.y+Math.sin(a2)*reach*0.9, z = 6,
        vx = tx*rnd(180, 300), vy = ty*rnd(180, 300), vz = rnd(80, 200), rot = rnd(TAU), vr = rnd(-12, 12),
        w = rnd(4, 7), h = rnd(3, 5), color = pick(new[]{"#8a8f96","#b9a88a","#6e7378"}), life = rnd(0.4, 0.7), max = 0.7 });
    }
    /* a click on a monster asked for ONE attack; a whirl gives it its tick */
    if(ctl.engaged || ctl.stand) CTL.fired = true;
  } else p.whirlAng = 0;
  var spdMul = chillMul() * (p.whirl ? (Js.Or(WHIRL_SLOW[p.blademaster], 1)) : 1);
  double vx, vy;
  if(p.stunT > 0){
    vx = 0; vy = 0; p.moving = false;      /* frozen solid — no moving, no swinging */
  } else if(p.dash.t>0){
    p.dash.t -= dt;
    vx = p.dash.dir.x*640; vy = p.dash.dir.y*640;
    if(Js.R()<0.6) addDust(p.x, p.y+2, 1);
  } else {
    var slow = p.swing != null ? 0.5 : 1;
    vx = ax*p.speed*slow*spdMul; vy = ay*p.speed*slow*spdMul;
    if(p.moving){ p.walk += dt*11*spdMul;
      p.stepT -= dt; if(p.stepT<=0){ addDust(p.x,p.y,1); p.stepT=0.22; } }
  }
  p.leadX = p.x; p.leadY = p.y; p.leadDt = dt;   /* sampled for lead-targeting */
  moveCollide(p, (vx + p.kx)*dt, (vy + p.ky)*dt);
  p.kx *= Math.max(0,1-8*dt); p.ky *= Math.max(0,1-8*dt);
  if(key("Space") && p.dash.cd<=0 && p.swing == null && p.stunT<=0 && !p.whirl){
    p.dash.t = 0.18; p.dash.cd = p.dash.cdMax;
    p.dash.dir = p.moving ? new Pt(ax, ay) : new Pt(Math.cos(aimA), Math.sin(aimA));
    SFX.dash(); addDust(p.x,p.y,5);
  }
  if(ctl.attack && p.swing == null && p.slamAct == null && p.atkCd<=0 && p.stunT<=0 && !p.whirl){
    if(ctl.engaged || ctl.stand) CTL.fired = true;         /* the one attack a click asked for */
    if(isCaster())      castBolt(aimA);
    else if(isArcher())  fireArrow(aimA);
    else if(isSummoner()){ /* the click is latched below */ }
    else if(isSlammer()) startSlam(aimA);
    else                startSwing(aimA);
  }
  /* THE SUMMON AURA: the scepter raises on its own, where you point, at its own
     rate, until the legion is full -- and a fallen place is refilled the same way */
  if(isSummoner()){
    p.raiseCd = p.raiseCd - dt;
    p.convoCd = Math.max(0, p.convoCd - dt);
    /* CONVOCATION is a CLICK, latched on mousedown: a quick tap can go down and
       up between two frames, and asking "is it held?" would miss it */
    if(ctl.press && p.stunT <= 0) convocation();
    convoTick(dt);
    if(!p.dead && p.stunT <= 0 && p.raiseCd <= 0 && legion().Count() < minionMax()) raiseMinion(aimA, false);
    /* ...and, with Raise Zombie, the dead that were zombies: their own pool, their own clock */
    p.zombieCd = p.zombieCd - dt;
    if(p.minions.zombies && !p.dead && p.stunT <= 0 && p.zombieCd <= 0 && zombieMinions().Count() < zombieCap()){
      if(raiseZombie() != null) p.zombieCd = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
    }
  }
  updateSlam(dt);
  if(p.swing != null){
    var s = p.swing;
    s.t += dt;
    double x = clamp(s.t/s.dur, 0, 1), e = 1-Math.pow(1-x,3);
    s.cur = lerp(s.from, s.to, e);
    if(x>0.1 && x<0.92){
      foreach (var en in enemiesNear(p.x, p.y, s.reach + 80).Live()){
        if(en.dead || en.state!="active" || s.hit.Contains(en)) continue;
        var d = dist(p,en);
        var hr = hitR(en);
        if(d < s.reach + hr){
          /* widen the arc by how much of it the body actually fills — testing a
             big foe's centre point made swings that clearly landed read as misses */
          var span = Math.asin(clamp(hr/Math.max(hr, d), 0, 1));
          var within = s.spin ? x>0.12 : inSweep(Math.atan2(en.y-p.y,en.x-p.x), s.from, s.to, e, 0.45+span);
          if(within){
            s.hit.Add(en);
            /* the spin is a MORE multiplier, and how big it is is the weapon's */
            var dmg = physDamage(s.spin ? new double[] {weapon.spin} : null);
            /* THE BACKSWING IS A CERTAINTY on the first body it reaches, and
               only the first: a 270-degree sweep that crit everything it
               touched would be a legendary, not a combo step. */
            var sure = s.crit && !s.crited;
            if(sure) s.crited = true;
            hitEnemy(en, dmg, Math.atan2(en.y-p.y,en.x-p.x),
                     210*(Js.Or(s.kb, 1))*weapon.kb, "sword", new HitOpts { spin = s.spin, forceCrit = sure });
            /* MOMENTUM IS BUILT BY CONNECTING, once per swing however many
               bodies are in the arc — otherwise one step into a crowd is the
               whole ramp and the mechanic is a formality. RAGE is the same
               rule: once a swing, not once a body. */
            if(!s.gained){
              s.gained = true;
              stackAdd("momentum", 1);
              if(p.rageOnHit > 0) stackAdd("rage", p.rageOnHit);
            }
            if(s.spin) addShake(0.12);
          }
        }
      }
      foreach (var pr in projectiles.Live()){
        /* curse bolts are not deflectable — you have to move */
        if((pr.kind!="arrow" && pr.kind!="skull" && pr.kind!="batbolt") || pr.owner!="enemy") continue;
        var d = dist(p,pr);
        if(d < s.reach+6){
          var within = s.spin || inSweep(Math.atan2(pr.y-p.y,pr.x-p.x), s.from, s.to, e, 0.5);
          if(within && !s.hit.Contains(pr)){
            s.hit.Add(pr);
            Ent tgt=null; double best=1e9;
            foreach (var en in enemies){ if(en.dead||en.state!="active") continue;
              var dd = dist(pr,en); if(dd<best){best=dd;tgt=en;} }
            var a2 = tgt != null ? angTo(pr,tgt) : aimA;
            pr.vx = Math.cos(a2)*460; pr.vy = Math.sin(a2)*460;
            pr.owner="player"; pr.deflected=true; pr.homing=false;
            addSparks(pr.x,pr.y,5,"#ffe08a"); SFX.deflect(); addHitNum(p.x,p.y-46,"DEFLECT!","#ffe08a",14);
          }
        }
      }
    }
    if(s.t>=s.dur) p.swing=null;
  }
  /* FROZEN SOLID. No moving, no swinging — and no casting either: nothing fires
     and no cooldown ticks, so being iced is a real hole in the run and not a
     pause that quietly banks four spells for the thaw. Anything already in the
     air finishes; the bricks orbiting you are objects, not a cast, so they stay. */
  var casting = p.stunT <= 0;
  var orb = p.orbit;
  orb.pos.Clear();
  if(orb.n>0){
    var hitCd = clamp(0.5 * 2.6/orb.spd, 0.05, 0.5);
    orb.rNow = orb.r;
    /* ONE RING, WRITTEN ONCE, CALLED TWICE: the inner ring, and the Twin Ring
       turning the other way on its own strike clock. A broken brick still has a
       place on the circle -- it is drawn rebuilding there -- but it neither
       strikes nor blocks until its clock runs out. */
    /* brk grows and shrinks with the ring in the JS (an array of n clocks); here it
       is a fixed block of clocks, and a place past n is reset as the JS's truncation
       would have dropped it */
    void ring(double radius, double dir, double[] brk, bool second, bool outer){
      int n = (int)orb.n;
      for(var i=n;i<brk.Length;i++) brk[i] = 0;
      for(var i=0;i<n;i++){
        var ang = dir*vtime*orb.spd + (i + (outer ? 0.5 : 0))*TAU/orb.n;
        var ox = p.x + Math.cos(ang)*radius;
        var oy = p.y-12 + Math.sin(ang)*radius*0.72;
        if(brk[i] > 0) brk[i] = Math.max(0, brk[i] - dt);
        orb.pos.Add(new OrbPos {x = ox, y = oy, ang = ang, r = radius, outer = outer, i = i, broken = brk[i]});
        if(brk[i] > 0) continue;
        foreach (var en in enemiesNear(ox, oy, orb.size + 80).Live()){
          if(en.dead || en.state!="active" || (second ? en.orbCd2 : en.orbCd)>0) continue;
          if(hitsBody(en, ox, oy, orb.size + 4)){
            if (second) en.orbCd2 = hitCd; else en.orbCd = hitCd;
            hitEnemy(en, calcDamage(orb.dmg,"spell"), angTo(p,en), 150, "orbit", new HitOpts {spin = true});
            addRing(en.x, en.y, 6, 160, 40, "#ffd166", 3, 0.3);
          }
        }
      }
    }
    ring(orb.r, 1, orb.brk, false, false);
    if(orb.twin != 0 && orb.aegis != 0) ring(twinRadius(orb), -1, orb.brk2, true, true);
  }
  /* bladestorm — the cooldown starts when the CAST ENDS, never while the burst
     is still pouring out of the hero. Same rule the vortex already followed. */
  var st = p.storm;
  if(st.n>0 && (casting || st.queued>0)){
    if(st.queued>0){
      st.qT -= dt;
      while(st.qT<=0 && st.queued>0){
        st.queued--; st.qT += 0.055;
        var ang = st.qOffset + st.qIdx*0.55;
        st.qIdx++;
        projectiles.Add(new Proj { kind = "wblade", art = thrownArt(), x = p.x, y = p.y-12,
          vx = Math.cos(ang)*st.spd, vy = Math.sin(ang)*st.spd,
          life = st.life, olife = st.life, dmg = calcDamage(st.dmg,"melee"), size = st.size,
          spin = ang, hit = new HashSet<object>(), phase = "out", legs = Js.Or(BOOMER_LEGS[st.boomer], 0) });
      }
      if(st.queued<=0) st.cd = st.rate;    /* cast finished — now it ticks */
    } else {
      st.cd -= dt;
      if(st.cd<=0){
        st.queued = st.n * st.count;
        st.qIdx = 0; st.qT = 0; st.qOffset = rnd(TAU);
        addRing(p.x, p.y, 12, 320, 70, "#cfd6dd", 4, 0.35);
        SFX.whoosh();
      }
    }
  }
  /* blade vortex — cooldown only ticks while NOT spinning (bugfix) */
  var vx2 = p.vortex;
  if(vx2.n>0 && (casting || vx2.t>0)){
    if(vx2.t>0){
      vx2.t -= dt;
      if(vx2.t<=0){ vx2.t = 0; vx2.cd = vx2.rate; }
      var vr = vx2.r + (vx2.n-1)*10;
      var vHit = 0.24 * vx2.hitMul;
      foreach (var en in enemiesNear(p.x, p.y, vr + 80).Live()){
        if(en.dead || en.state!="active" || en.vortCd>0) continue;
        if(dist(p,en) < vr + hitR(en)){
          en.vortCd = vHit;
          hitEnemy(en, calcDamage(vx2.dmg,"melee"), angTo(p,en), 180, "vortex", new HitOpts {spin = true});
        }
      }
      if(Js.R()<0.5)
        particles.Add(new Particle {kind = "spark", x = p.x+Math.cos(rnd(TAU))*vr*0.8, y = p.y+Math.sin(rnd(TAU))*vr*0.6,
          vx = rnd(-40,40), vy = rnd(-40,40), life = 0.25, max = 0.25, color = "#e8eef2"});
    } else {
      vx2.cd -= dt;
      if(vx2.cd<=0){
        vx2.t = vx2.dur + (vx2.n-1)*0.4;
        addRing(p.x, p.y, 14, 340, vx2.r + (vx2.n-1)*10, "#cfd6dd", 5, 0.4);
        SFX.vortex();
      }
    }
    /* the legendary: leave one standing where you are and walk away from it */
    if(vx2.totem != 0){
      vx2.totemCd -= dt;
      if(vx2.totemCd <= 0){
        var lv = vx2.totem;
        vx2.totemCd = TOTEM_EVERY[lv];
        var rr = vx2.r + (vx2.n-1)*10;
        projectiles.Add(new Proj { kind = "vtotem", art = thrownArt(), x = p.x, y = p.y, px = p.x, py = p.y,
          r = rr, life = TOTEM_LIFE[lv], max = TOTEM_LIFE[lv],
          dmg = calcDamage(vx2.dmg,"melee")*TOTEM_MUL[lv], seed = rnd(TAU) });
        addRing(p.x, p.y, 12, 300, rr, "#b9c6d2", 5, 0.4);
        SFX.vortex();
      }
    }
  }
  /* BRICKBANE — a wedge of gas, coughed out toward the cursor. It washes
     outward over about four tenths of a second and each body it reaches takes
     the opening hit once, and then the poison. */
  var bn = p.bane;
  if(bn.n>0 && casting){
    bn.cd -= dt;
    if(bn.cd<=0){
      bn.cd = bn.rate;
      var arc = Math.min(BANE_ARC_MAX, bn.arc);
      var rr  = bn.r;
      projectiles.Add(new Proj { kind = "gas", owner = "player", x = p.x, y = p.y-10,
        a0 = arc >= BANE_ARC_MAX-0.01 ? 0 : aimA, arc = arc, r = 0, rMax = rr,
        t = 0, dur = BANE_WAVE_T, life = BANE_WAVE_T+0.55, hit = new HashSet<object>(),
        dmg = calcDamage(bn.dmg,"spell"), dot = bn.dot, dotT = bn.dotT,
        stacks = bn.stacks, seed = rnd(TAU) });
      SFX.whoosh();
      addRing(p.x, p.y, 10, 260, rr*0.4, "#8ede4a", 5, 0.35);
    }
  }
  /* block freeze — pure AoE slow, no damage */
  var fz = p.freeze;
  if(fz.n>0 && casting){
    fz.cd -= dt;
    if(fz.cd<=0){
      fz.cd = fz.rate;
      var rr = fz.r;
      var dur = fz.dur;
      /* THE RING HAS TO BE THE SPELL. A fixed expansion speed with a fixed life
         drew a ring that reached the edge at exactly one radius and fell short
         at every other — so a hero who had bought reach watched a small ring
         freeze things well outside it. Speed is derived FROM the radius, so the
         frost always arrives at the edge, and always at the same moment. */
      var FZ_T = 0.42;
      addRing(p.x, p.y, 20, (rr-20)/FZ_T,              rr,     FX.friend.frostDeep, 8, FZ_T);
      addRing(p.x, p.y, 10, (rr*0.7-10)/(FZ_T*0.78),   rr*0.7, FX.friend.frost,     5, FZ_T*0.78);
      SFX.freeze();
      var cnt = 0;
      /* IT IS A FROST SPELL NOW, not pure crowd control: it lands elemental
         damage and lays chill stacks, and the fifth stack freezes solid on its
         own. Absolute Zero just skips straight to that. */
      var fdmg = frostDamage();
      /* THE CHILL IS THE COLD THAT LANDED, and how DEEP it goes is the spell's
         — hitEnemy rolls it, this says how many stacks a roll is worth. A pulse
         whose every point of cold has been converted into fire is a fire pulse:
         it burns, it does not chill, and it certainly does not freeze. */
      var csh = ailShare("frost", "frost");
      foreach (var en in enemiesNear(p.x, p.y, rr + 80).Live()){
        if(en.dead || en.state!="active") continue;
        if(dist(en,p) < rr + hitR(en)){
          en.slowT = Math.max(en.slowT, dur);
          hitEnemy(en, fdmg, angTo(p,en), 40, "frost", new HitOpts { ailN = freezeChill() });
          /* Absolute Zero: quiet, because the pulse is already shouting */
          if(fz.stun != 0 && csh > 0){ freezeSolid(en, true); addSparks(en.x, en.y-16, 6, "#ffffff"); }
          cnt++;
          addSparks(en.x, en.y-16, 3, FX.friend.frost);
        }
      }
      if(cnt>0) addHitNum(p.x, p.y-52, (fz.stun != 0?"FROZEN SOLID ×":"CHILLED ×")+S(cnt), "#bfe3ff", 16);
    }
  }
  var bo = p.bomb;
  if(bo.n>0 && casting){
    bo.cd -= dt;
    if(bo.cd<=0){
      bo.cd = bo.rate;
      Ent tgt=null; double best=1e9;
      foreach (var en in enemies){
        if(en.dead||en.state!="active") continue;
        var dd = dist(en,p);
        if(dd<best){ best=dd; tgt=en; }
      }
      var tx0 = tgt != null ? tgt.x : clamp(mouse.wx, W.wall+40, W.w-W.wall-40);
      var ty0 = tgt != null ? tgt.y : clamp(mouse.wy, W.wall+40, W.h-W.wall-40);
      var baseA = Math.atan2(ty0-p.y, tx0-p.x);
      var minR = bo.n>1 ? Math.min(bo.r*2, 380) : 0;
      var rng0 = clamp(Math.max(minR, dist(p,new Pt(tx0, ty0))), 60, 400);
      var T = clamp(rng0/460.0, 0.32, 0.8);
      var count = Math.min(bombMaxN(), bo.n);
      for(var i=0;i<count;i++){
        var off = BOMB_DIRS[i % BOMB_DIRS.Length];
        projectiles.Add(new Proj { kind = "bomb", x = p.x, y = p.y-10, sx = p.x, sy = p.y-10,
          tx = p.x+Math.cos(baseA+off)*rng0, ty = p.y+Math.sin(baseA+off)*rng0,
          t = 0, dur = T, spin = rnd(TAU),
          dmg = calcDamage(bo.dmg,"spell"), r = bo.r, gen = 0, cluster = bo.cluster });
      }
      SFX.swing();
    }
  }
  var mr = p.magnetT>0 ? 5000 : p.magnetR;
  var pm = p.magnetT>0 ? 1.7 : 1;
  var fort = p.fortuneT>0 ? p.fortuneMul : 1;

  /* storm bricks — up to CHAIN_MAX_ZAPPERS hovering zappers, each firing its own
     chaining bolt. Damage scales with spell damage. The bank stays CHARGED when no
     foes are in range, so it fires the instant one appears. */
  var chn = p.chain;
  if(chn.n>0 && chn.zappers>0 && casting){
    var zappers = Math.min(chainMaxZappers(), chn.zappers);
    /* every brick runs its own cooldown, staggered so they don't all dump at once */
    while(chn.cds.Count < zappers) chn.cds.Add(chn.rate * chn.cds.Count / zappers);
    if(chn.cds.Count > zappers) chn.cds.RemoveRange((int)zappers, chn.cds.Count - (int)zappers);
    List<Ent> cands = null; bool fired = false;
    var zapDmg = shockDamage();
    var claimed = new HashSet<Ent>();   /* spread the bricks over different first targets */
    /* the bricks take it in turns — a whole bank going off on one frame read as a
       single flash, and you could not tell how many you had */
    chn.gap = Math.max(0, chn.gap - dt);
    for(var z=0; z<zappers; z++){
      chn.cds[z] -= dt;
      if(chn.cds[z] > 0) continue;
      if(chn.gap > 0) continue;
      if(cands==null)
        cands = enemies.Where(e=>!e.dead && e.state=="active" && dist(e,p) < chn.r).ToList();
      var pool = cands.Where(e=>!e.dead && !claimed.Contains(e)).ToList();
      if(pool.Count == 0) pool = cands.Where(e=>!e.dead).ToList();
      if(pool.Count == 0){ chn.cds[z] = 0; continue; }   /* stay charged — fires the frame a foe walks in */
      chn.cds[z] = chn.rate;                          /* only consumed when it actually fires */
      chn.gap = chainGap();
      fired = true;
      /* Plot the whole bolt FIRST, so the falloff can be stretched across the
         foes it will actually reach rather than a fixed per-leap cut. With
         Crackling Storm every step forks, so this is a tree, not a line. */
      var branch = 1 + chn.fork;
      var root = pick(pool);
      claimed.Add(root);
      var hitSet = new HashSet<Ent> { root };
      var origin = chainBrickPos(p, z, zappers);
      /* nodes: {e, depth, fx, fy} — fx/fy is where the arc comes FROM */
      var nodes = new List<ChainNode> { new ChainNode { e = root, depth = 0, fx = origin.x, fy = origin.y } };
      var frontier = new List<ChainNode> { nodes[0] };
      for(var depth=1; depth<=chn.n-1 && frontier.Count > 0 && nodes.Count<CHAIN_MAX_NODES; depth++){
        var next = new List<ChainNode>();
        foreach (var node in frontier){
          /* the `branch` nearest foes this bolt has not touched yet */
          var near = new List<(Ent e, double dd)>();
          foreach (var e2 in enemies){
            if(e2.dead||e2.state!="active"||hitSet.Contains(e2)) continue;
            var dd = dist(e2, node.e);
            if(dd < 340) near.Add((e2, dd));
          }
          near = near.OrderBy(q=>q.dd).ToList();
          for(var k=0; k<branch && k<near.Count && nodes.Count<CHAIN_MAX_NODES; k++){
            var tg = near[k].e;
            if(hitSet.Contains(tg)) continue;
            hitSet.Add(tg);
            var n2 = new ChainNode { e = tg, depth = depth, fx = node.e.x, fy = node.e.y-14 };
            nodes.Add(n2); next.Add(n2);
          }
        }
        frontier = next;
      }
      double deepest = 0;
      foreach (var n2 in nodes) deepest = Math.max(deepest, n2.depth);
      /* Each jump lands a tenth of a second after the one before it, so the bolt
         visibly walks down the pack instead of resolving in a single frame. */
      foreach (var n2 in nodes){
        var dmg = (n2.depth==0 ? zapDmg : zapDmg * chainLeapFrac(n2.depth, deepest))
          / Math.pow(branch, n2.depth);     /* a fork splits the damage it carries */
        void fire(){
          addZap(n2.fx, n2.fy, n2.e.x, n2.e.y-14, n2.depth);
          if(n2.e.dead || n2.e.state!="active") return;
          /* the SHOCK goes on before the hit, the way the fireball lights a body
             before it strikes it — so a bolt that finishes something off still
             shocked it, and a shocked body takes the bolt's own hit harder */

          hitEnemy(n2.e, dmg, angTo(new Pt(n2.fx, n2.fy), n2.e), 0, "chain", new HitOpts {spin = true});
        }
        if(n2.depth==0) fire(); else schedule(n2.depth*CHAIN_JUMP_GAP, fire);
      }
    }
    if(fired) SFX.thunder();
  }
  /* pickups — magnetism + collection (this loop was accidentally deleted) */
  foreach (var pk in pickups.Live()){
    pk.t += dt;
    var d = dist(pk,p);
    if(p.magnetT > 0 && isStud(pk)){
      pk.seeking = false;      /* the drag itself happens in the physics pass */
    } else if(isStud(pk) && d < mr && pk.z<20){
      var closeness = clamp(1 - d/mr, 0, 1);
      var pull = (500 + 2400*Math.pow(closeness,1.5)) * pm;
      var a = angTo(pk,p);
      pk.vx += Math.cos(a)*pull*dt; pk.vy += Math.sin(a)*pull*dt;
    }
    if(d < 26 && pk.z<16){
      pk.got = true;
      if(pk.kind=="heart"){
        /* A heart does ONE thing: it makes you regenerate faster for half a
           minute. No free maximum hearts — handing those out on every drop was
           what let a hero walk into wave 40 with a health bar the length of the
           screen. Picking another one up refreshes the clock. */
        p.heartStacks = Math.min(HEART_STACK_MAX, p.heartStacks + 1);
        p.regenBoostT = HEART_REGEN_T;
        var hm = HEART_STACK_MUL[p.heartStacks];
        addDmg(p.x, p.y-46, "×"+S(hm)+" REGEN"+(p.heartStacks>1?" ("+S(p.heartStacks)+")":""), "#ff8a8a", 17);
        addRing(pk.x, pk.y, 8, 300, 90, "#ff8a8a", 6, 0.5);
        logEvent("heart ×"+S(p.heartStacks)+" — regeneration ×"+S(hm)+" for "+S(HEART_REGEN_T)+"s", "#ff8a8a");
        SFX.heart();
      } else if(pk.kind=="gheart"){
        p.gHearts = p.gHearts + 1;
        addMaxHp(3);                         /* the new hearts arrive full, no refill */
        addDmg(p.x, p.y-56, "+3 MAX HEARTS!", "#ffd166", 19);
        logEvent("golden heart — "+S(p.maxHp)+" hearts", "#ffd166");
        addRing(pk.x, pk.y, 8, 300, 90, "#ffd166", 6, 0.5);
        SFX.heart(); SFX.levelup();
      } else if(pk.kind=="magnet"){
        var addM = buffExtend(p, "magnet", true);
        addDmg(p.x, p.y-52, addM != 0 ? "+"+S(Math.round(addM))+"s MAGNET!" : "MAGNET!", "#9b45d6", 20);
        logEvent(addM != 0 ? "magnet +"+S(Math.round(addM))+"s — "+S(Math.ceil(p.magnetT))+"s left" : "magnet for 30s", "#9b45d6");
        addRing(pk.x, pk.y, 8, 300, 90, "#9b45d6", 6, 0.5);
        SFX.magnet();
      } else if(pk.kind=="rage"){
        var fullR = p.rageT > 0 && p.rageMul >= RAGE_CAP;
        if(!fullR) p.rageMul = Math.min(RAGE_CAP, p.rageMul*2);
        var addR = buffExtend(p, "rage", fullR);
        addDmg(p.x, p.y-52, addR != 0 ? "+"+S(Math.round(addR))+"s RAGE ×"+S(p.rageMul)+"!" : "RAGE ×"+S(p.rageMul)+"!", "#ff5a4a", 21);
        addRing(pk.x, pk.y, 8, 320, 100, "#ff5a4a", 7, 0.5);
        logEvent(addR != 0 ? "rage ×"+S(p.rageMul)+" +"+S(Math.round(addR))+"s — "+S(Math.ceil(p.rageT))+"s left"
                      : "rage ×"+S(p.rageMul)+" for "+S(Math.ceil(p.rageT))+"s", "#ff5a4a");
        SFX.levelup();
      } else if(pk.kind=="ward"){
        var fullW = p.wardT > 0 && p.wardN >= WARD_MAX;
        if(!fullW) p.wardN = Math.min(WARD_MAX, p.wardN+1);
        var addW = buffExtend(p, "ward", fullW);
        addDmg(p.x, p.y-52, addW != 0 ? "+"+S(Math.round(addW))+"s WARD ×"+S(p.wardN)+"!" : "WARD ×"+S(p.wardN)+"!", "#8fd4ff", 21);
        addRing(pk.x, pk.y, 8, 320, 100, "#8fd4ff", 7, 0.5);
        logEvent("ward ×"+S(p.wardN)+" — +"+S(wardArmour())+" armour, +"+
          S(Math.round(wardRes()*100))+"% all resistances"+
          (addW != 0 ? " · +"+S(Math.round(addW))+"s, "+S(Math.ceil(p.wardT))+"s left" : ""), "#8fd4ff");
        SFX.freeze();
      } else if(pk.kind=="fortune"){
        var fullF = p.fortuneT > 0 && p.fortuneMul >= FORTUNE_CAP;
        if(!fullF) p.fortuneMul = Math.min(FORTUNE_CAP, p.fortuneMul*2);
        var addF = buffExtend(p, "fortune", fullF);
        addDmg(p.x, p.y-52, addF != 0 ? "+"+S(Math.round(addF))+"s FORTUNE ×"+S(p.fortuneMul)+"!" : "FORTUNE ×"+S(p.fortuneMul)+"!", "#fe8a18", 20);
        logEvent(addF != 0 ? "fortune ×"+S(p.fortuneMul)+" +"+S(Math.round(addF))+"s — "+S(Math.ceil(p.fortuneT))+"s left"
                      : "fortune ×"+S(p.fortuneMul)+" for "+S(Math.ceil(p.fortuneT))+"s", "#fe8a18");
        addRing(pk.x, pk.y, 8, 300, 90, "#fe8a18", 6, 0.5);
        SFX.fortune();
      } else {
        p.studs += pk.val * fort;
        gainXp(pk.xp * fort * STUD_XP_SCALE);
        var TT = STUD_TIERS.GetValueOrDefault(pk.kind ?? "");
        if(TT != null && !string.IsNullOrEmpty(TT.label)){
          addDmg(p.x, p.y-56, "+"+S(TT.val*fort)+" "+TT.label+"!", sh(TT.col,0.35), 16);
          addRing(pk.x, pk.y, 6, 240, 40, TT.col, 4, 0.4);
        }
        p.streak = (p.streakT>0) ? p.streak+1 : 0;
        p.streakT = 0.9;
        SFX.stud(p.streak);
      }
    }
  }
  /* pickup physics + seeking drift (this is the ONLY place pickups move) */
  foreach (var pk in pickups.Live()){
    if(p.magnetT > 0 && isStud(pk)){
      /* A MAGNET IS A BLACK HOLE. The stud is dragged along the straight line to
         the hero and it accelerates as it closes — hop, bounce and drag all
         cancelled. Handing it a velocity was not enough: gravity and the ground
         bounce turned every flight into a series of hops going nowhere near me. */
      pk.vx = 0; pk.vy = 0; pk.vz = 0;
      pk.z = Math.max(0, pk.z - 200*dt);
      double dx = p.x-pk.x, dy = p.y-pk.y, dd = Js.Or(Math.hypot(dx,dy), 1);
      var speed = MAGNET_SPEED * (0.3 + 0.7*clamp(1 - dd/900.0, 0, 1));
      var step = Math.min(dd, speed*dt);
      pk.x += dx/dd*step; pk.y += dy/dd*step;
      if(Js.R() < 0.25)
        particles.Add(new Particle {kind = "spark", x = pk.x, y = pk.y-pk.z, vx = -dx/dd*120, vy = -dy/dd*120,
          life = 0.16, max = 0.16, color = "#c9a6ff"});
      continue;
    }
    pk.vz -= 800*dt; pk.z += pk.vz*dt;
    if(pk.z<0){
      pk.z=0; pk.vz*=-0.45; pk.vx*=0.6; pk.vy*=0.6;
      /* the moment it touches down, put it somewhere reachable */
      if(!pk.landed){ pk.landed = true; shoveToFloor(pk); }
    }
    pk.x += pk.vx*dt; pk.y += pk.vy*dt;
    pk.vx *= Math.max(0,1-2*dt); pk.vy *= Math.max(0,1-2*dt);
    if(pk.seeking && pk.z<=1){
      double dx = pk.seekX-pk.x, dy = pk.seekY-pk.y;
      var dd = Math.hypot(dx,dy);
      if(dd>8){
        /* a slow creep, deliberately. They are settling into a pile, not flying. */
        var pull = MERGE_CREEP;
        pk.x += dx/dd*pull*dt; pk.y += dy/dd*pull*dt;
        pk.vx *= Math.max(0,1-6*dt); pk.vy *= Math.max(0,1-6*dt);
      } else {
        pk.seeking = false; pk.vx *= 0.3; pk.vy *= 0.3;
      }
    }
    pk.x = clamp(pk.x, W.wall+14, W.w-W.wall-14);
    pk.y = clamp(pk.y, W.wall+14, W.h-W.wall-14);
  }
  pickups.RemoveAll(pk=>pk.got);
  p.streakT -= dt;
}
public static bool inSweep(double a, double from, double to, double prog, double margin){
  var total = angDiff(to, from);
  var swept = total*Math.min(1,prog);
  var d = angDiff(a, from);
  return total>0 ? (d>=-margin && d<=swept+margin) : (d<=margin && d>=swept-margin);
}
/* THE CASTER'S BASIC ATTACK. Where a sword opens an arc, a staff throws one bolt
   of its element at the cursor. It runs off the same cooldown the swing does, so
   cast speed and swing rate are one stat under two names. */
public static void castBolt(double aim){
  var p = player;
  p.combo = 0; p.comboTimer = 0;
  p.atkCd = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
  p.castT = 0.2;
  retPulse = 1;
  var spd = Js.Or(weapon.boltSpd, 520);
  projectiles.Add(new Proj { kind = "pbolt", el = weapon.elem ?? "fire",
    x = p.x + Math.cos(aim)*16, y = p.y - 14 + Math.sin(aim)*10,
    px = p.x, py = p.y-14,
    vx = Math.cos(aim)*spd, vy = Math.sin(aim)*spd,
    dmg = boltDamage(), r = boltRadius(),
    life = 1.35, spin = rnd(TAU), seed = rnd(TAU) });
  SFX.zap();
}
}

/* fields only updatePlayer reads and writes */
public sealed partial class Ent { public double orbCd2, whirlCd, whirlRageCd, whirlPropCd, raiseCd, convoCd, zombieCd, castT; }
public sealed partial class Proj { public bool whirlRolled, deflected; }
public sealed partial class Pickup { public bool landed; }
/* one leap of a storm brick's bolt: who, how deep, and where the arc comes from */
public sealed class ChainNode { public Ent e; public double depth, fx, fy; }
