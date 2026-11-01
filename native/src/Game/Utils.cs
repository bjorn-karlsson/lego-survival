using System.Text.RegularExpressions;

namespace Brickblade;

/* =============================== UTILS (JS 1014–1084) =============================== */
public static partial class G
{
    public const double TAU = Math.PI * 2;
    /* rnd(a) is 0..a; rnd(a, b) is a..b */
    public static double rnd(double a = 1) => Js.R() * a;
    public static double rnd(double a, double b) => a + Js.R() * (b - a);
    public static int irnd(double a, double b) => (int)Math.Floor(rnd(a, b + 1));
    public static double clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static double lerp(double a, double b, double t) => a + (b - a) * t;

    /* A NEGATIVE NUMBER TAKES ITS OWN WORD: "-25% increased" reads "25% reduced",
       "-20% more" reads "20% less". Done once, where text leaves for the screen. */
    public static string caseAs(string w, string to)
    {
        if (w == w.ToUpperInvariant()) return to.ToUpperInvariant();
        if (w[0] == char.ToUpperInvariant(w[0])) return char.ToUpperInvariant(to[0]) + to.Substring(1);
        return to;
    }
    public static readonly Regex SIGN_WORD_INC = new(@"(?<![\d.])\+?[−-]\s?(\d+(?:\.\d+)?)%(\s+)(increased|Increased|INCREASED)\b", RegexOptions.Compiled);
    public static readonly Regex SIGN_WORD_MORE = new(@"(?<![\d.])\+?[−-]\s?(\d+(?:\.\d+)?)%(\s+)(more|More|MORE)\b", RegexOptions.Compiled);
    public static string signWords(string t)
    {
        t = t ?? "";
        t = SIGN_WORD_INC.Replace(t, m => m.Groups[1].Value + "%" + m.Groups[2].Value + caseAs(m.Groups[3].Value, "reduced"));
        t = SIGN_WORD_MORE.Replace(t, m => m.Groups[1].Value + "%" + m.Groups[2].Value + caseAs(m.Groups[3].Value, "less"));
        return t;
    }
    static readonly Regex reNegVal = new(@"^\s*[−-]\s?\d"), reIncWord = new(@"\b(increased|Increased|INCREASED)\b"),
        reNegStrip = new(@"^\s*[−-]\s?"), reTimes = new(@"^\s*×\s?(\d*\.?\d+)"), reMoreWord = new(@"\b(more|More|MORE)\b");
    public static string[] signLabel(string label, string value)
    {
        string L = label ?? "", v = value ?? "";
        if (reNegVal.IsMatch(v) && reIncWord.IsMatch(L))
            return new[] { reIncWord.Replace(L, m => caseAs(m.Value, "reduced"), 1), reNegStrip.Replace(v, "", 1) };
        var x = reTimes.Match(v);
        if (x.Success && Js.ToNumber(x.Groups[1].Value) < 1 && reMoreWord.IsMatch(L))
            return new[] { reMoreWord.Replace(L, m => caseAs(m.Value, "less"), 1), v };
        return new[] { L, v };
    }
    public static double dist(Obj a, Obj b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    public static double angTo(Obj a, Obj b) => Math.Atan2(b.y - a.y, b.x - a.x);
    public static double angDiff(double a, double b)
    {
        double d = (a - b) % TAU;
        if (d > Math.PI) d -= TAU;
        if (d < -Math.PI) d += TAU;
        return d;
    }
    public static T pick<T>(IList<T> a) => a[(int)(Js.R() * a.Count)];
    public static double easeOutBack(double x) { const double c = 1.70158; return 1 + (c + 1) * Math.Pow(x - 1, 3) + c * Math.Pow(x - 1, 2); }
    /* the seeded generator the map and the roster use: bit for bit the JS one */
    public static Func<double> mulberry32(double seed)
    {
        int a = Js.I32(seed);
        return () =>
        {
            unchecked
            {
                a = a + 0x6D2B79F5;
                int t = Js.Imul(a ^ (int)((uint)a >> 15), 1 | a);
                t = (t + Js.Imul(t ^ (int)((uint)t >> 7), 61 | t)) ^ t;
                return ((uint)(t ^ (int)((uint)t >> 14))) / 4294967296.0;
            }
        };
    }
    public static double segDist(double x1, double y1, double x2, double y2, double px, double py)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double l2 = dx * dx + dy * dy;
        if (l2 < 0.0001) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = ((px - x1) * dx + (py - y1) * dy) / l2;
        t = clamp(t, 0, 1);
        double ex = px - (x1 + dx * t), ey = py - (y1 + dy * t);
        return Math.Sqrt(ex * ex + ey * ey);
    }
    /* a colour's three channels */
    public static double[] prs(Col h) => new double[] { h.r, h.g, h.b };
    public static Col css(double[] c) => new Col(Js.I32(c[0]), Js.I32(c[1]), Js.I32(c[2]));
    /* toward white (f > 0) or black (f < 0) by |f| */
    public static Col sh(Col c, double f)
    {
        double t = Math.Abs(f), m = f > 0 ? 255 : 0;
        return new Col(Js.I32(c.r + (m - c.r) * t), Js.I32(c.g + (m - c.g) * t), Js.I32(c.b + (m - c.b) * t));
    }
    public static Col mixc(Col a, Col b, double t) =>
        new Col(Js.I32(lerp(a.r, b.r, t)), Js.I32(lerp(a.g, b.g, t)), Js.I32(lerp(a.b, b.b, t)));
    /* `rgba(${r},${g},${b},${a})` without the string */
    public static Col rgba(double r, double g, double b, double a) => new Col(r, g, b, a);
    public static Col rgb(double r, double g, double b) => new Col(r, g, b, 1);
    /* Math.hypot, the two-argument case the game uses */
    public static double hypot(double x, double y) => Math.Sqrt(x * x + y * y);
    public static double hypot(double x, double y, double z) => Math.Sqrt(x * x + y * y + z * z);
    public static string N(double v) => Js.Num(v);
    public static string F(double v, int d) => Js.ToFixed(v, d);
}
