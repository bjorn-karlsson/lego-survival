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
    readonly JsMap<string, double> d = new();
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

/* A JS object used as a SET of names: { phys:true, fire:true }. Indexing a name
   it does not hold gives false, the way `o[k]` gives undefined. */
public sealed class Flags : HashSet<string>
{
    public Flags() { }
    public Flags(IEnumerable<string> a) : base(a) { }
    public bool this[string k] => k != null && Contains(k);
}
/* A JS array of numbers indexed by a NUMBER: `EMBER_SHOTS[lv]` with lv a double.
   An index past the end gives undefined in the JS -- read here as 0 (NaN would
   poison every sum it reached; every table in the game is read inside its range). */
public sealed class Tbl : IReadOnlyList<double>
{
    readonly double[] a;
    public Tbl(params double[] a) { this.a = a; }
    public static implicit operator Tbl(double[] a) => new Tbl(a);
    public double this[int i] => i >= 0 && i < a.Length ? a[i] : 0;
    public double this[double i] => this[(int)i];
    public int Count => a.Length;
    public int Length => a.Length;
    public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)a).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => a.GetEnumerator();
}

/* A field read and written by its name, for the few places the JS does
   `p[key]` with a computed key: cached reflection over the class's fields. */
public static class Fields
{
    static readonly JsMap<(Type, string), FieldInfo> cache = new();
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

/* A JS OBJECT USED AS A MAP. Reading a key that is not there is undefined in JS,
   never an error, and the game leans on that everywhere (`LOB_CONF[pr.kind]` and
   then `if(lc)`). This is a Dictionary whose indexer reads like that: a missing
   key gives null (or the value type's default: false, 0). Writing, enumeration
   and everything else are the Dictionary's own. */
public class JsMap<TK, TV> : Dictionary<TK, TV>
{
    public JsMap() { }
    public JsMap(IEqualityComparer<TK> cmp) : base(cmp) { }
    public JsMap(IDictionary<TK, TV> from) : base(from) { }
    public JsMap(IEnumerable<KeyValuePair<TK, TV>> from) : base(from) { }
    public JsMap(int capacity) : base(capacity) { }
    public new TV this[TK k]
    {
        get => k != null && TryGetValue(k, out var v) ? v : default;
        set => base[k] = value;
    }
}
public static class JsMapExt
{
    public static JsMap<TK, TV> ToJsMap<T, TK, TV>(this IEnumerable<T> src, Func<T, TK> key, Func<T, TV> val)
    {
        var m = new JsMap<TK, TV>(); foreach (var x in src) m[key(x)] = val(x); return m;
    }
    public static JsMap<TK, T> ToJsMap<T, TK>(this IEnumerable<T> src, Func<T, TK> key)
    {
        var m = new JsMap<TK, T>(); foreach (var x in src) m[key(x)] = x; return m;
    }
}
