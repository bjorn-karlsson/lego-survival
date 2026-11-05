using System.Text.Json;

namespace Brickblade;

/* localStorage, as a JSON file beside the player's other app data
   (%APPDATA%\Brickblade\storage.json on Windows). Same contract: string keys,
   string values, getItem returns null when absent. Every write goes to disk
   (through a flushed temp file, so a crash mid-write never loses the save, and
   the save before it is kept as storage.json.bak). */
public static class Store
{
    static Dictionary<string, string> map;
    static string path;
    public static bool Disabled;      /* a test can run with no storage at all */
    public static string Dir
    {
        get
        {
            string root = Environment.GetEnvironmentVariable("BRICKBLADE_DATA");
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Brickblade");
            return root;
        }
    }
    static void Load()
    {
        if (map != null) return;
        map = new Dictionary<string, string>();
        try
        {
            Directory.CreateDirectory(Dir);
            path = Path.Combine(Dir, "storage.json");
            /* the save, or (when it cannot be read: a power cut, a disk error) the
               one before it; a file that cannot be read is kept aside, never
               overwritten with an empty save */
            foreach (var f in new[] { path, path + ".bak" })
            {
                if (!File.Exists(f)) continue;
                try
                {
                    var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f));
                    if (d == null) throw new InvalidDataException("empty");
                    map = d;
                    if (f != path) Console.Error.WriteLine("save: storage.json could not be read; restored the previous save from storage.json.bak");
                    break;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"save: {Path.GetFileName(f)} could not be read ({e.Message})");
                    if (f == path) try { File.Copy(path, path + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
                }
            }
        }
        catch { }
    }
    /* THE WRITE IS OFF THE GAME'S THREAD: a save is a snapshot taken at once (a
       few KB of JSON) and handed to a writer that keeps only the newest one, so
       a flush to a slow disk never costs a frame. Flush() writes what is still
       waiting, at exit and on a crash. */
    static byte[] pending;
    static readonly object pendLock = new(), ioLock = new();
    static readonly AutoResetEvent wake = new(false);
    static Thread writer;
    static bool saveWarned;
    static void Save()
    {
        if (path == null) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(map);
        lock (pendLock) pending = bytes;
        if (writer == null)
        {
            writer = new Thread(() => { while (true) { wake.WaitOne(); Flush(); } }) { IsBackground = true, Name = "save writer" };
            writer.Start();
        }
        wake.Set();
    }
    public static void Flush()
    {
        lock (ioLock)
        {
            byte[] b; lock (pendLock) { b = pending; pending = null; }
            if (b == null || path == null) return;
            try
            {
                /* written in full and flushed to the disk before it takes the
                   save's place; the save it replaces stays as storage.json.bak */
                string tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(b);
                    fs.Flush(true);
                }
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, path + ".bak", true); }
                    catch (IOException) { File.Copy(path, path + ".bak", true); File.Move(tmp, path, true); }
                }
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                if (!saveWarned) { saveWarned = true; Console.Error.WriteLine("save: could not write " + path + " (" + e.Message + ")"); }
            }
        }
    }
    public static string getItem(string k) { if (Disabled) return null; Load(); return map.TryGetValue(k, out var v) ? v : null; }
    public static void setItem(string k, string v) { if (Disabled) return; Load(); map[k] = v ?? "null"; Save(); }
    public static void removeItem(string k) { if (Disabled) return; Load(); if (map.Remove(k)) Save(); }
    public static IEnumerable<string> keys() { Load(); return map.Keys.ToList(); }
    public static void clear() { Load(); map.Clear(); Save(); }
    /* tests: forget the file and start empty, in memory */
    public static void Reset(bool inMemory)
    {
        map = new Dictionary<string, string>();
        if (inMemory) path = null;
    }
}
