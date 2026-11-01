// lego-survival.html, JS 25143-25662: one frame of a boss (updateBoss) — its tiers,
// enrage, and the rotation of attacks it picks from.
namespace Brickblade;

public static partial class G
{
public static void updateBoss(Ent b, double dt, double toP, double dP){
  if(b.slamHold > 0) b.slamHold -= dt;
  /* THORNED: standing on it costs you. VENOMOUS: its touch is poison you carry
     for the rest of the fight. Both read off the trait roll made when it woke. */
  if(b.thorns && !player.dead && player.dash.t<=0 && player.invuln<=0 &&
     dP < b.r + player.r + 30){
    playerDamage(Math.max(1, Math.round(b.dmg*0.25)), b);
    addSparks(player.x, player.y-18, 3, "#ffb066");
  }
  if(b.thorns){
    minionsIn(b.x, b.y, b.r + 30, m=>{ if(m.thornCd <= vtime){ m.thornCd = vtime + 0.6; minionHurt(m, Math.max(1, Math.round(b.dmg*0.25)), null, b); } });
  }
  if(b.super){ updateSuperBoss(b, dt, toP, dP); return; }
  var enr = b.hp < b.maxHp*0.5;
  var spd = b.speed*(enr?1.45:1);
  var tier = Math.max(1, Math.floor(wave.n/5.0));
  if(b.bkey=="brickthane"){
    if(b.mode=="chase"){
      bossChase(b, toP, spd, dt);
      b.moving = true; b.walk += dt*8;
      b.nextAtk -= dt;
      if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
      if(b.nextAtk<=0){
        /* 5-mechanic cycle: summon, dash, slam, brick nova, MACE SPIN */
        b.cycle = (b.cycle+1)%5;
        if(b.cycle==0 && !b.sum1 && b.hp<b.maxHp*0.7){ b.sum1=true; b.mode="summonWind"; }
        else if(b.cycle==1 && dP>170 && dP<560){ b.mode="dashWind"; b.lockA=toP; }
        else if(b.cycle==2 && dP<=190){ b.mode="slamWind"; }
        else if(b.cycle==3){ b.mode="novaWind"; }
        else if(b.cycle==4){ b.mode="maceWind"; b.maceCast=0; }
        else { b.mode="volleyWind"; }
        b.modeT=0;
        if(b.mode!="chase"){ if(b.mode=="summonWind"||b.mode=="novaWind") SFX.roar(); else SFX.grunt(); }
      }
    }
    else if(b.mode=="dashWind"){
      b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(b.lockA), Math.sin(b.lockA));
      if(b.modeT>=0.6){ b.mode="dash"; b.modeT=0; SFX.roar(); }
    }
	else if(b.mode=="slamWind"){
		b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
		if(b.modeT < BOSS_PULL_T) bossPull(b, dt);
		else if(b.modeT>=BOSS_PULL_T+0.5){
		  b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.5:2.2;
		  spawnBossRing(b);
		  /* NEW: radial arrow burst on slam */
		  var na = 8;
		  for(var i=0;i<na;i++){
			var a2 = (double)i/na*TAU;
			projectiles.Add(new Proj {kind = "arrow", x = b.x, y = b.y-14,
			  vx = Math.cos(a2)*270, vy = Math.sin(a2)*270,
			  owner = "enemy", dmg = shotPow(b,"arrow"), life = 3});
		    }
        }
    }
    else if(b.mode=="dash"){
      b.modeT += dt;
      b.x += Math.cos(b.lockA)*760*dt; b.y += Math.sin(b.lockA)*760*dt;
      if(Js.R()<0.7) addDust(b.x,b.y,1);
      if(dP < b.r+player.r+6 && b.hitCd<=0){ playerDamage(b.dmg+1,b); b.hitCd=0.8; }
      if(b.modeT>=0.38){ b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.5:2.2; }
    }
    /* NEW mechanic: brick nova — ring of vbricks bursting outward */
    else if(b.mode=="maceWind"){
      if(maceSpin(b, dt, tier, enr, toP)){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
      }
    }
    else if(b.mode=="novaWind"){
      b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
      if(b.modeT>=0.7){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.5;
        var n = 12;
        for(var i=0;i<n;i++){
          var a2 = (double)i/n*TAU;
          projectiles.Add(new Proj {kind = "vbrick", dmg = shotPow(b,"vbrick"), sx = b.x, sy = b.y-30,
            tx = clamp(b.x+Math.cos(a2)*300, 120, W.w-120),
            ty = clamp(b.y+Math.sin(a2)*300, 120, W.h-120),
            t = 0, dur = 0.75, spin = rnd(TAU), delay = i*0.05});
        }
        addRing(b.x,b.y,20,420,180,"#ff9a3c",8,0.45);
        SFX.bomb();
      }
    }
    else if(b.mode=="volleyWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.6){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
        var n = 4 + tier;
        var lead = leadPlayer(b.x, b.y, 1.0, 0.9);
        var pts = bombSpread(lead.x, lead.y, n, 210, 80);
        for(var i=0;i<n;i++){
          projectiles.Add(new Proj {kind = "vbrick", dmg = shotPow(b,"vbrick"), sx = b.x, sy = b.y-30,
            tx = clamp(pts[i].x, 120, W.w-120),
            ty = clamp(pts[i].y, 120, W.h-120),
            t = 0, dur = rnd(0.75,0.95), spin = rnd(TAU), delay = i*0.1});
        }
        SFX.grunt();
      }
    }
    else if(b.mode=="summonWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.7){
        b.mode="recover"; b.modeT=0; b.nextAtk = 2.4;
        bossSummon(b, 8 + tier*2, ["imp","archer","boneling"]);
      }
    }
    else if(b.mode=="recover"){
      b.modeT += dt;
      if(b.modeT>0.45){ b.mode="chase"; }
    }
  }
  else if(b.bkey=="bonebaron"){
    if(b.mode=="chase"){
      bossChase(b, toP, spd, dt);
      b.moving = true; b.walk += dt*9;
      b.nextAtk -= dt;
      if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
      if(b.nextAtk<=0){
        /* 5-cycle: the compass barrage, the sweeping spiral, its court, the
           homing curse, and the hex that leaves you weaker for the next one */
        b.cycle = (b.cycle+1)%5;
        if(b.cycle==0){ b.mode="ringWind"; SFX.grunt(); }
        else if(b.cycle==1){ b.mode="spiralWind"; SFX.grunt();
          showBanner("THE BONE SPIRAL","", "#c9b8e8", 1.0, 30); }
        else if(b.cycle==2){ b.mode="summonWind"; SFX.roar(); }
        else if(b.cycle==3){ b.mode="curseWind"; SFX.grunt(); }
        else { b.mode="hexWind"; SFX.grunt(); }
        b.modeT=0;
      }
    }
    /* THE BARRAGE. One compass of skulls was a single thing to walk out of.
       It now fires five to ten of them in a row, half a second apart, each
       compass rotated a little from the last so the gaps keep moving. */
    else if(b.mode=="ringWind"){
      b.modeT += dt; b.moving=false;
      if(double.IsNaN(b.volleys)){
        b.volleys = SKULL_VOLLEYS[0] + Math.min(SKULL_VOLLEYS[1]-SKULL_VOLLEYS[0], tier) + (enr?2:0);
        b.volleyT = 0.55;                 /* a beat to wind up before the first */
        b.volleySpin = rnd(TAU);
      }
      b.volleyT -= dt;
      if(b.volleyT <= 0 && b.volleys > 0){
        b.volleys--;
        b.volleyT = SKULL_VOLLEY_GAP;
        b.volleySpin += 0.31;             /* twist each ring off the last one */
        var n = (enr?14:10) + Math.min(4,tier);
        for(var i=0;i<n;i++){
          var a2 = b.volleySpin + i/n*TAU + rnd(-0.05,0.05);
          projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-20,
            vx = Math.cos(a2)*235, vy = Math.sin(a2)*235,
            owner = "enemy", dmg = shotPow(b,"skull"), life = 4.2, homing = false});
        }
        addRing(b.x,b.y,20,420,150,"#cfd6dd",7,0.45);
        SFX.bomb();
      }
      if(b.volleys <= 0 && b.volleyT <= 0){
        b.volleys = double.NaN;
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.2:3.2;
      }
    }
    /* THE SPIRAL. An arm of skulls thrown out clockwise, and then another, and
       another — five sweeps in a row with a beat between them, each starting
       where the last one ended. You cannot stand still through it. */
    else if(b.mode=="spiralWind"){
      b.modeT += dt; b.moving=false;
      if(double.IsNaN(b.arms)){
        b.arms = SPIRAL_ARMS + (enr?2:0);
        b.armT = 0.5;
        b.armA = rnd(TAU);
      }
      b.armT -= dt;
      if(b.armT <= 0 && b.arms > 0){
        b.arms--;
        b.armT = SPIRAL_GAP;
        var n = 9 + Math.min(5, tier);
        for(var i=0;i<n;i++){
          /* every skull in the arm leaves a little later along the sweep, so the
             whole thing reads as one arm turning rather than a line of shots */
          var a2 = b.armA + i*SPIRAL_STEP;
          projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-20,
            vx = Math.cos(a2)*250, vy = Math.sin(a2)*250,
            owner = "enemy", dmg = shotPow(b,"skull"), life = 4.5, homing = false});
        }
        b.armA += n*SPIRAL_STEP + 0.55;     /* the next arm picks up where this ended */
        addRing(b.x,b.y,16,360,120,"#c9b8e8",6,0.4);
        SFX.bomb();
      }
      if(b.arms <= 0 && b.armT <= 0){
        b.arms = double.NaN;
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.9:2.8;
      }
    }
    /* THE HEX. It does not only throw bones — it takes something off you first:
       a curse, and a brand of the cold that lives in its crypt. */
    else if(b.mode=="hexWind"){
      b.modeT += dt; b.moving=false;
      if(Js.R()<0.5)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-34,34), y = b.y-rnd(20,80),
          vx = rnd(-24,24), vy = rnd(-30,10), life = 0.4, max = 0.4, color = "#c9b8e8"});
      if(b.modeT>=0.7){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.5;
        /* a fan of hex bolts, each carrying its own curse */
        var n = 5 + Math.min(3, tier);
        for(var i=0;i<n;i++){
          var a2 = toP + (i-(n-1)/2.0)*0.26;
          projectiles.Add(new Proj {kind = "hexbolt", x = b.x, y = b.y-20,
            vx = Math.cos(a2)*240, vy = Math.sin(a2)*240,
            owner = "enemy", dmg = shotPow(b,"hexbolt"), life = 4, curse = pick(CURSE_KEYS), src = b, seed = rnd(TAU)});
        }
        /* and one curse that simply lands, so the mechanic always does something */
        applyCurse(pick(CURSE_KEYS), b);
        addDmg(b.x, b.y-86*(Js.Or(b.scale, 1)), "THE BARON HEXES YOU", "#c9b8e8", 20);
        SFX.zap();
      }
    }
    /* NEW mechanic: homing skull curse */
    else if(b.mode=="curseWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.65){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.4;
        foreach (var off in new double[]{-0.4, 0, 0.4}){
          var a2 = toP+off;
          projectiles.Add(new Proj {kind = "skull", x = b.x, y = b.y-20,
            vx = Math.cos(a2)*190, vy = Math.sin(a2)*190,
            owner = "enemy", dmg = shotPow(b,"skull"), life = 4.5, homing = true, turn = 2.0});
        }
        SFX.grunt();
      }
    }
    else if(b.mode=="summonWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.75){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
        /* a court, not a handful: a rank of archers in front, its own
           necromancers behind them, and bonelings to fill the gaps */
        bossSummon(b, 8 + tier*3, ["boneling","archer","archer","revenant"]);
        bossSummon(b, 4 + tier*2, ["archer","marksman"]);
        bossSummon(b, 1 + Math.min(4, Math.floor(tier/2.0)+1), ["necromancer"]);
        showBanner("THE COURT RISES","", "#c9b8e8", 1.0, 30);
      }
    }
    else if(b.mode=="recover"){
      b.modeT += dt;
      if(b.modeT>0.45){ b.mode="chase"; }
    }
  }
  else if(b.bkey=="batsovereign"){
    /* it hovers rather than walks, so the bob is part of the silhouette */
    b.hover = b.hover + dt;
    if(b.mode=="chase"){
      bossChase(b, toP, spd, dt, 1.15);
      b.moving = true; b.walk += dt*12;
      b.nextAtk -= dt;
      if(Js.R()<0.3)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-26,26), y = b.y-rnd(20,70),
          vx = rnd(-16,16), vy = rnd(-12,12), life = 0.4, max = 0.4, color = "#b58fd6"});
      if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
      if(b.nextAtk<=0){
        /* 4-cycle: the roost, a salvo of bat missiles, a fan of hexes, then a
           string of charges back and forth across the arena */
        b.cycle = (b.cycle+1)%4;
        if(b.cycle==0){ b.mode="roostWind"; SFX.roar(); }
        else if(b.cycle==1){ b.mode="batsWind"; SFX.grunt(); }
        else if(b.cycle==2){ b.mode="hexWind"; SFX.grunt(); }
        else { b.mode="swoopWind"; b.swoops = 3 + Math.min(3, tier);
               showBanner("THE ROOST DIVES!","", "#b58fd6", 1.0, 32); SFX.roar(); }
        b.modeT = 0;
      }
    }
    else if(b.mode=="roostWind"){
      b.modeT += dt; b.moving=false;
      if(Js.R()<0.6)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-40,40), y = b.y-rnd(30,90),
          vx = rnd(-40,40), vy = rnd(-30,10), life = 0.4, max = 0.4, color = "#c9b8e8"});
      if(b.modeT>=0.8){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.5;
        /* both kinds of bat, and more of the small ones */
        double small = 6 + tier*2, big = 1 + Math.min(3, Math.floor(tier/2.0));
        bossSummon(b, small, ["cryptbat"]);
        bossSummon(b, big, ["direbat"]);
      }
    }
    else if(b.mode=="batsWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.55){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.5:2.2;
        /* bat-shaped missiles: they turn after you, so you have to break away */
        var n = 5 + tier;
        for(var i=0;i<n;i++){
          var a2 = toP + (i-(n-1)/2.0)*0.22;
          projectiles.Add(new Proj {kind = "batbolt", x = b.x, y = b.y-24,
            vx = Math.cos(a2)*250, vy = Math.sin(a2)*250,
            owner = "enemy", dmg = shotPow(b,"batbolt"), life = 5, homing = true, turn = 1.9,
            flap = rnd(TAU), seed = rnd(TAU)});
        }
        SFX.zap();
      }
    }
    else if(b.mode=="hexWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.6){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.6:2.4;
        foreach (var off in new double[]{-0.5,-0.25,0,0.25,0.5}){
          var a2 = toP+off;
          projectiles.Add(new Proj {kind = "hexbolt", x = b.x, y = b.y-22,
            vx = Math.cos(a2)*265, vy = Math.sin(a2)*265,
            owner = "enemy", dmg = shotPow(b,"hexbolt"), life = 3.6, curse = pick(CURSE_KEYS), src = b, seed = rnd(TAU)});
        }
        SFX.grunt();
      }
    }
    /* THE DIVE. It picks a line through the hero, throws itself down it at full
       tilt, overshoots, turns and does it again — the arena stops being a place
       you can stand still in. */
    else if(b.mode=="swoopWind"){
      b.modeT += dt; b.moving=false;
      b.face = new Pt(Math.cos(toP), Math.sin(toP));
      if(b.modeT>=0.45){ b.mode="swoop"; b.modeT=0; b.lockA=toP;
        b.lx=Math.cos(toP); b.ly=Math.sin(toP); SFX.whoosh(); }
    }
    else if(b.mode=="swoop"){
      b.modeT += dt;
      b.moving = true; b.walk += dt*20;
      var sp2 = 720 + tier*30;
      b.x += b.lx*sp2*dt; b.y += b.ly*sp2*dt;
      collideWorld(b);
      if(Js.R()<0.9)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-20,20), y = b.y-rnd(10,60),
          vx = -b.lx*140, vy = -b.ly*140, life = 0.3, max = 0.3, color = "#b58fd6"});
      if(dP < b.r+player.r+10 && b.hitCd<=0){ playerDamage(b.dmg+1,b); b.hitCd=0.5; }
      var outOfRoom = b.x<W.wall+90 || b.x>W.w-W.wall-90 || b.y<W.wall+90 || b.y>W.h-W.wall-90;
      if(b.modeT>=0.62 || outOfRoom){
        b.swoops--;
        if(b.swoops>0){ b.mode="swoopWind"; b.modeT=0; }
        else { b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6; }
      }
    }
    else if(b.mode=="recover"){
      b.modeT += dt;
      if(b.modeT>0.4){ b.mode="chase"; }
    }
  }
  else if(b.bkey=="lavabrick"){
    if(b.mode=="chase"){
      bossChase(b, toP, spd, dt, 0.9);
      b.moving = true; b.walk += dt*7;
      b.nextAtk -= dt;
      if(Js.R()<0.25)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-18,18), y = b.y-rnd(10,50), vx = 0, vy = -60,
          life = 0.5, max = 0.5, color = "#ff9a3c"});
      if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
      if(b.nextAtk<=0){
        /* 6-cycle: pools, slam, magma fan, volley, HELLFIRE, MACE SPIN */
        b.cycle = (b.cycle+1)%6;
        if(b.cycle==0){ b.mode="poolsWind"; SFX.grunt(); }
        else if(b.cycle==1){ b.mode="slamWind"; SFX.grunt(); }
        else if(b.cycle==2){ b.mode="fanWind"; SFX.fireball(); }
        else if(b.cycle==3){ b.mode="volleyWind"; SFX.grunt(); }
        else if(b.cycle==4){ b.mode="maceWind"; b.maceCast=0; SFX.grunt(); }
        else { b.mode="hellWind"; SFX.roar(); showBanner("HELLFIRE!","", "#ff6a3a", 1.1, 34); }
        b.modeT=0;
      }
    }
    else if(b.mode=="poolsWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.7){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.9:2.8;
        var n = 3 + Math.min(3, tier) + (enr?2:0);
        for(var i=0;i<n;i++){
          var dur = rnd(0.8,1.05);
          var lead = leadPlayer(b.x, b.y, dur + i*0.1, 0.8);
          projectiles.Add(new Proj {kind = "molten", sx = b.x, sy = b.y-30,
            tx = clamp(lead.x+rnd(-120,120), W.wall+60, W.w-W.wall-60),
            ty = clamp(lead.y+rnd(-120,120), W.wall+60, W.h-W.wall-60),
            t = 0, dur = dur, spin = rnd(TAU), delay = i*0.1, fr = fireRadius(b, 44)});
        }
      }
    }
    else if(b.mode=="maceWind"){
      if(maceSpin(b, dt, tier, enr, toP)){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
      }
    }
    else if(b.mode=="hellWind"){
      if(bossHellfire(b, dt, tier, enr)){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.1:3.0;
      }
    }
    /* NEW mechanic: magma fan — 3 molten bricks in a spread */
    else if(b.mode=="fanWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.6){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.5;
        var baseA = Math.atan2(player.y-b.y, player.x-b.x);
        foreach (var off in new double[]{-0.35, 0, 0.35}){
          projectiles.Add(new Proj {kind = "molten", sx = b.x, sy = b.y-30,
            tx = clamp(b.x+Math.cos(baseA+off)*320, W.wall+60, W.w-W.wall-60),
            ty = clamp(b.y+Math.sin(baseA+off)*320, W.wall+60, W.h-W.wall-60),
            t = 0, dur = 0.85, spin = rnd(TAU), delay = 0, fr = fireRadius(b, 44)});
        }
      }
    }
    else if(b.mode=="slamWind"){
      b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
      if(b.modeT < BOSS_PULL_T) bossPull(b, dt);
      else if(b.modeT>=BOSS_PULL_T+0.45){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.4;
        spawnBossRing(b);
      }
    }
    else if(b.mode=="volleyWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.6){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
        var n = 4 + tier;
        var lead = leadPlayer(b.x, b.y, 1.0, 0.9);
        var pts = bombSpread(lead.x, lead.y, n, 210, 80);
        for(var i=0;i<n;i++){
          projectiles.Add(new Proj {kind = "vbrick", dmg = shotPow(b,"vbrick"), sx = b.x, sy = b.y-30,
            tx = clamp(pts[i].x, 120, W.w-120),
            ty = clamp(pts[i].y, 120, W.h-120),
            t = 0, dur = rnd(0.75,0.95), spin = rnd(TAU), delay = i*0.1});
        }
        SFX.grunt();
      }
    }
    else if(b.mode=="recover"){
      b.modeT += dt;
      if(b.modeT>0.45){ b.mode="chase"; }
    }
  }
  else {
    if(b.mode=="chase"){
      bossChase(b, toP, spd, dt, 0.85);
      b.moving = true; b.walk += dt*6;
      b.nextAtk -= dt;
      if(Js.R()<0.3)
        particles.Add(new Particle {kind = "spark", x = b.x+rnd(-22,22), y = b.y-rnd(10,54), vx = rnd(-10,10), vy = -70,
          life = 0.6, max = 0.6, color = "#ff8a5a"});
      if(dP < b.r+player.r+4 && b.hitCd<=0){ playerDamage(b.dmg,b); b.hitCd=0.9; }
      if(b.nextAtk<=0){
        /* 5-cycle: rain, slam, ember ring, volley, MACE SPIN */
        b.cycle = (b.cycle+1)%5;
        if(b.cycle==0){ b.mode="rainWind"; SFX.roar(); showBanner("HELLFIRE!","FIRE FROM THE SKY", "#ff6a3a", 1.2, 34); }
        else if(b.cycle==1){ b.mode="slamWind"; SFX.grunt(); }
        else if(b.cycle==2){ b.mode="emberWind"; SFX.fireball(); }
        else if(b.cycle==3){ b.mode="maceWind"; b.maceCast=0; SFX.grunt(); }
        else { b.mode="volleyWind"; SFX.grunt(); }
        b.modeT=0;
      }
    }
    else if(b.mode=="rainWind"){
      if(bossHellfire(b, dt, tier, enr)){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?2.0:2.9;
      }
    }
    else if(b.mode=="maceWind"){
      if(maceSpin(b, dt, tier, enr, toP)){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
      }
    }
    /* NEW mechanic: ember ring — fireballs all around him */
    else if(b.mode=="emberWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.7){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
        var n = 10;
        for(var i=0;i<n;i++){
          var a2 = (double)i/n*TAU;
          projectiles.Add(new Proj {kind = "fireball", x = b.x, y = b.y-30, sx = b.x, sy = b.y-30,
            tx = clamp(b.x+Math.cos(a2)*280, W.wall+40, W.w-W.wall-40),
            ty = clamp(b.y+Math.sin(a2)*280, W.wall+40, W.h-W.wall-40),
            t = 0, dur = 0.8, spin = rnd(TAU), delay = i*0.06, fr = fireRadius(b, 31)});
        }
      }
    }
    else if(b.mode=="slamWind"){
      b.modeT += dt; b.moving=false; b.face=new Pt(Math.cos(toP), Math.sin(toP));
      if(b.modeT < BOSS_PULL_T) bossPull(b, dt);
      else if(b.modeT>=BOSS_PULL_T+0.45){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.7:2.4;
        spawnBossRing(b);
      }
    }
    else if(b.mode=="volleyWind"){
      b.modeT += dt; b.moving=false;
      if(b.modeT>=0.6){
        b.mode="recover"; b.modeT=0; b.nextAtk = enr?1.8:2.6;
        var n = 4 + tier;
        var lead = leadPlayer(b.x, b.y, 1.0, 0.9);
        var pts = bombSpread(lead.x, lead.y, n, 210, 80);
        for(var i=0;i<n;i++){
          projectiles.Add(new Proj {kind = "vbrick", dmg = shotPow(b,"vbrick"), sx = b.x, sy = b.y-30,
            tx = clamp(pts[i].x, 120, W.w-120),
            ty = clamp(pts[i].y, 120, W.h-120),
            t = 0, dur = rnd(0.75,0.95), spin = rnd(TAU), delay = i*0.1});
        }
        SFX.grunt();
      }
    }
    else if(b.mode=="recover"){
      b.modeT += dt;
      if(b.modeT>0.45){ b.mode="chase"; }
    }
  }
  collideWorld(b);
  if(b.ring != null){
    b.ring.r += 380*dt;
    bossRingHit(b);
    if(b.ring.r > RING_MAX) b.ring = null;
  }
}
/* NEW: SUPER BOSSES — bigger, meaner, signature mechanic */
/* THE QUAKE. The Omega heaves its mace overhead and drives it into the floor.
   Three shockwaves roll out of the impact, each one further than the last, and
   riding each wave is an arc of STONE SPIKES that tear up out of the ground in
   a 180-degree fan pointed at the hero — the Path of Exile slam. The spikes are
   what actually hits you; the rings are the tell that tells you where they are
   about to come up. */
}

/* a boss's attack clocks; volleys and arms are undefined between barrages (NaN) */
public sealed partial class Ent
{
    public double armA, armT, arms = double.NaN, hover, slamHold, swoops, thornCd, volleySpin, volleyT, volleys = double.NaN;
    public bool sum1;
}
