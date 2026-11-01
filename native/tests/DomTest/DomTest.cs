using Raylib_cs;
using System.Globalization;

namespace Brickblade;

public static class App { public static bool Focused = true; }

public static class Program
{
    public static string[] Args = Array.Empty<string>();
    public static bool Has(string f) => Args.Contains(f);
    /* --html FILE (the game's page) --out DIR --w W --h H [--show id ...] */
    public static int Main(string[] args)
    {
        Args = args;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string Opt(string k, string d) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }
        string html = File.ReadAllText(Opt("--html", "../../../lego-survival.html"));
        int W = int.Parse(Opt("--w", "1280")), H = int.Parse(Opt("--h", "800"));
        string outDir = Opt("--out", "out");
        Directory.CreateDirectory(outDir);
        int s0 = html.IndexOf("<style>") + 7, s1 = html.IndexOf("</style>");
        string css = html.Substring(s0, s1 - s0) + (args.Contains("--norot") ? "\n.boxplate{transform:none !important}" : "");
        int b0 = html.IndexOf("<body>") + 6, b1 = html.IndexOf("<script", b0);
        string body = html.Substring(b0, b1 - b0);
        Raylib.SetTraceLogLevel(TraceLogLevel.Error);
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(W, H, "domtest");
        Gpu.Init(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
        Fonts.Init();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Dom.Init(css, body);
        Dom.Resize(W, H, 1);
        Console.WriteLine($"init {sw.ElapsedMilliseconds} ms");
        foreach (var sel in (Opt("--probe", "") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            Dom.EnsureLayout();
            var e = Dom.document.querySelector(sel);
            if (e == null) { Console.WriteLine(sel + ": none"); continue; }
            var r = e.getBoundingClientRect();
            Console.WriteLine($"{sel}: bg={e.cs.bg} shadow={e.cs.boxShadow?.Count} rect={r.x:F1},{r.y:F1} {r.width:F1}x{r.height:F1} spec.bg={e.cs.spec.GetValueOrDefault("background-color")} disp={e.cs.display} font={e.cs.fontSize} {e.cs.family}");
        }
        /* the scenes: each a script of DOM calls, then a picture */
        var scenes = new List<(string name, Action setup)>
        {
            ("title", () => { }),
            ("pause", () => { Show("pauseScreen"); }),
            ("options", () => { Hide("pauseScreen"); Show("optScreen"); }),
            ("over", () => { Hide("optScreen"); Show("overScreen"); }),
        };
        var c = new Ctx(null);
        foreach (var (name, setup) in scenes)
        {
            setup();
            sw.Restart(); Dom.EnsureLayout(); Console.WriteLine($"{name}: layout {sw.Elapsed.TotalMilliseconds:F1} ms");
            for (int f = 0; f < 2; f++)
            {
                Raylib.BeginDrawing();
                Gpu.ClearScreen(new Col(16, 21, 26, 1));
                c.Reset();
                sw.Restart();
                Dom.Paint(c, f * 0.016);
                Gpu.Flush();
                if (f == 1) Console.WriteLine($"{name}: paint {sw.Elapsed.TotalMilliseconds:F1} ms, calls {Gpu.DrawCalls}");
                Gpu.EndFrame(); MaskPool.EndFrame();
                if (f == 1) { Shot.Save(Path.Combine(outDir, name + ".png")); File.WriteAllText(Path.Combine(outDir, name + ".txt"), Dump()); }
                Raylib.EndDrawing();
                Gpu.DrawCalls = 0;
            }
        }
        foreach (var e in Dom.errors) Console.WriteLine(e);
        Raylib.CloseWindow();
        return 0;
    }
    static string Dump()
    {
        var sb = new System.Text.StringBuilder();
        void Walk(Element e, string path)
        {
            foreach (var c in e.children)
            {
                string k = path + "/" + c.localName + (c.id.Length > 0 ? "#" + c.id : "") + (c.className.Trim().Length > 0 ? "." + string.Join(".", c.className.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)) : "");
                var r = c.getBoundingClientRect();
                if (r.width != 0 || r.height != 0) sb.Append(k).Append(' ').Append(string.Join(" ", new[] { r.x, r.y, r.width, r.height }.Select(v => v.ToString("F1")))).Append('\n');
                Walk(c, k);
            }
        }
        Walk(Dom.document.body, "body");
        return sb.ToString();
    }
    static void Show(string id) => Dom.document.getElementById(id)?.classList.remove("hidden");
    static void Hide(string id) => Dom.document.getElementById(id)?.classList.add("hidden");
}
