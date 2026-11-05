using System.Reflection;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace Brickblade;

/* A font file: its bytes, the metrics a browser sizes text by (units per em,
   ascender, descender) and which characters it actually has. */
public sealed class Face
{
    public string name;
    public byte[] data;
    public int upm = 1000, asc = 800, desc = -200, gap = 0;
    public HashSet<int> cmap = new();
    /* each character's glyph and the glyphs' advance widths (font units): exact, as a browser measures */
    public readonly Dictionary<int, int> gids = new();
    ushort[] adv = Array.Empty<ushort>();
    public double Adv(int cp)
    {
        if (!gids.TryGetValue(cp, out int g) || adv.Length == 0) return -1;
        return adv[Math.Min(g, adv.Length - 1)];
    }
    public readonly Dictionary<int, Atlas> atlases = new();
    /* KERNING, as the browser applies it to canvas and page text: the font's own
       pair adjustments (GPOS 'kern', both pair formats), in font units, for the
       characters the game draws */
    readonly Dictionary<(int, int), short> kern = new();
    public bool HasKern => kern.Count > 0;
    public double Kern(int cpA, int cpB)
    {
        if (kern.Count == 0 || !gids.TryGetValue(cpA, out int a) || !gids.TryGetValue(cpB, out int b)) return 0;
        return kern.TryGetValue((a, b), out var k) ? k : 0;
    }

    public Face(string name, byte[] data)
    {
        this.name = name; this.data = data;
        try { ReadTables(); } catch { }
    }
    static int U16(byte[] d, int o) => (d[o] << 8) | d[o + 1];
    static short S16(byte[] d, int o) => (short)((d[o] << 8) | d[o + 1]);
    static uint U32(byte[] d, int o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
    void ReadTables()
    {
        var d = data;
        int n = U16(d, 4);
        var tables = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            int o = 12 + i * 16;
            string tag = System.Text.Encoding.ASCII.GetString(d, o, 4);
            tables[tag] = (int)U32(d, o + 8);
        }
        if (tables.TryGetValue("head", out int head)) upm = U16(d, head + 18);
        gposAt = tables.TryGetValue("GPOS", out int gp) ? gp : -1;
        int nHM = 0;
        if (tables.TryGetValue("hhea", out int hhea)) { asc = S16(d, hhea + 4); desc = S16(d, hhea + 6); gap = S16(d, hhea + 8); nHM = U16(d, hhea + 34); }
        if (nHM > 0 && tables.TryGetValue("hmtx", out int hmtx))
        {
            adv = new ushort[nHM];
            for (int i = 0; i < nHM; i++) adv[i] = (ushort)U16(d, hmtx + i * 4);
        }
        if (tables.TryGetValue("cmap", out int cmapOff))
        {
            int nsub = U16(d, cmapOff + 2);
            int best = -1, bestFmt = 0;
            for (int i = 0; i < nsub; i++)
            {
                int pid = U16(d, cmapOff + 4 + i * 8), eid = U16(d, cmapOff + 6 + i * 8);
                int off = (int)U32(d, cmapOff + 8 + i * 8) + cmapOff;
                int fmt = U16(d, off);
                if (pid == 3 && (eid == 1 || eid == 10) || pid == 0)
                {
                    if (fmt == 12 && bestFmt != 12) { best = off; bestFmt = 12; }
                    else if (fmt == 4 && bestFmt == 0) { best = off; bestFmt = 4; }
                }
            }
            if (bestFmt == 4)
            {
                int segX2 = U16(d, best + 6), seg = segX2 / 2;
                int ends = best + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
                for (int s = 0; s < seg; s++)
                {
                    int end = U16(d, ends + s * 2), start = U16(d, starts + s * 2);
                    int delta = S16(d, deltas + s * 2), ro = U16(d, ranges + s * 2);
                    for (int c = start; c <= end && c != 0xFFFF; c++)
                    {
                        int gid;
                        if (ro == 0) gid = (c + delta) & 0xFFFF;
                        else { int go = ranges + s * 2 + ro + (c - start) * 2; gid = U16(d, go); if (gid != 0) gid = (gid + delta) & 0xFFFF; }
                        if (gid != 0) { cmap.Add(c); gids[c] = gid; }
                    }
                }
            }
            else if (bestFmt == 12)
            {
                uint ng = U32(d, best + 12);
                for (uint g = 0; g < ng; g++)
                {
                    int o = best + 16 + (int)g * 12;
                    uint sc = U32(d, o), ec = U32(d, o + 4), sg = U32(d, o + 8);
                    for (uint c = sc; c <= ec && c - sc < 70000; c++) if (sg + (c - sc) != 0) { cmap.Add((int)c); gids[(int)c] = (int)(sg + (c - sc)); }
                }
            }
        }
    }
    int gposAt = -1;
    /* the pairs among these characters that the font kerns */
    public void LoadKern(IEnumerable<int> cps)
    {
        if (gposAt < 0 || kern.Count > 0) return;
        try
        {
            var d = data; int g0 = gposAt;
            var glyphs = cps.Where(gids.ContainsKey).Select(c => gids[c]).Distinct().ToArray();
            if (glyphs.Length == 0) return;
            var gset = new HashSet<int>(glyphs);
            int featList = g0 + U16(d, g0 + 6), lookupList = g0 + U16(d, g0 + 8);
            var lookups = new SortedSet<int>();
            int nf = U16(d, featList);
            for (int i = 0; i < nf; i++)
            {
                string tag = System.Text.Encoding.ASCII.GetString(d, featList + 2 + i * 6, 4);
                if (tag != "kern") continue;
                int ft = featList + U16(d, featList + 6 + i * 6);
                int nl = U16(d, ft + 2);
                for (int k = 0; k < nl; k++) lookups.Add(U16(d, ft + 4 + k * 2));
            }
            int nLook = U16(d, lookupList);
            foreach (int li in lookups)
            {
                if (li >= nLook) continue;
                int lk = lookupList + U16(d, lookupList + 2 + li * 2);
                int type = U16(d, lk), nsub = U16(d, lk + 4);
                /* this lookup's value for each pair: the first subtable that applies wins */
                var done = new HashSet<(int, int)>();
                for (int si = 0; si < nsub; si++)
                {
                    int st = lk + U16(d, lk + 6 + si * 2), stType = type;
                    if (type == 9) { stType = U16(d, st + 2); st += (int)U32(d, st + 4); }
                    if (stType != 2) continue;
                    PairPos(d, st, glyphs, gset, done);
                }
            }
        }
        catch { kern.Clear(); }
    }
    static int VRSize(int fmt) { int n = 0; for (int b = 0; b < 8; b++) if ((fmt & (1 << b)) != 0) n += 2; return n; }
    /* the XAdvance a value record carries (0 if it has none) */
    static int XAdv(byte[] d, int at, int fmt)
    {
        int o = at;
        if ((fmt & 1) != 0) o += 2;
        if ((fmt & 2) != 0) o += 2;
        return (fmt & 4) != 0 ? S16(d, o) : 0;
    }
    static int CoverageIndex(byte[] d, int cov, int g)
    {
        int fmt = U16(d, cov);
        if (fmt == 1)
        {
            int n = U16(d, cov + 2), lo = 0, hi = n - 1;
            while (lo <= hi) { int m = (lo + hi) >> 1, v = U16(d, cov + 4 + m * 2); if (v == g) return m; if (v < g) lo = m + 1; else hi = m - 1; }
            return -1;
        }
        if (fmt == 2)
        {
            int n = U16(d, cov + 2);
            for (int i = 0; i < n; i++) { int r = cov + 4 + i * 6, a = U16(d, r), b = U16(d, r + 2); if (g >= a && g <= b) return U16(d, r + 4) + g - a; }
        }
        return -1;
    }
    static int ClassOf(byte[] d, int cd, int g)
    {
        int fmt = U16(d, cd);
        if (fmt == 1) { int start = U16(d, cd + 2), n = U16(d, cd + 4); return g >= start && g < start + n ? U16(d, cd + 6 + (g - start) * 2) : 0; }
        if (fmt == 2) { int n = U16(d, cd + 2); for (int i = 0; i < n; i++) { int r = cd + 4 + i * 6; if (g >= U16(d, r) && g <= U16(d, r + 2)) return U16(d, r + 4); } }
        return 0;
    }
    void PairPos(byte[] d, int st, int[] glyphs, HashSet<int> gset, HashSet<(int, int)> done)
    {
        int fmt = U16(d, st), cov = st + U16(d, st + 2), vf1 = U16(d, st + 4), vf2 = U16(d, st + 6);
        int s1 = VRSize(vf1), s2 = VRSize(vf2);
        if (fmt == 1)
        {
            int nset = U16(d, st + 8);
            foreach (int a in glyphs)
            {
                int ci = CoverageIndex(d, cov, a);
                if (ci < 0 || ci >= nset) continue;
                int ps = st + U16(d, st + 10 + ci * 2), n = U16(d, ps), rec = 2 + s1 + s2;
                for (int i = 0; i < n; i++)
                {
                    int r = ps + 2 + i * rec, b = U16(d, r);
                    if (!gset.Contains(b) || !done.Add((a, b))) continue;
                    int v = XAdv(d, r + 2, vf1);
                    if (v != 0) kern[(a, b)] = (short)(kern.GetValueOrDefault((a, b)) + v);
                }
            }
        }
        else if (fmt == 2)
        {
            int cd1 = st + U16(d, st + 8), cd2 = st + U16(d, st + 10), c1n = U16(d, st + 12), c2n = U16(d, st + 14);
            int rec = s1 + s2;
            var cls2 = glyphs.ToDictionary(b => b, b => ClassOf(d, cd2, b));
            foreach (int a in glyphs)
            {
                if (CoverageIndex(d, cov, a) < 0) continue;
                int c1 = ClassOf(d, cd1, a); if (c1 >= c1n) continue;
                foreach (int b in glyphs)
                {
                    int c2 = cls2[b]; if (c2 >= c2n || !done.Add((a, b))) continue;
                    int v = XAdv(d, st + 16 + (c1 * c2n + c2) * rec, vf1);
                    if (v != 0) kern[(a, b)] = (short)(kern.GetValueOrDefault((a, b)) + v);
                }
            }
        }
    }
    public bool Has(int cp) => cmap.Count == 0 || cmap.Contains(cp);
    /* size of the font in px of the em box -> raylib's "size" (which is ascent-descent) */
    public float RaylibSize(float emPx) => emPx * (asc - desc) / upm;
    public float Ascent(float emPx) => emPx * asc / upm;
    public float Descent(float emPx) => -emPx * desc / upm;
}

/* one face rasterised at one device size */
public sealed unsafe class Atlas
{
    public Font font;
    public int size;           /* em px on the device */
    public Dictionary<int, int> idx = new();
    public float asc;          /* baseline below the glyph-box top, device px */
    public uint tex;
}

public static unsafe class Fonts
{
    public static readonly Dictionary<string, Face> faces = new();
    public static Face Titan, F400, F500, F600, F700, Sans, SansBold, Mono, Emoji;
    /* every character the game prints, beyond ASCII */
    public static readonly int[] Extra = "—×·→’°−▲…─∏–≈←▼♪²◆✦⇅★✓⚡❄‘“”•✗✕⬆⬇▶◀■□●○♥♦♣♠☠⚔✚✖⚠⏸⌛⌚🔥🗑éèêüöäåñçáíóú€£¥¢¤§©®™±÷¼½¾¿¡«»"
        .EnumerateRunes().Select(r => r.Value).Distinct().ToArray();

    public static byte[] Res(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(name) ?? asm.GetManifestResourceStream(name.Replace('/', '.'));
        if (s == null) throw new FileNotFoundException("embedded resource " + name);
        using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
    }
    public static void Init()
    {
        Face L(string key, string file) { var f = new Face(key, Res("fonts/" + file)); faces[key] = f; return f; }
        Titan = L("titan", "TitanOne.ttf");
        F400 = L("f400", "Fredoka-400.ttf"); F500 = L("f500", "Fredoka-500.ttf");
        F600 = L("f600", "Fredoka-600.ttf"); F700 = L("f700", "Fredoka-700.ttf");
        Sans = L("sans", "DejaVuSans.ttf"); SansBold = L("sansb", "DejaVuSans-Bold.ttf");
        Mono = L("mono", "DejaVuSansMono.ttf"); Emoji = L("emoji", "NotoEmoji.ttf");
        var cps = Enumerable.Range(32, 95).Concat(Extra).ToArray();
        foreach (var f in new[] { Titan, F400, F500, F600, F700, Sans, SansBold }) f.LoadKern(cps);
    }
    public static Face Pick(string family, int weight)
    {
        string f = family.ToLowerInvariant();
        if (f.Contains("titan")) return Titan;
        if (f.Contains("fredoka"))
            return weight >= 650 ? F700 : weight >= 550 ? F600 : weight >= 450 ? F500 : F400;
        if (f.Contains("mono")) return Mono;
        return weight >= 600 ? SansBold : Sans;
    }
    /* the atlas for a face at a device size, made the first time it is asked for */
    public static int AtlasBuilds; public static long AtlasTicks;
    public static Atlas Get(Face face, int size)
    {
        size = Math.Clamp(size, 4, 220);
        if (face.atlases.TryGetValue(size, out var a)) return a;
        var cps = new List<int>();
        for (int c = 32; c < 127; c++) cps.Add(c);
        foreach (var c in Extra) if (face.Has(c)) cps.Add(c);
        if (face == Emoji) { cps.Clear(); foreach (var c in Extra) if (face.Has(c)) cps.Add(c); if (cps.Count == 0) cps.Add(32); }
        var arr = cps.ToArray();
        float rs = face.RaylibSize(size);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        /* what LoadFontFromMemory does, with the atlas premultiplied (the renderer
           blends everything premultiplied) BEFORE it goes to the GPU: reading a texture
           back to fix it stalls the whole frame, and a new size can appear mid-fight */
        var font = new Font();
        int fsize = (int)Math.Round(rs);
        fixed (byte* d = face.data) fixed (int* cp = arr)
        {
            int got = 0;
            font.BaseSize = fsize; font.GlyphPadding = 4;
            font.Glyphs = Raylib.LoadFontData(d, face.data.Length, fsize, cp, arr.Length, FontType.Default, &got);
            font.GlyphCount = got;
            Rectangle* recs = null;
            Image img = Raylib.GenImageFontAtlas(font.Glyphs, &recs, got, fsize, 4, 0);
            font.Recs = recs;
            Raylib.ImageAlphaPremultiply(&img);
            font.Texture = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
        }
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        AtlasBuilds++; AtlasTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        a = new Atlas { font = font, size = size, tex = font.Texture.Id };
        for (int i = 0; i < font.GlyphCount; i++) a.idx[font.Glyphs[i].Value] = i;
        a.asc = (float)Math.Round(rs) * face.asc / (face.asc - face.desc);
        face.atlases[size] = a;
        return a;
    }
    /* which face draws this character: the asked one, else a fallback that has it */
    public static Face For(Face primary, int cp)
    {
        if (cp < 128 || primary.Has(cp)) return primary;
        if (Sans.Has(cp)) return primary == F700 || primary == F600 || primary == Titan ? (SansBold.Has(cp) ? SansBold : Sans) : Sans;
        if (Emoji.Has(cp)) return Emoji;
        return primary;
    }
}

/* a parsed ctx.font string: '700 20px "Titan One"' */
public sealed class FontSpec
{
    public Face face; public double px; public int weight; public string src;
    static readonly Dictionary<string, FontSpec> cache = new();
    public static double TextK = 1;
    public static FontSpec Parse(string s)
    {
        string key = s + "|" + TextK;
        if (cache.TryGetValue(key, out var f)) return f;
        int weight = 400; double px = 10; string fam = "sans-serif";
        var toks = s.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        for (; i < toks.Length; i++)
        {
            var t = toks[i];
            if (t == "bold") weight = 700;
            else if (t == "normal" || t == "italic") { }
            else if (char.IsDigit(t[0]) && !t.Contains("px") && !t.Contains("em")) weight = int.Parse(t);
            else if (t.EndsWith("px")) { px = Js.ParseFloat(t); i++; break; }
            else break;
        }
        if (i < toks.Length) fam = string.Join(" ", toks.Skip(i)).Trim('"', '\'', ' ');
        f = new FontSpec { face = Fonts.Pick(fam, weight), px = px * TextK, weight = weight, src = s };
        cache[key] = f;
        return f;
    }
    public static void ClearCache() { cache.Clear(); ofCache.Clear(); }
    /* a font from computed CSS values (no TEXT_K: the stylesheet scales itself) */
    static readonly Dictionary<(string, int, double), FontSpec> ofCache = new();
    public static FontSpec Of(string family, int weight, double px)
    {
        var k = (family, weight, px);
        if (ofCache.TryGetValue(k, out var f)) return f;
        string fam = family ?? "sans-serif";
        /* the first family in the list this build carries */
        Face face = null;
        foreach (var part in fam.Split(','))
        {
            string n = part.Trim().Trim('"', '\'', ' ').ToLowerInvariant();
            if (n.Contains("titan") || n.Contains("fredoka") || n.Contains("mono") || n == "sans-serif" || n == "cursive" || n == "system-ui" || n == "serif" || n.Contains("dejavu"))
            { face = Fonts.Pick(n == "cursive" ? "titan" : n, weight); break; }
        }
        face ??= Fonts.Pick("sans-serif", weight);
        f = new FontSpec { face = face, px = px, weight = weight, src = weight + " " + Js.Num(px) + "px " + fam };
        ofCache[k] = f;
        return f;
    }
    /* line-height: normal, the way a browser takes it from the font's own metrics */
    /* Chrome rounds the ascent, the descent and the gap to whole pixels before it adds them */
    public double LineNormal => face == null ? Math.Round(px * 1.2) : Math.Round(px * face.asc / face.upm) + Math.Round(-px * face.desc / face.upm) + Math.Round(px * face.gap / face.upm);
    public double Ascent => face == null ? Math.Round(px * 0.8) : Math.Round(px * face.asc / face.upm);
    public double Descent => face == null ? Math.Round(px * 0.2) : Math.Round(-px * face.desc / face.upm);
}
