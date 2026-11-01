namespace Brickblade;

/* THE JS's ARRAY AND STRING METHODS, under their JS names, so a line ported from
   the JS can keep its shape: `list.filter(e=>...).map(e=>...).join(' · ')`.
   Each one does what the JS method does, including the edge cases the game
   leans on (slice with negative ends, indexOf of a missing item, join of an
   empty list). */
public static class JsExt
{
    /* `for (const x of list)` exactly: by index, re-reading the length each step, so a
       body that pushes to the list, splices it or empties it behaves as it did in the
       browser instead of throwing "collection was modified" */
    public static IEnumerable<T> Live<T>(this List<T> l) { for (int i = 0; i < l.Count; i++) yield return l[i]; }
    public static List<R> map<T, R>(this IEnumerable<T> a, Func<T, R> f) => a.Select(f).ToList();
    public static List<R> map<T, R>(this IEnumerable<T> a, Func<T, int, R> f) => a.Select(f).ToList();
    public static List<T> filter<T>(this IEnumerable<T> a, Func<T, bool> f) => a.Where(f).ToList();
    public static List<T> filter<T>(this IEnumerable<T> a, Func<T, int, bool> f) => a.Where(f).ToList();
    public static bool some<T>(this IEnumerable<T> a, Func<T, bool> f) => a.Any(f);
    public static bool every<T>(this IEnumerable<T> a, Func<T, bool> f) => a.All(f);
    public static T find<T>(this IEnumerable<T> a, Func<T, bool> f) => a.FirstOrDefault(f);
    public static int findIndex<T>(this IList<T> a, Func<T, bool> f) { for (int i = 0; i < a.Count; i++) if (f(a[i])) return i; return -1; }
    public static bool includes<T>(this IEnumerable<T> a, T v) => a.Contains(v);
    public static bool includes(this string s, string v) => s.Contains(v);
    public static int indexOf<T>(this IList<T> a, T v) => a.IndexOf(v);
    public static int indexOf(this string s, string v) => s.IndexOf(v, StringComparison.Ordinal);
    public static string join<T>(this IEnumerable<T> a, string sep = ",") => a.Join(sep);
    public static R reduce<T, R>(this IEnumerable<T> a, Func<R, T, R> f, R init) { var acc = init; foreach (var x in a) acc = f(acc, x); return acc; }
    public static void forEach<T>(this IEnumerable<T> a, Action<T> f) { foreach (var x in a.ToList()) f(x); }
    public static void forEach<T>(this IEnumerable<T> a, Action<T, int> f) { int i = 0; foreach (var x in a.ToList()) f(x, i++); }
    public static List<T> slice<T>(this List<T> a, int start = 0, int end = int.MaxValue) => a.Slice(start, end);
    public static List<T> slice<T>(this T[] a, int start = 0, int end = int.MaxValue) => a.ToList().Slice(start, end);
    public static string slice(this string s, int start, int end = int.MaxValue)
    {
        int n = s.Length;
        if (start < 0) start = System.Math.Max(0, n + start);
        if (end < 0) end = System.Math.Max(0, n + end);
        end = System.Math.Min(end, n);
        return start >= end ? "" : s.Substring(start, end - start);
    }
    public static List<T> concat<T>(this IEnumerable<T> a, IEnumerable<T> b) => a.Concat(b).ToList();
    public static List<T> concat<T>(this IEnumerable<T> a, params T[] b) => a.Concat(b).ToList();
    public static void push<T>(this List<T> a, params T[] v) => a.AddRange(v);
    public static T pop<T>(this List<T> a) => a.Pop();
    public static T shift<T>(this List<T> a) => a.Shift();
    public static void unshift<T>(this List<T> a, T v) => a.Insert(0, v);
    public static List<T> splice<T>(this List<T> a, int start, int count) => a.Splice(start, count);
    public static List<T> sort<T>(this List<T> a, Func<T, T, double> cmp) => a.SortJs(cmp);
    public static List<T> reverse<T>(this List<T> a) { a.Reverse(); return a; }
    public static string toFixed(this double v, int d) => Js.ToFixed(v, d);
    public static string toFixed(this int v, int d) => Js.ToFixed(v, d);
    public static string toUpperCase(this string s) => s.ToUpperInvariant();
    public static string toLowerCase(this string s) => s.ToLowerInvariant();
    public static string trim(this string s) => s.Trim();
    public static bool startsWith(this string s, string p) => s.StartsWith(p, StringComparison.Ordinal);
    public static bool endsWith(this string s, string p) => s.EndsWith(p, StringComparison.Ordinal);
    public static string padStart(this string s, int n, string pad = " ") => Js.PadStart(s, n, pad.Length > 0 ? pad[0] : ' ');
    public static string repeat(this string s, int n) => n <= 0 ? "" : string.Concat(Enumerable.Repeat(s, n));
    public static string repeat(this string s, double n) => s.repeat((int)n);
    public static List<string> split(this string s, string sep) => sep == "" ? s.Select(ch => ch.ToString()).ToList() : s.Split(sep).ToList();
    public static string charAt(this string s, int i) => i >= 0 && i < s.Length ? s[i].ToString() : "";
}

public static partial class G
{
    /* ${x} in a template literal: what String(x) gives */
    public static string S(double v) => Js.Num(v);
    public static string S(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string S(string v) => v ?? "null";
    public static string S(bool v) => v ? "true" : "false";
    public static string S(Col v) => v.ToString();
    public static string S(object v) => v switch
    {
        null => "null", double d => Js.Num(d), int i => S(i), bool b => S(b), string s => s, _ => v.ToString()
    };
    /* (1234567).toLocaleString() in an English locale: thousands grouped, up to three decimals */
    public static string toLocaleString(this double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
        return System.Math.Round(v, 3, MidpointRounding.AwayFromZero).ToString("#,0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
