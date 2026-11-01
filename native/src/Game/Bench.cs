// Port of lego-survival.html lines 18278-18844: the TEST BENCH (grant and revoke
// bricks, leagues on demand, the sandbox and its monster bands, the league tree
// and rare modifiers), the favourite row, how often a card turns up, drawing a
// reward screen's cards, and the live readout of every owned spell.
namespace Brickblade;

public sealed partial class Choice { public Upgrade t; public string rar; public Calc c; }
sealed class MobBand { public double lo, hi; public string name; public List<string> keys = new(); }

public static partial class G
{
    /* =============================== TEST BENCH =============================== */
    /* A developer shortcut: hand yourself any brick at epic and max it out, or jump
       straight to a wave. Type 1337 during play to open it. */
    /* One click, one pick — the bench hands out a single brick at a time so you can
       walk a stat up and watch it change, and it stops dead at the same ceiling the
       real run does. Right-click takes every copy of that brick back out. */
    public static void grantOne(string id, string rar = null)
    {
        var u = UPGRADES.Find(x => x.id == id);
        if (u == null) return;
        try { if (u.req != null && !u.req()) { buildCheatRows(); return; } } catch { return; }
        /* the bench's own tier unless a caller names one — granting a brick at a
           chosen rarity is the only way to stand up a hero with a REAL mixed stack */
        applyPick(u, (Js.T(rar) && RARITY.ContainsKey(rar)) ? rar : benchRarOf(u));
        buildCheatRows();
    }
    public static void revokeAll(string id)
    {
        if (taken[id] == 0) return;
        rebuildWithout(id);
        buildCheatRows();
    }
    /* THE BENCH'S LEAGUES: one put down now, whatever the wave -- sandbox, boss
       wave or wave 1 alike, none of which the league roll would ever allow */
    public static HashSet<string> benchMods = new(); public static string benchLastMob = "knight";
    public static bool benchLeague(string kind, string arg = null)
    {
        var p = player;
        if (p == null) return false;
        Pt near(double lo, double hi)
        {
            for (int i = 0; i < 60; i++)
            {
                double a = rnd(TAU), r = rnd(lo, hi);
                double x = p.x + Math.Cos(a) * r, y = p.y + Math.Sin(a) * r; if (abyssFloorOk(x, y, 60)) return new Pt { x = x, y = y };
            }
            return new Pt { x = clamp(p.x + lo, W.wall + 60, W.w - W.wall - 60), y = p.y };
        }
        if (kind == "breach")
        {
            var free = BREACH_LORD_KEYS.Where(q => !allBreaches().Any(B => B != null && B.lord == q)).ToList();
            if (free.Count == 0) return false;
            var Bn = breachNewAt(Js.T(arg) ? arg : free[(int)(Js.R() * free.Count)]); Bn.delay = 0;
            if (breach == null) breach = Bn; else breachExtra.Add(Bn);
            return true;
        }
        if (kind == "abyss") { var A = abyssNew(); A.delay = 0; if (abyss == null) abyss = A; else abyssExtra.Add(A); return true; }
        if (kind == "ritual") { if (!ritualCanRise()) return false; ritualS.pending = null; return ritualPlaceSet(); }
        if (kind == "legion")
        {
            if (legionActiveNow() || (legionEv != null && legionEv.state == "idle")) return false;
            legionEv = new Legion { state = "pending", delay = 0, n = wave.n, t = 0, seed = rnd(1000) };
            return legionPlace();
        }
        if (kind == "sbox") return sboxPlace(arg) != null;
        if (kind == "chest" || kind == "mimic")
        {
            var at = near(160, 260);
            props.Add(shoveToFloor(new Prop { type = "chest", x = at.x, y = at.y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16,
                rar = Js.T(arg) ? arg : rollRarity("chest"), mimic = kind == "mimic" }, 20));
            return true;
        }
        if (kind == "goblin") { var at = near(320, 420); goblinSpawn(at.x, at.y); return true; }
        if (kind == "orbs") { for (int i = 0; i < 3; i++) { var at = near(260, 340); orbSpawn(at.x, at.y, Js.T(arg) ? arg : "blood", Math.Max(1, 1 + wave.n * 0.3), null); } return true; }
        return false;
    }
    public static void buildCheatRows()
    {
        var waves = document.getElementById("cheatWaves");
        if (waves == null) return;
        waves.innerHTML = string.Join("", new[] { 1, 5, 10, 15, 20, 25, 30, 40, 50 }.Select(n =>
          $"<div class=\"cheatbtn\" data-wave=\"{n}\">WAVE {n}</div>"));
        foreach (var el in waves.children)
            el.addEventListener("click", _ =>
            {
                double n = Js.ToNumber(el.dataset["wave"]);
                enemies.Clear(); projectiles.Clear(); hazards.Clear();
                startWave(n); SFX.click(); toggleCheat();
            });

        var diffs = document.getElementById("cheatDiffs");
        diffs.innerHTML = string.Join("", DIFFS.Keys.Select(id =>
          $"<div class=\"cheatbtn{(difficulty == DIFFS[id] ? " got" : "")}\" data-diff=\"{id}\">{DIFFS[id].name}</div>"));
        foreach (var el in diffs.children)
            el.addEventListener("click", _ => { setDifficulty(el.dataset["diff"]); SFX.click(); buildCheatRows(); });

        var ups = document.getElementById("cheatUps");
        /* The bench used to be one long undifferentiated wall of cards in whatever
           order UPGRADES happens to be written in. It is filed now: the sword first,
           then damage, crit, defence, utility, and one section per SPELL with that
           spell's own bricks — its legendary last, because that is what you scroll
           looking for. Inside a section the order is common brick first, rarest last. */
        string cardHtml(Upgrade u)
        {
            double owned = taken[u.id];
            /* the bench can only hand out what the run could actually reach, so a brick
               sitting at its cap is shown greyed out rather than clickable */
            bool open = true;
            try { open = u.req == null || u.req(); } catch { open = false; }
            string rar = benchRarOf(u);
            var R = RARITY[rar];
            var (lo, hi) = benchRange(u);
            string body = "";
            try { body = esc(u.desc(u.calc(R.mult), owned)); } catch { body = ""; }
            return $@"<div class=""card mini{(open ? "" : " capped")}"" data-up=""{u.id}"">
      {(owned != 0 ? $"<div class=\"stack\">x{S(owned)}</div>" : "")}
      <div class=""band"" style=""background:{R.col}""><i></i><i></i><i></i></div>
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""{R.col}"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(u.icon ?? "") ?? ""}</svg>
      <h3>{esc(u.name)}</h3><p>{body}</p>
      {weaponTagHtml(u)}
      <div class=""rtag"" style=""background:{R.col}"">{R.name}</div>
      {(hi > lo ? "<div class=\"rscroll\">⇅ shift+scroll</div>" : "")}
      <div class=""ctag"" style=""background:{CLS_COL.GetValueOrDefault(u.cls ?? "") ?? "#555"}"">{u.cls}</div>
    </div>";
        }
        var byId = new Dictionary<string, Upgrade>(); foreach (var u in UPGRADES) byId[u.id] = u;
        var used = new HashSet<string>();
        List<Upgrade> take(List<string> ids) => ids.Select(id => byId.GetValueOrDefault(id)).Where(u =>
        {
            if (u == null || used.Contains(u.id)) return false;
            used.Add(u.id); return true;
        }).ToList();
        var sections = new List<(string name, List<Upgrade> list)>();
        foreach (var s in BENCH_SECTIONS) sections.Add((s.name, take(s.ids)));
        /* anything new that has not been filed yet still shows up, at the bottom */
        var rest = UPGRADES.Where(u => !used.Contains(u.id)).ToList();
        if (rest.Count > 0) sections.Add(("EVERYTHING ELSE", rest));
        ups.innerHTML = string.Join("", sections.Where(s => s.list.Count > 0).Select(s =>
          $@"<div class=""benchsec""><div class=""benchhead"">{s.name}</div>
      <div class=""benchrow"">{string.Join("", s.list.Select(cardHtml))}</div></div>"));
        foreach (var el in ups.querySelectorAll(".card.mini"))
        {
            var u = byId.GetValueOrDefault(el.dataset["up"] ?? "");
            el.addEventListener("click", _ =>
            {
                if (el.classList.contains("capped")) { SFX.click(); return; }
                cmpHide(); grantOne(el.dataset["up"]); SFX.pick();
            });
            el.addEventListener("contextmenu", ev =>
            {
                ev.preventDefault();
                cmpHide(); revokeAll(el.dataset["up"]); SFX.click();
            });
            if (u == null) continue;
            /* the bench gets the same now -> next panel the reward screen has, at
               whatever tier this card is currently being read at */
            string rar = benchRarOf(u);
            cmpWire(el, u, u.calc(RARITY[rar].mult), u.name + " · " + RARITY[rar].name);
            el.addEventListener("wheel", ev =>
            {
                if (!ev.shiftKey) return;
                ev.preventDefault();
                if (!benchRarStep(u, ev.deltaY > 0 ? -1 : 1)) return;
                SFX.click();
                /* rebuild, then put the panel back over the card the mouse is still on */
                buildCheatRows();
                var again = document.querySelector($"#cheatUps .card.mini[data-up=\"{u.id}\"]");
                if (again != null)
                {
                    string r2 = benchRarOf(u);
                    try { cmpShow(u, u.calc(RARITY[r2].mult), again, u.name + " · " + RARITY[r2].name); }
                    catch { cmpHide(); }
                }
            }, new { passive = false });
        }

        /* --- SANDBOX ------------------------------------------------------------
           Every monster in the game, by name, in the order the roster introduces
           them, then the siege units and every boss. Built from the same tables the
           waves are built from, so a monster added later turns up here for free. */
        var sand = document.getElementById("cheatSandbox");
        sand.innerHTML =
          $"<div class=\"cheatbtn{(sandbox ? " got" : "")}\" data-sand=\"toggle\">SANDBOX: {(sandbox ? "ON" : "OFF")}</div>" +
          "<div class=\"cheatbtn\" data-sand=\"clear\">CLEAR THE FIELD</div>";
        foreach (var el in sand.children)
            el.addEventListener("click", _ =>
            {
                if (el.dataset["sand"] == "toggle")
                {
                    sandbox = !sandbox;
                    if (sandbox)
                    {
                        /* stop whatever the wave was doing and leave the field to you */
                        wave.queue.Clear();
                        foreach (var e in enemies) e.dead = true;
                        wave.state = "fight";
                    }
                }
                else
                {
                    foreach (var e in enemies) e.dead = true;
                    projectiles.Clear(); hazards.Clear();
                }
                SFX.click(); buildCheatRows();
            });

        var spawn = document.getElementById("cheatSpawn");
        /* FILED BY WHEN THEY TURN UP. One flat wall of forty names told you nothing
           about the shape of a run; in bands of five you can see the roster arriving.
           Anything with no roster entry (the golems) goes in its own band at the end,
           because golems are seeded into waves rather than bought from the budget. */
        var bossKeys = BOSSES.Keys.Concat(SUPERBOSSES.Keys).ToList();
        var bands = new List<MobBand>();
        var rostered = ROSTER.ToList().SortJs((a, b) => a.at - b.at);
        foreach (var r in rostered)
        {
            double lo = Math.Max(1, Math.Floor((r.at - 1) / 5) * 5 + 1);
            var band = bands.Find(x => x.lo == lo);
            if (band == null) { band = new MobBand { lo = lo, hi = lo + 4 }; bands.Add(band); }
            band.keys.Add(r.k);
        }
        bands = bands.SortJs((a, b) => a.lo - b.lo);
        bands.Add(new MobBand { lo = 0, hi = 0, name = "GOLEMS &middot; seeded into waves, not bought", keys = new() { "stonegolem", "icegolem", "firegolem", "stormgolem" } });
        bands.Add(new MobBand { lo = 0, hi = 0, name = "SPECIALS &middot; not bought from the budget", keys = new() { "goblin", "mimic", "warbanner", "kin:pack" } });
        bands.Add(new MobBand { lo = 0, hi = 0, name = "BOSSES", keys = bossKeys.Select(k => "boss:" + k).ToList() });
        spawn.innerHTML = string.Join("", bands.Select(b =>
          $"<div class=\"mobband\"><div class=\"mobbandhead\">{(b.name ?? ("WAVE " + S(b.lo) + "&ndash;" + S(b.hi)))}</div><div class=\"mobbandrow\">" +
          string.Join("", b.keys.Select(k =>
          {
              if (k.StartsWith("boss:"))
              {
                  string bk = k.Substring(5);
                  return $"<div class=\"cheatbtn got mob\" data-mob=\"boss\" data-boss=\"{bk}\">{esc((BOSSES.GetValueOrDefault(bk) ?? SUPERBOSSES[bk]).name)}</div>";
              }
              if (k == "kin:pack") return $"<div class=\"cheatbtn\" data-kin=\"1\">KINDRED PACK ({esc(benchLastMob)})</div>";
              return $"<div class=\"cheatbtn mob\" data-mob=\"{k}\">{esc(k)}</div>";
          })) + "</div></div>"));
        foreach (var el in spawn.querySelectorAll(".cheatbtn.mob"))
        {
            void put(int n, bool elite)
            {
                var p = player;
                for (int i = 0; i < n; i++)
                {
                    double a = (double)i / n * TAU + rnd(-0.2, 0.2);
                    double d = n == 1 ? 190 : rnd(170, 280);
                    double x = p.x + Math.Cos(a) * d, y = p.y + Math.Sin(a) * d;
                    if (el.dataset["mob"] == "goblin") { goblinSpawn(x, y); continue; }
                    rareModForce = elite && benchMods.Count > 0 ? benchMods.ToList() : null;
                    try { spawnEnemy(el.dataset["mob"], x, y, elite, false, el.dataset["boss"]); } finally { rareModForce = null; }
                    var e = enemies.Count > 0 ? enemies[^1] : null;
                    if (e != null && e.type == "mimic" && !Js.T(e.mimicRar)) e.mimicRar = "rare";
                }
                if (el.dataset["boss"] == null && el.dataset["mob"] != "goblin")
                {
                    string was = benchLastMob; benchLastMob = el.dataset["mob"];
                    var kb = spawn.querySelector("[data-kin]"); if (kb != null && was != benchLastMob) kb.textContent = "KINDRED PACK (" + benchLastMob + ")";
                }
                SFX.click();
            }
            el.addEventListener("click", ev => put(1, ev.shiftKey));
            el.addEventListener("contextmenu", ev => { ev.preventDefault(); put(5, ev.shiftKey); });
            /* the same now -> next panel the bricks use, filled with what this body IS */
            string key = el.dataset["boss"] ?? el.dataset["mob"];
            el.addEventListener("mouseenter", _ => { try { mobTipShow(el, key); } catch { cmpHide(); } });
            el.addEventListener("mouseleave", _ => cmpHide());
        }
        var kinBtn = spawn.querySelector("[data-kin]");
        if (kinBtn != null) kinBtn.addEventListener("click", _ =>
        {
            var p = player; double a = rnd(TAU);
            var pk = spawnMagicPack(benchLastMob, new Pt { x = clamp(p.x + Math.Cos(a) * 260, W.wall + 80, W.w - W.wall - 80), y = clamp(p.y + Math.Sin(a) * 260, W.wall + 80, W.h - W.wall - 80) }, 6, true);
            foreach (var e in pk) e.state = "active";
            SFX.click();
        });

        var misc = document.getElementById("cheatMisc");
        misc.innerHTML = string.Join("", new[] {
          ("heal", "FULL HEARTS"), ("hearts", "+50 MAX HEARTS"), ("studs", "+1000 STUDS"),
          ("level", "+5 LEVELS"), ("clear", "CLEAR THE FIELD"), ("god", "TOGGLE GOD MODE"),
          ("buffs", "DROP EVERY BUFF") }.Select(t => $"<div class=\"cheatbtn{(t.Item1 == "god" && godMode ? " got" : "")}\" data-misc=\"{t.Item1}\">{t.Item2}</div>"));
        foreach (var el in misc.children)
            el.addEventListener("click", _ =>
            {
                string k = el.dataset["misc"]; var p = player;
                if (k == "heal") p.hp = p.maxHp;
                else if (k == "hearts") { addMaxHp(50); p.hp = p.maxHp; }
                else if (k == "studs") p.studs += 1000;
                else if (k == "level") { for (int i = 0; i < 5; i++) { p.level++; p.incAll += 0.05; } }
                else if (k == "clear") { foreach (var e in enemies) e.dead = true; }
                else if (k == "god") godMode = !godMode;
                else if (k == "buffs")
                {
                    /* one of each ground buff, in a ring, for testing what they stack into */
                    var kinds = new[] { "magnet", "fortune", "rage", "ward", "gheart", "heart" };
                    for (int i = 0; i < kinds.Length; i++)
                    {
                        double a = (double)i / kinds.Length * TAU;
                        pickups.Add(makePickup(kinds[i], p.x + Math.Cos(a) * 70, p.y + Math.Sin(a) * 70));
                    }
                }
                SFX.click(); buildCheatRows();
            });

        /* --- LEAGUES ------------------------------------------------------------ */
        var lg = document.getElementById("cheatLeagues");
        if (lg != null)
        {
            var LB = new[] { ("breach", "", "BREACH"), ("abyss", "", "ABYSS"), ("ritual", "", "RITUAL (3 ALTARS)"), ("legion", "", "LEGION MONOLITH"),
              ("sbox", "plain", "STRONGBOX"), ("sbox", "magic", "MAGIC STRONGBOX"), ("sbox", "rare", "RARE STRONGBOX"), ("sbox", "epic", "EPIC STRONGBOX"),
              ("chest", "", "CHEST"), ("mimic", "", "MIMIC CHEST"), ("goblin", "", "TREASURE GOBLIN"), ("orbs", "blood", "BLOOD ORBS ×3"), ("orbs", "chaos", "PLAGUE ORBS ×3") };
            lg.innerHTML = string.Join("", LB.Select(t => $"<div class=\"cheatbtn\" data-lg=\"{t.Item1}\" data-arg=\"{t.Item2}\">{t.Item3}</div>"));
            foreach (var el in lg.children)
                el.addEventListener("click", _ =>
                {
                    string arg = el.dataset["arg"];
                    bool ok = benchLeague(el.dataset["lg"], Js.T(arg) ? arg : null);
                    if (!ok) addHitNum(player.x, player.y - 60, "NOT NOW", "#9aa39c", 14);
                    SFX.click();
                });
        }

        /* --- THE LEAGUE TREE ------------------------------------------------------ */
        var lt = document.getElementById("cheatLtree");
        if (lt != null)
        {
            string tip(LtNode n) { var s = string.Join(" · ", n.stats.Select(kv => LT_STATS[kv.Key].txt(kv.Value))); return s.Length > 0 ? s : "where every road starts"; }
            lt.innerHTML =
              $"<div class=\"cheatbtn{(LEAGUE_TREE_ON ? " got" : "")}\" data-lt=\"toggle\">LEAGUE TREE: {(LEAGUE_TREE_ON ? "ON" : "OFF")}</div>" +
              $"<div class=\"cheatbtn\" data-lt=\"pts\">+5 POINTS ({S(LTREE.pts)})</div>" +
              "<div class=\"cheatbtn\" data-lt=\"all\">TAKE EVERY NODE</div>" +
              "<div class=\"cheatbtn\" data-lt=\"reset\">RESET THE TREE</div>" +
              string.Join("", LEAGUE_TREE.Where(n => !n.hub).Select(n => $"<div class=\"cheatbtn{(LTREE.nodes.Contains(n.id) ? " got" : "")}\" data-lt=\"node\" data-node=\"{n.id}\" title=\"{esc(tip(n))}\">" +
                esc((n.notable ? "★ " : "") + n.name) + "</div>"));
            foreach (var el in lt.children)
                el.addEventListener("click", _ =>
                {
                    string k = el.dataset["lt"];
                    if (k == "toggle") LEAGUE_TREE_ON = !LEAGUE_TREE_ON;
                    else if (k == "pts") { LTREE.pts += 5; ltreeSave(); }
                    else if (k == "all")
                    {
                        for (int pass = 0; pass < LEAGUE_TREE.Count; pass++)
                            foreach (var n in LEAGUE_TREE)
                                if (!LTREE.nodes.Contains(n.id) && n.links.Any(l => LTREE.nodes.Contains(l))) LTREE.nodes.Add(n.id);
                        ltreeRebuild(); ltreeSave();
                    }
                    else if (k == "reset") { LTREE = new LtreeState { pts = 0, nodes = new() { "lt_root" } }; ltreeRebuild(); ltreeSave(); }
                    else if (k == "node")
                    {
                        string id = el.dataset["node"];
                        if (LTREE.nodes.Contains(id)) ltreeRefund(id);
                        else { if (LTREE.pts < 1) LTREE.pts++; if (!ltreeTake(id) && LTREE.pts > 0) LTREE.pts--; }
                    }
                    SFX.click(); buildCheatRows();
                });
        }

        /* --- RARE MODIFIERS FOR THE NEXT ELITES ---------------------------------- */
        var rm = document.getElementById("cheatRareMods");
        if (rm != null)
        {
            rm.innerHTML = string.Join("", RARE_MODS.Select(m => $"<div class=\"cheatbtn{(benchMods.Contains(m.id) ? " got" : "")}\" data-rm=\"{m.id}\" title=\"{esc(m.tip)}\" style=\"color:{m.col}\">{m.name}</div>")) +
              "<div class=\"cheatbtn\" data-rm=\"\">CLEAR (ROLL AS IN A RUN)</div>";
            foreach (var el in rm.children)
                el.addEventListener("click", _ =>
                {
                    string id = el.dataset["rm"];
                    if (!Js.T(id)) benchMods.Clear(); else if (benchMods.Contains(id)) benchMods.Remove(id); else benchMods.Add(id);
                    SFX.click(); buildCheatRows();
                });
        }
    }
    public static void toggleCheat()
    {
        if (state == "play")
        {
            buildCheatRows();
            document.getElementById("cheatScreen").classList.remove("hidden");
            state = "cheat"; cvEl.style.cursor = "default";
        }
        else if (state == "cheat")
        {
            document.getElementById("cheatScreen").classList.add("hidden");
            state = "play"; cvEl.style.cursor = "none";
        }
    }
    public static void buildFavRow()
    {
        var row = document.getElementById("favRow");
        if (row == null) return;
        row.innerHTML = string.Join("", FAVOURITES.Where(favAllowed).Select(f =>
        {
            var perk = FAV_PERKS.GetValueOrDefault(f.id);
            string tip = perk != null ? "YOU START WITH\n" + string.Join("\n", favLines(perk).Select(l => "· " + l)) +
              (f.ids.Count > 0 ? "\n\nand its bricks come up more often" : "") : "";
            return $@"<div class=""fav"" data-fav=""{f.id}""{(tip.Length > 0 ? $" data-tip=\"{esc(tip)}\"" : "")}>
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""#ffd76e"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(f.icon ?? "") ?? ""}</svg>
      <span>{f.name}</span>
    </div>";
        }));
        foreach (var el in row.children)
            el.addEventListener("click", _ => { SFX.click(); setFavourite(el.dataset["fav"]); });
        string saved = "none";
        try { saved = Store.getItem("bb_fav") ?? "none"; } catch { }
        setFavourite(saved);
    }
    /* HOW OFTEN A BRICK TURNS UP among the others its rarity allows.
       CONVERSIONS are build-defining, not filler: half as often, and the ones that
       end in CHAOS -- nothing resists it -- rarer still.
       DEFENCES AND ATTRIBUTES FOLLOW THE HAND. A sword or mace is armour and
       strength, an axe or bow evasion and dexterity, a staff or scepter energy
       shield and intelligence: the weapon's own kind is the usual drop, the other
       two still turn up, only less often. */
    public const double CONV_WEIGHT = 0.5, CHAOS_CONV_WEIGHT = 0.25;
    public static readonly HashSet<string> CHAOS_CONV = new() { "convchaos", "gainchaos", "convrot", "gainrot" };
    public static readonly Dictionary<string, string> HOME_ATTR = new() { ["sword"] = "str", ["mace"] = "str", ["axe"] = "dex", ["bow"] = "dex", ["staff"] = "int", ["scepter"] = "int" };
    public static readonly Dictionary<string, string> DEF_FAMILY = new()
    {
        ["armor"] = "str", ["armorinc"] = "str", ["attstr"] = "str",
        ["eva"] = "dex", ["evainc"] = "dex", ["suppress"] = "dex", ["attdex"] = "dex",
        ["es"] = "int", ["esinc"] = "int", ["attint"] = "int",
    };
    public const double HOME_WEIGHT = 1.5, AWAY_WEIGHT = 0.35;
    public static readonly HashSet<string> AIL_CHANCE_CARDS = new() { "ignitechance", "chillchance", "shockchance", "poisonchance", "bleedchance" };
    public static double cardWeight(Upgrade u)
    {
        double w = 1;
        if (AIL_CHANCE_CARDS.Contains(u.id)) w *= AIL_CARD_W;
        if (Js.T(u.need)) w *= CHAOS_CONV.Contains(u.id) ? CHAOS_CONV_WEIGHT : CONV_WEIGHT;
        string fam = DEF_FAMILY.GetValueOrDefault(u.id); string home = weapon != null ? HOME_ATTR.GetValueOrDefault(weapon.id) : null;
        if (fam != null && home != null) w *= fam == home ? HOME_WEIGHT : AWAY_WEIGHT;
        return w;
    }
    /* ONE OF EACH, AT MOST, of DEFENSE and UTILITY on a screen: two ways to survive
       side by side is a choice between two halves of the same thing. Offence may
       come twice. */
    public static readonly HashSet<string> ONE_PER_SCREEN = new() { "DEFENSE", "UTILITY" };
    public static void pickChoices(string mode)
    {
        /* capped stats remove their rewards from the pool entirely */
        /* every ceiling in the game is a `req` on its own brick, so one filter is the
           whole rule: anything already maxed is not in the pool and cannot be offered */
        var pool = UPGRADES.Where(u => { try { return u.req == null || u.req(); } catch { return false; } }).ToList();
        double want = mode == "boss" ? 4 : mode == "chest" ? (player != null ? Js.Or(player.chestCards, 1) : 1) : 3;
        /* A CHEST IS A REWARD FOR EXPLORING, not a way to reroll your build. It
           deepens what you already cast rather than handing you a spell you then have
           to find room and bricks for. */
        var draw = mode == "chest" ? pool.Where(u => !grantsNewSpell(u)).ToList() : pool;
        choices = new List<Choice>();
        /* ONE CARD LEANS ON THE FAVOURITE, NOT ALL THREE. Weighting every draw meant
           a favourite's own bricks crowded the whole screen — pick Elemental Arts and
           all three slots filled with elemental increases, so a new spell almost never
           got a look in. The first card drawn is the weighted one; the rest are flat. */
        bool favUsed = false;
        Upgrade take(List<Upgrade> from, bool forceFlat = false)
        {
            if (from.Count == 0) return null;
            bool weighted = !forceFlat && !favUsed;
            /* roll the rarity FIRST, then draw only from what that tier is allowed to offer */
            string rar = forcedRarity ?? rollRarity(mode);
            var cands = from.Where(u => rarAllows(u, rar)).ToList();
            for (int tries = 0; cands.Count == 0 && tries < 4 && forcedRarity == null; tries++)
            {
                rar = rollRarity(mode);
                cands = from.Where(u => rarAllows(u, rar)).ToList();
            }
            if (cands.Count == 0)
            {
                /* nothing to offer at that tier — walk the roll DOWN until something fits.
                   A legendary roll with every legendary already owned must still hand you a
                   card, not quietly leave the slot empty. */
                for (int rk = (int)rarRank(rar); rk >= 0 && cands.Count == 0; rk--)
                {
                    rar = RAR_ORDER[rk];
                    cands = from.Where(u => rarAllows(u, rar)).ToList();
                }
                if (cands.Count == 0) return null;
            }
            /* favourite bricks are drawn heavier on the ONE weighted slot, never guaranteed */
            double wOf(Upgrade u) => ((weighted && favSet.Contains(u.id)) ? FAV_WEIGHT : 1) * shunMul(u.id) * cardWeight(u);
            double total = 0;
            foreach (var u in cands) total += wOf(u);
            double r = Js.R() * total; var t = cands[^1];
            foreach (var u in cands)
            {
                r -= wOf(u);
                if (r < 0) { t = u; break; }
            }
            if (weighted && favSet.Contains(t.id)) favUsed = true;
            int pi = pool.IndexOf(t);
            if (pi >= 0) pool.RemoveAt(pi);
            /* the chest draws from a filtered copy, so it has to be told too */
            if (draw != pool) { int di = draw.IndexOf(t); if (di >= 0) draw.RemoveAt(di); }
            /* a defence or a utility on the screen takes the rest of its class off it */
            if (ONE_PER_SCREEN.Contains(t.cls))
                foreach (var arr in (draw != pool ? new[] { pool, draw } : new[] { pool }))
                    for (int i = arr.Count - 1; i >= 0; i--) if (arr[i].cls == t.cls) arr.RemoveAt(i);
            choices.Add(new Choice { t = t, rar = rar, c = t.calc(RARITY[rar].mult) });
            return t;
        }
        /* A wave or level reward always offers a way to hit harder and a way to survive
           longer, so a run can't be starved of one side by bad luck. Boss and chest
           rewards stay fully random. */
        if (mode == "wave" || mode == "level")
        {
            take(pool.Where(u => u.cls == "OFFENSE" || u.cls == "SPELL").ToList());
            take(pool.Where(u => u.cls == "DEFENSE").ToList());
        }
        while (choices.Count < want && draw.Count > 0) take(draw);
        /* don't let the guaranteed pair always sit in the same slots */
        for (int i = choices.Count - 1; i > 0; i--)
        {
            int j = (int)(Js.R() * (i + 1));
            var t2 = choices[i]; choices[i] = choices[j]; choices[j] = t2;
        }
    }
    /* live damage readouts for every spell the player actually owns — the numbers here
       are computed the same way the update loop computes them, so they can't drift. */
    public static List<Ability> spellSummaries()
    {
        var p = player; var @out = new List<Ability>();
        string per(double n) => Js.ToFixed(n, 1);
        if (p.orbit.n > 0)
        {
            double d = calcDamage(p.orbit.dmg, "spell");
            double hitCd = clamp(0.5 * 2.6 / p.orbit.spd, 0.05, 0.5);
            @out.Add(new Ability
            {
                id = "orbit", icon = "orbit", name = "Guardian Brick", head = per(d) + " per hit", tip =
              $"{S(p.orbit.n)} brick{(p.orbit.n > 1 ? "s" : "")} orbiting at {S(Math.Round(p.orbit.r))} reach\n" +
              $"{per(d)} per hit — {S(p.orbit.dmg)} base × (1 + {S(Math.Round(incTotal("spell") * 100))}% increased){(moreShown() > 1 ? $" × {Js.ToFixed(moreShown(), 2)} more" : "")}\n" +
              $"A foe can be struck every {Js.ToFixed(hitCd, 2)}s" +
              (p.orbit.aegis != 0 ? $"\nAegis Ring: each brick stops one shot, then rebuilds in {S(AEGIS_REFORM)}s" : "") +
              (p.orbit.twin != 0 ? $"\nTwin Ring: {S(p.orbit.n)} more, turning the other way at {S(Math.Round(twinRadius(p.orbit)))}" : "")
            });
        }
        if (p.chain.n > 0 && p.chain.zappers > 0)
        {
            double d = shockDamage();
            double leaps = p.chain.n - 1;
            double ways = 1 + Js.Or(p.chain.fork, 0) + (p.chain.fork != 0 ? 1 : 0);
            double total = d;
            var steps = new List<string>();
            for (int i = 1; i <= leaps; i++)
            {
                double fr = chainLeapFrac(i, leaps);
                total += d * fr;
                if (steps.Count < 4) steps.Add(S(Math.Round(fr * 100)) + "%");
            }
            double zn = Math.Min(chainMaxZappers(), p.chain.zappers);
            @out.Add(new Ability
            {
                id = "chain", icon = "chain", name = "Storm Brick", head = per(d) + " opening strike", tip =
              $"{S(zn)} zapper{(zn > 1 ? "s, each on its own" : " on a")} {Js.ToFixed(p.chain.rate, 2)}s cooldown\n" +
              $"Opening strike {per(d)} — {S(p.chain.dmg)} base × (1 + {S(Math.Round(incTotal("spell") * 100))}% increased)\n" +
              (leaps > 0
                ? $"Leaps land for {string.Join(" / ", steps)}{(leaps > 4 ? " …" : "")} of the opening — a longer chain holds up better\n"
                : "No leaps yet — take another Storm Brick to start chaining\n") +
              $"{S(p.chain.n)} target{(p.chain.n > 1 ? "s" : "")} per bolt" +
              (leaps >= CHAIN_MAX_LEAPS ? $" ({S(CHAIN_MAX_LEAPS)}-leap cap reached)" : "") +
              $": {per(total)} down the chain\n" +
              $"All {S(zn)} bricks firing: {per(total * zn)}\n" +
              $"Each leap lands {Js.ToFixed(CHAIN_JUMP_GAP, 2)}s after the one before it" +
              (p.chain.fork != 0 ? $"\nCrackling Storm ×{S(p.chain.fork)}: every leap forks {S(ways)} ways " +
                $"(up to {S(CHAIN_MAX_NODES)} foes per bolt), each fork carrying a {S(ways)} way split" : "")
            });
        }
        if (p.bane.n > 0)
        {
            double d = calcDamage(p.bane.dmg, "spell");
            double deg = Math.Round(Math.Min(BANE_ARC_MAX, p.bane.arc) * 180 / Math.PI);
            double rr = Math.Round(p.bane.r);
            double total = p.bane.dot * p.bane.dotT;
            int bu = (int)p.bane.burst;
            @out.Add(new Ability
            {
                id = "bane", icon = "bane", name = "Brickbane", head = per(d) + " opening hit", tip =
              $"A {S(deg)}° wedge{(deg >= 359 ? " — all the way round you" : "")} reaching {S(rr)}px, every {Js.ToFixed(p.bane.rate, 2)}s\n" +
              $"Opening hit {per(d)} — {S(p.bane.dmg)} base × (1 + {S(Math.Round(incTotal("spell") * 100))}% increased)" +
              $"{(moreShown() > 1 ? $" × {Js.ToFixed(moreShown(), 2)} more" : "")}\n" +
              $"POISON: {Js.ToFixed(p.bane.dot, 2)}/s a stack for {Js.ToFixed(p.bane.dotT, 1)}s — {Js.ToFixed(total, 1)} a stack\n" +
              $"Every touch lays {S(p.bane.stacks)} dose{(p.bane.stacks > 1 ? "s" : "")} at once\n" +
              $"Stacks to {S(POISON_MAX * POISON_CAP_MUL[bu])} on a monster and " +
              $"{S(POISON_MAX_BOSS * POISON_CAP_MUL[bu])} on a boss: " +
              $"{Js.ToFixed(p.bane.dot * POISON_MAX * POISON_CAP_MUL[bu], 2)}/s at full depth\n" +
              (p.bane.burst != 0
                ? $"Spore Burst: a poisoned body bursts {S(Math.Round(BANE_BURST[bu] * 100))}% of the time, " +
                  $"handing all {S(POISON_MAX * POISON_CAP_MUL[bu])} stacks to everything within " +
                  $"{S(BANE_BURST_R[bu])} at {S(Math.Round(BANE_BURST_KEEP * 100))}% rate — no blast, only the rot"
                : "Every body the cloud touches takes both, and the poison keeps going once you have moved on")
            });
        }
        if (p.storm.n > 0)
        {
            double d = calcDamage(p.storm.dmg, "melee"), blades = p.storm.n * p.storm.count;
            @out.Add(new Ability
            {
                id = "storm", icon = "storm", name = "Bladestorm", head = per(d) + " per blade", tip =
              $"{S(blades)} blades every {Js.ToFixed(p.storm.rate, 2)}s\n" +
              $"{per(d)} each — {S(p.storm.dmg)} base × (1 + {S(Math.Round(incTotal("melee") * 100))}% increased)\n" +
              $"Full burst: {per(d * blades)} spread around you" +
              (p.storm.boomer > 0 ? $"\nReturning Blades ×{S(p.storm.boomer)}: " +
                (p.storm.boomer > 1 ? "four passes per blade — out, home, out, home"
                                    : "each blade flies home through everything again") : "")
            });
        }
        if (p.vortex.n > 0)
        {
            double d = calcDamage(p.vortex.dmg, "melee");
            double tick = 0.24 * p.vortex.hitMul;
            double dur = p.vortex.dur + (p.vortex.n - 1) * 0.4;
            int to = (int)p.vortex.totem;
            @out.Add(new Ability
            {
                id = "vortex", icon = "vortex", name = "Blade Vortex", head = per(d) + " per tick", tip =
              $"Spins for {Js.ToFixed(dur, 1)}s every {Js.ToFixed(p.vortex.rate, 2)}s\n" +
              $"{per(d)} damage per tick, one tick per foe every {Js.ToFixed(tick, 2)}s\n" +
              $"Radius {S(Math.Round(p.vortex.r + (p.vortex.n - 1) * 10))}\n" +
              (p.vortex.totem != 0 ? $"Lingering Vortex ×{S(p.vortex.totem)}: one stays behind every " +
                $"{S(TOTEM_EVERY[to])}s for {S(TOTEM_LIFE[to])}s at " +
                $"{S(Math.Round(TOTEM_MUL[to] * 100))}% damage\n" : "") +
              $"Up to {per(d * Math.Floor(dur / tick))} to a foe that stays inside"
            });
        }
        if (p.bomb.n > 0)
        {
            double d = calcDamage(p.bomb.dmg, "spell");
            string clusterLine = "";
            if (p.bomb.cluster > 0)
            {
                double tot = d, gen = d;
                for (int g = 1; g <= p.bomb.cluster; g++) { gen *= CLUSTER_FALLOFF; tot += gen * 4; }
                clusterLine = $"Cluster ×{S(p.bomb.cluster)}: 3-5 bomblets per blast at " +
                  $"{S(Math.Round(Math.Pow(CLUSTER_FALLOFF, 1) * 100))}%" +
                  (p.bomb.cluster > 1 ? $" then {S(Math.Round(Math.Pow(CLUSTER_FALLOFF, 2) * 100))}%" : "") +
                  $" — about {per(tot)} from one bomb\n";
            }
            @out.Add(new Ability
            {
                id = "bombplus", icon = "bomb", name = "Bomb Volley", head = per(d) + " per blast", tip =
              $"{S(p.bomb.n)} bomb{(p.bomb.n > 1 ? "s" : "")} of {S(bombMaxN())} every {Js.ToFixed(p.bomb.rate, 2)}s\n" +
              clusterLine +
              $"{per(d)} in a {S(Math.Round(p.bomb.r))} radius — {S(p.bomb.dmg)} base × (1 + {S(Math.Round(incTotal("spell") * 100))}% increased)\n" +
              $"Everything caught by all {S(p.bomb.n)}: {per(d * p.bomb.n)}"
            });
        }
        if (p.freeze.n > 0)
        {
            @out.Add(new Ability
            {
                id = "freeze", icon = "freeze", name = "Block Freeze", head = per(frostDamage()), tip =
              "Pure crowd control — deals no damage\n" +
              $"Radius {S(Math.Round(p.freeze.r))}, every {Js.ToFixed(p.freeze.rate, 2)}s\n" +
              $"{per(frostDamage())} FROST and {S(freezeChill())} CHILL stack{(freezeChill() > 1 ? "s" : "")} " +
              "to everything caught — the fifth stack freezes a body SOLID and leaves it " +
              $"BRITTLE (−{S(Math.Round(FROZEN_RES_STEP * 100))}% frost resistance a mark, " +
              $"{S(FROZEN_MAX)} deep)\n" +
              $"Frozen foes crawl at 45% speed for {Js.ToFixed(p.freeze.dur, 1)}s" +
              (p.freeze.stun != 0 ? $"\nAbsolute Zero: everything caught is frozen SOLID for {Js.ToFixed(FREEZE_STUN_T, 1)}s, " +
                $"and anything that DIES frozen shatters for {fmtHp(zeroShatterDmg(1))}" +
                $"–{fmtHp(zeroShatterDmg(FROZEN_MAX))} in {S(Math.Round(zeroShatterR()))}px " +
                "depending on how BRITTLE it died " +
                "— no moving, no attacks, no abilities" : "")
            });
        }
        return @out;
    }
}
