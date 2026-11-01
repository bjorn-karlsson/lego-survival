// lego-survival.html, JS 25967-26617: the axe that reaves, the boomerang blade's legs,
// and one frame of every flying thing (updateProjectiles).
namespace Brickblade;

public static partial class G
{
public static Ent reaveTarget(double x, double y, HashSet<object> seen = null){
  Ent best = null; double bd = REAVE_SEEK;
  foreach (var e in enemiesNear(x, y, REAVE_SEEK).Live()){
    if(e.dead || e.state != "active" || (seen != null && seen.Contains(e))) continue;
    var d = dist(new Pt(x, y), e);
    if(d < bd){ bd = d; best = e; }
  }
  return best;
}
public static Proj throwReave(Ent from, double dmg){
  var p = player;
  if(p == null || p.reaveCd > 0 || !(dmg > 0)) return null;
  /* the ceiling, not your hands. Past it a crit simply does not throw -- it is
     still a critical and still hits like one. */
  if(reaveAir() >= reaveAirMax()) return null;
  var seen = new HashSet<object> { from };
  var tgt = reaveTarget(from.x, from.y, seen);
  p.reaveCd = REAVE_CD;
  SFX.whoosh();
  if(tgt == null){
    /* A CRIT ALWAYS THROWS, however few are left. With nobody else in reach the
       axe leaves YOUR hand for the body you just crit and reaps it again -- and
       it carries a turning point past it, so a body your crit has already
       finished still sees the axe fly its arc and come home. */
    var a = Math.atan2(from.y - p.y, from.x - p.x);
    var pr0 = new Proj { kind = "reave", x = p.x, y = p.y - 14, px = p.x, py = p.y - 14,
                 dmg = dmg, legs = reaveLegs(), tgt = from, hit = new HashSet<object>(), spin = rnd(TAU),
                 life = REAVE_LIFE, back = false, solo = true,
                 wx = from.x + Math.cos(a)*REAVE_SOLO_PAST, wy = from.y - 16 + Math.sin(a)*REAVE_SOLO_PAST };
    projectiles.Add(pr0);
    return pr0;
  }
  var pr = new Proj { kind = "reave", x = from.x, y = from.y - 14, px = from.x, py = from.y - 14,
               dmg = dmg, legs = reaveLegs(), tgt = tgt, hit = seen, spin = rnd(TAU),
               life = REAVE_LIFE, back = false };
  projectiles.Add(pr);
  return pr;
}
public static void wbladeLeg(Proj pr, bool caught){
  if(pr.legs <= 0){ pr.dead = true; return; }
  pr.legs--;
  var sp = Js.Or(Math.hypot(pr.vx, pr.vy), 255);
  pr.hit = new HashSet<object>();                       /* a fresh pass cuts the same foes again */
  if(caught){
    pr.phase = "out"; pr.life = pr.olife;
    var a = Math.atan2(pr.vy, pr.vx) + rnd(-0.45, 0.45);
    pr.vx = Math.cos(a)*sp; pr.vy = Math.sin(a)*sp;
    addSparks(pr.x, pr.y, 4, "#e8eef2"); SFX.whoosh();
  } else {
    /* the return leg gets a long fuse — the hero is a moving target */
    pr.phase = "back"; pr.life = pr.olife*2.4 + 0.6;
    var a = Math.atan2((player.y-12)-pr.y, player.x-pr.x);
    pr.vx = Math.cos(a)*sp; pr.vy = Math.sin(a)*sp;
  }
}
public static void updateProjectiles(double dt){
  foreach (var pr in projectiles.Live()){
    if(double.IsNaN(pr.x) && !double.IsNaN(pr.sx)){ pr.x = pr.sx; pr.y = pr.sy; }
    pr.px = pr.x; pr.py = pr.y;
    if(pr.kind=="arrow" || pr.kind=="skull" || pr.kind=="frostbolt" || pr.kind=="bolt"
       || pr.kind=="hexbolt" || pr.kind=="frostrock" || pr.kind=="batbolt"){
      if(pr.delay > 0){ pr.delay -= dt; continue; }
      if((pr.kind=="skull"||pr.kind=="batbolt") && pr.homing && !pr.deflected){
        var ta = Math.atan2(player.y-pr.y, player.x-pr.x);
        var ca = Math.atan2(pr.vy, pr.vx);
        var na = ca + clamp(angDiff(ta, ca), -pr.turn*dt, pr.turn*dt);
        var sp = Math.hypot(pr.vx,pr.vy);
        pr.vx = Math.cos(na)*sp; pr.vy = Math.sin(na)*sp;
      }
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt;
      if(pr.kind=="bolt" && Js.R()<(pr.big?0.7:0.5))
        particles.Add(new Particle {kind = "spark", x = pr.x, y = pr.y, vx = rnd(-30,30), vy = rnd(-30,30),
          life = 0.15, max = 0.15, color = "#ffe95a"});
      if(pr.kind=="batbolt"){
        pr.flap += dt*22;
        if(Js.R()<0.4)
          particles.Add(new Particle {kind = "spark", x = pr.x, y = pr.y, vx = rnd(-24,24), vy = rnd(-24,24),
            life = 0.2, max = 0.2, color = "#b58fd6"});
      }
      if(pr.kind=="frostrock"){
        pr.spin += dt*7;
        /* it lays a freezing scar down the whole lane it crossed */
        pr.trailD += Math.hypot(pr.x-pr.px, pr.y-pr.py);
        if(pr.trailD >= 72){
          pr.trailD = 0;
          /* a pack of frost golems could otherwise carpet the whole floor */
          var frostPatches = 0;
          foreach (var h in hazards.Live()) if(h.kind=="frost") frostPatches++;
          if(frostPatches < FROST_TRAIL_CAP){
            var lf = pr.trailLife;
            hazards.Add(new Hazard {kind = "frost", x = pr.x, y = pr.y, r = pr.tr, life = lf, max = lf});
          }
        }
        if(Js.R()<0.8)
          particles.Add(new Particle {kind = "spark", x = pr.x+rnd(-10,10), y = pr.y+rnd(-8,8),
            vx = rnd(-40,40), vy = rnd(-50,-5), life = 0.4, max = 0.4, color = FX.foe.frost});
      }
      if(pr.kind=="frostbolt" && Js.R()<0.4)
        particles.Add(new Particle {kind = "spark", x = pr.x, y = pr.y, vx = rnd(-20,20), vy = rnd(-20,20),
          life = 0.25, max = 0.25, color = FX.foe.frostDeep});
      if(pr.kind=="hexbolt" && Js.R()<0.6)
        particles.Add(new Particle {kind = "spark", x = pr.x, y = pr.y, vx = rnd(-25,25), vy = rnd(-25,25),
          life = 0.3, max = 0.3, color = CURSES[pr.curse].col});
      if(pr.life<=0) pr.dead=true;
      foreach (var s in solids.Live()){
        /* a thrown boulder shoulders straight through saplings and crates; only
           something its own size stops it */
        if(pr.kind=="frostrock" && s.r < 20) continue;
        if(dist(pr,s) < s.r){ pr.dead=true; addDust(pr.x,pr.y,2); break; }
      }
      /* THE AEGIS, before the hero: a whole brick the shot's path crosses stops
         it, and breaks. Swept like the hero's own check, for the same reason --
         a fast boulder would otherwise step clean over a brick between frames. */
      if(pr.owner=="enemy" && !pr.dead && !player.dead && aegisBlock(pr)) continue;
      /* A SKELETON IN THE WAY IS HIT. Whatever a monster throws lands on the
         first of your dead it crosses -- the legion is you, as far as a missile
         can tell. A curse has nothing to hold on to in bones and simply breaks;
         a thrown boulder shoulders on through. */
      if(pr.owner=="enemy" && !pr.dead && minions.Count > 0){
        foreach (var m in minions.Live()){
          if(m.rise < 1 || m.gone) continue;
          if(!sweepsBody(m, pr.px, pr.py, pr.x, pr.y, !double.IsNaN(pr.hitR) ? pr.hitR : 7+(pr.big?5:0))) continue;
          if(pr.kind == "frostrock"){ if(!(pr.mHit != null && pr.mHit.Contains(m))){ (pr.mHit ??= new HashSet<Ent>()).Add(m); minionHurt(m, Js.Or(pr.dmg, 1), "frost", pr); } continue; }
          if(pr.kind != "hexbolt") minionHurt(m, Js.Or(pr.dmg, 1), pr.kind=="frostbolt" ? "frost" : pr.kind=="bolt" ? "shock" : null, pr);
          addSparks(pr.x, pr.y, 3, "#c9b8ff");
          pr.dead = true; break;
        }
        if(pr.dead) continue;
      }
      /* swept, not sampled: a 660px/s boulder covers 11px a frame and used to be
         able to step straight over the hero between two frames */
      if(pr.owner=="enemy" && !player.dead &&
         sweepsBody(player, pr.px, pr.py, pr.x, pr.y,
           (!double.IsNaN(pr.hitR) ? pr.hitR : 7+(pr.big?5:0)))){
        if(player.dash.t>0){
          if(!pr.dodged){ pr.dodged=true; perfectDodge(pr.x, pr.y); pr.dead=true; }
        }
        else if(pr.kind=="hexbolt"){
          /* curses weaken you rather than hurt you, so i-frames don't stop them —
             only rolling through the bolt does */
          applyCurse(pr.curse, pr.src);
          addSparks(player.x, player.y-20, 8, CURSES[pr.curse].col);
          SFX.deflect();
          pr.dead = true;
        }
        else if(player.invuln<=0){
          playerDamage(Js.Or(pr.dmg, 1), pr,
            (pr.kind=="frostbolt"||pr.kind=="frostrock") ? "frost" : pr.kind=="bolt" ? "shock" : null,
            (pr.kind=="skull"||pr.kind=="batbolt") ? true : null);   /* skulls and bats are spells, arrows are not */
          pr.dead=true;
          if(pr.kind=="frostrock"){
            addRing(pr.x,pr.y,16,300,110,FX.foe.frost,7,0.45);
            spawnBricks(pr.x,pr.y, new[]{"#cfeaff","#7fb9e0","#4d86ad"},7,180);
            SFX.freeze();
          }
        }
      }
      if(pr.owner=="player"){
        foreach (var e in enemiesNear((pr.x+pr.px)/2.0, (pr.y+pr.py)/2.0,
              Math.hypot(pr.x-pr.px, pr.y-pr.py)/2.0 + 80).Live()){
          if(e.dead||e.state!="active") continue;
          if(sweepsBody(e, pr.px, pr.py, pr.x, pr.y, 10)){
            hitEnemy(e, calcDamage(2,"melee"), Math.atan2(pr.vy,pr.vx),180,"arrow",new HitOpts {spin = true});
            pr.dead=true; break;
          }
        }
      }
    }
    else if(pr.kind=="vtotem"){
      pr.life -= dt;
      if(pr.life<=0){ pr.dead = true; addSparks(pr.x, pr.y-10, 8, "#cfd6dd"); }
      foreach (var e in enemiesNear(pr.x, pr.y, pr.r + 80).Live()){
        if(e.dead || e.state!="active" || e.totCd>0) continue;
        if(hitsBody(e, pr.x, pr.y, pr.r)){
          e.totCd = TOTEM_TICK;
          hitEnemy(e, pr.dmg, angTo(pr,e), 120, "vortex", new HitOpts {spin = true});
        }
      }
      if(Js.R()<0.6){
        var a2 = rnd(TAU);
        particles.Add(new Particle {kind = "spark", x = pr.x+Math.cos(a2)*pr.r*0.85,
          y = pr.y+Math.sin(a2)*pr.r*0.55, vx = rnd(-30,30), vy = rnd(-30,10),
          life = 0.22, max = 0.22, color = "#e8eef2"});
      }
    }
    else if(pr.kind=="zorb"){
      /* A slow ball of lightning with one arm of it whipping round clockwise.
         The arm is the threat while it travels; the ball itself, if it reaches
         you, dumps a fistful of shock at once. */
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt;
      pr.ang += ZORB_SPIN*dt;
      if(pr.life<=0) pr.dead = true;
      foreach (var so in solids.Live()) if(dist(pr,so) < so.r){ pr.dead = true; break; }
      if(pr.x < W.wall || pr.x > W.w-W.wall || pr.y < W.wall || pr.y > W.h-W.wall) pr.dead = true;
      pr.zapCd -= dt;
      if(pr.zapCd <= 0 && !pr.dead){
        pr.zapCd = ZORB_ZAP_GAP;
        double ex = pr.x + Math.cos(pr.ang)*ZORB_ARM, ey = pr.y + Math.sin(pr.ang)*ZORB_ARM*0.8;
        addZap(pr.x, pr.y, ex, ey, 1, true);
        for(var i=0;i<2;i++){
          var t = rnd(0.2,1);
          particles.Add(new Particle {kind = "spark", x = lerp(pr.x,ex,t), y = lerp(pr.y,ey,t),
            vx = rnd(-40,40), vy = rnd(-40,40), life = 0.2, max = 0.2, color = "#ffe95a"});
        }
        if(!player.dead && player.dash.t<=0 && player.invuln<=0 &&
           sweepsBody(player, pr.x, pr.y, ex, ey, 9))
          playerDamage(Js.Or(pr.dmg, 1), pr, "shock");
        foreach (var m in minions.Live()) if(m.rise >= 1 && !m.gone && segDist(pr.x, pr.y, ex, ey, m.x, m.y - 14) < 9 + m.r)
          minionHurt(m, Js.Or(pr.dmg, 1), "shock", pr);
      }
      if(!pr.dead && !player.dead &&
         sweepsBody(player, pr.px, pr.py, pr.x, pr.y, 17)){
        if(player.dash.t>0){
          if(!pr.dodged){ pr.dodged = true; perfectDodge(pr.x, pr.y); pr.dead = true; }
        } else if(player.invuln<=0){
          playerDamage((Js.Or(pr.dmg, 1))*1.6, pr, "shock");
          /* the jolt itself, not a tick of a hazard — the per-element throttle
             must not swallow the handful of stacks that IS this attack */
          player.ailCd["shock"] = 0;
          applyAilment("shock", pr, ZORB_STACKS);
          pr.dead = true;
          addRing(pr.x, pr.y, 18, 360, 150, "#ffe95a", 8, 0.5);
          addSparks(pr.x, pr.y, 18, "#fff3a0");
          SFX.thunder(); addShake(0.45);
        }
      }
    }
    else if(pr.kind=="blizzard"){
      pr.t += dt;
      if(Js.R()<0.7){
        double a = rnd(TAU), rr = pr.r*rnd(0.2,1);
        particles.Add(new Particle {kind = "spark", x = pr.x+Math.cos(a)*rr, y = pr.y+Math.sin(a)*rr*0.7,
          vx = rnd(-40,40), vy = rnd(-60,-10), life = 0.5, max = 0.5, color = FX.foe.frost});
      }
      if(pr.t>=pr.dur){
        pr.dead = true;
        addRing(pr.x,pr.y,20,340,pr.r,FX.foe.frost,8,0.45);
        addSparks(pr.x,pr.y,14,FX.foe.frostDeep);
        SFX.freeze(); addShake(0.25);
        minionsIn(pr.x, pr.y, pr.r, m=>minionHurt(m, Js.Or(pr.dmg, 1), "frost", pr));
        if(!player.dead){
          var d = dist(pr, player);
          if(d < pr.r){
            if(player.dash.t>0){ perfectDodge(pr.x, pr.y); }
            else if(player.invuln<=0){
              playerDamage(Js.Or(pr.dmg, 1), pr, "frost");
            }
          }
        }
      }
    }
    else if(pr.kind=="gas"){
      pr.t += dt; pr.life -= dt;
      if(pr.life<=0) pr.dead = true;
      pr.r = pr.rMax * Math.min(1, pr.t/pr.dur);
      var full = pr.arc >= BANE_ARC_MAX-0.01;
      foreach (var e in enemiesNear(pr.x, pr.y, pr.r + 90).Live()){
        if(e.dead || e.state!="active" || pr.hit.Contains(e)) continue;
        var d = dist(e, pr);
        if(d > pr.r + hitR(e)) continue;
        if(!full && Math.abs(angDiff(Math.atan2(e.y-pr.y, e.x-pr.x), pr.a0)) > pr.arc/2.0) continue;
        pr.hit.Add(e);
        baneStrike(e, pr.dmg, Math.atan2(e.y-pr.y, e.x-pr.x), 60, pr.dot, pr.dotT, pr.stacks);
      }
      if(Js.R() < 0.9){
        var a = full ? rnd(TAU) : pr.a0 + rnd(-pr.arc/2.0, pr.arc/2.0);
        var rq = pr.r * rnd(0.55, 1);
        particles.Add(new Particle {kind = "spark", x = pr.x+Math.cos(a)*rq, y = pr.y+Math.sin(a)*rq*0.8,
          vx = rnd(-16,16), vy = rnd(-26,-4), life = rnd(0.35,0.7), max = 0.7, color = "#8ede4a"});
      }
    }
    else if(pr.kind=="ember"){
      pr.life -= dt;
      if(pr.life<=0){ pr.dead = true; continue; }
      /* it is flung outward first, then it hunts. A loopback ember is thrown
         further out before turning, so you can see it go and come back. */
      if(pr.@out > 0){ pr.@out -= dt; }
      else {
        /* re-target every frame: the thing it was going for may already be
           dead, and the next nearest body is a better answer than fizzling */
        var t = pr.tgt;
        if(t == null || t.dead || t.state!="active"){
          /* the same preference as the throw: an ember that outlives its mark
             must not fall back to nearest-only, or the spread stops halfway */
          t = emberTarget(pr, pr.src, null);
          /* still nothing in reach — go home to the body that threw it */
          if(t == null && pr.src != null && !pr.src.dead && pr.src.state=="active"){ t = pr.src; pr.loop = true; }
          pr.tgt = t;
        }
        if(t == null){ pr.dead = true; continue; }
        var a = Math.atan2(t.y - 16 - pr.y, t.x - pr.x);
        /* turn toward it rather than snapping, so the flight reads as a flight */
        var cur = Math.atan2(pr.vy, pr.vx);
        var na = cur + clamp(angDiff(a, cur), -9*dt, 9*dt);
        pr.vx = Math.cos(na)*EMBER_SPD; pr.vy = Math.sin(na)*EMBER_SPD;
      }
      pr.x += pr.vx*dt; pr.y += pr.vy*dt;
      if(Js.R() < 0.8)
        particles.Add(new Particle {kind = "spark", x = pr.x+rnd(-3,3), y = pr.y+rnd(-3,3),
          vx = rnd(-14,14), vy = rnd(-30,-4), life = rnd(0.15,0.32), max = 0.32,
          color = Js.R()<0.5 ? "#ffb02e" : "#ff6a1a"});
      var t2 = pr.tgt;
      if(pr.@out <= 0 && t2 != null && !t2.dead && t2.state=="active" &&
         dist(pr, new Pt(t2.x, t2.y-16)) < (Js.Or(t2.r, 12)) + 10){
        pr.dead = true;
        addSparks(t2.x, t2.y-16, 6, "#ffb02e");
        /* the chain carries its depth, and the fire itself is hitEnemy's now */
        hitEnemy(t2, pr.dmg, angTo(pr, t2), 40, "fire", new HitOpts { ailDepth = pr.depth, ailSure = true });
      }
    }
    else if(pr.kind=="pbolt"){
      pr.px = pr.x; pr.py = pr.y;
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt; pr.spin += dt*11;
      if(pr.life<=0) pr.dead = true;
      foreach (var so in solids.Live()) if(dist(pr,so) < so.r){ pr.dead = true; break; }
      if(Js.R() < 0.9)
        particles.Add(new Particle {kind = "spark", x = pr.x+rnd(-5,5), y = pr.y+rnd(-5,5),
          vx = rnd(-24,24), vy = rnd(-40,-6), life = rnd(0.18,0.38), max = 0.38,
          color = Js.R()<0.5 ? "#ff9a3c" : "#ffd166"});
      if(!pr.dead){
        foreach (var e in enemiesNear(pr.x, pr.y, 70).Live()){
          if(e.dead || e.state!="active") continue;
          if(!sweepsBody(e, pr.px, pr.py, pr.x, pr.y, 9)) continue;
          pr.dead = true; break;
        }
      }
      /* it burns the SCENERY too — a staff hero opens chests and clears crates
         the way a sword hero cuts them down. This is the hero's OWN bolt, so it
         is never in the deflect list: that only ever holds what monsters throw. */
      if(!pr.dead){
        foreach (var prop in props.Live()){
          if(prop.dead || prop.indestructible) continue;
          if(dist(prop, pr) > prop.r + 10) continue;
          pr.dead = true; break;
        }
      }
      if(pr.dead){
        /* it BURSTS. Everything caught takes the hit and catches fire, and the
           fire goes on FIRST so a body the bolt kills outright still dies
           burning — the same rule the poison gas follows. */
        addRing(pr.x, pr.y, 8, 300, pr.r, "#ff8a3c", 6, 0.42);
        addRing(pr.x, pr.y, 4, 220, pr.r*0.6, "#ffd166", 4, 0.3);
        addSparks(pr.x, pr.y, 10, "#ff9a3c");
        addShake(0.1); SFX.boomSmall();
        /* the fire it leaves is the fire that landed, and hitEnemy is where
           that is decided now — for this and for every other ailment */
        foreach (var e in enemies.Live()){
          if(e.dead || e.state!="active") continue;
          if(dist(e, pr) > pr.r + (Js.Or(e.r, 12))) continue;
          hitEnemy(e, pr.dmg, angTo(pr, e), 140, "fire", new HitOpts {});
        }
        /* everything breakable inside the burst takes a hit */
        foreach (var prop in props.Live()){
          if(prop.dead || prop.indestructible) continue;
          if(dist(prop, pr) > pr.r + prop.r) continue;
          prop.hp--; prop.flash = 1;
          addSparks(prop.x, prop.y-8, 3, "#ff9a3c");
          if(prop.hp <= 0) breakProp(prop);
        }
      }
    }
    else if(pr.kind=="mbolt"){
      /* a skeleton mage's bolt: its element, the first body it meets */
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt;
      if(pr.life<=0) pr.dead = true;
      /* a chest or a crate in the way takes the knock -- how a summoner opens one */
      if(!pr.dead) foreach (var prop in props.Live()){
        if(prop.dead || prop.type == "torch" || prop.type == "crystal") continue;
        if(Math.abs(prop.x - pr.x) > 100 || Math.abs(prop.y - pr.y) > 100) continue;
        if(segDist(pr.px, pr.py, pr.x, pr.y, prop.x, prop.y) > prop.r + 3) continue;
        pr.dead = true; if(!prop.indestructible) knockProp(prop);
        break;
      }
      if(!pr.dead) foreach (var so in solids.Live()) if(dist(pr, so) < so.r){ pr.dead = true; break; }
      if(!pr.dead) foreach (var e in enemiesNear(pr.x, pr.y, 80).Live()){
        if(e.dead || e.state!="active" || !breachSees(e)) continue;
        if(!sweepsBody(e, pr.px, pr.py, pr.x, pr.y, 7)) continue;
        hitEnemy(e, pr.dmg, Math.atan2(pr.vy, pr.vx), 50, pr.srcKey, new HitOpts {});
        addSparks(pr.x, pr.y, 4, MAGE_COL[pr.el] ?? "#fff");
        pr.dead = true; break;
      }
    }
    else if(pr.kind=="parrow"){
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt;
      if(pr.life<=0) pr.dead = true;
      /* IT MEETS WHAT IS COMING THE OTHER WAY. An arrow that crosses a monster's
         shot takes it out of the air and is spent doing it -- both are gone.
         Swept against both paths, because two things closing at a thousand px/s
         pass clean through each other between frames. A thrown boulder is the
         one thing an arrow cannot stop: the arrow breaks on it, the boulder
         keeps coming. */
      if(!pr.dead) foreach (var q in projectiles.Live()){
        if(q == pr || q.dead || q.owner != "enemy") continue;
        if(Math.abs(q.x - pr.x) > 90 || Math.abs(q.y - pr.y) > 90) continue;
        var rr = ARROW_SHOOT_R + (!double.IsNaN(q.hitR) ? q.hitR : 7 + (q.big ? 5 : 0));
        if(segSegDist(pr.px, pr.py, pr.x, pr.y, double.IsNaN(q.px) ? q.x : q.px, double.IsNaN(q.py) ? q.y : q.py, q.x, q.y) > rr) continue;
        pr.dead = true;
        if(q.kind != "frostrock"){ q.dead = true; player.shotDown = player.shotDown + 1; }
        addSparks(q.x, q.y, 7, "#ffe08a"); SFX.deflect();
        break;
      }
      /* IT BREAKS WHAT IT HITS. Crates, trees, rocks, cacti and chests take an
         arrow the way they take a swing -- one knock each -- and the arrow stops
         in them. Chests are props but not solids, so without this an arrow flew
         straight through a chest and a bow hero could never open one at range. */
      if(!pr.dead) foreach (var prop in props.Live()){
        if(prop.dead) continue;
        if(Math.abs(prop.x - pr.x) > 120 || Math.abs(prop.y - pr.y) > 120) continue;
        if(segDist(pr.px, pr.py, pr.x, pr.y, prop.x, prop.y) > prop.r + 3) continue;
        if(prop.type == "torch" || prop.type == "crystal") continue;
        pr.dead = true;
        if(!prop.indestructible){
          prop.hp--; prop.flash = 1;
          addSparks(prop.x, prop.y-10, 3); SFX.hit();
          if(prop.hp <= 0) breakProp(prop);
        } else addDust(pr.x, pr.y, 2);
        break;
      }
      if(!pr.dead) foreach (var so in solids.Live()) if(dist(pr, so) < so.r){ pr.dead = true; addDust(pr.x, pr.y, 2); break; }
      if(!pr.dead) foreach (var e in enemiesNear((pr.x+pr.px)/2.0, (pr.y+pr.py)/2.0,
            Math.hypot(pr.x-pr.px, pr.y-pr.py)/2.0 + 90).Live()){
        if(e.dead||e.state!="active"||pr.hit.Contains(e)) continue;
        if(sweepsBody(e, pr.px, pr.py, pr.x, pr.y, 8)){
          /* each body already gone through has worn it down a little */
          var d = pr.dmg * Math.pow(1 - pr.pfall, pr.hit.Count);
          pr.hit.Add(e);
          hitEnemy(e, d, Math.atan2(pr.vy, pr.vx), 90, "arrow", new HitOpts {});
          arrowOnHit(pr, e, d);
          pr.pierce--;
          if(pr.pierce < 0){ if(!arrowRicochet(pr)) pr.dead = true; break; }
        }
      }
    }
    else if(pr.kind=="wblade"){
      pr.x += pr.vx*dt; pr.y += pr.vy*dt; pr.life -= dt; pr.spin += dt*14;
      if(pr.phase=="back"){
        /* it hunts the hero down on the way home instead of flying a dead line */
        var sp = Math.hypot(pr.vx, pr.vy);
        var ta = Math.atan2((player.y-12)-pr.y, player.x-pr.x);
        var ca = Math.atan2(pr.vy, pr.vx);
        var d2 = Math.hypot(player.x-pr.x, (player.y-12)-pr.y);
        /* rate-limited while it is still far out, but it homes dead-on once it
           is close — a turn-limited blade just orbits the hero and never lands */
        var na = d2 < 110 ? ta : ca + clamp(angDiff(ta, ca), -9*dt, 9*dt);
        pr.vx = Math.cos(na)*sp; pr.vy = Math.sin(na)*sp;
        if(Js.R()<0.35)
          particles.Add(new Particle {kind = "spark", x = pr.x, y = pr.y, vx = rnd(-25,25), vy = rnd(-25,25),
            life = 0.2, max = 0.2, color = "#dfe6ec"});
        if(d2 < 34) wbladeLeg(pr, true);
      }
      if(!pr.dead && pr.life<=0){
        if(pr.phase=="out" && pr.legs>0) wbladeLeg(pr, false);
        else pr.dead = true;
      }
      foreach (var e in enemiesNear((pr.x+pr.px)/2.0, (pr.y+pr.py)/2.0,
            Math.hypot(pr.x-pr.px, pr.y-pr.py)/2.0 + 90).Live()){
        if(e.dead||e.state!="active"||pr.hit.Contains(e)) continue;
        if(sweepsBody(e, pr.px, pr.py, pr.x, pr.y, 9*pr.size + 6)){
          pr.hit.Add(e);
          hitEnemy(e, pr.dmg, Math.atan2(pr.vy,pr.vx), 140, "storm", new HitOpts {spin = true});
        }
      }
    }
    else if(pr.kind=="reave"){
      /* it flies to the body it picked, reaps it, and looks for the next. When
         it runs out of bodies or bounces it comes home and fades -- the axe in
         your hands was never the one that left. */
      pr.spin += dt*26;
      pr.life -= dt;
      var t = pr.tgt;
      var live = !pr.back && t != null && !t.dead && t.state == "active";
      /* a lone throw whose body is gone flies on to its turning point first */
      var way = !pr.back && !live && !double.IsNaN(pr.wx);
      double goX = live ? t.x : way ? pr.wx : player.x, goY = live ? t.y - 16 : way ? pr.wy : player.y - 14;
      var a = Math.atan2(goY - pr.y, goX - pr.x);
      pr.x += Math.cos(a)*REAVE_SPEED*dt; pr.y += Math.sin(a)*REAVE_SPEED*dt;
      if(Js.R() < 0.7)
        particles.Add(new Particle { kind = "spark", x = pr.x + rnd(-4,4), y = pr.y + rnd(-4,4),
          vx = rnd(-18,18), vy = rnd(-26,-4), life = rnd(0.12,0.28), max = 0.28,
          color = Js.R() < 0.5 ? "#e0a81a" : "#ffe9b0" });
      if(Math.hypot(pr.x - goX, pr.y - goY) < 26){
        if(pr.back){ pr.dead = true; }
        else if(way){
          pr.wx = double.NaN;
          var nxt = pr.legs > 0 ? reaveTarget(pr.x, pr.y, pr.hit) : null;
          if(nxt != null) pr.tgt = nxt; else pr.back = true;
        }
        else {
          if(live && !pr.hit.Contains(t)){
            pr.hit.Add(t);
            /* RANK TWO: a body that is already bleeding costs the axe nothing to
               leave, which is what finally makes the axe's own terrible bleed
               multiplier something you would ever build around. */
            var free = (player.reave >= 2) && bleedStacks(t) > 0;
            hitEnemy(t, pr.dmg, a, 130, "sword", new HitOpts { spin = true, reave = true });
            addSparks(t.x, t.y - 16, 5, "#ffe9b0");
            if(!free) pr.legs--;
          } else pr.legs--;
          var nxt = pr.legs > 0 ? reaveTarget(pr.x, pr.y, pr.hit) : null;
          if(nxt != null) pr.tgt = nxt; else pr.back = true;
        }
      }
      /* the fuse. A blade is never lost, however the fight moves. */
      if(pr.life <= 0 && !pr.back) pr.back = true;
      if(pr.life <= -1.6) pr.dead = true;
    }
    else if(pr.kind=="bomb"){
      pr.t += dt;
      var k = clamp(pr.t/pr.dur,0,1);
      pr.x = lerp(pr.sx, pr.tx, k); pr.y = lerp(pr.sy, pr.ty, k);
      pr.z = 26 + 120*Math.sin(Math.PI*k);
      pr.spin += dt*8;
      if(k>=1){
        pr.dead = true;
        var br = Js.Or(pr.r, player.bomb.r);
        explode(pr.x, pr.y, br, Js.Or(pr.dmg, calcDamage(player.bomb.dmg,"spell")), new ExplodeOpts {source = "bomb"});
        SFX.bomb();
        /* Cluster Charge: the blast throws out bomblets that scatter and go off
           again, each generation weaker — the same falloff idea as chain lightning */
        var gen = Js.Or(pr.gen, 0);
        var liveBombs = projectiles.Count(q=> q.kind=="bomb" && !q.dead);
        if(gen < pr.cluster && liveBombs < 80){
          var n = gen==0 ? irnd(3,5) : irnd(2,3);   /* the second split is tighter */
          var @base = rnd(TAU);
          for(var i=0;i<n;i++){
            var a = @base + i*TAU/n + rnd(-0.3,0.3);
            var dd = br * rnd(0.8, 1.6);
            projectiles.Add(new Proj { kind = "bomb", x = pr.x, y = pr.y, sx = pr.x, sy = pr.y,
              tx = clamp(pr.x+Math.cos(a)*dd, W.wall+30, W.w-W.wall-30),
              ty = clamp(pr.y+Math.sin(a)*dd, W.wall+30, W.h-W.wall-30),
              t = 0, dur = rnd(0.34,0.5), spin = rnd(TAU),
              dmg = pr.dmg * CLUSTER_FALLOFF,
              r = br * 0.62, gen = gen+1, cluster = pr.cluster, small = true });
          }
        }
      }
    }
    else if(pr.kind=="pumpkin"){
      if(double.IsNaN(pr.x)){ pr.x = pr.sx; pr.y = pr.sy; pr.px = pr.sx; pr.py = pr.sy; }
      if(pr.delay>0){ pr.delay-=dt; continue; }
      if(pr.t < pr.dur){
        /* still in the air */
        pr.t += dt;
        var k = clamp(pr.t/pr.dur,0,1);
        pr.x = lerp(pr.sx, pr.tx, k); pr.y = lerp(pr.sy, pr.ty, k);
        pr.z = 26 + 130*Math.sin(Math.PI*k);
        pr.spin += dt*7;
        if(pr.t >= pr.dur){ pr.z = 0; SFX.boomSmall(); addDust(pr.x, pr.y, 4); }
      } else {
        /* grounded and ticking — you have three seconds to leave */
        pr.fuse -= dt;
        pr.z = Math.abs(Math.sin(vtime*9))*2;
        if(Js.R() < 0.5)
          particles.Add(new Particle {kind = "spark", x = pr.x+rnd(-4,4), y = pr.y-16, vx = rnd(-18,18), vy = rnd(-70,-30),
            life = 0.3, max = 0.3, color = pr.fuse<1 ? "#fff2b0" : "#ff9a3c"});
        if(pr.fuse <= 0){
          pr.dead = true;
          explode(pr.x, pr.y, pr.r*0.8, 0,
            new ExplodeOpts {hostile = true, brickCols = new Col[]{"#fe8a18","#ff9a3c","#3d7a2b"}, shake = 0.5});
          addRing(pr.x, pr.y, 20, 520, pr.r, "#ff9a3c", 11, 0.5);
          hazards.Add(new Hazard {kind = "fire", dmg = pr.dmg, x = pr.x, y = pr.y, r = pr.r*0.55, life = 3.5, max = 3.5});
          SFX.bomb();
          minionsIn(pr.x, pr.y, pr.r, m=>minionHurt(m, (Js.Or(pr.dmg, 1))*1.5, "fire", pr));
          if(dist(pr,player) < pr.r && !player.dead){
            if(player.dash.t>0){ perfectDodge(pr.x,pr.y); }
            else playerDamage((Js.Or(pr.dmg, 1))*1.5, pr, "fire");
          }
        }
      }
    }
    else if(pr.kind=="vbrick"||pr.kind=="spit"||pr.kind=="jrock"||pr.kind=="molten"
          ||pr.kind=="fireball"||pr.kind=="meteor"||pr.kind=="venom"){
      if(double.IsNaN(pr.x)){ pr.x = pr.sx; pr.y = pr.sy; pr.px = pr.sx; pr.py = pr.sy; }
      if(pr.delay>0){ pr.delay-=dt; continue; }
      pr.t += dt;
      var k = clamp(pr.t/pr.dur,0,1);
      pr.x = lerp(pr.sx, pr.tx, k); pr.y = lerp(pr.sy, pr.ty, k);
      pr.z = 30 + 150*Math.sin(Math.PI*k);
      pr.spin += dt*9;
      if(pr.kind=="fireball"||pr.kind=="meteor"){
        if(Js.R()<0.6)
          particles.Add(new Particle {kind = "spark", x = pr.x+rnd(-6,6), y = pr.y-(Js.Or(pr.z, 10))+rnd(-4,4),
            vx = rnd(-20,20), vy = rnd(-30,10), life = 0.3, max = 0.3,
            color = pr.kind=="meteor" ? "#ff8a5a" : "#ffb066"});
      }
      if(k>=1){
        pr.dead=true;
        if(pr.kind=="molten"){
          var fr = Js.Or(pr.fr, 66);
          explode(pr.x, pr.y, fr*0.6, 0, new ExplodeOpts {hostile = true, shake = 0.15});
          hazards.Add(new Hazard {kind = "lava", dmg = pr.dmg, x = pr.x, y = pr.y, r = fr, life = 6.5, max = 6.5});
          SFX.boomSmall();
          continue;
        }
        if(pr.kind=="fireball"){
          var fr = Js.Or(pr.fr, 46);
          explode(pr.x, pr.y, fr*0.74, 0, new ExplodeOpts {hostile = true, shake = 0.12});
          hazards.Add(new Hazard {kind = "fire", dmg = pr.dmg, x = pr.x, y = pr.y, r = fr, life = 3+rnd(0,2), max = 5});
          SFX.fireball();
          minionsIn(pr.x, pr.y, fr*0.87, m=>minionHurt(m, Js.Or(pr.dmg, 1), "fire", pr));
          if(dist(pr,player)<fr*0.87 && !player.dead){
            if(player.dash.t>0){ perfectDodge(pr.x,pr.y); }
            else if(player.invuln<=0) playerDamage(Js.Or(pr.dmg, 1), pr, "fire");
          }
          continue;
        }
        if(pr.kind=="venom"){
          /* a weak splash, and then the thing that actually matters */
          var fr = Js.Or(pr.fr, 58);
          explode(pr.x, pr.y, fr*0.5, 0, new ExplodeOpts {hostile = true,
            brickCols = new Col[]{"#2f5e23","#8ac24a","#4c7a2b"}, shake = 0.10});
          hazards.Add(new Hazard {kind = "venom", x = pr.x, y = pr.y, r = fr, life = VENOM_LIFE, max = VENOM_LIFE,
                        dmg = pr.dmg, tick = 0, seed = rnd(TAU)});
          SFX.boomSmall();
          minionsIn(pr.x, pr.y, fr*0.7, m=>minionHurt(m, Js.Or(pr.dmg, 1), null, pr));
          if(dist(pr,player)<fr*0.7 && !player.dead){
            if(player.dash.t>0){ perfectDodge(pr.x,pr.y); }
            else if(player.invuln<=0) playerDamage(pr.dmg, pr, null);
          }
          continue;
        }
        if(pr.kind=="meteor"){
          var fr = Js.Or(pr.fr, 54);
          explode(pr.x, pr.y, fr*0.81, 0, new ExplodeOpts {hostile = true, shake = 0.18});
          hazards.Add(new Hazard {kind = "fire", dmg = pr.dmg, x = pr.x, y = pr.y, r = fr, life = 3.5, max = 3.5});
          SFX.boomSmall();
          minionsIn(pr.x, pr.y, fr*0.85, m=>minionHurt(m, Js.Or(pr.dmg, 1), "fire", pr));
          if(dist(pr,player)<fr*0.85 && !player.dead){
            if(player.dash.t>0){ perfectDodge(pr.x,pr.y); }
            else if(player.invuln<=0) playerDamage(Js.Or(pr.dmg, 1), pr, "fire");
          }
          continue;
        }
        var conf = pr.kind=="vbrick" ? (rr: 62.0, cols: new Col[]{"#23272b","#3a3f44","#d3242b"}, shk: 0.22)
          : pr.kind=="jrock" ? (rr: 80.0, cols: new Col[]{"#8b8f8a","#6d6e68","#b6532b"}, shk: 0.3)
          : (rr: 46.0, cols: new Col[]{"#3d7a2b","#8ac24a","#2f5e23"}, shk: 0.18);
        explode(pr.x, pr.y, conf.rr, 0, new ExplodeOpts {hostile = true, brickCols = conf.cols, shake = conf.shk});
        minionsIn(pr.x, pr.y, conf.rr, m=>minionHurt(m, Js.Or(pr.dmg, shotPow(pr.src, pr.kind)), null, pr));
        if(dist(pr,player)<conf.rr && !player.dead){
          if(player.dash.t>0){ if(!pr.dodged){ pr.dodged=true; perfectDodge(pr.x,pr.y); } }
          else if(player.invuln<=0){
            /* it carries the damage worked out when it was thrown, so a wave-40
               boss's brick is a wave-40 brick instead of a flat 1 forever */
            playerDamage(Js.Or(pr.dmg, shotPow(pr.src, pr.kind)), pr);
          }
        }
      }
    }
  }
  projectiles = projectiles.filter(p=>!p.dead);
}
}

/* fields only the flying things here read: the reave's solo arc (wx, wy) and its
   return (back), a dodged blast, the skeletons a missile has met, a lob's height */
public sealed partial class Proj { public bool back, solo, dodged; public double wx = double.NaN, wy = double.NaN, z; public HashSet<Ent> mHit; }
public sealed partial class Ent { public double shotDown; }
