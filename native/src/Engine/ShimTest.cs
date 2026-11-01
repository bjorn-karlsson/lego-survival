using Raylib_cs;

namespace Brickblade;

/* Draws one fixed scene through the canvas shim and saves it, so it can be laid
   beside the same calls drawn by a browser (tools/shimtest.html). */
public static class ShimTest
{
    public static void Scene(Ctx c, double W, double H)
    {
        const double TAU = Math.PI * 2;
        c.fillStyle = "#10151a"; c.fillRect(0, 0, W, H);
        /* circles, alpha, outlines */
        for (int i = 0; i < 6; i++)
        {
            c.fillStyle = new Col(40 * i, 200 - 20 * i, 255 - 30 * i, 0.4 + i * 0.1);
            c.beginPath(); c.arc(60 + i * 70, 60, 26, 0, TAU); c.fill();
            c.strokeStyle = "rgba(20,30,40,0.55)"; c.lineWidth = 3; c.stroke();
        }
        /* rounded rect with arcTo, gradient */
        var g = c.createLinearGradient(0, 120, 0, 220);
        g.addColorStop(0, "#ffd76e"); g.addColorStop(1, "#d3242b");
        c.fillStyle = g;
        double x = 30, y = 120, w = 180, h = 100, r = 18;
        c.beginPath(); c.moveTo(x + r, y); c.arcTo(x + w, y, x + w, y + h, r); c.arcTo(x + w, y + h, x, y + h, r);
        c.arcTo(x, y + h, x, y, r); c.arcTo(x, y, x + w, y, r); c.closePath(); c.fill();
        c.strokeStyle = "#1b2a34"; c.lineWidth = 2.5; c.stroke();
        /* radial gradient */
        var rg = c.createRadialGradient(330, 170, 4, 330, 170, 60);
        rg.addColorStop(0, "rgba(255,240,190,1)"); rg.addColorStop(1, "rgba(255,215,110,0)");
        c.fillStyle = rg; c.fillRect(260, 100, 140, 140);
        /* transform: rotated rectangles, ellipse */
        c.save(); c.translate(500, 170); c.rotate(0.4);
        c.fillStyle = "#5a5266"; c.fillRect(-40, -20, 80, 40);
        c.strokeStyle = "#ffffff"; c.lineWidth = 2; c.strokeRect(-40, -20, 80, 40);
        c.beginPath(); c.ellipse(0, 50, 50, 18, 0, 0, TAU); c.fillStyle = "rgba(100,200,120,0.7)"; c.fill();
        c.restore();
        /* dashed circle and round-capped polyline */
        c.setLineDash(new double[] { 10, 8 }); c.strokeStyle = "rgba(255,233,90,0.9)"; c.lineWidth = 3;
        c.beginPath(); c.arc(80, 300, 50, 0, TAU); c.stroke(); c.setLineDash(Array.Empty<double>());
        c.lineCap = "round"; c.lineJoin = "round"; c.lineWidth = 8; c.strokeStyle = "rgba(143,212,255,0.8)";
        c.beginPath(); c.moveTo(170, 260); c.lineTo(230, 340); c.lineTo(290, 270); c.lineTo(350, 330); c.stroke();
        c.lineCap = "butt"; c.lineJoin = "miter";
        /* concave star, evenodd ring */
        c.beginPath();
        for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? 45 : 18; if (i == 0) c.moveTo(450 + Math.Cos(a) * rr, 300 + Math.Sin(a) * rr); else c.lineTo(450 + Math.Cos(a) * rr, 300 + Math.Sin(a) * rr); }
        c.closePath(); c.fillStyle = "#fe8a18"; c.fill(); c.strokeStyle = "rgba(15,20,25,0.7)"; c.lineWidth = 2; c.stroke();
        c.beginPath(); c.arc(560, 300, 45, 0, TAU); c.arc(560, 300, 25, 0, TAU, true); c.fillStyle = "#a45bd6"; c.fill();
        /* quadratic and bezier */
        c.beginPath(); c.moveTo(20, 380); c.quadraticCurveTo(120, 330, 220, 400); c.bezierCurveTo(260, 440, 320, 350, 380, 400);
        c.strokeStyle = "#7fe07a"; c.lineWidth = 4; c.stroke();
        /* text */
        c.textAlign = "left"; c.textBaseline = "alphabetic";
        c.font = "700 28px \"Titan One\""; c.strokeStyle = "rgba(15,20,25,0.8)"; c.lineWidth = 5;
        c.strokeText("BRICKBLADE", 400, 400); c.fillStyle = "#ffd76e"; c.fillText("BRICKBLADE", 400, 400);
        c.font = "600 14px Fredoka"; c.fillStyle = "#c9d2ce"; c.textAlign = "center"; c.textBaseline = "middle";
        c.fillText("monster level 3 · 46% open — the Warden ×2 → ✓ ★ 🔥", 320, 440);
        c.textAlign = "right"; c.textBaseline = "top"; c.font = "700 11px Fredoka";
        c.fillText("next skill point at level 12", 620, 455);
        /* clip */
        c.save(); c.beginPath(); c.arc(560, 400, 30, 0, TAU); c.clip();
        c.fillStyle = "#3ddc84"; c.fillRect(520, 360, 80, 40); c.fillStyle = "#1f7a4d"; c.fillRect(520, 400, 80, 40);
        c.restore();
        /* lighter blend */
        c.globalCompositeOperation = "lighter";
        c.fillStyle = "rgba(255,80,60,0.5)"; c.beginPath(); c.arc(600, 60, 30, 0, TAU); c.fill();
        c.fillStyle = "rgba(60,80,255,0.5)"; c.beginPath(); c.arc(620, 75, 30, 0, TAU); c.fill();
        c.globalCompositeOperation = "source-over";
    }

    public static int Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.SetTraceLogLevel(TraceLogLevel.Error);
        Raylib.InitWindow(660, 480, "shimtest");
        Gpu.Init(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
        Fonts.Init();
        var c = new Ctx(null);
        /* an offscreen canvas too: draw the scene small into it, then place it */
        var off = new OffCanvas { width = 160, height = 120 };
        for (int f = 0; f < 3; f++)
        {
            Raylib.BeginDrawing();
            Gpu.ClearScreen(new Col(0, 0, 0, 1));
            c.Reset();
            Scene(c, 660, 480);
            var oc = off.getContext("2d"); oc.Reset();
            Gpu.Clear(off.T);
            oc.scale(0.25, 0.25); Scene(oc, 660, 480);
            c.globalAlpha = 0.9; c.drawImage(off, 480, 110); c.globalAlpha = 1;
            Gpu.EndFrame(); MaskPool.EndFrame();
            if (f == 2) Shot.Save(Program.Opt("--out") ?? "shimtest.png");
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
        return 0;
    }
}
