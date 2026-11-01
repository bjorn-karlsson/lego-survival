// lego-survival.html, JS 30518-30991: THE COMBAT LOG — what you dealt, what you took,
// what happened, the per-ability DPS over the last minute, and the same by damage
// type; the panel that shows them.
namespace Brickblade;

public sealed class ClogLine { public string key; public double t, t0; public string txt; public Col col; public double n, sum; }
public sealed class ClogW { public double t, d; }
public sealed class ClogRunT { public double dealt, taken, kills, elites, bosses, hits, crits, evaded, blocked, leeched; }
public sealed class ClogHitT { public string who; public double n; public string elem; public double t; }
public sealed class DpsBuckets { public double[] b; public double sec; }
public sealed class ClogRect { public int i; public double x, y, w, h; public ClogRect panel; }

public static partial class G
{
    /* =============================== COMBAT LOG ===============================
       Three ledgers, not one stream. Damage you deal, damage you take, and
       everything else that happened — kills, ailments, curses, levels, waves —
       each on its own tab, because a single feed of all three is exactly the
       thing you cannot read while something is chewing on you. Press L to show
       it, click a tab (or shift+L) to change view. Nothing here touches the
       game: it only watches. */
    public const int CLOG_MAX = 200;        /* lines kept per tab */
    public const int CLOG_SHOW = 15;        /* lines drawn */
    public const double CLOG_MERGE = 0.45;  /* seconds within which repeat hits fold into one line */
    public const double CLOG_WINDOW = 5;    /* seconds the DPS meters average over */
    public const int CLOG_OUT = 0, CLOG_IN = 1, CLOG_EVT = 2, CLOG_DPS = 3, CLOG_TYPE = 4;
    public static readonly string[] CLOG_TABS = { "DEALT", "TAKEN", "EVENTS", "DPS", "TYPES" };
    /* PER-ABILITY DPS OVER A MINUTE. The run totals answer "what did the most
       damage all run", which flatters whatever you picked first and buries a spell
       that only came online at wave 30. This answers the balance question instead —
       what is each ability doing RIGHT NOW — by keeping one bucket a second per
       source and rolling them forward. Sixty buckets of a double is nothing, and a
       source that has gone quiet ages out of its own accord rather than sitting at
       its old number for ever. */
    public const int CLOG_DPS_WIN = 60;
    public static JsMap<string, DpsBuckets> clogDps = new();   /* source -> { b:[60], sec } (insertion order kept) */
    static readonly List<string> clogDpsOrder = new();
    public static DpsBuckets dpsSlot(string source)
    {
        if (!clogDps.TryGetValue(source, out var s)) { clogDps[source] = s = new DpsBuckets { b = new double[CLOG_DPS_WIN], sec = Math.floor(runTime) }; clogDpsOrder.Add(source); }
        return s;
    }
    /* ONE wrap, used by everything that indexes a bucket. JavaScript's % keeps the
       sign of its left operand, so a plain `x % WIN` on anything negative indexes
       off the end of the array and silently writes a property instead of a slot —
       which meant a roll could leave stale buckets behind and still look right. */
    public static int dpsIdx(double n) => (int)(((n % CLOG_DPS_WIN) + CLOG_DPS_WIN) % CLOG_DPS_WIN);
    /* age a source's buckets up to `now`, zeroing every second it missed. A gap
       wider than the window means everything it holds is stale. */
    public static void dpsRoll(DpsBuckets s, double now)
    {
        if (now == s.sec) return;
        if (now < s.sec || now - s.sec >= CLOG_DPS_WIN) { Array.Clear(s.b); s.sec = now; return; }
        for (int i = 1; i <= now - s.sec; i++) s.b[dpsIdx(s.sec + i)] = 0;
        s.sec = now;
    }
    public static void dpsAdd(string source, double d)
    {
        var now = Math.floor(runTime); var s = dpsSlot(source);
        dpsRoll(s, now);
        s.b[dpsIdx(now)] += d;
    }
    /* the window is only as long as the run has been going, or a spell picked up
       ten seconds ago reads as a sixth of what it is actually doing */
    public static double dpsWindow() => Math.max(1, Math.min(CLOG_DPS_WIN, runTime));
    public static double dpsOf(string source)
    {
        if (!clogDps.TryGetValue(source, out var s)) return 0;
        dpsRoll(s, Math.floor(runTime));
        double sum = 0; foreach (var v in s.b) sum += v;
        return sum / dpsWindow();
    }
    /* newest-last, so a sparkline reads left to right like every other chart */
    public static List<double> dpsSeries(string source)
    {
        if (!clogDps.TryGetValue(source, out var s)) return new List<double>();
        dpsRoll(s, Math.floor(runTime));
        var outp = new List<double>(); var now = s.sec;
        for (int i = CLOG_DPS_WIN - 1; i >= 0; i--) outp.Add(s.b[dpsIdx(now - i)]);
        return outp;
    }
    public static bool clogOn = false;
    public static int clogView = CLOG_OUT;
    public static ClogHitT clogLastHit = null;
    public static List<ClogLine>[] clogLines = { new(), new(), new() };   /* one scrollback per tab */
    public static List<ClogW> clogOutW = new(), clogInW = new();          /* rolling windows: {t, d} */
    public static NumMap clogSrc = new(), clogFrom = new();               /* run totals by damage source / by attacker */
    public static NumMap clogType = new();                                /* ...and by what the damage was MADE OF */
    /* the one character that keeps the by-type buckets out of the by-source chart */
    public const string DPS_TYPE = "~";
    public static NumMap clogKills = new();                               /* run kill counts by monster */
    public static ClogRunT clogRun = new();
    public static double clogAilAcc = 0;
    public static List<ClogRect> clogTabRects = new();                    /* filled in by the renderer, read by the mouse */
    public static ClogRect clogClearRect = null;
    public static void clogReset()
    {
        clogLines = new List<ClogLine>[] { new(), new(), new() };
        clogOutW = new(); clogInW = new(); clogSrc = new(); clogFrom = new(); clogKills = new(); clogDps = new(); clogDpsOrder.Clear();
        clogType = new();
        clogRun = new ClogRunT();
        clogAilAcc = 0;
    }
    public static void clogToggle()
    {
        clogOn = !clogOn;
        if (clogOn) clogPush(CLOG_EVT, null, "combat log on — click a tab, L to hide", "#9fdc7e");
        else { clogTabRects = new(); clogClearRect = null; }
    }
    public static void clogCycle()
    {
        if (!clogOn) { clogToggle(); return; }
        clogView = (clogView + 1) % CLOG_TABS.Length;
    }
    /* Lines merge while they repeat: twenty orbit ticks in a third of a second are
       one line that counts up, not twenty lines that scroll the tab away. */
    public static void clogPush(int ch, string key, string txt, Col? col = null)
    {
        if (!clogOn && ch != CLOG_IN) return;
        var L = clogLines[ch];
        var last = L.Count > 0 ? L[L.Count - 1] : null;
        if (!string.IsNullOrEmpty(key) && last != null && last.key == key && runTime - last.t0 < CLOG_MERGE)
        {
            last.n++; last.t = runTime; last.txt = txt + " ×" + S(last.n);
            return;
        }
        L.Add(new ClogLine { key = key, t = runTime, t0 = runTime, txt = txt, col = col ?? (Col)"#c9d2ce", n = 1, sum = 0 });
        if (L.Count > CLOG_MAX) L.RemoveAt(0);
    }
    public static void clogPushDmg(int ch, string key, string pre, double d, Col? col = null)
    {
        /* what hits YOU is always written down, open or not: after a death, the log
           is how you find out what did it */
        if (!clogOn && ch != CLOG_IN) return;
        var L = clogLines[ch];
        var last = L.Count > 0 ? L[L.Count - 1] : null;
        if (last != null && last.key == key && runTime - last.t0 < CLOG_MERGE)
        {
            last.n++; last.sum += d; last.t = runTime;
            last.txt = pre + " " + fmtHp(last.sum) + "  ×" + S(last.n);
            return;
        }
        L.Add(new ClogLine { key = key, t = runTime, t0 = runTime, txt = pre + " " + fmtHp(d), col = col ?? (Col)"#c9d2ce", n = 1, sum = d });
        if (L.Count > CLOG_MAX) L.RemoveAt(0);
    }
    public static void clogTrim(List<ClogW> arr)
    {
        var cut = runTime - CLOG_WINDOW;
        int k = 0; while (k < arr.Count && arr[k].t < cut) k++;
        if (k > 0) arr.RemoveRange(0, k);
    }
    public static double clogRate(List<ClogW> arr)
    {
        clogTrim(arr);
        double s = 0; foreach (var e in arr) s += e.d;
        return s / CLOG_WINDOW;
    }
    /* The totals are always kept — switching the log on mid-wave should not hand
       you an empty ledger — but the text lines cost nothing while it is off. */
    public static void logDealt(Ent e, double d, string source, bool crit, bool suppressed, NumMap mix)
    {
        clogRun.dealt += d; clogRun.hits++; if (crit) clogRun.crits++;
        clogSrc[source] = clogSrc[source] + d;
        dpsAdd(source, d);
        /* AND THE SAME DAMAGE AGAIN, BY WHAT IT WAS MADE OF. The DEALT tab and the
           DPS chart answer "which of my spells is doing the work", which is not the
           same question as "what am I actually dealing" — and once conversion exists
           the second one is the one a build is built around. Same buckets, same
           rolling window, keys marked so the two charts never mix. */
        if (mix != null) foreach (var k in mix.Keys)
            {
                var part = d * mix[k];
                if (!(part > 0)) continue;
                clogType[k] = clogType[k] + part;
                dpsAdd(DPS_TYPE + k, part);
            }
        clogOutW.Add(new ClogW { t = runTime, d = d }); clogTrim(clogOutW);
        if (!clogOn) return;
        var name = Js.Or(e.bname, e.type);
        var tag = (crit ? " CRIT" : "") + (suppressed ? " SUPP" : "") + (e.magic ? " [magic]" : "");
        clogPushDmg(CLOG_OUT, "o|" + source + "|" + name, "→ " + name + "  [" + source + "]" + tag,
          d, crit ? "#ffd166" : "#bfe3ff");
    }
    static string WhoOf(Obj src) => src == null ? null : !string.IsNullOrEmpty(src.bname) ? src.bname : !string.IsNullOrEmpty(src.type) ? src.type : (src as Proj)?.kind;
    public static void logTaken(double n, Obj src, string elem, bool suppressed, bool shockCrit)
    {
        clogRun.taken += n;
        var who = Js.Or(WhoOf(src), "the world");
        clogFrom[who] = clogFrom[who] + n;
        clogInW.Add(new ClogW { t = runTime, d = n }); clogTrim(clogInW);
        /* the last blow, for the death screen: who, how much, of what */
        clogLastHit = new ClogHitT { who = (src != null && src.type == "ailment") ? "ailments" : who, n = n, elem = string.IsNullOrEmpty(elem) ? null : elem, t = runTime };
        /* burns tick every single frame — they go in the totals in full, but they only
           earn a LINE once a whole heart of them has gone, or nothing else would be
           readable while you are on fire */
        if (src != null && src.type == "ailment")
        {
            clogAilAcc += n;
            if (clogAilAcc >= 1)
            {
                var whole = Math.floor(clogAilAcc); clogAilAcc -= whole;
                clogPushDmg(CLOG_IN, "i|ailment", "ailments → you", whole, "#ff8a3c");
            }
            return;
        }
        var tag = (!string.IsNullOrEmpty(elem) ? " " + elem : "") + (suppressed ? " SUPP" : "") + (shockCrit ? " SHOCK-CRIT" : "");
        clogPushDmg(CLOG_IN, "i|" + who + "|" + (elem ?? ""), who + " → you" + tag, n, "#ff8a6a");
    }
    public static void logKill(Ent e)
    {
        var name = Js.Or(e.bname, e.type);
        clogRun.kills++;
        clogKills[name] = clogKills[name] + 1;
        if (e.elite) clogRun.elites++;
        if (e.type == "boss") clogRun.bosses++;
        if (!clogOn) return;
        /* plain words, not a skull glyph — the game's font has no dingbats and a
           missing glyph draws as a tofu box */
        if (e.type == "boss") clogPush(CLOG_EVT, null, "KILLED " + name, "#ffd166");
        else clogPush(CLOG_EVT, "k|" + name, "killed " + name + (e.elite ? " (elite)" : ""), "#9fdc7e");
    }
    public static void logEvent(string txt, Col? col = null) { clogPush(CLOG_EVT, null, txt, col); }
    public static void logEvent(string txt, string col) { clogPush(CLOG_EVT, null, txt, col != null ? Col.Parse(col) : null); }
    public static void logEventK(string key, string txt, Col? col = null) { clogPush(CLOG_EVT, key, txt, col); }
    public static void logEventK(string key, string txt, string col) { clogPush(CLOG_EVT, key, txt, col != null ? Col.Parse(col) : null); }
    /* Object.keys(obj).sort((a,b)=> obj[b]-obj[a]): a stable sort, biggest first */
    static List<string> ByValue(NumMap obj) => obj.Keys.OrderByDescending(k => obj[k]).ToList();
    public static List<string> clogTop(NumMap obj, int n) => ByValue(obj).Take(n).ToList();
    /* EVERY source, biggest first, wrapped across as many lines as it takes. The
       panel used to print the top three and swallow the rest, which meant a build
       with a sword, two spells, poison and bleed could only ever see three of them
       — exactly the ones you already knew were working. */
    public static double clogBreakdown(Ctx c, string label, NumMap obj, double total, double x, double y, Paint col, double width)
    {
        var keys = ByValue(obj);
        c.font = "700 10px Fredoka";
        c.fillStyle = col;
        if (keys.Count == 0) { c.fillText(label + ": —", x, y); return y; }
        var parts = keys.Select(k => k + " " + S(Math.round(obj[k] / Math.max(0.01, total) * 100)) + "%");
        string line = label + ": "; bool first = true;
        foreach (var part in parts)
        {
            var next = line + (first ? "" : "  ") + part;
            if (c.measureText(next).width > width && !first)
            {
                c.fillText(line, x, y); y += 12; line = "   " + part;
            }
            else { line = next; }
            first = false;
        }
        c.fillText(line, x, y);
        return y;
    }
    public static int clogTabAt(double x, double y)
    {
        foreach (var r in clogTabRects)
            if (x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h) return r.i;
        return -1;
    }
    /* The panel eats clicks that land on it, so poking a tab does not also swing
       the sword at whatever is standing behind it. */
    /* one colour per damage source, held steady across the whole run so a bar and
       its sparkline are the same thing at a glance */
    /* A SOURCE THAT IS ALSO A TYPE WEARS THE TYPE'S COLOUR. The by-source chart
       and the by-type chart sit one tab apart, and a fireball drawn orange beside a
       FIRE row drawn red reads as two different things. Everything that has an
       obvious type takes it from the one palette; the rest keep a colour of their
       own, because a spell is not a damage type. */
    public static readonly JsMap<string, string> DPS_COLS = new()
    {
        ["sword"] = CONV_COL["phys"], ["slam"] = CONV_COL["phys"], ["storm"] = "#9fe8ff", ["vortex"] = "#b98cff",
        ["orbit"] = "#ffd166", ["arrow"] = "#e8f0c8", ["minion"] = "#c9b8ff", ["mfire"] = "#ff9a5a", ["mfrost"] = "#9fdcff", ["mshock"] = "#fff07a", ["mburst"] = "#ffb070", ["mchaos"] = "#c98bff", ["bomb"] = "#ffb04a", ["whirl"] = "#e6e6e6",
        ["chain"] = CONV_COL["shock"], ["fire"] = CONV_COL["fire"], ["frost"] = CONV_COL["frost"], ["bane"] = DOT_COL["bane"],
        ["burn"] = DOT_COL["burn"], ["bleed"] = DOT_COL["bleed"], ["poison"] = DOT_COL["bane"], ["ember"] = "#ffb02e",
        ["slamfire"] = CONV_COL["fire"], ["slamfrost"] = CONV_COL["frost"], ["slamshock"] = CONV_COL["shock"]
    };
    public const int CLOG_DPS_ROWS = 9;      /* sources listed; the rest fold into ONE row */
    public const double CLOG_DPS_ROWH = 26;
    /* how many rows the chart will draw, so the panel can be the right height
       before a single one of them is drawn */
    public static int dpsRowCount(bool typed)
    {
        int n = 0;
        foreach (var k in clogDpsOrder)
        {
            if ((k.StartsWith(DPS_TYPE)) != typed) continue;
            if (dpsOf(k) > 0.0001) n++;
        }
        return System.Math.Min(n, CLOG_DPS_ROWS);
    }
    public static Col dpsColour(string k)
    {
        if (DPS_COLS.TryGetValue(k, out var c)) return c;
        /* anything new gets a stable colour from its own name rather than a random one */
        int n = 0; for (int i = 0; i < k.Length; i++) n = (n * 31 + k[i]) & 0xffff;
        return "hsl(" + S(n % 360) + ",62%,68%)";
    }
    /* THE CHART. One row per source: name, its DPS over the window, a share bar,
       and a sixty-second sparkline so a spell that has gone quiet looks different
       from one that never fired. Everything is drawn from dpsSeries(), so the
       picture and the number can never disagree. */
    /* TYPED is the same chart asked a different question. By SOURCE it says which
       of your spells is doing the work; by TYPE it says what you are actually
       dealing, which is not the same thing at all the moment a conversion is in
       play — a staff converting all of its fire to chaos shows one fire source and
       nothing but chaos damage. */
    public static double drawDpsTab(Ctx c, double x, double y, double w, double panelBottom, bool typed)
    {
        string label(string k) => typed ? (CONV_NAME.GetValueOrDefault(k.Substring(1)) ?? k.Substring(1)) : k;
        Col colour(string k) => typed ? (Col)(CONV_COL.GetValueOrDefault(k.Substring(1)) ?? "#ffffff") : dpsColour(k);
        var keys = clogDpsOrder.Where(k => k.StartsWith(DPS_TYPE) == typed).ToList();
        var rows = keys.Select(k => (k, dps: dpsOf(k), tail: false)).Where(r => r.dps > 0.0001)
                       .OrderByDescending(r => r.dps).ToList();
        var win = dpsWindow();
        double total = 0; foreach (var r in rows) total += r.dps;
        c.font = "700 11px Fredoka";
        c.fillStyle = "#bfe3ff";
        c.fillText("TOTAL " + total.toFixed(1) + "/s", x + 12, y);
        c.fillStyle = "#8e9aa4";
        c.fillText("last " + S(Math.round(win)) + "s" + (win < CLOG_DPS_WIN ? " (filling)" : ""), x + 118, y);
        c.textAlign = "right";
        c.fillText(S(rows.Count) + (typed ? " type" : " source") + (rows.Count == 1 ? "" : "s"), x + w - 12, y);
        c.textAlign = "left";
        y += 14;
        c.strokeStyle = rgba(150, 170, 190, 0.2); c.lineWidth = 1;
        c.beginPath(); c.moveTo(x + 10, y); c.lineTo(x + w - 10, y); c.stroke();
        y += 12;
        if (rows.Count == 0)
        {
            c.font = "600 11px Fredoka"; c.fillStyle = "#5f6b75";
            c.fillText(typed ? "nothing has landed yet" : "nothing has dealt damage yet", x + 12, y);
            return y + 14;
        }
        /* fold the tail into one row rather than dropping it — a build with twelve
           sources should still add up to the total printed above */
        var show = rows;
        if (rows.Count > CLOG_DPS_ROWS)
        {
            show = rows.Take(CLOG_DPS_ROWS - 1).ToList();
            var tail = rows.Skip(CLOG_DPS_ROWS - 1).ToList();
            double d = 0; foreach (var r in tail) d += r.dps;
            show.Add(("+" + S(tail.Count) + " more", d, true));
        }
        var peak = Math.max(1e-6, rows[0].dps);
        double barX = x + 96, barW = w - 96 - 68;
        foreach (var r in show)
        {
            if (y + CLOG_DPS_ROWH > panelBottom - 4) break;
            Col col = r.tail ? "#7c8a80" : colour(r.k);
            var nm = r.tail ? r.k : label(r.k);
            c.font = "700 11px Fredoka"; c.fillStyle = col;
            c.fillText(nm.Length > 13 ? nm.Substring(0, 12) + "…" : nm, x + 12, y + 6);
            /* the share bar */
            c.fillStyle = rgba(255, 255, 255, 0.07);
            roundRect(c, barX, y + 1, barW, 10, 5); c.fill();
            c.fillStyle = col;
            var bw = Math.max(2, barW * (r.dps / peak));
            roundRect(c, barX, y + 1, bw, 10, 5); c.fill();
            /* the number, right-aligned so the column reads as a column */
            c.font = "700 11px Fredoka"; c.fillStyle = "#e8ece9";
            c.textAlign = "right";
            c.fillText(r.dps.toFixed(1) + "/s", x + w - 12, y + 6);
            c.font = "600 9px Fredoka"; c.fillStyle = "#8e9aa4";
            c.fillText(total > 0 ? S(Math.round(r.dps / total * 100)) + "%" : "0%", x + w - 12, y + 17);
            c.textAlign = "left";
            /* the sparkline: sixty seconds of this one source, newest on the right */
            if (!r.tail)
            {
                var ser = dpsSeries(r.k);
                double hi = 0; foreach (var v in ser) if (v > hi) hi = v;
                if (hi > 0)
                {
                    double sx = barX, sy = y + 13, shh = 11, sw = barW;
                    c.globalAlpha = 0.85;
                    c.strokeStyle = col; c.lineWidth = 1.2;
                    c.beginPath();
                    for (int i = 0; i < ser.Count; i++)
                    {
                        var px = sx + ((double)i / (ser.Count - 1)) * sw;
                        var py = sy + shh - (ser[i] / hi) * shh;
                        if (i != 0) c.lineTo(px, py); else c.moveTo(px, py);
                    }
                    c.stroke();
                    c.globalAlpha = 1;
                }
            }
            y += CLOG_DPS_ROWH;
        }
        c.font = "600 9px Fredoka"; c.fillStyle = "#5f6b75";
        c.fillText(typed ? "bar = share of the best · line = this type, second by second"
                         : "bar = share of the best · line = this source, second by second", x + 12, y + 4);
        return y + 14;
    }
    public static bool clogHit(double x, double y)
    {
        if (!clogOn || clogTabRects.Count == 0) return false;
        var p0 = clogTabRects[0].panel;
        return x >= p0.x && x <= p0.x + p0.w && y >= p0.y && y <= p0.y + p0.h;
    }
    public static bool clogClearAt(double x, double y)
    {
        var r = clogClearRect;
        return r != null && x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h;
    }
    public static void renderCombatLog()
    {
        if (!clogOn) { clogTabRects = new(); clogClearRect = null; return; }
        /* four tabs need a little more width than three did, or EVENTS runs into DPS */
        double w = 386, x = cw - w - 14, top = 108;
        var bottom = ch - 96;
        /* the breakdown wraps, so the panel has to have room for the extra lines */
        /* the two chart tabs are sized to the rows they actually have, not to the
           most they could ever show — an empty half-panel below five rows reads as
           a bug */
        var chart = clogView == CLOG_DPS || clogView == CLOG_TYPE;
        var h = Math.min(bottom - top,
          chart ? 106 + Math.max(1, dpsRowCount(clogView == CLOG_TYPE)) * CLOG_DPS_ROWH
                : 156 + CLOG_SHOW * 14);
        ctx.save();
        ctx.fillStyle = rgba(10, 14, 19, 0.80);
        roundRect(ctx, x, top, w, h, 10); ctx.fill();
        ctx.strokeStyle = rgba(150, 170, 190, 0.28); ctx.lineWidth = 1.5; ctx.stroke();
        ctx.textAlign = "left"; ctx.textBaseline = "middle";
        var y = top + 15;
        ctx.font = "700 12px \"Titan One\""; ctx.fillStyle = "#ffd76e";
        ctx.fillText("COMBAT LOG", x + 12, y);
        ctx.font = "600 10px Fredoka"; ctx.fillStyle = "#8e9aa4";
        ctx.textAlign = "right"; ctx.fillText("L to hide", x + w - 12, y);
        ctx.textAlign = "left";
        /* ---- CLEAR. Everything in here is a record of the run so far, and half of
           reading a record is being able to start a fresh one: pull a build together,
           wipe it, fight one wave and see what THAT wave did. ---- */
        {
            double bw = 50, bh = 17, bx = x + w - 12 - 52 - bw, by = y - 9;
            clogClearRect = new ClogRect { x = bx, y = by, w = bw, h = bh, panel = new ClogRect { x = x, y = top, w = w, h = h } };
            var hot = mouse != null && mouse.x >= bx && mouse.x <= bx + bw && mouse.y >= by && mouse.y <= by + bh;
            ctx.fillStyle = hot ? rgba(255, 138, 106, 0.26) : rgba(255, 255, 255, 0.07);
            roundRect(ctx, bx, by, bw, bh, 6); ctx.fill();
            ctx.strokeStyle = hot ? rgba(255, 138, 106, 0.8) : rgba(150, 170, 190, 0.28);
            ctx.lineWidth = 1.2; ctx.stroke();
            ctx.font = "700 9px \"Titan One\""; ctx.fillStyle = hot ? "#ffb9a4" : "#93a0ab";
            ctx.textAlign = "center"; ctx.fillText("CLEAR", bx + bw / 2, by + bh / 2 + 0.5);
            ctx.textAlign = "left";
        }
        y += 16;
        /* ---- the tabs ---- */
        clogTabRects = new();
        var panel = new ClogRect { x = x, y = top, w = w, h = h };
        var tw = (w - 24) / CLOG_TABS.Length;
        for (int i = 0; i < CLOG_TABS.Length; i++)
        {
            double tx = x + 12 + i * tw, th = 19;
            var on = clogView == i;
            ctx.fillStyle = on ? rgba(255, 215, 110, 0.22) : rgba(255, 255, 255, 0.06);
            roundRect(ctx, tx + 1, y - 9, tw - 2, th, 6); ctx.fill();
            if (on) { ctx.strokeStyle = rgba(255, 215, 110, 0.75); ctx.lineWidth = 1.4; ctx.stroke(); }
            ctx.font = on ? "700 10px \"Titan One\"" : "600 10px Fredoka";
            ctx.fillStyle = on ? "#ffd76e" : "#93a0ab";
            ctx.textAlign = "center";
            ctx.fillText(CLOG_TABS[i], tx + tw / 2, y + 0.5);
            clogTabRects.Add(new ClogRect { i = i, x = tx, y = y - 9, w = tw, h = th, panel = panel });
        }
        ctx.textAlign = "left";
        y += 20;
        /* ---- the header for whichever tab is showing ---- */
        var outr = clogRate(clogOutW); var inc = clogRate(clogInW);
        ctx.font = "700 11px Fredoka";
        if (clogView == CLOG_OUT)
        {
            ctx.fillStyle = "#bfe3ff"; ctx.fillText("DPS " + outr.toFixed(1), x + 12, y);
            ctx.fillStyle = "#c9d2ce";
            var critPct = clogRun.hits != 0 ? Math.round(clogRun.crits / clogRun.hits * 100) : 0;
            ctx.fillText("total " + S(Math.round(clogRun.dealt)), x + 104, y);
            ctx.fillText("crit " + S(critPct) + "%", x + 196, y);
            ctx.fillText("hits " + S(clogRun.hits), x + 262, y);
            y += 16;
            y = clogBreakdown(ctx, "SOURCES", clogSrc, clogRun.dealt, x + 12, y, "#bfe3ff", w - 30);
        }
        else if (clogView == CLOG_IN)
        {
            ctx.fillStyle = "#ff8a6a"; ctx.fillText("DPS IN " + inc.toFixed(1), x + 12, y);
            ctx.fillStyle = "#c9d2ce";
            ctx.fillText("total " + clogRun.taken.toFixed(1), x + 116, y);
            ctx.fillText("evaded " + S(clogRun.evaded), x + 212, y);
            if (clogRun.leeched > 0)
            {
                ctx.fillStyle = "#8ede4a";
                ctx.fillText("leeched " + clogRun.leeched.toFixed(1), x + 296, y);
            }
            y += 16;
            y = clogBreakdown(ctx, "HURT BY", clogFrom, clogRun.taken, x + 12, y, "#ff8a6a", w - 30);
        }
        else if (clogView == CLOG_DPS || clogView == CLOG_TYPE)
        {
            y = drawDpsTab(ctx, x, y, w, top + h, clogView == CLOG_TYPE);
            ctx.globalAlpha = 1; ctx.restore();
            return;
        }
        else
        {
            ctx.fillStyle = "#9fdc7e"; ctx.fillText("kills " + S(clogRun.kills), x + 12, y);
            ctx.fillStyle = "#c9d2ce";
            ctx.fillText("elites " + S(clogRun.elites), x + 94, y);
            ctx.fillText("bosses " + S(clogRun.bosses), x + 170, y);
            ctx.fillText("wave " + S(wave.n), x + 254, y);
            y += 16;
            var ks = clogTop(clogKills, 3);
            ctx.font = "700 10px Fredoka"; ctx.fillStyle = "#9fdc7e";
            ctx.fillText("MOST KILLED: " + (ks.Count > 0 ? string.Join("  ", ks.Select(k => k + " ×" + S(clogKills[k]))) : "—"), x + 12, y);
        }
        y += 12;
        ctx.strokeStyle = rgba(150, 170, 190, 0.2); ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(x + 10, y); ctx.lineTo(x + w - 10, y); ctx.stroke();
        y += 12;
        var rowsN = (int)Math.max(0, Math.floor((top + h - 8 - y) / 14));
        var feed = clogLines[clogView];
        var take = System.Math.Min(rowsN, CLOG_SHOW);
        var lines = take > 0 ? feed.Skip(System.Math.Max(0, feed.Count - take)).ToList() : feed.ToList();
        ctx.font = "600 11px Fredoka";
        if (lines.Count == 0)
        {
            ctx.fillStyle = "#5f6b75";
            ctx.fillText(clogView == CLOG_OUT ? "nothing hit yet"
              : clogView == CLOG_IN ? "nothing has hurt you yet" : "nothing has happened yet", x + 12, y);
        }
        foreach (var L in lines)
        {
            /* old lines fade, so your eye goes to what just happened */
            var age = runTime - L.t;
            ctx.globalAlpha = age > 6 ? 0.45 : (age > 2 ? 0.75 : 1);
            ctx.fillStyle = "#5f6b75";
            ctx.fillText(L.t.toFixed(1), x + 12, y);
            ctx.fillStyle = L.col;
            ctx.fillText(L.txt, x + 48, y);
            y += 14;
        }
        ctx.globalAlpha = 1;
        ctx.restore();
    }
}
