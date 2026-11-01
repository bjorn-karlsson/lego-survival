// lego-survival.html, JS 20960-21194: the equipment screen's tooltip, its one
// pointer handler (click, ctrl-click, drag anywhere it fits, the bin), tidying,
// and gear on the floor.
namespace Brickblade;

/* what a press landed on: a worn slot, or an entry in the bag or the stash */
public sealed class GearSrc { public string slot; public GearItem it; public Element el; public GearEntry entry; public string list; }
public sealed class GearDragSt { public GearSrc src; public double x0, y0, ox, oy; public bool on, ctrl; public int gx, gy; }
public sealed class GearCell { public int x, y; public double c; public string list; }
public sealed class GearTarget { public bool bin; public string slot; public (int x, int y)? cell; public string list; }

public static partial class G
{
    /* ---- THE TOOLTIP ---- */
    public static void gearTipHide() { var t = document.getElementById("gearTip"); if (t != null) t.classList.add("hidden"); }
    public static void gearTipShow(GearSrc src, double cx, double cy)
    {
        var t = document.getElementById("gearTip");
        if (t == null || src == null) return;
        var eq = gearEqNow();
        string h = "";
        if (!string.IsNullOrEmpty(src.slot)) { h = gearTipHtml(eq[src.slot], "EQUIPPED", src.slot); }
        else
        {
            var it = src.entry.it; var slot = gearPickSlot(it); var old = eq.GetValueOrDefault(slot ?? "");
            h = gearTipHtml(it, src.list == "stash" ? "IN THE STASH" : null, slot);
            if (!gearLinked())
            {
                h += gearDeltaHtml(it, old, slot);
                if (old != null) h += gearTipHtml(old, "CURRENTLY EQUIPPED", slot);
            }
        }
        t.innerHTML = h;
        t.classList.remove("hidden");
        var r = t.getBoundingClientRect(); var vw = window.innerWidth; var vh = window.innerHeight;
        double x = cx + 22, y = cy - 20;
        if (x + r.width > vw - 8) x = cx - r.width - 22;
        if (y + r.height > vh - 8) y = vh - 8 - r.height;
        t.style.left = S(Math.max(8, x)) + "px"; t.style.top = S(Math.max(8, y)) + "px";
    }
    /* ---- THE HAND ----
       One pointer handler for the whole screen. A press on an item that moves more
       than a few pixels is a DRAG; one that does not is a CLICK:
         bag item    click wears it           ctrl-click sends it to the stash
         stash item  click brings it to the bag, and so does ctrl-click
         worn item   click takes it off       ctrl-click takes it off into the stash
       and a drag goes anywhere it fits -- either grid, a slot, or the bin. */
    public static readonly GearDragSt gearDrag = new();
    public static GearSrc gearSrcOf(Element el)
    {
        var it = el?.closest(".gitem");
        if (it == null) return null;
        var ds = it.dataset["slot"];
        if (!string.IsNullOrEmpty(ds)) { var eq = gearEqNow(); return eq.GetValueOrDefault(ds) != null ? new GearSrc { slot = ds, it = eq[ds], el = it } : null; }
        var di = it.dataset["inv"];
        if (di != null) { var i = Js.ToNumber(di); var e = i >= 0 && i < GEAR.inv.Count ? GEAR.inv[(int)i] : null; return e != null ? new GearSrc { entry = e, it = e.it, el = it, list = "inv" } : null; }
        var dst = it.dataset["stash"];
        if (dst != null) { var i = Js.ToNumber(dst); var e = i >= 0 && i < GEAR.stash.Count ? GEAR.stash[(int)i] : null; return e != null ? new GearSrc { entry = e, it = e.it, el = it, list = "stash" } : null; }
        return null;
    }
    public static GearCell gearCellAt(double cx, double cy)
    {
        foreach (var (id, key) in new[] { ("gearInv", "inv"), ("gearStash", "stash") })
        {
            var g = document.getElementById(id);
            if (g == null) continue;
            var r = g.getBoundingClientRect();
            if (cx < r.left || cx > r.right || cy < r.top || cy > r.bottom) continue;
            /* the stash is taller than its window: only the rows you can see count */
            var v = key == "stash" ? document.getElementById("gearStashView") : null;
            if (v != null) { var vr = v.getBoundingClientRect(); if (cy < vr.top || cy > vr.bottom || cx > vr.left + r.width) continue; }
            var c = r.width / GEAR_DIM[key].cols;
            return new GearCell { x = Js.I32(Math.floor((cx - r.left) / c)), y = Js.I32(Math.floor((cy - r.top) / c)), c = c, list = key };
        }
        return null;
    }
    public static GearTarget gearDropTarget(double cx, double cy)
    {
        var ghost = document.getElementById("gearGhost");
        if (ghost != null) ghost.style.display = "none";
        var el = document.elementFromPoint(cx, cy);
        if (ghost != null) ghost.style.display = "";
        if (el == null) return null;
        if (el.closest("#gearBin") != null) return new GearTarget { bin = true };
        var sl = el.closest(".gslot");
        if (sl != null) return new GearTarget { slot = sl.dataset["slot"] };
        var cell = gearCellAt(cx, cy);
        if (cell != null) return new GearTarget { cell = (cell.x - gearDrag.gx, cell.y - gearDrag.gy), list = cell.list };
        return null;
    }
    public static void gearDragPaint(double cx, double cy)
    {
        var g = document.getElementById("gearGhost");
        if (g != null) { g.style.left = S(cx - gearDrag.ox) + "px"; g.style.top = S(cy - gearDrag.oy) + "px"; }
        foreach (var e in document.querySelectorAll(".gslot.drop-ok,.gslot.drop-bad")) e.classList.remove("drop-ok", "drop-bad");
        foreach (var e in document.querySelectorAll(".gshadow")) e.classList.add("hidden");
        var T = gearDropTarget(cx, cy); var src = gearDrag.src;
        if (T == null) return;
        if (!string.IsNullOrEmpty(T.slot))
        {
            var el = document.querySelector($".gslot[data-slot=\"{T.slot}\"]");
            if (el != null) el.classList.add(gearFitsSlot(src.it, T.slot) ? "drop-ok" : "drop-bad");
        }
        else if (T.cell != null)
        {
            var shadow = document.querySelector($".gshadow[data-shadow=\"{T.list}\"]");
            if (shadow == null) return;
            var s = gearSize(src.it); var (tx, ty) = T.cell.Value;
            var ok = gearRoomAt(gearList(T.list), src.it, tx, ty, src.entry);
            shadow.className = "gshadow " + (ok ? "ok" : "bad");
            shadow.style.left = $"calc(var(--gc)*{S(tx)})"; shadow.style.top = $"calc(var(--gc)*{S(ty)})";
            shadow.style.width = $"calc(var(--gc)*{S(s.w)})"; shadow.style.height = $"calc(var(--gc)*{S(s.h)})";
        }
    }
    /* DRAGGING TO THE EDGE of the stash scrolls it, the way a long list does --
       and keeps scrolling while the pointer is held there */
    public static double gearScrollT = double.NaN;
    public static void gearAutoScroll(double cx, double cy)
    {
        var v = document.getElementById("gearStashView");
        clearInterval(gearScrollT); gearScrollT = double.NaN;
        if (v == null || !gearDrag.on) return;
        var r = v.getBoundingClientRect(); double edge = 36;
        if (cx < r.left || cx > r.right) return;
        int dir = cy < r.top + edge && cy > r.top - 20 ? -1 : cy > r.bottom - edge && cy < r.bottom + 20 ? 1 : 0;
        if (dir == 0) return;
        void step()
        {
            if (!gearDrag.on) { clearInterval(gearScrollT); gearScrollT = double.NaN; return; }
            v.scrollTop += dir * 14; gearDragPaint(cx, cy);
        }
        step(); gearScrollT = setInterval(step, 30);
    }
    public static void gearDown(DomEvent e)
    {
        if (!gearOn || e.button != 0) return;
        var src = gearSrcOf(e.target);
        if (src == null) return;
        e.preventDefault();
        var r = src.el.getBoundingClientRect();
        var s = gearSize(src.it); var c = r.width / s.w;
        gearDrag.src = src; gearDrag.x0 = e.clientX; gearDrag.y0 = e.clientY; gearDrag.on = false;
        gearDrag.ox = e.clientX - r.left; gearDrag.oy = e.clientY - r.top;
        gearDrag.gx = Js.I32(clamp(Math.floor((e.clientX - r.left) / c), 0, s.w - 1));
        gearDrag.gy = Js.I32(clamp(Math.floor((e.clientY - r.top) / c), 0, s.h - 1));
        gearDrag.ctrl = e.ctrlKey || e.metaKey;
    }
    public static void gearMoveEv(DomEvent e)
    {
        if (!gearOn) return;
        if (gearDrag.src != null)
        {
            if (!gearDrag.on && Math.hypot(e.clientX - gearDrag.x0, e.clientY - gearDrag.y0) > 5)
            {
                gearDrag.on = true;
                gearTipHide();
                var g = document.getElementById("gearGhost");
                var r = gearDrag.src.el.getBoundingClientRect();
                g.innerHTML = gearDrag.src.el.outerHTML;
                g.style.width = S(r.width) + "px"; g.style.height = S(r.height) + "px";
                g.classList.remove("hidden");
                gearDrag.src.el.style.opacity = "0.25";
            }
            if (gearDrag.on) { gearAutoScroll(e.clientX, e.clientY); gearDragPaint(e.clientX, e.clientY); return; }
        }
        var src = gearSrcOf(e.target);
        if (src != null) gearTipShow(src, e.clientX, e.clientY); else gearTipHide();
    }
    public static void gearUp(DomEvent e)
    {
        if (!gearOn || gearDrag.src == null) return;
        var src = gearDrag.src; var was = gearDrag.on;
        gearDrag.src = null; gearDrag.on = false;
        clearInterval(gearScrollT); gearScrollT = double.NaN;
        var g = document.getElementById("gearGhost");
        if (g != null) { g.classList.add("hidden"); g.innerHTML = ""; }
        if (!was)
        {
            gearTipHide();
            if (!string.IsNullOrEmpty(src.slot)) gearUnequip(src.slot, null, null, gearDrag.ctrl ? "stash" : "inv");
            else if (src.list == "stash" || gearDrag.ctrl) gearTransfer(src.entry);
            else gearEquip(src.entry);
            return;
        }
        var T = gearDropTarget(e.clientX, e.clientY);
        if (T == null) { renderGear(); return; }
        if (T.bin) gearDestroy(src);
        else if (!string.IsNullOrEmpty(T.slot))
        {
            if (!string.IsNullOrEmpty(src.slot))
            {
                if (T.slot == src.slot || !gearFitsSlot(src.it, T.slot)) { renderGear(); return; }
                if (gearLinked()) { gearLockNote(); renderGear(); return; }
                /* ring to ring: they swap */
                var other = GEAR.eq.GetValueOrDefault(T.slot); GEAR.eq[T.slot] = src.it; GEAR.eq[src.slot] = other;
                gearChanged(null);
            }
            else if (!gearEquip(src.entry, T.slot)) renderGear();
        }
        else if (T.cell != null)
        {
            var (cx, cy) = T.cell.Value;
            if (!string.IsNullOrEmpty(src.slot)) { if (!gearUnequip(src.slot, cx, cy, T.list)) renderGear(); }
            else if (!gearMove(src.entry, cx, cy, T.list)) renderGear();
        }
    }
    public static void gearWire()
    {
        var scr = document.getElementById("gearScreen");
        if (scr == null) return;
        scr.addEventListener("pointerdown", gearDown);
        window.addEventListener("pointermove", gearMoveEv);
        window.addEventListener("pointerup", gearUp);
        window.addEventListener("resize", () => { if (gearOn) gearFit(); });
        scr.addEventListener("pointerleave", _ => { if (gearDrag.src == null) gearTipHide(); });
        void on(string id, Action f) { var b = document.getElementById(id); if (b != null) b.addEventListener("click", _ => f()); }
        on("gearCloseBtn", () => toggleGear());
        on("gearSortBtn", () => { gearSort("inv"); renderGear(); });
        on("gearStashSortBtn", () => { gearSort("stash"); renderGear(); });
        on("gearStashAllBtn", () => { gearStashAll(); });
        var tabs = document.getElementById("gearTabs");
        if (tabs != null) tabs.addEventListener("click", e =>
        {
            var b = e.target?.closest(".gtab");
            if (b == null || b.disabled || b.dataset["hero"] == weapon.id) return;
            if (gearFrom != "title" || gearLinked()) return;
            setWeapon(b.dataset["hero"]); renderGear();
            try { SFX.click(); } catch { }
        });
    }
    /* TIDY: biggest first, packed column by column */
    public static void gearSort(string key)
    {
        var src = gearList(key); var dim = GEAR_DIM[key == "stash" ? "stash" : "inv"];
        var list = src.ToList();
        list.Sort((a, b) =>
        {
            var A = gearSize(a.it); var B = gearSize(b.it);
            int d = B.w * B.h - A.w * A.h;
            return d != 0 ? d : (string.CompareOrdinal(a.it.b, b.it.b) < 0 ? -1 : 1);
        });
        var outp = new List<GearEntry>();
        foreach (var e in list) { var at = gearFreeSpot(outp, e.it, null, dim); if (at != null) outp.Add(new GearEntry { it = e.it, x = at.Value.x, y = at.Value.y }); else outp.Add(e); }
        if (key == "stash") GEAR.stash = outp; else GEAR.inv = outp;
        gearSave();
    }
    /* ---- ON THE FLOOR, DRAWN ----
       A small picture of the thing on a pool of its rarity's light -- and a RARE
       throws a beam up, PoE's way, so it can be seen across a crowded screen. */
    public static void drawGearDrops()
    {
        foreach (var d in gearDrops.Live())
        {
            if (!onScreen(d, 80)) continue;
            var it = d.it; var B = GEAR_BASE_BY_ID[it.b]; var C = GEAR_CLS[B.cls];
            var pulse = 0.6 + 0.4 * Math.sin(vtime * 4 + d.x * 0.01);
            if (it.r == "rare")
            {
                var gr = ctx.createLinearGradient(d.x, d.y - 150, d.x, d.y);
                gr.addColorStop(0, rgba(255, 255, 119, 0)); gr.addColorStop(1, rgba(255, 255, 119, 0.30 * pulse));
                ctx.fillStyle = gr; ctx.fillRect(d.x - 7, d.y - 150, 14, 150);
            }
            ctx.fillStyle = it.r == "rare" ? rgba(255, 240, 110, 0.30 * pulse) : it.r == "magic" ? rgba(120, 120, 255, 0.28 * pulse) : rgba(220, 220, 220, 0.16);
            ctx.beginPath(); ctx.ellipse(d.x, d.y + 2, 18, 8, 0, 0, TAU); ctx.fill();
            var k = 32.0 / (System.Math.Max(C.w, C.h) * 12);
            ctx.save();
            ctx.translate(d.x - C.w * 6 * k, d.y - d.z - C.h * 12 * k + 4);
            ctx.scale(k, k);
            ctx.lineWidth = 1.2; ctx.strokeStyle = rgba(10, 14, 18, 0.8);
            Col col = B.col ?? "#a9b3bc";
            foreach (var q in gearPaths(B.cls)) { ctx.fillStyle = gearPaint(q.k, col); ctx.fill(q.p, "evenodd"); ctx.stroke(q.p); }
            ctx.restore();
        }
    }
    /* THE LABELS, over everything: PoE's name plate, in the rarity's colour. A plain
       normal item only speaks up when you are close to it. */
    public static void drawGearLabels()
    {
        var p = player;
        ctx.textAlign = "center";
        ctx.font = "700 11px Fredoka";
        foreach (var d in gearDrops.Live())
        {
            if (!onScreen(d, 80)) continue;
            var it = d.it; var R = GEAR_RAR[it.r];
            if (it.r == "normal" && p != null && dist(d, p) > 260) continue;
            var t = gearName(it); var w = ctx.measureText(t).width + 10; var y = d.y - d.z - 34;
            ctx.fillStyle = rgba(8, 10, 12, 0.78);
            roundRect(ctx, d.x - w / 2, y - 10, w, 15, 3); ctx.fill();
            ctx.strokeStyle = it.r == "rare" ? rgba(255, 255, 119, 0.7) : it.r == "magic" ? rgba(136, 136, 255, 0.6) : rgba(200, 200, 200, 0.3);
            ctx.lineWidth = 1; ctx.stroke();
            ctx.fillStyle = (Col)R.col; ctx.fillText(t, d.x, y + 1.5);
        }
    }
}
