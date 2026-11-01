using System.Text.Json;
using System.Text.Json.Nodes;

namespace Brickblade;

/* =============================== CANVAS / SETTINGS / TEXT SIZE (JS 3367–3552) =============================== */
public sealed class OptDef
{
    public string id, name, note, unit;
    public object def;
    public string[] choices; public Dictionary<string, string> names;
    public double[] range;
}
public sealed class TextSize { public string name; public double k; }
/* OPT: one record of preferences. Read by name (OPT["autoLoot"]) or through the
   typed properties the game uses most. */
public sealed class OptStore
{
    readonly Dictionary<string, object> v = new();
    public object this[string k] { get => v.GetValueOrDefault(k); set => v[k] = value; }
    public bool dmgNumbers => v.GetValueOrDefault("dmgNumbers") is bool b && b;
    public bool autoLoot => v.GetValueOrDefault("autoLoot") is bool b && b;
    public bool autoLog => v.GetValueOrDefault("autoLog") is bool b && b;
    public string moveMode => v.GetValueOrDefault("moveMode") as string ?? "wasd";
    public string attackBtn => v.GetValueOrDefault("attackBtn") as string ?? "left";
    public double mapAlpha => v.GetValueOrDefault("mapAlpha") is double d ? d : 70;
    public double mapSize => v.GetValueOrDefault("mapSize") is double d ? d : 100;
    public string textSize => v.GetValueOrDefault("textSize") as string ?? "normal";
    public string renderScale => v.GetValueOrDefault("renderScale") as string ?? "auto";
    public bool vsync => !(v.GetValueOrDefault("vsync") is bool b) || b;
}

public static partial class G
{
    public static Ctx ctx;               /* the window's canvas */
    public static double cw = 0, ch = 0, dpr = 1, zoom = 1;
    public static void reclampCam()
    {
        if (!Js.IsFinite(cam.x) || !Js.IsFinite(cam.y)) { cam.x = W.w / 2; cam.y = W.h / 2; return; }
        double viewW = cw / zoom, viewH = ch / zoom;
        cam.x = viewW >= W.w ? W.w / 2 : clamp(cam.x, viewW / 2, W.w - viewW / 2);
        cam.y = viewH >= W.h ? W.h / 2 : clamp(cam.y, viewH / 2, W.h - viewH / 2);
    }
    /* the window's size in CSS pixels and its pixel density (the render scale
       option lowers the density the world is drawn at) */
    public static void resize(double winW, double winH, double density)
    {
        dpr = Math.Min(density, 2);
        cw = Math.Max(1, winW); ch = Math.Max(1, winH);
        zoom = clamp(Math.Min(cw / 1350, ch / 900), 0.9, 1.9);
        zoom = Math.Max(zoom, Math.Max(cw / (W.w - 20), ch / (W.h - 20)));
        zoom = Math.Min(zoom, 3);
        reclampCam();
    }
    /* THE TITLE FITS THE WINDOW: the native title screen lays itself out to the
       window each frame (Ui), which is what fitTitle did with CSS zoom */
    public const double TITLE_FIT_PAD = 40, TITLE_FIT_MIN = 0.5, TITLE_ROW_MIN = 0.6;
    public static void fitTitle() => Ui.TitleDirty();
    public static bool titleFitQ = false;
    public static void fitTitleSoon() => Ui.TitleDirty();

    /* ============================== SETTINGS ============================== */
    public const string OPT_KEY = "bb_opts";
    public static readonly OptDef[] OPT_DEFS =
    {
        new OptDef { id = "dmgNumbers", def = true, name = "DAMAGE NUMBERS",
            note = "every number and word that floats off a hit: yours, the ones you take, your minions', " +
                   "and MISS, BLOCKED, PERFECT!, SCORCHED! and the rest. Off is a much quieter screen in a wave " +
                   "of sixty — the combat log still counts every point" },
        new OptDef { id = "autoLoot", def = false, name = "AUTO-LOOT",
            note = "every reward opens the moment you earn it. Off, rewards STACK UP instead and wait for " +
                   "you: press R once and they open one after another, oldest first, until none are left" },
        new OptDef { id = "autoLog", def = false, name = "START WITH THE COMBAT LOG",
            note = "open the log as soon as a run begins, instead of pressing L every time. SHIFT+L still walks the tabs" },
        new OptDef { id = "moveMode", def = "wasd", name = "MOVEMENT", choices = new[] { "wasd", "click" }, names = new() { ["wasd"] = "WASD", ["click"] = "POINT & CLICK" },
            note = "WASD: the keys walk and the mouse aims. POINT & CLICK: click the ground to walk there (hold to " +
                   "follow the cursor), click a monster to walk into range and attack it (hold to keep attacking), " +
                   "SHIFT+click to attack at the cursor without stopping your walk. SPACE still rolls" },
        new OptDef { id = "attackBtn", def = "left", name = "ATTACK BUTTON", choices = new[] { "left", "right" }, names = new() { ["left"] = "LEFT CLICK", ["right"] = "RIGHT CLICK" },
            note = "which mouse button uses your weapon. With POINT & CLICK on the left, the left button walks and " +
                   "attacks; on the right, the left button only ever walks and the right attacks at the cursor while " +
                   "you keep walking — hold both to walk and fight at once. Standing still, a right-click on a " +
                   "monster walks up to it and hits it" },
        new OptDef { id = "mapAlpha", def = 70.0, range = new double[] { 20, 100, 5 }, unit = "%", name = "MAP OPACITY",
            note = "how solid the map is when TAB lays it over the field. Low, and you fight straight through it" },
        new OptDef { id = "mapSize", def = 100.0, range = new double[] { 50, 200, 10 }, unit = "%", name = "MAP SIZE",
            note = "how big TAB draws the map. It is always centred on you, so the dot that is you sits on your hero" },
        new OptDef { id = "textSize", def = "normal", name = "TEXT SIZE", choices = new[] { "small", "normal", "large", "xlarge" },
            note = "every word in the menus, the sheets, the cards and the log, and the text drawn in the " +
                   "field -- the HUD, damage numbers, names over monsters" },
        /* THE NATIVE BUILD'S OWN: how sharp the field is drawn, against how fast */
        new OptDef { id = "renderScale", def = "auto", name = "RENDER QUALITY", choices = new[] { "auto", "100", "75", "50" },
            names = new() { ["auto"] = "AUTO", ["100"] = "FULL", ["75"] = "75%", ["50"] = "50%" },
            note = "how many pixels the field is drawn with. FULL is your screen's own resolution; 75% and 50% draw " +
                   "it smaller and scale it up, which is much lighter on a slow graphics card or a 4K screen. AUTO " +
                   "starts at full and steps down by itself when frames start to drop" },
        new OptDef { id = "vsync", def = true, name = "VSYNC",
            note = "wait for the screen before drawing the next frame: no tearing, and no more frames than your " +
                   "monitor can show. Off draws as fast as it can" },
    };
    public static readonly Dictionary<string, TextSize> TEXT_SIZES = new()
    {
        ["small"] = new TextSize { name = "SMALL", k = 0.88 }, ["normal"] = new TextSize { name = "NORMAL", k = 1 },
        ["large"] = new TextSize { name = "LARGE", k = 1.15 }, ["xlarge"] = new TextSize { name = "VERY LARGE", k = 1.32 },
    };
    public static readonly OptStore OPT = MakeOpt();
    static OptStore MakeOpt() { var o = new OptStore(); foreach (var d in OPT_DEFS) o[d.id] = d.def; return o; }
    public static bool optOn = false; public static string optFrom = "title"; public static int optArmed = 0;
    public static void optLoad()
    {
        string raw = null;
        try { raw = Store.getItem(OPT_KEY); } catch { }
        JsonObject o = null;
        try { o = raw != null ? JsonNode.Parse(raw) as JsonObject : null; } catch { }
        foreach (var d in OPT_DEFS)
        {
            JsonNode v = o != null && o.ContainsKey(d.id) ? o[d.id] : null;
            if (d.choices != null)
            {
                string s = v is JsonValue jv && jv.TryGetValue<string>(out var ss) ? ss : null;
                OPT[d.id] = s != null && d.choices.Contains(s) ? s : d.def;
            }
            else if (d.range != null)
            {
                OPT[d.id] = v is JsonValue jn && jn.TryGetValue<double>(out var dv) && Js.IsFinite(dv) ? clamp(dv, d.range[0], d.range[1]) : d.def;
            }
            else
            {
                OPT[d.id] = v is JsonValue jb && jb.TryGetValue<bool>(out var bv) ? bv : d.def;
            }
        }
    }
    public static void optSave()
    {
        var o = new JsonObject();
        foreach (var d in OPT_DEFS)
        {
            var v = OPT[d.id];
            o[d.id] = d.choices != null ? JsonValue.Create(v as string)
                    : d.range != null ? JsonValue.Create(v is double dd ? dd : 0)
                    : JsonValue.Create(v is bool b && b);
        }
        try { Store.setItem(OPT_KEY, o.ToJsonString()); } catch { }
    }
    public static object optSet(string id, object v)
    {
        var d = OPT_DEFS.FirstOrDefault(q => q.id == id);
        if (d != null && d.choices != null) OPT[id] = v is string s && d.choices.Contains(s) ? s : d.def;
        else if (d != null && d.range != null)
        {
            double n = v is double dv ? dv : v is int iv ? iv : v is string sv ? Js.ToNumber(sv) : double.NaN;
            OPT[id] = Js.IsFinite(n) ? clamp(Math.Round(n / d.range[2]) * d.range[2], d.range[0], d.range[1]) : d.def;
        }
        else OPT[id] = v is bool b ? b : v is double dd ? dd != 0 : v != null;
        optSave();
        if (id == "textSize") applyTextSize();
        if (id == "renderScale" || id == "vsync") App.SettingsChanged();
        return OPT[id];
    }
    /* ---- TEXT SIZE: every canvas font is scaled at the moment it is set ---- */
    public static double TEXT_K = 1;
    public static void textScaleRules() { }      // js: textScaleRules, textFontOrig, textFontCache
    public static void applyTextSize()
    {
        TEXT_K = (TEXT_SIZES.GetValueOrDefault(OPT.textSize) ?? TEXT_SIZES["normal"]).k;
        FontSpec.TextK = TEXT_K;
        FontSpec.ClearCache();
        fitTitleSoon();
    }
    public static object optToggle(string id) => optSet(id, !(OPT[id] is bool b && b));
}
