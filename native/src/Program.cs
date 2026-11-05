using System.Globalization;
using System.Text;
using Raylib_cs;

namespace Brickblade;

public static class Program
{
    public static string[] Args = Array.Empty<string>();
    public static bool Has(string flag) => Args.Contains(flag);
    public static string Opt(string name) { int i = Array.IndexOf(Args, name); return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null; }

    [STAThread]
    public static int Main(string[] args)
    {
        Args = args;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        LogInit();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject as Exception);
        if (Has("--shimtest")) return ShimTest.Run();
        if (Opt("--audiotest") is string adir) return AudioTest.Run(adir);
        if (Opt("--audioprobe") is string pdir) return AudioProbes.Run(pdir);
        try { return App.Run(); }
        catch (Exception e) { Crash(e); return 1; }
    }

    /* A WINDOWS GAME HAS NO CONSOLE. What the browser's console would have shown
       (a frame that threw, a screen that failed) goes to brickblade.log beside the
       save, fresh each start and capped so a fault repeating every frame cannot
       fill the disk. Run from a terminal, it goes to the terminal as well. */
    static string logPath;
    static void LogInit()
    {
        try
        {
            Directory.CreateDirectory(Store.Dir);
            logPath = Path.Combine(Store.Dir, "brickblade.log");
            var file = new CappedWriter(new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true }, 2_000_000);
            var tee = new TeeWriter(Console.Error, file);
            Console.SetError(tee);
            Console.Error.WriteLine("BRICKBLADE " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · " + Environment.OSVersion + " · .NET " + Environment.Version);
        }
        catch { }
    }
    static void Crash(Exception e)
    {
        try
        {
            string msg = "CRASH " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + e;
            Console.Error.WriteLine(msg);
            File.WriteAllText(Path.Combine(Store.Dir, "crash.log"), msg);
        }
        catch { }
    }
    sealed class TeeWriter : TextWriter
    {
        readonly TextWriter a, b;
        public TeeWriter(TextWriter a, TextWriter b) { this.a = a; this.b = b; }
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char c) { try { a.Write(c); } catch { } try { b.Write(c); } catch { } }
        public override void Write(string s) { try { a.Write(s); } catch { } try { b.Write(s); } catch { } }
        public override void WriteLine(string s) { try { a.WriteLine(s); } catch { } try { b.WriteLine(s); } catch { } }
        public override void Flush() { try { a.Flush(); } catch { } try { b.Flush(); } catch { } }
    }
    sealed class CappedWriter : TextWriter
    {
        readonly TextWriter w; long left;
        public CappedWriter(TextWriter w, long cap) { this.w = w; left = cap; }
        public override Encoding Encoding => Encoding.UTF8;
        bool Room(int n) { if (left <= 0) return false; left -= n; if (left <= 0) { w.WriteLine("\n[log full: later lines dropped]"); return false; } return true; }
        public override void Write(char c) { if (Room(1)) w.Write(c); }
        public override void Write(string s) { if (s != null && Room(s.Length)) w.Write(s); }
        public override void WriteLine(string s) { if (Room((s?.Length ?? 0) + 1)) w.WriteLine(s); }
        public override void Flush() => w.Flush();
    }
}
