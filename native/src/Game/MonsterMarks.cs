// lego-survival.html, JS 26803-27346: what a monster wears — its wounds, its shock,
// its fires, its poison and the counts over them — and THE ROCK MONSTER: the golem's
// aura, its skins, its rolled-once profile, its baked body and its live limbs.
namespace Brickblade;

public sealed class GolemSkin { public Col[] rock; public Col dark, edge, eye; public double[] eyeGlow; public string vein, grit; }
public sealed class GolemProf { public double[][] back, chest, hip, shL, shR, head, lumps; public double browY; }
public sealed class GolemBakeT { public OffCanvas cv; public double s, ox, oy; }
public sealed partial class Ent { public GolemProf _prof; public GolemBakeT _bake; }

public static partial class G
{
    public static readonly double[][] BLEED_CUTS = { new double[]{-5,-19}, new double[]{5,-26}, new double[]{6,-15}, new double[]{-6,-29}, new double[]{0,-22}, new double[]{-2,-33}, new double[]{8,-21}, new double[]{-9,-24} };
    public static void drawBleedMark(Ctx c, Ent e){
      var n = bleedStacks(e);
      if(n == 0) return;
      var s = Js.Or(e.scale, 1);
      var still = e.spdNow < BLEED_STILL_SPD;
      var a = still ? 0.45 : 0.9;
      c.lineCap = "round";
      /* HALF THE ROSTER IS RED, so a red gash on its own vanishes. Each one is cut
         twice: a near-black liner, then the bright wound on top of it. */
      foreach (var pass in new double[]{0,1}){
        c.strokeStyle = pass != 0 ? rgba(255,74,84,a) : rgba(58,4,8,a*0.9);
        c.lineWidth = pass != 0 ? 2.2*s : 4.4*s;
        for(var i=0;i<n && i<BLEED_CUTS.Length;i++){
          double ox = BLEED_CUTS[i][0]*s, oy = BLEED_CUTS[i][1]*s;
          var flip = (i & 1) != 0 ? -1 : 1;
          c.beginPath();
          c.moveTo(e.x+ox-3.5*s*flip, e.y+oy-3.5*s);
          c.lineTo(e.x+ox+3.5*s*flip, e.y+oy+3.5*s);
          c.stroke();
        }
      }
      c.lineCap = "butt";
      /* a bead running down, slower when the body has stopped */
      var drip = ((vtime*(still?0.5:1.3) + e.phase) % 1);
      c.fillStyle = rgba(40,2,6,(1-drip)*a*0.8);
      c.beginPath(); c.arc(e.x+2*s, e.y + (-18 + drip*16)*s, 2.8*s, 0, TAU); c.fill();
      c.fillStyle = rgba(230,40,50,(1-drip)*a);
      c.beginPath(); c.arc(e.x+2*s, e.y + (-18 + drip*16)*s, 2*s, 0, TAU); c.fill();
      /* a pool at its feet that widens with the wounds, so a body eight deep reads
         as eight deep from across the field */
      c.fillStyle = rgba(150,16,24,(0.20 + 0.035*n)*a);
      c.beginPath(); c.ellipse(e.x, e.y+2, (7+1.6*n)*s, (2.8+0.5*n)*s, 0, 0, TAU); c.fill();
    }
    /* A BURNING BODY WEARS ITS FIRES: one flame pip per stack across its shoulders,
       and the BURNT marks above them, because how deep a body is burning decides
       whether the next bolt breaks it. */
    /* SHOCK, ON THE BODY. The ailment landed and nothing said so, which made the
       one mechanic whose whole job is "hit this one next" invisible. Arcs crawl the
       silhouette rather than pips sitting beside it: a shocked body should read as
       shocked from across the field, at a glance, while it is moving. The yellow
       deepens with the stack count, and the arcs jitter on their own clock so a pack
       of five shocked bodies does not strobe in unison. */
    public static void drawShockArcs(Ctx c, Ent e){
      var n = shockStacksOn(e);
      if(!Js.T(n)) return;
      double s = Js.Or(e.scale, 1), k = n / ESHOCK_MAX;
      double top = e.y - 44*s, h = 44*s, w = 15*s;
      /* a charged haze, brighter the deeper the stacks */
      c.save();
      c.fillStyle = rgba(255,233,90,0.035 + 0.032*n);
      c.beginPath(); c.ellipse(e.x, e.y - 20*s, w*1.05, 23*s, 0, 0, TAU); c.fill();
      /* the arcs themselves — one per stack, each a jagged line down the body */
      c.lineCap = "round"; c.lineJoin = "round";
      for(var i=0;i<n;i++){
        /* a per-arc clock, so they crackle independently */
        var t = vtime*11 + i*2.7 + e.phase*3;
        var side = (i % 2) != 0 ? 1 : -1;
        /* the arcs HUG the silhouette — pushed out to its edge and jittering only a
           little, so the body stays readable underneath them */
        var x0 = e.x + side*w*(0.78 + 0.16*Math.sin(t*0.7));
        c.beginPath();
        for(var seg=0; seg<=4; seg++){
          var f = seg/4.0;
          var px = x0 + Math.sin(t + seg*2.2)*w*0.22*(1-f*0.35);
          var py = top + h*f;
          if (seg != 0) c.lineTo(px, py); else c.moveTo(px, py);
        }
        /* drawn twice: a wide soft glow, then the hot white core over it */
        c.strokeStyle = rgba(255,214,60,0.22 + 0.20*k); c.lineWidth = 2.3;
        c.stroke();
        c.strokeStyle = rgba(255,252,214,0.40 + 0.28*k); c.lineWidth = 1.0;
        c.stroke();
      }
      /* at full stacks it sparks off the body as well, so the cap is a visible state
         and not just a number that stopped going up */
      if(n >= ESHOCK_MAX && Js.R() < 0.35)
        particles.Add(new Particle {kind = "spark", x = e.x+rnd(-11,11)*s, y = e.y-rnd(4,40)*s,
          vx = rnd(-40,40), vy = rnd(-70,-10), life = rnd(0.12,0.28), max = 0.28, color = "#fff6b0"});
      c.restore();
    }
    public static void drawBurnPips(Ctx c, Ent e){
      double n = burnStacksOn(e), marks = (e.burnt != null && e.burnt.t > 0) ? e.burnt.n : 0;
      if(!Js.T(n) && !Js.T(marks)) return;
      var s = Js.Or(e.scale, 1);
      if(Js.T(n)){
        /* a hot haze over the body first */
        c.fillStyle = rgba(255,138,60,Math.min(0.24, 0.05 + 0.04*n));
        c.beginPath(); c.ellipse(e.x, e.y - 20*s, 14*s, 21*s, 0, 0, TAU); c.fill();
        /* THE FLAMES BURN AT ITS FEET, not across its chest. Orange pips on the
           torso of a red imp are invisible; a fire at the base of the body reads
           from across the field and cannot collide with the health bar or with the
           poison and wound counts stacked above its head. */
        var w = 8*s;
        /* a scorched patch under it, widening with the fire */
        c.fillStyle = rgba(90,26,4,0.14 + 0.05*n);
        c.beginPath(); c.ellipse(e.x, e.y+3, (9+3.4*n)*s, (3.4+0.7*n)*s, 0, 0, TAU); c.fill();
        for(var i=0;i<n;i++){
          var px = e.x + (i - (n-1)/2.0)*w;
          var py = e.y + 1*s;
          var h = (8.5 + Math.sin(vtime*13 + i*2.1 + e.phase)*2.2)*s;
          /* a dark liner first, so the flame reads against a red body */
          c.fillStyle = "rgba(58,10,2,0.8)";
          c.beginPath(); c.moveTo(px, py);
          c.quadraticCurveTo(px-h*0.62, py-h*0.45, px, py-h*1.15);
          c.quadraticCurveTo(px+h*0.62, py-h*0.45, px, py);
          c.fill();
          c.fillStyle = "#ff6a1a";
          c.beginPath(); c.moveTo(px, py-h*0.05);
          c.quadraticCurveTo(px-h*0.44, py-h*0.42, px, py-h*0.98);
          c.quadraticCurveTo(px+h*0.44, py-h*0.42, px, py-h*0.05);
          c.fill();
          c.fillStyle = "#ffb02e";
          c.beginPath(); c.moveTo(px, py-h*0.12);
          c.quadraticCurveTo(px-h*0.26, py-h*0.38, px, py-h*0.7);
          c.quadraticCurveTo(px+h*0.26, py-h*0.38, px, py-h*0.12);
          c.fill();
          c.fillStyle = "#fff0b0";
          c.beginPath(); c.arc(px, py-h*0.3, h*0.15, 0, TAU); c.fill();
        }
      }
      if(Js.T(marks)) tagCount(c, "BURNT \u00d7"+S(marks), e.x, e.y - 90*s, s, "#ff9a3c", "rgba(60,14,2,0.92)");
    }
    public const double PIP_MAX = 12;          /* beads drawn before the count takes over */
    public static void drawPoisonPips(Ctx c, Ent e){
      var po = e.poison;
      if(po == null || !Js.T(po.n)) return;
      var s = Js.Or(e.scale, 1);
      double ry = -34*s, rx = 15*s;
      /* a sickly haze over the body first — CAPPED, because Spore Burst can put
         thirty-six doses in a body and a haze that deep would swallow the monster */
      c.fillStyle = rgba(126,214,58,Math.min(0.26, 0.10 + 0.020*po.n));
      c.beginPath(); c.ellipse(e.x, e.y - 20*s, 15*s, 22*s, 0, 0, TAU); c.fill();
      /* you can count beads up to a point; past it the number is the readable thing */
      var beads = Math.min(po.n, PIP_MAX);
      for(var i=0;i<beads;i++){
        var a = vtime*1.6 + i*TAU/beads + e.phase;
        var px = e.x + Math.cos(a)*rx;
        var py = e.y + ry + Math.sin(a)*rx*0.42 + Math.sin(vtime*3+i)*1.5;
        var rr = 2.6*s;
        c.fillStyle = "rgba(30,64,16,0.55)";
        c.beginPath(); c.arc(px, py+1.2, rr+0.9, 0, TAU); c.fill();
        c.fillStyle = "#8ede4a";
        c.beginPath(); c.arc(px, py, rr, 0, TAU); c.fill();
        c.fillStyle = "rgba(220,255,170,0.9)";
        c.beginPath(); c.arc(px-rr*0.3, py-rr*0.3, rr*0.4, 0, TAU); c.fill();
      }
    }
    /* Past a dozen doses nobody can count beads, so the number takes over. It sits
       clear of the health bar at -56, which is the only thing up there. */
    public static void drawPoisonCount(Ctx c, Ent e){
      var s = Js.Or(e.scale, 1);
      var po = e.poison;
      if(po != null && po.n > PIP_MAX) tagCount(c, "\u00d7"+S(po.n), e.x, e.y - 64*s, s, "#b6f272", "rgba(16,38,8,0.92)");
      /* wounds sit one line higher, so a body carrying both never overlaps itself */
      var bn = bleedStacks(e);
      if(bn > 1) tagCount(c, "\u00d7"+S(bn), e.x, e.y - 78*s, s, "#ff7a82", "rgba(50,4,8,0.92)");
    }
    public static void tagCount(Ctx c, string txt, double x, double y, double s, Paint fill, Paint line){
      c.font = $"700 {S(Math.round(12*s))}px Fredoka, sans-serif";
      c.textAlign = "center";
      c.lineWidth = 3.5;
      c.strokeStyle = line;
      c.strokeText(txt, x, y);
      c.fillStyle = fill;
      c.fillText(txt, x, y);
      c.textAlign = "left";
    }
    public static readonly JsMap<string, (double[] a, double[] b)> GOLEM_RGB = new()
    {
        ["ice"] = (new double[] { 150, 210, 255 }, new double[] { 223, 242, 255 }),
        ["fire"] = (new double[] { 255, 120, 40 }, new double[] { 255, 209, 102 }),
        ["stone"] = (new double[] { 150, 156, 150 }, new double[] { 210, 214, 210 }),
        ["storm"] = (new double[] { 255, 233, 90 }, new double[] { 255, 243, 160 })
    };
    /* `rgba(${rgb},${a})` where rgb is the JS's "r,g,b" string */
    static Col rgbA(double[] c, double a) => rgba(c[0], c[1], c[2], a);
    public static void drawGolemAura(Ctx c, Ent e)
    {
        var gg = GOLEM_RGB[e.golem]; var s = e.scale;
        var wind = (e.mode == "stompWind" || e.mode == "chargeWind" || e.mode == "throwWind");
        var spin = (e.golem == "stone" ? 0.9 : 1.7) * (wind ? 2.6 : 1);
        /* a pool of its element seeping into the ground it stands on */
        c.fillStyle = rgbA(gg.a, 0.15 + 0.07 * Math.sin(vtime * 4 + e.phase));
        c.beginPath(); c.ellipse(e.x, e.y + 2, 23 * s, 9 * s, 0, 0, TAU); c.fill();
        c.strokeStyle = rgbA(gg.b, 0.45); c.lineWidth = 2.2;
        c.beginPath(); c.ellipse(e.x, e.y + 2, 23 * s, 9 * s, 0, 0, TAU); c.stroke();
        for (int i = 0; i < 5; i++)
        {
            var ga = vtime * spin + i * TAU / 5 + e.phase;
            var gr = (30 + (wind ? 5 : 0)) * s + Math.sin(vtime * 2 + i) * 3 * s;
            var gx = e.x + Math.cos(ga) * gr;
            var gy = e.y - 54 * s + Math.sin(ga) * gr * 0.42;
            c.save(); c.translate(gx, gy); c.rotate(ga * 1.6);
            if (e.golem == "fire")
            {
                var fl = 0.6 + 0.4 * Math.sin(vtime * 10 + i);
                c.fillStyle = rgbA(gg.a, 0.45 * fl);
                c.beginPath(); c.arc(0, 0, 4.2 * s, 0, TAU); c.fill();
                c.fillStyle = rgbA(gg.b, 0.75 + 0.25 * fl);
                c.beginPath(); c.arc(0, 0, 2.2 * s, 0, TAU); c.fill();
            }
            else
            {
                c.fillStyle = rgbA(gg.b, 0.95);
                c.strokeStyle = rgba(25, 40, 55, 0.65); c.lineWidth = 1.6;
                if (e.golem == "ice")
                {
                    c.beginPath(); c.moveTo(0, -4 * s); c.lineTo(2.5 * s, 0); c.lineTo(0, 4 * s); c.lineTo(-2.5 * s, 0);
                    c.closePath(); c.fill(); c.stroke();
                }
                else
                {
                    c.beginPath();
                    c.moveTo(-4 * s, -1.6 * s); c.lineTo(-1.4 * s, -4 * s); c.lineTo(3.4 * s, -2.4 * s);
                    c.lineTo(4 * s, 1.8 * s); c.lineTo(0.6 * s, 3.8 * s); c.lineTo(-3.4 * s, 2.2 * s);
                    c.closePath(); c.fill(); c.stroke();
                }
            }
            c.restore();
        }
    }

    /* THE ROCK MONSTER. Not a mountain and not a robot — an APE made of rubble.
       The read is the pose: hunched so far forward the head sits lower than the
       shoulders, enormous arms that reach past the knees and take the weight on
       their knuckles, a heavy slab of a back, stubby bent legs, and two red coals
       for eyes set in a blunt slab of a face. Every piece is a chunk of rock with
       flat faces and hard corners; nothing on it is smooth and nothing is a panel. */
    public static readonly JsMap<string, GolemSkin> GOLEM_SKIN = new()
    {
        ["stone"] = new GolemSkin { rock = new Col[] { "#9a9083", "#8a8073", "#756b60", "#a9a093", "#5f574d" }, dark = "#4c453d",
            edge = "rgba(30,26,20,0.8)", eye = "#ff2a1a", eyeGlow = new double[] { 255, 60, 40 },
            vein = null, grit = "rgba(60,54,44,0.5)" },
        ["ice"] = new GolemSkin { rock = new Col[] { "#bfe2f7", "#8fc4e8", "#68a6cf", "#e4f4ff", "#4d87b5" }, dark = "#376f97",
            edge = "rgba(14,42,66,0.9)", eye = "#eaf9ff", eyeGlow = new double[] { 150, 215, 255 },
            vein = "rgba(230,246,255,0.9)", grit = "rgba(255,255,255,0.35)" },
        ["fire"] = new GolemSkin { rock = new Col[] { "#4a2015", "#33150d", "#61301b", "#20100a", "#7a3a1d" }, dark = "#180b06",
            edge = "rgba(16,8,4,0.85)", eye = "#ffc247", eyeGlow = new double[] { 255, 150, 40 },
            vein = "rgba(255,110,25,0.95)", grit = "rgba(255,140,40,0.35)" },
        /* the scepter's own: bone and violet, so it is never read as one of theirs */
        ["bone"] = new GolemSkin { rock = new Col[] { "#e4def5", "#cfc6ea", "#b7aad8", "#f2eefb", "#9a8cc4" }, dark = "#5b4a8f",
            edge = "rgba(40,30,70,0.85)", eye = "#b7a6ff", eyeGlow = new double[] { 183, 166, 255 },
            vein = "rgba(183,166,255,0.8)", grit = "rgba(90,70,140,0.35)" },
        ["storm"] = new GolemSkin { rock = new Col[] { "#4a4636", "#38352a", "#5e5940", "#2a2820", "#6e6848" }, dark = "#221f18",
            edge = "rgba(20,18,10,0.85)", eye = "#fff3a0", eyeGlow = new double[] { 255, 233, 90 },
            vein = "rgba(255,233,90,0.95)", grit = "rgba(255,243,160,0.4)" },
        /* the two the scepter's golems brought: void and meat */
        ["chaos"] = new GolemSkin { rock = new Col[] { "#3a2350", "#2a1840", "#4d2f6a", "#1c1030", "#5e3a80" }, dark = "#140a22",
            edge = "rgba(12,6,20,0.85)", eye = "#d6a6ff", eyeGlow = new double[] { 180, 92, 255 },
            vein = "rgba(180,92,255,0.9)", grit = "rgba(140,255,120,0.3)" },
        ["carrion"] = new GolemSkin { rock = new Col[] { "#8a4a44", "#6e3a36", "#a35a52", "#e4d6c8", "#5a2e2a" }, dark = "#3a1c1a",
            edge = "rgba(30,10,8,0.85)", eye = "#ffe0a0", eyeGlow = new double[] { 255, 200, 120 },
            vein = "rgba(210,90,90,0.8)", grit = "rgba(230,214,200,0.4)" }
    };
    /* Every chunk of one monster, rolled once from its seed and kept for life, so a
       rock monster has its own lumps instead of shimmering new ones every frame. */
    public static GolemProf golemProfile(Ent e)
    {
        if (e._prof != null) return e._prof;
        double h(double n) => hash2(Js.I32(e.phase * 997) + n * 13, n * 61);
        /* an irregular block: [x, y, w, h] jittered into a six-point slab */
        double[][] slab(double x, double y, double w, double hh, double n)
        {
            var pts = new[] { new[] { -0.5, -0.5 }, new[] { 0.12, -0.56 }, new[] { 0.5, -0.4 }, new[] { 0.52, 0.42 }, new[] { 0.06, 0.54 }, new[] { -0.5, 0.44 } };
            var p = new double[pts.Length][];
            for (int k = 0; k < pts.Length; k++)
                p[k] = new[] { x + pts[k][0] * w + (h(n + k) - 0.5) * w * 0.22,
                               y + pts[k][1] * hh + (h(n + k + 7) - 0.5) * hh * 0.22 };
            return p;
        }
        var prof = new GolemProf
        {
            /* the hunched back: one big mass, highest at the shoulders, falling away */
            back = slab(0, -52, 46 + h(1) * 7, 32 + h(2) * 6, 1),
            chest = slab(1, -32, 38 + h(3) * 6, 26, 20),
            hip = slab(0, -15, 31 + h(4) * 5, 20, 40),
            /* boulders capping each shoulder — carried high, above the head */
            shL = slab(-24 - h(5) * 3, -62, 25, 21, 60),
            shR = slab(24 + h(6) * 3, -62, 25, 21, 80),
            /* the head, slung FORWARD and DOWN, sitting LOWER than the shoulders —
               the single thing that separates a rock monster from a rock man */
            head = slab(6, -42, 30 + h(7) * 4, 23, 100),
            /* loose rubble welded onto the back */
            lumps = new[] { 0, 1, 2, 3, 4 }.Select(k => new[] { (h(120 + k) - 0.5) * 40, -66 + h(130 + k) * 28, 3 + h(140 + k) * 4 }).ToArray(),
            browY = -47 - h(8) * 2
        };
        e._prof = prof;
        return prof;
    }
    public static void polyPath(Ctx c, double[][] pts)
    {
        c.beginPath();
        c.moveTo(pts[0][0], pts[0][1]);
        for (int k = 1; k < pts.Length; k++) c.lineTo(pts[k][0], pts[k][1]);
        c.closePath();
    }
    public static void rockChunk(Ctx c, double[][] pts, Paint fill, Paint edge, Paint? shade)
    {
        c.fillStyle = fill; c.strokeStyle = edge;
        polyPath(c, pts); c.fill(); c.stroke();
        if (shade != null)
        {
            /* one flat face catching the light, so every chunk reads as faceted stone */
            c.fillStyle = shade.Value;
            c.beginPath();
            c.moveTo(pts[0][0], pts[0][1]);
            c.lineTo(pts[1][0], pts[1][1]);
            c.lineTo(pts[2][0], pts[2][1]);
            c.lineTo((pts[2][0] + pts[5][0]) / 2, (pts[2][1] + pts[5][1]) / 2);
            c.closePath(); c.fill();
        }
    }
    /* ONE ARM. It hangs from the shoulder boulder, is longer than the whole body,
       and ends in a fist the size of the head that it walks on. */
    public static void golemArm(Ctx c, GolemSkin K, Func<int, Col> R, double side, double a, double reach)
    {
        c.save(); c.rotate(a);
        c.strokeStyle = K.edge; c.lineWidth = 2; c.lineJoin = "round";
        /* upper */
        c.fillStyle = R(2);
        c.beginPath();
        c.moveTo(-11, -12); c.lineTo(17, -10); c.lineTo(20, 9); c.lineTo(-10, 11);
        c.closePath(); c.fill(); c.stroke();
        /* forearm — longer than the upper, which is what makes it an ape */
        c.save(); c.translate(18, 0); c.rotate(side > 0 ? 0.34 : -0.34);
        c.fillStyle = R(0);
        c.beginPath();
        c.moveTo(-5, -11); c.lineTo(reach - 6, -13); c.lineTo(reach - 2, 10); c.lineTo(-4, 10);
        c.closePath(); c.fill(); c.stroke();
        /* the knuckle it takes its weight on */
        c.fillStyle = R(3);
        c.beginPath();
        c.moveTo(reach - 9, -14); c.lineTo(reach + 8, -11); c.lineTo(reach + 12, 3);
        c.lineTo(reach + 5, 14); c.lineTo(reach - 8, 12);
        c.closePath(); c.fill(); c.stroke();
        c.fillStyle = rgba(15, 12, 8, 0.26);
        c.beginPath();
        c.moveTo(reach - 9, 4); c.lineTo(reach + 11, 4); c.lineTo(reach + 5, 14); c.lineTo(reach - 8, 12);
        c.closePath(); c.fill();
        c.restore();
        c.restore();
    }
    /* The body of a rock monster never changes shape: hips, chest, the hunched
       back, the rubble welded to it, both shoulder boulders, the head, the brow and
       the mouth are the same picture every frame of its life. Forty of them redrawing
       that stack sixty times a second is thousands of paths for a still image, so it
       is baked ONCE into a canvas of its own and blitted after that — one baked pixel
       to one screen pixel, because a RESAMPLED blit costs more than the paths it
       replaces. Only the arms, the legs, the coals and the veins are drawn live,
       because only they move. */
    public static void golemBake(Ent e, GolemSkin K, GolemProf P)
    {
        var s = e.scale * 1.05;
        if (e._bake != null && e._bake.s == s) return;
        Col R(int i) => K.rock[i];
        Paint SH = rgba(255, 255, 255, 0.10);
        int w = (int)Math.ceil(120 * s), h = (int)Math.ceil(110 * s);
        var cv = e._bake?.cv ?? new OffCanvas().Own(e);
        cv.width = w; cv.height = h;
        var g = cv.getContext("2d");
        g.setTransform(1, 0, 0, 1, 0, 0);
        double ox = Math.round(56 * s), oy = Math.round(92 * s);
        g.translate(ox, oy); g.scale(s, s);
        g.lineJoin = "round"; g.lineWidth = 2;

        rockChunk(g, P.hip, R(2), K.edge, SH);
        rockChunk(g, P.chest, R(1), K.edge, SH);
        rockChunk(g, P.back, R(0), K.edge, SH);
        for (int k = 0; k < P.lumps.Length; k++)
        {
            double lx = P.lumps[k][0], ly = P.lumps[k][1], lr = P.lumps[k][2];
            g.fillStyle = R(k % 2 != 0 ? 3 : 4); g.strokeStyle = K.edge; g.lineWidth = 1.4;
            g.beginPath();
            for (int q = 0; q < 6; q++)
            {
                double a2 = q / 6.0 * TAU, rr = lr * (0.75 + hash2(Js.I32(lx * 9) + q, Js.I32(ly * 5) + q * 3) * 0.5);
                double px = lx + Math.cos(a2) * rr, py = ly + Math.sin(a2) * rr * 0.85;
                if (q != 0) g.lineTo(px, py); else g.moveTo(px, py);
            }
            g.closePath(); g.fill(); g.stroke();
        }
        g.lineWidth = 2;
        rockChunk(g, P.shL, R(3), K.edge, SH);
        rockChunk(g, P.shR, R(1), K.edge, SH);
        rockChunk(g, P.head, R(0), K.edge, SH);
        g.fillStyle = K.dark;
        g.beginPath();
        g.moveTo(-9, P.browY); g.lineTo(21, P.browY - 1); g.lineTo(20, P.browY + 5); g.lineTo(-8, P.browY + 6);
        g.closePath(); g.fill();
        g.fillStyle = rgba(12, 9, 6, 0.72);
        g.beginPath();
        g.moveTo(-5, -34); g.lineTo(18, -35); g.lineTo(16, -30); g.lineTo(-4, -29);
        g.closePath(); g.fill();
        e._bake = new GolemBakeT { cv = cv, s = s, ox = ox, oy = oy };
    }
    public static void drawGolem(Ctx c, Ent e)
    {
        var K = GOLEM_SKIN.GetValueOrDefault(e.golem ?? "") ?? GOLEM_SKIN["stone"];
        var P = golemProfile(e);
        var s = e.scale * 1.05;
        double mv = e.moving ? 1 : 0;
        var w = e.walk;
        /* a knuckle-walker lurches: the body swings up onto the arms and drops back */
        var lurch = Math.abs(Math.sin(w)) * 3.2 * mv + Math.sin(vtime * 1.5 + e.phase) * 0.5;
        var roll = Math.sin(w) * 0.05 * mv;
        var armsUp = (e.mode == "stompWind" || e.mode == "throwWind");
        var braced = (e.mode == "chargeWind" || e.mode == "charge");
        var fl = e.flash > 0 ? Math.min(1, e.flash * 0.85) : 0;
        Col R(int i) => fl != 0 ? mixc(K.rock[i], "#ffffff", fl) : K.rock[i];
        Col D() => fl != 0 ? mixc(K.dark, "#ffffff", fl) : K.dark;
        var dir = (e.face != null && e.face.x < 0) ? -1 : 1;

        c.save();
        c.fillStyle = rgba(15, 30, 20, 0.34);
        c.beginPath(); c.ellipse(e.x, e.y + 2, 30 * s, 9 * s, 0, 0, TAU); c.fill();
        c.translate(e.x, e.y - lurch * s);
        c.scale(dir * s, s);
        c.rotate(roll);
        c.lineWidth = 2; c.lineJoin = "round";

        /* --- the far arm, behind everything --- */
        var swing = Math.sin(w) * 0.30 * mv;
        double armAngle(double side) => armsUp ? (side > 0 ? -1.45 : Math.PI + 1.45)
          : braced ? (side > 0 ? 0.20 : Math.PI - 0.20)
          /* at rest they plant forward and down, knuckles on the floor */
          : (side > 0 ? (1.16 + swing) : (Math.PI - 1.16 + swing));
        c.save(); c.translate(-23, -60);
        golemArm(c, K, i => mixc(R(i), "#000000", 0.20), -1, armAngle(-1), 34);
        c.restore();

        /* --- stubby bent legs, set wide and half hidden under the body --- */
        var step = Math.sin(w + Math.PI) * 4 * mv;
        foreach (var (lx, off) in new[] { (-13.0, step), (13.0, -step) })
        {
            c.fillStyle = D(); c.strokeStyle = K.edge;
            c.beginPath();
            c.moveTo(lx - 10, -16); c.lineTo(lx + 10, -16); c.lineTo(lx + 11, -5 + Math.max(0, off) * 0.3);
            c.lineTo(lx + 13, 1); c.lineTo(lx - 12, 1); c.lineTo(lx - 11, -6);
            c.closePath(); c.fill(); c.stroke();
            /* a blocky foot */
            c.fillStyle = R(4);
            c.beginPath();
            c.moveTo(lx - 13, -4); c.lineTo(lx + 14, -4); c.lineTo(lx + 16, 2); c.lineTo(lx - 14, 2);
            c.closePath(); c.fill(); c.stroke();
        }

        /* --- the body, off its own baked canvas --- */
        golemBake(e, K, P);
        var B = e._bake;
        c.save();
        c.rotate(-roll); c.scale(dir / s, 1 / s);
        c.drawImage(B.cv, -B.ox, -B.oy);
        c.restore();
        if (fl > 0)
        {
            /* a hit whitens the whole stack over the top of the bake */
            c.save(); c.globalAlpha = fl * 0.8; c.fillStyle = "#ffffff";
            foreach (var pts in new[] { P.hip, P.chest, P.back, P.shL, P.shR, P.head }) { polyPath(c, pts); c.fill(); }
            c.restore();
        }

        /* --- THE EYES. Two red coals under the brow, and the only light on it. --- */
        var eg = 0.62 + 0.38 * Math.sin(vtime * 4.5 + e.phase);
        foreach (var side in new[] { 0, 1 })
        {
            var ex2 = 2 + side * 11;
            c.fillStyle = rgbA(K.eyeGlow, 0.34 * eg);
            c.beginPath(); c.arc(ex2, P.browY + 8, 8, 0, TAU); c.fill();
            c.fillStyle = rgbA(K.eyeGlow, 0.78 * eg);
            c.beginPath(); c.ellipse(ex2, P.browY + 8, 4.2, 3.2, 0, 0, TAU); c.fill();
            c.fillStyle = K.eye;
            c.beginPath(); c.ellipse(ex2, P.browY + 8, 2.4, 2, 0, 0, TAU); c.fill();
        }

        /* molten or frozen light bleeding out of the seams between the chunks */
        if (K.vein != null)
        {
            var pulse = 0.5 + 0.5 * Math.sin(vtime * (e.golem == "fire" ? 6 : 2.5) + e.phase);
            c.strokeStyle = K.vein; c.lineWidth = 2.2; c.globalAlpha = pulse;
            c.beginPath();
            c.moveTo(-20, -30); c.lineTo(-9, -36); c.lineTo(-14, -46);
            c.moveTo(18, -28); c.lineTo(8, -33); c.lineTo(15, -44);
            c.moveTo(-6, -18); c.lineTo(4, -22);
            c.stroke();
            c.globalAlpha = 1;
        }

        /* --- the near arm, in front of the body --- */
        c.save(); c.translate(23, -60);
        golemArm(c, K, R, 1, armAngle(1), 36);
        c.restore();

        if (e.elite)
        {
            c.strokeStyle = rgba(255, 185, 0, 0.8); c.lineWidth = 2.5;
            c.beginPath(); c.ellipse(0, 1, 33, 10, 0, 0, TAU); c.stroke();
        }
        if (e.magic)
        {
            c.setLineDash(new double[] { 5, 4 }); c.strokeStyle = MAGIC_RING; c.lineWidth = 2.5;
            c.beginPath(); c.ellipse(0, 1, 34, 10.5, 0, 0, TAU); c.stroke(); c.setLineDash(new double[0]);
        }
        c.restore();
    }

    /* The chip that breaks off one when it dies: the same hunched ape, knee high.
       Two arms it walks on, a blunt head, two coals. */
    public static void drawMiniGolem(Ctx c, Ent e)
    {
        var K = GOLEM_SKIN.GetValueOrDefault(e.mini ?? "") ?? GOLEM_SKIN["stone"];
        var s = e.scale * 1.5;
        double mv = e.moving ? 1 : 0;
        var w = e.walk;
        var fl = e.flash > 0 ? Math.min(1, e.flash * 0.85) : 0;
        Col R(int i) => fl != 0 ? mixc(K.rock[i], "#ffffff", fl) : K.rock[i];
        var dir = (e.face != null && e.face.x < 0) ? -1 : 1;
        double h(double n) => hash2(Js.I32(e.phase * 613) + n * 7, n * 29);

        c.save();
        c.fillStyle = rgba(15, 30, 20, 0.3);
        c.beginPath(); c.ellipse(e.x, e.y + 2, 13 * s, 4.5 * s, 0, 0, TAU); c.fill();
        c.translate(e.x, e.y - Math.abs(Math.sin(w)) * 1.6 * mv * s);
        c.scale(dir * s, s);
        c.rotate(Math.sin(w) * 0.07 * mv);
        c.lineWidth = 1.6; c.lineJoin = "round"; c.strokeStyle = K.edge;

        var sw = Math.sin(w) * 2.4 * mv;
        /* far arm */
        c.fillStyle = mixc(R(2), "#000000", 0.18);
        c.beginPath(); c.moveTo(-10, -14); c.lineTo(-3, -15); c.lineTo(-1, -2 + sw * 0.3);
        c.lineTo(-8, -1 + sw * 0.3); c.closePath(); c.fill(); c.stroke();
        /* squat legs */
        foreach (var (lx, off) in new[] { (-5.0, sw), (5.0, -sw) })
        {
            c.fillStyle = K.dark;
            c.beginPath(); c.moveTo(lx - 4, -7); c.lineTo(lx + 4, -7);
            c.lineTo(lx + 5, 1); c.lineTo(lx - 5, 1); c.closePath(); c.fill(); c.stroke();
        }
        /* hunched body */
        c.fillStyle = R(0);
        c.beginPath();
        c.moveTo(-11, -8 - h(1) * 2); c.lineTo(-8, -19 - h(2) * 2); c.lineTo(3, -21);
        c.lineTo(11, -15); c.lineTo(10, -5); c.lineTo(-9, -4);
        c.closePath(); c.fill(); c.stroke();
        c.fillStyle = rgba(255, 255, 255, 0.10);
        c.beginPath(); c.moveTo(-8, -19); c.lineTo(3, -21); c.lineTo(11, -15); c.lineTo(0, -14);
        c.closePath(); c.fill();
        /* head slung forward, lower than the back */
        c.fillStyle = R(3);
        c.beginPath();
        c.moveTo(2, -16); c.lineTo(12, -17); c.lineTo(14, -8); c.lineTo(3, -7);
        c.closePath(); c.fill(); c.stroke();
        /* coals */
        var eg = 0.6 + 0.4 * Math.sin(vtime * 6 + e.phase);
        c.fillStyle = rgbA(K.eyeGlow, 0.34 * eg);
        c.beginPath(); c.arc(7, -13, 3.6, 0, TAU); c.arc(12, -13, 3.6, 0, TAU); c.fill();
        c.fillStyle = K.eye;
        c.beginPath(); c.arc(7, -13, 1.4, 0, TAU); c.arc(12, -13, 1.4, 0, TAU); c.fill();
        /* near arm, planted forward */
        c.fillStyle = R(2);
        c.beginPath(); c.moveTo(6, -14); c.lineTo(13, -15); c.lineTo(16, -1 - sw * 0.3);
        c.lineTo(9, 0 - sw * 0.3); c.closePath(); c.fill(); c.stroke();

        if (e.elite)
        {
            c.strokeStyle = rgba(255, 185, 0, 0.75); c.lineWidth = 2;
            c.beginPath(); c.ellipse(0, 1, 14, 5, 0, 0, TAU); c.stroke();
        }
        c.restore();
    }
}
