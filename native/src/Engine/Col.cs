using System.Globalization;

namespace Brickblade;

/* A colour the way the canvas takes one: anything that was a CSS string in the
   JS ('#d3242b', 'rgba(20,30,40,0.55)', 'rgb(1,2,3)') converts implicitly, and
   rgba()/rgb() build one without going through a string at all. Channels are
   0..255, alpha 0..1, straight (not premultiplied) -- the renderer premultiplies. */
public struct Col : IEquatable<Col>
{
    public float r, g, b, a;
    public Col(double r, double g, double b, double a = 1) { this.r = (float)r; this.g = (float)g; this.b = (float)b; this.a = (float)a; }

    static readonly Dictionary<string, Col> cache = new();
    public static Col Parse(string s)
    {
        if (s == null) return new Col(0, 0, 0, 0);
        lock (cache)
        {
            if (cache.TryGetValue(s, out var c)) return c;
            c = ParseRaw(s.Trim());
            if (cache.Count > 50000) cache.Clear();
            cache[s] = c;
            return c;
        }
    }
    static int Hex(char ch) => ch <= '9' ? ch - '0' : (ch | 32) - 'a' + 10;
    static Col ParseRaw(string s)
    {
        if (s.Length > 0 && s[0] == '#')
        {
            if (s.Length == 4) return new Col(Hex(s[1]) * 17, Hex(s[2]) * 17, Hex(s[3]) * 17);
            if (s.Length == 5) return new Col(Hex(s[1]) * 17, Hex(s[2]) * 17, Hex(s[3]) * 17, Hex(s[4]) * 17 / 255.0);
            if (s.Length >= 7)
            {
                var c = new Col(Hex(s[1]) * 16 + Hex(s[2]), Hex(s[3]) * 16 + Hex(s[4]), Hex(s[5]) * 16 + Hex(s[6]));
                if (s.Length >= 9) c.a = (Hex(s[7]) * 16 + Hex(s[8])) / 255f;
                return c;
            }
            return new Col(0, 0, 0, 1);
        }
        int p = s.IndexOf('(');
        if (p > 0)
        {
            string fn = s.Substring(0, p).Trim().ToLowerInvariant();
            string body = s.Substring(p + 1).TrimEnd(')', ' ');
            var parts = body.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            double N(int i, double scale)
            {
                if (i >= parts.Length) return 0;
                var t = parts[i];
                if (t.EndsWith("%")) return double.Parse(t.TrimEnd('%'), CultureInfo.InvariantCulture) / 100.0 * scale;
                return double.Parse(t, CultureInfo.InvariantCulture);
            }
            if (fn == "rgb" || fn == "rgba")
                return new Col(N(0, 255), N(1, 255), N(2, 255), parts.Length > 3 ? N(3, 1) : 1);
            if (fn == "hsl" || fn == "hsla")
            {
                double h = N(0, 1), sat = N(1, 1), l = N(2, 1);
                if (parts.Length > 1 && parts[1].EndsWith("%")) { } else { sat /= 100; l /= 100; }
                double a = parts.Length > 3 ? N(3, 1) : 1;
                return FromHsl(h, sat, l, a);
            }
        }
        switch (s.ToLowerInvariant())
        {
            case "white": return new Col(255, 255, 255);
            case "black": return new Col(0, 0, 0);
            case "red": return new Col(255, 0, 0);
            case "transparent": return new Col(0, 0, 0, 0);
            case "gold": return new Col(255, 215, 0);
            case "yellow": return new Col(255, 255, 0);
            case "orange": return new Col(255, 165, 0);
            case "gray": case "grey": return new Col(128, 128, 128);
        }
        return new Col(0, 0, 0, 1);
    }
    public static Col FromHsl(double h, double s, double l, double a)
    {
        h = ((h % 360) + 360) % 360 / 360.0;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        double f(double t) { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6.0) return p + (q - p) * 6 * t; if (t < 0.5) return q; if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6; return p; }
        return new Col(f(h + 1 / 3.0) * 255, f(h) * 255, f(h - 1 / 3.0) * 255, a);
    }
    public static implicit operator Col(string s) => Parse(s);
    public Col WithAlpha(double na) { var c = this; c.a = (float)na; return c; }
    public bool Equals(Col o) => r == o.r && g == o.g && b == o.b && a == o.a;
    public override bool Equals(object o) => o is Col c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(r, g, b, a);
    public static bool operator ==(Col x, Col y) => x.Equals(y);
    public static bool operator !=(Col x, Col y) => !x.Equals(y);
    /* the CSS text, for the places the JS keeps a colour inside a string (HTML) */
    public override string ToString() => a >= 1 ? $"rgb({(int)r},{(int)g},{(int)b})" : $"rgba({(int)r},{(int)g},{(int)b},{Js.Num(a)})";
}

/* what a fill or a stroke paints with: a colour or a gradient */
public struct Paint
{
    public Col col; public Grad grad;
    public static implicit operator Paint(string s) => new Paint { col = Col.Parse(s) };
    public static implicit operator Paint(Col c) => new Paint { col = c };
    public static implicit operator Paint(Grad g) => new Paint { grad = g };
}

public sealed class Grad
{
    public bool radial;
    public double x0, y0, r0, x1, y1, r1;
    public readonly List<(double t, Col c)> stops = new();
    public void addColorStop(double t, Col c)
    {
        int i = stops.Count;
        while (i > 0 && stops[i - 1].t > t) i--;
        stops.Insert(i, (Math.Clamp(t, 0, 1), c));
    }
}
