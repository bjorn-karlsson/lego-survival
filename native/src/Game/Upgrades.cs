namespace Brickblade;

/* =============================== RARITY & UPGRADES (JS 11358–12665) ===============================
   The five rarities and what they multiply, the reward roll, the flat ladders,
   the card icons, the golems, and THE DECK: every brick the reward screen can
   hand out. UPGRADES was put through tools/js2cs.py and then fixed by hand. */
public sealed class RarityDef { public string name, col; public double mult; }
/* what a card's calc() hands its desc() and apply(): every field any of them uses */
public sealed class Calc { public double v, n, s, r, t, p; }
public sealed partial class Upgrade
{
    public string id, name, icon, cls, minRar, need, ailType, golemType;
    /* the weapon class a brick serves when it says so itself (none of the deck does yet: brickWeapons asks the req) */
    public string weap;
    public Func<bool> req; public Func<double, Calc> calc; public Func<Calc, double, string> desc; public Action<Calc> apply;
}
public sealed class GolemTypeDef
{
    public string name, skin, col, el, says, fights; public bool taunt, carrion;
    public NumMap k; public (string stat, double v)[] buff;
}

public static partial class G
{
    public static readonly Dictionary<string, RarityDef> RARITY = new()
    {
        ["common"] = new RarityDef { name = "COMMON", col = "#9ba19d", mult = 1.0 },
        ["uncommon"] = new RarityDef { name = "UNCOMMON", col = "#4b9b41", mult = 1.6 },
        ["rare"] = new RarityDef { name = "RARE", col = "#2e7dd1", mult = 2.4 },
        ["epic"] = new RarityDef { name = "EPIC", col = "#9b45d6", mult = 3.6 },
        ["legendary"] = new RarityDef { name = "LEGENDARY", col = "#ff8a1f", mult = 5.0 },
    };
    /* studs paid out when you turn a reward down instead of taking a brick */
    public static readonly NumMap REJECT_STUDS = new(("common", 30), ("uncommon", 60), ("rare", 120), ("epic", 260), ("legendary", 700));
    /* Every table now runs one legendary per nine epics */
    public const double LEG_PER_EPIC = 1.0 / 9;
    public static readonly Dictionary<string, NumMap> MODE_W = new()
    {
        ["wave"] = new NumMap(("common", 58), ("uncommon", 26), ("rare", 12), ("epic", 4), ("legendary", 4 * LEG_PER_EPIC)),
        ["level"] = new NumMap(("common", 50), ("uncommon", 30), ("rare", 15), ("epic", 5), ("legendary", 5 * LEG_PER_EPIC)),
        ["boss"] = new NumMap(("common", 0), ("uncommon", 22), ("rare", 50), ("epic", 28), ("legendary", 28 * LEG_PER_EPIC)),
        ["chest"] = new NumMap(("common", 30), ("uncommon", 34), ("rare", 26), ("epic", 10), ("legendary", 10 * LEG_PER_EPIC)),
        /* the mystery chest's own table -- no commons at all */
        ["wild"] = new NumMap(("common", 0), ("uncommon", 20), ("rare", 32), ("epic", 34), ("legendary", 34 * 0.30)),
    };
    public static readonly string[] RAR_ORDER = { "common", "uncommon", "rare", "epic", "legendary" };
    public static int rarRank(string r) => Math.Max(0, Array.IndexOf(RAR_ORDER, r));
    public static readonly int LEGENDARY_RANK = Array.IndexOf(RAR_ORDER, "legendary");
    /* A card with `minRar` never appears below that tier */
    public static bool rarAllows(Upgrade u, string rar)
    {
        int need = rarRank(u.minRar ?? "common"), got = rarRank(rar);
        /* a legendary roll is spent on a legendary brick or nothing */
        if (got == LEGENDARY_RANK || need == LEGENDARY_RANK) return need == got;
        return got >= need;
    }
    /* A live fortune brick is RARITY FIND as well as a stud multiplier */
    public const double FORTUNE_RAR_MAX = 0.25;
    public static double fortuneRarity()
    {
        if (player == null || player.fortuneT <= 0) return 1;
        double steps = Math.Log2(Math.Max(1, player.fortuneMul));
        return 1 + Math.Min(FORTUNE_RAR_MAX, FORTUNE_RAR_MAX * steps / 3);
    }
    public static string rollRarity(string mode)
    {
        var b = MODE_W.GetValueOrDefault(mode == "bonus" ? "wave" : mode ?? "") ?? MODE_W["wave"];
        /* difficulty tilts the roll: the harder the run, the better the bricks */
        double q = difficulty.rar;
        double f = fortuneRarity();
        var w = new NumMap(("common", b["common"] / q / f), ("uncommon", b["uncommon"]),
                           ("rare", b["rare"] * q * f), ("epic", b["epic"] * q * q * f),
                           ("legendary", b["legendary"] * q * q * f));
        double t = 0; foreach (var (_, v) in w) t += v;
        double r = Js.R() * t;
        foreach (var k in RAR_ORDER) { if ((r -= w[k]) < 0) return k; }
        return "common";
    }
    /* Every FLAT damage brick reads off one ladder */
    public static readonly Tbl FLAT_SPELL = new double[] { 0.5, 1, 3, 5, 8 };
    public static readonly Tbl FLAT_BOMB = new double[] { 1, 1.5, 3, 5, 8 };
    public static readonly Tbl FLAT_FREEZE = new double[] { 0.3, 0.6, 1.2, 2, 3.2 };
    /* attributes are BORING BY DESIGN */
    public static readonly Tbl ATTR_CARD = new double[] { 12, 20, 32, 52, 80 };
    public static readonly Tbl ATTR_ALL_CARD = new double[] { 0, 0, 12, 20, 32 };
    /* EVERYTHING that deepens the pulse meets this one number */
    public const double FREEZE_CHILL_MAX = 4;
    /* ABSOLUTE ZERO'S PAYOFF */
    public const double ZERO_BURST_R_MUL = 0.75;
    public const double ZERO_BRITTLE_MUL = 0.5;
    public const int ZERO_DEPTH = 2;
    public static readonly double[] RAR_MULTS = RAR_ORDER.Select(r => RARITY[r].mult).ToArray();
    public static double byRar(double m, IReadOnlyList<double> table)
    {
        int best = 0; double bd = double.PositiveInfinity;
        for (int i = 0; i < RAR_MULTS.Length; i++)
        {
            double d = Math.Abs(RAR_MULTS[i] - m);
            if (d < bd) { bd = d; best = i; }
        }
        return table[Math.Min(best, table.Count - 1)];
    }
    /* How much of a SPELL a spell card hands over, by the rarity it rolled */
    public static readonly Tbl SPELL_COUNT = new double[] { 1, 2, 3, 5, 8 };
    /* BLOCK FREEZE HAS ITS OWN LADDER */
    public static readonly Tbl FREEZE_COUNT = new double[] { 0, 0, 1, 2, 3 };
    public static double spellCount(double m) => byRar(m, SPELL_COUNT);
    public static readonly Dictionary<string, string> CLS_COL = new() { ["OFFENSE"] = "#d3242b", ["DEFENSE"] = "#1668b3", ["UTILITY"] = "#c78c00", ["SPELL"] = "#9b45d6" };
    public static readonly Dictionary<string, string> ICONS = new()
        {
      ["sword"] = "<path d=\"M20 4 L8 16 M18 2 L22 6 M6.5 13.5 L10.5 17.5 M8 16 L4 20 M3 21 L4.5 19.5\"/>",
      ["bolt"] = "<path d=\"M13 2 L5 13 H11 L9 22 L19 10 H13 Z\"/>",
      ["heart"] = "<path d=\"M12 20 C6 15 3.5 11.5 5.5 8.5 C7.5 5.5 11 6.5 12 9 C13 6.5 16.5 5.5 18.5 8.5 C20.5 11.5 18 15 12 20 Z\"/>",
      ["boot"] = "<path d=\"M7 3 V12 L5 16 V19 H14 C18 19 19.5 16.5 16.5 15.5 L11 13.5 V3 Z M7 7 H11\"/>",
      ["roll"] = "<path d=\"M5 5 L12 12 L5 19 M12 5 L19 12 L12 19\"/>",
      ["bomb"] = "<circle cx=\"9.5\" cy=\"14.5\" r=\"5.5\"/><path d=\"M13.5 10.5 L16 8 M16 8 Q18 5 21 6 M19 3 L20.5 4.5 M21.5 2.5 L20.5 3.5\"/>",
      ["magnet"] = "<path d=\"M6 4 V11 a6 6 0 0 0 12 0 V4 M6 4 h4 M14 4 h4 M6 8 h4 M14 8 h4\"/>",
      ["target"] = "<circle cx=\"12\" cy=\"12\" r=\"7\"/><circle cx=\"12\" cy=\"12\" r=\"2.5\"/><path d=\"M12 2 v4 M12 18 v4 M2 12 h4 M18 12 h4\"/>",
      ["heartplus"] = "<path d=\"M10 19 C5 15 2.5 11.5 4.5 8.5 C6.5 5.5 9.5 6.5 10 9 C10.5 6.5 13.5 5.5 15.5 8.5 C17.5 11.5 15 15 10 19 Z M17 14 v6 M14 17 h6\"/>",
      ["regen"] = "<path d=\"M12 20 C6 15 3.5 11.5 5.5 8.5 C7.5 5.5 11 6.5 12 9 C13 6.5 16.5 5.5 18.5 8.5 C20.5 11.5 18 15 12 20 Z\"/><path d=\"M12 8.5 v5 M9.5 11 h5\"/>",
      ["coin"] = "<circle cx=\"12\" cy=\"12\" r=\"8\"/><circle cx=\"12\" cy=\"12\" r=\"3.5\"/>",
      ["range"] = "<circle cx=\"12\" cy=\"12\" r=\"3.5\"/><path d=\"M12 6 V2 M12 22 v-4 M6 12 H2 M22 12 h-4\"/>",
      ["orbit"] = "<circle cx=\"12\" cy=\"12\" r=\"3.6\"/><ellipse cx=\"12\" cy=\"12\" rx=\"10\" ry=\"4.4\" transform=\"rotate(-24 12 12)\"/>",
      ["skull"] = "<path d=\"M12 3 C7 3 4.5 6.4 4.5 10.2 c0 2.6 1.3 4.4 3 5.3 V19 h9 v-3.5 c1.7 -0.9 3 -2.7 3 -5.3 C19.5 6.4 17 3 12 3 Z\"/><circle cx=\"9\" cy=\"10.5\" r=\"1.7\"/><circle cx=\"15\" cy=\"10.5\" r=\"1.7\"/><path d=\"M10 19 v2 M14 19 v2\"/>",
      ["bow"] = "<path d=\"M7 3 C17 7 17 17 7 21\"/><path d=\"M7 3 V21\"/><path d=\"M3 12 H19\"/><path d=\"M16 9.5 L19 12 L16 14.5\"/>",
      ["storm"] = "<path d=\"M12 3 v4 M12 17 v4 M3 12 h4 M17 12 h4 M5.6 5.6 l2.8 2.8 M15.6 15.6 l2.8 2.8 M18.4 5.6 l-2.8 2.8 M8.4 15.6 l-2.8 2.8\"/><circle cx=\"12\" cy=\"12\" r=\"2.4\"/>",
      ["vortex"] = "<path d=\"M12 12 m-2 0 a2 2 0 1 0 4 0 a2 2 0 1 0 -4 0\"/><path d=\"M12 12 m-5.5 0 a5.5 5.5 0 1 1 5.5 -5.5\"/><path d=\"M12 12 m-9 0 a9 9 0 1 1 9 -9\"/>",
      ["freeze"] = "<path d=\"M12 2 v20 M2 12 h20 M5 5 l14 14 M19 5 L5 19\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
      ["force"] = "<path d=\"M3 12 h8\"/><path d=\"M12 7 l8 5 -8 5 Z\"/><circle cx=\"4.5\" cy=\"12\" r=\"1.8\"/>",
      ["xp"] = "<path d=\"M12 3 l2.3 5.7 6.1 0.5 -4.6 4 1.4 6 -5.2-3.2 -5.2 3.2 1.4-6 -4.6-4 6.1-0.5 Z\"/>",
      ["chain"] = "<path d=\"M7 12 a5 5 0 0 1 5-5 h2 M17 12 a5 5 0 0 1 -5 5 h-2\"/><path d=\"M13 7 l3-3 M11 17 l-3 3\"/><circle cx=\"12\" cy=\"12\" r=\"1.6\"/>",
      /* One glyph per idea. Armour was a heart, fire resistance was a bomb, spell
         suppression was a snowflake and every "triggers faster" card was a lightning
         bolt — so the art told you nothing about what you were picking. */
      ["shield"] = "<path d=\"M12 2.6 l7.4 3 v5.2 c0 4.4 -3 8 -7.4 9.6 c-4.4 -1.6 -7.4 -5.2 -7.4 -9.6 V5.6 Z\"/>",
      ["shieldplus"] = "<path d=\"M12 2.6 l7.4 3 v5.2 c0 4.4 -3 8 -7.4 9.6 c-4.4 -1.6 -7.4 -5.2 -7.4 -9.6 V5.6 Z\"/><path d=\"M12 8.4 v6 M9 11.4 h6\"/>",
      ["bane"] = "<path d=\"M10 3 h4 v4.5 l4 8.5 a4 4 0 0 1 -3.6 5.8 h-4.8 a4 4 0 0 1 -3.6 -5.8 l4 -8.5 Z\"/>"+
           "<path d=\"M9 3 h6\"/><circle cx=\"10.5\" cy=\"16\" r=\"1.2\"/><circle cx=\"14\" cy=\"17.5\" r=\"1\"/>",
      ["ward"] = "<path d=\"M12 2.6 l7.4 3 v5.2 c0 4.4 -3 8 -7.4 9.6 c-4.4 -1.6 -7.4 -5.2 -7.4 -9.6 V5.6 Z\"/><path d=\"M8.4 12.4 l2.6 2.6 4.6 -5.6\"/>",
      ["flame"] = "<path d=\"M12 2.4 c3.4 4.2 5.4 6.4 5.4 9.4 a5.4 5.4 0 0 1 -10.8 0 c0 -2.2 1.4 -3.8 2.8 -5.4 c.6 1.6 1.4 2.2 2 2.6 c.4 -2.6 -.8 -4.6 .6 -6.6 Z\"/>",
      ["snow"] = "<path d=\"M12 2.5 v19 M3.8 7.2 l16.4 9.6 M20.2 7.2 l-16.4 9.6\"/><path d=\"M12 6 l-2.2 -2 M12 6 l2.2 -2 M12 18 l-2.2 2 M12 18 l2.2 2\"/>",
      /* ELEMENTAL RESISTANCE: the prism stands UP, and each face throws off one
         element — a flame to the right, a bolt below, a flake to the left. */
      ["prism"] = "<path d=\"M12 3.6 L19.8 17.6 H4.2 Z\"/>"+
            "<path d=\"M16.6 11.4 h1.8\"/>"+
            "<path d=\"M22.4 11.6 C22.4 13.1 21.4 13.9 20.3 13.9 C19.1 13.9 18.3 13 18.3 11.9 "+
              "C18.3 10.5 19.7 10 19.9 8.3 C21.2 9.4 22.4 10.2 22.4 11.6 Z\"/>"+
            "<path d=\"M12 18.2 v0.9\"/>"+
            "<path d=\"M13 19.1 L10.7 19.1 L12 20.5 L10 20.5\"/>"+
            "<path d=\"M7.4 11.4 h-1.8\"/>"+
            "<path d=\"M2 11.6 v-2.4 M2 11.6 v2.4 M0 10.4 L4 12.8 M4 10.4 L0 12.8\"/>",
      /* ELEMENTAL DAMAGE: the same prism turned over, pointing DOWN — what you put
         out rather than what you keep off. */
      ["prismdn"] = "<path d=\"M12 20.4 L4.2 6.4 H19.8 Z\"/>"+
            "<path d=\"M16.6 12.6 h1.8\"/>"+
            "<path d=\"M22.4 12.8 C22.4 14.3 21.4 15.1 20.3 15.1 C19.1 15.1 18.3 14.2 18.3 13.1 "+
              "C18.3 11.7 19.7 11.2 19.9 9.5 C21.2 10.6 22.4 11.4 22.4 12.8 Z\"/>"+
            "<path d=\"M12 5.8 v-0.9\"/>"+
            "<path d=\"M13 4.9 L10.7 4.9 L12 3.5 L10 3.5\"/>"+
            "<path d=\"M7.4 12.6 h-1.8\"/>"+
            "<path d=\"M2 12.8 v-2.4 M2 12.8 v2.4 M0 11.6 L4 14 M4 11.6 L0 14\"/>",
      ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"8.6\"/><path d=\"M12 7.2 V12 l3.8 2.2\"/>",
      /* tempo: a clock leaning forward with the motion lines it left behind */
      ["tempo"] = "<circle cx=\"14\" cy=\"12\" r=\"7.2\"/><path d=\"M14 7.6 V12 l3.2 1.9\"/>"+
            "<path d=\"M4.6 7.5 H1.4 M4 12 H0.8 M4.6 16.5 H1.4\"/>",
      ["burst"] = "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M12 1.8 V5 M12 19 v3.2 M1.8 12 H5 M19 12 h3.2 M4.8 4.8 l2.3 2.3 M16.9 16.9 l2.3 2.3 M19.2 4.8 l-2.3 2.3 M7.1 16.9 l-2.3 2.3\"/>",
      ["fist"] = "<path d=\"M7.4 11.6 V8.4 a2.1 2.1 0 0 1 4.2 0 v3.2 M11.6 11.6 V7.2 a2.1 2.1 0 0 1 4.2 0 v4.4 M15.8 11.6 V9 a2.1 2.1 0 0 1 4.2 0 v5.6 a6.4 6.4 0 0 1 -6.4 6.4 h-2.8 a5.4 5.4 0 0 1 -5.4 -5.4 v-4.6 a2.1 2.1 0 0 1 4.2 0\"/>",
      ["spin"] = "<path d=\"M20.4 12 a8.4 8.4 0 1 1 -3.4 -6.7\"/><path d=\"M17 2.4 l.4 3.4 -3.4 .2\"/><path d=\"M12 12 l7 -7\"/>",
      ["arcane"] = "<path d=\"M12 2.6 l2.7 6.7 6.7 2.7 -6.7 2.7 -2.7 6.7 -2.7 -6.7 -6.7 -2.7 6.7 -2.7 Z\"/>",
      ["gift"] = "<rect x=\"3.4\" y=\"9.6\" width=\"17.2\" height=\"11.4\" rx=\"1.6\"/><path d=\"M2.6 9.6 h18.8 M12 9.6 V21\"/><path d=\"M12 9.6 C8 9.6 6 8 6 6.4 A2.6 2.6 0 0 1 12 6 a2.6 2.6 0 0 1 6 .4 c0 1.6 -2 3.2 -6 3.2 Z\"/>",
      ["totem"] = "<path d=\"M12 21.4 V7.6\"/><ellipse cx=\"12\" cy=\"6.6\" rx=\"5.4\" ry=\"2.8\"/><ellipse cx=\"12\" cy=\"12.4\" rx=\"3.8\" ry=\"2\"/><path d=\"M8.6 21.4 h6.8\"/>",
      ["tide"] = "<circle cx=\"12\" cy=\"12\" r=\"2.6\"/><path d=\"M12 4.2 a7.8 7.8 0 0 1 7.8 7.8 M12 19.8 A7.8 7.8 0 0 1 4.2 12\"/><path d=\"M19.8 12 l2 -2.2 M19.8 12 l2 2.2 M4.2 12 l-2 -2.2 M4.2 12 l-2 2.2\"/>",
      ["split"] = "<path d=\"M2.6 12 h6\"/><path d=\"M8.6 12 L17 5.6 M8.6 12 h8.4 M8.6 12 L17 18.4\"/><path d=\"M17 5.6 l-.4 2.6 2.6 -.6 M17 18.4 l-.4 -2.6 2.6 .6 M17 12 l2.4 0\"/>",
      ["fork"] = "<path d=\"M5.4 3 l4.8 7.2 H7.2 l3.6 5.6\"/><path d=\"M10.2 10.2 l6.4 -2.8 M10.2 10.2 l6.6 6.6 M10.2 10.2 l-1 7.4\"/>",
      ["boomerang"] = "<path d=\"M4.6 7.4 a10 10 0 0 1 13.2 12.2\"/><path d=\"M4.6 7.4 l4 1.6 M4.6 7.4 l1.4 -4\"/><path d=\"M17.8 19.6 l-3.4 -1.4 M17.8 19.6 l-1.2 -3.6\"/>",
      ["bomblets"] = "<circle cx=\"9.4\" cy=\"14.4\" r=\"5.4\"/><path d=\"M12.6 9.8 l2 -2.6\"/><circle cx=\"18.2\" cy=\"7.2\" r=\"2\"/><circle cx=\"19\" cy=\"14.6\" r=\"1.6\"/><circle cx=\"14.8\" cy=\"19.6\" r=\"1.4\"/>",
      ["zapper"] = "<rect x=\"7.6\" y=\"3.4\" width=\"8.8\" height=\"6.4\" rx=\"1.4\"/><path d=\"M12 9.8 l-2.6 5 h2.2 l-1.6 5.8 4.6 -7.2 h-2.4 l1.4 -3.6\"/>",
      ["icecube"] = "<path d=\"M12 2.8 l7.4 4.2 v9 L12 20.2 4.6 16 V7 Z\"/><path d=\"M4.6 7 L12 11.2 l7.4 -4.2 M12 11.2 V20.2\"/>",
      ["nofav"] = "<circle cx=\"12\" cy=\"12\" r=\"8.6\"/><path d=\"M6.6 17.4 L17.4 6.6\"/>",
      /* a core with fire coming off it — Ember Core */
      ["ember"] = "<circle cx=\"12\" cy=\"13.5\" r=\"4.4\"/><path d=\"M12 9 C12 5 9.5 4 9.5 1.6 C7 4 7.5 7 9 9\"/>"+
            "<path d=\"M14.5 9.6 C16 7.6 15.8 5.4 14.6 4 C16.8 5 18 7.4 17 9.8\"/>"+
            "<path d=\"M6.2 17.6 C4.6 18.6 3.6 20 3.4 21.6\"/><path d=\"M17.8 17.6 C19.4 18.6 20.4 20 20.6 21.6\"/>",
      /* a single tongue of flame — Pyromancy */
      ["pyro"] = "<path d=\"M12 22 C7.6 22 5 19 5 15.4 C5 11 9 9.4 9.6 5 C12.6 7 13.2 10 12.6 12.4 "+
           "C13.6 11.4 14.2 9.8 14.2 8.2 C17.2 10.6 19 13 19 15.6 C19 19.2 16.4 22 12 22 Z\"/>"
    };
    /* =============================== GOLEMS ===============================
       Each KIND grants you something while it stands, and fights with its own element. */
    public const int GOLEM_BASE_CAP = 2, GOLEM_CAP_MAX = 3;
    public static readonly Dictionary<string, GolemTypeDef> GOLEM_TYPES = new()
    {
        ["flame"] = new GolemTypeDef { name = "Flame Golem", skin = "fire", col = "#ff8a3c", el = "fire",
            k = new NumMap(("dmg", 1.7), ("rate", 1.05), ("hp", 2.6), ("range", 420), ("keep", 200), ("reach", 0)),
            buff = new[] { ("incAll", 0.20), ("boltR", 0.15), ("bombR", 0.15), ("baneR", 0.15) },
            says = "20% increased damage (yours AND the legion's), and 15% increased area -- fireball, bomb and Brickbane radius",
            fights = "throws fire bolts from the back" },
        ["lightning"] = new GolemTypeDef { name = "Lightning Golem", skin = "storm", col = "#ffe95a", el = "shock",
            k = new NumMap(("dmg", 1.2), ("rate", 0.75), ("hp", 2.6), ("range", 460), ("keep", 220), ("reach", 0)),
            buff = new[] { ("incAtk", 0.12), ("incCast", 0.12), ("minionSpd", 0.10) },
            says = "12% increased attack speed and cast speed -- and, since there is no mana to regenerate, 10% faster minions",
            fights = "looses quick lightning bolts" },
        ["ice"] = new GolemTypeDef { name = "Ice Golem", skin = "ice", col = "#8fd8ff", el = "frost",
            k = new NumMap(("dmg", 2.4), ("rate", 1.2), ("hp", 3.5)),
            buff = new[] { ("incCrit", 0.30), ("incAcc", 0.25) },
            says = "30% increased critical strike chance and 25% increased accuracy",
            fights = "slams with cold, chilling what it hits" },
        ["stone"] = new GolemTypeDef { name = "Stone Golem", skin = "stone", col = "#c9cec9", el = null, taunt = true,
            k = new NumMap(("dmg", 1.6), ("rate", 1.3), ("hp", 6.0)),
            buff = new[] { ("regen", 0.12), ("incRegen", 0.20), ("incArmour", 0.25), ("incEva", 0.25) },
            says = "0.12 hearts a second of regeneration, 20% increased regeneration, and 25% increased armour and evasion",
            fights = "TAUNTS -- anything near it comes for it instead of you -- and slams" },
        ["chaos"] = new GolemTypeDef { name = "Chaos Golem", skin = "chaos", col = "#b45cff", el = "chaos",
            k = new NumMap(("dmg", 2.4), ("rate", 1.2), ("hp", 3.5)),
            buff = new[] { ("chaosRes", 0.15), ("incDot", 0.25) },
            says = "15% chaos resistance and 25% increased damage over time",
            fights = "slams with chaos, which poisons" },
        ["carrion"] = new GolemTypeDef { name = "Carrion Golem", skin = "carrion", col = "#d25a5a", el = null, carrion = true,
            k = new NumMap(("dmg", 2.0), ("rate", 1.1), ("hp", 3.0)),
            buff = new (string, double)[0],
            says = "35% of the scepter's base as ADDED PHYSICAL damage to every minion that is not a golem",
            fights = "hits harder the more of your skeletons and zombies fight beside it: 6% more for each within 320px, up to double" },
    };
    public static readonly List<string> GOLEM_KEYS = GOLEM_TYPES.Keys.ToList();
    public const double CARRION_FLAT = 0.35, CARRION_R = 320, CARRION_PER = 0.06, CARRION_MAX = 1.0;
    public const double STONE_TAUNT_R = 360;
    public const double BARBED_OVER = 0.5;
    public static readonly List<Upgrade> UPGRADES = [
      new Upgrade { id = "dmg", name = "Sharpened Blade", icon = "sword", cls = "OFFENSE",
        req = ()=>!isCaster() && !isSummoner(),
        /* common barely sharpens it; rare and epic are a real weapon upgrade */
        calc = m=>(new Calc {v = byRar(m, [0.5, 1, 3, 5, 8])}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT PHYSICAL damage — before any increase, whatever you are holding",
        apply = c=>{ player.flatPhys = Math.round((player.flatPhys + c.v)*100)/100.0; } },
      /* THE ELEMENTAL TREE. Only offered to a hero actually carrying an elemental
         weapon, so a swordsman is never handed a dead card — and the moment a
         second caster weapon exists these all work for it untouched. */
      /* IT WAS NEVER A FIRE CARD. `flatElem` is read by the bolt, by Block Freeze
         and by the Storm Brick alike, so this has always been the best single
         upgrade a frost build can take — and it was called Ember Core, wore a
         flame, and said "to your fire bolt". Picking Block Freeze as a favourite
         and being handed an ember is a card lying about itself, not a bad roll. */
      /* THE FOUR EVERYBODY CAN BE OFFERED. No weapon gate, no favourite gate, no
         spell to own first: attributes are the one currency the whole game shares,
         and these are the only bricks in the deck that hand them over directly. */
      new Upgrade { id = "attstr", name = "Ironbone", icon = "fist", cls = "OFFENSE",
        calc = m=>(new Calc {v = byRar(m, ATTR_CARD)}),
        desc = (c, _owned)=>$"+{S(c.v)} STRENGTH — {S(ATTRS[0].what)} (now {S(strOf())})",
        apply = c=>{ player.attr["str"] += c.v; syncStats(); } },
      new Upgrade { id = "attint", name = "Runescript", icon = "arcane", cls = "OFFENSE",
        calc = m=>(new Calc {v = byRar(m, ATTR_CARD)}),
        desc = (c, _owned)=>$"+{S(c.v)} INTELLIGENCE — {S(ATTRS[1].what)} (now {S(intOf())})",
        apply = c=>{ player.attr["int"] += c.v; syncStats(); } },
      new Upgrade { id = "attdex", name = "Quicksilver", icon = "boot", cls = "OFFENSE",
        calc = m=>(new Calc {v = byRar(m, ATTR_CARD)}),
        desc = (c, _owned)=>$"+{S(c.v)} DEXTERITY — {S(ATTRS[2].what)} (now {S(dexOf())})",
        apply = c=>{ player.attr["dex"] += c.v; syncStats(); } },
      new Upgrade { id = "attall", name = "The Whole Brick", icon = "prism", cls = "OFFENSE", minRar = "rare",
        calc = m=>(new Calc {v = byRar(m, ATTR_ALL_CARD)}),
        desc = (c, _owned)=>$"+{S(c.v)} to ALL THREE attributes — strength, intelligence and dexterity "+
          $"(now {S(strOf())}/{S(intOf())}/{S(dexOf())})",
        apply = c=>{ player.attr["str"] += c.v; player.attr["int"] += c.v; player.attr["dex"] += c.v;
                   syncStats(); } },
      /* ---- CONVERSION. Damage only ever travels one way — physical, lightning,
         cold, fire, chaos — so nothing here can loop, and a converted part keeps
         the increases for HOW it was built while gaining the ones for WHAT it
         became. CONVERTED takes the damage out of the source; GAIN AS EXTRA leaves
         it where it is and adds a second helping on top, which is why it is worth
         less per point. ---- */
      new Upgrade { id = "convfire", name = "Emberforge", icon = "pyro", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your PHYSICAL damage CONVERTED to FIRE — it stops "+
          $"meeting armour and starts meeting fire resistance, and it takes your increased "+
          $"fire damage with it{S(convOwnTag("phys","fire"))}",
        apply = c=>{ convAdd(player.conv, "phys", "fire", c.v); } },
      new Upgrade { id = "convfrost", name = "Rimeblade", icon = "snow", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your PHYSICAL damage CONVERTED to COLD — past armour, "+
          $"into frost resistance, scaling with everything that makes your cold hurt"+
          $"{S(convOwnTag("phys","frost"))}",
        apply = c=>{ convAdd(player.conv, "phys", "frost", c.v); } },
      new Upgrade { id = "convshock", name = "Galvanise", icon = "zapper", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your PHYSICAL damage CONVERTED to LIGHTNING — past "+
          $"armour, into lightning resistance{S(convOwnTag("phys","shock"))}",
        apply = c=>{ convAdd(player.conv, "phys", "shock", c.v); } },
      new Upgrade { id = "convchaos", name = "The Unmaking", icon = "prismdn", cls = "OFFENSE", minRar = "rare",
        need = "fire", req = ()=>canDeal("fire"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your FIRE damage CONVERTED to CHAOS — chaos is neither "+
          $"physical nor elemental, so nothing in the game resists it{S(convOwnTag("fire","chaos"))}",
        apply = c=>{ convAdd(player.conv, "fire", "chaos", c.v); } },
      new Upgrade { id = "gainchaos", name = "Creeping Blight", icon = "bane", cls = "OFFENSE", minRar = "rare",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your PHYSICAL damage as EXTRA CHAOS — the physical "+
          $"stays where it is, and a hit carrying chaos can leave a dose of poison behind"+
          $"{S(convOwnTag("phys","chaos", true))}",
        apply = c=>{ convGain(player.conv, "phys", "chaos", c.v); } },
      new Upgrade { id = "gainfire", name = "Catalyst", icon = "prism", cls = "OFFENSE", minRar = "rare",
        need = "frost", req = ()=>canDeal("frost"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your COLD damage as EXTRA FIRE — the cold is not "+
          $"touched, and a frost build starts burning things too{S(convOwnTag("frost","fire", true))}",
        apply = c=>{ convGain(player.conv, "frost", "fire", c.v); } },
      new Upgrade { id = "convsf", name = "Stormfrost", icon = "snow", cls = "OFFENSE", minRar = "rare",
        need = "shock", req = ()=>canDeal("shock"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your LIGHTNING damage CONVERTED to COLD — and it keeps "+
          $"your increased lightning on the way{S(convOwnTag("shock","frost"))}",
        apply = c=>{ convAdd(player.conv, "shock", "frost", c.v); } },
      new Upgrade { id = "convff", name = "Fulminate", icon = "pyro", cls = "OFFENSE", minRar = "rare",
        need = "frost", req = ()=>canDeal("frost"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your COLD damage CONVERTED to FIRE — stack it under "+
          $"Stormfrost and a storm bolt arrives as fire carrying all three increases"+
          $"{S(convOwnTag("frost","fire"))}",
        apply = c=>{ convAdd(player.conv, "frost", "fire", c.v); } },
      new Upgrade { id = "convrot", name = "Rotgale", icon = "bane", cls = "OFFENSE", minRar = "epic",
        need = "frost", req = ()=>canDeal("frost"),
        calc = m=>(new Calc {v = byRar(m, CONV_CARD)}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% of your COLD damage CONVERTED to CHAOS — out of the "+
          $"elements entirely, where nothing resists it{S(convOwnTag("frost","chaos"))}",
        apply = c=>{ convAdd(player.conv, "frost", "chaos", c.v); } },
      new Upgrade { id = "gainshock", name = "Static Charge", icon = "zapper", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your PHYSICAL damage as EXTRA LIGHTNING — the "+
          $"physical stays where it is and the lightning is added on top"+
          $"{S(convOwnTag("phys","shock", true))}",
        apply = c=>{ convGain(player.conv, "phys", "shock", c.v); } },
      new Upgrade { id = "gainfrost", name = "Hoarfrost", icon = "icecube", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your PHYSICAL damage as EXTRA COLD — and every "+
          $"hit starts chilling what it lands on{S(convOwnTag("phys","frost", true))}",
        apply = c=>{ convGain(player.conv, "phys", "frost", c.v); } },
      new Upgrade { id = "gainpf", name = "Brand Iron", icon = "flame", cls = "OFFENSE", minRar = "uncommon",
        need = "phys", req = ()=>canDeal("phys"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your PHYSICAL damage as EXTRA FIRE"+
          $"{S(convOwnTag("phys","fire", true))}",
        apply = c=>{ convGain(player.conv, "phys", "fire", c.v); } },
      new Upgrade { id = "gainrot", name = "Sour Ending", icon = "bane", cls = "OFFENSE", minRar = "epic",
        need = "fire", req = ()=>canDeal("fire"),
        calc = m=>(new Calc {v = byRar(m, GAIN_CARD)}),
        desc = (c, _owned)=>$"GAIN {S(Math.round(c.v*100))}% of your FIRE damage as EXTRA CHAOS — a fire build "+
          $"that leaves poison behind it{S(convOwnTag("fire","chaos", true))}",
        apply = c=>{ convGain(player.conv, "fire", "chaos", c.v); } },
      new Upgrade { id = "chaosres", name = "Sourproof", icon = "ward", cls = "DEFENSE", minRar = "uncommon",
        req = ()=>player.resFlat["chaos"] < RES_CAP,
        calc = m=>(new Calc {v = 0.08+0.05*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% CHAOS resistance — poison is chaos damage over time, "+
          $"and nothing that says \"every elemental resistance\" covers this one "+
          $"(now {S(Math.round(player.res["chaos"]*100))}%, cap {S(Math.round(RES_CAP*100))}%)",
        apply = c=>{ player.resFlat["chaos"] += c.v; syncStats(); } },
      new Upgrade { id = "incchaos", name = "Entropy", icon = "burst", cls = "OFFENSE", minRar = "rare",
        req = ()=>convAny(),
        calc = m=>(new Calc {v = 0.14+0.08*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased CHAOS damage — nothing else in the game reads "+
          $"this pool, and nothing in the game resists what it scales",
        apply = c=>{ player.inc["chaos"] += c.v; } },
      /* PENETRATION, one per element, offered only to a hero whose hits carry it */
      ..PEN_KEYS.map(el=>(new Upgrade { id = "pen"+el, name = PEN_NAME[el]+" Penetration",
        icon = new Dictionary<string, string> { ["fire"] = "flame", ["frost"] = "icecube", ["shock"] = "chain", ["chaos"] = "bane" }[el], cls = "OFFENSE",
        minRar = el == "chaos" ? "rare" : "uncommon",
        req = ()=>!isSummoner() && !!dealtTypes()[el] && penOf(el) < PEN_MAX - 1e-9,
        calc = m=>(new Calc {v = byRar(m, [0.05, 0.06, 0.08, 0.11, 0.15])}),
        desc = (c, _owned)=>{ double now = penOf(el), to = Math.min(PEN_MAX, now + c.v);
          return $"Your hits PENETRATE +{S(Math.round((to - now)*100))}% {S(PEN_NAME[el].toUpperCase())} RESISTANCE "+
            $"({S(Math.round(now*100))}% \u2192 {S(Math.round(to*100))}%, cap {S(Math.round(PEN_MAX*100))}%) \u2014 a body resisting "+
            $"{S(PEN_NAME[el].toLowerCase())} counts that much lower against you, down to nothing and no further"; },
        apply = c=>{ player.pen[el] = Math.round((player.pen[el] + c.v)*1000)/1000.0; } })),
      new Upgrade { id = "penelem", name = "Elemental Penetration", icon = "prism", cls = "OFFENSE", minRar = "rare",
        req = ()=>!isSummoner() && new[] {"fire","frost","shock"}.some(el=>dealtTypes()[el] && penOf(el) < PEN_MAX - 1e-9),
        calc = m=>(new Calc {v = byRar(m, [0.03, 0.04, 0.05, 0.07, 0.10])}),
        desc = (c, _owned)=>$"Your hits PENETRATE +{S(Math.round(c.v*100))}% of EVERY ELEMENTAL RESISTANCE \u2014 fire, cold and "+
          $"lightning (now {S(new[] {"fire","frost","shock"}.map(el=>Math.round(penOf(el)*100)+"%").join(" / "))}, cap {S(Math.round(PEN_MAX*100))}% each)",
        apply = c=>{ player.pen["elem"] = Math.round((player.pen["elem"] + c.v)*1000)/1000.0; } },
      new Upgrade { id = "elemflat", name = "Elemental Core", icon = "prism", cls = "OFFENSE",
        req = ()=>isCaster(),
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT ELEMENTAL damage, added to the BASE of every elemental "+
          $"ability you own — your {S(weapon.elem??"elemental")} bolt, Block Freeze, the Storm "+
          $"Brick — before a single increase touches it",
        apply = c=>{ player.flatElem = Math.round((player.flatElem + c.v)*100)/100.0; } },
      /* A SWORD OWNS ELEMENTAL DAMAGE TOO. The Storm Brick is lightning, Block
         Freeze is frost, a bomb is fire and a mace's slam cycles all three — gating
         the one brick that scales every last one of them behind CARRYING a staff
         was a rule about the hand rather than about the build. */
      new Upgrade { id = "eleminc", name = "Elemental Focus", icon = "prismdn", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>incRoom("spell", weapon.elem??"fire") && dealsElemental(),
        calc = m=>(new Calc {v = 0.08+0.05*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased ELEMENTAL damage — fire, frost and lightning alike "+
          $"{S(incCapTag("spell", weapon.elem??"fire"))}",
        apply = c=>{ player.inc["elem"] += c.v; } },
      new Upgrade { id = "firinc", name = "Pyromancy", icon = "pyro", cls = "OFFENSE", minRar = "rare",
        req = ()=>isCaster() && incRoom("spell", "fire"),
        calc = m=>(new Calc {v = 0.14+0.08*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased FIRE damage — the bolt and the burning both "+
          $"{S(incCapTag("spell", "fire"))}",
        apply = c=>{ player.inc["fire"] += c.v; } },
      /* THE OTHER TWO ELEMENTS GET THEIR OWN POOL. Fire had one because the staff
         is fire; frost and lightning had nowhere to put an increase, which is why a
         Block Freeze or storm-brick build could only ever buy the GLOBAL elemental
         pool. Each of these is offered only to a hero who owns the thing it is for,
         so a fire caster is never handed a lightning card it cannot use. */
      new Upgrade { id = "froinc", name = "Cryomancy", icon = "snow", cls = "OFFENSE", minRar = "rare",
        req = ()=>player.freeze.n > 0 && incRoom("spell", "frost"),
        calc = m=>(new Calc {v = 0.14+0.08*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased FROST damage — Block Freeze and every chill "+
          $"it lays {S(incCapTag("spell", "frost"))}",
        apply = c=>{ player.inc["frost"] += c.v; } },
      new Upgrade { id = "shockinc", name = "Conduction", icon = "zapper", cls = "OFFENSE", minRar = "rare",
        req = ()=>player.chain.n > 0 && incRoom("spell", "shock"),
        calc = m=>(new Calc {v = 0.14+0.08*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased LIGHTNING damage — every storm bolt and "+
          $"every leap it makes {S(incCapTag("spell", "shock"))}",
        apply = c=>{ player.inc["shock"] += c.v; } },
      new Upgrade { id = "boltarea", name = "Wildfire", icon = "bomb", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>isCaster() && player.inc["boltR"] < BOLT_R_MAX_INC,
        calc = m=>(new Calc {v = 0.12+0.09*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% wider burst (now {S(Math.round(boltRadius()))} → "+
          $"{S(Math.round(Math.min(BOLT_R_CAP, (Js.Or(weapon.boltR, 46))*(1+Math.min(BOLT_R_MAX_INC, player.inc["boltR"]+c.v)))))})",
        apply = c=>{ player.inc["boltR"] += c.v; } },
      new Upgrade { id = "boltember", name = "Ember Scatter", icon = "burst", cls = "SPELL", minRar = "legendary",
        req = ()=>isCaster() && player.ember < EMBER_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var lv = Math.min(EMBER_MAX, player.ember + 1);
          var was = Js.Or(player.ember, 0);
          return $"Hitting a body that is ALREADY burning knocks {S(EMBER_SHOTS[lv])} embers off "+
            $"it — each flies to the nearest other monster and sets it alight, and with "+
            $"nothing else standing they loop back into the body that threw them. "+
            $"Fire also piles {S(EBURN_MAX*BURN_CAP_MUL[lv])} stacks deep instead of "+
            $"{S(EBURN_MAX*BURN_CAP_MUL[was])}"+
            (Js.T(was) ? $" (was {S(EMBER_SHOTS[was])} embers)" : "");
        },
        apply = c=>{ player.ember = Math.min(EMBER_MAX, player.ember + 1); } },
      new Upgrade { id = "bladem", name = "Blade Mastery", icon = "fist", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>!isCaster() && !isSummoner() && incRoom("melee"),
        calc = m=>(new Calc {v = 0.07+0.045*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased PHYSICAL damage — your weapon, bladestorm and vortex "+
          $"{S(incCapTag("melee"))}",
        apply = c=>{ player.incMelee += c.v; } },
      /* ------------------------------ THE SLAM -------------------------------- */
      /* Only a slammer is ever offered these, the same way only a caster is offered
         the fireball's own bricks. Every one of them is gated on its ceiling too, so
         a maxed fan stops appearing on the reward screen entirely. */
      new Upgrade { id = "slampow", name = "Fault Hammer", icon = "fist", cls = "OFFENSE",
        req = ()=>isSlammer(),
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to every tooth of the slam — before any increase",
        apply = c=>{ player.slam.flat = Math.round((player.slam.flat + c.v)*100)/100.0; syncStats(); } },
      new Upgrade { id = "slamarc", name = "Wider Fault", icon = "range", cls = "OFFENSE",
        req = ()=>isSlammer() && player.slamArc < slamArcCap() - 1e-6,
        calc = m=>(new Calc {v = 0.18+0.12*m}),
        desc = (c, _owned)=>$"The fault opens {S(Math.round(c.v*57.3))}\u00b0 wider — {S(Math.round(player.slamArc*57.3))}\u00b0 \u2192 "+
          $"{S(Math.round(Math.min(slamArcCap(), player.slamArc+c.v)*57.3))}\u00b0 (cap {S(Math.round(slamArcCap()*57.3))}\u00b0). "+
          $"A wider fan throws MORE teeth, never the same teeth further apart",
        apply = c=>{ player.slam.arcFlat += c.v; syncStats(); } },
      new Upgrade { id = "slamrange", name = "Deep Fault", icon = "range", cls = "OFFENSE",
        req = ()=>isSlammer() && player.slamRange < SLAM_RANGE_MAX - 1e-6,
        calc = m=>(new Calc {v = 0.10+0.075*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased slam RANGE — {S(Math.round(player.slamRange))} \u2192 "+
          $"{S(Math.round(Math.min(SLAM_RANGE_MAX, pool(SLAM_RANGE_BASE,0,player.slam.rangeInc+c.v,1))))} px "+
          $"(cap {S(SLAM_RANGE_MAX)})",
        apply = c=>{ player.slam.rangeInc += c.v; syncStats(); } },
      new Upgrade { id = "slamspd", name = "Fault Line", icon = "clock", cls = "OFFENSE",
        req = ()=>isSlammer() && player.slamSpeed < SLAM_SPEED_MAX - 1e-6,
        calc = m=>(new Calc {v = 0.12+0.09*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased slam SPEED — the fault runs out at "+
          $"{S(Math.round(Math.min(SLAM_SPEED_MAX, pool(SLAM_SPEED_BASE,0,player.slam.spdInc+c.v,1))))} px/s "+
          $"(cap {S(SLAM_SPEED_MAX)}), so the far teeth land before anything can walk off them",
        apply = c=>{ player.slam.spdInc += c.v; syncStats(); } },
      new Upgrade { id = "slamelem", name = "Slam of the Elements", icon = "prism", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isSlammer() && player.slam.elem < SLAM_ELEM_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=> player.slam.elem==0
          ? $"Every slam CYCLES an element — 🔥 fire, \u26a1 lightning, \u2744 frost, in turn. "+
            $"Half of every tooth is converted into it and brings its ailment with it: "+
            $"burning, shock, chill. The physical half still bleeds"
          : $"The fault closes into a RING: the fan opens all the way to "+
            $"{S(Math.round(SLAM_ARC_FULL*57.3))}\u00b0, so the stone comes up all around you",
        apply = c=>{ player.slam.elem = Math.min(SLAM_ELEM_MAX, player.slam.elem + 1); syncStats(); } },
      new Upgrade { id = "moredmg", name = "Overcharged Core", icon = "force", cls = "OFFENSE", minRar = "rare",
        req = ()=>player.moreAll < MORE_DMG_CAP - 1e-9 && heroHits(),
        calc = m=>(new Calc {v = 0.06+0.04*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% MORE damage with everything — its own multiplier, outside "+
          $"the increased pool (now ×{S(moreShown().toFixed(2))}, cap ×{S(MORE_DMG_CAP.toFixed(2))})",
        apply = c=>{ player.moreAll *= (1+c.v); } },
      new Upgrade { id = "armor", name = "Reinforced Armour", icon = "shield", cls = "DEFENSE",
        req = ()=>player.armourFlat < ARMOUR_FLAT_MAX && armourValue() < ARMOUR_VALUE_MAX,
        calc = m=>(new Calc {v = Math.round(14+10*m)}),
        desc = (c, _owned)=>$"+{S(c.v)} flat armour (cap {S(ARMOUR_FLAT_MAX)}) — less damage from attacks, never from spells",
        apply = c=>{ player.armourFlat += c.v; syncStats(); } },
      new Upgrade { id = "armorinc", name = "Plated Rig", icon = "shieldplus", cls = "DEFENSE",
        req = ()=>player.armourFlat>0 && armourValue() < ARMOUR_VALUE_MAX,
        calc = m=>(new Calc {v = 0.07+0.045*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased armour (cap {S(Math.round(ARMOUR_INC_MAX*100))}%)",
        apply = c=>{ player.incArmour += c.v; syncStats(); } },
      new Upgrade { id = "suppress", name = "Spell Suppression", icon = "ward", cls = "DEFENSE", minRar = "uncommon",
        /* the WHOLE chance, what you hold included: a shield's suppression counts,
           and at 100% there is nothing left for this brick to give */
        req = ()=>suppressChance() < 1,
        calc = m=>(new Calc {v = 0.08+0.06*m}),
        desc = (c, _owned)=>{ double now = suppressChance(), to = Math.min(1, now + c.v);
          return $"+{S(Math.round((to - now)*100))}% chance to suppress spell damage ({S(Math.round(now*100))}% \u2192 {S(Math.round(to*100))}%, cap 100%) — "+
            $"a suppressed hit deals {S(Math.round(SUPPRESS_TAKEN*100))}%"; },
        apply = c=>{ player.suppressFlat += c.v; syncStats(); } },
      new Upgrade { id = "reach", name = "Longsword", icon = "range", cls = "OFFENSE",
        /* a staff has no reach to lengthen — it throws. Nor has a mace: it does not
           swing at all any more, so reach would buy it nothing. */
        req = ()=>isMelee() && !isSlammer() && player.reach < REACH_MAX,
        calc = m=>(new Calc {v = Math.round(10*m+8)}),
        desc = (c, _owned)=>$"+{S(c.v)} melee reach (cap {S(weapon.reach+REACH_MAX)})",
        apply = c=>{ player.reachFlat += c.v; syncStats(); } },
      new Upgrade { id = "aspd", name = "Quickening", icon = "clock", cls = "OFFENSE",
        req = ()=>player.atkRate > atkFloor(),
        calc = m=>(new Calc {v = 0.10+0.05*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased ATTACK SPEED — swings and casts alike "+
          $"(floor {S(atkFloor().toFixed(2))}s per {S(isCaster()?"cast":isArcher()?"shot":isSummoner()?"raising":"swing")})",
        apply = c=>{ player.inc["atk"] += c.v; syncStats(); } },
      /* THE GLOBAL CAST SPEED. `Quickening` is the hero's own hands — it shortens
         the swing or the bolt. This is every ABILITY the hero owns, all at once,
         and it is the only brick that touches all of them. */
      new Upgrade { id = "castspd", name = "Runic Tempo", icon = "tempo", cls = "SPELL", minRar = "uncommon",
        req = ()=>spellsOwned() > 0 && castRoom(),
        calc = m=>(new Calc {v = 0.10+0.06*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased CAST SPEED — every ability you own comes off "+
          $"cooldown faster, each one stopping at its own floor",
        apply = c=>{ player.inc["cast"] += c.v; syncStats(); } },
      new Upgrade { id = "crit", name = "Lucky Brick", icon = "target", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>critChance() < CRIT_CHANCE_CAP,
        calc = m=>(new Calc {v = 0.16+0.13*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased critical strike chance (cap {S(Math.round(CRIT_CHANCE_CAP*100))}%)",
        apply = c=>{ player.incCrit += c.v; } },
      new Upgrade { id = "critpow", name = "Heavy Crits", icon = "burst", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>player.critMultPct < CRIT_MULT_CAP,
        calc = m=>(new Calc {v = Math.round(6+4.5*m)}),
        desc = (c, _owned)=>$"+{S(c.v)}% to critical strike multiplier (cap {S(CRIT_MULT_CAP)}%)",
        apply = c=>{ player.critMultFlat += c.v; syncStats(); } },
      new Upgrade { id = "hp", name = "Sturdy Plates", icon = "heartplus", cls = "DEFENSE",
        req = ()=>player.maxHp < heartCap(),
        calc = m=>(new Calc {v = Math.max(2,Math.round(2*m))}),
        desc = (c, _owned)=>$"+{S(c.v)} FLAT max hearts, and heal what they add (cap {S(heartCap())})",
        apply = c=>{ addMaxHp(c.v); } },
      /* the middle rung of the health ladder: Sturdy Plates adds flat hearts at the
         bottom, Titanium Chassis is a legendary MORE at the top, and nothing in
         between multiplied what the flat ones gave you. This does. */
      new Upgrade { id = "hpinc", name = "Reinforced Frame", icon = "heartplus", cls = "DEFENSE", minRar = "epic",
        req = ()=>player.maxHp < heartCap(),
        calc = m=>(new Calc {v = m >= 4.3 ? 0.60 : 0.40}),
        desc = (c, _owned)=>{
          var before = player.maxHp;
          var after = Math.min(heartCap(), Math.round(pool(HP_BASE, player.hpFlat, player.inc["hp"] + c.v, player.hpMore)));
          return $"{S(Math.round(c.v*100))}% INCREASED maximum hearts — "+
            $"{S(before)} → {S(after)}, and it heals what it adds (cap {S(heartCap())})";
        },
        apply = c=>{ var before = player.maxHp;
          player.inc["hp"] += c.v; syncStats();
          if(player.maxHp > before) player.hp = Math.round(Math.min(player.maxHp, player.hp + (player.maxHp-before))*100)/100.0; } },
        new Upgrade { id = "regen", name = "Repair Bricks", icon = "regen", cls = "DEFENSE",
        req = ()=>player.regenRate < REGEN_MAX,
        calc = m=>(new Calc {v = 0.035+0.022*m}),
        desc = (c, _owned)=>$"Regenerate {S((c.v).toFixed(3))} more hearts per second per {S(REGEN_REF_HP)} max hearts "+
          $"— it scales with your bar (cap {S(REGEN_MAX.toFixed(2))})",
        apply = c=>{ player.regenFlat += c.v; syncStats(); } },
      new Upgrade { id = "spd", name = "Sprint Studs", icon = "boot", cls = "UTILITY",
        req = ()=>player.speed < SPEED_MAX,
        calc = m=>(new Calc {v = 0.04+0.03*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% move speed (cap {S(SPEED_MAX)} px/s)",
        apply = c=>{ player.inc["speed"] += c.v; syncStats(); } },
      new Upgrade { id = "roll", name = "Phantom Roll", icon = "roll", cls = "UTILITY",
        req = ()=>player.dash.cdMax > DASH_CD_MIN,
        calc = m=>(new Calc {v = 0.10+0.05*m}),
        desc = (c, _owned)=>$"Roll cooldown -{S(Math.round(c.v*100))}% (floor {S(DASH_CD_MIN.toFixed(2))}s)",
        apply = c=>{ player.inc["roll"] += c.v; syncStats(); } },
      new Upgrade { id = "mag", name = "Magnet Bricks", icon = "magnet", cls = "UTILITY",
        req = ()=>player.magnetR < MAGNET_MAX,
        calc = m=>(new Calc {v = 0.20+0.15*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% stud pickup range (cap {S(MAGNET_MAX)})",
        apply = c=>{ player.inc["magnet"] += c.v; syncStats(); } },
      new Upgrade { id = "greed", name = "Golden Touch", icon = "coin", cls = "UTILITY",
        calc = m=>(new Calc {v = 0.15+0.10*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% studs from every source",
        apply = c=>{ player.inc["studs"] += c.v; syncStats(); } },
      new Upgrade { id = "wisdom", name = "Stud Wisdom", icon = "xp", cls = "UTILITY",
        calc = m=>(new Calc {v = 0.15+0.10*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% MORE XP — its own multiplier, so they compound",
        apply = c=>{ player.xpMore *= (1+c.v); syncStats(); } },
      /* LIFESTEAL, in two halves, because the two feel completely different: more
         often is a steadier drip, more per swing is a bigger gulp. Neither is
         offered to a caster — the staff is never in reach to drink. */
      new Upgrade { id = "leech", name = "Vampiric Edge", icon = "heart", cls = "DEFENSE", minRar = "uncommon",
        req = ()=>isMelee() && player.leech < LEECH_MAX,
        calc = m=>(new Calc {v = 0.015+0.012*m}),
        desc = (c, _owned)=>$"Your swings leech +{S((c.v*100).toFixed(1))}% of the damage they land "+
          $"(cap {S(Math.round(LEECH_MAX*100))}%, and never more than "+
          $"{S(Math.round(LEECH_HIT_CAP*100))}% of your hearts in one swing)",
        apply = c=>{ player.leechFlat += c.v; syncStats(); } },
      new Upgrade { id = "leechrate", name = "Thirsting Grip", icon = "heart", cls = "DEFENSE", minRar = "uncommon",
        req = ()=>isMelee() && player.leechChance < LEECH_CHANCE_MAX,
        calc = m=>(new Calc {v = 0.05+0.045*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% chance for a swing to leech at all "+
          $"(cap {S(Math.round(LEECH_CHANCE_MAX*100))}%)",
        apply = c=>{ player.leechChanceFlat += c.v; syncStats(); } },
      new Upgrade { id = "resfire", name = "Flameguard Plates", icon = "flame", cls = "DEFENSE", minRar = "rare",
        req = ()=>player.resFlat["fire"] < RES_CAP,
        calc = m=>(new Calc {v = 0.04+0.035*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% fire resistance (cap {S(Math.round(RES_CAP*100))}%)",
        apply = c=>{ player.resFlat["fire"] += c.v; syncStats(); } },
      new Upgrade { id = "resfrost", name = "Thermal Lining", icon = "snow", cls = "DEFENSE", minRar = "rare",
        req = ()=>player.resFlat["frost"] < RES_CAP,
        calc = m=>(new Calc {v = 0.04+0.035*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% frost resistance (cap {S(Math.round(RES_CAP*100))}%)",
        apply = c=>{ player.resFlat["frost"] += c.v; syncStats(); } },
      new Upgrade { id = "resshock", name = "Rubber Soles", icon = "bolt", cls = "DEFENSE", minRar = "rare",
        req = ()=>player.resFlat["shock"] < RES_CAP,
        calc = m=>(new Calc {v = 0.04+0.035*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% lightning resistance (cap {S(Math.round(RES_CAP*100))}%)",
        apply = c=>{ player.resFlat["shock"] += c.v; syncStats(); } },
      new Upgrade { id = "resall", name = "Elementalist Bricks", icon = "prism", cls = "DEFENSE", minRar = "rare",
        req = ()=>ELEMENTS.some(el=>player.resFlat[el] < RES_CAP),
        calc = m=>(new Calc {v = 0.025+0.022*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% to fire, frost AND lightning resistance",
        apply = c=>{ foreach (var el in ELEMENTS) player.resFlat[el] += c.v; syncStats(); } },
      new Upgrade { id = "orbit", name = "Guardian Brick", icon = "orbit", cls = "SPELL", minRar = "uncommon",
        req = ()=>canTakeSpell("orbit") && player.orbit.n < orbitMaxN(),
        calc = m=>(new Calc {n = spellCount(m)}),
        desc = (c,owned)=> owned>0 ? $"+{S(c.n)} orbiting brick{S(c.n>1?"s":"")} (cap {S(orbitMaxN())})"
          : $"Spell: {S(c.n)} brick{S(c.n>1?"s":"")} circle{S(c.n>1?"":"s")} you, smashing foes (max {S(orbitMaxN())})",
        apply = c=>{ player.orbit.n = Math.min(orbitMaxN(), player.orbit.n + c.n); } },
      new Upgrade { id = "orbitpow", name = "Heavy Orbit", icon = "orbit", cls = "SPELL", req = ()=>player.orbit.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to every guardian brick — before any increase",
        apply = c=>{ player.orbit.dmg = Math.round((player.orbit.dmg + c.v)*100)/100.0; } },
      new Upgrade { id = "orbitring", name = "Wide Orbit", icon = "orbit", cls = "SPELL",
        req = ()=>player.orbit.n>0 && (player.orbit.r<ORBIT_MAX_R || player.orbit.size<ORBIT_MAX_SIZE),
        calc = m=>(new Calc {r = Math.round(6*m), s = Math.round(2*m)}),
        desc = (c, _owned)=>$"+{S(c.r)} ring reach (cap {S(ORBIT_MAX_R)}) and +{S(c.s)} brick size (cap {S(ORBIT_MAX_SIZE)}) — "+
          $"the ring sweeps a wider circle and each brick covers more of it",
        apply = c=>{ player.orbit.r = Math.min(ORBIT_MAX_R, player.orbit.r + c.r);
          player.orbit.size = Math.min(ORBIT_MAX_SIZE, player.orbit.size + c.s); } },
      new Upgrade { id = "orbitspd", name = "Swift Orbit", icon = "clock", cls = "SPELL",
        req = ()=>player.orbit.n>0 && player.orbit.spd<ORBIT_MAX_SPD,
        calc = m=>(new Calc {v = 0.22+0.12*m}),
        desc = (c, _owned)=>$"Orbit spins {S(Math.round(c.v*100))}% faster (hits much more often, capped at ×{S((ORBIT_MAX_SPD/ORBIT_SPD_BASE).toFixed(1))})",
        apply = c=>{ player.inc["orbitSpd"] += c.v; syncStats(); } },
      new Upgrade { id = "orbm", name = "Orb Mastery", icon = "arcane", cls = "SPELL", minRar = "uncommon",
        req = ()=>incRoom("spell"),
        calc = m=>(new Calc {v = 0.07+0.045*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased spell damage — every brick you cast "+
          $"{S(incCapTag("spell"))}",
        apply = c=>{ player.incSpell += c.v; } },
      new Upgrade { id = "chain", name = "Storm Brick", icon = "chain", cls = "SPELL", minRar = "uncommon",
        req = ()=>canTakeSpell("chain") && player.chain.n < chainMaxTargets(),
        calc = m=>(new Calc {n = spellCount(m)}),
        desc = (c,owned)=>{
          double n2 = Math.min(chainMaxTargets(), player.chain.n + c.n), lp = n2-1;
          var tail = lp>0 ? $" — leaps land for {S(Math.round(chainLeapStart(lp)*100))}%…{S(Math.round(chainLeapFloor(lp)*100))}% of the opening strike" : "";
          var cap = $" (max {S(CHAIN_MAX_LEAPS)} leaps)";
          return owned>0 ? $"+{S(n2 - player.chain.n)} chain — every bolt now leaps to {S(n2)} foes{S(cap)}{S(tail)}"
            : $"Spell: a storm brick hovers by you, firing lightning that leaps to {S(c.n)} foe{S(c.n>1?"s":"")}{S(cap)}{S(tail)}";
        },
        apply = c=>{ player.chain.n = Math.min(chainMaxTargets(), player.chain.n + c.n);
          if(player.chain.zappers<1) player.chain.zappers = 1; } },
      /* A second hovering brick is a second whole bolt — the strongest single step
         any spell can take — so it starts at rare and gives one at a time. */
      new Upgrade { id = "chainbrick", name = "Chain Lightning Brick", icon = "zapper", cls = "SPELL", minRar = "rare",
        req = ()=>player.chain.n>0 && player.chain.zappers<chainMaxZappers(),
        calc = m=>(new Calc {n = byRar(m, [1, 1, 1, 2, 3])}),
        desc = (c, _owned)=>{
          return $"+{S(c.n)} storm brick{S(c.n>1?"s":"")} hovering by you, each firing its own bolt (max {S(chainMaxZappers())})";
        },
        apply = c=>{ player.chain.zappers = Math.min(chainMaxZappers(), player.chain.zappers + c.n); } },
      new Upgrade { id = "chainpow", name = "Storm Surge", icon = "chain", cls = "SPELL", req = ()=>player.chain.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to the storm brick — the opening strike and every leap off it",
        apply = c=>{ player.chain.dmg = Math.round((player.chain.dmg + c.v)*100)/100.0; } },
      /* ONE AILMENT PER BRICK (this was Afflict, one chance for all five). Each is
         only offered to a hero whose damage is actually of that type -- by the same
         dealtTypes() the conversions use, so a sword that has turned half its edge
         into fire is offered Kindling and a pure sword is not. */
      ..new[] { ("fire", "ignitechance", "Kindling", "flame", "IGNITE", "fire"),
          ("frost", "chillchance", "Numbing Cold", "snow", "CHILL", "cold"),
          ("shock", "shockchance", "Static Charge", "bolt", "SHOCK", "lightning"),
          ("chaos", "poisonchance", "Venom Glands", "bane", "POISON", "chaos") }.map(row => { var (t, id, name, icon, AIL, dmg) = row; return new Upgrade {
        id = id, name = name, icon = icon, cls = "OFFENSE", minRar = "uncommon", ailType = t,
        req = ()=>!!dealtTypes()[t] && ailTypeChance(t) < AIL_TYPE_MAX,
        calc = m=>(new Calc {v = byRar(m, AIL_TYPE_STEP)}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% chance for a {S(dmg)} hit to {S(AIL)} (now "+
          $"{S(Math.round(ailTypeChance(t)*100))}%, cap {S(Math.round(AIL_TYPE_MAX*100))}%)"+
          (AIL_CRIT[t] >= 1 ? $" — a critical already does, this is what an ordinary hit rolls" : ""),
        apply = c=>{ player.ailTypeFlat[t] = player.ailTypeFlat[t] + c.v; syncStats(); } }; }),
      new Upgrade { id = "storm", name = "Bladestorm", icon = "storm", cls = "SPELL", minRar = "uncommon",
        req = ()=>isMelee() && canTakeSpell("storm") && player.storm.n < stormMaxN(),
        calc = m=>(new Calc {n = spellCount(m)}),
        desc = (c,owned)=>{
          var cnt = player.storm.count;
          var next = Math.min(stormMaxN(), player.storm.n + c.n);
          return owned>0
            ? $"+{S(next-player.storm.n)} bladestorm — {S(next*cnt)} swords a burst (cap {S(STORM_MAX_BLADES)})"
            : $"Spell: a whirl of {S(c.n*cnt)} swords bursts out of you (cap {S(STORM_MAX_BLADES)})";
        },
        apply = c=>{ player.storm.n = Math.min(stormMaxN(), player.storm.n + c.n); } },
      new Upgrade { id = "stormpow", name = "Storm Edge", icon = "storm", cls = "SPELL",
        /* ONE req, not two. A second `req:` key on the same object literal silently
           wins, and the one that lost here was the one checking you own the spell —
           so a sword hero was offered upgrades to a Bladestorm they had never
           picked up. gate2.js walks every spell tree against a hero who owns
           nothing and fails on anything that answers yes. */
        req = ()=>isMelee() && player.storm.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to every bladestorm blade — before any increase",
        apply = c=>{ player.storm.dmg = Math.round((player.storm.dmg + c.v)*100)/100.0; } },
      new Upgrade { id = "stormsize", name = "Broad Blades", icon = "storm", cls = "SPELL",
        req = ()=>isMelee() && player.storm.n>0 && player.storm.size<STORM_MAX_SIZE,
        calc = m=>(new Calc {s = 0.15*m}),
        desc = (c, _owned)=>$"+{S(Math.round(c.s*100))}% blade size (cap ×{S(STORM_MAX_SIZE.toFixed(1))}) — "+
          $"a bigger blade sweeps a wider spiral and catches more on the way out",
        apply = c=>{ player.storm.size = Math.min(STORM_MAX_SIZE, player.storm.size + c.s); } },
      new Upgrade { id = "stormrate", name = "Storm Caller", icon = "clock", cls = "SPELL",
        req = ()=>isMelee() && player.storm.n>0 && player.storm.rate > STORM_MIN_RATE,
        calc = m=>(new Calc {v = 0.14+0.06*m}),
        desc = (c, _owned)=>$"Bladestorm triggers {S(Math.round(c.v*100))}% faster (floor {S(STORM_MIN_RATE.toFixed(2))}s)",
        apply = c=>{ player.inc["stormRate"] += c.v; syncStats(); } },
      new Upgrade { id = "vortex", name = "Blade Vortex", icon = "vortex", cls = "SPELL", minRar = "uncommon",
        req = ()=>isMelee() && canTakeSpell("vortex"),
        calc = m=>(new Calc {n = spellCount(m)}),
        desc = (c,owned)=> owned>0 ? $"+{S(c.n)} stack{S(c.n>1?"s":"")} — longer & wider whirlwind"
          : "Spell: spin like a whirlwind, shredding everything around you (scales with Blade Mastery)",
        apply = c=>{ player.vortex.n += c.n; } },
      new Upgrade { id = "vortexpow", name = "Whirling Edge", icon = "vortex", cls = "SPELL",
        /* ONE req, not two. A second `req:` key on the same object silently wins,
           so this brick was offered to a hero who owns no vortex at all — and then
           moved nothing, because every vortex probe reads player.vortex.n. */
        req = ()=>isMelee() && player.vortex.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_SPELL)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to every vortex tick — before any increase",
        apply = c=>{ player.vortex.dmg = Math.round((player.vortex.dmg + c.v)*100)/100.0; } },
      new Upgrade { id = "vortexarea", name = "Vortex Reach", icon = "vortex", cls = "SPELL",
        req = ()=>isMelee() && player.vortex.n>0 && player.vortex.r < VORTEX_MAX_R,
        calc = m=>(new Calc {v = Math.round(8*m)}),
        desc = (c, _owned)=>$"+{S(c.v)} blade vortex radius (cap {S(VORTEX_MAX_R)})",
        apply = c=>{ player.vortex.r = Math.min(VORTEX_MAX_R, player.vortex.r + c.v); } },
      new Upgrade { id = "vortexrate", name = "Vortex Speed", icon = "clock", cls = "SPELL",
        req = ()=>isMelee() && player.vortex.n>0 && (player.vortex.rate > VORTEX_MIN_RATE || player.vortex.hitMul > VORTEX_MIN_HITMUL),
        calc = m=>(new Calc {v = 0.13+0.06*m}),
        desc = (c, _owned)=>$"Vortex triggers {S(Math.round(c.v*100))}% faster & spins quicker",
        apply = c=>{ player.inc["vortexRate"] += c.v; syncStats(); } },
      new Upgrade { id = "bane", name = "Brickbane", icon = "bane", cls = "SPELL", minRar = "rare",
        req = ()=>canTakeSpell("bane") && (player.bane.n == 0 || player.bane.stacks < BANE_STACK_MAX),
        calc = m=>(new Calc {n = byRar(m, [0, 0, 1, 2, 3])}),
        desc = (c,owned)=>{
          var now  = player.bane.stacks;
          var next = Math.min(BANE_STACK_MAX, now + c.n);
          return owned>0
            ? (next > now
                ? $"The gas lays {S(next)} dose{S(next>1?"s":"")} per touch instead of {S(now)} "+
                  $"(cap {S(BANE_STACK_MAX)}) — every body it washes over starts that much deeper in"
                : $"Already laying the full {S(BANE_STACK_MAX)} doses per touch")
            : $"Spell: cough a {S(Math.round(BANE_ARC_BASE*180.0/Math.PI))}° wedge of poison gas — "+
              $"it hits once, then POISONS everything it touched {S(next)} dose{S(next>1?"s":"")} deep";
        },
        apply = c=>{ if(player.bane.n == 0) player.bane.n = 1;
          player.bane.stacks = Math.min(BANE_STACK_MAX, player.bane.stacks + c.n); } },
      new Upgrade { id = "banearc", name = "Choking Spread", icon = "bane", cls = "SPELL",
        req = ()=>player.bane.n>0 && player.bane.arc < BANE_ARC_MAX-0.01,
        calc = m=>(new Calc {v = (26+18*m)*Math.PI/180.0}),
        desc = (c, _owned)=>{
          var now = Math.round(player.bane.arc*180.0/Math.PI);
          var next = Math.round(Math.min(BANE_ARC_MAX, player.bane.arc+c.v)*180.0/Math.PI);
          return $"Widen the wedge {S(now)}° → {S(next)}° (at 360° the cloud comes off you in every direction)";
        },
        apply = c=>{ player.bane.arc = Math.min(BANE_ARC_MAX, player.bane.arc + c.v); } },
      new Upgrade { id = "baner", name = "Creeping Fog", icon = "bane", cls = "SPELL",
        req = ()=>player.bane.n>0 && player.bane.r < BANE_R_MAX,
        calc = m=>(new Calc {v = 0.14+0.09*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased gas reach (cap {S(BANE_R_MAX)})",
        apply = c=>{ player.inc["baneR"] += c.v; syncStats(); } },
      new Upgrade { id = "banerate", name = "Foul Bellows", icon = "clock", cls = "SPELL",
        req = ()=>player.bane.n>0 && player.bane.rate > BANE_MIN_RATE,
        calc = m=>(new Calc {v = 0.14+0.07*m}),
        desc = (c, _owned)=>$"Brickbane coughs {S(Math.round(c.v*100))}% faster (floor {S(BANE_MIN_RATE.toFixed(2))}s)",
        apply = c=>{ player.inc["baneRate"] += c.v; syncStats(); } },
      new Upgrade { id = "banedot", name = "Virulence", icon = "bane", cls = "SPELL", req = ()=>player.bane.n>0,
        calc = m=>(new Calc {v = byRar(m, [0.3, 0.6, 1.2, 2.0, 3.2]), t = byRar(m, [0.5, 0.8, 1.4, 2, 3])}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} poison damage per second and +{S(fmtHp(c.t))}s duration "+
          $"(cap {S(BANE_DOT_MAX_T)}s) — the damage over time, not the opening hit",
        apply = c=>{ player.baneDotFlat = Math.round((player.baneDotFlat + c.v)*100)/100.0;
          player.bane.dotT = Math.min(BANE_DOT_MAX_T, player.bane.dotT + c.t);
          syncStats(); } },
      /* not a Brickbane brick — a DAMAGE brick that happens to have one customer
         so far. Everything the hero inflicts that eats away over time reads it. */
      new Upgrade { id = "banedotinc", name = "Rotbrick", icon = "arcane", cls = "OFFENSE", minRar = "rare",
        req = ()=>hasAnyDot() && player.inc["dot"] < INC_DMG_CAP - 1e-9,
        calc = m=>(new Calc {v = 0.10+0.07*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased DAMAGE OVER TIME — every lingering "+
          $"damage you inflict, whatever put it there (now {S(Math.round(dotInc()*100))}%, "+
          $"cap {S(Math.round(INC_DMG_CAP*100))}%)",
        apply = c=>{ player.inc["dot"] += c.v; syncStats(); } },
      /* BLEED lives in the crit tree, not in a tree of its own: a swing bleeds
         because it crit, and these two decide how often and how deep. */
      new Upgrade { id = "bleedchance", name = "Serrated Edge", icon = "sword", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>!isCaster() && !isSummoner() && !!dealtTypes()["phys"] && player.bleedChance < AIL_TYPE_MAX,
        calc = m=>(new Calc {v = byRar(m, AIL_TYPE_STEP)}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% chance for an ATTACK to open a BLEED — a wound is "+
          $"the one ailment a critical does not hand you, so this is what opens one "+
          $"(now {S(Math.round(bleedChance(false)*100))}% on a swing, cap {S(Math.round(AIL_TYPE_MAX*100))}%)",
        apply = c=>{ player.bleedChanceFlat += c.v; syncStats(); } },
      new Upgrade { id = "bleedpow", name = "Deep Cuts", icon = "sword", cls = "OFFENSE", minRar = "rare",
        req = ()=>!isCaster() && !isSummoner() && player.bleedShare < BLEED_SHARE_MAX,
        calc = m=>(new Calc {v = byRar(m, [0.03, 0.05, 0.08, 0.12, 0.18])}),
        desc = (c, _owned)=>{
          var next = Math.min(BLEED_SHARE_MAX, player.bleedShare + c.v);
          return $"Every wound bleeds for {S(Math.round(next*100))}% of the hit each second "+
            $"instead of {S(Math.round(player.bleedShare*100))}% (cap {S(Math.round(BLEED_SHARE_MAX*100))}%), "+
            $"for {S(BLEED_T)}s — {S(Math.round(next*BLEED_STACK_MAX*100))}%/s with all "+
            $"{S(BLEED_STACK_MAX)} open";
        },
        apply = c=>{ player.bleedShareFlat += c.v; syncStats(); } },
      new Upgrade { id = "baneburst", name = "Spore Burst", icon = "bomblets", cls = "SPELL", minRar = "legendary",
        req = ()=>player.bane.n>0 && player.bane.burst < BANE_BURST.Count()-1,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var lv = player.bane.burst + 1;
          return $"A poisoned body that dies BURSTS — {S(Math.round(BANE_BURST[lv]*100))}% of the time — "+
            $"handing EVERY stack it was carrying to everything within {S(BANE_BURST_R[lv])}. "+
            $"No blast: what spreads is the rot. "+
            $"Poison also piles {S(POISON_CAP_MUL[lv])}× deeper — {S(POISON_MAX*POISON_CAP_MUL[lv])} "+
            $"on a monster, {S(POISON_MAX_BOSS*POISON_CAP_MUL[lv])} on a boss"+
            (lv==1 ? "" : " (was "+Math.round(BANE_BURST[lv-1]*100)+"%, "+
              BANE_BURST_R[lv-1]+" reach and "+POISON_MAX*POISON_CAP_MUL[lv-1]+" deep)");
        },
        apply = c=>{ player.bane.burst = Math.min(BANE_BURST.Count()-1, player.bane.burst + 1); } },
      new Upgrade { id = "freeze", name = "Block Freeze", icon = "freeze", cls = "SPELL", minRar = "rare",
        /* a frost spell belongs to the hero who can carry frost */
        req = ()=>isCaster() && canTakeSpell("freeze") && (player.freeze.n == 0 || freezeDeeper()),
        calc = m=>(new Calc {n = Math.max(1, byRar(m, FREEZE_COUNT))}),
        desc = (c,owned)=> owned>0
          ? freezeChillDesc(c.n)
          : $"Spell: a burst of frost around you for {S(fmtHp(frostDamage()))} FROST damage, "+
            $"laying a CHILL stack — five freezes solid, and leaves the body BRITTLE",
        /* clamped where it is WRITTEN, not only where it is read, so the character
           sheet never reports a stack the pulse cannot use */
        apply = c=>{ player.freeze.n = Math.min(FREEZE_CHILL_MAX, player.freeze.n + c.n); } },
      new Upgrade { id = "freezr", name = "Deep Chill", icon = "freeze", cls = "SPELL",
        req = ()=>isCaster() && player.freeze.n>0 && player.freeze.r < FREEZE_MAX_R,
        calc = m=>(new Calc {v = Math.round(20*m)}),
        desc = (c, _owned)=>$"+{S(c.v)} block freeze radius (cap {S(FREEZE_MAX_R)})",
        apply = c=>{ player.freeze.r = Math.min(FREEZE_MAX_R, player.freeze.r + c.v); } },
      new Upgrade { id = "freezc", name = "Winter Wind", icon = "clock", cls = "SPELL",
        req = ()=>isCaster() && player.freeze.n>0 && player.freeze.rate > FREEZE_MIN_RATE,
        calc = m=>(new Calc {v = 0.12+0.05*m}),
        desc = (c, _owned)=>$"Freeze triggers {S(Math.round(c.v*100))}% faster (floor {S(FREEZE_MIN_RATE.toFixed(1))}s)",
        apply = c=>{ player.inc["freezeRate"] += c.v; syncStats(); } },
      /* the one the spell never had: a flat number to build on. Deepening Winter
         lived beside it and did the same job the spell's own stacks now do, which
         made picking the spell up twice and picking the spell up once feel like the
         same card wearing two coats. */
      new Upgrade { id = "freezf", name = "Killing Frost", icon = "freeze", cls = "SPELL",
        req = ()=>isCaster() && player.freeze.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_FREEZE)}),
        desc = (c, _owned)=>$"+{S(c.v)} flat frost damage to every Block Freeze pulse",
        apply = c=>{ player.freeze.flat = Math.round((player.freeze.flat + c.v)*100)/100.0; } },
      new Upgrade { id = "bombplus", name = "Bomb Volley", icon = "bomb", cls = "SPELL", minRar = "uncommon",
        req = ()=>canTakeSpell("bomb") && player.bomb.n < bombMaxN(),
        calc = m=>(new Calc {n = spellCount(m)}),
        desc = (c,owned)=> owned>0 ? $"+{S(c.n)} bomb{S(c.n>1?"s":"")} in the compass spread (cap {S(bombMaxN())})"
          : $"Spell: launch {S(Math.min(bombMaxN(),c.n))} bomb{S(c.n>1?"s":"")} in a compass spread",
        apply = c=>{ player.bomb.n = Math.min(bombMaxN(), player.bomb.n + c.n); } },
      new Upgrade { id = "bombpow", name = "Big Kaboom", icon = "bomb", cls = "SPELL", req = ()=>player.bomb.n>0,
        calc = m=>(new Calc {v = byRar(m, FLAT_BOMB)}),
        desc = (c, _owned)=>$"+{S(fmtHp(c.v))} FLAT base damage to every bomb — before any increase",
        apply = c=>{ player.bomb.dmg = Math.round((player.bomb.dmg + c.v)*100)/100.0; } },
      new Upgrade { id = "bombarea", name = "Wide Blast", icon = "bomb", cls = "SPELL",
        req = ()=>player.bomb.n>0 && player.bomb.r < BOMB_MAX_R,
        calc = m=>(new Calc {r = 0.06+0.03*m}),
        desc = (c, _owned)=>$"{S(Math.round(c.r*100))}% increased blast radius (cap {S(BOMB_MAX_R)}) — "+
          $"every bomb, and every bomblet that falls out of one",
        apply = c=>{ player.inc["bombR"] += c.r; syncStats(); } },
      new Upgrade { id = "bombcd", name = "Quick Fuse", icon = "clock", cls = "SPELL",
        req = ()=>player.bomb.n>0 && player.bomb.rate > BOMB_MIN_RATE,
        calc = m=>(new Calc {v = 0.12+0.05*m}),
        desc = (c, _owned)=>$"Bombs launch {S(Math.round(c.v*100))}% faster (floor {S(BOMB_MIN_RATE.toFixed(1))}s)",
        apply = c=>{ player.inc["bombRate"] += c.v; syncStats(); } },
      new Upgrade { id = "cluster", name = "Cluster Charge", icon = "bomblets", cls = "SPELL", minRar = "legendary",
        req = ()=>player.bomb.n>0 && player.bomb.cluster < CLUSTER_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var lv = player.bomb.cluster + 1;
          return lv==1
            ? $"Every bomb bursts into 3-5 bomblets that scatter and detonate for {S(Math.round(CLUSTER_FALLOFF*100))}% damage"
            : $"Bomblets burst again — a third wave at {S(Math.round(CLUSTER_FALLOFF*CLUSTER_FALLOFF*100))}% damage";
        },
        apply = c=>{ player.bomb.cluster = Math.min(CLUSTER_MAX, player.bomb.cluster + 1); } },
      new Upgrade { id = "stormback", name = "Returning Blades", icon = "boomerang", cls = "SPELL", minRar = "legendary",
        req = ()=>isMelee() && player.storm.n>0 && player.storm.boomer < BOOMER_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=> player.storm.boomer==0
          ? "Storm blades stop fading at the end of their flight — each one wheels around and cuts its way back to you"
          : "You catch every returning blade and hurl it straight back out: four passes per blade",
        apply = c=>{ player.storm.boomer = Math.min(BOOMER_MAX, player.storm.boomer + 1); } },
      /* ---- THE BOW. The arrow is upgraded by everything a weapon attack reads --
         flat physical, increased physical, attack speed, crit, accuracy, bleed --
         and by these, which are its own. ---- */
      /* ---- THE LEGION. The scepter's bricks: how many, how hard, how long ---- */
      new Upgrade { id = "minionmax", name = "Bone Legion", icon = "skull", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>isSummoner() && player.minions.max < MINION_MAX_CAP,
        calc = m=>(new Calc {n = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>{ var to = Math.min(MINION_MAX_CAP, player.minions.max + c.n);
          return $"+{S(to - player.minions.max)} SKELETON{S(to - player.minions.max > 1 ? "S" : "")} in your legion "+
            $"({S(player.minions.max)} → {S(to)}, cap {S(MINION_MAX_CAP)})"; },
        apply = c=>{ player.minions.max = Math.min(MINION_MAX_CAP, player.minions.max + c.n); } },
      new Upgrade { id = "miniondmg", name = "Grim Feast", icon = "skull", cls = "OFFENSE",
        req = ()=>isSummoner(),
        calc = m=>(new Calc {v = byRar(m, [0.12, 0.18, 0.26, 0.36, 0.50])}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% increased MINION damage — their own pool, which nothing you buy for "+
          $"your own hits touches (a skeleton hits for {S(fmtHp(minionDamage(player.minions.mages)))} now)",
        apply = c=>{ player.minions.inc = Math.round((player.minions.inc + c.v)*1000)/1000.0; } },
      new Upgrade { id = "minionlife", name = "Bone Armour", icon = "shieldplus", cls = "DEFENSE",
        req = ()=>isSummoner(),
        calc = m=>(new Calc {v = byRar(m, [0.15, 0.22, 0.32, 0.45, 0.65])}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% increased MINION life (a skeleton stands up with "+
          $"{S(Math.round(minionMaxHp()))} now). Skeletons already standing keep what they rose with",
        apply = c=>{ player.minions.life = Math.round((player.minions.life + c.v)*1000)/1000.0; } },
      new Upgrade { id = "minionspd", name = "Quickened Bones", icon = "tempo", cls = "OFFENSE",
        req = ()=>isSummoner() && player.minions.spd < 1.5,
        calc = m=>(new Calc {v = byRar(m, [0.08, 0.12, 0.16, 0.22, 0.30])}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% increased MINION attack and cast speed",
        apply = c=>{ player.minions.spd = Math.min(1.5, Math.round((player.minions.spd + c.v)*1000)/1000.0); } },
      new Upgrade { id = "minionflat", name = "Sharpened Bones", icon = "sword", cls = "OFFENSE",
        req = ()=>isSummoner(),
        calc = m=>(new Calc {v = byRar(m, [0.3, 0.5, 0.8, 1.2, 1.8])}),
        desc = (c, _owned)=>$"+{S(c.v)} FLAT damage to every minion hit, before any increase multiplies it",
        apply = c=>{ player.minions.flat = Math.round((player.minions.flat + c.v)*100)/100.0; } },
      new Upgrade { id = "miniontwin", name = "Twin Raising", icon = "split", cls = "OFFENSE", minRar = "rare",
        req = ()=>isSummoner() && player.minions.perCast < MINION_PERCAST_MAX,
        calc = m=>(new Calc {n = byRar(m, [1, 1, 1, 2, 3])}),
        desc = (c, _owned)=>{ var to = Math.min(MINION_PERCAST_MAX, player.minions.perCast + c.n);
          return $"Each raising stands up {S(to)} skeletons ({S(player.minions.perCast)} → {S(to)}, cap {S(MINION_PERCAST_MAX)})"; },
        apply = c=>{ player.minions.perCast = Math.min(MINION_PERCAST_MAX, player.minions.perCast + c.n); } },
      /* SIX GOLEMS, each once, and only as many as you may keep */
      ..GOLEM_KEYS.map(gt=>(new Upgrade { id = "golem"+gt, name = GOLEM_TYPES[gt].name, icon = "skull", cls = "OFFENSE", minRar = "rare", golemType = gt,
        req = ()=>isSummoner() && !golemTypesOwned().includes(gt) && golemTypesOwned().Count() < golemCap(),
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{ var T = GOLEM_TYPES[gt];
          return $"A {S(T.name.toUpperCase())} rises at your side and stays. While it stands you have "+
            $"{S(T.says)}. It {S(T.fights)}. You may keep {S(golemCap())} golems, never two alike "+
            $"({S(golemTypesOwned().Count())}/{S(golemCap())} now) — choose; one that falls rises again {S(GOLEM_BACK)}s later"; },
        apply = c=>{ player.minions.golemTypes.push(gt); if(golems().Count() < golemMax()) raiseGolem(gt); } })),
      /* the overseer, only ever epic */
      new Upgrade { id = "raisezombie", name = "Raise Zombie", icon = "skull", cls = "OFFENSE", minRar = "epic",
        req = ()=>isSummoner() && !player.minions.zombies,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your summon aura also RAISES ZOMBIES out of the corpses zombies leave behind -- the one "+
          $"nearest your cursor, within {S(ZOMBIE_REACH)}px of you -- up to {S(ZOMBIE_MINION_MAX)}, a pool of their own "+
          $"beside the legion. Tough and slow, with {S(kindOf("zombie").hp)}\u00d7 a skeleton's life",
        apply = c=>{ player.minions.zombies = true; } },
      /* THE HORDE: only for a summoner who already raises zombies */
      new Upgrade { id = "zombiehorde", name = "The Horde", icon = "skull", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isSummoner() && player.minions.zombies && !player.minions.horde,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your raised dead are a HORDE: the zombie cap is {S(ZOMBIE_HORDE_MUL)}× ({S(ZOMBIE_MINION_MAX*ZOMBIE_HORDE_MUL)}), and what "+
          $"rises is any of the family -- walkers, fast RUNNERS, low CRAWLERS, fat BLOATERS and heavy HULKS -- each "+
          $"fighting the way it does on the other side",
        apply = c=>{ player.minions.horde = true; } },
      new Upgrade { id = "boneoverseer", name = "Bone Overseer", icon = "skull", cls = "OFFENSE", minRar = "epic",
        req = ()=>isSummoner() && !player.minions.overseer,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"A BONE OVERSEER rises at your shoulder and stays there. It does not fight; every minion "+
          $"within {S(OVERSEER_R)}px of it deals {S(Math.round((OVERSEER_MORE-1)*100))}% MORE damage and attacks "+
          $"{S(Math.round((OVERSEER_SPD-1)*100))}% faster. It is not one of the legion, and if it falls it rises "+
          $"again {S(OVERSEER_BACK)}s later",
        apply = c=>{ player.minions.overseer = true; player.minions.overseerCd = 0; } },
      /* THE VOLLEY: the legion's multiple projectiles, card by card and then the
         legendary support that makes a fan of it */
      new Upgrade { id = "bonevolley", name = "Bone Volley", icon = "split", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>isSummoner() && player.minions.volleys < MINION_VOLLEY_MAX,
        calc = m=>(new Calc {n = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>{ var add = Math.min(c.n, MINION_VOLLEY_MAX - player.minions.volleys);
          return $"Your skeleton ARCHERS and MAGES loose +{S(add)} PROJECTILE{S(add > 1 ? "S" : "")} a shot ({S(minionProj())} → "+
            $"{S(Math.min(1 + MINION_PROJ_CAP, minionProj() + add))}), fanned out beside the first. "+
            $"Bone Volley adds {S(MINION_VOLLEY_MAX)} at most ({S(Js.Or(player.minions.volleys, 0))} so far)"; },
        apply = c=>{ var M = player.minions; double add = Math.min(c.n, MINION_VOLLEY_MAX - M.volleys);
          M.volleys = M.volleys + add; M.proj = M.proj + add; } },
      new Upgrade { id = "bonegmp", name = "Greater Multiple Projectiles", icon = "split", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isSummoner() && !player.minions.gmp,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your skeleton archers and mages loose +{S(MINION_GMP_N)} PROJECTILES a shot "+
          $"({S(minionProj())} → {S(Math.min(1 + MINION_PROJ_CAP, minionProj() + MINION_GMP_N))}) in a wide fan, "+
          $"for {S(Math.round(MINION_GMP_LESS*100))}% LESS projectile damage. A body close enough takes several",
        apply = c=>{ var M = player.minions; M.gmp = true; M.proj = M.proj + MINION_GMP_N;
          M.projMore = (Js.Or(M.projMore, 1)) * (1 - MINION_GMP_LESS); } },
      new Upgrade { id = "skelmages", name = "Skeleton Mages", icon = "skull", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isSummoner() && !player.minions.mages,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"EVERY SKELETON BECOMES A MAGE — the ones standing and every one you raise after. "+
          $"A mage stands off out of reach and throws fast bolts of fire, cold or lightning, a third of "+
          $"the legion each, for {S(Math.round(MAGE_SHARE*100))}% of a blade's hit; each bolt is its element "+
          $"and leaves its element's ailment",
        apply = c=>{ player.minions.mages = true; minionsToMages(); } },
      new Upgrade { id = "arrowmulti", name = "Split Arrow", icon = "bow", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>isArcher() && player.bow.n < ARROW_MAX_N,
        calc = m=>(new Calc {n = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>{ var to = Math.min(ARROW_MAX_N, player.bow.n + c.n);
          return $"+{S(to - player.bow.n)} ARROW{S(to - player.bow.n > 1 ? "S" : "")} every shot ({S(player.bow.n)} \u2192 {S(to)}, cap {S(ARROW_MAX_N)}), "+
            $"fanned out beside the first. Every arrow is the whole arrow, not a share of one"; },
        apply = c=>{ player.bow.n = Math.min(ARROW_MAX_N, player.bow.n + c.n); } },
      new Upgrade { id = "arrowpierce", name = "Piercing Shot", icon = "bow", cls = "OFFENSE",
        req = ()=>isArcher() && player.bow.kind != "chaos" && player.bow.pierce < ARROW_MAX_PIERCE,
        calc = m=>(new Calc {p = byRar(m, [1, 2, 3, 4, 5])}),
        desc = (c, _owned)=>{
          double now = player.bow.pierce, next = Math.min(ARROW_MAX_PIERCE, now + c.p);
          return $"+{S(next-now)} pierce (cap {S(ARROW_MAX_PIERCE)}) \u2014 an arrow goes on through {S(next)} "+
            $"bod{S(next==1?"y":"ies")} after the first, {S(Math.round(pierceFall(next)*100))}% less for each one it has passed";
        },
        apply = c=>{ player.bow.pierce = Math.min(ARROW_MAX_PIERCE, player.bow.pierce + c.p); } },
      new Upgrade { id = "arrowflat", name = "Barbed Arrows", icon = "bow", cls = "OFFENSE",
        req = ()=>isArcher(),
        /* the Sharpened Blade's numbers and half a point more: it only ever feeds
           arrows, so it has to beat the brick that feeds everything */
        calc = m=>(new Calc {v = byRar(m, [0.5, 1, 3, 5, 8]) + BARBED_OVER}),
        desc = (c, _owned)=>$"+{S(c.v)} FLAT physical damage to every arrow \u2014 before any increase, so every "+
          $"increase multiplies it (an arrow is {S(fmtHp(arrowDamage()))} now)",
        apply = c=>{ player.bow.flat = Math.round((player.bow.flat + c.v)*100)/100.0; } },
      /* ONE OF THREE. Taking any closes the other two: the arrow becomes one thing */
      new Upgrade { id = "arrowlight", name = "Lightning Arrow", icon = "chain", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isArcher() && player.bow.kind == null,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your arrows become LIGHTNING: {S(Math.round(ARROW_KINDS["light"].conv*100))}% of their physical "+
          $"damage is converted to lightning, and every arrow that lands FORKS \u2014 a bolt leaps from the "+
          $"body it hit to up to {S(ARROW_FORK_N)} others within {S(ARROW_FORK_R)}px, each for "+
          $"{S(Math.round(ARROW_FORK_SHARE*100))}% of the arrow, once and never onward. "+
          $"+{S(Math.round(ARROW_KINDS["light"].ail*100))}% chance to SHOCK with arrows. "+
          $"One arrow legendary a run: this closes Fire Arrow and Chaos Arrow",
        apply = c=>{ player.bow.kind = "light"; } },
      new Upgrade { id = "arrowfire", name = "Fire Arrow", icon = "flame", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isArcher() && player.bow.kind == null,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your arrows become FIRE: {S(Math.round(ARROW_KINDS["fire"].conv*100))}% of their physical damage "+
          $"is converted to fire, and every arrow BURSTS where it lands \u2014 the body it hit takes the whole "+
          $"arrow and everything within {S(ARROW_BLAST_R)}px takes {S(Math.round(ARROW_BLAST_LESS*100))}% less. "+
          $"+{S(Math.round(ARROW_KINDS["fire"].ail*100))}% chance to IGNITE with arrows. "+
          $"One arrow legendary a run: this closes Lightning Arrow and Chaos Arrow",
        apply = c=>{ player.bow.kind = "fire"; } },
      new Upgrade { id = "arrowchaos", name = "Chaos Arrow", icon = "bane", cls = "OFFENSE", minRar = "legendary",
        req = ()=>isArcher() && player.bow.kind == null,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your arrows become CHAOS: {S(Math.round(ARROW_KINDS["chaos"].conv*100))}% of their physical damage "+
          $"is converted to chaos, and every arrow PIERCES EVERYTHING in its line, unworn. "+
          $"+{S(Math.round(ARROW_KINDS["chaos"].ail*100))}% chance to POISON with arrows. "+
          $"One arrow legendary a run: this closes Lightning Arrow and Fire Arrow",
        apply = c=>{ player.bow.kind = "chaos"; } },
      new Upgrade { id = "orbitaegis", name = "Aegis Ring", icon = "ward", cls = "SPELL", minRar = "legendary",
        req = ()=>player.orbit.n>0 && !Js.T(player.orbit.aegis),
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Your guardian bricks become a wall. Every brick in the ring STOPS one hostile "+
          $"projectile that crosses it — arrows, bolts, skulls, hexes, a thrown boulder — and "+
          $"breaks doing it, rebuilding itself {S(AEGIS_REFORM)}s later. More bricks, a thicker wall; "+
          $"a faster spin, more of the circle covered",
        apply = c=>{ player.orbit.aegis = 1; } },
      new Upgrade { id = "orbittwin", name = "Twin Ring", icon = "orbit", cls = "SPELL", minRar = "legendary",
        req = ()=>player.orbit.n>0 && Js.T(player.orbit.aegis) && !Js.T(player.orbit.twin),
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"A SECOND ring of {S(player.orbit.n)} guardian brick{S(player.orbit.n>1?"s":"")} turns the other "+
          $"way, out at {S(Math.round(twinRadius(player.orbit)))} — clear of the first — and it blocks "+
          $"shots too. Its bricks strike on their own clock, so a body caught across both rings "+
          $"is hit by both",
        apply = c=>{ player.orbit.twin = 1; } },
      new Upgrade { id = "freezestun", name = "Absolute Zero", icon = "icecube", cls = "SPELL", minRar = "legendary",
        req = ()=>isCaster() && player.freeze.n>0 && !Js.T(player.freeze.stun),
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>$"Block Freeze no longer just slows — it freezes every caught foe SOLID "+
          $"for {S(FREEZE_STUN_T.toFixed(1))}s and leaves it BRITTLE. And a body that dies "+
          $"frozen SHATTERS: the corpse throws your own pulse, "+
          $"{S(fmtHp(zeroShatterDmg(1)))} frost in {S(Math.round(zeroShatterR()))}px, "+
          $"+{S(Math.round(ZERO_BRITTLE_MUL*100))}% for every BRITTLE mark it died with "+
          $"({S(fmtHp(zeroShatterDmg(FROZEN_MAX)))} at {S(FROZEN_MAX)})",
        apply = c=>{ player.freeze.stun = 1; } },
      new Upgrade { id = "morehp", name = "Titanium Chassis", icon = "heart", cls = "DEFENSE", minRar = "legendary",
        req = ()=>(Js.Or(player.hpMore, 1)) < 4,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var now = (Js.Or(player.hpMore, 1));
          return $"100% MORE maximum hearts"+
            (now>1 ? " — ×4 in total" : " — and it can be found once more, for ×4");
        },
        apply = c=>{ var p = player;
          var before = p.maxHp;
          p.hpMore = (Js.Or(p.hpMore, 1)) * 2;     /* stored — it keeps multiplying what comes later */
          syncStats();
          /* you keep the same share of a bigger bar */
          p.hp = Math.min(p.maxHp, Math.round(p.hp * (p.maxHp/Math.max(1,before))*100)/100.0); } },
      new Upgrade { id = "chainfork", name = "Crackling Storm", icon = "fork", cls = "SPELL", minRar = "legendary",
        req = ()=>player.chain.n>0 && player.chain.fork < CHAIN_FORK_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var ways = 2 + player.chain.fork;
          return $"Every leap FORKS {S(ways)} ways instead of one — a bolt walks the pack as "+
            $"a branching tree (1 → {S(ways)} → {S(ways*ways)} → …). Each fork carries a "+
            $"{S(ways)} way split of the damage.";
        },
        apply = c=>{ player.chain.fork = Math.min(CHAIN_FORK_MAX, player.chain.fork + 1); } },
      new Upgrade { id = "blademaster", name = "Blademaster", icon = "spin", cls = "OFFENSE", minRar = "legendary",
        /* THE WHIRL IS THE SPIN HELD DOWN, so a weapon with no spin in its combo
           has nothing to hold. A staff does not swing, a mace has traded its swing
           for the ground slam, and the AXE swings twice — a chop and a backswing —
           which is its own rhythm, with its own legendary to go with it. Asked of
           the combo table, so a fifth weapon opts in by having a spin. */
        req = ()=>isMelee() && !isSlammer() && comboSteps().indexOf("spin") >= 0
                && player.blademaster < WHIRL_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=> (player.blademaster==0
          ? $"HOLD the swing button and you whirl without stopping, cutting everything in "+
            $"reach for {S(Math.round(WHIRL_MUL*100))}% of a swing every {S(WHIRL_TICK.toFixed(2))}s. "+
            $"You walk at full speed while it is up and the blade turns aside "+
            $"{S(Math.round(WHIRL_DEFLECT[1]*100))}% of what is thrown at you — but you cannot roll."
          : $"The whirl carries you: {S(Math.round(WHIRL_SLOW[2]*100))}% move speed while spinning, "+
            $"and {S(Math.round(WHIRL_DEFLECT[2]*100))}% of incoming missiles are batted away. Still no rolling.")
          + (((Func<double, string>)(r=>$" And always: +{S(Math.round(BM_SPEED[r]*100))}% move speed, +{S(BM_REACH[r])} reach "+
            $"and +{S(Math.round(BM_ATK[r]*100))}% attack speed — your blade grows with the reach"))(Math.min(WHIRL_MAX, player.blademaster+1))),
        apply = c=>{
          var p = player;
          if(p.blademaster >= WHIRL_MAX) return;
          p.blademaster++;
          var r = p.blademaster;
          p.inc["speed"] += BM_SPEED[r]; p.reachFlat += BM_REACH[r]; p.inc["atk"] += BM_ATK[r];
          syncStats();
        } },
      /* RAGE, AND THE BRICK THAT TURNS IT ON. Nothing generates rage by default —
         the ceiling is thirty and the generation is nothing — so this is the card
         that makes it a build at all, and the tree deepens it afterwards. */
      new Upgrade { id = "ragehit", name = "Warpath", icon = "fist", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>isMelee() && player.rageOnHit < stackCap("rage"),
        calc = m=>(new Calc {v = byRar(m, [0.5, 0.8, 1.2, 1.8, 2.6])}),
        desc = (c, _owned)=>$"Your swings build RAGE: +{S(c.v.toFixed(1))} a hit (now "+
          $"{S(player.rageOnHit.toFixed(1))}). Every point of rage is "+
          $"+{S(Math.round(HERO_STACKS["rage"].mods["moreDmg"]*100))}% MORE attack damage, to a ceiling of "+
          $"{S(stackCap("rage"))} — and it pours away at {S(HERO_STACKS["rage"].rate)} a second once you "+
          $"have gone {S(HERO_STACKS["rage"].grace)}s without landing or taking one",
        apply = c=>{ player.rageOnHit += c.v; } },
      new Upgrade { id = "ragehurt", name = "Spite", icon = "shield", cls = "DEFENSE", minRar = "uncommon",
        req = ()=>isMelee() && player.rageOnHurt < stackCap("rage"),
        calc = m=>(new Calc {v = byRar(m, [0.6, 1.0, 1.5, 2.2, 3.2])}),
        desc = (c, _owned)=>$"Being hit builds RAGE: +{S(c.v.toFixed(1))} a hit taken (now "+
          $"{S(player.rageOnHurt.toFixed(1))}) — what is done to you is worth "+
          $"{S(Math.round(HERO_STACKS["rage"].mods["moreDmg"]*100))}% MORE attack damage a point",
        apply = c=>{ player.rageOnHurt += c.v; } },
      new Upgrade { id = "ragemax", name = "Deep Well", icon = "force", cls = "OFFENSE", minRar = "rare",
        req = ()=>(player.rageOnHit > 0 || player.rageOnHurt > 0) && stackCap("rage") < RAGE_CAP_MAX,
        calc = m=>(new Calc {v = byRar(m, [4, 6, 8, 12, 16])}),
        desc = (c, _owned)=>$"+{S(c.v)} maximum RAGE (now {S(stackCap("rage"))}) — a deeper well to fill, "+
          $"and {S(Math.round(c.v*HERO_STACKS["rage"].mods["moreDmg"]*100))}% MORE attack damage at the top of it",
        apply = c=>{ player.stackMax["rage"] += c.v; } },
      /* ---- THE THREE CHARGES, AND THE THREE THINGS THAT EARN THEM. Same shape as
         the rage bricks above and for the same reason: nobody generates a charge
         until something hands them the reason to, so these are the cards that turn
         a charge into a build and the tree is what deepens it. ---- */
      new Upgrade { id = "chgkill", name = "Blood Rush", icon = "sword", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>stackAllows("frenzy") && chargeGenRoom("frenzy", player.chargeKill),
        calc = m=>(new Calc {v = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>$"Killing a monster grants {S(c.v)} FRENZY charge{S(c.v>1?"s":"")} (now "+
          $"{S(player.chargeKill+c.v)} a kill, {S(stackCap("frenzy"))} at once). Each is "+
          $"+{S(Math.round(HERO_STACKS["frenzy"].mods["atk"]*100))}% attack AND cast speed and "+
          $"+{S(Math.round(HERO_STACKS["frenzy"].mods["moreAll"]*100))}% MORE damage, falling off one "+
          $"every {S(HERO_STACKS["frenzy"].decay)}s",
        apply = c=>{ player.chargeKill += c.v; } },
      new Upgrade { id = "chgcrit", name = "Sharpened Focus", icon = "target", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>stackAllows("power") && chargeGenRoom("power", player.chargeCrit),
        calc = m=>(new Calc {v = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>$"A CRITICAL grants {S(c.v)} POWER charge{S(c.v>1?"s":"")} (now "+
          $"{S(player.chargeCrit+c.v)} a crit, {S(stackCap("power"))} at once). Each is "+
          $"+{S(Math.round(HERO_STACKS["power"].mods["crit"]*100))}% INCREASED critical chance, so a "+
          $"critical makes the next one likelier — it pools with every other increase you own",
        apply = c=>{ player.chargeCrit += c.v; } },
      new Upgrade { id = "chghurt", name = "Set Jaw", icon = "shield", cls = "DEFENSE", minRar = "uncommon",
        req = ()=>chargeGenRoom("endurance", player.chargeHurt),
        calc = m=>(new Calc {v = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>$"Being hit grants {S(c.v)} ENDURANCE charge{S(c.v>1?"s":"")} (now "+
          $"{S(player.chargeHurt+c.v)} a hit taken, {S(stackCap("endurance"))} at once). Each is "+
          $"{S(Math.round(HERO_STACKS["endurance"].mods["less"]*100))}% LESS damage taken and "+
          $"+{S(Math.round(HERO_STACKS["endurance"].mods["res"]*100))}% to every elemental resistance",
        apply = c=>{ player.chargeHurt += c.v; } },
      new Upgrade { id = "chgmax", name = "Reservoir", icon = "force", cls = "OFFENSE", minRar = "rare",
        /* only for a charge this hero can carry AND is earning */
        req = ()=>new[] { ("frenzy","chargeKill"), ("power","chargeCrit"), ("endurance","chargeHurt") }
                .some(kg => Fields.Num(player, kg.Item2) > 0 && stackAllows(kg.Item1) && stackCap(kg.Item1) < CHARGE_CAP_MAX),
        calc = m=>(new Calc {v = byRar(m, [1, 1, 2, 3, 4])}),
        desc = (c, _owned)=>$"+{S(c.v)} maximum of EVERY charge — FRENZY to {S(stackCap("frenzy")+c.v)}, "+
          $"POWER to {S(stackCap("power")+c.v)}, ENDURANCE to {S(stackCap("endurance")+c.v)}. "+
          $"A deeper well for whichever of them you are actually filling",
        apply = c=>{ foreach (var k in CHARGE_IDS) player.stackMax[k] += Math.max(0, Math.min(c.v, CHARGE_CAP_MAX - stackCap(k))); } },
      /* ---- ACCURACY AND EVASION. Two sides of one contest, and two very
         different cards: accuracy is a thing a melee hero eventually NEEDS, so it
         is common and cheap; evasion is a thing nobody has any of, so the first one
         you take is the whole of it. ---- */
      new Upgrade { id = "acc", name = "True Edge", icon = "target", cls = "OFFENSE",
        req = ()=>!isCaster() && !isSummoner(),
        calc = m=>(new Calc {v = byRar(m, [40, 70, 110, 170, 260])}),
        desc = (c, _owned)=>{
          var was = accuracyValue();
          player.accFlat += c.v; var now = accuracyValue(); player.accFlat -= c.v;
          return $"+{S(c.v)} ACCURACY ({S(Math.round(was))} → {S(Math.round(now))}). Only an ATTACK "+
            $"has to find its target — a spell arrives where it was aimed. Against a body "+
            $"that dodges hard you land {S(Math.round(hitChance(was, 600)*100))}% of your swings; "+
            $"with this, {S(Math.round(hitChance(now, 600)*100))}%";
        },
        apply = c=>{ player.accFlat += c.v; } },
      new Upgrade { id = "accinc", name = "Practised Hand", icon = "target", cls = "OFFENSE", minRar = "rare",
        req = ()=>!isCaster() && !isSummoner() && player.accFlat > 0,
        calc = m=>(new Calc {v = byRar(m, [0.10, 0.16, 0.24, 0.36, 0.50])}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased ACCURACY — on the whole pool, "+
          $"including your dexterity and the weapon's own (now {S(Math.round(accuracyValue()))})",
        apply = c=>{ player.inc["acc"] += c.v; } },
      /* ---- THE SHIELD. Two cards, the same shape as accuracy and evasion: a flat
         one that anybody can take and an increase that is only worth anything once
         you have a pool to increase. ---- */
      new Upgrade { id = "es", name = "Ward Brick", icon = "ward", cls = "DEFENSE",
        calc = m=>(new Calc {v = byRar(m, [2, 3.5, 5.5, 8, 12])}),
        desc = (c, _owned)=>{
          var was = player.esMax;
          player.esFlat += c.v; syncStats();
          var now = player.esMax; player.esFlat -= c.v; syncStats();
          return $"+{S(c.v)} ENERGY SHIELD ({S(fmtHp(was))} → {S(fmtHp(now))}). A second bar in "+
            $"front of your hearts: it takes the hit first and comes back on its own "+
            $"{S(ES_RATE*100)}% a second after {S(ES_DELAY)}s untouched. CHAOS goes straight "+
            $"through it — a dose is the one thing a shield cannot stop";
        },
        apply = c=>{ player.esFlat += c.v; syncStats(); } },
      new Upgrade { id = "esinc", name = "Deep Ward", icon = "ward", cls = "DEFENSE", minRar = "rare",
        req = ()=>player.esFlat > 0,
        calc = m=>(new Calc {v = byRar(m, [0.12, 0.20, 0.30, 0.45, 0.65])}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased ENERGY SHIELD — and your intelligence "+
          $"increases it too, {S(Math.round(intOf()*ATTR_INT_ES*100))}% of it so far "+
          $"(now {S(fmtHp(player.esMax))})",
        apply = c=>{ player.inc["es"] += c.v; syncStats(); } },
      new Upgrade { id = "eva", name = "Loose Footing", icon = "boot", cls = "DEFENSE",
        calc = m=>(new Calc {v = byRar(m, [60, 110, 180, 280, 420])}),
        desc = (c, _owned)=>{
          var was = evasionValue();
          player.evaFlat += c.v; var now = evasionValue(); player.evaFlat -= c.v;
          return $"+{S(c.v)} EVASION ({S(Math.round(was))} → {S(Math.round(now))}). An ordinary "+
            $"monster's swing lands {S(Math.round(hitChance(ACC_MOB_BASE, was)*100))}% of the time; "+
            $"with this, {S(Math.round(hitChance(ACC_MOB_BASE, now)*100))}%. It does nothing at all "+
            $"against a SPELL — armour and suppression are what stand in front of those";
        },
        apply = c=>{ player.evaFlat += c.v; } },
      new Upgrade { id = "evainc", name = "Light on the Feet", icon = "boot", cls = "DEFENSE", minRar = "rare",
        req = ()=>player.evaFlat > 0,
        calc = m=>(new Calc {v = byRar(m, [0.12, 0.20, 0.30, 0.45, 0.65])}),
        desc = (c, _owned)=>$"{S(Math.round(c.v*100))}% increased EVASION — and your dexterity increases it "+
          $"too, {S(Math.round(dexOf()*ATTR_DEX_EVA*100))}% of it so far (now {S(Math.round(evasionValue()))})",
        apply = c=>{ player.inc["eva"] += c.v; } },
      /* ---- EXPOSURE. The one damage increase that is not yours: it comes off the
         BODY, so it sits outside your own increased pool and outside the MORE
         ceiling with it — which is what makes it worth a card to a build that has
         already bought everything there is to buy. ---- */
      new Upgrade { id = "expose", name = "Solvent", icon = "prismdn", cls = "OFFENSE", minRar = "uncommon",
        req = ()=>EXPOSE_ON && player.exposeChance < EXPOSE_CHANCE_MAX && (canDeal("fire") || canDeal("frost")
                || canDeal("shock") || canDeal("chaos")),
        calc = m=>(new Calc {v = byRar(m, [0.10, 0.16, 0.24, 0.34, 0.48])}),
        desc = (c, _owned)=>$"+{S(Math.round(c.v*100))}% chance for a hit to EXPOSE the element it arrived as: "+
          $"−{S(Math.round(EXPOSE_STEP*100))}% of that resistance for {S(EXPOSE_T)}s (now "+
          $"{S(Math.round(player.exposeChance*100))}%). Resistance taken OFF the monster — it "+
          $"is not in your damage pool, so it is not stopped by your ceiling",
        apply = c=>{ player.exposeChanceFlat += c.v; syncStats(); } },
      /* THE AXE'S OWN. Every other weapon had a legendary that changed what it IS
         — the sword whirls, the mace slams the elements, the staff scatters embers
         — and the axe borrowed the sword's. */
      new Upgrade { id = "reave", name = "Reaving", icon = "boomerang", cls = "OFFENSE", minRar = "legendary",
        req = ()=>weapon.id == "axe" && player.reave < REAVE_MAX,
        calc = c=>(new Calc {n = 1}),
        desc = (c, _owned)=> player.reave == 0
          ? $"Every CRITICAL hurls a spinning axe. It reaps through up to {S(REAVE_BOUNCES)} more "+
            $"bodies within {S(REAVE_SEEK)}px — each for {S(Math.round(REAVE_MUL*100))}% of the "+
            $"critical that threw it, each able to crit on its own. You KEEP swinging, and up to "+
            $"{S(REAVE_AIR)} can be in the air at once."
          : $"The blade leaves a BLEEDING body for free: reaping one costs it none of its "+
            $"{S(REAVE_BOUNCES)} bounces, so a wound is a longer harvest — and the axe's own "+
            $"{S(Math.round(WEAPONS["axe"].bleed*100))}% bleed becomes something worth building. "+
            $"{S(REAVE_AIR_R2)} axes may fly at once.",
        apply = c=>{ player.reave = Math.min(REAVE_MAX, player.reave + 1); } },
      new Upgrade { id = "vortextotem", name = "Lingering Vortex", icon = "totem", cls = "SPELL", minRar = "legendary",
        req = ()=>isMelee() && player.vortex.n>0 && player.vortex.totem < TOTEM_MAX,
        calc = m=>(new Calc {n = 1}),
        desc = (c, _owned)=>{
          var lv = player.vortex.totem + 1;
          return $"Every {S(TOTEM_EVERY[lv])}s you leave a whirlwind standing where you are. It "+
            $"does not follow you — it stays for {S(TOTEM_LIFE[lv])}s, shredding anything that "+
            $"walks in for {S(Math.round(TOTEM_MUL[lv]*100))}% of your vortex damage.";
        },
        apply = c=>{ player.vortex.totem = Math.min(TOTEM_MAX, player.vortex.totem + 1);
          player.vortex.totemCd = 0; } }
    ];
}
