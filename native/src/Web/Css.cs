// The native UI engine, part 3: CSS in. The stylesheet the browser build carries
// is parsed once into rules; selectors match right to left the way a browser's
// do, with specificity, :hover/:active/:focus state, :not(), :nth-child() and
// the pseudo-elements the screens use (::before, ::after, scrollbars, range
// thumbs, placeholders).
using System.Text;

namespace Brickblade;

public sealed class CssRule
{
    public Selector sel; public List<(string k, string v, bool imp)> decls; public int order; public string media;
}
public sealed class Keyframes { public string name; public List<(double at, List<(string k, string v, bool imp)> decls)> frames = new(); }

/* one compound selector: tag#id.class[attr]:pseudo */
public sealed class Compound
{
    public string tag, id; public List<string> classes = new();
    public List<(string name, string op, string val)> attrs = new();
    public List<(string name, string arg, List<Selector> sub)> pseudos = new();
    public char comb = ' ';   /* how it joins to the compound on its LEFT: ' ', '>', '+', '~' */
}

public sealed class Selector
{
    public List<Compound> parts = new();   /* left to right */
    public string pseudoEl;                 /* before, after, -webkit-scrollbar, ... */
    public int spec;
    public bool dynamic;                    /* depends on :hover/:active/:focus */
    public string text;
    public bool Matches(Element e, Element scope) => Match(e, parts.Count - 1);
    bool Match(Element e, int i)
    {
        var c = parts[i];
        if (!MatchCompound(e, c)) return false;
        if (i == 0) return true;
        switch (c.comb)
        {
            case '>': return e.parentNode != null && e.parentNode.localName != "#root" && Match(e.parentNode, i - 1);
            case '+': { var p = e.previousElementSibling; return p != null && Match(p, i - 1); }
            case '~': { for (var p = e.previousElementSibling; p != null; p = p.previousElementSibling) if (Match(p, i - 1)) return true; return false; }
            default:
                for (var p = e.parentNode; p != null; p = p.parentNode) if (Match(p, i - 1)) return true;
                return false;
        }
    }
    static bool MatchCompound(Element e, Compound c)
    {
        if (c.tag != null && c.tag != "*" && !string.Equals(c.tag, e.localName, StringComparison.OrdinalIgnoreCase)) return false;
        if (c.id != null && e.attrs.GetValueOrDefault("id") != c.id) return false;
        if (c.classes.Count > 0) { var cl = e.Classes; foreach (var k in c.classes) if (!cl.Contains(k)) return false; }
        foreach (var (name, op, val) in c.attrs)
        {
            var v = e.getAttribute(name);
            if (v == null) return false;
            switch (op)
            {
                case null: break;
                case "=": if (v != val) return false; break;
                case "~=": if (!v.Split(' ').Contains(val)) return false; break;
                case "^=": if (!v.StartsWith(val, StringComparison.Ordinal)) return false; break;
                case "$=": if (!v.EndsWith(val, StringComparison.Ordinal)) return false; break;
                case "*=": if (!v.Contains(val, StringComparison.Ordinal)) return false; break;
                case "|=": if (v != val && !v.StartsWith(val + "-", StringComparison.Ordinal)) return false; break;
            }
        }
        foreach (var (name, arg, sub) in c.pseudos)
            if (!Pseudo(e, name, arg, sub)) return false;
        return true;
    }
    static bool Pseudo(Element e, string name, string arg, List<Selector> sub)
    {
        switch (name)
        {
            case "hover": return Dom.InHover(e);
            case "active": return Dom.InActive(e);
            case "focus": case "focus-visible": return Dom.focused == e;
            case "focus-within": return Dom.focused != null && e.contains(Dom.focused);
            case "disabled": return e.hasAttribute("disabled");
            case "enabled": return !e.hasAttribute("disabled");
            case "checked": return e.@checked;
            case "not": return !sub.Any(s => s.Matches(e, null));
            case "is": case "where": case "matches": return sub.Any(s => s.Matches(e, null));
            case "has": { bool f = false; Element.Walk(e, d => { if (sub.Any(s => s.Matches(d, null))) { f = true; return false; } return true; }); return f; }
            case "first-child": return e.parentNode != null && e.previousElementSibling == null;
            case "last-child": return e.parentNode != null && e.nextElementSibling == null;
            case "only-child": return e.parentNode != null && e.previousElementSibling == null && e.nextElementSibling == null;
            case "first-of-type": { for (var p = e.previousElementSibling; p != null; p = p.previousElementSibling) if (p.localName == e.localName) return false; return true; }
            case "last-of-type": { for (var p = e.nextElementSibling; p != null; p = p.nextElementSibling) if (p.localName == e.localName) return false; return true; }
            case "empty": return e.childNodes.Count == 0 || e.childNodes.All(n => n is TextNode t && t.data.Length == 0);
            case "root": return e == Dom.document.documentElement;
            case "nth-child": case "nth-last-child": case "nth-of-type": case "nth-last-of-type":
                {
                    if (e.parentNode == null) return false;
                    bool last = name.Contains("last"), oftype = name.Contains("type");
                    int idx = 0;
                    var sibs = e.parentNode.childNodes;
                    if (!last) { foreach (var n in sibs) { if (n is Element x && (!oftype || x.localName == e.localName)) { idx++; if (x == e) break; } } }
                    else { for (int k = sibs.Count - 1; k >= 0; k--) if (sibs[k] is Element x && (!oftype || x.localName == e.localName)) { idx++; if (x == e) break; } }
                    return Nth(arg, idx);
                }
            case "placeholder-shown": return e.localName == "input" && e.value.Length == 0;
            default: return false;
        }
    }
    static bool Nth(string arg, int idx)
    {
        arg = (arg ?? "").Replace(" ", "").ToLowerInvariant();
        if (arg == "odd") return idx % 2 == 1;
        if (arg == "even") return idx % 2 == 0;
        int a = 0, b;
        int n = arg.IndexOf('n');
        if (n < 0) return int.TryParse(arg, out b) && idx == b;
        string sa = arg.Substring(0, n), sb = arg.Substring(n + 1);
        a = sa == "" || sa == "+" ? 1 : sa == "-" ? -1 : int.Parse(sa);
        b = sb == "" ? 0 : int.Parse(sb);
        if (a == 0) return idx == b;
        return (idx - b) % a == 0 && (idx - b) / a >= 0;
    }
}

public static class CssParser
{
    static string StripComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '*') { int e = s.IndexOf("*/", i + 2, StringComparison.Ordinal); i = e < 0 ? s.Length : e + 1; continue; }
            if (s[i] == '"' || s[i] == '\'') { char q = s[i]; int j = i + 1; while (j < s.Length && s[j] != q) { if (s[j] == '\\') j++; j++; } sb.Append(s, i, Math.Min(s.Length, j + 1) - i); i = j; continue; }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
    /* the index of the brace that closes the one at i */
    static int Match(string s, int i)
    {
        int d = 0;
        for (int j = i; j < s.Length; j++)
        {
            char c = s[j];
            if (c == '"' || c == '\'') { j++; while (j < s.Length && s[j] != c) { if (s[j] == '\\') j++; j++; } continue; }
            if (c == '{') d++;
            else if (c == '}') { d--; if (d == 0) return j; }
        }
        return s.Length;
    }
    public static void ParseSheet(string css, List<CssRule> rules, Dictionary<string, Keyframes> kfs, string media = null)
    {
        css = StripComments(css);
        int i = 0;
        while (i < css.Length)
        {
            int open = css.IndexOf('{', i);
            if (open < 0) break;
            string pre = css.Substring(i, open - i).Trim();
            int close = Match(css, open);
            string body = css.Substring(open + 1, Math.Max(0, close - open - 1));
            i = close + 1;
            /* a stray `;` before an at-rule (from @import or @charset) */
            int semi = pre.LastIndexOf(';'); if (semi >= 0) pre = pre.Substring(semi + 1).Trim();
            if (pre.StartsWith("@media"))
            {
                string cond = pre.Substring(6).Trim();
                ParseSheet(body, rules, kfs, media == null ? cond : media + " and " + cond);
                continue;
            }
            if (pre.StartsWith("@supports")) { ParseSheet(body, rules, kfs, media); continue; }
            if (pre.StartsWith("@keyframes") || pre.StartsWith("@-webkit-keyframes"))
            {
                var k = new Keyframes { name = pre.Substring(pre.IndexOf(' ') + 1).Trim() };
                int j = 0;
                while (j < body.Length)
                {
                    int o = body.IndexOf('{', j); if (o < 0) break;
                    int c = Match(body, o);
                    var decls = ParseDecls(body.Substring(o + 1, c - o - 1));
                    foreach (var at in body.Substring(j, o - j).Split(','))
                    {
                        var t = at.Trim();
                        double p = t == "from" ? 0 : t == "to" ? 1 : Js.ParseFloat(t) / 100;
                        if (!double.IsNaN(p)) k.frames.Add((p, decls));
                    }
                    j = c + 1;
                }
                k.frames.Sort((a, b) => a.at.CompareTo(b.at));
                kfs[k.name] = k;
                continue;
            }
            if (pre.StartsWith("@")) continue;
            var decl = ParseDecls(body);
            foreach (var s in SelectorList(pre))
                rules.Add(new CssRule { sel = s, decls = decl, order = rules.Count, media = media });
        }
    }
    /* split on a separator outside parentheses and quotes */
    public static List<string> SplitTop(string s, char sep)
    {
        var l = new List<string>(); int d = 0, st = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' || c == '\'') { i++; while (i < s.Length && s[i] != c) { if (s[i] == '\\') i++; i++; } continue; }
            if (c == '(' || c == '[') d++;
            else if (c == ')' || c == ']') d--;
            else if (c == sep && d == 0) { l.Add(s.Substring(st, i - st)); st = i + 1; }
        }
        l.Add(s.Substring(st));
        return l;
    }
    public static List<(string k, string v, bool imp)> ParseDecls(string body)
    {
        var l = new List<(string, string, bool)>();
        foreach (var part in SplitTop(StripComments(body), ';'))
        {
            int c = part.IndexOf(':');
            if (c <= 0) continue;
            string k = part.Substring(0, c).Trim(), v = part.Substring(c + 1).Trim();
            if (!k.StartsWith("--")) k = k.ToLowerInvariant();
            bool imp = false;
            int bang = v.LastIndexOf('!');
            if (bang >= 0 && v.Substring(bang + 1).Trim().Equals("important", StringComparison.OrdinalIgnoreCase)) { imp = true; v = v.Substring(0, bang).Trim(); }
            if (k.Length == 0) continue;
            l.Add((k, v, imp));
        }
        return l;
    }

    static readonly Dictionary<string, List<Selector>> selCache = new();
    public static List<Selector> SelectorList(string s)
    {
        if (selCache.TryGetValue(s, out var l)) return l;
        l = new List<Selector>();
        foreach (var part in SplitTop(s, ','))
        {
            var t = part.Trim();
            if (t.Length == 0) continue;
            try { var x = ParseSelector(t); if (x != null) l.Add(x); } catch { }
        }
        if (selCache.Count > 4000) selCache.Clear();
        selCache[s] = l;
        return l;
    }
    static bool IdentChar(char c) => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c > 127 || c == '\\';
    static Selector ParseSelector(string s)
    {
        var sel = new Selector { text = s };
        int i = 0, a = 0, b = 0, c = 0;
        var cur = new Compound(); bool any = false; char comb = ' ';
        void Flush() { if (any) { cur.comb = comb; sel.parts.Add(cur); } cur = new Compound(); any = false; }
        string Ident() { int st = i; while (i < s.Length && IdentChar(s[i])) { if (s[i] == '\\') i++; i++; } return s.Substring(st, i - st).Replace("\\", ""); }
        while (i < s.Length)
        {
            char ch = s[i];
            if (char.IsWhiteSpace(ch) || ch == '>' || ch == '+' || ch == '~')
            {
                char cb = ' ';
                while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '>' || s[i] == '+' || s[i] == '~')) { if (s[i] != ' ' && !char.IsWhiteSpace(s[i])) cb = s[i]; i++; }
                if (any) { Flush(); comb = cb; }
                else if (sel.parts.Count > 0) comb = cb;
                continue;
            }
            if (ch == '*') { cur.tag = "*"; any = true; i++; continue; }
            if (ch == '#') { i++; cur.id = Ident(); a++; any = true; continue; }
            if (ch == '.') { i++; cur.classes.Add(Ident()); b++; any = true; continue; }
            if (ch == '[')
            {
                int e = s.IndexOf(']', i);
                string inner = s.Substring(i + 1, e - i - 1); i = e + 1;
                string op = null, name = inner, val = null;
                foreach (var o in new[] { "~=", "^=", "$=", "*=", "|=", "=" })
                {
                    int p = inner.IndexOf(o, StringComparison.Ordinal);
                    if (p > 0) { op = o; name = inner.Substring(0, p); val = inner.Substring(p + o.Length).Trim().Trim('"', '\''); break; }
                }
                cur.attrs.Add((name.Trim(), op, val)); b++; any = true; continue;
            }
            if (ch == ':')
            {
                bool el = i + 1 < s.Length && s[i + 1] == ':';
                i += el ? 2 : 1;
                string name = Ident().ToLowerInvariant();
                string arg = null;
                if (i < s.Length && s[i] == '(')
                {
                    int d = 0, st = i + 1;
                    for (; i < s.Length; i++) { if (s[i] == '(') d++; else if (s[i] == ')') { d--; if (d == 0) break; } }
                    arg = s.Substring(st, i - st); i++;
                }
                if (el || name == "before" || name == "after" || name == "placeholder" || name.StartsWith("-webkit-") || name.StartsWith("-moz-") || name == "selection" || name == "marker")
                { sel.pseudoEl = name; c++; any = true; continue; }
                List<Selector> sub = null;
                if (name == "not" || name == "is" || name == "where" || name == "matches" || name == "has")
                {
                    sub = SelectorList(arg ?? "");
                    if (name != "where") { int best = 0; foreach (var x in sub) best = Math.Max(best, x.spec); a += best / 10000; b += best / 100 % 100; c += best % 100; }
                }
                else b++;
                if (name == "hover" || name == "active" || name == "focus" || name == "focus-within" || name == "focus-visible") sel.dynamic = true;
                if (sub != null && sub.Any(x => x.dynamic)) sel.dynamic = true;
                cur.pseudos.Add((name, arg, sub)); any = true; continue;
            }
            if (IdentChar(ch)) { cur.tag = Ident().ToLowerInvariant(); c++; any = true; continue; }
            i++;
        }
        Flush();
        if (sel.parts.Count == 0) return null;
        sel.parts[0].comb = ' ';
        sel.spec = a * 10000 + b * 100 + c;
        return sel;
    }
}

/* the stylesheet the document carries: rules indexed by what their last compound needs */
public static class Sheet
{
    public static readonly List<CssRule> rules = new();
    /* the user-agent rules come first; TEXT SIZE scales only the game's own */
    public static int uaCount;
    public static double TextK = 1;
    public static readonly Dictionary<string, Keyframes> keyframes = new();
    static readonly Dictionary<string, List<CssRule>> byId = new(), byClass = new(), byTag = new();
    static readonly List<CssRule> universal = new();
    public static void Load(string css)
    {
        int before = rules.Count;
        CssParser.ParseSheet(css, rules, keyframes);
        for (int i = before; i < rules.Count; i++)
        {
            var r = rules[i]; r.order = i;
            var last = r.sel.parts[^1];
            if (last.id != null) Add(byId, last.id, r);
            else if (last.classes.Count > 0) Add(byClass, last.classes[0], r);
            else if (last.tag != null && last.tag != "*") Add(byTag, last.tag, r);
            else universal.Add(r);
        }
        Dom.StyleAll();
    }
    static void Add(Dictionary<string, List<CssRule>> d, string k, CssRule r) { if (!d.TryGetValue(k, out var l)) d[k] = l = new(); l.Add(r); }
    /* every rule whose selector matches e, in cascade order of appearance */
    public static void Matching(Element e, List<CssRule> into)
    {
        into.Clear();
        var id = e.attrs.GetValueOrDefault("id");
        if (id != null && byId.TryGetValue(id, out var l1)) Test(e, l1, into);
        foreach (var c in e.Classes) if (byClass.TryGetValue(c, out var l2)) Test(e, l2, into);
        if (byTag.TryGetValue(e.localName, out var l3)) Test(e, l3, into);
        Test(e, universal, into);
    }
    static void Test(Element e, List<CssRule> l, List<CssRule> into)
    {
        foreach (var r in l)
        {
            if (r.media != null && !Media(r.media)) continue;
            if (r.sel.Matches(e, null) && !into.Contains(r)) into.Add(r);
        }
    }
    /* (max-width:560px) and friends, against the viewport in CSS px */
    public static bool Media(string q)
    {
        foreach (var part in q.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim().Trim('(', ')').Trim();
            if (t == "screen" || t == "all" || t.Length == 0) continue;
            int c = t.IndexOf(':'); if (c < 0) continue;
            string k = t.Substring(0, c).Trim(); double v = Js.ParseFloat(t.Substring(c + 1).Trim());
            switch (k)
            {
                case "max-width": if (!(Dom.ViewW <= v)) return false; break;
                case "min-width": if (!(Dom.ViewW >= v)) return false; break;
                case "max-height": if (!(Dom.ViewH <= v)) return false; break;
                case "min-height": if (!(Dom.ViewH >= v)) return false; break;
            }
        }
        return true;
    }
    public static bool HasMedia => rules.Any(r => r.media != null);
}
