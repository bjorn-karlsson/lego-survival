// lego-survival.html, JS 27347-28128: DRAW: MINIFIGURE — the one figure everything with
// arms is drawn as (hero, monsters, skeletons), the crypt bat, every weapon's art, the
// reaving axe in the air, the whirl's cyclone and the hero's held weapon.
namespace Brickblade;

public static partial class G
{
/* =============================== DRAW: MINIFIGURE =============================== */
public static void drawFigure(Ctx c, Fig o){
  double s = o.s; Col T(Col col) => o.flash>0 ? mixc(col,"#ffffff",Math.min(1,o.flash*0.85)) : col;
  var C = o.colors; double w = o.walk;
  /* a whirl drives the legs even when the hero is turning on the spot */
  var mv = (o.moving || !double.IsNaN(o.whirlA)) ? 1 : 0;
  var bob = Math.abs(Math.sin(w))*2.2*s*mv + Math.sin(vtime*2+o.phase)*0.5;
  double fx = o.face != null ? Js.Or(o.face.x, 0) : 0, fy = o.face != null ? Js.Or(o.face.y, 1) : 1;
  var fln = Math.hypot(fx,fy);
  if(fln>0.001){ fx/=fln; fy/=fln; } else { fx=0; fy=1; }
  var vis = clamp(fy*0.5+0.55, 0, 1);
  var showFace = vis > 0.2;
  c.save(); c.translate(o.x, o.y - o.z);
  c.fillStyle = "rgba(15,30,20,0.3)";
  c.beginPath(); c.ellipse(0, 2, 15*s, 5.5*s, 0, 0, TAU); c.fill();
  if(o.elite){ c.strokeStyle="rgba(255,185,0,0.75)"; c.lineWidth=2.5;
    c.beginPath(); c.ellipse(0,2,19*s,7.5*s,0,0,TAU); c.stroke(); }
  if(o.magic){ c.setLineDash([5,4]); c.strokeStyle=MAGIC_RING; c.lineWidth=2.5;
    c.beginPath(); c.ellipse(0,2,19*s,7.5*s,0,0,TAU); c.stroke(); c.setLineDash([]); }
  if(o.lord){ c.setLineDash([3,3]); c.strokeStyle="rgba(220,200,255,0.6)"; c.lineWidth=2;
    c.beginPath(); c.ellipse(0,2,20*s,8*s,0,0,TAU); c.stroke(); c.setLineDash([]); }
  c.translate(0,-bob);
  /* A WHIRL. The body drops into a crouch, leans into the turn and keeps
     turning — east, south, west, north and round again — while the weapon is
     held straight OUT from the hand. The blade never twirls on its own; the
     hero is what rotates. */
  if(!double.IsNaN(o.whirlA)){
    c.translate(0, 4*s);
    c.scale(1.06, 0.84);              /* hunched down over the swing */
    c.rotate(Math.sin(o.whirlA*2)*0.10);
  }
  c.lineWidth = 2; c.lineJoin="round";
  var OL = "rgba(20,30,40,0.5)";
  if(o.style=="boss"){
    var sway = Math.sin(vtime*3+o.phase)*3;
    c.fillStyle = T("#7b1d22");
    c.beginPath();
    c.moveTo(-12*s,-27*s); c.lineTo(12*s,-27*s);
    c.lineTo(17*s+sway,2*s); c.lineTo(-17*s+sway,2*s); c.closePath();
    c.fill(); c.strokeStyle=OL; c.stroke();
  }
  var lift = Math.sin(w)*3.8*s*mv;
  double lL = Math.max(0,lift), lR = Math.max(0,-lift);
  c.fillStyle = T(C.legs); c.strokeStyle = OL;
  roundRect(c, -8.5*s, -9.5*s-lL, 7.5*s, 9.5*s+lL, 2*s); c.fill(); c.stroke();
  roundRect(c, 1*s, -9.5*s-lR, 7.5*s, 9.5*s+lR, 2*s); c.fill(); c.stroke();
  c.fillStyle = T(sh(C.legs,-0.3));
  c.fillRect(-8.5*s, -2.5*s-lL, 7.5*s, 2.5*s); c.fillRect(1*s, -2.5*s-lR, 7.5*s, 2.5*s);
  c.fillStyle = T(C.torso); c.strokeStyle = OL;
  c.beginPath();
  c.moveTo(-9*s,-13*s); c.lineTo(9*s,-13*s); c.lineTo(11*s,-28*s); c.lineTo(-11*s,-28*s);
  c.closePath(); c.fill(); c.stroke();
  /* a bloater: the belly that ate too much, straining over the belt */
  if(o.fat){
    c.fillStyle = T(sh(C.torso, 0.10));
    c.beginPath(); c.ellipse(0, -19*s, 14*s, 10.5*s, 0, 0, TAU); c.fill(); c.stroke();
    c.fillStyle = T(sh(C.head, -0.05));
    c.beginPath(); c.ellipse(1.5*s, -16*s, 6*s, 4*s, 0, 0, TAU); c.fill();
  }
  if(o.style=="zombie"){
    /* the shirt it was buried in: torn, and the skin showing through */
    c.fillStyle = T(sh(C.head, -0.12));
    c.beginPath(); c.moveTo(-6*s,-24*s); c.lineTo(-2*s,-22*s); c.lineTo(-5*s,-18*s); c.lineTo(-8*s,-20*s); c.closePath(); c.fill();
    c.beginPath(); c.moveTo(4*s,-17*s); c.lineTo(8*s,-15*s); c.lineTo(5*s,-13.5*s); c.closePath(); c.fill();
    c.strokeStyle = T(sh(C.torso,-0.45)); c.lineWidth = 1.2;
    c.beginPath(); c.moveTo(2*s,-26*s); c.lineTo(2*s,-20*s);
    for(var k=0;k<3;k++){ c.moveTo(0.5*s, (-25+k*2)*s); c.lineTo(3.5*s, (-25+k*2)*s); }
    c.stroke(); c.lineWidth = 2; c.strokeStyle = OL;
  } else if(o.style=="skel"||o.style=="lancer"){
    c.strokeStyle = T(sh(C.torso,-0.4)); c.lineWidth=1.6;
    for(var i=0;i<3;i++){ c.beginPath(); c.moveTo(-7*s,(-24+i*3.6)*s); c.lineTo(7*s,(-24+i*3.6)*s); c.stroke(); }
    c.lineWidth = 2; c.strokeStyle = OL;
  } else if(o.style=="hero"){
    c.strokeStyle = T(sh(C.torso,-0.28)); c.lineWidth=2;
    c.beginPath(); c.moveTo(-9.5*s,-16.5*s); c.lineTo(9.5*s,-16.5*s); c.stroke();
    c.fillStyle = T(PAL.gold);
    c.beginPath(); c.moveTo(0,-26*s); c.lineTo(3.4*s,-22.5*s); c.lineTo(0,-19*s); c.lineTo(-3.4*s,-22.5*s);
    c.closePath(); c.fill(); c.strokeStyle=OL; c.lineWidth=1.4; c.stroke(); c.lineWidth=2;
  } else if(o.style=="brute"){
    c.fillStyle = T("#8b8f8a");
    roundRect(c,-6.5*s,-26*s,13*s,11*s,2*s); c.fill(); c.strokeStyle=OL; c.stroke();
    if(o.lava){
      c.strokeStyle = T("#ff9a3c"); c.lineWidth=1.8;
      c.beginPath();
      c.moveTo(-4*s,-26*s); c.lineTo(-1*s,-22*s); c.lineTo(-4*s,-19*s); c.lineTo(0,-16*s);
      c.moveTo(4*s,-25*s); c.lineTo(2*s,-21*s); c.lineTo(5*s,-18*s);
      c.stroke();
      c.lineWidth = 2; c.strokeStyle = OL;
    }
  } else if(o.style=="knight"){
    c.fillStyle = T("#b8bdb9");
    roundRect(c,-6.5*s,-26*s,13*s,11*s,2*s); c.fill(); c.strokeStyle=OL; c.stroke();
  } else if(o.style=="bomber"){
    c.fillStyle = T(PAL.red);
    c.fillRect(-10.5*s,-21*s,21*s,4*s);
    c.strokeStyle=OL; c.strokeRect(-10.5*s,-21*s,21*s,4*s);
  } else if(o.style=="boss"){
    c.fillStyle = T(PAL.red);
    c.beginPath(); c.moveTo(0,-26.5*s); c.lineTo(4.5*s,-21*s); c.lineTo(0,-15.5*s); c.lineTo(-4.5*s,-21*s);
    c.closePath(); c.fill(); c.strokeStyle=OL; c.lineWidth=1.4; c.stroke(); c.lineWidth=2;
  }
  var walkA = Math.sin(w)*0.5*mv;
  double aL = -0.32 - walkA, aR = 0.32 + walkA; Pt gripR = null;
  if(!double.IsNaN(o.bladeAng)){
    gripR = new Pt(Math.cos(o.bladeAng)*14*s, -20*s + Math.sin(o.bladeAng)*10*s);
    aR = 0;
  }
  /* the zombie's reach: both arms out in front of it, swaying as it shambles */
  if(o.zombieArms && !o.armsUp){ aL = -1.4 - walkA*0.25; aR = 1.4 + walkA*0.25; gripR = null; }
  if(o.armsUp){ aL = -2.6; aR = 2.6; gripR = null; }
  /* THE SLAM. `o.slam` runs 0 (mace overhead, both hands on the haft) to 1 (mace
     driven into the floor in front). The far arm crosses over to grip it, which
     is what sells the weight — one-handing a slam looks like a wave. */
  if(!double.IsNaN(o.slam)){
    var k = clamp(o.slam, 0, 1);
    aL = -2.75 + k*3.95;
    aR =  2.70 - k*1.45;
    gripR = null;
  }
  double armLen = 12*s, handR = (o.style=="brute"?5:3.5)*s;
  Pt shL = new Pt(-10*s, -26.5*s), shR = new Pt(10*s, -26.5*s);
  var handL = new Pt(shL.x + Math.sin(aL)*armLen, shL.y + Math.cos(aL)*armLen);
  var handR2 = gripR != null ? new Pt(shR.x+(gripR.x-shR.x)*0.85, shR.y+(gripR.y-shR.y)*0.85)
    : new Pt(shR.x + Math.sin(aR)*armLen, shR.y + Math.cos(aR)*armLen);
  /* the weapon arm reaches out along the turn; the other braces across the chest */
  if(!double.IsNaN(o.whirlA)){
    var wl = 19*s;
    handR2.x = shR.x + Math.cos(o.whirlA)*wl;
    handR2.y = shR.y + Math.sin(o.whirlA)*wl*0.62;
    handL.x = shL.x - Math.cos(o.whirlA)*7*s;
    handL.y = shL.y - Math.sin(o.whirlA)*4*s + 5*s;
  }
  c.strokeStyle = T(C.torso); c.lineWidth = 6*s; c.lineCap="round";
  c.beginPath(); c.moveTo(shL.x,shL.y); c.lineTo(handL.x,handL.y); c.stroke();
  c.beginPath(); c.moveTo(shR.x,shR.y); c.lineTo(handR2.x,handR2.y); c.stroke();
  c.lineWidth = 2;
  if((o.style=="skel"||o.style=="lancer") && !double.IsNaN(o.bowPull)){
    var bowA = Math.atan2(fy,fx);
    double hx = handL.x, hy = handL.y;
    c.save(); c.translate(hx,hy); c.rotate(bowA);
    c.strokeStyle = T(PAL.brown); c.lineWidth=2.6;
    c.beginPath(); c.arc(0,0,9*s,-1.15,1.15); c.stroke();
    c.strokeStyle = T("#e8e4d8"); c.lineWidth=1.2;
    double px = Math.cos(1.15)*9*s, py = Math.sin(1.15)*9*s;
    c.beginPath(); c.moveTo(px,-py); c.lineTo(-o.bowPull*5*s,0); c.lineTo(px,py); c.stroke();
    if(o.bowPull>0.2){ c.strokeStyle=T("#8b8f8a"); c.lineWidth=2;
      c.beginPath(); c.moveTo(-o.bowPull*5*s,0); c.lineTo(10*s,0); c.stroke(); }
    c.restore(); c.lineWidth=2;
  }
  c.fillStyle = T(C.hands); c.strokeStyle = OL;
  c.beginPath(); c.arc(handL.x,handL.y,handR,0,TAU); c.fill(); c.stroke();
  /* The weapon goes down BEFORE the weapon hand, so the grip passes behind the
     fist and the fist closes over it. Drawn the other way round the pommel sat
     on top of the hand and nobody appeared to be holding anything. */
  /* AT REST THE HERO SHOULDERS IT. A weapon trailing off at the arm's angle
     read like it had been dropped; carried straight up it reads like it is
     about to be used. Only the hero does this — a boss mid-swing must not. */
  var IDLE_UP = -Math.PI/2.0;
  /* the hero's axe rests shouldered on a lean rather than dead upright — with a
     bit to either side of the haft, straight up buries the inner one behind the
     minifig's head */
  var AXE_LEAN = 0.5;
  var heldAng = (o.hero && double.IsNaN(o.whirlA)) ? IDLE_UP
                : (!double.IsNaN(o.whirlA) ? o.whirlA : double.NaN);
  if(o.weapon=="sword" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    /* dead-on the turn angle: the blade sticks out radially and stays there */
    c.rotate(!double.IsNaN(heldAng) ? heldAng : aR+0.6);
    drawBlade(c, s*(!double.IsNaN(o.whirlA) ? 1.1 : 0.9)*(Js.Or(o.wScale, 1)), !double.IsNaN(o.whirlA));
    c.restore();
  }
  /* the hero's own axe and staff — gated on bladeAng like the sword, so the
     swing arc draws the weapon during a swing instead of it appearing twice */
  if(o.weapon=="axe" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    /* TWO BITS NEED ROOM. Straight up, the inner bit lands square behind the
       minifig's head and the axe reads as single-bitted; shouldered on a lean
       it clears the skull and both edges show. */
    c.rotate(!double.IsNaN(heldAng) ? heldAng + (heldAng==IDLE_UP ? AXE_LEAN : 0) : aR+0.6);
    drawAxe(c, s*(!double.IsNaN(o.whirlA) ? 1.1 : 0.9)*(Js.Or(o.wScale, 1)), !double.IsNaN(o.whirlA));
    c.restore();
  }
  if(o.weapon=="scepter" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    /* HELD STRAIGHT. Its art runs up the y axis where a blade's runs along x, so
       the blade's shouldering angle laid it flat; a quarter turn stands it up */
    c.rotate((!double.IsNaN(heldAng) ? heldAng : aR+0.3) + Math.PI/2.0);
    drawScepterArt(c, s*0.95, false);
    c.restore();
  }
  if(o.weapon=="bow" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    /* A BOW IS HELD UPRIGHT, not pointed like a blade: a quarter turn clockwise
       from the sword's grip stands the limbs up and down with the belly out
       front and the string towards the body */
    c.rotate((!double.IsNaN(heldAng) ? heldAng : aR+0.2) + BOW_HOLD_TURN);
    drawBowArt(c, s*0.95, false);
    c.restore();
  }
  if(o.weapon=="pstaff" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    c.rotate(!double.IsNaN(heldAng) ? heldAng : aR+0.6);
    drawStaffArt(c, s*(!double.IsNaN(o.whirlA) ? 1.1 : 0.9), !double.IsNaN(o.whirlA));
    c.restore();
  }
  /* HOW A MONSTER (OR A SKELETON) CARRIES ITS WEAPON. It used to take the
     arm's own swing angle, which left a spear pointing at the floor and a blade
     dangling off the wrist; and the moment the arms went up for an attack the
     weapon was simply not drawn. Carried now the way the hero carries his --
     upright, leaning a little forward -- and cocked back overhead while the
     arms are up, so the wind-up shows what is about to come down. Art that runs
     along x (blades, maces) and art that runs along y (spears, staves) each get
     their own quarter turn of the same angle. */
  var carryX = o.armsUp ? -Math.PI/2.0 - 0.45 : -Math.PI/2.0 + 0.32;
  var carryY = carryX + Math.PI/2.0;
  if(o.weapon=="mace" && double.IsNaN(o.bladeAng)){
    c.save(); c.translate(handR2.x,handR2.y);
    /* overhead at -2.0, through the arc, into the floor at +1.1 */
    c.rotate(!double.IsNaN(o.slam) ? (-2.0 + clamp(o.slam,0,1)*3.1)
      : !double.IsNaN(heldAng) ? heldAng : carryX);
    drawMace(c, s*((!double.IsNaN(o.whirlA) || !double.IsNaN(o.slam)) ? 1.15 : 1)*(Js.Or(o.wScale, 1)));
    c.restore();
  }
  if(o.weapon=="greatsword"){
    c.save(); c.translate(handR2.x,handR2.y);
    c.rotate(!double.IsNaN(o.whirlA) ? o.whirlA : carryX);
    drawGreatsword(c, s*(!double.IsNaN(o.whirlA) ? 1.1 : 0.95)*(Js.Or(o.wScale, 1)));
    c.restore();
  }
  if(o.weapon=="spear"){
    c.save(); c.translate(handR2.x,handR2.y); c.rotate(carryY);
    drawSpear(c, s*(Js.Or(o.wScale, 1)));
    c.restore();
  }
  if(o.weapon=="staff"){
    c.save(); c.translate(handR2.x,handR2.y);
    /* a bossstaff is planted upright; any other staff is carried upright too,
       and raised a little higher while it casts */
    c.rotate(o.staffUpright ? 0 : carryY*0.6);
    c.strokeStyle = T("#4a3524"); c.lineWidth=(o.staffUpright?3.4:2.4)*s;
    c.beginPath(); c.moveTo(0,(o.staffUpright?16:10)*s); c.lineTo(0,-(o.staffUpright?36:28)*s); c.stroke();
    if(o.staffUpright){
      /* bone rings down the shaft */
      c.strokeStyle = T(sh(C.head,-0.1)); c.lineWidth = 1.6*s;
      foreach (var ry in new double[]{-8, 0, 8}){
        c.beginPath(); c.moveTo(-3.6*s, ry*s); c.lineTo(3.6*s, ry*s); c.stroke();
      }
      /* a claw cradling the orb */
      c.strokeStyle = T(C.head); c.lineWidth = 2.4*s; c.lineCap="round";
      foreach (var sgn in new double[]{-1, 1}){
        c.beginPath();
        c.moveTo(sgn*2*s, -32*s);
        c.quadraticCurveTo(sgn*8*s, -37*s, sgn*4.5*s, -44*s);
        c.stroke();
      }
      c.lineCap="butt";
    }
    var orb = o.orbCol ?? "#a5ff5a";
    var orbY = o.staffUpright ? -39 : -31;
    var orbR = (o.staffUpright ? 5.2 : 3.4) + (o.castGlow?Math.sin(vtime*18)*1.2:0);
    { var oc = prs(orb); c.fillStyle = rgba(oc[0], oc[1], oc[2], 0.30); }
    c.beginPath(); c.arc(0,orbY*s,orbR*2*s,0,TAU); c.fill();
    c.fillStyle = T(orb);
    c.beginPath(); c.arc(0,orbY*s,orbR*s,0,TAU); c.fill();
    c.strokeStyle = OL; c.lineWidth=1.4; c.stroke();
    c.restore();
  }
  /* the weapon hand, last, closing over whatever it is holding */
  c.fillStyle = T(C.hands); c.strokeStyle = OL;
  c.beginPath(); c.arc(handR2.x,handR2.y,handR,0,TAU); c.fill(); c.stroke();
  double hy2 = -37*s, hr = 8.5*s;
  c.fillStyle = T(C.head); c.strokeStyle = OL;
  c.beginPath(); c.arc(0,hy2,hr,0,TAU); c.fill(); c.stroke();
  c.fillStyle = T(sh(C.head,0.14));
  c.beginPath(); c.ellipse(0,hy2-hr+1.2,4.4*s,2.2*s,0,0,TAU); c.fill(); c.stroke();
  var exs = fx*3.6*s*(0.35+0.65*vis);
  var sep = 3.2*s*(0.5+0.5*vis);
  var eyeY = hy2 + fy*2.4*s - 0.6*s;
  if(o.style=="skel"){
    if(showFace){
      c.fillStyle = T("#1b2a34");
      var er = 2.5*s*(0.6+0.4*vis);
      c.beginPath(); c.arc(-sep*0.9+exs,eyeY,er,0,TAU); c.arc(sep*0.9+exs,eyeY,er,0,TAU); c.fill();
      if(vis>0.35){
        c.fillStyle = T(sh(C.head,-0.35));
        var ty = hy2+4.0*s+fy*1.4*s;
        for(var i=-2;i<=2;i++) c.fillRect(i*1.7*s+exs-0.5*s, ty, 1*s, 2.2*s);
      }
    }
  } else if(o.style=="boss"){
    double vx0 = fx*2.6*s*vis, vy0 = fy*2.4*s;
    c.fillStyle = T("#101418");
    c.fillRect(-1.4*s+vx0, hy2-6.5*s+vy0, 2.8*s, 9*s);
    c.fillRect(-5*s+vx0, hy2-1.5*s+vy0, 10*s, 2.8*s);
    if(showFace){
      c.fillStyle = T("#ff3b30");
      c.beginPath(); c.arc(-2.4*s+vx0,hy2+0.4*s+vy0,1.1*s,0,TAU);
      c.arc(2.4*s+vx0,hy2+0.4*s+vy0,1.1*s,0,TAU); c.fill();
    }
  } else if(o.style=="hexer"){
    if(showFace){
      c.fillStyle = T(o.orbCol ?? "#a5ff5a");
      var er = 1.7*s*(0.7+0.3*vis);
      c.beginPath(); c.arc(-sep*0.85+exs,eyeY,er,0,TAU); c.arc(sep*0.85+exs,eyeY,er,0,TAU); c.fill();
    }
  } else if(o.style=="stalker"){
    c.fillStyle = T("#ff3b30");
    var er = 1.6*s*(0.7+0.3*vis);
    c.beginPath(); c.arc(-sep*0.85+exs,eyeY,er,0,TAU); c.arc(sep*0.85+exs,eyeY,er,0,TAU); c.fill();
  } else if(o.style=="knight"){
    if(vis>0.25){
      c.fillStyle = T("#1b2a34");
      var wS = 10*s*(0.45+0.55*vis);
      c.fillRect(fx*2.5*s - wS/2.0, hy2-2.2*s+fy*2.2*s, wS, 2.4*s);
    }
  } else if(showFace){
    c.fillStyle = T("#1b2a34");
    if(o.blink>0){
      c.fillRect(-sep+exs-1.7*s,eyeY-0.7*s,3.4*s,1.4*s);
      c.fillRect(sep+exs-1.7*s,eyeY-0.7*s,3.4*s,1.4*s);
    } else {
      var er = 1.5*s*(0.75+0.25*vis);
      c.beginPath(); c.arc(-sep+exs,eyeY,er,0,TAU); c.arc(sep+exs,eyeY,er,0,TAU); c.fill();
    }
    c.strokeStyle = T("#1b2a34"); c.lineWidth = 1.5*s;
    if(o.angry){
      c.beginPath();
      c.moveTo(-sep-2.2*s+exs,eyeY-3.2*s); c.lineTo(-sep+1.4*s+exs,eyeY-1.6*s);
      c.moveTo(sep+2.2*s+exs,eyeY-3.2*s); c.lineTo(sep-1.4*s+exs,eyeY-1.6*s);
      c.stroke();
      c.fillStyle = T("#1b2a34");
      c.beginPath(); c.ellipse(exs, eyeY+3.8*s, 2*s, 2.4*s, 0, 0, TAU); c.fill();
    } else {
      c.beginPath(); c.arc(exs, eyeY+2.4*s, 3.0*s*(0.55+0.45*vis), 0.25, Math.PI-0.25); c.stroke();
    }
    c.lineWidth = 2;
  }
  if(o.style=="goblin"){
    /* long green ears, swept out sideways */
    c.fillStyle = T(o.colors.head); c.strokeStyle = OL;
    foreach (var side in new double[]{-1, 1}){
      c.beginPath();
      c.moveTo(side*hr*0.75, hy2 - 3*s);
      c.quadraticCurveTo(side*(hr + 8*s), hy2 - 8*s, side*(hr + 14*s), hy2 - 11*s);
      c.quadraticCurveTo(side*(hr + 6*s), hy2 + 1*s, side*hr*0.75, hy2 + 3*s);
      c.closePath(); c.fill(); c.stroke();
    }
  }
  if(o.style=="imp"){
    /* the big one wears the same horns, grown: longer, swept back, and a ridge
       of spines between them. Same silhouette, read at twice the size. */
    var k = o.bigImp ? 1.9 : 1;
    c.fillStyle = T(o.bigImp ? "#6e0f14" : "#8f161d"); c.strokeStyle = OL;
    foreach (var side in new double[]{-1, 1}){
      c.beginPath();
      c.moveTo(side*6*s, hy2-6*s);
      c.quadraticCurveTo(side*(9+2*k)*s, hy2-(9+2*k)*s, side*(9*k)*s, hy2-12*k*s);
      c.lineTo(side*3.5*s, hy2-8.4*s);
      c.closePath(); c.fill(); c.stroke();
    }
    if(o.bigImp){
      c.fillStyle = T("#8f161d");
      foreach (var dx in new double[]{-3.2, 0, 3.2}){
        c.beginPath();
        c.moveTo((dx-1.8)*s, hy2-hr*0.9);
        c.lineTo(dx*s, hy2-hr-4.4*s);
        c.lineTo((dx+1.8)*s, hy2-hr*0.9);
        c.closePath(); c.fill(); c.stroke();
      }
    }
  }
  if(o.style=="hero"){
    c.fillStyle = T(C.hair); c.strokeStyle = OL;
    c.beginPath(); c.arc(0,hy2-1*s,hr+1.6*s, Math.PI*1.02, Math.PI*1.98);
    c.lineTo(hr*0.9,hy2-2.4*s); c.lineTo(hr*0.45,hy2-4*s); c.lineTo(0,hy2-2.6*s);
    c.lineTo(-hr*0.45,hy2-4*s); c.lineTo(-hr*0.9,hy2-2.4*s);
    c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="skel" && !o.noHelm){
    c.fillStyle = T("#5c5e59"); c.strokeStyle = OL;
    c.beginPath(); c.arc(0,hy2-0.5*s,hr+2*s, Math.PI, TAU); c.closePath(); c.fill(); c.stroke();
    c.fillRect(-hr-1.4*s, hy2-1*s, 3.4*s, 7*s); c.strokeRect(-hr-1.4*s, hy2-1*s, 3.4*s, 7*s);
    c.fillRect(hr-2*s, hy2-1*s, 3.4*s, 7*s); c.strokeRect(hr-2*s, hy2-1*s, 3.4*s, 7*s);
  }
  if(o.wings){
    /* two membrane wings beating behind the shoulders */
    var beat = Math.sin(vtime*7 + o.phase);
    foreach (var side in new double[]{-1, 1}){
      c.save(); c.translate(side*8*s, -30*s); c.scale(side, 1);
      c.rotate(-0.35 + beat*0.4);
      c.fillStyle = "rgba(70,54,104,0.95)";
      c.strokeStyle = "rgba(20,14,34,0.75)"; c.lineWidth = 2;
      c.beginPath();
      c.moveTo(0,0);
      c.quadraticCurveTo(22*s, -16*s, 40*s, -6*s);
      c.lineTo(31*s, 0);  c.lineTo(36*s, 7*s);
      c.lineTo(24*s, 5*s); c.lineTo(27*s, 12*s);
      c.lineTo(12*s, 8*s); c.closePath();
      c.fill(); c.stroke();
      /* wing ribs */
      c.strokeStyle = "rgba(20,14,34,0.5)"; c.lineWidth = 1.4;
      foreach (var t in new double[]{0.35, 0.62, 0.85}){
        c.beginPath(); c.moveTo(2*s, 2*s);
        c.lineTo(36*s*t, -8*s*t + 8*s*t*t); c.stroke();
      }
      c.restore();
    }
  }
  if(o.horns){
    c.fillStyle = T("#4a4b47"); c.strokeStyle = OL;
    c.beginPath(); c.moveTo(-7*s,hy2-4*s); c.lineTo(-12*s,hy2-12*s); c.lineTo(-4*s,hy2-7.5*s);
    c.closePath(); c.fill(); c.stroke();
    c.beginPath(); c.moveTo(7*s,hy2-4*s); c.lineTo(12*s,hy2-12*s); c.lineTo(4*s,hy2-7.5*s);
    c.closePath(); c.fill(); c.stroke();
    c.fillStyle = T("#5c5e59");
    c.beginPath(); c.arc(0,hy2-0.5*s,hr+1.6*s, Math.PI, TAU); c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="spitter" || o.style=="hexer"){
    c.fillStyle = T(o.style=="hexer" ? (o.hoodCol ?? "#2c1d3d") : "#2f5e23"); c.strokeStyle = OL;
    c.beginPath();
    c.moveTo(-hr-1*s,hy2+2*s);
    c.quadraticCurveTo(-hr-7*s,hy2-7*s, -2*s,hy2-hr-2*s);
    c.quadraticCurveTo(0,hy2-hr+2*s, 2*s,hy2-hr-2*s);
    c.quadraticCurveTo(hr+7*s,hy2-7*s, hr+1*s,hy2+2*s);
    c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="knight"){
    c.fillStyle = T("#b8bdb9"); c.strokeStyle = OL;
    c.beginPath();
    c.arc(0,hy2,hr+1.8*s, Math.PI*0.98, Math.PI*2.02);
    c.lineTo(hr*0.95,hy2+3.5*s); c.lineTo(-hr*0.95,hy2+3.5*s);
    c.closePath(); c.fill(); c.stroke();
    c.fillStyle = T(PAL.red);
    c.beginPath();
    c.moveTo(0,hy2-hr-1*s);
    c.quadraticCurveTo(6*s,hy2-hr-9*s, 11*s,hy2-hr-5*s);
    c.quadraticCurveTo(6*s,hy2-hr-3.5*s, 0,hy2-hr-1*s);
    c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="lancer"){
    c.fillStyle = T("#6d6e68"); c.strokeStyle = OL;
    c.beginPath(); c.arc(0,hy2-0.5*s,hr+1.6*s, Math.PI, TAU); c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="brute"){
    c.fillStyle = T("#3d3e3b"); c.strokeStyle = OL;
    c.beginPath(); c.arc(0,hy2-1*s,hr+2.2*s, Math.PI, TAU); c.closePath(); c.fill(); c.stroke();
    c.beginPath(); c.moveTo(-7*s,hy2-5*s); c.lineTo(-12*s,hy2-11*s); c.lineTo(-4.5*s,hy2-8*s);
    c.closePath(); c.fill(); c.stroke();
    c.beginPath(); c.moveTo(7*s,hy2-5*s); c.lineTo(12*s,hy2-11*s); c.lineTo(4.5*s,hy2-8*s);
    c.closePath(); c.fill(); c.stroke();
  }
  if(o.style=="bomber"){
    c.strokeStyle = T("#1b2a34"); c.lineWidth = 2*s;
    c.beginPath(); c.moveTo(0,hy2-hr+1*s);
    c.quadraticCurveTo(3*s,hy2-hr-6*s, 7*s,hy2-hr-7*s); c.stroke();
    c.fillStyle = "#ffd166";
    c.beginPath();
    c.arc(7*s, hy2-hr-7*s, (2.2+Math.sin(vtime*22+o.phase)*0.9)*s, 0, TAU); c.fill();
    c.lineWidth = 2;
  }
  if(o.style=="boss" || o.crown){
    c.fillStyle = T("#2f3438"); c.strokeStyle = OL;
    c.beginPath(); c.arc(0,hy2-1*s,hr+2.4*s, Math.PI, TAU); c.closePath(); c.fill(); c.stroke();
    c.fillStyle = T(PAL.gold);
    c.beginPath();
    c.moveTo(-7*s,hy2-hr-0.5*s); c.lineTo(-7*s,hy2-hr-5*s); c.lineTo(-3.8*s,hy2-hr-2.4*s);
    c.lineTo(0,hy2-hr-6*s); c.lineTo(3.8*s,hy2-hr-2.4*s); c.lineTo(7*s,hy2-hr-5*s); c.lineTo(7*s,hy2-hr-0.5*s);
    c.closePath(); c.fill(); c.stroke();
    c.fillStyle = T(PAL.red);
    c.beginPath(); c.arc(0,hy2-hr-2.6*s,1.3*s,0,TAU); c.fill();
  }
  c.restore();
}
/* crypt bat — a winged floating skull-brick: no legs, all wings */
public static void drawCryptBat(Ctx c, Ent e){
  var s = e.scale;
  var flap = Math.sin(vtime*16 + e.phase);
  var fz = 12 + Math.sin(vtime*5 + e.phase)*3;
  Col T(Col col) => e.flash>0 ? mixc(col,"#ffffff",Math.min(1,e.flash*0.85)) : col;
  c.fillStyle = "rgba(15,30,20,0.22)";
  c.beginPath(); c.ellipse(e.x, e.y, 10*s, 4*s, 0, 0, TAU); c.fill();
  if(e.elite){ c.strokeStyle="rgba(255,185,0,0.75)"; c.lineWidth=2;
    c.beginPath(); c.ellipse(e.x,e.y,13*s,5*s,0,0,TAU); c.stroke(); }
  if(e.magic){ c.setLineDash([5,4]); c.strokeStyle=MAGIC_RING; c.lineWidth=2;
    c.beginPath(); c.ellipse(e.x,e.y,13*s,5*s,0,0,TAU); c.stroke(); c.setLineDash([]); }
  c.save(); c.translate(e.x, e.y - fz);
  /* wings */
  foreach (var side in new double[]{-1,1}){
    c.save(); c.translate(side*5*s, -14*s); c.rotate(side*flap*0.45);
    c.fillStyle = T("#3a3350"); c.strokeStyle="rgba(15,20,30,0.55)"; c.lineWidth=1.6;
    c.beginPath();
    c.moveTo(0,0);
    c.quadraticCurveTo(side*12*s, -9*s, side*20*s, -1*s + flap*3*s);
    c.quadraticCurveTo(side*13*s, 3*s, side*8*s, 5*s);
    c.quadraticCurveTo(side*3.5*s, 4*s, 0, 2*s);
    c.closePath(); c.fill(); c.stroke();
    c.strokeStyle="rgba(90,79,112,0.7)"; c.lineWidth=1;
    c.beginPath(); c.moveTo(0,0); c.lineTo(side*15*s,-4*s);
    c.moveTo(0,1*s); c.lineTo(side*10*s,3*s); c.stroke();
    c.restore();
  }
  /* floating body — a small brick cocoon, no legs */
  c.fillStyle = T("#4a3f5e"); c.strokeStyle="rgba(15,20,30,0.6)"; c.lineWidth=2;
  roundRect(c,-7*s,-19*s,14*s,13*s,4*s); c.fill(); c.stroke();
  c.fillStyle = T("#3a3350");
  roundRect(c,-7*s,-10*s,14*s,4*s,2*s); c.fill();
  /* face plate */
  c.fillStyle = T("#5a4f70");
  c.beginPath(); c.arc(0,-14*s,6*s,0,TAU); c.fill();
  c.strokeStyle="rgba(15,20,30,0.6)"; c.stroke();
  /* glowing eyes + fangs */
  c.fillStyle = T("#ff3b30");
  c.beginPath(); c.arc(-2.4*s,-15*s,1.5*s,0,TAU); c.arc(2.4*s,-15*s,1.5*s,0,TAU); c.fill();
  c.fillStyle = "#eef0ee";
  c.beginPath(); c.moveTo(-2.4*s,-10*s); c.lineTo(-1.6*s,-7.6*s); c.lineTo(-0.8*s,-10*s); c.closePath(); c.fill();
  c.beginPath(); c.moveTo(2.4*s,-10*s); c.lineTo(1.6*s,-7.6*s); c.lineTo(0.8*s,-10*s); c.closePath(); c.fill();
  /* frost tint */
  if(e.slowT>0){
    c.globalAlpha = 0.4;
    c.fillStyle = "rgba(150,215,255,0.5)";
    c.beginPath(); c.ellipse(0,-13*s,12*s,9*s,0,0,TAU); c.fill();
    c.globalAlpha = 1;
  }
  c.restore();
}
public static void drawBlade(Ctx c, double s, bool bright = false){
  c.strokeStyle = "rgba(20,30,40,0.5)"; c.lineWidth = 2; c.lineJoin="round";
  c.fillStyle = bright ? "#ffffff" : "#cfd6dd";
  c.beginPath();
  c.moveTo(1.5*s,-3.2*s); c.lineTo(26*s,-3.2*s); c.lineTo(33*s,0); c.lineTo(26*s,3.2*s); c.lineTo(1.5*s,3.2*s);
  c.closePath(); c.fill(); c.stroke();
  c.strokeStyle = bright ? "#dddddd" : "#eef2f4"; c.lineWidth=1.4;
  c.beginPath(); c.moveTo(3*s,0); c.lineTo(26*s,0); c.stroke();
  c.fillStyle = PAL.gold;
  c.fillRect(-2.2*s,-6*s,3.4*s,12*s);
  c.strokeStyle = "rgba(20,30,40,0.5)"; c.strokeRect(-2.2*s,-6*s,3.4*s,12*s);
  c.fillStyle = PAL.brown; c.fillRect(-7*s,-2.4*s,5*s,4.8*s); c.strokeRect(-7*s,-2.4*s,5*s,4.8*s);
}
/* A boss greatsword: a broad blade with a fuller, a winged crossguard and a
   jewelled pommel. Drawn pointing along +x from the grip, like every other
   weapon here, so the whirl can just rotate it. */
public static void drawGreatsword(Ctx c, double s){
  c.lineJoin = "round";
  /* blade */
  c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 2;
  c.fillStyle = "#e6edf2";
  c.beginPath();
  c.moveTo(6*s, -6.4*s);
  c.lineTo(40*s, -5.2*s); c.lineTo(52*s, 0); c.lineTo(40*s, 5.2*s);
  c.lineTo(6*s, 6.4*s);
  c.closePath(); c.fill(); c.stroke();
  /* fuller down the middle */
  c.strokeStyle = "#aebcc6"; c.lineWidth = 1.8*s;
  c.beginPath(); c.moveTo(9*s, 0); c.lineTo(41*s, 0); c.stroke();
  c.strokeStyle = "rgba(255,255,255,0.85)"; c.lineWidth = 1.1*s;
  c.beginPath(); c.moveTo(10*s, -2.6*s); c.lineTo(38*s, -2.2*s); c.stroke();
  /* winged crossguard */
  c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 2;
  c.fillStyle = PAL.gold;
  c.beginPath();
  c.moveTo(2*s, -11*s); c.lineTo(8*s, -4*s); c.lineTo(8*s, 4*s); c.lineTo(2*s, 11*s);
  c.lineTo(-1.5*s, 7*s); c.lineTo(-1.5*s, -7*s);
  c.closePath(); c.fill(); c.stroke();
  /* grip and pommel */
  c.fillStyle = "#4a3524";
  roundRect(c, -12*s, -2.6*s, 11*s, 5.2*s, 2*s); c.fill(); c.stroke();
  c.fillStyle = "#d3242b";
  c.beginPath(); c.arc(-13.5*s, 0, 3.6*s, 0, TAU); c.fill(); c.stroke();
}
/* An axe: a haft carrying two broad crescent bits, one to each side, their
   edges facing opposite ways. Drawn along +x from the grip, like everything
   else here, so a whirl or a throw is just a rotation. */
public static void drawAxe(Ctx c, double s, bool bright = false){
  c.lineJoin = "round";

  var dark = "rgba(20,30,40,0.55)";
  var steel = bright ? "#ffffff" : "#cfd6dd";
  /* THE HEAD IS MOUNTED ACROSS THE HAFT, not along it. An axe bit sits at right
     angles to the shaft with its edge running parallel to it — a head pointing
     the same way the handle does is a mace on a stick, which is exactly what
     this looked like. Haft along +x, bits standing off to either side.
     DOUBLE-BIT: one bit on each side of the haft, edges facing opposite ways,
     so the axe cuts on the backswing as well as the swing. The two are the same
     shape mirrored about the haft, which keeps the head balanced on the shaft
     however the weapon is turned. */
  c.strokeStyle = dark; c.lineWidth = 2;
  c.fillStyle = PAL.brown;
  c.fillRect(-9*s, -2.4*s, 43*s, 4.8*s);
  c.strokeRect(-9*s, -2.4*s, 43*s, 4.8*s);

  /* one bit; d flips it to the other side of the haft */
  void bit(double d){
    c.strokeStyle = dark; c.lineWidth = 2;
    c.fillStyle = steel;
    c.beginPath();
    c.moveTo(20*s, 2*s*d);                                    /* root, toe side */
    c.lineTo(15*s, 12*s*d);                                   /* the beard, hooking back */
    c.quadraticCurveTo(24*s, 22*s*d, 36*s, 19*s*d);           /* THE EDGE, along the haft */
    c.quadraticCurveTo(34*s, 9*s*d, 31*s, 2*s*d);             /* heel back down to the haft */
    c.closePath(); c.fill(); c.stroke();
    /* the bevel, traced just inside the edge */
    c.strokeStyle = bright ? "#dcdcdc" : "#f4f8fa"; c.lineWidth = 2.2;
    c.beginPath();
    c.moveTo(17*s, 13*s*d);
    c.quadraticCurveTo(25*s, 19.5*s*d, 34*s, 17*s*d);
    c.stroke();
  }
  bit(1); bit(-1);

  /* Eye — centred now that there is a bit hanging off either side */
  c.strokeStyle = dark;
  c.lineWidth = 2;
  c.fillStyle = bright ? "#c8ced4" : "#9ba19d";
  c.beginPath();
  c.moveTo(21*s, 4.5*s);  c.lineTo(30*s, 4.5*s);
  c.lineTo(30*s, -4.5*s); c.lineTo(21*s, -4.5*s);
  c.closePath(); c.fill(); c.stroke();
  /* butt cap, so the grip end reads as a grip */
  c.fillStyle = PAL.gold;
  c.fillRect(-11*s, -3.2*s, 3.4*s, 6.4*s);
  c.strokeRect(-11*s, -3.2*s, 3.4*s, 6.4*s);
}
/* The hero's staff: a bound shaft with a stone in the head. Its THROWN art is a
   sword, because the staff's own spellwork is not built yet. */
public static void drawStaffArt(Ctx c, double s, bool bright = false){
  c.lineJoin = "round";
  c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 2;
  c.fillStyle = "#6b4a2e";
  c.fillRect(-9*s, -1.8*s, 40*s, 3.6*s);
  c.strokeRect(-9*s, -1.8*s, 40*s, 3.6*s);
  c.strokeStyle = "#3f2c1b"; c.lineWidth = 1.6*s;
  foreach (var x in new double[]{6, 12, 18}){
    c.beginPath(); c.moveTo(x*s, -2.4*s); c.lineTo(x*s, 2.4*s); c.stroke();
  }
  c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 2;
  c.fillStyle = bright ? "#e6d0ff" : "#9b45d6";
  c.beginPath(); c.arc(33*s, 0, 6.5*s, 0, TAU); c.fill(); c.stroke();
  c.fillStyle = "rgba(255,255,255,0.75)";
  c.beginPath(); c.arc(31*s, -2*s, 2.2*s, 0, TAU); c.fill();
}
/* A SHORT BOW: a curved stave, a string, a grip in the middle of it. Drawn
   pointing along +x like every other held weapon. */
public const double BOW_HOLD_TURN = Math.PI/2.0;
public static void drawBowArt(Ctx c, double s, bool bright = false){
  c.lineCap = "round";
  c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 5*s;
  c.beginPath(); c.moveTo(-4*s, -17*s); c.quadraticCurveTo(12*s, 0, -4*s, 17*s); c.stroke();
  c.strokeStyle = bright ? "#e8d0a0" : "#8a5a2b"; c.lineWidth = 3.2*s;
  c.beginPath(); c.moveTo(-4*s, -17*s); c.quadraticCurveTo(12*s, 0, -4*s, 17*s); c.stroke();
  c.strokeStyle = "#efe8d8"; c.lineWidth = 1*s;
  c.beginPath(); c.moveTo(-4*s, -17*s); c.lineTo(-4*s, 17*s); c.stroke();
  c.fillStyle = "#3f2c1b";
  c.fillRect(1.5*s, -3.2*s, 3.2*s, 6.4*s);
}
/* ONE DISPATCHER. Everywhere that used to hardcode a sword asks for art by name
   instead, so a new weapon draws itself across the whole game the moment its
   row names an art. */
public static void drawWeaponArt(Ctx c, double s, bool bright, string kind){
  if(string.IsNullOrEmpty(kind)) return;                    /* nothing in your hands, and it shows */
  if(kind=="axe")   { drawAxe(c, s, bright); return; }
  if(kind=="mace")  { drawMace(c, s); return; }
  if(kind=="staff" || kind=="pstaff") { drawStaffArt(c, s, bright); return; }
  if(kind=="bow") { drawBowArt(c, s, bright); return; }
  if(kind=="scepter") { drawScepterArt(c, s, bright); return; }
  drawBlade(c, s, bright);
}
/* IS THE AXE IN YOUR HANDS? While it is out reaping, it is not — you cannot
   swing and you are drawn empty-handed, which is the whole cost of the thing. */
public static bool isReaver(){ return player != null && player.reave > 0 && weapon != null && weapon.id == "axe"; }
/* HOW MANY ARE OUT. This used to be a timer on the hero that emptied his hands;
   it is a count of live blades now, and the only thing it gates is whether a
   crit may throw another. The axe never leaves your hands, so heldArt is just
   the weapon again and startSwing has nothing to ask. */
public static double reaveAir(){
  double n = 0;
  foreach (var pr in projectiles.Live()) if(pr.kind == "reave" && !pr.dead) n++;
  return n;
}
public static double reaveAirMax(){ return (player != null && player.reave >= 2) ? REAVE_AIR_R2 : REAVE_AIR; }
public static bool reaveOut(){ return reaveAir() > 0; }
public static string heldArt(){   return Js.Or(weapon != null ? weapon.hold : null, "sword"); }
/* A LONGER ARM IS A LONGER BLADE. Reach you have bought grows the sword or axe
   you are drawn holding -- the whole of the reach ceiling is a blade 1.6 times
   the size -- and a mace grows with the slam's range the same way, so what
   you see in your hands is what it can reach. */
public const double WEAPON_GROW_MAX = 0.6;
public static double heroWeaponScale(){
  var p = player;
  if(p == null || weapon == null) return 1;
  if(weapon.id == "mace")
    return 1 + WEAPON_GROW_MAX * clamp((Js.Or(p.slamRange, SLAM_RANGE_BASE) - SLAM_RANGE_BASE) / (SLAM_RANGE_MAX - SLAM_RANGE_BASE), 0, 1);
  if(weapon.id == "sword" || weapon.id == "axe")
    return 1 + WEAPON_GROW_MAX * clamp(p.reach / REACH_MAX, 0, 1);
  return 1;
}
public static string thrownArt(){ return Js.Or(weapon != null ? weapon.@throw : null, "blade"); }
public static void drawMace(Ctx c, double s){
  c.strokeStyle = "rgba(20,30,40,0.5)"; c.lineWidth=2;
  c.fillStyle = "#5c5e59";
  c.fillRect(-2*s,-2*s,26*s,4*s); c.strokeRect(-2*s,-2*s,26*s,4*s);
  c.fillStyle = "#3d3e3b";
  c.beginPath(); c.arc(30*s,0,8*s,0,TAU); c.fill(); c.stroke();
  for(var i=0;i<6;i++){ var a=i/6.0*TAU;
    c.beginPath();
    c.moveTo(30*s+Math.cos(a-0.3)*8*s, Math.sin(a-0.3)*8*s);
    c.lineTo(30*s+Math.cos(a)*13*s, Math.sin(a)*13*s);
    c.lineTo(30*s+Math.cos(a+0.3)*8*s, Math.sin(a+0.3)*8*s);
    c.closePath(); c.fill(); c.stroke(); }
}
public static void drawSpear(Ctx c, double s){
  c.strokeStyle = "#7b5132"; c.lineWidth = 2.6*s;
  c.beginPath(); c.moveTo(0,8*s); c.lineTo(0,-30*s); c.stroke();
  c.fillStyle = "#cfd6dd";
  c.beginPath();
  c.moveTo(0,-41*s); c.lineTo(-3.6*s,-27*s); c.lineTo(3.6*s,-27*s);
  c.closePath(); c.fill();
  c.strokeStyle = "rgba(20,30,40,0.5)"; c.lineWidth=1.6; c.stroke();
  c.fillStyle = "#583927";
  c.fillRect(-1.8*s,-3*s,3.6*s,7*s);
}
/* THE CYCLONE. PoE's cyclone is a storm with a man in it: a floor of churned
   dust, wind torn into streaks that run faster the further out they are, the
   blade smeared round in after-images, and grit flung off the rim. Every one
   of those is drawn here, on a TRUE circle -- the hit test is a circle, so a
   flattened drawing would strike a foot above and below what you can see. */
public static void drawCyclone(Ctx c, Ent p){
  double R = whirlReach(), a = p.whirlAng, x = p.x, y = p.y;
  c.save();
  /* the churned floor: dark at the heart, a pale lip at the reach */
  var g = c.createRadialGradient(x, y, R*0.15, x, y, R);
  g.addColorStop(0, "rgba(60,64,70,0.16)"); g.addColorStop(0.7, "rgba(200,204,208,0.07)"); g.addColorStop(1, "rgba(232,238,242,0.16)");
  c.fillStyle = g; c.beginPath(); c.arc(x, y, R, 0, TAU); c.fill();
  c.strokeStyle = "rgba(232,238,242,0.30)"; c.lineWidth = 2.5;
  c.beginPath(); c.arc(x, y, R, 0, TAU); c.stroke();
  /* THE WIND, in three bands turning at three speeds, each streak a comet --
     bright at its head, thinning to nothing along the turn behind it */
  var bands = new[] { (r: 0.45, n: 2.0, sp: 0.8, w: 3.0), (r: 0.72, n: 3.0, sp: 1.0, w: 4.5), (r: 0.97, n: 4.0, sp: 1.25, w: 6.0) };
  c.lineCap = "round";
  foreach (var B in bands){
    for(var k=0;k<B.n;k++){
      var head = a*B.sp + k*TAU/B.n + B.r*3;
      for(var si=0;si<7;si++){
        double t0 = head - si*0.13, t1 = t0 - 0.16, al = (0.55 - si*0.075) * (0.6 + 0.4*B.r);
        if(al <= 0) break;
        c.strokeStyle = rgba(236,241,245,Js.ToNumber(al.toFixed(3)));
        c.lineWidth = B.w * (1 - si*0.11);
        c.beginPath(); c.arc(x, y, R*B.r, t1, t0); c.stroke();
      }
    }
  }
  /* THE BLADE, smeared: after-images trailing the real one round the turn */
  var s = heroWeaponScale() * 1.1;
  for(var k=1;k<=4;k++){
    var ba = a - k*0.34;
    c.globalAlpha = 0.34 - k*0.07;
    c.save(); c.translate(x + Math.cos(ba)*14, y - 18 + Math.sin(ba)*10); c.rotate(ba);
    drawWeaponArt(c, s, true, heldArt());
    c.restore();
  }
  c.globalAlpha = 1;
  c.restore();
}
public static void drawPlayerWeapon(Ctx c){
  var p = player; double s = heroWeaponScale();
  if(p.swing == null) return;
  var sw = p.swing;
  var x = clamp(sw.t/sw.dur,0,1);
  var alpha = 0.34*(1-x*0.7);
  var dir = Js.Or(Math.sign(angDiff(sw.to, sw.from)), 1);
  var tail = sw.spin ? sw.cur - dir*1.3 : sw.from;
  c.beginPath();
  c.arc(p.x, p.y-18, sw.reach+10, Math.min(tail,sw.cur), Math.max(tail,sw.cur));
  c.arc(p.x, p.y-18, sw.spin?30:22, Math.max(tail,sw.cur), Math.min(tail,sw.cur), true);
  c.closePath();
  c.fillStyle = rgba(255,255,255,alpha); c.fill();
  var ba = sw.cur;
  double gx = p.x + Math.cos(ba)*14, gy = p.y-18 + Math.sin(ba)*10;
  c.save(); c.translate(gx,gy); c.rotate(ba);
  drawWeaponArt(c, s, x<0.35, heldArt());
  c.restore();
}

}
