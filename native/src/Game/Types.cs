using System.Reflection;
using System.Runtime.CompilerServices;

namespace Brickblade;

/* Anything with a position: what dist() and angTo() take. Every JS object
   literal with x and y that is passed around as a point becomes one. */
public class Obj
{
    public double x, y, r;
    public bool dead;
    /* what hurt you, in the log and on the death screen: every source of damage carries these */
    public string type, bname;
    public double dmg;
    public Obj() { }
    public Obj(double x, double y) { this.x = x; this.y = y; }
}
/* a bare {x, y} */
public sealed class Pt : Obj
{
    public Pt() { }
    public Pt(double x, double y) : base(x, y) { }
}

/* A JS object used as a map from a name to a number: { fire:0, frost:0 }.
   Reading a key it never had gives 0, the way `o[k] || 0` does in the JS. Keys
   keep their insertion order, so `for (k in o)` walks them as the JS did. */
public sealed class NumMap : IEnumerable<KeyValuePair<string, double>>
{
    readonly List<string> order = new();
    readonly Dictionary<string, double> d = new();
    public NumMap() { }
    public NumMap(params (string k, double v)[] kv) { foreach (var (k, v) in kv) this[k] = v; }
    public double this[string k]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => k != null && d.TryGetValue(k, out var v) ? v : 0;
        set { if (k == null) return; if (!d.ContainsKey(k)) order.Add(k); d[k] = value; }
    }
    public bool Has(string k) => k != null && d.ContainsKey(k);
    public void Delete(string k) { if (k != null && d.Remove(k)) order.Remove(k); }
    public IEnumerable<string> Keys => order;
    public int Count => order.Count;
    public void Clear() { order.Clear(); d.Clear(); }
    public NumMap Clone() { var n = new NumMap(); foreach (var k in order) n[k] = d[k]; return n; }
    /* Object.assign(this, other) */
    public NumMap Assign(NumMap o) { if (o != null) foreach (var k in o.order) this[k] = o.d[k]; return this; }
    public IEnumerator<KeyValuePair<string, double>> GetEnumerator() { foreach (var k in order.ToList()) yield return new(k, d[k]); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/* A field read and written by its name, for the few places the JS does
   `p[key]` with a computed key: cached reflection over the class's fields. */
public static class Fields
{
    static readonly Dictionary<(Type, string), FieldInfo> cache = new();
    static FieldInfo F(object o, string name)
    {
        var key = (o.GetType(), name);
        if (!cache.TryGetValue(key, out var f))
        {
            f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            cache[key] = f;
        }
        return f;
    }
    public static double Num(object o, string name)
    {
        if (o == null) return 0;
        var f = F(o, name);
        if (f == null) return 0;
        var v = f.GetValue(o);
        return v switch { double d => d, int i => i, bool b => b ? 1 : 0, float fl => fl, _ => 0 };
    }
    public static void SetNum(object o, string name, double v)
    {
        if (o == null) return;
        var f = F(o, name);
        if (f == null) return;
        if (f.FieldType == typeof(double)) f.SetValue(o, v);
        else if (f.FieldType == typeof(int)) f.SetValue(o, (int)v);
        else if (f.FieldType == typeof(bool)) f.SetValue(o, v != 0);
    }
    public static object Get(object o, string name) { if (o == null) return null; var f = F(o, name); return f?.GetValue(o); }
    public static void Set(object o, string name, object v) { if (o == null) return; var f = F(o, name); f?.SetValue(o, v); }
    public static bool Has(object o, string name) => o != null && F(o, name) != null;
    /* Object.assign(target, source) for two objects of one class: every field */
    public static void CopyAll(object target, object source)
    {
        foreach (var f in source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            f.SetValue(target, f.GetValue(source));
    }
}

/* The small helpers every section uses: List and array methods spelled the way
   the JS spells them, so ported code reads like the original. */
public static class JsList
{
    public static T Pick<T>(this IList<T> a) => a.Count == 0 ? default : a[(int)(Js.R() * a.Count)];
    public static void Push<T>(this List<T> a, T v) => a.Add(v);
    public static T Shift<T>(this List<T> a) { if (a.Count == 0) return default; var v = a[0]; a.RemoveAt(0); return v; }
    public static T Pop<T>(this List<T> a) { if (a.Count == 0) return default; var v = a[^1]; a.RemoveAt(a.Count - 1); return v; }
    public static void Unshift<T>(this List<T> a, T v) => a.Insert(0, v);
    public static List<T> Slice<T>(this List<T> a, int start, int end = int.MaxValue)
    {
        int n = a.Count;
        if (start < 0) start = Math.Max(0, n + start);
        if (end < 0) end = Math.Max(0, n + end);
        end = Math.Min(end, n);
        if (start >= end) return new List<T>();
        return a.GetRange(start, end - start);
    }
    public static List<T> Splice<T>(this List<T> a, int start, int count)
    {
        if (start < 0) start = Math.Max(0, a.Count + start);
        if (start > a.Count) start = a.Count;
        count = Math.Max(0, Math.Min(count, a.Count - start));
        var r = a.GetRange(start, count); a.RemoveRange(start, count); return r;
    }
    /* Array.prototype.sort with a comparator: stable, as V8's is */
    public static List<T> SortJs<T>(this List<T> a, Func<T, T, double> cmp)
    {
        var idx = Enumerable.Range(0, a.Count).ToList();
        var copy = a.ToList();
        idx.Sort((i, j) => { double c = cmp(copy[i], copy[j]); return c < 0 ? -1 : c > 0 ? 1 : i.CompareTo(j); });
        for (int k = 0; k < idx.Count; k++) a[k] = copy[idx[k]];
        return a;
    }
    public static string Join<T>(this IEnumerable<T> a, string sep = ",") => string.Join(sep, a.Select(x => x is double d ? Js.Num(d) : x?.ToString() ?? ""));
    /* in-place filter, the way `list = list.filter(...)` is used on the world lists */
    public static List<T> Filter<T>(this List<T> a, Func<T, bool> keep) => a.Where(keep).ToList();
    public static int IndexOfJs<T>(this IList<T> a, T v) => a.IndexOf(v);
    public static bool Includes<T>(this IList<T> a, T v) => a.Contains(v);
    public static T At<T>(this IList<T> a, int i) => i >= 0 && i < a.Count ? a[i] : default;
}
