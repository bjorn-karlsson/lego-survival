using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Brickblade;

/* The JavaScript semantics the port leans on, in one place: Math.random, the
   number-to-text rules (String(n), toFixed), int32 arithmetic, and the
   truthiness of a number. Everything here behaves the way V8 does for the
   values the game produces. */
public static class Js
{
    /* ---- Math.random: xoshiro256**, seeded from the clock (or a test's seed) ---- */
    static ulong s0, s1, s2, s3;
    static Js() { Seed((ulong)DateTime.UtcNow.Ticks ^ 0x9E3779B97F4A7C15UL); }
    public static void Seed(ulong seed)
    {
        ulong sm = seed;
        ulong next() { sm += 0x9E3779B97F4A7C15UL; ulong z = sm; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
        s0 = next(); s1 = next(); s2 = next(); s3 = next();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
    /* Math.random() */
    public static double Random()
    {
        ulong result = Rotl(s1 * 5, 7) * 9, t = s1 << 17;
        s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3; s2 ^= t; s3 = Rotl(s3, 45);
        return (result >> 11) * (1.0 / 9007199254740992.0);
    }
    /* an override for tests: when set, Math.random() returns this sequence */
    public static Func<double> RandomOverride;
    public static double R() => RandomOverride != null ? RandomOverride() : Random();

    /* ---- int32 ---- */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int I32(double v)
    {
        if (v >= -2147483648.0 && v <= 2147483647.0) return (int)v;
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
        double m = Math.Truncate(v) % 4294967296.0; if (m < 0) m += 4294967296.0;
        return (int)(uint)m;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint U32(double v) => (uint)I32(v);
    public static int Imul(int a, int b) => unchecked(a * b);

    /* ---- truthiness ---- */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool T(double v) => v != 0 && !double.IsNaN(v);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool T(string s) => !string.IsNullOrEmpty(s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool T(object o) => o != null;
    /* a || b for numbers: the first when it is truthy */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Or(double a, double b) => (a != 0 && !double.IsNaN(a)) ? a : b;
    public static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;

    /* ---- numbers as text ---- */
    /* String(n) / `${n}` */
    public static string Num(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsPositiveInfinity(v)) return "Infinity";
        if (double.IsNegativeInfinity(v)) return "-Infinity";
        if (v == 0) return "0";
        if (v == Math.Floor(v) && Math.Abs(v) < 1e21) return ((long)v).ToString(CultureInfo.InvariantCulture) is var s && Math.Abs(v) < 9e18 ? s : v.ToString("F0", CultureInfo.InvariantCulture);
        double av = Math.Abs(v);
        string r = v.ToString("R", CultureInfo.InvariantCulture);
        if (av >= 1e21 || av < 1e-6)
        {
            /* JS exponent form: 1e+21, 1.5e-7 */
            r = v.ToString("0.#################e+0", CultureInfo.InvariantCulture);
            return r;
        }
        if (r.Contains('E'))
        {
            /* .NET picked exponent form for a value JS writes in full */
            r = ((decimal)v).ToString(CultureInfo.InvariantCulture);
        }
        return r;
    }
    public static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);
    public static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
    /* n.toFixed(d) */
    public static string ToFixed(double v, int d)
    {
        if (double.IsNaN(v)) return "NaN";
        if (Math.Abs(v) >= 1e21) return Num(v);
        if (v == 0) v = 0; /* -0 prints as 0 */
        return v.ToString("F" + d, CultureInfo.InvariantCulture);
    }
    /* n.toLocaleString() for the en-US grouping the game shows */
    public static string Locale(double v) => v.ToString("#,0.###", CultureInfo.InvariantCulture);
    /* parseFloat / parseInt / +s */
    public static double ParseFloat(string s)
    {
        if (s == null) return double.NaN;
        s = s.Trim();
        int i = 0; var sb = new StringBuilder();
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) sb.Append(s[i++]);
        bool dot = false, any = false;
        while (i < s.Length && (char.IsDigit(s[i]) || (!dot && s[i] == '.'))) { if (s[i] == '.') dot = true; else any = true; sb.Append(s[i++]); }
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E') && any)
        {
            int j = i + 1; var e = new StringBuilder("e");
            if (j < s.Length && (s[j] == '-' || s[j] == '+')) e.Append(s[j++]);
            int k = j; while (j < s.Length && char.IsDigit(s[j])) e.Append(s[j++]);
            if (j > k) sb.Append(e);
        }
        if (!any) return double.NaN;
        return double.Parse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    public static double ParseInt(string s, int radix = 10)
    {
        if (s == null) return double.NaN;
        s = s.Trim(); if (s.Length == 0) return double.NaN;
        bool neg = false; int i = 0;
        if (s[0] == '-' || s[0] == '+') { neg = s[0] == '-'; i = 1; }
        double v = 0; bool any = false;
        for (; i < s.Length; i++)
        {
            int dgt = char.IsDigit(s[i]) ? s[i] - '0' : char.IsLetter(s[i]) ? (char.ToLowerInvariant(s[i]) - 'a' + 10) : 99;
            if (dgt >= radix) break;
            v = v * radix + dgt; any = true;
        }
        return any ? (neg ? -v : v) : double.NaN;
    }
    /* +s, Number(s) */
    public static double ToNumber(string s)
    {
        if (s == null) return 0;
        s = s.Trim();
        if (s.Length == 0) return 0;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        if (s == "Infinity") return double.PositiveInfinity;
        if (s == "-Infinity") return double.NegativeInfinity;
        return double.NaN;
    }
    public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    /* String.prototype.padStart */
    public static string PadStart(string s, int n, char c = ' ') => s.Length >= n ? s : new string(c, n - s.Length) + s;
}
