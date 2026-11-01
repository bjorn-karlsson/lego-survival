// Port of lego-survival.html lines 17930-18277: THE MAP (TAB) overlay with its
// fog of war, the RPG's header, labels, use prompt and world props, the
// difficulty row, and how the test bench files and tiers its bricks.
using System.Text.RegularExpressions;

namespace Brickblade;

public sealed class BenchSection { public string name; public List<string> ids; }

public static partial class G
{
    /* ---- THE MAP (TAB) ----
       PoE's overlay: TAB lays the map over the whole field, centred on you, drawn
       in lines and dots so it covers nothing you need to see -- and it takes no
       clicks. How solid and how big it is are in the options. Out in an RPG zone
       it fills in as you walk (MAP_REVEAL around you); the town and the arcade's
       arena are known from the start. */
    public const double MAP_CELL = 80, MAP_REVEAL = 960, MAP_K = 0.16, MAP_BASE = 0.15, MAP_NEAR = 1400;
    public static bool mapOn = false, mapDirty = true;
    public static byte[] mapSeen = null; public static string mapKey = null;
    public static OffCanvas mapArtCv = null, mapMaskCv = null, mapCompCv = null;
    public static double mapRevealT = 0;
    public static bool mapToggle() { mapOn = !mapOn; try { SFX.click(); } catch { } return mapOn; }
    public static bool mapFogged() => rpgOn() && RPG.zone != "town";
    public static OffCanvas mapCanvas() { var c = new OffCanvas(); c.width = (int)Math.Ceiling(W.w * MAP_K); c.height = (int)Math.Ceiling(W.h * MAP_K); return c; }
    public static void mapEnsure()
    {
        string key = (rpgOn() ? RPG.zone : "arcade") + ":" + S(seed) + ":" + S(caveMap != null ? caveMap.seed : 0);
        if (key != mapKey) { mapKey = key; mapSeen = null; mapArtCv?.Free(); mapArtCv = null; }
        if (mapSeen == null)
        {
            int C = (int)Math.Ceiling(W.w / MAP_CELL), R = (int)Math.Ceiling(W.h / MAP_CELL);
            mapSeen = new byte[C * R]; mapMaskCv?.Free(); mapCompCv?.Free(); mapMaskCv = mapCanvas(); mapCompCv = mapCanvas(); mapDirty = true;
            if (!mapFogged()) { Array.Fill(mapSeen, (byte)1); var g = mapMaskCv.getContext("2d"); g.fillStyle = "#fff"; g.fillRect(0, 0, mapMaskCv.width, mapMaskCv.height); }
        }
        if (mapArtCv == null) { mapArtCv = mapPaint(); mapDirty = true; }
    }
    static readonly Regex MAP_MARKER = new("^r(stash|board|waypoint|cave|portal|exit)$");
    /* the still part of the map: the ground you can stand on, its edges, water, what blocks */
    public static OffCanvas mapPaint()
    {
        var c = mapCanvas(); var g = c.getContext("2d");
        g.scale(MAP_K, MAP_K); g.lineJoin = "round"; g.lineCap = "round";
        if (caveMap != null && rpgOn() && RPG.zone == "cave")
        {
            if (caveArt == null || caveArt.M != caveMap) caveArt = caveArtBuild(caveMap);
            g.fillStyle = "rgba(12,10,26,0.42)"; g.fill(caveArt.floor, "evenodd");
            g.strokeStyle = "rgba(0,0,0,0.7)"; g.lineWidth = 34; g.stroke(caveArt.floor);
            g.strokeStyle = "rgba(228,218,255,0.95)"; g.lineWidth = 14; g.stroke(caveArt.floor);
        }
        else
        {
            double w = W.wall;
            g.fillStyle = "rgba(10,16,24,0.34)"; g.fillRect(w, w, W.w - 2 * w, W.h - 2 * w);
            g.strokeStyle = "rgba(0,0,0,0.7)"; g.lineWidth = 34; g.strokeRect(w, w, W.w - 2 * w, W.h - 2 * w);
            g.strokeStyle = "rgba(232,232,220,0.95)"; g.lineWidth = 14; g.strokeRect(w, w, W.w - 2 * w, W.h - 2 * w);
            foreach (var r in waters) if (!r.cave) { g.fillStyle = "rgba(0,0,0,0.6)"; g.fillRect(r.x - 10, r.y - 10, r.w + 20, r.h + 20); }
            g.fillStyle = "rgba(90,160,245,0.9)";
            foreach (var r in waters) if (!r.cave) g.fillRect(r.x, r.y, r.w, r.h);
        }
        foreach (var q in props)
        {
            if (q.dead || !q.solid || !solids.Contains(q)) continue;
            if (q.type == "rwall" || q.type == "rhouse")
            {
                double r = Math.Max(q.r, 30);
                g.fillStyle = "rgba(0,0,0,0.7)"; g.fillRect(q.x - r - 10, q.y - r - 10, r * 2 + 20, r * 2 + 20);
                g.fillStyle = "rgba(240,215,170,0.95)"; g.fillRect(q.x - r, q.y - r, r * 2, r * 2); continue;
            }
            if (q.type == "rgate" || MAP_MARKER.IsMatch(q.type ?? "")) continue;     /* drawn live, as markers */
            g.fillStyle = "rgba(0,0,0,0.65)"; g.beginPath(); g.arc(q.x, q.y, Math.Max(22, q.r) + 9, 0, TAU); g.fill();
            g.fillStyle = "rgba(215,215,205,0.9)"; g.beginPath(); g.arc(q.x, q.y, Math.Max(22, q.r), 0, TAU); g.fill();
        }
        return c;
    }
    /* fill in what you have walked near */
    public static void mapReveal(bool force = false)
    {
        if (mapSeen == null || player == null || !mapFogged()) return;
        mapRevealT -= Js.Or(frameRaw, 1.0 / 60); if (mapRevealT > 0 && !force) return; mapRevealT = 0.12;
        int C = (int)Math.Ceiling(W.w / MAP_CELL), R = (int)Math.Ceiling(W.h / MAP_CELL); var p = player; int n = (int)Math.Ceiling(MAP_REVEAL / MAP_CELL);
        int cx = (int)Math.Floor(p.x / MAP_CELL), cy = (int)Math.Floor(p.y / MAP_CELL); var g = mapMaskCv.getContext("2d");
        g.fillStyle = "#fff"; double s = MAP_CELL * MAP_K;
        for (int y = Math.Max(0, cy - n); y <= Math.Min(R - 1, cy + n); y++)
            for (int x = Math.Max(0, cx - n); x <= Math.Min(C - 1, cx + n); x++)
            {
                int i = y * C + x; if (mapSeen[i] != 0) continue;
                if (hypot((x + 0.5) * MAP_CELL - p.x, (y + 0.5) * MAP_CELL - p.y) > MAP_REVEAL) continue;
                mapSeen[i] = 1; g.fillRect(x * s - 0.5, y * s - 0.5, s + 1, s + 1); mapDirty = true;
            }
    }
    public static bool mapSeenAt(double x, double y)
    {
        if (mapSeen == null) return false;
        int C = (int)Math.Ceiling(W.w / MAP_CELL), cx = (int)Math.Floor(x / MAP_CELL), cy = (int)Math.Floor(y / MAP_CELL);
        int i = cy * C + cx;
        return cx >= 0 && cy >= 0 && cx < C && i < mapSeen.Length && mapSeen[i] != 0;
    }
    public static double mapScale() => MAP_BASE * clamp(Js.Or(OPT.mapSize, 100), 50, 200) / 100;
    public static void drawMapOverlay()
    {
        if (player == null || state == "title" || W == null) return;
        mapEnsure(); mapReveal();
        if (!mapOn || !(state == "play" || state == "pause" || state == "book")) return;
        if (mapDirty)
        {
            var g = mapCompCv.getContext("2d");
            g.globalCompositeOperation = "source-over"; g.clearRect(0, 0, mapCompCv.width, mapCompCv.height);
            g.drawImage(mapMaskCv, 0, 0); g.globalCompositeOperation = "source-in"; g.drawImage(mapArtCv, 0, 0);
            g.globalCompositeOperation = "source-over"; mapDirty = false;
        }
        double s = mapScale(); var p = player;
        double psx = (p.x - cam.x) * zoom + cw / 2, psy = (p.y - cam.y) * zoom + ch / 2, ox = psx - p.x * s, oy = psy - p.y * s;
        (double, double) at(double x, double y) => (ox + x * s, oy + y * s);
        ctx.save(); ctx.globalAlpha = clamp(Js.Or(OPT.mapAlpha, 70), 10, 100) / 100;
        ctx.imageSmoothingEnabled = true;
        ctx.drawImage(mapCompCv, ox, oy, W.w * s, W.h * s);
        void dot(double x, double y, double r, string col, string ring = null)
        {
            var (sx, sy) = at(x, y); ctx.fillStyle = col; ctx.beginPath(); ctx.arc(sx, sy, r, 0, TAU); ctx.fill();
            if (ring != null) { ctx.strokeStyle = ring; ctx.lineWidth = 1.5; ctx.stroke(); }
        }
        void box(double x, double y, double r, string col)
        {
            var (sx, sy) = at(x, y); ctx.fillStyle = col; ctx.fillRect(sx - r, sy - r, r * 2, r * 2);
            ctx.strokeStyle = "rgba(10,10,15,0.8)"; ctx.lineWidth = 1; ctx.strokeRect(sx - r, sy - r, r * 2, r * 2);
        }
        /* the places: portals and waypoints, the stash, the board, the gate, the way out, chests */
        foreach (var q in props)
        {
            if (q.dead || !mapSeenAt(q.x, q.y)) continue;
            if (q.type == "rportal" || q.type == "rwaypoint")
            {
                var (sx, sy) = at(q.x, q.y);
                string sc = q.type == "rportal" ? "#ffd76e" : "#8fd4ff";
                ctx.strokeStyle = sc; ctx.lineWidth = 2.5; ctx.beginPath(); ctx.arc(sx, sy, 6, 0, TAU); ctx.stroke();
                ctx.fillStyle = sc; ctx.beginPath(); ctx.arc(sx, sy, 2.5, 0, TAU); ctx.fill();
            }
            else if (q.type == "rstash") box(q.x, q.y, 5, "#ffd76e");
            else if (q.type == "rboard") box(q.x, q.y, 4.5, "#c9905a");
            else if (q.type == "rcave") box(q.x, q.y, 5, "#c9a6ff");
            else if (q.type == "rexit") { var (sx, sy) = at(q.x, q.y); ctx.fillStyle = "#a5e06e"; ctx.beginPath(); ctx.moveTo(sx - 7, sy); ctx.lineTo(sx + 3, sy - 6); ctx.lineTo(sx + 3, sy + 6); ctx.closePath(); ctx.fill(); }
            else if (q.type == "rgate") { var (sx, sy) = at(q.x, q.y); ctx.fillStyle = q.open ? "#a5e06e" : "#ff5a4a"; ctx.fillRect(sx - 2.5, sy - q.r * s, 5, q.r * s * 2); }
            else if (q.type == "chest") box(q.x, q.y, 3.5, "#ffd76e");
            else if (q.type == "strongbox") box(q.x, q.y, 4, "#fe8a18");
        }
        foreach (var d in gearDrops)
            if (mapSeenAt(d.x, d.y))
            {
                var (sx, sy) = at(d.x, d.y);
                ctx.fillStyle = "#ffb45a"; ctx.beginPath(); ctx.moveTo(sx, sy - 4); ctx.lineTo(sx + 4, sy); ctx.lineTo(sx, sy + 4); ctx.lineTo(sx - 4, sy); ctx.closePath(); ctx.fill();
            }
        /* the monsters near you, as PoE shows them: red, blue for magic, yellow for rares */
        foreach (var e in enemies)
        {
            if (e.dead || e.state != "active" || !mapSeenAt(e.x, e.y)) continue;
            bool boss = e.rpgBoss || e.boss;
            if (!boss && hypot(e.x - p.x, e.y - p.y) > MAP_NEAR) continue;
            double a = e.dormant ? 0.55 : 1;
            ctx.globalAlpha *= a;
            if (boss) dot(e.x, e.y, 6, "#ff6ad5", "rgba(20,0,20,0.9)");
            else if (e.elite) dot(e.x, e.y, 4.5, "#ffd23a", "rgba(30,20,0,0.85)");
            else if (e.magic) dot(e.x, e.y, 3.8, "#6aa8ff", "rgba(0,10,30,0.8)");
            else dot(e.x, e.y, 3.2, "#ff4a3a", "rgba(30,0,0,0.8)");
            ctx.globalAlpha /= a;
        }
        foreach (var m in minions) if (!m.dead) dot(m.x, m.y, 2, "#7fe07a");
        /* and you */
        dot(p.x, p.y, 4.5, "#ffd76e", "rgba(20,15,5,0.95)");
        ctx.restore();
    }

    /* ---- DRAWN: the header that stands where the wave number does, and the use prompt ---- */
    public static void drawRpgHud()
    {
        if (!rpgOn()) return;
        var Z = RPG_ZONES[RPG.zone];
        ctx.textAlign = "center"; ctx.font = "700 20px \"Titan One\"";
        ctx.strokeStyle = "rgba(15,20,25,0.8)"; ctx.lineWidth = 4;
        ctx.strokeText(Z.name, cw / 2, 32); ctx.fillStyle = Z.col; ctx.fillText(Z.name, cw / 2, 32);
        ctx.font = "600 13px Fredoka"; ctx.fillStyle = "#c9d2ce";
        string line;
        if (RPG.zone == "outskirts")
        {
            int awake = enemies.Count(e => !e.dead && !e.dormant && e.state == "active");
            line = RPG.gateOpen ? "THE GATE IS OPEN — walk east into Brickhaven"
              : (RPG.boss != null && !RPG.boss.dormant ? "THE GATEKEEPER — break it to open the gate"
              : "monster level " + S(mobLevelRpg()) + " · the town is east, past the gate" + (awake > 0 ? " · " + awake + " awake" : ""));
        }
        else if (RPG.zone == "cave" && caveMap != null)
        {
            var M = caveMap; int awake = enemies.Count(e => !e.dead && !e.dormant && e.state == "active");
            line = "NOT CANON · cellular automata: seed " + S(M.seed) + " · fill " + S(Math.Round(M.fill * 100)) + "% · " + CAVE_STEPS + " passes · " +
              S(Math.Round(M.open * 100)) + "% open" + (M.tries > 1 ? " · try " + M.tries : "") + " · " + (RPG.boss != null && !RPG.boss.dead ? "the Warden waits at the far end" : "the Warden is down") +
              (awake > 0 ? " · " + awake + " awake" : "");
        }
        else line = "safe ground · F at the stash, the board or the waypoint · the gate west leads out";
        ctx.fillText(line, cw / 2, 52);
    }
    public static void drawRpgWorld(Ctx c)
    {
        if (!rpgOn()) return;
        var q = RPG.near;
        foreach (var p in props)
        {
            if (!Js.T(p.label) || p == q || !RPG_USES.ContainsKey(p.type ?? "") && !p.home) continue;
            if (hypot(p.x - player.x, p.y - player.y) > 520) continue;
            c.font = "700 13px \"Titan One\""; c.textAlign = "center";
            c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 4;
            double y = p.y - (p.type == "rhouse" ? p.r * 1.55 : p.r + 40);
            c.strokeText(p.label, p.x, y); c.fillStyle = p == q ? "#ffd76e" : "#f2f3f2"; c.fillText(p.label, p.x, y);
        }
        if (q != null)
        {
            double y = q.y - (q.type == "rhouse" ? q.r * 1.55 : q.r + 40) - 20;
            c.font = "700 12px Fredoka"; c.textAlign = "center";
            string t = "F · " + (RPG_USES.GetValueOrDefault(q.type ?? "") ?? "USE");
            double w = c.measureText(t).width + 16;
            c.fillStyle = "rgba(12,16,20,0.75)"; roundRect(c, q.x - w / 2, y - 13, w, 19, 6); c.fill();
            c.strokeStyle = "#ffd76e"; c.lineWidth = 1.4; roundRect(c, q.x - w / 2, y - 13, w, 19, 6); c.stroke();
            c.fillStyle = "#ffd76e"; c.fillText(t, q.x, y + 1);
        }
        /* the Gatekeeper's name */
        var b = RPG.boss;
        if (b != null && !b.dead)
        {
            c.font = "700 14px \"Titan One\""; c.textAlign = "center";
            c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 4;
            double y = b.y - 70 * Js.Or(b.scale, 1);
            c.strokeText(b.rpgName, b.x, y); c.fillStyle = "#ff8a4a"; c.fillText(b.rpgName, b.x, y);
        }
    }
    /* the world's own pieces: a palisade post, the gate, a house, the stash, the
       board, the waypoint, the way out */
    public static void drawRpgProp(Ctx c, Prop p)
    {
        const string OL = "rgba(20,30,40,0.55)";
        if (p.type == "rwall")
        {
            var wood = sh("#8a5a3a", (hash2(Js.I32(p.seed), 3) - 0.5) * 0.16);
            c.strokeStyle = OL; c.lineWidth = 2;
            for (int i = 0; i < 3; i++)
            {
                c.fillStyle = sh(wood, i != 0 ? 0.05 * i : -0.08);
                roundRect(c, -p.r * 0.8, -10 - i * 16, p.r * 1.6, 17, 3); c.fill(); c.stroke();
            }
            c.fillStyle = sh(wood, 0.12);
            c.beginPath(); c.moveTo(-p.r * 0.8, -42); c.lineTo(0, -56); c.lineTo(p.r * 0.8, -42); c.closePath(); c.fill(); c.stroke();
        }
        else if (p.type == "rgate")
        {
            double h = p.r;
            c.strokeStyle = OL; c.lineWidth = 2.5;
            foreach (var s in new[] { -1, 1 })
            {
                c.fillStyle = "#6d4a2e"; roundRect(c, -14, s * h - 12, 28, 24, 4); c.fill(); c.stroke();
                c.fillStyle = "#5a3a24"; roundRect(c, -11, s * h - 70, 22, 60, 3); c.fill(); c.stroke();
                drawStudG(c, 0, s * h - 70, 4, "#6d4a2e");
            }
            if (!p.open)
            {
                /* two doors of planks across the gap, banded in iron, barred in the middle */
                foreach (var s in new[] { -1, 1 })
                {
                    c.fillStyle = "#a06a40";
                    roundRect(c, -14, s < 0 ? -h + 10 : 2, 28, h - 12, 3); c.fill(); c.stroke();
                    c.strokeStyle = "rgba(60,35,20,0.55)"; c.lineWidth = 1.5;
                    for (int k = 1; k < 3; k++) { c.beginPath(); c.moveTo(-14 + k * 9.3, s < 0 ? -h + 12 : 4); c.lineTo(-14 + k * 9.3, s < 0 ? -2 : h - 12); c.stroke(); }
                    c.strokeStyle = OL; c.lineWidth = 2.5;
                    c.fillStyle = "#4a4f55";
                    roundRect(c, -16, s * h * 0.62 - 4, 32, 8, 2); c.fill(); c.stroke();
                }
                c.fillStyle = "#5a3a24"; roundRect(c, -20, -6, 40, 12, 3); c.fill(); c.stroke();
                drawStudG(c, 0, 0, 3.5, "#5a3a24");
            }
            else
            {
                c.fillStyle = "rgba(255,215,110,0.18)"; c.beginPath(); c.ellipse(26, 0, 40, h * 0.9, 0, 0, TAU); c.fill();
            }
        }
        else if (p.type == "rhouse")
        {
            double r = p.r, w = r * 2, hh = r * 1.05; string col = Js.T(p.col) ? p.col : "#c8553d";
            c.strokeStyle = OL; c.lineWidth = 2.5;
            c.fillStyle = sh("#d9d2c2", -0.08); roundRect(c, -r, -hh, w, hh + 8, 4); c.fill(); c.stroke();
            for (int i = 0; i < 3; i++) { c.fillStyle = sh("#d9d2c2", i % 2 != 0 ? 0.04 : -0.02); c.fillRect(-r + 4, -hh + 6 + i * hh / 3, w - 8, hh / 3 - 4); }
            c.fillStyle = "#5a3a24"; roundRect(c, -r * 0.18, -hh * 0.62, r * 0.36, hh * 0.62 + 8, 3); c.fill(); c.stroke();
            c.fillStyle = "#9fd4ff"; foreach (var s in new[] { -1, 1 }) { roundRect(c, s * r * 0.55 - r * 0.16, -hh * 0.72, r * 0.32, r * 0.28, 2); c.fill(); c.stroke(); }
            c.fillStyle = col;
            c.beginPath(); c.moveTo(-r - 12, -hh); c.lineTo(0, -hh - r * 0.75); c.lineTo(r + 12, -hh); c.closePath(); c.fill(); c.stroke();
            for (int i = -2; i <= 2; i++) drawStudG(c, i * r * 0.32, -hh - r * 0.75 * (1 - Math.Abs(i) / 3.0) + 8, 4.5, col);
        }
        else if (p.type == "rstash")
        {
            c.strokeStyle = OL; c.lineWidth = 2.2;
            c.fillStyle = "#7b5132"; roundRect(c, -24, -26, 48, 30, 4); c.fill(); c.stroke();
            c.fillStyle = "#ffd76e"; roundRect(c, -26, -34, 52, 12, 4); c.fill(); c.stroke();
            c.fillStyle = "#ffb900"; roundRect(c, -5, -24, 10, 12, 2); c.fill(); c.stroke();
            drawStudG(c, -14, -30, 3.5, "#ffd76e"); drawStudG(c, 14, -30, 3.5, "#ffd76e");
        }
        else if (p.type == "rboard")
        {
            c.strokeStyle = OL; c.lineWidth = 2;
            c.fillStyle = "#6d4a2e"; foreach (var s in new[] { -1, 1 }) { roundRect(c, s * 22 - 3, -40, 6, 44, 2); c.fill(); c.stroke(); }
            c.fillStyle = "#9c6a45"; roundRect(c, -30, -56, 60, 34, 3); c.fill(); c.stroke();
            c.fillStyle = "#f2ead6"; foreach (var (x, y) in new[] { (-22, -50), (-4, -52), (12, -48) }) { c.fillRect(x, y, 13, 16); }
            c.fillStyle = "#c8553d"; foreach (var (x, y) in new[] { (-16, -50), (2, -52), (18, -48) }) { c.beginPath(); c.arc(x, y, 1.8, 0, TAU); c.fill(); }
        }
        else if (p.type == "rwaypoint")
        {
            double g = 0.5 + 0.5 * Math.Sin(vtime * 2.4 + p.seed);
            c.fillStyle = "#5a6d82"; c.strokeStyle = OL; c.lineWidth = 2;
            c.beginPath(); c.ellipse(0, 0, p.r, p.r * 0.45, 0, 0, TAU); c.fill(); c.stroke();
            c.fillStyle = $"rgba(111,182,255,{S(0.35 + 0.35 * g)})";
            c.beginPath(); c.ellipse(0, -2, p.r * 0.72, p.r * 0.32, 0, 0, TAU); c.fill();
            c.strokeStyle = $"rgba(200,230,255,{S(0.5 + 0.4 * g)})"; c.lineWidth = 2;
            c.save(); c.scale(1, 0.45); c.rotate(vtime * 0.8); c.beginPath();
            for (int i = 0; i < 6; i++) { double a = i / 6.0 * TAU; c.moveTo(Math.Cos(a) * p.r * 0.3, Math.Sin(a) * p.r * 0.3); c.lineTo(Math.Cos(a) * p.r * 0.62, Math.Sin(a) * p.r * 0.62); }
            c.stroke(); c.restore();
        }
        else if (p.type == "rcave")
        {
            /* a mouth in a heap of dark stone, a TEST sign on a post */
            c.strokeStyle = OL; c.lineWidth = 2.2;
            c.fillStyle = "#4a4650";
            c.beginPath(); c.ellipse(0, -18, p.r + 12, p.r + 4, 0, Math.PI, 0); c.lineTo(p.r + 12, 0); c.lineTo(-p.r - 12, 0); c.closePath(); c.fill(); c.stroke();
            foreach (var (x, y) in new[] { (-22, -34), (0, -46), (22, -34) }) drawStudG(c, x, y, 5, "#4a4650");
            c.fillStyle = "#120f16";
            c.beginPath(); c.ellipse(0, -6, p.r * 0.55, p.r * 0.62, 0, Math.PI, 0); c.lineTo(p.r * 0.55, 0); c.lineTo(-p.r * 0.55, 0); c.closePath(); c.fill();
            double g = 0.5 + 0.5 * Math.Sin(vtime * 2 + p.seed);
            c.fillStyle = $"rgba(201,166,255,{S(0.25 + 0.3 * g)})"; c.beginPath(); c.ellipse(0, -10, p.r * 0.3, p.r * 0.36, 0, 0, TAU); c.fill();
            c.fillStyle = "#6d4a2e"; roundRect(c, p.r + 6, -30, 5, 30, 1); c.fill();
            c.fillStyle = "#ffd76e"; roundRect(c, p.r - 6, -42, 30, 14, 2); c.fill(); c.stroke();
            c.fillStyle = "#20160a"; c.font = "700 8px Fredoka"; c.textAlign = "center"; c.fillText("TEST", p.r + 9, -32);
        }
        else if (p.type == "rstalag")
        {
            /* a stalagmite: round bricks stacked narrowing up, a drip of light on its tip */
            var col = sh("#5a5266", (hash2(Js.I32(p.seed), 5) - 0.5) * 0.2); int n = 4; double r0 = p.r;
            c.fillStyle = "rgba(6,4,12,0.4)"; c.beginPath(); c.ellipse(0, 3, r0 * 1.15, r0 * 0.45, 0, 0, TAU); c.fill();
            c.strokeStyle = "rgba(15,10,20,0.6)"; c.lineWidth = 1.5;
            for (int i = 0; i < n; i++)
            {
                double rr = r0 * (1 - i * 0.2), y = -i * r0 * 0.62;
                c.fillStyle = sh(col, -0.25); c.beginPath(); c.ellipse(0, y, rr, rr * 0.42, 0, 0, Math.PI); c.lineTo(-rr, y - r0 * 0.5);
                c.ellipse(0, y - r0 * 0.5, rr, rr * 0.42, 0, Math.PI, 0, true); c.closePath(); c.fill(); c.stroke();
                c.fillStyle = sh(col, 0.08 + i * 0.04); c.beginPath(); c.ellipse(0, y - r0 * 0.5, rr, rr * 0.42, 0, 0, TAU); c.fill(); c.stroke();
            }
            double tipY = -n * r0 * 0.62 - r0 * 0.2;
            c.fillStyle = sh(col, 0.15); c.beginPath(); c.moveTo(-r0 * 0.3, tipY + r0 * 0.35); c.lineTo(0, tipY - r0 * 0.7); c.lineTo(r0 * 0.3, tipY + r0 * 0.35); c.closePath(); c.fill(); c.stroke();
            drawStudG(c, 0, -(n - 1) * r0 * 0.62 - r0 * 0.5, r0 * 0.22, col);
        }
        else if (p.type == "rportal")
        {
            double g = 0.5 + 0.5 * Math.Sin(vtime * 3 + p.seed);
            c.strokeStyle = OL; c.lineWidth = 2;
            c.fillStyle = "#5a5662"; roundRect(c, -p.r - 6, -66, 10, 66, 3); c.fill(); c.stroke(); roundRect(c, p.r - 4, -66, 10, 66, 3); c.fill(); c.stroke();
            c.fillStyle = "#6d6977"; roundRect(c, -p.r - 10, -76, p.r * 2 + 20, 12, 3); c.fill(); c.stroke();
            c.fillStyle = $"rgba(255,215,110,{S(0.35 + 0.35 * g)})";
            c.beginPath(); c.ellipse(0, -32, p.r - 6, 30, 0, 0, TAU); c.fill();
            c.strokeStyle = $"rgba(255,240,190,{S(0.5 + 0.4 * g)})"; c.lineWidth = 2;
            c.beginPath(); c.ellipse(0, -32, (p.r - 6) * 0.6, 18, vtime * 0.6, 0, TAU); c.stroke();
        }
        else if (p.type == "rexit")
        {
            double g = 0.5 + 0.5 * Math.Sin(vtime * 3 + p.seed);
            c.fillStyle = $"rgba(165,224,110,{S(0.18 + 0.2 * g)})";
            c.beginPath(); c.ellipse(0, 0, 38, p.r * 1.4, 0, 0, TAU); c.fill();
            c.fillStyle = "#a5e06e"; c.font = "700 22px \"Titan One\""; c.textAlign = "center"; c.fillText("←", 0, 8);
        }
    }
    public static void buildDiffRow()
    {
        var row = document.getElementById("diffRow");
        if (row == null) return;
        row.innerHTML = string.Join("", DIFFS.Keys.Select(id => $"<div class=\"diff\" data-diff=\"{id}\">{DIFFS[id].name}</div>"));
        foreach (var el in row.children)
            el.addEventListener("click", _ => { SFX.click(); setDifficulty(el.dataset["diff"]); });
        string saved = "hard";
        try { saved = Store.getItem("bb_diff") ?? "hard"; } catch { }
        setDifficulty(DIFFS.ContainsKey(saved) ? saved : "hard");
    }

    /* The order the bench files its bricks in: by what they are for, not by the
       order they happen to be declared. Ids that do not appear here still show up,
       under EVERYTHING ELSE, so a new brick is never invisible. */
    static BenchSection BS(string name, params string[] ids) => new BenchSection { name = name, ids = ids.ToList() };
    public static readonly List<BenchSection> BENCH_SECTIONS = new()
    {
        BS("ATTRIBUTES", "attstr", "attint", "attdex", "attall"),
        BS("CONVERSION", "convfire", "convfrost", "convshock", "convsf", "convff",
                         "convrot", "convchaos", "gainpf", "gainfrost", "gainshock",
                         "gainfire", "gainchaos", "gainrot", "incchaos", "chaosres"),
        BS("SWORD", "dmg", "aspd", "reach", "bladem", "blademaster"),
        BS("GROUND SLAM", "slampow", "slamarc", "slamrange", "slamspd", "slamelem"),
        BS("DAMAGE", "orbm", "moredmg", "banedotinc"),
        /* ELEMENTAL is the GLOBAL tree: pools every elemental source reads, wherever
           it came from. The fireball's own numbers live with the fireball. */
        BS("ELEMENTAL", "eleminc", "firinc", "froinc", "shockinc"),
        BS("CRITICALS", "crit", "critpow", "bleedchance", "bleedpow"),
        BS("SURVIVAL", "hp", "hpinc", "regen", "armor", "armorinc", "suppress", "morehp"),
        BS("RESISTANCES", "resfire", "resfrost", "resshock", "resall"),
        BS("LIFESTEAL", "leech", "leechrate"),
        BS("MOVEMENT & LOOT", "spd", "roll", "mag", "greed", "wisdom"),
        BS("GUARDIAN BRICK", "orbit", "orbitpow", "orbitring", "orbitspd", "orbitaegis", "orbittwin"),
        BS("THE BOW", "arrowmulti", "arrowpierce", "arrowflat", "arrowlight", "arrowfire", "arrowchaos"),
        BS("THE LEGION", "minionmax", "miniondmg", "minionlife", "minionspd", "minionflat", "miniontwin", "golemflame", "golemlightning", "golemice", "golemstone", "golemchaos", "golemcarrion", "boneoverseer", "raisezombie", "zombiehorde", "bonevolley", "bonegmp", "skelmages"),
        BS("STORM BRICK", "chain", "chainbrick", "chainpow", "chainfork"),
        BS("BLADESTORM", "storm", "stormpow", "stormsize", "stormrate", "stormback"),
        BS("BLADE VORTEX", "vortex", "vortexpow", "vortexarea", "vortexrate", "vortextotem"),
        BS("BOMB VOLLEY", "bombplus", "bombpow", "bombarea", "bombcd", "cluster"),
        BS("FIREBALL", "elemflat", "boltarea", "boltember"),
        BS("BLOCK FREEZE", "freeze", "freezr", "freezc", "freezf", "freezestun"),
        BS("BRICKBANE", "bane", "banearc", "baner", "banerate", "banedot", "baneburst"),
    };
    /* The bench shows one brick at one tier at a time, and SHIFT+SCROLL walks that
       tier up and down — between the brick's own minimum and the highest it can
       actually roll at, which is epic for everything except the legendary-only
       bricks. Reading a card at common and at epic side by side is the whole point
       of the bench when you are balancing numbers. */
    public static readonly JsMap<string, double> benchRar = new();
    public static (double lo, double hi) benchRange(Upgrade u)
    {
        double lo = rarRank(Js.T(u.minRar) ? u.minRar : "common");
        double hi = (u.minRar == "legendary") ? LEGENDARY_RANK : rarRank("epic");
        return (lo, Math.Max(lo, hi));
    }
    /* The bench used to open every brick at EPIC, which is a terrible default for
       balancing: you are shown the best roll of every card and have to scroll each
       one down to see what it is actually worth when it drops. It opens at each
       brick's OWN floor now, and shift+scroll walks up from there. */
    public static string benchRarOf(Upgrade u)
    {
        var (lo, hi) = benchRange(u);
        return RAR_ORDER[(int)clamp(benchRar.TryGetValue(u.id, out var v) ? v : lo, lo, hi)];
    }
    public static bool benchRarStep(Upgrade u, double dir)
    {
        var (lo, hi) = benchRange(u);
        double cur = clamp(benchRar.TryGetValue(u.id, out var v) ? v : lo, lo, hi);
        double next = clamp(cur + dir, lo, hi);
        if (next == cur) return false;
        benchRar[u.id] = next;
        return true;
    }
}
