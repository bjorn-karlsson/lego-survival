// lego-survival.html, JS 20464-20643: THE EQUIPMENT SCREEN, its first half —
// the paper doll, the item pictures, the words on an item, and opening it.
namespace Brickblade;

public sealed class DollSlot { public double x, y, w, h; }
public sealed class GearPathPart { public string k; public Path2D p; }

public static partial class G
{
    /* =============================== THE EQUIPMENT SCREEN ===============================
       PoE's inventory panel: the hero's slots above, the bag below. Click an item in
       the bag to put it on (whatever it replaces goes where it was), click a worn one
       to take it off, drag either anywhere it fits, drop one in the bin -- or
       Ctrl-click it twice -- to be rid of it. The run waits while this is open. */
    public static bool gearOn = false; public static string gearFrom = "play";
    /* where each slot sits on the paper doll, in bag cells */
    public static readonly Dictionary<string, DollSlot> GEAR_DOLL = new()
    {
        ["weapon"] = new DollSlot { x = 0, y = 1, w = 2, h = 4 },
        ["helmet"] = new DollSlot { x = 4, y = 0, w = 2, h = 2 },
        ["offhand"] = new DollSlot { x = 8, y = 1, w = 2, h = 4 },
        ["body"] = new DollSlot { x = 4, y = 2.2, w = 2, h = 3 },
        ["amulet"] = new DollSlot { x = 6.3, y = 1.1, w = 1, h = 1 },
        ["ring1"] = new DollSlot { x = 2.7, y = 3.2, w = 1, h = 1 },
        ["ring2"] = new DollSlot { x = 6.3, y = 3.2, w = 1, h = 1 },
        ["gloves"] = new DollSlot { x = 1.4, y = 5.5, w = 2, h = 2 },
        ["belt"] = new DollSlot { x = 4, y = 5.5, w = 2, h = 1 },
        ["boots"] = new DollSlot { x = 6.6, y = 5.5, w = 2, h = 2 }
    };
    /* ---- THE PICTURES ----
       One drawing per class, in a box the shape of its footprint (12 units a cell),
       as SVG paths -- so the SAME drawing is an <svg> in the bag and a Path2D on the
       floor. Each part names its paint: m the base's colour, d darker, l lighter,
       s steel, w wood, g gold. */
    public static readonly Dictionary<string, (string k, string d)[]> GEAR_ICON = new()
    {
        ["helmet"] = new[] { ("m", "M3 16 C3 7 7 3 12 3 C17 3 21 7 21 16 L22 16 L22 19 L2 19 L2 16 Z"), ("d", "M7 11 L17 11 L17 13.5 L7 13.5 Z") },
        ["body"] = new[] { ("m", "M7 4 L10 6 L14 6 L17 4 L22 9 L19 13 L19 32 L5 32 L5 13 L2 9 Z"), ("d", "M11 8 L13 8 L13 30 L11 30 Z"),
                 ("l", "M7 15 L10 15 L10 18 L7 18 Z M14 15 L17 15 L17 18 L14 18 Z") },
        ["gloves"] = new[] { ("m", "M6 22 L6 12 L5 5 L7.5 5 L8.5 11 L8.5 3 L11 3 L11 11 L12 2.5 L14.5 2.5 L14 11 L15.5 4.5 L18 4.5 L17 13 L19 11 L21 12.5 L17 18 L17 22 Z"),
                 ("d", "M6 19 L17 19 L17 22 L6 22 Z") },
        ["boots"] = new[] { ("m", "M6 2 L14 2 L14 14 L21 16.5 L22 21 L5 21 Z"), ("d", "M5 19 L22 19 L22 21.5 L5 21.5 Z"), ("l", "M6 2 L14 2 L14 4.5 L6 4.5 Z") },
        ["shield"] = new[] { ("m", "M12 2 L22 6 L22 16 C22 24 17 30 12 34 C7 30 2 24 2 16 L2 6 Z"), ("d", "M12 5 L19 8 L19 16 C19 22 16 27 12 30 Z"),
                 ("l", "M8 14 A4 4 0 1 0 16 14 A4 4 0 1 0 8 14 Z") },
        ["quiver"] = new[] { ("w", "M7.5 1 L9 1 L9.5 10 L8 10 Z M11.3 0 L12.7 0 L12.7 10 L11.3 10 Z M15 1 L16.5 1 L16 10 L14.5 10 Z"),
                 ("m", "M6 10 L18 10 L16 34 L8 34 Z"), ("d", "M6.4 15 L17.6 15 L17.4 17.5 L6.6 17.5 Z") },
        ["belt"] = new[] { ("m", "M1 4 L23 4 L23 9 L1 9 Z"), ("g", "M9 2.5 L15 2.5 L15 10.5 L9 10.5 Z"), ("d", "M10.6 4 L13.4 4 L13.4 9 L10.6 9 Z") },
        ["amulet"] = new[] { ("g", "M2.5 1 C2.5 5 4.5 7 6 7.4 C7.5 7 9.5 5 9.5 1 L8.6 1 C8.6 4.4 7.2 6.2 6 6.5 C4.8 6.2 3.4 4.4 3.4 1 Z"),
                 ("m", "M6 6.2 L9.2 9 L6 11.8 L2.8 9 Z") },
        ["ring"] = new[] { ("g", "M1.5 7.5 A4.5 4.5 0 1 0 10.5 7.5 A4.5 4.5 0 1 0 1.5 7.5 Z M3.3 7.5 A2.7 2.7 0 1 1 8.7 7.5 A2.7 2.7 0 1 1 3.3 7.5 Z"),
                 ("m", "M4.3 1 L7.7 1 L8.8 2.7 L6 4.6 L3.2 2.7 Z") },
        ["sword"] = new[] { ("s", "M6 1 L8.5 5 L8.5 24 L3.5 24 L3.5 5 Z"), ("m", "M1 24 L11 24 L11 26.5 L1 26.5 Z"),
                 ("w", "M4.8 26.5 L7.2 26.5 L7.2 32 L4.8 32 Z"), ("m", "M4 33 A2 2 0 1 0 8 33 A2 2 0 1 0 4 33 Z") },
        ["axe"] = new[] { ("w", "M11 6 L13.5 6 L13.5 35 L11 35 Z"), ("s", "M13 4 C18 2 23 5 23 12 C23 16 20 19 17 19 L13 15 Z"),
                 ("m", "M11 7 L6 5 L6 12 L11 11 Z") },
        ["mace"] = new[] { ("w", "M10.8 14 L13.2 14 L13.2 35 L10.8 35 Z"), ("m", "M12 2 L18 5 L19 12 L12 16 L5 12 L6 5 Z"),
                 ("s", "M12 0 L13.2 3 L10.8 3 Z M20.5 7 L22.5 8.8 L19.5 9.8 Z M3.5 7 L1.5 8.8 L4.5 9.8 Z") },
        ["scepter"] = new[] { ("g", "M5 11 L7 11 L7 35 L5 35 Z"), ("m", "M6 1 L10.5 6 L6 12 L1.5 6 Z"), ("l", "M6 3 L8 6 L6 8.5 L4 6 Z") },
        ["staff"] = new[] { ("w", "M10.8 8 L13.2 8 L13.2 47 L10.8 47 Z M7 3 L10.8 9 L10 10.5 L5.5 4 Z M17 3 L13.2 9 L14 10.5 L18.5 4 Z"),
                 ("m", "M7 6 A5 5 0 1 0 17 6 A5 5 0 1 0 7 6 Z"), ("l", "M10 5 A2 2 0 1 0 14 5 A2 2 0 1 0 10 5 Z") },
        ["bow"] = new[] { ("w", "M5 2 C20 10 20 38 5 46 L7.5 46 C21.5 37 21.5 11 7.5 2 Z"), ("l", "M5.6 3 L6.4 3 L6.4 45 L5.6 45 Z"),
                 ("m", "M16.2 21 L19.8 21 L19.8 27 L16.2 27 Z") }
    };
    public static Col gearPaint(string k, Col col) =>
        k == "m" ? col : k == "d" ? sh(col, -0.35) : k == "l" ? sh(col, 0.35)
      : k == "s" ? (Col)"#c9d1d8" : k == "w" ? (Col)"#8a5a2e" : (Col)"#d9b45a";
    public static string gearIconSvg(GearItem it)
    {
        var B = GEAR_BASE_BY_ID[it.b]; var C = GEAR_CLS[B.cls]; Col col = B.col ?? "#a9b3bc";
        var parts = GEAR_ICON.GetValueOrDefault(B.cls ?? "") ?? GEAR_ICON["ring"];
        return $"<svg viewBox=\"0 0 {S(C.w * 12)} {S(C.h * 12)}\" preserveAspectRatio=\"xMidYMid meet\">" +
          string.Join("", parts.Select(pt => $"<path fill-rule=\"evenodd\" fill=\"{S(gearPaint(pt.k, col))}\" stroke=\"rgba(10,14,18,.55)\" stroke-width=\".5\" d=\"{pt.d}\"/>")) +
          "</svg>";
    }
    public static readonly Dictionary<string, List<GearPathPart>> gearPathCache = new();
    public static List<GearPathPart> gearPaths(string cls)
    {
        if (!gearPathCache.TryGetValue(cls ?? "", out var l))
            gearPathCache[cls ?? ""] = l = (GEAR_ICON.GetValueOrDefault(cls ?? "") ?? GEAR_ICON["ring"]).Select(pt => new GearPathPart { k = pt.k, p = new Path2D(pt.d) }).ToList();
        return l;
    }
    /* ---- THE WORDS ---- */
    public static string gearKindWord(string cls) { var k = gearDmgKind(cls); return k == "elem" ? "elemental" : k == "minion" ? "minion" : "physical"; }
    public static string gearModText(string stat, double v, string cls = null)
    {
        string pct(double x) => Math.round(Math.abs(x) * 100) + "%";
        string sg(double x) => x < 0 ? "−" : "+";
        switch (stat)
        {
            case "locArmour": return sg(v) + S(Math.abs(v)) + " to Armour";
            case "locEva": return sg(v) + S(Math.abs(v)) + " to Evasion";
            case "locEs": return sg(v) + S(Math.abs(v)) + " to Energy Shield";
            case "locArmourInc": return pct(v) + " increased Armour";
            case "locEvaInc": return pct(v) + " increased Evasion";
            case "locEsInc": return pct(v) + " increased Energy Shield";
            case "locDmg": return "Adds " + fmtHp(v) + " " + gearKindWord(cls) + " damage";
            case "locDmgInc": return pct(v) + " increased " + gearKindWord(cls) + " damage";
        }
        var X = GEAR_XSTATS.GetValueOrDefault(stat ?? "");
        if (X != null) return sg(v) + fmtHp(Math.abs(v)) + " " + X.n;
        if (stat == "armour") return sg(v) + S(Math.abs(Math.round(v))) + " armour";
        return metaModText(new Mod(stat, v));
    }
    public static List<(string k, string v, bool mod)> gearDefLines(GearItem it)
    {
        var B = GEAR_BASE_BY_ID[it.b]; var L = new List<(string k, string v, bool mod)>();
        var got = new NumMap(); foreach (var (k, v) in gearMods(it)) got[k] = got[k] + v;
        var loc = it.m.Any(m => (GEAR_AFFIX_BY_ID.GetValueOrDefault(m.id ?? "")?.stat ?? "").StartsWith("loc"));
        string num(string k) => got.Has(k) ? S(got[k]) : "undefined";
        if (B.def != null)
        {
            if (B.def["armour"] != 0) L.Add(("Armour", num("armour"), loc && got["armour"] != B.def["armour"]));
            if (B.def["eva"] != 0) L.Add(("Evasion", num("eva"), loc && got["eva"] != B.def["eva"]));
            if (B.def["es"] != 0) L.Add(("Energy Shield", num("es"), loc && got["es"] != B.def["es"]));
        }
        if (B.dmg != 0 && !double.IsNaN(B.dmg))
        {
            var k = gearDmgKind(B.cls); var key = k == "elem" ? "flatElem" : k == "minion" ? "minionFlat" : "flatPhys";
            L.Add((k == "minion" ? "Adds to every minion hit" : "Adds " + gearKindWord(B.cls) + " damage", "+" + fmtHp(got[key]), loc && Math.abs(got[key] - B.dmg) > 1e-9));
        }
        return L;
    }
    public static string gearTipHtml(GearItem it, string head, string slot = null)
    {
        var B = GEAR_BASE_BY_ID[it.b]; var R = GEAR_RAR[it.r]; var C = GEAR_CLS[B.cls];
        var nm = gearName(it);
        var h = $"<div class=\"gtip {it.r}\">";
        if (!string.IsNullOrEmpty(head)) h += $"<div class=\"gtcap\">{head}</div>";
        h += $"<div class=\"gth\" style=\"color:{R.col}\">{nm}{(it.r == "rare" ? "<br>" + B.name : "")}</div><div class=\"gtb\">";
        h += $"<div class=\"gtcls\">{C.name}{(!string.IsNullOrEmpty(C.weap) ? " · " + (C.weap == "staff" || C.weap == "bow" ? "two hands" : "one hand") : "")}</div>";
        foreach (var (k, v, mod) in gearDefLines(it)) h += $"<div class=\"gtdef\">{k}: <b class=\"{(mod ? "gmod" : "")}\">{v}</b></div>";
        h += $"<div class=\"gtlvl\">Item Level: <b>{S(it.il)}</b></div>";
        if (it.imp.Count > 0) { h += "<hr>"; foreach (var (k, v) in it.imp) h += $"<div class=\"gtimp\">{gearModText(k, v, B.cls)}</div>"; }
        if (it.m.Count > 0)
        {
            h += "<hr>";
            /* a stable sort, as Array.prototype.sort is: prefixes first, each in its rolled order */
            var ord = it.m.OrderBy(a => GEAR_AFFIX_BY_ID[a.id].kind == "pre" ? 0 : 1).ToList();
            foreach (var (id, ti, v) in ord)
            {
                var a = GEAR_AFFIX_BY_ID[id]; var T = gearTierNo(a, ti);
                h += $"<div class=\"gtmod\">{gearModText(a.stat, v, B.cls)}<i class=\"{(T <= 2 ? "top" : "")}\">{(a.kind == "pre" ? "P" : "S")} · T{S(T)}</i></div>";
            }
        }
        var why = gearActiveWhy(it, slot ?? gearSlotOf(it));
        if (!string.IsNullOrEmpty(why)) h += $"<div class=\"gtwarn\">INACTIVE — {why}</div>";
        return h + "</div></div>";
    }
    /* WHAT CHANGES if this goes on: every stat the two items touch, the new total
       against the old, green up and red down */
    public static NumMap gearSumMods(GearItem it, string slot)
    {
        var o = new NumMap();
        if (it != null && gearActive(it, slot)) foreach (var (k, v) in gearMods(it)) o[k] = o[k] + v;
        return o;
    }
    public static string gearDeltaHtml(GearItem it, GearItem old, string slot)
    {
        var a = gearSumMods(it, slot); var b = gearSumMods(old, slot);
        var keys = new List<string>(); foreach (var k in a.Keys.Concat(b.Keys)) if (!keys.Contains(k)) keys.Add(k);
        var rows = new List<string>();
        foreach (var k in keys)
        {
            var d = gearRound(k, a[k] - b[k]);
            if (Math.abs(d) < 1e-9) continue;
            rows.Add($"<div class=\"{(d > 0 ? "gup" : "gdown")}\">{(d > 0 ? "▲" : "▼")} {gearModText(k, d)}</div>");
        }
        if (rows.Count == 0) return "";
        return $"<div class=\"gtip gdelta\"><div class=\"gtcap\">IF YOU EQUIP IT</div><div class=\"gtb\">{string.Join("", rows)}</div></div>";
    }
    /* ---- OPEN AND CLOSE ---- */
    /* ONE CELL SIZE for the whole screen, chosen to fit it: the stash, the paper
       doll and bag, and the summary side by side, and the tallest of them. */
    public static void gearFit()
    {
        var el = document.getElementById("gearScreen");
        if (el == null) return;
        var vw = Js.Or(window.innerWidth, 1300); var vh = Js.Or(window.innerHeight, 850);
        var gc = Math.floor(clamp(Math.min((vw - 400) / 24.5, (vh - 230) / 13.6), 22, 40));
        el.style.setProperty("--gc", S(gc) + "px");
    }
    /* THE STASH IS IN TOWN. Between runs (the title) and in the arcade's old way it
       is always there; in the RPG, out in a zone, I opens the bag and what you wear
       and nothing else -- the stash waits at home. */
    public static bool gearStashHere() => gearFrom == "title" || !rpgOn() || RPG.zone == "town";
    public static void toggleGear()
    {
        var el = document.getElementById("gearScreen");
        if (el == null) return;
        if (gearOn)
        {
            gearOn = false;
            el.classList.add("hidden");
            gearTipHide();
            state = gearFrom;
            if (state == "play") { cvEl.style.cursor = "none"; try { window.focus(); } catch { } }
            if (state == "title") refreshGearBtn();
            if (gearFromRpg) { gearFromRpg = false; if (state == "title") toggleRpgScreen(true); }
            try { SFX.click(); } catch { }
            return;
        }
        if (!(state is "play" or "title" or "pause")) return;
        if (GEAR == null) gearLoad();
        gearFrom = state; gearOn = true; state = "gear";
        el.classList.toggle("nostash", !gearStashHere());
        el.classList.remove("hidden");
        cvEl.style.cursor = "default";
        gearFit();
        gearNote("");
        renderGear();
        try { SFX.click(); } catch { }
    }
}
