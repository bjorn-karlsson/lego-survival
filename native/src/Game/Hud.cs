// lego-survival.html, JS 30992-31835: THE HUD — the wave clock, a boss's debuffs,
// the icons, THE GLOBES (life, shield, mana), the buff and ailment columns, the
// ability row, the banners, the boss bar, and the reticle.
namespace Brickblade;

public sealed class BossDebuff { public string k, el, name; public double n; public Col col; }
public sealed class HudRow { public Col c; public string t; public List<string> more; }
public sealed class HudAuto { public double f; public string label; public Action<double, double> icon; }

public static partial class G
{
/* =============================== HUD =============================== */
/* WHERE THE WAVE BLOCK ENDS. The wave number, the modifier chips and the
   bodies-left line share the top of the screen, and what they occupy depends on
   whether the wave has modifiers and whether it is still on its break. One
   function, so the boss block below can stack under it instead of through it. */
public static double hudWaveBottom(){
  if(wave.n <= 0) return 34;
  return hudWaveBase() + (wave.state == "cleared" && wave.hold ? WAVE_HOLD_H : 0);
}
public static double hudWaveBase(){
  if(waveMods.Count > 0) return wave.state == "ready" ? 95 : 83;
  return wave.state == "ready" ? 70 : 58;
}
/* THE HELD WAVE'S PROMPT, top middle, under the wave block: what is left, and F */
public const double WAVE_HOLD_H = 40;
public static void drawWaveHold(){
  if(wave.state != "cleared" || !wave.hold || state != "play" || player == null || player.dead) return;
  var y = hudWaveBase() + 22;
  string head = "WAVE CLEARED — "+leagueLeftText()+" left", tail = "CONTINUE";
  ctx.font = "700 13px Fredoka";
  double w1 = ctx.measureText(head).width, w2 = ctx.measureText(tail).width;
  double w = 16 + w1 + 16 + 22 + 8 + w2 + 16, x0 = cw/2.0 - w/2.0;
  var pulse = 0.5 + 0.5*Math.sin(vtime*4);
  ctx.fillStyle = "rgba(15,20,25,0.78)"; roundRect(ctx, x0, y - 15, w, 30, 11); ctx.fill();
  ctx.strokeStyle = rgba(165,224,110,0.45 + 0.4*pulse); ctx.lineWidth = 2; roundRect(ctx, x0, y - 15, w, 30, 11); ctx.stroke();
  ctx.textAlign = "left"; ctx.textBaseline = "middle";
  ctx.fillStyle = "#e8ebe9"; ctx.fillText(head, x0 + 16, y);
  var x = x0 + 16 + w1 + 16;
  ctx.fillStyle = "#a5e06e"; roundRect(ctx, x, y - 11, 22, 22, 5); ctx.fill();
  ctx.fillStyle = "#1b2a34"; ctx.textAlign = "center"; ctx.font = "800 12px Fredoka"; ctx.fillText("F", x + 11, y + 1);
  ctx.font = "700 13px Fredoka"; ctx.textAlign = "left"; ctx.fillStyle = "#a5e06e"; ctx.fillText(tail, x + 30, y);
}
/* EVERY DEBUFF ON THE BOSS, on one line under its health bar. No names — a
   symbol and a count, left to right, because on a boss you are reading this
   mid-fight and a row of words is not something you can read mid-fight.
   The glyphs are DRAWN, not typed: the game's font has no dingbats and a
   missing one renders as a tofu box. */
public static void bossDebuffIcon(Ctx c, string kind, double x, double y, double r, Col col){
  c.save(); c.translate(x, y);
  c.lineJoin = "round"; c.lineCap = "round";
  if(kind == "expose"){
    /* a shield with a crack through it, in the element's colour */
    c.fillStyle = col;
    c.beginPath(); c.moveTo(0, -r); c.lineTo(r*0.85, -r*0.6); c.lineTo(r*0.7, r*0.35);
    c.lineTo(0, r); c.lineTo(-r*0.7, r*0.35); c.lineTo(-r*0.85, -r*0.6); c.closePath(); c.fill();
    c.strokeStyle = "#141a20"; c.lineWidth = 1.5;
    c.beginPath(); c.moveTo(-r*0.1, -r*0.8); c.lineTo(r*0.2, -r*0.15); c.lineTo(-r*0.2, r*0.2); c.lineTo(r*0.1, r*0.8); c.stroke();
  } else if(kind == "slow"){
    /* an hourglass in frost */
    c.fillStyle = "#9fd4ff";
    c.beginPath(); c.moveTo(-r*0.75, -r); c.lineTo(r*0.75, -r); c.lineTo(0, 0); c.closePath(); c.fill();
    c.beginPath(); c.moveTo(-r*0.75, r); c.lineTo(r*0.75, r); c.lineTo(0, 0); c.closePath(); c.fill();
  } else if(kind == "poison"){
    c.fillStyle = "#8ac24a";
    c.beginPath(); c.moveTo(0, -r);
    c.bezierCurveTo(r*0.9, -r*0.1, r*0.62, r*0.85, 0, r*0.85);
    c.bezierCurveTo(-r*0.62, r*0.85, -r*0.9, -r*0.1, 0, -r);
    c.fill();
  } else if(kind == "bleed"){
    c.fillStyle = "#ff5a5a";
    c.beginPath(); c.moveTo(0, -r);
    c.bezierCurveTo(r*0.9, -r*0.1, r*0.62, r*0.85, 0, r*0.85);
    c.bezierCurveTo(-r*0.62, r*0.85, -r*0.9, -r*0.1, 0, -r);
    c.fill();
  } else if(kind == "burn"){
    c.fillStyle = "#ff6a1a";
    c.beginPath(); c.moveTo(0, r*0.9);
    c.quadraticCurveTo(-r*0.95, r*0.1, 0, -r);
    c.quadraticCurveTo(r*0.95, r*0.1, 0, r*0.9);
    c.fill();
    c.fillStyle = "#ffd166";
    c.beginPath(); c.moveTo(0, r*0.85);
    c.quadraticCurveTo(-r*0.42, r*0.2, 0, -r*0.35);
    c.quadraticCurveTo(r*0.42, r*0.2, 0, r*0.85);
    c.fill();
  } else if(kind == "burnt"){
    /* a charred flame: black at the heart, a glowing orange edge */
    c.fillStyle = "#2a1206";
    c.beginPath(); c.moveTo(0, r*0.9);
    c.quadraticCurveTo(-r*0.95, r*0.1, 0, -r);
    c.quadraticCurveTo(r*0.95, r*0.1, 0, r*0.9); c.fill();
    c.strokeStyle = "#ff8a3c"; c.lineWidth = 1.6; c.stroke();
    c.fillStyle = "#ff5a1a";
    c.beginPath(); c.arc(0, r*0.35, r*0.22, 0, TAU); c.fill();
  } else if(kind == "chill"){
    c.strokeStyle = "#bfe3ff"; c.lineWidth = 1.8;
    for(var i=0;i<3;i++){
      var a = i/3.0*Math.PI;
      c.beginPath();
      c.moveTo(-Math.cos(a)*r, -Math.sin(a)*r);
      c.lineTo(Math.cos(a)*r, Math.sin(a)*r);
      c.stroke();
    }
  } else if(kind == "frozen"){
    c.fillStyle = "rgba(223,242,255,0.9)";
    roundRect(c, -r*0.8, -r*0.8, r*1.6, r*1.6, r*0.28); c.fill();
    c.strokeStyle = "#7fb9e0"; c.lineWidth = 1.5;
    roundRect(c, -r*0.8, -r*0.8, r*1.6, r*1.6, r*0.28); c.stroke();
  } else if(kind == "shock"){
    c.fillStyle = "#ffe95a";
    c.beginPath();
    c.moveTo(r*0.25, -r); c.lineTo(-r*0.55, r*0.12); c.lineTo(-r*0.02, r*0.12);
    c.lineTo(-r*0.3, r); c.lineTo(r*0.6, -r*0.18); c.lineTo(r*0.04, -r*0.18);
    c.closePath(); c.fill();
  } else if(kind == "shocked"){
    /* THE SHOCKED MARK -- electrocuted: a bolt inside a crackling ring. It was
       listed and counted but never had a picture, so the row showed a bare number */
    c.strokeStyle = "#fff6a8"; c.lineWidth = 1.4;
    c.beginPath(); c.arc(0, 0, r*0.95, 0, TAU); c.stroke();
    c.fillStyle = "#fff6a8";
    c.beginPath();
    c.moveTo(r*0.2, -r*0.75); c.lineTo(-r*0.42, r*0.1); c.lineTo(-r*0.02, r*0.1);
    c.lineTo(-r*0.22, r*0.75); c.lineTo(r*0.45, -r*0.14); c.lineTo(r*0.04, -r*0.14);
    c.closePath(); c.fill();
  } else if(kind == "brittle"){
    /* BRITTLE -- a cracked shard of ice */
    c.fillStyle = "#dff2ff";
    c.beginPath(); c.moveTo(0, -r); c.lineTo(r*0.75, -r*0.2); c.lineTo(r*0.4, r); c.lineTo(-r*0.4, r); c.lineTo(-r*0.75, -r*0.2); c.closePath(); c.fill();
    c.strokeStyle = "#4d86ad"; c.lineWidth = 1.3;
    c.beginPath(); c.moveTo(-r*0.1, -r*0.7); c.lineTo(r*0.18, -r*0.1); c.lineTo(-r*0.15, r*0.25); c.lineTo(r*0.1, r*0.8); c.stroke();
  }
  c.restore();
}
/* every kind the boss row can list, so a new debuff cannot again arrive with a
   count and no picture (the test draws each one and counts its paint) */
public static readonly string[] BOSS_DEBUFF_KINDS = {"poison", "bleed", "burn", "burnt", "chill", "frozen", "shock", "shocked", "brittle", "slow", "expose"};
public static List<BossDebuff> bossDebuffs(Ent e){
  var outp = new List<BossDebuff>();
  var psn = e.poison != null ? Js.Or(e.poison.n, 0) : 0;
  if(psn > 0) outp.Add(new BossDebuff { k = "poison", n = psn, col = "#8ac24a" });
  var bl = bleedStacks(e);
  if(bl > 0) outp.Add(new BossDebuff { k = "bleed", n = bl, col = "#ff5a5a" });
  var bn = burnStacksOn(e);
  if(bn > 0) outp.Add(new BossDebuff { k = "burn", n = bn, col = "#ff9a3c" });
  if(e.burnt != null && e.burnt.t > 0) outp.Add(new BossDebuff { k = "burnt", n = e.burnt.n, col = "#ff8a3c" });
  var ch = chillStacksOn(e);
  if(ch > 0) outp.Add(new BossDebuff { k = "chill", n = ch, col = "#bfe3ff" });
  var sh = shockStacksOn(e);
  if(sh > 0) outp.Add(new BossDebuff { k = "shock", n = sh, col = "#ffe95a" });
  if(shockedMarks(e) > 0) outp.Add(new BossDebuff { k = "shocked", n = shockedMarks(e), col = "#fff6a8" });
  if(frozenMarks(e) > 0) outp.Add(new BossDebuff { k = "brittle", n = frozenMarks(e), col = "#dff2ff" });
  if(e.stunT > 0) outp.Add(new BossDebuff { k = "frozen", n = Math.ceil(e.stunT), col = "#dff2ff" });
  /* slowed with no chill stacks to show for it: Block Freeze's frost field */
  else if(e.slowT > 0 && !Js.T(ch)) outp.Add(new BossDebuff { k = "slow", n = Math.ceil(e.slowT), col = "#9fd4ff" });
  /* EXPOSURE, per element: SCORCHED, BRITTLE, SAPPED, WITHERED -- a resistance
     your hits have stripped off it, with the seconds it has left. It was on the
     boss all along and never shown. */
  if(e.expose != null) foreach (var el in EXPOSE_ELEMS)
    if(e.expose[el] > 0) outp.Add(new BossDebuff { k = "expose", el = el, n = Math.ceil(e.expose[el]), col = EXPOSE_COL[el], name = EXPOSE_NAME[el] });
  return outp;
}
public static void drawBossDebuffs(Ctx c, Ent e, double cx, double y){
  var list = bossDebuffs(e);
  if(list.Count == 0) return;
  c.save();
  c.font = "700 11px Fredoka";
  c.textBaseline = "middle";
  double r = 6, gap = 7, pad = 6;
  /* measure the whole row first so it can be centred under the bar */
  var widths = list.Select(d => r*2 + 3 + c.measureText("" + S(d.n)).width + pad*2).ToList();
  double total = 0; foreach (var w0 in widths) total += w0;
  total += gap*(list.Count-1);
  var x = cx - total/2.0;
  for(var i=0;i<list.Count;i++){
    var d = list[i]; var w = widths[i];
    c.fillStyle = "rgba(12,16,20,0.66)";
    roundRect(c, x, y, w, 18, 8); c.fill();
    c.strokeStyle = d.col; c.lineWidth = 1.2; c.globalAlpha = 0.65;
    roundRect(c, x, y, w, 18, 8); c.stroke();
    c.globalAlpha = 1;
    bossDebuffIcon(c, d.k, x + pad + r, y + 9, r, d.col);
    c.fillStyle = d.col; c.textAlign = "left";
    c.fillText("" + S(d.n), x + pad + r*2 + 3, y + 9.5);
    x += w + gap;
  }
  c.restore();
}
public static void heartPath(Ctx c, double x, double y, double s){
  c.beginPath();
  c.moveTo(x, y+s*0.9);
  c.bezierCurveTo(x-s*1.15, y+s*0.12, x-s*0.8, y-s*0.85, x, y-s*0.28);
  c.bezierCurveTo(x+s*0.8, y-s*0.85, x+s*1.15, y+s*0.12, x, y+s*0.9);
  c.closePath();
}
/* `frac` is how much of this heart is left, 0..1. A part-heart is drawn at
   proportional opacity AND clipped to a rising level, so you can read "half a
   heart" at a glance and still watch it drain hundredth by hundredth. */
public static void drawHeartAt(double x, double y, double frac, Col col, double pulse = 0){
  frac = clamp(frac, 0, 1);
  ctx.save(); ctx.translate(x,y); ctx.scale(pulse,pulse);
  heartPath(ctx,0,0,10);
  ctx.fillStyle = "rgba(20,28,34,0.55)";
  ctx.fill();
  if(frac > 0){
    ctx.save();
    heartPath(ctx,0,0,10);
    ctx.clip();
    /* a full heart is solid; a partial one fades as it empties but never vanishes */
    ctx.globalAlpha = frac >= 1 ? 1 : 0.42 + 0.58*frac;
    ctx.fillStyle = col;
    ctx.fillRect(-11, 11 - 24*frac, 22, 24);
    ctx.restore();
  }
  ctx.strokeStyle="rgba(15,20,25,0.7)"; ctx.lineWidth=2.2; ctx.stroke();
  if(frac >= 1){ ctx.fillStyle="rgba(255,255,255,0.65)";
    ctx.beginPath(); ctx.ellipse(-4,-3,2.7,1.8,-0.5,0,TAU); ctx.fill(); }
  ctx.restore();
}
public static void drawMagnetIcon(double x, double y){
  ctx.save(); ctx.translate(x,y); ctx.scale(1.15,1.15);
  ctx.fillStyle = "#9b45d6"; ctx.strokeStyle="rgba(20,30,40,0.55)"; ctx.lineWidth=1.6;
  ctx.beginPath();
  ctx.moveTo(-8,-1); ctx.arc(0,-1,8,Math.PI,0,true);
  ctx.lineTo(8,7); ctx.lineTo(3.5,7); ctx.lineTo(3.5,-1);
  ctx.arc(0,-1,3.5,0,Math.PI,true);
  ctx.lineTo(-3.5,7); ctx.lineTo(-8,7);
  ctx.closePath(); ctx.fill(); ctx.stroke();
  ctx.fillStyle = "#cfd6dd";
  ctx.fillRect(-8,4.2,4.5,3.4); ctx.fillRect(3.5,4.2,4.5,3.4);
  ctx.restore();
}
public static void drawFortuneIcon(double x, double y, double mult){
  ctx.save(); ctx.translate(x,y); ctx.rotate(Math.sin(vtime*3)*0.15);
  ctx.fillStyle = PAL.gold; ctx.strokeStyle="rgba(20,30,40,0.55)"; ctx.lineWidth=1.6;
  roundRect(ctx,-10,-10,20,20,4); ctx.fill(); ctx.stroke();
  drawStudG(ctx,0,-2,5,"#ffcf00");
  ctx.fillStyle = "#d3242b"; ctx.font="700 9px Fredoka"; ctx.textAlign="center"; ctx.textBaseline="middle";
  ctx.fillText("×"+mult, 0, 9);
  ctx.restore();
}
public static void drawRegenIcon(double x, double y){
  ctx.save(); ctx.translate(x,y);
  heartPath(ctx,0,0,10);
  ctx.fillStyle = "#ff8a8a"; ctx.fill();
  ctx.strokeStyle="rgba(20,30,40,0.55)"; ctx.lineWidth=2; ctx.stroke();
  ctx.strokeStyle = "#fff"; ctx.lineWidth=2.2; ctx.lineCap="round";
  ctx.beginPath(); ctx.moveTo(0,-4); ctx.lineTo(0,4); ctx.moveTo(-4,0); ctx.lineTo(4,0); ctx.stroke();
  ctx.fillStyle="#fff"; ctx.font="700 8px Fredoka"; ctx.textAlign="center"; ctx.textBaseline="middle";
  ctx.fillText("×2", 0, 11);
  ctx.restore();
}
public static void drawRageIcon(double x, double y, double mult){
  ctx.save(); ctx.translate(x,y); ctx.rotate(Math.sin(vtime*4)*0.12);
  ctx.fillStyle = "#d3242b"; ctx.strokeStyle="rgba(20,30,40,0.55)"; ctx.lineWidth=1.6;
  roundRect(ctx,-10,-10,20,20,4); ctx.fill(); ctx.stroke();
  ctx.save(); ctx.rotate(-0.7); ctx.translate(-4,0); drawBlade(ctx, 0.34, true); ctx.restore();
  ctx.fillStyle = "#ffd166"; ctx.font="700 9px Fredoka"; ctx.textAlign="center"; ctx.textBaseline="middle";
  ctx.fillText("×"+mult, 0, 9);
  ctx.restore();
}
public static void drawWardIcon(double x, double y, double n){
  ctx.save(); ctx.translate(x,y);
  ctx.fillStyle = "#2e7dd1"; ctx.strokeStyle="rgba(15,30,50,0.6)"; ctx.lineWidth=1.6;
  ctx.beginPath();
  ctx.moveTo(0,-11); ctx.lineTo(9,-7); ctx.lineTo(9,1);
  ctx.quadraticCurveTo(9,9, 0,12);
  ctx.quadraticCurveTo(-9,9, -9,1);
  ctx.lineTo(-9,-7); ctx.closePath(); ctx.fill(); ctx.stroke();
  ctx.strokeStyle = "#bfe3ff"; ctx.lineWidth=1.8;
  ctx.beginPath(); ctx.moveTo(0,-5); ctx.lineTo(0,6); ctx.moveTo(-4,0); ctx.lineTo(4,0); ctx.stroke();
  if(n>1){
    ctx.fillStyle="#eaf6ff"; ctx.font="700 8px Fredoka"; ctx.textAlign="center"; ctx.textBaseline="middle";
    ctx.fillText("×"+n, 0, -13);
  }
  ctx.restore();
}
public static void drawFreezeIcon(double x, double y){
  ctx.save(); ctx.translate(x,y);
  ctx.strokeStyle="#bfe3ff"; ctx.lineWidth=2; ctx.lineCap="round";
  for(var i=0;i<3;i++){
    var a=i/3.0*Math.PI;
    ctx.beginPath();
    ctx.moveTo(-Math.cos(a)*9, -Math.sin(a)*9);
    ctx.lineTo( Math.cos(a)*9,  Math.sin(a)*9);
    ctx.stroke();
  }
  ctx.fillStyle="#dff2ff";
  ctx.beginPath(); ctx.arc(0,0,2.6,0,TAU); ctx.fill();
  ctx.restore();
}
public static void drawStudHUD(double x, double y, Col? col0 = null){ Col col = col0 ?? (Col)PAL.gold;
  ctx.save(); ctx.translate(x,y);
  ctx.fillStyle = sh(col,-0.26);
  ctx.beginPath();
  ctx.moveTo(-13,0); ctx.lineTo(-13,8);
  ctx.ellipse(0,8,13,7.2,0,Math.PI,0,true);
  ctx.lineTo(13,0);
  ctx.closePath(); ctx.fill();
  ctx.fillStyle = sh(col,0.04);
  ctx.beginPath(); ctx.ellipse(0,0,13,7.2,0,0,TAU); ctx.fill();
  ctx.strokeStyle="rgba(0,0,0,0.22)"; ctx.lineWidth=1.4; ctx.stroke();
  ctx.strokeStyle = sh(col,0.4); ctx.lineWidth=2.4;
  ctx.beginPath(); ctx.ellipse(0,-1,8.4,4.6,0,0,TAU); ctx.stroke();
  ctx.fillStyle = "rgba(255,255,255,0.85)";
  ctx.beginPath(); ctx.ellipse(-4.5,-2.4,2.6,1.4,-0.5,0,TAU); ctx.fill();
  ctx.restore();
}
/* ============================== THE GLOBES ==============================
   Life, energy shield and mana, each its own pool, the way PoE shows them: a
   glass globe of liquid at each bottom corner. The life globe is red; the
   shield is a pale-blue layer poured over it and a ring round its rim that
   empties as the shield does; the mana globe is blue. The number in each is
   what is in it, over what it holds. */
public const double GLOBE_R = 50, GLOBE_PAD = 24;
public const double GLOBE_ROW_X = 2*GLOBE_R + GLOBE_PAD + 46;     /* where the ability row starts, beside a globe */
public static readonly Col LIFE_COL = "#d3242b";
public static Pt globeCentre(double side){ return new Pt(side < 0 ? GLOBE_PAD + GLOBE_R : cw - GLOBE_PAD - GLOBE_R, ch - GLOBE_PAD - GLOBE_R); }
public static void globeLiquid(Ctx c, double x, double y, double r, double frac, Col top, Col bottom, double alpha = double.NaN){
  if(!(frac > 0)) return;
  var lvl = y + r - 2*r*clamp(frac, 0, 1);
  var g = c.createLinearGradient(0, lvl, 0, y + r);
  g.addColorStop(0, top); g.addColorStop(1, bottom);
  c.save(); c.globalAlpha = double.IsNaN(alpha) ? 1 : alpha; c.fillStyle = g;
  c.beginPath(); c.moveTo(x - r, y + r);
  for(int i=0;i<=24;i++){ var xx = x - r + i/24.0*2*r; c.lineTo(xx, lvl + (frac < 1 ? Math.sin(vtime*3 + i*0.7)*2 : 0)); }
  c.lineTo(x + r, y + r); c.closePath(); c.fill(); c.restore();
}
public static void drawGlobe(Ctx c, double x, double y, double r, Action<Ctx> fill, Col? glow = null){
  /* the socket: a dark disc, then the liquid clipped to the glass, then glass */
  c.save();
  c.fillStyle = rgba(12,16,20,0.82);
  c.beginPath(); c.arc(x, y, r + 6, 0, TAU); c.fill();
  c.beginPath(); c.arc(x, y, r, 0, TAU); c.clip();
  c.fillStyle = "#0c0f13"; c.fillRect(x - r, y - r, 2*r, 2*r);
  fill(c);
  var hl = c.createRadialGradient(x - r*0.35, y - r*0.45, 2, x - r*0.35, y - r*0.45, r*0.9);
  hl.addColorStop(0, rgba(255,255,255,0.35)); hl.addColorStop(1, rgba(255,255,255,0));
  c.fillStyle = hl; c.fillRect(x - r, y - r, 2*r, 2*r);
  c.restore();
  /* the rim: a brick-grey bezel with four studs, and a glow when it is asked for */
  c.strokeStyle = glow ?? (Col)"#5f6770"; c.lineWidth = glow != null ? 4 : 3;
  c.beginPath(); c.arc(x, y, r + 2, 0, TAU); c.stroke();
  c.fillStyle = "#8a929a";
  for(int k=0;k<4;k++){ var a = -Math.PI/4 + k*Math.PI/2; c.beginPath(); c.arc(x + Math.cos(a)*(r + 4), y + Math.sin(a)*(r + 4), 3.2, 0, TAU); c.fill(); }
}
public static void globeText(Ctx c, double x, double y, string big, string small, Col? col = null){
  c.textAlign = "center"; c.textBaseline = "middle";
  c.font = "700 20px \"Titan One\""; c.lineWidth = 4; c.strokeStyle = rgba(10,12,16,0.85);
  c.strokeText(big, x, y - 4); c.fillStyle = col ?? (Col)"#ffffff"; c.fillText(big, x, y - 4);
  if(!string.IsNullOrEmpty(small)){ c.font = "700 11px Fredoka"; c.lineWidth = 3; c.strokeText(small, x, y + 15); c.fillStyle = "#e8ebe9"; c.fillText(small, x, y + 15); }
}
public static void drawPoolGlobes(Ent p){
  var c = ctx; double r = GLOBE_R;
  /* LIFE, with the SHIELD over it */
  { var gc = globeCentre(-1); double x = gc.x, y = gc.y;
    double lf = clamp(p.hp/Js.Or(p.maxHp, 1), 0, 1), ef = p.esMax > 0 ? clamp(p.es/p.esMax, 0, 1) : 0;
    var low = lowLife() && p.hp > 0;
    drawGlobe(c, x, y, r, cc=>{
      globeLiquid(cc, x, y, r, lf, "#ff4b3e", "#a3121a");
      if(p.esMax > 0) globeLiquid(cc, x, y, r, ef, "rgba(210,245,255,0.9)", "rgba(90,190,235,0.9)", 0.45 + 0.15*p.esFlash);
    }, low ? rgba(255,60,60,0.6 + 0.4*Math.sin(vtime*6)) : null);
    if(p.esMax > 0 && ef > 0){
      c.strokeStyle = ES_COL; c.lineWidth = 4; c.lineCap = "round";
      c.beginPath(); c.arc(x, y, r + 9, -Math.PI/2, -Math.PI/2 + TAU*ef); c.stroke(); c.lineCap = "butt";
    }
    globeText(c, x, y, fmtHp(p.hp), "/ "+fmtHp(p.maxHp)+(p.esMax > 0 ? "  ·  "+fmtHp(p.es)+" ES" : ""));
  }
  /* MANA */
  { var gc = globeCentre(1); double x = gc.x, y = gc.y;
    var mf = p.maxMana > 0 ? clamp(p.mana/p.maxMana, 0, 1) : 0;
    drawGlobe(c, x, y, r, cc=>globeLiquid(cc, x, y, r, mf, "#5b93ff", "#1a3cae"));
    globeText(c, x, y, S(Math.floor(p.mana))+"", "/ "+S(Math.round(p.maxMana))+" mana", "#dfe8ff");
  }
}
public static void renderHUD(){
  if(state=="title" || player == null) return;
  var p = player;
  /* THE POOLS ARE GLOBES NOW, PoE's: life (with the shield over it) bottom
     left, mana bottom right -- drawn with the ability row below. The top left
     keeps the level and its bar. */
  var xpY = 22;
  var bx = 24;
  ctx.fillStyle = "rgba(15,20,25,0.6)";
  roundRect(ctx, bx+26, xpY, 190, 12, 6); ctx.fill();
  ctx.fillStyle = PAL.gold;
  roundRect(ctx, bx+26, xpY, Math.max(8,190*clamp(p.xp/p.xpNext,0,1)), 12, 6); ctx.fill();
  ctx.fillStyle = PAL.gold;
  ctx.beginPath(); ctx.arc(bx+10, xpY+6, 15, 0, TAU); ctx.fill();
  ctx.strokeStyle="rgba(15,20,25,0.7)"; ctx.lineWidth=2.5; ctx.stroke();
  ctx.fillStyle = "#1b2a34"; ctx.font="700 15px \"Titan One\""; ctx.textAlign="center"; ctx.textBaseline="middle";
  ctx.fillText(S(p.level), bx+10, xpY+7);
  /* ...and under the bar, which level pays the next skill point */
  { var nl = rpgOn() ? 0 : metaNextLevelPoint();
    if(nl != 0){
      var soon = nl == p.level + 1;
      ctx.font = "700 10px Fredoka"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
      var txt = "next skill point at level " + S(nl);
      ctx.strokeStyle = "rgba(15,20,25,0.75)"; ctx.lineWidth = 3; ctx.strokeText(txt, bx+30, xpY+21);
      ctx.fillStyle = soon ? "#8fd4ff" : "#9fb0a4"; ctx.fillText(txt, bx+30, xpY+21);
    }
    /* and the second chance: ready, or waiting on a boss */
    if(arcadeMode() && !sandbox){
      bool ok = respawn.free > 0; string t2 = ok ? "second chance ready" : "second chance used — kill a boss";
      ctx.font = "700 10px Fredoka"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
      var y2 = xpY + (nl != 0 ? 34 : 21);
      ctx.strokeStyle = "rgba(15,20,25,0.75)"; ctx.lineWidth = 3; ctx.strokeText(t2, bx+30, y2);
      ctx.fillStyle = ok ? "#ffd76e" : "#9fb0a4"; ctx.fillText(t2, bx+30, y2);
    } }
  /* POINTS WAITING. A point earned mid-run is only worth something if you know
     it is there, so while any sit unplaced the level bar says so -- and names
     the key, because T is a new door into an old screen. */
  if(metaRun != null && META != null && META.on){
    var waiting = metaPointsLeft();
    if(waiting > 0){
      var txt = "T \u00b7 " + S(waiting) + " SKILL POINT" + (waiting > 1 ? "S" : "");
      ctx.font = "700 10.5px Fredoka";
      var tw = ctx.measureText(txt).width + 16;
      var pulse = 0.75 + 0.25*Math.sin(vtime*3.2);
      ctx.fillStyle = "rgba(15,20,25,0.7)";
      roundRect(ctx, bx+224, xpY - 2, tw, 16, 8); ctx.fill();
      ctx.strokeStyle = rgba(143,212,255,pulse); ctx.lineWidth = 1.5; ctx.stroke();
      ctx.fillStyle = "#8fd4ff"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
      ctx.fillText(txt, bx+232, xpY + 6.5);
    }
  }
  ctx.textBaseline="middle";
  drawStudHUD(cw-112, 36, p.studs>=200 ? "#a45bd6" : PAL.gold);
  ctx.font="700 26px \"Titan One\""; ctx.textAlign="left";
  ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=5;
  ctx.strokeText(S(p.studs), cw-90, 38); ctx.fillStyle="#ffd76e"; ctx.fillText(S(p.studs), cw-90, 38);
  if(rpgOn()) drawRpgHud();
  else if(wave.n>0){
    ctx.textAlign="center"; ctx.font="700 20px \"Titan One\"";
    ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4;
    var left = enemies.Count(e=>!e.dead&&e.state=="active"&&!e.dormant&&!isBanner(e)) + wave.queue.Count;
    /* the monster LEVEL rides beside the wave number once it starts to bite —
       it is the number that explains why wave 60 hurts more than wave 50, so it
       has to be something you can actually see */
    var ml = mobRank();
    var wtxt = "WAVE "+S(wave.n);
    ctx.strokeText(wtxt, cw/2.0, 32); ctx.fillStyle="#f2f3f2"; ctx.fillText(wtxt, cw/2.0, 32);
    if(ml > 0){
      var half = ctx.measureText(wtxt).width/2.0;
      ctx.textAlign = "left"; ctx.font="700 12px \"Titan One\"";
      ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=3.5;
      var lt = "MOB LV "+S(ml);
      ctx.strokeText(lt, cw/2.0 + half + 10, 30);
      ctx.fillStyle = "#ff8a6a"; ctx.fillText(lt, cw/2.0 + half + 10, 30);
      ctx.textAlign = "center"; ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4;
    }
    /* THE WAVE'S MODIFIERS, for as long as the wave lasts. A banner you can
       miss is not how you tell someone the pack they are about to fight has
       double armour — these sit under the wave number until it is cleared,
       each in its own colour so the set reads at a glance. */
    if(waveMods.Count > 0){
      ctx.font = "700 11px \"Titan One\"";
      /* during the break the countdown and its bar own the two rows under the
         wave number, so the chips drop below them rather than through them */
      var modY = wave.state=="ready" ? 74 : 62;
      double gap = 9, pad = 7;
      double total = 0;
      var widths = waveMods.Select(m=>{ var w0 = ctx.measureText(m.name).width + pad*2;
        total += w0; return w0; }).ToList();
      total += gap*(waveMods.Count-1);
      var mx = cw/2.0 - total/2.0;
      for(var i=0;i<waveMods.Count;i++){
        var m = waveMods[i]; var w = widths[i];
        ctx.fillStyle = "rgba(12,16,20,0.62)";
        roundRect(ctx, mx, modY, w, 17, 6); ctx.fill();
        ctx.strokeStyle = m.col; ctx.lineWidth = 1.4;
        roundRect(ctx, mx, modY, w, 17, 6); ctx.stroke();
        ctx.textAlign = "center"; ctx.fillStyle = m.col;
        ctx.fillText(m.name, mx + w/2.0, modY + 9.5);
        mx += w + gap;
      }
      ctx.textAlign = "center"; ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4;
    }
    ctx.font="600 13px Fredoka"; ctx.fillStyle="#c9d2ce";
    if(wave.state=="ready"){
      var t = Math.max(0, wave.readyT);
      var rest = wave.readyMax >= WAVE_BREAK_BOSS;
      ctx.fillStyle = rest ? "#a5e06e" : "#ffd76e";
      ctx.fillText((rest?"catch your breath — ":"incoming in ")+t.toFixed(1)+"s  ·  F to start now", cw/2.0, 52);
      /* a thinning bar under it, so the wait is something you can see run out */
      double bw = 210, rbx = cw/2.0-bw/2.0, k = wave.readyMax>0 ? clamp(t/wave.readyMax,0,1) : 0;
      ctx.fillStyle = "rgba(15,20,25,0.45)"; roundRect(ctx, rbx, 60, bw, 6, 3); ctx.fill();
      ctx.fillStyle = rest ? "#a5e06e" : "#ffd76e";
      roundRect(ctx, rbx, 60, Math.max(2, bw*k), 6, 3); ctx.fill();
    } else {
      ctx.fillText(left>0? S(left)+" bricks left" : "", cw/2.0, 52);
      var folded = Js.Or(wave.merged, 0);
      if(wave.callIn > 0){
        /* the clock you asked for */
        ctx.font="700 17px \"Titan One\""; ctx.textAlign="center";
        ctx.fillStyle = "#ff8a4a";
        ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4;
        var txt = "WAVE "+S(wave.n+1)+" IN "+wave.callIn.toFixed(1)+"s";
        ctx.strokeText(txt, cw/2.0, 76); ctx.fillText(txt, cw/2.0, 76);
      } else if(wave.callT > 0){
        /* holding F — a bar filling toward the commitment */
        var k = clamp(wave.callT/CALL_HOLD, 0, 1);
        double bw = 180, cbx = cw/2.0-bw/2.0;
        ctx.fillStyle = "rgba(15,20,25,0.45)"; roundRect(ctx, cbx, 66, bw, 7, 3.5); ctx.fill();
        ctx.fillStyle = "#ff8a4a"; roundRect(ctx, cbx, 66, Math.max(2,bw*k), 7, 3.5); ctx.fill();
        ctx.font="600 12px Fredoka"; ctx.textAlign="center"; ctx.fillStyle="#ffb98a";
        ctx.fillText("hold F — call wave "+S(wave.n+1)+" in early", cw/2.0, 88);
      } else if(Js.T(folded) && left>0){
        ctx.font="600 12px Fredoka"; ctx.textAlign="center"; ctx.fillStyle="#ff8a4a";
        ctx.fillText(S(folded+1)+" WAVES AT ONCE", cw/2.0, 68);
      }
    }
  }
  drawBreachHud();
  drawAbyssHud();
  drawSboxHud();
  drawRitualHud();
  drawLegionHud();
  drawWaveHold();
  drawLootStash();
  var boss = enemies.Find(e=>e.type=="boss" && !e.dead && e.state=="active");
  if(boss != null){
    var bname = Js.Or(boss.bname, "BOSS");
    /* THE BOSS BLOCK STACKS UNDER THE WAVE BLOCK instead of through it. The
       wave number, its modifier chips and the bodies-left line all live at the
       top of the screen, and the boss traits were being drawn straight over
       them — so the block starts from whatever the wave block actually used. */
    double bw = Math.min(460, cw-80), bx2 = cw/2.0-bw/2.0, by2 = hudWaveBottom() + 46;
    /* THE SHIELD TRACK SITS IN THE ROW THE NAME USED, so everything above the
       health bar moves up by exactly its height when there is one. Without this
       THE DREAD ROOST was printed straight through its own shield. */
    var esLift = boss.esMax > 0 ? 13 : 0;
    ctx.font="700 15px \"Titan One\""; ctx.textAlign="center";
    ctx.strokeStyle="rgba(15,20,25,0.8)"; ctx.lineWidth=4;
    if(boss.traits != null && boss.traits.Count > 0){
      ctx.font="700 11px Fredoka";
      var parts = boss.traits.Select(t=>t.name).ToList();
      double tw = 0; foreach (var t in parts) tw += ctx.measureText(t).width + 18;
      var tx = cw/2.0 - tw/2.0;
      for(var i=0;i<parts.Count();i++){
        var w4 = ctx.measureText(parts[i]).width + 12;
        ctx.fillStyle = "rgba(15,20,25,0.55)";
        roundRect(ctx, tx, by2-38-esLift, w4, 15, 7); ctx.fill();
        ctx.fillStyle = boss.traits[i].col;
        ctx.textAlign="center"; ctx.fillText(parts[i], tx + w4/2.0, by2-27-esLift);
        tx += w4 + 6;
      }
      ctx.textAlign="center";
    }
    ctx.font="700 15px \"Titan One\"";
    ctx.strokeText(bname, cw/2.0, by2-8-esLift);
    ctx.fillStyle = boss.super ? "#c95bd0" : "#ff5a4a";
    ctx.fillText(bname, cw/2.0, by2-8-esLift);
    ctx.fillStyle="rgba(15,20,25,0.65)"; roundRect(ctx,bx2,by2,bw,18,6); ctx.fill();
    ctx.fillStyle = boss.super ? "#9b45d6" : "#d3242b";
    roundRect(ctx,bx2+2,by2+2,Math.max(4,(bw-4)*clamp(boss.hp/boss.maxHp,0,1)),14,4); ctx.fill();
    ctx.strokeStyle="rgba(15,20,25,0.5)"; ctx.lineWidth=2;
    for(var i=1;i<10;i++){ ctx.beginPath(); ctx.moveTo(bx2+bw*i/10.0,by2+2); ctx.lineTo(bx2+bw*i/10.0,by2+16); ctx.stroke(); }
    /* THE SHIELD, ON TOP OF THE HEALTH BAR AND NOT BESIDE IT, because that is
       what it is: a thing standing in front. It refills while you watch, so a
       fight you cannot out-damage looks exactly like what it is. */
    if(boss.esMax > 0){
      var f = clamp(boss.es/boss.esMax, 0, 1);
      ctx.fillStyle = "rgba(15,20,25,0.55)";
      roundRect(ctx, bx2, by2-9, bw, 8, 4); ctx.fill();
      if(f > 0){
        ctx.fillStyle = ES_COL;
        ctx.globalAlpha = 0.85 + 0.15*boss.esFlash;
        roundRect(ctx, bx2+1.5, by2-8, Math.max(3,(bw-3)*f), 6, 3); ctx.fill();
        ctx.globalAlpha = 1;
      }
      /* and it says when it is coming back, because that is the decision --
         inside the track's own right end, where it cannot reach the name */
      if(boss.esT <= 0 && f < 1){
        ctx.font="700 8px Fredoka"; ctx.textAlign="right";
        ctx.fillStyle = ES_COL;
        ctx.fillText("RECHARGING", bx2 + bw - 4, by2 - 2.5);
        ctx.textAlign="center";
      }
    }
    drawBossDebuffs(ctx, boss, cw/2.0, by2 + 24);
  }
  if(!everInput && runTime > 3.5){
    var a2 = 0.55+0.45*Math.sin(vtime*4);
    ctx.textAlign="center"; ctx.font="600 15px Fredoka";
    ctx.strokeStyle="rgba(15,20,25,0.85)"; ctx.lineWidth=4;
    ctx.strokeText("Click the game window once, then use WASD to move", cw/2.0, ch-118);
    ctx.fillStyle = rgba(255,214,110,a2);
    ctx.fillText("Click the game window once, then use WASD to move", cw/2.0, ch-118);
  }
  /* ================== WHAT IS ON YOU, IN TWO COLUMNS ==================
     One list of everything bad was already there and doing its job. What was
     missing was the other half: a hero carrying three charges, a rage well and
     a fortune had no way to see any of it except the chevrons over their own
     head, which say how many and never what they are worth.
     BUFFS on the left, AILMENTS on the right, because the left is where your
     eye already goes for your own health bar and the right is where the thing
     happening TO you belongs. Both columns are built from data -- the stack
     table, the curse table, the element list -- so a fourth charge or a fifth
     curse arrives in here by existing. */
  var buffs = new List<HudRow>();
  foreach (var id in (p.stacks != null ? p.stacks.Keys.ToList() : new List<string>())){
    double n = stackOf(id); var d = stackDef(id);
    if(!Js.T(n) || d == null) continue;
    /* WHAT IT IS WORTH, not how many there are -- the chevrons over your head
       already say how many. Each row prints the thing the stack actually pays. */
    var worth = new List<string>();
    var m = d.mods ?? new NumMap();
    if(Js.T(m["moreAll"]))  worth.Add("+"+S(Math.round(m["moreAll"]*n*100))+"% MORE dmg");
    if(Js.T(m["moreDmg"]))  worth.Add("+"+S(Math.round(m["moreDmg"]*n*100))+"% MORE atk");
    if(Js.T(m["atk"]))      worth.Add("+"+S(Math.round(m["atk"]*n*100))+"% atk spd");
    if(Js.T(m["crit"]))     worth.Add("+"+S(Math.round(m["crit"]*n*100))+"% crit");
    if(Js.T(m["less"]))     worth.Add("-"+S(Math.round(m["less"]*n*100))+"% dmg taken");
    if(Js.T(m["res"]))      worth.Add("+"+S(Math.round(m["res"]*n*100))+"% res");
    if(Js.T(m["reach"]))    worth.Add("+"+S(Math.round(m["reach"]*n*100))+"% reach");
    buffs.Add(new HudRow { c = d.col, t = d.name+" \u00d7"+S(d.frac ? Math.round(n) : n),
                 more = worth });
  }
  if(p.rageT > 0)    buffs.Add(new HudRow { c = "#ff4a3c", t = "RAGE BRICK \u00d7"+S(p.rageMul)+
                                   " \u00b7 "+S(Math.ceil(p.rageT))+"s" });
  if(p.fortuneT > 0) buffs.Add(new HudRow { c = "#ffd166", t = "FORTUNE \u00d7"+S(p.fortuneMul)+
                                   " studs & XP \u00b7 "+S(Math.ceil(p.fortuneT))+"s" });
  if(p.magnetT > 0)  buffs.Add(new HudRow { c = "#9fd4ff", t = "MAGNET \u00b7 "+S(Math.ceil(p.magnetT))+"s" });
  if(isSummoner()){
    var L = legion(); var kinds = new NumMap();
    foreach (var m in L) kinds[m.kind] = kinds[m.kind] + 1;
    var worth = kinds.Select(kv=>S(kv.Value)+" "+(kv.Key == "bow" ? "archer" : kv.Key == "spear" ? "spear" : kv.Key)+(kv.Value > 1 ? "s" : "")).ToList();
    if(golemMax() != 0) worth.Add(S(golems().Count)+"/"+S(golemMax())+" golems");
    if(player.minions.zombies) worth.Add(S(zombieMinions().Count)+"/"+S(zombieCap())+" zombies · "+S(corpses.Count)+" corpses");
    if(minions.Any(m=>m.kind == "overseer" && !m.gone)) worth.Add("overseer");
    buffs.Add(new HudRow { c = "#b7a6ff", t = "LEGION "+S(L.Count)+"/"+S(minionMax()), more = worth });
  }
  /* active ailments, so you can see why you're slow / on fire / getting crit */
  var ails = new List<HudRow>();
  if(burnStacks()>0)  ails.Add(new HudRow {c = "#ff8a3c", t = "BURN ×"+burnStacks()+" · "+burnDps().toFixed(1)+"/s · "+Math.ceil(burnTime())+"s"});
  if(psnStacks()>0)   ails.Add(new HudRow {c = "#8ac24a", t = "POISON ×"+psnStacks()+" · "+psnDps().toFixed(1)+"/s · "+Math.ceil(psnTime())+"s"});
  if(pbleedStacks()>0) ails.Add(new HudRow {c = "#ff5a5a", t = "BLEED ×"+pbleedStacks()+" · "+(pbleedDps()*(p.moving ? PBLEED_MOVE : 1)).toFixed(1)+"/s"+(p.moving ? " (moving ×2)" : "")+" · "+Math.ceil(pbleedTime())+"s"});
  if(p.chill.stacks>0)ails.Add(new HudRow {c = "#bfe3ff", t = "CHILL ×"+Math.round(p.chill.stacks)+" · -"+Math.round((1-chillMul())*100)+"% spd"});
  if(p.shock.stacks>0)ails.Add(new HudRow {c = "#ffe95a", t = "SHOCK ×"+Math.round(p.shock.stacks)+" · +"+Math.round(shockCritChance()*100)+"% crit taken"});
  foreach (var el in ELEMENTS)
    if(p.brand[el] > 0)
      ails.Add(new HudRow {c = BRAND_COL[el], t = BRAND_NAME[el]+" ×"+p.brand[el]+" · "+el+" res "+
        Math.round((1-elemTake(el))*100)+"% · "+Math.ceil(p.brandT[el])+"s"});
  if(p.curse["t"]>0) foreach (var k in CURSE_KEYS){
    if(p.curse[k] > 0)
      ails.Add(new HudRow {c = CURSES[k].col, t = CURSES[k].name+(p.curseN[k]>1?" ×"+p.curseN[k]:"")+
        " -"+Math.round(p.curse[k]*100)+"% · "+Math.ceil(p.curse["t"])+"s"});
  }
  /* TWO COLUMNS, and the right one only takes up room when there is something
     in it: a clean hero shows one narrow list on the left and nothing else. */
  double COL_W = 228, COL_GAP = 8, PILL = COL_W - 8, TXT_X = 22;
  /* A ROW THAT RUNS OUT OF THE PILL is worse than a row that says less. The
     extras are dropped one at a time until what is left fits, so the NAME and
     the count are never the thing that gets cut. */
  string fit(HudRow a){
    ctx.font="700 11px Fredoka";
    var room = PILL - TXT_X - 8;
    var parts = (a.more ?? new List<string>()).ToList();
    var t = a.t;
    while(parts.Count > 0 && ctx.measureText(t + " \u00b7 " + string.Join(" \u00b7 ", parts)).width > room)
      parts.RemoveAt(parts.Count - 1);
    return parts.Count > 0 ? t + " \u00b7 " + string.Join(" \u00b7 ", parts) : t;
  }
  var ROW0 = xpY + 44;
  void column(List<HudRow> list, double x){ for (int i = 0; i < list.Count; i++) { var a = list[i];
    var ay = ROW0 + i*20;
    ctx.fillStyle = "rgba(15,20,25,0.62)";
    roundRect(ctx, x, ay, PILL, 17, 8); ctx.fill();
    ctx.fillStyle = a.c;
    ctx.beginPath(); ctx.arc(x + 11, ay+8.5, 5, 0, TAU); ctx.fill();
    ctx.textAlign="left"; ctx.textBaseline="middle";
    ctx.fillStyle = a.c; ctx.fillText(fit(a), x + TXT_X, ay+9);
  } }
  /* the header sits over a brick field of any colour, so it carries its own
     dark outline the way every other floating label in the game does */
  void head(string txt, double x, Col col){
    ctx.font="700 10px Fredoka"; ctx.textAlign="left"; ctx.textBaseline="middle";
    ctx.letterSpacing = "1.5px";
    ctx.strokeStyle = "rgba(12,16,20,0.85)"; ctx.lineWidth = 3;
    ctx.strokeText(txt, x + 3, ROW0 - 12);
    ctx.fillStyle = col; ctx.fillText(txt, x + 3, ROW0 - 12);
    ctx.letterSpacing = "0px";
  }
  if(buffs.Count > 0){ head("BUFFS", 24, "#9fdc7e"); column(buffs, 24); }
  if(ails.Count > 0){
    var ax = 24 + (buffs.Count > 0 ? COL_W + COL_GAP : 0);
    head("AILMENTS", ax, "#ff8a6a");
    column(ails, ax);
  }
  ctx.textBaseline="middle";
  void autoSlot(double x, double frac, Action<double, double> iconFn, string label){
    ctx.fillStyle="rgba(15,20,25,0.6)";
    ctx.beginPath(); ctx.arc(x, ch-56, 24, 0, TAU); ctx.fill();
    try{ iconFn(x, ch-56); }catch{}
    if(frac>0 && frac<1){
      ctx.fillStyle="rgba(10,14,18,0.72)";
      ctx.beginPath(); ctx.moveTo(x,ch-56);
      ctx.arc(x, ch-56, 25, -Math.PI/2.0, -Math.PI/2.0 + TAU*(1-frac)); ctx.closePath(); ctx.fill();
    }
    ctx.fillStyle="#e8ebe9"; ctx.font="700 11px Fredoka"; ctx.textAlign="center";
    /* a count goes on a line of its own: 'ZOMBIES 12/12' is wider than a slot */
    var two = System.Text.RegularExpressions.Regex.Match(label, @"^(\S+) (\d+/\d+)$");
    if(two.Success){ ctx.fillText(two.Groups[1].Value, x, ch-56+37); ctx.font="700 10px Fredoka"; ctx.fillStyle="#b9c0bc"; ctx.fillText(two.Groups[2].Value, x, ch-56+49); }
    else ctx.fillText(label, x, ch-56+40);
  }
  var autos = new List<HudAuto>();
  /* THE LEGION, COUNTED. In a crowd your skeletons are hard to pick out, so the
     count sits in the ability row: how many stand of how many may, the ring
     filling as the legion does */
  if(isSummoner()){
    var cc = Js.Or(p.convoCd, 0);
    autos.Add(new HudAuto { f = cc > 0 ? clamp(1 - cc/CONVO_CD, 0, 1) : 1, label = cc > 0 ? cc.toFixed(1)+"s" : "CALL",
      icon = (x,y)=>drawConvoIcon(x, y) });
    var L = legion(); double up = L.Count(m=>m.rise >= 1), mx = minionMax();
    autos.Add(new HudAuto { f = mx != 0 ? clamp(up/mx, 0, 1) : 1, label = "LEGION "+S(up)+"/"+S(mx), icon = (x,y)=>drawLegionIcon(x, y, up) });
    if(p.minions.zombies){
      double zu = zombieMinions().Count(m=>m.rise >= 1);
      autos.Add(new HudAuto { f = clamp(zu/zombieCap(), 0, 1), label = "ZOMBIES "+S(zu)+"/"+S(zombieCap()), icon = (x,y)=>drawZombieBadge(x, y, zu) });
    }
    var gm = golemMax();
    if(gm != 0){
      double gu = golems().Count(m=>m.rise >= 1);
      autos.Add(new HudAuto { f = clamp(gu/gm, 0, 1), label = "GOLEMS "+S(gu)+"/"+S(gm), icon = (x,y)=>drawGolemBadge(x, y, gu) });
    }
  }
  if(p.orbit.n>0) autos.Add(new HudAuto {f = 1, label = "ORBIT ×"+p.orbit.n, icon = (x,y)=>{
    ctx.save(); ctx.translate(x,y); ctx.rotate(vtime*2);
    ctx.fillStyle="#d3242b"; ctx.strokeStyle="rgba(20,30,40,0.5)"; ctx.lineWidth=1.4;
    roundRect(ctx,-8,-4,16,8,2); ctx.fill(); ctx.stroke();
    drawStudG(ctx,-3.5,0,1.8,"#8f161d"); drawStudG(ctx,3.5,0,1.8,"#8f161d");
    ctx.restore();
  }});
  if(p.storm.n>0) autos.Add(new HudAuto {
    f = p.storm.queued>0 ? 0 : clamp(p.storm.cd/p.storm.rate,0,1),
    label = "STORM ×"+p.storm.n, icon = (x,y)=>{
      ctx.save(); ctx.translate(x,y); ctx.rotate(vtime*3);
      ctx.strokeStyle="#cfd6dd"; ctx.lineWidth=2.4; ctx.lineCap="round";
      ctx.beginPath(); ctx.moveTo(-7,7); ctx.lineTo(7,-7); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(7,7); ctx.lineTo(-7,-7); ctx.stroke();
      ctx.restore();
    }});
  if(p.vortex.n>0) autos.Add(new HudAuto {
    f = p.vortex.t>0 ? 0 : clamp(p.vortex.cd/p.vortex.rate,0,1),
    label = "VORTEX", icon = (x,y)=>{
      ctx.save(); ctx.translate(x,y); ctx.rotate(vtime*6);
      ctx.strokeStyle="#e8eef2"; ctx.lineWidth=2.2; ctx.lineCap="round";
      ctx.beginPath(); ctx.arc(0,0,9,0.4,2.6); ctx.stroke();
      ctx.beginPath(); ctx.arc(0,0,4.5,2.8,5.2); ctx.stroke();
      ctx.restore();
    }});
  if(p.freeze.n>0) autos.Add(new HudAuto {
    f = clamp(p.freeze.cd/p.freeze.rate,0,1),
    label = "FREEZE", icon = (x,y)=> drawFreezeIcon(x,y) });
  if(p.chain.n>0 && p.chain.zappers>0) autos.Add(new HudAuto {f = clamp((p.chain.cds.Count > 0 ? p.chain.cds.Min() : double.PositiveInfinity)/p.chain.rate,0,1),
    label = "CHAIN "+Math.min(chainMaxZappers(),p.chain.zappers)+"×"+p.chain.n, icon = (x,y)=>{
    ctx.strokeStyle="#8fd4ff"; ctx.lineWidth=2.2; ctx.lineCap="round"; ctx.lineJoin="round";
    ctx.beginPath(); ctx.moveTo(x-8,y-5); ctx.lineTo(x-2,y-1); ctx.lineTo(x-7,y+2); ctx.lineTo(x+3,y+6); ctx.stroke();
    ctx.fillStyle="#8fd4ff";
    ctx.beginPath(); ctx.arc(x+8,y-5,2.4,0,TAU); ctx.fill();
  }});
  if(p.bane.n>0) autos.Add(new HudAuto {f = clamp(p.bane.cd/p.bane.rate,0,1), label = "BANE ×"+p.bane.n, icon = (x,y)=>{
    ctx.fillStyle="#8ede4a";
    ctx.beginPath(); ctx.moveTo(x-4,y-7); ctx.lineTo(x+4,y-7); ctx.lineTo(x+6,y+5);
    ctx.lineTo(x-6,y+5); ctx.closePath(); ctx.fill();
    ctx.fillStyle="rgba(200,255,150,0.85)";
    ctx.beginPath(); ctx.arc(x-1,y+1,2,0,TAU); ctx.fill();
  }});
  if(p.bomb.n>0) autos.Add(new HudAuto {f = clamp(p.bomb.cd/p.bomb.rate,0,1), label = "BOMB ×"+p.bomb.n, icon = (x,y)=>{
    ctx.save(); ctx.translate(x,y-1); ctx.rotate(-0.2);
    ctx.fillStyle=PAL.red; ctx.strokeStyle="rgba(20,30,40,0.5)"; ctx.lineWidth=1.4;
    roundRect(ctx,-9,-5,18,10,2); ctx.fill(); ctx.stroke();
    for(var r2=0;r2<2;r2++) for(var c2=0;c2<3;c2++) drawStudG(ctx,-5+c2*5,-2+r2*4.4,1.7,PAL.dred);
    ctx.restore();
  }});
  /* the globes first, and the ability row starts where the life globe ends */
  drawPoolGlobes(p);
  for (int ai = 0; ai < autos.Count; ai++) { var a = autos[ai]; autoSlot(GLOBE_ROW_X + ai*62, a.f, a.icon, a.label); }
  void ab(double x, double cd, double cdMax, Action<double, double> drawFn, string label){
    ctx.fillStyle="rgba(15,20,25,0.6)";
    ctx.beginPath(); ctx.arc(x, ch-56, 27, 0, TAU); ctx.fill();
    try { drawFn(x, ch-56); } catch {}
    if(cd>0){
      ctx.fillStyle="rgba(10,14,18,0.72)";
      ctx.beginPath(); ctx.moveTo(x,ch-56);
      ctx.arc(x, ch-56, 28, -Math.PI/2.0, -Math.PI/2.0 + TAU*clamp(cd/cdMax,0,1)); ctx.closePath(); ctx.fill();
    } else {
      ctx.strokeStyle=rgba(255,185,0,0.5+0.3*Math.sin(vtime*5)); ctx.lineWidth=2.5;
      ctx.beginPath(); ctx.arc(x, ch-56, 29, 0, TAU); ctx.stroke();
    }
    ctx.fillStyle="#e8ebe9"; ctx.font="700 11px Fredoka"; ctx.textAlign="center";
    ctx.fillText(label, x, ch-56+42);
  }
  ab(cw-GLOBE_ROW_X, p.dash.cd, p.dash.cdMax, (x,y)=>{
    ctx.strokeStyle="#9fdc7e"; ctx.lineWidth=3.5; ctx.lineCap="round"; ctx.lineJoin="round";
    ctx.beginPath(); ctx.moveTo(x-11,y-9); ctx.lineTo(x-2,y); ctx.lineTo(x-11,y+9);
    ctx.moveTo(x+2,y-9); ctx.lineTo(x+11,y); ctx.lineTo(x+2,y+9); ctx.stroke();
  }, "SPACE");
  if(p.magnetT>0)
    ab(cw-GLOBE_ROW_X-74, Js.Or(p.magnetMax, BUFF_T)-p.magnetT, Js.Or(p.magnetMax, BUFF_T), (x,y)=> drawMagnetIcon(x,y), Math.ceil(p.magnetT)+"s");
  if(p.fortuneT>0)
    ab(cw-GLOBE_ROW_X-148, Js.Or(p.fortuneMax, BUFF_T)-p.fortuneT, Js.Or(p.fortuneMax, BUFF_T), (x,y)=> drawFortuneIcon(x,y,p.fortuneMul),
       "×"+p.fortuneMul+" · "+Math.ceil(p.fortuneT)+"s");
  if(p.rageT>0)
    ab(cw-GLOBE_ROW_X-222, Js.Or(p.rageMax, BUFF_T)-p.rageT, Js.Or(p.rageMax, BUFF_T), (x,y)=> drawRageIcon(x,y,p.rageMul),
       "×"+p.rageMul+" dmg · "+Math.ceil(p.rageT)+"s");
  if(p.wardT>0)
    ab(cw-GLOBE_ROW_X-296, Js.Or(p.wardMax, BUFF_T)-p.wardT, Js.Or(p.wardMax, BUFF_T), (x,y)=> drawWardIcon(x,y,p.wardN),
       "+"+wardArmour()+" arm · "+Math.ceil(p.wardT)+"s");
  if(p.regenBoostT>0)
    ab(cw-GLOBE_ROW_X-370, HEART_REGEN_T-p.regenBoostT, HEART_REGEN_T, (x,y)=> drawRegenIcon(x,y),
       "×"+HEART_STACK_MUL[Math.min(HEART_STACK_MAX,Js.Or(p.heartStacks, 1))]+" · "+
       cursedRegen().toFixed(2)+"/s · "+Math.ceil(p.regenBoostT)+"s");
  if(musicToast != null){
    musicToast.life -= frameRaw;
    if(musicToast.life<=0) musicToast = null;
    else {
      var a2 = clamp(musicToast.life/0.6,0,1) * clamp((3.2-musicToast.life)/0.3,0,1);
      ctx.textAlign="left"; ctx.font="700 12px Fredoka";
      ctx.globalAlpha = a2;
      ctx.strokeStyle="rgba(15,20,25,0.85)"; ctx.lineWidth=4;
      ctx.strokeText("♪  "+musicToast.name, 26, ch - 2*GLOBE_R - 44);
      ctx.fillStyle="#ffd76e"; ctx.fillText("♪  "+musicToast.name, 26, ch - 2*GLOBE_R - 44);
      ctx.globalAlpha = 1;
    }
  }
  renderCombatLog();
  if(banner != null){
    banner.t += frameRaw;
    var b = banner;
    var inK = clamp(b.t/0.3,0,1);
    var alpha = b.t > b.dur-0.4 ? clamp((b.dur-b.t)/0.4,0,1) : 1;
    var sc = easeOutBack(inK);
    ctx.save(); ctx.translate(cw/2.0, ch*0.28); ctx.scale(sc,sc); ctx.globalAlpha=alpha;
    ctx.textAlign="center"; ctx.font=$"700 {S(b.size)}px \"Titan One\"";
    ctx.strokeStyle="rgba(15,20,25,0.85)"; ctx.lineWidth=b.size*0.16;
    ctx.strokeText(b.text,0,0);
    ctx.fillStyle=b.color; ctx.fillText(b.text,0,0);
    if(!string.IsNullOrEmpty(b.sub)){ ctx.font="600 17px Fredoka"; ctx.lineWidth=4;
      ctx.strokeText(b.sub,0,b.size*0.7); ctx.fillStyle="#f2f3f2"; ctx.fillText(b.sub,0,b.size*0.7); }
    ctx.restore(); ctx.globalAlpha=1;
    if(b.t>b.dur) banner=null;
  }
}
public static void renderReticle(){
  double x = mouse.x, y = mouse.y;
  var r = 9 + retPulse*5;
  ctx.strokeStyle = "#ffb900"; ctx.lineWidth = 2.5;
  ctx.beginPath(); ctx.arc(x,y,Math.max(1,r),0,TAU); ctx.stroke();
  ctx.fillStyle = "#ffb900";
  ctx.beginPath(); ctx.arc(x,y,2.2,0,TAU); ctx.fill();
  for(var i=0;i<4;i++){ var a=i/4.0*TAU + vtime*0.8;
    ctx.beginPath(); ctx.moveTo(x+Math.cos(a)*(r+3), y+Math.sin(a)*(r+3));
    ctx.lineTo(x+Math.cos(a)*(r+8), y+Math.sin(a)*(r+8)); ctx.stroke(); }
}

}
