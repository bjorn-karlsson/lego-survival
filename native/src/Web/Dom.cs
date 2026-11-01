// The native UI engine, part 1: the document. The game's screens are HTML and
// CSS in the browser build, and they are driven from script through the DOM
// (getElementById, innerHTML, classList, style, events). This is enough of that
// DOM, with the same names, for the screen code to port line for line; Css.cs
// styles it, Layout.cs lays it out and Paint.cs draws it through the Ctx shim.
using System.Text;

namespace Brickblade;

public abstract class Node
{
    public Element parentNode;
    public Element parentElement => parentNode;
    public abstract string textContent { get; set; }
    public Document ownerDocument => Dom.document;
    int Index() => parentNode == null ? -1 : parentNode.childNodes.IndexOf(this);
    public Node nextSibling { get { int i = Index(); return i < 0 || i + 1 >= parentNode.childNodes.Count ? null : parentNode.childNodes[i + 1]; } }
    public Node previousSibling { get { int i = Index(); return i <= 0 ? null : parentNode.childNodes[i - 1]; } }
    public Element nextElementSibling
    {
        get
        {
            if (parentNode == null) return null;
            var l = parentNode.childNodes; int i = l.IndexOf(this);
            for (int k = i + 1; k < l.Count; k++) if (l[k] is Element e) return e;
            return null;
        }
    }
    public Element previousElementSibling
    {
        get
        {
            if (parentNode == null) return null;
            var l = parentNode.childNodes; int i = l.IndexOf(this);
            for (int k = i - 1; k >= 0; k--) if (l[k] is Element e) return e;
            return null;
        }
    }
    public void remove() => parentNode?.removeChild(this);
    /* attached to the document (not a detached fragment) */
    public bool isConnected { get { Node n = this; while (n.parentNode != null) n = n.parentNode; return n == Dom.document.documentElement; } }
}

public sealed class TextNode : Node
{
    string _data;
    public TextNode(string d) { _data = d ?? ""; }
    public string data { get => _data; set { value ??= ""; if (value != _data) { _data = value; Dom.Changed(this); } } }
    public string nodeValue { get => data; set => data = value; }
    public override string textContent { get => _data; set => data = value; }
}

/* el.classList */
public sealed class ClassList
{
    readonly Element el;
    internal ClassList(Element e) { el = e; }
    public bool contains(string c) => el.Classes.Contains(c);
    public void add(params string[] cs)
    {
        var l = el.Classes; bool ch = false;
        foreach (var c in cs) if (!string.IsNullOrEmpty(c) && !l.Contains(c)) { l.Add(c); ch = true; }
        if (ch || el.getAttribute("class") == null) el.SetClasses(l);
    }
    public void remove(params string[] cs)
    {
        var l = el.Classes; bool ch = false;
        foreach (var c in cs) ch |= l.Remove(c);
        if (ch) el.SetClasses(l);
    }
    public bool toggle(string c, bool? force = null)
    {
        bool has = contains(c);
        bool want = force ?? !has;
        if (want && !has) add(c); else if (!want && has) remove(c);
        return want;
    }
    public bool toggle(string c, bool force) => toggle(c, (bool?)force);
    public void replace(string a, string b) { if (contains(a)) { remove(a); add(b); } }
    public int length => el.Classes.Count;
    public string this[int i] => i < el.Classes.Count ? el.Classes[i] : null;
    public override string ToString() => string.Join(" ", el.Classes);
}

/* el.dataset: data-foo-bar <-> dataset["fooBar"] */
public sealed class Dataset
{
    readonly Element el;
    internal Dataset(Element e) { el = e; }
    static string Attr(string camel)
    {
        var sb = new StringBuilder("data-");
        foreach (var ch in camel) { if (char.IsUpper(ch)) { sb.Append('-'); sb.Append(char.ToLowerInvariant(ch)); } else sb.Append(ch); }
        return sb.ToString();
    }
    public string this[string k] { get => el.getAttribute(Attr(k)); set { if (value == null) el.removeAttribute(Attr(k)); else el.setAttribute(Attr(k), value); } }
    public bool has(string k) => el.hasAttribute(Attr(k));
    public void delete(string k) => el.removeAttribute(Attr(k));
}

/* el.style: the inline declarations, in the order they were set */
public sealed class StyleDecl
{
    readonly Element el;
    internal readonly List<(string k, string v, bool imp)> decls = new();
    internal StyleDecl(Element e) { el = e; }
    public string getPropertyValue(string k) { foreach (var d in decls) if (d.k == k) return d.v; return ""; }
    public void setProperty(string k, string v, string prio = null)
    {
        k = k.Trim().ToLowerInvariant().StartsWith("--") ? k.Trim() : k.Trim().ToLowerInvariant();
        v = (v ?? "").Trim();
        bool imp = prio == "important";
        if (v.EndsWith("!important")) { imp = true; v = v.Substring(0, v.Length - 10).Trim(); }
        int i = decls.FindIndex(d => d.k == k);
        if (v.Length == 0) { if (i >= 0) { decls.RemoveAt(i); Sync(); } return; }
        if (i >= 0) { if (decls[i].v == v && decls[i].imp == imp) return; decls[i] = (k, v, imp); }
        else decls.Add((k, v, imp));
        Sync();
    }
    public string removeProperty(string k) { var v = getPropertyValue(k); setProperty(k, ""); return v; }
    void Sync() { el.StyleAttrFromDecl(); }
    internal void ParseFrom(string css)
    {
        decls.Clear();
        if (string.IsNullOrEmpty(css)) return;
        foreach (var (k, v, imp) in CssParser.ParseDecls(css)) { int i = decls.FindIndex(d => d.k == k); if (i >= 0) decls.RemoveAt(i); decls.Add((k, v, imp)); }
    }
    public string cssText
    {
        get => string.Join(" ", decls.Select(d => d.k + ": " + d.v + (d.imp ? " !important" : "") + ";"));
        set => el.setAttribute("style", value ?? "");
    }
    public int length => decls.Count;
    /* the properties the game's script writes, by their camelCase names */
    public string display { get => getPropertyValue("display"); set => setProperty("display", value); }
    public string position { get => getPropertyValue("position"); set => setProperty("position", value); }
    public string left { get => getPropertyValue("left"); set => setProperty("left", value); }
    public string top { get => getPropertyValue("top"); set => setProperty("top", value); }
    public string right { get => getPropertyValue("right"); set => setProperty("right", value); }
    public string bottom { get => getPropertyValue("bottom"); set => setProperty("bottom", value); }
    public string width { get => getPropertyValue("width"); set => setProperty("width", value); }
    public string height { get => getPropertyValue("height"); set => setProperty("height", value); }
    public string maxHeight { get => getPropertyValue("max-height"); set => setProperty("max-height", value); }
    public string maxWidth { get => getPropertyValue("max-width"); set => setProperty("max-width", value); }
    public string minWidth { get => getPropertyValue("min-width"); set => setProperty("min-width", value); }
    public string cursor { get => getPropertyValue("cursor"); set => setProperty("cursor", value); }
    public string fontSize { get => getPropertyValue("font-size"); set => setProperty("font-size", value); }
    public string color { get => getPropertyValue("color"); set => setProperty("color", value); }
    public string background { get => getPropertyValue("background"); set => setProperty("background", value); }
    public string backgroundColor { get => getPropertyValue("background-color"); set => setProperty("background-color", value); }
    public string boxShadow { get => getPropertyValue("box-shadow"); set => setProperty("box-shadow", value); }
    public string overflowY { get => getPropertyValue("overflow-y"); set => setProperty("overflow-y", value); }
    public string borderColor { get => getPropertyValue("border-color"); set => setProperty("border-color", value); }
    public string zoom { get => getPropertyValue("zoom"); set => setProperty("zoom", value); }
    public string opacity { get => getPropertyValue("opacity"); set => setProperty("opacity", value); }
    public string transform { get => getPropertyValue("transform"); set => setProperty("transform", value); }
    public string visibility { get => getPropertyValue("visibility"); set => setProperty("visibility", value); }
    public string pointerEvents { get => getPropertyValue("pointer-events"); set => setProperty("pointer-events", value); }
    public string zIndex { get => getPropertyValue("z-index"); set => setProperty("z-index", value); }
}

/* createSVGPoint / getScreenCTM: a point and a 2D matrix */
public sealed class DOMPoint
{
    public double x, y;
    public DOMPoint matrixTransform(DOMMatrix m) => new DOMPoint { x = m.a * x + m.c * y + m.e, y = m.b * x + m.d * y + m.f };
}
public sealed class DOMMatrix
{
    public double a = 1, b, c, d = 1, e, f;
    public DOMMatrix inverse()
    {
        double det = a * d - b * c;
        if (Math.Abs(det) < 1e-12) return new DOMMatrix { a = double.NaN, d = double.NaN };
        return new DOMMatrix { a = d / det, b = -b / det, c = -c / det, d = a / det, e = (c * f - d * e) / det, f = (b * e - a * f) / det };
    }
}

public sealed class DOMRect
{
    public double x, y, width, height;
    public double left => x; public double top => y; public double right => x + width; public double bottom => y + height;
}

/* what an event listener is handed: one shape for mouse, pointer, wheel, key and input events */
public sealed class DomEvent
{
    public string type;
    public Element target, currentTarget, relatedTarget;
    public double clientX, clientY, movementX, movementY, deltaX, deltaY, deltaMode, offsetX, offsetY;
    public double pageX => clientX; public double pageY => clientY;
    public int button, buttons, detail;
    public bool shiftKey, ctrlKey, altKey, metaKey, repeat;
    public string key = "", code = "";
    public bool bubbles = true, defaultPrevented;
    internal bool stopped, stoppedNow;
    public object data;
    public void preventDefault() => defaultPrevented = true;
    public void stopPropagation() => stopped = true;
    public void stopImmediatePropagation() { stopped = true; stoppedNow = true; }
}

public sealed class Listener { public Action<DomEvent> fn; public bool capture, once; }

public class Element : Node
{
    public readonly string localName;
    public readonly bool isSvg;
    public string tagName => isSvg ? localName : localName.ToUpperInvariant();
    public string nodeName => tagName;
    public readonly List<Node> childNodes = new();
    internal readonly List<string> attrOrder = new();
    internal readonly Dictionary<string, string> attrs = new();
    List<string> classes;
    public readonly ClassList classList;
    public readonly Dataset dataset;
    public readonly StyleDecl style;
    internal Dictionary<string, List<Listener>> listeners;
    /* the layout's view of this element (Layout.cs) and its computed style (Css.cs) */
    internal Box box;
    internal CStyle cs, csBefore, csAfter;
    internal int styleVer = -1;
    internal bool dirty = true, kidsDirty, sibsDirty, subDirty, changedSinceFocus;
    /* scroll position of a scroll container */
    internal double scrollX, scrollY;
    /* form state */
    string _value; public bool @checked;
    internal int caret, selFrom = -1;
    /* a <canvas> element's backing store */
    public OffCanvas canvas;

    public Element(string name, bool svg = false)
    {
        isSvg = svg; localName = svg ? name : name.ToLowerInvariant();
        classList = new ClassList(this); dataset = new Dataset(this); style = new StyleDecl(this);
        if (localName == "canvas") canvas = new OffCanvas { width = 300, height = 150 };
    }

    /* ---- attributes ---- */
    public string getAttribute(string k) { if (!isSvg) k = k.ToLowerInvariant(); if (k == "style" && style.decls.Count > 0) return attrs.GetValueOrDefault(k) ?? style.cssText; return attrs.TryGetValue(k, out var v) ? v : null; }
    public bool hasAttribute(string k) => attrs.ContainsKey(isSvg ? k : k.ToLowerInvariant());
    public void setAttribute(string k, string v)
    {
        if (!isSvg) k = k.ToLowerInvariant();
        v ??= "null";
        if (attrs.TryGetValue(k, out var old) && old == v) return;
        if (!attrs.ContainsKey(k)) attrOrder.Add(k);
        attrs[k] = v;
        AttrChanged(k, old, v);
    }
    public void setAttribute(string k, double v) => setAttribute(k, Js.Num(v));
    public void removeAttribute(string k)
    {
        if (!isSvg) k = k.ToLowerInvariant();
        if (!attrs.TryGetValue(k, out var old)) return;
        attrs.Remove(k); attrOrder.Remove(k);
        AttrChanged(k, old, null);
    }
    public bool toggleAttribute(string k, bool? force = null)
    {
        bool want = force ?? !hasAttribute(k);
        if (want) { if (!hasAttribute(k)) setAttribute(k, ""); } else removeAttribute(k);
        return want;
    }
    void AttrChanged(string k, string old, string v)
    {
        if (k == "class") classes = null;
        else if (k == "id") Dom.IdsDirty();
        else if (k == "style") style.ParseFrom(v);
        else if (k == "value" && localName == "input" && _value == null) { }
        else if (canvas != null && (k == "width" || k == "height"))
        {
            int n = (int)Math.Max(1, Js.ToNumber(v ?? "0"));
            if (k == "width") canvas.width = n; else canvas.height = n;
        }
        Dom.Changed(this);
    }
    internal void StyleAttrFromDecl()
    {
        string css = style.cssText;
        if (!attrs.ContainsKey("style")) attrOrder.Add("style");
        attrs["style"] = css;
        if (css.Length == 0) { attrs.Remove("style"); attrOrder.Remove("style"); }
        Dom.Changed(this);
    }
    public IEnumerable<string> getAttributeNames() => attrOrder;

    internal List<string> Classes
    {
        get
        {
            if (classes != null) return classes;
            classes = new List<string>();
            var c = attrs.GetValueOrDefault("class");
            if (!string.IsNullOrEmpty(c)) foreach (var s in c.Split(new[] { ' ', '\t', '\n', '\r', '\f' }, StringSplitOptions.RemoveEmptyEntries)) if (!classes.Contains(s)) classes.Add(s);
            return classes;
        }
    }
    internal void SetClasses(List<string> l) => setAttribute("class", string.Join(" ", l));
    public string className { get => attrs.GetValueOrDefault("class") ?? ""; set => setAttribute("class", value ?? ""); }
    public string id { get => attrs.GetValueOrDefault("id") ?? ""; set => setAttribute("id", value ?? ""); }
    public string title { get => getAttribute("title") ?? ""; set => setAttribute("title", value ?? ""); }
    public string type { get => (getAttribute("type") ?? (localName == "input" ? "text" : localName == "button" ? "submit" : "")).ToLowerInvariant(); set => setAttribute("type", value); }
    public string placeholder { get => getAttribute("placeholder") ?? ""; set => setAttribute("placeholder", value); }
    public bool disabled { get => hasAttribute("disabled"); set => toggleAttribute("disabled", value); }
    public bool hidden { get => hasAttribute("hidden"); set => toggleAttribute("hidden", value); }
    public int width { get => canvas != null ? canvas.width : (int)Js.ToNumber(getAttribute("width") ?? "0"); set { if (canvas != null) canvas.width = value; setAttribute("width", value); } }
    public int height { get => canvas != null ? canvas.height : (int)Js.ToNumber(getAttribute("height") ?? "0"); set { if (canvas != null) canvas.height = value; setAttribute("height", value); } }
    public Ctx getContext(string kind = "2d") => canvas?.getContext(kind);
    /* an input's value: the attribute until the field is typed into or set */
    public string value
    {
        get
        {
            if (_value != null) return _value;
            var a = getAttribute("value");
            if (localName == "input" && type == "range")
            {
                double mn = RangeMin, mx = RangeMax, v = a != null ? Js.ToNumber(a) : (mn + mx) / 2;
                return Js.Num(RangeClamp(double.IsNaN(v) ? (mn + mx) / 2 : v));
            }
            return a ?? "";
        }
        set
        {
            string nv = value ?? "";
            if (localName == "input" && type == "range") { double d = Js.ToNumber(nv); nv = Js.Num(RangeClamp(double.IsNaN(d) ? (RangeMin + RangeMax) / 2 : d)); }
            if (nv == _value) return;
            _value = nv; caret = Math.Min(caret, nv.Length); if (Dom.focused == this) caret = nv.Length; selFrom = -1;
            Dom.Changed(this);
        }
    }
    internal double RangeMin { get { var d = Js.ToNumber(getAttribute("min") ?? "0"); return double.IsNaN(d) ? 0 : d; } }
    internal double RangeMax { get { var d = Js.ToNumber(getAttribute("max") ?? "100"); return double.IsNaN(d) ? 100 : d; } }
    internal double RangeStep { get { var s = getAttribute("step"); if (s == "any") return 0; var d = Js.ToNumber(s ?? "1"); return double.IsNaN(d) || d <= 0 ? 1 : d; } }
    internal double RangeClamp(double v)
    {
        double mn = RangeMin, mx = Math.Max(RangeMin, RangeMax), st = RangeStep;
        v = Math.Min(mx, Math.Max(mn, v));
        if (st > 0) { v = mn + Math.Round((v - mn) / st) * st; if (v > mx) v -= st; }
        return Math.Round(v * 1e9) / 1e9;
    }
    public double valueAsNumber { get => Js.ToNumber(value); set => this.value = Js.Num(value); }

    /* ---- the tree ---- */
    public List<Element> children { get { var l = new List<Element>(); foreach (var n in childNodes) if (n is Element e) l.Add(e); return l; } }
    public int childElementCount { get { int c = 0; foreach (var n in childNodes) if (n is Element) c++; return c; } }
    public Node firstChild => childNodes.Count > 0 ? childNodes[0] : null;
    public Node lastChild => childNodes.Count > 0 ? childNodes[^1] : null;
    public Element firstElementChild { get { foreach (var n in childNodes) if (n is Element e) return e; return null; } }
    public Element lastElementChild { get { for (int i = childNodes.Count - 1; i >= 0; i--) if (childNodes[i] is Element e) return e; return null; } }
    public Node appendChild(Node n) => insertBefore(n, null);
    public void append(params object[] items) { foreach (var x in items) appendChild(x as Node ?? new TextNode(G.S(x))); }
    public void prepend(params object[] items) { var f = firstChild; foreach (var x in items) insertBefore(x as Node ?? new TextNode(G.S(x)), f); }
    public Node insertBefore(Node n, Node before)
    {
        if (n == null) return null;
        n.parentNode?.Detach(n);
        int i = before == null ? childNodes.Count : childNodes.IndexOf(before);
        if (i < 0) i = childNodes.Count;
        childNodes.Insert(i, n); n.parentNode = this;
        Dom.TreeChanged(this);
        return n;
    }
    public Node removeChild(Node n) { if (n?.parentNode != this) return n; Detach(n); Dom.TreeChanged(this); return n; }
    internal void Detach(Node n)
    {
        childNodes.Remove(n); n.parentNode = null;
        if (n is Element e) Dom.Detached(e);
    }
    public Node replaceChild(Node nw, Node old) { insertBefore(nw, old); removeChild(old); return old; }
    public void replaceWith(Node n) { var p = parentNode; if (p == null) return; p.insertBefore(n, this); p.removeChild(this); }
    public void replaceChildren(params Node[] ns)
    {
        foreach (var c in childNodes.ToList()) Detach(c);
        foreach (var n in ns) { n.parentNode?.Detach(n); childNodes.Add(n); n.parentNode = this; }
        Dom.TreeChanged(this);
    }
    public bool contains(Node n) { for (var p = n; p != null; p = p.parentNode) if (p == this) return true; return false; }
    public Element cloneNode(bool deep = false)
    {
        var c = new Element(localName, isSvg);
        foreach (var k in attrOrder) { c.attrOrder.Add(k); c.attrs[k] = attrs[k]; }
        c.style.ParseFrom(c.attrs.GetValueOrDefault("style"));
        if (deep) foreach (var n in childNodes) c.childNodes.Add(n is Element e ? e.cloneNode(true).Adopt(c) : new TextNode(((TextNode)n).data) { parentNode = c });
        return c;
    }
    Element Adopt(Element p) { parentNode = p; return this; }

    public override string textContent
    {
        get { var sb = new StringBuilder(); Text(this, sb); return sb.ToString(); }
        set
        {
            foreach (var c in childNodes.ToList()) Detach(c);
            if (!string.IsNullOrEmpty(value)) { var t = new TextNode(value) { parentNode = this }; childNodes.Add(t); }
            Dom.TreeChanged(this);
        }
    }
    static void Text(Element e, StringBuilder sb) { foreach (var n in e.childNodes) if (n is TextNode t) sb.Append(t.data); else Text((Element)n, sb); }
    public string innerText { get => textContent; set => textContent = value; }
    public string innerHTML
    {
        get { var sb = new StringBuilder(); foreach (var n in childNodes) Html.Serialize(n, sb); return sb.ToString(); }
        set
        {
            foreach (var c in childNodes.ToList()) Detach(c);
            foreach (var n in Html.Parse(value ?? "", isSvg && localName != "foreignObject")) { childNodes.Add(n); n.parentNode = this; }
            Dom.TreeChanged(this);
        }
    }
    public string outerHTML { get { var sb = new StringBuilder(); Html.Serialize(this, sb); return sb.ToString(); } }
    public void insertAdjacentHTML(string where, string html)
    {
        var ns = Html.Parse(html ?? "", isSvg);
        switch (where.ToLowerInvariant())
        {
            case "beforeend": foreach (var n in ns) appendChild(n); break;
            case "afterbegin": { var f = firstChild; foreach (var n in ns) insertBefore(n, f); break; }
            case "beforebegin": if (parentNode != null) foreach (var n in ns) parentNode.insertBefore(n, this); break;
            case "afterend": if (parentNode != null) { var nx = nextSibling; foreach (var n in ns) parentNode.insertBefore(n, nx); } break;
        }
    }

    /* ---- selectors ---- */
    public Element querySelector(string sel)
    {
        var s = CssParser.SelectorList(sel);
        Element found = null;
        Walk(this, e => { if (found == null && s.Any(x => x.Matches(e, this))) { found = e; return false; } return true; });
        return found;
    }
    public List<Element> querySelectorAll(string sel)
    {
        var s = CssParser.SelectorList(sel); var l = new List<Element>();
        Walk(this, e => { if (s.Any(x => x.Matches(e, this))) l.Add(e); return true; });
        return l;
    }
    public List<Element> getElementsByTagName(string t) => querySelectorAll(t);
    public List<Element> getElementsByClassName(string c) => querySelectorAll("." + c.Trim().Replace(" ", "."));
    public bool matches(string sel) => CssParser.SelectorList(sel).Any(x => x.Matches(this, null));
    public Element closest(string sel)
    {
        var s = CssParser.SelectorList(sel);
        for (var e = this; e != null; e = e.parentNode) if (s.Any(x => x.Matches(e, null))) return e;
        return null;
    }
    /* descendants in document order; return false from fn to stop */
    internal static bool Walk(Element root, Func<Element, bool> fn)
    {
        foreach (var n in root.childNodes)
            if (n is Element e) { if (!fn(e)) return false; if (!Walk(e, fn)) return false; }
        return true;
    }

    /* ---- events ---- */
    public void addEventListener(string type, Action<DomEvent> fn, object opts = null)
    {
        listeners ??= new();
        if (!listeners.TryGetValue(type, out var l)) listeners[type] = l = new();
        bool cap = opts is bool b ? b : false;
        if (l.Any(x => x.fn == fn && x.capture == cap)) return;
        l.Add(new Listener { fn = fn, capture = cap });
    }
    public void addEventListener(string type, Action fn, object opts = null) => addEventListener(type, _ => fn(), opts);
    public void removeEventListener(string type, Action<DomEvent> fn)
    {
        if (listeners != null && listeners.TryGetValue(type, out var l)) l.RemoveAll(x => x.fn == fn);
    }
    Action<DomEvent> _onclick, _oninput, _onchange;
    public Action<DomEvent> onclick { get => _onclick; set => _onclick = value; }
    public Action<DomEvent> oninput { get => _oninput; set => _oninput = value; }
    public Action<DomEvent> onchange { get => _onchange; set => _onchange = value; }
    internal Action<DomEvent> Prop(string type) => type == "click" ? _onclick : type == "input" ? _oninput : type == "change" ? _onchange : null;
    public bool dispatchEvent(DomEvent ev) { ev.target ??= this; Dom.Dispatch(this, ev); return !ev.defaultPrevented; }
    public void click() { if (disabled && (localName == "button" || localName == "input")) return; Dom.Dispatch(this, new DomEvent { type = "click", target = this }); }
    public void focus() => Dom.Focus(this);
    public void blur() { if (Dom.focused == this) Dom.Focus(null); }
    public void select() { if (Dom.focused != this) Dom.Focus(this); selFrom = 0; caret = value.Length; }
    public void setSelectionRange(int a, int b) { selFrom = a; caret = b; }

    /* ---- geometry (forces a layout, the way reading it does in a browser) ---- */
    public DOMRect getBoundingClientRect() { Dom.EnsureLayout(); return box != null ? box.ClientRect() : new DOMRect(); }
    public double offsetWidth { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.w * box.zoomOut) : 0; } }
    public double offsetHeight { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.h * box.zoomOut) : 0; } }
    public double offsetLeft { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.x) : 0; } }
    public double offsetTop { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.y) : 0; } }
    public double clientWidth { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.PadW) : 0; } }
    public double clientHeight { get { Dom.EnsureLayout(); return box != null ? Math.Round(box.PadH) : 0; } }
    public double scrollHeight { get { Dom.EnsureLayout(); return box != null ? Math.Round(Math.Max(box.PadH, box.contentH)) : 0; } }
    public double scrollWidth { get { Dom.EnsureLayout(); return box != null ? Math.Round(Math.Max(box.PadW, box.contentW)) : 0; } }
    public double scrollTop
    {
        get => scrollY;
        set { Dom.EnsureLayout(); double mx = box != null ? Math.Max(0, box.contentH - box.PadH) : 0; double v = Math.Max(0, Math.Min(mx, double.IsNaN(value) ? 0 : value)); if (v != scrollY) { scrollY = v; Dom.Scrolled(); } }
    }
    public double scrollLeft
    {
        get => scrollX;
        set { Dom.EnsureLayout(); double mx = box != null ? Math.Max(0, box.contentW - box.PadW) : 0; double v = Math.Max(0, Math.Min(mx, double.IsNaN(value) ? 0 : value)); if (v != scrollX) { scrollX = v; Dom.Scrolled(); } }
    }
    public void scrollTo(double x, double y) { scrollLeft = x; scrollTop = y; }
    public DOMPoint createSVGPoint() => new DOMPoint();
    /* user space -> client px for an <svg> (its viewBox, letterboxed into its content box) */
    public DOMMatrix getScreenCTM()
    {
        Dom.EnsureLayout();
        if (box == null) return null;
        var r = box.ClientRect();
        double k = box.w > 0 ? r.width / box.w : 1;
        double cx = r.x + (box.bl + box.pl) * k, cy = r.y + (box.bt + box.pt) * k;
        double cw = (box.w - box.bl - box.br - box.pl - box.pr) * k, ch = (box.h - box.bt - box.bb - box.pt - box.pb) * k;
        var m = Mat.T(cx, cy).Mul(Svg.ViewMat(this, cw, ch));
        return new DOMMatrix { a = m.a, b = m.b, c = m.c, d = m.d, e = m.e, f = m.f };
    }
    public void scrollIntoView(object opts = null) => Dom.ScrollIntoView(this);
    public override string ToString() => "<" + localName + (id.Length > 0 ? "#" + id : "") + (className.Length > 0 ? "." + className.Replace(' ', '.') : "") + ">";
}

public sealed class Document
{
    public readonly Element documentElement, head, body;
    internal Document()
    {
        documentElement = new Element("html"); head = new Element("head"); body = new Element("body");
        documentElement.childNodes.Add(head); head.parentNode = documentElement;
        documentElement.childNodes.Add(body); body.parentNode = documentElement;
    }
    public Element getElementById(string id) => Dom.ById(id);
    public Element querySelector(string s) => documentElement.querySelector(s);
    public List<Element> querySelectorAll(string s) => documentElement.querySelectorAll(s);
    public Element createElement(string t) => new Element(t);
    public Element createElementNS(string ns, string t) => new Element(t, ns != null && ns.Contains("svg"));
    public TextNode createTextNode(string s) => new TextNode(s);
    public Element activeElement => Dom.focused ?? body;
    public Element elementFromPoint(double x, double y) => Dom.HitTest(x, y);
    public bool hasFocus() => App.Focused;
    public void addEventListener(string type, Action<DomEvent> fn, object opts = null) => documentElement.addEventListener(type, fn, opts);
}

/* `window` as the screens use it: the viewport size and window-level listeners */
public sealed class Win
{
    public double innerWidth => Dom.ViewW;
    public double innerHeight => Dom.ViewH;
    public double devicePixelRatio => Dom.Scale;
    /* window.focus(): keys go back to the page, so whatever element had focus loses it */
    public void focus() => Dom.Focus(null);
    internal readonly Dictionary<string, List<Listener>> listeners = new();
    public void addEventListener(string type, Action<DomEvent> fn, object opts = null)
    {
        if (!listeners.TryGetValue(type, out var l)) listeners[type] = l = new();
        l.Add(new Listener { fn = fn });
    }
    public void addEventListener(string type, Action fn, object opts = null) => addEventListener(type, _ => fn(), opts);
    public void removeEventListener(string type, Action<DomEvent> fn) { if (listeners.TryGetValue(type, out var l)) l.RemoveAll(x => x.fn == fn); }
    internal void Fire(DomEvent ev)
    {
        if (!listeners.TryGetValue(ev.type, out var l)) return;
        foreach (var x in l.ToList()) { try { x.fn(ev); } catch (Exception e) { Dom.Report(e); } if (ev.stoppedNow) break; }
    }
    public CStyleView getComputedStyle(Element e) => new CStyleView(e);
}
