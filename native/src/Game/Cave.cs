// Port of lego-survival.html lines 17478-17929: THE TEST CAVE (not canon) -- a
// cellular-automata cave, its walls as collision rectangles, its rock painted
// into cached tiles, and its air: darkness, glows, spores and bats.
namespace Brickblade;

public sealed class CaveMapT
{
    public int cols, rows; public byte[] g; public double seed, fill, open; public int tries, regions;
    public int from, to, walk; public int[] dist;
}
public sealed class CaveLoop { public float[] xs, ys, nx, ny; public int n; public double[] box; }
public sealed class CaveStud { public double x, y, r; }
public sealed class CaveLump { public double x, y, r, a; public Col c; public bool stud; }
public sealed class CaveDeco { public double x, y, r, a, rx, ry, ph, per, s; public string col; public Col c; public bool top, skull; public int n; public List<(double dx, double dy, double s)> caps; public string cs; }
public sealed class CaveLight { public double x, y, r, a, ph; public string col; }
public sealed class CaveBat { public double x, y, vx, vy, ph, life; }
public sealed class CaveTileT { public OffCanvas c; public int used; }
public sealed class CaveArtT
{
    public CaveMapT M; public List<CaveLoop> loops; public Path2D floor, rock; public JsMap<string, CaveTileT> tiles = new();
    public double k = 1; public int frame;
    public List<CaveStud> studs = new(); public List<CaveLump> lumps = new();
    public List<CaveDeco> cracks = new(), moss = new(), pebbles = new(), shrooms = new(), shards = new(), puddles = new(), patches = new(), bones = new();
    public List<CaveLight> lights = new(); public List<CaveBat> bats = new(); public double batT;
}

public static partial class G
{
    /* ======================= THE TEST CAVE (not canon) =======================
       A playground for generated instances, reached from a stone in Brickhaven.
       The cave is CELLULAR AUTOMATA, the RogueBasin recipe:
         1. every cell of a coarse grid is wall with chance CAVE_FILL, the rim always;
         2. CAVE_STEPS smoothing passes: a cell becomes WALL when 5 or more of its 8
            neighbours are wall and FLOOR when fewer than 4 are; on the first
            CAVE_OPEN passes a cell with at most 2 walls within two cells also turns
            wall, which breaks up the big empty halls the plain rule leaves;
         3. a flood fill keeps the LARGEST connected cave and fills every other
            pocket in, so everything you can see you can walk to -- and a cave too
            small (under CAVE_MIN of the map) is thrown away and rolled again.
       You come in at the westmost roomy cell and the far end is the floor cell
       farthest from it by WALKING distance (a breadth-first search over the same
       grid), where a rare waits by a second portal. The walls are rectangles in
       `waters` -- one per horizontal run of wall cells -- so the collision, the
       nav grid, the spawners and every line-of-sight test already know them;
       only their drawing is their own, and they stop every missile. */
    public const double CAVE_CELL = 80, CAVE_FILL = 0.47, CAVE_MIN = 0.28;
    public const int CAVE_STEPS = 5, CAVE_OPEN = 3, CAVE_PACKS = 14;
    public static CaveMapT caveMap = null;
    static readonly (int dx, int dy)[] DIR4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    public static CaveMapT caveGenerate(double seed0)
    {
        int cols = (int)Math.Floor(W.w / CAVE_CELL), rows = (int)Math.Floor(W.h / CAVE_CELL);
        uint seedv = (uint)Js.I32(seed0); CaveMapT best = null;
        for (int tries = 1; tries <= 12; tries++, seedv = unchecked(seedv * 1103515245u + 12345u))
        {
            var rng = mulberry32(seedv);
            var g = new byte[cols * rows];
            bool rim(int x, int y) => x < 1 || y < 1 || x >= cols - 1 || y >= rows - 1;
            for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++) g[y * cols + x] = (byte)(rim(x, y) || rng() < CAVE_FILL ? 1 : 0);
            byte at(byte[] gg, int x, int y) => (x < 0 || y < 0 || x >= cols || y >= rows) ? (byte)1 : gg[y * cols + x];
            for (int step = 0; step < CAVE_STEPS; step++)
            {
                var n = new byte[cols * rows];
                for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++)
                    {
                        if (rim(x, y)) { n[y * cols + x] = 1; continue; }
                        int w1 = 0, w2 = 0;
                        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int v0 = at(g, x + dx, y + dy);
                                if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) w1 += v0; else w2 += v0;
                            }
                        int cur = g[y * cols + x];
                        int v = w1 >= 5 ? 1 : w1 < 4 ? 0 : cur;
                        if (step < CAVE_OPEN && w1 + w2 <= 2) v = 1;
                        n[y * cols + x] = (byte)v;
                    }
                g = n;
            }
            /* the regions, 4-connected, and the largest kept */
            var reg = new int[cols * rows]; Array.Fill(reg, -1); var sizes = new List<int>();
            for (int i = 0; i < g.Length; i++)
            {
                if (g[i] != 0 || reg[i] >= 0) continue;
                int id = sizes.Count, cnt = 0; var q = new List<int> { i }; reg[i] = id;
                while (q.Count > 0)
                {
                    int c = q[^1]; q.RemoveAt(q.Count - 1); cnt++; int x = c % cols, y = c / cols;
                    foreach (var (dx, dy) in DIR4)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= cols || yy >= rows) continue; int j = yy * cols + xx;
                        if (g[j] == 0 && reg[j] < 0) { reg[j] = id; q.Add(j); }
                    }
                }
                sizes.Add(cnt);
            }
            if (sizes.Count == 0) continue;
            int big = sizes.IndexOf(sizes.Max()); double open = (double)sizes[big] / (cols * rows);
            for (int i = 0; i < g.Length; i++) if (g[i] == 0 && reg[i] != big) g[i] = 1;
            var cand = new CaveMapT { cols = cols, rows = rows, g = g, seed = seedv, fill = CAVE_FILL, open = open, tries = tries, regions = sizes.Count };
            if (best == null || open > best.open) best = cand;
            if (open >= CAVE_MIN) break;
        }
        var M = best; int C = M.cols, R = M.rows; var G2 = M.g;
        /* a ROOMY cell: floor with floor all round it */
        bool roomy(int i)
        {
            int x = i % C, y = i / C; if (G2[i] != 0) return false;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= C || yy >= R || G2[yy * C + xx] != 0) return false;
                }
            return true;
        }
        int from = -1;
        for (int x = 1; x < C - 1 && from < 0; x++) for (int y = 1; y < R - 1; y++) if (roomy(y * C + x)) { from = y * C + x; break; }
        if (from < 0) for (int i = 0; i < G2.Length; i++) if (G2[i] == 0) { from = i; break; }
        /* walking distance from the way in, over the grid */
        var d = new int[C * R]; Array.Fill(d, -1); d[from] = 0; var qq = new List<int> { from };
        for (int h = 0; h < qq.Count; h++)
        {
            int c = qq[h], x = c % C, y = c / C;
            foreach (var (dx, dy) in DIR4)
            {
                int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= C || yy >= R) continue;
                int j = yy * C + xx; if (G2[j] == 0 && d[j] < 0) { d[j] = d[c] + 1; qq.Add(j); }
            }
        }
        int to = from;
        for (int i = 0; i < G2.Length; i++) if (d[i] > d[to] && roomy(i)) to = i;
        M.from = from; M.to = to; M.dist = d; M.walk = d[to];
        return M;
    }
    public static Pt caveXY(CaveMapT M, int i) => new Pt { x = (i % M.cols) * CAVE_CELL + CAVE_CELL / 2, y = (i / M.cols) * CAVE_CELL + CAVE_CELL / 2 };
    public static bool caveWallAt(double x, double y)
    {
        var M = caveMap; if (M == null) return false;
        int cx = (int)Math.Floor(x / CAVE_CELL), cy = (int)Math.Floor(y / CAVE_CELL);
        if (cx < 0 || cy < 0 || cx >= M.cols || cy >= M.rows) return true;
        return M.g[cy * M.cols + cx] != 0;
    }
    public static void rpgBuildCave()
    {
        var M = caveMap = caveGenerate(seed);
        runBiome = "stone"; CAVES = new(); lakeGroups = new(); flowers = new(); props = new(); solids = new(); waters = new();
        /* the walls: one rectangle a horizontal run */
        for (int y = 0; y < M.rows; y++)
        {
            int x = 0;
            while (x < M.cols)
            {
                if (M.g[y * M.cols + x] == 0) { x++; continue; }
                int x0 = x; while (x < M.cols && M.g[y * M.cols + x] != 0) x++;
                waters.Add(new WaterRect { x = x0 * CAVE_CELL, y = y * CAVE_CELL, w = (x - x0) * CAVE_CELL, h = CAVE_CELL, cave = true });
            }
        }
        var A = caveXY(M, M.from); var B = caveXY(M, M.to);
        RPG.caveIn = A; RPG.caveOut = B;
        rpgProp("rportal", A.x, A.y - 50, 30, q => { q.solid = false; q.label = "TO BRICKHAVEN"; });
        rpgProp("rportal", B.x, B.y - 50, 30, q => { q.solid = false; q.label = "TO BRICKHAVEN"; });
        /* crystals for light, on floor away from the walls */
        for (int i = 0, k = 0; i < M.g.Length && k < 40; i += 37)
        {
            int c = (int)((long)i * 7919 % M.g.Length);
            if (M.g[c] != 0 || Js.R() < 0.6) continue;
            var p = caveXY(M, c);
            if (hypot(p.x - A.x, p.y - A.y) < 260) continue;
            rpgProp("crystal", p.x + rnd(-20, 20), p.y + rnd(-20, 20), 12, q => { q.solid = false; }); k++;
        }
        /* stalagmites out in the open, to walk round and fight among */
        for (int i = 0, k = 0; i < 600 && k < 18; i++)
        {
            int c = (int)(Js.R() * M.g.Length), x = c % M.cols, y = c / M.cols;
            if (M.g[c] != 0 || M.dist[c] < 5 || x < 1 || y < 1 || x >= M.cols - 1 || y >= M.rows - 1) continue;
            bool open = true; for (int dy = -1; dy <= 1 && open; dy++) for (int dx = -1; dx <= 1; dx++) if (M.g[(y + dy) * M.cols + x + dx] != 0) { open = false; break; }
            if (!open) continue;
            var p = caveXY(M, c); if (hypot(p.x - B.x, p.y - B.y) < 320 || props.Any(q => q.type == "rstalag" && hypot(q.x - p.x, q.y - p.y) < 260)) continue;
            rpgProp("rstalag", p.x + rnd(-18, 18), p.y + rnd(-18, 18), rnd(15, 23)); k++;
        }
        navBuild();
        /* the packs: dormant, on roomy floor, the further in the more of them rare */
        var floor = new List<int>();
        for (int i = 0; i < M.g.Length; i++)
        {
            if (M.g[i] != 0 || M.dist[i] < 8) continue;
            int x = i % M.cols, y = i / M.cols;
            bool ok = true; for (int dy = -1; dy <= 1 && ok; dy++) for (int dx = -1; dx <= 1; dx++) if (M.g[(y + dy) * M.cols + x + dx] != 0) { ok = false; break; }
            if (ok) floor.Add(i);
        }
        var spots = new List<(Pt p, double depth)>();
        for (int tries = 0; tries < 600 && spots.Count < CAVE_PACKS && floor.Count > 0; tries++)
        {
            int c = floor[(int)(Js.R() * floor.Count)]; var p = caveXY(M, c);
            if (spots.Any(s => hypot(s.p.x - p.x, s.p.y - p.y) < 420)) continue;
            if (hypot(p.x - B.x, p.y - B.y) < 300) continue;
            if (hypot(p.x - A.x, p.y - A.y) < RPG_AGGRO_R + 260) continue;   /* none wakes as you arrive, rock or no rock */
            spots.Add((p, M.dist[c] / (double)Math.Max(1, M.walk)));
        }
        var cave = new (string k, double w)[] { ("cryptbat", 3), ("shade", 2), ("skeleton", 3), ("boneling", 3), ("zombie", 2), ("spitter", 1), ("archer", 1) };
        string pick()
        {
            double t = 0; foreach (var (_, w) in cave) t += w; double r = Js.R() * t;
            foreach (var (k, w) in cave) { r -= w; if (r <= 0) return k; }
            return cave[0].k;
        }
        for (int i = 0; i < spots.Count; i++)
        {
            var (s, depth) = spots[i];
            int id = 200 + i, before = enemies.Count; string type = pick(); int n = irnd(3, 6);
            if (depth > 0.6 && Js.R() < 0.5) spawnEnemy(type, s.x, s.y, true);
            else if (Js.R() < RPG_MAGIC) spawnMagicPack(type, s, n, false);
            else for (int k = 0; k < n; k++)
                {
                    double a = (double)k / n * TAU, rr = rnd(24, 60); var at = floorAt(s.x + Math.Cos(a) * rr, s.y + Math.Sin(a) * rr, 16);
                    spawnEnemy(type, at.x, at.y);
                }
            foreach (var e in enemies.Skip(before)) { e.faceA = rnd(TAU); e.dormant = true; e.rpgPack = id; }
        }
        /* and at the far end, a rare by the way out */
        spawnEnemy("knight", B.x + 70, B.y + 40, true);
        var b = enemies[^1]; b.maxHp *= 2.5; b.hp = b.maxHp; b.rpgBoss = true; b.rpgName = "THE CAVE WARDEN";
        b.dormant = true; b.rpgPack = 199; b.rpgCave = true;
        RPG.boss = b; RPG.packs = spots.Count;
    }
    /* ---- THE CAVE, DRAWN ----
       The grid is what the cave IS (collision, paths, spawns); this is what it
       LOOKS like. The rock's edge is traced off the grid as closed loops, cut into
       20px steps, nudged by noise and smoothed, so the 80px staircase reads as a
       worn rock face instead of stacked blocks. Each loop has the rock on its
       right. The rock is drawn in 3/4 view: a cliff FACE where it drops to the
       floor below, a TOP lifted CAVE_TOP_H above it, studs and moss along the lit
       rim, soft shadow pooled at its foot. All of that is painted once into tiles
       and cached; what moves (drips, spores, bats, the light) is drawn per frame. */
    public const double CAVE_TILE = 384, CAVE_TOP_H = 22; public const int CAVE_TILE_MAX = 48;
    public static CaveArtT caveArt = null; public static OffCanvas caveLightCv = null;
    sealed class CEdge { public int ax, ay, bx, by, dx, dy; public bool used; }
    public static List<CaveLoop> caveLoops(CaveMapT M)
    {
        int C = M.cols, R = M.rows; var Gg = M.g; double Sz = CAVE_CELL; int V = C + 1;
        int wall(int x, int y) => x < 0 || y < 0 || x >= C || y >= R ? 1 : Gg[y * C + x];
        var @out = new JsMap<int, List<CEdge>>(); var edges = new List<CEdge>();
        void add(int ax, int ay, int bx, int by)
        {
            var e = new CEdge { ax = ax, ay = ay, bx = bx, by = by, dx = bx - ax, dy = by - ay, used = false };
            int id = ay * V + ax; if (!@out.ContainsKey(id)) @out[id] = new(); @out[id].Add(e); edges.Add(e);
        }
        for (int y = 0; y < R; y++) for (int x = 0; x < C; x++)
            {
                if (Gg[y * C + x] == 0) continue;
                if (wall(x, y - 1) == 0) add(x, y, x + 1, y);
                if (wall(x + 1, y) == 0) add(x + 1, y, x + 1, y + 1);
                if (wall(x, y + 1) == 0) add(x + 1, y + 1, x, y + 1);
                if (wall(x - 1, y) == 0) add(x, y + 1, x, y);
            }
        var loops = new List<CaveLoop>();
        foreach (var e0 in edges)
        {
            if (e0.used) continue;
            var pts = new List<int>(); var e = e0;
            while (e != null && !e.used)
            {
                e.used = true; pts.Add(e.ax); pts.Add(e.ay);
                var cand = (@out.GetValueOrDefault(e.by * V + e.bx) ?? new()).Where(q => !q.used).ToList();
                var ee = e;
                e = cand.Count > 1 ? (cand.Find(q => q.dx == -ee.dy && q.dy == ee.dx) ?? cand[0]) : cand.Count > 0 ? cand[0] : null;
            }
            if (pts.Count < 6) continue;
            /* 20px steps, a nudge of noise across the line, then smoothing */
            int n = pts.Count / 2; var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double ax = pts[i * 2] * Sz, ay = pts[i * 2 + 1] * Sz, bx = pts[((i + 1) % n) * 2] * Sz, by = pts[((i + 1) % n) * 2 + 1] * Sz;
                double tx = (bx - ax) / Sz, ty = (by - ay) / Sz;
                for (int k = 0; k < 4; k++)
                {
                    double px = ax + (bx - ax) * k / 4, py = ay + (by - ay) * k / 4;
                    double j = (hash2(Js.I32(px * 0.37), Js.I32(py * 0.53)) - 0.5) * 12 + Math.Sin(px * 0.021 + py * 0.017) * 5;
                    xs.Add(px - ty * j); ys.Add(py + tx * j);
                }
            }
            int m = xs.Count;
            for (int pass = 0; pass < 7; pass++)
            {
                var x2 = xs.ToArray(); var y2 = ys.ToArray();
                for (int i = 0; i < m; i++)
                {
                    int a = (i - 1 + m) % m, b = (i + 1) % m;
                    xs[i] = (x2[a] + 2 * x2[i] + x2[b]) / 4; ys[i] = (y2[a] + 2 * y2[i] + y2[b]) / 4;
                }
            }
            /* the normal into the rock: the rock is on the right of the walk */
            var nx = new float[m]; var ny = new float[m];
            for (int i = 0; i < m; i++)
            {
                int a = (i - 1 + m) % m, b = (i + 1) % m;
                double tx = xs[b] - xs[a], ty = ys[b] - ys[a]; double l = hypot(tx, ty); if (l == 0) l = 1; tx /= l; ty /= l;
                nx[i] = (float)-ty; ny[i] = (float)tx;
            }
            double minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
            for (int i = 0; i < m; i++) { minX = Math.Min(minX, xs[i]); maxX = Math.Max(maxX, xs[i]); minY = Math.Min(minY, ys[i]); maxY = Math.Max(maxY, ys[i]); }
            loops.Add(new CaveLoop { xs = xs.Select(v => (float)v).ToArray(), ys = ys.Select(v => (float)v).ToArray(), nx = nx, ny = ny, n = m, box = new[] { minX, minY, maxX, maxY } });
        }
        return loops;
    }
    public static CaveArtT caveArtBuild(CaveMapT M)
    {
        var rng = mulberry32((uint)(Js.I32(M.seed) ^ 0x5bd1e995)); double r(double a, double b) => a + rng() * (b - a);
        var loops = caveLoops(M);
        var floor = new Path2D();
        foreach (var L in loops) { floor.moveTo(L.xs[0], L.ys[0]); for (int i = 1; i < L.n; i++) floor.lineTo(L.xs[i], L.ys[i]); floor.closePath(); }
        var rock = new Path2D(); rock.rect(-400, -400, W.w + 800, W.h + 800); rock.addPath(floor);
        var A = new CaveArtT { M = M, loops = loops, floor = floor, rock = rock, k = 1, frame = 0, batT = r(3, 8) };
        string[] SHROOM = { "#5ff0d0", "#c08bff", "#ffb45a", "#8fd4ff" }, SHARD = { "#b48cff", "#7fe8ff", "#ff8fd0", "#9dffb0" };
        bool mossy(double x, double y) => Math.Sin(x * 0.009 + 1.3) + Math.Sin(y * 0.011 - 0.4) + Math.Sin((x + y) * 0.006) > 0.9;
        foreach (var L in loops)
        {
            for (int i = 0; i < L.n; i++)
            {
                double x = L.xs[i], y = L.ys[i], nx = L.nx[i], ny = L.ny[i], h = hash2(Js.I32(x), Js.I32(y));
                /* studs on the lit rim of the rock top, two broken rows */
                if (h > 0.38) A.studs.Add(new CaveStud { x = x + nx * 15, y = y + ny * 15 - CAVE_TOP_H, r = 5.5 + (h > 0.9 ? 1.5 : 0) });
                if (i % 2 == 0 && hash2(Js.I32(y), Js.I32(x)) > 0.62) A.studs.Add(new CaveStud { x = x + nx * 36, y = y + ny * 36 - CAVE_TOP_H, r = 5 });
                if (mossy(x, y) && h < 0.55) { double d1 = r(4, 20), d2 = r(4, 20); A.moss.Add(new CaveDeco { x = x + nx * d1, y = y + ny * d2 - CAVE_TOP_H, r = r(6, 14), top = true }); }
                if (mossy(x, y) && h > 0.5) { double d1 = r(14, 40), d2 = r(14, 40); A.moss.Add(new CaveDeco { x = x - nx * d1, y = y - ny * d2, r = r(10, 22), top = false }); }
                if (rng() < 0.16) { double d1 = r(16, 60), d2 = r(16, 60), rr = r(3, 7); A.pebbles.Add(new CaveDeco { x = x - nx * d1, y = y - ny * d2, r = rr, c = sh("#6b6274", r(-0.15, 0.12)) }); }
                /* a crystal vein in a face you can see: rock to the north */
                if (ny < -0.6 && rng() < 0.03)
                {
                    string col = SHARD[(int)(rng() * SHARD.Length)];
                    A.shards.Add(new CaveDeco { x = x, y = y - CAVE_TOP_H * 0.45, col = col, n = 2 + (int)(rng() * 3), s = r(0.8, 1.3), ph = rng() * TAU });
                    A.lights.Add(new CaveLight { x = x, y = y - 6, r = 170, col = col, a = 0.75, ph = rng() * TAU });
                }
                /* glowing mushrooms at the foot of the rock */
                if (rng() < 0.022)
                {
                    string col = SHROOM[(int)(rng() * SHROOM.Length)]; double cx = x - nx * 26, cy = y - ny * 26; var caps = new List<(double, double, double)>();
                    for (int k = 0, kn = 3 + (int)(rng() * 4); k < kn; k++) { double dx = r(-18, 18), dy = r(-10, 10), s = r(0.6, 1.25); caps.Add((dx, dy, s)); }
                    A.shrooms.Add(new CaveDeco { x = cx, y = cy, col = col, caps = caps, ph = rng() * TAU });
                    A.lights.Add(new CaveLight { x = cx, y = cy, r = 120, col = col, a = 0.6, ph = rng() * TAU });
                }
            }
        }
        /* puddles, dirt and bones out on the floor */
        var roomyL = new List<int>();
        for (int y = 1; y < M.rows - 1; y++) for (int x = 1; x < M.cols - 1; x++)
            {
                bool ok = true; for (int dy = -1; dy <= 1 && ok; dy++) for (int dx = -1; dx <= 1; dx++) if (M.g[(y + dy) * M.cols + x + dx] != 0) { ok = false; break; }
                if (ok) roomyL.Add(y * M.cols + x);
            }
        for (int k = 0; k < 16 && roomyL.Count > 0; k++)
        {
            var p = caveXY(M, roomyL[(int)(rng() * roomyL.Count)]);
            double ox = r(-20, 20), oy = r(-20, 20), rx = r(26, 62), ph = rng() * 10, per = r(2.6, 5.5);
            A.puddles.Add(new CaveDeco { x = p.x + ox, y = p.y + oy, rx = rx, ph = ph, per = per });
        }
        for (int y = 0; y < M.rows; y++) for (int x = 0; x < M.cols; x++)
            {
                if (M.g[y * M.cols + x] == 0 || rng() < 0.45) continue;
                double lx = x * CAVE_CELL + r(0, CAVE_CELL), ly = y * CAVE_CELL + r(0, CAVE_CELL) - CAVE_TOP_H, lr = r(18, 40), la = r(0, TAU);
                var lc = sh("#3a3342", r(-0.12, 0.14)); bool ls = rng() < 0.3;
                A.lumps.Add(new CaveLump { x = lx, y = ly, r = lr, a = la, c = lc, stud = ls });
            }
        string[] PATCH = { "rgba(70,52,40,0.16)", "rgba(36,30,52,0.16)", "rgba(52,60,48,0.14)", "rgba(20,16,30,0.14)" };
        for (int k = 0; k < 150 && roomyL.Count > 0; k++)
        {
            var p = caveXY(M, roomyL[(int)(rng() * roomyL.Count)]);
            double ox = r(-60, 60), oy = r(-60, 60), rx = r(50, 140), ry = r(30, 80), a = r(0, TAU);
            A.patches.Add(new CaveDeco { x = p.x + ox, y = p.y + oy, rx = rx, ry = ry, a = a, cs = PATCH[(int)(rng() * 4)] });
        }
        for (int k = 0; k < 120 && roomyL.Count > 0; k++)
        {
            var p = caveXY(M, roomyL[(int)(rng() * roomyL.Count)]);
            double ox = r(-40, 40), oy = r(-40, 40), a = r(0, TAU);
            A.cracks.Add(new CaveDeco { x = p.x + ox, y = p.y + oy, a = a });
        }
        for (int k = 0; k < 10 && roomyL.Count > 0; k++)
        {
            var p = caveXY(M, roomyL[(int)(rng() * roomyL.Count)]);
            double ox = r(-30, 30), oy = r(-30, 30), a = r(0, TAU); bool skull = rng() < 0.4;
            A.bones.Add(new CaveDeco { x = p.x + ox, y = p.y + oy, a = a, skull = skull });
        }
        return A;
    }
    /* one tile of the painted cave: floor dressing, the shadow, the face, the top */
    public static OffCanvas caveTile(CaveArtT A, int tx, int ty)
    {
        double S = CAVE_TILE, k = A.k, x0 = tx * S, y0 = ty * S;
        var c = new OffCanvas(); c.width = c.height = (int)Math.Ceiling(S * k);
        var g = c.getContext("2d"); g.scale(k, k); g.translate(-x0, -y0);
        bool near(double x, double y, double m) => x > x0 - m && x < x0 + S + m && y > y0 - m && y < y0 + S + m;
        g.lineJoin = "round"; g.lineCap = "round";
        /* the floor: darker, cooler, with patches of dirt */
        g.fillStyle = "rgba(26,18,40,0.36)"; g.fill(A.floor, "evenodd");
        /* hairline cracks across the floor, so the plate does not read as tiles */
        g.strokeStyle = "rgba(12,8,20,0.35)"; g.lineWidth = 1.6;
        foreach (var d in A.cracks)
            if (near(d.x, d.y, 90))
            {
                g.beginPath(); g.moveTo(d.x, d.y); double x = d.x, y = d.y, a = d.a;
                for (int i = 0; i < 6; i++) { a += Math.Sin(d.x * 0.1 + i * 2.3) * 0.7; x += Math.Cos(a) * 14; y += Math.Sin(a) * 14; g.lineTo(x, y); }
                g.stroke();
            }
        foreach (var d in A.patches) if (near(d.x, d.y, 120)) { g.fillStyle = d.cs; g.beginPath(); g.ellipse(d.x, d.y, d.rx, d.ry, d.a, 0, TAU); g.fill(); }
        foreach (var d in A.moss)
            if (!d.top && near(d.x, d.y, 30))
            {
                g.fillStyle = "rgba(92,140,70,0.38)"; g.beginPath(); g.ellipse(d.x, d.y, d.r * 1.4, d.r * 0.8, 0, 0, TAU); g.fill();
                g.fillStyle = "rgba(140,190,90,0.35)"; g.beginPath(); g.arc(d.x - d.r * 0.3, d.y - 2, d.r * 0.45, 0, TAU); g.fill();
            }
        foreach (var d in A.puddles)
            if (near(d.x, d.y, 80))
            {
                g.fillStyle = "rgba(20,30,50,0.55)"; g.beginPath(); g.ellipse(d.x, d.y, d.rx, d.rx * 0.5, 0, 0, TAU); g.fill();
                g.fillStyle = "rgba(60,90,130,0.45)"; g.beginPath(); g.ellipse(d.x, d.y - 2, d.rx * 0.86, d.rx * 0.4, 0, 0, TAU); g.fill();
                g.strokeStyle = "rgba(170,200,240,0.35)"; g.lineWidth = 2; g.beginPath(); g.ellipse(d.x - d.rx * 0.2, d.y - d.rx * 0.14, d.rx * 0.45, d.rx * 0.14, 0, Math.PI * 1.05, Math.PI * 1.6); g.stroke();
            }
        foreach (var d in A.bones)
            if (near(d.x, d.y, 30))
            {
                g.save(); g.translate(d.x, d.y); g.rotate(d.a); g.fillStyle = "#d9d2bf"; g.strokeStyle = "rgba(40,30,30,0.5)"; g.lineWidth = 1.5;
                if (d.skull) { g.beginPath(); g.arc(0, 0, 8, 0, TAU); g.fill(); g.stroke(); g.fillStyle = "#2a2326"; g.beginPath(); g.arc(-3, -1, 2, 0, TAU); g.arc(3, -1, 2, 0, TAU); g.fill(); }
                else { roundRect(g, -12, -2.5, 24, 5, 2.5); g.fill(); g.stroke(); g.beginPath(); g.arc(-12, 0, 3.5, 0, TAU); g.arc(12, 0, 3.5, 0, TAU); g.fill(); }
                g.restore();
            }
        foreach (var d in A.pebbles)
            if (near(d.x, d.y, 10))
            {
                g.fillStyle = "rgba(10,6,16,0.35)"; g.beginPath(); g.ellipse(d.x + 1, d.y + 2, d.r, d.r * 0.6, 0, 0, TAU); g.fill();
                g.fillStyle = d.c; g.beginPath(); g.ellipse(d.x, d.y, d.r, d.r * 0.75, 0, 0, TAU); g.fill();
            }
        /* the shadow pooled at the rock's foot */
        g.save(); g.shadowColor = "rgba(4,2,10,0.8)"; g.shadowBlur = 34 * k; g.shadowOffsetY = 14 * k;
        g.fillStyle = "#16121c"; g.fill(A.rock, "evenodd"); g.restore();
        /* the face: layered stone, darker at its foot */
        g.save(); g.clip(A.rock, "evenodd");
        g.fillStyle = "#3a3242"; g.fillRect(x0, y0, S, S);
        for (double y = Math.Floor(y0 / 9) * 9; y < y0 + S; y += 9)
        {
            g.strokeStyle = ((y / 9) % 2) != 0 ? "rgba(0,0,0,0.22)" : "rgba(255,240,255,0.05)"; g.lineWidth = 2;
            g.beginPath(); for (double x = x0 - 10; x <= x0 + S + 10; x += 24) g.lineTo(x, y + Math.Sin(x * 0.05 + y * 0.3) * 2.5); g.stroke();
        }
        g.strokeStyle = "rgba(0,0,0,0.4)"; g.lineWidth = 10; g.stroke(A.floor);
        g.restore();
        /* crystal veins in the face */
        foreach (var d in A.shards)
            if (near(d.x, d.y, 40))
            {
                for (int i = 0; i < d.n; i++)
                {
                    double a = -0.5 + i / (double)Math.Max(1, d.n - 1) - 0.5 * 0, h = (10 + i * 5) * d.s, w = 4.5 * d.s;
                    g.save(); g.translate(d.x + (i - (d.n - 1) / 2.0) * 7 * d.s, d.y + 6); g.rotate(a * 0.8);
                    var gr = g.createLinearGradient(0, 0, 0, -h); gr.addColorStop(0, sh(d.col, -0.45)); gr.addColorStop(1, "#ffffff");
                    g.fillStyle = gr; g.beginPath(); g.moveTo(-w, 0); g.lineTo(0, -h); g.lineTo(w, 0); g.closePath(); g.fill();
                    g.strokeStyle = "rgba(255,255,255,0.5)"; g.lineWidth = 1; g.stroke(); g.restore();
                }
            }
        /* the top: deep rock dark, the rim lit, studs and moss along it */
        g.save(); g.translate(0, -CAVE_TOP_H); g.clip(A.rock, "evenodd");
        g.fillStyle = "#332d3a"; g.fillRect(x0, y0 + CAVE_TOP_H, S, S);
        /* the deep rock is not flat: boulders of it, lit from above */
        foreach (var d in A.lumps)
            if (near(d.x, d.y + CAVE_TOP_H, d.r + 10))
            {
                g.fillStyle = d.c; g.beginPath(); g.ellipse(d.x, d.y + CAVE_TOP_H, d.r, d.r * 0.8, d.a, 0, TAU); g.fill();
                g.fillStyle = "rgba(255,245,255,0.06)"; g.beginPath(); g.ellipse(d.x - d.r * 0.25, d.y + CAVE_TOP_H - d.r * 0.3, d.r * 0.55, d.r * 0.35, d.a, 0, TAU); g.fill();
                if (d.stud) drawStudG(g, d.x, d.y + CAVE_TOP_H - d.r * 0.2, 5, d.c);
            }
        g.strokeStyle = "rgba(58,51,68,0.85)"; g.lineWidth = 72; g.stroke(A.floor);
        g.strokeStyle = "#4b4455"; g.lineWidth = 36; g.stroke(A.floor);
        g.strokeStyle = "#5d5568"; g.lineWidth = 12; g.stroke(A.floor);
        g.restore();
        g.save(); g.translate(0, -CAVE_TOP_H);
        g.strokeStyle = "rgba(235,225,250,0.45)"; g.lineWidth = 2.5; g.stroke(A.floor);
        g.restore();
        foreach (var d in A.studs) if (near(d.x, d.y, 12)) drawStudG(g, d.x, d.y, d.r, "#4f4859");
        foreach (var d in A.moss)
            if (d.top && near(d.x, d.y, 20))
            {
                g.fillStyle = "rgba(96,150,72,0.75)"; g.beginPath(); g.arc(d.x, d.y, d.r, 0, TAU); g.fill();
                g.fillStyle = "rgba(150,200,100,0.6)"; g.beginPath(); g.arc(d.x - d.r * 0.3, d.y - d.r * 0.3, d.r * 0.45, 0, TAU); g.fill();
            }
        return c;
    }
    public static void drawCaveWalls(double vx, double vy, double vw, double vh)
    {
        var M = caveMap; if (M == null || !rpgOn() || RPG.zone != "cave") return;
        if (caveArt == null || caveArt.M != M) caveArt = caveArtBuild(M);
        var A = caveArt; double k = clamp(zoom * dpr, 1, 1.5);
        if (Math.Abs(A.k - k) > 0.01) { A.k = k; foreach (var t in A.tiles.Values) t.c.Free(); A.tiles.Clear(); }
        A.frame++;
        double S = CAVE_TILE; int t0x = (int)Math.Floor(vx / S), t1x = (int)Math.Floor((vx + vw) / S), t0y = (int)Math.Floor(vy / S), t1y = (int)Math.Floor((vy + vh) / S);
        for (int ty = t0y; ty <= t1y; ty++) for (int tx = t0x; tx <= t1x; tx++)
            {
                string key = tx + "," + ty;
                if (!A.tiles.TryGetValue(key, out var T)) { T = new CaveTileT { c = caveTile(A, tx, ty), used = 0 }; A.tiles[key] = T; }
                T.used = A.frame; ctx.drawImage(T.c, tx * S, ty * S, S, S);
            }
        /* one more tile a frame, just past the view, so walking never waits on paint */
        bool done = false;
        for (int ty = t0y - 1; ty <= t1y + 1 && !done; ty++) for (int tx = t0x - 1; tx <= t1x + 1; tx++)
            {
                if (tx < -1 || ty < -1 || tx * S > W.w || ty * S > W.h || A.tiles.ContainsKey(tx + "," + ty)) continue;
                A.tiles[tx + "," + ty] = new CaveTileT { c = caveTile(A, tx, ty), used = A.frame }; done = true; break;
            }
        if (A.tiles.Count > CAVE_TILE_MAX)
        {
            var old = A.tiles.OrderBy(e => e.Value.used).ToList();
            for (int i = 0; i < old.Count - CAVE_TILE_MAX; i++) { old[i].Value.c.Free(); A.tiles.Remove(old[i].Key); }
        }
        drawCaveLife(vx, vy, vw, vh);
    }
    /* what lives on the floor: mushrooms breathing light, drops falling into puddles */
    public static void drawCaveLife(double vx, double vy, double vw, double vh)
    {
        var A = caveArt; if (A == null) return;
        bool vis(double x, double y, double m) => x > vx - m && x < vx + vw + m && y > vy - m && y < vy + vh + m;
        foreach (var d in A.puddles)
        {
            if (!vis(d.x, d.y, 150)) continue;
            double t = (vtime + d.ph) % d.per, fall = 0.45;
            if (t < fall)
            {
                double f = t / fall; ctx.fillStyle = "rgba(190,220,255,0.8)";
                ctx.beginPath(); ctx.ellipse(d.x + d.rx * 0.15, d.y - 130 * (1 - f * f), 2, 4, 0, 0, TAU); ctx.fill();
            }
            else
            {
                double f = (t - fall) / 1.1;
                if (f < 1)
                    for (int q = 0; q < 2; q++)
                    {
                        double ff = f - q * 0.25; if (ff <= 0) continue;
                        ctx.strokeStyle = $"rgba(190,220,255,{S(0.55 * (1 - ff))})"; ctx.lineWidth = 1.5;
                        ctx.beginPath(); ctx.ellipse(d.x + d.rx * 0.15, d.y, 4 + ff * d.rx * 0.6, (4 + ff * d.rx * 0.6) * 0.45, 0, 0, TAU); ctx.stroke();
                    }
            }
        }
        foreach (var d in A.shrooms)
        {
            if (!vis(d.x, d.y, 40)) continue;
            double pulse = 0.6 + 0.4 * Math.Sin(vtime * 1.6 + d.ph);
            foreach (var c in d.caps)
            {
                double x = d.x + c.dx, y = d.y + c.dy, s = c.s;
                ctx.fillStyle = "#e8e0d0"; roundRect(ctx, x - 2 * s, y - 9 * s, 4 * s, 9 * s, 1.5 * s); ctx.fill();
                ctx.fillStyle = sh(d.col, -0.25 + 0.25 * pulse);
                ctx.beginPath(); ctx.ellipse(x, y - 9 * s, 7 * s, 4.5 * s, 0, Math.PI, 0); ctx.fill();
                ctx.fillStyle = $"rgba(255,255,255,{S(0.35 * pulse)})"; ctx.beginPath(); ctx.arc(x - 2 * s, y - 11 * s, 1.4 * s, 0, TAU); ctx.fill();
            }
        }
    }
    /* the air: darkness with your light in it, the glows, spores, and bats */
    public static void drawCaveLight()
    {
        var A = caveArt; if (A == null || !rpgOn() || RPG.zone != "cave" || player == null) return;
        var p = player; double Z = zoom * dpr, cvW = cw * dpr, cvH = ch * dpr, hw = cvW / 2, hh = cvH / 2;
        (double, double) Sp(double x, double y) => ((x - cam.x) * Z + hw, (y - cam.y) * Z + hh);
        bool on(double sx, double sy, double R) => sx > -R && sy > -R && sx < cvW + R && sy < cvH + R;
        /* the bats: a flock now and then, across the view */
        double dt = Math.Min(0.05, Js.Or(frameRaw, 1.0 / 60));
        A.batT -= dt;
        if (A.batT <= 0)
        {
            A.batT = 9 + Js.R() * 14;
            double from = Js.R() < 0.5 ? -1 : 1, y0 = cam.y + (Js.R() - 0.5) * ch / zoom * 0.7; int n = 3 + (int)(Js.R() * 5);
            for (int i = 0; i < n; i++)
            {
                double bx = cam.x - from * (cw / zoom / 2 + 60 + Js.R() * 160), by = y0 + (Js.R() - 0.5) * 160;
                double bvx = from * (260 + Js.R() * 120), bvy = (Js.R() - 0.5) * 60, bph = Js.R() * TAU;
                A.bats.Add(new CaveBat { x = bx, y = by, vx = bvx, vy = bvy, ph = bph, life = 9 });
            }
        }
        foreach (var b in A.bats) { b.x += b.vx * dt; b.y += (b.vy + Math.Sin(vtime * 3 + b.ph) * 40) * dt; b.life -= dt; }
        A.bats = A.bats.Where(b => b.life > 0).ToList();
        foreach (var b in A.bats)
        {
            double fl = Math.Sin(vtime * 22 + b.ph), d = Math.Sign(b.vx);
            ctx.fillStyle = "#0d0a12";
            ctx.beginPath(); ctx.moveTo(b.x - 14, b.y - 6 * fl); ctx.quadraticCurveTo(b.x - 6, b.y - 2, b.x, b.y);
            ctx.quadraticCurveTo(b.x + 6, b.y - 2, b.x + 14, b.y - 6 * fl); ctx.lineTo(b.x + 6, b.y + 2); ctx.lineTo(b.x - 6, b.y + 2); ctx.closePath(); ctx.fill();
            ctx.beginPath(); ctx.arc(b.x, b.y + 1, 3.5, 0, TAU); ctx.fill();
            ctx.fillStyle = "#ff4a5a"; ctx.fillRect(b.x + d * 1.5 - 0.8, b.y, 1.6, 1.6);
        }
        /* the dark, with holes cut where there is light */
        var L = caveLightCv ??= new OffCanvas();
        if (L.width != (int)cvW || L.height != (int)cvH) { L.width = Math.Max(1, (int)cvW); L.height = Math.Max(1, (int)cvH); }
        var g = L.getContext("2d");
        g.globalCompositeOperation = "source-over"; g.clearRect(0, 0, L.width, L.height);
        g.fillStyle = "rgba(7,4,16,0.64)"; g.fillRect(0, 0, L.width, L.height);
        g.globalCompositeOperation = "destination-out";
        void hole(double x, double y, double r, double a)
        {
            var (sx, sy) = Sp(x, y); double R = r * Z; if (!on(sx, sy, R)) return;
            var gr = g.createRadialGradient(sx, sy, 0, sx, sy, R);
            gr.addColorStop(0, $"rgba(0,0,0,{S(a)})"); gr.addColorStop(0.5, $"rgba(0,0,0,{S(a * 0.7)})"); gr.addColorStop(1, "rgba(0,0,0,0)");
            g.fillStyle = gr; g.fillRect(sx - R, sy - R, R * 2, R * 2);
        }
        hole(p.x, p.y - 10, 560 + Math.Sin(vtime * 7) * 6 + Math.Sin(vtime * 13) * 4, 1);
        foreach (var l in A.lights) hole(l.x, l.y, l.r, l.a * (0.8 + 0.2 * Math.Sin(vtime * 1.6 + l.ph)));
        foreach (var q in props) if (!q.dead && (q.type == "crystal" || q.type == "rportal")) hole(q.x, q.y - 20, q.type == "rportal" ? 260 : 150, 0.8);
        foreach (var pr in projectiles) if (!pr.dead) hole(pr.x, pr.y, 70, 0.55);
        foreach (var m in minions) if (!m.dead) hole(m.x, m.y, 110, 0.35);
        g.globalCompositeOperation = "source-over";
        ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.drawImage(L, 0, 0);
        /* the coloured glows, added on top */
        ctx.globalCompositeOperation = "lighter";
        void glow(double x, double y, double r, string col, double a)
        {
            var (sx, sy) = Sp(x, y); double R = r * Z; if (!on(sx, sy, R)) return;
            var gr = ctx.createRadialGradient(sx, sy, 0, sx, sy, R); gr.addColorStop(0, col); gr.addColorStop(1, "rgba(0,0,0,0)");
            ctx.globalAlpha = a; ctx.fillStyle = gr; ctx.fillRect(sx - R, sy - R, R * 2, R * 2);
        }
        foreach (var l in A.lights) glow(l.x, l.y, l.r * 0.7, l.col, 0.22 * (0.7 + 0.3 * Math.Sin(vtime * 1.6 + l.ph)));
        foreach (var q in props) if (!q.dead && q.type == "crystal") glow(q.x, q.y - 16, 110, "#3ddc84", 0.2);
        glow(p.x, p.y - 10, 300, "#ffcf8a", 0.08);
        /* spores drifting through the light */
        ctx.globalAlpha = 1;
        const double span = 1800;
        for (int i = 0; i < 70; i++)
        {
            double hx = hash2(i, 17), hy = hash2(i, 71), sp = 8 + hash2(i, 5) * 14;
            double wx = cam.x - span / 2 + (((hx * span + vtime * sp * (hash2(i, 9) - 0.5) * 2 + Math.Sin(vtime * 0.4 + i) * 30) % span) + span) % span;
            double wy = cam.y - span / 2 + (((hy * span - vtime * sp) % span) + span) % span;
            var (sx, sy) = Sp(wx, wy); if (!on(sx, sy, 4) || caveWallAt(wx, wy + CAVE_TOP_H)) continue;
            double tw = 0.5 + 0.5 * Math.Sin(vtime * 2 + i * 1.7);
            ctx.fillStyle = i % 3 != 0 ? $"rgba(170,255,220,{S(0.35 * tw)})" : $"rgba(220,190,255,{S(0.4 * tw)})";
            ctx.beginPath(); ctx.arc(sx, sy, (1 + hash2(i, 3) * 1.6) * Z, 0, TAU); ctx.fill();
        }
        ctx.restore();
    }
}
