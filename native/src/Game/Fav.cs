// Port of lego-survival.html lines 16310-17098: favourites (their weights and
// opening kits), which weapons a brick serves, difficulty and weapon pickers,
// the weapon card, the title screen's two build panels and their tooltips.
using System.Text.RegularExpressions;

namespace Brickblade;

public sealed class FavPerk { public NumMap attr; public List<string> lines; public Action<Ent> apply; public string grants; }
public sealed class Favourite
{
    public string id, name, icon; public bool melee, swing, noaxe, axe, slam, cast, summon, bow;
    public List<string> ids = new();
}
public sealed class ComboBrick { public string id, name, icon, body; public bool open, blocked; }
public sealed class ComboPreview
{
    public double hearts, hit, rate, dps, crit, critMult, armour, speed, regen;
    public string hitLabel, rateLabel; public List<string> spells; public List<ComboBrick> bricks;
}
public sealed class CpLayer { public string key, name, tag, col; }
public sealed class Ability { public string id, icon, name, head, tip; }
public sealed class ComboLayersOut
{
    public Dictionary<string, double> bare, weap, fav, tree; public List<Ability> abilities; public object favBricks;
    public Dictionary<string, double> this[string k] => k switch { "bare" => bare, "weap" => weap, "fav" => fav, _ => tree };
}

public static partial class G
{
    public const double FAV_WEIGHT = 3.2;
    /* WHICH BRICK HANDS YOU A SPELL YOU DO NOT HAVE. The reward screen needs this to
       mark the card, and the chest needs it to refuse one — those are the only two
       places that care, so it lives here rather than as a flag on each brick. */
    public static readonly Dictionary<string, string> SPELL_GRANT = new()
    {
        ["orbit"] = "orbit", ["chain"] = "chain", ["storm"] = "storm",
        ["vortex"] = "vortex", ["bombplus"] = "bomb", ["freeze"] = "freeze", ["bane"] = "bane",
    };
    public static bool grantsNewSpell(Upgrade u)
    {
        string k = u != null ? SPELL_GRANT.GetValueOrDefault(u.id ?? "") : null;
        return Js.T(k) && !spellHas(k);
    }
    static NumMap A3(double s, double d, double i) => new() { ["str"] = s, ["dex"] = d, ["int"] = i };
    /* WHAT YOU START WITH. Picking a favourite used to do one thing — weight its
       bricks into the reward pool — so the first five minutes of every run were
       identical. Each one now also opens with its own kit: the spell already in
       hand, and one bonus that belongs to it (usually a cap raised by one, so the
       ceiling you are building towards is yours alone). Taking no favourite trades
       all of that for a little of everything.
       `lines` is what the hover panel shows; `apply` is what actually happens. */
    public static readonly Dictionary<string, FavPerk> FAV_PERKS = new()
    {
        ["none"] = new FavPerk { attr = A3(8, 8, 8), lines = new() { "+0.5 flat physical damage", "+1 max heart", "+10 armour",
                 "+5% increased damage", "+4% move speed", "+10% studs and XP" },
            apply = p => { p.flatPhys += 0.5; p.hpFlat += 1; p.hp += 1; p.armourFlat += 10;
                p.incAll += 0.05; p.inc["speed"] += 0.04; p.inc["studs"] += 0.10; p.xpInc += 0.10; } },
        ["sword"] = new FavPerk { attr = A3(16, 8, 0), lines = new() { "+1 flat physical damage", "+8% swing rate", "+8 melee reach" },
            apply = p => { p.flatPhys += 1; p.inc["atk"] += 0.09; p.reachFlat += 8; } },
        ["axe"] = new FavPerk { attr = A3(8, 16, 0), lines = new() { "+40% increased critical chance", "+25% critical damage",
                "+1 to the maximum stack of MOMENTUM" },
            apply = p => { p.incCrit += 0.40; p.critMultFlat += 25; p.stackMax["momentum"] += 1; } },
        ["slam"] = new FavPerk { attr = A3(24, 0, 0), lines = new() { "+1 flat damage to every tooth of the slam", "+20° wider fault",
                 "+15% slam range" },
            apply = p => { p.slam.flat += 1; p.slam.arcFlat += 20 * Math.PI / 180; p.slam.rangeInc += 0.15; } },
        ["orbit"] = new FavPerk { attr = A3(12, 0, 12), grants = "orbit", lines = new() { "Start with 1 guardian brick", "+1 to the guardian brick cap (16)",
                  "+10% ring reach" },
            apply = p => { p.capUp["orbit"] += 1; p.orbit.n = 1; p.orbit.r = Math.Round(p.orbit.r * 1.10); } },
        ["chain"] = new FavPerk { attr = A3(0, 8, 16), grants = "chain", lines = new() { "Start with 1 storm brick", "+1 to the hovering-brick cap (6)",
                  "Its bolt already leaps to a second foe" },
            apply = p => { p.capUp["chain"] += 1; p.chain.n = 2; p.chain.zappers = 1; } },
        ["storm"] = new FavPerk { attr = A3(8, 16, 0), grants = "storm", lines = new() { "Start with 1 bladestorm", "+2 blades per burst",
                  "-10% bladestorm cooldown" },
            apply = p => { p.storm.n = 1; p.storm.count += 2; p.inc["stormRate"] += 0.11; } },
        ["vortex"] = new FavPerk { attr = A3(12, 12, 0), grants = "vortex", lines = new() { "Start with 1 blade vortex", "+20% vortex radius",
                   "-10% vortex cooldown" },
            apply = p => { p.vortex.n = 1; p.vortex.r = Math.Round(p.vortex.r * 1.20); p.inc["vortexRate"] += 0.11; } },
        ["freeze"] = new FavPerk { attr = A3(0, 0, 24), grants = "freeze", lines = new() { "Start with Block Freeze", "+20% freeze radius",
                   "+0.5s freeze duration" },
            apply = p => { p.freeze.n = 1; p.freeze.r = Math.Round(p.freeze.r * 1.20); p.freeze.dur += 0.5; } },
        ["bomb"] = new FavPerk { attr = A3(0, 12, 12), grants = "bombplus", lines = new() { "Start with 1 bomb", "+1 to the compass cap (9)", "+15% blast radius" },
            apply = p => { p.capUp["bomb"] += 1; p.bomb.n = 1; p.inc["bombR"] += 0.15; } },
        ["bane"] = new FavPerk { attr = A3(0, 16, 8), grants = "bane", lines = new() { "Start with Brickbane", "The wedge opens to 90° already",
                                "+0.4 poison damage a second" },
            apply = p => { p.bane.n = 1; p.bane.arc = Math.Max(p.bane.arc, 90 * Math.PI / 180); p.baneDotFlat += 0.4; } },
        /* THE BOW'S OPENING HAND */
        ["necro"] = new FavPerk { attr = A3(0, 0, 24), lines = new() { "+1 skeleton in your legion", "+15% increased minion damage",
                  "+20% increased minion life" },
            apply = p => { p.minions.max = Math.Min(MINION_MAX_CAP, p.minions.max + 1); p.minions.inc += 0.15; p.minions.life += 0.20; } },
        ["arrow"] = new FavPerk { attr = A3(0, 24, 0), lines = new() { "+1 pierce on every arrow", "+10% attack speed",
                  "+60 accuracy" },
            apply = p => { p.bow.pierce += 1; p.inc["atk"] += 0.10; p.accFlat += 60; } },
        ["elem"] = new FavPerk { attr = A3(0, 0, 24), lines = new() { "+1 flat elemental damage", "+12% increased elemental damage",
                 "+8% increased cast speed" },
            apply = p => { p.flatElem += 1; p.inc["elem"] += 0.12; p.inc["atk"] += 0.08; } },
        ["tough"] = new FavPerk { attr = A3(24, 0, 0), lines = new() { "+2 max hearts", "+25 armour", "+8% to every resistance",
                  "+0.02 hearts/s regeneration" },
            apply = p => { p.hpFlat += 2; p.hp += 2; p.armourFlat += 25;
                foreach (var el in ELEMENTS) p.resFlat[el] += 0.08;
                p.regenFlat += 0.02; } },
    };
    /* EVERY HAND CARRIES SOME ATTRIBUTES, and every hand carries the SAME TWELVE,
       split by what the hand is: a sword leans on strength, a poison on dexterity,
       a frost caster on intelligence, and No Favourite takes four of each. Twelve
       for all of them is the balance -- no hand is ahead on attributes alone; what
       differs is which of the three defences and damage lines they feed. Written
       as data and printed from the data, so the hover line cannot drift from what
       is applied. */
    public const double FAV_ATTR_TOTAL = 24;
    public static string favAttrLine(FavPerk f)
    {
        var a = f?.attr;
        if (a == null) return "";
        var parts = new List<string>();
        if (a["str"] != 0) parts.Add("+" + S(a["str"]) + " STR");
        if (a["dex"] != 0) parts.Add("+" + S(a["dex"]) + " DEX");
        if (a["int"] != 0) parts.Add("+" + S(a["int"]) + " INT");
        return string.Join(" · ", parts);
    }
    public static List<string> favLines(FavPerk f)
    {
        if (f == null) return new();
        string a = favAttrLine(f);
        return a.Length > 0 ? f.lines.Concat(new[] { a }).ToList() : f.lines.ToList();
    }
    public static void applyFavPerks()
    {
        var f = FAV_PERKS.GetValueOrDefault(favourite.id ?? "");
        if (f != null) try { f.apply(player); } catch { }
        if (f != null && f.attr != null)
        {
            player.attr["str"] += f.attr["str"];
            player.attr["dex"] += f.attr["dex"];
            player.attr["int"] += f.attr["int"];
        }
        /* the spell the kit handed you counts as taken, so the reward screen offers
           it as the UPGRADE it would be and never again as a first pick */
        if (f != null && Js.T(f.grants)) taken[f.grants] = taken[f.grants] + 1;
        syncStats();
    }
    static List<string> L(params string[] s) => s.ToList();
    public static readonly List<Favourite> FAVOURITES = new()
    {
        new Favourite { id = "none", name = "No Favourite", icon = "nofav", ids = L() },
        /* `melee` and `cast` gate a favourite to the weapons that can actually use it:
           there is no point weighting the reward roll toward bricks you will never be
           offered. A favourite with neither flag suits any weapon. */
        new Favourite { id = "sword", name = "Sword & Steel", icon = "sword", melee = true, swing = true, noaxe = true,
            ids = L("dmg", "aspd", "reach", "crit", "critpow", "bladem", "moredmg") },
        /* THE AXE'S OWN OPENING HAND. Sword & Steel is a sword's — flat damage, swing
           rate, reach — and it was the only melee hand an axe could take, which made
           the crit weapon open like the balanced one. This leans on what the axe
           actually is: it crits twice as often and twice as hard, its backswing is a
           certain critical, and Reaving turns every critical into a thrown axe. */
        new Favourite { id = "axe", name = "Reaver’s Edge", icon = "spin", melee = true, swing = true, axe = true,
            ids = L("crit", "critpow", "reave", "aspd", "bleedchance", "bleedpow", "banedotinc", "moredmg") },
        /* the mace's own opening hand — Sword & Steel for a weapon with no sword */
        new Favourite { id = "slam", name = "Earthshaker", icon = "fist", melee = true, slam = true,
            ids = L("slampow", "slamarc", "slamrange", "slamspd", "dmg", "aspd", "bladem", "moredmg") },
        new Favourite { id = "orbit", name = "Guardian Brick", icon = "orbit", ids = L("orbit", "orbitpow", "orbitring", "orbitspd", "orbm") },
        new Favourite { id = "chain", name = "Storm Brick", icon = "chain", ids = L("chain", "chainbrick", "chainpow", "shockchance", "eleminc", "shockinc", "orbm") },
        new Favourite { id = "storm", name = "Bladestorm", icon = "storm", melee = true, ids = L("storm", "stormpow", "stormsize", "stormrate", "stormback", "bladem", "moredmg") },
        new Favourite { id = "vortex", name = "Blade Vortex", icon = "vortex", melee = true, ids = L("vortex", "vortexpow", "vortexarea", "vortexrate", "bladem", "moredmg") },
        new Favourite { id = "freeze", name = "Block Freeze", icon = "freeze", cast = true, ids = L("freeze", "freezr", "freezc", "freezf", "eleminc", "elemflat") },
        /* the caster's own opening hand — the staff equivalent of Sword & Steel */
        /* the bow's own opening hand -- the archer's Sword & Steel */
        /* the scepter's own opening hand */
        new Favourite { id = "necro", name = "Necromancer", icon = "skull", summon = true,
            ids = L("minionmax", "miniondmg", "minionlife", "minionspd", "minionflat", "miniontwin", "skelmages", "golemflame", "golemlightning", "golemice", "golemstone", "golemchaos", "golemcarrion", "boneoverseer", "castspd") },
        new Favourite { id = "arrow", name = "Deadeye", icon = "bow", bow = true,
            ids = L("arrowmulti", "arrowpierce", "arrowflat", "aspd", "crit", "critpow", "acc", "bladem", "moredmg") },
        new Favourite { id = "elem", name = "Elemental Arts", icon = "ember", cast = true,
            ids = L("elemflat", "eleminc", "firinc", "boltarea", "aspd", "crit", "critpow", "orbm", "moredmg") },
        new Favourite { id = "bomb", name = "Bomb Volley", icon = "bomb", ids = L("bombplus", "bombpow", "bombarea", "bombcd", "orbm") },
        new Favourite { id = "bane", name = "Brickbane", icon = "bane", ids = L("bane", "banearc", "baner", "banerate", "banedot", "banedotinc", "orbm") },
        new Favourite { id = "tough", name = "Tough Bricks", icon = "shield",
            ids = L("hp", "regen", "armor", "armorinc", "suppress", "resfire", "resfrost", "resshock", "resall") },
    };
    public static Favourite favourite = FAVOURITES[0];
    public static bool godMode = false;
    public static HashSet<string> favSet = new();
    /* WHICH WEAPONS A BRICK SERVES. A brick with no `weap` of its own is read off
       its `req` -- the gate is the truth, and this keeps the two from drifting
       apart. The card prints the answer so you never have to guess why something
       is not being offered. */
    public static readonly Dictionary<string, List<string>> WEAP_TAGS = new() { ["melee"] = L("SWORD", "AXE", "MACE"), ["cast"] = L("STAFF") };
    public static List<string> brickWeapons(Upgrade u)
    {
        if (Js.T(u.weap)) return WEAP_TAGS.GetValueOrDefault(u.weap) ?? WEAP_TAGS["melee"];
        /* ask the gate itself, once per weapon, with the real weapon swapped in */
        var keep = weapon; var @out = new List<string>();
        foreach (var id in WEAPON_KEYS)
        {
            weapon = WEAPONS[id];
            /* the stats have to be REBUILT for the weapon being probed, or a gate that
               reads a derived number — a cast floor, a reach cap — answers for whatever
               the hero happens to be holding instead */
            if (player != null) syncStats();
            bool ok = true;
            try { ok = u.req == null || u.req(); } catch { ok = true; }
            if (ok) @out.Add(WEAPONS[id].name);
        }
        weapon = keep;
        if (player != null) syncStats();
        return @out;
    }
    /* the little row under a card: nothing when it works with everything, the
       names when it does not — and for a conversion, what it actually NEEDS.
       A conversion out of physical is not a melee card. It was tagged SWORD AXE
       MACE because those are the weapons that happen to deal physical with nothing
       else equipped, and that reads as a weapon lock when the real rule is "you
       need a source of physical damage" — which a staff carrying Bomb Volley, or
       Bladestorm, or the Guardian Brick has. Say the rule. */
    public static string weaponTagHtml(Upgrade u)
    {
        if (Js.T(u.need)) return $"<div class=\"wreq need\"><b>needs {CONV_NAME[u.need]} damage</b></div>";
        var list = brickWeapons(u);
        if (list.Count >= WEAPON_KEYS.Count) return "";
        return $"<div class=\"wreq\">{string.Join("", list.Select(n => $"<b>{n}</b>"))}</div>";
    }
    /* which favourites this weapon can actually make use of */
    public static bool favAllowed(Favourite f)
    {
        /* the bow is its own hand: nothing that swings, nothing that casts, and
           Deadeye for nobody else */
        if (f.bow && !isArcher()) return false;
        /* the scepter's hand is the scepter's, and nothing that swings, casts or shoots is */
        if (f.summon && !isSummoner()) return false;
        if (isSummoner() && (f.melee || f.swing || f.slam || f.cast || f.bow)) return false;
        if (isArcher() && (f.melee || f.swing || f.slam)) return false;
        if (f.melee && isCaster()) return false;
        if (f.cast && !isCaster()) return false;
        /* a slam favourite is no use to a weapon that swings, and Sword & Steel is no
           use to one that does not */
        if (f.slam && !isSlammer()) return false;
        if (f.swing && isSlammer()) return false;
        /* and the two swinging hands are not interchangeable: Sword & Steel is the
           sword's opening and Reaver's Edge is the axe's */
        if (f.axe && !(weapon != null && weapon.id == "axe")) return false;
        if (f.noaxe && weapon != null && weapon.id == "axe") return false;
        return true;
    }
    public static void setFavourite(string id)
    {
        favourite = FAVOURITES.Find(f => f.id == id) ?? FAVOURITES[0];
        if (!favAllowed(favourite)) favourite = FAVOURITES[0];
        favSet = new HashSet<string>(favourite.ids);
        try { Store.setItem("bb_fav", favourite.id); } catch { }
        var row = document.getElementById("favRow");
        if (row != null) foreach (var el in row.children) el.classList.toggle("on", el.dataset["fav"] == favourite.id);
        buildComboPanel();
    }
    public static void setDifficulty(string id)
    {
        difficulty = DIFFS.GetValueOrDefault(id ?? "") ?? DIFFS["hard"];
        try { Store.setItem("bb_diff", id); } catch { }
        var row = document.getElementById("diffRow");
        if (row != null)
            foreach (var el in row.children)
            {
                bool on = el.dataset["diff"] == id;
                string col = DIFFS[el.dataset["diff"]].col;
                el.classList.toggle("on", on);
                el.style.borderColor = on ? col : "rgba(255,255,255,.18)";
                el.style.background = on ? col : "rgba(9,13,17,.34)";
                el.style.color = on ? "#12181d" : "#f0e6e6";
                el.style.boxShadow = on ? $"0 4px 0 {sh(col, -0.35)}" : "none";
            }
        var blurb = document.getElementById("diffBlurb");
        if (blurb != null) blurb.textContent = difficulty.blurb;
        /* a tree earned on EASY is not a tree earned on NIGHTMARE — switching the
           difficulty switches which save you are spending */
        refreshTreeBtn();
        if (treeOn) renderTree();
    }
    /* "a sword", but "an axe" */
    static readonly Regex VOWEL0 = new("^[aeiou]", RegexOptions.IgnoreCase);
    public static string anA(string word) => (VOWEL0.IsMatch(word) ? "an " : "a ") + word;
    /* What a weapon is worth, read against the sword. The number and its arrow are
       the SAME colour — green when the difference helps you, red when it costs you —
       because a bare "+59%" on swing time reads like good news and is not.
       The sword prints no delta column at all: it IS the baseline. */
    public static string weaponTip(Weapon w)
    {
        var b = BARE_HANDS;
        bool isBase = w.id == BARE_HANDS.id;
        string d(double v, double @base, bool lowerIsBetter = false)
        {
            if (isBase || Math.Abs(v - @base) < 1e-9) return "";
            double pct = Math.Round((v / @base - 1) * 100);
            bool better = lowerIsBetter ? pct < 0 : pct > 0;
            string cls = better ? "up" : "dn";
            string arrow = better ? "▲" : "▼";
            return $"<b class=\"{cls}\">{(pct > 0 ? "+" : "")}{S(pct)}% {arrow}</b>";
        }
        string pad(string t, int n) => (t + "                      ").Substring(0, n);
        string row(string label, string val, string delta) => $"<i>{pad(label, 19)}</i>{pad(val, 9)}{delta}";
        var Ls = new List<string>();
        Ls.Add($"<u>{w.name}</u>");
        Ls.Add(w.blurb);
        Ls.Add("");
        if (!isBase) Ls.Add("<i>every % is against BARE HANDS — a level-1</i>");
        if (!isBase) Ls.Add("<i>hero with nothing in them</i>");
        if (!isBase) Ls.Add("");
        Ls.Add(row("Base damage", Js.ToFixed(w.dmg, 2), d(w.dmg, b.dmg)));
        if (w.summon)
        {
            /* A SCEPTER IS ITS LEGION: what one skeleton hits for, how long a raising
               takes, how many stand -- and none of the hero's own hitting lines */
            Ls.Add(row("Raise time", Js.ToFixed(w.rate, 2) + "s", d(w.rate, b.rate, true)));
            Ls.Add(row("Legion", S(MINION_MAX_BASE) + " skeletons", ""));
            Ls.Add(row("Skeleton life", S(MINION_HP_BASE), ""));
            Ls.Add(row("Spell suppression", "+" + S(Math.Round(w.suppress * 100)) + "%",
              w.suppress > 0 ? "<b class=\"up\">▲</b>" : ""));
            Ls.Add("");
            Ls.Add("IT DOES NOT SWING. Holding attack RAISES THE DEAD");
            Ls.Add("where you point: skeletons that walk to whatever");
            Ls.Add("is near you and cut it down, and hold the crowd");
            Ls.Add("off you while they do: spearmen, swordsmen and");
            Ls.Add("archers. At a full legion the oldest crumbles and");
            Ls.Add("the new one takes its place.");
            Ls.Add("Minion damage is its own pool. The legendary makes");
            Ls.Add("every one of them a SKELETON MAGE.");
        }
        else if (w.shoot)
        {
            /* A BOW HAS NO REACH AND NO SPIN either — it has range, a steadier eye
               and quick feet, so those are its lines. */
            Ls.Add(row("Draw time", Js.ToFixed(w.rate, 2) + "s", d(w.rate, b.rate, true)));
            Ls.Add(row("Crit chance", S(Math.Round(w.crit * 100)) + "%", d(w.crit, b.crit)));
            Ls.Add(row("Crit multiplier", "×" + Js.ToFixed(w.critMult / 100, 2), d(w.critMult, b.critMult)));
            Ls.Add(row("Bleed chance", "×" + Js.ToFixed(w.bleed, 2), d(w.bleed, b.bleed)));
            Ls.Add(row("Accuracy", "×" + Js.ToFixed(Js.Or(w.acc, 1), 2), d(Js.Or(w.acc, 1), Js.Or(b.acc, 1))));
            Ls.Add("");
            Ls.Add(row("Evasion", "+" + S(Math.Round(w.evaBase)), w.evaBase > 0 ? "<b class=\"up\">▲</b>" : ""));
            Ls.Add(row("Spell suppression", "+" + S(Math.Round(w.suppress * 100)) + "%",
              w.suppress > 0 ? "<b class=\"up\">▲</b>" : ""));
            Ls.Add("");
            Ls.Add("IT DOES NOT SWING. Its basic attack is an ARROW:");
            Ls.Add("physical, it bleeds, it rolls to hit, and it flies");
            Ls.Add("until something stops it. Its bricks add arrows,");
            Ls.Add("pierce and barbs, and ONE legendary a run turns it");
            Ls.Add("into Lightning, Fire or Chaos Arrow. No lifesteal,");
            Ls.Add("no armour, and none of the swing’s own bricks.");
        }
        else if (w.cast)
        {
            /* A CASTER HAS NO REACH, NO BLEED AND NO SPIN, so printing them as -100%
               against a sword would be technically true and completely useless. It
               gets the lines that mean something to it instead. */
            Ls.Add(row("Cast time", Js.ToFixed(w.rate, 2) + "s", d(w.rate, b.rate, true)));
            Ls.Add(row("Burst radius", S(Js.Or(w.boltR, 46)), ""));
            Ls.Add(row("Crit chance", S(Math.Round(w.crit * 100)) + "%", d(w.crit, b.crit)));
            Ls.Add(row("Crit multiplier", "×" + Js.ToFixed(w.critMult / 100, 2), d(w.critMult, b.critMult)));
            Ls.Add("");
            Ls.Add(row("Spell damage", "+" + S(Math.Round(w.incSpell * 100)) + "%", "<b class=\"up\">increased ▲</b>"));
            Ls.Add(row("Elemental dmg", "+" + S(Math.Round(w.incElem * 100)) + "%", "<b class=\"up\">increased ▲</b>"));
            Ls.Add(row("Cast speed", "+" + S(Math.Round(w.incAtk * 100)) + "%", "<b class=\"up\">increased ▲</b>"));
        }
        else
        {
            Ls.Add(row("Swing time", Js.ToFixed(w.rate, 2) + "s", d(w.rate, b.rate, true)));
            Ls.Add(row("Reach", S(w.reach), d(w.reach, b.reach)));
            Ls.Add(row("Crit chance", S(Math.Round(w.crit * 100)) + "%", d(w.crit, b.crit)));
            Ls.Add(row("Crit multiplier", "×" + Js.ToFixed(w.critMult / 100, 2), d(w.critMult, b.critMult)));
            Ls.Add(row("Bleed chance", "×" + Js.ToFixed(w.bleed, 2), d(w.bleed, b.bleed)));
            Ls.Add(row("Spin swing", "×" + Js.ToFixed(w.spin, 2), d(w.spin, b.spin)));
            Ls.Add(row("Knockback", "×" + Js.ToFixed(w.kb, 2), d(w.kb, b.kb)));
            Ls.Add("");
            /* BARE HANDS HAVE NONE OF THESE, and "+infinity%" against zero is not a
               comparison — these three print what the weapon gives, full stop. */
            Ls.Add(row("Damage taken", "−" + S(Math.Round(w.lessTaken * 100)) + "%",
              w.lessTaken > 0 ? "<b class=\"up\">LESS ▲</b>" : ""));
            Ls.Add(row("Spell suppression", "+" + S(Math.Round(w.suppress * 100)) + "%",
              w.suppress > 0 ? "<b class=\"up\">▲</b>" : ""));
            Ls.Add(row("Lifesteal", S(Math.Round(w.leechChance * 100)) + "% for " +
              Js.ToFixed(w.leech * 100, 1) + "%",
              w.leech > 0 ? "<b class=\"up\">▲</b>" : ""));
            if (w.slam)
            {
                Ls.Add("");
                Ls.Add("IT DOES NOT SWING. Its basic attack is a GROUND");
                Ls.Add("SLAM: a fault opens in front of you and lines of");
                Ls.Add("stone teeth tear up out of it, " + S(Math.Round(SLAM_SHARE * 100)) + "% of a swing");
                Ls.Add("each, physical, bleeding. Up to " + S(SLAM_MAX_HITS) + " bite one body.");
                Ls.Add("It has its own bricks and its own legendary, and");
                Ls.Add("is offered none of the sword’s.");
            }
        }
        Ls.Add("");
        /* one attack a second, crits folded in — the honest single-number comparison.
           A caster's own increases are part of what carrying it is worth, so they
           count here; a sword's zero of them is why this is fair. */
        double dps(Weapon x)
        {
            double hit = x.dmg * (1 + x.incSpell + x.incElem) * (1 + x.crit * (x.critMult / 100 - 1));
            /* MELEE GETS ITS COMBO COUNTED. Every third swing is the spin, and leaving
               that out made a caster look twice the weapon it is. */
            double combo = (x.cast || x.shoot) ? 1 : x.summon ? MINION_MAX_BASE * x.rate / MINION_RATE : (2 + x.spin) / 3;
            return hit * combo / (x.rate / (1 + x.incAtk));
        }
        Ls.Add(row("Damage a second", Js.ToFixed(dps(w), 2), d(dps(w), dps(b))));
        Ls.Add("");
        if (w.cast)
        {
            Ls.Add(row("All resistances", "+" + S(Math.Round(w.res * 100)) + "%", "<b class=\"up\">▲</b>"));
            Ls.Add("");
            Ls.Add("It buys its safety with RANGE, so it gets none");
            Ls.Add("of the mitigation — or the lifesteal — that");
            Ls.Add("the melee weapons carry.");
            Ls.Add("It REPLACES the swing: no reach, no bleed,");
            Ls.Add("no spin combo. Physical bricks are not offered");
            Ls.Add("to you; the elemental ones are.");
            Ls.Add("A bolt has a 25% chance to set a fire, and a");
            Ls.Add("critical always does. Five stacks is the");
            Ls.Add("threshold: from there every fire can BRAND the");
            Ls.Add("body BURNT, and a deeper fire brands sooner.");
        }
        else Ls.Add("Every brick works with every weapon.");
        Ls.Add("Ability names never change: " + anA(w.name.ToLowerInvariant()));
        Ls.Add("still swings Bladestorm.");
        return string.Join("\n", Ls);
    }
    /* WHAT THIS COMBINATION ACTUALLY OPENS WITH. Rather than describe the weapon
       and the favourite separately and leave you to do the arithmetic, this builds
       a throwaway hero, gives it the favourite's starting kit with the chosen weapon
       in its hands, and reads the finished numbers straight off the same functions
       the game plays with. Nothing is estimated and nothing is duplicated. */
    public static ComboPreview comboPreview(string favId)
    {
        var keepP = player; var keepF = favourite; var keepW = weapon;
        var f = FAVOURITES.Find(x => x.id == favId) ?? FAVOURITES[0];
        /* stand a throwaway hero up from scratch, holding whatever `weapon` currently
           points at. Called again per weapon below, to ask a brick's own requirement
           whether the WEAPON is what is turning it away or merely the empty hand. */
        void build()
        {
            player = newPlayer();
            favourite = f;
            syncStats();
            var perk = FAV_PERKS.GetValueOrDefault(f.id);
            if (perk != null) try { perk.apply(player); } catch { }
            syncStats();
        }
        build();
        var p = player; bool cast = isCaster(), shoot = isArcher(), summon = isSummoner();
        double hit = cast ? boltDamage() : shoot ? arrowDamage() : summon ? minionDamage(p.minions.mages) : physDamage();
        var @out = new ComboPreview
        {
            hearts = p.maxHp,
            hitLabel = cast ? "Bolt damage" : shoot ? "Arrow hit" : summon ? "Minion hit" : "Weapon hit",
            hit = hit,
            rate = 1 / p.atkRate,
            rateLabel = cast ? "Casts a second" : shoot ? "Shots a second" : summon ? "Raisings a second" : "Swings a second",
            dps = cast ? hit / p.atkRate : swordDps(),
            crit = critChance() * 100,
            critMult = critMultPct(),
            armour = armourValue(),
            speed = p.speed,
            regen = p.regenRate,
            spells = SPELL_KEYS.Where(k => p.Spell(k) != null && p.Spell(k).n > 0).ToList(),
        };
        /* WHICH OF THE FAVOURITE'S BRICKS THIS HERO CAN ACTUALLY BE OFFERED. Asking
           each brick's own req() is the only honest test, but it has to be asked of
           the hero the favourite BUILDS — the menu's blank player owns no Brickbane,
           so every poison brick would answer no for a reason that has nothing to do
           with the weapon. When a brick still says no, the same question is put to
           the other weapons: a yes anywhere else means the weapon is the blocker and
           the brick is greyed out for good; a no everywhere means it is a brick you
           unlock later in the run, which is not a warning, it is just Tuesday. */
        @out.bricks = (f.ids ?? new()).Select(id =>
        {
            var u = UPGRADES.Find(x => x.id == id);
            if (u == null) return null;
            bool ask() { try { return u.req == null || u.req(); } catch { return false; } }
            bool open = ask();
            bool elsewhere = false;
            if (!open)
            {
                foreach (var k in WEAPON_KEYS)
                {
                    if (k == keepW.id) continue;
                    weapon = WEAPONS[k]; build();
                    if (ask()) { elsewhere = true; break; }
                }
                weapon = keepW; build();
            }
            string body = "";
            try { body = u.desc(u.calc(RARITY["rare"].mult), 0); } catch { body = ""; }
            return new ComboBrick { id = id, name = u.name, icon = u.icon, body = body, open = open, blocked = !open && elsewhere };
        }).Where(x => x != null).ToList();
        /* THE SIX GOLEMS ARE ONE LINE here: six long cards would bury the rest of the
           list, and the choice they share is the thing to say */
        {
            int gi = @out.bricks.FindIndex(b => b.id.StartsWith("golem"));
            if (gi >= 0)
            {
                var gs = @out.bricks.Where(b => b.id.StartsWith("golem")).ToList();
                var one = new ComboBrick
                {
                    id = "golems", name = "Golems — " + string.Join(", ", gs.Select(b => b.name.Replace(" Golem", ""))), icon = gs[0].icon,
                    body = $"Keep {GOLEM_BASE_CAP} (a third from the tree), never two alike. Each grants you something while it stands " +
                           "and fights with its own element.", open = gs.Any(b => b.open), blocked = gs.All(b => b.blocked),
                };
                @out.bricks = @out.bricks.Where(b => !b.id.StartsWith("golem")).ToList();
                @out.bricks.Insert(gi, one);
            }
        }
        player = keepP; favourite = keepF; weapon = keepW;
        if (player != null) syncStats();
        return @out;
    }
    /* TWO PANELS, NOT ONE. The numbers and the brick list are different questions
       — "what do I open with" and "what will I be offered" — and stacking them in
       one column made a panel taller than a laptop window, which is how it grew a
       scrollbar and started clipping. They sit either side of the red box now. */
    /* ONE tooltip element for the whole title screen, moved and refilled as the
       pointer lands on a row. Fixed-position, because both panels scroll and an
       absolutely positioned child of a scrolling box gets clipped by it. */
    public static Element statTipEl = null;
    public static Element statTip()
    {
        if (statTipEl == null)
        {
            statTipEl = document.createElement("div");
            statTipEl.id = "statTip";
            document.body.appendChild(statTipEl);
        }
        return statTipEl;
    }
    public static void statTipHide() { if (statTipEl != null) statTipEl.style.display = "none"; }
    /* THE SAME BOX, GIVEN HTML AND PLACED WHERE IT FITS. Preferred above the row,
       dropped below when there is no room above, clamped to the viewport either
       way, and allowed to scroll rather than run off the top of the window. */
    /* `side` puts the tip BESIDE the row instead of above it. The weapon card is
       taller than the gap above or below a button in the middle of the title card,
       so the above/below rule clamped it straight down on top of the button -- the
       card covered its own label and the pointer that summoned it. Beside it, it
       is the same card at the same size, and the pointer stays visible. */
    public static void floatTipShow(Element row, string html, string cls = null, bool side = false)
    {
        var el = statTip();
        el.className = cls ?? "";
        el.style.maxHeight = "none";
        el.innerHTML = html;
        el.style.display = "block";
        el.style.left = "-9999px"; el.style.top = "0px";
        var r = row.getBoundingClientRect();
        const double PADV = 10;
        double room = Math.Max(160, window.innerHeight - PADV * 2);
        if (el.offsetHeight > room) { el.style.maxHeight = S(room) + "px"; el.style.overflowY = "auto"; }
        else el.style.overflowY = "visible";
        double w = el.offsetWidth, h = el.offsetHeight;
        if (side)
        {
            const double GAP = 22;
            /* right of the row when it fits, left of it when it does not */
            double x0 = r.right + GAP;
            if (x0 + w > window.innerWidth - 8) x0 = r.left - w - GAP;
            x0 = Math.Max(8, x0);
            /* level with the row, then kept on screen */
            double y0 = r.top + r.height / 2 - h / 2;
            y0 = Math.Min(Math.Max(PADV, y0), Math.Max(PADV, window.innerHeight - h - PADV));
            el.style.left = S(Math.Round(x0)) + "px";
            el.style.top = S(Math.Round(y0)) + "px";
            return;
        }
        double x = Math.Round(r.left + r.width / 2 - w / 2);
        x = Math.Min(Math.Max(8, x), Math.Max(8, window.innerWidth - w - 8));
        double y = r.top - h - 12;
        if (y < PADV) y = r.bottom + 12;                    /* no room above: go below */
        if (y + h > window.innerHeight - PADV) y = window.innerHeight - h - PADV;
        el.style.left = S(x) + "px";
        el.style.top = S(Math.Round(Math.Max(PADV, y))) + "px";
    }
    public static void statTipShow(Element row, string label, string text)
    {
        var el = statTip();
        el.className = "";
        el.style.maxHeight = "none"; el.style.overflowY = "visible";
        el.innerHTML = $"<b>{esc(label)}</b>{esc(text)}";
        el.style.display = "block";
        var r = row.getBoundingClientRect();
        double w = el.offsetWidth, h = el.offsetHeight;
        /* to the right of the panel when there is room, to its left when there is not
           — the left-hand panel would otherwise push its tip off the screen edge */
        double x = r.right + 14;
        if (x + w > window.innerWidth - 8) x = r.left - w - 14;
        if (x < 8) x = 8;
        double y = r.top - 6;
        if (y + h > window.innerHeight - 8) y = window.innerHeight - h - 8;
        el.style.left = S(Math.Round(x)) + "px";
        el.style.top = S(Math.Round(Math.Max(8, y))) + "px";
    }
    /* wire every explained row in a panel that has just been rebuilt */
    public static void wireStatTips(Element root)
    {
        if (root == null) return;
        /* an ability's tip is a whole damage panel, so it gets the pre-formatted box
           rather than the one-paragraph one the stat rows use */
        foreach (var row in root.querySelectorAll(".cpabil.cptip"))
        {
            string why = row.getAttribute("data-why") ?? "";
            string label = row.querySelector("span")?.textContent ?? "";
            if (why.Length == 0) continue;
            row.addEventListener("mouseenter", _ => floatTipShow(row, $"<u>{esc(label)}</u>\n{esc(why)}", "wtipbox"));
            row.addEventListener("mouseleave", _ => statTipHide());
        }
        foreach (var row in root.querySelectorAll(".cprow.cptip"))
        {
            string why = row.getAttribute("data-why") ?? "";
            string label = row.querySelector("span")?.textContent ?? "";
            if (why.Length == 0) continue;
            row.addEventListener("mouseenter", _ => statTipShow(row, label, why));
            row.addEventListener("mouseleave", _ => statTipHide());
        }
    }
    /* WHERE EVERY NUMBER COMES FROM. The panel used to show nine opening stats and
       a percentage against "no favourite", which answered one question out of
       three: a hero is a BARE HAND plus a weapon plus a favourite plus a skill
       tree, and until now the tree was invisible on this screen entirely.

       Four heroes are stood up from scratch and every probe on the character sheet
       is read off each of them. The difference between one layer and the next IS
       that layer's contribution — nothing is estimated, nothing is duplicated, and
       a stat nobody has thought about yet is on this panel the moment somebody adds
       a probe for it. */
    public static readonly List<CpLayer> CP_LAYERS = new()
    {
        new CpLayer { key = "bare", name = "BARE HANDS" },
        new CpLayer { key = "weap", name = "weapon", tag = "W", col = "#cfd6dd" },
        new CpLayer { key = "fav", name = "favourite", tag = "F", col = "#ffd76e" },
        new CpLayer { key = "tree", name = "skill tree", tag = "T", col = "#9b45d6" },
    };
    public static ComboLayersOut comboLayers()
    {
        var keepP = player; var keepF = favourite; var keepW = weapon;
        Dictionary<string, double> snap()
        {
            var o = new Dictionary<string, double>();
            foreach (var q in STAT_PROBES)
            {
                double v = 0;
                try { v = q.get(); } catch { v = 0; }
                o[q.g + "|" + q.k] = double.IsFinite(v) ? v : 0;
            }
            return o;
        }
        /* NO FAVOURITE IS A FAVOURITE. It hands out +5% increased damage, a heart,
           ten armour and the rest, so a "weapon only" layer that quietly applied it
           made every real favourite look like it COST you five per cent — which it
           does, and which is exactly the thing the F column should be saying out
           loud rather than hiding inside the W one. The perk is its own step. */
        Ent stand(Weapon w, Favourite f2, bool perk, bool tree)
        {
            weapon = w; favourite = f2;
            player = newPlayer();
            syncStats();
            var pk = perk && f2 != null ? FAV_PERKS.GetValueOrDefault(f2.id) : null;
            if (pk != null) try { pk.apply(player); } catch { }
            if (tree) try { applyMetaTree(player); } catch { }
            syncStats();
            return player;
        }
        var none = FAVOURITES[0]; var f = keepF ?? none;
        var @out = new ComboLayersOut();
        stand(BARE_HANDS, none, false, false); @out.bare = snap();
        stand(keepW, none, false, false); @out.weap = snap();
        stand(keepW, f, true, false); @out.fav = snap();
        var hero = stand(keepW, f, true, true);
        @out.tree = snap();
        /* the ABILITIES list is read off the hero you would actually start with */
        @out.abilities = new List<Ability>();
        string wname = isCaster() ? "Fireball" : isSlammer() ? "Ground Slam" : isArcher() ? "Arrow" : isSummoner() ? "Raise Skeleton" : "Weapon swing";
        string wicon = isCaster() ? "flame" : isSlammer() ? "fist" : isArcher() ? "bow" : isSummoner() ? "skull" : "sword";
        @out.abilities.Add(new Ability
        {
            id = "weapon", icon = wicon, name = wname,
            head = fmtHp(isCaster() ? boltDamage() : isSlammer() ? slamSpikeDamage() : isArcher() ? arrowDamage()
              : isSummoner() ? minionDamage(hero.minions.mages) : physDamage()),
            tip = $"{weapon.name}, every {Js.ToFixed(hero.atkRate, 2)}s\n" +
                $"{fmtHp(swordDps())} a second sustained, crits folded in\n" +
                (isCaster() ? $"{S(Math.Round(boltRadius()))} burst radius, " +
                    $"{S(Math.Round(ailChanceOf("fire", "fire", false) * 100))}% to set a fire (a crit always does)"
                 : isSlammer() ? $"A fault of {SLAM_MAX_HITS} teeth, {S(Math.Round(SLAM_SHARE * 100))}% of a swing each"
                 : isSummoner() ? $"a legion of {S(minionMax())} skeletons, {S(Math.Round(minionMaxHp()))} life each; " +
                    "a legendary makes them Skeleton Mages"
                 : isArcher() ? $"{S(clamp(Js.I32(hero.bow.n), 1, ARROW_MAX_N))} arrow a shot, pierces {S(hero.bow.pierce)}; " +
                    "a legendary makes it Lightning, Fire or Chaos Arrow"
                 : $"{S(Math.Round(hero.reach))} reach, every third swing is the spin combo"),
        });
        try { foreach (var sp in spellSummaries()) @out.abilities.Add(sp); } catch { }
        @out.favBricks = null;
        player = keepP; favourite = keepF; weapon = keepW;
        if (player != null) syncStats();
        return @out;
    }
    /* every probe, in the order the character sheet lists them, with the layer that
       moved it named beside it */
    public static string comboStatRows(ComboLayersOut L, bool all)
    {
        var parts = new List<string>();
        string group = null;
        /* THE SHORT LIST: only the basic probes, under the simple headings, and only
           the ones this hero actually HAS -- a sword is not shown its bolt damage as
           a dash, it is simply not shown it. */
        var @short = new Dictionary<string, List<string>>();
        foreach (var q in STAT_PROBES)
        {
            if (!all && !Js.T(q.basic)) continue;
            string k = q.g + "|" + q.k;
            var v = new Dictionary<string, double> { ["bare"] = L.bare[k], ["weap"] = L.weap[k], ["fav"] = L.fav[k], ["tree"] = L.tree[k] };
            if (!all && !(v["tree"] > 0 || v["tree"] < 0)) continue;
            var chips = new List<string>();
            for (int i = 1; i < CP_LAYERS.Count; i++)
            {
                var cur = CP_LAYERS[i]; var prev = CP_LAYERS[i - 1];
                double d = v[cur.key] - v[prev.key];
                if (Math.Abs(d) < 1e-9) continue;
                chips.Add($"<i class=\"cpchip {(d > 0 ? "up" : "dn")}\" title=\"{esc(cur.name)}\">" +
                  $"{cur.tag}{(d > 0 ? "+" : "−")}{cpNum(Math.Abs(d), q)}</i>");
            }
            bool dead = v["bare"] == 0 && v["weap"] == 0 && v["fav"] == 0 && v["tree"] == 0;
            if (dead && !q.zero) continue;                 /* a probe that is off for everyone */
            string why = statWhy(q.g, q.k) ?? "";
            var lv = signLabel(q.k, cpFmt(v["tree"], q)); string lab = lv[0], val = lv[1];
            string html = $"<div class=\"cprow cpstat{(dead ? " cpdim" : "")}{(why.Length > 0 ? " cptip" : "")}\"" +
              $"{(why.Length > 0 ? $" data-why=\"{esc(why)}\"" : "")}>" +
              /* the W/F/T chips need the legend the long list prints above them; the
                 short list has no legend, so it has no chips -- a number, not a sum */
              $"<span>{esc(lab)}</span><b>{esc(val)}{(all ? string.Join("", chips) : "")}</b></div>";
            if (!all) { if (!@short.TryGetValue(q.basic, out var sl)) @short[q.basic] = sl = new(); sl.Add(html); continue; }
            if (q.g != group)
            {
                group = q.g;
                parts.Add($"<div class=\"cphead cpgrp\">{esc(group)}</div>");
            }
            parts.Add(html);
        }
        if (!all) foreach (var h in STAT_BASIC_ORDER) if (@short.TryGetValue(h, out var sl))
                    parts.Add($"<div class=\"cphead cpgrp\">{h}</div>" + string.Join("", sl));
        return string.Join("", parts);
    }
    public static string cpFmt(double v, StatProbe q)
    {
        try { return q.fmt != null ? q.fmt(v) : S(Math.Round(v * 100) / 100); }
        catch { return S(v); }
    }
    /* a DELTA formatted like the value it moved, but never with the unit twice */
    static readonly Regex CPNUM = new(@"^[^0-9-]*(-?[\d.,]+)\s*(.*)$");
    public static string cpNum(double v, StatProbe q)
    {
        string t = cpFmt(v, q);
        var m = CPNUM.Match(t);
        return m.Success ? (m.Groups[1].Value + (m.Groups[2].Value.Length > 0 && m.Groups[2].Value.Length <= 2 ? m.Groups[2].Value : "")) : t;
    }
    /* SHORT BY DEFAULT. The menu is the first thing a new player reads, and it was
       handing them every number in the game -- burning rows for a sword, ceilings
       nobody reaches for an hour. It opens on the short list now, and the button
       at the bottom of it is the whole panel, exactly as it was. Remembered, so a
       player who wants the lot only asks once. */
    public static bool menuStatsAll = false;
    public static void MenuStatsLoad() { try { menuStatsAll = Store.getItem("bb_menustats") == "1"; } catch { } }
    public static void setMenuStatsAll(bool on)
    {
        menuStatsAll = on;
        try { Store.setItem("bb_menustats", menuStatsAll ? "1" : "0"); } catch { }
        buildComboPanel();
    }
    public static void buildComboPanel()
    {
        var stats = document.getElementById("comboStats");
        var el = document.getElementById("comboBricks");
        if (stats == null || el == null) return;
        var f = favourite; bool cast = isCaster();
        var @base = comboPreview("none"); var now = comboPreview(f.id);
        string num(double v, double d = double.NaN) => d == 0 ? S(Math.Round(v)) : Js.ToFixed(v, double.IsNaN(d) ? 2 : (int)d);
        /* every row carries its difference from running this weapon with no
           favourite at all, so the favourite's contribution is never guesswork */
        /* the SAME sentence the reward comparison and the character sheet show, so a
           stat is explained once and read in three places. `why` names the probe key
           when the panel's own label differs from the probe's. */
        string row(string label, double v, double b, double d, string unit, string[] why)
        {
            string tag = "";
            if (Math.Abs(v - b) > 1e-6)
            {
                double pct = Math.Round((v / (b != 0 ? b : 1) - 1) * 100);
                tag = $"<i class=\"{(v > b ? "up" : "dn")}\">{(pct > 0 ? "+" : "")}{S(pct)}%</i>";
            }
            string tip = why != null ? statWhy(why[0], why[1]) : "";
            return $"<div class=\"cprow{(Js.T(tip) ? " cptip" : "")}\"{(Js.T(tip) ? $" data-why=\"{esc(tip)}\"" : "")}>" +
              $"<span>{label}</span><b>{num(v, d)}{unit ?? ""}{tag}</b></div>";
        }
        /* a tiny helper for the rows that are the panel's own, not a probe's */
        string orow(string label, string value, string tip) =>
          $"<div class=\"cprow{(Js.T(tip) ? " cptip" : "")}\"{(Js.T(tip) ? $" data-why=\"{esc(tip)}\"" : "")}>" +
          $"<span>{label}</span><b>{value}</b></div>";
        var perk = FAV_PERKS.GetValueOrDefault(f.id);
        string bricks = string.Join("", now.bricks.Select(b =>
          $@"<div class=""cpbrick{(b.blocked ? " off" : "")}"">
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""{(b.blocked ? "#7c8a80" : "#ffd76e")}"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(b.icon ?? "") ?? ""}</svg>
      <div><b>{esc(b.name)}{(b.blocked ? " — never offered to " + anA(weapon.name.ToLowerInvariant()) : "")}</b>" +
         $@"<em>{esc(b.body)}</em></div>
    </div>"));
        var Ly = comboLayers();
        string abil = string.Join("", Ly.abilities.Select(a =>
          $@"<div class=""cpabil cptip"" data-why=""{esc(a.tip ?? "")}"">
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""#ffd76e"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(a.icon ?? "") ?? ""}</svg>
      <span>{esc(a.name)}</span><b>{esc(a.head)}</b></div>"));
        stats.innerHTML =
          $@"<h4>{weapon.name} × {esc(f.name).ToUpperInvariant()}</h4>
     <div class=""cpsub"">{esc(weapon.blurb)}</div>" +
          (perk != null ? $"<div class=\"cphead\">YOU START WITH</div><ul>{string.Join("", favLines(perk).Select(l => $"<li>{esc(l)}</li>"))}</ul>" : "") +
          @"<div class=""cphead"">ABILITIES</div>
     <div class=""cpvs"">what you open with. Hover one for its damage</div>" +
          abil +
          (menuStatsAll ?
          @"<div class=""cphead"">EVERY STAT</div>
     <div class=""cpvs"">the finished number, and which layer moved it:
       <i class=""cpchip cpleg up"">W</i> weapon
       <i class=""cpchip cpleg up"">F</i> favourite
       <i class=""cpchip cpleg up"">T</i> skill tree.
       Everything is measured from BARE HANDS — a level-1 hero holding nothing.
       Hover a row for what it means.</div>"
          : @"<div class=""cphead"">YOUR STATS</div>
       <div class=""cpvs"">what you start the run with. Hover a row for what it means.</div>") +
          comboStatRows(Ly, menuStatsAll) +
          $"<button class=\"cpmore\" id=\"cpMore\">{(menuStatsAll ? "SHOW FEWER STATS" : "SHOW EVERY STAT & THE CEILINGS")}</button>" +
          (!menuStatsAll ? "" :
          @"<div class=""cphead"">CEILINGS</div>
     <div class=""cpvs"">nothing in a run climbs past these, however long it runs</div>" +
          orow("Increased damage", S(Math.Round(INC_DMG_CAP * 100)) + "%",
            "every increase that applies to one hit, summed — levels, physical, spell, elemental — stops here") +
          orow("MORE multiplier", "×" + Js.ToFixed(MORE_DMG_CAP, 2),
            "every PERMANENT more multiplier together. A timed rage buff multiplies on top of this, on purpose") +
          orow("Crit chance", S(Math.Round(CRIT_CHANCE_CAP * 100)) + "%", statWhy("PHYSICAL", "Crit chance")) +
          orow("Crit multiplier", S(CRIT_MULT_CAP) + "%", statWhy("SWORD", "Crit damage")) +
          string.Join("", SPELL_FLOOR.Keys.Select(k =>
            orow(esc(SPELL_NAME.GetValueOrDefault(k) ?? k) + " floor", Js.ToFixed(SPELL_FLOOR[k], 2) + "s",
              $"however much CAST SPEED you buy, {SPELL_NAME.GetValueOrDefault(k) ?? k} never comes round faster than this"))) +
          orow("Guardian ring", Js.ToFixed(ORBIT_MAX_SPD, 1) + " rad/s",
            "past this the guardian bricks strobe instead of spinning"));
        {
            var more = document.getElementById("cpMore");
            if (more != null) more.addEventListener("click", _ => { SFX.click(); setMenuStatsAll(!menuStatsAll); });
        }

        el.innerHTML =
          (bricks.Length > 0 ? $@"<div class=""cphead"">BRICKS THAT COME UP MORE OFTEN</div>{bricks}
       <div class=""cpnote"">Shown at RARE, read off this exact hero. A greyed brick is one this
       weapon can never roll &mdash; either it is the wrong class of brick, or the weapon already
       sits on that stat's cap. Pick another weapon or another favourite.</div>"
           : @"<div class=""cphead"">BRICKS THAT COME UP MORE OFTEN</div>
        <div class=""cpnote"">No favourite weights nothing: every brick in the bench is
        equally likely, and the kit opposite is what you are paid for spreading yourself
        thin. Pick a favourite to see what committing to one costs, and buys.</div>") +
          "";
        /* both panels were just rebuilt from scratch, so every listener went with
           them — rewire before the pointer has a chance to land on anything */
        statTipHide();
        wireStatTips(stats);
        wireStatTips(el);
    }
    public static void buildWeaponRow()
    {
        var row = document.getElementById("weaponRow");
        if (row == null) return;
        row.innerHTML = string.Join("", WEAPON_KEYS.Select(id =>
          $"<div class=\"diff\" data-weapon=\"{id}\">{WEAPONS[id].name}</div>"));
        foreach (var el in row.children)
        {
            el.addEventListener("click", _ => { SFX.click(); setWeapon(el.dataset["weapon"]); });
            el.addEventListener("mouseenter", _ =>
              floatTipShow(el, weaponTip(WEAPONS[el.dataset["weapon"]]), "wtipbox", true));
            el.addEventListener("mouseleave", _ => statTipHide());
        }
        string saved = "sword";
        try { saved = Store.getItem("bb_weapon") ?? "sword"; } catch { }
        setWeapon(WEAPONS.ContainsKey(saved) ? saved : "sword");
    }
}

/* one row of the character sheet (STAT_PROBES, JS 18980): its group, its label,
   which short-list heading it sits under, how it is read and printed, and why */
public sealed class StatProbe
{
    public string g, k, basic, why; public Func<double> get; public Func<double, string> fmt; public bool zero;
}
