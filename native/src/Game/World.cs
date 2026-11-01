namespace Brickblade;

/* =============================== WORLD (JS 4554–4938) =============================== */
public sealed class WaterRect { public double x, y, w, h; public int g; public bool cave; }
public sealed class Flower { public double x, y; public string col; }
/* a prop: a tree, a rock, a chest, a pillar, a torch, a town building... */
public sealed partial class Prop : Obj
{
    public double flash, seed, hp; public bool indestructible;
    public double tall; public bool landmark; public double lx, ly, lr;
}

public static partial class G
{
    public static double hash2(double x, double y)
    {
        int h = Js.I32(x * 374761393.0 + y * 668265263.0);
        h = Js.Imul(h ^ (int)((uint)h >> 13), 1274126177);
        return (uint)(h ^ (int)((uint)h >> 16)) / 4294967295.0;
    }
    public static void drawStudG(Ctx g, double cx, double cy, double r, Col col)
    {
        r = Math.Max(0.5, r);
        g.fillStyle = sh(col, -0.3); g.beginPath(); g.arc(cx, cy + 2, r, 0, TAU); g.fill();
        g.fillStyle = sh(col, 0.08); g.beginPath(); g.arc(cx, cy, r, 0, TAU); g.fill();
        g.strokeStyle = sh(col, 0.3); g.lineWidth = Math.Max(1.4, r * 0.2);
        g.beginPath(); g.arc(cx, cy, Math.Max(0.4, r - 2.2), -2.6, -0.5); g.stroke();
        g.strokeStyle = "rgba(0,0,0,0.14)"; g.lineWidth = 1;
        g.beginPath(); g.arc(cx, cy, r, 0, TAU); g.stroke();
    }
    public static void drawStudPickup(Ctx c, double r, Col col)
    {
        double sq = 0.55, hh = r * 0.66;
        c.fillStyle = sh(col, -0.26);
        c.beginPath();
        c.moveTo(-r, 0);
        c.lineTo(-r, hh);
        c.ellipse(0, hh, r, r * sq, 0, Math.PI, 0, true);
        c.lineTo(r, 0);
        c.closePath(); c.fill();
        c.fillStyle = sh(col, 0.04);
        c.beginPath(); c.ellipse(0, 0, r, r * sq, 0, 0, TAU); c.fill();
        c.strokeStyle = "rgba(0,0,0,0.22)"; c.lineWidth = 1; c.stroke();
        c.strokeStyle = sh(col, 0.4); c.lineWidth = Math.Max(1.2, r * 0.16);
        c.beginPath(); c.ellipse(0, -r * sq * 0.12, r * 0.66, r * 0.66 * sq, 0, 0, TAU); c.stroke();
        c.fillStyle = "rgba(255,255,255,0.8)";
        c.beginPath(); c.ellipse(-r * 0.34, -r * sq * 0.3, r * 0.2, r * 0.11, -0.5, 0, TAU); c.fill();
    }
    public static void roundRect(Ctx g, double x, double y, double w, double h, double r)
    {
        w = Math.Max(1, w); h = Math.Max(1, h); r = Math.Min(r, Math.Min(w / 2, h / 2));
        g.beginPath(); g.moveTo(x + r, y);
        g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r);
        g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath();
    }
    /* ONE biome per run */
    public static readonly JsMap<string, string[]> BIOMES = new()
    {
        ["forest"] = new[] { "#4f9a37", "#55a43c", "#4a9133", "#59ab40" },
        ["desert"] = new[] { "#d3bc7e", "#ccb474", "#dcc689", "#c9b276" },
        ["swamp"] = new[] { "#3f7d3a", "#468540", "#386f33", "#4b8c43" },
        ["stone"] = new[] { "#8f948f", "#878c87", "#989d98", "#828782" },
        ["tundra"] = new[] { "#dfe8ee", "#d3dee6", "#e8f0f5", "#c9d6df" },
        ["ash"] = new[] { "#54494a", "#4c4243", "#5d5253", "#453c3d" },
        ["meadow"] = new[] { "#7db84a", "#86c152", "#74ae44", "#8fca5c" },
        ["cave"] = new[] { "#6e6a62", "#67635c", "#767268", "#5f5b55" },
    };
    public static readonly string[] BIOME_ORDER = { "forest", "desert", "stone", "swamp", "tundra", "ash", "meadow" };
    public static readonly JsMap<string, string> BIOME_NAMES = new()
    {
        ["forest"] = "THE GREENWOOD", ["desert"] = "THE DUST FLATS", ["stone"] = "THE GREY REACH", ["swamp"] = "THE MIRE",
        ["tundra"] = "THE WHITE WASTE", ["ash"] = "THE CINDER FIELDS", ["meadow"] = "THE LONG MEADOW",
    };
    public static readonly JsMap<string, (string t, int c)[]> BIOME_PROPS = new()
    {
        ["forest"] = new[] { ("tree", 26), ("crate", 6), ("crystal", 4) },
        ["desert"] = new[] { ("cactus", 20), ("rock", 12), ("crate", 5) },
        ["stone"] = new[] { ("rock", 22), ("crate", 10), ("crystal", 8) },
        ["swamp"] = new[] { ("tree", 20), ("crate", 8), ("crystal", 4) },
        ["tundra"] = new[] { ("rock", 14), ("tree", 10), ("crystal", 10) },
        ["ash"] = new[] { ("rock", 18), ("boulder", 6), ("crate", 6) },
        ["meadow"] = new[] { ("tree", 12), ("crate", 8), ("cactus", 4) },
    };
    public static string runBiome = "forest";
    public static string biomeAt(double x, double y) => inCave(x, y) ? "cave" : runBiome;
    static readonly JsMap<Col, OffCanvas> spriteCache = new();
    public static OffCanvas cellSprite(Col col)
    {
        if (spriteCache.TryGetValue(col, out var s)) return s;
        s = new OffCanvas { width = CS, height = CS };
        var g = s.getContext("2d");
        g.fillStyle = col; g.fillRect(0, 0, CS, CS);
        drawStudG(g, CS / 2.0, CS / 2.0, 11, col);
        spriteCache[col] = s;
        return s;
    }
    public static string cellColorKey(int ix, int iy)
    {
        double cxw = ix * CS + 20, cyw = iy * CS + 20;
        /* the cave floor is mottled stud by stud, and has no sand in the middle */
        if (caveMap != null && RPG != null && RPG.zone == "cave") { var pal0 = BIOMES["stone"]; return pal0[Js.I32(hash2(ix * 7 + 3, iy * 5 + 1) * pal0.Length)]; }
        if (hypot(cxw - CX, cyw - CY) < 260) return new[] { "#e4cd9e", "#dcc795", "#ecd4a7" }[Js.I32(hash2(ix, iy) * 3)];
        var pal = BIOMES[biomeAt(cxw, cyw)];
        return pal[Js.I32(hash2(ix >> 2, iy >> 2) * pal.Length)];
    }
    public static string biomeName() => BIOME_NAMES.GetValueOrDefault(runBiome) ?? "THE FIELD";
    public static void buildGround()
    {
        var rng = mulberry32(seed + 5);
        CAVES = new List<Cave>();
        for (int i = 0; i < 3 && CAVES.Count < 3; i++)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                double r = 360 + rng() * 220;
                double cx2 = r + 220 + rng() * (W.w - 2 * (r + 220)), cy2 = r + 220 + rng() * (W.h - 2 * (r + 220));
                if (hypot(cx2 - CX, cy2 - CY) < 900 + r) continue;
                if (CAVES.Any(c => hypot(c.x - cx2, c.y - cy2) < c.r + r + 220)) continue;
                CAVES.Add(new Cave { x = cx2, y = cy2, r = r });
                break;
            }
        }
        runBiome = BIOME_ORDER[Js.I32(rng() * BIOME_ORDER.Length)];
        /* Scattered lakes, laid on the stud grid in whole cells */
        waters = new List<WaterRect>(); lakeGroups = new List<List<WaterRect>>();
        double lakeTarget = Math.Max(2, Math.Round(W.w * W.h / 800000 * (0.6 + rng() * 0.8)));
        int cellsX = (int)Math.Floor(W.w / CS), cellsY = (int)Math.Floor(W.h / CS);
        for (int tries = 0; tries < 400 && lakeGroups.Count < lakeTarget; tries++)
        {
            int cw2 = 5 + Js.I32(rng() * 7), ch2 = 4 + Js.I32(rng() * 5);
            int gx = 5 + Js.I32(rng() * (cellsX - 10 - cw2)), gy = 5 + Js.I32(rng() * (cellsY - 10 - ch2));
            var cells = new List<int[]> { new[] { gx, gy, cw2, ch2 } };
            if (rng() < 0.45)
            {
                int lw = 3 + Js.I32(rng() * 4), lh = 3 + Js.I32(rng() * 4), side = Js.I32(rng() * 4);
                if (side == 0) cells.Add(new[] { gx + cw2 - 2, gy + (Js.I32(rng() * (ch2 + lh - 3)) - (lh - 2)), lw + 2, lh });
                else if (side == 1) cells.Add(new[] { gx - lw, gy + (Js.I32(rng() * (ch2 + lh - 3)) - (lh - 2)), lw + 2, lh });
                else if (side == 2) cells.Add(new[] { gx + (Js.I32(rng() * (cw2 + lw - 3)) - (lw - 2)), gy + ch2 - 2, lw, lh + 2 });
                else cells.Add(new[] { gx + (Js.I32(rng() * (cw2 + lw - 3)) - (lw - 2)), gy - lh, lw, lh + 2 });
            }
            var parts = cells.Select(c => new WaterRect { x = c[0] * CS, y = c[1] * CS, w = c[2] * CS, h = c[3] * CS }).ToList();
            if (parts.Any(q => q.x < 180 || q.y < 180 || q.x + q.w > W.w - 180 || q.y + q.h > W.h - 180)) continue;
            if (parts.Any(q => hypot(q.x + q.w / 2 - CX, q.y + q.h / 2 - CY) < 700 || inCave(q.x + q.w / 2, q.y + q.h / 2))) continue;
            if (parts.Any(q => waters.Any(o => q.x < o.x + o.w + 150 && q.x + q.w > o.x - 150 && q.y < o.y + o.h + 150 && q.y + q.h > o.y - 150))) continue;
            int g = lakeGroups.Count;
            foreach (var q in parts) { q.g = g; waters.Add(q); }
            lakeGroups.Add(parts);
        }
        flowers = new List<Flower>();
        double flowerTarget = Math.Round(W.w * W.h / 62000);
        for (int i = 0; i < flowerTarget * 3 && flowers.Count < flowerTarget; i++)
        {
            double fx = 130 + rng() * (W.w - 260), fy = 130 + rng() * (W.h - 260);
            string fc = rng() < 0.5 ? PAL.red : PAL.gold;
            if (Math.Abs(fx - CX) < 300 || Math.Abs(fy - CY) < 100) continue;
            if (biomeAt(fx, fy) == "cave") continue;
            bool bad = false;
            foreach (var w in waters) if (fx > w.x - 20 && fx < w.x + w.w + 20 && fy > w.y - 20 && fy < w.y + w.h + 20) bad = true;
            if (bad) continue;
            flowers.Add(new Flower { x = fx, y = fy, col = fc });
        }
    }
    public static void wallBrick(double bx, double by, double w, double h)
    {
        bool accent = hash2(bx, by) < 0.1;
        Col bse = accent ? "#a83228" : sh("#7d7f7a", (hash2(bx + 3, by + 9) - 0.5) * 0.18);
        ctx.fillStyle = bse; ctx.fillRect(bx, by, w, h);
        ctx.strokeStyle = "rgba(0,0,0,0.25)"; ctx.lineWidth = 2; ctx.strokeRect(bx + 1, by + 1, w - 2, h - 2);
        drawStudG(ctx, bx + w * 0.3, by + h / 2, 7.5, bse);
        drawStudG(ctx, bx + w * 0.7, by + h / 2, 7.5, bse);
    }
    /* A LAKE, BUILT: sand shelf, a rim of plates, water plates with studs */
    public static bool lakeInside(List<WaterRect> parts, double x, double y, WaterRect self)
    {
        foreach (var q in parts) if (q != self && x > q.x && x < q.x + q.w && y > q.y && y < q.y + q.h) return true;
        return false;
    }
    public static void drawLake(List<WaterRect> parts)
    {
        const string rimC = "#d8c79b";
        ctx.fillStyle = "#bda877";
        foreach (var w in parts) { roundRect(ctx, w.x - 18, w.y - 16, w.w + 36, w.h + 36, 22); ctx.fill(); }
        ctx.fillStyle = "#cbb98a";
        foreach (var w in parts) { roundRect(ctx, w.x - 18, w.y - 18, w.w + 36, w.h + 36, 22); ctx.fill(); }
        void plate(double x, double y, double w2, double h2, (double, double)[] studs, double key)
        {
            var col = sh(rimC, (hash2(Js.I32(key), Js.I32(x + y)) - 0.5) * 0.16);
            ctx.fillStyle = sh(col, -0.22); roundRect(ctx, x, y + 1.5, w2, h2, 2); ctx.fill();
            ctx.fillStyle = col; roundRect(ctx, x, y, w2, h2, 2); ctx.fill();
            foreach (var (sx, sy) in studs) drawStudG(ctx, sx, sy, 3, rimC);
        }
        foreach (var w in parts)
        {
            double nx = w.w / CS, ny = w.h / CS;
            for (int k = 0; k < nx; k++)
            {
                double x0 = w.x + k * CS;
                if (!lakeInside(parts, x0 + CS / 2.0, w.y - 8, w))
                    plate(x0 + 2, w.y - 14, CS - 4, 11, new[] { (x0 + CS * 0.27, w.y - 8.5), (x0 + CS * 0.73, w.y - 8.5) }, 1);
                if (!lakeInside(parts, x0 + CS / 2.0, w.y + w.h + 8, w))
                    plate(x0 + 2, w.y + w.h + 3, CS - 4, 11, new[] { (x0 + CS * 0.27, w.y + w.h + 8.5), (x0 + CS * 0.73, w.y + w.h + 8.5) }, 2);
            }
            for (int k = 0; k < ny; k++)
            {
                double y0 = w.y + k * CS;
                if (!lakeInside(parts, w.x - 8, y0 + CS / 2.0, w))
                    plate(w.x - 14, y0 + 2, 11, CS - 4, new[] { (w.x - 8.5, y0 + CS * 0.27), (w.x - 8.5, y0 + CS * 0.73) }, 3);
                if (!lakeInside(parts, w.x + w.w + 8, y0 + CS / 2.0, w))
                    plate(w.x + w.w + 3, y0 + 2, 11, CS - 4, new[] { (w.x + w.w + 8.5, y0 + CS * 0.27), (w.x + w.w + 8.5, y0 + CS * 0.73) }, 4);
            }
            foreach (var (cx2, cy2) in new[] { (w.x - 8.5, w.y - 8.5), (w.x + w.w + 8.5, w.y - 8.5), (w.x - 8.5, w.y + w.h + 8.5), (w.x + w.w + 8.5, w.y + w.h + 8.5) })
                if (!lakeInside(parts, cx2, cy2, w) && !lakeInside(parts, cx2, cy2, null))
                    plate(cx2 - 5.5, cy2 - 5.5, 11, 11, new[] { (cx2, cy2) }, 5);
        }
        ctx.fillStyle = "#175c99";
        foreach (var w in parts) { roundRect(ctx, w.x, w.y, w.w, w.h, 14); ctx.fill(); }
        ctx.fillStyle = "#1f74bd";
        foreach (var w in parts) { roundRect(ctx, w.x + 3, w.y + 3, w.w - 6, w.h - 6, 12); ctx.fill(); }
        var seen = new HashSet<long>();
        foreach (var w in parts)
            for (double ix = w.x / CS; ix < (w.x + w.w) / CS; ix++)
                for (double iy = w.y / CS; iy < (w.y + w.h) / CS; iy++)
                {
                    long key = (long)ix * 4096 + (long)iy;
                    if (!seen.Add(key)) continue;
                    drawStudG(ctx, ix * CS + CS / 2.0, iy * CS + CS / 2.0, 10, "#2a80c8");
                }
        var w0 = parts[0];
        ctx.fillStyle = "rgba(255,255,255,0.07)";
        roundRect(ctx, w0.x + 8, w0.y + 6, w0.w - 16, Math.Min(w0.h * 0.32, CS * 2), 10); ctx.fill();
    }
    public static void drawGround(double vx, double vy, double vw, double vh)
    {
        ctx.fillStyle = (BIOMES.GetValueOrDefault(runBiome) ?? BIOMES["forest"])[0];
        ctx.fillRect(vx, vy, vw, vh);
        int ix0 = (int)Math.Max(0, Math.Floor(vx / CS)), ix1 = (int)Math.Min(Math.Ceiling(W.w / CS) - 1, Math.Ceiling((vx + vw) / CS));
        int iy0 = (int)Math.Max(0, Math.Floor(vy / CS)), iy1 = (int)Math.Min(Math.Ceiling(W.h / CS) - 1, Math.Ceiling((vy + vh) / CS));
        for (int iy = iy0; iy <= iy1; iy++) for (int ix = ix0; ix <= ix1; ix++)
                ctx.drawImage(cellSprite(cellColorKey(ix, iy)), ix * CS, iy * CS, CS + 1, CS + 1);
        foreach (var parts in lakeGroups)
        {
            if (!waters.Contains(parts[0])) continue;
            if (!parts.Any(w => !(w.x - 30 > vx + vw || w.x + w.w + 30 < vx || w.y - 30 > vy + vh || w.y + w.h + 30 < vy))) continue;
            drawLake(parts);
        }
        foreach (var f in flowers)
        {
            if (f.x < vx - 20 || f.x > vx + vw + 20 || f.y < vy - 20 || f.y > vy + vh + 20) continue;
            drawStudG(ctx, f.x, f.y, 5, f.col);
        }
        ctx.strokeStyle = "rgba(20,35,25,0.35)"; ctx.lineWidth = 26;
        ctx.strokeRect(W.wall + 8, W.wall + 8, W.w - 2 * (W.wall + 8), W.h - 2 * (W.wall + 8));
        double T = W.wall;
        ctx.fillStyle = "#6d6e68";
        if (vy < T) ctx.fillRect(vx, 0, vw, T);
        if (vy + vh > W.h - T) ctx.fillRect(vx, W.h - T, vw, T);
        if (vx < T) ctx.fillRect(0, vy, T, vh);
        if (vx + vw > W.w - T) ctx.fillRect(W.w - T, vy, T, vh);
        if (vy < T || vy + vh > W.h - T)
        {
            bool yTop = vy < T, yBot = vy + vh > W.h - T;
            for (double bx = Math.Floor(vx / 70) * 70 - 70; bx < vx + vw + 70; bx += 70)
            {
                if (yTop) { wallBrick(bx + 2, 4, 66, 28); wallBrick(bx + 37, 36, 66, 28); }
                if (yBot) { wallBrick(bx + 2, W.h - 64, 66, 28); wallBrick(bx + 37, W.h - 32, 66, 28); }
            }
        }
        if (vx < T || vx + vw > W.w - T)
        {
            bool xL = vx < T, xR = vx + vw > W.w - T;
            for (double by = Math.Floor(vy / 70) * 70 - 70; by < vy + vh + 70; by += 70)
            {
                if (xL) { wallBrick(4, by + 2, 28, 66); wallBrick(36, by + 37, 28, 66); }
                if (xR) { wallBrick(W.w - 64, by + 2, 28, 66); wallBrick(W.w - 32, by + 37, 28, 66); }
            }
        }
    }
    public static void buildProps()
    {
        props = new List<Prop>(); solids = new List<Prop>();
        var rng = mulberry32(seed + 11);
        var torchPos = new[] { (CX - 320, CY - 200), (CX + 320, CY - 200), (CX - 320, CY + 200), (CX + 320, CY + 200) };
        foreach (var (tx, ty) in torchPos) addProp("torch", tx, ty, rng);
        double dens = (W.w * W.h) / (3600 * 2700);
        buildLandmarks(rng, dens);
        var quotas = (BIOME_PROPS.GetValueOrDefault(runBiome) ?? BIOME_PROPS["forest"]).Select(q => (runBiome, q.t, q.c))
            .Concat(new[] { ("cave", "boulder", 9), ("cave", "crystal", 14) })
            .Select(q => (q.Item1, q.Item2, Math.Round(q.Item3 * dens))).ToList();
        int tries = 0;
        double tryCap = Math.Round(3200 * dens);
        foreach (var (biome, type, count) in quotas)
        {
            int placed = 0;
            while (placed < count && tries < tryCap)
            {
                tries++;
                double x = 170 + rng() * (W.w - 340), y = 170 + rng() * (W.h - 340);
                if (biomeAt(x, y) != biome) continue;
                if (hypot(x - CX, y - CY) < 420) continue;
                bool bad = false;
                foreach (var w in waters) if (x > w.x - 75 && x < w.x + w.w + 75 && y > w.y - 75 && y < w.y + w.h + 75) bad = true;
                foreach (var p in props) if (hypot(p.x - x, p.y - y) < (p.landmark ? 120 : 95)) bad = true;
                if (bad) continue;
                addProp(type, x, y, rng); placed++;
            }
        }
    }
    /* LANDMARKS: a ring of pillars, a colonnade, an outcrop of boulders */
    public static readonly string[] LANDMARK_KINDS = { "ring", "colonnade", "outcrop" };
    public static bool landmarkRoom(double x, double y, double R)
    {
        if (x < 260 + R || y < 260 + R || x > W.w - 260 - R || y > W.h - 260 - R) return false;
        if (hypot(x - CX, y - CY) < 620 + R) return false;
        foreach (var w in waters) if (x > w.x - R - 120 && x < w.x + w.w + R + 120 && y > w.y - R - 120 && y < w.y + w.h + R + 120) return false;
        foreach (var p in props) if (p.landmark && hypot(p.lx - x, p.ly - y) < R + p.lr + 260) return false;
        return true;
    }
    public static void buildLandmarks(Func<double> rng, double dens)
    {
        double n = Math.Max(2, Math.Round((2 + rng() * 3) * dens));
        int made = 0;
        for (int tries = 0; tries < 120 && made < n; tries++)
        {
            string kind = LANDMARK_KINDS[Js.I32(rng() * LANDMARK_KINDS.Length)];
            double R = kind == "ring" ? 150 + rng() * 70 : kind == "colonnade" ? 190 + rng() * 90 : 90;
            double x = 300 + rng() * (W.w - 600), y = 300 + rng() * (W.h - 600);
            if (!landmarkRoom(x, y, R)) continue;
            Prop put(string type, double px, double py) { var q = addProp(type, px, py, rng); q.landmark = true; q.lx = x; q.ly = y; q.lr = R; return q; }
            if (kind == "ring")
            {
                int k = 8 + Js.I32(rng() * 4), gap1 = Js.I32(rng() * k), gap2 = rng() < 0.55 ? (gap1 + k / 2) % k : -1;
                double a0 = rng() * TAU;
                for (int i = 0; i < k; i++)
                {
                    if (i == gap1 || i == gap2) continue;
                    double a = a0 + (double)i / k * TAU;
                    put("pillar", x + Math.Cos(a) * R, y + Math.Sin(a) * R);
                }
            }
            else if (kind == "colonnade")
            {
                double a = rng() * Math.PI; int k = (int)Math.Max(4, Math.Round(R * 2 / 62));
                for (int i = 0; i < k; i++)
                {
                    double t = ((double)i / (k - 1) - 0.5) * 2 * R;
                    put("pillar", x + Math.Cos(a) * t, y + Math.Sin(a) * t);
                }
            }
            else
            {
                int k = 3 + Js.I32(rng() * 3);
                for (int i = 0; i < k; i++)
                {
                    double a = rng() * TAU, d = i != 0 ? 30 + rng() * 45 : 0;
                    put("boulder", x + Math.Cos(a) * d, y + Math.Sin(a) * d);
                }
            }
            made++;
        }
    }
    public static Prop addProp(string type, double x, double y, Func<double> rng)
    {
        var p = new Prop
        {
            type = type, x = x, y = y, flash = 0, seed = rng() * 1000, dead = false,
            hp = type == "tree" ? 4 : type == "rock" ? 6 : type == "cactus" ? 3 : 2,
            r = type == "tree" ? 14 : type == "rock" ? (rng() < 0.5 ? 21 : 14) : type == "crate" ? 15
              : type == "cactus" ? 11 : type == "boulder" ? (28 + rng() * 12) : type == "crystal" ? 12
              : type == "pillar" ? (20 + rng() * 4) : 6,
            indestructible = type == "boulder" || type == "crystal" || type == "pillar"
        };
        if (type == "pillar") p.tall = rng() < 0.28 ? 0.45 + rng() * 0.25 : 1;
        props.Add(p);
        if (type != "torch" && type != "crystal") solids.Add(p);
        return p;
    }
    public static void breakProp(Prop p)
    {
        if (p.indestructible) return;
        if (p.type == "chest" && p.mimic) { mimicReveal(p); return; }
        if (p.type == "chest")
        {
            p.dead = true;
            if (p.wild) p.rar = rollRarity("wild");
            string CC = (RARITY.GetValueOrDefault(p.rar ?? "") ?? RARITY["common"]).col;
            spawnBricks(p.x, p.y, new Col[] { "#9c6a45", CC, sh(CC, 0.35) }, 10, 200);
            addRing(p.x, p.y, 8, 300, 90, CC, 6, 0.5);
            addSparks(p.x, p.y - 10, 12, sh(CC, 0.4));
            SFX.fortune();
            var CR = RARITY.GetValueOrDefault(p.rar ?? "") ?? RARITY["common"];
            showBanner(p.wild ? "MYSTERY CHEST!" : "TREASURE!", CR.name + " BRICK INSIDE", CR.col, 1.6, 40);
            if (p.wild) { addRing(p.x, p.y, 10, 420, 150, CR.col, 8, 0.6); SFX.levelup(); }
            queuePick("chest", p.rar);
            return;
        }
        p.dead = true; solids = solids.Where(s => s != p).ToList();
        if (p.r >= NAV_SMALL) navRefresh(p.x, p.y, p.r);
        Col[] cols = p.type == "tree" ? new Col[] { PAL.brown, "#3d7a2b", "#4b9b41", "#57a33c" }
            : p.type == "rock" ? new Col[] { "#8b8f8a", "#6d6e68", "#9ba19d" }
            : p.type == "cactus" ? new Col[] { "#3d7a2b", "#4b9b41", "#57a33c" }
            : new Col[] { "#7b5132", "#915f3d", "#583927" };
        spawnBricks(p.x, p.y, cols, p.type == "rock" ? 12 : 10, 190);
        sfxAt(p.x).brick(6); addShake(0.18);
        if (p.type == "crate")
        {
            spawnStuds(p.x, p.y, irnd(4, 9));
            if (Js.R() < 0.35) pickups.Add(makePickup("heart", p.x + rnd(-14, 14), p.y + rnd(-14, 14)));
        }
        else if (Js.R() < 0.4) spawnStuds(p.x, p.y, irnd(1, 3));
        for (int i = 0; i < 3; i++) if (Js.R() < 0.5) pickups.Add(makePickup("stud1", p.x + rnd(-20, 20), p.y + rnd(-20, 20)));
    }
}
