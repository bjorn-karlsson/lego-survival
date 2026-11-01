namespace Brickblade;

/* ========================= THE SKILL TREE: the stats (JS 12666–13044) =========================
   The save stores NODE IDS, never the stats they grant; points spent are summed
   from the live table; points earned are derived from achievements; nodes write
   into the same pools the bricks do. META_STATS went through tools/js2cs.py. */
public sealed partial class MetaStat
{
    public double pct, px, deg, scale; public string col; public string[] only;
    /* a writer that takes a word rather than a number: 'grant' takes a spell key */
    public Action<Ent, string> ws;
}
public sealed class MetaMastery { public string name, icon, note; public Func<Ent, bool> done; }
/* one line of a node: [stat, value] -- the value a number, a word ('grant'), or nothing at all (a switch) */
public sealed class Mod
{
    public string k; public double v = double.NaN; public string s;
    public Mod(string k) { this.k = k; }
    public Mod(string k, double v) { this.k = k; this.v = v; }
    public Mod(string k, string s) { this.k = k; this.s = s; }
    public bool num => !double.IsNaN(v);
    public static implicit operator Mod((string k, double v) t) => new Mod(t.k, t.v);
    public static implicit operator Mod((string k, string s) t) => new Mod(t.k, t.s);
}

public static partial class G
{
    public const string META_KEY = "bb_meta";
    public const double META_VER = 2;
    /* The square root compresses a full clear to about 4x a wave-15 death */
    public const double META_BANK_K = 4;
    /* THE HARD CEILING on points */
    public const double META_POINT_MAX = 120;
    /* BUMP THIS WHENEVER THE LAYOUT MOVES */
    public const double META_TREE_VER = 4;
    /* what a node's DAMAGE INCREASE and ATTRIBUTE are worth, whatever its template wrote */
    public static readonly System.Text.RegularExpressions.Regex META_INC_DMG =
        new(@"^(incMelee|incSpell|incAll|incElem|incFire|incFrost|incShock|incChaos|incDot|minionDmg|inc_[a-z]+)$");
    public const double META_INC_MINOR = 0.16, META_INC_NOTABLE = 0.30;
    public const double META_ATTR_MINOR = 10, META_ATTR_NOTABLE = 20;
    /* ONE POINT A LEVEL */
    public const double META_POINTS_PER_LEVEL = 1;
    /* MASTERY: every CAPPED brick of one spell, at its cap */
    public static readonly Dictionary<string, MetaMastery> META_MASTERY = new()
    {
        ["bane"] = new MetaMastery { name = "BRICKBANE MASTER", icon = "bane",
            note = "every capped Brickbane brick at its cap \u2014 stacks, spread, reach, " +
                   "rate, dose length and Spore Burst. Flat poison damage has no cap, so it is not asked for.",
            done = p => p != null && p.bane != null && p.bane.n > 0
                && p.bane.stacks >= BANE_STACK_MAX
                && p.bane.arc >= BANE_ARC_MAX - 0.01
                && p.bane.r >= BANE_R_MAX - 0.5
                && p.bane.rate <= BANE_MIN_RATE + 1e-6
                && p.bane.dotT >= BANE_DOT_MAX_T - 1e-6
                && p.bane.burst >= BANE_BURST.Length - 1 },
    };
    /* INTELLIGENCE FEEDS THE LEGION: [stat it rides on, minion stat, how much of it] */
    public static readonly Dictionary<string, (string k, double m)> MINION_SISTERS = new()
    {
        ["incSpell"] = ("minionDmg", 0.8), ["incElem"] = ("minionDmg", 0.8), ["incAll"] = ("minionDmg", 0.8),
        ["incFire"] = ("minionDmg", 0.6), ["incFrost"] = ("minionDmg", 0.6), ["incShock"] = ("minionDmg", 0.6), ["incChaos"] = ("minionDmg", 0.6),
        ["incCast"] = ("minionSpd", 0.8),
        ["es"] = ("minionLife", 0.012), ["incEs"] = ("minionLife", 0.5), ["allRes"] = ("minionLife", 2),
        ["fireRes"] = ("minionLife", 1), ["frostRes"] = ("minionLife", 1), ["shockRes"] = ("minionLife", 1), ["incRegen"] = ("minionLife", 0.5),
    };
    public static Mod minionSister(string stat, double v)
    {
        if (stat == null || !MINION_SISTERS.TryGetValue(stat, out var s2) || !(v > 0)) return null;
        return new Mod(s2.k, Math.Round(v * s2.m * 1000) / 1000);
    }
    public static readonly Dictionary<string, MetaStat> META_STATS = new()
    {
      ["incMelee"] = new MetaStat { n = "increased physical damage",    pct = 1, w = (p,v)=>{ p.incMelee += v; } },
      ["incSpell"] = new MetaStat { n = "increased spell damage",       pct = 1, w = (p,v)=>{ p.incSpell += v; } },
      ["incAll"] = new MetaStat { n = "increased damage",             pct = 1, w = (p,v)=>{ p.incAll += v; } },
      ["moreAll"] = new MetaStat { n = "MORE damage",                  pct = 1, w = (p,v)=>{ p.moreAll *= (1+v); } },
      ["flatPhys"] = new MetaStat { n = "flat physical damage",                w = (p,v)=>{ p.flatPhys += v; } },
      ["flatElem"] = new MetaStat { n = "flat elemental damage",               w = (p,v)=>{ p.flatElem += v; } },
      ["incAtk"] = new MetaStat { n = "increased attack speed",       pct = 1, w = (p,v)=>{ p.inc["atk"] += v; } },
      ["incCast"] = new MetaStat { n = "increased cast speed",         pct = 1, w = (p,v)=>{ p.inc["cast"] += v; } },
      ["incCrit"] = new MetaStat { n = "increased critical chance",    pct = 1, w = (p,v)=>{ p.incCrit += v; } },
      ["critMult"] = new MetaStat { n = "critical multiplier",          pct = 1, w = (p,v)=>{ p.critMultFlat += v*100; } },
      ["reach"] = new MetaStat { n = "melee reach",                  px = 1,  w = (p,v)=>{ p.reachFlat += v; },
                    only = ["sword","axe","mace"] },
      /* ailments and everything that lingers */
      ["incDot"] = new MetaStat { n = "increased damage over time",   pct = 1, w = (p,v)=>{ p.inc["dot"] += v; } },
      ["lessDot"] = new MetaStat { n = "LESS damage over time",        pct = 1, w = (p,v)=>{ p.dotMore *= (1 - v); } },
      ["bleedChance"] = new MetaStat { n = "chance to open a wound",       pct = 1, w = (p,v)=>{ p.bleedChanceFlat += v; } },
      ["bleedShare"] = new MetaStat { n = "of the hit bleeds",            pct = 1, w = (p,v)=>{ p.bleedShareFlat += v; } },
      ["poisonDot"] = new MetaStat { n = "poison damage a second",              w = (p,v)=>{ p.baneDotFlat += v; } },
      ["ailChance"] = new MetaStat { n = "chance to leave an ailment",   pct = 1, w = (p,v)=>{ p.ailChanceFlat += v; } },
      /* ---- RAGE. Generation is bought, the ceiling is bought, and neither of them
         exists until you walk to it. ---- */
      ["rageHit"] = new MetaStat { n = "RAGE when you land a hit",            w = (p,v)=>{ p.rageOnHit  += v; } },
      ["rageHurt"] = new MetaStat { n = "RAGE when you are hit",               w = (p,v)=>{ p.rageOnHurt += v; } },
      ["rageMax"] = new MetaStat { n = "maximum RAGE",                        w = (p,v)=>{ p.stackMax["rage"] += v; } },
      /* ---- THE THREE CHARGES. Generation is bought one thing at a time -- kill,
         crit, or take a hit -- so which charge a build runs on says what it does.
         The ceilings are their own stats, and a node that raises all three is the
         reason to walk to it. ---- */
      ["chargeKill"] = new MetaStat { n = "FRENZY charge when you kill",         w = (p,v)=>{ p.chargeKill += v; } },
      ["chargeCrit"] = new MetaStat { n = "POWER charge on a critical",          w = (p,v)=>{ p.chargeCrit += v; } },
      ["chargeHurt"] = new MetaStat { n = "ENDURANCE charge when you are hit",   w = (p,v)=>{ p.chargeHurt += v; } },
      /* ---- THE CONTEST. Dexterity already pays into both of these; these are what
         a build buys on top of it. ---- */
      ["acc"] = new MetaStat { n = "ACCURACY",                            w = (p,v)=>{ p.accFlat += v; } },
      ["incAcc"] = new MetaStat { n = "increased ACCURACY",           pct = 1, w = (p,v)=>{ p.inc["acc"] += v; } },
      ["eva"] = new MetaStat { n = "EVASION",                             w = (p,v)=>{ p.evaFlat += v; } },
      ["incEva"] = new MetaStat { n = "increased EVASION",            pct = 1, w = (p,v)=>{ p.inc["eva"] += v; } },
      ["es"] = new MetaStat { n = "ENERGY SHIELD",                       w = (p,v)=>{ p.esFlat += v; } },
      ["incEs"] = new MetaStat { n = "increased ENERGY SHIELD",      pct = 1, w = (p,v)=>{ p.inc["es"] += v; } },
      ["chargeMax"] = new MetaStat { n = "maximum of EVERY charge",
                    w = (p,v)=>{ foreach (var k in CHARGE_IDS) p.stackMax[k] += v; } },
      ["chargeAll"] = new MetaStat { n = "of every charge, by every means",
                    w = (p,v)=>{ p.chargeKill += v; p.chargeCrit += v; p.chargeHurt += v; } },
      /* ---- EXPOSURE, and how hard an ailment lands ---- */
      /* while Exposure is switched off (EXPOSE_ON), a node that sold it sells
         elemental damage instead, at `scale` of its number -- the Etching's 10%
         chance is 4% increased elemental damage -- and says so */
      ["exposeChance"] = EXPOSE_ON
        ? new MetaStat { n = "chance to EXPOSE what a hit arrived as", pct = 1, w = (p,v)=>{ p.exposeChanceFlat += v; } }
        : new MetaStat { n = "increased elemental damage", pct = 1, scale = 0.4, w = (p,v)=>{ p.inc["elem"] += v*0.4; } },
      ["ailMag"] = new MetaStat { n = "increased CHILL and SHOCK magnitude", pct = 1,
                    w = (p,v)=>{ p.ailMagInc += v; } },
      /* elements */
      ["incElem"] = new MetaStat { n = "increased elemental damage",   pct = 1, w = (p,v)=>{ p.inc["elem"] += v; } },
      ["incFire"] = new MetaStat { n = "increased fire damage",        pct = 1, w = (p,v)=>{ p.inc["fire"] += v; } },
      ["incFrost"] = new MetaStat { n = "increased frost damage",       pct = 1, w = (p,v)=>{ p.inc["frost"] += v; } },
      ["incShock"] = new MetaStat { n = "increased lightning damage",   pct = 1, w = (p,v)=>{ p.inc["shock"] += v; } },
      ["incChaos"] = new MetaStat { n = "increased chaos damage",       pct = 1, w = (p,v)=>{ p.inc["chaos"] += v; } },
      ["chaosRes"] = new MetaStat { n = "chaos resistance",             pct = 1, w = (p,v)=>{ p.resFlat["chaos"] += v; } },
      /* ---- THE STRANGE ONES. Not pools: switches, and each one is the reason a
         build exists rather than a number on top of one. ---- */
      ["heartsPerInt"] = new MetaStat { n = "heart for every 10 INTELLIGENCE",
                    w = (p,v)=>{ p.heartsPerInt = p.heartsPerInt + v; } },
      ["noArmour"] = new MetaStat { n = "NO armour at all, ever",              w = (p, _v)=>{ p.noArmour = true; } },
      ["lessSpell"] = new MetaStat { n = "LESS spell damage taken",      pct = 1,
                    w = (p,v)=>{ p.lessSpell = 1 - (1 - p.lessSpell) * (1 - v); } },
      ["grant"] = new MetaStat { n = "",                                    ws = (p,v)=>{
                    (p.metaGrants ??= new List<string>()).Add(v); } },
      ["boltR"] = new MetaStat { n = "increased fireball radius",    pct = 1, w = (p,v)=>{ p.inc["boltR"] += v; }, only = ["staff"] },
      ["ember"] = new MetaStat { n = "rank of Ember Scatter",               w = (p,v)=>{ p.ember = Math.max(Js.Or(p.ember, 0), v); }, only = ["staff"] },
      /* defence */
      ["armour"] = new MetaStat { n = "flat armour",                         w = (p,v)=>{ p.armourFlat += v; } },
      ["incArmour"] = new MetaStat { n = "increased armour",             pct = 1, w = (p,v)=>{ p.incArmour += v; } },
      ["suppress"] = new MetaStat { n = "spell suppression",            pct = 1, w = (p,v)=>{ p.suppressFlat += v; } },
      ["allRes"] = new MetaStat { n = "to every resistance",          pct = 1, w = (p,v)=>{ foreach (var el in ELEMENTS) p.resFlat[el] += v; } },
      ["fireRes"] = new MetaStat { n = "fire resistance",              pct = 1, w = (p,v)=>{ p.resFlat["fire"] += v; } },
      ["frostRes"] = new MetaStat { n = "frost resistance",             pct = 1, w = (p,v)=>{ p.resFlat["frost"] += v; } },
      ["shockRes"] = new MetaStat { n = "lightning resistance",         pct = 1, w = (p,v)=>{ p.resFlat["shock"] += v; } },
      /* ATTRIBUTES. What every step of corridor hands over, and what makes walking
         across the map worth something without ever being worth as much as arriving. */
      ["str"] = new MetaStat { n = "STRENGTH",     col = "#ff5a4a", w = (p,v)=>{ p.attr["str"] += v; } },
      ["int"] = new MetaStat { n = "INTELLIGENCE", col = "#5aa8ff", w = (p,v)=>{ p.attr["int"] += v; } },
      ["dex"] = new MetaStat { n = "DEXTERITY",    col = "#6ee06e", w = (p,v)=>{ p.attr["dex"] += v; } },
      ["hearts"] = new MetaStat { n = "maximum hearts",                      w = (p,v)=>{ p.hpFlat += v; } },
      /* +0.03 MAXIMUM HEARTS was a node nobody could feel and nobody could read.
         A percentage of a pool that grows with the build is both. */
      ["incHp"] = new MetaStat { n = "increased maximum hearts", pct = 1, w = (p,v)=>{ p.inc["hp"] += v; } },
      ["incCritMult"] = new MetaStat { n = "increased critical damage", pct = 1, w = (p,v)=>{ p.incCritMult += v; } },
      ["moreHp"] = new MetaStat { n = "MORE maximum hearts",          pct = 1, w = (p,v)=>{ p.hpMore *= (1+v); } },
      ["mana"] = new MetaStat { n = "maximum MANA",                 w = (p,v)=>{ p.manaFlat += v; } },
      ["incMana"] = new MetaStat { n = "increased maximum MANA",       pct = 1, w = (p,v)=>{ p.inc["mana"] += v; } },
      ["manaRegen"] = new MetaStat { n = "MANA regenerated a second",    w = (p,v)=>{ p.manaRegenFlat += v; } },
      ["incManaRegen"] = new MetaStat { n = "increased MANA regeneration", pct = 1, w = (p,v)=>{ p.inc["manaRegen"] += v; } },
      ["regen"] = new MetaStat { n = "hearts a second regenerated",         w = (p,v)=>{ p.regenFlat += v; } },
      ["incRegen"] = new MetaStat { n = "increased regeneration",       pct = 1, w = (p,v)=>{ p.inc["regen"] += v; } },
      ["leech"] = new MetaStat { n = "lifesteal share",              pct = 1, w = (p,v)=>{ p.leechFlat += v; },
                    only = ["sword","axe","mace"] },
      ["leechRate"] = new MetaStat { n = "lifesteal chance",             pct = 1, w = (p,v)=>{ p.leechChanceFlat += v; },
                    only = ["sword","axe","mace"] },
      ["heartDrop"] = new MetaStat { n = "increased heart drops",        pct = 1, w = (p,v)=>{ p.inc["heart"] += v; } },
      /* movement, loot, and the slam */
      ["speed"] = new MetaStat { n = "move speed",                   pct = 1, w = (p,v)=>{ p.inc["speed"] += v; } },
      ["magnet"] = new MetaStat { n = "pickup range",                 pct = 1, w = (p,v)=>{ p.inc["magnet"] += v; } },
      ["roll"] = new MetaStat { n = "faster roll cooldown",         pct = 1, w = (p,v)=>{ p.inc["roll"] += v; } },
      ["studs"] = new MetaStat { n = "studs from every source",      pct = 1, w = (p,v)=>{ p.inc["studs"] += v; } },
      ["xp"] = new MetaStat { n = "increased experience",         pct = 1, w = (p,v)=>{ p.xpInc += v; } },
      ["slamDmg"] = new MetaStat { n = "flat damage to every tooth",          w = (p,v)=>{ p.slam.flat += v; }, only = ["mace"] },
      ["slamArc"] = new MetaStat { n = "wider fault",                  deg = 1, w = (p,v)=>{ p.slam.arcFlat += v*Math.PI/180.0; }, only = ["mace"] },
      ["slamRange"] = new MetaStat { n = "increased slam range",         pct = 1, w = (p,v)=>{ p.slam.rangeInc += v; }, only = ["mace"] },
      ["spellCap"] = new MetaStat { n = "to every spell stack cap",            w = (p,v)=>{ p.capUp["orbit"]+=v;
                                                                        p.capUp["chain"]+=v; p.capUp["bomb"]+=v; } },
      /* ---- ONE ABILITY AT A TIME. A tree that only ever says "+5% damage" has no
         opinions about anything. A node that says "+1 damage with the Guardian
         Brick" makes you look at what you are actually holding, and makes the tree
         worth re-reading every time the deck hands you something new. Every one of
         these clamps exactly where the brick that grants it clamps, so the ceilings
         still bind. ---- */
      ["orbitDmg"] = new MetaStat { n = "damage with Guardian Brick",          w = (p,v)=>{ p.orbit.dmg += v; } },
      ["orbitR"] = new MetaStat { n = "to the Guardian Brick orbit",  px = 1,  w = (p,v)=>{ p.orbit.r = Math.min(ORBIT_MAX_R, p.orbit.r+v); } },
      ["orbitSpd"] = new MetaStat { n = "increased Guardian Brick speed", pct = 1,w = (p,v)=>{ p.inc["orbitSpd"] += v; } },
      ["chainDmg"] = new MetaStat { n = "damage with Storm Brick",             w = (p,v)=>{ p.chain.dmg += v; } },
      ["chainFork"] = new MetaStat { n = "Storm Brick fork",                    w = (p,v)=>{ p.chain.fork = Math.min(CHAIN_FORK_MAX, p.chain.fork+v); } },
      ["chainRate"] = new MetaStat { n = "increased Storm Brick rate",   pct = 1, w = (p,v)=>{ p.inc["chainRate"] += v; } },
      ["stormDmg"] = new MetaStat { n = "damage per Bladestorm blade",         w = (p,v)=>{ p.storm.dmg += v; } },
      ["stormSize"] = new MetaStat { n = "to Bladestorm blade size",            w = (p,v)=>{ p.storm.size = Math.min(STORM_MAX_SIZE, p.storm.size+v); } },
      ["stormRate"] = new MetaStat { n = "increased Bladestorm rate",    pct = 1, w = (p,v)=>{ p.inc["stormRate"] += v; } },
      ["vortexDmg"] = new MetaStat { n = "damage with Blade Vortex",            w = (p,v)=>{ p.vortex.dmg += v; } },
      ["vortexR"] = new MetaStat { n = "to the Blade Vortex radius",   px = 1,  w = (p,v)=>{ p.vortex.r = Math.min(VORTEX_MAX_R, p.vortex.r+v); } },
      ["vortexRate"] = new MetaStat { n = "increased Blade Vortex rate",  pct = 1, w = (p,v)=>{ p.inc["vortexRate"] += v; } },
      ["freezeR"] = new MetaStat { n = "to the Block Freeze radius",   px = 1,  w = (p,v)=>{ p.freeze.r = Math.min(FREEZE_MAX_R, p.freeze.r+v); } },
      ["freezeRate"] = new MetaStat { n = "increased Block Freeze rate",  pct = 1, w = (p,v)=>{ p.inc["freezeRate"] += v; } },
      ["bombDmg"] = new MetaStat { n = "damage per bomb",                     w = (p,v)=>{ p.bomb.dmg += v; } },
      ["bombCluster"] = new MetaStat { n = "bomb cluster",                        w = (p,v)=>{ p.bomb.cluster = Math.min(CLUSTER_MAX, p.bomb.cluster+v); } },
      ["bombRate"] = new MetaStat { n = "increased Bomb Volley rate",   pct = 1, w = (p,v)=>{ p.inc["bombRate"] += v; } },
      ["bombR"] = new MetaStat { n = "increased bomb radius",        pct = 1, w = (p,v)=>{ p.inc["bombR"] += v; } },
      ["baneDmg"] = new MetaStat { n = "damage with Brickbane",               w = (p,v)=>{ p.bane.dmg += v; } },
      ["baneR"] = new MetaStat { n = "increased Brickbane radius",   pct = 1, w = (p,v)=>{ p.inc["baneR"] += v; } },
      ["baneRate"] = new MetaStat { n = "increased Brickbane rate",     pct = 1, w = (p,v)=>{ p.inc["baneRate"] += v; } },
      /* the flags, which are not pools — see metaClampFlags */
      ["tollDelay"] = new MetaStat { n = "waves before each elemental toll",    w = (p,v)=>{ p.tollDelay += v; } },
      ["reroll"] = new MetaStat { n = "reroll on every reward screen",       w = (p,v)=>{ p.rerollBonus += v; } },
      ["chestCards"] = new MetaStat { n = "cards from every chest",              w = (p,v)=>{ p.chestCards = Math.max(Js.Or(p.chestCards, 1), v); } },
      ["bankMul"] = new MetaStat { n = "× the studs you bank",           w = (p,v)=>{ p.bankMul *= v; } },
      ["noRegen"] = new MetaStat { n = "NO regeneration at all",              w = (p, _v)=>{ p.noRegen = true; } },
      /* ---- THE THREE SHIELD KEYSTONES, straight out of PoE. Each one is a trade
         only an energy-shield build can win. ---- */
      /* THE LEGION's own pools -- worth nothing to anything but a scepter */
      ["minionDmg"] = new MetaStat { n = "increased minion damage", pct = 1, w = (p,v)=>{ p.minions.inc += v; }, only = ["scepter"] },
      ["minionLife"] = new MetaStat { n = "increased minion life",   pct = 1, w = (p,v)=>{ p.minions.life += v; }, only = ["scepter"] },
      ["minionSpd"] = new MetaStat { n = "increased minion attack, cast and movement speed", pct = 1, w = (p,v)=>{ p.minions.spd += v; }, only = ["scepter"] },
      ["minionZombies"] = new MetaStat { n = "your summon aura raises ZOMBIES from corpses, up to twelve",
                    w = (p, _v)=>{ p.minions.zombies = true; }, only = ["scepter"] },
      ["minionProj"] = new MetaStat { n = "projectile for your skeleton archers and mages", w = (p,v)=>{ p.minions.proj += v; }, only = ["scepter"] },
      ["golemCap"] = new MetaStat { n = "maximum golems", w = (p,v)=>{ p.minions.golemCap = p.minions.golemCap + v; }, only = ["scepter"] },
      ["minionMax"] = new MetaStat { n = "skeleton in your legion", w = (p,v)=>{ p.minions.max = Math.min(MINION_MAX_CAP, p.minions.max + v); }, only = ["scepter"] },
      ["minionLess"] = new MetaStat { n = "LESS minion damage", pct = 1, w = (p,v)=>{ p.minions.more *= (1 - v); }, only = ["scepter"] },
      ["minionMirror"] = new MetaStat { n = "your minions gain three quarters of your increased damage, and your conversions",
                    w = (p, _v)=>{ p.minions.mirror = true; }, only = ["scepter"] },
      ["minionInstab"] = new MetaStat { n = "a skeleton that falls BURSTS: a share of its life as fire, to everything close",
                    w = (p, _v)=>{ p.minions.instab = true; }, only = ["scepter"] },
      ["chaosInoc"] = new MetaStat { n = "maximum hearts become 1 — every heart you gave up is 2 ENERGY SHIELD — and chaos cannot touch you",
                    w = (p, _v)=>{ p.chaosInoc = true; } },
      ["ghostReaver"] = new MetaStat { n = "your lifesteal fills your ENERGY SHIELD, and never your hearts",
                    w = (p, _v)=>{ p.ghostReaver = true; } },
      ["zealot"] = new MetaStat { n = "your regeneration fills your ENERGY SHIELD, and never your hearts",
                    w = (p, _v)=>{ p.zealot = true; } },
      /* ---- THE BOW ---- */
      ["arrowFlat"] = new MetaStat { n = "flat physical damage to arrows",       w = (p,v)=>{ p.bow.flat += v; }, only = ["bow"] },
      ["arrowPierce"] = new MetaStat { n = "arrow pierce",                         w = (p,v)=>{ p.bow.pierce = Math.min(ARROW_MAX_PIERCE, p.bow.pierce + v); },
                    only = ["bow"] },
      ["arrowN"] = new MetaStat { n = "arrow on every shot",                  w = (p,v)=>{ p.bow.n = Math.min(ARROW_MAX_N, p.bow.n + v); }, only = ["bow"] },
      ["arrowFork"] = new MetaStat { n = "body Lightning Arrow forks to",        w = (p,v)=>{ p.bow.forkPlus = p.bow.forkPlus + v; }, only = ["bow"] },
      ["arrowBlast"] = new MetaStat { n = "increased Fire Arrow burst radius", pct = 1, w = (p,v)=>{ p.bow.blastInc = p.bow.blastInc + v; }, only = ["bow"] },
      /* ---- THE WEAPONS' OWN SKILLS ---- */
      ["whirlShare"] = new MetaStat { n = "of a swing on every Blademaster whirl hit", pct = 1, w = (p,v)=>{ p.whirlPlus = p.whirlPlus + v; }, only = ["sword","axe"] },
      ["reaveShare"] = new MetaStat { n = "of the critical carried by every thrown axe", pct = 1, w = (p,v)=>{ p.reavePlus = p.reavePlus + v; }, only = ["axe"] },
      ["reaveBounce"] = new MetaStat { n = "body a thrown axe reaps through",      w = (p,v)=>{ p.reaveLegs = p.reaveLegs + v; }, only = ["axe"] },
      /* ---- PENETRATION (see penOf) ---- */
      ["penFire"] = new MetaStat { n = "fire resistance PENETRATED by your hits",      pct = 1, w = (p,v)=>{ p.pen["fire"]  += v; } },
      ["penFrost"] = new MetaStat { n = "cold resistance PENETRATED by your hits",      pct = 1, w = (p,v)=>{ p.pen["frost"] += v; } },
      ["penShock"] = new MetaStat { n = "lightning resistance PENETRATED by your hits", pct = 1, w = (p,v)=>{ p.pen["shock"] += v; } },
      ["penChaos"] = new MetaStat { n = "chaos resistance PENETRATED by your hits",     pct = 1, w = (p,v)=>{ p.pen["chaos"] += v; } },
      ["penElem"] = new MetaStat { n = "of every elemental resistance PENETRATED by your hits", pct = 1, w = (p,v)=>{ p.pen["elem"] += v; } },
      ["ricochet"] = new MetaStat { n = "chance for an arrow that would stop to RICOCHET to another body, up to twice", pct = 1,
                    w = (p,v)=>{ p.ricochet = p.ricochet + v; }, only = ["bow"] },
      ["arrowLess"] = new MetaStat { n = "LESS arrow damage", pct = 1, w = (p,v)=>{ p.bow.more *= (1 - v); }, only = ["bow"] },
      ["noReroll"] = new MetaStat { n = "NO rerolls, ever",                    w = (p, _v)=>{ p.noReroll = true; } },
      /* ---- AND THE NEW SWITCHES. Every one of these changes a RULE rather than a
         number, which is the only thing worth walking to the edge of the map for.
         They live in the tree and nowhere else: a reward screen is not the place to
         hand somebody a sentence they cannot give back. ---- */
      ["prolif"] = new MetaStat { n = "reach — what dies spreads its BURN, POISON and WOUNDS that far", px = 1,
                    w = (p,v)=>{ p.prolifR = Math.max(Js.Or(p.prolifR, 0), v); } },
      ["critAilAll"] = new MetaStat { n = "every CRITICAL leaves every ailment its damage can",
                    w = (p, _v)=>{ p.critAilAll = true; } },
      ["noCrit"] = new MetaStat { n = "you can NEVER deal a critical strike",
                    w = (p, _v)=>{ p.noCrit = true; } },
      ["ailFull"] = new MetaStat { n = "CHILL and SHOCK always land at full magnitude",
                    w = (p, _v)=>{ p.ailFull = true; } },
    };

    static readonly System.Text.RegularExpressions.Regex LESS_RE = new(@"\bLESS\b"), TAKEN_RE = new(@"\btaken\b");
    public static string metaModText(Mod m)
    {
        var st = META_STATS.GetValueOrDefault(m.k ?? "");
        if (st == null) return "";
        /* a stat standing in for another prints its own number (see exposeChance) */
        double v = (m.num && Js.T(st.scale)) ? m.v * st.scale : m.v;
        if (m.k == "grant") return "START THE RUN WITH " + (SPELL_NAME.GetValueOrDefault(m.s ?? "") ?? m.s).ToUpperInvariant();
        if (!m.num) return st.n;
        /* A STAT NAMED FOR ITS COST prints the way PoE prints one */
        if (LESS_RE.IsMatch(st.n))
        {
            string n2 = Js.T(st.pct) ? Js.Num(Math.Round(Math.Abs(v) * 1000) / 10) + "%" : Js.Num(Math.Round(Math.Abs(v) * 100) / 100);
            return n2 + " " + (v < 0 ? LESS_RE.Replace(st.n, "MORE", 1) : st.n);
        }
        string sign = v < 0 ? "−" : "+";
        double a = Math.Abs(v);
        string num = Js.T(st.pct) ? Js.Num(Math.Round(a * 1000) / 10) + "%"
                   : Js.T(st.px) ? Js.Num(Math.Round(a)) + " px"
                   : Js.T(st.deg) ? Js.Num(Math.Round(a)) + "°"
                   : st.col != null ? Js.Num(Math.Round(a))
                   : Js.Num(Math.Round(a * 100) / 100);
        return string.Join("", signWords(sign + num + " " + st.n));
    }
    /* ---- EVERY CONVERSION THE PIPELINE ALLOWS, GENERATED, and one pair per weapon ---- */
    /* run once from boot(), in the JS's order: it reads tables from other files */
    public static bool MetaGenStats()
    {
        foreach (var a in CONV_ORDER) foreach (var b in CONV_ORDER)
            {
                if (!convLegal(a, b)) continue;
                string aa = a, bb = b;
                META_STATS["cv_" + a + "_" + b] = new MetaStat { n = "of " + CONV_NAME[a].ToLowerInvariant() + " damage converted to " + CONV_NAME[b], pct = 1,
                    w = (p, v) => { convAdd(p.conv, aa, bb, v); } };
                META_STATS["gn_" + a + "_" + b] = new MetaStat { n = "of " + CONV_NAME[a].ToLowerInvariant() + " damage gained as EXTRA " + CONV_NAME[b], pct = 1,
                    w = (p, v) => { convGain(p.conv, aa, bb, v); } };
            }
        foreach (var wk in WEAPON_KEYS)
        {
            var W0 = WEAPONS.GetValueOrDefault(wk); bool cast = W0 != null && W0.cast; string nm = W0?.name ?? wk;
            /* NO FLAT WEAPON DAMAGE IN THE TREE ANY MORE: the writer is kept because a CARD can still use it */
            META_STATS["dmg_" + wk] = new MetaStat { n = "flat " + (cast ? "elemental" : "physical") + " damage — " + nm + " only", only = new[] { wk },
                w = (p, v) => { if (cast) p.flatElem += v; else p.flatPhys += v; } };
            META_STATS["inc_" + wk] = new MetaStat { n = "increased " + (cast ? "spell" : "physical") + " damage — " + nm + " only", pct = 1, only = new[] { wk },
                w = (p, v) => { if (cast) p.incSpell += v; else p.incMelee += v; } };
        }
        return true;
    }
    public static bool metaModGood(Mod m)
    {
        if (m.k == "noRegen" || m.k == "noReroll" || m.k == "noArmour") return false;
        if (m.k == "grant") return true;
        /* a stat whose NAME is a cost is bad when it is positive */
        var st = META_STATS.GetValueOrDefault(m.k ?? "");
        if (st != null && LESS_RE.IsMatch(st.n) && !TAKEN_RE.IsMatch(st.n))
            return !(m.num && m.v > 0);
        return !(m.num && m.v < 0);
    }
}
