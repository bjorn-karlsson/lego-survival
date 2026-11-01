global using Math = Brickblade.JsMath;

namespace Brickblade;

/* THE JS'S Math, under C#'s name. Every `Math.` in the port means this class
   (see the global using above), so the code reads as the JS did and behaves as
   it did. The one real difference is ROUNDING: C#'s Math.Round sends halves to
   the even neighbour (2.5 -> 2), the JS's Math.round sends them up (2.5 -> 3,
   -2.5 -> -2). A ported Math.Round is the JS's. Everything else forwards. */
public static class JsMath
{
    public const double PI = System.Math.PI, E = System.Math.E;
    /* Math.round: the nearest integer, halves toward +infinity */
    public static double Round(double x)
    {
        if (!double.IsFinite(x)) return x;
        double r = System.Math.Floor(x);
        if (x - r >= 0.5) r += 1;
        return r;
    }
    public static int Max(int a, int b) => System.Math.Max(a, b);
    public static long Max(long a, long b) => System.Math.Max(a, b);
    public static float Max(float a, float b) => System.Math.Max(a, b);
    public static double Max(double a, double b) => System.Math.Max(a, b);
    public static int Min(int a, int b) => System.Math.Min(a, b);
    public static long Min(long a, long b) => System.Math.Min(a, b);
    public static float Min(float a, float b) => System.Math.Min(a, b);
    public static double Min(double a, double b) => System.Math.Min(a, b);
    public static int Abs(int a) => System.Math.Abs(a);
    public static float Abs(float a) => System.Math.Abs(a);
    public static double Abs(double a) => System.Math.Abs(a);
    public static int Clamp(int v, int lo, int hi) => System.Math.Clamp(v, lo, hi);
    public static float Clamp(float v, float lo, float hi) => System.Math.Clamp(v, lo, hi);
    public static double Clamp(double v, double lo, double hi) => System.Math.Clamp(v, lo, hi);
    public static int Sign(double a) => System.Math.Sign(a);
    public static int Sign(int a) => System.Math.Sign(a);
    public static double Floor(double a) => System.Math.Floor(a);
    public static double Ceiling(double a) => System.Math.Ceiling(a);
    public static double Truncate(double a) => System.Math.Truncate(a);
    public static double Sqrt(double a) => System.Math.Sqrt(a);
    public static double Pow(double a, double b) => System.Math.Pow(a, b);
    public static double Exp(double a) => System.Math.Exp(a);
    public static double Log(double a) => System.Math.Log(a);
    public static double Log2(double a) => System.Math.Log2(a);
    public static double Log10(double a) => System.Math.Log10(a);
    public static double Sin(double a) => System.Math.Sin(a);
    public static double Cos(double a) => System.Math.Cos(a);
    public static double Tan(double a) => System.Math.Tan(a);
    public static double Tanh(double a) => System.Math.Tanh(a);
    public static double Asin(double a) => System.Math.Asin(a);
    public static double Acos(double a) => System.Math.Acos(a);
    public static double Atan(double a) => System.Math.Atan(a);
    public static double Atan2(double y, double x) => System.Math.Atan2(y, x);
    public static double Cbrt(double a) => System.Math.Cbrt(a);
    /* JS's Math.min/max take any number of arguments */
    public static double Min(double a, double b, double c, params double[] more) { double m = System.Math.Min(System.Math.Min(a, b), c); foreach (var x in more) m = System.Math.Min(m, x); return m; }
    public static double Max(double a, double b, double c, params double[] more) { double m = System.Math.Max(System.Math.Max(a, b), c); foreach (var x in more) m = System.Math.Max(m, x); return m; }
    /* ...and under their JS names, so translated code reads as it did */
    public static double round(double x) => Round(x);
    public static double floor(double x) => System.Math.Floor(x);
    public static double ceil(double x) => System.Math.Ceiling(x);
    public static double trunc(double x) => System.Math.Truncate(x);
    public static double abs(double x) => System.Math.Abs(x);
    public static double sqrt(double x) => System.Math.Sqrt(x);
    public static double cbrt(double x) => System.Math.Cbrt(x);
    public static double pow(double a, double b) => System.Math.Pow(a, b);
    public static double exp(double x) => System.Math.Exp(x);
    public static double log(double x) => System.Math.Log(x);
    public static double log2(double x) => System.Math.Log2(x);
    public static double log10(double x) => System.Math.Log10(x);
    public static double sin(double x) => System.Math.Sin(x);
    public static double cos(double x) => System.Math.Cos(x);
    public static double tan(double x) => System.Math.Tan(x);
    public static double tanh(double x) => System.Math.Tanh(x);
    public static double atan(double x) => System.Math.Atan(x);
    public static double acos(double x) => System.Math.Acos(x);
    public static double asin(double x) => System.Math.Asin(x);
    public static double atan2(double y, double x) => System.Math.Atan2(y, x);
    public static double sign(double x) => double.IsNaN(x) ? double.NaN : System.Math.Sign(x);
    public static double hypot(double a, double b) => System.Math.Sqrt(a * a + b * b);
    public static double hypot(double a, double b, double c) => System.Math.Sqrt(a * a + b * b + c * c);
    public static double random() => Js.R();
    public static double min(double a, double b) => System.Math.Min(a, b);
    public static double max(double a, double b) => System.Math.Max(a, b);
    public static double min(double a, double b, double c, params double[] more) => Min(a, b, c, more);
    public static double max(double a, double b, double c, params double[] more) => Max(a, b, c, more);
    public static double min(IEnumerable<double> a) { double m = double.PositiveInfinity; foreach (var x in a) m = System.Math.Min(m, x); return m; }
    public static double max(IEnumerable<double> a) { double m = double.NegativeInfinity; foreach (var x in a) m = System.Math.Max(m, x); return m; }
}
