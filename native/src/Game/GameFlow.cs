namespace Brickblade;

/* =============================== TITLE SCREEN / GAME FLOW / OPTIONS (JS 31836–32242) =============================== */
public sealed class TitleCell { public double x, y, delay; public Col col; }
public sealed class OptRow
{
    public string key, name, note, vol, unit;
    public Func<bool> on; public Action hit;
    public string[] choices; public JsMap<string, string> names; public double[] range;
}

public static partial class G
{
    /* =============================== TITLE SCREEN =============================== */
    public static readonly JsMap<char, string[]> GLYPHS = new()
    {
        ['B'] = new[] { "1110", "1001", "1001", "1110", "1001", "1001", "1110" },
        ['R'] = new[] { "1110", "1001", "1001", "1110", "1010", "1001", "1001" },
        ['I'] = new[] { "111", "010", "010", "010", "010", "010", "111" },
        ['C'] = new[] { "0111", "1000", "1000", "1000", "1000", "1000", "0111" },
        ['K'] = new[] { "1001", "1010", "1100", "1000", "1100", "1010", "1001" },
        ['L'] = new[] { "1000", "1000", "1000", "1000", "1000", "1000", "1111" },
        ['A'] = new[] { "0110", "1001", "1001", "1111", "1001", "1001", "1001" },
        ['D'] = new[] { "1110", "1001", "1001", "1001", "1001", "1001", "1110" },
        ['E'] = new[] { "1111", "1000", "1000", "1110", "1000", "1000", "1111" },
    };
    /* the JS reads these at load; here the DOM exists from boot, so they are set there */
    public static Element tcv; public static Ctx tctx;
    public static readonly List<TitleCell> titleCells = new();
    public static void buildTitle()
    {
        tcv = document.getElementById("titleCanvas"); tctx = tcv?.getContext("2d");
        titleCells.Clear();
        var lines = new[] { "BRICK", "BLADE" };
        var lineCols = new[] { new Col[] { PAL.red, PAL.blue, "#4b9b41", PAL.orange, PAL.gold },
                               new Col[] { "#eef0ee", "#eef0ee", "#eef0ee", "#eef0ee", "#eef0ee" } };
        const double pitch = 22;
        double y0 = 26;
        double cvW = tcv != null ? tcv.width : 760;
        for (int li = 0; li < lines.Length; li++)
        {
            var word = lines[li];
            var widths = word.Select(ch => (double)GLYPHS[ch][0].Length).ToArray();
            double totalW = widths.Aggregate(0.0, (a, w) => a + w * pitch) + (word.Length - 1) * pitch;
            double lx = (cvW - totalW) / 2;
            for (int chi = 0; chi < word.Length; chi++)
            {
                var g = GLYPHS[word[chi]]; var col = lineCols[li][chi % lineCols[li].Length];
                for (int r = 0; r < 7; r++) for (int cix = 0; cix < g[r].Length; cix++)
                {
                    if (g[r][cix] != '1') continue;
                    titleCells.Add(new TitleCell { x = lx + cix * pitch, y = y0 + r * pitch, col = col,
                        delay = li * 0.35 + chi * 0.12 + r * 0.045 + cix * 0.025 });
                }
                lx += widths[chi] * pitch + pitch;
            }
            y0 += 7 * pitch + 30;
        }
    }
    public static void renderTitle()
    {
        var c = tctx;
        if (c == null) return;
        c.clearRect(0, 0, tcv.width, tcv.height);
        foreach (var cell in titleCells)
        {
            double t = clamp((titleClock - cell.delay) / 0.5, 0, 1);
            if (t <= 0) continue;
            double k = easeOutBack(t);
            double y = cell.y - (1 - k) * 140;
            double jx = Math.sin(vtime * 1.6 + cell.y * 0.02) * 1.5;
            double jy = Math.cos(vtime * 1.3 + cell.x * 0.015) * 1.2;
            double sweep = ((cell.x + cell.y * 1.5 - vtime * 110) % 1500 + 1500) % 1500;
            double glow = sweep < 100 ? (1 - sweep / 100) : 0;
            c.save(); c.translate(cell.x + 11 + jx, y + 11 + jy);
            c.fillStyle = "rgba(0,0,0,0.3)"; roundRect(c, -8, -7, 20, 20, 4); c.fill();
            c.fillStyle = sh(cell.col, -0.05 + (hash2(cell.x, cell.y) - 0.5) * 0.1);
            roundRect(c, -10, -10, 20, 20, 4); c.fill();
            c.strokeStyle = "rgba(0,0,0,0.25)"; c.lineWidth = 1.5; c.stroke();
            drawStudG(c, 0, 0, 6.2, cell.col);
            if (glow > 0)
            {
                c.globalAlpha = glow * 0.55; c.fillStyle = "#fff";
                roundRect(c, -10, -10, 20, 20, 4); c.fill(); c.globalAlpha = 1;
            }
            c.restore();
        }
        const double mx = 692, my = 372, ms = 1.5;
        c.fillStyle = "#4f9a37";
        roundRect(c, mx - 46, my - 6, 96, 22, 8); c.fill();
        c.strokeStyle = "rgba(0,0,0,0.25)"; c.lineWidth = 2; c.stroke();
        drawStudG(c, mx - 26, my + 5, 7, "#59ab40");
        drawStudG(c, mx, my + 5, 7, "#59ab40");
        drawStudG(c, mx + 26, my + 5, 7, "#59ab40");
        double mt = vtime % 3.6;
        double swA = mt < 0.35 ? (-2.4 + (mt / 0.35) * 3.4) : double.NaN;
        drawFigure(c, new Fig { x = mx, y = my, s = ms, face = new Pt(-0.5, 0.9), walk = 0, moving = false,
            flash = 0, blink = ((vtime % 3.1) < 0.13) ? 1 : 0, colors = HERO_COLORS, style = "hero",
            hero = true, weapon = heldArt(), bladeAng = swA, elite = false, phase = 0 });
    }
    public static double titleClock = 0;

    /* =============================== GAME FLOW =============================== */
    public static void resetRun(bool demo)
    {
        ctlReset();
        seed = (!demo && runLink != null && Js.T(runLink.s)) ? runLink.s : Js.I32(Js.R() * 1e9);
        enemies = new(); projectiles = new(); pickups = new(); debris = new(); particles = new(); dmgTexts = new(); sched = new(); hazards = new(); spikes = new(); pspikes = new();
        minions = new(); corpses = new(); gearDrops = new();
        taken = new(); buildLog = new(); banner = null; runTime = 0; redFlash = 0; tScale = 1;
        pendingPicks = new(); upgradeMode = "wave"; lootChain = false;
        rerollsUsed = 0; screenRerolls = 0; shunned = new();
        clogReset(); clogLastHit = null;
        respawnReset();
        buildGround(); buildProps();
        navBuild();
        player = newPlayer(); heroObj = player;
        syncStats();
        if (!demo)
        {
            /* the arcade's tree and favourite are the arcade's: the RPG's power is its gear */
            if (GAME_MODE != "rpg") { applyMetaTree(player); applyFavPerks(); }
            /* WHAT THE TREE STARTED YOU WITH. A legendary node does not hand over a
               number, it hands over a BRICK — so it is applied here, once, where a real
               run begins, and never in newPlayer or on the bench's preview heroes. */
            if (GAME_MODE != "rpg") metaGrantBricks(player.metaGrants ?? new List<string>());
            /* ...and WHAT YOU ARE WEARING, which is the one thing a death leaves you --
               in RPG. An ARCADE run wears nothing. */
            if (modeHasGear()) gearApply(player, true);
        }
        /* a run starts with a full mana pool, whatever the tree and the gear made it */
        player.mana = player.maxMana;
        /* the records as this run found them -- see metaLiveBank */
        if (META == null) metaLoad();
        {
            var m0 = metaCur();
            /* an RPG level is the RPG's: it never feeds the arcade tree */
            metaRun = (!demo && META.on && runLink == null && GAME_MODE != "rpg")
                ? new MetaRunSnap { wave = Js.Or(m0.best.wave, 0), level = Js.Or(m0.best.level, 0), earned = metaPointsEarned(m0) } : null;
        }
        wave = new Wave { n = 0, state = "idle", t = 0, queue = new(), spawnT = 0, readyT = 0, readyMax = 0, done = 0 };
        waveMods = new();
        breach = null; breachExtra = new(); abyss = null; abyssExtra = new(); leagueMiss = 0; sboxPending = new();
        ritualS = ritualBlank(); rmCores = new(); kindredUid = 0; goblinPending = null; bloodOrbs = new(); legionEv = null;
        rollRoster();
        /* a linked run meets the same roster it was copied from */
        if (!demo && runLink != null && runLink.ro != null)
            foreach (var (k, at) in runLink.ro) { var r = ROSTER.FirstOrDefault(x => x.k == k); if (r != null && Js.T(at)) r.at = at; }
        if (demo)
        {
            for (int i = 0; i < 4; i++)
            {
                var sp = randomSpawnPos();
                spawnEnemy("imp", sp.x, sp.y); enemies[enemies.Count - 1].docile = true;
            }
            spawnEnemy("archer", CX - 380, CY + 280); enemies[enemies.Count - 1].docile = true;
            spawnEnemy("spitter", CX + 380, CY + 260); enemies[enemies.Count - 1].docile = true;
        }
    }
    public static void updateCamera(double raw, double tx0, double ty0)
    {
        double viewW = cw / zoom, viewH = ch / zoom;
        double tx = viewW >= W.w ? W.w / 2 : clamp(tx0, viewW / 2, W.w - viewW / 2);
        double ty = viewH >= W.h ? W.h / 2 : clamp(ty0, viewH / 2, W.h - viewH / 2);
        double k = clamp(raw * 8, 0, 1);
        cam.x = lerp(cam.x, tx, k);
        cam.y = lerp(cam.y, ty, k);
        reclampCam();
    }
    public static void startGame()
    {
        audioInit();
        closeBook();
        document.getElementById("cheatScreen").classList.add("hidden");
        document.getElementById("charScreen").classList.add("hidden");
        document.getElementById("titleScreen").classList.add("hidden");
        resetRun(false);
        double firstWave = runLink != null ? runLinkApply() : 1;
        cam.x = player.x; cam.y = player.y; cam.shake = 0;
        reclampCam();
        state = "play"; Ui.Cursor("none");
        /* AFTER resetRun, which calls clogReset -- and clogReset does not touch
           clogOn, so a log left open by the last run is already open. Opening it
           twice would toggle it shut. */
        if (OPT.autoLog && !clogOn) clogToggle();
        try { window.focus(); } catch { }
        try { cvEl?.focus(); } catch { }
        startWave(firstWave);
    }
    public static void showGameOver()
    {
        state = "over"; Ui.Cursor("default");
        document.getElementById("stWave").textContent = S(wave.n);
        document.getElementById("stKill").textContent = S(player.kills);
        document.getElementById("stStud").textContent = S(player.studs);
        document.getElementById("stLvl").textContent = S(player.level);
        double m = Math.floor(runTime / 60), s = Math.floor(runTime % 60);
        document.getElementById("stTime").textContent = S(m) + ":" + S(s).PadLeft(2, '0');
        /* THE RUN IS BANKED BEFORE THE SCREEN IS DRAWN, so what you read is the truth
           and not a promise. Read `prevBest` first — metaBank moves it. */
        var cur = metaCur();
        double prevBest = metaRun != null ? metaRun.wave : Js.Or(cur.best.wave, 0);
        /* WITH A SECOND CHANCE ON THE TABLE THE RUN IS NOT OVER, so it is not banked:
           standing up again would bank it twice. It is banked when you leave. */
        var offer = respawnOffer();
        bool hold = offer != null && offer.can;
        MetaBankResult res = null;
        if (!hold && META != null && META.on && runLink == null && !respawn.banked) { respawn.banked = true; res = metaBank(); }
        renderRunEnd(res, prevBest, hold);
        renderRespawn(offer);
        var ov = document.getElementById("overScreen");
        ov.classList.remove("peek");
        ov.classList.remove("hidden");
        try
        {
            double best = Math.max(wave.n, Js.ToNumber(Store.getItem("bb_best") is string sb && sb != "" ? sb : "0"));
            Store.setItem("bb_best", S(best));
        }
        catch { }
    }
    /* WHAT YOU EARNED, not just what happened. A death screen that only reports the
       run is why dying felt like nothing: it has to show progress toward the NEXT
       thing, name what is newly within reach, and shout about anything that was a
       FIRST. When nothing new happened it says so rather than inventing a reward —
       a screen that celebrates every run teaches you to ignore it. */
    /* WHAT KILLED YOU: the last blow the log saw, and the way to the log itself */
    public static string killerLine()
    {
        var h = clogLastHit;
        if (h == null) return "";
        string what = h.who == "ailments" ? "what you were burning, bleeding or poisoned with" : S(h.who);
        return "<div class=\"rekill\">KILLED BY <b>" + esc(what) + "</b> <i>" + fmtHp(h.n) + (!string.IsNullOrEmpty(h.elem) ? " " + esc(h.elem) : "") + " · COMBAT LOG for the rest</i></div>";
    }
    public static void renderRespawn(RespawnOffer offer)
    {
        var b = document.getElementById("respawnBtn");
        if (b == null) return;
        if (offer == null) { b.style.display = "none"; return; }
        b.style.display = "";
        b.disabled = !offer.can;
        b.textContent = offer.free ? "SECOND CHANCE — FREE" : "RESPAWN — " + S(offer.cost) + " STUDS";
        b.title = offer.free ? "stand up where you fell, whole -- it comes back when you next kill a boss"
            : (offer.can ? "the fine multiplies by " + S(RESPAWN_FINE_MUL) + " with every one paid this run"
                         : "you have " + S(Js.Or(player.studs, 0)) + " studs -- kill a boss to get the free one back");
    }
    public static void renderRunEnd(MetaBankResult res, double prevBest, bool hold)
    {
        var el = document.getElementById("runEnd");
        if (el == null) return;
        if (hold) { el.innerHTML = killerLine() + "<div class=\"renone\">A second chance stands — the run is banked to the tree when you leave.</div>"; return; }
        if (res == null) { el.innerHTML = killerLine() + "<div class=\"renone\">The skill tree is switched off for this run.</div>"; return; }
        var m = metaCur();
        var L = new List<string>();
        /* THE WAVE YOU CLEARED. You died ON wave 30 without finishing it, so the run
           is a wave-29 run and the tree is paid for 29. */
        double done = res.cleared;
        if (clogLastHit != null) L.Add(killerLine());
        L.Add("<div class=\"rebest\">CLEARED <b>" + S(done) + "</b>" +
            (done > prevBest ? " <em>a new best — was " + S(prevBest) + "</em>"
                             : " <span>best " + S(prevBest) + "</span>") +
            (wave.n > done ? " <span>· died on " + S(wave.n) + "</span>" : "") + "</div>");
        L.Add("<div class=\"rerow\"><span>studs banked</span><b>+" + S(res.banked) + "</b>" +
              "<i>" + S(res.total) + " total · " + weapon.name + " on " + difficulty.name + "</i></div>");
        L.Add("<div class=\"rerow\"><span>reached level</span><b>" + S(res.level) + "</b>" +
              "<i>best " + S(Js.Or(m.best.level, 0)) + " · " + S(META_POINTS_PER_LEVEL) + " skill points a level</i></div>");
        foreach (var f in res.firsts)
            L.Add("<div class=\"rerow first\"><span>FIRST</span><b>" + esc(f.text) + "</b>" +
                  "<i>+" + S(f.pts) + " skill point" + (f.pts == 1 ? "" : "s") + "</i></div>");
        if (res.gained > 0)
            L.Add("<div class=\"rerow first\"><span>skill points</span><b>+" + S(res.gained) + "</b>" +
                  "<i>" + S(res.left) + " unspent</i></div>");
        double next = metaNextPoint(m);
        if (!double.IsNaN(next))
            L.Add("<div class=\"rerow\"><span>next point</span><b>clear wave " + S(next) + "</b>" +
                  "<i>" + S(metaPointsEarned(m)) + " earned, ceiling " + S(META_POINT_MAX) + "</i></div>");
        /* NAME SOMETHING YOU COULD ACTUALLY GO AND TAKE — not a door you already
           stand in, and not a node on the far side of a tree you have not walked to */
        /* a corridor node is not a reward — name the nearest thing worth walking to */
        var reach = META_NODES.Where(n =>
                n.kind != "start" && !metaHas(n.id, m) && !metaLocked(n, m)
                && metaConnected(n.id, m, new List<string> { metaStartId() }))
            .OrderBy(n => metaCost(n)).ToList();
        var want = reach.FirstOrDefault(n => n.kind != "travel") ?? reach.FirstOrDefault();
        if (want != null)
        {
            double @short = metaCost(want) - metaPointsLeft(m);
            string gives = string.Join(" · ", want.mods.Select(mod => metaModText(mod)));
            L.Add("<div class=\"rewant\"><b>" + esc(want.name) + "</b>" + (gives != "" ? " — " + esc(gives) : "") + "<br>" +
                (@short <= 0 ? "<em>you can take it now</em>"
                             : "<span>" + S(@short) + " more point" + (@short == 1 ? "" : "s") + "</span>") + "</div>");
        }
        if (res.firsts.Count == 0 && res.gained == 0)
            L.Add("<div class=\"renone\">Nothing new this run — the wave to beat is " + S(prevBest + 1) + ".</div>");
        el.innerHTML = string.Join("", L);
    }
    public static void togglePause()
    {
        if (state == "play")
        {
            state = "pause"; Ui.Cursor("default");
            document.getElementById("pauseScreen").classList.remove("hidden");
        }
        else if (state == "pause")
        {
            state = "play"; Ui.Cursor("none");
            document.getElementById("pauseScreen").classList.add("hidden");
        }
    }

    /* ============================== OPTIONS SCREEN ==============================
       ONE SCREEN, TWO DOORS. It opens off the title plate and off the pause menu,
       and it remembers which one it came through, so BACK and ESC put you where
       you were instead of dumping a paused run onto the title. It never changes
       `state`: a paused run stays paused underneath it and a title stays a title,
       which is why it can be opened from either without a second state machine. */
    /* A[r.vol]: the two volumes, by the name a row carries */
    static double audioVolGet(string k) => k == "musVol" ? A.musVol : A.sfxVol;
    static void audioVolSet(string k, double v) { if (k == "musVol") A.musVol = v; else A.sfxVol = v; }
    public static List<OptRow> optRows()
    {
        /* audio is not in OPT_DEFS -- it has its own store and its own live effect on
           a running context -- so it is described here and toggled through its own
           functions. The rows read the same way to a player, which is the point. */
        var rows = new List<OptRow>
        {
            new OptRow { key = "music", name = "MUSIC", on = () => A.musicOn && !A.muted,
                note = "the field and boss tracks. M still takes everything down at once",
                vol = "musVol", hit = () => { audioInit(); toggleMusic(); } },
            new OptRow { key = "sfx", name = "SOUND", on = () => A.sfxOn && !A.muted,
                note = "every hit, pickup, cast and smash",
                vol = "sfxVol", hit = () => { audioInit(); toggleSfx(); } },
        };
        rows.AddRange(OPT_DEFS.Select(d => new OptRow { key = d.id, name = d.name, note = d.note, choices = d.choices, names = d.names, range = d.range, unit = d.unit,
            on = () => Js.T(OPT[d.id]), hit = () => optToggle(d.id) }));
        return rows;
    }
    static string optChoiceName(string c) => (TEXT_SIZES.GetValueOrDefault(c) ?? new TextSize { name = c }).name;
    public static void optRender()
    {
        var list = document.getElementById("optList");
        if (list == null) return;
        var rows = optRows().Select(r =>
        {
            /* a choice: one button per value, the chosen one lit */
            if (r.choices != null) return "<div class=\"optrow choice\"><div class=\"otext\"><b>" + r.name + "</b><i>" + r.note + "</i>" +
                "<div class=\"ochoices\">" + string.Join("", r.choices.Select(c => "<button class=\"ochoice" + (OPT[r.key] as string == c ? " on" : "") + "\" data-choice=\"" + r.key + "\" data-val=\"" + c + "\">" +
                    ((r.names != null ? r.names.GetValueOrDefault(c) : null) is string nm && nm != "" ? nm : optChoiceName(c)) + "</button>")) + "</div></div></div>";
            /* a slider of its own, with its value beside it */
            if (r.range != null) return "<div class=\"optrow\"><div class=\"otext\"><b>" + r.name + "</b><i>" + r.note + "</i></div>" +
                "<div class=\"ovol\"><input type=\"range\" min=\"" + S(r.range[0]) + "\" max=\"" + S(r.range[1]) + "\" step=\"" + S(r.range[2]) + "\" value=\"" + S(OPT[r.key]) + "\" data-optr=\"" + r.key + "\" aria-label=\"" + r.name + "\">" +
                "<span class=\"ovoln\">" + S(OPT[r.key]) + (r.unit ?? "") + "</span></div></div>";
            /* a switch, and -- for music and sound -- a volume slider beside it */
            string vol = r.vol != null ? "<div class=\"ovol\"><input type=\"range\" min=\"0\" max=\"100\" step=\"1\" value=\"" + S(Math.round(audioVolGet(r.vol) * 100)) + "\" data-vol=\"" + r.vol + "\" aria-label=\"" + r.name + " volume\">" +
                "<span class=\"ovoln\">" + S(Math.round(audioVolGet(r.vol) * 100)) + "%</span></div>" : "";
            return "<div class=\"optrow\"><div class=\"otext\"><b>" + r.name + "</b><i>" + r.note + "</i></div>" + vol +
                "<button class=\"oswitch " + (r.on() ? "on" : "") + "\" data-opt=\"" + r.key + "\">" +
                (r.on() ? "ON" : "OFF") + "</button></div>";
        }).ToList();
        /* THE ONE THING THAT CANNOT BE UNDONE gets its own look and two clicks. It
           wipes the tree for EVERY weapon on EVERY difficulty, because "my character"
           is all of them and half a wipe is worse than none. */
        rows.Add(
            "<div class=\"optrow danger\"><div class=\"otext\"><b>RESET CHARACTER PROGRESSION</b>" +
            "<i>every skill tree, every weapon, every difficulty — best wave, best level, " +
            "bosses put down, spells mastered and studs banked, all back to nothing. " +
            "This cannot be undone.</i></div>" +
            "<button class=\"oswitch " + (optArmed != 0 ? "armed" : "warn") + "\" data-opt=\"wipe\">" +
            (optArmed != 0 ? "ARE YOU SURE?" : "RESET") + "</button></div>");
        list.innerHTML = string.Join("", rows);
        foreach (var b in list.querySelectorAll(".oswitch"))
            b.addEventListener("click", () => optClick(b.getAttribute("data-opt")));
        foreach (var b in list.querySelectorAll(".ochoice"))
            b.addEventListener("click", () =>
            {
                SFX.click(); optArmed = 0;
                optSet(b.dataset["choice"], b.dataset["val"]);
                optSay(OPT_DEFS.First(d => d.id == b.dataset["choice"]).name + " is now " + optChoiceName(b.dataset["val"]));
                optRender();
            });
        foreach (var inp in list.querySelectorAll("input[data-optr]"))
        {
            var d = OPT_DEFS.First(q => q.id == inp.dataset["optr"]); var lab = inp.parentNode.querySelector(".ovoln");
            void fill()
            {
                double f = (Js.ToNumber(inp.value) - d.range[0]) / (d.range[1] - d.range[0]) * 100;
                inp.style.background = "linear-gradient(90deg, #ffb900 " + S(f) + "%, rgba(0,0,0,.35) " + S(f) + "%)";
            }
            fill();
            inp.addEventListener("input", () => { optSet(d.id, Js.ToNumber(inp.value)); if (lab != null) lab.textContent = S(OPT[d.id]) + (d.unit ?? ""); fill(); });
        }
        foreach (var inp in list.querySelectorAll("input[data-vol]"))
        {
            var lab = inp.parentNode.querySelector(".ovoln");
            /* the track fills gold up to the thumb */
            void fill() { inp.style.background = "linear-gradient(90deg, #ffb900 " + inp.value + "%, rgba(0,0,0,.35) " + inp.value + "%)"; }
            fill();
            inp.addEventListener("input", () =>
            {
                audioInit();
                string vk = inp.dataset["vol"];
                audioVolSet(vk, clamp(Js.ToNumber(inp.value) / 100, 0, 1));
                if (lab != null) lab.textContent = S(Math.round(audioVolGet(vk) * 100)) + "%";
                fill();
                audioGains(); audioSave();
            });
            /* let go of the sound slider and you hear where you put it */
            inp.addEventListener("change", () => { if (inp.dataset["vol"] == "sfxVol") { try { sfxLast.Clear(); SFX.hit(); } catch { } } });
        }
    }
    public static void optSay(string msg)
    {
        var el = document.getElementById("optNote");
        if (el != null) el.textContent = msg ?? "";
    }
    public static void optClick(string key)
    {
        SFX.click();
        if (key == "wipe")
        {
            if (optArmed == 0)
            {
                optArmed = 1; optSay("Click again to wipe every tree. Nothing is kept.");
                optRender(); return;
            }
            optArmed = 0; metaWipe();
            optSay("Gone. Every weapon starts from zero.");
            optRender(); return;
        }
        optArmed = 0;
        var r = optRows().FirstOrDefault(x => x.key == key);
        if (r == null) return;
        r.hit();
        optSay(r.name + " is now " + (r.on() ? "ON" : "OFF"));
        optRender();
    }
    /* A WIPE IS A WIPE. metaBlankSave() builds the empty record for every
       difficulty and every weapon, so nothing has to be walked by hand and a
       weapon added tomorrow cannot be missed. The personal-best line on the title
       plate is part of the same record to a player, so it goes too -- what it is
       NOT allowed to touch is the options themselves. */
    public static void metaWipe()
    {
        META = metaBlankSave();
        metaSave();
        try { Store.removeItem("bb_best"); } catch { }
        treeRefund = false;
        refreshTreeBtn();
        showBest();
        if (treeOn) renderTree();
    }
    public static void openOptions(string from = null)
    {
        optFrom = from ?? "title";
        optOn = true; optArmed = 0;
        optSay("");
        optRender();
        document.getElementById("optScreen").classList.remove("hidden");
    }
    public static void closeOptions()
    {
        optOn = false; optArmed = 0;
        document.getElementById("optScreen").classList.add("hidden");
    }
    public static void toggleOptions(string from = null) { if (optOn) closeOptions(); else openOptions(from); }

    public static void backToTitle()
    {
        rpgLeave();
        var rd = document.getElementById("rpgDeathScreen"); if (rd != null) rd.classList.add("hidden");
        /* leaving a linked run: the next run is your own again (the native build has
           no address bar to clean: a link arrives as a launch argument) */
        if (runLink != null) runLink = null;
        closeBook();
        closeOptions();
        document.getElementById("cheatScreen").classList.add("hidden");
        document.getElementById("charScreen").classList.add("hidden");
        document.getElementById("overScreen").classList.add("hidden");
        document.getElementById("pauseScreen").classList.add("hidden");
        document.getElementById("upgradeScreen").classList.add("hidden");
        document.getElementById("titleScreen").classList.remove("hidden");
        resetRun(true);
        state = "title"; titleClock = 0; Ui.Cursor("default");
        showBest();
        refreshGearBtn();
        fitTitle();
    }
    public static void showBest()
    {
        double best = 0;
        try { var v = Store.getItem("bb_best"); best = Js.ToNumber(string.IsNullOrEmpty(v) ? "0" : v); } catch { }
        document.getElementById("bestLine").textContent = best > 0 ? "Personal best: wave " + S(best) : "";
    }

    /* confirm(): the browser's blocking yes/no box, drawn by the window */
    public static bool confirm(string msg) => App.Confirm(msg);

    /* JS 32243–32328: the title, pause, death and options buttons */
    public static void GameFlowWire()
    {
        document.getElementById("startBtn").addEventListener("click", startPressed);
        document.getElementById("rpgBackBtn").addEventListener("click", () => { SFX.click(); toggleRpgScreen(false); });
        document.getElementById("rpgPlayBtn").addEventListener("click", () => { SFX.click(); rpgStart(); });
        document.getElementById("rpgResetBtn").addEventListener("click", () =>
        {
            if (!confirm("Start this class over at level 1, in the Outskirts? Your gear and the stash are kept.")) return;
            rpgCharReset(); rpgRenderScreen();
        });
        document.getElementById("rpgDeathBtn").addEventListener("click", () => { SFX.click(); rpgResurrect(); });
        document.getElementById("rpgDeathLogBtn").addEventListener("click", () =>
        {
            SFX.click();
            var el = document.getElementById("rpgDeathScreen"); bool on = !el.classList.contains("peek");
            el.classList.toggle("peek", on);
            document.getElementById("rpgDeathLogBtn").textContent = on ? "BACK" : "COMBAT LOG";
            if (on) { if (!clogOn) clogToggle(); clogView = CLOG_IN; }
        });
        document.getElementById("rpgDeathTitleBtn").addEventListener("click", () => { SFX.click(); backToTitle(); });
        document.getElementById("rpgGearBtn").addEventListener("click", rpgOpenGear);
        document.getElementById("treeBtn").addEventListener("click", () => { SFX.click(); toggleTree(); });
        /* audioInit() FIRST on both: a browser will not start an audio context until
           somebody clicks something, and this click is as good as any — otherwise
           turning the music on in the menu does nothing at all until the run starts. */
        document.getElementById("musicBtn").addEventListener("click", () => { audioInit(); toggleMusic(); SFX.click(); });
        document.getElementById("sfxBtn").addEventListener("click", () => { audioInit(); toggleSfx(); SFX.click(); });
        document.getElementById("optBtn").addEventListener("click", () => { audioInit(); SFX.click(); openOptions("title"); });
        document.getElementById("pauseTreeBtn").addEventListener("click", () => { SFX.click(); toggleTree(); });
        document.getElementById("pauseOptBtn").addEventListener("click", () => { SFX.click(); openOptions("pause"); });
        document.getElementById("optBackBtn").addEventListener("click", () => { SFX.click(); closeOptions(); });
        audioLoad();
        optLoad(); applyTextSize();
        document.getElementById("treeCloseBtn").addEventListener("click", () => { SFX.click(); toggleTree(); });
        document.getElementById("treeInBtn").addEventListener("click", () => { SFX.click(); treeZoomBy(1.5); });
        document.getElementById("treeOutBtn").addEventListener("click", () => { SFX.click(); treeZoomBy(1 / 1.5); });
        document.getElementById("treeFitBtn").addEventListener("click", () => { SFX.click(); treeFit(); });
        document.getElementById("treeZoomLbl").addEventListener("click", () => { SFX.click(); treeHome(); });
        document.getElementById("treeSecBtn").addEventListener("click", () => { SFX.click(); treeToggleSectors(); refreshTreeSecBtn(); });
        refreshTreeSecBtn();
        /* REFUND MODE is a mode, not a button that eats your tree: turn it on, then
           click the nodes you want back, one at a time. Clearing the lot is its own
           button and says so. */
        document.getElementById("respecBtn").addEventListener("click", () =>
        {
            if (state == "tree") return;
            SFX.click(); treeRefund = !treeRefund; treePick = null; renderTree();
        });
        document.getElementById("clearBtn").addEventListener("click", () =>
        {
            if (state == "tree") return;
            SFX.click(); metaRespec(); treeRefund = false; treePick = null;
            renderTree(); refreshTreeBtn();
        });
        document.getElementById("treeOffBtn").addEventListener("click", () =>
        {
            if (state == "tree") return;
            SFX.click(); metaToggle(); renderTree(); refreshTreeBtn();
        });
        document.getElementById("retryBtn").addEventListener("click", () =>
        {
            SFX.click(); overLeave();
            document.getElementById("overScreen").classList.add("hidden"); startGame();
        });
        document.getElementById("titleBtn").addEventListener("click", () => { SFX.click(); overLeave(); backToTitle(); });
        document.getElementById("respawnBtn").addEventListener("click", () => { if (respawnNow()) SFX.levelup(); else SFX.click(); });
        document.getElementById("overLogBtn").addEventListener("click", () => { SFX.click(); overPeek(true); });
        document.getElementById("overBackBtn").addEventListener("click", () => { SFX.click(); overPeek(false); });
        document.getElementById("resumeBtn").addEventListener("click", () => { SFX.click(); togglePause(); });
        document.getElementById("restartBtn").addEventListener("click", () =>
        {
            SFX.click();
            document.getElementById("pauseScreen").classList.add("hidden"); if (rpgOn()) rpgStart(); else startGame();
        });
        document.getElementById("pauseTitleBtn").addEventListener("click", () => { SFX.click(); backToTitle(); });
        document.getElementById("gearBtn").addEventListener("click", () => { SFX.click(); toggleGear(); });
        document.getElementById("pauseGearBtn").addEventListener("click", () => { toggleGear(); });
        ritualUiWire();
        document.getElementById("pauseLinkBtn").addEventListener("click", e => { SFX.click(); runLinkCopy(e.currentTarget); });
        document.getElementById("overLinkBtn").addEventListener("click", e => { SFX.click(); runLinkCopy(e.currentTarget); });
    }
    public static void refreshTreeSecBtn()
    {
        var b = document.getElementById("treeSecBtn");
        if (b == null) return;
        b.textContent = "COUNTRIES";
        b.classList.toggle("on", treeSectors);
    }
    /* leaving a death screen that held the run back banks it now */
    public static void overLeave()
    {
        if (!respawn.banked && META != null && META.on && runLink == null && player != null && player.dead) { respawn.banked = true; metaBank(); }
        document.getElementById("overScreen").classList.remove("peek");
    }
    /* THE LOG, from the death screen: the plate steps aside and the combat log opens
       on what you TOOK; BACK brings the plate back */
    public static void overPeek(bool on)
    {
        var ov = document.getElementById("overScreen");
        ov.classList.toggle("peek", on);
        if (on) { if (!clogOn) clogToggle(); clogView = CLOG_IN; }
    }

    /* =============================== MAIN LOOP =============================== */
    public static double last = double.NaN, sizeCheck = 0;
    public static void update(double dt, double raw)
    {
        cam.shake = Math.max(0, cam.shake - raw * 2.4);
        if (state == "title")
        {
            titleClock += raw;
            foreach (var e in enemies.Live()) if (e.docile) updateEnemy(e, raw);
            enemies = enemies.Where(e => !e.dead).ToList();
            updateFx(raw);
            player.x = CX; player.y = CY - 30;
            player.face = new Pt(Math.cos(vtime * 0.4), Math.sin(vtime * 0.4));
            updateCamera(raw, CX + Math.cos(vtime * 0.07) * 140, CY + Math.sin(vtime * 0.09) * 100);
        }
        else if (state == "play")
        {
            runTime += dt;
            navBudget = NAV_BUDGET;
            mouse.wx = cam.x + (mouse.x - cw / 2) / zoom;
            mouse.wy = cam.y + (mouse.y - ch / 2) / zoom;
            updatePlayer(dt);
            egridBuild();
            flowFresh(dt);
            foreach (var e in enemies.Live()) updateEnemy(e, dt);
            separate();
            enemies = enemies.Where(e => !e.dead).ToList();
            updateMinions(dt);
            updateCorpses(dt);
            updateProjectiles(dt);
            if (rpgOn()) rpgTick(dt); else updateWave(dt);
            updateBreach(dt);
            updateAbyss(dt);
            updateStrongboxes(dt);
            updateRitual(dt);
            ritualClamp();
            updateRareMods(dt);
            updateSpecials(dt);
            updateBloodOrbs(dt);
            updateLegion(dt);
            updateGearDrops(dt);
            updateFx(dt);
            if (player.swing != null)
            {
                var sw = player.swing;
                foreach (var pr in props.Live())
                {
                    if (pr.dead || pr.indestructible || sw.hitP.Contains(pr)) continue;
                    double d = dist(pr, player);
                    if (d < sw.reach + pr.r)
                    {
                        bool within = sw.spin ? sw.t / sw.dur > 0.12 : inSweep(angTo(player, pr), sw.from, sw.to, sw.t / sw.dur, 0.5);
                        if (within)
                        {
                            sw.hitP.Add(pr); pr.hp--; pr.flash = 1;
                            addSparks(pr.x, pr.y - 10, 3); SFX.hit();
                            if (pr.hp <= 0) breakProp(pr);
                        }
                    }
                }
            }
            updateCamera(raw, player.x, player.y);
            if (pendingPicks.Count > 0 && !player.dead)
            {
                if (OPT.autoLoot) lootNow();
                /* a chain started by R (or a held R) keeps opening, a breath apart */
                else if (lootChain || (lootHeld && key("KeyR"))) { lootGapT -= raw; if (lootGapT <= 0) lootNow(); }
            }
            else lootChain = false;
        }
    }
    public static void frame(double now)
    {
        requestAnimationFrame(frame);
        if (double.IsNaN(last)) last = now;
        double raw = Math.min((now - last) / 1000, 0.05); last = now;
        frameRaw = raw;
        vtime += raw;
        sizeCheck -= raw;
        if (sizeCheck <= 0)
        {
            sizeCheck = 0.5;
            double iw = window.innerWidth, ih = window.innerHeight;
            if (iw > 0 && ih > 0 && (iw != cw || ih != ch)) resizeNow();
        }
        double dt = raw * tScale;
        tScale += (1 - tScale) * Math.min(1, raw * 3);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { update(dt, raw); } catch (Exception err) { Console.Error.WriteLine("update: " + err); }
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!Js.IsFinite(cam.x) || !Js.IsFinite(cam.y) || cam.x < -120 || cam.x > W.w + 120 || cam.y < -120 || cam.y > W.h + 120)
        {
            cam.x = player != null ? player.x : W.w / 2;
            cam.y = player != null ? player.y : W.h / 2;
            reclampCam();
        }
        long g1 = Gpu.GlTicks;
        try { render(); } catch (Exception err) { Console.Error.WriteLine("render: " + err); }
        perfGl += (Gpu.GlTicks - g1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (state == "title") { try { renderTitle(); } catch (Exception err) { Console.Error.WriteLine("title: " + err); } }
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        /* NATIVE: what the frame cost, for the F3 readout (smoothed) */
        double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        updMs += ((t1 - t0) * f - updMs) * 0.05; renMs += ((t2 - t1) * f - renMs) * 0.05;
        perfUpd += (t1 - t0) * f; perfRen += (t2 - t1) * f; perfN++; perfEn += enemies.Count;
        perfMax = Math.Max(perfMax, (t2 - t0) * f);
    }
    public static double perfUpd, perfRen, perfEn, perfGl, perfMax; public static int perfN;
    public static double updMs, renMs;
    /* JS 32431–32443: the script's last statements */
    public static void GameBoot()
    {
        metaLoad();          /* before the first hero, so nothing is stood up half-built */
        gearLoad();          /* ...and the gear, for the same reason */
        gearWire();
        resetRun(true);
        buildFavRow();
        buildModeRow();
        buildDiffRow();
        buildWeaponRow();
        refreshTreeBtn();
        refreshGearBtn();
        showBest();
        fitTitle();
        requestAnimationFrame(frame);
        /* opened on a run link: that run, at that wave, with that build */
        runLinkBoot();
    }
}
