namespace Brickblade;

/* =============================== CONSTANTS, part 3 (JS 2401–3366) =============================== */
public sealed class ConvParcel { public string type; public double amount; public List<string> via; }
public sealed class ConvPart { public string type; public double @base, amount; }
public sealed class ConvSplit { public string from, kind; public NumMap share; public double mul = 1; }
public sealed class StackDef
{
    public string name, col;
    public double max, decay = double.NaN, grace = double.NaN, rate;
    public bool frac, mob;
    public NumMap mods;
    public string[] only, not;
}
public sealed class StackSt { public double n, t; }
public sealed class Curse { public string name, col, tip; public double step, cap; }
public sealed class Cave { public double x, y, r; }
public sealed class Cam { public double x, y, shake; }
/* one stack of a lingering damage: a burn, a dose, a wound */
public sealed class Dot { public double dps, t, max, n; public string src; }
public sealed class DotList { public List<Dot> list = new(); public double tick, acc, brkCd; }
public sealed class EPoison { public double n, dps, t, tick, acc, max; }
public sealed class Frozen { public double n, t; }

public static partial class G
{
    public static List<ConvParcel> convParcels(double b, string from, Conv c)
    {
        if (!(b > 0) || from == null || !CONV_RANK.ContainsKey(from)) return new List<ConvParcel>();
        var live = new List<ConvParcel> { new ConvParcel { type = from, amount = b, via = new List<string> { from } } };
        foreach (var a in CONV_ORDER)
        {
            var next = new List<ConvParcel>();
            foreach (var p in live)
            {
                if (p.type != a) { next.Add(p); continue; }
                var gainRow = c?.gain?.GetValueOrDefault(a) ?? new NumMap();
                var shares = convShares(c, a);
                double moved = 0;
                foreach (var to in CONV_ORDER)
                {
                    if (!convLegal(a, to)) continue;
                    if (shares[to] > 0)
                    {
                        next.Add(new ConvParcel { type = to, amount = p.amount * shares[to], via = p.via.Append(to).ToList() });
                        moved += shares[to];
                    }
                    if (gainRow[to] > 0)
                        next.Add(new ConvParcel { type = to, amount = p.amount * gainRow[to], via = p.via.Append(to).ToList() });
                }
                double kept = p.amount * (1 - moved);
                if (kept > 1e-12) next.Add(new ConvParcel { type = a, amount = kept, via = p.via });
            }
            live = next;
        }
        return live;
    }
    public static NumMap convertDamage(double b, string from, Conv c)
    {
        var o = new NumMap();
        foreach (var k in CONV_ORDER) o[k] = 0;
        foreach (var p in convParcels(b, from, c)) o[p.type] += p.amount;
        return o;
    }
    public static List<ConvPart> convParts(double b, string from, Conv c, Func<string, List<string>, double> incOf)
    {
        var tot = new NumMap(); var raw = new NumMap();
        foreach (var k in CONV_ORDER) { tot[k] = 0; raw[k] = 0; }
        foreach (var p in convParcels(b, from, c))
        {
            double mul = 1;
            try { mul = incOf != null ? incOf(p.type, p.via) : 1; } catch { mul = 1; }
            raw[p.type] += p.amount;
            tot[p.type] += p.amount * mul;
        }
        var parts = new List<ConvPart>();
        foreach (var k in CONV_ORDER)
            if (raw[k] > 0) parts.Add(new ConvPart { type = k, @base = raw[k], amount = tot[k] });
        return parts;
    }
    public static readonly JsMap<string, string> CONV_ELEM = new() { ["phys"] = null, ["shock"] = "shock", ["frost"] = "frost", ["fire"] = "fire", ["chaos"] = "chaos" };
    public static readonly JsMap<string, string> CONV_RESIST = new() { ["shock"] = "shock", ["frost"] = "frost", ["fire"] = "fire", ["chaos"] = "chaos" };
    public static readonly Tbl CONV_CARD = new double[] { 0.10, 0.15, 0.20, 0.30, 0.40 };
    public static readonly Tbl GAIN_CARD = new double[] { 0.06, 0.10, 0.15, 0.22, 0.30 };
    public const double CHAOS_POISON_S = 0.30, CHAOS_POISON_T = 4;
    public static readonly JsMap<string, string> SOURCE_ELEM = new()
    {
        ["fire"] = "fire", ["burn"] = "fire", ["ember"] = "fire", ["frost"] = "frost", ["freeze"] = "frost",
        ["chain"] = "shock", ["shock"] = "shock", ["bane"] = "chaos", ["poison"] = "chaos",
        ["slamfire"] = "fire", ["slamfrost"] = "frost", ["slamshock"] = "shock",
        ["mfire"] = "fire", ["mfrost"] = "frost", ["mshock"] = "shock", ["mburst"] = "fire", ["mchaos"] = "chaos"
    };
    public const double ERES_CAP = 0.90;
    public static double frozenMarks(Ent e) => e != null && e.frozen != null && e.frozen.t > 0 ? e.frozen.n : 0;
    /* PENETRATION */
    public const double PEN_MAX = 0.60;
    public static readonly string[] PEN_KEYS = { "fire", "frost", "shock", "chaos" };
    public static readonly JsMap<string, string> PEN_NAME = new() { ["fire"] = "Fire", ["frost"] = "Cold", ["shock"] = "Lightning", ["chaos"] = "Chaos" };
    public static double penOf(string el)
    {
        var P = player?.pen;
        if (P == null) return 0;
        return clamp(P[el] + (el != "chaos" ? P["elem"] : 0), 0, PEN_MAX);
    }
    public static double penRes(Ent e, string el, string source)
    {
        double r = enemyRes(e, el);
        if (!(r > 0) || MINION_SOURCES.Contains(source ?? "")) return r;
        return Math.Max(0, r - penOf(el));
    }
    public static double enemyRes(Ent e, string el)
    {
        if (e == null || el == null) return 0;
        double r = Math.Min(ERES_CAP, e.res != null ? e.res[el] : 0);
        if (el == "frost") r -= FROZEN_RES_STEP * frozenMarks(e);
        r -= EXPOSE_STEP * exposedOn(e, el);
        return r;
    }
    /* EXPOSURE (switched off as something a player can get; the mechanic stays) */
    public const double EXPOSE_STEP = 0.22, EXPOSE_T = 6, EXPOSE_CHANCE_MAX = 1.0;
    public static bool EXPOSE_ON = false;
    public static readonly string[] EXPOSE_ELEMS = { "fire", "frost", "shock", "chaos" };
    public static readonly JsMap<string, string> EXPOSE_COL = new() { ["fire"] = "#ff6a2a", ["frost"] = "#7fb9e0", ["shock"] = "#e0a81a", ["chaos"] = "#b46aff" };
    public static readonly JsMap<string, string> EXPOSE_NAME = new() { ["fire"] = "SCORCHED", ["frost"] = "BRITTLE", ["shock"] = "SAPPED", ["chaos"] = "WITHERED" };
    public static double exposedOn(Ent e, string el)
    {
        if (e == null || e.expose == null) return 0;
        return e.expose[el] > 0 ? 1 : 0;
    }
    public static void exposeEnemy(Ent e, string el)
    {
        if (e == null || e.dead || e.state != "active" || Array.IndexOf(EXPOSE_ELEMS, el) < 0) return;
        if (e.expose == null) e.expose = new NumMap();
        bool fresh = !(e.expose[el] > 0);
        e.expose[el] = EXPOSE_T;
        if (fresh)
        {
            addHitNum(e.x, e.y - 44 * Js.Or(e.scale, 1), EXPOSE_NAME[el], EXPOSE_COL[el], 14);
            addSparks(e.x, e.y - 18 * Js.Or(e.scale, 1), 4, EXPOSE_COL[el]);
        }
    }
    public static void exposeTick(Ent e, double dt)
    {
        if (e == null || e.expose == null) return;
        foreach (var el in EXPOSE_ELEMS)
            if (e.expose[el] > 0) e.expose[el] = Math.Max(0, e.expose[el] - dt);
    }
    public static double exposeCount(Ent e) { double n = 0; foreach (var el in EXPOSE_ELEMS) n += exposedOn(e, el); return n; }
    /* CONVERSION, APPLIED TO A REAL HIT */
    public static string convFrom(string source) => source != null && SOURCE_ELEM.TryGetValue(source, out var v) ? v : "phys";
    public static double arrowAilBonus(string source, string type)
    {
        var k = player?.bow?.kind;
        if (k == null || !ARROW_SOURCES.Contains(source ?? "")) return 0;
        var K = ARROW_KINDS.GetValueOrDefault(k);
        return K != null && K.to == type ? K.ail : 0;
    }
    public static Conv convTableFor(string source)
    {
        if (MINION_SOURCES.Contains(source ?? "") && !(player != null && player.minions != null && player.minions.mirror)) return null;
        var bse = player?.conv;
        var k = player?.bow?.kind;
        if (k == null || !ARROW_SOURCES.Contains(source ?? "")) return bse;
        var K = ARROW_KINDS[k];
        var c = blankConv();
        c.conv["phys"][K.to] = K.conv;
        if (bse != null)
        {
            var row = convShares(bse, "phys");
            foreach (var to in row.Keys.ToList()) c.conv["phys"][to] = c.conv["phys"][to] + row[to] * (1 - K.conv);
            foreach (var a in CONV_ORDER)
            {
                if (a != "phys") foreach (var to in bse.conv[a].Keys.ToList()) c.conv[a][to] = bse.conv[a][to];
                foreach (var to in bse.gain[a].Keys.ToList()) c.gain[a][to] = bse.gain[a][to];
            }
        }
        c.on = true;
        return c;
    }
    public static bool isAttack(string source) =>
        source == null || MELEE_SOURCES.Contains(source) || ARROW_SOURCES.Contains(source)
        || source == "slamfire" || source == "slamfrost" || source == "slamshock";
    public static string convKind(string source) =>
        SPELL_SOURCES.Contains(source ?? "") || source == "fire" || source == "frost" || source == "burn" || source == "freeze" ? "spell" : "melee";
    public static readonly JsMap<string, ConvSplit> CONV_PLAIN = CONV_ORDER.ToJsMap(k => k, k => new ConvSplit { from = k, share = new NumMap((k, 1)), mul = 1 });
    public static double incVia(string kind, List<string> via)
    {
        var p = player;
        if (p == null) return 0;
        double el = 0; bool anyElem = false;
        foreach (var t in via)
        {
            if (t == "chaos") { el += p.inc["chaos"]; continue; }
            var e = CONV_ELEM.GetValueOrDefault(t);
            if (e == null) continue;
            anyElem = true;
            el += p.inc[e];
        }
        if (anyElem) el += p.inc["elem"] + weapon.incElem;
        return Math.Min(INC_DMG_CAP, p.incAll + (kind == "melee" ? incMeleeAll() : kind == "spell" ? incSpellAll() : 0) + el);
    }
    public static ConvSplit convSplit(string source)
    {
        string from = convFrom(source);
        var C = player != null ? convTableFor(source) : null;
        if (C == null || !C.on) return CONV_PLAIN[from];
        string kind = convKind(source);
        double plain = 1 + incTotal(kind, CONV_ELEM[from]);
        var parts = convParts(1, from, C, (k, via) => 1 + incVia(kind, via));
        double total = 0;
        foreach (var pt in parts) total += pt.amount;
        var share = new NumMap();
        foreach (var pt in parts) share[pt.type] = total > 0 ? pt.amount / total : 0;
        return new ConvSplit { from = from, kind = kind, share = share, mul = plain > 0 ? total / plain : 1 };
    }
    /* STACKS THE HERO CARRIES: momentum, rage, and the three charges */
    public static readonly JsMap<string, StackDef> HERO_STACKS = new()
    {
        ["momentum"] = new StackDef { name = "MOMENTUM", col = "#e0a81a", max = 6, decay = 2.0, mods = new NumMap(("atk", 0.08), ("reach", 0.04)), only = new[] { "axe" } },
        ["rage"] = new StackDef { name = "RAGE", col = "#ff4a3c", max = 30, grace = 2.0, rate = 10, frac = true, mob = true, mods = new NumMap(("moreDmg", 0.01)) },
        ["frenzy"] = new StackDef { name = "FRENZY", col = "#6ee06e", max = 3, decay = 4.0, not = new[] { "scepter" }, mods = new NumMap(("atk", 0.04), ("cast", 0.04), ("moreAll", 0.04)) },
        ["power"] = new StackDef { name = "POWER", col = "#b46aff", max = 3, decay = 4.0, not = new[] { "scepter" }, mods = new NumMap(("crit", 0.30)) },
        ["endurance"] = new StackDef { name = "ENDURANCE", col = "#ff8a3c", max = 3, decay = 4.0, mods = new NumMap(("less", 0.05), ("res", 0.04)) },
    };
    public static readonly string[] CHARGE_IDS = { "frenzy", "power", "endurance" };
    public static bool chargeGenRoom(string id, double have) => have < stackCap(id);
    public const double CHARGE_CAP_MAX = 9, RAGE_CAP_MAX = 80;
    public static StackDef stackDef(string id) => id != null ? HERO_STACKS.GetValueOrDefault(id) : null;
    public static bool stackAllows(string id, Ent ent = null)
    {
        var d = stackDef(id);
        if (d == null) return false;
        if (ent != null && ent != player) return d.mob && ent.stackOn != null && ent.stackOn.Contains(id);
        if (d.only != null && (weapon == null || Array.IndexOf(d.only, weapon.id) < 0)) return false;
        if (d.not != null && weapon != null && Array.IndexOf(d.not, weapon.id) >= 0) return false;
        return true;
    }
    public static double stackCap(string id, Ent ent = null)
    {
        var d = stackDef(id);
        if (d == null) return 0;
        var o = ent ?? player;
        return d.max + (o != null && o.stackMax != null ? o.stackMax[id] : 0);
    }
    public static double stackOf(string id, Ent ent = null)
    {
        var o = ent ?? player;
        StackSt st = null;
        if (o != null && o.stacks != null) o.stacks.TryGetValue(id, out st);
        return st != null && st.n > 0 && stackAllows(id, o) ? st.n : 0;
    }
    public static double stackAdd(string id, double n = double.NaN, Ent ent = null)
    {
        var o = ent ?? player; var d = stackDef(id);
        if (o == null || d == null || !stackAllows(id, o)) return 0;
        if (o.stacks == null) o.stacks = new();
        if (!o.stacks.TryGetValue(id, out var st)) o.stacks[id] = st = new StackSt();
        st.n = Math.Min(stackCap(id, o), st.n + (double.IsNaN(n) ? 1 : n));
        st.t = !double.IsNaN(d.grace) ? d.grace : d.decay;
        return st.n;
    }
    public static void stackClear(string id, Ent ent = null)
    {
        var o = ent ?? player;
        if (o != null && o.stacks != null && o.stacks.TryGetValue(id, out var st)) { st.n = 0; st.t = 0; }
    }
    public static double stackMod(string key, Ent ent = null)
    {
        var o = ent ?? player;
        if (o == null || o.stacks == null) return 0;
        double v = 0;
        foreach (var id in o.stacks.Keys)
        {
            double n = stackOf(id, o);
            if (!Js.T(n)) continue;
            var m = stackDef(id).mods;
            if (m != null && Js.T(m[key])) v += m[key] * n;
        }
        return v;
    }
    public static void stackTick(double dt, Ent ent = null)
    {
        var o = ent ?? player;
        if (o == null || o.stacks == null) return;
        foreach (var (id, st) in o.stacks)
        {
            var d = stackDef(id);
            if (st == null || d == null || st.n <= 0) continue;
            if (!stackAllows(id, o)) { st.n = 0; st.t = 0; continue; }
            st.t -= dt;
            if (st.t > 0) continue;
            if (!double.IsNaN(d.grace))
            {
                st.n = Math.Max(0, st.n - d.rate * dt);
                if (!d.frac) st.n = Math.Floor(st.n);
            }
            else { st.n--; st.t = !double.IsNaN(d.decay) ? d.decay : 1; }
        }
    }
    public static double rageMore(Ent ent = null) => 1 + stackMod("moreDmg", ent);

    /* EVERY AILMENT, ONE RULE: a property of the damage type, not the spell */
    public static readonly NumMap AIL_BASE = new(("phys", 0), ("frost", 0), ("fire", 0), ("shock", 0), ("chaos", 0));
    public static readonly NumMap AIL_CRIT = new(("phys", 0), ("frost", 1), ("fire", 1), ("shock", 1), ("chaos", 0));
    public static readonly HashSet<string> AIL_NATIVE = new() { "fire", "ember", "frost", "freeze", "slamfire", "slamfrost", "slamshock" };
    public static readonly JsMap<string, double> AIL_NATIVE_CHANCE = new() { ["fire"] = 0.25 };
    public static double ailNativeChance(string source) => source != null && AIL_NATIVE_CHANCE.TryGetValue(source, out var v) ? v : 1;
    public static readonly JsMap<string, string> AIL_NAME = new() { ["phys"] = "BLEED", ["frost"] = "CHILL", ["fire"] = "IGNITE", ["shock"] = "SHOCK", ["chaos"] = "POISON" };
    public const double AIL_MAG_MIN = 0.012, AIL_MAG_FULL = 0.10;
    public const int AIL_STACK_HIT = 3;
    public const double AIL_POOL_BOSS = 0.05;
    public static double ailPool(Ent e)
    {
        double hp = Math.Max(1, e != null ? Js.Or(e.maxHp, 1) : 1);
        return e != null && (e.type == "boss" || e.boss) ? hp * AIL_POOL_BOSS : hp;
    }
    public static double ailMag(Ent e, double part)
    {
        if (!(part > 0)) return 0;
        if (player != null && player.ailFull) return 1;
        double share = part / ailPool(e);
        double m = (share - AIL_MAG_MIN) / (AIL_MAG_FULL - AIL_MAG_MIN);
        return clamp(m * (1 + (player != null ? player.ailMagInc : 0)), 0, 1);
    }
    public static double ailStacks(Ent e, double part, bool floorOne)
    {
        double m = ailMag(e, part);
        if (m <= 0) return floorOne ? 1 : 0;
        return Math.Max(1, Math.Round(m * AIL_STACK_HIT));
    }
    public static double ailChanceOf(string type, string source, bool crit)
    {
        var p = player;
        if (p == null) return 0;
        if (crit && p.critAilAll && !(type == "phys" && !isAttack(source))) return 1;
        if (source != null && AIL_NATIVE.Contains(source) && convFrom(source) == type)
        {
            double nat = ailNativeChance(source);
            if (nat >= 1) return 1;
            return clamp(nat + AIL_BASE[type] + (crit ? AIL_CRIT[type] : 0) + p.ailChance + ailTypeChance(type), 0, 1);
        }
        if (type == "phys" && !isAttack(source)) return 0;
        double ch = AIL_BASE[type] + (crit ? AIL_CRIT[type] : 0) + p.ailChance + ailTypeChance(type) + arrowAilBonus(source, type);
        if (type == "phys") ch += p.bleedChance;
        if (type == "phys" && (source == null || source == "sword")) ch *= weapon.bleed;
        return clamp(ch, 0, 1);
    }
    public static string ailTableText()
    {
        var rows = new[] { ("phys", "Physical", "BLEED"), ("fire", "Fire", "IGNITE"), ("frost", "Cold", "CHILL"), ("shock", "Lightning", "SHOCK"), ("chaos", "Chaos", "POISON") };
        return string.Join("\n", rows.Select(r =>
        {
            var (t, el, name) = r;
            double hit = Math.Round(ailChanceOf(t, null, false) * 100);
            double crit = Math.Round(ailChanceOf(t, null, true) * 100);
            string tail = t == "phys" ? "   (attacks only)" : "";
            return "  " + el + " / " + name + " — " + N(hit) + "% on a hit, " + N(crit) + "% on a crit" + tail;
        })) + "\n";
    }
    public static void applyAilments(Ent e, double d, string source, NumMap mix, bool crit, HitOpts opts)
    {
        if (e == null || e.dead || e.state != "active") return;
        var p = player;
        if (p == null) return;
        foreach (var t in CONV_ORDER)
        {
            double share = mix[t];
            if (!(share > 0)) continue;
            bool sure = crit && p.critAilAll && !(t == "phys" && !isAttack(source));
            bool own = opts != null && opts.ailSure && source != null && AIL_NATIVE.Contains(source) && convFrom(source) == t;
            double ch = sure ? 1 : (own ? 1 : ailChanceOf(t, source, crit)) * share;
            if (!(ch > 0) || Js.R() >= ch) continue;
            double part = d * share;
            if (t == "phys") bleedEnemy(e, part);
            else if (t == "fire") burnEnemy(e, part, opts != null ? opts.ailDepth : 0);
            else
            {
                bool native = source != null && AIL_NATIVE.Contains(source) && convFrom(source) == t;
                double n = Math.Max(opts != null ? opts.ailN : 0, ailStacks(e, part, native));
                if (t == "frost") { if (n > 0) chillEnemy(e, n); }
                else if (t == "shock") { if (n > 0) shockEnemy(e, n); }
                else if (t == "chaos") poisonEnemy(e, dotRate(part * CHAOS_POISON_S, 0, true), CHAOS_POISON_T);
            }
        }
    }
    public static void applyExposure(Ent e, NumMap mix)
    {
        var p = player;
        if (p == null || !(p.exposeChance > 0) || e == null || e.dead || e.state != "active") return;
        foreach (var el in EXPOSE_ELEMS)
        {
            double share = mix[el];
            if (!(share > 0)) continue;
            if (Js.R() < p.exposeChance * share) exposeEnemy(e, el);
        }
    }
    /* PROLIFERATION: a keystone; what spreads is the ailment, weaker */
    public const double PROLIF_KEEP = 0.6;
    public static void proliferate(Ent e)
    {
        var p = player; double r = p.prolifR;
        if (!(r > 0) || e == null) return;
        var burns = e.eburn != null && e.eburn.list.Count > 0 ? e.eburn.list.ToList() : null;
        var psn = e.poison != null && e.poison.n > 0 ? e.poison : null;
        var wounds = bleedList(e);
        if (burns == null && psn == null && !(wounds != null && wounds.Count > 0)) return;
        void copyBurn(Ent e2, Dot f)
        {
            if (e2.eburn == null) e2.eburn = new DotList { tick = rnd(0.05, EBURN_TICK) };
            var L = e2.eburn.list; double dps = f.dps * PROLIF_KEEP;
            if (L.Count < burnCap()) L.Add(new Dot { dps = dps, t = f.t });
            else
            {
                int wi = 0;
                for (int i = 1; i < L.Count; i++) if (L[i].dps < L[wi].dps) wi = i;
                if (dps >= L[wi].dps) L[wi] = new Dot { dps = dps, t = f.t };
            }
        }
        void copyWound(Ent e2, Dot w)
        {
            if (e2.bleed == null) e2.bleed = new DotList { tick = rnd(0.05, BLEED_TICK) };
            var L = e2.bleed.list; double dps = w.dps * PROLIF_KEEP;
            if (L.Count < BLEED_STACK_MAX) L.Add(new Dot { dps = dps, t = w.t });
            else
            {
                int wi = 0;
                for (int i = 1; i < L.Count; i++) if (L[i].dps < L[wi].dps) wi = i;
                if (dps > L[wi].dps) L[wi] = new Dot { dps = dps, t = w.t };
            }
        }
        int touched = 0;
        foreach (var e2 in enemies.ToList())
        {
            if (e2 == e || e2.dead || e2.state != "active") continue;
            if (dist(e, e2) > r + hitR(e2)) continue;
            if (burns != null) foreach (var f in burns) copyBurn(e2, f);
            if (wounds != null) foreach (var w in wounds) copyWound(e2, w);
            if (psn != null) for (int i = 0; i < psn.n; i++) poisonEnemy(e2, psn.dps * PROLIF_KEEP, psn.t);
            touched++;
        }
        if (touched > 0)
        {
            addRing(e.x, e.y, 8, 300, r, "#c96a3c", 7, 0.5);
            addSparks(e.x, e.y - 16, 8, "#ffb07c");
        }
    }
    /* WHAT CAN THIS HERO ACTUALLY DEAL? */
    public static Flags dealtTypes()
    {
        var o = new Flags();
        if (player == null) return o;
        void add(string t) { if (t != null) o.Add(t); }
        if (isCaster()) add(convFrom(weapon.elem ?? "fire"));
        else if (!isSummoner()) add("phys");
        if (isArcher() && player.bow != null && player.bow.kind != null) add(ARROW_KINDS[player.bow.kind].to);
        if (isSlammer())
        {
            add("phys");
            if (player.slam != null && player.slam.elem > 0)
                foreach (var e in SLAM_ELEMS) add(convFrom("slam" + e));
        }
        foreach (var k in SPELL_KEYS) if (spellHas(k)) add(convFrom(k));
        var c = player.conv;
        if (c != null && c.on) foreach (var a in CONV_ORDER)
            {
                if (!o.Contains(a)) continue;
                foreach (var b in CONV_ORDER)
                    if ((c.conv.ContainsKey(a) && c.conv[a][b] > 0) || (c.gain.ContainsKey(a) && c.gain[a][b] > 0)) add(b);
            }
        return o;
    }
    public static bool canDeal(string t) => dealtTypes().Contains(t);
    public static bool heroHits() => dealtTypes().Count > 0 || (isSummoner() && player.minions != null && player.minions.mirror);
    public static bool dealsElemental()
    {
        var d = dealtTypes();
        return d.Contains("fire") || d.Contains("frost") || d.Contains("shock") || (isSummoner() && player.minions != null && player.minions.mirror);
    }
    public static double ailShare(string source, string type) { var sp = convSplit(source); return sp?.share?[type] ?? 0; }
    public static string convOwnTag(string from, string to, bool gain = false)
    {
        var c = player?.conv;
        if (c == null) return "";
        double have = ((gain ? c.gain : c.conv).GetValueOrDefault(from) ?? new NumMap())[to];
        if (!(have > 0)) return "";
        string tag = " (you have " + N(Math.Round(have * 100)) + "%";
        if (!gain)
        {
            double total = 0;
            foreach (var k in CONV_ORDER) if (convLegal(from, k)) total += c.conv[from][k];
            if (total >= 1) tag += ", and every point of " + CONV_NAME[from].ToLowerInvariant() + " is already spoken for";
        }
        return tag + ")";
    }
    public static bool convAny(Conv c = null)
    {
        c = c ?? player?.conv;
        if (c == null) return false;
        foreach (var a in CONV_ORDER) foreach (var b in CONV_ORDER)
            {
                if (!convLegal(a, b)) continue;
                if ((c.conv.GetValueOrDefault(a)?[b] ?? 0) > 0 || (c.gain.GetValueOrDefault(a)?[b] ?? 0) > 0) return true;
            }
        return false;
    }
    public const int EBURN_MAX = 5;
    public const double EBURN_DUR = 9, EBURN_SHARE = 0.30, EBURN_TICK = 0.4, BURNT_STEP = 0.25;
    public const int EBURN_BRAND_AT = 5;
    public const double BURNT_CHANCE = 0.30, BURNT_CHANCE_PER = 0.05, BURNT_T = 16;
    public const int BURNT_MAX = 4;
    /* EMBER SCATTER */
    public const int EMBER_MAX = 2;
    public static readonly Tbl EMBER_SHOTS = new double[] { 0, 2, 5 };
    public static readonly Tbl BURN_CAP_MUL = new double[] { 1, 2, 4 };
    public const double EMBER_SHARE = 0.45, EMBER_SPD = 430;
    public const int EMBER_DEPTH = 3;
    public const double EMBER_CD = 0.25, EMBER_SEEK_R = 520, EMBER_LIT_COST = 460, EMBER_SPREAD_COST = 240;
    public static Ent emberTarget(Obj from, Ent src, JsMap<Ent, double> taken)
    {
        Ent best = null; double bestScore = double.PositiveInfinity;
        foreach (var e2 in enemies)
        {
            if (e2 == src || e2.dead || e2.state != "active") continue;
            double d = dist(e2, from);
            if (d > EMBER_SEEK_R) continue;
            double score = d + (burnStacksOn(e2) > 0 ? EMBER_LIT_COST : 0)
                           + (taken != null && taken.TryGetValue(e2, out var tv) ? tv : 0) * EMBER_SPREAD_COST;
            if (score < bestScore) { bestScore = score; best = e2; }
        }
        return best;
    }
    public static double burnCap()
    {
        int k = player != null ? (int)player.ember : 0;
        return EBURN_MAX * (k >= 0 && k < BURN_CAP_MUL.Length ? BURN_CAP_MUL[k] : 1);
    }
    public static readonly JsMap<string, string> DOT_COL = new() { ["bane"] = "#4f9e3a", ["poison"] = "#4f9e3a", ["bleed"] = "#a3222b", ["burn"] = "#ff9a3c" };
    public const string LEECH_COL = "#8ede4a";
    public const double DMG_SPLIT_MIN = 0.4, DMG_SPLIT_SHARE = 0.06;
    public static string dmgColour(string source, NumMap mix)
    {
        if (source != null && DOT_COL.TryGetValue(source, out var dc)) return dc;
        string best = null; double bv = -1;
        if (mix != null) foreach (var (k, v) in mix) if (v > bv) { bv = v; best = k; }
        return (best != null ? CONV_COL.GetValueOrDefault(best) : null) ?? CONV_COL.GetValueOrDefault(convFrom(source)) ?? "#ffffff";
    }
    public const int SPELL_MAX = 4;
    public static readonly string[] SPELL_KEYS = { "orbit", "chain", "storm", "vortex", "freeze", "bomb", "bane" };
    public static bool spellHas(string k) { var sp = player?.Spell(k); return sp != null && sp.n > 0; }
    public static int spellsOwned() { int n = 0; foreach (var k in SPELL_KEYS) if (spellHas(k)) n++; return n; }
    public static bool canTakeSpell(string k) => spellHas(k) || spellsOwned() < SPELL_MAX;
    public static readonly string[] ELEMENTS = { "fire", "frost", "shock" };
    public static readonly string[] RES_KINDS = ELEMENTS.Concat(new[] { "chaos" }).ToArray();
    public static readonly JsMap<string, string> ELEM_COL = new() { ["fire"] = "#ff8a3c", ["frost"] = "#bfe3ff", ["shock"] = "#ffe95a" };
    /* Imp master curses. They STACK. */
    public static readonly JsMap<string, Curse> CURSES = new()
    {
        ["resist"] = new Curse { name = "SUNDERED", col = "#9b45d6", step = 0.12, cap = 0.90, tip = "elemental resistance cut" },
        ["haste"] = new Curse { name = "LEADEN", col = "#c9b8e8", step = 0.11, cap = 1.50, tip = "swings come slower" },
        ["armor"] = new Curse { name = "BRITTLE", col = "#7fb9e0", step = 0.13, cap = 0.92, tip = "armour weakened" },
        ["regen"] = new Curse { name = "WITHERED", col = "#a5ca18", step = 0.16, cap = 0.95, tip = "regeneration choked" },
    };
    public static readonly List<string> CURSE_KEYS = CURSES.Keys.ToList();
    public const int CURSE_STACK_MAX = 9;
    public static List<Cave> CAVES = new();
    public static bool inCave(double x, double y)
    {
        foreach (var c in CAVES) if (hypot(x - c.x, y - c.y) < c.r) return true;
        return false;
    }
    public static class PAL
    {
        public const string red = "#d3242b", dred = "#8f161d", gold = "#ffb900", hyellow = "#ffcf00",
            blue = "#1668b3", green = "#4b9b41", white = "#f2f3f2", black = "#1b2a34",
            gray = "#9ba19d", dgray = "#6d6e68", orange = "#fe8a18", brown = "#583927",
            tan = "#e4cd9e", lime = "#a5ca18";
    }
    public static Cam cam = new Cam { x = CX, y = CY, shake = 0 };
}
