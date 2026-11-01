// The native UI engine, part 2: HTML in and out. innerHTML is how the screens
// build themselves, so this is a forgiving parser of the HTML the game writes
// (and of the inline SVG its icons are) plus the serializer innerHTML reads back.
using System.Text;

namespace Brickblade;

public static class Html
{
    static readonly HashSet<string> Void = new() { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr" };
    static readonly HashSet<string> RawText = new() { "style", "script", "textarea", "title" };
    static readonly Dictionary<string, string> Ent = new()
    {
        ["amp"] = "&", ["lt"] = "<", ["gt"] = ">", ["quot"] = "\"", ["apos"] = "'", ["nbsp"] = " ",
        ["middot"] = "·", ["mdash"] = "—", ["ndash"] = "–", ["minus"] = "−", ["times"] = "×", ["hellip"] = "…",
        ["rarr"] = "→", ["larr"] = "←", ["uarr"] = "↑", ["darr"] = "↓", ["bull"] = "•", ["deg"] = "°", ["copy"] = "©",
        ["hearts"] = "♥", ["divide"] = "÷", ["plusmn"] = "±", ["laquo"] = "«", ["raquo"] = "»", ["lsquo"] = "‘", ["rsquo"] = "’",
        ["ldquo"] = "“", ["rdquo"] = "”", ["thinsp"] = " ", ["ensp"] = " ", ["emsp"] = " ", ["star"] = "☆", ["check"] = "✓",
        ["frac12"] = "½", ["sup2"] = "²", ["infin"] = "∞", ["le"] = "≤", ["ge"] = "≥", ["ne"] = "≠", ["asymp"] = "≈",
    };
    /* SVG keeps the case of its element names; HTML parsing lowercases them, so the camel ones are put back */
    static readonly Dictionary<string, string> SvgCase = new[] { "viewBox", "preserveAspectRatio", "linearGradient", "radialGradient", "clipPath", "foreignObject", "textPath",
        "gradientUnits", "gradientTransform", "stdDeviation", "feGaussianBlur", "feDropShadow", "patternUnits" }.ToDictionary(s => s.ToLowerInvariant());

    public static string Decode(string s)
    {
        if (s.IndexOf('&') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '&') { sb.Append(c); continue; }
            int semi = s.IndexOf(';', i + 1);
            if (semi < 0 || semi - i > 12) { sb.Append(c); continue; }
            string name = s.Substring(i + 1, semi - i - 1);
            if (name.Length > 1 && name[0] == '#')
            {
                int cp; bool ok = name[1] == 'x' || name[1] == 'X'
                    ? int.TryParse(name.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out cp)
                    : int.TryParse(name.Substring(1), out cp);
                if (ok && cp > 0 && cp <= 0x10FFFF) { sb.Append(char.ConvertFromUtf32(cp)); i = semi; continue; }
            }
            else if (Ent.TryGetValue(name, out var v)) { sb.Append(v); i = semi; continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static List<Node> Parse(string src, bool svg = false)
    {
        var root = new Element("#root", svg);
        var stack = new List<Element> { root };
        Element Top() => stack[^1];
        void Add(Node n) { var t = Top(); n.parentNode = t; t.childNodes.Add(n); }
        void AddText(string raw)
        {
            if (raw.Length == 0) return;
            var t = Top();
            if (t.childNodes.Count > 0 && t.childNodes[^1] is TextNode prev) { prev.textContent = prev.data + Decode(raw); return; }
            Add(new TextNode(Decode(raw)));
        }
        void Close(string name)
        {
            for (int k = stack.Count - 1; k > 0; k--)
                if (string.Equals(stack[k].localName, name, StringComparison.OrdinalIgnoreCase)) { stack.RemoveRange(k, stack.Count - k); return; }
        }
        int i = 0, n = src.Length;
        var text = new StringBuilder();
        while (i < n)
        {
            char c = src[i];
            if (c != '<' || i + 1 >= n) { text.Append(c); i++; continue; }
            char d = src[i + 1];
            if (src.AsSpan(i).StartsWith("<!--"))
            {
                AddText(text.ToString()); text.Clear();
                int e = src.IndexOf("-->", i + 4, StringComparison.Ordinal); i = e < 0 ? n : e + 3; continue;
            }
            if (d == '!' || d == '?')
            {
                AddText(text.ToString()); text.Clear();
                int e = src.IndexOf('>', i); i = e < 0 ? n : e + 1; continue;
            }
            if (d == '/')
            {
                int e = src.IndexOf('>', i); if (e < 0) { text.Append(c); i++; continue; }
                AddText(text.ToString()); text.Clear();
                Close(src.Substring(i + 2, e - i - 2).Trim());
                i = e + 1; continue;
            }
            if (!char.IsLetter(d)) { text.Append(c); i++; continue; }
            AddText(text.ToString()); text.Clear();
            /* the tag name */
            int j = i + 1;
            while (j < n && !char.IsWhiteSpace(src[j]) && src[j] != '>' && src[j] != '/') j++;
            string tag = src.Substring(i + 1, j - i - 1);
            bool inSvg = Top().isSvg && Top().localName != "foreignObject";
            bool isSvg = inSvg || tag.Equals("svg", StringComparison.OrdinalIgnoreCase);
            string lname = tag.ToLowerInvariant();
            if (isSvg && SvgCase.TryGetValue(lname, out var cased)) lname = cased;
            var el = new Element(lname, isSvg);
            /* attributes */
            bool selfClose = false;
            while (j < n)
            {
                while (j < n && char.IsWhiteSpace(src[j])) j++;
                if (j >= n) break;
                if (src[j] == '>') { j++; break; }
                if (src[j] == '/') { selfClose = true; j++; continue; }
                int a0 = j;
                while (j < n && !char.IsWhiteSpace(src[j]) && src[j] != '=' && src[j] != '>' && !(src[j] == '/' && j + 1 < n && src[j + 1] == '>')) j++;
                string an = src.Substring(a0, j - a0);
                while (j < n && char.IsWhiteSpace(src[j])) j++;
                string av = "";
                if (j < n && src[j] == '=')
                {
                    j++;
                    while (j < n && char.IsWhiteSpace(src[j])) j++;
                    if (j < n && (src[j] == '"' || src[j] == '\''))
                    {
                        char q = src[j]; int e = src.IndexOf(q, j + 1); if (e < 0) e = n;
                        av = Decode(src.Substring(j + 1, e - j - 1)); j = Math.Min(n, e + 1);
                    }
                    else
                    {
                        int v0 = j; while (j < n && !char.IsWhiteSpace(src[j]) && src[j] != '>') j++;
                        av = Decode(src.Substring(v0, j - v0));
                    }
                }
                if (an.Length == 0) { j++; continue; }
                string key = isSvg ? (SvgCase.TryGetValue(an.ToLowerInvariant(), out var ac) ? ac : an) : an.ToLowerInvariant();
                if (!el.attrs.ContainsKey(key)) { el.attrOrder.Add(key); el.attrs[key] = av; }
            }
            i = j;
            if (el.attrs.TryGetValue("style", out var st)) el.style.ParseFrom(st);
            if (el.canvas != null)
            {
                if (el.attrs.TryGetValue("width", out var w)) el.canvas.width = (int)Math.Max(1, Js.ToNumber(w));
                if (el.attrs.TryGetValue("height", out var h)) el.canvas.height = (int)Math.Max(1, Js.ToNumber(h));
            }
            /* the end tags HTML implies */
            if (!isSvg)
            {
                if (lname == "li") { for (int k = stack.Count - 1; k > 0; k--) { var s = stack[k].localName; if (s == "li") { stack.RemoveRange(k, stack.Count - k); break; } if (s == "ul" || s == "ol") break; } }
                else if (lname == "p" || lname == "div" || lname == "ul" || lname == "ol" || lname.Length == 2 && lname[0] == 'h' && char.IsDigit(lname[1]))
                { if (Top().localName == "p") stack.RemoveAt(stack.Count - 1); }
            }
            Add(el);
            if (selfClose || (!isSvg && Void.Contains(lname))) continue;
            if (!isSvg && RawText.Contains(lname))
            {
                int e = src.IndexOf("</" + lname, i, StringComparison.OrdinalIgnoreCase); if (e < 0) e = n;
                string raw = src.Substring(i, e - i);
                if (raw.Length > 0) { var t = new TextNode(lname == "textarea" || lname == "title" ? Decode(raw) : raw) { parentNode = el }; el.childNodes.Add(t); }
                int gt = src.IndexOf('>', e); i = gt < 0 ? n : gt + 1;
                continue;
            }
            stack.Add(el);
        }
        AddText(text.ToString());
        var outl = root.childNodes.ToList();
        foreach (var x in outl) x.parentNode = null;
        return outl;
    }

    static string Esc(string s, bool attr)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': if (attr) sb.Append(c); else sb.Append("&lt;"); break;
                case '>': if (attr) sb.Append(c); else sb.Append("&gt;"); break;
                case '"': if (attr) sb.Append("&quot;"); else sb.Append(c); break;
                case ' ': sb.Append("&nbsp;"); break;
                default: sb.Append(c); break;
            }
        return sb.ToString();
    }
    public static void Serialize(Node n, StringBuilder sb)
    {
        if (n is TextNode t)
        {
            var p = t.parentNode?.localName;
            sb.Append(p == "style" || p == "script" ? t.data : Esc(t.data, false)); return;
        }
        var e = (Element)n;
        sb.Append('<').Append(e.localName);
        foreach (var k in e.attrOrder) sb.Append(' ').Append(k).Append("=\"").Append(Esc(e.getAttribute(k) ?? "", true)).Append('"');
        sb.Append('>');
        if (!e.isSvg && Void.Contains(e.localName)) return;
        foreach (var c in e.childNodes) Serialize(c, sb);
        sb.Append("</").Append(e.localName).Append('>');
    }
}
