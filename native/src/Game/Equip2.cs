// lego-survival.html, JS 20644-20959: the ritual's offer screen (U), and the
// equipment screen's moves — put on, take off, move, stash, destroy — and its
// drawing.
namespace Brickblade;

public sealed class RitualLook { public string icon, name, col, sub, desc; }

public static partial class G
{
    /* ---- THE RITUAL'S OFFER (U) ----
       What the current set of altars is offering, what it costs in tribute, and
       what you have. The run stands still while it is open. */
    public static bool ritualOn = false; public static string ritualFrom = "play"; public static double ritualNoteT = double.NaN;
    public static void toggleRitual()
    {
        var el = document.getElementById("ritualScreen");
        if (el == null) return;
        if (ritualOn)
        {
            ritualOn = false;
            el.classList.add("hidden");
            gearTipHide();
            state = ritualFrom;
            if (state == "play") { cvEl.style.cursor = "none"; try { window.focus(); } catch { } }
            try { SFX.click(); } catch { }
            return;
        }
        if (state != "play") return;
        if (!ritualCanShop())
        {
            if (player != null) addDmg(player.x, player.y - 60, ritualS.altars.Count > 0 ? "FINISH A RITUAL FIRST" : "NO RITUAL YET", "#ff8a8a", 14);
            return;
        }
        if (GEAR == null) gearLoad();
        ritualFrom = state; ritualOn = true; state = "ritual";
        el.classList.remove("hidden");
        cvEl.style.cursor = "default";
        ritualNote("");
        renderRitual();
        try { SFX.click(); } catch { }
    }
    public static void ritualNote(string t, bool bad = false)
    {
        var n = document.getElementById("ritNote");
        if (n == null) return;
        n.textContent = t ?? ""; n.style.color = bad ? "#ff8a6a" : "#9fdc7e";
        clearTimeout(ritualNoteT);
        if (!string.IsNullOrEmpty(t)) ritualNoteT = setTimeout(() => { n.textContent = ""; }, 3200);
    }
    public static string ritualChestSvg(string col) =>
        "<svg viewBox=\"0 0 40 32\"><rect x=\"4\" y=\"12\" width=\"32\" height=\"17\" rx=\"2\" fill=\"#8a5a2b\" stroke=\"#1b1f23\" stroke-width=\"1.2\"/>" +
        "<path d=\"M4 12 Q20 -1 36 12 Z\" fill=\"#a86c33\" stroke=\"#1b1f23\" stroke-width=\"1.2\"/>" +
        $"<rect x=\"3\" y=\"10.5\" width=\"34\" height=\"3.2\" fill=\"{col}\"/><circle cx=\"20\" cy=\"19.5\" r=\"3.2\" fill=\"{col}\"/></svg>";
    public static RitualLook ritualOfferLook(RitualOffer o)
    {
        if (o.kind == "gear")
        {
            var B = GEAR_BASE_BY_ID[o.it.b]; var Rg = GEAR_RAR[o.it.r];
            return new RitualLook { icon = gearIconSvg(o.it), name = gearName(o.it), col = Rg.col,
                sub = GEAR_CLS[B.cls].name + " · item level " + S(o.it.il) };
        }
        var R = RARITY.GetValueOrDefault(o.rar ?? "") ?? RARITY["common"];
        if (o.kind == "card")
        {
            var u = UPGRADES.Find(x => x.id == o.uid); var owned = taken[o.uid];
            return new RitualLook { icon = $"<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"{R.col}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{ICONS.GetValueOrDefault(u.icon ?? "") ?? ""}</svg>",
                name = u.name, col = R.col, sub = R.name + " CARD · " + u.cls + (owned != 0 ? " · you have ×" + S(owned) : ""),
                desc = signWords(u.desc(u.calc(R.mult), owned)) };
        }
        return new RitualLook { icon = ritualChestSvg(R.col), name = R.name + " CHEST", col = R.col, sub = "a " + o.rar + " brick, opened on R" };
    }
    public static void renderRitual()
    {
        var St = ritualS;
        var done = St.altars.Count(A => A.state == "done");
        var sub = document.getElementById("ritSub");
        if (sub != null) sub.textContent = S(done) + " of " + S(St.altars.Count) + " altars complete · every altar in a set offers the same, and tribute you do not spend is kept";
        var tr = document.getElementById("ritTrib");
        if (tr != null) tr.innerHTML = "TRIBUTE <b>" + S(St.tribute) + "</b>";
        var g = document.getElementById("ritGrid");
        if (g != null)
        {
            g.innerHTML = St.offers.Count > 0 ? string.Join("", St.offers.Select(o =>
            {
                var L = ritualOfferLook(o); var usable = ritualOfferUsable(o); var afford = St.tribute >= o.cost && usable;
                var canDefer = St.deferred.Count < RITUAL_DEFER_MAX;
                return $"<div class=\"ritcard{(afford ? "" : " poor")}\" data-id=\"{S(o.id)}\">" +
                  (o.carried ? "<div class=\"ritcarried\">RETURNED</div>" : "") +
                  $"<div class=\"riticon\">{L.icon}</div><div class=\"ritname\" style=\"color:{L.col}\">{L.name}</div>" +
                  $"<div class=\"ritkind\">{L.sub}</div>" + (!string.IsNullOrEmpty(L.desc) ? $"<div class=\"ritdesc\">{L.desc}</div>" : "") +
                  $"<div class=\"ritcost\">{(usable ? S(o.cost) + " tribute" : "YOU CAN NO LONGER TAKE IT")}</div>" +
                  $"<div class=\"ritbtns\"><button class=\"tbtn\" data-act=\"buy\"{(afford ? "" : " disabled")}>{(o.kind == "card" ? "TAKE" : "BUY")}</button>" +
                  $"<button class=\"tbtn gray\" data-act=\"defer\"{(canDefer ? "" : " disabled")}>DEFER</button></div></div>";
            })) : "<div class=\"ritkind\" style=\"grid-column:1/-1;padding:20px\">NOTHING LEFT ON OFFER</div>";
        }
        var d = document.getElementById("ritDefer");
        if (d != null) d.textContent = St.deferred.Count > 0
          ? "DEFERRED TO THE NEXT RITUAL (" + S(St.deferred.Count) + "/" + S(RITUAL_DEFER_MAX) + "): " + string.Join(" — ", St.deferred.Select(o => ritualOfferLook(o).name + " · " + S(o.cost)))
          : "";
        var rb = document.getElementById("ritRerollBtn");
        if (rb != null) { var c = ritualRerollCost(); rb.textContent = "REROLL · " + S(c); rb.disabled = St.tribute < c; }
    }
    public static void ritualUiWire()
    {
        var g = document.getElementById("ritGrid");
        if (g == null || !string.IsNullOrEmpty(g.dataset["wired"])) return;
        g.dataset["wired"] = "1";
        g.addEventListener("click", ev =>
        {
            var b = ev.target?.closest("button[data-act]"); var card = ev.target?.closest(".ritcard");
            if (b == null || card == null || b.disabled) return;
            var id = Js.I32(Js.ToNumber(card.dataset["id"])); var o = ritualS.offers.Find(q => q.id == id);
            if (o == null) return;
            if (b.dataset["act"] == "buy")
            {
                var L = ritualOfferLook(o); var r = ritualBuy(id);
                if (r == "poor") { ritualNote("NOT ENOUGH TRIBUTE", true); return; }
                if (r == "maxed") { ritualNote("YOU CAN NO LONGER TAKE " + L.name, true); return; }
                ritualNote(r == "floor" ? "BAG FULL — " + L.name + " LIES AT YOUR FEET"
                         : r == "chest" ? L.name + " — WAITING ON R"
                         : r == "card" ? L.name + " — TAKEN" : L.name + " — IN YOUR BAG");
                gearTipHide();
                try { SFX.fortune(); } catch { }
            }
            else
            {
                if (ritualDefer(id)) { ritualNote("DEFERRED — IT COMES BACK IN THE NEXT RITUAL"); gearTipHide(); }
                try { SFX.click(); } catch { }
            }
            renderRitual();
            if (ritualStuck()) { gearTipHide(); setTimeout(() => { if (ritualOn && ritualStuck()) ritualFinish(); }, 700); }
        });
        g.addEventListener("mousemove", ev =>
        {
            var card = ev.target?.closest(".ritcard");
            var o = card != null ? ritualS.offers.Find(q => q.id == Js.ToNumber(card.dataset["id"])) : null;
            if (o != null && o.kind == "gear") gearTipShow(new GearSrc { entry = new GearEntry { it = o.it }, it = o.it, list = "inv" }, ev.clientX, ev.clientY);
            else gearTipHide();
        });
        g.addEventListener("mouseleave", _ => gearTipHide());
        document.getElementById("ritRerollBtn").addEventListener("click", _ =>
        {
            if (ritualReroll()) { ritualNote("THE OFFER CHANGES"); try { SFX.click(); } catch { } }
            else ritualNote("NOT ENOUGH TRIBUTE", true);
            renderRitual();
        });
        document.getElementById("ritCloseBtn").addEventListener("click", _ => toggleRitual());
        document.getElementById("ritDoneBtn").addEventListener("click", _ => ritualFinish());
    }
    public static double gearNoteT = double.NaN;
    public static void gearNote(string t, bool bad = false)
    {
        var n = document.getElementById("gearNote");
        if (n == null) return;
        n.textContent = t ?? ""; n.style.color = bad ? "#ff8a6a" : "#9fdc7e";
        clearTimeout(gearNoteT);
        if (!string.IsNullOrEmpty(t)) gearNoteT = setTimeout(() => { n.textContent = ""; }, 3200);
    }
    public static void refreshGearBtn()
    {
        if (GEAR == null) gearLoad();
        var b = document.getElementById("gearCount");
        if (b != null) b.textContent = S(GEAR.inv.Count);
    }
    /* ---- WHAT A CHANGE DOES ----
       Mid-run, the hero wears it now. */
    public static void gearChanged(GearItem it)
    {
        gearSave();
        if ((gearFrom == "play" || gearFrom == "pause") && player != null && !player.dead) gearApply(player, false);
        renderGear();
    }
    public static string gearPickSlot(GearItem it)
    {
        var s = gearSlotOf(it);
        if (s != "ring") return s;
        return GEAR.eq.GetValueOrDefault("ring1") == null ? "ring1" : GEAR.eq.GetValueOrDefault("ring2") == null ? "ring2" : "ring1";
    }
    public static void gearLockNote() { gearNote("A RUN LINK WEARS THE GEAR IT WAS COPIED WITH", true); }
    /* the list an entry lives in: the bag or the stash */
    public static List<GearEntry> gearListOf(GearEntry entry) => GEAR.stash.IndexOf(entry) >= 0 ? GEAR.stash : GEAR.inv;
    public static List<GearEntry> gearList(string k) => k == "stash" ? GEAR.stash : GEAR.inv;
    /* PUT IT ON, from the bag or the stash. What it replaces goes where it was if it
       fits there, else anywhere in the bag, else anywhere in the stash. */
    public static bool gearEquip(GearEntry entry, string slot = null)
    {
        if (gearLinked()) { gearLockNote(); return false; }
        var it = entry.it; var list = gearListOf(entry);
        /* ANOTHER HERO'S WEAPON. Between runs, putting it on switches to that hero
           (and its own set of gear) the way the weapon row does; mid-run the hero you
           are playing cannot pick up a different trade. */
        var C = GEAR_CLS[GEAR_BASE_BY_ID[it.b].cls];
        if (!string.IsNullOrEmpty(C.weap) && C.weap != weapon.id)
        {
            if (gearFrom != "title") { gearNote("A " + weapon.name + " HERO CANNOT WIELD A " + C.weap.ToUpperInvariant() + " — switch hero on the title screen", true); return false; }
            setWeapon(C.weap);
        }
        slot = string.IsNullOrEmpty(slot) ? gearPickSlot(it) : slot;
        if (!gearFitsSlot(it, slot)) { gearNote("THAT DOES NOT GO THERE", true); return false; }
        var old = GEAR.eq.GetValueOrDefault(slot); var idx = list.IndexOf(entry);
        if (idx < 0) return false;
        list.RemoveAt(idx);
        if (old != null)
        {
            var to = list; (int x, int y)? at = gearRoomAt(list, old, entry.x, entry.y) ? (entry.x, entry.y) : null;
            if (at == null) { to = GEAR.inv; at = gearFreeSpot(GEAR.inv, old); }
            if (at == null) { to = GEAR.stash; at = gearFreeSpot(GEAR.stash, old); }
            if (at == null) { list.Insert(idx, entry); gearNote("NO ROOM FOR WHAT YOU ARE WEARING", true); return false; }
            to.Add(new GearEntry { it = old, x = at.Value.x, y = at.Value.y });
        }
        GEAR.eq[slot] = it;
        gearChanged(it);
        return true;
    }
    /* TAKE IT OFF into the bag (or the stash), at a spot or the first that fits */
    public static bool gearUnequip(string slot, int? x = null, int? y = null, string to = null)
    {
        if (gearLinked()) { gearLockNote(); return false; }
        var it = GEAR.eq.GetValueOrDefault(slot);
        if (it == null) return false;
        var list = gearList(to);
        (int x, int y)? at = x != null ? (gearRoomAt(list, it, x.Value, y.Value) ? (x.Value, y.Value) : null) : gearFreeSpot(list, it);
        if (at == null) { gearNote(x == null ? (to == "stash" ? "THE STASH IS FULL" : "THE BAG IS FULL") : "IT DOES NOT FIT THERE", true); return false; }
        GEAR.eq[slot] = null;
        list.Add(new GearEntry { it = it, x = at.Value.x, y = at.Value.y });
        gearChanged(null);
        return true;
    }
    /* MOVE an entry to a spot, in its own grid or the other one */
    public static bool gearMove(GearEntry entry, int x, int y, string to = null)
    {
        var from = gearListOf(entry); var dest = to != null ? gearList(to) : from;
        if (!gearRoomAt(dest, entry.it, x, y, entry)) { gearNote("IT DOES NOT FIT THERE", true); return false; }
        if (dest != from) { from.Remove(entry); dest.Add(entry); }
        entry.x = x; entry.y = y;
        gearSave(); renderGear();
        return true;
    }
    /* CTRL-CLICK: across to the other grid, wherever it first fits */
    public static bool gearTransfer(GearEntry entry)
    {
        if (!gearStashHere()) { gearNote("THE STASH IS IN BRICKHAVEN", true); return false; }
        var from = gearListOf(entry); var dest = from == GEAR.stash ? GEAR.inv : GEAR.stash;
        var at = gearFreeSpot(dest, entry.it);
        if (at == null) { gearNote(dest == GEAR.stash ? "THE STASH IS FULL" : "THE BAG IS FULL", true); return false; }
        from.Remove(entry);
        entry.x = at.Value.x; entry.y = at.Value.y; dest.Add(entry);
        gearSave(); renderGear();
        return true;
    }
    /* THE BUTTON: everything in the bag into the stash, biggest first so the big
       pieces find room before the rings fill the gaps. Returns how many moved. */
    public static int gearStashAll()
    {
        if (!gearStashHere()) { gearNote("THE STASH IS IN BRICKHAVEN", true); return 0; }
        var list = GEAR.inv.OrderByDescending(a => { var A = gearSize(a.it); return A.w * A.h; }).ToList();
        int n = 0;
        foreach (var e in list)
        {
            var at = gearFreeSpot(GEAR.stash, e.it);
            if (at == null) continue;
            GEAR.inv.Remove(e);
            e.x = at.Value.x; e.y = at.Value.y; GEAR.stash.Add(e); n++;
        }
        gearSave(); renderGear();
        gearNote(n == 0 ? (GEAR.inv.Count > 0 ? "THE STASH IS FULL" : "THE BAG IS EMPTY")
          : GEAR.inv.Count > 0 ? S(n) + " STASHED — THE STASH IS FULL" : S(n) + " STASHED", n == 0 || GEAR.inv.Count > 0);
        return n;
    }
    public static bool gearDestroy(GearSrc src)
    {
        if (!string.IsNullOrEmpty(src.slot))
        {
            if (gearLinked()) { gearLockNote(); return false; }
            var it = GEAR.eq.GetValueOrDefault(src.slot); if (it == null) return false;
            GEAR.eq[src.slot] = null;
            gearNote("DESTROYED — " + gearName(it));
        }
        else
        {
            var list = gearListOf(src.entry); var i = list.IndexOf(src.entry); if (i < 0) return false;
            list.RemoveAt(i);
            gearNote("DESTROYED — " + gearName(src.entry.it));
        }
        gearChanged(null);
        return true;
    }
    /* ---- DRAWING IT ---- */
    public static string gearItemEl(GearItem it, bool off)
    {
        var R = GEAR_RAR[it.r];
        return $"<div class=\"gitem {it.r}{(off ? " off" : "")}\" style=\"background:{R.bg}\">{gearIconSvg(it)}</div>";
    }
    static string ReplaceFirst(string s, string a, string b) { int i = s.IndexOf(a, StringComparison.Ordinal); return i < 0 ? s : s.Substring(0, i) + b + s.Substring(i + a.Length); }
    public static string gearGridHtml(List<GearEntry> list, string key)
    {
        var h = "";
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i]; var s = gearSize(e.it);
            var one = ReplaceFirst(gearItemEl(e.it, false), "class=\"gitem", $"data-{key}=\"{S(i)}\" class=\"gitem");
            h += ReplaceFirst(one, "style=\"", $"style=\"left:calc(var(--gc)*{S(e.x)});top:calc(var(--gc)*{S(e.y)});width:calc(var(--gc)*{S(s.w)});height:calc(var(--gc)*{S(s.h)});");
        }
        return h + $"<div class=\"gshadow hidden\" data-shadow=\"{key}\"></div>";
    }
    public static void renderGear()
    {
        if (GEAR == null) gearLoad();
        var doll = document.getElementById("gearDoll"); var inv = document.getElementById("gearInv"); var st = document.getElementById("gearStash");
        if (doll == null || inv == null) return;
        var eq = gearEqNow();
        var linked = gearLinked();
        var sub = document.getElementById("gearSub");
        if (sub != null) sub.textContent = linked ? "A RUN LINK WEARS THE GEAR IT WAS COPIED WITH — your own bag and stash are below"
          : !gearStashHere() ? "Your bag and what you wear. The stash is at home, in Brickhaven."
          : "Each hero wears its own gear; the bag and stash are shared. Saved as it changes — dying keeps it all.";
        var h2 = document.querySelector("#gearScreen h2"); if (h2 != null) h2.textContent = gearStashHere() ? "EQUIPMENT" : "INVENTORY";
        var foot = document.querySelector("#gearScreen .sheetfoot");
        if (foot != null) foot.innerHTML = gearStashHere() ? "I or ESC to close &middot; click to wear or take off &middot; Ctrl-click to send between bag and stash &middot; drag anywhere it fits &middot; drop in the bin to destroy"
          : "I or ESC to close &middot; click to wear or take off &middot; drag anywhere it fits &middot; drop in the bin to destroy &middot; the stash is in Brickhaven";
        var h = "";
        foreach (var s in GEAR_SLOTS)
        {
            var D = GEAR_DOLL[s]; var it = eq.GetValueOrDefault(s);
            h += $"<div class=\"gslot\" data-slot=\"{s}\" style=\"left:calc(var(--gc)*{S(D.x)});top:calc(var(--gc)*{S(D.y)});" +
              $"width:calc(var(--gc)*{S(D.w)});height:calc(var(--gc)*{S(D.h)})\">" +
              (it != null ? ReplaceFirst(gearItemEl(it, !gearActive(it, s)), "class=\"gitem", $"data-slot=\"{s}\" class=\"gitem")
                  : $"<div class=\"glbl\">{GEAR_SLOT_NAME[s]}</div>") + "</div>";
        }
        doll.innerHTML = h;
        /* THE SIX HEROES, each with its own set: switchable between runs, fixed mid-run */
        var tabs = document.getElementById("gearTabs");
        if (tabs != null)
        {
            var free = gearFrom == "title" && !linked;
            tabs.innerHTML = string.Join("", WEAPON_KEYS.Select(k =>
            {
                var W = WEAPONS[k]; var on = k == weapon.id;
                var n = GEAR_SLOTS.Count(s => GEAR.eqBy.TryGetValue(k, out var E) && E != null && E.GetValueOrDefault(s) != null);
                return $"<button class=\"gtab{(on ? " on" : "")}\" data-hero=\"{k}\" {(!on && !free ? "disabled" : "")} " +
                  $"style=\"--hc:{S(W.col)}\" title=\"{(free || on ? W.name + " — " + S(n) + " worn" : "switch hero on the title screen")}\">{W.name}<i>{S(n)}</i></button>";
            }));
        }
        inv.innerHTML = gearGridHtml(GEAR.inv, "inv");
        if (st != null) st.innerHTML = gearGridHtml(GEAR.stash, "stash");
        var sc = document.getElementById("gearStashCount");
        if (sc != null)
        {
            int deep = 0; foreach (var e in GEAR.stash) deep = System.Math.Max(deep, e.y + gearSize(e.it).h);
            sc.textContent = S(GEAR.stash.Count) + " item" + (GEAR.stash.Count == 1 ? "" : "s") + " · " + S(deep) + " of " + S(GEAR_STASH_ROWS) + " rows";
        }
        /* what it all adds up to */
        var sum = document.getElementById("gearSum");
        if (sum != null)
        {
            var tot = new NumMap();
            foreach (var s in GEAR_SLOTS) { var it = eq.GetValueOrDefault(s); if (it != null && gearActive(it, s)) foreach (var (k, v) in gearMods(it)) tot[k] = tot[k] + v; }
            var ks = tot.Keys.OrderBy(gearStatRank).ToList();
            sum.innerHTML = "<div class=\"gshead\">YOUR GEAR GIVES</div>" + (ks.Count > 0
              ? string.Join("", ks.Select(k => $"<div class=\"gsrow\">{gearModText(k, gearRound(k, tot[k]))}</div>"))
              : "<div class=\"gsnone\">Nothing yet. Every boss drops a piece of gear — a super boss two.</div>")
              + $"<div class=\"gsfoot\">{S(GEAR.inv.Count)} in the bag · {S(GEAR.stash.Count)} in the stash</div>";
        }
    }
    /* the summary reads like a character sheet: what keeps you alive, then what
       you resist, then what you are made of, then what you hit with */
    public static readonly string[] GEAR_STAT_ORDER = { "hearts","es","armour","eva","regen","suppress","fireRes","frostRes","shockRes","chaosRes","allRes",
      "str","dex","int","allAttr","flatPhys","flatElem","arrowFlat","minionFlat","incMelee","incSpell","incElem","minionDmg","minionLife",
      "incAtk","incCast","incCrit","critMult","acc","incAcc","bleedChance","leech","speed","studs","magnet" };
    public static int gearStatRank(string k) { var i = Array.IndexOf(GEAR_STAT_ORDER, k); return i < 0 ? 999 : i; }
}
