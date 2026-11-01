// Port of lego-survival.html lines 17099-17477: GAME MODES (arcade and the RPG)
// and the RPG's world -- characters saved per class, the Outskirts, the gate and
// its keeper, the town of Brickhaven, using things, dying and standing up again.
using System.Text.Json.Nodes;

namespace Brickblade;

public sealed class GameModeDef { public string name, col, blurb; }
public sealed class RpgZone { public string name, col, sub; public double level; public bool hostile, test; }
public sealed class RpgWorld
{
    public string zone; public Prop gate; public bool gateOpen; public Ent boss; public Prop near; public bool fLatch; public double packs;
    public Pt caveIn, caveOut;
}
public sealed class RpgCharSave { public double level = 1, xp = 0, deaths = 0; public bool town = false; }
public sealed partial class Prop { public bool open, home; public bool solid = true; public string label, col; }
public sealed partial class Ent { public bool rpgBoss, rpgCave; public string rpgName; public Pt spot; }

public static partial class G
{
    /* ============================== GAME MODES ==============================
       ARCADE is the game as it is: a difficulty, a weapon, a favourite, waves,
       levels into the arcade tree, reward cards, bosses and leagues -- and NO
       gear: nothing is worn, nothing drops, the ritual sells none. RPG is the
       second way to play and is work in progress: for now it is a screen, and the
       equipment, the inventory and the stash live behind it (docs/RPG-BLUEPRINT.md).
       Every piece of gear code is still here, asked through modeHasGear(). */
    public static readonly JsMap<string, GameModeDef> GAME_MODES = new()
    {
        ["arcade"] = new GameModeDef { name = "ARCADE", col = "#ffb900", blurb = "Waves, reward cards and the arcade skill tree. No gear: what you pick up in a run is bricks." },
        ["rpg"] = new GameModeDef { name = "RPG · W.I.P.", col = "#6fb6ff", blurb = "A character per class, the Outskirts and the town of Brickhaven, gear that drops. Missions, gems and the RPG tree come next." },
    };
    public static string GAME_MODE = "arcade";
    public static bool modeHasGear() => GAME_MODE == "rpg";
    public static void setGameMode(string id)
    {
        GAME_MODE = GAME_MODES.ContainsKey(id ?? "") ? id : "arcade";
        try { Store.setItem("bb_mode", GAME_MODE); } catch { }
        var row = document.getElementById("modeRow");
        if (row != null)
            foreach (var el in row.children)
            {
                bool on = el.dataset["mode"] == GAME_MODE; string col = GAME_MODES[el.dataset["mode"]].col;
                el.classList.toggle("on", on);
                el.style.borderColor = on ? col : "rgba(255,255,255,.18)";
                el.style.background = on ? col : "rgba(9,13,17,.34)";
                el.style.color = on ? "#12181d" : "#f0e6e6";
            }
        var bl = document.getElementById("modeBlurb"); if (bl != null) bl.textContent = GAME_MODES[GAME_MODE].blurb;
        bool rpg = GAME_MODE == "rpg";
        var sb = document.getElementById("startBtn"); if (sb != null) sb.textContent = rpg ? "RPG — CHARACTER" : "START ADVENTURE";
        var gb = document.getElementById("gearBtn"); if (gb != null) gb.classList.toggle("hidden", !rpg);
        var pg = document.getElementById("pauseGearBtn"); if (pg != null) pg.classList.toggle("hidden", !rpg);
        var tb = document.getElementById("treeBtn"); if (tb != null) tb.classList.toggle("hidden", rpg);
    }
    public static void buildModeRow()
    {
        var row = document.getElementById("modeRow");
        if (row == null) return;
        row.innerHTML = string.Join("", GAME_MODES.Keys.Select(id => $"<div class=\"diff\" data-mode=\"{id}\">{GAME_MODES[id].name}</div>"));
        foreach (var el in row.children) el.addEventListener("click", _ => { SFX.click(); setGameMode(el.dataset["mode"]); });
        string saved = "arcade";
        try { saved = Store.getItem("bb_mode") ?? "arcade"; } catch { }
        setGameMode(saved);
    }
    public static bool gearFromRpg = false;   /* equipment opened from the RPG screen closes back to it */
    public static bool rpgScreenOn() { var el = document.getElementById("rpgScreen"); return el != null && !el.classList.contains("hidden"); }
    public static void toggleRpgScreen(bool show)
    {
        var el = document.getElementById("rpgScreen");
        if (el != null) el.classList.toggle("hidden", !show);
    }
    public static void rpgOpenGear() { toggleRpgScreen(false); gearFromRpg = true; toggleGear(); if (!gearOn) gearFromRpg = false; }
    /* START: an arcade run, or -- in RPG -- the work-in-progress screen */
    public static void startPressed()
    {
        if (GAME_MODE == "rpg") { try { SFX.click(); } catch { } rpgRenderScreen(); toggleRpgScreen(true); return; }
        startGame();
    }
    /* ============================ RPG: THE WORLD ============================
       Phase one of docs/RPG-BLUEPRINT.md: you wake in THE OUTSKIRTS, a field with
       its packs already standing in it, walk east through them to a palisade whose
       gate a GATEKEEPER holds, and through it into BRICKHAVEN -- the town, an
       INSTANCE of its own: no monsters, your home with the stash, a mission board
       and a waypoint back out. Every visit to a zone is a fresh instance (new
       packs, new ground); the town is always the same town.

       A zone REPLACES the wave: `wave.state` is 'rpg' and `wave.n` is the zone's
       level, so every monster built here is built at that level by the same code
       that builds the arcade's. Packs stand DORMANT (the ritual's sleeping-guard
       rule) until you come near or touch one, then the whole pack wakes. No reward
       cards in the RPG: a level is +5% damage and a refill, and what you find is
       GEAR -- a rare drops something more often than not, the Gatekeeper twice.

       The CHARACTER is saved per class (the weapon you chose on the title): level,
       experience and whether you have reached the town. Death sends you to the town
       once you have reached it, back to the Outskirts' start before then. */
    public const string RPG_KEY = "bb_rpg";
    public static readonly JsMap<string, RpgZone> RPG_ZONES = new()
    {
        ["outskirts"] = new RpgZone { name = "THE OUTSKIRTS", level = 2, col = "#a5e06e", hostile = true,
                   sub = "a field outside the walls -- the town is east, past the gate" },
        ["town"] = new RpgZone { name = "BRICKHAVEN", level = 1, col = "#ffd76e", hostile = false,
                   sub = "the town -- safe ground: your home, the board, the waypoint" },
        ["cave"] = new RpgZone { name = "THE TEST CAVE", level = 3, col = "#c9a6ff", hostile = true, test = true,
                   sub = "not canon -- a cave grown by cellular automata, new every time" },
    };
    public const int RPG_PACKS = 13;                          /* packs standing in the Outskirts */
    public static readonly int[] RPG_PACK_N = { 3, 6 };       /* bodies a pack */
    public const double RPG_PACK_GAP = 380;                   /* packs never closer than this */
    public const double RPG_AGGRO_R = 520;                    /* a pack wakes when you come this close */
    public const double RPG_MAGIC = 0.25; public const int RPG_RARES = 2;   /* magic share of the packs; rare packs a zone */
    public static readonly (string k, double w)[] RPG_ROSTER = { ("imp", 3), ("zombie", 3), ("boneling", 3), ("archer", 2), ("skeleton", 2), ("spitter", 1), ("bloater", 1) };
    public static readonly double RPG_GATE_X = W.w - 330;    /* the palisade's line, x */
    public const double RPG_GATE_HALF = 70;                   /* half the gate's width */
    public static readonly Pt RPG_START = new Pt { x = 330, y = CY };
    public const double RPG_BOSS_HP = 4;                      /* the Gatekeeper: a rare, four times the health */
    public const double RPG_USE_R = 92;                       /* how close F has to be */
    public static readonly NumMap RPG_DROP = new() { ["rare"] = 0.6, ["magic"] = 0.12, ["normal"] = 0.025 };   /* gear a kill drops, by rarity */
    public static readonly Pt RPG_TOWN = new Pt { x = CX - 120, y = CY + 40 };    /* where you stand up in town */
    public static RpgWorld RPG = null;                        /* the live world */
    public static RpgCharSave rpgChar = null;                 /* the saved character for this class */
    public static bool rpgOn() => GAME_MODE == "rpg" && RPG != null;

    /* ---- THE CHARACTER, one per class ---- */
    public static RpgCharSave rpgCharBlank() => new RpgCharSave { level = 1, xp = 0, town = false, deaths = 0 };
    public static JsonObject rpgSaveAll()
    {
        try { return (JsonNode.Parse(Store.getItem(RPG_KEY) ?? "{}") as JsonObject) ?? new JsonObject(); } catch { return new JsonObject(); }
    }
    static RpgCharSave RpgCharFrom(JsonNode n)
    {
        var c = rpgCharBlank();
        if (n is not JsonObject o) return c;
        /* Object.assign: whatever the save holds overwrites the blank */
        if (o["level"] is JsonValue lv && lv.TryGetValue<double>(out var l)) c.level = l;
        if (o["xp"] is JsonValue xv && xv.TryGetValue<double>(out var x)) c.xp = x;
        if (o["deaths"] is JsonValue dv && dv.TryGetValue<double>(out var d)) c.deaths = d;
        if (o["town"] is JsonValue tv && tv.TryGetValue<bool>(out var t)) c.town = t;
        return c;
    }
    static JsonObject RpgCharJson(RpgCharSave c) => new() { ["level"] = c.level, ["xp"] = c.xp, ["town"] = c.town, ["deaths"] = c.deaths };
    static string RpgWid(string wid = null) => Js.T(wid) ? wid : (weapon != null && Js.T(weapon.id) ? weapon.id : "sword");
    public static RpgCharSave rpgCharLoad(string wid = null)
    {
        var all = rpgSaveAll(); var c = all[RpgWid(wid)];
        rpgChar = RpgCharFrom(c);
        return rpgChar;
    }
    public static void rpgCharSave()
    {
        if (rpgChar == null) return;
        if (player != null && rpgOn()) { rpgChar.level = player.level; rpgChar.xp = Math.Floor(player.xp); }
        var all = rpgSaveAll(); all[RpgWid()] = RpgCharJson(rpgChar);
        try { Store.setItem(RPG_KEY, all.ToJsonString()); } catch { }
    }
    public static void rpgCharReset(string wid = null)
    {
        var all = rpgSaveAll(); all.Remove(RpgWid(wid));
        try { Store.setItem(RPG_KEY, all.ToJsonString()); } catch { }
        rpgChar = rpgCharBlank();
    }
    /* the saved level, walked up the same curve gainXp walks: the experience bar
       and the +5% a level are exactly what the levels would have given */
    public static void rpgApplyLevel(Ent p, double lv, double xp)
    {
        for (int l = 2; l <= lv; l++) { p.xpNext = Math.Round(p.xpNext * xpStep(l) + 5); p.incAll += 0.05; }
        p.level = Math.Max(1, Js.I32(lv)); p.xp = Math.Max(0, Math.Min(Js.Or(xp, 0), p.xpNext - 1));
    }

    /* ---- PROPS OF THE WORLD: the palisade, its gate, the town's buildings ---- */
    public static Prop rpgProp(string type, double x, double y, double r, Action<Prop> extra = null)
    {
        var p = new Prop { type = type, x = x, y = y, r = r, flash = 0, seed = Js.I32(Js.R() * 1000), dead = false, hp = 1, indestructible = true };
        extra?.Invoke(p);
        props.Add(p);
        if (p.solid) solids.Add(p);
        return p;
    }
    public static readonly JsMap<string, string> RPG_USES = new()
    {
        ["rstash"] = "STASH", ["rboard"] = "MISSIONS", ["rwaypoint"] = "WAYPOINT", ["rexit"] = "TO THE OUTSKIRTS", ["rhome"] = "HOME", ["rcave"] = "NEW CAVE", ["rportal"] = "TO BRICKHAVEN",
    };

    /* ---- ENTERING A ZONE: a fresh instance, the hero carried in ---- */
    public static bool rpgEnter(string id, Obj at = null)
    {
        var Z = RPG_ZONES.GetValueOrDefault(id ?? "");
        if (Z == null) return false;
        var p = player;
        ctlReset();
        enemies = new(); projectiles = new(); pickups = new(); debris = new(); particles = new(); dmgTexts = new(); sched = new();
        hazards = new(); spikes = new(); pspikes = new(); corpses = new(); gearDrops = new(); bloodOrbs = new();
        breach = null; breachExtra = new(); abyss = null; abyssExtra = new(); sboxPending = new(); ritualS = ritualBlank();
        rmCores = new(); legionEv = null; goblinPending = null; waveMods = new();
        seed = Js.I32(Js.R() * 1e9);
        wave = new Wave { n = Z.level, state = "rpg", t = 0, queue = new(), spawnT = 0, readyT = 0, readyMax = 0, done = 0 };
        RPG.zone = id; RPG.gate = null; RPG.gateOpen = false; RPG.boss = null; RPG.near = null; RPG.packs = 0;
        caveMap = null; caveArt = null;
        if (id == "town") rpgBuildTown(); else if (id == "cave") rpgBuildCave(); else rpgBuildOutskirts();
        navBuild();
        Obj to = at ?? (id == "town" ? RPG_TOWN : id == "cave" ? RPG.caveIn : RPG_START);
        p.x = to.x; p.y = to.y; p.kx = 0; p.ky = 0; p.dash.t = 0;
        /* the legion walks in with you */
        foreach (var m in minions)
        {
            double a = rnd(TAU); var q = floorAt(p.x + Math.Cos(a) * rnd(40, 90), p.y + Math.Sin(a) * rnd(40, 90), Js.Or(m.r, 12) + 2);
            m.x = q.x; m.y = q.y; m.spot = null;
        }
        cam.x = p.x; cam.y = p.y; cam.shake = 0; reclampCam();
        if (!Z.hostile) { p.hp = p.maxHp; if (p.esMax > 0) p.es = p.esMax; p.mana = p.maxMana; }
        if (id == "town" && rpgChar != null && !rpgChar.town) { rpgChar.town = true; logEvent("BRICKHAVEN reached -- the waypoint will bring you back", "#ffd76e"); }
        showBanner(Z.name, Z.hostile ? "MONSTER LEVEL " + S(mobLevelRpg()) : "SAFE GROUND", Z.col, 2.2, 50);
        logEvent("entered " + Z.name, Z.col);
        rpgCharSave();
        return true;
    }
    public static double mobLevelRpg() => Math.Max(1, Js.Or(mobRank(), wave.n));

    /* ---- THE OUTSKIRTS ---- */
    public static void rpgBuildOutskirts()
    {
        buildGround();
        runBiome = "forest";
        /* nothing wet where you wake, at the gate, or in the yard behind the palisade */
        lakeGroups = lakeGroups.Where(g => !g.Any(q =>
          hypot(q.x + q.w / 2 - RPG_START.x, q.y + q.h / 2 - RPG_START.y) < 520 ||
          q.x + q.w > RPG_GATE_X - 420)).ToList();
        waters = lakeGroups.SelectMany(g => g).ToList();
        buildProps();
        /* no card chests in the RPG, and the start, the gate and the yard are clear */
        props = props.Where(q => q.type != "chest" && q.type != "strongbox" && q.type != "torch" &&
          hypot(q.x - RPG_START.x, q.y - RPG_START.y) > 260 && q.x < RPG_GATE_X - 260).ToList();
        solids = solids.Where(q => props.Contains(q)).ToList();
        /* THE PALISADE: a post every 42px from wall to wall, the gate in the middle */
        for (double y = W.wall + 24; y < W.h - W.wall - 10; y += 42)
        {
            if (Math.Abs(y - CY) < RPG_GATE_HALF + 20) continue;
            rpgProp("rwall", RPG_GATE_X, y, 21);
        }
        RPG.gate = rpgProp("rgate", RPG_GATE_X, CY, RPG_GATE_HALF, q => { q.open = false; });
        rpgProp("torch", RPG_GATE_X - 70, CY - RPG_GATE_HALF - 40, 6, q => { q.solid = false; });
        rpgProp("torch", RPG_GATE_X - 70, CY + RPG_GATE_HALF + 40, 6, q => { q.solid = false; });
        /* a waypoint where you wake: once you have reached the town it takes you back */
        rpgProp("rwaypoint", RPG_START.x - 110, RPG_START.y - 120, 34, q => { q.solid = false; });
        navBuild();
        /* THE PACKS, standing where they are, asleep until you come */
        var spots = new List<Pt>();
        for (int tries = 0; tries < 900 && spots.Count < RPG_PACKS; tries++)
        {
            /* never close enough to the start that a pack is awake the moment you are */
            double x0 = RPG_START.x + RPG_AGGRO_R + 260;
            double x = x0 + Js.R() * (RPG_GATE_X - 560 - x0), y = 280 + Js.R() * (W.h - 560);
            if (spots.Any(s => hypot(s.x - x, s.y - y) < RPG_PACK_GAP)) continue;
            if (!floorClear(x, y, 60)) continue;
            spots.Add(new Pt { x = x, y = y });
        }
        string pick()
        {
            double t = 0; foreach (var (_, w) in RPG_ROSTER) t += w; double r = Js.R() * t;
            foreach (var (k, w) in RPG_ROSTER) { r -= w; if (r <= 0) return k; }
            return RPG_ROSTER[0].k;
        }
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            int id = i + 1; string type = pick();
            int n = irnd(RPG_PACK_N[0], RPG_PACK_N[1]);
            int before = enemies.Count;
            if (i < RPG_RARES)
            {
                spawnEnemy(type, s.x, s.y, true);
                for (int k = 0; k < 3; k++) { double a = k / 3.0 * TAU; spawnEnemy(pick(), s.x + Math.Cos(a) * 70, s.y + Math.Sin(a) * 70); }
            }
            else if (Js.R() < RPG_MAGIC) spawnMagicPack(type, s, n, false);
            else for (int k = 0; k < n; k++)
                {
                    double a = (double)k / n * TAU + rnd(-0.3, 0.3), rr = rnd(30, 70);
                    spawnEnemy(type, s.x + Math.Cos(a) * rr, s.y + Math.Sin(a) * rr);
                }
            foreach (var e in enemies.Skip(before)) { e.dormant = true; e.rpgPack = id; e.faceA = rnd(TAU); }
        }
        RPG.packs = spots.Count;
        /* THE GATEKEEPER, and two at its side */
        double gx = RPG_GATE_X - 240;
        spawnEnemy("brute", gx, CY, true);
        var b = enemies[^1];
        b.maxHp *= RPG_BOSS_HP; b.hp = b.maxHp; b.scale = Js.Or(b.scale, 1) * 1.3; b.r = Math.Round(Js.Or(b.r, 16) * 1.3);
        b.rpgBoss = true; b.rpgName = "THE GATEKEEPER"; b.dormant = true; b.rpgPack = 99;
        RPG.boss = b;
        foreach (var dy in new[] { -90.0, 90.0 }) { spawnEnemy("skeleton", gx + 40, CY + dy); var g = enemies[^1]; g.dormant = true; g.rpgPack = 99; }
    }

    /* ---- BRICKHAVEN, the town: always the same town ---- */
    public static void rpgBuildTown()
    {
        double keep = seed; seed = 7331;              /* the town's ground is the same every visit */
        buildGround(); seed = keep;
        runBiome = "meadow"; CAVES = new(); waters = new(); lakeGroups = new();
        flowers = flowers.Where(f => Math.Abs(f.x - CX) < 1100 && Math.Abs(f.y - CY) < 760).ToList();
        props = new(); solids = new();
        /* the town wall: a square of posts round the middle of the map, a gate west */
        double L = CX - 1150, R = CX + 1150, T = CY - 800, B = CY + 800;
        for (double x = L; x <= R; x += 42) { rpgProp("rwall", x, T, 21); rpgProp("rwall", x, B, 21); }
        for (double y = T + 42; y < B; y += 42)
        {
            rpgProp("rwall", R, y, 21);
            if (Math.Abs(y - CY) > RPG_GATE_HALF + 20) rpgProp("rwall", L, y, 21);
        }
        rpgProp("rexit", L - 6, CY, 44, q => { q.solid = false; q.label = "TO THE OUTSKIRTS"; });
        /* the buildings: your home (the stash in front of it), and the rest of the street */
        rpgProp("rhouse", CX + 520, CY - 420, 88, q => { q.col = "#c8553d"; q.label = "YOUR HOME"; q.home = true; });
        rpgProp("rstash", CX + 520, CY - 290, 24, q => { q.label = "STASH"; });
        rpgProp("rhouse", CX - 560, CY - 450, 80, q => { q.col = "#5a7fb5"; q.label = ""; });
        rpgProp("rhouse", CX + 640, CY + 430, 80, q => { q.col = "#7d9c4a"; q.label = ""; });
        rpgProp("rhouse", CX - 220, CY + 470, 74, q => { q.col = "#b08a3a"; q.label = ""; });
        rpgProp("rboard", CX - 300, CY - 120, 26, q => { q.label = "MISSION BOARD"; });
        rpgProp("rwaypoint", CX + 60, CY + 60, 40, q => { q.solid = false; q.label = "WAYPOINT"; });
        rpgProp("rcave", CX - 720, CY + 200, 34, q => { q.label = "CAVE GENERATOR · TEST"; });
        foreach (var (tx, ty) in new[] { (CX - 140, CY - 160), (CX + 260, CY - 160), (CX - 140, CY + 260), (CX + 260, CY + 260) })
            rpgProp("torch", tx, ty, 6, q => { q.solid = false; });
    }

    /* ---- EVERY FRAME, in a zone ---- */
    public static void rpgWakePack(double id)
    {
        foreach (var e in enemies) if (e.rpgPack == id && e.dormant) { e.dormant = false; e.atkCd = Math.Max(Js.Or(e.atkCd, 0), 0.3); }
    }
    public static void rpgTick(double dt)
    {
        if (!rpgOn() || player == null || player.dead) return;
        var p = player; var Z = RPG_ZONES[RPG.zone];
        /* the gate: open once the Gatekeeper is down; walk through it and you are in town */
        if (RPG.zone == "outskirts" && RPG.gateOpen && p.x > RPG_GATE_X + 18 && Math.Abs(p.y - CY) < RPG_GATE_HALF + 30)
        {
            rpgEnter("town", new Pt { x = CX - 1150 + 110, y = CY }); return;
        }
        /* the town's exit is a doorway: stepping into it is leaving */
        if (RPG.zone == "town" && p.x < CX - 1150 + 16 && Math.Abs(p.y - CY) < RPG_GATE_HALF + 30)
        {
            rpgEnter("outskirts"); return;
        }
        /* what is close enough to use */
        Prop near = null; double nd = RPG_USE_R;
        foreach (var q in props)
        {
            if (!RPG_USES.ContainsKey(q.type ?? "") || q.type == "rexit") continue;
            if (q.type == "rwaypoint" && RPG.zone == "outskirts" && !(rpgChar != null && rpgChar.town)) continue;
            double d = hypot(q.x - p.x, q.y - p.y) - Js.Or(q.r, 0);
            if (d < nd) { nd = d; near = q; }
        }
        RPG.near = near;
        if (RPG.zone == "cave") foreach (var pr in projectiles) if (!pr.dead && caveWallAt(pr.x, pr.y)) { pr.dead = true; addDust(pr.x, pr.y, 1); }
        bool f = key("KeyF");
        if (f && !RPG.fLatch && near != null) rpgUse(near);
        RPG.fLatch = f;
    }
    /* F on a thing */
    public static string rpgUse(Prop q)
    {
        SFX.click();
        if (q.type == "rstash") { toggleGear(); return "stash"; }
        if (q.type == "rwaypoint")
        {
            if (RPG.zone == "town") rpgEnter("outskirts");
            else if (rpgChar != null && rpgChar.town) rpgEnter("town");
            return "waypoint";
        }
        if (q.type == "rcave") { rpgEnter("cave"); return "cave"; }
        if (q.type == "rportal") { rpgEnter("town"); return "portal"; }
        if (q.type == "rboard")
        {
            showBanner("MISSION BOARD", "NO MISSIONS YET -- THE DUNGEONS OPEN IN A LATER UPDATE", "#ffd76e", 2.4, 40);
            return "board";
        }
        return null;
    }
    /* a death in a zone, a kill in a zone */
    public static void rpgOnKill(Ent e)
    {
        if (!rpgOn()) return;
        if (e.rpgPack != 0) rpgWakePack(e.rpgPack);
        rpgDrop(e);
        if (e.rpgBoss && e.rpgCave)
        {
            showBanner("THE WARDEN FALLS", "THE PORTAL HOME IS BY IT", "#c9a6ff", 2.0, 40);
            SFX.levelup();
        }
        else if (e.rpgBoss)
        {
            RPG.gateOpen = true;
            if (RPG.gate != null) { RPG.gate.open = true; solids = solids.Where(s => s != RPG.gate).ToList(); navRefresh(RPG.gate.x, RPG.gate.y, RPG.gate.r + 40); }
            showBanner("THE GATE IS OPEN", "BRICKHAVEN IS THROUGH IT", "#ffd76e", 2.2, 44);
            logEvent("the Gatekeeper falls -- the gate is open", "#ffd76e");
            SFX.levelup();
        }
    }
    public static List<GearDrop> rpgDrop(Ent e)
    {
        double ch = e.rpgBoss ? 1 : e.elite ? RPG_DROP["rare"] : e.magic ? RPG_DROP["magic"] : RPG_DROP["normal"];
        int n = e.rpgBoss ? 2 : (Js.R() < ch ? 1 : 0);
        double lv = gearItemLevel(); var @out = new List<GearDrop>();
        for (int i = 0; i < n; i++)
        {
            var B = gearRollBase(lv); if (B == null) continue;
            string r = gearRollRarity(e.rpgBoss || e.elite ? 1 : 0);
            if ((e.rpgBoss || e.elite) && r == "normal") r = "magic";
            @out.Add(gearDropAt(gearMake(B.id, r, lv), e.x + (i != 0 ? 30 : -30), e.y + 20));
        }
        return @out;
    }

    /* ---- DEATH: back to town (or the Outskirts' start before you have one) ---- */
    public static void rpgDeath()
    {
        state = "over"; cvEl.style.cursor = "default";
        if (rpgChar != null) { rpgChar.deaths = Js.Or(rpgChar.deaths, 0) + 1; rpgCharSave(); }
        var el = document.getElementById("rpgDeathScreen");
        el.classList.remove("peek"); document.getElementById("rpgDeathLogBtn").textContent = "COMBAT LOG";
        string where = rpgChar != null && rpgChar.town ? "BRICKHAVEN" : "THE OUTSKIRTS";
        var h = clogLastHit;
        document.getElementById("rpgDeathWho").innerHTML = h != null ? "KILLED BY <b>" + esc(S(h.who)) + "</b>" : "";
        document.getElementById("rpgDeathBtn").textContent = "STAND UP IN " + where;
        el.classList.remove("hidden");
    }
    public static void rpgResurrect()
    {
        var p = player;
        document.getElementById("rpgDeathScreen").classList.add("hidden", "peek");
        document.getElementById("rpgDeathScreen").classList.remove("peek");
        p.dead = false; p.deadT = 0; p.invuln = 2; p.stunT = 0;
        p.psn = new DotList { acc = 0 }; p.pbleed = new DotList { acc = 0 }; p.burn = new DotList { acc = 0, brkCd = 0 };
        p.chill = new Stacks { stacks = 0, t = 0 }; p.shock = new Stacks { stacks = 0, t = 0 }; p.slowT = 0;
        tScale = 1;
        p.hp = p.maxHp; if (p.esMax > 0) p.es = p.esMax; p.mana = p.maxMana;
        rpgEnter(rpgChar != null && rpgChar.town ? "town" : "outskirts");
        state = "play"; cvEl.style.cursor = clickMode() ? "default" : "none";
    }

    /* ---- STARTING: a character, carried into its zone ---- */
    public static void rpgStart()
    {
        audioInit(); closeBook();
        foreach (var id in new[] { "cheatScreen", "charScreen", "titleScreen", "rpgScreen", "rpgDeathScreen" })
        { var el = document.getElementById(id); if (el != null) el.classList.add("hidden"); }
        if (runLink != null) { runLink = null; }
        resetRun(false);
        rpgCharLoad();
        rpgApplyLevel(player, rpgChar.level, rpgChar.xp);
        syncStats(); player.hp = player.maxHp; player.mana = player.maxMana;
        RPG = new RpgWorld { zone = null };
        rpgEnter(rpgChar.town ? "town" : "outskirts");
        state = "play"; cvEl.style.cursor = clickMode() ? "default" : "none";
        if (OPT.autoLog && !clogOn) clogToggle();
    }
    public static void rpgLeave() { if (RPG != null) { rpgCharSave(); RPG = null; } caveMap = null; caveArt = null; }

    /* ---- THE SCREEN BEHIND START: the character and the way in ---- */
    public static void rpgRenderScreen()
    {
        var c = rpgCharLoad();
        var el = document.getElementById("rpgChar");
        if (el != null) el.innerHTML = $"<b>{esc((weapon != null && Js.T(weapon.name) ? weapon.name : "Sword").ToUpperInvariant())}</b> · level {S(c.level)}" +
          (c.town ? " · has reached BRICKHAVEN" : " · in THE OUTSKIRTS") + (c.deaths != 0 ? " · " + S(c.deaths) + " death" + (c.deaths > 1 ? "s" : "") : "");
        var go = document.getElementById("rpgPlayBtn");
        if (go != null) go.textContent = c.town ? "PLAY — TO BRICKHAVEN" : "PLAY — THE OUTSKIRTS";
    }
}
