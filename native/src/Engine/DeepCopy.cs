// structuredClone and Object.assign for the game's own objects: a field-by-field
// deep copy (cycles kept as cycles) and a shallow field copy back. The previewer
// uses them to try a brick on a copy of the hero and put everything back.
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Brickblade;

public static class DeepCopy
{
    static readonly Dictionary<Type, FieldInfo[]> fieldsOf = new();
    static FieldInfo[] Fields(Type t)
    {
        if (fieldsOf.TryGetValue(t, out var f)) return f;
        var l = new List<FieldInfo>();
        for (var x = t; x != null && x != typeof(object); x = x.BaseType)
            l.AddRange(x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return fieldsOf[t] = l.ToArray();
    }
    /* things that are shared, never copied: resources, the DOM, delegates */
    static bool Shared(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || typeof(Delegate).IsAssignableFrom(t)
        || t == typeof(OffCanvas) || t == typeof(Target) || typeof(Node).IsAssignableFrom(t) || t == typeof(Ctx) || t == typeof(Type);
    public static T Clone<T>(T o) where T : class => (T)Copy(o, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));
    static object Copy(object o, Dictionary<object, object> seen)
    {
        if (o == null) return null;
        var t = o.GetType();
        if (Shared(t) || t.IsValueType && !HasRefs(t)) return o;
        if (seen.TryGetValue(o, out var done)) return done;
        if (t.IsArray)
        {
            var a = (Array)o; var c = Array.CreateInstance(t.GetElementType(), a.Length); seen[o] = c;
            for (int i = 0; i < a.Length; i++) c.SetValue(Copy(a.GetValue(i), seen), i);
            return c;
        }
        if (t.IsValueType)
        {
            object box = RuntimeHelpers.GetUninitializedObject(t);
            foreach (var f in Fields(t)) f.SetValue(box, Copy(f.GetValue(o), seen));
            return box;
        }
        if (o is IDictionary d && t.IsGenericType)
        {
            var c = (IDictionary)Activator.CreateInstance(t); seen[o] = c;
            foreach (DictionaryEntry e in d) c[Copy(e.Key, seen)] = Copy(e.Value, seen);
            return c;
        }
        if (o is IList l && t.IsGenericType)
        {
            var c = (IList)Activator.CreateInstance(t); seen[o] = c;
            foreach (var x in l) c.Add(Copy(x, seen));
            return c;
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            var c = Activator.CreateInstance(t); seen[o] = c;
            var add = t.GetMethod("Add");
            foreach (var x in (IEnumerable)o) add.Invoke(c, new[] { Copy(x, seen) });
            return c;
        }
        var n = RuntimeHelpers.GetUninitializedObject(t); seen[o] = n;
        foreach (var f in Fields(t)) f.SetValue(n, Copy(f.GetValue(o), seen));
        return n;
    }
    static readonly Dictionary<Type, bool> refsIn = new();
    static bool HasRefs(Type t)
    {
        if (refsIn.TryGetValue(t, out var b)) return b;
        refsIn[t] = false;
        b = Fields(t).Any(f => !f.FieldType.IsPrimitive && !f.FieldType.IsEnum && (!f.FieldType.IsValueType || HasRefs(f.FieldType)));
        return refsIn[t] = b;
    }
    /* Object.assign({}, o): the fields, not what they point at */
    public static T Shallow<T>(T o) where T : class
    {
        if (o == null) return null;
        var n = RuntimeHelpers.GetUninitializedObject(o.GetType());
        foreach (var f in Fields(o.GetType())) f.SetValue(n, f.GetValue(o));
        return (T)n;
    }
    /* Object.assign(into, from): every field of `from` written onto `into` */
    public static void Assign<T>(T into, T from) where T : class
    {
        if (into == null || from == null) return;
        foreach (var f in Fields(from.GetType())) if (!f.IsInitOnly) f.SetValue(into, f.GetValue(from));
    }
}
