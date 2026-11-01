// lego-survival.html, JS 23924-24794: one frame of a monster (updateEnemyCore) — its
// ailments and clocks, and then what its kind does: every monster's mind in one place.
namespace Brickblade;

public static partial class G
{
public static void updateEnemyCore(Ent e, double dt){
  if(e.stacks != null) stackTick(dt, e);
  if(e.state=="build"){
    e.buildT += dt/0.55;
    if(e.buildT>=1){ e.state="active"; addDust(e.x,e.y,4); SFX.build(); }
    return;
  }
  /* How far this body actually travelled last frame. Bleed reads it, and it has
     to be MEASURED rather than taken off the AI's intent: a monster shoving
     against a rock, frozen solid, or winding up an attack is standing still no
     matter what it meant to do.

     THE FIELD NAMES MATTER HERE. `lx`/`ly` were already taken: they hold the
     UNIT DIRECTION a charging or lunging body committed to. Storing a position
     in them meant the next frame read `vx = e.lx * chargeSpd` as roughly two
     thousand times four hundred and thirty, and every golem, direbat, lancer
     and boss that charged was flung into the far corner of the map. */
  if(e.rmods != null) rareModTick(e, dt);
  e.spdNow = dt > 0 ? Math.hypot(e.x - (double.IsNaN(e._spx) ? e.x : e._spx), e.y - (double.IsNaN(e._spy) ? e.y : e._spy)) / dt : 0;
  e._spx = e.x; e._spy = e.y;
  e.flash = Math.max(0, e.flash-4*dt);
  e.orbCd -= dt;
  if(Js.T(e.orbCd2)) e.orbCd2 -= dt;
  e.vortCd -= dt; e.whirlCd = Math.max(0, e.whirlCd - dt);
  e.totCd = Math.max(0, e.totCd - dt);
  e.slowT = Math.max(0, e.slowT - dt);
  if(e.poison != null) poisonTick(e, dt);
  if(e.bleed != null)  bleedTick(e, dt);
  if(e.eburn != null || e.burnt != null) burnTickEnemy(e, dt);
  /* a lord wards everything around it, whatever else it is doing */
  if(LORD_WARD.GetValueOrDefault(e.type ?? "") != null) lordWardTick(e, dt);
  if(e.chillT > 0){ e.chillT -= dt; if(e.chillT <= 0) e.chillN = 0; }
  if(e.shockT > 0){ e.shockT -= dt; if(e.shockT <= 0) e.shockN = 0; }
  if(e.shocked != null){ e.shocked.t -= dt; if(e.shocked.t <= 0) e.shocked = null; }
  if(e.expose != null) exposeTick(e, dt);
  if(e.esMax > 0){ esTick(e, dt); e.esFlash = Math.max(0, e.esFlash - dt*3); }
  if(e.frozen != null){ e.frozen.t -= dt; if(e.frozen.t <= 0) e.frozen = null; }
  if(e.emberCd > 0) e.emberCd -= dt;
  if(e.slowT>0 && Js.R()<0.15)
    particles.Add(new Particle {kind = "spark", x = e.x+rnd(-10,10), y = e.y-rnd(6,36), vx = rnd(-10,10), vy = -25,
      life = 0.4, max = 0.4, color = "#dff2ff"});
  /* FROZEN IN TIME: it takes its wounds, and nothing else happens to it */
  if(e.legionFrozen){ e.x = e.lgx; e.y = e.lgy; e.kx = 0; e.ky = 0; e.moving = false; e.strikeT = 0; return; }
  e.hitCd -= dt; e.atkCd -= dt;
  e.x += e.kx*dt; e.y += e.ky*dt;
  e.kx *= Math.max(0,1-7*dt); e.ky *= Math.max(0,1-7*dt);
  double vx=0, vy=0;
  double toP = angTo(e, player), dP = dist(e, player);
  /* frozen solid — Absolute Zero. No AI, no attacks, no contact damage. */
  if(e.stunT > 0){
    e.stunT -= dt;
    e.moving = false;
    if(Js.R()<0.45)
      particles.Add(new Particle {kind = "spark", x = e.x+rnd(-16,16), y = e.y-rnd(6,42),
        vx = rnd(-18,18), vy = rnd(-18,18), life = 0.3, max = 0.3, color = "#dff2ff"});
    collideWorld(e);
    if(e.ring != null){                       /* a shockwave already in flight keeps going */
      var rmax = Js.Or(e.ring.max, REV_MAX);
      e.ring.r += (Js.Or(e.ring.speed, 300))*dt;
      if(e.ring.r > rmax) e.ring = null;
    }
    return;
  }
  /* a sleeping ritual guard stands where it was put */
  if(e.dormant && ritualDormantTick(e)) return;
  if(e.docile){
    e.wanderT -= dt;
    if(e.wanderT<=0){ var sp = randomSpawnPos(); e.wx=sp.x; e.wy=sp.y; e.wanderT=rnd(2,5); }
    var a = angTo(e,new Pt(e.wx, e.wy));
    if(dist(e,new Pt(e.wx, e.wy))>20){ vx = Math.cos(a)*40; vy=Math.sin(a)*40; }
  }
  else if(e.type=="goblin"){ var v = goblinTick(e, dt, toP, dP); vx = v.x; vy = v.y; if(e.dead) return; }
  else if(e.type=="witchdoctor"){
    if(string.IsNullOrEmpty(e.orbKind)) e.orbKind = Js.R() < 0.5 ? "blood" : "chaos";
    if(e.mode == "cast"){
      e.modeT += dt; e.face = new Pt(Math.cos(toP), Math.sin(toP));
      if(e.modeT >= WD_CAST_T){
        for(var i=0;i<WD_ORBS;i++) orbSpawn(e.x + rnd(-34,34), e.y - 10 + rnd(-20,20), e.orbKind, orbDmg(e), e);
        e.mode = "kite"; e.atkCd = WD_CD*e.atkS;
      }
    } else {
      e.mode = "kite";
      var mv = enemyCasterMove(e, dt, toP, dP, 420, 280); vx = mv.x; vy = mv.y;
      if(e.atkCd <= 0 && dP < 700 && !e.docile){ e.mode = "cast"; e.modeT = 0; }
    }
  }
  else if(e.type=="warbanner"){ e.mode = "planted"; e.kx = 0; e.ky = 0; }
  else if(e.type=="bannerman"){
    var live = e.banner != null && !e.banner.dead;
    if(!live){ e.banner = null; e.plantCd = Math.max(0, e.plantCd - dt); }
    if(e.mode == "plant"){
      e.modeT += dt; e.face = new Pt(Math.cos(toP), Math.sin(toP));
      /* asked again as it goes in: five bearers can all start planting in the
         same second, before any of their banners is up to be counted */
      if(e.modeT >= BANNER_PLANT_T){ if(bannersUp() < bannerCap()) plantBanner(e); else e.plantCd = 2; e.mode = "chase"; }
    } else if(!live && e.plantCd <= 0 && dP < BANNER_PLANT_D && !e.docile && bannersUp() < bannerCap()){ e.mode = "plant"; e.modeT = 0; }
    else { e.mode = "chase"; vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed; }
  }
  else if(e.type=="mimic"){
    /* it hops at you, and when it is close it lunges with its lid open */
    if(e.mode == "lunge"){
      e.modeT += dt;
      vx = Math.cos(e.lockA)*430; vy = Math.sin(e.lockA)*430;
      if(e.modeT >= 0.22){ e.mode = "chase"; e.atkCd = 1.3*e.atkS; }
    } else {
      e.mode = "chase";
      var hopK = 0.35 + 0.9*Math.abs(Math.sin(vtime*5 + e.phase));
      vx = Math.cos(toP)*e.speed*hopK; vy = Math.sin(toP)*e.speed*hopK;
      if(e.atkCd <= 0 && dP < 170 && !e.docile){ e.mode = "lunge"; e.modeT = 0; e.lockA = toP; try { SFX.grunt(); } catch {} }
    }
  }
  else if(e.type=="imp"){
    var wob = Math.sin(vtime*4 + e.phase)*0.5;
    var a = toP + wob*0.35;
    vx = Math.cos(a)*e.speed; vy = Math.sin(a)*e.speed;
  }
  else if(e.type=="boneling"){
    var wob = Math.sin(vtime*6 + e.phase)*0.8;
    var a = toP + wob*0.4;
    vx = Math.cos(a)*e.speed; vy = Math.sin(a)*e.speed;
  }
  else if(e.type=="stalker"){
    if(e.mode=="lunge"){
      e.modeT += dt;
      vx = Math.cos(e.lockA)*480; vy = Math.sin(e.lockA)*480;
      if(e.modeT>=0.18){ e.mode="chase"; e.atkCd=1.6*e.atkS; }
    } else {
      var wob = Math.sin(vtime*5 + e.phase)*0.9;
      var a = toP + wob;
      vx = Math.cos(a)*e.speed; vy = Math.sin(a)*e.speed;
      if(dP<150 && e.atkCd<=0){ e.mode="lunge"; e.modeT=0; e.lockA=toP; SFX.grunt(); }
    }
  }
  else if(e.type=="archer"){
    e.strafeT -= dt; if(e.strafeT<=0){ e.strafe*=-1; e.strafeT=rnd(1.5,3.5); }
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.5){
        e.mode="chase"; e.atkCd = e.fire;
        projectiles.Add(new Proj {kind = "arrow", x = e.x, y = e.y-14, vx = Math.cos(toP)*265, vy = Math.sin(toP)*265,
          owner = "enemy", dmg = shotPow(e,"arrow"), life = 3});
        SFX.swing();
      }
    } else {
      if(dP > 310){ vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed; }
      else if(dP < 200){ vx = -Math.cos(toP)*e.speed*0.6; vy = -Math.sin(toP)*e.speed*0.6; }
      else { vx = Math.cos(toP+Math.PI/2.0)*e.speed*e.strafe*0.8; vy = Math.sin(toP+Math.PI/2.0)*e.speed*e.strafe*0.8; }
      if(e.atkCd<=0 && dP < 330){ e.mode="aim"; e.aimT=0; e.face=new Pt(Math.cos(toP), Math.sin(toP)); }
    }
  }
  /* NEW: marksman — elite bowman, 3-arrow fans */
  else if(e.type=="marksman"){
    var mv = enemyCasterMove(e, dt, toP, dP, 340, 220);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.5){
        e.mode="chase"; e.atkCd = e.fire;
        foreach (var off in new double[]{-0.26, 0, 0.26}){
          var a2 = toP+off;
          projectiles.Add(new Proj {kind = "arrow", x = e.x, y = e.y-14, vx = Math.cos(a2)*300, vy = Math.sin(a2)*300,
            owner = "enemy", dmg = shotPow(e,"arrow"), life = 3});
        }
        SFX.swing();
      }
    } else {
      if(e.atkCd<=0 && dP < 370){ e.mode="aim"; e.aimT=0; e.face=new Pt(Math.cos(toP), Math.sin(toP)); }
    }
  }
    else if(e.type=="cryptbat" || e.type=="direbat"){
    var dire = e.type=="direbat";
    var chargeSpd = dire ? 520 : 430;
    var chargeT = dire ? 0.22 : 0.16;
    var chainN = dire ? 4 : 3;
    var range = dire ? 360 : 220;
    if(e.mode=="lunge"){
      e.modeT += dt;
      vx = Math.cos(e.lockA)*chargeSpd; vy = Math.sin(e.lockA)*chargeSpd;
      if(dire && Js.R()<0.5)
        particles.Add(new Particle {kind = "spark", x = e.x+rnd(-12,12), y = e.y-rnd(4,26), vx = 0, vy = -30,
          life = 0.3, max = 0.3, color = "#b58fd6"});
      if(e.modeT>=chargeT){
        e.batChain = e.batChain+1;
        if(e.batChain>=chainN){ e.mode="chase"; e.batChain=0; e.atkCd=(dire?2.4:2.0)*e.atkS; }
        else { e.mode="wind2"; e.modeT=0; e.lockA=angTo(e,player); }
      }
    } else if(e.mode=="wind2"){
      e.modeT += dt; vx=0; vy=0;
      if(e.modeT>=(dire?0.26:0.22)){ e.mode="lunge"; e.modeT=0; }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<range && e.atkCd<=0){ e.mode="lunge"; e.modeT=0; e.batChain=0; e.lockA=toP; SFX.grunt(); }
    }
  }
  /* imp master — alternates between refilling the field with imps and hexing you */
  else if(e.type=="impmaster"){
    var mv = enemyCasterMove(e, dt, toP, dP, 440, 280);
    vx = mv.x; vy = mv.y;
    if(e.mode=="summon"){
      e.modeT += dt; vx = 0; vy = 0;
      if(e.modeT>=0.8){
        e.mode="chase"; e.atkCd = 5.0*e.atkS;
        var cnt = e.elite ? irnd(5,7) : irnd(3,5);
        for(var i=0;i<cnt;i++){
          var a = i/cnt*TAU + rnd(-0.3,0.3);
          spawnEnemy(wave.n >= 5 ? "skeleton" : "imp",
            clamp(e.x+Math.cos(a)*90, W.wall+40, W.w-W.wall-40),
            clamp(e.y+Math.sin(a)*90, W.wall+40, W.h-W.wall-40));
        }
        addRing(e.x, e.y, 14, 300, 110, "#ff8a8a", 6, 0.5);
        SFX.roar();
      }
    } else if(e.mode=="hex"){
      e.aimT += dt;
      if(e.aimT>=0.55){
        e.mode="chase"; e.atkCd = e.fire;
        /* a fan of curse bolts, each carrying its own hex */
        foreach (var off in new double[]{-0.2, 0, 0.2}){
          var a2 = toP+off;
          projectiles.Add(new Proj {kind = "hexbolt", x = e.x, y = e.y-18,
            vx = Math.cos(a2)*250, vy = Math.sin(a2)*250,
            owner = "enemy", dmg = shotPow(e,"hexbolt"), life = 3.4, curse = pick(CURSE_KEYS), src = e, seed = rnd(TAU)});
        }
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 560){
        var impCount = enemies.reduce((a,e2)=> a + (e2.type=="imp" && !e2.dead ? 1 : 0), 0);
        e.mode = (impCount < 14 && Js.R() < 0.5) ? "summon" : "hex";
        e.modeT = 0; e.aimT = 0;
        addRing(e.x, e.y, 16, 200, 70, e.mode=="summon" ? "#ff8a8a" : "#9b45d6", 4, 0.6);
      }
    }
  }
  /* pumpkin lord — lobs fused lego-pumpkins from range */
  else if(e.type=="pumpkinlord"){
    var mv = enemyCasterMove(e, dt, toP, dP, 420, 240);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.6){
        e.mode="chase"; e.atkCd = Math.max(PUMPKIN_MIN_RATE, e.fire);
        /* one pumpkin, two if it has been promoted — and never more than a
           dozen fuses burning on the field at once, however many lords live */
        var live = 0;
        foreach (var q in projectiles) if(q.kind=="pumpkin" && !q.dead) live++;
        var n = Math.max(0, Math.min(e.elite ? 2 : 1, PUMPKIN_LIVE_CAP - live));
        for(var i=0;i<n;i++){
          var dur = rnd(0.75,1.0);
          /* IT AIMS AT YOU. Leading by the whole three-second fuse meant it was
             throwing at where you might be in three seconds' time, which is
             indistinguishable from throwing at random — and the fuse already
             gives you the time to walk off it, which is the actual mechanic.
             It leads the FLIGHT only, and the scatter is half what it was. */
          var lead = leadPlayer(e.x, e.y, dur + 0.45, 0.8);
          projectiles.Add(new Proj {kind = "pumpkin", sx = e.x, sy = e.y-26,
            tx = clamp(lead.x+rnd(-26,26), W.wall+50, W.w-W.wall-50),
            ty = clamp(lead.y+rnd(-26,26), W.wall+50, W.h-W.wall-50),
            t = 0, dur = dur, spin = rnd(TAU), delay = i*0.22,
            fuse = 3, r = fireRadius(e, 57) });
        }
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 480){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(!string.IsNullOrEmpty(e.golem)){
    var G = GOLEM_CONF[e.golem];
    if(e.mode=="chargeWind"){
      /* braced, aiming — the ground streak in front of it is the tell */
      e.modeT += dt;
      e.face = new Pt(Math.cos(e.lockA), Math.sin(e.lockA));
      if(e.modeT>=0.55){ e.mode="charge"; e.modeT=0; e.lx=Math.cos(e.lockA); e.ly=Math.sin(e.lockA); SFX.grunt(); }
    }
    else if(e.mode=="charge"){
      e.modeT += dt;
      vx = e.lx*G.chargeSpd; vy = e.ly*G.chargeSpd;
      if(Js.R()<0.8) addDust(e.x, e.y, 1);
      /* it stops the moment it reaches you, then plants the stomp */
      if(e.modeT>=0.42 || dP < e.r+player.r+10){ e.mode="stompWind"; e.modeT=0; SFX.slam(); }
    }
    else if(e.mode=="stompWind"){
      /* arms up, ring painted on the ground, then a slow heavy landing */
      e.modeT += dt; vx=0; vy=0;
      if(Js.R()<0.5)
        particles.Add(new Particle {kind = "spark", x = e.x+rnd(-24,24), y = e.y-rnd(6,52), vx = 0, vy = -40,
          life = 0.4, max = 0.4, color = G.dust});
      if(e.modeT>=G.wind){
        e.mode="chase"; e.modeT=0; e.atkCd = 4.4*e.atkS;
        golemStomp(e, G);
      }
    }
    /* frost and ember both wind up a throw instead: planted, arms back, then it
       goes. Re-aimed right up to the release so you cannot just walk sideways. */
    else if(e.mode=="throwWind"){
      e.modeT += dt; vx=0; vy=0;
      if(e.modeT < GOLEM_THROW_WIND*0.6) e.lockA = toP;
      e.face = new Pt(Math.cos(e.lockA), Math.sin(e.lockA));
      if(Js.R()<0.6)
        particles.Add(new Particle {kind = "spark", x = e.x+rnd(-22,22), y = e.y-rnd(20,58),
          vx = rnd(-20,20), vy = -50, life = 0.4, max = 0.4, color = G.dust});
      if(e.modeT>=GOLEM_THROW_WIND){
        e.mode="chase"; e.modeT=0;
        e.atkCd = (e.golem=="ice" ? 5.0 : e.golem=="storm" ? 5.4 : 4.6)*e.atkS;
        if(G.kit=="coils") golemCoils(e, G); else golemThrow(e, G);
      }
    }
    else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(e.atkCd<=0){
        if(G.kit=="stomp"){
          if(dP < G.stompR*0.72){ e.mode="stompWind"; e.modeT=0; }           /* already on top of you */
          else if(dP < G.range){ e.mode="chargeWind"; e.modeT=0; e.lockA=toP; }  /* close the gap first */
        } else if(dP < G.range){
          e.mode="throwWind"; e.modeT=0; e.lockA=toP;
        }
      }
    }
  }
  else if(e.type=="greenwarden"){
    /* he keeps his distance and keeps everything else alive */
    var mv = enemyCasterMove(e, dt, toP, dP, 560, 400);
    vx = mv.x; vy = mv.y;
    e.wardT = e.wardT - dt;
    if(e.wardT <= 0){
      e.wardT = 0.5;
      var mended = 0;
      foreach (var e2 in enemiesNear(e.x, e.y, WARDEN_R + 60)){
        /* himself, never — that is the deal */
        if(e2==e || e2.dead || e2.state!="active") continue;
        if(e2.hp >= e2.maxHp) continue;
        if(dist(e2, e) > WARDEN_R) continue;
        e2.hp = Math.min(e2.maxHp, e2.hp + e2.maxHp*WARDEN_HEAL*0.5);
        mended++;
        if(Js.R()<0.35)
          particles.Add(new Particle {kind = "spark", x = e2.x+rnd(-10,10), y = e2.y-rnd(10,34),
            vx = rnd(-8,8), vy = rnd(-40,-14), life = 0.5, max = 0.5, color = "#bdf0a8"});
      }
      if(mended != 0) e.wardLit = 0.5;
    }
    e.wardLit = Math.max(0, e.wardLit - dt);
  }
  else if(e.shade){
    /* it does not run you down, it drifts in — two sine weaves at different
       rates so the approach never reads as a straight line */
    var wob = Math.sin(vtime*1.5 + e.phase)*0.8 + Math.sin(vtime*0.63 + e.phase*2)*0.45;
    var sp = e.speed * (0.82 + 0.26*Math.sin(vtime*2.1 + e.phase));
    var a = toP + wob*0.5;
    vx = Math.cos(a)*sp; vy = Math.sin(a)*sp;
    if(Js.R()<0.25)
      particles.Add(new Particle {kind = "spark", x = e.x+rnd(-13,13), y = e.y-rnd(10,40),
        vx = rnd(-8,8), vy = rnd(-26,-6), life = 0.5, max = 0.5,
        color = e.zorb ? "#ffe95a" : "#9b45d6"});
    if(e.zorb){
      if(e.mode=="aim"){
        e.aimT += dt; vx *= 0.2; vy *= 0.2;
        if(e.aimT>=0.75){
          e.mode="chase"; e.atkCd = e.fire;
          /* aimed where you are heading — the orb is slow enough to walk out of
             if it is thrown at where you stand */
          var lead = leadPlayer(e.x, e.y, 1.8, 0.6);
          var a2 = Math.atan2(lead.y-(e.y-22), lead.x-e.x);
          projectiles.Add(new Proj {kind = "zorb", x = e.x, y = e.y-22, owner = "enemy",
            vx = Math.cos(a2)*ZORB_SPD, vy = Math.sin(a2)*ZORB_SPD,
            life = 5.4, ang = rnd(TAU), zapCd = 0, seed = rnd(TAU), dmg = shotPow(e,"zorb")});
          addRing(e.x, e.y, 12, 240, 80, "#ffe95a", 5, 0.45);
          SFX.thunder();
        }
      } else if(e.atkCd<=0 && dP < 640){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="bastion"){
    /* slow tanky melee —heavy hits, innate armor already applied at spawn */
    if(e.mode=="windup"){
      e.modeT += dt;
      if(e.modeT>=0.8){ e.mode="lunge"; e.modeT=0; e.lx=Math.cos(e.lockA); e.ly=Math.sin(e.lockA); SFX.grunt(); }
    } else if(e.mode=="lunge"){
      e.modeT += dt;
      vx = e.lx*260; vy = e.ly*260;
      if(Js.R()<0.4) addDust(e.x,e.y,1);
      if(e.modeT>=0.4){ e.mode="chase"; e.atkCd=2.8*e.atkS; }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<140 && e.atkCd<=0){ e.mode="windup"; e.modeT=0; e.lockA=toP; }
    }
  }
  else if(e.type=="hexer"){
    var mv = enemyCasterMove(e, dt, toP, dP, 400, 280);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.55){
        e.mode="chase"; e.atkCd = e.fire;
        foreach (var off in new double[]{-0.26,0,0.26}){
          var a2 = toP+off;
          projectiles.Add(new Proj {kind = "skull", x = e.x, y = e.y-16,
            vx = Math.cos(a2)*205, vy = Math.sin(a2)*205,
            owner = "enemy", dmg = shotPow(e,"skull"), life = 4, homing = true, turn = 1.5});
        }
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 430){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="pyromancer" || e.type=="pyrolord"){
    var lord = e.type=="pyrolord";
    var mv = enemyCasterMove(e, dt, toP, dP, lord?420:380, lord?280:240);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.55){
        e.mode="chase"; e.atkCd = e.fire;
        var shots = lord ? new double[] {-130, 0, 130} : new double[] {0};
        shots.forEach((off, i)=>{
          projectiles.Add(new Proj {kind = "fireball", x = e.x, y = e.y-16, sx = e.x, sy = e.y-16,
            dmg = shotPow(e,"fireball"),
            tx = clamp(player.x+off+rnd(-30,30), W.wall+40, W.w-W.wall-40),
            ty = clamp(player.y+rnd(-40,40), W.wall+40, W.h-W.wall-40),
            t = 0, dur = 0.9, spin = rnd(TAU), delay = i*0.15, fr = fireRadius(e, 31)});
        });
        SFX.fireball();
      }
    } else {
      if(e.atkCd<=0 && dP < (lord?520:460)){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="frostmage" || e.type=="frostlord"){
    var lord = e.type=="frostlord";
    var mv = enemyCasterMove(e, dt, toP, dP, lord?440:400, lord?300:260);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.45){
        e.mode="chase"; e.atkCd = e.fire;
        var n = lord?2:1;
        for(var i=0;i<n;i++)
          projectiles.Add(new Proj {kind = "frostbolt", x = e.x, y = e.y-16,
            vx = Math.cos(toP+(i-(n-1)/2.0)*0.16)*330, vy = Math.sin(toP+(i-(n-1)/2.0)*0.16)*330,
            owner = "enemy", dmg = shotPow(e,"frostbolt"), life = 2.6});
        SFX.freeze();
      }
    } else if(e.mode=="blizzard"){
      e.modeT += dt;
      if(e.modeT>=0.7){
        e.mode="chase"; e.atkCd = e.fire*1.7 + 2;
        var n = lord ? 3 : 1;
        for(var i=0;i<n;i++){
          projectiles.Add(new Proj {kind = "blizzard",
            x = clamp(player.x+(i != 0? rnd(-220,220):0), W.wall+80, W.w-W.wall-80),
            y = clamp(player.y+(i != 0? rnd(-220,220):0), W.wall+80, W.h-W.wall-80),
            t = 0, dur = 1.2, r = lord?135:120});
        }
        SFX.freeze();
      }
    } else {
      if(e.atkCd<=0 && dP < 430){
        e.mode = Js.R()<(lord?0.5:0.35) ? "blizzard" : "aim";
        e.aimT = 0; e.modeT = 0;
      }
    }
  }
  else if(e.type=="stormcaller" || e.type=="stormlord"){
    var lord = e.type=="stormlord";
    var mv = enemyCasterMove(e, dt, toP, dP, lord?460:420, lord?290:250);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.4){
        e.mode="chase"; e.atkCd = e.fire;
        /* wider fan — dodgeable */
        var n = lord?3:1;
        for(var i=0;i<n;i++){
          var a2 = toP + (i-(n-1)/2.0)*(lord?0.46:0.42);
          projectiles.Add(new Proj {kind = "bolt", x = e.x, y = e.y-16,
            vx = Math.cos(a2)*(lord?320:430), vy = Math.sin(a2)*(lord?320:430),
            owner = "enemy", dmg = shotPow(e,"bolt"), life = lord?2.2:1.6, big = lord});
        }
        SFX.thunder();
      }
    } else {
      if(e.atkCd<=0 && dP < 460){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="necromancer" || e.type=="necrolord"){
    var lord = e.type=="necrolord";
    var mv = enemyCasterMove(e, dt, toP, dP, lord?520:460, lord?360:320);
    vx = mv.x; vy = mv.y;
    if(e.mode=="summon"){
      e.modeT += dt; vx = 0; vy = 0;
      if(e.modeT>=0.85){
        e.mode="chase"; e.atkCd = (lord?8:4.6)*e.atkS + 2;
        var cnt = lord ? irnd(9,12) : irnd(2,3);
        for(var i=0;i<cnt;i++){
          var a = rnd(TAU);
          var kind = lord ? pick(["boneling","boneling","archer","imp"]) : pick(["boneling","archer"]);
          spawnEnemy(kind,
            clamp(e.x+Math.cos(a)*(lord?110:70), W.wall+40, W.w-W.wall-40),
            clamp(e.y+Math.sin(a)*(lord?110:70), W.wall+40, W.h-W.wall-40));
        }
        addRing(e.x, e.y, 12, 300, lord?150:90, "#c9b8e8", 5, 0.5);
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 620 && enemies.Count() < (lord?150:110)){
        e.mode="summon"; e.modeT=0;
        addRing(e.x, e.y, 20, 200, lord?110:70, "#c9b8e8", 4, 0.7);
      }
    }
  }
  else if(e.type=="spitter"){
    var mv = enemyCasterMove(e, dt, toP, dP, 360, 230);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT>=0.45){
        e.mode="chase"; e.atkCd = e.fire;
        projectiles.Add(new Proj {kind = "spit", dmg = shotPow(e,"spit"), x = e.x, y = e.y-16, sx = e.x, sy = e.y-16,
          tx = clamp(player.x+rnd(-40,40), W.wall+30, W.w-W.wall-30),
          ty = clamp(player.y+rnd(-40,40), W.wall+30, W.h-W.wall-30),
          t = 0, dur = 0.8, spin = rnd(TAU), delay = 0});
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 400){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="bloater"){
    /* PLAGUE BREATH: it plants its feet, heaves, and retches a cone of rot that
       turns slowly after you for a second -- poison on you, rot on your minions */
    if(e.mode=="breathWind"){
      e.modeT += dt; vx = 0; vy = 0; e.breathA = toP;
      if(e.modeT >= BLOAT_WIND){ e.mode = "breath"; e.modeT = 0; e.breathTick = 0; SFX.grunt(); }
    } else if(e.mode=="breath"){
      e.modeT += dt; vx = 0; vy = 0;
      e.breathA += clamp(angDiff(toP, e.breathA), -1.2*dt, 1.2*dt);
      e.face = new Pt(Math.cos(e.breathA), Math.sin(e.breathA));
      for(var i=0;i<3;i++){
        double a = e.breathA + rnd(-BLOAT_CONE, BLOAT_CONE), sp = rnd(150, 260);
        particles.Add(new Particle { kind = "spark", x = e.x + Math.cos(e.breathA)*14*e.scale, y = e.y - 26*e.scale,
          vx = Math.cos(a)*sp, vy = Math.sin(a)*sp, life = 0.55, max = 0.55, color = Js.R() < 0.5 ? "#9ede5a" : "#6b8e23" });
      }
      e.breathTick -= dt;
      if(e.breathTick <= 0){
        e.breathTick = BLOAT_TICK;
        bool inCone(Obj t) => Math.hypot(t.x - e.x, t.y - e.y) < BLOAT_RANGE + (Js.Or(t.r, 12)) &&
                           Math.abs(angDiff(Math.atan2(t.y - e.y, t.x - e.x), e.breathA)) < BLOAT_CONE;
        var dose = Math.max(0.15, (Js.Or(e.dmg, 1)) * BLOAT_PSN);
        var hero = heroObj ?? player;
        if(hero != null && !hero.dead && hero.dash.t <= 0 && inCone(hero)){
          var keep = player; player = hero; poisonPlayer(dose, PSN_DUR); player = keep;
        }
        foreach (var m in minions) if(m.rise >= 1 && !m.gone && inCone(m)) minionHurt(m, dose*BLOAT_TICK*6, "chaos", e);
      }
      if(e.modeT >= BLOAT_BREATH){ e.mode = "chase"; e.atkCd = BLOAT_CD * e.atkS; }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(!e.docile && dP < BLOAT_RANGE*0.85 && e.atkCd <= 0){ e.mode = "breathWind"; e.modeT = 0; }
    }
  }
  else if(e.type=="blightspitter"){
    /* it keeps its distance like any caster and lobs the gobbet in an arc, so it
       can drop blight over a wall of its own bodies and onto your feet */
    var mv = enemyCasterMove(e, dt, toP, dP, 430, 300);
    vx = mv.x; vy = mv.y;
    if(e.mode=="aim"){
      e.aimT += dt;
      if(e.aimT >= 0.7){
        e.mode="chase"; e.atkCd = e.fire;
        projectiles.Add(new Proj {kind = "venom", dmg = shotPow(e,"venom"), x = e.x, y = e.y-16, sx = e.x, sy = e.y-16,
          /* it leads you a little, so simply walking in a line does not beat it */
          tx = clamp(player.x + player.vx*0.35 + rnd(-34,34), W.wall+30, W.w-W.wall-30),
          ty = clamp(player.y + player.vy*0.35 + rnd(-34,34), W.wall+30, W.h-W.wall-30),
          t = 0, dur = 0.9, spin = rnd(TAU), delay = 0});
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP < 470){ e.mode="aim"; e.aimT=0; }
    }
  }
  else if(e.type=="overseer"){
    /* battle herald — pulses stacking buffs onto nearby monsters */
    var mv = enemyCasterMove(e, dt, toP, dP, 520, 380);
    vx = mv.x; vy = mv.y;
    if(e.mode=="buffWind"){
      e.modeT += dt; vx = 0; vy = 0;
      if(e.modeT>=0.9){
        e.mode="chase"; e.atkCd = 6*e.atkS;
        addRing(e.x, e.y, 20, 340, 300, "#ffe95a", 7, 0.55);
        addRing(e.x, e.y, 12, 260, 220, "#c9b8e8", 5, 0.5);
        SFX.fortune();
        var cnt = 0;
        foreach (var e2 in enemies){
          if(e2==e || e2.dead || e2.state!="active" || e2.type=="boss") continue;
          if(dist(e2,e) < 300){
            if(!addMobBuff(e2, "empower", OVERSEER_MAX_STACKS)) continue;
            e2.buffStacks = buffStacksOf(e2, "empower");
            var oldMax = e2.maxHp;
            e2.maxHp = Math.round(e2.maxHp*1.15);
            e2.hp += e2.maxHp - oldMax;
            e2.armour = e2.armour + 10;      /* flat armour, additive */
            e2.suppress = Math.min(0.6, e2.suppress + 0.08);
            if(e2.buffDmg < 6){ e2.buffDmg = e2.buffDmg+1; e2.dmg += 1; }
            /* buffStacks also feeds elemPower(), so their fire/frost/lightning
               lands harder and burns a wider patch of ground */
            cnt++;
            addSparks(e2.x, e2.y-20, 3, "#ffe95a");
          }
        }
        if(cnt>0) addHitNum(e.x, e.y-60, "EMPOWERED ×"+cnt, "#ffe95a", 16);
      }
    } else {
      if(e.atkCd<=0 && dP < 700){
        e.mode="buffWind"; e.modeT=0;
        addRing(e.x, e.y, 20, 200, 300, "#ffe95a", 4, 0.8);
      }
    }
  }
  else if(e.type=="brute"){
    if(e.mode=="windup"){
      e.modeT += dt;
      if(e.modeT>=0.55){ e.mode="lunge"; e.modeT=0; e.lx=Math.cos(e.lockA); e.ly=Math.sin(e.lockA); SFX.grunt(); }
    } else if(e.mode=="lunge"){
      e.modeT += dt;
      vx = e.lx*340; vy = e.ly*340;
      if(Js.R()<0.5) addDust(e.x,e.y,1);
      if(e.modeT>=0.32){ e.mode="chase"; e.atkCd=2.4*e.atkS; }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<130 && e.atkCd<=0){ e.mode="windup"; e.modeT=0; e.lockA=toP; }
    }
  }
  else if(e.type=="bomber"){
    if(e.mode=="fuse"){
      e.modeT += dt;
      e.flash = 0.4 + 0.4*Math.sin(vtime*30);
      vx = Math.cos(toP)*e.speed*1.5; vy = Math.sin(toP)*e.speed*1.5;
      if(e.modeT>=0.9){
        e.dead = true;
        explode(e.x, e.y, 85, 0, new ExplodeOpts {hostile = true, brickCols = new Col[]{"#fe8a18","#3a3f44","#d3242b"}, shake = 0.45});
        spawnBricks(e.x, e.y, new[]{e.col.head, e.col.torso}, 8, 220);
        SFX.brick(5);
        /* THE BOMBER IS THE BOMB. Its entire existence is this one detonation
           and it was paying a hardcoded 1 for it — a third of a heart after
           armour, at any wave, for a monster that has to reach you to work. */
        if(dist(e,player)<85 && player.dash.t<=0 && player.invuln<=0 && !player.dead)
          playerDamage(Math.max(2, e.dmg*2.6), e);
        minionsIn(e.x, e.y, 85, m=>{ if(!(player.isProxy && player.minion == m)) minionHurt(m, Math.max(2, e.dmg*2.6), null, e); });
        return;
      }
    } else {
      var wob = Math.sin(vtime*4+e.phase)*0.3;
      vx = Math.cos(toP+wob)*e.speed; vy = Math.sin(toP+wob)*e.speed;
      if(dP<95 && e.atkCd<=0){ e.mode="fuse"; e.modeT=0; SFX.grunt(); }
    }
  }
  else if(e.type=="lancer"){
    if(e.mode=="windup"){
      e.modeT += dt;
      if(e.modeT>=0.7){ e.mode="lunge"; e.modeT=0; e.lx=Math.cos(e.lockA); e.ly=Math.sin(e.lockA); SFX.grunt(); }
    } else if(e.mode=="lunge"){
      e.modeT += dt;
      vx = e.lx*520; vy = e.ly*520;
      if(Js.R()<0.6) addDust(e.x,e.y,1);
      if(e.modeT>=0.26){ e.mode="chase"; e.atkCd=2.2*e.atkS; }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<210 && e.atkCd<=0){ e.mode="windup"; e.modeT=0; e.lockA=toP; }
    }
  }
  /* JUGG: mace SLAM with a mini shockwave — no more brick throwing */
  else if(e.type=="jugg"){
    if(e.mode=="wind"){
      e.modeT += dt;
      if(e.modeT>=0.7){
        e.mode="chase"; e.atkCd=3.0*e.atkS;
        addRing(e.x,e.y,20,300,130,"#ffd166",7,0.4);
        addShake(0.3); SFX.slam();
        spawnBricks(e.x,e.y+8,new[]{"#6d6e68","#8b8f8a"},4,140);
        e.ring = new BossRing { r = 24, hit = false, max = 130, speed = 340 };
      }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<120 && e.atkCd<=0){ e.mode="wind"; e.modeT=0; SFX.grunt(); }
    }
  }
  /* NEW: lobber — green skeleton throwing heavy bricks */
  else if(e.type=="lobber"){
    var mv = enemyCasterMove(e, dt, toP, dP, 420, 260);
    vx = mv.x; vy = mv.y;
    if(e.mode=="wind"){
      e.modeT += dt;
      if(e.modeT>=0.6){
        e.mode="chase"; e.atkCd=3.4*e.atkS;
        projectiles.Add(new Proj {kind = "jrock", dmg = shotPow(e,"jrock"), x = e.x, y = e.y-30, sx = e.x, sy = e.y-30,
          tx = clamp(player.x+rnd(-50,50), W.wall+40, W.w-W.wall-40),
          ty = clamp(player.y+rnd(-50,50), W.wall+40, W.h-W.wall-40),
          t = 0, dur = 1.0, spin = rnd(TAU), delay = 0});
        SFX.grunt();
      }
    } else {
      if(e.atkCd<=0 && dP<540){ e.mode="wind"; e.modeT=0; }
    }
  }
  else if(e.type=="revenant"){
    if(e.mode=="windup"){
      e.modeT += dt;
      if(e.modeT>=0.8){
        e.mode="chase"; e.atkCd=5.2*e.atkS;
        addRing(e.x,e.y,26,320,REV_MAX,"#cfd6dd",9,0.5);
        addShake(0.4); SFX.bomb();
        spawnBricks(e.x,e.y+10,new[]{"#6d6e68","#9ba19d"},6,160);
        e.ring = new BossRing { r = 30, hit = false, max = REV_MAX, speed = 300 };
      }
    } else {
      vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed;
      if(dP<300 && e.atkCd<=0){ e.mode="windup"; e.modeT=0; SFX.grunt(); }
    }
  }
  else if(e.type=="knight"){
    var dd = angDiff(toP, e.faceA);
    e.faceA += clamp(dd, -2.2*dt, 2.2*dt);
    e.face = new Pt(Math.cos(e.faceA), Math.sin(e.faceA));
    /* it holds off behind the shield, and steps in to strike whenever its
       sword is ready -- a knight that only ever held off never hit anything */
    if(dP > 95 || (e.hitCd <= 0 && !e.docile)){ vx = Math.cos(toP)*e.speed; vy = Math.sin(toP)*e.speed; }
    else if(dP < 70 && !(e.strikeT > 0)){ vx = -Math.cos(toP)*e.speed*0.45; vy = -Math.sin(toP)*e.speed*0.45; }
  }
  else if(e.type=="boss"){ updateBoss(e, dt, toP, dP); return; }
  else {
    /* Anything without a kit of its own just runs you down. Mini golems used to
       land here and stand perfectly still, because there was no `else`. */
    var wob = Math.sin(vtime*5 + e.phase) * (!string.IsNullOrEmpty(e.mini) ? 0.5 : 0.25);
    var a = toP + wob*0.3;
    vx = Math.cos(a)*e.speed; vy = Math.sin(a)*e.speed;
  }
  /* A* takes over whenever a monster that is chasing the player can't see them.
     Committed attacks (lunges, charges, dashes) are never redirected. */
  var committed = (e.mode=="lunge" || e.mode=="charge" || e.mode=="dash");
  Pt navDir = null;
  if((Js.T(vx)||Js.T(vy)) && !e.docile && !committed){
    var sp = Math.hypot(vx,vy);
    var toward = sp>0 ? (vx*Math.cos(toP) + vy*Math.sin(toP))/sp : 0;
    if(toward > 0.3){
      var dir = navSteer(e, dt);
      /* Having a path used to reset the stuck timer every frame, which silenced
         the detector in exactly the case that needed it: a body following a path
         straight into a rock it cannot pass. The timer now belongs to whether it
         MOVED, and nothing else. */
      if(dir != null){ navDir = dir; vx = dir.x*sp; vy = dir.y*sp; }
    } else { e.path = null; }
  } else if(committed){ e.path = null; }
  if((Js.T(vx)||Js.T(vy)) && !e.docile){
    var slid = slideAroundSolids(e, vx, vy, dt);
    if(slid != null){ vx = slid.x; vy = slid.y; }
  }
  if((Js.T(vx)||Js.T(vy)) && !e.docile){
    var want = Math.hypot(vx,vy)*dt;
    var moved = dist(e, new Pt(e._lx, e._ly));
    /* CROWDING IS NOT STUCK. Sixty bodies packed around the hero barely move and
       that is correct — shoving them apart with an escape just makes the crowd
       wander. Only a body held up by GEOMETRY gets rescued, which is the case
       that could never fix itself. The geometry question is only ASKED of a body
       that has already stopped moving: asking it of all three hundred every
       frame was three hundred walks of the solid list, and it cost more than
       everything it was there to fix. */
    if(want > 0.4 && moved < want*0.35 && nearBlockers(e, e.r + 26).Count > 0) e.stuckT += dt;
    else e.stuckT = Math.max(0, e.stuckT - dt*2);
    if(e.stuckT > 0.35){
      e.stuckT = 0;
      /* whatever it was following is demonstrably not working */
      e.path = null; e.pathT = 0; e.slideT = 0; e.flowAt = null;
      navLearn(e);
      /* inside the padded band the grid has a definite answer; wedged between
         two things it has never heard of, the normals do */
      var esc = navEscape(e.x, e.y) ?? openWayOut(e);
      e.avoidA = esc != null ? Math.atan2(esc.y-e.y, esc.x-e.x)
                     : toP + (Js.R()<0.5?1:-1)*Math.PI/2.0;
      e.avoidT = 0.5;
    }
    e._lx = e.x; e._ly = e.y;
  }
  /* The escape LEANS the path rather than replacing it. A path is a better idea
     than a shove — in a dead-end pocket the path is the only way out and pushing
     away from the rocks just walks back into the middle of it — but a body that
     has stopped moving needs something added sideways to unwedge it. With no
     path at all, the shove is all there is. */
  if(e.avoidT > 0){
    e.avoidT -= dt;
    double ex = Math.cos(e.avoidA), ey = Math.sin(e.avoidA);
    if(navDir != null){
      double bx = navDir.x*0.62 + ex*0.38, by = navDir.y*0.62 + ey*0.38;
      var bl = Js.Or(Math.hypot(bx, by), 1);
      vx = bx/bl*e.speed*0.95; vy = by/bl*e.speed*0.95;
    } else {
      vx = ex*e.speed*0.9; vy = ey*e.speed*0.9;
    }
  }
  /* a chilled body crawls by how deep the chill is; a plain frost hazard with
     no stacks still uses the old flat slow */
  if(e.slowT>0){ var m = chillStacksOn(e) != 0 ? chillMulOn(e) : 0.45; vx*=m; vy*=m; }
  e.moving = (vx!=0||vy!=0);
  if(e.moving){ e.walk += dt*10; if(e.type!="knight") e.face=new Pt(vx, vy); }
  else if(!e.docile && e.type!="knight") e.face = new Pt(Math.cos(toP), Math.sin(toP));
  /* A STRIKE IS A THING YOU SEE. It stops, raises its weapon, and swings;
     what is still within reach when the swing comes down takes the hit. And it
     does not walk INTO what it is fighting -- it stops at the edge of it. */
  if(e.strikeT > 0){ vx *= 0.12; vy *= 0.12; e.face = new Pt(Math.cos(e.strikeA), Math.sin(e.strikeA)); }
  else if(dP < e.r + player.r + 4 && e.mode != "lunge"){
    double ux = Math.cos(toP), uy = Math.sin(toP), fwd = vx*ux + vy*uy;
    if(fwd > 0){ vx -= ux*fwd; vy -= uy*fwd; }
  }
  e.x += vx*dt; e.y += vy*dt;
  collideWorld(e);
  if(e.mode == "lunge"){
    /* a lunge IS the attack: the spear arriving is the hit */
    if(!e.docile && e.hitCd<=0 && dP < e.r + player.r + 4 && !player.dead){
      playerDamage(e.dmg+1, e, Js.Or(e.touchElem, null), false);
      e.hitCd = Js.Or(e.touchCd, 0.9);
    }
  } else if(e.strikeT > 0){
    e.strikeT -= dt;
    if(e.strikeT <= 0){
      e.strikeT = 0;
      var reach = e.r + player.r + STRIKE_REACH + 8;
      slashFx(e.x, e.y - 16*(Js.Or(e.scale, 1)), e.strikeA, 26*(Js.Or(e.scale, 1)), "#ffffff");
      if(!player.dead && dist(e, player) < reach && Math.abs(angDiff(angTo(e, player), e.strikeA)) < 1.4)
        playerDamage(e.dmg, e, Js.Or(e.touchElem, null), false);
      e.hitCd = Js.Or(e.touchCd, 0.9);
      SFX.hit();
    }
  } else if(!e.docile && e.hitCd<=0 && !player.dead && dP < e.r + player.r + STRIKE_REACH &&
            (e.mode == "chase" || e.mode == null)){
    e.strikeT = STRIKE_WIND * (Js.Or(e.atkS, 1)); e.strikeA = toP;
  }
  if(e.ring != null){
    var rmax = Js.Or(e.ring.max, REV_MAX);
    e.ring.r += (Js.Or(e.ring.speed, 300))*dt;
    /* the shockwave does not know whom it was aimed at: every skeleton on it */
    minionsOnRing(e.ring, e.x, e.y, !string.IsNullOrEmpty(e.ring.golem) ? 28 : 22, double.IsNaN(e.ring.dmg) ? (e.type=="jugg"?1:2) : e.ring.dmg,
      e.ring.golem=="ice" ? "frost" : e.ring.golem=="fire" ? "fire" : null);
    if(player.isProxy && !e.ring.heroHit && heroObj != null && !heroObj.dead &&
       Math.abs(dist(heroObj, e) - e.ring.r) < (!string.IsNullOrEmpty(e.ring.golem)?28:22)){
      /* ...and the hero standing in it, even while it was swinging at a skeleton */
      e.ring.heroHit = e.ring.hit = true;
      var keep = player; player = heroObj;
      if(heroObj.dash.t <= 0 && heroObj.invuln <= 0) playerDamage(double.IsNaN(e.ring.dmg) ? (e.type=="jugg"?1:2) : e.ring.dmg, e);
      player = keep;
    }
    if(!e.ring.hit && !player.dead && !player.isProxy){
      /* a true circle, drawn and measured — see bossRingHit (a skeleton it
         was after is on minionsOnRing already -- once, not twice) */
      var d = dist(player, e);
      if(Math.abs(d - e.ring.r) < (!string.IsNullOrEmpty(e.ring.golem)?28:22)){
        if(player.dash.t>0){ e.ring.hit = true; perfectDodge(player.x, player.y); }
        else if(player.invuln<=0){
          playerDamage(double.IsNaN(e.ring.dmg) ? (e.type=="jugg"?1:2) : e.ring.dmg, e,
            e.ring.golem=="ice" ? "frost" : e.ring.golem=="fire" ? "fire" : null);
          e.ring.hit = true;
        }
      }
    }
    if(e.ring.r > rmax) e.ring = null;
  }
}
/* Every boss shockwave goes through here so both halves of a double slam announce
   themselves. `n` is 1 for the slam, 2 for the aftershock that follows it. */
/* The slam now opens with a vortex that hauls the hero in, then lands ONE
   shockwave. Two overlapping waves read as a glitch; a pull you can see coming
   followed by a single hit reads as a mechanic. */
}

/* fields only a monster's own frame reads and writes. _spx/_spy are where it stood
   last frame (undefined until the first: NaN); vx/vy its last chosen walk */
/* a bat's lunge count is `chain` in the JS; the hero's chain spell has that name here */
public sealed partial class Ent
{
    public double batChain;
    public double _spx = double.NaN, _spy = double.NaN, breathA, breathTick, buffDmg, lockA, vx, vy;
    public string orbKind;
}
