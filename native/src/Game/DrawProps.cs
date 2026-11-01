// lego-survival.html, JS 28129-28696: DRAW: PROPS & WORLD — every prop, the beacons
// round the special pickups, the pickups, flying bricks, and the enemy missiles.
namespace Brickblade;

public sealed class BrickTier { public double y, w, h, studs; public Col col; }
public sealed class Beacon { public Col col; public double r; }

public static partial class G
{
/* =============================== DRAW: PROPS & WORLD =============================== */
public static void drawProp(Ctx c, Prop p){
  var jig = p.flash>0 ? Math.sin(vtime*60)*3*p.flash : 0;
  c.save(); c.translate(p.x + jig, p.y);
  c.fillStyle="rgba(15,30,20,0.28)";
  c.beginPath(); c.ellipse(0,2,p.r+6,(p.r+6)*0.42,0,0,TAU); c.fill();
  if(p.type=="strongbox") drawStrongbox(c, p);
  else if(p.type=="tree"){
    /* trunk: two 1x2 bricks stacked, studs showing at each joint */
    var OL = "rgba(20,30,40,0.5)";
    c.strokeStyle = OL; c.lineWidth = 2;
    for(var i=0;i<2;i++){
      c.fillStyle = sh(PAL.brown, i != 0?0.06:-0.06);
      roundRect(c, -7, -2-i*10, 14, 11, 2); c.fill(); c.stroke();
      drawStudG(c, -3.5, -1.5-i*10, 2.8, PAL.brown);
      drawStudG(c,  3.5, -1.5-i*10, 2.8, PAL.brown);
    }
    var sw2 = Math.sin(vtime*1.4+p.seed)*2;
    c.translate(sw2,0);
    /* canopy: three brick tiers, each narrower than the one below it */
    var tiers = new BrickTier[] {
      new BrickTier { y = -34, w = 44, h = 14, col = "#3d7a2b", studs = 4 },
      new BrickTier { y = -46, w = 34, h = 13, col = "#4b9b41", studs = 3 },
      new BrickTier { y = -56, w = 22, h = 12, col = "#57a33c", studs = 2 }
    };
    foreach (var t in tiers){
      c.fillStyle = sh(t.col,-0.3);
      roundRect(c, -t.w/2.0, t.y+2.5, t.w, t.h, 3); c.fill();
      c.fillStyle = t.col; c.strokeStyle = OL; c.lineWidth = 2;
      roundRect(c, -t.w/2.0, t.y, t.w, t.h, 3); c.fill(); c.stroke();
      c.fillStyle = sh(t.col, 0.16);
      c.fillRect(-t.w/2.0+2.5, t.y+2, t.w-5, 3);
      for(var i=0;i<t.studs;i++)
        drawStudG(c, -t.w/2.0 + t.w*(i+0.5)/t.studs, t.y+1.5, 3.4, t.col);
    }
  }
  else if(p.type=="cactus"){
    c.fillStyle = "#3d7a2b"; c.strokeStyle="rgba(20,30,40,0.5)"; c.lineWidth=2;
    roundRect(c,-5,-30,10,32,4); c.fill(); c.stroke();
    roundRect(c,-14,-22,9,6,3); c.fill(); c.stroke();
    roundRect(c,5,-26,9,6,3); c.fill(); c.stroke();
    drawStudG(c,0,-26,3.5,"#4b9b41"); drawStudG(c,0,-14,3.5,"#4b9b41");
    drawStudG(c,0,-4,3.5,"#4b9b41");
    c.fillStyle = "#ff8a8a";
    c.beginPath(); c.arc(3,-31,2,0,TAU); c.fill();
  }
  else if(p.type=="rock"){
    /* a cairn of slabs — irregular, but every one is clearly a brick */
    double r = p.r; Col OL = "rgba(20,30,40,0.55)";
    var slabs = new BrickTier[] {
      new BrickTier { y = 0,          w = r*2.0, h = r*0.58, col = "#6d6e68", studs = 3 },
      new BrickTier { y = -r*0.52,    w = r*1.6, h = r*0.5,  col = "#8b8f8a", studs = 2 },
      new BrickTier { y = -r*0.98,    w = r*1.0, h = r*0.44, col = "#9ba19d", studs = 1 }
    };
    for(var i=0;i<slabs.Count();i++){
      var sl = slabs[i];
      var off = (hash2(p.seed*7+i, i*13)-0.5) * r*0.24;
      c.fillStyle = sh(sl.col,-0.3);
      roundRect(c, off-sl.w/2.0, sl.y-sl.h+2.5, sl.w, sl.h, 2.5); c.fill();
      c.fillStyle = sl.col; c.strokeStyle = OL; c.lineWidth = 2;
      roundRect(c, off-sl.w/2.0, sl.y-sl.h, sl.w, sl.h, 2.5); c.fill(); c.stroke();
      c.fillStyle = sh(sl.col, 0.18);
      c.fillRect(off-sl.w/2.0+2, sl.y-sl.h+2, sl.w-4, Math.max(1.5, sl.h*0.2));
      for(var k=0;k<sl.studs;k++)
        drawStudG(c, off - sl.w/2.0 + sl.w*(k+0.5)/sl.studs, sl.y-sl.h+1.5,
          Math.max(2, r*0.16), sl.col);
    }
  }
  else if(p.type=="crate"){
    c.fillStyle = "#915f3d"; c.strokeStyle="rgba(20,30,40,0.5)"; c.lineWidth=2;
    roundRect(c,-15,-26,30,28,3); c.fill(); c.stroke();
    c.strokeStyle = "rgba(60,35,18,0.5)";
    c.strokeRect(-11,-22,22,20);
    drawStudG(c,-7,-16,4.5,"#9c6a45"); drawStudG(c,7,-16,4.5,"#9c6a45");
    drawStudG(c,-7,-5,4.5,"#9c6a45"); drawStudG(c,7,-5,4.5,"#9c6a45");
  }
  /* NEW: Rock Raiders green crystal — glowing shard cluster */
  else if(p.type=="crystal"){
    var glow = 0.55+0.35*Math.sin(vtime*2.2+p.seed);
    var shards = new (double sx, double sy, double w, double h, double tilt)[] { (-8,0,9,24,-0.35), (1,-2,11,32,0), (9,0,8,20,0.35) };
    foreach (var (sx,sy,w,h,tilt) in shards){
      c.save(); c.translate(sx,sy-4); c.rotate(tilt);
      var g2 = c.createLinearGradient(0,0,0,-h);
      g2.addColorStop(0,"#1f7a4d"); g2.addColorStop(0.55,"#3ddc84"); g2.addColorStop(1,"#c8ffe0");
      c.fillStyle = g2;
      c.beginPath();
      c.moveTo(-w/2.0,0); c.lineTo(-w*0.18,-h*0.28); c.lineTo(0,-h); c.lineTo(w*0.18,-h*0.28); c.lineTo(w/2.0,0);
      c.closePath(); c.fill();
      c.strokeStyle="rgba(10,40,25,0.55)"; c.lineWidth=1.5; c.stroke();
      c.fillStyle="rgba(255,255,255,0.35)";
      c.beginPath(); c.moveTo(-w*0.14,-2); c.lineTo(0,-h*0.85); c.lineTo(w*0.06,-h*0.3); c.closePath(); c.fill();
      c.restore();
    }
    c.fillStyle = rgba(170,255,205,glow*0.55);
    c.beginPath(); c.arc(1,-20,3.5+glow,0,TAU); c.fill();
    c.fillStyle = "#1f7a4d"; c.strokeStyle="rgba(10,40,25,0.5)"; c.lineWidth=1.5;
    roundRect(c,-7,-4,14,6,2); c.fill(); c.stroke();
    drawStudG(c,-3,-1,2,"#2f9a63"); drawStudG(c,3,-1,2,"#2f9a63");
  }
  /* a pillar of the ruins: 2x2 round bricks stacked, the run's own stone */
  else if(p.type=="pillar"){
    double r = p.r; Col OL = "rgba(20,30,40,0.55)";
    Col @base = runBiome=="desert" ? "#cdb888" : runBiome=="ash" ? "#5d5657"
               : runBiome=="tundra" ? "#dfe6ea" : runBiome=="swamp" ? "#7d8a78" : "#a3a7a2";
    double tiers = Math.max(1, Math.round(4 * (Js.Or(p.tall, 1)))), th = 15;
    /* the footing slab */
    c.fillStyle = sh(@base, -0.25); c.strokeStyle = OL; c.lineWidth = 2;
    roundRect(c, -r-4, -8, (r+4)*2, 12, 3); c.fill(); c.stroke();
    for(var i=0;i<tiers;i++){
      double y0 = -8 - (i+1)*th; Col col = sh(@base, (hash2(Js.I32(p.seed), i)-0.5)*0.14);
      c.fillStyle = sh(col, -0.18);
      roundRect(c, -r*0.78, y0, r*1.56, th+1, 3); c.fill(); c.stroke();
      c.fillStyle = sh(col, 0.10);
      c.fillRect(-r*0.78+3, y0+2, r*0.5, th-4);
    }
    var top = -8 - tiers*th;
    c.fillStyle = sh(@base, 0.06);
    c.beginPath(); c.ellipse(0, top, r*0.78, r*0.36, 0, 0, TAU); c.fill(); c.stroke();
    if((Js.Or(p.tall, 1)) < 1){
      /* snapped off: a jagged break and a fallen drum beside it */
      c.fillStyle = sh(@base, -0.3);
      c.beginPath(); c.moveTo(-r*0.5, top); c.lineTo(-r*0.15, top-5); c.lineTo(r*0.2, top+1); c.lineTo(r*0.5, top-4);
      c.lineTo(r*0.5, top+3); c.lineTo(-r*0.5, top+3); c.closePath(); c.fill();
      c.fillStyle = sh(@base, -0.1);
      roundRect(c, r*0.55, -10, th+2, r*1.1, 3); c.fill(); c.stroke();
    } else {
      drawStudG(c, -r*0.3, top, 4, @base); drawStudG(c, r*0.3, top, 4, @base);
    }
  }
  /* NEW: giant indestructible cave boulder */
  else if(p.type=="boulder"){
    var r = p.r;
    c.fillStyle = "#6e6a62"; c.strokeStyle="rgba(15,20,25,0.55)"; c.lineWidth=2.5;
    roundRect(c,-r,-r*0.85,r*2,r*1.45,9); c.fill(); c.stroke();
    c.fillStyle = "#7a766e";
    roundRect(c,-r+5,-r*0.85-5,r*2-10,r*0.62,8); c.fill(); c.stroke();
    c.fillStyle = "#847f76";
    roundRect(c,-r+8,-r*0.85-2,r*0.6,r*0.4,6); c.fill();
    c.stroke();
    var ns = Math.max(2, Math.floor(r/11.0));
    for(var i=0;i<ns;i++){
      drawStudG(c, -r+10+(i+0.5)*((r*2-20)/ns), -r*0.85+r*0.31, 6.5, "#7a766e");
    }
    c.strokeStyle="rgba(0,0,0,0.18)"; c.lineWidth=2;
    c.beginPath(); c.moveTo(-r*0.4, -r*0.1); c.lineTo(-r*0.1, r*0.15); c.stroke();
  }
  else if(p.type=="chest"){
    /* A MYSTERY CHEST wears no tier. It cycles every colour on the ladder, so
       what is inside is genuinely unknown until the lid comes off — and it is
       unmistakable across a field of ordinary brown ones. */
    /* the cycle is kept in 0..3 whatever the clock reads: a negative or broken
       time gave a negative index, an undefined rarity and a frame that threw */
    var wi = Js.Or((((Math.floor(vtime*3 + p.seed) % 4) + 4) % 4), 0);
    var R2 = (p.wild ? RARITY.GetValueOrDefault(RAR_ORDER[(int)(1 + wi)]) : RARITY.GetValueOrDefault(p.rar ?? "")) ?? RARITY["common"];
    Col rc = R2.col; var rgb = prs(rc);
    double tier = p.wild ? 4 : Array.IndexOf(new[] {"common","uncommon","rare","epic"}, p.rar??"common");
    var glow = 0.5+0.5*Math.sin(vtime*3+p.seed);
    var bob2 = Math.sin(vtime*2+p.seed)*1.5;
    c.translate(0,bob2);
    /* the aura grows with the tier, so a purple chest reads across the field */
    c.fillStyle = rgba(rgb[0],rgb[1],rgb[2],0.10+glow*(0.10+tier*0.05));
    c.beginPath(); c.arc(0,-14,28+tier*5+glow*(4+tier*2),0,TAU); c.fill();
    c.fillStyle = sh(rc,-0.42); c.strokeStyle="rgba(20,30,40,0.6)"; c.lineWidth=2;
    roundRect(c,-16,-16,32,18,3); c.fill(); c.stroke();
    c.fillStyle = sh(rc,-0.22);
    roundRect(c,-16,-24,32,10,3); c.fill(); c.stroke();
    c.fillStyle = rgba(rgb[0],rgb[1],rgb[2],0.55+glow*0.45);
    roundRect(c,-12,-14,24,5,2); c.fill();
    c.fillStyle = rc;
    roundRect(c,-3,-16,6,18,2); c.fill(); c.stroke();
    c.fillRect(-16,-13,32,3); c.strokeRect(-16,-13,32,3);
    drawStudG(c,-9,-26,3,sh(rc,0.2)); drawStudG(c,9,-26,3,sh(rc,0.2));
    /* A MIMIC BREATHES. Every few seconds the lid lifts a crack and there are
       teeth under it -- there to be noticed, if you are looking */
    if(p.mimic && ((vtime + p.seed) % 4.5) < 0.4){
      c.fillStyle = "#3a0c16"; c.fillRect(-14, -16, 28, 3);
      c.fillStyle = "#fff";
      for(var i=0;i<5;i++){ var x = -13 + i*5.5; c.beginPath(); c.moveTo(x, -16); c.lineTo(x + 2.5, -13); c.lineTo(x + 5, -16); c.fill(); }
    }
    if(p.wild){
      /* a question mark where the pips would be, and a slow halo turning round it */
      c.fillStyle = "#ffffff";
      c.font = "700 15px \"Titan One\""; c.textAlign = "center";
      c.fillText("?", 0, -25);
      c.strokeStyle = rgba(rgb[0],rgb[1],rgb[2],0.5+0.4*glow);
      c.lineWidth = 2.5; c.setLineDash([5,7]);
      c.save(); c.translate(0,-14); c.rotate(vtime*1.2);
      c.beginPath(); c.arc(0,0,26,0,TAU); c.stroke();
      c.restore(); c.setLineDash([]);
    } else {
      /* rarity pips on the lid */
      c.fillStyle = "#fff";
      for(var i=0;i<=tier;i++){
        c.beginPath(); c.arc(-6+i*4, -30, 1.5, 0, TAU); c.fill();
      }
    }
  }
  else if(p.type=="rwall" || p.type=="rgate" || p.type=="rhouse" || p.type=="rstash" ||
          p.type=="rboard" || p.type=="rwaypoint" || p.type=="rexit" || p.type=="rcave" || p.type=="rportal" || p.type=="rstalag") drawRpgProp(c, p);
  else if(p.type=="torch"){
    c.fillStyle = "#6d6e68"; c.strokeStyle="rgba(20,30,40,0.5)"; c.lineWidth=2;
    roundRect(c,-3.5,-26,7,28,2); c.fill(); c.stroke();
    drawStudG(c,0,-24,4.5,"#7d7f7a");
    double fl = Math.sin(vtime*13+p.seed)*2, fl2 = Math.cos(vtime*17+p.seed)*1.5;
    c.fillStyle = "rgba(254,138,24,0.9)";
    c.beginPath(); c.arc(fl*0.5,-32,6,0,TAU); c.fill();
    c.fillStyle = "rgba(255,209,102,0.95)";
    c.beginPath(); c.arc(fl2*0.6,-35+fl*0.4,3.6,0,TAU); c.fill();
  }
  c.restore();
  if(p.flash>0) p.flash = Math.max(0, p.flash-0.08);
}
/* The prizes — hearts, magnets, fortunes, the two combat bricks — were the same
   size as a stud and got lost in the litter of a cleared wave. Each now stands
   in a BEACON: a pulsing ring painted on the floor, a soft column of its own
   colour, and a chevron bobbing overhead. You can see them across the room. */
public static readonly JsMap<string, Beacon> BEACONS = new() {
  ["heart"] = new Beacon { col = "#ff5a4a", r = 26 },
  ["gheart"] = new Beacon { col = "#ffd166", r = 30 },
  ["magnet"] = new Beacon { col = "#9b45d6", r = 28 },
  ["fortune"] = new Beacon { col = "#fe8a18", r = 28 },
  ["rage"] = new Beacon { col = "#ff5a4a", r = 28 },
  ["ward"] = new Beacon { col = "#8fd4ff", r = 28 }
};
public static void drawBeacon(Ctx c, Pickup pk, Beacon B){
  var t = vtime*2.4 + pk.spin;
  var pr5 = prs(B.col);
  /* the ring on the floor, breathing */
  var k = 0.5 + 0.5*Math.sin(t);
  c.save(); c.scale(1, GROUND_SQUASH);
  c.fillStyle = rgba(pr5[0],pr5[1],pr5[2],0.10 + 0.07*k);
  c.beginPath(); c.arc(0, 0, B.r*(0.82+0.18*k), 0, TAU); c.fill();
  c.strokeStyle = rgba(pr5[0],pr5[1],pr5[2],0.45 + 0.3*k);
  c.lineWidth = 2.4;
  c.beginPath(); c.arc(0, 0, B.r*(0.82+0.18*k), 0, TAU); c.stroke();
  c.restore();
  /* the column of light standing in it */
  var g = c.createLinearGradient(0, -B.r*2.1, 0, 0);
  g.addColorStop(0, rgba(pr5[0],pr5[1],pr5[2],0));
  g.addColorStop(1, rgba(pr5[0],pr5[1],pr5[2],0.20 + 0.10*k));
  c.fillStyle = g;
  c.beginPath();
  c.moveTo(-B.r*0.42, 0); c.lineTo(B.r*0.42, 0);
  c.lineTo(B.r*0.22, -B.r*2.1); c.lineTo(-B.r*0.22, -B.r*2.1);
  c.closePath(); c.fill();
  /* and a chevron pointing down at it */
  var yy = -B.r*1.75 - Math.abs(Math.sin(t*0.9))*5;
  c.fillStyle = rgba(pr5[0],pr5[1],pr5[2],0.95);
  c.strokeStyle = "rgba(15,20,25,0.55)"; c.lineWidth = 1.6; c.lineJoin="round";
  c.beginPath();
  c.moveTo(0, yy+7); c.lineTo(-6.5, yy-3); c.lineTo(-2.4, yy-3);
  c.lineTo(-2.4, yy-8); c.lineTo(2.4, yy-8); c.lineTo(2.4, yy-3);
  c.lineTo(6.5, yy-3); c.closePath();
  c.fill(); c.stroke();
}
public static void drawPickup(Ctx c, Pickup pk){
  var bobz = Math.sin(vtime*3+pk.spin)*2;
  c.save(); c.translate(pk.x, pk.y);
  c.fillStyle="rgba(15,30,20,0.25)";
  c.beginPath(); c.ellipse(0,2,7,3,0,0,TAU); c.fill();
  var B = BEACONS.GetValueOrDefault(pk.kind ?? "");
  if(B != null) drawBeacon(c, pk, B);
  c.translate(0, -pk.z - bobz);
  /* the prizes are drawn half again as big as a stud, too */
  if(B != null) c.scale(1.3, 1.3);
  if(pk.kind=="heart" || pk.kind=="gheart"){
    var gold = pk.kind=="gheart";
    var s = gold ? 14 : 12;
    c.fillStyle = gold ? PAL.gold : PAL.red;
    c.strokeStyle="rgba(20,30,40,0.55)"; c.lineWidth=2;
    c.beginPath();
    c.moveTo(0, s*0.9);
    c.bezierCurveTo(-s*1.15, s*0.12, -s*0.8, -s*0.8, 0, -s*0.25);
    c.bezierCurveTo(s*0.8, -s*0.8, s*1.15, s*0.12, 0, s*0.9);
    c.closePath(); c.fill(); c.stroke();
    if(gold){
      c.strokeStyle="rgba(255,255,255,0.85)"; c.lineWidth=1.4;
      c.beginPath(); c.moveTo(-4,-6); c.lineTo(-1,-9); c.moveTo(4,-5); c.lineTo(7,-8); c.stroke();
    }
    c.fillStyle="rgba(255,255,255,0.7)";
    c.beginPath(); c.ellipse(-s*0.38,-s*0.28,2.2,1.5,-0.5,0,TAU); c.fill();
  } else if(pk.kind=="magnet"){
    c.rotate(Math.sin(vtime*2+pk.spin)*0.25);
    c.scale(1.25,1.25);
    c.strokeStyle="rgba(20,30,40,0.55)"; c.lineWidth=1.8;
    c.fillStyle = "#9b45d6";
    c.beginPath();
    c.moveTo(-8,-1); c.arc(0,-1,8,Math.PI,0,true);
    c.lineTo(8,7); c.lineTo(3.5,7); c.lineTo(3.5,-1);
    c.arc(0,-1,3.5,0,Math.PI,true);
    c.lineTo(-3.5,7); c.lineTo(-8,7);
    c.closePath(); c.fill(); c.stroke();
    c.fillStyle = "#cfd6dd";
    c.fillRect(-8,4.2,4.5,3.4); c.fillRect(3.5,4.2,4.5,3.4);
    c.fillStyle = "#ffd166";
    c.beginPath(); c.arc(10,-9,2+Math.sin(vtime*10)*0.8,0,TAU); c.fill();
  } else if(pk.kind=="rage"){
    /* a brick with a blade through it, beating like something angry */
    var pulse = 1 + Math.sin(vtime*9+pk.spin)*0.07;
    c.rotate(Math.sin(vtime*2.4+pk.spin)*0.2); c.scale(1.3*pulse, 1.3*pulse);
    c.fillStyle = rgba(255,90,74,0.22+0.12*Math.sin(vtime*8));
    c.beginPath(); c.arc(0,0,15,0,TAU); c.fill();
    c.fillStyle = "#d3242b"; c.strokeStyle="rgba(20,30,40,0.6)"; c.lineWidth=1.8;
    roundRect(c,-9,-9,18,18,3); c.fill(); c.stroke();
    drawStudG(c,-4.6,-4.6,2.4,"#8f161d"); drawStudG(c,4.6,-4.6,2.4,"#8f161d");
    c.save(); c.rotate(-0.7); drawBlade(c, 0.42, true); c.restore();
    c.fillStyle = "#fff";
    c.beginPath(); c.arc(11,-11,2+Math.sin(vtime*12)*0.8,0,TAU); c.fill();
  } else if(pk.kind=="ward"){
    /* a shield brick, plated and cold */
    c.rotate(Math.sin(vtime*2+pk.spin)*0.16); c.scale(1.3,1.3);
    c.fillStyle = rgba(143,212,255,0.20+0.10*Math.sin(vtime*6));
    c.beginPath(); c.arc(0,0,15,0,TAU); c.fill();
    c.fillStyle = "#2e7dd1"; c.strokeStyle="rgba(15,30,50,0.65)"; c.lineWidth=1.8;
    c.beginPath();
    c.moveTo(0,-10); c.lineTo(9,-6); c.lineTo(9,2);
    c.quadraticCurveTo(9,9, 0,12);
    c.quadraticCurveTo(-9,9, -9,2);
    c.lineTo(-9,-6); c.closePath(); c.fill(); c.stroke();
    drawStudG(c,-4,-4,2.4,"#1d4a6b"); drawStudG(c,4,-4,2.4,"#1d4a6b");
    c.strokeStyle = "#bfe3ff"; c.lineWidth = 1.8;
    c.beginPath(); c.moveTo(0,-3); c.lineTo(0,7); c.moveTo(-4,1); c.lineTo(4,1); c.stroke();
    c.fillStyle = "#fff";
    c.beginPath(); c.arc(11,-11,2+Math.sin(vtime*12)*0.8,0,TAU); c.fill();
  } else if(pk.kind=="fortune"){
    c.rotate(Math.sin(vtime*3+pk.spin)*0.3);
    c.scale(1.3,1.3);
    c.fillStyle = PAL.gold; c.strokeStyle="rgba(20,30,40,0.6)"; c.lineWidth=1.8;
    roundRect(c,-9,-9,18,18,3); c.fill(); c.stroke();
    drawStudG(c,0,-2,4.5,"#ffcf00");
    c.fillStyle = "#d3242b";
    c.font="700 8px Fredoka"; c.textAlign="center"; c.textBaseline="middle";
    c.fillText("2×", 0, 10.5);
    c.fillStyle = "#fff";
    c.beginPath(); c.arc(11,-11,2+Math.sin(vtime*12)*0.8,0,TAU); c.fill();
  } else {
    var TT = STUD_TIERS.GetValueOrDefault(pk.kind ?? "") ?? STUD_TIERS["stud1"];
    double r = TT.r; Col col = TT.col;
    var spinX = Math.max(0.22, Math.abs(Math.cos(vtime*5 + pk.spin)));
    c.save();
    c.scale(spinX, 1);
    drawStudPickup(c, r, col);
    c.restore();
    /* purple studs shimmer; mid-merge tiers pulse gently */
    if(!string.IsNullOrEmpty(TT.label)){
      /* the high tiers wear a halo so they're findable in a pile */
      var pr3 = prs(TT.col);
      ctx.fillStyle = rgba(pr3[0],pr3[1],pr3[2],0.3+0.25*Math.sin(vtime*6));
      ctx.beginPath(); ctx.arc(0,-2,12+Math.sin(vtime*6)*2,0,TAU); ctx.fill();
      ctx.strokeStyle = rgba(255,255,255,0.35+0.25*Math.sin(vtime*7+pk.spin));
      ctx.lineWidth = 1.6;
      ctx.beginPath(); ctx.arc(0,-2,r+4.5,0,TAU); ctx.stroke();
    } else if(pk.kind!="stud1"){
      ctx.fillStyle = rgba(191,227,255,0.12+0.1*Math.sin(vtime*4+pk.spin));
      ctx.beginPath(); ctx.arc(0,-2,10,0,TAU); ctx.fill();
    }
  }
  c.restore();
}
public static void drawDebris(Ctx c, Debris d){
  c.save(); c.globalAlpha = clamp(d.life/0.3, 0, 1);
  c.fillStyle = "rgba(15,30,20,0.22)";
  c.beginPath(); c.ellipse(d.x, d.y, d.w*0.42, d.w*0.18, 0, 0, TAU); c.fill();
  c.translate(d.x, d.y - d.z); c.rotate(d.rot);
  /* a chunk with a lip and a row of studs, not a plain rectangle */
  double w = d.w, h = d.h;
  c.fillStyle = sh(d.color,-0.28);
  roundRect(c, -w/2.0, -h/2.0+1.5, w, h, 1.6); c.fill();
  c.fillStyle = d.color; c.strokeStyle="rgba(20,30,40,0.5)"; c.lineWidth=1.4;
  roundRect(c, -w/2.0, -h/2.0, w, h, 1.6); c.fill(); c.stroke();
  c.fillStyle = sh(d.color,0.22);
  c.fillRect(-w/2.0+1.2, -h/2.0+1.2, w-2.4, Math.max(1, h*0.26));
  var studs = w > 13 ? 2 : 1;
  for(var i=0;i<studs;i++)
    drawStudG(c, -w/2.0 + w*(i+0.5)/studs, -h*0.1, Math.max(1.1, h*0.25), d.color);
  c.restore();
}
public static void drawArrow(Ctx c, Proj pr){
  c.save(); c.translate(pr.x, pr.y); c.rotate(Math.atan2(pr.vy,pr.vx));
  c.strokeStyle = pr.deflected ? PAL.red : "#7b5132"; c.lineWidth=2.6; c.lineCap="round";
  c.beginPath(); c.moveTo(-10,0); c.lineTo(7,0); c.stroke();
  c.fillStyle = pr.deflected ? PAL.red : "#9ba19d";
  c.beginPath(); c.moveTo(12,0); c.lineTo(5,-3.4); c.lineTo(5,3.4); c.closePath(); c.fill();
  c.strokeStyle = "#e8e4d8"; c.lineWidth=1.6;
  c.beginPath(); c.moveTo(-10,0); c.lineTo(-14,-3); c.moveTo(-10,0); c.lineTo(-14,3); c.stroke();
  c.restore();
}
public static void drawSkull(Ctx c, Proj pr){
  c.save(); c.translate(pr.x, pr.y - 6);
  c.rotate(Math.sin(vtime*8+pr.spin)*0.3);
  c.fillStyle = pr.deflected ? PAL.red : "#eef0ee";
  c.strokeStyle="rgba(20,30,40,0.6)"; c.lineWidth=1.6;
  c.beginPath(); c.arc(0,0,7,0,TAU); c.fill(); c.stroke();
  c.fillRect(-4.5,4,9,4);
  c.fillStyle = "#1b2a34";
  c.beginPath(); c.arc(-2.6,-0.5,1.9,0,TAU); c.arc(2.6,-0.5,1.9,0,TAU); c.fill();
  c.fillRect(-2.4,4.6,1.4,2.6); c.fillRect(0.4,4.6,1.4,2.6);
  c.restore();
}
/* A bat-shaped missile: a little brick body with two wings beating. It turns
   after the hero, so it reads as alive rather than thrown. */
public static void drawBatBolt(Ctx c, Proj pr){
  var a = Math.atan2(pr.vy, pr.vx);
  c.save(); c.translate(pr.x, pr.y); c.rotate(a);
  var flap = Math.sin(pr.flap)*0.8;
  foreach (var side in new double[]{-1, 1}){
    c.fillStyle = "rgba(120,90,170,0.95)";
    c.strokeStyle = "rgba(30,20,50,0.6)"; c.lineWidth = 1.4;
    c.save(); c.scale(1, side);
    c.rotate(flap*0.4);
    c.beginPath();
    c.moveTo(-1, 0); c.lineTo(-9, -7-flap*3); c.lineTo(-3, -10-flap*3);
    c.lineTo(3, -6); c.closePath(); c.fill(); c.stroke();
    c.restore();
  }
  c.fillStyle = "#4a3a63"; c.strokeStyle = "rgba(20,14,34,0.7)"; c.lineWidth = 1.5;
  roundRect(c, -6, -3.5, 12, 7, 2.5); c.fill(); c.stroke();
  drawStudG(c, 0, -2.5, 2.2, "#2e2442");
  c.fillStyle = "#ffe95a";
  c.beginPath(); c.arc(4, -1, 1.4, 0, TAU); c.arc(4, 1.4, 1.4, 0, TAU); c.fill();
  c.restore();
}
/* The shade-storm's orb: a caged ball of lightning with one arm of it sweeping
   round, drawn as a jagged polyline so it reads as lightning and not a laser. */
public static void drawZorb(Ctx c, Proj pr){
  double ex = pr.x + Math.cos(pr.ang)*ZORB_ARM, ey = pr.y + Math.sin(pr.ang)*ZORB_ARM*0.8;
  c.save();
  c.strokeStyle = "rgba(255,233,90,0.85)"; c.lineWidth = 3; c.lineJoin = "round";
  c.beginPath(); c.moveTo(pr.x, pr.y);
  for(var i=1;i<=6;i++){
    var t = i/6.0;
    double nx = -Math.sin(pr.ang), ny = Math.cos(pr.ang);
    var j = (i==6 ? 0 : Math.sin(pr.seed + i*2.3 + vtime*22) * 11);
    c.lineTo(lerp(pr.x,ex,t) + nx*j, lerp(pr.y,ey,t) + ny*j);
  }
  c.stroke();
  c.strokeStyle = "rgba(255,255,255,0.55)"; c.lineWidth = 1.2; c.stroke();
  /* the ball */
  c.fillStyle = "rgba(255,233,90,0.22)";
  c.beginPath(); c.arc(pr.x, pr.y, 26 + Math.sin(vtime*14)*3, 0, TAU); c.fill();
  c.translate(pr.x, pr.y); c.rotate(-vtime*3);
  var cols = new Col[] {"#fff3a0","#ffe95a","#e0bf22"};
  for(var i=0;i<3;i++){
    c.fillStyle = cols[i]; c.strokeStyle = "rgba(60,50,10,0.5)"; c.lineWidth = 1.4;
    c.save(); c.rotate(i*TAU/3.0);
    roundRect(c, -8, -5, 16, 10, 2.5); c.fill(); c.stroke();
    c.restore();
  }
  c.fillStyle = "#ffffff";
  c.beginPath(); c.arc(0, 0, 5 + Math.sin(vtime*18)*1.4, 0, TAU); c.fill();
  c.restore();
}
/* The frost golem's boulder: a rough stack of ice bricks tumbling end over end. */
public static void drawFrostRock(Ctx c, Proj pr){
  var ta = Math.atan2(pr.vy, pr.vx);
  for(var i=1;i<=4;i++){
    c.fillStyle = rgba(191,227,255,0.20/i);
    c.beginPath(); c.arc(pr.x-Math.cos(ta)*i*11, pr.y-Math.sin(ta)*i*11, 15-i*2.2, 0, TAU); c.fill();
  }
  c.save(); c.translate(pr.x, pr.y);
  c.fillStyle = "rgba(0,0,0,0.18)";
  c.beginPath(); c.ellipse(0, 20, 17, 6, 0, 0, TAU); c.fill();
  c.rotate(Js.Or(pr.spin, 0));
  var cols = new Col[] {"#e6f6ff","#cfeaff","#7fb9e0"};
  var blocks = new double[][] { new double[]{-11,-8,14,11}, new double[]{3,-11,13,12}, new double[]{-7,3,16,11}, new double[]{6,2,10,10} };
  foreach (var (b2, i) in blocks.Select((b, k) => (b, k))) {
    c.fillStyle = cols[i%3];
    c.strokeStyle = "rgba(40,90,130,0.55)"; c.lineWidth = 1.4;
    roundRect(c, b2[0], b2[1], b2[2], b2[3], 2.5); c.fill(); c.stroke();
    drawStudG(c, b2[0]+b2[2]/2.0, b2[1]+2.5, 3, "#4d86ad");
  }
  c.restore();
}
public static void drawFrostbolt(Ctx c, Proj pr){
  c.save(); c.translate(pr.x, pr.y);
  /* frozen vapour trailing behind the shard */
  var ta = Math.atan2(pr.vy,pr.vx);
  for(var i=1;i<=3;i++){
    c.fillStyle = rgba(191,227,255,0.22/i);
    c.beginPath(); c.arc(-Math.cos(ta)*i*7, -Math.sin(ta)*i*7, 7-i*1.4, 0, TAU); c.fill();
  }
  c.fillStyle = "rgba(140,210,255,0.35)";
  c.beginPath(); c.arc(0,0,10+Math.sin(vtime*12)*1.2,0,TAU); c.fill();
  c.rotate(ta + vtime*3);
  /* six-armed snowflake */
  c.strokeStyle = "#dff2ff"; c.lineWidth = 2.2; c.lineCap="round";
  for(var i=0;i<3;i++){
    var a = i*Math.PI/3.0;
    c.beginPath(); c.moveTo(-Math.cos(a)*8, -Math.sin(a)*8); c.lineTo(Math.cos(a)*8, Math.sin(a)*8); c.stroke();
  }
  c.strokeStyle = "rgba(255,255,255,0.9)"; c.lineWidth = 1.4;
  for(var i=0;i<6;i++){
    var a = i*Math.PI/3.0;
    double bx = Math.cos(a)*5, by = Math.sin(a)*5;
    c.beginPath();
    c.moveTo(bx,by); c.lineTo(bx+Math.cos(a+0.9)*3.2, by+Math.sin(a+0.9)*3.2);
    c.moveTo(bx,by); c.lineTo(bx+Math.cos(a-0.9)*3.2, by+Math.sin(a-0.9)*3.2);
    c.stroke();
  }
  c.fillStyle = "#ffffff";
  c.beginPath(); c.arc(0,0,2.2,0,TAU); c.fill();
  c.restore();
}
/* a jittering multi-branch arc that re-rolls every frame, so it crackles */
public static void drawBolt(Ctx c, Proj pr){
  var sc = pr.big ? 1.8 : 1;
  if(double.IsNaN(pr.seed)) pr.seed = rnd(TAU);
  c.save(); c.translate(pr.x, pr.y); c.rotate(Math.atan2(pr.vy,pr.vx)); c.scale(sc,sc);
  double L = 17, segs = 7, t = Math.floor(vtime*26);
  var jag = new List<Pt>();
  for(var i=0;i<=segs;i++){
    var u = i/segs;
    var n = Math.sin(pr.seed + i*2.7 + t*1.3) * Math.sin(pr.seed*1.7 + i*4.1 + t*0.7);
    jag.Add(new Pt(-L + u*L*2, n*5.5*Math.sin(u*Math.PI)));
  }
  void stroke(double w, Paint col){
    c.strokeStyle = col; c.lineWidth = w; c.lineJoin="round"; c.lineCap="round";
    c.beginPath(); c.moveTo(jag[0].x, jag[0].y);
    for(var i=1;i<jag.Count();i++) c.lineTo(jag[i].x, jag[i].y);
    c.stroke();
  }
  stroke(9, "rgba(255,233,90,0.20)");   /* outer glow */
  stroke(5.5, "rgba(255,200,40,0.55)");
  stroke(2.8, "#ffe95a");
  stroke(1.2, "#ffffff");               /* white-hot core */
  /* short forks peeling off the arc */
  c.strokeStyle = "rgba(255,245,170,0.75)"; c.lineWidth = 1.4;
  for(var i=2;i<segs-1;i+=2){
    var fy = jag[i].y + (Math.sin(pr.seed+i*3.3+t)>0 ? 1 : -1) * (4+2*Math.abs(Math.sin(t*0.9+i)));
    c.beginPath(); c.moveTo(jag[i].x, jag[i].y);
    c.lineTo(jag[i].x + 4, fy); c.lineTo(jag[i].x + 7, fy + Math.sin(t+i)*2.5);
    c.stroke();
  }
  c.fillStyle = "rgba(255,255,255,0.9)";
  c.beginPath(); c.arc(L, jag[(int)segs].y, 2.6+Math.sin(vtime*30)*0.7, 0, TAU); c.fill();
  c.restore();
}
public static void drawFireball(Ctx c, Proj pr, bool big = false){
  var r = big?11:7.5;
  c.save(); c.translate(pr.x, pr.y - (Js.Or(pr.z, 10)));
  var fl = Math.sin(vtime*20+pr.spin)*1.6;
  /* trailing embers streaming up off the ball */
  for(var i=1;i<=4;i++){
    var wob = Math.sin(vtime*14 + pr.spin + i)*2.4;
    c.fillStyle = big ? rgba(255,138,90,0.30/i) : rgba(255,150,60,0.30/i);
    c.beginPath(); c.arc(wob, i*(r*0.7), r*(1-i*0.17), 0, TAU); c.fill();
  }
  c.fillStyle = big ? "rgba(255,110,40,0.30)" : "rgba(255,140,50,0.28)";
  c.beginPath(); c.arc(0,0,r+8+fl,0,TAU); c.fill();
  c.fillStyle = "rgba(255,120,40,0.55)";
  c.beginPath(); c.arc(0,0,r+3.5+fl*0.6,0,TAU); c.fill();
  /* ragged flame edge instead of a clean circle */
  c.fillStyle = big ? "#ff5f22" : "#ff7a2a";
  c.beginPath();
  for(var i=0;i<=10;i++){
    var a = i/10.0*TAU;
    var rr = r*(1 + 0.18*Math.sin(a*3 + vtime*16 + pr.spin));
    if (i != 0) c.lineTo(Math.cos(a)*rr, Math.sin(a)*rr); else c.moveTo(Math.cos(a)*rr, Math.sin(a)*rr);
  }
  c.closePath(); c.fill();
  c.fillStyle = "#ffb14a";
  c.beginPath(); c.arc(-r*0.12,-r*0.12,r*0.62,0,TAU); c.fill();
  c.fillStyle = "#fff0b8";
  c.beginPath(); c.arc(-r*0.22,-r*0.24,r*0.32,0,TAU); c.fill();
  c.restore();
}

}
