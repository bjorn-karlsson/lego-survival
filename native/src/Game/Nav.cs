// lego-survival.html, JS 23269-23695: PATHFINDING — the coarse nav grid, A*,
// sliding round what is in the way, THE BUCKET GRID (enemiesNear), and THE FLOW
// FIELD every monster walks down towards the hero.
namespace Brickblade;

public sealed class NavGrid { public double cs = 40; public int w, h; public byte[] blocked; public int gen; }
public sealed class EGridT { public double cs = 130; public int w, h; public List<Ent>[] cells; }
public sealed class FlowT { public float[] dist; public int[] heap; public int ix = -1, iy = -1, gen = -1; public double t; public double built; public byte[] done; }
public sealed partial class Prop { public bool navHard; }

public static partial class G
{
    /* =============================== PATHFINDING ===============================
       A coarse navigation grid over the map, rebuilt whenever the world is. Monsters
       walk straight at the player while they can see them; the moment the line is
       blocked they ask for an A* path and follow its waypoints instead of grinding
       into a boulder. Searches are budgeted per frame and each path is cached, so a
       field of 500 monsters costs a handful of searches, not 500. */
    public static readonly NavGrid NAV = new();
    public const int NAV_BUDGET = 14;          /* A* searches allowed per frame */
    public const double NAV_PAD = 18;          /* a body's width kept off geometry */
    public const double NAV_SMALL = 18;        /* props thinner than this are left to local steering */
    public static double navBudget = NAV_BUDGET;
    /* scratch buffers reused by every search so the hot path allocates nothing
       (Float32Array in the JS: the same float32 rounding here) */
    public static float[] navG = null, navF = null; public static int[] navFrom = null, navSeen = null; public static int navStamp = 0;

    /* A cell is blocked when its CENTRE is inside something big plus a body's width.
       A sapling or a crate is not on the grid at all: walled off cell by cell, a wood
       of them read as a maze with no way through, and one circle is exactly what the
       local steering already slides round. Lakes, boulders, big rocks and the ruins
       are what a body has to PLAN around, so they are what the grid knows. */
    public static bool navCellBad(int ix, int iy)
    {
        double cs = NAV.cs, cx = ix * cs + cs / 2, cy = iy * cs + cs / 2, pad = NAV_PAD;
        if (cx < W.wall + pad || cx > W.w - W.wall - pad || cy < W.wall + pad || cy > W.h - W.wall - pad) return true;
        foreach (var so in solids)
        {
            if (so.dead || (so.r < NAV_SMALL && !so.navHard)) continue;
            if (Math.abs(cx - so.x) > so.r + pad || Math.abs(cy - so.y) > so.r + pad) continue;
            if (Math.hypot(cx - so.x, cy - so.y) < so.r + pad) return true;
        }
        foreach (var wa in waters)
        {
            if (cx > wa.x - pad && cx < wa.x + wa.w + pad && cy > wa.y - pad && cy < wa.y + wa.h + pad) return true;
        }
        return false;
    }
    public static void navBuild()
    {
        var cs = NAV.cs;
        NAV.w = (int)Math.ceil(W.w / cs); NAV.h = (int)Math.ceil(W.h / cs);
        var n = NAV.w * NAV.h;
        NAV.blocked = new byte[n];
        navG = new float[n]; navF = new float[n];
        navFrom = new int[n]; navSeen = new int[n]; navStamp = 0;
        FLOW.dist = new float[n]; FLOW.heap = new int[n * 4 + 16];
        for (int iy = 0; iy < NAV.h; iy++) for (int ix = 0; ix < NAV.w; ix++)
                NAV.blocked[iy * NAV.w + ix] = (byte)(navCellBad(ix, iy) ? 1 : 0);
        NAV.gen++;
    }
    /* THE GRID LEARNS. A crate a body's width from a lake is a gap the grid swears
       is open and nothing can fit through; the field sent body after body into it.
       The first time something is wedged against a small prop, that prop joins the
       grid for the rest of the run, and every route in the field goes round it. */
    public static bool navLearn(Ent e)
    {
        if (NAV.blocked == null) return false;
        bool learnt = false;
        foreach (var so in solids)
        {
            if (so.dead || so.navHard || so.r >= NAV_SMALL) continue;
            if (Math.hypot(so.x - e.x, so.y - e.y) > so.r + Js.Or(e.r, 12) + 34) continue;
            so.navHard = true; navRefresh(so.x, so.y, so.r); learnt = true;
        }
        return learnt;
    }
    /* something big came apart: the cells it covered are floor again */
    public static void navRefresh(double x, double y, double r)
    {
        if (NAV.blocked == null) return;
        double cs = NAV.cs, R = r + NAV_PAD + cs;
        int x0 = (int)clamp(Math.floor((x - R) / cs), 0, NAV.w - 1), x1 = (int)clamp(Math.floor((x + R) / cs), 0, NAV.w - 1);
        int y0 = (int)clamp(Math.floor((y - R) / cs), 0, NAV.h - 1), y1 = (int)clamp(Math.floor((y + R) / cs), 0, NAV.h - 1);
        for (int iy = y0; iy <= y1; iy++) for (int ix = x0; ix <= x1; ix++)
                NAV.blocked[iy * NAV.w + ix] = (byte)(navCellBad(ix, iy) ? 1 : 0);
        NAV.gen++;
    }
    public static int navIdx(int ix, int iy) => iy * NAV.w + ix;
    public static double navCX(int ix) => ix * NAV.cs + NAV.cs / 2;
    public static double navCY(int iy) => iy * NAV.cs + NAV.cs / 2;
    /* NAV.blocked[i] === 1, with an index off the end reading as the JS's undefined */
    static bool navBlk(int i) => (uint)i < (uint)NAV.blocked.Length && NAV.blocked[i] == 1;
    public static (int ix, int iy) navCell(double x, double y) =>
        ((int)clamp(Math.floor(x / NAV.cs), 0, NAV.w - 1), (int)clamp(Math.floor(y / NAV.cs), 0, NAV.h - 1));
    public static bool navBlockedAt(double x, double y)
    {
        if (NAV.blocked == null) return false;
        var c = navCell(x, y);
        return navBlk(navIdx(c.ix, c.iy));
    }
    /* nearest walkable cell, for a monster that has been shoved inside geometry */
    public static (int ix, int iy)? navNearestOpen(int ix, int iy)
    {
        if (NAV.blocked[navIdx(ix, iy)] == 0) return (ix, iy);
        for (int r = 1; r <= 6; r++)
        {
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r) continue;
                    int nx = ix + dx, ny = iy + dy;
                    if (nx < 0 || ny < 0 || nx >= NAV.w || ny >= NAV.h) continue;
                    if (NAV.blocked[navIdx(nx, ny)] == 0) return (nx, ny);
                }
        }
        return null;
    }
    /* Bresenham over the grid — true when nothing blocks the straight line */
    public static bool navLineOpen(double x1, double y1, double x2, double y2)
    {
        if (NAV.blocked == null) return true;
        var a = navCell(x1, y1); var b = navCell(x2, y2);
        int x = a.ix, y = a.iy;
        int dx = System.Math.Abs(b.ix - x), dy = System.Math.Abs(b.iy - y);
        int sx = x < b.ix ? 1 : -1, sy = y < b.iy ? 1 : -1;
        int err = dx - dy, guard = 0;
        while (guard++ < 800)
        {
            if (navBlk(navIdx(x, y))) return false;
            if (x == b.ix && y == b.iy) return true;
            var e2 = 2 * err;
            bool stepX = e2 > -dy, stepY = e2 < dx;
            /* a diagonal step may not squeeze between two blocked cells that only
               touch at a corner -- the line the old walk drew straight through a
               lake's corner and into the water */
            if (stepX && stepY && (navBlk(navIdx(x + sx, y)) || navBlk(navIdx(x, y + sy)))) return false;
            if (stepX) { err -= dy; x += sx; }
            if (stepY) { err += dx; y += sy; }
        }
        return true;
    }
    /* A* on the 8-connected grid. Returns world-space waypoints, or null. */
    public static List<Pt> navFindPath(double sx, double sy, double tx, double ty)
    {
        if (NAV.blocked == null) return null;
        var sc0 = navCell(sx, sy); var tc0 = navCell(tx, ty);
        var sc1 = navNearestOpen(sc0.ix, sc0.iy); var tc1 = navNearestOpen(tc0.ix, tc0.iy);
        if (sc1 == null || tc1 == null) return null;
        var sc = sc1.Value; var tc = tc1.Value;
        int start = navIdx(sc.ix, sc.iy), goal = navIdx(tc.ix, tc.iy);
        if (start == goal) return new List<Pt> { new Pt(tx, ty) };
        var stamp = ++navStamp;
        double h(int ix, int iy)
        {
            double ddx = System.Math.Abs(ix - tc.ix), ddy = System.Math.Abs(iy - tc.iy);
            return (ddx + ddy) + (Math.SQRT2 - 2) * Math.min(ddx, ddy);   /* octile */
        }
        /* a plain binary heap, rebuilt per search — these searches stay small */
        var heap = new List<int> { start };
        navSeen[start] = stamp; navG[start] = 0; navF[start] = (float)h(sc.ix, sc.iy); navFrom[start] = -1;
        var closed = new HashSet<int>();
        void push(int i)
        {
            heap.Add(i);
            int c = heap.Count - 1;
            while (c > 0) { int p = (c - 1) >> 1; if (navF[heap[p]] <= navF[heap[c]]) break; (heap[p], heap[c]) = (heap[c], heap[p]); c = p; }
        }
        int pop()
        {
            int top = heap[0], last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            if (heap.Count > 0)
            {
                heap[0] = last; int c = 0;
                for (; ; )
                {
                    int l = 2 * c + 1, r2 = l + 1, m = c;
                    if (l < heap.Count && navF[heap[l]] < navF[heap[m]]) m = l;
                    if (r2 < heap.Count && navF[heap[r2]] < navF[heap[m]]) m = r2;
                    if (m == c) break; (heap[m], heap[c]) = (heap[c], heap[m]); c = m;
                }
            }
            return top;
        }
        int expanded = 0;
        while (heap.Count > 0)
        {
            var cur = pop();
            if (closed.Contains(cur)) continue;
            closed.Add(cur);
            if (cur == goal) break;
            if (++expanded > 12000) return null;         /* give up rather than stall a frame */
            int cx = cur % NAV.w, cy = cur / NAV.w;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= NAV.w || ny >= NAV.h) continue;
                    var ni = navIdx(nx, ny);
                    if (NAV.blocked[ni] == 1 || closed.Contains(ni)) continue;
                    /* no cutting diagonally through a corner */
                    if (dx != 0 && dy != 0 && (navBlk(navIdx(cx + dx, cy)) || navBlk(navIdx(cx, cy + dy)))) continue;
                    var step = (dx != 0 && dy != 0) ? Math.SQRT2 : 1;
                    double ng = navG[cur] + step;
                    if (navSeen[ni] != stamp || ng < navG[ni])
                    {
                        navSeen[ni] = stamp; navG[ni] = (float)ng; navFrom[ni] = cur;
                        navF[ni] = (float)(ng + h(nx, ny));
                        push(ni);
                    }
                }
        }
        if (navSeen[goal] != stamp) return null;
        var outp = new List<Pt>();
        for (int i = goal; i != -1 && outp.Count < 400; i = navFrom[i])
            outp.Add(new Pt(navCX(i % NAV.w), navCY(i / NAV.w)));
        outp.Reverse();
        outp[outp.Count - 1] = new Pt(tx, ty);              /* finish on the real target */
        return outp;
    }
    /* Slide around a solid rather than pushing into it. The side is chosen once and
       held for half a second, which is what stops the back-and-forth wiggle: without
       that the cross-product flips as the body drifts and the monster shuffles in
       place instead of committing to a way round. */
    /* Everything a body has to walk around, as circles. A lake contributes the point
       on its rim closest to the body — that is what the collision code pushes against,
       so it is what the steering has to slide along. Without the lakes in here a
       monster walking a shoreline was stopped dead every frame with no tangent to
       follow, and the two faces of a corner took turns shoving it back: the shuffle. */
    public static List<Obj> nearBlockers(Obj e, double reach)
    {
        var outp = new List<Obj>();
        foreach (var so in solids)
        {
            if (Math.abs(so.x - e.x) > reach + so.r || Math.abs(so.y - e.y) > reach + so.r) continue;
            outp.Add(so);
        }
        foreach (var w in waters)
        {
            if (e.x < w.x - reach || e.x > w.x + w.w + reach || e.y < w.y - reach || e.y > w.y + w.h + reach) continue;
            double qx = clamp(e.x, w.x, w.x + w.w), qy = clamp(e.y, w.y, w.y + w.h);
            /* inside the lake there is no sensible tangent — collideWorld will eject it */
            if (qx == e.x && qy == e.y) continue;
            outp.Add(new Pt(qx, qy) { r = 0 });
        }
        return outp;
    }
    public static Pt slideAroundSolids(Ent e, double vx, double vy, double dt)
    {
        var sp = Math.hypot(vx, vy);
        if (sp < 1) return null;
        double wx = vx / sp, wy = vy / sp;               /* where it WANTS to go */
        double nx = wx, ny = wy; bool touched = false;
        foreach (var so in nearBlockers(e, e.r + 10 + sp * 0.35))
        {
            double dx = so.x - e.x, dy = so.y - e.y;
            var d = Math.hypot(dx, dy);
            if (d < 0.001) continue;
            var rr = so.r + e.r + 6;
            var look = rr + sp * 0.35;
            if (d > look) continue;
            var ahead = (dx * nx + dy * ny) / d;       /* is it in front of us at all? */
            if (ahead < 0.2) continue;
            touched = true;
            e.slideT = e.slideT - dt;
            if (e.slideT <= 0)
            {
                var cross = nx * dy - ny * dx;
                e.slideSign = cross > 0 ? -1 : 1;      /* pick a side and keep it */
                e.slideT = 0.55;
            }
            /* A SLIDE NEVER TURNS IT ROUND. The side is held for half a second so it
               does not dither, but a side picked while it wanted one thing can point
               straight back the way it came once it wants another -- a brute lunging
               up past a lake's corner was slid DOWN, walked back, wound up, and was slid
               down again, forever. Held or not, the side is the one that still goes
               forward. */
            double tx = -dy / d * e.slideSign, ty = dx / d * e.slideSign;
            if (tx * wx + ty * wy < 0) { e.slideSign = -e.slideSign; tx = -tx; ty = -ty; }
            var k = clamp((look - d) / Math.max(1, rr * 0.75), 0, 1);
            nx = nx * (1 - k) + tx * k; ny = ny * (1 - k) + ty * k;
            var n2 = Js.Or(Math.hypot(nx, ny), 1); nx /= n2; ny /= n2;
        }
        if (!touched) { e.slideT = 0; return null; }
        return new Pt(nx * sp, ny * sp);
    }

    /* THE WAY OUT OF A PINCH. The nav grid only marks a cell when its CENTRE is
       inside something, which is right — a 14px sapling must not wall off 144px of
       ground — but it means the grid has never heard of the gap between two rocks
       that a body can wedge itself into, or the inside corner of a lake. When a body
       stops moving and is not in the grid's padded band, this is what it does: step
       out along the sum of the normals of everything crowding it, turned a little to
       one side so it walks AROUND rather than straight back the way it came. */
    public static Pt openWayOut(Ent e)
    {
        double ox = 0, oy = 0; int n = 0;
        foreach (var b in nearBlockers(e, e.r + 48))
        {
            double dx = e.x - b.x, dy = e.y - b.y;
            var d = Js.Or(Math.hypot(dx, dy), 1);
            var w = 1 / Math.max(6, d - b.r);     /* the closest thing pushes hardest */
            ox += dx / d * w; oy += dy / d * w; n++;
        }
        if (n == 0) return null;
        var l = Math.hypot(ox, oy);
        if (l < 1e-6) return null;
        var a = Math.atan2(oy / l, ox / l) + Js.Or(e.slideSign, 1) * 0.7;
        return new Pt(e.x + Math.cos(a) * NAV.cs, e.y + Math.sin(a) * NAV.cs);
    }
    /* =============================== THE BUCKET GRID ===============================
       Every spell in the game walked the whole enemy list to find what it hit. With
       three hundred bodies and a hundred projectiles in the air that is thirty
       thousand distance checks a frame, and it showed as a stutter whenever a wave
       got big. The enemies go into coarse buckets once a frame instead, and a spell
       asks only for the buckets it actually overlaps. */
    public static readonly EGridT EGRID = new();
    public static void egridBuild()
    {
        int w = (int)Math.ceil(W.w / EGRID.cs), h = (int)Math.ceil(W.h / EGRID.cs), n = w * h;
        if (EGRID.cells == null || EGRID.w != w || EGRID.h != h)
        {
            EGRID.w = w; EGRID.h = h;
            EGRID.cells = new List<Ent>[n];
            for (int i = 0; i < n; i++) EGRID.cells[i] = new List<Ent>();
        }
        else
        {
            for (int i = 0; i < n; i++) if (EGRID.cells[i].Count > 0) EGRID.cells[i].Clear();
        }
        foreach (var e in enemies)
        {
            if (e.dead || e.state != "active") continue;
            int ix = (int)clamp(Math.floor(e.x / EGRID.cs), 0, w - 1);
            int iy = (int)clamp(Math.floor(e.y / EGRID.cs), 0, h - 1);
            EGRID.cells[iy * w + ix].Add(e);
        }
    }
    /* Everything within roughly `r` of (x,y). The caller still does the real
       distance test — this only narrows the field. `out` is reused so a per-frame
       query does not allocate. (A caller that walks the result while something it
       calls asks again sees the buffer refilled, as in the JS: walk it with .Live(),
       or copy it with .ToList() where the JS did .slice().) */
    public static readonly List<Ent> _nearBuf = new();
    public static List<Ent> enemiesNear(double x, double y, double r, List<Ent> outBuf = null)
    {
        var res = outBuf ?? _nearBuf;
        res.Clear();
        if (EGRID.cells == null)
        {
            foreach (var e in enemies) res.Add(e);
            return res;
        }
        double cs = EGRID.cs; int w = EGRID.w, h = EGRID.h;
        int x0 = (int)clamp(Math.floor((x - r) / cs), 0, w - 1), x1 = (int)clamp(Math.floor((x + r) / cs), 0, w - 1);
        int y0 = (int)clamp(Math.floor((y - r) / cs), 0, h - 1), y1 = (int)clamp(Math.floor((y + r) / cs), 0, h - 1);
        for (int iy = y0; iy <= y1; iy++)
        {
            var row = iy * w;
            for (int ix = x0; ix <= x1; ix++)
            {
                var b = EGRID.cells[row + ix];
                for (int k = 0; k < b.Count; k++) res.Add(b[k]);
            }
        }
        return res;
    }
    /* THE FLOW FIELD. Every monster in the game is after the same hero, so instead of
       each asking for its own A* path (fourteen a frame, cached, and a body whose turn
       had not come walked straight into the lake meanwhile), ONE search runs outward
       from the hero's cell and every open cell learns how far it is from the hero,
       going round everything. A monster then only has to walk downhill. It is redone
       when the hero changes cell (and at most every 0.12s), so it is always about
       where the hero IS, and a field of five hundred costs the same as a field of one. */
    public static readonly FlowT FLOW = new();
    public const double FLOW_INF = 1e9;
    public static void flowBuild(double tx, double ty)
    {
        int W2 = NAV.w, H2 = NAV.h; var dist = FLOW.dist; var blk = NAV.blocked;
        var heap = FLOW.heap;
        Array.Fill(dist, (float)FLOW_INF);
        var t0 = navCell(tx, ty); var t = navNearestOpen(t0.ix, t0.iy);
        FLOW.ix = t0.ix; FLOW.iy = t0.iy; FLOW.gen = NAV.gen; FLOW.built++;
        if (t == null) return;
        /* a binary heap of cell indices keyed on dist; stale entries are skipped on pop */
        int n = 0;
        void push(int i)
        {
            if (n >= heap.Length) { var h2 = new int[heap.Length * 2]; Array.Copy(heap, h2, heap.Length); heap = FLOW.heap = h2; }
            int c = n++; heap[c] = i;
            while (c > 0) { int q = (c - 1) >> 1; if (dist[heap[q]] <= dist[heap[c]]) break; (heap[q], heap[c]) = (heap[c], heap[q]); c = q; }
        }
        int pop()
        {
            int top = heap[0]; heap[0] = heap[--n]; int c = 0;
            for (; ; )
            {
                int l = 2 * c + 1, r2 = l + 1, m = c;
                if (l < n && dist[heap[l]] < dist[heap[m]]) m = l;
                if (r2 < n && dist[heap[r2]] < dist[heap[m]]) m = r2;
                if (m == c) break; (heap[m], heap[c]) = (heap[c], heap[m]); c = m;
            }
            return top;
        }
        var g = t.Value.iy * W2 + t.Value.ix;
        dist[g] = 0; push(g);
        if (FLOW.done == null || FLOW.done.Length != W2 * H2) FLOW.done = new byte[W2 * H2];
        var dn = FLOW.done; Array.Clear(dn);
        while (n > 0)
        {
            var cur = pop();
            if (dn[cur] != 0) continue;
            dn[cur] = 1;
            int cx = cur % W2, cy = cur / W2; double dc = dist[cur];
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= W2 || ny >= H2) continue;
                    var ni = ny * W2 + nx;
                    if (blk[ni] == 1 || dn[ni] != 0) continue;
                    if (dx != 0 && dy != 0 && (blk[cy * W2 + nx] == 1 || blk[ny * W2 + cx] == 1)) continue;
                    var nd = dc + (dx != 0 && dy != 0 ? 1.4142 : 1);
                    if (nd < dist[ni]) { dist[ni] = (float)nd; push(ni); }
                }
        }
    }
    /* keep the field about the hero where the hero is now */
    public static void flowFresh(double dt)
    {
        if (NAV.blocked == null || FLOW.dist == null || heroObj == null) return;
        FLOW.t -= dt;
        var c = navCell(heroObj.x, heroObj.y);
        if (FLOW.gen != NAV.gen || ((c.ix != FLOW.ix || c.iy != FLOW.iy) && FLOW.t <= 0))
        {
            flowBuild(heroObj.x, heroObj.y); FLOW.t = 0.12;
        }
    }
    /* Does a body of radius r walking the segment cross a lake? The grid's sight
       line runs cell centre to cell centre, and a cell can be open with its edge
       against the water -- a lancer on a lake's corner saw straight past it and
       walked into the shore forever. This is the real shape, checked only against
       the lakes (the round things are what the local steering slides round). */
    public static bool segHitsWater(double x1, double y1, double x2, double y2, double r)
    {
        double dx = x2 - x1, dy = y2 - y1;
        foreach (var w in waters)
        {
            double L = w.x - r, R = w.x + w.w + r, T = w.y - r, B = w.y + w.h + r;
            if (Math.max(x1, x2) < L || Math.min(x1, x2) > R || Math.max(y1, y2) < T || Math.min(y1, y2) > B) continue;
            double t0 = 0, t1 = 1;
            if (Math.abs(dx) < 1e-9) { if (x1 < L || x1 > R) continue; }
            else
            {
                double a = (L - x1) / dx, b = (R - x1) / dx; if (a > b) (a, b) = (b, a);
                t0 = Math.max(t0, a); t1 = Math.min(t1, b); if (t0 > t1) continue;
            }
            if (Math.abs(dy) < 1e-9) { if (y1 < T || y1 > B) continue; }
            else
            {
                double a = (T - y1) / dy, b = (B - y1) / dy; if (a > b) (a, b) = (b, a);
                t0 = Math.max(t0, a); t1 = Math.min(t1, b); if (t0 > t1) continue;
            }
            return true;
        }
        return false;
    }
    /* the far end of the downhill walk that is still in plain sight of `e` */
    public static Pt flowAim(Ent e)
    {
        var dist = FLOW.dist; int W2 = NAV.w, H2 = NAV.h; var blk = NAV.blocked;
        var c0 = navCell(e.x, e.y);
        int ix = c0.ix, iy = c0.iy; Pt best = null;
        if (dist[iy * W2 + ix] >= FLOW_INF)
        {
            /* standing in the padded band round something: the open neighbour nearest home */
            double bd = FLOW_INF; int bx = -1, by = -1;
            for (int r = 1; r <= 3 && bx < 0; r++)
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = ix + dx, ny = iy + dy;
                        if (nx < 0 || ny < 0 || nx >= W2 || ny >= H2) continue;
                        var d = dist[ny * W2 + nx] + Math.hypot(dx, dy);
                        if (d < bd) { bd = d; bx = nx; by = ny; }
                    }
            if (bx < 0) return null;                       /* cut off from the hero entirely */
            return new Pt(navCX(bx), navCY(by));
        }
        for (int step = 0; step < 10; step++)
        {
            double bd = dist[iy * W2 + ix]; int nx0 = -1, ny0 = -1;
            if (bd == 0) break;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = ix + dx, ny = iy + dy;
                    if (nx < 0 || ny < 0 || nx >= W2 || ny >= H2) continue;
                    if (dx != 0 && dy != 0 && (blk[iy * W2 + nx] == 1 || blk[ny * W2 + ix] == 1)) continue;
                    double d = dist[ny * W2 + nx];
                    if (d < bd) { bd = d; nx0 = nx; ny0 = ny; }
                }
            if (nx0 < 0) break;
            ix = nx0; iy = ny0;
            double px = navCX(ix), py = navCY(iy);
            if (step < 1 || (navLineOpen(e.x, e.y, px, py) && !segHitsWater(e.x, e.y, px, py, Js.Or(e.r, 12) * 0.8))) best = new Pt(px, py);
            else break;
        }
        return best;
    }
}
