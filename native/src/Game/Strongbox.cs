namespace Brickblade;

/* =============================== STRONGBOXES (JS 10215–10431) ===============================
   A locked iron chest wearing one to four mods; walk onto it and its guardians
   come out of the ground in bursts, carrying the mods. Kill them all and the
   lock gives. Also the loot stash pips, drawn here in the JS. */
public sealed class SboxTier { public string name, col, rar; public int mods, bursts, extra; public double w; }
public sealed class SboxPending { public double delay; }
public sealed partial class Prop
{
    /* a strongbox's own */
    public string tier; public List<string> mods; public bool sprung; public double bursts, burstT, guards;
    /* a chest that came out of one */
    public bool sbox;
}

public static partial class G
{
    public const double SBOX_FROM = 2, SBOX_CHANCE = 0.6;
    /* THE COMMON ONE: most waves leave a box */
    public const double SBOX_MORE_PER_LVL = 0.03, SBOX_MORE_MAX = 0.5, SBOX_PER_WAVE = 3;
    public const int SBOX_MAX_LIVE = 4;
    public static readonly Tbl SBOX_DELAY = new double[] { 3, 9 };
    public const double SBOX_TOUCH = 46;
    /* the four tiers */
    public static readonly Dictionary<string, SboxTier> SBOX_TIERS = new()
    {
        ["plain"] = new SboxTier { name = "STRONGBOX", col = "#b8c0c6", mods = 1, bursts = 2, extra = 0, rar = "common", w = 0.52 },
        ["magic"] = new SboxTier { name = "MAGIC STRONGBOX", col = "#5a8dff", mods = 2, bursts = 3, extra = 0, rar = "uncommon", w = 0.30 },
        ["rare"] = new SboxTier { name = "RARE STRONGBOX", col = "#ffd23a", mods = 3, bursts = 4, extra = 1, rar = "rare", w = 0.12 },
        ["epic"] = new SboxTier { name = "EPIC STRONGBOX", col = "#b05aff", mods = 4, bursts = 5, extra = 3, rar = "epic", w = 0.06 },
    };
    public const double SBOX_EPIC_FROM = 10;
    public static readonly string[] SBOX_MODS = { "swift", "ironclad", "brutal", "vital", "warded", "venomous", "thorned" };
    public const double SBOX_BURST_T = 1.4;
    public static readonly Tbl SBOX_PACK = new double[] { 3, 5 }, SBOX_RING = new double[] { 60, 130 };
    public const string SBOX_CHEST_MAX = "epic";
    public static List<SboxPending> sboxPending = new();
    public static void sboxRoll(double n)
    {
        sboxPending = new List<SboxPending>();
        if (sandbox || n < SBOX_FROM || n % 5 == 0) return;
        if (Js.R() >= sboxChanceNow()) return;
        double more = Math.Min(SBOX_MORE_MAX, SBOX_MORE_PER_LVL * mobRank(n));
        int k = 1; while (k < SBOX_PER_WAVE && Js.R() < more) k++;
        for (int i = 0; i < k; i++) sboxPending.Add(new SboxPending { delay = rnd(SBOX_DELAY[0], SBOX_DELAY[1]) + i * rnd(2, 5) });
    }
    public static string sboxTierRoll(double n = double.NaN)
    {
        if (double.IsNaN(n)) n = wave.n;
        var ks = SBOX_TIERS.Keys.Where(k => k != "epic" || n >= SBOX_EPIC_FROM).ToList();
        double r = Js.R() * ks.Aggregate(0.0, (a, k) => a + SBOX_TIERS[k].w);
        foreach (var k in ks) { r -= SBOX_TIERS[k].w; if (r < 0) return k; }
        return "plain";
    }
    public static List<Prop> sboxLive() => props.Where(p => p.type == "strongbox" && !p.dead).ToList();
    public static bool sboxActiveNow() => props.Any(p => p.type == "strongbox" && !p.dead && p.sprung);
    /* a box, somewhere off to one side on open floor */
    public static Prop sboxPlace(string tier = null)
    {
        Pt at = null;
        for (int i = 0; i < 80 && at == null; i++)
        {
            double a = rnd(TAU), rr = rnd(300, 700);
            double x = player.x + Math.Cos(a) * rr, y = player.y + Math.Sin(a) * rr;
            if (abyssFloorOk(x, y, 120) && props.All(q => q.dead || hypot(q.x - x, q.y - y) > 80)) at = new Pt(x, y);
        }
        if (at == null) return null;
        string tk = tier ?? sboxTierRoll();
        var T = SBOX_TIERS[tk];
        var pool = SBOX_MODS.ToList(); var mods = new List<string>();
        for (int i = 0; i < T.mods && pool.Count > 0; i++) mods.Add(pool.Splice((int)(Js.R() * pool.Count), 1)[0]);
        var box = new Prop
        {
            type = "strongbox", x = at.x, y = at.y, r = 18, flash = 0, seed = rnd(1000), dead = false, hp = 3,
            indestructible = true, tier = tk, mods = mods, sprung = false, bursts = T.bursts, burstT = 0, guards = 0
        };
        props.Add(box);
        logEvent("   a " + T.name.ToLowerInvariant() + " lies on the floor — " + sboxModNames(box), T.col);
        addDmg(box.x, box.y - 40, T.name, T.col, 15);
        return box;
    }
    public static string sboxModNames(Prop box) =>
        string.Join(" · ", box.mods.Select(id => { var t = BOSS_TRAITS.FirstOrDefault(q => q.id == id); return t != null ? t.name : id; }));
    public static void sboxSpring(Prop box)
    {
        box.sprung = true; box.burstT = 0;
        var T = SBOX_TIERS[box.tier];
        showBanner(T.name, sboxModNames(box), T.col, 1.6, 40);
        logEvent("   the " + T.name.ToLowerInvariant() + " springs — its guardians come for you", T.col);
        addRing(box.x, box.y, 10, 360, 110, T.col, 6, 0.5); addShake(0.25); SFX.roar();
    }
    /* one burst of guardians round the box, every one wearing its mods */
    public static void sboxBurst(Prop box)
    {
        var types = abyssTypes(wave.n); string type = types[(int)(Js.R() * types.Count)];
        double n = irnd(SBOX_PACK[0], SBOX_PACK[1]) + (SBOX_TIERS.GetValueOrDefault(box.tier ?? "")?.extra ?? 0);
        for (int k = 0; k < n; k++)
        {
            double a = rnd(TAU), rr = rnd(SBOX_RING[0], SBOX_RING[1]);
            var at = floorAt(box.x + Math.Cos(a) * rr, box.y + Math.Sin(a) * rr, 14);
            int n0 = enemies.Count;
            /* the first of a burst leads it: magic from a magic box up, and an ELITE from an epic one */
            bool lead = k == 0 && box.tier != "plain";
            spawnEnemy(type, at.x, at.y, lead && box.tier == "epic", lead && box.tier != "epic");
            if (enemies.Count == n0) continue;
            var e = enemies[^1];
            e.sbox = box; box.guards++;
            foreach (var id in box.mods) { var t = BOSS_TRAITS.FirstOrDefault(q => q.id == id); if (t != null) try { t.apply(e); } catch { } }
            e.buildT = Math.Max(e.buildT, 0.4);
        }
        box.bursts--;
        addDust(box.x, box.y, 6);
    }
    public static void sboxOnKill(Ent e)
    {
        var box = e.sbox;
        if (box == null || box.dead) return;
        box.guards = Math.Max(0, box.guards - 1);
    }
    /* THE LOCK GIVES: the box becomes an ordinary chest of its tier's colour */
    public static void sboxOpen(Prop box)
    {
        var T = SBOX_TIERS[box.tier];
        box.dead = true;
        string rar = Array.IndexOf(RAR_ORDER, T.rar) > Array.IndexOf(RAR_ORDER, SBOX_CHEST_MAX) ? SBOX_CHEST_MAX : T.rar;
        props.Add(new Prop { type = "chest", x = box.x, y = box.y, flash = 0, seed = rnd(1000), dead = false, hp = 3, r = 16, rar = rar, sbox = true });
        spawnStuds(box.x, box.y, 6 + wave.n + 8 * T.mods);
        spawnBricks(box.x, box.y, new[] { "#5a5f66", "#8a9098", T.col }, 10, 200);
        showBanner("THE LOCK GIVES", (RARITY.GetValueOrDefault(rar) ?? RARITY["common"]).name + " CHEST INSIDE", T.col, 1.6, 40);
        logEvent("   the " + T.name.ToLowerInvariant() + " is open: a " + rar + " chest", T.col);
        SFX.fortune();
    }
    public static void updateStrongboxes(double dt)
    {
        if (player == null) return;
        if (wave.state == "fight" && sboxPending.Count > 0)
        {
            foreach (var s in sboxPending) s.delay -= dt;
            var due = sboxPending.Where(s => s.delay <= 0).ToList();
            sboxPending = sboxPending.Filter(s => s.delay > 0);
            foreach (var s in due) if (sboxLive().Count < SBOX_MAX_LIVE) sboxPlace();
        }
        if (wave.state == "cleared") sboxPending = new List<SboxPending>();
        foreach (var box in sboxLive())
        {
            if (!box.sprung)
            {
                if (!player.dead && hypot(player.x - box.x, player.y - box.y) < SBOX_TOUCH + Js.Or(player.r, 12)) sboxSpring(box);
                continue;
            }
            if (box.bursts > 0)
            {
                box.burstT -= dt;
                if (box.burstT <= 0) { sboxBurst(box); box.burstT = SBOX_BURST_T; }
                continue;
            }
            /* recount, in case something left without a kill call */
            box.guards = enemies.Count(e => e.sbox == box && !e.dead);
            if (box.guards <= 0) sboxOpen(box);
        }
    }
    /* THE STASH, bottom centre: one pip per reward in the order they will open, and the key */
    public static readonly Dictionary<string, string> LOOT_PIP = new() { ["chest"] = null, ["level"] = "#ffd23a", ["boss"] = "#ff8a1f", ["wave"] = "#6fdc6f", ["bonus"] = "#6fdc6f" };
    public static void drawLootStash()
    {
        if (OPT.autoLoot || pendingPicks.Count == 0 || state != "play" || player == null || player.dead) return;
        int n = pendingPicks.Count; var show = pendingPicks.Take(12).ToList();
        double y = ch - 132, pw = 14;
        ctx.font = "700 13px Fredoka";
        double headW = ctx.measureText(n == 1 ? "1 REWARD" : n + " REWARDS").width;
        double w = 14 + headW + 12 + show.Count * pw + (n > show.Count ? 22 : 0) + 34;
        double x0 = cw / 2 - w / 2;
        double pulse = 0.5 + 0.5 * Math.Sin(vtime * 4);
        ctx.fillStyle = "rgba(15,20,25,0.72)"; roundRect(ctx, x0, y - 17, w, 34, 12); ctx.fill();
        ctx.strokeStyle = rgba(255, 215, 110, 0.45 + 0.35 * pulse); ctx.lineWidth = 2; roundRect(ctx, x0, y - 17, w, 34, 12); ctx.stroke();
        ctx.font = "700 13px Fredoka"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
        ctx.fillStyle = "#ffd76e";
        string head = n == 1 ? "1 REWARD" : n + " REWARDS";
        ctx.fillText(head, x0 + 14, y);
        double x = x0 + 14 + ctx.measureText(head).width + 12;
        foreach (var q in show)
        {
            string col = q.mode == "chest" ? (RARITY.GetValueOrDefault(q.rar ?? "") ?? RARITY["common"]).col : (LOOT_PIP.GetValueOrDefault(q.mode ?? "") ?? "#cfd6dd");
            ctx.fillStyle = col; roundRect(ctx, x, y - 5, 10, 10, 2); ctx.fill();
            ctx.strokeStyle = "rgba(15,20,25,0.8)"; ctx.lineWidth = 1.2; roundRect(ctx, x, y - 5, 10, 10, 2); ctx.stroke();
            x += pw;
        }
        if (n > show.Count) { ctx.fillStyle = "#cfd6dd"; ctx.fillText("+" + (n - show.Count), x, y); x += 22; }
        /* the key */
        ctx.fillStyle = "#e8ebe9"; roundRect(ctx, x + 4, y - 10, 20, 20, 5); ctx.fill();
        ctx.fillStyle = "#1b2a34"; ctx.textAlign = "center"; ctx.font = "800 12px Fredoka"; ctx.fillText("R", x + 14, y + 1);
    }
    static readonly (double, double)[] SBOX_RIVETS = { (-15, -24), (15, -24), (-15, -3), (15, -3) };
    /* the box: dark iron with bands, a padlock, its tier's colour, and its mods written over it */
    public static void drawStrongbox(Ctx c, Prop p)
    {
        var T = SBOX_TIERS.GetValueOrDefault(p.tier ?? "") ?? SBOX_TIERS["plain"]; Col tc = T.col;
        double glow = 0.5 + 0.5 * Math.Sin(vtime * 3 + p.seed);
        c.fillStyle = tc.WithAlpha(0.12 + 0.12 * glow + (p.sprung ? 0.1 : 0));
        c.beginPath(); c.arc(0, -14, 30 + 5 * glow, 0, TAU); c.fill();
        c.strokeStyle = "rgba(20,30,40,0.7)"; c.lineWidth = 2;
        c.fillStyle = "#3d4248"; roundRect(c, -18, -18, 36, 20, 3); c.fill(); c.stroke();
        c.fillStyle = "#51575e"; roundRect(c, -18, -27, 36, 11, 3); c.fill(); c.stroke();
        c.fillStyle = "#6c737b"; c.fillRect(-18, -15, 36, 3); c.fillRect(-11, -27, 3, 29); c.fillRect(8, -27, 3, 29);
        c.fillStyle = tc;
        foreach (var (x, y) in SBOX_RIVETS) { c.beginPath(); c.arc(x, y, 1.8, 0, TAU); c.fill(); }
        /* the padlock -- open-shackled once it has sprung */
        c.strokeStyle = tc; c.lineWidth = 2.2;
        c.beginPath(); c.arc(0, -16 - (p.sprung ? 3 : 0), 4, Math.PI, 0); c.stroke();
        c.fillStyle = tc; roundRect(c, -5, -16, 10, 8, 2); c.fill();
        c.fillStyle = "#1b1f23"; c.fillRect(-1, -14, 2, 4);
        c.font = "700 10px Fredoka"; c.textAlign = "center"; c.textBaseline = "middle";
        c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 3;
        string label = p.sprung ? "GUARDED" : sboxModNames(p);
        c.strokeText(label, 0, -38); c.fillStyle = tc; c.fillText(label, 0, -38);
    }
    /* a gold-edged stain under every guardian, so you can see what you have to kill */
    public static void drawSboxGuards()
    {
        var c = ctx;
        foreach (var e in enemies)
        {
            if (e.sbox == null || e.dead || !onScreen(e, 40)) continue;
            Col tc = (SBOX_TIERS.GetValueOrDefault(e.sbox.tier ?? "") ?? SBOX_TIERS["plain"]).col;
            c.strokeStyle = tc.WithAlpha(0.65); c.lineWidth = 2;
            c.beginPath(); c.ellipse(e.x, e.y + 2, Js.Or(e.r, 12) * 1.45, Js.Or(e.r, 12) * 0.6, 0, 0, TAU); c.stroke();
        }
    }
    public static void drawSboxHud()
    {
        var box = props.FirstOrDefault(p => p.type == "strongbox" && !p.dead && p.sprung);
        if (box == null) return;
        var T = SBOX_TIERS[box.tier];
        double by = hudWaveBottom() + 8 + (breachOpenNow() ? 30 : 0) + (abyssOpenNow() ? 30 : 0);
        int left = enemies.Count(e => e.sbox == box && !e.dead);
        string head = T.name + " · " + (box.bursts > 0 ? "guardians coming" : left + " guardian" + (left == 1 ? "" : "s") + " left");
        ctx.font = "700 12px Fredoka"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
        ctx.strokeStyle = "rgba(15,20,25,0.85)"; ctx.lineWidth = 4;
        ctx.strokeText(head, cw / 2, by); ctx.fillStyle = T.col; ctx.fillText(head, cw / 2, by);
    }
}
