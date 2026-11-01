using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Brickblade;

/* =============================== STATE (JS 4057–4391) =============================== */
public sealed class Banner { public string text, sub; public Col color; public double dur, size, t; }
public sealed class PendingPick { public string mode, rar; }
public sealed class Sched { public double t; public Action fn; }
public sealed class BuildEntry { public string id, rar; }
/* a run link, decoded: the fields runLinkState writes, by their short names */
public sealed class RunLinkData
{
    public double v, n, s, tv, lv, xp, xn, st, k, gh, hp, bd, rt, treeStale;
    public string w, d, f;
    public JsonObject g;         /* the gear it was copied with, packed (gearPackEq) */
    public bool on;
    public List<string> t = new();
    public List<(string k, double at)> ro = new();
    public List<(string id, int ri)> b = new();
    public NumMap r = new(), sp = new();
}

public static partial class G
{
    public static string state = "title";
    public static double vtime = 0;
    public static double frameRaw = 1.0 / 60;
    public static double tScale = 1;
    public static List<Ent> enemies = new();
    public static List<Proj> projectiles = new();
    public static List<Pickup> pickups = new();
    public static List<Debris> debris = new();
    public static List<Particle> particles = new();
    public static List<DmgText> dmgTexts = new();
    public static List<Sched> sched = new();
    public static List<Prop> props = new(), solids = new();
    public static List<WaterRect> waters = new();
    public static List<List<WaterRect>> lakeGroups = new();
    public static List<Flower> flowers = new();
    public static List<Hazard> hazards = new();
    public static List<Spike> spikes = new(), pspikes = new();
    public static List<Ent> minions = new();
    /* THE HERO, always -- `player` is the hero too, except for the length of one
       monster's update when that monster has a skeleton for a target */
    public static Ent heroObj = null;
    public static Wave wave = new Wave { n = 0, state = "idle" };
    public static Breach breach = null;
    public static List<Breach> breachExtra = new();
    public static Banner banner = null;
    public static List<Choice> choices = new();
    public static NumMap taken = new();
    public static string upgradeMode = "wave";
    public static string forcedRarity = null;
    public static List<PendingPick> pendingPicks = new();
    public static bool previewing = false;
    public static double rerollsUsed = 0;
    /* WHAT YOU THREW BACK: a rerolled brick goes scarce for a few waves */
    public const double SHUN_MIN = 3, SHUN_MAX = 5, SHUN_WEIGHT = 0.2;
    public static NumMap shunned = new();
    public static void shunCards(List<Choice> list)
    {
        foreach (var k in shunned.Keys.ToList()) if (shunned[k] <= wave.n) shunned.Delete(k);
        foreach (var c in list ?? new List<Choice>())
        {
            if (c == null || c.t == null) continue;
            double dur = SHUN_MIN + Math.Floor(Js.R() * (SHUN_MAX - SHUN_MIN + 1));
            shunned[c.t.id] = Math.Max(shunned[c.t.id], wave.n + dur);
        }
    }
    public static double shunMul(string id) => shunned[id] > wave.n ? SHUN_WEIGHT : 1;
    public static double screenRerolls = 0;
    public static double runTime = 0, retPulse = 0, redFlash = 0;
    public static double seed = 1;
    public static double mergeT = 0;
    public static Ent player = null;
    /* Every brick ever applied, in order, with the rarity it rolled at */
    public static List<BuildEntry> buildLog = new();
    public static void applyPick(Upgrade u, string rar)
    {
        u.apply(u.calc(RARITY[rar].mult));
        syncStats();
        logEvent("took " + u.name + " (" + rar + ")", RARITY[rar].col);
        taken[u.id] = taken[u.id] + 1;
        buildLog.Add(new BuildEntry { id = u.id, rar = rar });
        /* A SPELL CAN BE FINISHED MID-RUN, and the point for it exists the moment it is */
        if (!sandbox && runLink == null && state != "title") foreach (var k in metaMastery(player))
            {
                var M = META_MASTERY[k];
                logEvent("ACHIEVEMENT — " + M.name + " · +1 skill point", "#ffd76e");
                showBanner(M.name, "+1 skill point", "#ffd76e", 2.2, 34);
            }
    }
    /* Rebuild the whole character from the log with one brick left out */
    public static void rebuildWithout(string id)
    {
        var keep = buildLog.Where(e => e.id != id).ToList();
        var p = player;
        double hpFrac = p.maxHp > 0 ? p.hp / p.maxHp : 1;
        double cx = p.x, cy = p.y, clevel = p.level, cxp = p.xp, cxn = p.xpNext, cst = p.studs, ck = p.kills;
        var blank = newPlayer();
        Fields.CopyAll(p, blank);
        p.x = cx; p.y = cy; p.level = clevel; p.xp = cxp; p.xpNext = cxn; p.studs = cst; p.kills = ck;
        p.incAll = 0.05 * (p.level - 1);
        p.hpFlat += levelHearts(p);
        applyMetaTree(p);
        applyFavPerks();
        if (modeHasGear()) gearApply(p, true);
        taken = new NumMap(); buildLog = new List<BuildEntry>();
        foreach (var e in keep)
        {
            var u = UPGRADES.FirstOrDefault(x => x.id == e.id);
            if (u != null) applyPick(u, e.rar);
        }
        p.hp = Math.Max(1, Math.Min(p.maxHp, Math.Round(p.maxHp * hpFrac)));
    }

    /* =============================== THE RUN, AS A LINK =============================== */
    public const int RUNLINK_V = 1;
    public static RunLinkData runLink = null;
    public static JsonObject runLinkState()
    {
        var p = player;
        var tree = new List<string>(); bool on = false;
        try
        {
            if (META != null && META.on)
            {
                var m = metaCur(); on = true;
                tree = m.nodes.Where(id => META_BY_ID.ContainsKey(id) && !metaLocked(META_BY_ID[id], m)).ToList();
            }
        }
        catch { }
        if (runLink != null && runLink.t != null && runLink.t.Count > 0) { tree = runLink.t.ToList(); on = runLink.on; }
        var o = new JsonObject
        {
            ["v"] = RUNLINK_V, ["w"] = weapon.id, ["d"] = difficulty?.id ?? "hard", ["f"] = favourite.id,
            ["n"] = Math.Max(1, Js.I32(wave.n)), ["s"] = seed, ["on"] = on,
            ["t"] = new JsonArray(tree.Select(MetaIdJson).ToArray()), ["tv"] = META_TREE_VER,
            ["ro"] = new JsonArray(ROSTER.Select(r => (JsonNode)new JsonArray(r.k, r.at)).ToArray()),
            ["b"] = new JsonArray(buildLog.Select(e => (JsonNode)new JsonArray(e.id, Math.Max(0, Array.IndexOf(RAR_ORDER, e.rar)))).ToArray()),
            ["lv"] = p.level, ["xp"] = Math.Round(p.xp), ["xn"] = p.xpNext, ["st"] = p.studs, ["k"] = p.kills, ["gh"] = p.gHearts,
            ["hp"] = Js.ToNumber(Js.ToFixed(p.maxHp > 0 ? p.hp / p.maxHp : 1, 3)),
            ["r"] = NumMapJson(p.rings), ["sp"] = NumMapJson(p.splinters), ["bd"] = p.breachDepth,
            ["rt"] = Math.Round(runTime),
        };
        var g = gearLinkPart();
        if (g != null) o["g"] = g;
        return o;
    }
    /* tree node ids are numbers in the JS; they travel as numbers */
    static JsonNode MetaIdJson(string id) => double.TryParse(id, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? JsonValue.Create(d) : JsonValue.Create(id);
    static JsonObject NumMapJson(NumMap m) { var o = new JsonObject(); if (m != null) foreach (var (k, v) in m) o[k] = v; return o; }
    /* the gear goes in the link only when there is some */
    public static JsonObject gearLinkPart() { if (!modeHasGear()) return null; return gearPackEq(gearEqNow()); }
    /* COMPRESSED, THEN BASE64: LZW with growing code widths, bit for bit the JS */
    public static byte[] lzwPack(byte[] bytes)
    {
        var dict = new Dictionary<string, int>(); int next = 256;
        var outb = new List<byte>(); int acc = 0, nb = 0, k = 0;
        void put(int code)
        {
            int width = (int)Math.Min(16, Math.Ceiling(Math.Log2(Math.Min(65536, 256 + k))));
            k++;
            for (int i = width - 1; i >= 0; i--) { acc = (acc << 1) | ((code >> i) & 1); nb++; if (nb == 8) { outb.Add((byte)acc); acc = 0; nb = 0; } }
        }
        if (bytes.Length == 0) return Array.Empty<byte>();
        string w = ((char)bytes[0]).ToString(); int wc = bytes[0];
        for (int i = 1; i < bytes.Length; i++)
        {
            int c = bytes[i]; string keyS = w + (char)c;
            if (dict.TryGetValue(keyS, out var got)) { w = keyS; wc = got; continue; }
            put(wc);
            if (next < 65536) dict[keyS] = next++;
            w = ((char)c).ToString(); wc = c;
        }
        put(wc);
        if (nb != 0) outb.Add((byte)((acc << (8 - nb)) & 0xFF));
        return outb.ToArray();
    }
    public static byte[] lzwUnpack(byte[] bytes)
    {
        var dict = new List<List<byte>>(); for (int i = 0; i < 256; i++) dict.Add(new List<byte> { (byte)i });
        int pos = 0, k = 0, total = bytes.Length * 8;
        int get()
        {
            int width = (int)Math.Min(16, Math.Ceiling(Math.Log2(Math.Min(65536, 256 + k))));
            if (pos + width > total) return -1;
            int v = 0;
            for (int i = 0; i < width; i++) { v = (v << 1) | ((bytes[pos >> 3] >> (7 - (pos & 7))) & 1); pos++; }
            k++;
            return v;
        }
        var outb = new List<byte>();
        int prev = get(); if (prev < 0) return Array.Empty<byte>();
        var w = dict[prev]; outb.AddRange(w);
        for (; ; )
        {
            int code = get(); if (code < 0) break;
            List<byte> entry;
            if (code < dict.Count) entry = dict[code];
            else if (code == dict.Count) { entry = new List<byte>(w) { w[0] }; }
            else throw new Exception("bad run link");
            outb.AddRange(entry);
            if (dict.Count < 65536) dict.Add(new List<byte>(w) { entry[0] });
            w = entry;
        }
        return outb.ToArray();
    }
    public static string b64url(byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    public static byte[] unb64url(string str)
    {
        string b = (str ?? "").Replace('-', '+').Replace('_', '/');
        while (b.Length % 4 != 0) b += "=";
        return Convert.FromBase64String(b);
    }
    public static string runLinkEncode(JsonObject o) => "z" + b64url(lzwPack(Encoding.UTF8.GetBytes(o.ToJsonString())));
    public static RunLinkData runLinkDecode(string str)
    {
        str = str ?? "";
        string json = str.Length > 0 && str[0] == 'z'
            ? Encoding.UTF8.GetString(lzwUnpack(unb64url(str.Substring(1))))
            : Encoding.UTF8.GetString(unb64url(str));
        var j = JsonNode.Parse(json) as JsonObject;
        double num(string key) => j.TryGetPropertyValue(key, out var n) && n is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;
        string str_(string key) => j.TryGetPropertyValue(key, out var n) && n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var o = new RunLinkData
        {
            v = num("v"), w = str_("w"), d = str_("d"), f = str_("f"), n = num("n"), s = num("s"), tv = num("tv"),
            lv = num("lv"), xp = num("xp"), xn = num("xn"), st = num("st"), k = num("k"), gh = num("gh"), hp = num("hp"),
            bd = num("bd"), rt = num("rt"), g = j["g"] is JsonObject go ? (JsonObject)go.DeepClone() : null,
            on = j.TryGetPropertyValue("on", out var onN) && onN is JsonValue ov && ov.TryGetValue<bool>(out var ob) && ob,
        };
        if (j["t"] is JsonArray ta) foreach (var x in ta) o.t.Add(x is JsonValue xv && xv.TryGetValue<double>(out var dd) ? Js.Num(dd) : x?.ToString());
        if (j["ro"] is JsonArray ra) foreach (var x in ra) if (x is JsonArray pr && pr.Count >= 2) o.ro.Add((pr[0]?.GetValue<string>(), pr[1]?.GetValue<double>() ?? 0));
        if (j["b"] is JsonArray ba) foreach (var x in ba) if (x is JsonArray pr && pr.Count >= 2) o.b.Add((pr[0]?.GetValue<string>(), (int)(pr[1]?.GetValue<double>() ?? 0)));
        if (j["r"] is JsonObject rr) foreach (var (kk, vv) in rr) o.r[kk] = vv?.GetValue<double>() ?? 0;
        if (j["sp"] is JsonObject spo) foreach (var (kk, vv) in spo) o.sp[kk] = vv?.GetValue<double>() ?? 0;
        if (o.v != RUNLINK_V || o.w == null || !WEAPONS.ContainsKey(o.w)) throw new Exception("not a run link");
        return o;
    }
    /* natively there is no address bar: the link is the code itself */
    public const string RUNLINK_PREFIX = "brickblade:run=";
    public static string runLinkURL() => RUNLINK_PREFIX + runLinkEncode(runLinkState());
    /* hand it to the clipboard */
    public static string runLinkCopy(Element btn = null)
    {
        string url = runLinkURL();
        void done(bool ok)
        {
            if (btn == null) return; string was = btn.dataset["label"] ?? btn.textContent;
            btn.dataset["label"] = was; btn.textContent = ok ? "LINK COPIED \u2713" : "COPY FAILED";
            setTimeout(() => { btn.textContent = was; }, 1600);
        }
        bool okc = true;
        try { Dom.ClipboardSet(url); } catch { okc = false; }
        done(okc);
        logEvent("run link copied — wave " + N(Math.Max(1, Js.I32(wave.n))), "#9fd4ff");
        return url;
    }
    /* after resetRun has stood the hero up with the link's tree, favourite, seed and roster */
    public static double runLinkApply()
    {
        var o = runLink; var p = player;
        p.level = Math.Max(1, Js.I32(o.lv)); p.incAll += 0.05 * (p.level - 1); p.hpFlat += levelHearts(p);
        p.xp = o.xp; p.xpNext = Js.Or(o.xn, p.xpNext);
        int pre = buildLog.Count;
        foreach (var (id, ri) in o.b.Skip(pre))
        {
            var u = UPGRADES.FirstOrDefault(x => x.id == id);
            if (u != null) try { applyPick(u, ri >= 0 && ri < RAR_ORDER.Length ? RAR_ORDER[ri] : "common"); } catch (Exception ex) { Console.Error.WriteLine("run link brick " + id + " " + ex.Message); }
        }
        foreach (var (k, v) in o.r) { double n2 = Js.I32(v); if (n2 > 0 && BREACH_LORDS.ContainsKey(k)) p.rings[k] = n2; }
        p.splinters.Assign(o.sp); p.breachDepth = o.bd;
        if (Js.T(o.gh)) { p.gHearts = o.gh; p.hpFlat += 3 * o.gh; }
        p.studs = o.st; p.kills = o.k;
        syncStats();
        p.hp = Math.Max(1, Math.Round(p.maxHp * (o.hp > 0 ? Math.Min(1, o.hp) : 1)));
        runTime = o.rt;
        double n = clamp(Js.I32(o.n), 1, 999);
        logEvent("RUN LINK — wave " + N(n) + ", level " + N(p.level) + ", " + o.b.Count + " bricks", "#9fd4ff");
        if (Js.T(o.treeStale)) logEvent("   its " + N(o.treeStale) + " tree points were placed on an older skill tree and are left out", "#ffb900");
        showBanner("RUN LINK", "wave " + N(n) + " · level " + N(p.level) + " · " + o.b.Count + " bricks" +
            (Js.T(o.treeStale) ? " · OLDER TREE LEFT OUT" : ""), "#9fd4ff", 2.4, 44);
        return n;
    }
    /* the game was opened on a run link (a launch argument, or the title's paste) */
    public static bool runLinkBoot(string link = null)
    {
        link ??= Program.Opt("--run");
        if (string.IsNullOrEmpty(link)) return false;
        var m = System.Text.RegularExpressions.Regex.Match(link, @"(?:^|[#&:=])run=([A-Za-z0-9_-]+)|^([A-Za-z0-9_-]+)$");
        if (!m.Success) return false;
        string code = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        try
        {
            var o = runLinkDecode(code);
            if (o.t.Count > 0 && Js.I32(o.tv) != META_TREE_VER) { o.treeStale = o.t.Count; o.t = new List<string>(); o.tv = META_TREE_VER; }
            runLink = o;
            weapon = WEAPONS.GetValueOrDefault(o.w) ?? WEAPONS["sword"];
            difficulty = DIFFS.GetValueOrDefault(o.d ?? "") ?? DIFFS["hard"];
            favourite = FAVOURITES.FirstOrDefault(f => f.id == o.f) ?? FAVOURITES[0];
            favSet = new HashSet<string>(favourite.ids);
            setGameMode("arcade");
            startGame();
            return true;
        }
        catch (Exception e)
        {
            runLink = null;
            Console.Error.WriteLine("run link: " + e.Message);
            return false;
        }
    }
    /* where storm brick #i of `total` hovers */
    public static Pt chainBrickPos(Obj p, double i, double total)
    {
        double a = vtime * 1.6 + 2.2 + i * TAU / Math.Max(1, total);
        return new Pt(p.x + Math.Cos(a) * 50, p.y - 30 + Math.Sin(a) * 27);
    }
    public static void schedule(double delay, Action fn) { sched.Add(new Sched { t = delay, fn = fn }); }
    public static void queuePick(string mode, string rar) { if (rpgOn()) return; pendingPicks.Add(new PendingPick { mode = mode, rar = rar }); }
    /* THE REWARD STASH: R opens the oldest, and one press opens the lot */
    public const double LOOT_HOLD_GAP = 0;
    public static bool lootHeld = false; public static double lootGapT = 0;
    public static bool lootChain = false;
    public static bool lootNow()
    {
        if (pendingPicks.Count == 0 || player == null || player.dead || state != "play") return false;
        var q = pendingPicks.Shift();
        openUpgrades(q.mode, q.rar);
        lootGapT = LOOT_HOLD_GAP;
        return true;
    }
    public static bool lootNext()
    {
        if (pendingPicks.Count == 0) { lootChain = false; return false; }
        if (!(lootChain || lootHeld || OPT.autoLoot)) return false;
        return lootNow();
    }
    public static void addShake(double a) { cam.shake = Math.Min(0.55, cam.shake + a * 0.5); }
    public static void showBanner(string text, string sub, string color = "#ffb900", double dur = 1.7, double size = 54)
    { banner = new Banner { text = text, sub = sub, color = color, dur = dur, size = size, t = 0 }; }
    /* THE CODE: type 1337 to open the bench */
    public static readonly string[] CHEAT_CODE = { "Digit1", "Digit3", "Digit3", "Digit7" };
    public static int cheatIdx = 0;
    public static void cheatCode(string code)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^Digit[0-9]$") && !System.Text.RegularExpressions.Regex.IsMatch(code, "^Numpad[0-9]$")) return;
        string k = code.Replace("Numpad", "Digit");
        if (k == CHEAT_CODE[cheatIdx]) cheatIdx++;
        else cheatIdx = k == CHEAT_CODE[0] ? 1 : 0;
        if (cheatIdx == CHEAT_CODE.Length)
        {
            cheatIdx = 0;
            showBanner("1337", "test bench unlocked", "#9b45d6", 1.4, 46);
            SFX.fortune();
            toggleCheat();
        }
    }
}
