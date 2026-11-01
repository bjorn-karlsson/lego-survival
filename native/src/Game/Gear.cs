using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Brickblade;

/* =============================== GEAR (JS 7201–7863) ===============================
   THINGS YOU WEAR, and the one part of a run that survives it. Path of Exile's
   item model, kept small: a BASE (slot, footprint, defence or damage, implicit),
   a RARITY (how many affixes), and AFFIXES with TIERS gated by the item level. */
public sealed class GearCls { public string name, slot, weap; public int w, h; }
public sealed class GearBase
{
    public string id, cls, name, col; public double lvl, dmg;
    public NumMap def;                                     /* {armour|eva|es: n}, or null for a weapon or a trinket */
    public List<(string stat, double lo, double hi)> imp;  /* null: no implicit */
}
public sealed class GearTier { public double lvl, min, max; public string name; }
public sealed class GearAffix { public string id, kind, stat, need; public string[] cls; public double w; public List<GearTier> tiers; }
public sealed class GearRar { public string name, col, bg; public int min, max; }
/* one item: base, rarity, item level, implicits, affixes [id, tier index, value], a rare's name */
public sealed class GearItem
{
    public int u; public string b, r; public int il;
    public List<(string stat, double v)> imp = new();
    public List<(string id, int ti, double v)> m = new();
    public string n;
}
public sealed class GearEntry { public GearItem it; public int x, y; }
public sealed class GearState
{
    public int v = 3;
    public Dictionary<string, Dictionary<string, GearItem>> eqBy = new();
    public Dictionary<string, GearItem> eq;
    public List<GearEntry> inv = new(), stash = new();
    public int uid = 1;
}
public sealed class GearDim { public int cols, rows; public bool rowMajor; }
public sealed class GearDrop : Obj { public GearItem it; public double z, vz, t; public bool got; }
/* a stat a line can write: META_STATS (the tree) or one of the two gear adds */
public sealed partial class MetaStat { public string n; public Action<Ent, double> w; }

public static partial class G
{
    public const string GEAR_KEY = "bb_gear";
    public const int GEAR_COLS = 12, GEAR_ROWS = 5;
    /* THE STASH: twelve wide and 256 deep, and it SCROLLS */
    public const int GEAR_STASH_COLS = 12, GEAR_STASH_ROWS = 256, GEAR_STASH_VIEW = 12;
    public static readonly string[] GEAR_SLOTS = { "weapon", "helmet", "offhand", "body", "amulet", "ring1", "ring2", "gloves", "belt", "boots" };
    public static readonly Dictionary<string, string> GEAR_SLOT_NAME = new()
    {
        ["weapon"] = "WEAPON", ["offhand"] = "OFF HAND", ["helmet"] = "HELMET", ["body"] = "BODY ARMOUR",
        ["gloves"] = "GLOVES", ["boots"] = "BOOTS", ["belt"] = "BELT", ["amulet"] = "AMULET", ["ring1"] = "RING", ["ring2"] = "RING",
    };
    /* every class of item: the slot that takes it and how much of the bag it fills */
    public static readonly Dictionary<string, GearCls> GEAR_CLS = new()
    {
        ["helmet"] = new GearCls { name = "Helmet", slot = "helmet", w = 2, h = 2 },
        ["body"] = new GearCls { name = "Body Armour", slot = "body", w = 2, h = 3 },
        ["gloves"] = new GearCls { name = "Gloves", slot = "gloves", w = 2, h = 2 },
        ["boots"] = new GearCls { name = "Boots", slot = "boots", w = 2, h = 2 },
        ["shield"] = new GearCls { name = "Shield", slot = "offhand", w = 2, h = 3 },
        ["quiver"] = new GearCls { name = "Quiver", slot = "offhand", w = 2, h = 3 },
        ["belt"] = new GearCls { name = "Belt", slot = "belt", w = 2, h = 1 },
        ["amulet"] = new GearCls { name = "Amulet", slot = "amulet", w = 1, h = 1 },
        ["ring"] = new GearCls { name = "Ring", slot = "ring", w = 1, h = 1 },
        ["sword"] = new GearCls { name = "Sword", slot = "weapon", w = 1, h = 3, weap = "sword" },
        ["axe"] = new GearCls { name = "Axe", slot = "weapon", w = 2, h = 3, weap = "axe" },
        ["mace"] = new GearCls { name = "Mace", slot = "weapon", w = 2, h = 3, weap = "mace" },
        ["scepter"] = new GearCls { name = "Sceptre", slot = "weapon", w = 1, h = 3, weap = "scepter" },
        ["staff"] = new GearCls { name = "Staff", slot = "weapon", w = 2, h = 4, weap = "staff" },
        ["bow"] = new GearCls { name = "Bow", slot = "weapon", w = 2, h = 4, weap = "bow" },
    };
    /* WHAT HOLDS WHAT: a staff and a bow take both hands */
    public static readonly string[] GEAR_ONE_HAND = { "sword", "axe", "mace", "scepter" };
    /* ---- THE BASES ---- */
    public static readonly List<GearBase> GEAR_BASES = new();
    public const double GEAR_BASE_LVL_K = 0.6;
    static readonly bool _gearBases = BuildGearBases();
    static (string, double, double)[] IMP(string s, double lo, double hi) => new[] { (s, lo, hi) };
    static bool BuildGearBases()
    {
        /* the levels below were written on a wave scale; the item level is the MONSTER level */
        void B(string id, string cls, string name, double lvl, NumMap stat, (string, double, double)[] imp, string col)
        {
            bool isDmg = stat != null && stat.Has("dmg");
            GEAR_BASES.Add(new GearBase
            {
                id = id, cls = cls, name = name, lvl = Math.Max(1, Math.Round(lvl * GEAR_BASE_LVL_K)),
                def = stat != null && !isDmg ? stat : null, dmg = isDmg ? stat["dmg"] : 0,
                imp = imp?.ToList(), col = col
            });
        }
        /* armour pieces come in the three PoE flavours: armour, evasion, energy shield */
        var ARMOUR_SETS = new (string cls, (double lvl, string a, string e, string s, double av, double ev, double sv)[] rows)[]
        {
            ("helmet", new[] { (1.0, "Iron Hat", "Leather Cap", "Vine Circlet", 8.0, 40.0, 2.0),
                               (14, "Cone Helmet", "Tricorne", "Iron Circlet", 18, 90, 4),
                               (30, "Barbute Helmet", "Lion Pelt", "Necromancer Circlet", 30, 150, 6),
                               (48, "Royal Burgonet", "Sinner Tricorne", "Hubris Circlet", 44, 220, 9) }),
            ("body", new[] { (1.0, "Plate Vest", "Shabby Jerkin", "Simple Robe", 16.0, 80.0, 4.0),
                             (14, "Copper Plate", "Strapped Leather", "Silken Vest", 36, 180, 8),
                             (30, "Colosseum Plate", "Buckskin Tunic", "Scholar’s Robe", 60, 300, 12),
                             (48, "Astral Plate", "Assassin’s Garb", "Vaal Regalia", 88, 440, 18) }),
            ("gloves", new[] { (1.0, "Iron Gauntlets", "Rawhide Gloves", "Wool Gloves", 6.0, 30.0, 2.0),
                               (14, "Bronze Gauntlets", "Goathide Gloves", "Velvet Gloves", 14, 70, 3),
                               (30, "Steel Gauntlets", "Deerskin Gloves", "Silk Gloves", 24, 120, 5),
                               (48, "Titan Gauntlets", "Slink Gloves", "Sorcerer Gloves", 36, 180, 7) }),
            ("boots", new[] { (1.0, "Iron Greaves", "Rawhide Boots", "Wool Shoes", 6.0, 30.0, 2.0),
                              (14, "Steel Greaves", "Goathide Boots", "Velvet Slippers", 14, 70, 3),
                              (30, "Antique Greaves", "Deerskin Boots", "Silk Slippers", 24, 120, 5),
                              (48, "Titan Greaves", "Slink Boots", "Sorcerer Boots", 36, 180, 7) }),
            ("shield", new[] { (1.0, "Splintered Tower Shield", "Goathide Buckler", "Twig Spirit Shield", 12.0, 60.0, 3.0),
                               (20, "Corroded Tower Shield", "Pine Buckler", "Ivory Spirit Shield", 28, 140, 6),
                               (40, "Ezomyte Tower Shield", "Crusader Buckler", "Titanium Spirit Shield", 50, 250, 10) }),
        };
        string tag(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z]+", "_");
        foreach (var (cls, rows) in ARMOUR_SETS)
            for (int i = 0; i < rows.Length; i++)
            {
                var (lvl, a, e, s, av, ev, sv) = rows[i];
                /* the late body armours carry PoE's own implicits */
                bool top = cls == "body" && i == 3;
                B(tag(a), cls, a, lvl, new NumMap(("armour", av)), top ? IMP("allRes", 0.06, 0.10) : null, "#a9b3bc");
                B(tag(e), cls, e, lvl, new NumMap(("eva", ev)),
                    top ? IMP("speed", 0.03, 0.05) : cls == "shield" ? IMP("suppress", 0.03 + 0.01 * i, 0.05 + 0.01 * i) : null, "#b07a45");
                B(tag(s), cls, s, lvl, new NumMap(("es", sv)), cls == "shield" ? IMP("incSpell", 0.05 + 0.04 * i, 0.08 + 0.04 * i) : null, "#9a84e0");
            }
        /* quivers: a bow's second hand */
        B("serrated_quiver", "quiver", "Serrated Arrow Quiver", 1, null, IMP("arrowFlat", 0.2, 0.4), "#8a6a3a");
        B("two_point_quiver", "quiver", "Two-Point Arrow Quiver", 1, null, IMP("acc", 40, 60), "#8a6a3a");
        B("broadhead_quiver", "quiver", "Broadhead Arrow Quiver", 25, null, IMP("incMelee", 0.10, 0.15), "#8a6a3a");
        B("spike_quiver", "quiver", "Spike-Point Arrow Quiver", 45, null, IMP("incCrit", 0.20, 0.30), "#8a6a3a");
        /* belts */
        B("rustic_sash", "belt", "Rustic Sash", 1, null, IMP("incMelee", 0.08, 0.12), "#9a7a4e");
        B("chain_belt", "belt", "Chain Belt", 1, null, IMP("es", 2, 3), "#a9b3bc");
        B("leather_belt", "belt", "Leather Belt", 8, null, IMP("hearts", 1, 1), "#7a4a2e");
        B("heavy_belt", "belt", "Heavy Belt", 20, null, IMP("str", 24, 36), "#6c6f73");
        B("vanguard_belt", "belt", "Vanguard Belt", 45, null, IMP("armour", 30, 40), "#6c6f73");
        B("crystal_belt", "belt", "Crystal Belt", 45, null, IMP("es", 6, 8), "#9fd8ff");
        /* amulets */
        B("coral_amulet", "amulet", "Coral Amulet", 1, null, IMP("regen", 0.03, 0.05), "#ff7a6a");
        B("amber_amulet", "amulet", "Amber Amulet", 3, null, IMP("str", 24, 36), "#ffb347");
        B("jade_amulet", "amulet", "Jade Amulet", 3, null, IMP("dex", 24, 36), "#6ee06e");
        B("lapis_amulet", "amulet", "Lapis Amulet", 3, null, IMP("int", 24, 36), "#5aa8ff");
        B("gold_amulet", "amulet", "Gold Amulet", 10, null, IMP("studs", 0.12, 0.20), "#ffd23e");
        B("onyx_amulet", "amulet", "Onyx Amulet", 20, null, IMP("allAttr", 12, 20), "#3a3f48");
        /* rings */
        B("iron_ring", "ring", "Iron Ring", 1, null, IMP("flatPhys", 0.2, 0.4), "#a9b3bc");
        B("coral_ring", "ring", "Coral Ring", 1, null, IMP("hearts", 1, 1), "#ff7a6a");
        B("ruby_ring", "ring", "Ruby Ring", 5, null, IMP("fireRes", 0.10, 0.15), "#ff4a3a");
        B("sapphire_ring", "ring", "Sapphire Ring", 5, null, IMP("frostRes", 0.10, 0.15), "#4a8aff");
        B("topaz_ring", "ring", "Topaz Ring", 5, null, IMP("shockRes", 0.10, 0.15), "#ffe14a");
        B("gold_ring", "ring", "Gold Ring", 10, null, IMP("studs", 0.08, 0.14), "#ffd23e");
        B("moonstone_ring", "ring", "Moonstone Ring", 15, null, IMP("es", 2, 4), "#cfe6ff");
        B("amethyst_ring", "ring", "Amethyst Ring", 25, null, IMP("chaosRes", 0.10, 0.14), "#b05aff");
        B("diamond_ring", "ring", "Diamond Ring", 25, null, IMP("incCrit", 0.15, 0.25), "#e8f6ff");
        B("prismatic_ring", "ring", "Prismatic Ring", 35, null, IMP("allRes", 0.06, 0.09), "#ff9aff");
        /* weapons: five of each, the damage they add climbing with the level */
        var WEAP = new (string cls, string[] names, double mul, string imp, double lo, double hi, string col)[]
        {
            ("sword", new[] { "Rusted Sword", "Copper Sword", "Cutlass", "Broad Sword", "Tiger Hook" }, 1.00, "incAcc", 0.08, 0.24, "#cfd6dd"),
            ("axe", new[] { "Rusted Hatchet", "Jade Hatchet", "Boarding Axe", "Cleaver", "Siege Axe" }, 1.15, "bleedChance", 0.04, 0.12, "#e0a81a"),
            ("mace", new[] { "Driftwood Club", "Tribal Club", "Spiked Club", "Rock Breaker", "Gavel" }, 1.40, "incMelee", 0.06, 0.18, "#b6532b"),
            ("scepter", new[] { "Driftwood Sceptre", "Darkwood Sceptre", "Bronze Sceptre", "Quartz Sceptre", "Crystal Sceptre" }, 1.00, "minionDmg", 0.06, 0.18, "#b7a6ff"),
            ("staff", new[] { "Gnarled Branch", "Primitive Staff", "Long Staff", "Iron Staff", "Coiled Staff" }, 1.30, "incSpell", 0.08, 0.24, "#9b45d6"),
            ("bow", new[] { "Crude Bow", "Short Bow", "Long Bow", "Composite Bow", "Recurve Bow" }, 1.10, "incCrit", 0.10, 0.30, "#6ee06e"),
        };
        double[] LV = { 1, 12, 24, 36, 50 }, DMG = { 0.3, 0.6, 1.0, 1.5, 2.1 };
        foreach (var Wp in WEAP)
            for (int i = 0; i < Wp.names.Length; i++)
            {
                string nm = Wp.names[i];
                double f = i / 4.0, a = Wp.lo + (Wp.hi - Wp.lo) * f;
                B(tag(nm), Wp.cls, nm, LV[i], new NumMap(("dmg", Math.Round(DMG[i] * Wp.mul * 10) / 10)),
                    IMP(Wp.imp, Math.Round(a * 0.85 * 100) / 100, Math.Round(a * 1.15 * 100) / 100), Wp.col);
            }
        return true;
    }
    public static readonly Dictionary<string, GearBase> GEAR_BASE_BY_ID = GEAR_BASES.ToDictionary(b => b.id, b => b);
    /* the one damage type a weapon adds, by what the hand holding it does */
    public static string gearDmgKind(string cls) => cls == "staff" ? "elem" : cls == "scepter" ? "minion" : "phys";
    /* ---- THE AFFIXES ---- */
    public static readonly Dictionary<string, MetaStat> GEAR_XSTATS = new()
    {
        ["minionFlat"] = new MetaStat { n = "flat damage to every minion hit", w = (p, v) => { p.minions.flat += v; } },
        ["allAttr"] = new MetaStat { n = "to all ATTRIBUTES", w = (p, v) => { p.attr["str"] += v; p.attr["dex"] += v; p.attr["int"] += v; } },
    };
    public static readonly string[] GEAR_ARM = { "helmet", "body", "gloves", "boots", "shield" };
    public static readonly string[] GEAR_JEW = { "ring", "amulet" };
    public static readonly string[] GEAR_NONWEAP = GEAR_ARM.Concat(GEAR_JEW).Concat(new[] { "belt", "quiver" }).ToArray();
    public static readonly string[] GEAR_WEAP = { "sword", "axe", "mace", "scepter", "staff", "bow" };
    public static readonly string[] GEAR_ATKW = { "sword", "axe", "mace", "bow" };
    /* TEN TIERS, PoE's way: T1 is the best a line can roll and T10 the worst */
    public static readonly double[] GEAR_TIER_LVL = { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55 };   /* T10 ... T1 */
    public const double GEAR_TIER_DECAY = 0.8;
    public static readonly List<GearAffix> GEAR_AFFIXES = BuildGearAffixes();
    static List<GearAffix> BuildGearAffixes()
    {
        var L0 = new List<GearAffix>();
        void A(string id, string kind, string stat, IEnumerable<string> cls, double lo, double hi, string[] names, double w = 0, string need = null, int n = 0)
        {
            if (n == 0) n = 10;
            var L = GEAR_TIER_LVL.Skip(GEAR_TIER_LVL.Length - n).ToList();
            var tiers = L.Select((lvl, k) => new GearTier
            {
                lvl = lvl, min = lo + (hi - lo) * k / n, max = lo + (hi - lo) * (k + 1) / n,
                name = names[Math.Min(names.Length - 1, (int)Math.Floor((double)k * names.Length / n))]
            }).ToList();
            L0.Add(new GearAffix { id = id, kind = kind, stat = stat, cls = cls.ToArray(), w = Js.Or(w, 100), need = need, tiers = tiers });
        }
        string[] S(params string[] a) => a;
        /* ---------------- PREFIXES ---------------- */
        A("hearts", "pre", "hearts", GEAR_ARM.Concat(S("belt", "amulet", "ring")), 1, 6, S("Hale", "Healthy", "Sanguine", "Stalwart", "Stout"), 140);
        A("esflat", "pre", "es", S("ring", "amulet", "belt"), 1, 12, S("Shining", "Glimmering", "Glittering", "Glowing", "Radiating"));
        A("locarm", "pre", "locArmour", GEAR_ARM, 4, 60, S("Lacquered", "Studded", "Ribbed", "Fortified", "Plated"), 100, "armour");
        A("loceva", "pre", "locEva", GEAR_ARM, 20, 300, S("Agile", "Dancer’s", "Acrobat’s", "Fleet", "Blurred"), 100, "eva");
        A("loces", "pre", "locEs", GEAR_ARM, 1, 12, S("Protective", "Strong-Willed", "Resolute", "Fearless", "Dauntless"), 100, "es");
        A("locarminc", "pre", "locArmourInc", GEAR_ARM, 0.15, 1.10, S("Reinforced", "Layered", "Lobstered", "Buttressed", "Thickened"), 100, "armour");
        A("locevainc", "pre", "locEvaInc", GEAR_ARM, 0.15, 1.10, S("Shade’s", "Ghost’s", "Spectre’s", "Wraith’s", "Phantasm’s"), 100, "eva");
        A("locesinc", "pre", "locEsInc", GEAR_ARM, 0.15, 1.10, S("Shimmering", "Seething", "Blazing", "Scintillating", "Incandescent"), 100, "es");
        A("flatphys", "pre", "flatPhys", S("ring", "amulet", "gloves", "quiver"), 0.2, 2.6, S("Glinting", "Burnished", "Polished", "Honed", "Gleaming"));
        A("flatelem", "pre", "flatElem", S("ring", "amulet", "gloves"), 0.2, 2.6, S("Humming", "Buzzing", "Snapping", "Crackling", "Sparking"));
        A("locdmg", "pre", "locDmg", GEAR_WEAP, 0.2, 3.0, S("Glinting", "Burnished", "Polished", "Honed", "Gleaming"), 120);
        A("locdmginc", "pre", "locDmgInc", GEAR_WEAP, 0.20, 1.40, S("Heavy", "Serrated", "Wicked", "Vicious", "Bloodthirsty"), 120);
        A("incspell", "pre", "incSpell", S("staff", "amulet", "shield"), 0.08, 0.60, S("Apprentice’s", "Adept’s", "Scholar’s", "Professor’s", "Occultist’s"));
        A("incelem", "pre", "incElem", S("amulet", "ring", "staff"), 0.06, 0.42, S("Catalysing", "Infusing", "Empowering", "Unleashed", "Overpowering"));
        A("miniondmg", "pre", "minionDmg", S("scepter", "helmet", "amulet"), 0.08, 0.60, S("Baron’s", "Viscount’s", "Marquess’s", "Duke’s", "Prince’s"));
        A("movespd", "pre", "speed", S("boots"), 0.08, 0.32, S("Runner’s", "Sprinter’s", "Stallion’s", "Gazelle’s", "Cheetah’s"), 160);
        /* ---------------- SUFFIXES ---------------- */
        A("fireres", "suf", "fireRes", GEAR_NONWEAP, 0.06, 0.40, S("of the Whelpling", "of the Salamander", "of the Drake", "of the Kiln", "of the Furnace"), 120);
        A("frostres", "suf", "frostRes", GEAR_NONWEAP, 0.06, 0.40, S("of the Inuit", "of the Seal", "of the Penguin", "of the Yeti", "of the Walrus"), 120);
        A("shockres", "suf", "shockRes", GEAR_NONWEAP, 0.06, 0.40, S("of the Cloud", "of the Squall", "of the Storm", "of the Thunderhead", "of the Tempest"), 120);
        A("chaosres", "suf", "chaosRes", GEAR_NONWEAP, 0.05, 0.24, S("of the Lost", "of Banishment", "of Eviction", "of Expulsion"), 40, null, 6);
        A("allres", "suf", "allRes", S("ring", "amulet", "shield"), 0.03, 0.14, S("of the Crystal", "of the Prism", "of the Kaleidoscope", "of Variegation"), 60, null, 7);
        var ALL = GEAR_NONWEAP.Concat(GEAR_WEAP).ToArray();
        A("str", "suf", "str", ALL, 12, 80, S("of the Brute", "of the Wrestler", "of the Bear", "of the Lion", "of the Gorilla"));
        A("dex", "suf", "dex", ALL, 12, 80, S("of the Mongoose", "of the Lynx", "of the Fox", "of the Falcon", "of the Panther"));
        A("int", "suf", "int", ALL, 12, 80, S("of the Pupil", "of the Student", "of the Prodigy", "of the Augur", "of the Philosopher"));
        A("allattr", "suf", "allAttr", S("amulet"), 6, 28, S("of the Clouds", "of the Sky", "of the Meteor", "of the Comet"), 60, null, 7);
        A("atkspd", "suf", "incAtk", S("gloves", "quiver", "ring").Concat(GEAR_ATKW), 0.05, 0.22, S("of Skill", "of Ease", "of Mastery", "of Renown", "of Grandmastery"));
        A("castspd", "suf", "incCast", S("amulet", "ring", "staff", "scepter", "shield"), 0.05, 0.24, S("of Talent", "of Nimbleness", "of Expertise", "of Legerdemain", "of Prestidigitation"));
        A("crit", "suf", "incCrit", GEAR_WEAP.Concat(S("amulet", "quiver", "ring")), 0.10, 0.42, S("of Needling", "of Stinging", "of Piercing", "of Puncturing", "of Penetrating"));
        A("critmult", "suf", "critMult", GEAR_WEAP.Concat(S("amulet", "quiver")), 0.06, 0.30, S("of Ire", "of Anger", "of Rage", "of Fury"), 100, null, 8);
        A("regen", "suf", "regen", S("helmet", "body", "amulet", "ring", "belt", "shield"), 0.02, 0.12, S("of the Newt", "of the Lizard", "of the Flatworm", "of the Starfish", "of the Hydra"));
        A("acc", "suf", "acc", S("helmet", "gloves", "quiver", "ring", "amulet").Concat(GEAR_ATKW), 20, 300, S("of Calm", "of Steadiness", "of Accuracy", "of Precision", "of the Ranger"));
        A("studs", "suf", "studs", S("helmet", "gloves", "boots", "ring", "amulet"), 0.06, 0.32, S("of Plunder", "of Raiding", "of Archaeology", "of Excavation", "of Windfall"));
        A("suppress", "suf", "suppress", S("body", "boots", "gloves", "helmet", "shield"), 0.03, 0.14, S("of Rebuttal", "of Snuffing", "of Revoking", "of Abjuration"), 70, null, 7);
        A("leech", "suf", "leech", S("sword", "axe", "mace", "gloves", "ring"), 0.01, 0.05, S("of the Remora", "of the Lamprey", "of the Vampire"), 60, null, 6);
        A("minionlife", "suf", "minionLife", S("scepter", "helmet", "amulet", "body"), 0.08, 0.36, S("of Loyalty", "of Devotion", "of Fealty", "of Allegiance"));
        A("magnet", "suf", "magnet", S("boots", "belt"), 0.10, 0.44, S("of the Magpie", "of the Jackdaw", "of the Raven", "of the Crow"), 70, null, 8);
        return L0;
    }
    /* keep the tier a line was rolled at while its value still fits it */
    public static int gearTierFix(GearAffix a, int ti, double v)
    {
        var t = ti >= 0 && ti < a.tiers.Count ? a.tiers[ti] : null;
        if (t != null && v >= gearRound(a.stat, t.min) - 0.011 && v <= gearRound(a.stat, t.max) + 0.011) return ti;
        return gearTierOf(a, v);
    }
    /* what a tier is CALLED: T1 is the best of them */
    public static int gearTierNo(GearAffix a, int ti) => a.tiers.Count - ti;
    /* the tier a stored value belongs in -- the best one whose floor it reaches */
    public static int gearTierOf(GearAffix a, double v)
    {
        int best = 0;
        for (int i = 0; i < a.tiers.Count; i++) { var t = a.tiers[i]; if (v >= Math.Min(t.min, gearRound(a.stat, t.min)) - 1e-9) best = i; }
        return best;
    }
    public static readonly Dictionary<string, GearAffix> GEAR_AFFIX_BY_ID = GEAR_AFFIXES.ToDictionary(a => a.id, a => a);
    /* WHOLE NUMBERS where the number is one */
    public static readonly HashSet<string> GEAR_WHOLE = new() { "hearts", "es", "armour", "eva", "str", "dex", "int", "allAttr", "acc",
        "locArmour", "locEva", "locEs" };
    public static readonly HashSet<string> GEAR_TENTHS = new() { "flatPhys", "flatElem", "locDmg", "arrowFlat", "minionFlat" };
    public static double gearRound(string stat, double v)
    {
        if (GEAR_WHOLE.Contains(stat)) return JsRound(v);
        if (GEAR_TENTHS.Contains(stat)) return JsRound(v * 10) / 10;
        return JsRound(v * 100) / 100;
    }
    static double JsRound(double v) => Math.Round(v);
    public static double gearRollIn(string stat, double lo, double hi) => gearRound(stat, lo + Js.R() * (hi - lo));
    /* ---- RARITY ---- */
    public static readonly Dictionary<string, GearRar> GEAR_RAR = new()
    {
        ["normal"] = new GearRar { name = "NORMAL", col = "#c8c8c8", bg = "rgba(200,200,200,.10)", min = 0, max = 0 },
        ["magic"] = new GearRar { name = "MAGIC", col = "#8888ff", bg = "rgba(90,90,255,.20)", min = 1, max = 2 },
        ["rare"] = new GearRar { name = "RARE", col = "#ffff77", bg = "rgba(255,230,80,.16)", min = 3, max = 6 },
    };
    public static readonly Dictionary<string, int> GEAR_MAX_PRE = new() { ["normal"] = 0, ["magic"] = 1, ["rare"] = 3 };
    public static readonly Dictionary<string, int> GEAR_MAX_SUF = GEAR_MAX_PRE;
    /* ONLY BOSSES DROP GEAR */
    public const double GEAR_BOSS_EXTRA = 0;
    public static readonly double[] GEAR_RARE_W = { 0.20, 0.35 };
    public static readonly double[] GEAR_MAGIC_W = { 0.50, 0.65 };
    public static string gearRollRarity(double tier)
    {
        int t = (int)clamp(Js.I32(tier), 0, 1); double r = Js.R();
        if (r < GEAR_RARE_W[t]) return "rare";
        if (r < GEAR_RARE_W[t] + GEAR_MAGIC_W[t]) return "magic";
        return "normal";
    }
    /* THE ITEM LEVEL IS THE MONSTER LEVEL, and never below one */
    public static double gearItemLevel() => Math.Max(1, mobRank());
    /* WHAT CAN DROP FOR THIS HERO */
    public static bool gearCanDrop(string cls)
    {
        var C = GEAR_CLS[cls]; string wid = weapon?.id ?? "sword";
        if (C.weap != null) return C.weap == wid;
        if (cls == "quiver") return wid == "bow";
        if (cls == "shield") return Array.IndexOf(GEAR_ONE_HAND, wid) >= 0;
        return true;
    }
    /* A NEWER BASE IS LIKELIER, not certain */
    public static GearBase gearRollBase(double ilvl, string cls = null)
    {
        var ok = GEAR_BASES.Where(b => b.lvl <= ilvl && (cls != null ? b.cls == cls : gearCanDrop(b.cls))).ToList();
        if (ok.Count == 0) return null;
        double tot = 0; var ws = ok.Select(b => { double w = 1 + b.lvl / 7; tot += w; return w; }).ToList();
        double r = Js.R() * tot;
        for (int i = 0; i < ok.Count; i++) { r -= ws[i]; if (r <= 0) return ok[i]; }
        return ok[^1];
    }
    public static bool gearAffixFits(GearAffix a, GearBase B, double ilvl, List<(string id, int ti, double v)> have)
    {
        if (Array.IndexOf(a.cls, B.cls) < 0) return false;
        if (a.need != null && !(B.def != null && Js.T(B.def[a.need]))) return false;
        if (have.Any(m => m.id == a.id)) return false;
        return a.tiers.Any(t => t.lvl <= ilvl);
    }
    public static bool gearRollAffix(GearItem it, string kind)
    {
        var B = GEAR_BASE_BY_ID[it.b];
        var pool = GEAR_AFFIXES.Where(a => a.kind == kind && gearAffixFits(a, B, it.il, it.m)).ToList();
        if (pool.Count == 0) return false;
        double tot = 0; foreach (var q in pool) tot += q.w;
        double r = Js.R() * tot; var a = pool[^1];
        foreach (var q in pool) { r -= q.w; if (r <= 0) { a = q; break; } }
        /* every tier the item is deep enough for can roll, each better one a fifth less likely */
        var tiers = new List<int>(); for (int i = 0; i < a.tiers.Count; i++) if (a.tiers[i].lvl <= it.il) tiers.Add(i);
        double tw = 0; var ws = tiers.Select((ti, k) => { double w = Math.Pow(GEAR_TIER_DECAY, k); tw += w; return w; }).ToList();
        double rr = Js.R() * tw; int tsel = tiers[^1];
        for (int k = 0; k < tiers.Count; k++) { rr -= ws[k]; if (rr <= 0) { tsel = tiers[k]; break; } }
        var T = a.tiers[tsel];
        it.m.Add((a.id, tsel, gearRollIn(a.stat, T.min, T.max)));
        return true;
    }
    public static int gearUid = 1;
    public static GearItem gearMake(string baseId, string rar, double ilvl)
    {
        var B = baseId != null ? GEAR_BASE_BY_ID.GetValueOrDefault(baseId) : null;
        if (B == null) return null;
        var it = new GearItem { u = gearUid++, b = B.id, r = rar != null && GEAR_RAR.ContainsKey(rar) ? rar : "normal", il = Math.Max(1, Js.I32(ilvl)) };
        if (B.imp != null) foreach (var (stat, lo, hi) in B.imp) it.imp.Add((stat, gearRollIn(stat, lo, hi)));
        var R = GEAR_RAR[it.r];
        int n = 0;
        if (it.r == "magic") n = Js.R() < 0.5 ? 1 : 2;
        else if (it.r == "rare") { double q = Js.R(); n = q < 0.35 ? 3 : q < 0.70 ? 4 : q < 0.90 ? 5 : 6; }
        n = (int)clamp(n, R.min, R.max);
        int pre = 0, suf = 0, guard = 0;
        while (pre + suf < n && guard++ < 20)
        {
            var kinds = new List<string>();
            if (pre < GEAR_MAX_PRE[it.r]) kinds.Add("pre");
            if (suf < GEAR_MAX_SUF[it.r]) kinds.Add("suf");
            if (kinds.Count == 0) break;
            if (kinds.Count == 2 && Js.R() < 0.5) kinds.Reverse();
            bool ok = false;
            foreach (var k in kinds) if (gearRollAffix(it, k)) { if (k == "pre") pre++; else suf++; ok = true; break; }
            if (!ok) break;
        }
        if (it.r == "rare") it.n = gearRareName(B.cls);
        return it;
    }
    /* a whole item, from the level it dropped at */
    public static GearItem gearRoll(double ilvl, double tier, string cls = null)
    {
        var B = gearRollBase(ilvl, cls);
        return B != null ? gearMake(B.id, gearRollRarity(tier), ilvl) : null;
    }
    /* ---- NAMES ---- */
    public static readonly string[] GEAR_RARE_A = { "Agony", "Apocalypse", "Armageddon", "Beast", "Behemoth", "Blight", "Blood", "Bramble",
        "Brimstone", "Brood", "Carrion", "Cataclysm", "Chimeric", "Corpse", "Corruption", "Damnation", "Death", "Demon",
        "Dire", "Dragon", "Dread", "Doom", "Dusk", "Eagle", "Empyrean", "Fate", "Foe", "Gale", "Ghoul", "Gloom", "Glyph",
        "Golem", "Grim", "Hate", "Havoc", "Honour", "Horror", "Hypnotic", "Kraken", "Loath", "Maelstrom", "Mind", "Miracle",
        "Morbid", "Oblivion", "Onslaught", "Pain", "Pandemonium", "Phoenix", "Plague", "Rage", "Rapture", "Rune", "Skull",
        "Sol", "Soul", "Sorrow", "Spirit", "Storm", "Tempest", "Torment", "Vengeance", "Victory", "Viper", "Vortex", "Woe", "Wrath" };
    public static readonly Dictionary<string, string[]> GEAR_RARE_B = new()
    {
        ["helmet"] = new[] { "Brow", "Corona", "Cowl", "Crest", "Crown", "Halo", "Horn", "Keep", "Peak", "Salvation", "Shelter", "Star", "Veil", "Visage" },
        ["body"] = new[] { "Carapace", "Cloak", "Coat", "Curtain", "Guardian", "Hide", "Jack", "Mantle", "Pelt", "Sanctuary", "Shell", "Shroud", "Skin", "Suit", "Ward", "Wrap" },
        ["gloves"] = new[] { "Caress", "Claw", "Clutches", "Fingers", "Fist", "Grasp", "Grip", "Hand", "Hold", "Knuckle", "Mitts", "Nails", "Palm", "Paw", "Talons", "Touch", "Vise" },
        ["boots"] = new[] { "Dash", "Goad", "Hoof", "League", "March", "Pace", "Road", "Slippers", "Sole", "Span", "Spur", "Stride", "Track", "Trail", "Tread", "Urge" },
        ["shield"] = new[] { "Aegis", "Badge", "Barrier", "Bastion", "Bulwark", "Duty", "Emblem", "Fend", "Guard", "Refuge", "Rock", "Rook", "Tower", "Watch", "Wing" },
        ["quiver"] = new[] { "Arrow", "Barb", "Bite", "Bolt", "Dart", "Flight", "Hail", "Impaler", "Nails", "Needle", "Quill", "Shot", "Spear", "Spike", "Stinger" },
        ["belt"] = new[] { "Bind", "Bond", "Buckle", "Clasp", "Cord", "Girdle", "Harness", "Lash", "Leash", "Lock", "Shackle", "Snare", "Strap", "Tether", "Twine" },
        ["amulet"] = new[] { "Beads", "Braid", "Charm", "Choker", "Collar", "Idol", "Gorget", "Heart", "Locket", "Medallion", "Noose", "Pendant", "Rosary", "Scarab", "Talisman", "Torc" },
        ["ring"] = new[] { "Band", "Circle", "Coil", "Eye", "Finger", "Gyre", "Hold", "Knot", "Loop", "Nail", "Spiral", "Turn", "Twirl", "Whorl" },
        ["sword"] = new[] { "Bane", "Beak", "Edge", "Fang", "Gutter", "Hunger", "Impaler", "Needle", "Razor", "Saw", "Scratch", "Sever", "Skewer", "Slicer", "Song", "Spike", "Stinger", "Thirst" },
        ["axe"] = new[] { "Bane", "Beak", "Bite", "Butcher", "Edge", "Etcher", "Gnash", "Hunger", "Mangler", "Rend", "Roar", "Sever", "Slayer", "Song", "Splitter", "Sunder", "Thirst" },
        ["mace"] = new[] { "Bane", "Batter", "Blast", "Blow", "Blunt", "Brand", "Breaker", "Burst", "Crack", "Crusher", "Grinder", "Knell", "Ram", "Roar", "Ruin", "Shatter", "Smasher", "Star", "Wreck" },
        ["scepter"] = new[] { "Bane", "Blow", "Breaker", "Call", "Chant", "Crack", "Crusher", "Cry", "Gnarl", "Knell", "Ram", "Roar", "Song", "Spike", "Star", "Weaver" },
        ["staff"] = new[] { "Bane", "Beam", "Branch", "Call", "Chant", "Cry", "Gnarl", "Goad", "Mast", "Pillar", "Pole", "Post", "Roar", "Song", "Spire", "Weaver" },
        ["bow"] = new[] { "Arch", "Bane", "Barrage", "Blast", "Branch", "Breeze", "Fletch", "Guide", "Horn", "Mark", "Nock", "Rain", "Reach", "Siege", "Song", "Stinger", "Strike", "Thunder", "Volley", "Wind", "Wing" },
    };
    public static string gearRareName(string cls) => pick(GEAR_RARE_A) + " " + pick(GEAR_RARE_B.GetValueOrDefault(cls ?? "") ?? GEAR_RARE_B["ring"]);
    public static string gearName(GearItem it)
    {
        var B = GEAR_BASE_BY_ID.GetValueOrDefault(it.b ?? "");
        if (B == null) return "?";
        if (it.r == "rare" && !string.IsNullOrEmpty(it.n)) return it.n;
        if (it.r == "magic")
        {
            string pre = "", suf = "";
            foreach (var (id, ti, _) in it.m)
            {
                var a = GEAR_AFFIX_BY_ID.GetValueOrDefault(id); if (a == null || ti < 0 || ti >= a.tiers.Count) continue;
                if (a.kind == "pre") pre = a.tiers[ti].name + " "; else suf = " " + a.tiers[ti].name;
            }
            return pre + B.name + suf;
        }
        return B.name;
    }
    /* ---- WHAT AN ITEM IS WORTH: local lines fold into the item's own number first ---- */
    public static List<(string stat, double v)> gearMods(GearItem it)
    {
        var B = it != null ? GEAR_BASE_BY_ID.GetValueOrDefault(it.b ?? "") : null;
        var outL = new List<(string, double)>();
        if (B == null) return outL;
        var loc = new NumMap();
        var lines = new List<(string stat, double v)>();
        foreach (var (id, ti, v) in it.m) { var a = GEAR_AFFIX_BY_ID.GetValueOrDefault(id); if (a != null) lines.Add((a.stat, v)); }
        foreach (var (stat, v) in lines) if (stat.StartsWith("loc")) loc[stat] = loc[stat] + v;
        if (B.def != null)
        {
            if (Js.T(B.def["armour"])) outL.Add(("armour", JsRound((B.def["armour"] + loc["locArmour"]) * (1 + loc["locArmourInc"]))));
            if (Js.T(B.def["eva"])) outL.Add(("eva", JsRound((B.def["eva"] + loc["locEva"]) * (1 + loc["locEvaInc"]))));
            if (Js.T(B.def["es"])) outL.Add(("es", JsRound((B.def["es"] + loc["locEs"]) * (1 + loc["locEsInc"]))));
        }
        if (Js.T(B.dmg))
        {
            double v = JsRound((B.dmg + loc["locDmg"]) * (1 + loc["locDmgInc"]) * 10) / 10;
            string k = gearDmgKind(B.cls);
            outL.Add((k == "elem" ? "flatElem" : k == "minion" ? "minionFlat" : "flatPhys", v));
        }
        foreach (var m in it.imp) outL.Add((m.stat, m.v));
        foreach (var (stat, v) in lines) if (!stat.StartsWith("loc")) outL.Add((stat, v));
        return outL;
    }
    /* the stat writer behind a line: the tree's own, or one of the two gear adds */
    public static MetaStat gearWriter(string stat) => stat == null ? null : GEAR_XSTATS.GetValueOrDefault(stat) ?? META_STATS.GetValueOrDefault(stat);
    public static void gearWrite(Ent p, string stat, double v) { var S = gearWriter(stat); if (S != null && S.w != null) try { S.w(p, v); } catch { } }
    /* ---- THE SAVE ---- */
    public static GearState GEAR = null;
    /* ONE SET OF GEAR PER WEAPON, ONE STASH FOR ALL */
    public static Dictionary<string, GearItem> gearBlankEq() { var eq = new Dictionary<string, GearItem>(); foreach (var s in GEAR_SLOTS) eq[s] = null; return eq; }
    public static GearState gearBlank()
    {
        var Gs = new GearState();
        foreach (var k in WEAPON_KEYS) Gs.eqBy[k] = gearBlankEq();
        Gs.eq = Gs.eqBy[weapon?.id ?? "sword"];
        return Gs;
    }
    public static void gearBindClass()
    {
        if (GEAR == null) return;
        string k = (weapon != null && WEAPONS.ContainsKey(weapon.id)) ? weapon.id : "sword";
        if (!GEAR.eqBy.ContainsKey(k) || GEAR.eqBy[k] == null) GEAR.eqBy[k] = gearBlankEq();
        GEAR.eq = GEAR.eqBy[k];
    }
    static bool JNum(JsonNode n, out double d)
    {
        d = 0;
        if (n is not JsonValue v) return false;
        if (v.TryGetValue<double>(out d)) return double.IsFinite(d);
        if (v.TryGetValue<string>(out var s)) { d = Js.ToNumber(s); return double.IsFinite(d); }
        if (v.TryGetValue<bool>(out var b)) { d = b ? 1 : 0; return true; }
        return false;
    }
    static int JI32(JsonNode n) => JNum(n, out var d) ? Js.I32(d) : 0;
    static string JStr(JsonNode n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public static GearItem gearValid(JsonNode itN)
    {
        if (itN is not JsonObject it) return null;
        string b = JStr(it["b"]);
        if (b == null || !GEAR_BASE_BY_ID.ContainsKey(b)) return null;
        var B = GEAR_BASE_BY_ID[b];
        string r = JStr(it["r"]);
        int u = JI32(it["u"]);
        var o = new GearItem { u = u != 0 ? u : gearUid++, b = b, r = r != null && GEAR_RAR.ContainsKey(r) ? r : "normal", il = Math.Max(1, JI32(it["il"])) };
        if (it["imp"] is JsonArray ia)
            foreach (var m in ia)
                if (m is JsonArray ma && ma.Count > 1 && JStr(ma[0]) is string st && gearWriter(st) != null && JNum(ma[1], out var v))
                    o.imp.Add((st, v));
        if (it["m"] is JsonArray mm)
            foreach (var m in mm)
                if (m is JsonArray ma && ma.Count > 2 && JStr(ma[0]) is string id && GEAR_AFFIX_BY_ID.ContainsKey(id) && JNum(ma[2], out var v))
                    o.m.Add((id, gearTierFix(GEAR_AFFIX_BY_ID[id], JI32(ma[1]), v), v));
        if (o.r == "rare") { var nm = JStr(it["n"]); o.n = nm != null ? (nm.Length > 40 ? nm.Substring(0, 40) : nm) : gearRareName(B.cls); }
        return o;
    }
    static JsonObject GearItemJson(GearItem it)
    {
        var o = new JsonObject
        {
            ["u"] = it.u, ["b"] = it.b, ["r"] = it.r, ["il"] = it.il,
            ["imp"] = new JsonArray(it.imp.Select(m => (JsonNode)new JsonArray(m.stat, m.v)).ToArray()),
            ["m"] = new JsonArray(it.m.Select(m => (JsonNode)new JsonArray(m.id, m.ti, m.v)).ToArray()),
        };
        if (it.r == "rare") o["n"] = it.n;
        return o;
    }
    public static GearState gearLoad()
    {
        string raw = null; try { raw = Store.getItem(GEAR_KEY); } catch { }
        var Gs = gearBlank();
        try
        {
            var o = raw != null ? JsonNode.Parse(raw) as JsonObject : null;
            if (o != null)
            {
                int maxU = 0;
                void readSet(JsonNode src, Dictionary<string, GearItem> into)
                {
                    foreach (var s in GEAR_SLOTS)
                    {
                        var it = gearValid(src is JsonObject so ? so[s] : null);
                        if (it != null && gearFitsSlot(it, s)) { into[s] = it; maxU = Math.Max(maxU, it.u); }
                    }
                }
                if (o["eqBy"] is JsonObject eb) { foreach (var k in WEAPON_KEYS) readSet(eb[k], Gs.eqBy[k]); }
                else if (o["eq"] is JsonObject eq0)
                {
                    /* A SAVE FROM BEFORE THE SPLIT had one set */
                    string k = null;
                    var w = gearValid(eq0["weapon"]); var C = w != null ? GEAR_CLS[GEAR_BASE_BY_ID[w.b].cls] : null;
                    if (C != null && C.weap != null) k = C.weap;
                    if (k == null) { try { var bw = Store.getItem("bb_weapon"); if (bw != null && WEAPONS.ContainsKey(bw)) k = bw; } catch { } }
                    readSet(eq0, Gs.eqBy[k ?? "sword"]);
                }
                foreach (var k in new[] { "inv", "stash" })
                    if (o[k] is JsonArray arr)
                        foreach (var e in arr)
                        {
                            var it = gearValid(e is JsonObject eo ? eo["it"] : null); if (it == null) continue;
                            var list = k == "stash" ? Gs.stash : Gs.inv; var dim = k == "stash" ? GEAR_DIM["stash"] : GEAR_DIM["inv"];
                            int x = JI32(((JsonObject)e)["x"]), y = JI32(((JsonObject)e)["y"]);
                            var at = gearRoomAt(list, it, x, y, null, dim) ? (x, y) : gearFreeSpot(list, it, null, dim);
                            if (at != null) { list.Add(new GearEntry { it = it, x = at.Value.x, y = at.Value.y }); maxU = Math.Max(maxU, it.u); }
                        }
                gearUid = Math.Max(Math.Max(gearUid, maxU + 1), JI32(o["uid"]));
            }
        }
        catch { }
        GEAR = Gs;
        gearBindClass();
        return Gs;
    }
    public static void gearSave()
    {
        if (GEAR == null) return;
        GEAR.uid = gearUid;
        var eqBy = new JsonObject();
        foreach (var (k, set) in GEAR.eqBy)
        {
            var so = new JsonObject();
            foreach (var s in GEAR_SLOTS) so[s] = set.TryGetValue(s, out var it) && it != null ? GearItemJson(it) : null;
            eqBy[k] = so;
        }
        JsonArray list(List<GearEntry> l) => new JsonArray(l.Select(e => (JsonNode)new JsonObject { ["it"] = GearItemJson(e.it), ["x"] = e.x, ["y"] = e.y }).ToArray());
        var keep = new JsonObject { ["v"] = GEAR.v, ["eqBy"] = eqBy, ["inv"] = list(GEAR.inv), ["stash"] = list(GEAR.stash), ["uid"] = GEAR.uid };
        try { Store.setItem(GEAR_KEY, keep.ToJsonString()); } catch { }
    }
    /* ---- THE BAG ---- */
    public static (int w, int h) gearSize(GearItem it)
    {
        var B = GEAR_BASE_BY_ID.GetValueOrDefault(it.b ?? ""); var C = B != null ? GEAR_CLS.GetValueOrDefault(B.cls) : null;
        return C != null ? (C.w, C.h) : (1, 1);
    }
    public static readonly Dictionary<string, GearDim> GEAR_DIM = new()
    {
        ["inv"] = new GearDim { cols = GEAR_COLS, rows = GEAR_ROWS },
        ["stash"] = new GearDim { cols = GEAR_STASH_COLS, rows = GEAR_STASH_ROWS, rowMajor = true },
    };
    /* which grid a list is: the stash's, or the bag's */
    public static GearDim gearDimOf(List<GearEntry> list) => (GEAR != null && list == GEAR.stash) ? GEAR_DIM["stash"] : GEAR_DIM["inv"];
    public static bool gearRoomAt(List<GearEntry> inv, GearItem it, int x, int y, GearEntry skip = null, GearDim dim = null)
    {
        var s = gearSize(it); var D = dim ?? gearDimOf(inv);
        if (x < 0 || y < 0 || x + s.w > D.cols || y + s.h > D.rows) return false;
        foreach (var e in inv)
        {
            if (e == skip || e.it == it) continue;
            var t = gearSize(e.it);
            if (x < e.x + t.w && e.x < x + s.w && y < e.y + t.h && e.y < y + s.h) return false;
        }
        return true;
    }
    /* A MAP OF WHAT IS TAKEN, then one pass over it */
    public static (int x, int y)? gearFreeSpot(List<GearEntry> inv, GearItem it, GearEntry skip = null, GearDim dim = null)
    {
        var D = dim ?? gearDimOf(inv); var s = gearSize(it); int C = D.cols, R = D.rows;
        var occ = new byte[C * R];
        foreach (var e in inv)
        {
            if (e == skip || e.it == it) continue;
            var t = gearSize(e.it);
            for (int yy = Math.Max(0, e.y); yy < Math.Min(R, e.y + t.h); yy++)
                for (int xx = Math.Max(0, e.x); xx < Math.Min(C, e.x + t.w); xx++) occ[yy * C + xx] = 1;
        }
        bool fits(int x, int y) { for (int yy = y; yy < y + s.h; yy++) for (int xx = x; xx < x + s.w; xx++) if (occ[yy * C + xx] != 0) return false; return true; }
        if (D.rowMajor) { for (int y = 0; y + s.h <= R; y++) for (int x = 0; x + s.w <= C; x++) if (fits(x, y)) return (x, y); }
        else for (int x = 0; x + s.w <= C; x++) for (int y = 0; y + s.h <= R; y++) if (fits(x, y)) return (x, y);
        return null;
    }
    public static bool gearInvAdd(GearItem it)
    {
        if (GEAR == null) gearLoad();
        var at = gearFreeSpot(GEAR.inv, it);
        if (at == null) return false;
        GEAR.inv.Add(new GearEntry { it = it, x = at.Value.x, y = at.Value.y });
        gearSave();
        return true;
    }
    /* ---- WHAT FITS WHERE ---- */
    public static string gearSlotOf(GearItem it) { var B = GEAR_BASE_BY_ID.GetValueOrDefault(it.b ?? ""); return B != null ? GEAR_CLS[B.cls].slot : null; }
    public static bool gearFitsSlot(GearItem it, string slot)
    {
        var s = gearSlotOf(it);
        return s != null && (s == slot || (s == "ring" && (slot == "ring1" || slot == "ring2")));
    }
    /* AN ITEM ONLY WORKS IN A HAND THAT CAN USE IT, and says why */
    public static string gearActiveWhy(GearItem it, string slot = null)
    {
        var B = it != null ? GEAR_BASE_BY_ID.GetValueOrDefault(it.b ?? "") : null;
        if (B == null) return "unknown item";
        var C = GEAR_CLS[B.cls]; string wid = weapon?.id ?? "sword";
        if (C.weap != null && C.weap != wid) return "you are wielding a " + (WEAPONS.GetValueOrDefault(wid)?.name ?? wid).ToUpperInvariant() + " — this only works for a " + C.weap.ToUpperInvariant() + " hero";
        if (B.cls == "quiver" && wid != "bow") return "a quiver only works with a BOW";
        if (B.cls == "shield" && Array.IndexOf(GEAR_ONE_HAND, wid) < 0) return "a " + (WEAPONS.ContainsKey(wid) ? WEAPONS[wid].name : wid).ToUpperInvariant() + " takes both hands — no room for a shield";
        return "";
    }
    public static bool gearActive(GearItem it, string slot = null) => gearActiveWhy(it, slot) == "";
    /* THE GEAR THIS RUN WEARS: a run link carries the gear it was copied with */
    public static Dictionary<string, GearItem> gearLinkCache = null; public static JsonObject gearLinkSrc = null;
    public static bool gearLinked() => runLink != null && runLink.g != null;
    public static Dictionary<string, GearItem> gearEqNow()
    {
        if (gearLinked())
        {
            if (gearLinkSrc != runLink.g) { gearLinkSrc = runLink.g; gearLinkCache = gearUnpackEq(runLink.g); }
            return gearLinkCache;
        }
        if (GEAR == null) gearLoad();
        return GEAR.eq;
    }
    public static List<(string stat, double v)> gearActiveMods()
    {
        var eq = gearEqNow(); var outL = new List<(string, double)>();
        foreach (var s in GEAR_SLOTS) { var it = eq.GetValueOrDefault(s); if (it != null && gearActive(it, s)) outL.AddRange(gearMods(it)); }
        return outL;
    }
    /* PUT ON WHAT IS WORN: take the old lines off, put the new ones on */
    public static void gearApply(Ent p, bool fill)
    {
        if (p == null) return;
        var want = gearActiveMods(); var had = p.gearMods ?? new List<(string stat, double v)>();
        double hp0 = p.maxHp, hpNow = p.hp;
        foreach (var (k, v) in had) gearWrite(p, k, -v);
        foreach (var (k, v) in want) gearWrite(p, k, v);
        p.gearMods = want;
        syncStats();
        if (fill) p.hp = Math.Min(p.maxHp, hpNow + Math.Max(0, p.maxHp - hp0));
        else if (hp0 > 0 && p.maxHp != hp0 && hpNow > 0) p.hp = Math.Min(p.maxHp, hpNow * p.maxHp / hp0);
    }
    /* the gear, squeezed for a run link: base, rarity, level, implicits, affixes */
    public static JsonObject gearPackEq(Dictionary<string, GearItem> eq)
    {
        var o = new JsonObject();
        foreach (var s in GEAR_SLOTS)
        {
            var it = eq?.GetValueOrDefault(s);
            if (it != null)
                o[s] = new JsonArray(it.b, it.r.Substring(0, 1), it.il,
                    new JsonArray(it.imp.Select(m => (JsonNode)new JsonArray(m.stat, m.v)).ToArray()),
                    new JsonArray(it.m.Select(m => (JsonNode)new JsonArray(m.id, m.ti, m.v)).ToArray()),
                    !string.IsNullOrEmpty(it.n) ? JsonValue.Create(it.n) : JsonValue.Create(0));
        }
        return o.Count > 0 ? o : null;
    }
    public static Dictionary<string, GearItem> gearUnpackEq(JsonObject o)
    {
        var eq = new Dictionary<string, GearItem>();
        foreach (var s in GEAR_SLOTS)
        {
            eq[s] = null;
            if (o?[s] is not JsonArray a) continue;
            string rr = JStr(a.At(1)); string r = rr == "r" ? "rare" : rr == "m" ? "magic" : "normal";
            var n5 = a.At(5); string nm = JStr(n5);
            var src = new JsonObject
            {
                ["u"] = 0, ["b"] = a.At(0)?.DeepClone(), ["r"] = r, ["il"] = a.At(2)?.DeepClone(),
                ["imp"] = a.At(3)?.DeepClone(), ["m"] = a.At(4)?.DeepClone(), ["n"] = !string.IsNullOrEmpty(nm) ? nm : null
            };
            var it = gearValid(src);
            if (it != null && gearFitsSlot(it, s)) eq[s] = it;
        }
        return eq;
    }
    /* ---- ON THE FLOOR ---- */
    public static List<GearDrop> gearDrops = new();
    public const double GEAR_PICK_R = 30;
    /* past this many, the oldest item of the lowest rarity on the floor goes */
    public const int GEAR_FLOOR_MAX = 60;
    public static GearDrop gearDropAt(GearItem it, double x, double y)
    {
        if (it == null) return null;
        var d = shoveToFloor(new GearDrop { it = it, x = x + rnd(-14, 14), y = y + rnd(-14, 14), z = 10, vz = rnd(160, 260), t = 0, got = false }, 18);
        gearDrops.Add(d);
        if (gearDrops.Count > GEAR_FLOOR_MAX)
        {
            foreach (var r in new[] { "normal", "magic", "rare" })
            {
                int i = gearDrops.FindIndex(q => q.it.r == r);
                if (i >= 0) { gearDrops.RemoveAt(i); break; }
            }
        }
        return d;
    }
    public static double gearFullT = 0;
    public static void updateGearDrops(double dt)
    {
        var p = player;
        gearFullT = Math.Max(0, gearFullT - dt);
        foreach (var d in gearDrops.ToList())
        {
            d.t += dt;
            if (d.z > 0 || d.vz > 0) { d.vz -= 900 * dt; d.z = Math.Max(0, d.z + d.vz * dt); if (d.z == 0) d.vz = 0; }
            if (p == null || p.dead || d.got || d.t < 0.35) continue;
            if (dist(d, p) > GEAR_PICK_R + Js.Or(p.r, 13)) continue;
            if (gearInvAdd(d.it))
            {
                d.got = true;
                var R = GEAR_RAR[d.it.r];
                addDmg(p.x, p.y - 52, gearName(d.it), R.col, d.it.r == "rare" ? 18 : 15);
                logEvent("LOOT — " + gearName(d.it), R.col);
                SFX.pick();
            }
            else if (gearFullT <= 0)
            {
                gearFullT = 2.5;
                addDmg(p.x, p.y - 52, "INVENTORY FULL", "#ff8a6a", 16);
            }
        }
        gearDrops = gearDrops.Filter(d => !d.got);
    }
    /* what a boss leaves: one piece, and a super boss a second, at least magic */
    public static List<GearDrop> gearBossSpoils(Ent e)
    {
        double lv = gearItemLevel(); var outL = new List<GearDrop>();
        if (!modeHasGear()) return outL;          /* an arcade boss drops bricks, not gear */
        void drop(double tier, double dx, bool floor = false)
        {
            var B = gearRollBase(lv); if (B == null) return;
            string r = gearRollRarity(tier); if (floor && r == "normal") r = "magic";
            outL.Add(gearDropAt(gearMake(B.id, r, lv), e.x + dx, e.y + 20));
        }
        drop(0, -30);
        if (e.super) drop(1, 30, true);
        else if (Js.R() < GEAR_BOSS_EXTRA) drop(0, 30);
        return outL;
    }
}
