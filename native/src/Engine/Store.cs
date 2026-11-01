using System.Text.Json;

namespace Brickblade;

/* localStorage, as a JSON file beside the player's other app data
   (%APPDATA%\Brickblade\storage.json on Windows). Same contract: string keys,
   string values, getItem returns null when absent. Every write goes to disk
   (through a temp file, so a crash mid-write never loses the save). */
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
            if (File.Exists(path))
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (d != null) map = d;
            }
        }
        catch { }
    }
    static void Save()
    {
        if (path == null) return;
        try
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            File.Move(tmp, path, true);
        }
        catch { }
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
