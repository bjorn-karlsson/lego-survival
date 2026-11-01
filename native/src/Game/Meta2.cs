namespace Brickblade;

/* ========================= THE SKILL TREE: the map (JS 13045–13957) =========================
   A LATTICE of hubs and lanes, clusters picked off a template library by a hash
   of where they are, so the map is the same every time it is built. The template
   tables went through tools/js2cs.py; everything around them is by hand. */
public sealed class MetaSpot { public double ring, rad, deg; }
public sealed class MetaPrize { public string name, note; public double cost = double.NaN; public List<Mod> mods = new(); }
public sealed class MetaSpoke { public string id, name, col; }
public sealed class MetaRing { public double r, n, phase; }
public sealed class MetaDoor { public string id, weap, home, name; }
/* one template: a group, a notable's cluster, a keystone's, a legend's, a heart group */
public sealed class MetaTpl
{
    public string id, name, sp, home, shape, only;
    public double[] d = { 0, 4 };
    public bool must, lateOnly, after;
    public double r = double.NaN;
    public MetaSpot spot;
    public List<(string name, string stat, double v)> minors = new();
    public MetaPrize notable, keystone, legend;
}

public static partial class G
{
    /* ---- WHAT A NODE LOOKS LIKE: read off the stat the node gives ---- */
    public static readonly Dictionary<string, string> META_ICON_EXACT = BuildIconExact();
    static Dictionary<string, string> BuildIconExact()
    {
        var d = new Dictionary<string, string>();
        void S(string list) { foreach (var kv in list.Split(',')) { var p = kv.Split(':'); d[p[0].Trim()] = p[1].Trim(); } }
        S("incMelee:sword, flatPhys:sword, incAll:burst, moreAll:burst, incSpell:arcane, incCast:tempo, incAtk:tempo, boltR:range");
        S("incCrit:target, critMult:target, reach:range, incDot:bane, bleedChance:bane, bleedShare:bane, poisonDot:bane");
        S("incElem:prism, flatElem:prism, incFire:flame, incFrost:snow, incShock:bolt, incChaos:vortex, ailChance:bane");
        S("rageHit:fist, rageHurt:shield, rageMax:force, fireRes:ward, frostRes:ward, shockRes:ward, chaosRes:ward, allRes:ward");
        S("armour:shield, incArmour:shieldplus, suppress:shield, hearts:heartplus, incHp:heart, moreHp:heart, regen:regen, incRegen:regen");
        S("leech:tide, leechRate:tide, heartDrop:gift, incCritMult:target, speed:boot, roll:roll, magnet:magnet, studs:coin, xp:xp");
        S("str:fist, dex:boot, int:arcane, ember:ember, spellCap:prism, orbitDmg:orbit, orbitR:orbit, orbitSpd:orbit");
        S("chainDmg:chain, chainFork:fork, chainRate:chain, stormDmg:boomerang, stormSize:boomerang, stormRate:boomerang");
        S("vortexDmg:vortex, vortexR:vortex, vortexRate:vortex, freezeR:freeze, freezeRate:icecube");
        S("bombDmg:bomb, bombCluster:bomblets, bombR:bomb, bombRate:bomb, baneDmg:bane, baneR:bane, baneRate:bane");
        S("slamDmg:fist, slamArc:fist, slamRange:fist, tollDelay:clock, reroll:split, noReroll:nofav, chestCards:gift");
        S("bankMul:coin, noRegen:nofav, noArmour:nofav, lessSpell:ward, heartsPerInt:heartplus, grant:totem");
        S("chargeHurt:shield, chargeKill:sword, chargeCrit:target, acc:target, incAcc:target, eva:boot, incEva:boot");
        S("es:ward, incEs:ward, chargeMax:force, chargeAll:force, exposeChance:prismdn, ailMag:force");
        S("minionDmg:skull, minionLife:skull, minionSpd:skull, minionMax:skull, golemCap:skull, minionProj:split, minionZombies:skull, minionLess:skull, minionMirror:skull, minionInstab:skull");
        S("prolif:bane, lessDot:bane, arrowFlat:bow, arrowPierce:bow, arrowN:bow, arrowFork:chain, arrowBlast:flame, whirlShare:spin, reaveShare:boomerang, reaveBounce:boomerang");
        S("penFire:flame, penFrost:icecube, penShock:chain, penChaos:bane, penElem:prism, ricochet:bow, arrowLess:bow, chaosInoc:ward, ghostReaver:ward, zealot:ward, critAilAll:bane, noCrit:nofav, ailFull:icecube");
        return d;
    }
    /* a conversion wears the type it ARRIVES as */
    public static readonly Dictionary<string, string> CONV_ICON = new() { ["phys"] = "sword", ["shock"] = "bolt", ["frost"] = "snow", ["fire"] = "flame", ["chaos"] = "vortex" };
    public static readonly Dictionary<string, string> WEAP_ICON = new() { ["sword"] = "sword", ["axe"] = "spin", ["mace"] = "fist", ["staff"] = "arcane" };
    static readonly System.Text.RegularExpressions.Regex CV_RE = new(@"^(?:cv|gn)_([a-z]+)_([a-z]+)$"), WINC_RE = new(@"^(?:dmg|inc)_(.+)$");
    public static string metaIconKey(MetaNode n)
    {
        if (n == null) return "burst";
        if (n.kind == "start") return "totem";
        string k = (n.mods != null && n.mods.Count > 0) ? n.mods[0].k : "";
        if (string.IsNullOrEmpty(k)) return "burst";
        if (META_ICON_EXACT.TryGetValue(k, out var ic)) return ic;
        var cv = CV_RE.Match(k);
        if (cv.Success) return CONV_ICON.GetValueOrDefault(cv.Groups[2].Value) ?? "prism";
        var w = WINC_RE.Match(k);
        if (w.Success) return WEAP_ICON.GetValueOrDefault(w.Groups[1].Value) ?? "sword";
        return "burst";
    }
    public static readonly List<MetaSpoke> META_SPOKES = new()
    {
        new() { id = "war", name = "WARFARE", col = "#ff8a4a" },
        new() { id = "prec", name = "PRECISION", col = "#ffd166" },
        new() { id = "swift", name = "SWIFTNESS", col = "#7ecbff" },
        new() { id = "aff", name = "AFFLICTION", col = "#8ede4a" },
        new() { id = "vit", name = "VITALITY", col = "#7ed47e" },
        new() { id = "arm", name = "ARMOUR", col = "#9fb0bd" },
        new() { id = "elem", name = "ELEMENTS", col = "#c95bd0" },
        new() { id = "arc", name = "ARCANA", col = "#9b45d6" },
        new() { id = "core", name = "THE CORE", col = "#ffd76e" },
        /* the three highways wear the colour of what they pay */
        new() { id = "str", name = "THE IRON ROAD", col = "#ff5a4a" },
        new() { id = "int", name = "THE RUNE ROAD", col = "#5aa8ff" },
        new() { id = "dex", name = "THE SWIFT ROAD", col = "#6ee06e" },
    };
    /* [from, to] clockwise in degrees, 0 = east. They tile the circle exactly. */
    public static readonly (double cx, double cy, Dictionary<string, double[]> sector) META_MAP =
        (4200, 4200, new Dictionary<string, double[]> { ["str"] = new double[] { 95, 235 }, ["int"] = new double[] { 235, 345 }, ["dex"] = new double[] { 345, 95 } });
    public static string metaSectorOf(double deg)
    {
        double a = ((deg % 360) + 360) % 360;
        foreach (var k in new[] { "str", "int", "dex" })
        {
            double f = META_MAP.sector[k][0], t = META_MAP.sector[k][1];
            if (f < t ? (a >= f && a < t) : (a >= f || a < t)) return k;
        }
        return "str";
    }
    public static string metaSectorAt(double x, double y) => metaSectorOf(Math.Atan2(y - META_MAP.cy, x - META_MAP.cx) * 180 / Math.PI);
    /* WHICH COUNTRY EACH WEAPON CALLS HOME */
    public static readonly Dictionary<string, string> META_HOME = new() { ["sword"] = "str", ["mace"] = "str", ["axe"] = "dex", ["staff"] = "int", ["bow"] = "dex", ["scepter"] = "int" };
    /* THE HEART, AND THE ROOM IT NEEDS */
    public const double CORE_ROOM = 560;
    public const double DOOR_R = 1300;
    public const double HEART_R = 520, HEART_N = 12;
    public const double HEART_STRIDE = 250;
    public const double HEART_SITE_R = 1000;
    public const double HEART_GROUP_GAP = 50;
    /* 1.0 SINCE THE HEART: dead even */
    public const double DOOR_SPREAD = 1.0;
    public static List<MetaDoor> META_DOORS = new();
    /* THE RINGS: radius, how many hubs sit on it, and how far round it they are turned */
    public static readonly List<MetaRing> META_RINGS = new()
    {
        new() { r = 1180 + CORE_ROOM, n = 12, phase = 0 },
        new() { r = 2230 + CORE_ROOM, n = 18, phase = 10 },
        new() { r = 3380 + CORE_ROOM, n = 24, phase = 5 },
        new() { r = 4590 + CORE_ROOM, n = 30, phase = 8 },
        new() { r = 5830 + CORE_ROOM, n = 36, phase = 3 },
    };
    public const double HUB_JIT = 0.055;
    public const double HUB_SWING = 0.30;
    /* NO NODE SELLS A NUMBER NOBODY CAN READ */
    public const double META_MIN_FLAT = 0.2;
    public const double LANE_STEP = 560;
    /* THE BALANCING BREAK */
    public const double ATTR_BREAK_N = 30, ATTR_BREAK_R = 31, ATTR_BREAK_D = 150;
    public static readonly Dictionary<string, string> ATTR_COL = new() { ["str"] = "#ff5a4a", ["dex"] = "#6ee06e", ["int"] = "#5aa8ff" };
    public const double LANE_VARY = 0.46;
    /* what a step of lane is worth: an ATTRIBUTE, by the ground it crosses */
    public static readonly Dictionary<string, (string name, string stat, double v)> META_TRAVEL = new()
    {
        ["str"] = ("Strength", "str", 10),
        ["dex"] = ("Dexterity", "dex", 10),
        ["int"] = ("Intelligence", "int", 10),
        ["core"] = ("Resolve", "str", 20),
    };
    public static readonly Dictionary<string, List<List<(string name, string stat, double v)>>> META_BRANCH = new()
    {
      /* RED IS TWO BEGINNINGS, not one. The sword and the mace both live in the red
         country, so a build that never leaves it can walk into twenty-four cheap
         opening nodes where green and blue get twelve — which put a stay-at-home
         red build over the power budget the moment these paths grew from one node
         to three. Red's openings are the smaller ones because red has twice as many
         of them; the flat physical node went altogether, for the reason it always
         goes: flat damage on a weapon whose base is one is a multiplier in
         disguise. */
      ["sword"] = [
        [("Edge Work", "incMelee", 0.025),  ("Keen Edge", "incMelee", 0.03),   ("Swordsman", "incMelee", 0.04)],
        [("Footwork", "incAtk", 0.025),     ("Quickstep", "incAtk", 0.03),     ("Sidestep", "speed", 0.03)],
        [("Parry", "armour", 2),            ("Bracing", "armour", 3),          ("Guard Stance", "fireRes", 0.04)],
        [("Opening", "incCrit", 0.035),     ("First Blood", "bleedChance", 0.03),("Cruel Edge", "critMult", 0.08)]],
      ["mace"] = [
        [("Heft", "slamDmg", 0.3),          ("Overhead", "slamDmg", 0.4),      ("Wide Fault", "slamRange", 0.06)],
        [("Set Stance", "armour", 2),       ("Plated", "incArmour", 0.05),     ("Read the Room", "suppress", 0.025)],
        [("Thick Skin", "incHp", 0.025),    ("Hale", "incHp", 0.03),           ("Knitting", "incRegen", 0.10)],
        [("Follow Up", "incMelee", 0.025),  ("Momentum", "incMelee", 0.03),    ("Heavy Hands", "incAtk", 0.025)]],
      ["axe"] = [
        [("Quick Hands", "incAtk", 0.04),   ("Flurry", "incAtk", 0.04),        ("Light Feet", "speed", 0.04)],
        [("Openings", "incCrit", 0.04),     ("Precision", "incCrit", 0.05),    ("Savagery", "critMult", 0.10)],
        [("Bloodletting", "bleedChance", 0.04),("Gutting", "bleedShare", 0.03),("Septic", "incDot", 0.05)],
        [("Surefoot", "speed", 0.03),       ("Vaulting", "roll", 0.10),        ("Warpaint", "frostRes", 0.05)]],
      ["bow"] = [
        [("Draw", "incMelee", 0.03),        ("Full Draw", "incMelee", 0.035),  ("Barbed", "arrowFlat", 0.5)],
        [("Nock", "incAtk", 0.03),          ("Quick Nock", "incAtk", 0.035),   ("Fleet", "speed", 0.03)],
        [("Sighted", "acc", 30),            ("Steady", "incCrit", 0.04),       ("Marked", "critMult", 0.08)],
        [("Light Step", "eva", 40),         ("Slip", "incEva", 0.08),          ("Quiver", "arrowPierce", 1)]],
      ["scepter"] = [
        [("Bone Craft", "minionDmg", 0.04), ("Grave Might", "minionDmg", 0.05),("Marrow", "minionLife", 0.06)],
        [("Dark Pact", "incSpell", 0.03),   ("Incantation", "incCast", 0.04),  ("Deathless", "minionLife", 0.06)],
        [("Command", "minionSpd", 0.04),    ("Drill", "minionSpd", 0.05),      ("Muster", "minionDmg", 0.05)],
        [("Ward of Bone", "es", 3),         ("Grave Ward", "incEs", 0.06),     ("Scholar", "int", 5)]],
      ["staff"] = [
        [("First Words", "incSpell", 0.03), ("Second Words", "incSpell", 0.04),("Cadence", "incCast", 0.04)],
        [("Kindle", "incElem", 0.03),       ("Smoulder", "incFire", 0.05),     ("Charged", "incElem", 0.045)],
        [("Clear Mind", "incCast", 0.04),   ("Widening", "boltR", 0.06),       ("Resonant", "incSpell", 0.03)],
        [("Wards", "allRes", 0.03),         ("Attunement", "allRes", 0.03),    ("Bookish", "int", 5)]],
      ["_"] = [
        [("First Steps", "incAll", 0.02),   ("Onward", "incAll", 0.02),        ("Grip", "incAtk", 0.03)],
        [("Footing", "incHp", 0.03),        ("Rooted", "incHp", 0.03),         ("Regrowth", "incRegen", 0.10)],
        [("Guard", "armour", 3),            ("Braced", "armour", 4),           ("Warded", "allRes", 0.03)],
        [("Reach", "reach", 4),             ("Follow", "speed", 0.03),         ("Focus", "incCrit", 0.04)]],
    };
    public static readonly List<MetaStart> META_STARTS = new();
    public static List<string> META_TREE_UNPLACED = new();
    /* ---- THE SMALL GROUPS: ONE OR TWO KINDS OF STAT and nothing else ---- */
    public static readonly List<MetaTpl> META_GROUPS = [
      /* ============ RED: physical, hearts, armour, regeneration ============ */
      /* RED PAYS FOR THE DENSITY. These two are the cheapest damage in the game --
         they sit against the red door, they are INSTANCED several times around the
         ring, and flat physical on a weapon whose base is 1 is a multiplier wearing
         a disguise. Fifteen new clusters shortened every route on the map, which
         bought a stay-at-home red build 7% more power for the same sixty points and
         took it over the declared ceiling. The ceiling is the promise; these are
         the dial. */
      new MetaTpl { id = "whet",  name = "WHETSTONE",   sp = "war", home = "str", d = [0,3], shape = "ring",
        minors = [("Whetstone", "incMelee", 0.031),("Honing", "incMelee", 0.026),("Grindstone", "incMelee", 0.021)] },
      new MetaTpl { id = "heft",  name = "HEFT",        sp = "war", home = "str", d = [0,2], shape = "chain",
        minors = [("Heft", "incMelee", 0.042),("Weight", "incMelee", 0.032)] },
      new MetaTpl { id = "plate", name = "PLATING",     sp = "arm", home = "str", d = [0,3], shape = "ring",
        minors = [("Plated", "armour", 3),("Riveted", "incArmour", 0.06),("Banded", "armour", 3)] },
      new MetaTpl { id = "hale",  name = "HALE",        sp = "vit", home = "str", d = [0,3], shape = "ring",
        minors = [("Hale", "incHp", 0.035),("Sturdy", "incHp", 0.03),("Thickset", "incHp", 0.025)] },
      new MetaTpl { id = "mend",  name = "MENDING",     sp = "vit", home = "str", d = [1,4], shape = "arc",
        minors = [("Mending", "incRegen", 0.11),("Recovery", "incRegen", 0.09),("Knitting", "incRegen", 0.07)] },
      new MetaTpl { id = "quench",name = "QUENCHED",    sp = "arm", home = "str", d = [1,4], shape = "chain",
        minors = [("Quenched", "fireRes", 0.06),("Rimeplate", "frostRes", 0.06)] },
      new MetaTpl { id = "ground",name = "GROUNDED",    sp = "arm", home = "str", d = [1,4], shape = "chain",
        minors = [("Grounded", "shockRes", 0.06),("Bracing", "suppress", 0.04)] },
      new MetaTpl { id = "warcry",name = "WARCRY",      sp = "war", home = "str", d = [1,4], shape = "fan",
        minors = [("Overhead", "incAtk", 0.035),("Follow Through", "incMelee", 0.03),("Bellow", "incAtk", 0.025)] },
      new MetaTpl { id = "gore",  name = "GORE",        sp = "war", home = "str", d = [1,4], shape = "ring",
        minors = [("Serrated", "bleedChance", 0.05),("Gutting", "bleedShare", 0.04),("Rending", "bleedChance", 0.04)] },
      new MetaTpl { id = "drink", name = "BLOODBIND",   sp = "vit", home = "str", d = [2,4], shape = "chain",
        minors = [("Bloodscent", "leechRate", 0.05),("Draught", "leech", 0.015)] },
      new MetaTpl { id = "stone", name = "STONESKIN",   sp = "arm", home = "str", d = [2,4], shape = "arc",
        minors = [("Adamant", "incArmour", 0.10),("Anchored", "armour", 6),("Layered", "allRes", 0.04)] },
      new MetaTpl { id = "faults",name = "THE FAULT",   sp = "arm", home = "str", d = [2,4], shape = "chain",
        minors = [("Deadweight", "slamDmg", 0.5),("Wide Fault", "slamArc", 6)] },
      /* ============ GREEN: criticals, speed, bleed, poison, loot ============ */
      new MetaTpl { id = "keen",  name = "KEEN EDGE",   sp = "prec", home = "dex", d = [0,3], shape = "ring",
        minors = [("Keen Edge", "incCrit", 0.04),("Precision", "incCrit", 0.04),("Sharpening", "incCrit", 0.03)] },
      new MetaTpl { id = "cruel", name = "CRUELTY",     sp = "prec", home = "dex", d = [1,4], shape = "chain",
        minors = [("Cruelty", "critMult", 0.08),("Savagery", "critMult", 0.07)] },
      new MetaTpl { id = "fleet", name = "FLEET",       sp = "swift",home = "dex", d = [0,3], shape = "chain",
        minors = [("Fleet", "speed", 0.04),("Vaulting", "roll", 0.11)] },
      /* ---- RAGE ON THE MAP. Red country: it is a melee ramp built out of being
         hit and hitting back, which is what strength is for. Two groups of
         generation, then the ceiling — and the deep well is the notable, because a
         well you cannot fill is worth nothing. ---- */
      new MetaTpl { id = "warpath",name = "WARPATH",    sp = "war", home = "str", d = [1,3], shape = "chain", must = true,
        minors = [("Warpath", "rageHit", 0.2),("Follow Through", "rageHit", 0.2),("Bloodlust", "rageHit", 0.2)] },
      new MetaTpl { id = "spite",  name = "SPITE",      sp = "vit", home = "str", d = [1,4], shape = "chain", must = true,
        minors = [("Spite", "rageHurt", 0.2),("Grudge", "rageHurt", 0.2),("Scar", "rageHurt", 0.2)] },
      new MetaTpl { id = "wellrg", name = "THE DEEP WELL",sp = "war",home = "str",d = [2,4], shape = "arc", must = true,
        minors = [("Deeper", "rageMax", 2),("Deeper Still", "rageMax", 2),("Wider", "rageHit", 0.2)] },
      new MetaTpl { id = "quick", name = "QUICKDRAW",   sp = "swift",home = "dex", d = [0,3], shape = "ring",
        minors = [("Quick Hands", "incAtk", 0.04),("Wild Swing", "incAtk", 0.035),("Reflexes", "incAtk", 0.03)] },
      new MetaTpl { id = "venom", name = "VENOM",       sp = "aff", home = "dex", d = [1,4], shape = "ring",
        minors = [("Virulent", "poisonDot", 0.25),("Lingering", "incDot", 0.05),("Septic", "poisonDot", 0.20)] },
      new MetaTpl { id = "rot",   name = "ROT",         sp = "aff", home = "dex", d = [2,4], shape = "chain",
        minors = [("Pestilent", "incDot", 0.06),("Sour", "incChaos", 0.07)] },
      new MetaTpl { id = "lace",  name = "LACERATE",    sp = "aff", home = "dex", d = [1,4], shape = "fan",
        minors = [("Lacerate", "bleedChance", 0.06),("Bleeding Out", "bleedShare", 0.05),("Scoring", "bleedShare", 0.04)] },
      new MetaTpl { id = "purse", name = "THE PURSE",   sp = "swift",home = "dex", d = [0,2], shape = "chain",
        minors = [("Prospector", "studs", 0.08),("Magnetic", "magnet", 0.10)] },
      new MetaTpl { id = "temper",name = "TEMPERED",    sp = "prec", home = "dex", d = [1,4], shape = "chain",
        minors = [("Cold Nerve", "frostRes", 0.06),("Surefoot", "shockRes", 0.06)] },
      new MetaTpl { id = "hunt",  name = "BLOODHUNT",   sp = "prec", home = "dex", d = [2,4], shape = "arc",
        minors = [("Hawk-eyed", "incCrit", 0.06),("Merciless", "critMult", 0.12),("Unerring", "incCrit", 0.05)] },
      new MetaTpl { id = "inoc",  name = "INOCULATION", sp = "aff", home = "dex", d = [2,4], shape = "chain",
        minors = [("Sour Blood", "chaosRes", 0.06),("Inured", "chaosRes", 0.05)] },
      /* ============ BLUE: spells, the elements, cast speed ============ */
      /* THE LEGION IN BLUE. A summoner walks intelligence's country for its spells
         and its shield; these are the small stops along the way that feed the
         skeletons instead. Scepter-only stats -- worth nothing to anyone else. */
      new MetaTpl { id = "bones", name = "BONEWORK",    sp = "arc", home = "int", d = [0,3], shape = "chain", must = true,
        minors = [("Bonework", "minionDmg", 0.05),("Marrow", "minionLife", 0.06)] },
      new MetaTpl { id = "grave", name = "GRAVECALL",   sp = "arc", home = "int", d = [1,4], shape = "chain", must = true,
        minors = [("Gravecall", "minionDmg", 0.06),("Drill", "minionSpd", 0.04),("Deathless", "minionLife", 0.08)] },
      new MetaTpl { id = "focus", name = "FOCUS",       sp = "arc", home = "int", d = [0,3], shape = "ring",
        minors = [("Focus", "incSpell", 0.035),("Study", "incSpell", 0.03),("Resonant", "incSpell", 0.025)] },
      new MetaTpl { id = "cad",   name = "CADENCE",     sp = "arc", home = "int", d = [0,3], shape = "chain",
        minors = [("Cadence", "incCast", 0.05),("Metronome", "incCast", 0.04)] },
      new MetaTpl { id = "pyro",  name = "PYROMANCY",   sp = "elem",home = "int", d = [1,4], shape = "chain",
        minors = [("Smoulder", "incFire", 0.055),("Pyre", "incFire", 0.05)] },
      new MetaTpl { id = "rime",  name = "RIME",        sp = "elem",home = "int", d = [1,4], shape = "chain",
        minors = [("Rimebite", "incFrost", 0.055),("Hoar", "incFrost", 0.05)] },
      new MetaTpl { id = "tempe", name = "TEMPEST",     sp = "elem",home = "int", d = [1,4], shape = "chain",
        minors = [("Arcing", "incShock", 0.055),("Conduction", "incShock", 0.05)] },
      new MetaTpl { id = "prism", name = "PRISM",       sp = "elem",home = "int", d = [0,3], shape = "ring",
        minors = [("Prismatic", "incElem", 0.04),("Convergent", "incElem", 0.035),("Tinder", "incElem", 0.03)] },
      new MetaTpl { id = "ward",  name = "WARDING",     sp = "arc", home = "int", d = [0,4], shape = "ring",
        minors = [("Warding", "allRes", 0.03),("Attunement", "allRes", 0.03),("Sealed", "allRes", 0.03)] },
      new MetaTpl { id = "kindle",name = "KINDLING",    sp = "elem",home = "int", d = [0,2], shape = "chain",
        minors = [("Kindling", "incElem", 0.06),("Charged", "incElem", 0.045)] },
      new MetaTpl { id = "embw",  name = "THE EMBER WELL", sp = "elem", home = "int", d = [1,4], shape = "chain",
        minors = [("Emberskin", "fireRes", 0.06),("Firebrand", "incFire", 0.06)] },
      new MetaTpl { id = "frow",  name = "THE FROST WELL", sp = "elem", home = "int", d = [1,4], shape = "chain",
        minors = [("Frostskin", "frostRes", 0.06),("Frostbrand", "incFrost", 0.06)] },
      new MetaTpl { id = "stow",  name = "THE STORM WELL", sp = "elem", home = "int", d = [1,4], shape = "chain",
        minors = [("Stormskin", "shockRes", 0.06),("Stormbrand", "incShock", 0.06)] },
      new MetaTpl { id = "wide",  name = "WIDENING",    sp = "arc", home = "int", d = [1,4], shape = "chain",
        minors = [("Widening", "boltR", 0.10),("Harmonic", "incSpell", 0.04)] },
    ];
    /* ---- THE CONVERSION GROUPS: one per legal pair, both forms in the same cluster, generated ---- */
    public static readonly Dictionary<string, string> CONV_GROUP_NAME = new()
    {
        ["phys>shock"] = "THE LIVE WIRE", ["phys>frost"] = "THE RIMEBLADE", ["phys>fire"] = "THE FORGE-EDGE",
        ["phys>chaos"] = "CREEPING BLIGHT", ["shock>frost"] = "STORMFROST", ["shock>fire"] = "THE ARC-FLAME",
        ["shock>chaos"] = "VOIDARC", ["frost>fire"] = "FULMINATE", ["frost>chaos"] = "ROTGALE",
        ["fire>chaos"] = "THE UNMAKING",
    };
    /* the JS appends these to META_GROUPS at load, after the literal: run once from boot() */
    public static void MetaGenTemplates()
    {
        foreach (var a in CONV_ORDER) foreach (var b in CONV_ORDER)
            {
                if (!convLegal(a, b)) continue;
                string key = a + ">" + b;
                string nm = CONV_NAME[b];
                META_GROUPS.Add(new MetaTpl
                {
                    id = "cv_" + a + "_" + b, name = CONV_GROUP_NAME.GetValueOrDefault(key) ?? (CONV_NAME[a] + " TO " + CONV_NAME[b]),
                    sp = b == "chaos" ? "aff" : "elem",
                    home = b == "chaos" ? "dex" : a == "phys" ? "str" : "int",
                    d = new double[] { 1, 4 }, shape = "chain", must = true,
                    minors = [(nm.Substring(0, 1) + nm.Substring(1).ToLowerInvariant() + "brand", "cv_" + a + "_" + b, 0.10),
                              ("Bleed-over", "gn_" + a + "_" + b, 0.06)]
                });
            }
        /* ---- AND ONE GROUP PER WEAPON ---- */
        foreach (var wk in WEAPON_KEYS)
        {
            var W0 = WEAPONS.GetValueOrDefault(wk); string nm = (W0?.name ?? wk).ToUpperInvariant();
            META_GROUPS.Add(new MetaTpl
            {
                id = "w_" + wk, name = "THE " + nm + "'S OATH", sp = "core",
                home = META_HOME.GetValueOrDefault(wk) ?? "str", d = new double[] { 0, 3 }, shape = "fan", only = wk, must = true,
                minors = [("Grip", "inc_" + wk, 0.075), ("Practice", "inc_" + wk, 0.04), ("Drill", "inc_" + wk, 0.03)]
            });
        }
        /* ---- THE NEW GROUND'S OWN GROUPS ---- */
    META_GROUPS.AddRange(new MetaTpl[] {
      new MetaTpl { id = "l_sanct",  name = "SANCTUM",     sp = "arc",  home = "int", d = [1,4], shape = "ring", lateOnly = true,
        minors = [("Sanctum", "es", 3),("Hallowed", "incEs", 0.06),("Sanctified", "es", 3)] },
      new MetaTpl { id = "l_clar",   name = "CLARITY",     sp = "arc",  home = "int", d = [1,4], shape = "arc", lateOnly = true,
        minors = [("Clarity", "int", 5),("Calm Mind", "incRegen", 0.10),("Lucid", "int", 5)] },
      new MetaTpl { id = "l_sward",  name = "SPELLWARD",   sp = "elem", home = "int", d = [1,4], shape = "diamond", lateOnly = true,
        minors = [("Spellward", "suppress", 0.03),("Counterspell", "allRes", 0.03),("Nullify", "suppress", 0.03)] },
      new MetaTpl { id = "l_soul",   name = "SOULBOUND",   sp = "arc",  home = "int", d = [1,4], shape = "hook", lateOnly = true,
        minors = [("Soulbound", "minionLife", 0.06),("Tethered", "minionLife", 0.05),("Grave Sense", "es", 3)] },
      new MetaTpl { id = "l_arch",   name = "THE ARCHIVE", sp = "arc",  home = "int", d = [1,4], shape = "line", lateOnly = true,
        minors = [("Archive", "xp", 0.04),("Index", "int", 5),("Marginalia", "magnet", 0.08)] },
      new MetaTpl { id = "l_cold",   name = "COLD HEART",  sp = "elem", home = "int", d = [1,4], shape = "cresc", lateOnly = true,
        minors = [("Cold Heart", "frostRes", 0.05),("Numb", "chaosRes", 0.04),("Rimeward", "frostRes", 0.05)] },
      new MetaTpl { id = "l_blur",   name = "THE BLUR",    sp = "swift",home = "dex", d = [1,4], shape = "arc", lateOnly = true,
        minors = [("Blur", "eva", 40),("Phase", "incEva", 0.08),("Afterimage", "eva", 40)] },
      new MetaTpl { id = "l_wind",   name = "WINDRUNNER",  sp = "swift",home = "dex", d = [1,4], shape = "line", lateOnly = true,
        minors = [("Windrunner", "speed", 0.03),("Tumble", "roll", 0.08),("Tailwind", "speed", 0.03)] },
      new MetaTpl { id = "l_iron",   name = "IRON WILL",   sp = "arm",  home = "str", d = [1,4], shape = "ring", lateOnly = true,
        minors = [("Iron Will", "armour", 4),("Hardy", "incHp", 0.03),("Bulwark", "armour", 4)] } });
        /* the doors, one per weapon */
        META_DOORS = WEAPON_KEYS.Select(wk => new MetaDoor { id = wk, weap = wk, home = META_HOME.GetValueOrDefault(wk) ?? "str",
            name = "THE " + (WEAPONS.GetValueOrDefault(wk)?.name ?? wk).ToUpperInvariant() }).ToList();
        /* ---- THE LEGENDARIES: generated from SPELL_KEYS ---- */
        META_LEGENDS = SPELL_KEYS.Select((k, i) => new MetaTpl
        {
            id = "l_" + k, name = "THE " + (SPELL_NAME.GetValueOrDefault(k) ?? k).ToUpperInvariant(), sp = "core",
            home = new[] { "str", "dex", "int" }[i % 3], d = new double[] { 4, 4 }, shape = "fan", r = 120,
            minors = [("Resolve", "incAll", 0.03), ("Patience", "incHp", 0.03)],
            legend = new MetaPrize { name = "Born With " + (SPELL_NAME.GetValueOrDefault(k) ?? k), cost = 5,
                note = "you begin every run already holding it", mods = [new Mod("grant", k)] },
        }).ToList();
    }
    /* ---- THE NOTABLES. One of each, ever. LATE ARRIVALS go on ground the map left empty ---- */
    public static readonly List<MetaTpl> META_LATE = [
      /* the dead that were zombies, raised for you */
      new MetaTpl { id = "n_grave", name = "THE GRAVEYARD", sp = "arc", home = "int", d = [2,3], shape = "ring",
        spot = new MetaSpot { ring = 2, rad = 4050, deg = 262 },        /* the widest clear ground left in blue */
        minors = [("Grave Soil", "minionLife", 0.08),("Rot", "minionDmg", 0.06),("Shamble", "minionSpd", 0.04)],
        notable = new MetaPrize { name = "Raise Zombie", mods = [new Mod("minionZombies", null),new Mod("minionLife", 0.08)] } },
      /* one more golem: the third kind, for a summoner who walks out to it */
      new MetaTpl { id = "n_foundry", name = "THE FOUNDRY", sp = "arc", home = "int", d = [2,3], shape = "arc",
        spot = new MetaSpot { ring = 2, rad = 4500, deg = 246 },        /* the next clear ground in blue */
        minors = [("Clay", "minionLife", 0.08),("Kiln", "minionDmg", 0.06),("Mould", "minionLife", 0.06)],
        notable = new MetaPrize { name = "Golem Commander", note = "room for one more golem -- which one, a golem brick decides",
                  mods = [new Mod("golemCap", 1),new Mod("minionDmg", 0.06)] } },
      /* THE ARCHER'S GROUND. The bow's numbers were three minors scattered across
         the green -- no arrow anywhere, hardly any pierce. Three clusters of its own:
         one more arrow near the door, pierce on the way out, and one more arrow
         again at the far edge for a price. */
      new MetaTpl { id = "n_fletch", name = "THE FLETCHERY",  sp = "prec", home = "dex", d = [1,3], shape = "fan",
        after = true, spot = new MetaSpot { ring = 1, rad = 1400, deg = 16 },         /* clear ground a short walk off the green door */
        minors = [("Split Nock", "arrowFlat", 0.4),("Broadhead", "arrowPierce", 1),("Quick Nock", "incAtk", 0.04)],
        notable = new MetaPrize { name = "Volley", note = "one more arrow on every shot", mods = [new Mod("arrowN", 1),new Mod("incAtk", 0.04)] } },
      new MetaTpl { id = "n_skewer", name = "THE SKEWER",     sp = "swift",home = "dex", d = [2,3], shape = "chain",
        after = true, spot = new MetaSpot { ring = 2, rad = 3000, deg = 70 },         /* the widest gap in green's middle ring */
        minors = [("Bodkin", "arrowPierce", 1),("Fletching", "arrowFlat", 0.4),("Draw Weight", "incMelee", 0.05)],
        notable = new MetaPrize { name = "Skewer", note = "every arrow goes through two more", mods = [new Mod("arrowPierce", 2),new Mod("arrowFlat", 0.5)] } },
      new MetaTpl { id = "n_barrage",name = "THE BARRAGE",    sp = "prec", home = "dex", d = [2,4], shape = "arc",
        after = true, spot = new MetaSpot { ring = 3, rad = 5000, deg = 18 },         /* the widest clear ground left in green, far out */
        minors = [("Full Quiver", "arrowPierce", 1),("Barbed", "arrowFlat", 0.5),("Loose", "incAtk", 0.05)],
        notable = new MetaPrize { name = "Barrage", note = "one more arrow, each a little lighter", mods = [new Mod("arrowN", 1),new Mod("arrowLess", 0.08)] } },
      /* THE WEAPONS' OWN SKILLS. Not "+1 to the axe" but the thing each weapon DOES:
         the sword's whirl and its blades, the axe's thrown reaper and its wounds,
         the mace's fault, the bow's two elemental arrows -- and, for every hit that
         carries an element, a way through resistance. */
      new MetaTpl { id = "n_dervish", name = "THE DERVISH",    sp = "war",  home = "str", d = [2,3], shape = "ring",
        after = true, spot = new MetaSpot { ring = 1, rad = 2700, deg = 224 },   /* clear ground in red's inner quads */
        minors = [("Whirling Edge", "whirlShare", 0.05),("Storm Edge", "stormDmg", 1),("Vortex Edge", "vortexDmg", 1)],
        notable = new MetaPrize { name = "Dervish", note = "the whirl bites deeper, and the blades fly faster", mods = [new Mod("whirlShare", 0.15),new Mod("stormRate", 0.12),new Mod("vortexRate", 0.12)] } },
      new MetaTpl { id = "n_reaper", name = "THE REAPER",      sp = "swift",home = "dex", d = [2,3], shape = "arc",
        after = true, spot = new MetaSpot { ring = 1, rad = 2900, deg = 42 },    /* clear ground in green's inner quads */
        minors = [("Hurl", "reaveShare", 0.05),("Gutting", "bleedShare", 0.04),("Lacerate", "bleedChance", 0.05)],
        notable = new MetaPrize { name = "Reaper", note = "the thrown axe reaps one body further, and harder", mods = [new Mod("reaveBounce", 1),new Mod("reaveShare", 0.10)] } },
      new MetaTpl { id = "n_quake", name = "THE EARTHSHAKER",  sp = "war",  home = "str", d = [2,3], shape = "chain",
        after = true, spot = new MetaSpot { ring = 1, rad = 2500, deg = 124 },   /* red's other clear ground */
        minors = [("Tremor", "slamDmg", 0.5),("Fault", "slamArc", 8),("Rumble", "slamRange", 0.06)],
        notable = new MetaPrize { name = "Earthshaker", note = "a wider, longer fault, and every tooth of it heavier", mods = [new Mod("slamDmg", 1),new Mod("slamArc", 15),new Mod("slamRange", 0.10)] } },
      new MetaTpl { id = "n_tempest", name = "THE TEMPEST QUIVER", sp = "prec", home = "dex", d = [2,3], shape = "fan",
        after = true, spot = new MetaSpot { ring = 2, rad = 4300, deg = 86 },   /* green's outer clear ground */
        minors = [("Stormhead", "arrowBlast", 0.10),("Ricochet", "ricochet", 0.05),("Fletching", "arrowFlat", 0.4)],
        notable = new MetaPrize { name = "Tempest", note = "Lightning Arrow forks once more; Fire Arrow bursts wider", mods = [new Mod("arrowFork", 1),new Mod("arrowBlast", 0.25),new Mod("ricochet", 0.10)] } },
      new MetaTpl { id = "n_sunder", name = "THE SUNDERING",   sp = "elem", home = "int", d = [2,3], shape = "ring",
        after = true, spot = new MetaSpot { ring = 1, rad = 2500, deg = 320 },   /* blue's inner clear ground */
        minors = [("Searing", "penFire", 0.04),("Biting", "penFrost", 0.04),("Arcing", "penShock", 0.04)],
        notable = new MetaPrize { name = "Sundering", note = "your hits go through every elemental resistance", mods = [new Mod("penElem", 0.10),new Mod("incElem", 0.30)] } },
    ];
    public static readonly List<MetaTpl> META_NOTABLES = [
      /* red */
      new MetaTpl { id = "n_sword", name = "THE SWORN BLADE", sp = "war", home = "str", d = [1,2], shape = "wheel",
        minors = [("Riposte", "incCrit", 0.05),("Guard Stance", "fireRes", 0.05),("Drill", "incMelee", 0.04)],
        notable = new MetaPrize { name = "Swordsman", mods = [new Mod("incMelee", 0.07),new Mod("reach", 6)] } },
      new MetaTpl { id = "n_war",   name = "FURY",         sp = "war", home = "str", d = [2,3], shape = "ring",
        minors = [("Warlust", "incAll", 0.03),("Brutality", "incMelee", 0.05),("Ironhide", "fireRes", 0.06)],
        notable = new MetaPrize { name = "Warmonger", mods = [new Mod("incMelee", 0.08),new Mod("incAtk", 0.04)] } },
      /* the far end of the rage road: the ceiling you walk all that way for */
      new MetaTpl { id = "n_rage",  name = "THE RED MIST", sp = "war", home = "str", d = [3,4], shape = "cresc",
        minors = [("Seething", "rageHit", 0.2),("Simmering", "rageHurt", 0.2),("Boiling", "rageMax", 2)],
        notable = new MetaPrize { name = "The Red Mist", note = "a deeper well, and more reason to fill it",
                  mods = [new Mod("rageMax", 5),new Mod("rageHit", 0.2),new Mod("rageHurt", 0.2)] } },
      new MetaTpl { id = "n_break", name = "THE BREAKER",   sp = "arm", home = "str", d = [1,2], shape = "wheel",
        minors = [("Shockplate", "shockRes", 0.05),("Braced", "armour", 4),("Deadweight", "slamDmg", 0.5)],
        notable = new MetaPrize { name = "Breaker", mods = [new Mod("slamRange", 0.12),new Mod("armour", 5)] } },
      new MetaTpl { id = "n_forge", name = "THE FORGE",     sp = "arm", home = "str", d = [2,4], shape = "arc",
        minors = [("Tempered", "incArmour", 0.09),("Banded", "armour", 6),("Bracing", "suppress", 0.04)],
        notable = new MetaPrize { name = "Stonecutter", mods = [new Mod("incArmour", 0.12),new Mod("suppress", 0.05)] } },
      new MetaTpl { id = "n_watch", name = "THE WATCH",     sp = "vit", home = "str", d = [2,4], shape = "ring",
        minors = [("Stoic", "incHp", 0.04),("Second Wind", "incRegen", 0.14),("Bulwark", "armour", 5)],
        notable = new MetaPrize { name = "Warden", mods = [new Mod("incHp", 0.08),new Mod("incRegen", 0.22)] } },
      new MetaTpl { id = "n_hearth",name = "THE HEARTH",    sp = "vit", home = "str", d = [1,3], shape = "fan",
        minors = [("Hale", "incHp", 0.05),("Padded", "heartDrop", 0.10),("Knitting", "incRegen", 0.10)],
        notable = new MetaPrize { name = "Hearthkeeper", mods = [new Mod("incHp", 0.06),new Mod("heartDrop", 0.20)] } },
      new MetaTpl { id = "n_arm",   name = "THE ARMOURY",   sp = "war", home = "str", d = [1,3], shape = "ring",
        minors = [("Heft", "incMelee", 0.055),("Plated", "armour", 3),("Whetstone", "incMelee", 0.04)],
        notable = new MetaPrize { name = "Quartermaster", mods = [new Mod("flatPhys", 0.2),new Mod("armour", 4)] } },
      /* green */
      new MetaTpl { id = "n_axe",   name = "THE BERSERKER", sp = "swift",home = "dex", d = [1,2], shape = "wheel",
        minors = [("Wild Swing", "incAtk", 0.05),("Bloodscent", "leechRate", 0.05),("Warpaint", "frostRes", 0.05)],
        notable = new MetaPrize { name = "Berserker", mods = [new Mod("incAtk", 0.07),new Mod("leech", 0.02)] } },
      new MetaTpl { id = "n_mark",  name = "THE MARK",      sp = "prec", home = "dex", d = [2,4], shape = "ring",
        minors = [("Unerring", "incCrit", 0.07),("Savagery", "critMult", 0.14),("Cold Nerve", "frostRes", 0.06)],
        notable = new MetaPrize { name = "Assassin", mods = [new Mod("incCrit", 0.15),new Mod("critMult", 0.20)] } },
      new MetaTpl { id = "n_stride",name = "THE LONG STRIDE",sp = "swift",home = "dex",d = [2,4], shape = "chain",
        minors = [("Fleet", "speed", 0.05),("Vaulting", "roll", 0.12),("Surefoot", "shockRes", 0.06)],
        notable = new MetaPrize { name = "Outrider", mods = [new Mod("speed", 0.07),new Mod("roll", 0.15)] } },
      new MetaTpl { id = "n_wound", name = "OPEN WOUNDS",   sp = "aff", home = "dex", d = [2,4], shape = "fan",
        minors = [("Lacerate", "bleedChance", 0.07),("Septic", "poisonDot", 0.20),("Scar Tissue", "allRes", 0.03)],
        notable = new MetaPrize { name = "Plaguebearer", mods = [new Mod("incDot", 0.12),new Mod("poisonDot", 0.4)] } },
      new MetaTpl { id = "n_sight", name = "THE LONG SIGHT",sp = "prec", home = "dex", d = [1,3], shape = "arc",
        minors = [("Precision", "incCrit", 0.06),("Executioner", "critMult", 0.12),("Longsword", "reach", 8)],
        notable = new MetaPrize { name = "Duellist", mods = [new Mod("incCrit", 0.09),new Mod("incAtk", 0.04)] } },
      new MetaTpl { id = "n_courier",name = "THE COURIER",  sp = "swift",home = "dex", d = [0,2], shape = "chain",
        minors = [("Nimble", "speed", 0.04),("Magnetic", "magnet", 0.10),("Prospector", "studs", 0.08)],
        notable = new MetaPrize { name = "Courier", mods = [new Mod("speed", 0.05),new Mod("studs", 0.15)] } },
      new MetaTpl { id = "n_blight",name = "THE BLIGHT",    sp = "aff", home = "dex", d = [1,3], shape = "ring",
        minors = [("Virulent", "poisonDot", 0.25),("Gutting", "bleedShare", 0.04),("Sour", "incChaos", 0.06)],
        notable = new MetaPrize { name = "Blightbringer", mods = [new Mod("incChaos", 0.12),new Mod("poisonDot", 0.3)] } },
      /* blue */
      new MetaTpl { id = "n_staff", name = "THE ADEPT",     sp = "arc", home = "int", d = [1,2], shape = "wheel",
        minors = [("Warding", "allRes", 0.03),("Quick Words", "incCast", 0.04),("Kindling", "incElem", 0.04)],
        notable = new MetaPrize { name = "Adept", mods = [new Mod("incSpell", 0.08),new Mod("boltR", 0.08)] } },
      new MetaTpl { id = "n_crypt", name = "THE CRYPT",     sp = "arc", home = "int", d = [1,3], shape = "ring",
        minors = [("Crypt Dust", "minionLife", 0.08),("Grave Might", "minionDmg", 0.06),("Dark Pact", "incSpell", 0.04)],
        notable = new MetaPrize { name = "Crypt Keeper", mods = [new Mod("minionLife", 0.15),new Mod("minionSpd", 0.06)] } },
      /* the legion's own multiple projectiles, on the tree */
      new MetaTpl { id = "n_volley", name = "THE VOLLEY",   sp = "arc", home = "int", d = [2,3], shape = "arc",
        minors = [("Fletched Bone", "minionDmg", 0.06),("Quick Draw", "minionSpd", 0.05),("Grave Aim", "incSpell", 0.04)],
        notable = new MetaPrize { name = "Bone Rain", mods = [new Mod("minionProj", 2),new Mod("minionDmg", 0.06)] } },
      new MetaTpl { id = "n_chant", name = "THE CHANT",     sp = "arc", home = "int", d = [2,4], shape = "ring",
        minors = [("Harmonic", "incSpell", 0.05),("Cadence", "incCast", 0.05),("Attunement", "allRes", 0.03)],
        notable = new MetaPrize { name = "Archivist", mods = [new Mod("spellCap", 1),new Mod("incCast", 0.04)] } },
      new MetaTpl { id = "n_flux",  name = "FLUX",          sp = "elem",home = "int", d = [2,4], shape = "arc",
        minors = [("Prismatic", "incElem", 0.05),("Pyromancy", "incFire", 0.08),("Conduction", "incShock", 0.08)],
        notable = new MetaPrize { name = "Emberwright", mods = [new Mod("ember", 1),new Mod("incFire", 0.06)] } },
      new MetaTpl { id = "n_lib",   name = "THE LIBRARY",   sp = "arc", home = "int", d = [1,3], shape = "fan",
        minors = [("Resonant", "incSpell", 0.05),("Metronome", "incCast", 0.05),("Widening", "boltR", 0.10)],
        notable = new MetaPrize { name = "Librarian", mods = [new Mod("incSpell", 0.07),new Mod("incCast", 0.04)] } },
      new MetaTpl { id = "n_forgf", name = "THE FORGEFIRE", sp = "elem",home = "int", d = [1,3], shape = "ring",
        minors = [("Smoulder", "incFire", 0.07),("Rimebite", "incFrost", 0.07),("Arcing", "incShock", 0.07)],
        notable = new MetaPrize { name = "Elementalist", mods = [new Mod("incElem", 0.09),new Mod("allRes", 0.04)] } },
      new MetaTpl { id = "n_ember", name = "THE EMBER PATH",sp = "elem",home = "int", d = [0,2], shape = "chain",
        minors = [("Tinder", "incElem", 0.03),("Charged", "incElem", 0.06),("Ward", "allRes", 0.03)],
        notable = new MetaPrize { name = "Firewalker", mods = [new Mod("incFire", 0.10),new Mod("fireRes", 0.06)] } },

      /* ==================== THE CHARGES, ONE COUNTRY EACH ====================
         Three charges, three ways to earn them, and the three ways are the three
         countries: red is hit and keeps standing, green kills, blue picks its
         moment. Which charge a build runs on is decided by where it lives, which
         is the most a tree can say about a build without saying it out loud. */
      new MetaTpl { id = "n_endure",name = "THE UNBROKEN",  sp = "arm", home = "str", d = [2,3], shape = "ring",
        minors = [("Weathered", "armour", 5),("Set Jaw", "incHp", 0.04),("Dug In", "allRes", 0.03)],
        notable = new MetaPrize { name = "Unbroken", note = "what is thrown at you is what arms you",
                  mods = [new Mod("chargeHurt", 1),new Mod("armour", 6)] } },
      new MetaTpl { id = "n_frenzy",name = "THE BLOOD RUSH",sp = "swift",home = "dex",d = [2,3], shape = "cresc",
        minors = [("Quickblood", "incAtk", 0.04),("Hunting", "speed", 0.04),("Eager", "incCast", 0.04)],
        notable = new MetaPrize { name = "Blood Rush", note = "every body you put down makes the next one faster",
                  mods = [new Mod("chargeKill", 1),new Mod("incAtk", 0.05)] } },
      new MetaTpl { id = "n_power", name = "THE FOCUS",     sp = "prec",home = "dex", d = [2,3], shape = "wheel",
        minors = [("Steady", "incCrit", 0.06),("Patient", "critMult", 0.12),("Composed", "incAll", 0.03)],
        notable = new MetaPrize { name = "Focus", note = "a critical makes the next one likelier",
                  mods = [new Mod("chargeCrit", 1),new Mod("incCrit", 0.08)] } },
      /* ...and the one node that widens all three at once, out where it belongs */
      new MetaTpl { id = "n_res",   name = "THE RESERVOIR", sp = "core",home = "int", d = [3,4], shape = "ring",
        minors = [("Capacity", "incAll", 0.04),("Depth", "incHp", 0.04),("Reserve", "allRes", 0.03)],
        notable = new MetaPrize { name = "The Reservoir", note = "a deeper well for every charge you carry",
                  mods = [new Mod("chargeMax", 2)] } },

      /* ==================== EXPOSURE ====================
         Resistance taken OFF, which is the one kind of damage increase that does
         not live in your own pool -- so it is the thing a build already at its
         ceiling walks out here for. */
      /* ==================== THE SHIELD ====================
         Blue's, because intelligence is what increases it -- a caster who has been
         stacking INT for spell damage has been buying this all along and only finds
         out when there is somewhere to spend it. */
      new MetaTpl { id = "n_ward",  name = "THE WARDING",   sp = "arc", home = "int", d = [1,3], shape = "ring",
        minors = [("Warding", "es", 3),("Layered", "incEs", 0.12),("Reinforced", "es", 2.5)],
        notable = new MetaPrize { name = "Wardkeeper", note = "a second bar, and chaos is the only thing past it",
                  mods = [new Mod("es", 7),new Mod("incEs", 0.18)] } },
      new MetaTpl { id = "n_ward2", name = "THE RESERVOIR OF WILL", sp = "arc", home = "int", d = [2,4], shape = "arc",
        minors = [("Willed", "incEs", 0.14),("Studied", "int", 4),("Insulated", "es", 3)],
        notable = new MetaPrize { name = "Reservoir of Will", note = "what you learned is what stands in front of you",
                  mods = [new Mod("incEs", 0.30),new Mod("int", 6)] } },
      new MetaTpl { id = "n_expose",name = "THE UNMAKING",  sp = "elem",home = "int", d = [2,4], shape = "fan",
        minors = [("Etching", "exposeChance", 0.10),("Solvent", "incElem", 0.05),("Reagent", "incChaos", 0.06)],
        notable = new MetaPrize { name = "Sapper", note = "what you hit stops resisting what hit it",
                  mods = [new Mod("exposeChance", 0.25),new Mod("incElem", 0.06)] } },
      new MetaTpl { id = "n_strip", name = "THE STRIPPING", sp = "aff", home = "dex", d = [2,3], shape = "arc",
        minors = [("Corroding", "exposeChance", 0.08),("Souring", "incChaos", 0.06),("Wearing", "incDot", 0.05)],
        notable = new MetaPrize { name = "Corrosive", note = "the rot eats the armour before it eats the body",
                  mods = [new Mod("exposeChance", 0.20),new Mod("incChaos", 0.08)] } },

      /* ==================== HOW HARD AN AILMENT LANDS ====================
         A chill or a shock is now measured against the body it lands on, so the
         stat that matters is how much of the pool your hit counts for. This is the
         mace's road and the slow-heavy build's road. */
      /* NOTHING THAT MULTIPLIES A SWING IN HERE. Red holds two of the four weapons,
         which makes it the country the power budget is always tightest in — a
         stay-at-home red build was already at x2.55 against a declared x2.60 before
         this pass, and the first cut of these two nodes put it at x2.73. What red
         gets instead is WEIGHT: the same swing counting for more against the body
         it lands on, which is a thing you can only spend on ailments. */
      new MetaTpl { id = "n_mag",   name = "THE HAMMERFALL",sp = "war", home = "str", d = [2,4], shape = "arc",
        minors = [("Weighted", "ailMag", 0.20),("Settled", "incHp", 0.03),("Grounding", "shockRes", 0.05)],
        notable = new MetaPrize { name = "Hammerfall", note = "a smaller blow counts for more",
                  mods = [new Mod("ailMag", 0.55)] } },
      new MetaTpl { id = "n_deep",  name = "THE DEEP WINTER",sp = "elem",home = "int",d = [2,4], shape = "ring",
        minors = [("Biting", "ailMag", 0.18),("Rimed", "incFrost", 0.07),("Numbing", "ailChance", 0.05)],
        notable = new MetaPrize { name = "Deep Winter", note = "the cold and the storm both land heavier",
                  mods = [new Mod("ailMag", 0.40),new Mod("incFrost", 0.06),new Mod("incShock", 0.06)] } },

      /* ==================== AND TWO THAT WERE SIMPLY MISSING ====================
         Red had no node about ending a fight and green had none about surviving
         one, which is how a country reads as a one-note country. */
      /* ==================== THE CONTEST ====================
         Green's own, because dexterity already pays into both and a country that
         hands you accuracy and evasion is a country that reads as the quick one. */
      new MetaTpl { id = "n_acc",   name = "THE TRUE EDGE", sp = "prec",home = "dex", d = [1,3], shape = "arc",
        minors = [("Steady Hand", "acc", 60),("Sighting", "incAcc", 0.10),("Drilled", "acc", 50)],
        notable = new MetaPrize { name = "True Edge", note = "a swing that finds what it was aimed at",
                  mods = [new Mod("acc", 130),new Mod("incAcc", 0.15)] } },
      new MetaTpl { id = "n_eva",   name = "THE SHADOW STEP",sp = "swift",home = "dex", d = [2,3], shape = "ring",
        minors = [("Loose Footing", "eva", 90),("Sidestep", "incEva", 0.12),("Weaving", "eva", 80)],
        notable = new MetaPrize { name = "Shadow Step", note = "the swing that never arrives",
                  mods = [new Mod("eva", 220),new Mod("incEva", 0.20)] } },
      /* GREEN, NOT RED, and not for flavour: green sat at x1.68 total power against
         red's x2.55, so a damage notable added to red is a node that breaks a
         ceiling and the same node added to green is a node that fixes a gap. */
      new MetaTpl { id = "n_exec",  name = "THE HEADSMAN",  sp = "prec",home = "dex", d = [2,3], shape = "fan",
        minors = [("Heavy Hands", "incMelee", 0.055),("Follow Through", "incAtk", 0.04),("Weight Behind It", "ailMag", 0.20)],
        notable = new MetaPrize { name = "Headsman", note = "the whole weight of you behind one swing",
                  mods = [new Mod("critMult", 0.25),new Mod("ailMag", 0.25)] } },
      new MetaTpl { id = "n_quick", name = "THE SECOND SKIN",sp = "swift",home = "dex",d = [1,3], shape = "ring",
        minors = [("Loose", "speed", 0.04),("Reading", "suppress", 0.04),("Thickened", "incHp", 0.04)],
        notable = new MetaPrize { name = "Second Skin", note = "nothing cast at you lands properly",
                  mods = [new Mod("suppress", 0.07),new Mod("incHp", 0.06)] } },
    ];
    public static readonly List<MetaTpl> META_KEYSTONES = [
      new MetaTpl { id = "k_glass", name = "THE ORBIT",    sp = "arc", home = "int", d = [3,4], shape = "wheel",
        minors = [("Heavier Guard", "orbitDmg", 1),("Wider Guard", "orbitR", 20),("Swifter Guard", "orbitSpd", 0.12)],
        keystone = new MetaPrize { name = "Glass Bricks", note = "everything you own hits far harder, and you die to anything",
                   mods = [new Mod("incAll", 0.35),new Mod("moreHp", -0.30)] } },
      new MetaTpl { id = "k_perf",  name = "THE BARREL",   sp = "prec",home = "dex", d = [3,4], shape = "fan",
        minors = [("Hot Load", "arrowFlat", 0.4),("Rifling", "arrowPierce", 1),("Autoloader", "incAtk", 0.06)],
        keystone = new MetaPrize { name = "Perfect Strike", note = "every swing is a critical, and nothing else is",
                   mods = [new Mod("incCrit", 1.50),new Mod("critMult", -0.50)] } },
      new MetaTpl { id = "k_live",  name = "THE STORMHEAD",sp = "elem",home = "int", d = [3,4], shape = "arc",
        minors = [("Conductor", "chainDmg", 1),("Forked", "chainFork", 1),("Quickening", "chainRate", 0.12)],
        keystone = new MetaPrize { name = "Livewire", note = "the bolts come twice as fast, and nothing you do lingers",
                   mods = [new Mod("chainRate", 0.40),new Mod("incShock", 0.25),new Mod("incDot", -0.40)] } },
      new MetaTpl { id = "k_slow",  name = "THE WHIRLING", sp = "war", home = "str", d = [3,4], shape = "chain",
        minors = [("Heavier Blades", "stormDmg", 1),("Broader Blades", "stormSize", 0.3),("Faster Throw", "stormRate", 0.12)],
        keystone = new MetaPrize { name = "Slow Death", note = "everything lingering doubles; nothing lands hard",
                   mods = [new Mod("incDot", 0.60),new Mod("incAll", -0.25)] } },
      new MetaTpl { id = "k_iron",  name = "THE VORTEX",   sp = "arm", home = "str", d = [3,4], shape = "wheel",
        minors = [("Deeper Cut", "vortexDmg", 1),("Wider Whirl", "vortexR", 25),("Spinning Up", "vortexRate", 0.12)],
        keystone = new MetaPrize { name = "Ironclad Oath", note = "a wall that cannot run",
                   mods = [new Mod("incArmour", 0.50),new Mod("allRes", 0.10),new Mod("speed", -0.20)] } },
      new MetaTpl { id = "k_blood", name = "THE COLD SNAP",sp = "elem",home = "int", d = [3,4], shape = "fan",
        minors = [("Colder", "freezeR", 40),("Quicker", "freezeRate", 0.12),("Frostbite", "frostRes", 0.08)],
        keystone = new MetaPrize { name = "Bloodthirst", note = "you take it back by hitting, or not at all",
                   mods = [new Mod("leechRate", 0.20),new Mod("leech", 0.06),new Mod("noRegen", null)] } },
      new MetaTpl { id = "k_burn",  name = "THE ORCHARD",  sp = "aff", home = "dex", d = [3,4], shape = "ring",
        minors = [("Heavier Bane", "baneDmg", 1),("Wider Bane", "baneR", 0.12),("Quicker Bane", "baneRate", 0.12)],
        keystone = new MetaPrize { name = "Slow Burn", note = "the world stays warm for ten more waves, and teaches you less",
                   mods = [new Mod("tollDelay", 10),new Mod("xp", -0.30)] } },
      new MetaTpl { id = "k_scav",  name = "THE ORDNANCE", sp = "arm", home = "str", d = [3,4], shape = "arc",
        minors = [("Heavier Bomb", "bombDmg", 1),("Wider Bomb", "bombR", 0.12),("Clustering", "bombCluster", 1)],
        keystone = new MetaPrize { name = "Scavenger", note = "every chest is worth double, and you take what you are given",
                   mods = [new Mod("chestCards", 2),new Mod("noReroll", null)] } },
      new MetaTpl { id = "k_hoard", name = "THE EYRIE",    sp = "prec",home = "dex", d = [4,4], shape = "ring",
        minors = [("Hawk-eyed", "incCrit", 0.08),("Merciless", "critMult", 0.18),("Clear Sight", "allRes", 0.04)],
        keystone = new MetaPrize { name = "Hoarder", note = "the run pays double into the tree, and hits softer",
                   mods = [new Mod("bankMul", 2),new Mod("incAll", -0.20)] } },
      /* one keystone per country is pinned to the outermost ring and no more: green
         held two of the four and read as the long country to build in */
      new MetaTpl { id = "k_veng",  name = "THE ROT",      sp = "aff", home = "dex", d = [3,4], shape = "fan",
        minors = [("Pestilent", "incDot", 0.08),("Corrosive", "poisonDot", 0.4),("Inured", "chaosRes", 0.06)],
        keystone = new MetaPrize { name = "Vengeance", note = "what you have already hurt hurts far more; nothing you meet fresh does",
                   mods = [new Mod("incDot", 0.50),new Mod("bleedShare", 0.06),new Mod("incCrit", -0.60)] } },
      new MetaTpl { id = "k_stone", name = "THE KEEP",     sp = "arm", home = "str", d = [4,4], shape = "ring",
        minors = [("Adamant", "incArmour", 0.12),("Anchored", "armour", 8),("Absorbing", "suppress", 0.05)],
        keystone = new MetaPrize { name = "Stoneheart", note = "you will outlast anything; you will not out-hit it",
                   mods = [new Mod("moreHp", 0.35),new Mod("allRes", 0.06),new Mod("incAll", -0.30)] } },
      new MetaTpl { id = "k_cond",  name = "THE CONFLUENCE",sp = "elem",home = "int",d = [4,4], shape = "arc",
        minors = [("Pyre", "incFire", 0.09),("Rime", "incFrost", 0.09),("Tempest", "incShock", 0.09)],
        keystone = new MetaPrize { name = "Conduit", note = "every element at once, and the words come slowly",
                   mods = [new Mod("incElem", 0.50),new Mod("allRes", 0.08),new Mod("incCast", -0.35)] } },
      /* ---- AND THE STRANGE ONES. Each of these changes what a build IS rather
         than what its numbers are, which is the only reason to walk this far. ---- */
      new MetaTpl { id = "k_rune",  name = "RUNEBLOOD",    sp = "arc", home = "int", d = [3,4], shape = "chain",
        minors = [("Study", "incSpell", 0.04),("Bookish", "int", 6)],
        keystone = new MetaPrize { name = "Runeblood", note = "your learning is your body",
                   mods = [new Mod("heartsPerInt", 1),new Mod("moreHp", -0.15)] } },
      new MetaTpl { id = "k_void",  name = "VOIDCALL",     sp = "aff", home = "dex", d = [3,4], shape = "chain",
        minors = [("Leak", "gn_shock_chaos", 0.08),("Earthing", "shockRes", 0.06)],
        keystone = new MetaPrize { name = "Voidcall", note = "every storm you raise comes down as something worse",
                   mods = [new Mod("cv_shock_chaos", 1),new Mod("incShock", 0.30)] } },
      new MetaTpl { id = "k_sour",  name = "THE SOUR SUN", sp = "aff", home = "dex", d = [3,4], shape = "chain",
        minors = [("Sour Ending", "gn_fire_chaos", 0.08),("Cinderproof", "fireRes", 0.06)],
        keystone = new MetaPrize { name = "The Sour Sun", note = "your fireballs are not made of fire any more",
                   mods = [new Mod("cv_fire_chaos", 1),new Mod("incFire", 0.30)] } },
      new MetaTpl { id = "k_frost", name = "FROSTBOUND",   sp = "elem",home = "int", d = [3,4], shape = "chain",
        minors = [("Hoarfrost", "gn_phys_frost", 0.08),("Frostskin", "frostRes", 0.06)],
        keystone = new MetaPrize { name = "Frostbound", note = "nothing you swing is made of metal any more",
                   mods = [new Mod("cv_phys_frost", 1),new Mod("incFrost", 0.25)] } },
      new MetaTpl { id = "k_open",  name = "THE OPEN GUARD",sp = "arm",home = "str", d = [3,4], shape = "chain",
        minors = [("Unburdened", "speed", 0.05),("Read the Room", "suppress", 0.05)],
        keystone = new MetaPrize { name = "The Open Guard", note = "you wear nothing, and nothing cast at you lands properly",
                   mods = [new Mod("noArmour", null),new Mod("lessSpell", 0.50)] } },

      /* ==================== FIVE MORE SENTENCES ====================
         Every one of these is a TRADE, and both halves are real. That is the only
         thing that makes a keystone worth the walk: a node that is simply good is
         a big minor node, and the tree already has plenty of those. */

      /* what dies keeps killing. The one the ailment builds have never had: three
         single-target damage-over-time builds become wave clear in one sentence.
         IT WAS PRICED IN THE WRONG CURRENCY. The cost was 25% off everything you HIT
         -- and the build that walks here barely hits; its damage is the part that
         lingers, which the cost never touched. With a 210px spread on top it was
         the strongest node on the map. The spread is a room, not a screen, now, and
         the cost is paid in the thing the build is made of: HALF of every burn,
         wound and dose. Clear a pack, and a single boss takes twice as long. */
      new MetaTpl { id = "k_prolif",name = "THE LONG DYING",sp = "aff", home = "dex", d = [4,4], shape = "arc",
        minors = [("Lingering", "incDot", 0.08),("Seeding", "poisonDot", 0.3),("Festering", "bleedShare", 0.04)],
        keystone = new MetaPrize { name = "The Long Dying", note = "what you kill keeps killing, at half the rate",
                   mods = [new Mod("prolif", 120),new Mod("lessDot", 0.5)] } },

      /* the crit tree's other ending: stop hitting harder, start leaving
         everything. Pairs with the ailment table rather than fighting it. */
      new MetaTpl { id = "k_agony", name = "BLOOD AND SALT",sp = "prec",home = "dex", d = [4,4], shape = "cresc",
        minors = [("Salted", "ailChance", 0.06),("Opened", "bleedChance", 0.06),("Rubbed In", "incDot", 0.06)],
        keystone = new MetaPrize { name = "Blood and Salt", note = "your criticals stop hitting harder and start leaving everything",
                   mods = [new Mod("critAilAll", null),new Mod("critMult", -0.55)] } },

      /* ...and the ending where you have no criticals at all. Everything else you
         own gets bigger, which is a real build and not a consolation. */
      new MetaTpl { id = "k_over",  name = "THE OVERLOAD", sp = "elem",home = "int", d = [4,4], shape = "wheel",
        minors = [("Surging", "incElem", 0.07),("Unfocused", "incAll", 0.04),("Raw", "incElem", 0.075)],
        keystone = new MetaPrize { name = "The Overload", note = "you cannot crit at all, and everything you do is far bigger",
                   mods = [new Mod("noCrit", null),new Mod("moreAll", 0.32),new Mod("incElem", 0.25)] } },

      /* every chill and shock at full magnitude, whatever the hit was worth --
         bought with everything that lingers. The mirror of The Long Dying. */
      new MetaTpl { id = "k_cold",  name = "THE DEEP COLD", sp = "elem",home = "int", d = [3,4], shape = "cresc",
        minors = [("Glacial", "ailMag", 0.20),("Static", "incShock", 0.07),("Hoar", "incFrost", 0.07)],
        keystone = new MetaPrize { name = "The Deep Cold", note = "every chill and shock lands full force, and nothing you leave burns",
                   mods = [new Mod("ailFull", null),new Mod("incDot", -0.60)] } },

      /* THE ARCHER'S KEYSTONE. An arrow that would stop has a chance to find one
         more body, and then another -- bought with a little off every arrow. */
      new MetaTpl { id = "k_rico",  name = "THE RICOCHET", sp = "prec", home = "dex", d = [4,4], shape = "arc",
        minors = [("Fletched", "arrowPierce", 1),("Barbed", "arrowFlat", 0.5),("Quick Draw", "incAtk", 0.05)],
        keystone = new MetaPrize { name = "Ricochet", note = "an arrow that should stop finds another body, and hits a little softer",
                   mods = [new Mod("ricochet", 0.40),new Mod("arrowLess", 0.15)] } },

      /* ==================== THE SHIELD'S THREE ENDINGS ====================
         PoE's energy-shield keystones, each a trade only a shield build wins.
         CHAOS INOCULATION -- one heart, every heart you gave up paid back as two
           of shield, and chaos simply does not arrive: the one damage type that
           walks past a shield is the one damage type you are immune to.
         GHOST REAVER -- your lifesteal fills the shield instead of the hearts.
         ZEALOT'S OATH -- your regeneration fills the shield instead of the hearts.
         The last two cost exactly what they redirect: with no shield to fill,
         they are a leech or a regen thrown away. */
      /* ==================== THE LEGION ====================
         The scepter's corner of intelligence's country. MIRRORED LEGION is the
         keystone asked for by name: your minions fight the way you do -- three
         quarters of your own increased damage, physical, elemental and spell, and
         your conversions, ride on every minion hit -- and you pay for it in your
         own hits, which is the trade a summoner is happy to make. MINION
         INSTABILITY is PoE's: a skeleton that falls bursts into fire. */
      new MetaTpl { id = "k_mirror", name = "THE MIRROR",  sp = "arc", home = "int", d = [4,4], shape = "ring",
        minors = [("Bone Craft", "minionDmg", 0.08),("Marrow", "minionLife", 0.10),("Drill", "minionSpd", 0.06)],
        keystone = new MetaPrize { name = "Mirrored Legion", note = "your legion fights the way you do",
                   mods = [new Mod("minionMirror", null),new Mod("moreAll", -0.40)] } },
      new MetaTpl { id = "k_pyre",   name = "THE PYRE",    sp = "arc", home = "int", d = [3,4], shape = "arc",
        minors = [("Kindling Bones", "minionLife", 0.10),("Grave Heat", "minionDmg", 0.08),("Brittle", "minionSpd", 0.05)],
        keystone = new MetaPrize { name = "Minion Instability", note = "what falls, burns",
                   mods = [new Mod("minionInstab", null),new Mod("minionLess", 0.15)] } },
      new MetaTpl { id = "n_ossuary", name = "THE OSSUARY", sp = "arc", home = "int", d = [2,3], shape = "ring",
        minors = [("Ossuary", "minionDmg", 0.06),("Catacomb", "minionLife", 0.08),("Crypt", "int", 4)],
        notable = new MetaPrize { name = "Lord of Bones", mods = [new Mod("minionDmg", 0.14),new Mod("minionMax", 1)] } },
      new MetaTpl { id = "k_ci",    name = "THE INOCULATION", sp = "arc", home = "int", d = [4,4], shape = "ring",
        minors = [("Sterile", "es", 4),("Hollow", "incEs", 0.12),("Inured", "chaosRes", 0.10)],
        keystone = new MetaPrize { name = "Chaos Inoculation", note = "one heart, a far deeper shield, and chaos cannot touch you",
                   mods = [new Mod("chaosInoc", null)] } },
      new MetaTpl { id = "k_ghost", name = "THE GHOST REAVER", sp = "arc", home = "int", d = [4,4], shape = "arc",
        minors = [("Siphoning", "leech", 0.02),("Thirsting", "leechRate", 0.05),("Warded", "es", 3)],
        keystone = new MetaPrize { name = "Ghost Reaver", note = "what you steal becomes your shield, never your blood",
                   mods = [new Mod("ghostReaver", null)] } },
      new MetaTpl { id = "k_zeal",  name = "THE ZEALOT'S OATH", sp = "arc", home = "int", d = [4,4], shape = "fan",
        minors = [("Devout", "incRegen", 0.12),("Oathbound", "incEs", 0.12),("Steadfast", "incRegen", 0.10)],
        keystone = new MetaPrize { name = "Zealot's Oath", note = "what mends you mends your shield, never your hearts",
                   mods = [new Mod("zealot", null)] } },

      /* all three charges at once, on a body that cannot take a hit. The charge
         build's own ending, and the reason to raise three ceilings. */
      new MetaTpl { id = "k_relent",name = "THE UNRELENTING",sp = "war",home = "str", d = [4,4], shape = "fan",
        minors = [("Driving", "chargeMax", 1),("Unyielding", "armour", 6),("Relentless", "incAtk", 0.05)],
        keystone = new MetaPrize { name = "Unrelenting", note = "every charge, by every means — and a body made of glass",
                   mods = [new Mod("chargeAll", 1),new Mod("chargeMax", 2),new Mod("moreHp", -0.28)] } },
    ];

    /* ---- THE LEGENDARIES: built in MetaGenTemplates ---- */
    public static List<MetaTpl> META_LEGENDS = new();
    /* ---- THE HEART'S OWN GROUPS ---- */
    public static readonly List<MetaTpl> META_HEART_GROUPS = [
      /* red: armour, life, regeneration */
      new MetaTpl { id = "hg_bulwark", name = "THE BULWARK",    sp = "arm",  home = "str", shape = "arc",
        minors = [("Bulwark", "armour", 3),("Hale Heart", "incHp", 0.03),("Stout", "str", 4)] },
      new MetaTpl { id = "hg_mend",    name = "HEARTMENDING",   sp = "vit",  home = "str", shape = "ring",
        minors = [("Heartmend", "incRegen", 0.08),("Vigour", "incHp", 0.025),("Stout", "str", 4)] },
      new MetaTpl { id = "hg_iron",    name = "IRON HIDE",      sp = "arm",  home = "str", shape = "arc",
        minors = [("Iron Hide", "incArmour", 0.06),("Thick Blood", "incRegen", 0.06),("Brawn", "str", 4)] },
      new MetaTpl { id = "hg_hearth",  name = "THE HEARTH",     sp = "vit",  home = "str", shape = "ring",
        minors = [("Hearth", "incHp", 0.03),("Warm Blood", "incRegen", 0.06),("Brawn", "str", 4)] },
      new MetaTpl { id = "hg_stead",   name = "STEADFAST",      sp = "arm",  home = "str", shape = "arc",
        minors = [("Steadfast", "armour", 3),("Unbowed", "suppress", 0.03),("Brawn", "str", 4)] },
      /* green: evasion, accuracy, life */
      new MetaTpl { id = "hg_slip",    name = "THE SLIP",       sp = "swift",home = "dex", shape = "arc",
        minors = [("Slip", "incEva", 0.06),("Nimble", "dex", 4),("Wiry", "incHp", 0.025)] },
      new MetaTpl { id = "hg_steady",  name = "STEADY HAND",    sp = "prec", home = "dex", shape = "ring",
        minors = [("Steady", "acc", 25),("Deft", "dex", 4),("Sidestep", "eva", 30)] },
      new MetaTpl { id = "hg_keen",    name = "KEEN EYE",       sp = "prec", home = "dex", shape = "arc",
        minors = [("Keen Eye", "incAcc", 0.06),("Lithe", "dex", 4),("Wiry", "incHp", 0.025)] },
      new MetaTpl { id = "hg_dodge",   name = "THE DODGE",      sp = "swift",home = "dex", shape = "ring",
        minors = [("Dodge", "eva", 30),("Tumble", "roll", 0.06),("Lithe", "dex", 4)] },
      new MetaTpl { id = "hg_hunter",  name = "HUNTER'S HIDE",  sp = "swift",home = "dex", shape = "arc",
        minors = [("Hunter's Hide", "incEva", 0.05),("Lean", "incHp", 0.025),("Deft", "dex", 4)] },
      /* blue: energy shield, regeneration, resistances */
      new MetaTpl { id = "hg_ward",    name = "THE INNER WARD", sp = "arc",  home = "int", shape = "arc",
        minors = [("Inner Ward", "es", 3),("Warded", "incEs", 0.05),("Studied", "int", 4)] },
      new MetaTpl { id = "hg_calm",    name = "THE CALM",       sp = "vit",  home = "int", shape = "ring",
        minors = [("Calm", "incRegen", 0.07),("Insight", "int", 4),("Shielded", "es", 3)] },
      new MetaTpl { id = "hg_clear",   name = "CLEAR MIND",     sp = "arc",  home = "int", shape = "arc",
        minors = [("Clear Mind", "incEs", 0.05),("Warding", "allRes", 0.02),("Learned", "int", 4)] },
      new MetaTpl { id = "hg_lantern", name = "THE LANTERN",    sp = "arc",  home = "int", shape = "ring",
        minors = [("Lantern", "es", 3),("Glow", "incRegen", 0.06),("Learned", "int", 4)] },
      new MetaTpl { id = "hg_veil",    name = "THE VEIL",       sp = "arc",  home = "int", shape = "arc",
        minors = [("Veil", "incEs", 0.05),("Soft Step", "incHp", 0.025),("Studied", "int", 4)] },
    ];
    public static readonly List<MetaNode> META_NODES = new();
    public static readonly Dictionary<string, MetaNode> META_BY_ID = new();
    public static readonly Dictionary<string, MetaCluster> META_CLUSTER_BY_ID = new();
    public static readonly List<MetaCluster> META_CLUSTERS = new();
    public static List<MetaSite> META_SITES = new();
    /* how much clear ground a quad needs, beside its own group, to take a second */
    public const double SAT_WALL = 160, SAT_GAP = 330;
    /* how far round from its first way in a cluster's second one has to be */
    public const double THROUGH_ANGLE = 110 * Math.PI / 180;
    public static readonly Dictionary<string, double> META_NODE_R = new() { ["start"] = 46, ["legend"] = 44, ["keystone"] = 40, ["notable"] = 34, ["minor"] = 25, ["travel"] = 19 };
    /* what a ring costs to be standing on */
    public static readonly Tbl RING_AT = new double[] { 0, 5, 12, 22, 34 };
}
