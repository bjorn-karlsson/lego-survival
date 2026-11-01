// lego-survival.html, JS 28697-30517: RENDER — one frame of the world (render), the
// chest glyph and the edge markers, the energy-shield bubble, the hero and the stacks
// over its head, the shade, the bat boss, and every monster (drawEnemy).
namespace Brickblade;

public sealed class LobConf { public double rr, sc = 1, tele; public Col[] cols; }

public static partial class G
{
/* =============================== RENDER =============================== */
/* every one of these is thrown BY a monster, so they all sit on the deep side of
   the palette — the player's own lobbed bomb is the pale one */
public static readonly Dictionary<string, LobConf> LOB_CONF = new() {
  ["vbrick"] = new LobConf {rr = 62, sc = 1, cols = new Col[]{"#23272b","#3a3f44"}, tele = 40},
  ["jrock"] = new LobConf {rr = 80, sc = 1.35, cols = new Col[]{"#8b8f8a","#6d6e68"}, tele = 52},
  ["spit"] = new LobConf {rr = 46, sc = 0.7, cols = new Col[]{"#2f5e23","#5f8f33"}, tele = 28},
  ["molten"] = new LobConf {rr = 66, sc = 0.85, cols = new Col[]{"#c2410c","#7c2d12"}, tele = 34},
  ["fireball"] = new LobConf {rr = 44, sc = 0.7, cols = new Col[]{"#c2410c","#5c1f0e"}, tele = 26},
  ["meteor"] = new LobConf {rr = 50, sc = 1, cols = new Col[]{"#b5370d","#2a1f1a"}, tele = 30},
  /* the blight gobbet: a bigger telegraph than its damage deserves, because the
     thing you are dodging is the GROUND it is about to become */
  ["venom"] = new LobConf {rr = 58, sc = 0.85, cols = new Col[]{"#4c7a2b","#8ac24a"}, tele = 44}
};
public static void render(){
  ctx.setTransform(dpr,0,0,dpr,0,0);
  ctx.globalAlpha = 1;
  ctx.globalCompositeOperation = "source-over";
  ctx.fillStyle = "#10151a"; ctx.fillRect(0,0,cw,ch);
  var shk = cam.shake*cam.shake*7;
  double sx = (Js.R()-0.5)*2*shk, sy = (Js.R()-0.5)*2*shk;
  ctx.save();
  ctx.translate(cw/2.0+sx, ch/2.0+sy); ctx.scale(zoom,zoom); ctx.translate(-cam.x,-cam.y);
  double vx = clamp(cam.x - cw/2.0/zoom - 40, 0, W.w), vy = clamp(cam.y - ch/2.0/zoom - 40, 0, W.h);
  double vw = Math.min(cw/zoom+80, W.w-vx), vh = Math.min(ch/zoom+80, W.h-vy);
  viewSet(vx, vy, vw, vh);
  drawGround(vx, vy, Math.max(1,vw), Math.max(1,vh));
  drawCaveWalls(vx, vy, Math.max(1,vw), Math.max(1,vh));
  foreach (var w in waters){
    if(w.cave) continue;
    for(var i=0;i<4;i++){
      var a = Math.sin(vtime*2.2 + i*1.7 + w.x)*0.5+0.5;
      if(a>0.55){
        ctx.fillStyle = rgba(255,255,255,(a-0.55)*0.7);
        double px = w.x + hash2(i, w.x)*w.w, py = w.y + hash2(i+9, w.y)*w.h;
        ctx.fillRect(px-4,py-0.8,8,1.6); ctx.fillRect(px-0.8,py-4,1.6,8);
      }
    }
  }
  drawBreach();
  drawAbyss();
  drawSboxGuards();
  drawRitual();
  drawLegion();
  drawClickMarks(ctx);
  drawCorpses();
  foreach (var h in hazards){
    if(h.x < vx-120 || h.x > vx+vw+120 || h.y < vy-120 || h.y > vy+vh+120) continue;
    var k = clamp(h.life/h.max,0,1);
    if(h.kind=="coil"){
      var grow = Math.min(1, h.born/0.35);
      var rr = h.r*grow;
      var arming = h.arm > 0;
      /* DORMANT: dim, no arcs, and a clock sweeping round the rim so you can
         see exactly how long you have to be somewhere else. */
      var bright = arming ? 0.25 : 0.55 + 0.45*Math.sin(vtime*9 + h.seed);
      /* the floor it owns */
      ctx.fillStyle = arming
        ? rgba(180,160,70,0.07*k)
        : rgba(255,233,90,(0.10+0.06*bright)*k);
      ctx.beginPath(); ctx.arc(h.x, h.y, rr, 0, TAU); ctx.fill();
      ctx.setLineDash([10,8]);
      ctx.strokeStyle = arming
        ? rgba(200,180,90,0.40*k)
        : rgba(255,233,90,(0.55+0.35*bright)*k);
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(h.x, h.y, rr, 0, TAU); ctx.stroke();
      ctx.setLineDash([]);
      if(arming){
        /* the winding-up sweep: a solid arc closing the circle as it charges */
        var done = 1 - h.arm/Math.max(0.001, Js.Or(h.armMax, 1));
        ctx.strokeStyle = rgba(255,233,90,0.85*k); ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(h.x, h.y, rr, -Math.PI/2.0, -Math.PI/2.0 + TAU*done); ctx.stroke();
      }
      /* arcs crawling out to the rim — only once it is live */
      ctx.strokeStyle = rgba(255,243,160,0.75*k); ctx.lineWidth = 2;
      for(var i=0;i<(arming?0:4);i++){
        var a = h.spin + i*TAU/4.0;
        ctx.beginPath(); ctx.moveTo(h.x, h.y-18);
        for(var q=1;q<=4;q++){
          var t = q/4.0;
          ctx.lineTo(h.x + Math.cos(a)*rr*t + Math.sin(h.seed+q*3+vtime*20)*6,
                     h.y - 18 + Math.sin(a)*rr*t*0.8 + Math.cos(h.seed+q*2+vtime*18)*5);
        }
        ctx.stroke();
      }
      /* the post itself: a stubby brick mast with a ball of light on top */
      ctx.fillStyle = "rgba(15,20,25,0.28)";
      ctx.beginPath(); ctx.ellipse(h.x, h.y+3, 13, 5, 0, 0, TAU); ctx.fill();
      ctx.fillStyle = "#4a4636"; ctx.strokeStyle="rgba(20,18,10,0.7)"; ctx.lineWidth=2;
      roundRect(ctx, h.x-7, h.y-22, 14, 24, 3); ctx.fill(); ctx.stroke();
      drawStudG(ctx, h.x, h.y-21, 3, "#6e6848");
      ctx.fillStyle = rgba(255,233,90,0.22+0.14*bright);
      ctx.beginPath(); ctx.arc(h.x, h.y-28, 18+3*bright, 0, TAU); ctx.fill();
      ctx.fillStyle = "#fff3a0";
      ctx.beginPath(); ctx.arc(h.x, h.y-28, 7+1.5*bright, 0, TAU); ctx.fill();
      ctx.fillStyle = "#ffffff";
      ctx.beginPath(); ctx.arc(h.x, h.y-28, 3.2, 0, TAU); ctx.fill();
      /* the last two seconds it gutters, so you can see it about to die */
      if(h.life < 2 && Math.sin(vtime*22) > 0){
        ctx.globalAlpha = 0.5;
        ctx.fillStyle = "#fff3a0";
        ctx.beginPath(); ctx.arc(h.x, h.y-28, 10, 0, TAU); ctx.fill();
        ctx.globalAlpha = 1;
      }
      continue;
    }
    if(h.kind=="venom"){
      /* a low, creeping pool — deliberately flat and dark rather than a bright
         circle, so it reads as ground you do not want to be on rather than as
         one of your own spells */
      ctx.fillStyle = rgba(47,94,35,0.42*k);
      ctx.beginPath(); ctx.ellipse(h.x, h.y, h.r, h.r*0.62, 0, 0, TAU); ctx.fill();
      ctx.fillStyle = rgba(138,194,74,0.26*k);
      ctx.beginPath(); ctx.ellipse(h.x, h.y, h.r*0.78, h.r*0.48, 0, 0, TAU); ctx.fill();
      /* bubbles working their way up through it */
      ctx.fillStyle = rgba(190,232,130,0.5*k);
      for(var i=0;i<9;i++){
        var a = i*2.399 + h.seed;
        var bob = (Math.sin(vtime*2.2 + i*1.7 + h.seed) + 1)*0.5;
        var rr = h.r*0.80*Math.sqrt(i/9.0);
        ctx.beginPath();
        ctx.arc(h.x+Math.cos(a)*rr, h.y+Math.sin(a)*rr*0.62 - bob*3,
                1.6 + bob*2.4, 0, TAU);
        ctx.fill();
      }
      ctx.strokeStyle = rgba(122,176,66,0.55*k); ctx.lineWidth = 2;
      ctx.beginPath(); ctx.ellipse(h.x, h.y, h.r, h.r*0.62, 0, 0, TAU); ctx.stroke();
      continue;
    }
    if(h.kind=="frost"){
      ctx.fillStyle = rgba(150,205,240,0.30*k);
      ctx.beginPath(); ctx.arc(h.x,h.y,h.r,0,TAU); ctx.fill();
      ctx.fillStyle = rgba(223,242,255,0.34*k);
      ctx.beginPath(); ctx.arc(h.x,h.y,h.r*0.82,0,TAU); ctx.fill();
      /* frozen-over studs */
      ctx.fillStyle = rgba(255,255,255,0.30*k);
      for(var i=0;i<10;i++){
        double a = i*2.399, rr = h.r*0.85*Math.sqrt(i/10.0);
        ctx.beginPath(); ctx.arc(h.x+Math.cos(a)*rr, h.y+Math.sin(a)*rr*0.8, 6, 0, TAU); ctx.fill();
      }
      /* cracks radiating from the impact point */
      ctx.strokeStyle = rgba(255,255,255,0.6*k); ctx.lineWidth = 2;
      for(var i=0;i<7;i++){
        var a = i/7.0*TAU + h.x*0.01;
        ctx.beginPath(); ctx.moveTo(h.x,h.y);
        ctx.lineTo(h.x+Math.cos(a)*h.r*0.55, h.y+Math.sin(a)*h.r*0.45);
        ctx.lineTo(h.x+Math.cos(a+0.3)*h.r*0.85, h.y+Math.sin(a+0.3)*h.r*0.7);
        ctx.stroke();
      }
      /* jutting shards around the rim */
      ctx.fillStyle = rgba(223,242,255,0.85*k);
      ctx.strokeStyle = rgba(90,150,195,0.8*k); ctx.lineWidth = 1.5;
      for(var i=0;i<9;i++){
        var a = i/9.0*TAU + 0.3;
        double sx2 = h.x+Math.cos(a)*h.r*0.8, sy2 = h.y+Math.sin(a)*h.r*0.66;
        var hgt = 12 + 5*Math.sin(vtime*3+i);
        ctx.beginPath();
        ctx.moveTo(sx2-5, sy2); ctx.lineTo(sx2, sy2-hgt); ctx.lineTo(sx2+5, sy2);
        ctx.closePath(); ctx.fill(); ctx.stroke();
      }
      ctx.setLineDash([9,7]);
      ctx.strokeStyle = rgba(191,227,255,0.55*k); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(h.x,h.y,h.r*0.9,0,TAU); ctx.stroke();
      ctx.setLineDash([]);
      continue;
    }
    var fire = h.kind=="fire";
    var pulse = 0.85+0.15*Math.sin(vtime*6+h.x);
    /* charred ground first, so the flames read as flames and not as a flat disc */
    ctx.fillStyle = rgbaS(fire?"70,28,12":"50,20,10", 0.45*k);
    ctx.beginPath(); ctx.ellipse(h.x,h.y,h.r,h.r*0.86,0,0,TAU); ctx.fill();
    /* molten cracks crawling through the scorch */
    ctx.strokeStyle = rgba(255,fire?150:100,40,0.65*k*pulse);
    ctx.lineWidth = 3.5;
    for(var i=0;i<6;i++){
      var ca2 = i/6.0*TAU + h.x*0.02;
      var wob = Math.sin(vtime*3+i+h.x)*0.25;
      ctx.beginPath(); ctx.moveTo(h.x,h.y);
      ctx.quadraticCurveTo(h.x+Math.cos(ca2+wob)*h.r*0.45, h.y+Math.sin(ca2+wob)*h.r*0.38,
                           h.x+Math.cos(ca2)*h.r*0.88, h.y+Math.sin(ca2)*h.r*0.74);
      ctx.stroke();
    }
    ctx.fillStyle = rgba(255,fire?140:90,40,0.34*k*pulse);
    ctx.beginPath(); ctx.ellipse(h.x,h.y,h.r*0.7,h.r*0.58,0,0,TAU); ctx.fill();
    /* a ring of licking tongues, each on its own flicker clock */
    var tongues = fire ? 9 : 7;
    for(var i=0;i<tongues;i++){
      var fa = (double)i/tongues*TAU + h.x*0.13;
      var spread = 0.30 + 0.34*((i*7919+(Js.I32(h.x)*31))%100)/100.0;
      var fx = h.x + Math.cos(fa)*h.r*spread;
      var fy = h.y + Math.sin(fa)*h.r*spread*0.8;
      var flick = Math.sin(vtime*(10+i%4) + i*2.1 + h.x);
      var fh = (h.r*0.55 + h.r*0.34*flick) * (fire?1:0.7) * k;
      if(fh <= 2) continue;
      var wob = Math.sin(vtime*13+i*1.7)*4;
      var grad = ctx.createLinearGradient(fx, fy, fx, fy-fh);
      grad.addColorStop(0,   fire ? "rgba(255,90,20,0.95)" : "rgba(220,60,15,0.9)");
      grad.addColorStop(0.35,"rgba(255,150,40,0.92)");
      grad.addColorStop(0.7, "rgba(255,215,110,0.8)");
      grad.addColorStop(1,   "rgba(255,250,210,0)");
      ctx.fillStyle = grad;
      ctx.beginPath();
      ctx.moveTo(fx-5.5, fy);
      ctx.quadraticCurveTo(fx-4.5, fy-fh*0.5, fx+wob*0.4, fy-fh*0.78);
      ctx.quadraticCurveTo(fx+wob*0.9, fy-fh*0.92, fx+wob, fy-fh);
      ctx.quadraticCurveTo(fx+4.5, fy-fh*0.6, fx+5.5, fy);
      ctx.closePath(); ctx.fill();
      /* white-hot heart of the tongue */
      ctx.fillStyle = rgba(255,245,190,0.5*k);
      ctx.beginPath();
      ctx.moveTo(fx-2, fy);
      ctx.quadraticCurveTo(fx, fy-fh*0.4, fx+wob*0.3, fy-fh*0.52);
      ctx.quadraticCurveTo(fx+2, fy-fh*0.3, fx+2, fy);
      ctx.closePath(); ctx.fill();
    }
    /* embers drifting over the pool */
    ctx.fillStyle = rgba(255,215,120,0.85*k);
    for(var i=0;i<4;i++){
      var ba = i/4.0*TAU + vtime*(fire?2:1.4) + h.x;
      var lift = ((vtime*38 + i*40 + h.x) % 70);
      var br = h.r*0.055*(1+0.5*Math.sin(vtime*7+i*2));
      ctx.globalAlpha = clamp(1-lift/70.0,0,1)*k;
      ctx.beginPath();
      ctx.arc(h.x+Math.cos(ba)*h.r*0.5, h.y+Math.sin(ba)*h.r*0.4 - lift, br, 0, TAU);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
  ctx.fillStyle = "rgba(10,25,15,0.05)";
  for(var i=0;i<3;i++){
    var cxp = ((vtime*12 + i*1100) % (W.w+800)) - 400;
    ctx.beginPath(); ctx.ellipse(cxp, 300+i*640, 240, 120, 0, 0, TAU); ctx.fill();
  }
  foreach (var e in enemies){
    if(e.dead) continue;
    if(e.type=="brute" && e.mode=="windup"){
      var k = e.modeT/0.55;
      ctx.fillStyle = rgba(211,36,43,0.22+0.22*Math.sin(vtime*20));
      ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
      roundRect(ctx, 0, -16, 150*k+40, 32, 16); ctx.fill(); ctx.restore();
    }
    if(e.type=="lancer" && e.mode=="windup"){
      var k = e.modeT/0.7;
      ctx.fillStyle = rgba(255,170,60,0.2+0.2*Math.sin(vtime*18));
      ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
      roundRect(ctx, 0, -8, 210*k+40, 16, 8); ctx.fill(); ctx.restore();
    }
    /* golem: charge lane, then the stomp footprint painted on the ground */
    if(!string.IsNullOrEmpty(e.golem)){
      var G = GOLEM_CONF[e.golem];
      if(e.mode=="chargeWind"){
        var k = clamp(e.modeT/0.55,0,1);
        ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
        var rgbC = GOLEM_RGB[e.golem].a;
        ctx.fillStyle = rgbaS(rgbC, 0.26+0.20*Math.sin(vtime*18));
        roundRect(ctx, 0, -28, 300*k+60, 56, 26); ctx.fill();
        ctx.strokeStyle = rgbaS(rgbC, 0.85); ctx.lineWidth = 3;
        roundRect(ctx, 0, -28, 300*k+60, 56, 26); ctx.stroke();
        /* arrow heads racing down the lane */
        ctx.fillStyle = rgba(255,255,255,0.45+0.3*Math.sin(vtime*18));
        for(var ai=0; ai<3; ai++){
          var ax2 = 50 + ((vtime*260 + ai*110) % (300*k+40));
          ctx.beginPath(); ctx.moveTo(ax2, -14); ctx.lineTo(ax2+18, 0); ctx.lineTo(ax2, 14);
          ctx.closePath(); ctx.fill();
        }
        ctx.restore();
      }
      if(e.mode=="throwWind"){
        /* a short stub of a lane at the golem's own feet — enough to read which
           way it is facing, without fourteen of them tiling the whole floor */
        var k = clamp(e.modeT/GOLEM_THROW_WIND,0,1);
        var rgb = GOLEM_RGB[e.golem].a;
        ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
        ctx.fillStyle = rgbaS(rgb, 0.10+0.18*k);
        roundRect(ctx, 0, -14, 70+40*k, 28, 13); ctx.fill();
        ctx.restore();
      }
      if(e.mode=="stompWind"){
        var k = clamp(e.modeT/G.wind,0,1);
        var rgb = GOLEM_RGB[e.golem].a;
        groundDisc(ctx, e.x, e.y, G.stompR, rgbaS(rgb, 0.10+0.10*k));
        ctx.setLineDash([16,11]);
        groundRing(ctx, e.x, e.y, G.stompR, 6, rgbaS(rgb, 0.55+0.35*Math.sin(vtime*16)));
        ctx.setLineDash([]);
        /* a closing ring that lands exactly when the stomp does */
        groundRing(ctx, e.x, e.y, 28 + (G.stompR-28)*(1-k), 4, "rgba(255,255,255,0.9)");
      }
    }
    /* jugg mace slam telegraph */
    if(e.type=="jugg" && e.mode=="wind"){
      var k = clamp(e.modeT/0.7,0,1);
      ctx.setLineDash([10,8]);
      ctx.strokeStyle = rgba(255,209,102,0.45+0.3*Math.sin(vtime*16));
      ctx.lineWidth = 4;
      ctx.beginPath(); ctx.arc(e.x,e.y,130,0,TAU); ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillStyle = "rgba(255,209,102,0.08)";
      ctx.beginPath(); ctx.arc(e.x,e.y,130,0,TAU); ctx.fill();
      ctx.strokeStyle = "rgba(255,230,150,0.8)"; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(e.x,e.y, 24 + 106*(1-k), 0, TAU); ctx.stroke();
    }
    /* THE BLOATER'S PLAGUE BREATH, where it will land: the cone and how far it
       reaches, drawn from the moment it plants its feet -- filling outwards as
       the heave builds so it lands at the rim as the breath comes -- and then
       turning with the breath while it lasts. Flat, like the hit it shows. */
    if(e.type=="bloater" && (e.mode=="breathWind" || e.mode=="breath")){
      bool wind = e.mode=="breathWind"; double k = wind ? clamp(e.modeT/BLOAT_WIND,0,1) : 1;
      double a = e.breathA, R = BLOAT_RANGE;
      void cone(double rr){ ctx.beginPath(); ctx.moveTo(e.x, e.y); ctx.arc(e.x, e.y, rr, a - BLOAT_CONE, a + BLOAT_CONE); ctx.closePath(); };
      cone(R);
      ctx.fillStyle = wind ? rgba(158,222,90,0.06 + 0.08*k) : rgba(120,190,60,0.22 + 0.06*Math.sin(vtime*14));
      ctx.fill();
      ctx.setLineDash(wind ? new double[] {10,8} : new double[0]);
      ctx.strokeStyle = rgba(190,240,120,wind ? 0.5 + 0.3*Math.sin(vtime*16) : 0.8); ctx.lineWidth = 3; ctx.stroke();
      ctx.setLineDash([]);
      if(wind){ cone(R*k); ctx.fillStyle = "rgba(190,240,120,0.18)"; ctx.fill(); }
    }
    /* generic small shockwave rings (jugg) */
    if(e.ring != null && e.type!="revenant" && e.type!="boss"){
      var rmax = Js.Or(e.ring.max, 130);
      var kk = clamp(1-e.ring.r/rmax,0,1);
      string rgb = e.ring.golem=="ice" ? "191,227,255"
                : e.ring.golem=="fire" ? "255,138,60"
                : e.ring.golem=="stone" ? "201,206,201" : "255,209,102";
      flatRing(ctx, e.x, e.y, e.ring.r, !string.IsNullOrEmpty(e.ring.golem) ? 14 : 7, rgbaS(rgb, kk*0.9));
      if(!string.IsNullOrEmpty(e.ring.golem)){
        flatRing(ctx, e.x, e.y, e.ring.r-9, 4, rgba(255,255,255,kk*0.75));
        /* studs riding the crest, so it reads as bricks lifting off the floor */
        var studs = 20;
        ctx.fillStyle = rgba(255,255,255,kk*0.55);
        for(var i=0;i<studs;i++){
          var sa = (double)i/studs*TAU + e.ring.r*0.01;
          ctx.beginPath();
          ctx.arc(e.x+Math.cos(sa)*e.ring.r, e.y+Math.sin(sa)*e.ring.r,
            4*kk+1.5, 0, TAU);
          ctx.fill();
        }
      }
    }
    if(e.type=="lancer" && e.mode=="windup"){
      var k = e.modeT/0.7;
      ctx.fillStyle = rgba(255,170,60,0.2+0.2*Math.sin(vtime*18));
      ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
      roundRect(ctx, 0, -8, 210*k+40, 16, 8); ctx.fill(); ctx.restore();
    }
    if(e.type=="revenant" && e.mode=="windup"){
      ctx.fillStyle = "rgba(220,230,235,0.15)";
      ctx.beginPath(); ctx.arc(e.x,e.y,REV_MAX,0,TAU); ctx.fill();
      ctx.setLineDash([14,10]);
      ctx.strokeStyle = rgba(235,242,246,0.65+0.3*Math.sin(vtime*12));
      ctx.lineWidth = 5;
      ctx.beginPath(); ctx.arc(e.x,e.y,REV_MAX,0,TAU); ctx.stroke();
      ctx.setLineDash([]);
      var k = clamp(e.modeT/0.8,0,1);
      ctx.strokeStyle = "rgba(255,255,255,0.75)"; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(e.x,e.y, 30 + (REV_MAX-30)*(1-k), 0, TAU); ctx.stroke();
    }
    if(e.type=="revenant" && e.ring != null){
      var kr = clamp(1-e.ring.r/REV_MAX,0,1);
      groundRing(ctx, e.x, e.y, e.ring.r, 10, rgba(207,214,221,kr*0.9));
      groundRing(ctx, e.x, e.y, e.ring.r-8, 3, "rgba(255,255,255,0.5)");
    }
    if((e.type=="necromancer"||e.type=="necrolord") && e.mode=="summon"){
      var rr = e.type=="necrolord"?110:52;
      ctx.fillStyle = "rgba(201,184,232,0.12)";
      ctx.beginPath(); ctx.arc(e.x,e.y,rr,0,TAU); ctx.fill();
      ctx.strokeStyle = rgba(201,184,232,0.6+0.3*Math.sin(vtime*12));
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(e.x,e.y,rr*(0.9+0.1*Math.sin(vtime*8)),0,TAU); ctx.stroke();
    }
    if(e.type=="boss"){
      /* super boss golden aura */
      if(e.super){
        var pr2 = 1+0.06*Math.sin(vtime*4);
        ctx.setLineDash([12,8]);
        ctx.strokeStyle = rgba(255,209,102,0.5+0.25*Math.sin(vtime*5));
        ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(e.x,e.y,(e.r+20)*pr2,0,TAU); ctx.stroke();
        ctx.setLineDash([]);
      }
      if(e.mode=="quakeWind"){
        /* the ground the hammer is about to break, tightening as it falls */
        var k = clamp(e.modeT/QUAKE_WIND, 0, 1);
        flatDisc(ctx, e.x, e.y, QUAKE_MAX, rgba(207,214,221,0.07+0.10*k));
        ctx.setLineDash([18,13]);
        flatRing(ctx, e.x, e.y, QUAKE_MAX, 6, rgba(207,214,221,0.5+0.35*Math.sin(vtime*18)));
        ctx.setLineDash([]);
        flatRing(ctx, e.x, e.y, 24 + (QUAKE_MAX-24)*(1-k), 4, "rgba(255,255,255,0.9)");
      }
      if(e.quakes != null && e.quakes.Count > 0){
        /* each wave is an ARC, not a ring — it only exists inside the 180 fan
           the spikes are coming up in, so the shape tells you where to stand */
        foreach (var q in e.quakes){
          var band = quakeBand(q);
          var fade = clamp(1 - q.r/q.max, 0, 1);
          /* a TRUE circle: the spikes come up on a circle, so the crest that
             promises them has to be one too */
          ctx.save(); ctx.translate(e.x, e.y);
          for(var i=-1;i<=1;i++){
            var rr = Math.max(2, q.r + i*band*0.62);
            ctx.strokeStyle = rgba(220,228,234,(i==0?0.85:0.38)*(0.3+0.7*fade));
            ctx.lineWidth = Math.max(2, band*(i==0?0.9:0.5));
            ctx.beginPath(); ctx.arc(0, 0, rr, q.a-QUAKE_ARC/2.0, q.a+QUAKE_ARC/2.0); ctx.stroke();
          }
          ctx.strokeStyle = "rgba(255,255,255,0.9)";
          ctx.lineWidth = Math.max(1.5, band*0.32);
          ctx.beginPath(); ctx.arc(0, 0, q.r, q.a-QUAKE_ARC/2.0, q.a+QUAKE_ARC/2.0); ctx.stroke();
          /* dust dragging behind the crest */
          ctx.fillStyle = rgba(150,150,145,0.10*fade);
          ctx.beginPath(); ctx.moveTo(0,0);
          ctx.arc(0, 0, Math.max(2, q.r-band), q.a-QUAKE_ARC/2.0, q.a+QUAKE_ARC/2.0);
          ctx.closePath(); ctx.fill();
          ctx.restore();
        }
      }
      if(e.mode=="maceWind"){
        /* The circle the head sweeps, and a bright wedge racing round it. A TRUE
           circle — the hit test is `dist(b,player) < reach`, so drawing it
           squashed put the north and south edges of the sweep outside anything
           you could see coming. */
        var rr = maceReach(e);
        var winding = e.modeT < MACE_WIND;
        ctx.save();
        ctx.fillStyle = rgba(255,209,102,winding?0.10:0.18);
        ctx.beginPath(); ctx.arc(e.x, e.y, rr, 0, TAU); ctx.fill();
        ctx.setLineDash([14,10]);
        ctx.strokeStyle = rgba(255,209,102,0.55+0.35*Math.sin(vtime*16));
        ctx.lineWidth = 5;
        ctx.beginPath(); ctx.arc(e.x, e.y, rr, 0, TAU); ctx.stroke();
        ctx.setLineDash([]);
        if(!winding && !double.IsNaN(e.whirlA)){
          ctx.fillStyle = "rgba(255,240,180,0.28)";
          ctx.beginPath(); ctx.moveTo(e.x, e.y);
          ctx.arc(e.x, e.y, rr, e.whirlA-1.1, e.whirlA);
          ctx.closePath(); ctx.fill();
        }
        ctx.restore();
      }
      if(e.mode=="slamWind"){
        var pulling = e.modeT < BOSS_PULL_T;
        flatDisc(ctx, e.x, e.y, RING_MAX, rgba(255,90,50,pulling?0.10:0.16));
        ctx.setLineDash([16,12]);
        flatRing(ctx, e.x, e.y, RING_MAX, 6, rgba(255,110,70,0.6+0.3*Math.sin(vtime*14)));
        ctx.setLineDash([]);
        if(pulling){
          /* suction: arcs sweeping inward, so the drag is something you see coming */
          var w = clamp(e.modeT/BOSS_PULL_T, 0, 1);
          for(var i=0;i<5;i++){
            var rr = RING_MAX * (1 - ((w*1.6 + i/5.0) % 1));
            flatRing(ctx, e.x, e.y, rr, 5, rgba(255,200,120,0.65*(1-rr/RING_MAX)+0.15));
          }
          ctx.fillStyle = rgba(255,190,110,0.5+0.4*Math.sin(vtime*20));
          ctx.font = "700 15px Fredoka"; ctx.textAlign = "center";
          ctx.fillText("PULLED IN!", e.x, e.y - 84*(Js.Or(e.scale, 1)));
        } else {
          /* the strike: one ring closing onto the boss, landing with the slam */
          var k = clamp((e.modeT-BOSS_PULL_T)/0.5, 0, 1);
          flatRing(ctx, e.x, e.y, 30 + (RING_MAX-30)*(1-k), 6, "rgba(255,235,180,0.95)");
          flatDisc(ctx, e.x, e.y, RING_MAX*k*0.28, rgba(255,150,70,0.25*k));
        }
      }
      if(e.mode=="dashWind"){
        var k = clamp(e.modeT/0.6,0,1);
        ctx.save(); ctx.translate(e.x,e.y); ctx.rotate(e.lockA);
        ctx.fillStyle = rgba(255,60,40,0.18+0.18*Math.sin(vtime*18));
        roundRect(ctx, 0, -34, 420*k+60, 68, 30); ctx.fill();
        ctx.strokeStyle = "rgba(255,120,90,0.7)"; ctx.lineWidth = 3;
        roundRect(ctx, 0, -34, 420*k+60, 68, 30); ctx.stroke();
        ctx.restore();
      }
      if(e.mode=="ringWind"){
        var k = clamp(e.modeT/0.7,0,1);
        ctx.strokeStyle = rgba(230,235,240,0.4+0.3*Math.sin(vtime*16));
        ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(e.x,e.y, 60+90*k, 0, TAU); ctx.stroke();
      }
      /* super boss signature telegraphs */
      if(e.super && (e.mode=="novaWind"||e.mode=="spiralWind")){
        ctx.strokeStyle = rgba(255,209,102,0.35+0.3*Math.sin(vtime*16));
        ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(e.x,e.y, 40+50*(e.modeT/0.85), 0, TAU); ctx.stroke();
      }
      if(e.super && e.mode=="meteorWind"){
        ctx.fillStyle = "rgba(255,120,50,0.08)";
        ctx.beginPath(); ctx.arc(player.x, player.y, 320, 0, TAU); ctx.fill();
        ctx.setLineDash([14,10]);
        ctx.strokeStyle = rgba(255,120,50,0.4+0.25*Math.sin(vtime*12));
        ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(player.x, player.y, 320, 0, TAU); ctx.stroke();
        ctx.setLineDash([]);
      }
      if(e.ring != null){
        var k2 = clamp(1-e.ring.r/RING_MAX,0,1);
        string band = "255,106,42";
        /* a filled band on the floor, sweeping outward — a TRUE circle, because
           that is what the hit test is */
        ctx.fillStyle = rgbaS(band, k2*0.30);
        ctx.beginPath();
        ctx.arc(e.x,e.y,e.ring.r,0,TAU);
        ctx.arc(e.x,e.y,Math.max(1,e.ring.r-26),0,TAU,true);
        ctx.fill();
        flatRing(ctx, e.x, e.y, e.ring.r, 15, rgbaS(band, k2*0.95));
        flatRing(ctx, e.x, e.y, e.ring.r-10, 5, rgba(255,255,255,k2*0.85));
        /* studded crest so it reads as bricks flying outward */
        var studs = 22;
        ctx.fillStyle = rgba(255,255,255,k2*0.5);
        for(var i=0;i<studs;i++){
          var sa = (double)i/studs*TAU + e.ring.r*0.012;
          ctx.beginPath();
          ctx.arc(e.x+Math.cos(sa)*e.ring.r, e.y+Math.sin(sa)*e.ring.r,
            4.5*k2+1.5, 0, TAU);
          ctx.fill();
        }
        ctx.setLineDash([10,14]);
        flatRing(ctx, e.x, e.y, RING_MAX, 3, rgbaS(band, 0.3));
        ctx.setLineDash([]);
      }
    }
  }
  /* STONE SPIKES. Drawn after the ground and before the bodies, so they stand
     between the hero and the floor: a shadow at the base, a jagged rock tooth
     rising out of it, and a dusty collar where it broke through. */
  foreach (var sp in spikes){
    var h = spikeHeight(sp);
    if(h < 1) continue;
    var w = sp.w;
    ctx.save(); ctx.translate(sp.x, sp.y);
    ctx.fillStyle = "rgba(15,20,25,0.26)";
    ctx.beginPath(); ctx.ellipse(0, 1, w*1.35, w*0.52, 0, 0, TAU); ctx.fill();
    /* the tooth */
    ctx.fillStyle = "#8b8f8a";
    ctx.strokeStyle = "rgba(25,30,35,0.65)"; ctx.lineWidth = 2; ctx.lineJoin="round";
    ctx.beginPath();
    ctx.moveTo(-w, 2);
    ctx.lineTo(-w*0.55, -h*0.62);
    ctx.lineTo(Math.sin(sp.seed)*w*0.25, -h);
    ctx.lineTo(w*0.62, -h*0.55);
    ctx.lineTo(w, 2);
    ctx.closePath(); ctx.fill(); ctx.stroke();
    /* a lit face so it reads as three-dimensional */
    ctx.fillStyle = "#a8ada8";
    ctx.beginPath();
    ctx.moveTo(-w*0.2, 2); ctx.lineTo(-w*0.5, -h*0.6);
    ctx.lineTo(Math.sin(sp.seed)*w*0.25, -h); ctx.lineTo(w*0.1, -h*0.4);
    ctx.closePath(); ctx.fill();
    /* broken collar */
    ctx.fillStyle = "rgba(170,170,162,0.55)";
    ctx.beginPath(); ctx.ellipse(0, 2, w*1.15, w*0.4, 0, 0, TAU); ctx.fill();
    ctx.restore();
  }
  /* THE HERO'S OWN TEETH. Same rock, but they carry the element the legendary
     is cycling through — so a fire slam is a row of glowing coals coming up out
     of the floor and a frost one is ice. That is the whole read on which
     element is up this cast. */
  foreach (var sp in pspikes){
    if(sp.wait > 0) continue;
    var h = spikeHeight(sp);
    if(h < 1) continue;
    var w = sp.w;
    string el = sp.el, glow = el != null ? SLAM_COL[el] : null;
    ctx.save(); ctx.translate(sp.x, sp.y);
    ctx.globalAlpha = SLAM_SPIKE_A;   /* restored with the rest of the state */
    ctx.fillStyle = "rgba(15,20,25,0.26)";
    ctx.beginPath(); ctx.ellipse(0, 1, w*1.35, w*0.52, 0, 0, TAU); ctx.fill();
    if(glow != null){
      ctx.fillStyle = rgbaS(prs(glow), 0.22);
      ctx.beginPath(); ctx.ellipse(0, 1, w*2.2, w*0.9, 0, 0, TAU); ctx.fill();
    }
    ctx.fillStyle = el != null ? mixc("#8b8f8a", glow, 0.45) : "#9aa09b";
    ctx.strokeStyle = "rgba(25,30,35,0.65)"; ctx.lineWidth = 2; ctx.lineJoin="round";
    ctx.beginPath();
    ctx.moveTo(-w, 2);
    ctx.lineTo(-w*0.55, -h*0.62);
    ctx.lineTo(Math.sin(sp.seed)*w*0.25, -h);
    ctx.lineTo(w*0.62, -h*0.55);
    ctx.lineTo(w, 2);
    ctx.closePath(); ctx.fill(); ctx.stroke();
    ctx.fillStyle = el != null ? mixc("#c6ccc6", glow, 0.6) : "#b6bbb6";
    ctx.beginPath();
    ctx.moveTo(-w*0.2, 2); ctx.lineTo(-w*0.5, -h*0.6);
    ctx.lineTo(Math.sin(sp.seed)*w*0.25, -h); ctx.lineTo(w*0.1, -h*0.4);
    ctx.closePath(); ctx.fill();
    if(glow != null){
      /* a seam of the element running up the tooth */
      ctx.strokeStyle = rgbaS(prs(glow), 0.55+0.35*Math.sin(vtime*18+sp.seed));
      ctx.lineWidth = 1.8; ctx.lineCap="round";
      ctx.beginPath();
      ctx.moveTo(-w*0.15, 0); ctx.lineTo(w*0.12, -h*0.5);
      ctx.lineTo(-w*0.05, -h*0.72); ctx.lineTo(Math.sin(sp.seed)*w*0.2, -h*0.95);
      ctx.stroke(); ctx.lineCap="butt";
    }
    ctx.fillStyle = "rgba(170,170,162,0.55)";
    ctx.beginPath(); ctx.ellipse(0, 2, w*1.15, w*0.4, 0, 0, TAU); ctx.fill();
    ctx.restore();
  }
  foreach (var pr in projectiles){
    if(!onScreen(pr, 90)) continue;
    var lc = LOB_CONF[pr.kind];
    if(lc != null && (pr.delay>0 || pr.t<pr.dur-0.15)){
      var k = clamp(pr.t/pr.dur,0,1);
      ctx.strokeStyle = rgba(226,98,15,0.45+0.35*Math.sin(vtime*16)); ctx.lineWidth=3.5;
      ctx.beginPath(); ctx.arc(pr.tx, pr.ty, lc.tele*(1-k*0.4), 0, TAU); ctx.stroke();
    }
    if(pr.kind=="blizzard"){
      var k = clamp(pr.t/pr.dur,0,1);
      var rr = pr.r*(0.6+0.4*k);
      if(double.IsNaN(pr.seed)) pr.seed = rnd(TAU);
      ctx.fillStyle = rgba(191,227,255,0.10+0.14*k);
      ctx.beginPath(); ctx.arc(pr.x, pr.y, rr, 0, TAU); ctx.fill();
      /* flakes spiralling down into the impact point */
      ctx.strokeStyle = rgba(255,255,255,0.35+0.45*k); ctx.lineWidth = 1.8; ctx.lineCap="round";
      for(var i=0;i<14;i++){
        var fa = pr.seed + i*2.399 + vtime*2.4;
        var fr = rr*(0.25 + 0.72*(((i*0.137 + vtime*0.55) % 1)));
        double fx2 = pr.x + Math.cos(fa)*fr, fy2 = pr.y + Math.sin(fa)*fr*0.8;
        var fs = 3.4 - 1.6*(fr/rr);
        for(var a2=0;a2<3;a2++){
          var aa = a2*Math.PI/3.0 + vtime*3 + i;
          ctx.beginPath();
          ctx.moveTo(fx2-Math.cos(aa)*fs, fy2-Math.sin(aa)*fs);
          ctx.lineTo(fx2+Math.cos(aa)*fs, fy2+Math.sin(aa)*fs);
          ctx.stroke();
        }
      }
      ctx.setLineDash([10,8]);
      ctx.strokeStyle = rgba(191,227,255,0.6+0.3*Math.sin(vtime*14));
      ctx.lineWidth = 4.5;
      ctx.beginPath(); ctx.arc(pr.x, pr.y, rr, 0, TAU); ctx.stroke();
      ctx.setLineDash([]);
      /* the ring that closes exactly as it detonates */
      ctx.strokeStyle = rgba(255,255,255,0.5+0.4*k); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(pr.x, pr.y, Math.max(2, rr*(1-k)), 0, TAU); ctx.stroke();
    }
  }
  foreach (var pk in pickups) if(onScreen(pk, 60)) drawPickup(ctx, pk);
  drawGearDrops();
  if(player != null && player.magnetT>0 && !player.dead){
    ctx.setLineDash([8,10]);
    ctx.strokeStyle = rgba(255,214,110,0.45+0.3*Math.sin(vtime*6));
    ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(player.x, player.y, 44+Math.sin(vtime*4)*5, 0, TAU); ctx.stroke();
    ctx.setLineDash([]);
  }
  var list = new List<(double y, Action f)>();
  foreach (var p in props) if(!p.dead && onScreen(p, 120))
    list.Add((p.y, ()=>drawProp(ctx,p)));
  foreach (var e in enemies){
    if(!onScreen(e, 120)) continue;
    if(e.breach != null && !breachSees(e)) continue;      /* the other layer, where the circle has not reached */
    /* THE SHIELD GOES ON OUTSIDE drawEnemy, not inside it. That function is
       seven shapes behind five early returns -- golems, bats, minis, the bat
       boss and the minifigs -- and putting the bubble on the minifig path gave
       every one of the others a shield it did not draw. Out here there is one
       body per enemy and the bubble lands on top of whichever shape it was. */
    list.Add((e.y, ()=>{ drawEnemy(ctx, e);
      if(e.esMax > 0) drawShieldBubble(ctx, e, e.es/e.esMax, e.esFlash); }));
  }
  foreach (var m in minions) if(onScreen(m, 80)) list.Add((m.y, ()=>drawMinion(ctx, m)));
  if(player != null && (!player.dead || player.deadT<0.05)) list.Add((player.y, ()=>drawHero(ctx)));
  list = list.OrderBy(q=>q.y).ToList();   /* Array.prototype.sort is stable, as OrderBy is */
  foreach (var it in list) it.f();
  /* Lingering Vortex totems — the same whirlwind, standing still and unlit by the
     hero's own glow so you can tell it is not following you */
  foreach (var pr in projectiles){
    if(pr.kind!="vtotem" || pr.dead || !onScreen(pr, 120)) continue;
    var k = clamp(pr.life/1.2, 0, 1);            /* fades out at the end */
    ctx.save();
    ctx.globalAlpha = k;
    /* a TRUE circle: the hit test is `hitsBody(e, pr.x, pr.y, pr.r)`, so the
       flattened version was under-drawing its own reach north and south */
    flatDisc(ctx, pr.x, pr.y, pr.r, "rgba(150,166,182,0.10)");
    ctx.setLineDash([10,8]);
    flatRing(ctx, pr.x, pr.y, pr.r, 3, rgba(150,166,182,0.35+0.25*Math.sin(vtime*11+pr.seed)));
    ctx.setLineDash([]);
    for(var ri=0; ri<2; ri++){
      double n2 = 3+ri, rr = pr.r*(0.6+ri*0.42), w = ri != 0? -3.6 : 4.8;
      for(var i=0;i<n2;i++){
        var a = vtime*w + i*TAU/n2 + pr.seed + ri;
        double bx = pr.x + Math.cos(a)*rr, by = pr.y + Math.sin(a)*rr;
        ctx.save(); ctx.translate(bx,by); ctx.rotate(a+Math.PI/2.0);
        drawWeaponArt(ctx, 0.38, false, Js.Or(pr.art, thrownArt()));
        ctx.restore();
      }
    }
    ctx.restore();
  }
  /* Blademaster whirl — the trailing arc. It is drawn as a TRUE CIRCLE, not a
     squashed one: the hit test is a circle, so a flattened drawing meant the
     blade struck a foot above and below the ring you could see. */
  if(player != null && player.whirl && !player.dead) drawCyclone(ctx, player);
  /* blade vortex — 3 rows of spinning blades */
  if(player != null && player.vortex.n>0 && player.vortex.t>0 && !player.dead){
    var v = player.vortex;
    var vr = v.r + (v.n-1)*10;
    ctx.save();
    ctx.setLineDash([12,9]);
    ctx.strokeStyle = rgba(207,214,221,0.4+0.25*Math.sin(vtime*16));
    ctx.lineWidth = 3.5;
    ctx.beginPath(); ctx.arc(player.x, player.y, vr, 0, TAU); ctx.stroke();
    ctx.setLineDash([]);
    ctx.fillStyle = "rgba(207,214,221,0.06)";
    ctx.beginPath(); ctx.arc(player.x, player.y, vr, 0, TAU); ctx.fill();
    var rows = new[] { (rr: 0.55, n: 3.0, sp: 5.5), (rr: 0.82, n: 4.0, sp: -4.2), (rr: 1.08, n: 5.0, sp: 6.2) };
    var vSpin = clamp(1.0/Math.max(0.25, v.hitMul), 1, 1.8);
    for(var ri=0; ri<rows.Count(); ri++){
      var row = rows[ri];
      var w = clamp(row.sp*vSpin, -VORTEX_MAX_SPIN, VORTEX_MAX_SPIN);
      for(var i=0;i<row.n;i++){
        var a = vtime*w + i*TAU/row.n + ri*1.1;
        var bx = player.x + Math.cos(a)*vr*row.rr;
        var by = player.y-6 + Math.sin(a)*vr*row.rr*0.72;
        ctx.save(); ctx.translate(bx,by); ctx.rotate(a+Math.PI/2.0);
        drawWeaponArt(ctx, 0.42, true, thrownArt());
        ctx.restore();
      }
    }
    ctx.restore();
  }
  if(player != null && cursedRegen()>0 && !player.dead && player.hp < player.maxHp){
    double bw = 36, bxx = player.x-bw/2.0, byy = player.y-60;
    ctx.fillStyle="rgba(15,20,25,0.65)";
    roundRect(ctx, bxx-1, byy-1, bw+2, 7, 3); ctx.fill();
    ctx.fillStyle = "#7fdc7e";
    roundRect(ctx, bxx, byy, Math.max(2,bw*clamp(player.hp - Math.floor(player.hp),0,1)), 5, 2.5); ctx.fill();
  }
  /* hovering storm bricks — one per zapper */
  if(player != null && player.chain.n>0 && player.chain.zappers>0 && !player.dead){
    var zn = Math.min(chainMaxZappers(), player.chain.zappers);
    for(var z=0; z<zn; z++){
      var bp = chainBrickPos(player, z, zn);
      var ph = z*1.3;
      var charged = (z < player.chain.cds.Count ? player.chain.cds[z] : 0) <= 0.12;   /* each brick glows on its own clock */
      var gl = prs(STORM_BRICK.glow).join(",");
      ctx.fillStyle = rgbaS(gl, charged ? 0.26 : 0.16);
      ctx.beginPath(); ctx.arc(bp.x, bp.y-4, 16+Math.sin(vtime*5+ph)*3, 0, TAU); ctx.fill();
      ctx.save(); ctx.translate(bp.x, bp.y); ctx.rotate(Math.sin(vtime*2+ph)*0.2);
      ctx.fillStyle = STORM_BRICK.body; ctx.strokeStyle="rgba(50,36,0,0.6)"; ctx.lineWidth=1.8;
      roundRect(ctx,-13,-13,26,26,3); ctx.fill(); ctx.stroke();
      /* plain 2x2: two studs across, two rows deep */
      for(var row=0; row<2; row++)
        for(var k=0;k<2;k++) drawStudG(ctx,-6.4+k*12.8, -6.4+row*12.8, 4, STORM_BRICK.stud);
      ctx.fillStyle = STORM_BRICK.spark;
      ctx.beginPath(); ctx.arc(15,-15,2+Math.sin(vtime*12+ph)*0.8,0,TAU); ctx.fill();
      ctx.restore();
    }
  }
  var aliveList = enemies.filter(e=>!e.dead && e.state=="active" && !e.dormant && !isBanner(e));
  var markFew = state=="play" && aliveList.Count()>0 && aliveList.Count()<=6;
  if(markFew){
    foreach (var e in aliveList){
      var my = e.y - 64*e.scale - Math.abs(Math.sin(vtime*4+e.phase))*6;
      ctx.save(); ctx.translate(e.x, my); ctx.rotate(Math.sin(vtime*3+e.phase)*0.2);
      ctx.fillStyle = "#d3242b"; ctx.strokeStyle="rgba(20,30,40,0.6)"; ctx.lineWidth=1.5;
      roundRect(ctx,-8,-4,16,8,2); ctx.fill(); ctx.stroke();
      drawStudG(ctx,-3.5,0,1.8,"#8f161d"); drawStudG(ctx,3.5,0,1.8,"#8f161d");
      ctx.restore();
    }
  }
  if(player != null && player.orbit.n>0 && !player.dead){
    var os = Math.min(2, player.orbit.size/9.0);   /* capped visual growth */
    foreach (var op in player.orbit.pos){
      var rr = Js.Or(op.r, player.orbit.r);
      /* a brick that stopped a shot is rebuilding: an outline that fills back in */
      var back = op.broken > 0 ? 1 - op.broken/AEGIS_REFORM : 1;
      ctx.fillStyle = rgba(15,30,20,0.2*back);
      ctx.beginPath(); ctx.ellipse(op.x, player.y + Math.sin(op.ang)*rr*0.72 + 4, 9*os, 3.5*os, 0, 0, TAU); ctx.fill();
      ctx.save();
      ctx.translate(op.x, op.y - 4 + Math.sin(vtime*6+op.ang)*3);
      ctx.rotate(op.ang*1.4);
      ctx.scale(os,os);
      if(op.broken > 0){
        ctx.globalAlpha = 0.25 + 0.35*back;
        ctx.strokeStyle = "#ffd166"; ctx.lineWidth = 1.6; ctx.setLineDash([3,3]);
        roundRect(ctx,-11,-5.5,22,11,2.5); ctx.stroke(); ctx.setLineDash([]);
        ctx.fillStyle = op.outer ? "#e8a13a" : "#d3242b";
        roundRect(ctx,-11,-5.5,22*back,11,2.5); ctx.fill();
        ctx.globalAlpha = 1;
      } else {
        /* the Twin Ring is the gold one, so the two can be told apart at a glance */
        ctx.fillStyle = op.outer ? "#e8a13a" : "#d3242b";
        ctx.strokeStyle = player.orbit.aegis != 0 ? "rgba(255,224,138,0.85)" : "rgba(20,30,40,0.55)";
        ctx.lineWidth = player.orbit.aegis != 0 ? 2.2 : 1.8;
        roundRect(ctx,-11,-5.5,22,11,2.5); ctx.fill(); ctx.stroke();
        var stud = op.outer ? "#9a5f12" : "#8f161d";
        drawStudG(ctx,-5,0,3,stud); drawStudG(ctx,5,0,3,stud);
      }
      ctx.restore();
    }
  }
  foreach (var pr in projectiles){
    if(!onScreen(pr, 90)) continue;
    if(pr.kind=="arrow" && pr.gib) drawGib(ctx, pr);
    else if(pr.kind=="arrow") drawArrow(ctx,pr);
    else if(pr.kind=="skull") drawSkull(ctx,pr);
    else if(pr.kind=="frostbolt") drawFrostbolt(ctx,pr);
    else if(pr.kind=="frostrock") drawFrostRock(ctx,pr);
    else if(pr.kind=="zorb") drawZorb(ctx,pr);
    else if(pr.kind=="batbolt") drawBatBolt(ctx,pr);
    else if(pr.kind=="bolt") drawBolt(ctx,pr);
    else if(pr.kind=="fireball") drawFireball(ctx,pr,false);
    else if(pr.kind=="meteor") drawFireball(ctx,pr,true);
    else if(pr.kind=="hexbolt"){
      var C = CURSES[pr.curse]; var rgb = prs(C.col);
      ctx.save(); ctx.translate(pr.x, pr.y);
      ctx.fillStyle = rgba(rgb[0],rgb[1],rgb[2],0.28);
      ctx.beginPath(); ctx.arc(0,0,11+Math.sin(vtime*11+pr.seed)*2,0,TAU); ctx.fill();
      ctx.rotate(vtime*4 + pr.seed);
      ctx.strokeStyle = C.col; ctx.lineWidth = 2.4; ctx.lineJoin="round";
      ctx.beginPath();
      for(var i=0;i<3;i++){
        var a = i*TAU/3.0;
        if (i != 0) ctx.lineTo(Math.cos(a)*7, Math.sin(a)*7); else ctx.moveTo(Math.cos(a)*7, Math.sin(a)*7);
      }
      ctx.closePath(); ctx.stroke();
      ctx.rotate(-2*(vtime*4 + pr.seed));
      ctx.strokeStyle = "rgba(255,255,255,0.8)"; ctx.lineWidth = 1.6;
      ctx.beginPath();
      for(var i=0;i<3;i++){
        var a = i*TAU/3.0 + Math.PI/3.0;
        if (i != 0) ctx.lineTo(Math.cos(a)*5, Math.sin(a)*5); else ctx.moveTo(Math.cos(a)*5, Math.sin(a)*5);
      }
      ctx.closePath(); ctx.stroke();
      ctx.restore();
    }
    else if(pr.kind=="pumpkin"){
      var grounded = pr.t >= pr.dur;
      var fz = Js.Or(pr.z, 0);
      /* danger ring closes as the fuse burns down */
      if(grounded){
        var k2 = clamp(1 - pr.fuse/3.0, 0, 1);
        var urgent = pr.fuse < 1;
        ctx.fillStyle = rgba(255,140,50,0.07+0.10*k2);
        ctx.beginPath(); ctx.arc(pr.x, pr.y, pr.r, 0, TAU); ctx.fill();
        ctx.setLineDash([14,10]);
        ctx.strokeStyle = rgba(255,242,196,0.55+0.4*Math.sin(vtime*(urgent?30:12)));
        ctx.lineWidth = urgent ? 7 : 5;
        ctx.beginPath(); ctx.arc(pr.x, pr.y, pr.r, 0, TAU); ctx.stroke();
        ctx.setLineDash([]);
        ctx.strokeStyle = "rgba(255,255,255,0.85)"; ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(pr.x, pr.y, Math.max(3, pr.r*(1-k2)), 0, TAU); ctx.stroke();
      }
      ctx.fillStyle = "rgba(15,30,20,0.28)";
      ctx.beginPath(); ctx.ellipse(pr.x, pr.y, 12, 5, 0, 0, TAU); ctx.fill();
      ctx.save(); ctx.translate(pr.x, pr.y - fz - 11);
      if(!grounded) ctx.rotate(pr.spin*0.5);
      /* gourd body */
      ctx.fillStyle = "#e8720f"; ctx.strokeStyle = "rgba(70,30,5,0.65)"; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.ellipse(0, 0, 13, 11, 0, 0, TAU); ctx.fill(); ctx.stroke();
      ctx.fillStyle = "#ff9a3c";
      ctx.beginPath(); ctx.ellipse(-2, -1, 9.5, 10.5, 0, 0, TAU); ctx.fill();
      ctx.strokeStyle = "rgba(140,60,10,0.55)"; ctx.lineWidth = 1.6;
      foreach (var rx in new double[]{-6.5, 0, 6.5}){
        ctx.beginPath(); ctx.ellipse(rx, 0, 3.2, 10.6, 0, 0, TAU); ctx.stroke();
      }
      /* carved grin, lit from inside once it is armed */
      if(grounded){
        var lit = 0.55 + 0.45*Math.sin(vtime*(pr.fuse<1?22:8));
        ctx.fillStyle = rgba(255,241,170,lit);
        ctx.beginPath(); ctx.moveTo(-6,-3); ctx.lineTo(-2.5,1.5); ctx.lineTo(-9,1.5); ctx.closePath(); ctx.fill();
        ctx.beginPath(); ctx.moveTo(6,-3); ctx.lineTo(9,1.5); ctx.lineTo(2.5,1.5); ctx.closePath(); ctx.fill();
        ctx.beginPath();
        ctx.moveTo(-7,4.5); ctx.lineTo(-4,7.5); ctx.lineTo(-1,4.5); ctx.lineTo(2,7.5);
        ctx.lineTo(5,4.5); ctx.lineTo(7,6); ctx.lineTo(-6,6.5); ctx.closePath(); ctx.fill();
      }
      /* stud stem and fuse */
      ctx.fillStyle = "#3d7a2b"; ctx.strokeStyle = "rgba(30,60,20,0.7)"; ctx.lineWidth = 1.6;
      roundRect(ctx, -3.5, -14, 7, 6, 1.5); ctx.fill(); ctx.stroke();
      drawStudG(ctx, 0, -14, 2.6, "#3d7a2b");
      if(grounded){
        ctx.strokeStyle = "#cfd6dd"; ctx.lineWidth = 1.8;
        ctx.beginPath(); ctx.moveTo(0,-16); ctx.quadraticCurveTo(4,-21,2,-25); ctx.stroke();
        var sp = 2 + Math.sin(vtime*26)*1.1;
        ctx.fillStyle = pr.fuse<1 ? "#fff2b0" : "#ffd166";
        ctx.beginPath(); ctx.arc(2,-25,sp,0,TAU); ctx.fill();
      }
      ctx.restore();
    }
    else if(pr.kind=="gas"){
      var full = pr.arc >= BANE_ARC_MAX-0.01;
      var fade = clamp(pr.life/(BANE_WAVE_T+0.55), 0, 1);
      double a0 = pr.a0 - pr.arc/2.0, a1 = pr.a0 + pr.arc/2.0;
      ctx.save();
      /* A GREEN CLOUD ON GREEN GRASS is a cloud nobody can see, so it carries its
         own dark rim and a pale core rather than trusting the ground to contrast. */
      var wedge = ()=>{
        ctx.beginPath();
        if(full) ctx.arc(pr.x, pr.y, Math.max(1,pr.r), 0, TAU);
        else { ctx.moveTo(pr.x, pr.y); ctx.arc(pr.x, pr.y, Math.max(1,pr.r), a0, a1); ctx.closePath(); }
      };
      ctx.fillStyle = rgba(28,58,14,0.15*fade);    /* the shadow it casts */
      wedge(); ctx.fill();
      ctx.fillStyle = rgba(126,214,58,0.17*fade);
      wedge(); ctx.fill();
      /* a thick bright front where the gas is still moving, over a dark liner.
         The FRONT stays readable; it is the body of the cloud that thins out, so
         the spell stops painting over the fight it is winning. */
      ctx.strokeStyle = rgba(24,54,12,0.32*fade); ctx.lineWidth = 13;
      ctx.beginPath(); ctx.arc(pr.x, pr.y, Math.max(1,pr.r), full?0:a0, full?TAU:a1); ctx.stroke();
      ctx.strokeStyle = rgba(190,255,120,0.62*fade); ctx.lineWidth = 7;
      ctx.beginPath(); ctx.arc(pr.x, pr.y, Math.max(1,pr.r), full?0:a0, full?TAU:a1); ctx.stroke();
      /* and lumps of it rolling along, so it reads as gas and not a pie slice */
      for(var i=0;i<11;i++){
        var t = i/11.0;
        var a = full ? pr.seed + t*TAU : a0 + pr.arc*t;
        var rq = pr.r * (0.70 + 0.30*Math.sin(pr.seed + i*2.1 + vtime*5));
        var rad = 13 + 10*Math.sin(pr.seed*2 + i*1.7 + vtime*4);
        double px = pr.x+Math.cos(a)*rq, py = pr.y+Math.sin(a)*rq*0.82;
        ctx.fillStyle = rgba(36,72,18,0.17*fade);
        ctx.beginPath(); ctx.ellipse(px, py, rad+2, rad*0.8+2, 0, 0, TAU); ctx.fill();
        ctx.fillStyle = rgba(158,238,92,0.26*fade);
        ctx.beginPath(); ctx.ellipse(px, py, rad, rad*0.8, 0, 0, TAU); ctx.fill();
        ctx.fillStyle = rgba(214,255,160,0.17*fade);
        ctx.beginPath(); ctx.ellipse(px-rad*0.2, py-rad*0.2, rad*0.45, rad*0.36, 0, 0, TAU); ctx.fill();
      }
      ctx.restore();
    }
    else if(pr.kind=="ember"){
      var R = 4.6 + Math.sin(vtime*26 + pr.seed)*0.8;
      var tail = Math.atan2(pr.vy, pr.vx) + Math.PI;
      ctx.save(); ctx.translate(pr.x, pr.y);
      ctx.fillStyle = "rgba(255,106,26,0.30)";
      ctx.beginPath();
      ctx.moveTo(Math.cos(tail+0.7)*R, Math.sin(tail+0.7)*R);
      ctx.lineTo(Math.cos(tail)*R*3.6, Math.sin(tail)*R*3.6);
      ctx.lineTo(Math.cos(tail-0.7)*R, Math.sin(tail-0.7)*R);
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = "#ff6a1a";
      ctx.beginPath(); ctx.arc(0,0,R,0,TAU); ctx.fill();
      ctx.fillStyle = "#ffd166";
      ctx.beginPath(); ctx.arc(0,0,R*0.5,0,TAU); ctx.fill();
      ctx.restore();
    }
    else if(pr.kind=="pbolt"){
      var puls = 1 + 0.12*Math.sin(vtime*22 + pr.seed);
      var R = 9*puls;
      ctx.save(); ctx.translate(pr.x, pr.y);
      /* a hot core inside a soft coat, with a tail dragged back along its path */
      var tail = Math.atan2(pr.vy, pr.vx) + Math.PI;
      ctx.fillStyle = "rgba(255,120,40,0.28)";
      ctx.beginPath();
      ctx.moveTo(Math.cos(tail+0.5)*R, Math.sin(tail+0.5)*R);
      ctx.lineTo(Math.cos(tail)*R*3.4, Math.sin(tail)*R*3.4);
      ctx.lineTo(Math.cos(tail-0.5)*R, Math.sin(tail-0.5)*R);
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = "rgba(255,138,60,0.45)";
      ctx.beginPath(); ctx.arc(0,0,R*1.6,0,TAU); ctx.fill();
      ctx.fillStyle = "#ff8a3c";
      ctx.beginPath(); ctx.arc(0,0,R,0,TAU); ctx.fill();
      ctx.fillStyle = "#ffd166";
      ctx.beginPath(); ctx.arc(0,0,R*0.55,0,TAU); ctx.fill();
      ctx.fillStyle = "#fff6d8";
      ctx.beginPath(); ctx.arc(-R*0.18,-R*0.18,R*0.26,0,TAU); ctx.fill();
      ctx.restore();
    }
    else if(pr.kind=="mbolt" && pr.arrow){
      /* a bone arrow */
      var a = Math.atan2(pr.vy, pr.vx);
      ctx.save(); ctx.translate(pr.x, pr.y); ctx.rotate(a);
      ctx.strokeStyle = "#ede8ff"; ctx.lineWidth = 2; ctx.lineCap = "round";
      ctx.beginPath(); ctx.moveTo(-16, 0); ctx.lineTo(4, 0); ctx.stroke();
      ctx.fillStyle = "#b7a6ff"; ctx.beginPath(); ctx.moveTo(8, 0); ctx.lineTo(2, -3); ctx.lineTo(2, 3); ctx.closePath(); ctx.fill();
      ctx.restore();
    }
    else if(pr.kind=="mbolt"){
      var col = MAGE_COL[pr.el] ?? "#c9b8ff";
      ctx.fillStyle = col; ctx.globalAlpha = 0.35;
      ctx.beginPath(); ctx.arc(pr.x, pr.y, 9, 0, TAU); ctx.fill(); ctx.globalAlpha = 1;
      ctx.fillStyle = "#fff"; ctx.beginPath(); ctx.arc(pr.x, pr.y, 3.2, 0, TAU); ctx.fill();
      ctx.strokeStyle = col; ctx.lineWidth = 3; ctx.globalAlpha = 0.6;
      ctx.beginPath(); ctx.moveTo(pr.x, pr.y); ctx.lineTo(pr.x - pr.vx*0.03, pr.y - pr.vy*0.03); ctx.stroke();
      ctx.globalAlpha = 1;
    }
    else if(pr.kind=="parrow"){
      /* shaft, fletching and head, coloured by what the arrow has become */
      double a = Math.atan2(pr.vy, pr.vx); var K = pr.el != null ? ARROW_KINDS.GetValueOrDefault(pr.el) : null;
      ctx.save(); ctx.translate(pr.x, pr.y); ctx.rotate(a);
      if(K != null){ ctx.strokeStyle = K.col; ctx.globalAlpha = 0.35; ctx.lineWidth = 5;
             ctx.beginPath(); ctx.moveTo(-26, 0); ctx.lineTo(4, 0); ctx.stroke(); ctx.globalAlpha = 1; }
      ctx.strokeStyle = "#8a6a3e"; ctx.lineWidth = 2.2; ctx.lineCap = "round";
      ctx.beginPath(); ctx.moveTo(-16, 0); ctx.lineTo(7, 0); ctx.stroke();
      ctx.fillStyle = K != null ? K.col : "#c9d2d8"; ctx.strokeStyle = "rgba(20,30,40,0.6)"; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(12, 0); ctx.lineTo(5, -3.4); ctx.lineTo(5, 3.4); ctx.closePath(); ctx.fill(); ctx.stroke();
      ctx.fillStyle = "#f2efe6";
      ctx.beginPath(); ctx.moveTo(-16, 0); ctx.lineTo(-20, -3.6); ctx.lineTo(-13, 0); ctx.lineTo(-20, 3.6); ctx.closePath(); ctx.fill();
      ctx.restore();
    }
    else if(pr.kind=="wblade"){
      ctx.save();
      ctx.translate(pr.x, pr.y);
      ctx.globalAlpha = clamp(pr.life/0.3, 0, 1);
      ctx.rotate(pr.spin);
      drawWeaponArt(ctx, 0.55*pr.size, true, Js.Or(pr.art, thrownArt()));
      ctx.restore();
    }
    else if(pr.kind=="reave"){
      /* the axe itself, spinning, with a short gold streak behind it so you can
         read where it is going before it gets there */
      ctx.save();
      ctx.strokeStyle = "rgba(224,168,26,0.5)"; ctx.lineWidth = 4; ctx.lineCap = "round";
      ctx.beginPath(); ctx.moveTo(pr.px, pr.py); ctx.lineTo(pr.x, pr.y); ctx.stroke();
      ctx.translate(pr.x, pr.y);
      ctx.rotate(pr.spin);
      drawWeaponArt(ctx, 0.78, true, "axe");
      ctx.restore();
    }
    else if(LOB_CONF.ContainsKey(pr.kind ?? "") || pr.kind=="bomb"){
      var lc = LOB_CONF.GetValueOrDefault(pr.kind ?? "") ?? new LobConf {sc = 1, cols = new Col[]{PAL.red, PAL.dred}};
      /* the hero's bomb is bleached gold so it can never be mistaken for one of
         the deep-ember firebombs raining down on the same patch of floor */
      var cols = pr.kind=="bomb"
        ? (pr.gen != 0 ? new Col[] {"#fff6d8", FX.friend.brickStud} : new Col[] {FX.friend.brick, FX.friend.ringHot})
        : lc.cols;
      var sc = (Js.Or(lc.sc, 1)) * (pr.kind=="bomb" ? Math.pow(0.7, Js.Or(pr.gen, 0)) : 1);
      ctx.fillStyle="rgba(15,30,20,0.25)";
      ctx.beginPath(); ctx.ellipse(pr.x, pr.y, 10*sc, 4*sc, 0, 0, TAU); ctx.fill();
      ctx.save(); ctx.translate(pr.x, pr.y - (Js.Or(pr.z, 10))); ctx.rotate(pr.spin); ctx.scale(sc,sc);
      ctx.fillStyle = cols[0]; ctx.strokeStyle="rgba(20,30,40,0.5)"; ctx.lineWidth=2;
      roundRect(ctx,-14,-8,28,16,2); ctx.fill(); ctx.stroke();
      for(var r2=0;r2<2;r2++) for(var c2=0;c2<4;c2++)
        drawStudG(ctx, -10.5+c2*7, -3.5+r2*7, 2.6, cols[1]);
      ctx.restore();
    }
  }
  foreach (var d in debris) if(onScreen(d, 30)) drawDebris(ctx,d);
  foreach (var p in particles){
    /* a bolt can be 340px of storm hanging off its own midpoint, so it is culled
       by its whole span and not by its centre */
    if(p.kind=="zap"){
      var half = Math.max(Math.abs(p.x2-p.x1), Math.abs(p.y2-p.y1))*0.5;
      if(!onScreen(p, 40+half)) continue;
    } else if(!onScreen(p, 40)) continue;
    var k = clamp(p.life/Js.Or(p.maxLife, p.max),0,1);
    if(p.kind=="slash"){
      /* a swing: a bright arc across where the weapon passed */
      ctx.strokeStyle = p.color; ctx.globalAlpha = 0.85*k; ctx.lineWidth = 4*k + 1.5; ctx.lineCap = "round";
      ctx.beginPath(); ctx.arc(p.x, p.y, p.r, p.a - 0.95, p.a + 0.95); ctx.stroke(); ctx.globalAlpha = 1;
    } else if(p.kind=="ring"){
      ctx.strokeStyle = p.color; ctx.globalAlpha = k; ctx.lineWidth = Math.max(0.5,p.lw*k+1);
      ctx.beginPath(); ctx.arc(p.x,p.y,Math.max(0.5,p.r),0,TAU); ctx.stroke(); ctx.globalAlpha = 1;
    } else if(p.kind=="dust"){
      ctx.fillStyle = p.col.WithAlpha(k*0.5);
      ctx.beginPath(); ctx.arc(p.x,p.y,Math.max(0.5,p.r*(2-k)),0,TAU); ctx.fill();
    } else if(p.kind=="zap"){
      ctx.globalAlpha = k;
      var ZC = p.hostile ? FX.foe : FX.friend;
      ctx.strokeStyle = ZC.zap; ctx.lineWidth = Math.max(1.2, 2.8*Math.pow(0.8,Js.Or(p.link, 0)));
      ctx.beginPath();
      var segs = 6;
      ctx.moveTo(p.x1,p.y1);
      for(var i2=1;i2<segs;i2++){
        var t2 = (double)i2/segs;
        ctx.lineTo(
          lerp(p.x1,p.x2,t2) + Math.cos(p.seed+i2*2.7)*7,
          lerp(p.y1,p.y2,t2) + Math.sin(p.seed+i2*3.1)*7);
      }
      ctx.lineTo(p.x2,p.y2);
      ctx.stroke();
      ctx.strokeStyle = p.hostile ? "rgba(255,233,90,0.9)" : "rgba(255,255,255,0.9)";
      ctx.lineWidth = 1; ctx.stroke();
      ctx.fillStyle = ZC.zapCore;
      ctx.beginPath(); ctx.arc(p.x2,p.y2,3*k+1,0,TAU); ctx.fill();
      ctx.globalAlpha = 1;
    } else {
      ctx.strokeStyle = p.color; ctx.globalAlpha = k; ctx.lineWidth=2;
      ctx.beginPath(); ctx.moveTo(p.x,p.y); ctx.lineTo(p.x-p.vx*0.04,p.y-p.vy*0.04); ctx.stroke();
      ctx.globalAlpha = 1;
    }
  }
  drawCaveLight();
  drawRitualFog();
  ctx.textAlign="center";
  foreach (var t in dmgTexts){
    if(!onScreen(t, 60)) continue;
    var k = clamp(t.life/0.8,0,1);
    ctx.font = $"700 {S(t.size)}px Fredoka";
    ctx.globalAlpha = k;
    ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4; ctx.strokeText(t.txt,t.x,t.y);
    ctx.fillStyle=t.color; ctx.fillText(t.txt,t.x,t.y);
    ctx.globalAlpha = 1;
  }
  drawLegionIce();
  drawBloodOrbs();
  drawMonsterMods();
  drawRpgWorld(ctx);
  drawGearLabels();
  ctx.restore();
  drawMapOverlay();
  renderHUD();
  if(state=="play" && player != null && !player.dead){
    if(markFew) foreach (var e in aliveList)
      edgeMarker(e.x, e.y, "#ff5a4a", (x,y)=>{
        ctx.save(); ctx.translate(x,y); ctx.rotate(0.15);
        ctx.fillStyle="#d3242b";
        roundRect(ctx,-7,-4,14,8,2); ctx.fill();
        drawStudG(ctx,-3,0,1.6,"#8f161d"); drawStudG(ctx,3,0,1.6,"#8f161d");
        ctx.restore();
      });
    /* Everything worth walking towards gets a marker on the rim: the chests you
       cannot see, and every buff brick lying in the dark. Distance is written on
       it so you can decide whether it is worth the trip. */
    foreach (var pr in props)
      if(pr.type=="chest" && !pr.dead)
        edgeMarker(pr.x, pr.y, CHEST_RAR_COL(pr.rar), (x,y)=> drawChestGlyph(x, y), true);
    foreach (var pk in pickups){
      if(pk.got) continue;
      if(pk.kind=="magnet")
        edgeMarker(pk.x, pk.y, "#9b45d6", (x,y)=> drawMagnetIcon(x,y), true);
      else if(pk.kind=="fortune")
        edgeMarker(pk.x, pk.y, "#fe8a18", (x,y)=> drawFortuneIcon(x,y,2), true);
      else if(pk.kind=="rage")
        edgeMarker(pk.x, pk.y, "#ff5a4a", (x,y)=> drawRageIcon(x,y,2), true);
      else if(pk.kind=="ward")
        edgeMarker(pk.x, pk.y, "#8fd4ff", (x,y)=> drawWardIcon(x,y,1), true);
      else if(pk.kind=="gheart")
        edgeMarker(pk.x, pk.y, "#ffd166", (x,y)=>{
          ctx.save(); ctx.translate(x,y); ctx.scale(0.62,0.62);
          heartPath(ctx,0,0,10); ctx.fillStyle="#ffd166"; ctx.fill();
          ctx.strokeStyle="rgba(20,30,40,0.6)"; ctx.lineWidth=3; ctx.stroke();
          ctx.restore();
        }, true);
    }
    /* an unopened breach is worth walking to, so it is on the rim like a chest */
    foreach (var B in allBreaches()) if(B.state == "hand"){
      var col = BREACH_LORDS[B.lord].col;
      edgeMarker(B.x, B.y, col, (x,y)=> drawBreachHand(ctx, x, y+9, col, 0.5, false, 0.36), true);
    }
    /* ...and so is an unopened abyss: its green eye */
    /* a locked strongbox is a prize like a chest */
    foreach (var box in sboxLive()) if(!box.sprung){
      var col = (SBOX_TIERS.GetValueOrDefault(box.tier ?? "") ?? SBOX_TIERS["plain"]).col;
      edgeMarker(box.x, box.y, col, (x,y)=>{ ctx.fillStyle = "#3d4248"; ctx.strokeStyle = col; ctx.lineWidth = 1.5;
        roundRect(ctx, x - 8, y - 5, 16, 11, 2); ctx.fill(); ctx.stroke(); }, true);
    }
    /* a rare on the floor is worth the walk */
    foreach (var d in gearDrops) if(d.it.r == "rare")
      edgeMarker(d.x, d.y, "#ffff77", (x,y)=>{ ctx.fillStyle = "#ffff77"; ctx.strokeStyle = "rgba(20,20,10,0.7)"; ctx.lineWidth = 1.5;
        ctx.beginPath(); ctx.moveTo(x, y - 7); ctx.lineTo(x + 6, y); ctx.lineTo(x, y + 7); ctx.lineTo(x - 6, y); ctx.closePath(); ctx.fill(); ctx.stroke(); }, true);
    /* a ritual altar that still has something to give */
    foreach (var A in ritualS.altars) if(A.state == "guarded" || A.state == "ready")
      edgeMarker(A.x, A.y, RITUAL_COL, (x,y)=>{ ctx.fillStyle = A.state == "ready" ? RITUAL_COL : "#6a1a24"; ctx.strokeStyle = "rgba(20,4,8,0.8)"; ctx.lineWidth = 1.5;
        ctx.beginPath(); ctx.moveTo(x, y - 8); ctx.lineTo(x + 5, y); ctx.lineTo(x, y + 7); ctx.lineTo(x - 5, y); ctx.closePath(); ctx.fill(); ctx.stroke(); }, true);
    foreach (var A in allAbysses()) if(A.state == "idle")
      edgeMarker(A.x, A.y, ABYSS_COL, (x,y)=>{ ctx.fillStyle = "#06110a"; ctx.beginPath(); ctx.ellipse(x, y, 11, 6, 0, 0, TAU); ctx.fill();
        ctx.fillStyle = ABYSS_COL; ctx.beginPath(); ctx.ellipse(x, y, 4, 3, 0, 0, TAU); ctx.fill(); }, true);
  }
  var vg = ctx.createRadialGradient(cw/2.0, ch/2.0, Math.min(cw,ch)*0.36, cw/2.0, ch/2.0, Math.max(cw,ch)*0.72);
  vg.addColorStop(0,"rgba(8,14,18,0)"); vg.addColorStop(1,"rgba(8,14,18,0.36)");
  ctx.fillStyle = vg; ctx.fillRect(0,0,cw,ch);
  if(player != null && player.slowT>0 && !player.dead){
    ctx.fillStyle = "rgba(140,200,255,0.10)";
    ctx.fillRect(0,0,cw,ch);
  }
  if(redFlash>0){
    ctx.fillStyle = rgba(200,30,30,redFlash*0.25);
    ctx.fillRect(0,0,cw,ch);
    redFlash = Math.max(0, redFlash-0.03);
  }
  if(player != null && !player.dead && lowLife() && state=="play"){
    var pl = (Math.sin(vtime*5)*0.5+0.5)*0.12;
    ctx.fillStyle = rgba(200,30,30,pl); ctx.fillRect(0,0,cw,ch);
  }
  if(state=="play") renderReticle();
}
/* A ring pinned to the edge of the screen pointing at something off it. Used
   for the last few monsters of a wave and for every prize lying out of sight —
   a chest you never saw is a chest you never opened. */
public static Col CHEST_RAR_COL(string rar) => RARITY.GetValueOrDefault(rar ?? "")?.col ?? "#c78c00";
public static void drawChestGlyph(double x, double y){
  ctx.save(); ctx.translate(x, y);
  ctx.fillStyle = "#8a5a2b"; ctx.strokeStyle = "rgba(20,30,40,0.6)"; ctx.lineWidth = 1.6;
  roundRect(ctx, -9, -2, 18, 10, 2); ctx.fill(); ctx.stroke();
  ctx.fillStyle = "#a86c33";
  ctx.beginPath(); ctx.moveTo(-9,-2); ctx.quadraticCurveTo(0,-12, 9,-2); ctx.closePath();
  ctx.fill(); ctx.stroke();
  ctx.fillStyle = PAL.gold;
  ctx.fillRect(-10, -3, 20, 2.6);
  ctx.beginPath(); ctx.arc(0, 2.5, 2.2, 0, TAU); ctx.fill();
  ctx.restore();
}
public static void edgeMarker(double wx, double wy, Col col, Action<double, double> glyph, bool showDist = false){
  var sx2 = (wx - cam.x)*zoom + cw/2.0;
  var sy2 = (wy - cam.y)*zoom + ch/2.0;
  if(sx2 > -10 && sx2 < cw+10 && sy2 > -10 && sy2 < ch+10) return;
  /* the rim it pins to keeps clear of the HUD: hearts and the wave title along
     the top, the ability buttons along the bottom */
  double pad = 48, padTop = 96, padBot = 100;
  var cxm = clamp(sx2, pad, cw-pad);
  var cym = clamp(sy2, padTop, ch-padBot);
  var ang = Math.atan2(sy2-cym, sx2-cxm);
  var pr4 = prs(col);
  ctx.save(); ctx.translate(cxm, cym);
  ctx.fillStyle="rgba(15,20,25,0.65)";
  ctx.beginPath(); ctx.arc(0,0,17,0,TAU); ctx.fill();
  ctx.strokeStyle=rgba(pr4[0],pr4[1],pr4[2],0.9); ctx.lineWidth=2; ctx.stroke();
  try { glyph(0, 0); } catch {}
  ctx.save();
  ctx.rotate(ang);
  ctx.fillStyle = col;
  ctx.beginPath();
  ctx.moveTo(24,0); ctx.lineTo(13,-7); ctx.lineTo(13,7);
  ctx.closePath(); ctx.fill();
  ctx.restore();
  if(showDist && player != null){
    var d = Math.round(Math.hypot(wx-player.x, wy-player.y));
    ctx.font = "700 10px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 3;
    ctx.strokeText(d+"m", 0, 25);
    ctx.fillStyle = col; ctx.fillText(d+"m", 0, 25);
  }
  ctx.restore();
}
/* THE SHIELD YOU CAN SEE. A bar in the corner is not where your eyes are when
   something is winding up at you, and a boss's shield track is 300px from the
   boss. So the pool is drawn ON the body that owns it, and HOW MUCH IS LEFT is
   HOW SOLID IT IS: full and it is a bright dome, half spent and it is a hint,
   empty and there is nothing there at all. You read the number without reading
   anything, which is the only way to read something mid-fight.

   Faint on purpose. This sits on top of a body you are trying to watch, and a
   shield you cannot see through hides the wind-up it is there to survive --
   the same reason the mace's teeth went translucent. The RIM is what you read;
   the fill is barely there. */
public static readonly Col ES_RIM = "#dff2ff";
public const double ES_BUBBLE_PAD = 11;
public static void drawShieldBubble(Ctx c, Ent o, double frac, double flash){
  var f = clamp(Js.Or(frac, 0), 0, 1);
  if(!(f > 0.001)) return;        /* spent is GONE, not faint */
  var hit = clamp(Js.Or(flash, 0), 0, 1);
  /* WRAPPED ROUND THE BODY THE GAME ALREADY MODELS. bodyLo/bodyHi/bodyRad are
     what every swing and every projectile in the game is tested against, so
     using them here means the bubble is drawn exactly where the thing it is
     protecting can be struck -- and it fits a golem, a minifig and a bat
     without any of the three being measured by hand.
     `drawZ` is the one thing those cannot know: a bat is HIT as though it were
     standing on the floor, on purpose, but it is DRAWN in the air. */
  var lift = Js.Or(o.drawZ, 0);
  double lo = bodyLo(o) - lift, hi = bodyHi(o) - lift;
  var pad = ES_BUBBLE_PAD * Math.max(1, Math.sqrt(Js.Or(o.scale, 1)));
  /* it breathes, so a full shield is alive rather than a decal */
  var wob = 1 + 0.03*Math.sin(vtime*2.4 + (o.x + o.y)*0.02);
  double x = o.x, cy = (lo + hi)/2.0;
  double rx = (bodyRad(o) + pad) * wob, ry = ((lo - hi)/2.0 + pad) * wob;
  c.save();
  c.fillStyle = ES_COL;
  c.globalAlpha = (0.13 + 0.22*hit) * f;
  c.beginPath(); c.ellipse(x, cy, rx, ry, 0, 0, TAU); c.fill();
  c.strokeStyle = ES_RIM;
  c.globalAlpha = (0.62 + 0.38*hit) * f;
  c.lineWidth = 1.8 + 1.2*hit;
  c.beginPath(); c.ellipse(x, cy, rx, ry, 0, 0, TAU); c.stroke();
  /* one highlight up and to the left, so it reads as glass and not as a ring */
  c.globalAlpha = 0.5 * f;
  c.lineWidth = 2;
  c.beginPath(); c.ellipse(x, cy, rx*0.78, ry*0.78, 0, Math.PI*1.08, Math.PI*1.42); c.stroke();
  c.globalAlpha = 1;
  c.restore();
}
public static void drawHero(Ctx c){
  var p = player;
  if(p == null || p.dead) return;
  if(p.invuln>0 && Math.sin(vtime*30)>0) c.globalAlpha = 0.45;
  var sw = p.swing;
  var wh = p.whirl ? p.whirlAng : double.NaN;
  /* THE SLAM POSE. 0 is the mace overhead, 1 is it buried in the floor — the
     same ramp the bosses use, so the hero's slam reads as the thing they have
     been dodging all run. It holds at 1 for a beat while the fault runs out. */
  var sa = p.slamAct;
  var slam = sa != null ? (sa.t < SLAM_WIND ? sa.t/SLAM_WIND : 1)
             : (p.slamPose > 0 ? 1 : double.NaN);
  drawFigure(c, new Fig { x = p.x, y = p.y, s = 1,
    /* facing rides the spin, so the minifig turns its back to you and round again */
    face = !double.IsNaN(wh) ? new Pt(Math.cos(wh), Math.sin(wh))
        : sa != null ? new Pt(Math.cos(sa.aim), Math.sin(sa.aim))
        : p.slamPose > 0 ? new Pt(Math.cos(p.slamAim), Math.sin(p.slamAim)) : p.face,
    walk = p.walk, moving = p.moving, slam = slam,
    flash = p.flash, blink = p.blink, colors = HERO_COLORS, style = "hero",
    hero = true, weapon = heldArt(), bladeAng = (double.IsNaN(wh) && sw != null) ? sw.cur : double.NaN,
    whirlA = wh, elite = false, phase = 0, wScale = heroWeaponScale() });
  c.globalAlpha = 1;
  drawPlayerWeapon(c);
  if(p.esMax > 0) drawShieldBubble(c, p, p.es/p.esMax, p.esFlash);
  drawHeroStacks(c, p);
}
/* WHAT YOU ARE CARRYING, over your own head. A ramp you cannot see is a ramp
   you cannot play around, so every live stack is one chevron in its own colour,
   and the one nearest falling off fades as its clock runs down. Written against
   the TABLE, so a second kind of stack is drawn by existing. */
public static void drawHeroStacks(Ctx c, Ent p){
  if(p == null || p.stacks == null) return;
  var rows = new List<(string id, StackDef d, double n, double t)>();
  foreach (var id in p.stacks.Keys){
    var n = stackOf(id);
    if(n > 0) rows.Add((id, stackDef(id), n, p.stacks[id].t));
  }
  if(rows.Count == 0) return;
  c.save();
  var y = p.y - 62;
  foreach (var r in rows){
    if(r.d.frac){
      /* A COUNTER, NOT A COUNT. Thirty chevrons is a fence; a bar and a number
         is a thing you can read while something is chasing you — and it drains
         visibly the moment the grace runs out, which is the mechanic. */
      double barW = 46, h = 5, bx = p.x - barW/2.0;
      var full = Math.max(1, stackCap(r.id, p));
      c.globalAlpha = 0.85;
      c.fillStyle = "rgba(8,12,16,0.62)";
      roundRect(c, bx-1, y-2, barW+2, h+2, 3); c.fill();
      c.fillStyle = r.d.col;
      roundRect(c, bx, y-1, Math.max(1, barW*clamp(r.n/full, 0, 1)), h, 2); c.fill();
      c.globalAlpha = 1;
      c.font = "700 8px Fredoka"; c.textAlign = "center"; c.textBaseline = "middle";
      c.fillStyle = r.d.col;
      c.fillText(Math.round(r.n)+"", p.x, y - 7);
      y -= 15;
      continue;
    }
    double bw = 7, gap = 3, total = r.n*bw + (r.n-1)*gap;
    for(var i=0;i<r.n;i++){
      /* the last one is the one about to go, and it says so */
      var fading = (i == r.n-1) ? clamp(r.t / Math.max(0.01, r.d.decay), 0.25, 1) : 1;
      var x = p.x - total/2.0 + i*(bw+gap);
      c.globalAlpha = 0.9*fading;
      c.fillStyle = r.d.col;
      c.beginPath();
      c.moveTo(x, y+4); c.lineTo(x + bw/2.0, y - 3); c.lineTo(x + bw, y + 4);
      c.lineTo(x + bw/2.0, y + 1); c.closePath(); c.fill();
    }
    y -= 10;
  }
  c.globalAlpha = 1;
  c.restore();
}
/* A shade: a hooded minifig torso with nothing under it but a wisp. It hovers,
   so it gets a small floating offset and a faint pool of shadow instead of feet.
   The storm-touched one crackles yellow instead of violet. */
public static void drawShade(Ctx c, Ent e){
  var s = e.scale * 1.05;
  var storm = e.zorb;
  string glow = storm ? "255,233,90" : "155,69,214";
  var hov = 6 + Math.sin(vtime*2.2 + e.phase)*3;
  var fl = e.flash > 0 ? Math.min(1, e.flash*0.85) : 0;
  Col T(Col col) => fl != 0 ? mixc(col, "#ffffff", fl) : col;
  var dir = (e.face != null && e.face.x < 0) ? -1 : 1;
  c.save();
  /* it casts almost nothing — it is not standing on the ground */
  c.fillStyle = "rgba(15,20,30,0.22)";
  c.beginPath(); c.ellipse(e.x, e.y+2, 11*s, 4*s, 0, 0, TAU); c.fill();
  c.fillStyle = rgbaS(glow, 0.10+0.05*Math.sin(vtime*3+e.phase));
  c.beginPath(); c.ellipse(e.x, e.y-18*s-hov, 20*s, 24*s, 0, 0, TAU); c.fill();
  c.translate(e.x, e.y - hov);
  c.scale(dir*s, s);
  c.strokeStyle = "rgba(10,8,18,0.65)"; c.lineWidth = 1.8; c.lineJoin = "round";
  /* the wisp where the legs should be — three tapering tatters */
  for(var i=0;i<3;i++){
    var sw2 = Math.sin(vtime*3.4 + e.phase + i*1.9)*3.5;
    c.fillStyle = rgbaS(storm?"90,82,30":"58,48,86", 0.75 - i*0.18);
    c.beginPath();
    c.moveTo(-7+i*5, -14);
    c.lineTo(-3+i*5 + sw2, -2 + i*1.5);
    c.lineTo(-1+i*5 + sw2*1.4, 1 + i*1.5);
    c.lineTo(-9+i*5, -13);
    c.closePath(); c.fill();
  }
  /* torso: a cloak-shaped brick */
  c.fillStyle = T(e.col.torso);
  c.beginPath();
  c.moveTo(-9, -14); c.lineTo(9, -14); c.lineTo(7.5, -30); c.lineTo(-7.5, -30);
  c.closePath(); c.fill(); c.stroke();
  drawStudG(c, -3.5, -29, 2.6, T(e.col.legs));
  drawStudG(c, 3.5, -29, 2.6, T(e.col.legs));
  /* sleeves, drifting rather than swinging */
  foreach (var side in new double[]{-1, 1}){
    var a = side*(0.9 + Math.sin(vtime*2.6 + e.phase + (side>0?0:1.4))*0.22);
    c.save(); c.translate(side*8, -27); c.rotate(a);
    c.fillStyle = T(e.col.torso);
    roundRect(c, -3, -2.5, 13, 6, 2.5); c.fill(); c.stroke();
    c.fillStyle = T(e.col.hands);
    c.beginPath(); c.arc(11, 0, 3.4, 0, TAU); c.fill(); c.stroke();
    c.restore();
  }
  /* hood, with two pinpricks of light where a face should be */
  c.fillStyle = T(e.col.hair);
  c.beginPath();
  c.moveTo(-8.5, -30); c.lineTo(8.5, -30); c.lineTo(9.5, -41);
  c.quadraticCurveTo(0, -50, -9.5, -41);
  c.closePath(); c.fill(); c.stroke();
  c.fillStyle = "rgba(8,6,14,0.92)";
  roundRect(c, -6, -40, 12, 8, 3); c.fill();
  var eg = 0.55 + 0.45*Math.sin(vtime*4.5 + e.phase);
  c.fillStyle = rgbaS(glow, 0.35*eg);
  c.beginPath(); c.arc(-2.6,-36,4.5,0,TAU); c.arc(2.6,-36,4.5,0,TAU); c.fill();
  c.fillStyle = storm ? "#fff3a0" : "#d6a6ff";
  c.beginPath(); c.arc(-2.6,-36,1.5,0,TAU); c.arc(2.6,-36,1.5,0,TAU); c.fill();
  /* the storm one wears a crackle of lightning round the hood */
  if(storm){
    c.strokeStyle = rgba(255,233,90,0.5+0.4*Math.sin(vtime*13+e.phase));
    c.lineWidth = 1.6;
    c.beginPath();
    c.moveTo(-11,-44); c.lineTo(-6,-39); c.lineTo(-10,-35);
    c.moveTo(11,-45); c.lineTo(7,-40); c.lineTo(12,-36);
    c.stroke();
  }
  if(e.elite){ c.strokeStyle="rgba(255,185,0,0.8)"; c.lineWidth=2.2;
    c.beginPath(); c.ellipse(0,1,15,6,0,0,TAU); c.stroke(); }
  if(e.magic){ c.setLineDash([5,4]); c.strokeStyle=MAGIC_RING; c.lineWidth=2.2;
    c.beginPath(); c.ellipse(0,1,16,6.5,0,0,TAU); c.stroke(); c.setLineDash([]); }
  c.restore();
}
/* THE DREAD ROOST is a BAT, not a minifig that happens to have wings. No legs,
   no arms, no helmet: a furred body slung between two huge membrane wings, ears
   up, snout down, claws tucked. Everything hangs off the hover bob, because it
   never touches the ground. The wings spread wide to screech out a summon and
   fold into a spearhead when it dives. */
public static void drawBatBoss(Ctx c, Ent e){
  var s = (Js.Or(e.scale, 1)) * 1.06;
  var fl = e.flash>0 ? Math.min(1, e.flash*0.85) : 0;
  Col T(Col col) => fl != 0 ? mixc(col,"#ffffff",fl) : col;
  double hov = Js.Or(e.hover, 0), ph = Js.Or(e.phase, 0);
  var diving  = (e.mode=="swoop" || e.mode=="swoopWind");
  var casting = (e.mode=="roostWind"||e.mode=="hexWind"||e.mode=="batsWind");
  /* the beat: frantic on the dive, slow and heavy while it screeches */
  var beat = diving ? 18 : (casting ? 5.5 : 8.5);
  var flap = Math.sin(hov*beat + ph);
  /* how far the wings are thrown open, and how far back they are swept */
  var sp    = diving ? 0.60 : (casting ? 1.14 : 1);
  var sweep = diving ? 0.62 : (casting ? -0.18 : 0);
  var z = (diving ? 30 : 22) + Math.sin(hov*2.4 + ph)*6 + flap*4;
  /* how far off the floor this thing is DRAWN. Nothing that hits it reads this
     -- a bat is struck as though it stood on the ground, which is deliberate --
     but anything drawn around its body has to find the body. */
  e.drawZ = z;
  var dir = (e.face != null && e.face.x < 0) ? -1 : 1;
  var hi = casting ? 1 : 0;

  /* it is high up, so the shadow is small, soft and lags under the body */
  c.fillStyle = "rgba(15,20,30,0.26)";
  c.beginPath(); c.ellipse(e.x, e.y, 22*s - z*0.18, 8*s - z*0.05, 0, 0, TAU); c.fill();
  if(e.elite){ c.strokeStyle="rgba(255,185,0,0.75)"; c.lineWidth=2.4;
    c.beginPath(); c.ellipse(e.x,e.y,26*s,9.5*s,0,0,TAU); c.stroke(); }
  if(e.magic){ c.setLineDash([6,5]); c.strokeStyle=MAGIC_RING; c.lineWidth=2.4;
    c.beginPath(); c.ellipse(e.x,e.y,27*s,10*s,0,0,TAU); c.stroke(); c.setLineDash([]); }
  /* the violet haze it hunts inside */
  c.fillStyle = rgba(181,143,214,0.09 + 0.05*Math.sin(hov*3+ph) + hi*0.07);
  c.beginPath(); c.ellipse(e.x, e.y - z - 26*s, 54*s, 36*s, 0, 0, TAU); c.fill();

  c.save(); c.translate(e.x, e.y - z); c.scale(dir*s, s);
  c.lineJoin = "round"; c.lineCap = "round";

  /* ---- the wings, both behind the body ---- */
  foreach (var side in new double[]{-1, 1}){
    c.save();
    c.translate(side*7, -40);
    /* the far wing sits a touch higher and darker, so the pair reads as depth */
    var far = (side*dir < 0);
    c.rotate(side*(flap*0.34 - 0.12 + sweep));
    c.scale(sp, 1);
    var tipY = -20 + flap*9;
    var mem = far ? "#2e2740" : "#3c3454";
    c.fillStyle = T(mem);
    c.strokeStyle = "rgba(12,9,20,0.7)"; c.lineWidth = 2.2;
    /* leading edge out to the tip, then the scalloped trailing edge home */
    c.beginPath();
    c.moveTo(0, -4);
    c.quadraticCurveTo(side*34, tipY - 22, side*70, tipY);
    c.quadraticCurveTo(side*56, tipY + 16, side*50, tipY + 6);
    c.quadraticCurveTo(side*38, tipY + 30, side*33, tipY + 14);
    c.quadraticCurveTo(side*22, tipY + 38, side*18, tipY + 22);
    c.quadraticCurveTo(side*9, tipY + 34, 0, 14);
    c.closePath(); c.fill(); c.stroke();
    /* finger bones fanning out of the shoulder to every scallop */
    c.strokeStyle = far ? "rgba(120,104,150,0.5)" : "rgba(146,128,180,0.72)";
    c.lineWidth = 2.4;
    c.beginPath();
    c.moveTo(side*2, -2); c.quadraticCurveTo(side*36, tipY - 16, side*69, tipY + 1);
    c.moveTo(side*2, 0);  c.lineTo(side*49, tipY + 5);
    c.moveTo(side*2, 2);  c.lineTo(side*32, tipY + 13);
    c.moveTo(side*2, 4);  c.lineTo(side*17, tipY + 21);
    c.stroke();
    /* the thumb claw at the wrist */
    c.fillStyle = T("#d8d2e6");
    c.beginPath();
    c.moveTo(side*44, tipY - 11); c.lineTo(side*53, tipY - 17); c.lineTo(side*46, tipY - 5);
    c.closePath(); c.fill();
    c.restore();
  }

  /* ---- the body: furred, hunched, no legs ---- */
  c.strokeStyle = "rgba(12,9,20,0.75)"; c.lineWidth = 2.4;
  c.fillStyle = T("#4a3f5e");
  c.beginPath(); c.ellipse(0, -34, 15, 20, 0, 0, TAU); c.fill(); c.stroke();
  /* the chest ruff — a paler collar of fur */
  c.fillStyle = T("#6b5c85");
  c.beginPath();
  c.moveTo(-13, -40);
  c.quadraticCurveTo(0, -26, 13, -40);
  c.quadraticCurveTo(0, -34, -13, -40);
  c.closePath(); c.fill();
  /* tufts of fur down the belly */
  c.strokeStyle = "rgba(30,22,45,0.5)"; c.lineWidth = 1.6;
  c.beginPath();
  c.moveTo(-6,-30); c.lineTo(-4,-22); c.moveTo(0,-29); c.lineTo(0,-20);
  c.moveTo(6,-30); c.lineTo(4,-22);
  c.stroke();
  /* the hind claws, curled up under it the way a hanging bat holds them */
  c.strokeStyle = "rgba(12,9,20,0.7)"; c.lineWidth = 2.2;
  foreach (var side in new double[]{-1, 1}){
    var kick = diving ? 6 : Math.sin(hov*2.4 + ph + (side>0?0:1.1))*2.5;
    c.fillStyle = T("#3a3350");
    c.beginPath();
    c.moveTo(side*6, -20); c.quadraticCurveTo(side*10, -13 + kick, side*7, -8 + kick);
    c.quadraticCurveTo(side*4, -12 + kick, side*3, -19);
    c.closePath(); c.fill(); c.stroke();
    c.strokeStyle = T("#d8d2e6"); c.lineWidth = 1.8;
    c.beginPath();
    c.moveTo(side*7, -9 + kick); c.lineTo(side*10, -5 + kick);
    c.moveTo(side*5, -9 + kick); c.lineTo(side*5, -4 + kick);
    c.stroke();
    c.strokeStyle = "rgba(12,9,20,0.7)"; c.lineWidth = 2.2;
  }

  /* ---- the head: ears up, snout out, no helmet in sight ---- */
  var nod = diving ? 5 : Math.sin(hov*2.4 + ph)*1.6;
  c.save(); c.translate(0, -56 + nod);
  /* ears, twitching independently */
  foreach (var side in new double[]{-1, 1}){
    var tw = Math.sin(hov*3.3 + ph + (side>0?0:2.1))*0.09;
    c.save(); c.rotate(side*(0.34) + tw);
    c.fillStyle = T("#4a3f5e");
    c.beginPath();
    c.moveTo(side*2, -4); c.quadraticCurveTo(side*13, -16, side*9, -30);
    c.quadraticCurveTo(side*2, -20, side*(-1), -5);
    c.closePath(); c.fill(); c.stroke();
    /* the pink inner ear */
    c.fillStyle = T("#8d6fae");
    c.beginPath();
    c.moveTo(side*3, -7); c.quadraticCurveTo(side*10, -16, side*8, -25);
    c.quadraticCurveTo(side*4, -18, side*2, -8);
    c.closePath(); c.fill();
    c.restore();
  }
  /* skull */
  c.fillStyle = T("#5a4f70");
  c.beginPath(); c.ellipse(0, 0, 13, 11, 0, 0, TAU); c.fill(); c.stroke();
  /* snout, pushed forward so it reads as a muzzle and not a face plate */
  c.fillStyle = T("#6b5c85");
  c.beginPath(); c.ellipse(3, 5, 8.5, 6.5, 0.1, 0, TAU); c.fill(); c.stroke();
  c.fillStyle = "rgba(20,12,30,0.85)";
  c.beginPath(); c.ellipse(5.5, 2.5, 2.4, 1.7, 0.3, 0, TAU); c.fill();
  /* eyes: two coals, wide open on the dive */
  var eg = 0.6 + 0.4*Math.sin(vtime*5 + ph) + (diving?0.4:0);
  c.fillStyle = rgba(255,59,48,0.3*Math.min(1,eg));
  c.beginPath(); c.arc(-4.5,-2,6,0,TAU); c.arc(5,-3,6,0,TAU); c.fill();
  c.fillStyle = T("#ff3b30");
  c.beginPath(); c.ellipse(-4.5,-2, 3, diving?3:2.4, 0, 0, TAU);
  c.ellipse(5,-3, 3, diving?3:2.4, 0, 0, TAU); c.fill();
  c.fillStyle = "rgba(255,220,210,0.9)";
  c.beginPath(); c.arc(-5.4,-3,1,0,TAU); c.arc(4.1,-4,1,0,TAU); c.fill();
  /* the mouth: shut while it hunts, thrown open to screech */
  if(casting || diving){
    c.fillStyle = "rgba(28,10,20,0.95)";
    c.beginPath(); c.ellipse(3, 8, 5.5, 4 + (casting?1.6*Math.abs(flap):0), 0, 0, TAU);
    c.fill(); c.stroke();
  }
  c.fillStyle = "#eef0ee";
  c.beginPath(); c.moveTo(-1.5, 8.5); c.lineTo(-0.4, 14); c.lineTo(1, 8.5); c.closePath(); c.fill();
  c.beginPath(); c.moveTo(5.5, 8.5); c.lineTo(6.6, 14); c.lineTo(7.8, 8.5); c.closePath(); c.fill();
  /* the sovereign's crown, small and bone-coloured, sat between the ears */
  if(e.super || e.bkey=="batsovereign"){
    c.fillStyle = PAL.gold; c.strokeStyle = "rgba(60,40,10,0.6)"; c.lineWidth = 1.6;
    c.beginPath();
    c.moveTo(-8, -11); c.lineTo(-8, -18); c.lineTo(-4, -14); c.lineTo(0, -20);
    c.lineTo(4, -14); c.lineTo(8, -18); c.lineTo(8, -11);
    c.closePath(); c.fill(); c.stroke();
  }
  c.restore();

  /* it is still made of bricks under all that fur */
  drawStudG(c, -5, -52, 3, T("#3a3350"));
  drawStudG(c, 5, -52, 3, T("#3a3350"));

  if(e.slowT>0){
    c.globalAlpha = 0.4;
    c.fillStyle = "rgba(150,215,255,0.5)";
    c.beginPath(); c.ellipse(0,-42,24,26,0,0,TAU); c.fill();
    c.globalAlpha = 1;
  }
  c.restore();
}
public static void drawEnemy(Ctx c, Ent e){
  if(e.state=="build"){
    var t = clamp(e.buildT,0,1);
    var cols = new Col[] {e.col.head, e.col.torso, e.col.legs};
    for(var k=0;k<8;k++){
      var a = k/8.0*TAU + t*3;
      var r = (1-t)*70;
      double bx = e.x + Math.cos(a)*r, by = e.y + Math.sin(a)*r*0.6 - 14;
      c.save(); c.translate(bx,by); c.rotate(a+t*4); c.globalAlpha = 0.35+t*0.65;
      c.fillStyle = cols[k%3]; c.strokeStyle="rgba(20,30,40,0.4)"; c.lineWidth=1.5;
      c.fillRect(-6,-4,12,8); c.strokeRect(-6,-4,12,8);
      c.restore();
    }
    c.globalAlpha = 1;
    return;
  }
  if(!string.IsNullOrEmpty(e.mini)){
    /* a scrap of the mountain it fell off, still glowing with its element */
    var gg = GOLEM_RGB[e.mini];
    c.fillStyle = rgbaS(gg.a, 0.16+0.07*Math.sin(vtime*5+e.phase));
    c.beginPath(); c.ellipse(e.x, e.y+2, 13*e.scale, 5.5*e.scale, 0, 0, TAU); c.fill();
    drawMiniGolem(c, e);
    if(e.hp < e.maxHp){
      var w5 = 22*e.scale;
      c.fillStyle="rgba(15,20,25,0.6)";
      c.fillRect(e.x-w5/2.0, e.y-46*e.scale, w5, 4);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a");
      c.fillRect(e.x-w5/2.0, e.y-46*e.scale, w5*clamp(e.hp/e.maxHp,0,1), 4);
    }
    return;
  }
  if(e.shade){
    drawShade(c, e);
    if(e.hp < e.maxHp){
      var w4 = 26*e.scale;
      c.fillStyle="rgba(15,20,25,0.6)";
      c.fillRect(e.x-w4/2.0, e.y-62*e.scale, w4, 4);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a");
      c.fillRect(e.x-w4/2.0, e.y-62*e.scale, w4*clamp(e.hp/e.maxHp,0,1), 4);
    }
    return;
  }
  if(!string.IsNullOrEmpty(e.golem)){
    drawGolem(c, e);
    drawGolemAura(c, e);
    if(e.hp < e.maxHp){
      var w3 = 34*e.scale;
      c.fillStyle="rgba(15,20,25,0.6)";
      c.fillRect(e.x-w3/2.0, e.y-88*e.scale, w3, 5);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a");
      c.fillRect(e.x-w3/2.0, e.y-88*e.scale, w3*clamp(e.hp/e.maxHp,0,1), 5);
    }
    return;
  }
  if(e.type=="boss" && e.bkey=="batsovereign"){
    drawBatBoss(c, e);
    return;
  }
  if(EDEF.GetValueOrDefault(e.type ?? "")?.crawl == true){
    drawCrawler(c, e);
    if(e.hp < e.maxHp){
      var w2 = 24;
      c.fillStyle="rgba(15,20,25,0.6)"; c.fillRect(e.x-w2/2.0, e.y-30*e.scale, w2, 4);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a");
      c.fillRect(e.x-w2/2.0, e.y-30*e.scale, w2*clamp(e.hp/e.maxHp,0,1), 4);
    }
    return;
  }
  if(e.type=="cryptbat" || e.type=="direbat"){
    drawCryptBat(c, e);
    if(e.hp < e.maxHp){
      var w2 = 26;
      c.fillStyle="rgba(15,20,25,0.6)";
      c.fillRect(e.x-w2/2.0, e.y-40, w2, 4);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a");
      c.fillRect(e.x-w2/2.0, e.y-40, w2*clamp(e.hp/e.maxHp,0,1), 4);
    }
    return;
  }
  if(e.type=="greenwarden"){
    var pulse = 0.55 + 0.45*Math.sin(vtime*2.2 + e.phase);
    /* a TRUE circle — it heals on `dist(e2,e) < WARDEN_R`, so the picture has
       to be the circle the healing actually covers */
    c.fillStyle = rgba(150,225,130,0.055 + 0.035*pulse);
    c.beginPath(); c.arc(e.x, e.y+2, WARDEN_R, 0, TAU); c.fill();
    c.setLineDash([12,10]);
    c.strokeStyle = rgba(170,240,150,0.35 + 0.30*pulse); c.lineWidth = 3;
    c.beginPath(); c.arc(e.x, e.y+2, WARDEN_R, 0, TAU); c.stroke();
    c.setLineDash([]);
    /* motes of it drifting up off the ground he owns */
    if(Js.R() < 0.6){
      double a = rnd(TAU), rr = WARDEN_R*Math.sqrt(Js.R());
      particles.Add(new Particle {kind = "spark", x = e.x+Math.cos(a)*rr, y = e.y+Math.sin(a)*rr,
        vx = rnd(-6,6), vy = rnd(-30,-10), life = rnd(0.5,1.0), max = 1.0, color = "#bdf0a8"});
    }
  }
  if(e.type=="mimic" || e.type=="warbanner"){
    if(e.type=="mimic") drawMimic(c, e); else drawWarBanner(c, e);
    double wS = 30*(Js.Or(e.scale, 1)), yS = e.type=="mimic" ? e.y - 52*(Js.Or(e.scale, 1)) : e.y - 96;
    if(e.hp < e.maxHp){
      c.fillStyle="rgba(15,20,25,0.6)"; c.fillRect(e.x-wS/2.0, yS, wS, 4);
      c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:"#ff5a4a"); c.fillRect(e.x-wS/2.0, yS, wS*clamp(e.hp/e.maxHp,0,1), 4);
    }
    return;
  }
  drawPoisonPips(c, e);
  var o = new Fig { x = e.x, y = e.y, s = e.scale, face = e.face, walk = e.walk, moving = e.moving,
    flash = e.flash, colors = e.col, style = e.style, elite = e.elite, magic = e.magic,
    phase = e.phase, angry = e.type!="boss" };
  if(e.type=="boneling") o.noHelm = true;
  if(EDEF.GetValueOrDefault(e.type ?? "")?.zombie == true){ o.noHelm = true; o.zombieArms = true; if(EDEF[e.type].fat) o.fat = true; }
  if(e.type=="revenant") o.horns = true;
  if(e.type=="superimp") o.bigImp = true;
  if(e.type=="necrolord") o.horns = true;
  /* a bowman carries its bow all the time -- slack while it walks, drawn while it aims */
  if(e.type=="archer" || e.type=="marksman") o.bowPull = 0;
  if(e.type=="archer" && e.mode=="aim"){ o.bowPull = clamp(e.aimT/0.5,0,1); o.face = e.face; }
  if(e.type=="marksman" && e.mode=="aim"){ o.bowPull = clamp(e.aimT/0.5,0,1); o.face = e.face; }
  if(e.type=="spitter" && e.mode=="aim") o.armsUp = true;
  var lord = e.type.endsWith("lord") && e.type!="boss";
  if(lord) o.lord = true;
  if(e.type=="hexer"){ o.weapon="staff"; o.orbCol="#a5ff5a"; if(e.mode=="aim"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="pyromancer"){ o.weapon="staff"; o.orbCol="#ff9a3c"; o.hoodCol="#5e1f13"; if(e.mode=="aim"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="pyrolord"){ o.weapon="staff"; o.orbCol="#ff7a2a"; o.hoodCol="#7a2413"; if(e.mode=="aim"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="frostmage"){ o.weapon="staff"; o.orbCol="#bfe3ff"; o.hoodCol="#1d4a6b"; if(e.mode=="aim"||e.mode=="blizzard"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="frostlord"){ o.weapon="staff"; o.orbCol="#8fd4ff"; o.hoodCol="#22587f"; if(e.mode=="aim"||e.mode=="blizzard"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="stormcaller"){ o.weapon="staff"; o.orbCol="#ffe95a"; o.hoodCol="#6b5714"; if(e.mode=="aim"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="stormlord"){ o.weapon="staff"; o.orbCol="#fff3a0"; o.hoodCol="#8a7115"; if(e.mode=="aim"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="overseer"){ o.weapon="staff"; o.orbCol="#ffe95a"; o.hoodCol="#5e4a12"; o.lord=true; if(e.mode=="buffWind"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="impmaster"){ o.weapon="staff"; o.orbCol="#ff5a4a"; o.hoodCol="#3a0c16"; o.horns=true; o.lord=true;
    if(e.mode=="summon"||e.mode=="hex"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="witchdoctor"){ o.weapon="staff"; o.orbCol = e.orbKind=="chaos" ? "#8ede4a" : "#e0203a"; o.hoodCol="#3b2030";
    if(e.mode=="cast"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="necromancer"){ o.weapon="staff"; o.orbCol="#c9b8e8"; o.hoodCol="#271b3a"; if(e.mode=="summon"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="necrolord"){ o.weapon="staff"; o.orbCol="#dccdf5"; o.hoodCol="#31204d"; if(e.mode=="summon"){ o.armsUp=true; o.castGlow=true; } }
  if(e.type=="greenwarden"){ o.weapon="staff"; o.orbCol="#8ede4a"; o.hoodCol="#1d4523"; o.lord=true;
    o.armsUp = true; o.castGlow = e.wardLit > 0; }
  /* THEY CARRY WHAT THEY FIGHT WITH: the crypt's rank and file a rusted blade,
     a knight a sword behind its shield */
  if(e.type=="skeleton" || e.type=="revenant"){ o.weapon = "greatsword"; o.wScale = e.type=="revenant" ? 0.85 : 0.62; }
  if(e.type=="boneling"){ o.weapon = "greatsword"; o.wScale = 0.45; }
  if(e.type=="knight" || e.type=="bastion"){ o.weapon = "greatsword"; o.wScale = 0.7; }
  if(e.type=="brute" && e.mode=="windup") o.armsUp = true;
  if(e.type=="bloater" && (e.mode=="breathWind" || e.mode=="breath")) o.armsUp = true;
  if(e.strikeT > 0) o.armsUp = true;
  if(e.type=="jugg" && e.mode=="wind") o.armsUp = true;
  if(e.type=="lancer"){ o.weapon="spear"; o.armsUp = (e.mode=="windup"||e.mode=="lunge"); }
  if(e.type=="lobber" && e.mode=="wind") o.armsUp = true;
  if(e.type=="boss"){
    /* a boss mid-mace-spin hands the figure its whirl angle, so the body leans
       and turns and the mace really does go round with it */
    if(!double.IsNaN(e.whirlA) && e.mode=="maceWind") o.whirlA = e.whirlA;
    if(e.bkey=="bonebaron" || e.super) o.crown = true;
    if(e.bkey=="lavabrick"||e.bkey=="ultralava") o.lava = true;
    if(e.bkey=="ashtyrant"){ o.lava = true; o.horns = true; }
    if(e.bkey=="omegathane"){ o.horns = true; }
    /* the bosses that WHIRL carry a greatsword; the Omega keeps the mace,
       because its whole new mechanic is driving that mace into the floor */
    if(e.bkey=="brickthane"){ o.weapon="greatsword"; o.armsUp = (e.mode=="slamWind"||e.mode=="volleyWind"||e.mode=="summonWind"||e.mode=="novaWind"); }
    if(e.bkey=="omegathane"){ o.weapon="mace";
      o.armsUp = (e.mode=="slamWind"||e.mode=="volleyWind"||e.mode=="summonWind"||e.mode=="novaWind");
      /* the quake is a SLAM: raise, hold it up there, then drive it home and
         leave it in the floor while the ground is still coming apart */
      if(e.mode=="quakeWind"){
        var k = clamp(e.modeT/QUAKE_WIND, 0, 1);
        o.slam = k < 0.25 ? 1 - k/0.25 : (k < 0.8 ? 0 : (k-0.8)/0.2);
      } else if(e.slamHold > 0) o.slam = 1;
    }
    if(e.bkey=="bonebaron"||e.bkey=="megabaron"){
      o.weapon="staff"; o.staffUpright = true; o.orbCol = "#c9b8e8";
      /* the staff stays in hand through every cast — arms only go up to summon */
      o.armsUp = (e.mode=="summonWind");
      o.castGlow = (e.mode=="ringWind"||e.mode=="curseWind"||e.mode=="spiralWind"||
                    e.mode=="hexWind"||e.mode=="boneCallWind");
    }
    if(e.bkey=="lavabrick"||e.bkey=="ultralava"){ o.weapon="greatsword"; o.armsUp = (e.mode=="slamWind"||e.mode=="poolsWind"||e.mode=="volleyWind"||e.mode=="fanWind"||e.mode=="meteorWind"||e.mode=="hellWind"); }
    if(e.bkey=="ashtyrant"){ o.weapon="greatsword"; o.armsUp = (e.mode=="slamWind"||e.mode=="rainWind"||e.mode=="volleyWind"||e.mode=="emberWind"); }
  }
  drawFigure(c, o);
  drawSpecialExtras(c, e);
  /* ON TOP of the body, never under it: a gash is a mark ON the thing, and the
     dose count has to clear its head and its health bar. Anything drawn before
     drawFigure is simply painted over by the minifig. */
  drawBleedMark(c, e);
  drawPoisonCount(c, e);
  drawBurnPips(c, e);
  drawShockArcs(c, e);
  /* frostbite tint */
  if(e.slowT>0){
    c.save(); c.globalAlpha = 0.45;
    c.fillStyle = "rgba(150,215,255,0.5)";
    c.beginPath(); c.ellipse(e.x, e.y, e.r+7, (e.r+7)*0.5, 0, 0, TAU); c.fill();
    c.fillStyle = "#dff2ff";
    c.beginPath(); c.arc(e.x-e.r*0.5, e.y-e.r*0.7, 2.2, 0, TAU); c.fill();
    c.beginPath(); c.arc(e.x+e.r*0.4, e.y-e.r*0.9, 1.8, 0, TAU); c.fill();
    c.restore();
  }
  if(e.type=="knight"){
    var a = e.faceA;
    double sx2 = e.x + Math.cos(a)*15*e.scale, sy2 = e.y - 20*e.scale + Math.sin(a)*8*e.scale;
    c.save(); c.translate(sx2, sy2); c.rotate(a);
    c.fillStyle = "#9ba19d"; c.strokeStyle="rgba(20,30,40,0.6)"; c.lineWidth=2;
    roundRect(c, -4*e.scale, -12*e.scale, 11*e.scale, 24*e.scale, 5*e.scale); c.fill(); c.stroke();
    c.fillStyle = PAL.gold;
    c.beginPath(); c.arc(1.5*e.scale, 0, 3.6*e.scale, 0, TAU); c.fill(); c.stroke();
    c.restore();
  }
  if(e.type!="boss" && e.hp < e.maxHp){
    var w2 = 30*e.scale;
    c.fillStyle="rgba(15,20,25,0.6)";
    c.fillRect(e.x-w2/2.0, e.y-56*e.scale, w2, 5);
    c.fillStyle = e.elite?"#ffb900":(e.magic?MAGIC_COL:(lord?"#dccdf5":"#ff5a4a"));
    c.fillRect(e.x-w2/2.0, e.y-56*e.scale, w2*clamp(e.hp/e.maxHp,0,1), 5);
  }
  if(Js.T(e.buffStacks)){
    var ba = 0.35+0.2*Math.sin(vtime*5+e.phase);
    c.strokeStyle = rgba(255,233,90,ba);
    c.lineWidth = 2;
    c.setLineDash([4,3]);
    c.beginPath(); c.ellipse(e.x, e.y, e.r+9, (e.r+9)*0.5, 0, 0, TAU); c.stroke();
    c.setLineDash([]);
    c.fillStyle = "#ffe95a";
    c.font = "700 10px Fredoka"; c.textAlign = "center";
    c.fillText("+"+e.buffStacks + (buffCount(e) > 1 ? "·"+buffCount(e) : ""),
      e.x, e.y - 66*e.scale);
  }
}

}

/* how high a body is drawn off the ground (a bat): its shield bubble rides with it */
public sealed partial class Ent { public double drawZ; }
