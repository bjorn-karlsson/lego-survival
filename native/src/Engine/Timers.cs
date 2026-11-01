// setTimeout, setInterval and requestAnimationFrame: the browser's event-loop
// timers, run by the main loop once a frame against the wall clock.
namespace Brickblade;

public static partial class G
{
    sealed class Timer { public int id; public double at, every; public Action fn; }
    static readonly List<Timer> timers = new();
    static readonly List<(int id, Action<double> fn)> rafs = new();
    static int timerSeq;
    static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    /* performance.now(): milliseconds since start */
    public static double perfNow() => clock.Elapsed.TotalMilliseconds;
    public static int setTimeout(Action fn, double ms = 0) { var t = new Timer { id = ++timerSeq, at = perfNow() + Math.Max(0, Js.Or(ms, 0)), fn = fn }; timers.Add(t); return t.id; }
    public static int setInterval(Action fn, double ms) { double e = Math.Max(1, Js.Or(ms, 0)); var t = new Timer { id = ++timerSeq, at = perfNow() + e, every = e, fn = fn }; timers.Add(t); return t.id; }
    public static void clearTimeout(double id) { timers.RemoveAll(t => t.id == (int)id); }
    public static void clearInterval(double id) => clearTimeout(id);
    public static int requestAnimationFrame(Action<double> fn) { int id = ++timerSeq; rafs.Add((id, fn)); return id; }
    public static int requestAnimationFrame(Action fn) => requestAnimationFrame(_ => fn());
    public static void cancelAnimationFrame(double id) { rafs.RemoveAll(r => r.id == (int)id); }
    /* the timers that are due, in the order they fall due */
    public static void RunTimers()
    {
        double now = perfNow();
        for (int guard = 0; guard < 1000; guard++)
        {
            Timer due = null;
            foreach (var t in timers) if (t.at <= now && (due == null || t.at < due.at || t.at == due.at && t.id < due.id)) due = t;
            if (due == null) break;
            if (due.every > 0) due.at += due.every * Math.Max(1, Math.Ceiling((now - due.at) / due.every)); else timers.Remove(due);
            try { due.fn(); } catch (Exception e) { Dom.Report(e); }
        }
    }
    /* requestAnimationFrame callbacks, once, before the frame is drawn */
    public static void RunFrameCallbacks()
    {
        if (rafs.Count == 0) return;
        var now = perfNow();
        var list = rafs.ToList(); rafs.Clear();
        foreach (var (_, fn) in list) { try { fn(now); } catch (Exception e) { Dom.Report(e); } }
    }
    /* the DOM, by the names the script uses */
    public static Document document => Dom.document;
    public static Win window => Dom.window;
}
