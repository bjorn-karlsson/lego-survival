// Port of lego-survival.html lines 15111-15567: the tree's save, its points (all
// DERIVED), buying, refunding, presets, what it does to a hero and what a run banks.
using System.Text.Json.Nodes;

namespace Brickblade;

public sealed class MetaBest { public double wave, level; }
public sealed class MetaPreset { public string name; public List<string> nodes = new(); }
/* one tree: a difficulty's record for one weapon */
public sealed class MetaRec
{
    public MetaBest best = new(); public List<string> bosses = new(), mastery = new();
    public double studsEarned; public List<string> nodes = new(); public List<MetaPreset> presets; public double tv;
}
/* the whole save: { v, on, <difficulty>: { <weapon>: MetaRec } } */
public sealed class MetaSave
{
    public double v; public bool on;
    public readonly Dictionary<string, Dictionary<string, MetaRec>> d = new();
    public Dictionary<string, MetaRec> this[string k] => d.GetValueOrDefault(k);
}
public sealed class MetaRunSnap { public double wave, level, earned; }
public sealed class MetaStatLine { public string key, text, col; public bool good; }
public sealed class MetaFirst { public string text; public double pts; }
public sealed class MetaBankResult
{
    public double banked, collected, cleared, level, gained, total, left; public List<MetaFirst> firsts;
}
public sealed class MetaPresetPlaced { public double placed, of; }

public static partial class G
{
    public static double metaCost(MetaNode n) => n != null && !double.IsNaN(n.cost) ? n.cost : 1;
    public static double metaTotalCost() { double t = 0; foreach (var n in META_NODES) t += metaCost(n); return t; }
    /* a node whose every mod does nothing for this weapon — still walkable, but the
       panel says so rather than selling you something you cannot use */
    public static bool metaIdleFor(MetaNode n, string wk)
    {
        if (n.mods.Count == 0) return false;
        return n.mods.All(m =>
        {
            var st = META_STATS.GetValueOrDefault(m.k ?? "");
            return st != null && st.only != null && Array.IndexOf(st.only, wk) < 0;
        });
    }
    public static void metaApplyNode(MetaNode n, Ent p)
    {
        if (n.apply != null) { try { n.apply(p); } catch { } return; }
        foreach (var m in n.mods)
        {
            var st = META_STATS.GetValueOrDefault(m.k ?? "");
            if (st == null) continue;
            try
            {
                if (st.ws != null) st.ws(p, m.s);
                else st.w?.Invoke(p, m.v);
            }
            catch { }
        }
    }

    /* ---- the save. Difficulty, then WEAPON: twelve independent records. ---- */
    /* THREE NAMED PRESETS PER TREE. A preset is a saved allocation -- a name and
       the nodes, in the order they were bought -- on the SAME record as the tree it
       came from: a sword's build starts at the sword's door and means nothing on a
       staff's tree, so each of the twelve trees keeps its own three. */
    public const int META_PRESETS = 3, META_PRESET_NAME_MAX = 24;
    public static MetaPreset metaPresetBlank(int i) => new MetaPreset { name = "Preset " + (i + 1), nodes = new() };
    public static MetaRec blankMeta() => new MetaRec
    {
        best = new MetaBest { wave = 0, level = 0 }, bosses = new(), mastery = new(), studsEarned = 0, nodes = new(),
        presets = new[] { 0, 1, 2 }.Select(metaPresetBlank).ToList(), tv = META_TREE_VER,
    };
    public static MetaSave META = null;
    /* how many points the last load handed back because the tree moved under them */
    public static double META_REFUNDED = 0;
    /* typeof v === 'number' && isFinite(v) */
    static bool JStrictNum(JsonNode n, out double d)
    {
        d = 0;
        return n is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number && v.TryGetValue<double>(out d) && double.IsFinite(d);
    }
    static double metaInt(JsonNode v, double lo, double hi) => JStrictNum(v, out var d) ? clamp(Math.Floor(d), lo, hi) : lo;
    /* JS truthiness of a parsed JSON value */
    static bool JTruthy(JsonNode n)
    {
        if (n == null) return false;
        if (n is not JsonValue v) return true;
        switch (v.GetValueKind())
        {
            case System.Text.Json.JsonValueKind.False: return false;
            case System.Text.Json.JsonValueKind.True: return true;
            case System.Text.Json.JsonValueKind.Number: { var d = v.GetValue<double>(); return d != 0 && !double.IsNaN(d); }
            case System.Text.Json.JsonValueKind.String: return v.GetValue<string>().Length > 0;
            default: return false;
        }
    }
    /* typeof x === 'object' && x (an array is an object too) */
    static bool JIsObj(JsonNode n) => n is JsonObject || n is JsonArray;
    static JsonNode JGet(JsonNode o, string k) => o is JsonObject jo && jo.TryGetPropertyValue(k, out var v) ? v : null;
    /* an id read back the way an object key would see it: a string, or a number stringified */
    static string JKey(JsonNode n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        if (v.GetValueKind() == System.Text.Json.JsonValueKind.Number) return Js.Num(v.GetValue<double>());
        return null;
    }
    /* VALIDATE OR RESET. A corrupt save must start you empty, never throw. */
    public static MetaRec metaParseOne(JsonNode r)
    {
        var b = blankMeta();
        if (JIsObj(r))
        {
            b.tv = metaInt(JGet(r, "tv"), 0, 9999);
            b.studsEarned = metaInt(JGet(r, "studsEarned"), 0, 1e12);
            var best = JGet(r, "best");
            if (JIsObj(best))
            {
                b.best.wave = metaInt(JGet(best, "wave"), 0, 99999);
                b.best.level = metaInt(JGet(best, "level"), 0, 99999);
            }
            if (JGet(r, "bosses") is JsonArray bo)
                foreach (var kn in bo) { string k = JStr(kn); if (k != null && b.bosses.IndexOf(k) < 0) b.bosses.Add(k); }
            if (JGet(r, "mastery") is JsonArray ma)
                foreach (var kn in ma) { string k = JKey(kn); if (k != null && META_MASTERY.ContainsKey(k) && b.mastery.IndexOf(k) < 0) b.mastery.Add(k); }
            /* an unknown node id is simply dropped, and because SPENT is derived its
               points come back on their own */
            if (JGet(r, "nodes") is JsonArray no)
                foreach (var kn in no) { string k = JKey(kn); if (k != null && META_BY_ID.ContainsKey(k) && b.nodes.IndexOf(k) < 0) b.nodes.Add(k); }
            /* a preset is validated like the tree itself: a name that is a string of
               sane length, and only node ids the map still has */
            if (JGet(r, "presets") is JsonArray pa)
                for (int i = 0; i < META_PRESETS; i++)
                {
                    var q = i < pa.Count ? pa[i] : null;
                    if (!JIsObj(q)) continue;
                    var pr = b.presets[i];
                    string nm = JStr(JGet(q, "name"));
                    if (nm != null && nm.Trim().Length > 0)
                        pr.name = nm.Trim().slice(0, META_PRESET_NAME_MAX);
                    if (JGet(q, "nodes") is JsonArray qn)
                        foreach (var kn in qn) { string k = JKey(kn); if (k != null && META_BY_ID.ContainsKey(k) && pr.nodes.IndexOf(k) < 0) pr.nodes.Add(k); }
                }
        }
        return b;
    }
    public static MetaSave metaParse(string raw)
    {
        JsonNode o = null;
        try { o = JsonNode.Parse(raw); } catch { return null; }
        if (!JIsObj(o)) return null;
        /* A VERSION-1 SAVE HAD ONE RECORD PER DIFFICULTY AND NO WEAPONS, and there is
           exactly one weapon it can honestly have belonged to: the one that was
           selected when it was written. Copying it into all four is what made the
           tree look shared, and it was never true — a staff's points are a staff's. */
        string lastWeap = null;
        try { lastWeap = Store.getItem("bb_weapon"); } catch { }
        if (WEAPON_KEYS.IndexOf(lastWeap) < 0) lastWeap = WEAPON_KEYS[0];
        var onN = JGet(o, "on");
        var @out = new MetaSave { v = META_VER, on = !(onN is JsonValue ov && ov.GetValueKind() == System.Text.Json.JsonValueKind.False) };
        foreach (var d in DIFFS.Keys)
        {
            @out.d[d] = new Dictionary<string, MetaRec>();
            var drN = JGet(o, d);
            var dr = JIsObj(drN) ? drN : null;
            var legacy = (dr != null && !WEAPON_KEYS.Any(w => JTruthy(JGet(dr, w)))
                          && (JTruthy(JGet(dr, "nodes")) || JGet(dr, "studsEarned") != null)) ? dr : null;
            foreach (var w in WEAPON_KEYS)
            {
                var own = dr != null ? JGet(dr, w) : null;
                @out.d[d][w] = metaParseOne(JTruthy(own) ? own : (w == lastWeap ? legacy : null));
            }
        }
        /* A LAYOUT CHANGE IS A FREE RESPEC, NEVER A RESET. Every stud, every best
           wave and every boss is kept — the points just come back, because the nodes
           they were spent on are not where they were left. metaTrim would drop them
           one at a time anyway; doing it here means we can SAY SO. */
        META_REFUNDED = 0;
        foreach (var d in DIFFS.Keys) foreach (var w in WEAPON_KEYS)
            {
                var m = @out.d[d][w];
                if (m.tv == META_TREE_VER) continue;
                if (m.nodes.Count > 0) META_REFUNDED += metaPointsSpent(m);
                m.nodes = new(); m.tv = META_TREE_VER;
                /* a preset saved on the old map is a shape that is not there any more */
                foreach (var pr in m.presets ?? new()) pr.nodes = new();
            }
        return @out;
    }
    public static MetaSave metaBlankSave()
    {
        var o = new MetaSave { v = META_VER, on = true };
        foreach (var d in DIFFS.Keys) { o.d[d] = new(); foreach (var w in WEAPON_KEYS) o.d[d][w] = blankMeta(); }
        return o;
    }
    public static void metaLoad()
    {
        string raw = null;
        try { raw = Store.getItem(META_KEY); } catch { }
        META = (Js.T(raw) ? metaParse(raw) : null) ?? metaBlankSave();
        metaTrim();
    }
    /* JSON.stringify(META), keys in the JS's order */
    static JsonNode MetaListJson(List<string> l) => new JsonArray(l.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
    public static JsonObject metaRecJson(MetaRec m) => new JsonObject
    {
        ["best"] = new JsonObject { ["wave"] = m.best.wave, ["level"] = m.best.level },
        ["bosses"] = MetaListJson(m.bosses), ["mastery"] = MetaListJson(m.mastery),
        ["studsEarned"] = m.studsEarned, ["nodes"] = MetaListJson(m.nodes),
        ["presets"] = new JsonArray((m.presets ?? new()).Select(p => (JsonNode)new JsonObject { ["name"] = p.name, ["nodes"] = MetaListJson(p.nodes) }).ToArray()),
        ["tv"] = m.tv,
    };
    public static string metaJson(MetaSave s)
    {
        var o = new JsonObject { ["v"] = s.v, ["on"] = s.on };
        foreach (var (d, ws) in s.d)
        {
            var dj = new JsonObject();
            foreach (var (w, m) in ws) dj[w] = metaRecJson(m);
            o[d] = dj;
        }
        return o.ToJsonString();
    }
    public static void metaSave() { try { Store.setItem(META_KEY, metaJson(META)); } catch { } }
    public static double wavesCleared() => Math.Max(0, Js.Or(wave.done, 0));
    public static string metaWeapon() => (weapon != null && Js.T(weapon.id)) ? weapon.id : "sword";
    public static MetaRec metaCur()
    {
        if (META == null) metaLoad();
        var d = META[(difficulty != null && Js.T(difficulty.id)) ? difficulty.id : "hard"];
        return d.GetValueOrDefault(metaWeapon()) ?? d["sword"];
    }
    public static string metaStartId(string wk = null)
    {
        string want = Js.T(wk) ? wk : metaWeapon();
        var st = META_STARTS.Find(x => x.weap == want);
        return st != null ? st.id : META_STARTS[0].id;
    }

    /* ---- points, all DERIVED ---- */
    /* the high-water mark, not the lap count */
    public static double metaWavePoints(MetaRec m) => Math.Max(0, m.best.wave);
    public static double metaLevelPoints(MetaRec m) => META_POINTS_PER_LEVEL * Math.Max(0, m.best.level);
    public static double metaMilestonePoints(MetaRec m) =>
        metaLevelPoints(m) + (m.bosses?.Count ?? 0) + (m.mastery?.Count ?? 0);
    public static double metaPointsEarned(MetaRec m = null)
    {
        m ??= metaCur();
        return Math.Min(META_POINT_MAX, metaWavePoints(m) + metaMilestonePoints(m));
    }
    /* THE NEXT LEVEL THAT PAYS A POINT. A level pays one the first time it is
       reached -- your BEST level is the record -- so on a fresh character it is the
       next level up, and on a hero below their best it is one past the best. null
       (NaN) when nothing would pay: the tree switched off, a replayed link, the ceiling. */
    public static double metaNextLevelPoint()
    {
        if (metaRun == null || META == null || !META.on || player == null) return double.NaN;
        var m = metaCur();
        if (metaPointsEarned(m) >= META_POINT_MAX) return double.NaN;
        return Math.Max(m.best.level, Js.Or(player.level, 0)) + 1;
    }
    public static double metaPointsSpent(MetaRec m = null)
    {
        m ??= metaCur();
        double t = 0;
        foreach (var id in m.nodes) { var n = META_BY_ID.GetValueOrDefault(id); if (n != null) t += metaCost(n); }
        return t;
    }
    public static double metaPointsLeft(MetaRec m = null) { m ??= metaCur(); return metaPointsEarned(m) - metaPointsSpent(m); }
    /* what the NEXT point costs, which is now always one more wave (NaN: none) */
    public static double metaNextPoint(MetaRec m = null)
    {
        m ??= metaCur();
        if (metaPointsEarned(m) >= META_POINT_MAX) return double.NaN;
        return m.best.wave + 1;
    }
    public static bool metaHas(string id, MetaRec m = null) { m ??= metaCur(); return m.nodes.IndexOf(id) >= 0; }
    public static bool metaLocked(MetaNode n, MetaRec m = null) { m ??= metaCur(); return m.best.wave < Js.Or(n.at, 0); }
    /* ---- THE GRAPH. You cannot drop points anywhere you like: a node has to touch
       something you already hold, and everything you hold has to trace back to the
       start. That is what makes a tree a tree rather than a shopping list. ---- */
    public static bool metaConnected(string id, MetaRec m = null, List<string> extra = null)
    {
        m ??= metaCur();
        var n = META_BY_ID.GetValueOrDefault(id);
        if (n == null) return false;
        /* EVERY DOOR'S PATHS ARE OPEN TO EVERYBODY, and the DOOR is the thing that
           is not. You begin in yours; nothing walks through anybody else's. So the
           staff's first steps are buyable by a sword hero — from the far end, the
           long way round the ring they hang off, which is the whole price of them. */
        if (n.kind == "start") return n.id == metaStartId();
        foreach (var r in n.req)
        {
            var rn = META_BY_ID.GetValueOrDefault(r);
            if (rn != null && rn.kind == "start" && r != metaStartId()) continue;
            if (metaHas(r, m) || (extra != null && extra.IndexOf(r) >= 0)) return true;
        }
        return false;
    }
    public static bool metaCanBuy(MetaNode n, MetaRec m = null)
    {
        m ??= metaCur();
        if (n == null || n.kind == "start") return false;   /* a door is where you begin, never a purchase */
        if (metaHas(n.id, m) || metaLocked(n, m)) return false;
        if (metaPointsLeft(m) < metaCost(n)) return false;
        return metaConnected(n.id, m, new List<string> { metaStartId() });
    }
    public static bool metaBuy(string id)
    {
        var m = metaCur(); var n = META_BY_ID.GetValueOrDefault(id);
        if (n == null || !metaCanBuy(n, m)) return false;
        m.nodes.Add(id); metaSave(); return true;
    }
    /* everything still allocated must trace back to the start — so refunding a node
       that holds up a branch is refused rather than silently orphaning it */
    public static bool metaWouldStrand(string id, MetaRec m = null)
    {
        m ??= metaCur();
        var keep = m.nodes.Where(x => x != id).ToList();
        var reached = new List<string> { metaStartId() };
        bool grew = true; int guard = 0;
        while (grew && guard++ < 200)
        {
            grew = false;
            foreach (var k in keep)
            {
                if (reached.IndexOf(k) >= 0) continue;
                var n = META_BY_ID.GetValueOrDefault(k);
                if (n == null) continue;
                if (n.req.Any(r => reached.IndexOf(r) >= 0)) { reached.Add(k); grew = true; }
            }
        }
        return keep.Any(k => reached.IndexOf(k) < 0);
    }
    public static bool metaCanRefund(string id, MetaRec m = null)
    {
        m ??= metaCur();
        return metaHas(id, m) && !metaWouldStrand(id, m);
    }
    public static bool metaRefund(string id)
    {
        var m = metaCur();
        if (!metaCanRefund(id, m)) return false;
        m.nodes = m.nodes.Where(x => x != id).ToList();
        metaSave(); return true;
    }
    public static void metaRespec() { var m = metaCur(); m.nodes = new(); metaSave(); }
    /* SAVE is a snapshot of what is placed now, under the slot's name. */
    public static bool metaPresetSave(int i)
    {
        var m = metaCur(); var pr = m.presets != null && i >= 0 && i < m.presets.Count ? m.presets[i] : null;
        if (pr == null) return false;
        pr.nodes = m.nodes.ToList();
        metaSave(); return true;
    }
    static readonly System.Text.RegularExpressions.Regex WS_RUN = new(@"\s+");
    public static bool metaPresetRename(int i, string name)
    {
        var m = metaCur(); var pr = m.presets != null && i >= 0 && i < m.presets.Count ? m.presets[i] : null;
        if (pr == null) return false;
        string t = WS_RUN.Replace(name ?? "", " ").Trim().slice(0, META_PRESET_NAME_MAX);
        pr.name = Js.T(t) ? t : metaPresetBlank(i).name;
        metaSave(); return true;
    }
    /* LOAD IS A RESPEC INTO THE SAVED SHAPE, and it is BOUGHT, not copied: the
       nodes are placed through the same rules a click obeys -- connected to what is
       held, unlocked, affordable -- so a preset can never put the tree into a state
       the tree could not have reached by hand. Several passes, because a build that
       was refunded in the middle can hold a node before the one it now hangs from.
       Whatever cannot be placed (a ceiling you have not reached, a node locked
       behind a wave) is reported rather than forced. Never mid-run: that would be
       a refund, and refunds wait for the run to end. */
    public static MetaPresetPlaced metaPresetLoad(int i)
    {
        if (state == "tree") return null;
        var m = metaCur(); var pr = m.presets != null && i >= 0 && i < m.presets.Count ? m.presets[i] : null;
        if (pr == null || pr.nodes.Count == 0) return null;
        m.nodes = new();
        bool grew = true; int guard = 0;
        while (grew && guard++ < 50)
        {
            grew = false;
            foreach (var id in pr.nodes)
            {
                if (m.nodes.IndexOf(id) >= 0) continue;
                var n = META_BY_ID.GetValueOrDefault(id);
                if (n != null && metaCanBuy(n, m)) { m.nodes.Add(id); grew = true; }
            }
        }
        metaSave();
        return new MetaPresetPlaced { placed = m.nodes.Count, of = pr.nodes.Count };
    }
    public static double metaPresetCost(MetaPreset pr)
    {
        double t = 0;
        foreach (var id in pr.nodes) { var n = META_BY_ID.GetValueOrDefault(id); if (n != null) t += metaCost(n); }
        return t;
    }
    public static bool metaToggle() { if (META == null) metaLoad(); META.on = !META.on; metaSave(); return META.on; }
    /* A COST RISE CAN LEAVE A SAVE OVERSPENT, and a layout change can leave it
       disconnected. Refund the newest nodes until both hold. */
    public static void metaTrim()
    {
        foreach (var d in DIFFS.Keys) foreach (var w in WEAPON_KEYS)
            {
                var m = META[d][w];
                int guard = 0;
                while (m.nodes.Count > 0 && guard++ < 500)
                {
                    if (metaPointsSpent(m) > metaPointsEarned(m)) { m.nodes.RemoveAt(m.nodes.Count - 1); continue; }
                    string start = (META_STARTS.Find(x => x.weap == w) ?? META_STARTS[0]).id;
                    var reached = new List<string> { start };
                    bool grew = true; int g2 = 0;
                    while (grew && g2++ < 200)
                    {
                        grew = false;
                        foreach (var k in m.nodes)
                        {
                            if (reached.IndexOf(k) >= 0) continue;
                            var n = META_BY_ID.GetValueOrDefault(k);
                            if (n != null && n.req.Any(r => reached.IndexOf(r) >= 0)) { reached.Add(k); grew = true; }
                        }
                    }
                    var orphan = m.nodes.Find(k => reached.IndexOf(k) < 0);
                    if (orphan == null) break;
                    m.nodes = m.nodes.Where(k => k != orphan).ToList();
                }
            }
    }

    /* ---- what it does to a hero ---- */
    public static void applyMetaTree(Ent p)
    {
        if (META == null) metaLoad();
        /* a linked run carries the tree it was copied with, whatever this browser's is */
        if (runLink != null && runLink.t != null)
        {
            if (!runLink.on) return;
            foreach (var id in runLink.t) { var n = META_BY_ID.GetValueOrDefault(id ?? ""); if (n != null) metaApplyNode(n, p); }
            metaClampFlags(p);
            return;
        }
        if (!META.on) return;
        var m = metaCur();
        foreach (var id in m.nodes)
        {
            var n = META_BY_ID.GetValueOrDefault(id);
            if (n == null || metaLocked(n, m)) continue;
            metaApplyNode(n, p);
        }
        metaClampFlags(p);
    }
    /* Pools are bound by the ceilings on the numbers they feed. THESE ARE NOT
       POOLS — they are switches and multipliers nothing rebuilds and nothing caps,
       so the one hole in invariant 4 is closed by hand rather than by trusting
       every future node to be reasonable. */
    public static readonly NumMap META_FLAG_CAP = new() { ["tollDelay"] = 30, ["rerollBonus"] = 3, ["chestCards"] = 3, ["bankMul"] = 4 };
    public static void metaClampFlags(Ent p)
    {
        p.tollDelay = clamp(Js.Or(p.tollDelay, 0), 0, META_FLAG_CAP["tollDelay"]);
        p.rerollBonus = clamp(Js.Or(p.rerollBonus, 0), 0, META_FLAG_CAP["rerollBonus"]);
        p.chestCards = clamp(Js.Or(p.chestCards, 1), 1, META_FLAG_CAP["chestCards"]);
        p.bankMul = clamp(Js.Or(p.bankMul, 1), 1, META_FLAG_CAP["bankMul"]);
    }
    /* EVERYTHING THE TREE IS GIVING YOU, totalled — good and bad, in one list, read
       off the same `mods` the tooltips read so the two can never disagree. */
    public static List<MetaStatLine> metaStatLines(MetaRec m = null)
    {
        m ??= metaCur();
        string wk = metaWeapon();
        /* insertion-ordered totals: a number, a switch (no value) or a word */
        var keys = new List<string>(); var tot = new Dictionary<string, Mod>();
        void put(string k, Mod v) { if (!tot.ContainsKey(k)) keys.Add(k); tot[k] = v; }
        foreach (var id in m.nodes)
        {
            var n = META_BY_ID.GetValueOrDefault(id);
            if (n == null || metaLocked(n, m)) continue;
            foreach (var mod in n.mods)
            {
                var st = META_STATS.GetValueOrDefault(mod.k ?? "");
                if (st == null) continue;
                if (st.only != null && Array.IndexOf(st.only, wk) < 0) continue;   /* no use to this weapon */
                var prev = tot.GetValueOrDefault(mod.k);
                if (mod.s != null)
                {
                    /* a word adds the way JS adds it: (tot[k]||0) + 'orbit' */
                    string before = prev == null ? "0" : prev.s ?? (prev.num ? Js.Num(prev.v) : "0");
                    put(mod.k, new Mod(mod.k) { s = before + mod.s });
                    continue;
                }
                if (!mod.num) { put(mod.k, new Mod(mod.k)); continue; }
                if (prev != null && prev.s != null) { put(mod.k, new Mod(mod.k) { s = prev.s + Js.Num(mod.v) }); continue; }
                double pv = prev != null && prev.num ? prev.v : double.NaN;
                if (mod.k == "bankMul") put(mod.k, new Mod(mod.k) { v = (double.IsNaN(pv) || pv == 0 ? 1 : pv) * mod.v });
                else put(mod.k, new Mod(mod.k) { v = (double.IsNaN(pv) ? 0 : pv) + mod.v });
            }
        }
        return keys.Select(k => new MetaStatLine
        {
            key = k, text = metaModText(tot[k]),
            col = META_STATS.GetValueOrDefault(k)?.col ?? "",
            good = metaModGood(tot[k]),
        }).ToList();
    }
    /* ---- THE TREE, DURING A RUN ----
       A level used to be worth a skill point only once you DIED: the record was
       written by metaBank at the end, so a hero who reached level 38 on a fresh
       character spent the whole run with the points they were earning sitting in
       the future. The records are written the moment they are set now -- a new
       best level on the level-up, a new best wave on the clear -- and the tree
       opens on T mid-run, with the game frozen behind it, to spend them.
       `metaRun` is what the records were when THIS run began, so the death screen
       can still say what the run was worth: without it, a record already banked
       live would read as "nothing new". */
    public static MetaRunSnap metaRun = null;
    public static double metaLiveBank()
    {
        if (metaRun == null || META == null || !META.on || player == null || state != "play") return 0;
        var m = metaCur();
        double before = metaPointsEarned(m);
        double lvl = Math.Max(0, Js.Or(player.level, 0)), done = wavesCleared();
        bool moved = false;
        if (lvl > m.best.level) { m.best.level = lvl; moved = true; }
        if (done > m.best.wave) { m.best.wave = done; moved = true; }
        if (!moved) return 0;
        metaSave();
        double got = metaPointsEarned(m) - before;
        if (got > 0)
        {
            logEvent("+" + S(got) + " SKILL POINT" + (got > 1 ? "S" : "") + " — press T to place", "#8fd4ff");
            addDmg(player.x, player.y - 74, "+" + S(got) + " SKILL POINT · T", "#8fd4ff", 15);
        }
        return got;
    }
    /* A NODE BOUGHT MID-RUN LANDS ON THE HERO NOW. The tree only ever writes
       POOLS (invariant 4), so applying one node on top of a live hero is exactly
       what applying it at the start would have done, and syncStats rebuilds every
       number that reads them. A node that hands over a BRICK hands it over now. */
    public static void metaGrantBricks(List<string> list)
    {
        foreach (var g in list)
        {
            var u = UPGRADES.Find(x => SPELL_GRANT.GetValueOrDefault(x.id) == g);
            if (u == null) continue;
            try { if (u.req != null && !u.req()) continue; } catch { continue; }
            try { applyPick(u, "common"); } catch { }
        }
    }
    public static bool metaBuyLive(string id)
    {
        if (!metaBuy(id)) return false;
        if (state == "tree" && player != null && META != null && META.on)
        {
            var n = META_BY_ID[id];
            int had = player.metaGrants?.Count ?? 0;
            metaApplyNode(n, player);
            metaClampFlags(player);
            metaGrantBricks((player.metaGrants ?? new()).Skip(had).ToList());
            syncStats();
            logEvent("TREE — " + n.name, "#8fd4ff");
        }
        return true;
    }
    /* ---- what a finished run is worth ---- */
    public static MetaBankResult metaBank()
    {
        if (META == null) metaLoad();
        var m = metaCur(); var p = player;
        /* measured from where THIS RUN began, not from the records -- which the run
           may already have moved, live, on the level-up and the wave clear */
        double before = metaRun != null ? metaRun.earned : metaPointsEarned(m);
        double collected = Math.Max(0, Math.Round(Js.Or(p.studsTotal, 0)));
        double banked = Math.Floor(META_BANK_K * Math.Sqrt(collected) * Js.Or(p.bankMul, 1));
        m.studsEarned += banked;
        /* THE WAVE YOU CLEARED, not the one you died on. Dying to the wave-30 boss is
           a wave-29 run — the banner said otherwise and it was simply wrong. */
        double done = wavesCleared();
        /* EVERY FIRST CARRIES WHAT IT IS WORTH. It used to be a bare string and the
           run-end screen printed "+1 skill point" under all of them, which was a lie
           the moment a wave first was worth two -- and is a bigger one now a level is
           worth two of its own. */
        var firsts = new List<MetaFirst>();
        /* THE HIGH-WATER MARK. Beating your own best by two waves is worth two
           points; beating it by nothing is worth nothing, however long the run was. */
        double wasWave = metaRun != null ? metaRun.wave : m.best.wave;
        if (done > wasWave)
        {
            firsts.Add(new MetaFirst { text = "wave " + S(done), pts = done - wasWave });
            m.best.wave = Math.Max(m.best.wave, done);
        }
        double lvl = Math.Max(0, Js.Or(p.level, 0)), wasLvl = metaRun != null ? metaRun.level : m.best.level;
        if (lvl > wasLvl)
        {
            firsts.Add(new MetaFirst { text = "level " + S(lvl), pts = (lvl - wasLvl) * META_POINTS_PER_LEVEL });
            m.best.level = Math.Max(m.best.level, lvl);
        }
        metaSave();
        /* A BANKED RUN IS OVER. Its snapshot goes with it, so nothing can bank it
           live again and a second call (there should never be one) reads the save
           like it always did instead of paying the same run twice. */
        metaRun = null;
        return new MetaBankResult
        {
            banked = banked, collected = collected, firsts = firsts, cleared = done, level = lvl,
            gained = metaPointsEarned(m) - before,
            total = m.studsEarned, left = metaPointsLeft(m),
        };
    }
    /* A SPELL YOU HAVE ACTUALLY FINISHED. Checked whenever a brick lands, because
       the last cap can be met mid-run and a point earned at wave 30 should not have
       to wait for you to die before it exists. */
    public static List<string> metaMastery(Ent p = null)
    {
        if (META == null) metaLoad();
        var m = metaCur(); var got = new List<string>();
        foreach (var k in META_MASTERY.Keys)
        {
            if (m.mastery.IndexOf(k) >= 0) continue;
            bool ok;
            try { ok = META_MASTERY[k].done(p ?? player); } catch { ok = false; }
            if (!ok) continue;
            m.mastery.Add(k); got.Add(k);
        }
        if (got.Count > 0) metaSave();
        return got;
    }
    public static bool metaBossKill(string key)
    {
        if (!Js.T(key)) return false;
        if (META == null) metaLoad();
        var m = metaCur();
        if (m.bosses.IndexOf(key) >= 0) return false;
        m.bosses.Add(key); metaSave(); return true;
    }
}
