// The native UI engine, part 8: the document's state and its events. Style and
// layout are recomputed lazily (a change marks what it touched; the next read
// of geometry, or the next frame, brings everything up to date); pointer,
// wheel and keyboard input is routed the way a browser routes it: hover and
// active chains, mouseenter/leave, mousedown/up/click on the topmost element,
// contextmenu, wheel scrolling of the nearest scroller, focus, and the default
// editing of a focused text field or slider.
using Raylib_cs;

namespace Brickblade;

/* what a browser KeyboardEvent carried, as the native loop fills it in */
public sealed class KeyEvt { public string code, key; public bool repeat, shiftKey, ctrlKey, metaKey, prevented; public void preventDefault() => prevented = true; }

public static class Dom
{
    public static Document document;
    public static Win window;
    public static double ViewW = 1280, ViewH = 720, Scale = 1, ZoomRoot = 1;
    public static double RootFont => document?.documentElement.cs?.fontSize ?? 16;
    public static Element focused, hovered, pressed;
    /* the element the game's field is drawn in (#game): drawn on the window first, the page over it */
    public static Element FieldEl;
    public static double lastKeyTime;
    public static int StyleGen;
    static bool styleDirty = true, layoutDirty = true, allDirty = true;
    static Dictionary<string, Element> ids; static bool idsDirty = true;
    static bool dynSubtree;     /* some :hover rule styles a descendant of the hovered element */
    static Box root;
    public static bool Ready => document != null;
    public static readonly List<string> errors = new();
    public static void Report(Exception e) { errors.Add(e.ToString()); if (errors.Count > 50) errors.RemoveAt(0); Console.Error.WriteLine("[ui] " + e.Message + "\n" + e.StackTrace); }

    public static void Init(string css, string bodyHtml)
    {
        document = new Document(); window = new Win();
        Sheet.Load(Cascade.UA);
        Sheet.uaCount = Sheet.rules.Count;
        Sheet.Load(css);
        dynSubtree = Sheet.rules.Any(r => r.sel.dynamic && r.sel.parts.Take(r.sel.parts.Count - 1).Any(p => p.pseudos.Any(q => q.name is "hover" or "active" or "focus" or "focus-within")) || r.sel.dynamic && r.sel.pseudoEl != null);
        document.body.innerHTML = bodyHtml;
        StyleAll();
    }

    /* ---- invalidation ---- */
    internal static void Changed(Node n)
    {
        layoutDirty = true;
        if (n is Element e) MarkDirty(e, sibs: true);
        else if (n is TextNode t && t.parentNode != null) { MarkDirty(t.parentNode, false, onlyEmpty: true); MutationObserver.Note(t.parentNode); }
    }
    internal static void TreeChanged(Element parent)
    {
        MutationObserver.Note(parent);
        idsDirty = true; layoutDirty = true;
        MarkDirty(parent, true);
        foreach (var c in parent.childNodes) if (c is Element ce) MarkDirty(ce, false);
    }
    internal static void Detached(Element e)
    {
        if (focused != null && e.contains(focused)) focused = null;
        if (hovered != null && e.contains(hovered)) hovered = null;
        if (pressed != null && e.contains(pressed)) pressed = null;
        idsDirty = true;
    }
    internal static void IdsDirty() { idsDirty = true; }
    /* dirt: the element restyles; ancestors know a descendant must be visited */
    static void MarkDirty(Element e, bool sibs, bool onlyEmpty = false)
    {
        styleDirty = true;
        e.dirty = true;
        if (sibs && e.parentNode != null) e.parentNode.sibsDirty = true;
        for (var p = e.parentNode; p != null && !p.kidsDirty; p = p.parentNode) p.kidsDirty = true;
    }
    static void MarkSubtree(Element e) { MarkDirty(e, false); e.subDirty = true; }
    public static void StyleAll() { allDirty = true; styleDirty = true; layoutDirty = true; Cascade.ClearShare(); StyleGen++; }
    internal static void Scrolled() { scrolledFlag = true; }
    static bool scrolledFlag;

    public static Element ById(string id)
    {
        if (idsDirty || ids == null)
        {
            ids = new Dictionary<string, Element>();
            void W(Element e) { var i = e.attrs.GetValueOrDefault("id"); if (i != null && !ids.ContainsKey(i)) ids[i] = e; foreach (var c in e.childNodes) if (c is Element ce) W(ce); }
            W(document.documentElement);
            idsDirty = false;
        }
        return ids.GetValueOrDefault(id ?? "");
    }

    /* ---- style and layout, brought up to date ---- */
    public static void EnsureStyle()
    {
        if (!styleDirty && !allDirty) return;
        bool all = allDirty;
        allDirty = false; styleDirty = false;
        Restyle(document.documentElement, null, all, null);
        StyleGen++;
        layoutDirty = true;
    }
    static void Restyle(Element e, CStyle parentCs, bool force, CStyle rootCs)
    {
        bool sibs = e.sibsDirty;
        if (force || e.dirty || e.cs == null || e.subDirty)
        {
            var old = e.cs;
            try { Cascade.Compute(e, parentCs, rootCs); }
            catch (Exception ex) { Report(ex); e.cs ??= new CStyle(); }
            if (old != null && old != e.cs) Anim.Restyled(e, old, e.cs);
            bool changed = old != e.cs;
            bool sub = e.subDirty;
            e.dirty = false; e.subDirty = false; e.kidsDirty = false; e.sibsDirty = false;
            rootCs ??= e.cs;
            foreach (var c in e.childNodes) if (c is Element ce) Restyle(ce, e.cs, force || changed || sub || sibs, rootCs);
            return;
        }
        if (!e.kidsDirty && !sibs) return;
        e.kidsDirty = false; e.sibsDirty = false;
        rootCs ??= e.cs;
        foreach (var c in e.childNodes) if (c is Element ce) { if (sibs) ce.dirty = true; Restyle(ce, e.cs, false, rootCs); }
    }
    public static void EnsureLayout()
    {
        if (document == null) return;
        EnsureStyle();
        if (!layoutDirty) return;
        layoutDirty = false;
        try
        {
            root = Layout.Build(document.documentElement, null);
            if (root != null) Layout.Run(root, ViewW, ViewH);
            ClampScrolls(document.documentElement);
        }
        catch (Exception ex) { Report(ex); }
        LayoutCount++;
    }
    public static int LayoutCount;
    static void ClampScrolls(Element e)
    {
        if (e.box != null && (e.scrollX != 0 || e.scrollY != 0))
        {
            var b = e.box;
            e.scrollY = Math.Max(0, Math.Min(e.scrollY, Math.Max(0, b.contentH - b.PadH)));
            e.scrollX = Math.Max(0, Math.Min(e.scrollX, Math.Max(0, b.contentW - b.PadW)));
        }
        foreach (var c in e.childNodes) if (c is Element ce) ClampScrolls(ce);
    }

    /* ---- the frame ---- */
    public static void Resize(double cssW, double cssH, double scale)
    {
        if (cssW == ViewW && cssH == ViewH && scale == Scale) return;
        ViewW = cssW; ViewH = cssH; Scale = scale;
        StyleAll();
        if (window != null) window.Fire(new DomEvent { type = "resize" });
    }
    public static void Paint(Ctx g, double time)
    {
        if (document == null) return;
        Paint2.Time = time; Anim.Now = time;
        EnsureLayout();
        if (root == null) return;
        try { Paint2.Paint(g, root, Scale); }
        catch (Exception ex) { Report(ex); }
        Gpu.Scissor(null);
    }
    public static Element HitTest(double x, double y)
    {
        EnsureLayout();
        if (root == null) return null;
        try { return Paint2.Hit(root, x, y, Scale, out _); } catch (Exception ex) { Report(ex); return null; }
    }

    /* ---- hover, active, focus ---- */
    internal static bool InHover(Element e) => hovered != null && e.contains(hovered);
    internal static bool InActive(Element e) => pressed != null && e.contains(pressed) && InHover(e);
    static void StateChange(Element a, Element b)
    {
        /* every element whose :hover/:active state flipped restyles (with its subtree when a rule reaches into it) */
        var sa = new HashSet<Element>(); for (var p = a; p != null; p = p.parentNode) sa.Add(p);
        var sb = new HashSet<Element>(); for (var p = b; p != null; p = p.parentNode) sb.Add(p);
        foreach (var x in sa) if (!sb.Contains(x)) Touch(x);
        foreach (var x in sb) if (!sa.Contains(x)) Touch(x);
        layoutDirty = true;
    }
    static void Touch(Element x)
    {
        if (dynSubtree) MarkSubtree(x); else MarkDirty(x, true);
    }
    public static void Focus(Element e)
    {
        if (e == focused) return;
        var old = focused;
        focused = e;
        if (old != null) { Touch(old); Dispatch(old, new DomEvent { type = "blur", bubbles = false }); Dispatch(old, new DomEvent { type = "focusout" }); if (old.localName == "input" && old.type != "range" && old.changedSinceFocus) { old.changedSinceFocus = false; Dispatch(old, new DomEvent { type = "change" }); } }
        if (e != null) { Touch(e); e.caret = e.value.Length; e.selFrom = -1; Dispatch(e, new DomEvent { type = "focus", bubbles = false }); Dispatch(e, new DomEvent { type = "focusin" }); }
        layoutDirty = true;
    }
    public static bool TextFocused => focused != null && focused.localName is "input" or "textarea" && focused.type != "range" && focused.isConnected;

    /* ---- event dispatch: target, then up the tree, then the window ---- */
    public static void Dispatch(Element target, DomEvent ev)
    {
        ev.target ??= target;
        var path = new List<Element>();
        for (var p = target; p != null; p = p.parentNode) path.Add(p);
        foreach (var el in path)
        {
            ev.currentTarget = el;
            if (el.listeners != null && el.listeners.TryGetValue(ev.type, out var l))
                foreach (var x in l.ToList())
                {
                    try { x.fn(ev); } catch (Exception e) { Report(e); }
                    if (ev.stoppedNow) break;
                }
            var prop = el.Prop(ev.type);
            if (prop != null) { try { prop(ev); } catch (Exception e) { Report(e); } }
            if (ev.stopped || !ev.bubbles) break;
        }
        if (ev.bubbles && !ev.stopped) { ev.currentTarget = null; window.Fire(ev); }
    }
    static void Enter(Element e, Element related, string type)
    {
        Dispatch(e, new DomEvent { type = type, bubbles = false, clientX = mx, clientY = my, relatedTarget = related });
    }

    /* ---- pointer input (client CSS px) ---- */
    static double mx = -1, my = -1;
    static Element downTarget, rangeDrag; static int downButton = -1;
    public static Element PointerTarget { get; private set; }
    public static bool Shift, Ctrl, Alt;
    public static void PointerMove(double x, double y, int buttons)
    {
        double dx = mx < 0 ? 0 : x - mx, dy = my < 0 ? 0 : y - my;
        mx = x; my = y; lastButtons = buttons;
        var t = HitTest(x, y);
        PointerTarget = t;
        if (t != hovered)
        {
            var old = hovered;
            hovered = t;
            StateChange(old, t);
            /* leave the old chain where it is not shared, enter the new */
            var oldChain = new List<Element>(); for (var p = old; p != null; p = p.parentNode) oldChain.Add(p);
            var newChain = new List<Element>(); for (var p = t; p != null; p = p.parentNode) newChain.Add(p);
            var keep = new HashSet<Element>(newChain);
            if (old != null) { Dispatch(old, Ev("mouseout", x, y, buttons, t)); Dispatch(old, Ev("pointerout", x, y, buttons, t)); }
            foreach (var p in oldChain) if (!keep.Contains(p) && p.isConnected) { Enter(p, t, "mouseleave"); Enter(p, t, "pointerleave"); }
            var had = new HashSet<Element>(oldChain);
            for (int i = newChain.Count - 1; i >= 0; i--) if (!had.Contains(newChain[i])) { Enter(newChain[i], old, "mouseenter"); Enter(newChain[i], old, "pointerenter"); }
            if (t != null) { Dispatch(t, Ev("mouseover", x, y, buttons, old)); Dispatch(t, Ev("pointerover", x, y, buttons, old)); }
        }
        if (dx != 0 || dy != 0)
        {
            var target = t ?? document.body;
            var ev = Ev("mousemove", x, y, buttons, null); ev.movementX = dx; ev.movementY = dy;
            Dispatch(target, ev);
            var pv = Ev("pointermove", x, y, buttons, null); pv.movementX = dx; pv.movementY = dy;
            Dispatch(target, pv);
            if (rangeDrag != null) RangeSet(rangeDrag, x);
        }
    }
    /* WHAT IS UNDER A STILL POINTER CHANGES WITH THE PAGE. A browser re-hit-tests
       after a layout change (a fake mouse move): a screen that hides under the
       pointer loses its hover, and its tooltip goes, without the mouse moving. */
    static int refreshedAt = -1, lastButtons;
    public static void PointerRefresh()
    {
        if (mx < 0 || refreshedAt == LayoutCount) return;
        refreshedAt = LayoutCount;
        PointerMove(mx, my, lastButtons);
    }
    static DomEvent Ev(string type, double x, double y, int buttons, Element related) => new DomEvent
    {
        type = type, clientX = x, clientY = y, buttons = buttons, relatedTarget = related, shiftKey = Shift, ctrlKey = Ctrl, altKey = Alt,
    };
    public static void PointerDown(int button)
    {
        var t = HitTest(mx, my) ?? document.body;
        PointerTarget = t;
        downTarget = t; downButton = button;
        if (button == 0) { var old = pressed; pressed = t; StateChange(old, t); }
        var ev = Ev("mousedown", mx, my, 1 << button, null); ev.button = button; ev.target = t;
        var pv = Ev("pointerdown", mx, my, 1 << button, null); pv.button = button; pv.target = t;
        Dispatch(t, pv);
        Dispatch(t, ev);
        /* focus follows the click; text fields and sliders take it */
        if (button == 0 && !ev.defaultPrevented)
        {
            Element f = null;
            for (var p = t; p != null; p = p.parentNode) if (p.localName is "input" or "textarea" or "button" || p.hasAttribute("tabindex")) { f = p; break; }
            if (f != null && f.localName == "input" && !f.disabled) { Focus(f); if (f.type == "range") { rangeDrag = f; RangeSet(f, mx); } else CaretAt(f, mx); }
            else if (f == null || f.localName != "input") Focus(f != null && f.localName == "button" ? null : f);
        }
    }
    public static void PointerUp(int button)
    {
        var t = HitTest(mx, my) ?? document.body;
        var ev = Ev("mouseup", mx, my, 0, null); ev.button = button; ev.target = t;
        var pv = Ev("pointerup", mx, my, 0, null); pv.button = button; pv.target = t;
        Dispatch(t, pv);
        Dispatch(t, ev);
        if (button == 0 && rangeDrag != null) { var r = rangeDrag; rangeDrag = null; Dispatch(r, new DomEvent { type = "change" }); }
        if (button == 0 && pressed != null) { var old = pressed; pressed = null; StateChange(old, null); }
        if (downTarget != null && downButton == button)
        {
            /* the click goes to the nearest element both ends of the press share */
            Element c = null;
            for (var p = t; p != null; p = p.parentNode) if (p.contains(downTarget)) { c = p; break; }
            if (c != null && button == 0)
            {
                bool dis = false; for (var p = c; p != null; p = p.parentNode) if (p.localName is "button" or "input" && p.disabled) { dis = true; break; }
                if (!dis) { var ce = Ev("click", mx, my, 0, null); ce.button = 0; ce.detail = 1; Dispatch(c, ce); }
            }
            else if (c != null && button == 2) { var ce = Ev("contextmenu", mx, my, 0, null); ce.button = 2; Dispatch(c, ce); }
            else if (c != null && button == 1) { var ce = Ev("auxclick", mx, my, 0, null); ce.button = 1; Dispatch(c, ce); }
        }
        downTarget = null; downButton = -1;
    }
    /* the wheel: listeners first, then the nearest element that can scroll that way */
    public static bool Wheel(double dx, double dy)
    {
        var t = HitTest(mx, my) ?? document.body;
        var ev = Ev("wheel", mx, my, 0, null); ev.deltaX = dx; ev.deltaY = dy; ev.target = t;
        Dispatch(t, ev);
        if (ev.defaultPrevented) return true;
        EnsureLayout();
        if (Shift && dx == 0) { dx = dy; dy = 0; }
        for (var p = t; p != null; p = p.parentNode)
        {
            var b = p.box;
            if (b == null || !b.cs.Scrolls) continue;
            double maxY = Math.Max(0, b.contentH - b.PadH), maxX = Math.Max(0, b.contentW - b.PadW);
            if (dy != 0 && b.cs.ovY is Ovf.Auto or Ovf.Scroll && maxY > 0.5)
            {
                double before = p.scrollY;
                p.scrollY = Math.Max(0, Math.Min(maxY, p.scrollY + dy));
                if (p.scrollY != before) { Dispatch(p, new DomEvent { type = "scroll", bubbles = false }); return true; }
            }
            if (dx != 0 && b.cs.ovX is Ovf.Auto or Ovf.Scroll && maxX > 0.5)
            {
                double before = p.scrollX;
                p.scrollX = Math.Max(0, Math.Min(maxX, p.scrollX + dx));
                if (p.scrollX != before) { Dispatch(p, new DomEvent { type = "scroll", bubbles = false }); return true; }
            }
        }
        return t != document.body && t?.id != "game";
    }
    public static void ScrollIntoView(Element e)
    {
        EnsureLayout();
        if (e.box == null) return;
        var r = e.box.ClientRect();
        for (var p = e.parentNode; p != null; p = p.parentNode)
        {
            var b = p.box;
            if (b == null || !b.cs.Scrolls) continue;
            var pr = b.ClientRect();
            double top = pr.y + b.bt, bot = top + b.PadH;
            if (r.y < top) p.scrollTop = p.scrollY - (top - r.y);
            else if (r.y + r.height > bot) p.scrollTop = p.scrollY + (r.y + r.height - bot);
            break;
        }
    }
    /* the window lost focus: nothing stays pressed */
    public static void Blur() { pressed = null; rangeDrag = null; downTarget = null; window?.Fire(new DomEvent { type = "blur" }); }

    /* ---- sliders ---- */
    static void RangeSet(Element r, double x)
    {
        if (r.box == null) return;
        var rect = r.getBoundingClientRect();
        var th = Paint2.PseudoStyle(r, "-webkit-slider-thumb");
        double tw = 16; if (th != null && th.TryGetValue("width", out var w)) tw = Cascade.ParseLen(w, 16, 16).R(0);
        double t = rect.width > tw ? (x - rect.x - tw / 2) / (rect.width - tw) : 0;
        t = Math.Clamp(t, 0, 1);
        double v = r.RangeMin + t * (r.RangeMax - r.RangeMin);
        string before = r.value;
        r.value = Js.Num(v);
        if (r.value != before) Dispatch(r, new DomEvent { type = "input" });
    }

    /* ---- the keyboard ---- */
    /* a key goes to the focused element (bubbling to the window); then the field's default editing */
    public static bool KeyDown(KeyEvt k, Action<KeyEvt> gameHandler)
    {
        lastKeyTime = Paint2.Time;
        var t = focused != null && focused.isConnected ? focused : document.body;
        var ev = new DomEvent { type = "keydown", key = k.key, code = k.code, shiftKey = k.shiftKey, ctrlKey = k.ctrlKey, metaKey = k.metaKey, repeat = k.repeat, target = t };
        /* listeners on the element path, then the window (where the game listens) */
        var path = new List<Element>(); for (var p = t; p != null; p = p.parentNode) path.Add(p);
        foreach (var el in path)
        {
            if (el.listeners != null && el.listeners.TryGetValue("keydown", out var l))
                foreach (var x in l.ToList()) { ev.currentTarget = el; try { x.fn(ev); } catch (Exception e) { Report(e); } if (ev.stoppedNow) break; }
            if (ev.stopped) break;
        }
        if (!ev.stopped)
        {
            window.Fire(ev);
            if (ev.defaultPrevented) k.preventDefault();
            if (!ev.stoppedNow) gameHandler?.Invoke(k);
        }
        if (ev.defaultPrevented || k.prevented) return true;
        if (TextFocused && focused == t) { EditKey(t, k); return true; }
        if (t.localName == "input" && t.type == "range") { RangeKey(t, k); return true; }
        /* Enter or Space on a focused button clicks it */
        if (t.localName == "button" && (k.code is "Enter" or "Space" or "NumpadEnter")) { t.click(); return true; }
        return false;
    }
    public static void KeyUp(KeyEvt k, Action<KeyEvt> gameHandler)
    {
        var t = focused != null && focused.isConnected ? focused : document.body;
        var ev = new DomEvent { type = "keyup", key = k.key, code = k.code, shiftKey = k.shiftKey, ctrlKey = k.ctrlKey, target = t };
        Dispatch(t, ev);
        gameHandler?.Invoke(k);
    }
    static void RangeKey(Element r, KeyEvt k)
    {
        double st = r.RangeStep > 0 ? r.RangeStep : (r.RangeMax - r.RangeMin) / 100, v = Js.ToNumber(r.value);
        if (k.code is "ArrowLeft" or "ArrowDown") v -= st; else if (k.code is "ArrowRight" or "ArrowUp") v += st; else return;
        string before = r.value; r.value = Js.Num(v);
        if (r.value != before) { Dispatch(r, new DomEvent { type = "input" }); Dispatch(r, new DomEvent { type = "change" }); }
    }
    /* a typed character */
    public static void TypeChar(int cp)
    {
        if (!TextFocused || cp < 32 || cp == 127) return;
        lastKeyTime = Paint2.Time;
        Insert(focused, char.ConvertFromUtf32(cp));
    }
    static void Insert(Element e, string s)
    {
        string v = e.value; int a = Math.Clamp(e.caret, 0, v.Length), z = a;
        if (e.selFrom >= 0 && e.selFrom != e.caret) { a = Math.Min(e.selFrom, e.caret); z = Math.Max(e.selFrom, e.caret); a = Math.Clamp(a, 0, v.Length); z = Math.Clamp(z, 0, v.Length); }
        var ml = Js.ToNumber(e.getAttribute("maxlength") ?? "");
        if (!double.IsNaN(ml) && ml >= 0) { int room = (int)ml - (v.Length - (z - a)); if (room <= 0) return; if (s.Length > room) s = s.Substring(0, room); }
        string nv = v.Substring(0, a) + s + v.Substring(z);
        e.value = nv; e.caret = a + s.Length; e.selFrom = -1;
        e.changedSinceFocus = true;
        Dispatch(e, new DomEvent { type = "input", data = s });
    }
    static void EditKey(Element e, KeyEvt k)
    {
        string v = e.value; int c = Math.Clamp(e.caret, 0, v.Length);
        bool sel = e.selFrom >= 0 && e.selFrom != e.caret;
        void Move(int to, bool extend) { if (extend) { if (e.selFrom < 0) e.selFrom = c; } else e.selFrom = -1; e.caret = Math.Clamp(to, 0, v.Length); Changed(e); }
        int WordLeft() { int i = c; while (i > 0 && v[i - 1] == ' ') i--; while (i > 0 && v[i - 1] != ' ') i--; return i; }
        int WordRight() { int i = c; while (i < v.Length && v[i] != ' ') i++; while (i < v.Length && v[i] == ' ') i++; return i; }
        switch (k.code)
        {
            case "Backspace":
                if (sel) { Insert(e, ""); return; }
                if (c > 0) { int from = k.ctrlKey ? WordLeft() : c - 1; e.value = v.Substring(0, from) + v.Substring(c); e.caret = from; e.changedSinceFocus = true; Dispatch(e, new DomEvent { type = "input" }); }
                return;
            case "Delete":
                if (sel) { Insert(e, ""); return; }
                if (c < v.Length) { int to = k.ctrlKey ? WordRight() : c + 1; e.value = v.Substring(0, c) + v.Substring(to); e.caret = c; e.changedSinceFocus = true; Dispatch(e, new DomEvent { type = "input" }); }
                return;
            case "ArrowLeft": Move(sel && !k.shiftKey ? Math.Min(e.selFrom, c) : k.ctrlKey ? WordLeft() : c - 1, k.shiftKey); return;
            case "ArrowRight": Move(sel && !k.shiftKey ? Math.Max(e.selFrom, c) : k.ctrlKey ? WordRight() : c + 1, k.shiftKey); return;
            case "Home": case "ArrowUp": Move(0, k.shiftKey); return;
            case "End": case "ArrowDown": Move(v.Length, k.shiftKey); return;
            case "Escape": e.blur(); return;
            case "Enter": case "NumpadEnter":
                if (e.changedSinceFocus) { e.changedSinceFocus = false; Dispatch(e, new DomEvent { type = "change" }); }
                return;
        }
        if (k.ctrlKey || k.metaKey)
        {
            switch (k.code)
            {
                case "KeyA": e.selFrom = 0; e.caret = v.Length; Changed(e); return;
                case "KeyC": if (sel) Clipboard(v.Substring(Math.Min(e.selFrom, c), Math.Abs(e.selFrom - c))); return;
                case "KeyX": if (sel) { Clipboard(v.Substring(Math.Min(e.selFrom, c), Math.Abs(e.selFrom - c))); Insert(e, ""); } return;
                case "KeyV": { string clip = ClipboardGet(); if (!string.IsNullOrEmpty(clip)) Insert(e, clip.Replace("\r", "").Replace("\n", " ")); return; }
            }
        }
    }
    public static Action<string> ClipboardSet = s => { try { Raylib.SetClipboardText(s); } catch { } };
    public static Func<string> ClipboardGetter = () => { try { return Raylib.GetClipboardText_(); } catch { return null; } };
    static void Clipboard(string s) => ClipboardSet(s);
    static string ClipboardGet() => ClipboardGetter();
    /* a click inside a text field puts the caret under the pointer */
    static void CaretAt(Element e, double x)
    {
        if (e.box == null) return;
        var r = e.getBoundingClientRect();
        var b = e.box; var f = b.cs.Font;
        double lx = x - r.x - b.bl - b.pl;
        string v = e.value; int best = v.Length;
        for (int i = 0; i <= v.Length; i++) { double w = Ctx.Measure(v.Substring(0, i), f) + b.cs.letter * i; if (w >= lx) { best = i; if (i > 0) { double pw = Ctx.Measure(v.Substring(0, i - 1), f) + b.cs.letter * (i - 1); if (lx - pw < w - lx) best = i - 1; } break; } }
        e.caret = best; e.selFrom = -1; Changed(e);
    }
    /* the CSS cursor under the pointer */
    public static string Cursor()
    {
        var t = PointerTarget;
        if (t == null) return "default";
        var c = t.cs?.cursor ?? "auto";
        if (c == "auto") return t.localName == "input" && t.type != "range" ? "text" : "default";
        return c;
    }
    public static bool Animating => Anim.Active;
}

/* MutationObserver, for the changes the game watches (childList, subtree,
   characterData): callbacks run once, after the script that made the change */
public sealed class MutationObserverInit { public bool childList, subtree, characterData, attributes; }
public sealed class MutationObserver
{
    static readonly List<MutationObserver> all = new();
    readonly Action cb; Element target; MutationObserverInit opts; bool pending;
    public MutationObserver(Action cb) { this.cb = cb; }
    public void observe(Element t, MutationObserverInit o) { target = t; opts = o; if (!all.Contains(this)) all.Add(this); }
    public void disconnect() { all.Remove(this); }
    internal static void Note(Element changed)
    {
        foreach (var m in all)
        {
            if (m.target == null || m.pending) continue;
            if (changed == m.target || (m.opts.subtree && m.target.contains(changed))) m.pending = true;
        }
    }
    public static void Flush()
    {
        foreach (var m in all.ToList()) if (m.pending) { m.pending = false; try { m.cb(); } catch (Exception e) { Dom.Report(e); } }
    }
}

/* CSS transitions (transform, opacity, background-color) and the one keyframe
   animation the stylesheet has (an outline blinking on a doomed item) */
internal static class Anim
{
    public static double Now;
    sealed class Tr { public double t0, dur, delay; public object from, to; public string ease; }
    static readonly Dictionary<Element, Dictionary<string, Tr>> live = new();
    public static bool Active => live.Count > 0 || anyKeyframes;
    static bool anyKeyframes;
    static Trans For(CStyle c, string prop)
    {
        if (c.transition == null) return null;
        Trans hit = null;
        foreach (var t in c.transition) if (t.prop == prop || t.prop == "all" || prop == "background-color" && t.prop == "background") hit = t;
        return hit != null && hit.dur > 0 ? hit : null;
    }
    public static void Restyled(Element e, CStyle old, CStyle nw)
    {
        if (nw.transition == null && !live.ContainsKey(e)) return;
        void Start(string prop, object from, object to)
        {
            var t = For(nw, prop);
            if (t == null) return;
            if (!live.TryGetValue(e, out var d)) live[e] = d = new();
            /* start from wherever a running one has got to */
            if (d.TryGetValue(prop, out var cur)) from = Value(cur, prop, e.box) ?? from;
            d[prop] = new Tr { t0 = Now, dur = t.dur, delay = t.delay, from = from, to = to, ease = t.ease };
        }
        if (!SameT(old.transform, nw.transform)) Start("transform", old.transform, nw.transform);
        if (old.opacity != nw.opacity) Start("opacity", old.opacity, nw.opacity);
        if (old.bg != nw.bg) Start("background-color", old.bg, nw.bg);
    }
    static bool SameT(List<TOp> a, List<TOp> b)
    {
        if (a == null || b == null) return a == b;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i].f != b[i].f || a[i].v1 != b[i].v1 || a[i].v2 != b[i].v2 || a[i].a.px != b[i].a.px || a[i].a.pct != b[i].a.pct || a[i].b.px != b[i].b.px || a[i].b.pct != b[i].b.pct) return false;
        return true;
    }
    static double Ease(string e, double t) => e switch
    {
        "linear" => t, "ease-in" => t * t, "ease-out" => 1 - (1 - t) * (1 - t), "ease-in-out" => t < 0.5 ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t),
        _ => t < 0.5 ? 2 * t * t * (1.5 - t) : 1 - Math.Pow(1 - t, 2) * (1 + (t - 0.5)),
    };
    static double Prog(Tr t) => Math.Clamp((Now - t.t0 - t.delay) / Math.Max(1e-6, t.dur), 0, 1);
    static object Value(Tr t, string prop, Box b)
    {
        double k = Ease(t.ease, Prog(t));
        switch (prop)
        {
            case "opacity": return (double)t.from + ((double)t.to - (double)t.from) * k;
            case "background-color": { var a = (Col)t.from; var z = (Col)t.to; return new Col(a.r + (z.r - a.r) * k, a.g + (z.g - a.g) * k, a.b + (z.b - a.b) * k, a.a + (z.a - a.a) * k); }
            default: return null;
        }
    }
    static Tr Get(Box b, string prop)
    {
        if (b?.el == null || b.isPseudo || !live.TryGetValue(b.el, out var d) || !d.TryGetValue(prop, out var t)) return null;
        if (Prog(t) >= 1) { d.Remove(prop); if (d.Count == 0) live.Remove(b.el); return null; }
        return t;
    }
    public static Col? Bg(Box b) { var t = Get(b, "background-color"); return t == null ? (Col?)null : (Col)Value(t, "background-color", b); }
    public static double Opacity(Box b) { var t = Get(b, "opacity"); return t == null ? b.cs.opacity : (double)Value(t, "opacity", b); }
    /* the transform matrix mid-transition: the two ends' matrices, blended */
    public static Mat? Transform(Box b, Func<List<TOp>, Mat> eval)
    {
        var t = Get(b, "transform");
        if (t == null) return null;
        double k = Ease(t.ease, Prog(t));
        var a = eval(t.from as List<TOp>); var z = eval(t.to as List<TOp>);
        return new Mat { a = a.a + (z.a - a.a) * k, b = a.b + (z.b - a.b) * k, c = a.c + (z.c - a.c) * k, d = a.d + (z.d - a.d) * k, e = a.e + (z.e - a.e) * k, f = a.f + (z.f - a.f) * k };
    }
    public static bool HasTransform(Box b) => b?.el != null && !b.isPseudo && live.TryGetValue(b.el, out var d) && d.ContainsKey("transform");
    /* @keyframes on outline-color (the only property the stylesheet animates) */
    public static Col? OutlineColor(Box b)
    {
        var c = b.cs;
        if (c.anim == null || !Sheet.keyframes.TryGetValue(c.anim, out var kf) || c.animDur <= 0) return null;
        anyKeyframes = true;
        double p = (Now - c.animDelay) / c.animDur;
        if (!c.animInfinite && p >= c.animCount) return null;
        double f = p - Math.Floor(p);
        if (c.animAlternate && ((int)Math.Floor(p)) % 2 == 1) f = 1 - f;
        Col from = c.outlineCol, to = c.outlineCol;
        foreach (var (at, decls) in kf.frames) foreach (var (k, v, _) in decls) if (k == "outline-color") { if (at <= 0) from = Cascade.ColorOf(v, c.color); else to = Cascade.ColorOf(v, c.color); }
        return new Col(from.r + (to.r - from.r) * f, from.g + (to.g - from.g) * f, from.b + (to.b - from.b) * f, from.a + (to.a - from.a) * f);
    }
}
