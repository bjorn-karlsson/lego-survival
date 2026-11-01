namespace Brickblade;

/* =============================== ENEMIES (JS 5412–6233) ===============================
   The roster of monsters, the bosses and their traits, the rare modifiers, the
   kindred packs, the three specials (goblin, mimic, banner) and the blood orbs. */

/* one row of EDEF, BOSSES or SUPERBOSSES: every field any of them carries */
public sealed class EDef
{
    public string name, type, style, mini, elem, golem, sig;
    public double hp, speed, r, scale, dmg, xp, armourMul, fire, block = double.NaN, evade;
    public double es, chaos, hpMul, atkMul, dmgMul;
    public double[] studs;
    public FigCols col;
    public bool bigimp, shield, zombie, crawl, fat, shade, zorb, boss;
    public NumMap res;
}
public sealed class GolemConf
{
    public string ring, dust, kit, mini, name; public double stompR, chargeSpd, wind, dmg, range; public string[] bricks;
}
/* a boss trait or a rare modifier: a name over its head and what it does to it */
public sealed class MonMod { public string id, name, col, tip; public Action<Ent> apply; }
public sealed class RmCore : Obj { public double t; public bool done; }
public sealed class GoblinPending { public double delay; }
public sealed class OrbKind { public string col, elem, name; public int r, g, b; }
public sealed class BloodOrb : Obj { public double px, py, vx, vy, t, hold, phase; public string kind, state; }
public sealed class DropRange { public double a, b; }

public static partial class G
{
    static FigCols FC(string head, string torso, string legs, string hands, string hair) =>
        new FigCols { head = head, torso = torso, legs = legs, hands = hands, hair = hair };
    static NumMap R(params (string k, double v)[] kv) => new NumMap(kv);

    public static readonly Dictionary<string, EDef> EDEF = new()
    {
        ["imp"] = new EDef { hp = 3, speed = 88, r = 12, scale = 0.78, dmg = 1, xp = 2, studs = new double[] { 2, 4 },
            col = FC(PAL.red, PAL.dred, "#2b2f33", PAL.hyellow, "#8f161d"), style = "imp" },
        /* the imp that got fed: same charge, twice the size, and it does not fall
           over to one swing the way its little brothers do */
        ["superimp"] = new EDef { hp = 20, speed = 74, r = 17, scale = 1.38, dmg = 2, xp = 10, studs = new double[] { 9, 15 }, armourMul = 1.6, bigimp = true,
            col = FC("#c4241f", "#6e0f14", "#241f22", "#ff8a6a", "#5e0d10"), style = "imp" },
        ["archer"] = new EDef { hp = 2, speed = 66, r = 12, scale = 0.8, dmg = 1, xp = 3, studs = new double[] { 3, 5 }, fire = 2.3,
            col = FC(PAL.white, "#c9cecb", "#9ba19d", PAL.white, "#5c5e59"), style = "skel" },
        ["boneling"] = new EDef { hp = 2, speed = 128, r = 10, scale = 0.66, dmg = 1, xp = 2, studs = new double[] { 2, 3 },
            col = FC("#eef0ee", "#d9dcd7", "#b8bdb9", "#eef0ee", "#b8bdb9"), style = "skel" },
        /* the rank and file of every crypt: a skeleton with a rusted blade */
        ["skeleton"] = new EDef { hp = 4, speed = 96, r = 12, scale = 0.82, dmg = 1, xp = 3, studs = new double[] { 2, 5 },
            col = FC("#e8eae6", "#cdd1cb", "#a7ada7", "#e8eae6", "#7d837d"), style = "skel" },
        ["spitter"] = new EDef { hp = 3, speed = 60, r = 12, scale = 0.85, dmg = 1, xp = 3, studs = new double[] { 3, 5 }, fire = 2.6,
            col = FC("#8ac24a", "#3d7a2b", "#2f5e23", "#8ac24a", "#2f5e23"), style = "spitter" },
        ["brute"] = new EDef { hp = 10, speed = 44, r = 17, scale = 1.5, dmg = 2, xp = 8, studs = new double[] { 6, 10 },
            col = FC("#7fb646", PAL.dgray, "#4a4b47", "#3d3e3b", "#3d3e3b"), style = "brute" },
        ["knight"] = new EDef { hp = 9, speed = 46, r = 14, scale = 1.15, dmg = 2, xp = 7, studs = new double[] { 6, 9 }, shield = true, block = 0.5,
            col = FC("#e8e4d8", "#2b2f33", "#23272b", "#2b2f33", "#2b2f33"), style = "knight" },
        ["bomber"] = new EDef { hp = 4, speed = 108, r = 12, scale = 0.82, dmg = 1, xp = 4, studs = new double[] { 4, 8 },
            col = FC("#fe8a18", "#3a3f44", "#23272b", "#fe8a18", "#3a3f44"), style = "bomber" },
        ["lancer"] = new EDef { hp = 5, speed = 62, r = 13, scale = 0.95, dmg = 2, xp = 5, studs = new double[] { 4, 7 },
            col = FC("#e8e4d8", "#9ba19d", "#6d6e68", "#e8e4d8", "#5c5e59"), style = "lancer" },
        ["hexer"] = new EDef { hp = 4, speed = 58, r = 12, scale = 0.9, dmg = 1, xp = 5, studs = new double[] { 4, 7 }, fire = 2.9,
            col = FC("#e8e4d8", "#3f2a55", "#2c1d3d", "#c9b8e8", "#2c1d3d"), style = "hexer" },
        ["stalker"] = new EDef { hp = 3, speed = 134, r = 11, scale = 0.8, dmg = 1, xp = 3, studs = new double[] { 3, 5 },
            col = FC("#2b2f33", "#23272b", "#1b1f23", "#3a3f44", "#1b1f23"), style = "stalker" },
        /* the jugg is a wall that walks */
        ["jugg"] = new EDef { hp = 24, speed = 40, r = 20, scale = 1.8, dmg = 4, xp = 14, studs = new double[] { 10, 18 },
            col = FC("#d19a6a", "#b6532b", "#6e3a1e", "#8a4a26", "#6e3a1e"), style = "brute" },
        ["revenant"] = new EDef { hp = 30, speed = 36, r = 19, scale = 1.7, dmg = 2, xp = 16, studs = new double[] { 12, 20 },
            col = FC("#cfd6dd", "#6d6e68", "#5c5e59", "#cfd6dd", "#4a4b47"), style = "skel" },
        /* super tanky melee skeleton -- slow, high HP, innate armor */
        ["bastion"] = new EDef { hp = 46, speed = 34, r = 16, scale = 1.45, dmg = 2, xp = 18, studs = new double[] { 12, 20 },
            col = FC("#eef0ee", "#8a8f96", "#6d6e68", "#b8bdb9", "#5c5e59"), style = "knight" },
        ["necromancer"] = new EDef { hp = 8, speed = 50, r = 13, scale = 1.0, dmg = 1, xp = 10, studs = new double[] { 8, 14 },
            col = FC("#eef0ee", "#3a2a55", "#271b3a", "#c9b8e8", "#271b3a"), style = "hexer" },
        ["pyromancer"] = new EDef { hp = 5, speed = 56, r = 12, scale = 0.92, dmg = 1, xp = 6, studs = new double[] { 5, 9 }, fire = 3.0,
            res = R(("fire", 0.45)),
            col = FC("#eef0ee", "#8a2f1d", "#5e1f13", "#ffb066", "#5e1f13"), style = "hexer" },
        ["frostmage"] = new EDef { hp = 5, speed = 56, r = 12, scale = 0.92, dmg = 1, xp = 6, studs = new double[] { 5, 9 }, fire = 3.2,
            res = R(("frost", 0.45)),
            col = FC("#eef0ee", "#2e6f9e", "#1d4a6b", "#bfe3ff", "#1d4a6b"), style = "hexer" },
        ["stormcaller"] = new EDef { hp = 5, speed = 62, r = 12, scale = 0.92, dmg = 1, xp = 6, studs = new double[] { 5, 9 }, fire = 2.5,
            res = R(("shock", 0.45)),
            col = FC("#eef0ee", "#9a7d1e", "#6b5714", "#ffe95a", "#6b5714"), style = "hexer" },
        ["lobber"] = new EDef { hp = 7, speed = 52, r = 13, scale = 0.95, dmg = 1, xp = 8, studs = new double[] { 6, 10 },
            col = FC("#eef0ee", "#3d7a2b", "#2f5e23", "#8ac24a", "#2f5e23"), style = "skel" },
        /* a marksman is a sniper, and a sniper that plinks is just an archer */
        ["marksman"] = new EDef { hp = 6, speed = 70, r = 12, scale = 0.9, dmg = 3, xp = 8, studs = new double[] { 6, 10 }, fire = 2.6,
            col = FC("#f6f2e4", "#c9a83a", "#9a8228", "#f6f2e4", "#8a7d4a"), style = "skel" },
        /* THE BLIGHTSPITTER: what it lobs barely hurts, the pool it leaves is the weapon */
        ["blightspitter"] = new EDef { hp = 7, speed = 56, r = 12, scale = 0.95, dmg = 1, xp = 11, studs = new double[] { 8, 13 }, fire = 3.4,
            res = R(("fire", -0.20)),
            col = FC("#d8e8bc", "#4c7a2b", "#33551d", "#9ede5a", "#33551d"), style = "skel" },
        /* THE DEAD THAT WALK */
        ["zombie"] = new EDef { hp = 5, speed = 50, r = 12, scale = 0.84, dmg = 1, xp = 3, studs = new double[] { 2, 4 }, zombie = true,
            col = FC("#86b25e", "#6a5a44", "#3e4d63", "#86b25e", "#3d5230"), style = "zombie" },
        ["crawler"] = new EDef { hp = 3, speed = 34, r = 11, scale = 0.84, dmg = 1, xp = 1, studs = new double[] { 1, 2 }, zombie = true, crawl = true,
            col = FC("#86b25e", "#6a5a44", "#3e4d63", "#86b25e", "#3d5230"), style = "zombie" },
        ["bloater"] = new EDef { hp = 18, speed = 38, r = 17, scale = 1.12, dmg = 1, xp = 9, studs = new double[] { 6, 10 }, zombie = true, fat = true, armourMul = 1.2,
            col = FC("#9dbb62", "#7e6b4a", "#4a4d3a", "#9dbb62", "#4d6630"), style = "zombie" },
        ["zrunner"] = new EDef { hp = 6, speed = 116, r = 12, scale = 0.84, dmg = 1, xp = 5, studs = new double[] { 3, 6 }, zombie = true,
            col = FC("#7aa65a", "#8a2f2a", "#2e3440", "#7aa65a", "#2b3a22"), style = "zombie" },
        /* the zombie WITCH DOCTOR raises blood orbs (or plague orbs) that hunt you down */
        ["witchdoctor"] = new EDef { hp = 7, speed = 52, r = 13, scale = 1.02, dmg = 2, xp = 12, studs = new double[] { 6, 10 }, zombie = true,
            col = FC("#8fa874", "#5a2e4a", "#3b2030", "#8fa874", "#2b1a22"), style = "zombie" },
        ["zhulk"] = new EDef { hp = 26, speed = 56, r = 16, scale = 1.25, dmg = 3, xp = 13, studs = new double[] { 8, 13 }, zombie = true, armourMul = 1.3,
            col = FC("#6f9a4c", "#3d4a5c", "#2b2f38", "#6f9a4c", "#243020"), style = "zombie" },
        ["bigcrawler"] = new EDef { hp = 11, speed = 40, r = 14, scale = 1.2, dmg = 2, xp = 3, studs = new double[] { 2, 4 }, zombie = true, crawl = true,
            col = FC("#6f9a4c", "#3d4a5c", "#2b2f38", "#6f9a4c", "#243020"), style = "zombie" },
        /* crypt bat -- chains 3 charges in a row */
        ["cryptbat"] = new EDef { hp = 6, speed = 120, r = 11, scale = 0.9, dmg = 1, xp = 5, studs = new double[] { 4, 7 },
            col = FC("#4a3f5e", "#3a3350", "#2a2440", "#5a4f70", "#3a3350"), style = "stalker" },
        /* THE TREASURE GOBLIN: it does not fight, it runs -- with a sack of your studs */
        ["goblin"] = new EDef { hp = 110, speed = 150, r = 13, scale = 0.9, dmg = 0, xp = 40, studs = new double[] { 0, 0 }, armourMul = 3,
            res = R(("fire", 0.3), ("frost", 0.3), ("shock", 0.3)),
            col = FC("#7ac943", "#7a4f2a", "#3b2a1a", "#7ac943", "#3b2a1a"), style = "goblin" },
        /* a chest with teeth */
        ["mimic"] = new EDef { hp = 22, speed = 118, r = 16, scale = 1.0, dmg = 3, xp = 24, studs = new double[] { 10, 18 }, armourMul = 1.8,
            col = FC("#9c6a45", "#7b5132", "#583927", "#9c6a45", "#583927"), style = "brute" },
        /* the banner-bearer plants a war banner, and everything near it fights harder */
        ["bannerman"] = new EDef { hp = 7, speed = 64, r = 14, scale = 1.1, dmg = 2, xp = 12, studs = new double[] { 6, 10 },
            col = FC("#e8e4d8", "#a8201a", "#5a1410", "#e8e4d8", "#5a1410"), style = "knight" },
        ["warbanner"] = new EDef { hp = 14, speed = 0, r = 12, scale = 1.0, dmg = 0, xp = 6, studs = new double[] { 3, 6 }, armourMul = 1.2,
            col = FC("#a8201a", "#a8201a", "#6d6e68", "#a8201a", "#a8201a"), style = "knight" },
        /* THE GREENWARDEN: never attacks, MENDS the pack */
        ["greenwarden"] = new EDef { hp = 95, speed = 46, r = 15, scale = 1.45, dmg = 1, xp = 52, studs = new double[] { 36, 58 }, armourMul = 1.6,
            col = FC("#eef0ee", "#2f6b33", "#1d4523", "#bdf0a8", "#1d4523"), style = "hexer" },
        ["necrolord"] = new EDef { hp = 34, speed = 44, r = 16, scale = 1.35, dmg = 1, xp = 30, studs = new double[] { 22, 38 },
            col = FC("#eef0ee", "#4a2f75", "#31204d", "#dccdf5", "#31204d"), style = "hexer" },
        ["pyrolord"] = new EDef { hp = 20, speed = 50, r = 15, scale = 1.25, dmg = 1, xp = 22, studs = new double[] { 16, 26 }, fire = 3.4,
            res = R(("fire", 0.75), ("frost", -0.25)),
            col = FC("#eef0ee", "#b23a1f", "#7a2413", "#ffc07a", "#7a2413"), style = "hexer" },
        ["frostlord"] = new EDef { hp = 20, speed = 50, r = 15, scale = 1.25, dmg = 1, xp = 22, studs = new double[] { 16, 26 }, fire = 3.6,
            res = R(("frost", 0.75), ("fire", -0.25)),
            col = FC("#eef0ee", "#3a8ec9", "#22587f", "#d8efff", "#22587f"), style = "hexer" },
        ["stormlord"] = new EDef { hp = 20, speed = 58, r = 15, scale = 1.25, dmg = 1, xp = 22, studs = new double[] { 16, 26 }, fire = 2.9,
            res = R(("shock", 0.75), ("frost", -0.25)),
            col = FC("#eef0ee", "#c9a523", "#8a7115", "#fff3a0", "#8a7115"), style = "hexer" },
        ["overseer"] = new EDef { hp = 26, speed = 48, r = 14, scale = 1.2, dmg = 1, xp = 35, studs = new double[] { 25, 40 }, fire = 6.0,
            col = FC("#eef0ee", "#8a7115", "#5e4a12", "#ffe95a", "#5e4a12"), style = "hexer" },
        /* imp master -- keeps a pit of imps coming and hexes the hero with curses */
        ["impmaster"] = new EDef { hp = 30, speed = 54, r = 14, scale = 1.3, dmg = 1, xp = 24, studs = new double[] { 18, 30 }, fire = 3.2,
            col = FC("#d3242b", "#5e1220", "#3a0c16", "#ff8a8a", "#3a0c16"), style = "hexer" },
        /* dire bat -- bursts into a swarm of ordinary bats when it goes down */
        ["direbat"] = new EDef { hp = 26, speed = 132, r = 16, scale = 1.5, dmg = 2, xp = 16, studs = new double[] { 12, 20 },
            col = FC("#6a4f7e", "#513a68", "#3a2a50", "#7a5f92", "#513a68"), style = "stalker" },
        /* pumpkin lord -- lobs lego-pumpkins on a three-second fuse */
        ["pumpkinlord"] = new EDef { hp = 22, speed = 62, r = 14, scale = 1.2, dmg = 2, xp = 20, studs = new double[] { 15, 26 }, fire = 5.2,
            col = FC("#ff9a3c", "#3a3f44", "#23272b", "#ffc07a", "#7a3a20"), style = "bomber" },
        /* SHADES -- the only things in the game with EVADE */
        ["shade"] = new EDef { hp = 13, speed = 76, r = 12, scale = 1.05, dmg = 1, xp = 9, studs = new double[] { 6, 12 }, shade = true, evade = 0.32,
            col = FC("#3b3654", "#2a2740", "#201d30", "#584f7d", "#100e1c"), style = "shade" },
        ["shadestorm"] = new EDef { hp = 32, speed = 64, r = 13, scale = 1.3, dmg = 2, xp = 24, studs = new double[] { 18, 30 }, shade = true, evade = 0.44,
            fire = 4.6, zorb = true,
            col = FC("#6b6321", "#443f18", "#2b2810", "#ffe95a", "#100e1c"), style = "shade" },
        /* MINI GOLEMS -- what is left when a golem comes apart */
        ["ministone"] = new EDef { hp = 11, speed = 112, r = 10, scale = 0.85, dmg = 1, xp = 6, studs = new double[] { 4, 9 }, mini = "stone",
            res = R(("fire", 0.15), ("frost", 0.15), ("shock", 0.15)), armourMul = 2.0,
            col = FC("#9aa0a0", "#7b8184", "#5f6467", "#8b9194", "#5f6467"), style = "brute" },
        ["miniice"] = new EDef { hp = 9, speed = 122, r = 10, scale = 0.82, dmg = 1, xp = 6, studs = new double[] { 4, 9 }, mini = "ice", elem = "frost",
            res = R(("frost", 0.75), ("fire", -0.25), ("shock", 0.15)),
            col = FC("#cfeaff", "#7fb9e0", "#4d86ad", "#e6f6ff", "#4d86ad"), style = "brute" },
        ["minifire"] = new EDef { hp = 9, speed = 128, r = 10, scale = 0.82, dmg = 1, xp = 6, studs = new double[] { 4, 9 }, mini = "fire", elem = "fire",
            res = R(("fire", 0.75), ("frost", -0.25), ("shock", 0.15)),
            col = FC("#ff9a3c", "#8a3418", "#4a1d0f", "#ffc07a", "#4a1d0f"), style = "brute" },
        /* GOLEMS -- slow siege units */
        ["stormgolem"] = new EDef { hp = 46, speed = 36, r = 20, scale = 1.85, dmg = 4, xp = 32, studs = new double[] { 22, 34 }, golem = "storm", elem = "shock",
            res = R(("shock", 0.90), ("frost", -0.35), ("fire", 0.20)),
            col = FC("#6e6848", "#4a4636", "#2a2820", "#ffe95a", "#38352a"), style = "brute" },
        ["ministorm"] = new EDef { hp = 9, speed = 124, r = 10, scale = 0.82, dmg = 1, xp = 6, studs = new double[] { 4, 9 }, mini = "storm", elem = "shock",
            res = R(("shock", 0.75), ("frost", -0.25), ("fire", 0.15)),
            col = FC("#6e6848", "#4a4636", "#2a2820", "#ffe95a", "#38352a"), style = "imp" },
        /* A GOLEM IS MADE OF ITS ELEMENT */
        ["stonegolem"] = new EDef { hp = 52, speed = 33, r = 21, scale = 1.9, dmg = 4, xp = 26, studs = new double[] { 18, 30 }, golem = "stone",
            armourMul = 3.2, res = R(("fire", 0.25), ("frost", 0.25), ("shock", 0.25)),
            col = FC("#9aa0a0", "#7b8184", "#5f6467", "#8b9194", "#5f6467"), style = "brute" },
        ["icegolem"] = new EDef { hp = 44, speed = 37, r = 20, scale = 1.8, dmg = 3, xp = 28, studs = new double[] { 20, 32 }, golem = "ice",
            res = R(("frost", 0.90), ("fire", -0.35), ("shock", 0.20)),
            col = FC("#cfeaff", "#7fb9e0", "#4d86ad", "#e6f6ff", "#4d86ad"), style = "brute" },
        ["firegolem"] = new EDef { hp = 40, speed = 41, r = 20, scale = 1.8, dmg = 3, xp = 28, studs = new double[] { 20, 32 }, golem = "fire",
            res = R(("fire", 0.90), ("frost", -0.35), ("shock", 0.20)),
            col = FC("#ff9a3c", "#8a3418", "#4a1d0f", "#ffc07a", "#4a1d0f"), style = "brute" },
    };
    /* Each golem has its OWN kit */
    public static readonly Dictionary<string, GolemConf> GOLEM_CONF = new()
    {
        ["stone"] = new GolemConf { ring = "#c9cec9", dust = "#b8bdb9", stompR = 300, chargeSpd = 430, wind = 1.0, dmg = 5,
            kit = "stomp", range = 620, mini = "ministone", bricks = new[] { "#9aa0a0", "#7b8184", "#5f6467" }, name = "STONE" },
        ["ice"] = new GolemConf { ring = "#bfe3ff", dust = "#dff2ff", stompR = 330, chargeSpd = 400, wind = 1.1, dmg = 4,
            kit = "boulder", range = 1000, mini = "miniice", bricks = new[] { "#cfeaff", "#7fb9e0", "#4d86ad" }, name = "FROST" },
        ["fire"] = new GolemConf { ring = "#ff8a3c", dust = "#ffb066", stompR = 285, chargeSpd = 460, wind = 0.95, dmg = 4,
            kit = "bombs", range = 640, mini = "minifire", bricks = new[] { "#ff9a3c", "#8a3418", "#4a1d0f" }, name = "EMBER" },
        /* it plants coils of live lightning around where you are standing */
        ["storm"] = new GolemConf { ring = "#ffe95a", dust = "#fff3a0", stompR = 300, chargeSpd = 440, wind = 1.0, dmg = 5,
            kit = "coils", range = 760, mini = "ministorm", bricks = new[] { "#ffe95a", "#c9a213", "#6b5714" }, name = "STORM" },
    };
    public const double COIL_LIFE = 12, COIL_R = 88, COIL_TICK = 0.55, COIL_MAX = 14;
    /* a coil lands DORMANT and hums before it bites */
    public const double COIL_ARM_MIN = 2.0, COIL_ARM_MAX = 3.0;
    /* and it lands AROUND you, at arm's length */
    public const double COIL_SPREAD_MIN = 150, COIL_SPREAD_MAX = 330;
    /* What one shot carries: a share of the monster's melee `dmg` */
    public static readonly Dictionary<string, double> SHOT_SHARE = new()
    {
        ["arrow"] = 0.85, ["bolt"] = 0.80, ["frostbolt"] = 0.80, ["batbolt"] = 0.50,
        ["skull"] = 0.34, ["hexbolt"] = 0.30,
        ["vbrick"] = 1.10, ["jrock"] = 1.30, ["spit"] = 0.70,
        ["venom"] = 0.35,
        ["molten"] = 0.95, ["meteor"] = 1.05, ["fireball"] = 0.90, ["zorb"] = 0.55,
        ["frostrock"] = 1.15,
    };
    public static double shotPow(Obj src, string kind)
    {
        double d = src != null ? Js.Or(src.dmg, 1) : 1;
        double share = kind != null && SHOT_SHARE.TryGetValue(kind, out var s) ? Js.Or(s, 0.6) : 0.6;
        return Math.Max(1, Math.Round(d * share * 10) / 10);
    }
    /* ranged monsters get half the wave armor -- melee stays the threat */
    public static readonly HashSet<string> RANGED_SET = new() { "archer", "spitter", "hexer", "pyromancer", "frostmage", "stormcaller",
        "lobber", "marksman", "necromancer", "necrolord", "pyrolord", "frostlord", "stormlord", "overseer",
        "shadestorm", "blightspitter" };
    /* HOW DEEP A BOSS IS, AS ONE CURVE */
    public const double ULTRA_STEP = 2.3, ULTRA_FLAT = 1250, ULTRA_BASE = 1300;
    public const double BOSS_SHARE = 0.5, BOSS_BASE_MUL = 3.2;
    /* THE ENDGAME BOSS: x4 for every decade from wave 20 */
    public const double BOSS_LATE_FROM = 20, BOSS_LATE_BASE = 4, BOSS_LATE_PER = 10, BOSS_LATE_MAX = 64;
    public static double bossLateMul(double n = double.NaN)
    {
        double w = double.IsNaN(n) ? wave.n : n;
        if (w < BOSS_LATE_FROM) return 1;
        return Math.Min(BOSS_LATE_MAX, Math.Pow(BOSS_LATE_BASE, 1 + Math.Floor((w - BOSS_LATE_FROM) / BOSS_LATE_PER)));
    }
    public static double bossPool(double baseHp, double st) => baseHp * 1.2 * (1 + (st - 1) * ULTRA_STEP) + st * ULTRA_FLAT + ULTRA_BASE;
    public static readonly Dictionary<string, EDef> BOSSES = new()
    {
        ["brickthane"] = new EDef { name = "VON BRICKTHANE", hp = 90, speed = 52, r = 26, scale = 2.15, dmg = 2, xp = 40, studs = new double[] { 40, 60 },
            col = FC("#3a3f44", "#23272b", "#1b1f23", "#2f3438", "#2f3438"), style = "boss" },
        ["bonebaron"] = new EDef { name = "THE BONE BARON", hp = 85, speed = 62, r = 24, scale = 2.05, dmg = 2, xp = 45, studs = new double[] { 45, 68 },
            es = 0.35,
            col = FC("#eef0ee", "#c9cecb", "#9ba19d", "#eef0ee", "#5c5e59"), style = "skel" },
        ["lavabrick"] = new EDef { name = "LORD LAVABRICK", hp = 120, speed = 44, r = 28, scale = 2.35, dmg = 3, xp = 55, studs = new double[] { 50, 78 },
            col = FC("#ff9a3c", "#4a342a", "#2a2422", "#5a4a3a", "#5a4a3a"), style = "brute" },
        ["ashtyrant"] = new EDef { name = "THE ASH TYRANT", hp = 150, speed = 48, r = 29, scale = 2.4, dmg = 3, xp = 65, studs = new double[] { 60, 95 },
            es = 0.25, chaos = 0.20,
            col = FC("#6b5f58", "#3a2f2a", "#241d1a", "#8a2f1d", "#241d1a"), style = "brute" },
        /* The bat boss: it never stands still */
        ["batsovereign"] = new EDef { name = "THE DREAD ROOST", hp = 170, speed = 72, r = 30, scale = 2.5, dmg = 3, xp = 80, studs = new double[] { 70, 110 },
            es = 0.30, chaos = 0.30,
            col = FC("#4a3a63", "#2e2442", "#1d1730", "#6b5790", "#140f22"), style = "bat" },
    };
    /* SUPER BOSSES at waves 10 / 20 / 30 ... */
    public static readonly Dictionary<string, EDef> SUPERBOSSES = new()
    {
        ["omegathane"] = new EDef { name = "OMEGA BRICKTHANE", hp = 640, speed = 66, r = 40, scale = 3.1, dmg = 5, xp = 120, studs = new double[] { 120, 180 }, sig = "nova",
            es = 0.30, chaos = 0.15,
            col = FC("#2c3136", "#181c20", "#101315", "#3a4045", "#3a4045"), style = "boss" },
        ["megabaron"] = new EDef { name = "MEGA BONE BARON", hp = 400, speed = 70, r = 38, scale = 3.0, dmg = 4, xp = 130, studs = new double[] { 130, 190 }, sig = "spiral",
            hpMul = 1.30, armourMul = 2.0, dmgMul = 1.35, atkMul = 0.72,
            es = 0.45, chaos = 0.25,
            col = FC("#f6f2e4", "#d9cf9a", "#b3a878", "#f6f2e4", "#8a7d4a"), style = "skel" },
        ["ultralava"] = new EDef { name = "ULTRA LAVABRICK", hp = 520, speed = 52, r = 42, scale = 3.3, dmg = 5, xp = 150, studs = new double[] { 150, 220 }, sig = "meteor",
            es = 0.20, chaos = 0.35,
            col = FC("#ff7a2a", "#5e2a1a", "#331610", "#7a3a20", "#7a3a20"), style = "brute" },
    };
    /* which ultra appears at which multiple-of-10 wave */
    public static string ultraForWave(double n)
    {
        var keysS = SUPERBOSSES.Keys.ToList();
        int st = (int)Math.Max(1, Math.Floor(n / 10));
        return keysS[(st - 1) % keysS.Count];
    }

    /* NO TWO FIGHTS THE SAME: every boss rolls traits when it wakes up */
    public static readonly List<MonMod> BOSS_TRAITS = new()
    {
        new MonMod { id = "swift", name = "SWIFT", col = "#a5e06e", tip = "moves and swings faster",
            apply = b => { b.speed *= 1.20; b.atkS *= 0.85; } },
        new MonMod { id = "ironclad", name = "IRONCLAD", col = "#9ba19d", tip = "armoured, and slower for it",
            apply = b => { b.armour = Math.Round(b.armour * 1.7); b.speed *= 0.9; } },
        new MonMod { id = "brutal", name = "BRUTAL", col = "#ff5a4a", tip = "hits far harder",
            apply = b => { b.dmg = Math.Round(b.dmg * 1.35); } },
        new MonMod { id = "vital", name = "VITAL", col = "#ff8a8a", tip = "a deeper health bar",
            apply = b => { b.maxHp = Math.Round(b.maxHp * 1.30); b.hp = b.maxHp; } },
        new MonMod { id = "warded", name = "WARDED", col = "#9fd4ff", tip = "shrugs off spells",
            apply = b => { b.suppress = Math.Min(0.85, b.suppress + 0.25); } },
        new MonMod { id = "venomous", name = "VENOMOUS", col = "#8ede4a", tip = "its touch poisons you",
            apply = b => { b.venom = true; } },
        new MonMod { id = "restless", name = "RESTLESS", col = "#ffd166", tip = "attacks come round quicker",
            apply = b => { b.trRest = true; } },
        new MonMod { id = "legion", name = "LEGION", col = "#c9b8e8", tip = "brings more of its court",
            apply = b => { b.trLegion = true; } },
        new MonMod { id = "thorned", name = "THORNED", col = "#ffb066", tip = "hurts to stand next to",
            apply = b => { b.thorns = true; } },
        new MonMod { id = "bloodcall", name = "BLOOD-CALLER", col = "#e0203a", tip = "blood orbs rise around it and hunt you",
            apply = b => { b.orbAura = "blood"; } },
        new MonMod { id = "plaguecall", name = "PLAGUE-CALLER", col = "#8ede4a", tip = "plague orbs rise around it and hunt you",
            apply = b => { b.orbAura = "chaos"; } },
    };
    public static void rollBossTraits(Ent b)
    {
        /* one trait always, a second from wave ten on, and never the same one twice */
        var pool = BOSS_TRAITS.ToList();
        int want = (wave.n >= 10 ? 2 : 1) + (b.super ? 1 : 0);
        b.traits = new List<MonMod>();
        for (int i = 0; i < want && pool.Count > 0; i++)
        {
            int k = (int)(Js.R() * pool.Count);
            var t = pool.Splice(k, 1)[0];
            try { t.apply(b); } catch { }
            b.traits.Add(t);
        }
        if (b.traits.Count > 0)
            logEvent((b.bname ?? "BOSS") + " — " + string.Join(" + ", b.traits.Select(t => t.name)), "#ffb900");
    }

    /* ============================== RARE MONSTER MODIFIERS ============================== */
    public const double RARE_MOD_FROM = 6;
    public const double RM_NEAR_R = 300;
    public const double RM_SOUL_R = 650, RM_SOUL_MAX = 12;
    public const double RM_CORE_T = 1.4, RM_CORE_R = 120, RM_CORE_MUL = 2.5;
    public static int rareModCount(double n = double.NaN)
    {
        if (double.IsNaN(n)) n = wave.n;
        return n >= 30 ? 3 : n >= 15 ? 2 : n >= RARE_MOD_FROM ? 1 : 0;
    }
    public static readonly List<MonMod> RARE_MODS = new()
    {
        new MonMod { id = "hasted", name = "HASTED", col = "#a5e06e", tip = "moves 30% faster and attacks faster",
            apply = e => { e.speed *= 1.30; e.atkS *= 0.78; e.touchCd *= 0.78; } },
        new MonMod { id = "armoured", name = "ARMOURED", col = "#b8bfbb", tip = "double armour, and +25% to every elemental resistance",
            apply = e => { e.armour *= 2; foreach (var k in new[] { "fire", "frost", "shock" }) e.res[k] = Math.Min(0.75, e.res[k] + 0.25); } },
        new MonMod { id = "regen", name = "REGENERATING", col = "#ff8a8a", tip = "regenerates 2.5% of its life a second",
            apply = e => { e.rmRegen = 0.025; } },
        new MonMod { id = "volatile", name = "VOLATILE", col = "#ff8a3c", tip = "dies into a core that bursts 1.4s later for fire damage -- step away",
            apply = e => { e.rmVolatile = true; } },
        new MonMod { id = "souleater", name = "SOUL EATER", col = "#c9b8e8", tip = "every monster that dies near it makes it bigger, faster and harder-hitting",
            apply = e => { e.rmSoul = 0; } },
        new MonMod { id = "proximal", name = "PROXIMAL TANGIBILITY", col = "#9fd4ff", tip = "can only be hurt while you stand within its ring",
            apply = e => { e.rmNear = RM_NEAR_R; } },
        new MonMod { id = "berserk", name = "BERSERKER", col = "#ff5a4a", tip = "below half its life it hits 50% harder and moves 30% faster",
            apply = e => { e.rmBerserk = 1; } },
        new MonMod { id = "unstoppable", name = "UNSTOPPABLE", col = "#ffd166", tip = "cannot be knocked back, slowed, chilled, frozen or stunned",
            apply = e => { e.rmUnstop = true; } },
        new MonMod { id = "envenomed", name = "ENVENOMED", col = "#8ede4a", tip = "every hit it lands poisons you",
            apply = e => { e.rmVenom = true; } },
    };
    public static readonly Dictionary<string, MonMod> RARE_MOD_BY_ID = RARE_MODS.ToDictionary(m => m.id, m => m);
    public static bool applyRareMod(Ent e, string id)
    {
        var M = id != null ? RARE_MOD_BY_ID.GetValueOrDefault(id) : null;
        if (M == null || (e.rmods != null && e.rmods.Contains(id))) return false;
        (e.rmods ??= new List<string>()).Add(id);
        M.apply(e);
        return true;
    }
    public static List<string> rareModForce = null;   /* the test bench's pick: exactly these, whatever the wave */
    public static void rollRareMods(Ent e)
    {
        if (rareModForce != null && rareModForce.Count > 0) { foreach (var id in rareModForce) applyRareMod(e, id); return; }
        int want = rareModCount(); var pool = RARE_MODS.Select(m => m.id).ToList();
        for (int i = 0; i < want && pool.Count > 0; i++) applyRareMod(e, pool.Splice((int)(Js.R() * pool.Count), 1)[0]);
    }
    /* every frame, for a body that carries any of them */
    public static void rareModTick(Ent e, double dt)
    {
        if (Js.T(e.rmRegen) && e.hp < e.maxHp) e.hp = Math.Min(e.maxHp, e.hp + e.maxHp * e.rmRegen * dt);
        if (e.rmBerserk == 1 && e.hp < e.maxHp * 0.5)
        {
            e.rmBerserk = 2; e.dmg = e.dmg * 1.5; e.speed *= 1.30; e.atkS *= 0.85;
            addHitNum(e.x, e.y - 60 * Js.Or(e.scale, 1), "BERSERK!", "#ff5a4a", 16);
            addRing(e.x, e.y, 8, 300, 90, "#ff5a4a", 5, 0.4);
        }
        if (e.rmUnstop) { e.kx = 0; e.ky = 0; e.slowT = 0; e.chillN = 0; e.chillT = 0; e.frozen = null; e.stunT = 0; }
    }
    /* the rest happens when something dies */
    public static List<RmCore> rmCores = new();
    public static void rareModOnKill(Ent e)
    {
        if (e.rmVolatile)
            rmCores.Add(new RmCore { x = e.x, y = e.y, t = RM_CORE_T, dmg = Math.Max(2, Js.Or(e.dmg, 1) * RM_CORE_MUL), type = "volatile core", bname = "a volatile core" });
        foreach (var o in enemies.ToList())
        {
            if (o == e || o.dead || double.IsNaN(o.rmSoul) || o.rmSoul >= RM_SOUL_MAX) continue;
            if (hypot(o.x - e.x, o.y - e.y) > RM_SOUL_R) continue;
            o.rmSoul++; o.dmg = o.dmg * 1.06; o.speed *= 1.03; o.scale *= 1.025; o.r *= 1.025;
            if (Js.R() < 0.5) particles.Add(new Particle { kind = "spark", x = e.x, y = e.y - 20, vx = (o.x - e.x) * 1.4, vy = (o.y - e.y) * 1.4, life = 0.6, max = 0.6, color = "#c9b8e8" });
        }
        if (e.kindred != 0) kindredOnKill(e);
    }
    public static void updateRareMods(double dt)
    {
        if (rmCores.Count == 0) return;
        var h = heroObj ?? player;
        foreach (var c in rmCores.ToList())
        {
            c.t -= dt;
            if (c.t > 0) continue;
            c.done = true;
            addRing(c.x, c.y, 10, 520, RM_CORE_R, "#ff8a3c", 9, 0.45);
            addSparks(c.x, c.y - 10, 16, "#ffb066"); addShake(0.12);
            SFX.bomb();
            if (h != null && !h.dead && hypot(h.x - c.x, h.y - c.y) < RM_CORE_R + Js.Or(h.r, 12)) playerDamage(c.dmg, c, "fire", true);
            var cc = c;
            minionsIn(c.x, c.y, RM_CORE_R, m => minionHurt(m, cc.dmg, "fire", cc));
        }
        rmCores = rmCores.Filter(c => !c.done);
    }
    /* ---- KINDRED: a magic pack that has to die together ---- */
    public const double KINDRED_FROM = 8, KINDRED_CHANCE = 0.35, KINDRED_MAX = 8;
    public const double KINDRED_HP = 1.15, KINDRED_DMG = 1.20, KINDRED_SPD = 1.06, KINDRED_SIZE = 1.10, KINDRED_HEAL = 0.15;
    public static int kindredUid = 0;
    public static bool kindredGrow(Ent o)
    {
        if (o.kinN >= KINDRED_MAX) return false;
        o.kinN = o.kinN + 1;
        double hpK = o.hp / Math.Max(1, o.maxHp);
        o.maxHp = o.maxHp * KINDRED_HP;
        o.hp = Math.Min(o.maxHp, o.maxHp * hpK + o.maxHp * KINDRED_HEAL);
        o.dmg = o.dmg * KINDRED_DMG; o.speed *= KINDRED_SPD; o.scale *= KINDRED_SIZE; o.r *= KINDRED_SIZE;
        o.flash = 1;
        addRing(o.x, o.y, 8, 300, 70 * o.scale, MAGIC_COL, 5, 0.4);
        addHitNum(o.x, o.y - 56 * o.scale, "KINDRED ×" + Js.Num(o.kinN), MAGIC_COL, 15);
        return true;
    }
    public static void kindredOnKill(Ent e)
    {
        foreach (var o in enemies.ToList()) if (o != e && !o.dead && o.kindred == e.kindred) kindredGrow(o);
    }
    /* the magic pack, as the wave spawns it: n bodies in a loose ring, bound or not */
    public static List<Ent> spawnMagicPack(string type, Obj pos, double n, bool? kinForce = null)
    {
        bool bound = kinForce.HasValue ? kinForce.Value : (wave.n >= KINDRED_FROM && Js.R() < KINDRED_CHANCE);
        int kin = bound ? ++kindredUid : 0;
        var outL = new List<Ent>();
        for (int k = 0; k < n; k++)
        {
            double a = k / n * TAU + rnd(-0.25, 0.25), rr = rnd(40, 30 + n * 9);
            spawnEnemy(type, clamp(pos.x + Math.Cos(a) * rr, W.wall + 50, W.w - W.wall - 50),
                clamp(pos.y + Math.Sin(a) * rr, W.wall + 50, W.h - W.wall - 50), false, true);
            var e = enemies[^1];
            if (kin != 0) e.kindred = kin;
            outL.Add(e);
        }
        return outL;
    }
    /* DRAWN over everything: a rare's names, its ring if it has one, the cores, and
       a bound pack's tethers */
    public static void drawMonsterMods()
    {
        var c = ctx;
        foreach (var k in rmCores)
        {
            double f = 1 - clamp(k.t / RM_CORE_T, 0, 1);
            c.fillStyle = rgba(255, 138, 60, 0.08 + 0.12 * f); c.beginPath(); c.arc(k.x, k.y, RM_CORE_R, 0, TAU); c.fill();
            c.setLineDash(new double[] { 10, 8 }); c.strokeStyle = rgba(255, 176, 102, 0.5 + 0.35 * Math.Sin(vtime * 18)); c.lineWidth = 3;
            c.beginPath(); c.arc(k.x, k.y, RM_CORE_R, 0, TAU); c.stroke(); c.setLineDash(new double[0]);
            c.strokeStyle = "rgba(255,230,160,0.9)"; c.lineWidth = 3; c.beginPath(); c.arc(k.x, k.y, 10 + (RM_CORE_R - 10) * f, 0, TAU); c.stroke();
            c.fillStyle = "#ffb066"; c.beginPath(); c.arc(k.x, k.y - 8, 7 + 3 * Math.Sin(vtime * 20), 0, TAU); c.fill();
        }
        var groups = new Dictionary<int, List<Ent>>(); var gOrder = new List<int>();
        c.textAlign = "center";
        foreach (var e in enemies)
        {
            if (e.dead || e.state != "active" || !onScreen(e, 120)) continue;
            if (e.kindred != 0)
            {
                if (!groups.TryGetValue(e.kindred, out var g)) { g = new List<Ent>(); groups[e.kindred] = g; gOrder.Add(e.kindred); }
                g.Add(e);
            }
            if (e.rmods == null || e.rmods.Count == 0) continue;
            if (Js.T(e.rmNear))
            {
                c.setLineDash(new double[] { 8, 9 }); c.strokeStyle = "rgba(159,212,255,0.45)"; c.lineWidth = 2;
                c.beginPath(); c.arc(e.x, e.y, e.rmNear, 0, TAU); c.stroke(); c.setLineDash(new double[0]);
            }
            double y = e.y - 64 * Js.Or(e.scale, 1) - 8;
            c.font = "700 12px Fredoka";
            for (int i = e.rmods.Count - 1; i >= 0; i--)
            {
                var M = RARE_MOD_BY_ID[e.rmods[i]];
                string t = M.id == "souleater" && Js.T(e.rmSoul) ? M.name + " ×" + Js.Num(e.rmSoul) : M.name;
                c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 3; c.strokeText(t, e.x, y);
                c.fillStyle = M.col; c.fillText(t, e.x, y);
                y -= 13;
            }
        }
        foreach (var gk in gOrder)
        {
            var g = groups[gk];
            if (g.Count < 1) continue;
            double cx = 0, cy = 0; foreach (var e in g) { cx += e.x; cy += e.y; }
            cx /= g.Count; cy /= g.Count;
            c.strokeStyle = rgba(110, 160, 255, 0.22 + 0.12 * Math.Sin(vtime * 4)); c.lineWidth = 2;
            foreach (var e in g) { c.beginPath(); c.moveTo(cx, cy); c.lineTo(e.x, e.y - 14 * Js.Or(e.scale, 1)); c.stroke(); }
            double top = g.Min(e => e.y - 60 * Js.Or(e.scale, 1));
            double n = g.Max(e => e.kinN);
            string tt = "KINDRED" + (Js.T(n) ? " ×" + Js.Num(n) : "");
            c.font = "700 12px Fredoka";
            c.strokeStyle = "rgba(15,20,25,0.85)"; c.lineWidth = 3; c.strokeText(tt, cx, top - 6);
            c.fillStyle = MAGIC_COL; c.fillText(tt, cx, top - 6);
        }
    }

    /* ============================== THE SPECIALS ============================== */
    /* ---- THE TREASURE GOBLIN ---- */
    public const double GOBLIN_FROM = 4, GOBLIN_CHANCE = 0.10;
    public const double GOBLIN_LIFE = 14, GOBLIN_PORTAL_T = 2.2, GOBLIN_CALM = 520;
    public static readonly Tbl GOBLIN_STUDS = new double[] { 30, 10 };   /* 30 + 10 a wave, x your stud multiplier */
    public static GoblinPending goblinPending = null;
    public static bool goblinRoll(double n)
    {
        goblinPending = (n >= GOBLIN_FROM && Js.R() < goblinChanceNow()) ? new GoblinPending { delay = rnd(6, 20) } : null;
        return goblinPending != null;
    }
    public static Ent goblinSpawn(double x = double.NaN, double y = double.NaN)
    {
        if (double.IsNaN(x)) { var sp = randomSpawnPos(); x = sp.x; y = sp.y; }
        spawnEnemy("goblin", x, y);
        var e = enemies[^1];
        e.state = "active"; e.mode = "flee"; e.gobT = 0; e.gobDrop = 0.9;
        showBanner("A TREASURE GOBLIN!", "CATCH IT BEFORE IT PORTALS OUT", "#7ac943", 2.2, 40);
        logEvent("   a TREASURE GOBLIN — it runs, and it portals out in " + Js.Num(GOBLIN_LIFE) + "s", "#7ac943");
        SFX.fortune();
        return e;
    }
    static double GobDrop(Ent e) => double.IsNaN(e.gobDrop) ? 0.9 : e.gobDrop;
    public static Pt goblinTick(Ent e, double dt, double toP = 0, double dP = 0)
    {
        e.mode = "flee"; e.gobT = e.gobT + dt;
        /* it leaks studs as it is hit: a tenth of its life, a handful */
        while (e.hp < e.maxHp * GobDrop(e) && GobDrop(e) > 0)
        {
            spawnStuds(e.x, e.y, 2 + Math.Round(wave.n * 0.4)); e.gobDrop = GobDrop(e) - 0.1;
        }
        if (!double.IsNaN(e.gobPortal))
        {
            e.gobPortal -= dt;
            if (Js.R() < 0.6) particles.Add(new Particle { kind = "spark", x = e.x + rnd(-26, 26), y = e.y - rnd(0, 60), vx = rnd(-20, 20), vy = rnd(-60, -20), life = 0.5, max = 0.5, color = "#7ac943" });
            if (e.gobPortal <= 0) goblinEscape(e);
            return new Pt(0, 0);
        }
        if (e.gobT >= GOBLIN_LIFE)
        {
            e.gobPortal = GOBLIN_PORTAL_T;
            addHitNum(e.x, e.y - 60, "OPENING A PORTAL!", "#7ac943", 17);
            return new Pt(0, 0);
        }
        var h = heroObj ?? player;
        double ax = e.x - h.x, ay = e.y - h.y; double d = Js.Or(hypot(ax, ay), 1); ax /= d; ay /= d;
        double m = W.wall + 240;
        if (e.x < m) ax += 1.3; else if (e.x > W.w - m) ax -= 1.3;
        if (e.y < m) ay += 1.3; else if (e.y > W.h - m) ay -= 1.3;
        double a = Math.Atan2(ay, ax) + Math.Sin(vtime * 2.3 + e.phase) * 0.35;
        double sp2 = d < GOBLIN_CALM ? e.speed : e.speed * 0.55;
        if (Js.R() < 0.08) particles.Add(new Particle { kind = "spark", x = e.x - 10, y = e.y - 26, vx = rnd(-30, 30), vy = rnd(-50, -10), life = 0.5, max = 0.5, color = "#ffd166" });
        return new Pt(Math.Cos(a) * sp2, Math.Sin(a) * sp2);
    }
    public static void goblinEscape(Ent e)
    {
        e.dead = true; e.escaped = true;
        addRing(e.x, e.y, 10, 420, 110, "#7ac943", 7, 0.5);
        showBanner("THE GOBLIN GOT AWAY", "AND YOUR STUDS WITH IT", "#9aa39c", 1.8, 34);
        logEvent("   the treasure goblin portalled out", "#9aa39c");
    }
    public static (double studs, int chests) goblinLoot(Ent e)
    {
        double n = Math.Round((GOBLIN_STUDS[0] + GOBLIN_STUDS[1] * wave.n) * Js.Or(player.studMul, 1));
        spawnStuds(e.x, e.y, n);
        pickups.Add(makePickup("heart", e.x + rnd(-20, 20), e.y + rnd(-20, 20)));
        var r1 = rollRarity("boss"); queuePick("chest", r1);
        bool two = Js.R() < 0.5; if (two) queuePick("chest", rollRarity("boss"));
        showBanner("TREASURE GOBLIN SLAIN!", Js.Num(n) + " STUDS · " + (two ? "TWO CHESTS" : "A CHEST") + " ON R", "#ffd166", 2.2, 42);
        addRing(e.x, e.y, 12, 520, 160, "#ffd166", 8, 0.6); addShake(0.25);
        return (n, two ? 2 : 1);
    }
    /* ---- THE MIMIC ---- */
    public const double MIMIC_FROM = 5, MIMIC_CHANCE = 0.18;
    public static Ent mimicReveal(Prop p)
    {
        p.dead = true;
        spawnEnemy("mimic", p.x, p.y);
        var e = enemies[^1];
        e.state = "active"; e.mimicRar = p.wild ? "epic" : (p.rar ?? "common");
        e.mode = "chase"; e.atkCd = 0.25;
        showBanner("IT'S A MIMIC!", "KILL IT FOR WHAT IT WAS HIDING", "#ff5a4a", 1.8, 40);
        addRing(p.x, p.y, 8, 360, 90, "#ff5a4a", 6, 0.45); addShake(0.2);
        SFX.roar();
        return e;
    }
    public static string mimicLoot(Ent e)
    {
        /* what it swallowed, a tier better for the trouble -- never past epic */
        int rk = Math.Min(rarRank(e.mimicRar ?? "common") + 1, rarRank("epic"));
        string rar = RAR_ORDER[rk];
        queuePick("chest", rar);
        spawnStuds(e.x, e.y, 10 + wave.n * 3);
        var R = RARITY.GetValueOrDefault(rar) ?? RARITY["common"];
        showBanner("THE MIMIC'S HOARD", R.name + " BRICK · ON R", R.col, 1.8, 38);
        return rar;
    }
    /* ---- THE BANNER-BEARER ---- */
    public const double BANNER_R = 280, BANNER_PLANT_D = 420, BANNER_PLANT_T = 0.9, BANNER_REPLANT = 10;
    public const double BANNER_DMG = 1.30, BANNER_SPD = 1.20, BANNER_ATK = 0.85;
    /* HOW MANY MAY STAND AT ONCE */
    public static double bannerCap(double n = double.NaN) => Math.Min(3, 1 + Math.Floor(Math.Max(0, (double.IsNaN(n) ? wave.n : n) - 12) / 12));
    public static int bannersUp() => enemies.Count(b => b.type == "warbanner" && !b.dead);
    /* A BANNER IS NOT A MONSTER: not counted in what is left of a wave */
    public static bool isBanner(Ent e) => e != null && e.type == "warbanner";
    public static Ent plantBanner(Ent e)
    {
        double fx = e.face != null ? e.face.x : 0, fy = e.face != null ? e.face.y : 1, fl = Js.Or(hypot(fx, fy), 1);
        spawnEnemy("warbanner", clamp(e.x + fx / fl * 18, W.wall + 40, W.w - W.wall - 40), clamp(e.y + fy / fl * 18, W.wall + 40, W.h - W.wall - 40));
        var b = enemies[^1];
        b.state = "active"; b.mode = "planted"; b.bearer = e;
        e.banner = b; e.plantCd = BANNER_REPLANT;
        addRing(b.x, b.y, 8, 360, BANNER_R, "#fff0b0", 3, 0.3);
        SFX.clank();
        return b;
    }
    public static void bannerSet(Ent e, bool on)
    {
        if (e.bannered == on) return;
        e.bannered = on;
        if (on) { e.dmg *= BANNER_DMG; e.speed *= BANNER_SPD; e.atkS *= BANNER_ATK; }
        else { e.dmg /= BANNER_DMG; e.speed /= BANNER_SPD; e.atkS /= BANNER_ATK; }
    }
    public static void updateBanners()
    {
        var bs = enemies.Where(b => b.type == "warbanner" && !b.dead && b.state == "active").ToList();
        foreach (var e in enemies)
        {
            if (e.dead || e.type == "warbanner") continue;
            bannerSet(e, bs.Count > 0 && bs.Any(b => hypot(b.x - e.x, b.y - e.y) < BANNER_R));
        }
    }
    /* the part of all three the main loop runs */
    public static void updateSpecials(double dt)
    {
        if (goblinPending != null && wave.state == "fight")
        {
            goblinPending.delay -= dt;
            if (goblinPending.delay <= 0) { goblinPending = null; goblinSpawn(); }
        }
        updateBanners();
    }
    public static void specialOnKill(Ent e)
    {
        if (e.type == "goblin") goblinLoot(e);
        else if (e.type == "mimic") mimicLoot(e);
        else if (e.type == "warbanner" && e.bearer != null && e.bearer.banner == e) e.bearer.banner = null;
    }
    /* ---- DRAWN ---- */
    public static void drawMimic(Ctx c, Ent e)
    {
        Col rc = (RARITY.GetValueOrDefault(e.mimicRar ?? "") ?? RARITY["common"]).col; double s = Js.Or(e.scale, 1) * 1.25;
        bool biting = e.mode == "lunge" || e.strikeT > 0;
        double chomp = biting ? 1 : 0.3 + 0.3 * Math.Abs(Math.Sin(vtime * 7 + e.phase));
        double hop = e.moving ? Math.Abs(Math.Sin(e.walk * 0.6)) * 7 : 0;
        c.save(); c.translate(e.x, e.y);
        c.fillStyle = "rgba(0,0,0,0.25)"; c.beginPath(); c.ellipse(0, 2, 18 * s, 6 * s, 0, 0, TAU); c.fill();
        c.translate(0, -hop); c.scale(s, s);
        c.strokeStyle = "rgba(20,30,40,0.7)"; c.lineWidth = 2;
        double lp = Math.Sin(e.walk) * 3;
        c.fillStyle = sh(rc, -0.55); c.fillRect(-13, -3 + lp * 0.3, 6, 7); c.fillRect(7, -3 - lp * 0.3, 6, 7);
        c.fillStyle = sh(rc, -0.42); roundRect(c, -16, -16, 32, 16, 3); c.fill(); c.stroke();
        double gap = 3 + chomp * 13;
        c.fillStyle = "#3a0c16"; roundRect(c, -15, -16 - gap, 30, gap + 2, 2); c.fill();
        c.fillStyle = "#e0457a"; c.beginPath(); c.ellipse(4, -16 - gap * 0.35, 6, 3, 0.2, 0, TAU); c.fill();
        c.fillStyle = "#fff";
        for (int i = 0; i < 5; i++) { double x = -15 + i * 6; c.beginPath(); c.moveTo(x, -16); c.lineTo(x + 3, -21); c.lineTo(x + 6, -16); c.fill(); }
        c.save(); c.translate(-16, -16 - gap);
        c.fillStyle = sh(rc, -0.22); roundRect(c, 0, -10, 32, 10, 3); c.fill(); c.stroke();
        c.fillStyle = "#fff";
        for (int i = 0; i < 5; i++) { double x = 1 + i * 6; c.beginPath(); c.moveTo(x, 0); c.lineTo(x + 3, 5); c.lineTo(x + 6, 0); c.fill(); }
        c.fillStyle = "#ffe95a"; c.beginPath(); c.arc(16, -5, 3.4, 0, TAU); c.fill();
        c.fillStyle = "#1b1f23"; c.fillRect(15.2, -7.5, 1.6, 5);
        c.restore();
        c.fillStyle = rc; roundRect(c, -3, -16, 6, 16, 2); c.fill(); c.stroke();
        c.restore();
    }
    public static void drawWarBanner(Ctx c, Ent e)
    {
        double pulse = 0.5 + 0.5 * Math.Sin(vtime * 3 + e.phase);
        /* pale and faint: the ground it holds should be readable, not a red alarm */
        c.fillStyle = rgba(255, 240, 176, 0.03 + 0.015 * pulse);
        c.beginPath(); c.arc(e.x, e.y, BANNER_R, 0, TAU); c.fill();
        c.setLineDash(new double[] { 12, 10 }); c.strokeStyle = rgba(255, 240, 176, 0.16 + 0.08 * pulse); c.lineWidth = 2;
        c.beginPath(); c.arc(e.x, e.y, BANNER_R, 0, TAU); c.stroke(); c.setLineDash(new double[0]);
        c.save(); c.translate(e.x, e.y);
        c.fillStyle = "rgba(0,0,0,0.25)"; c.beginPath(); c.ellipse(0, 2, 12, 5, 0, 0, TAU); c.fill();
        c.fillStyle = "#6d6e68"; c.strokeStyle = "rgba(20,30,40,0.6)"; c.lineWidth = 2;
        roundRect(c, -8, -6, 16, 8, 2); c.fill(); c.stroke();
        c.fillStyle = "#7b5132"; c.fillRect(-2, -84, 4, 80); c.strokeRect(-2, -84, 4, 80);
        drawStudG(c, 0, -86, 3.5, PAL.gold);
        double wv = Math.Sin(vtime * 4 + e.phase) * 3;
        c.fillStyle = e.flash > 0 ? "#ffffff" : "#a8201a";
        c.beginPath(); c.moveTo(2, -80); c.quadraticCurveTo(18, -80 + wv, 34, -78); c.lineTo(34, -50);
        c.quadraticCurveTo(18, -52 - wv, 2, -48); c.closePath(); c.fill(); c.stroke();
        c.fillStyle = PAL.gold; c.beginPath(); c.moveTo(18, -72); c.lineTo(24, -64); c.lineTo(18, -56); c.lineTo(12, -64); c.closePath(); c.fill();
        c.restore();
    }
    static readonly (double, double)[] SACK_STUDS = { (-18, -44), (-10, -45), (-14, -47) };
    public static void drawSpecialExtras(Ctx c, Ent e)
    {
        if (e.type == "goblin")
        {
            /* the sack, and a few of your studs spilling out of it */
            double bob = Math.Sin(e.walk * 0.8) * 2, s = Js.Or(e.scale, 1);
            c.fillStyle = "#b0835a"; c.strokeStyle = "rgba(20,30,40,0.6)"; c.lineWidth = 2;
            c.beginPath(); c.ellipse(e.x - 14 * s, e.y - 34 * s + bob, 13 * s, 15 * s, -0.2, 0, TAU); c.fill(); c.stroke();
            c.fillStyle = "#7a4f2a"; c.fillRect(e.x - 17 * s, e.y - 50 * s + bob, 7 * s, 4 * s);
            foreach (var (dx, dy) in SACK_STUDS) drawStudG(c, e.x + dx * s, e.y + dy * s + bob, 2.6 * s, PAL.gold);
            if (!double.IsNaN(e.gobPortal))
            {
                double k = 1 - clamp(e.gobPortal / GOBLIN_PORTAL_T, 0, 1);
                c.strokeStyle = rgba(122, 201, 67, 0.5 + 0.4 * Math.Sin(vtime * 20)); c.lineWidth = 5;
                c.beginPath(); c.ellipse(e.x, e.y - 30, 26 + 20 * k, 40 + 26 * k, 0, 0, TAU); c.stroke();
            }
        }
        else if (e.type == "bannerman" && !(e.banner != null && !e.banner.dead))
        {
            double s = Js.Or(e.scale, 1);
            c.fillStyle = "#7b5132"; c.fillRect(e.x + 9 * s, e.y - 80 * s, 3 * s, 70 * s);
            c.fillStyle = "#a8201a"; c.strokeStyle = "rgba(20,30,40,0.6)"; c.lineWidth = 1.5;
            roundRect(c, e.x + 6 * s, e.y - 78 * s, 9 * s, 30 * s, 3); c.fill(); c.stroke();
        }
    }

    /* ============================== BLOOD ORBS ==============================
       A red ball rises beside the caster and HUNTS you, then hangs over a circle
       and drops: physical damage and a bleed. The CHAOS kind is green: chaos and poison. */
    public const double WD_CAST_T = 0.7, WD_ORBS = 3, WD_CD = 4.2;
    public const double ORB_TTL = 4.0, ORB_SPD0 = 45, ORB_ACC = 60, ORB_SPD_MAX = 330, ORB_TURN = 2.4;
    public const double ORB_HOLD_T = 1.0, ORB_R = 58, ORB_TOUCH = 24, ORB_CAP = 48;
    public const double ORB_AURA_T = 5, ORB_AURA_N = 2, ORB_DMG_MUL = 0.9;
    public static readonly Dictionary<string, OrbKind> ORB_KINDS = new()
    {
        ["blood"] = new OrbKind { col = "#e0203a", r = 224, g = 32, b = 58, elem = null, name = "blood orb" },
        ["chaos"] = new OrbKind { col = "#8ede4a", r = 142, g = 222, b = 74, elem = "chaos", name = "plague orb" },
    };
    public static List<BloodOrb> bloodOrbs = new();
    public static double orbDmg(Obj e) => Math.Max(1, (e != null ? Js.Or(e.dmg, 1) : 1) * ORB_DMG_MUL);
    public static BloodOrb orbSpawn(double x, double y, string kind, double dmg, Obj src)
    {
        if (bloodOrbs.Count >= ORB_CAP) return null;
        var h = heroObj ?? player;
        /* it sets off roughly your way */
        double a = (h != null ? Math.Atan2(h.y - y, h.x - x) : 0) + rnd(-1, 1);
        var K = (kind != null ? ORB_KINDS.GetValueOrDefault(kind) : null) ?? ORB_KINDS["blood"];
        string who = src != null ? (src.bname ?? src.type) : null;
        var o = new BloodOrb
        {
            x = x, y = y, px = x, py = y, vx = Math.Cos(a) * ORB_SPD0, vy = Math.Sin(a) * ORB_SPD0, t = 0, hold = 0,
            kind = kind != null && ORB_KINDS.ContainsKey(kind) ? kind : "blood", dmg = dmg, state = "fly", phase = rnd(TAU),
            type = K.name, bname = !string.IsNullOrEmpty(who) ? who + "'s " + K.name : "a " + K.name
        };
        bloodOrbs.Add(o);
        return o;
    }
    public static void orbDrop(BloodOrb o)
    {
        var K = ORB_KINDS[o.kind]; var h = heroObj ?? player;
        addRing(o.x, o.y, 8, 420, ORB_R, K.col, 6, 0.35);
        addSparks(o.x, o.y - 4, 10, K.col);
        sfxAt(o.x).hit();
        if (h != null && !h.dead && hypot(h.x - o.x, h.y - o.y) < ORB_R + Js.Or(h.r, 12))
        {
            var keep = player; player = h;
            try
            {
                bool can = !h.dead && h.invuln <= 0 && h.dash.t <= 0 && !ritualOutside(o) && !godMode;
                playerDamage(o.dmg, o, K.elem, true);
                if (can) { if (o.kind == "chaos") poisonPlayer(Math.Max(0.2, o.dmg * 0.4), PSN_DUR); else bleedPlayer(o.dmg); }
            }
            finally { player = keep; }
        }
        minionsIn(o.x, o.y, ORB_R, m => minionHurt(m, o.dmg, K.elem, o));
    }
    public static void updateBloodOrbs(double dt)
    {
        /* the casters that carry them as an aura: a boss that calls blood */
        foreach (var e in enemies.ToList())
        {
            if (e.dead || e.state != "active" || e.orbAura == null) continue;
            e.orbAuraT = (double.IsNaN(e.orbAuraT) ? 2 : e.orbAuraT) - dt;
            if (e.orbAuraT <= 0)
            {
                e.orbAuraT = ORB_AURA_T;
                for (int i = 0; i < ORB_AURA_N + (e.super ? 1 : 0); i++) orbSpawn(e.x + rnd(-70, 70), e.y - 20 + rnd(-50, 50), e.orbAura, orbDmg(e), e);
            }
        }
        if (bloodOrbs.Count == 0) return;
        var h = heroObj ?? player;
        foreach (var o in bloodOrbs.ToList())
        {
            o.px = o.x; o.py = o.y;
            if (o.state == "fly")
            {
                o.t += dt;
                /* it gathers speed the longer it lives */
                double sp = Math.Min(ORB_SPD_MAX, ORB_SPD0 + ORB_ACC * o.t * (1 + 0.6 * o.t));
                double want = Math.Atan2(h.y - o.y, h.x - o.x), cur = Math.Atan2(o.vy, o.vx);
                double na = cur + clamp(angDiff(want, cur), -ORB_TURN * dt, ORB_TURN * dt);
                o.vx = Math.Cos(na) * sp; o.vy = Math.Sin(na) * sp;
                o.x += o.vx * dt; o.y += o.vy * dt;
                if (o.t >= ORB_TTL || hypot(h.x - o.x, h.y - o.y) < ORB_TOUCH) { o.state = "hold"; o.hold = ORB_HOLD_T; }
            }
            else if (o.state == "hold")
            {
                o.hold -= dt;
                if (o.hold <= 0) { orbDrop(o); o.dead = true; }
            }
        }
        bloodOrbs = bloodOrbs.Filter(o => !o.dead);
    }
    public static void drawBloodOrbs()
    {
        var c = ctx;
        foreach (var o in bloodOrbs)
        {
            if (!onScreen(o, 120)) continue;
            var K = ORB_KINDS[o.kind];
            if (o.state == "hold")
            {
                double f = 1 - clamp(o.hold / ORB_HOLD_T, 0, 1);
                c.fillStyle = rgba(K.r, K.g, K.b, 0.10 + 0.18 * f); c.beginPath(); c.arc(o.x, o.y, ORB_R, 0, TAU); c.fill();
                c.strokeStyle = rgba(K.r, K.g, K.b, 0.55 + 0.35 * Math.Sin(vtime * 22)); c.lineWidth = 3;
                c.beginPath(); c.arc(o.x, o.y, ORB_R, 0, TAU); c.stroke();
                c.strokeStyle = "rgba(255,255,255,0.8)"; c.lineWidth = 2; c.beginPath(); c.arc(o.x, o.y, 6 + (ORB_R - 6) * (1 - f), 0, TAU); c.stroke();
            }
            double z = o.state == "hold" ? 34 : 26 + Math.Sin(vtime * 6 + o.phase) * 4;
            c.fillStyle = "rgba(0,0,0,0.22)"; c.beginPath(); c.ellipse(o.x, o.y + 2, 8, 3, 0, 0, TAU); c.fill();
            c.fillStyle = rgba(K.r, K.g, K.b, 0.28); c.beginPath(); c.arc(o.x, o.y - z, 15, 0, TAU); c.fill();
            c.fillStyle = K.col; c.beginPath(); c.arc(o.x, o.y - z, 8.5, 0, TAU); c.fill();
            c.fillStyle = "rgba(255,255,255,0.75)"; c.beginPath(); c.arc(o.x - 2.5, o.y - z - 3, 2.6, 0, TAU); c.fill();
        }
    }
}
