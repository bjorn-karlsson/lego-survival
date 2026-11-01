namespace Brickblade;

/* THE HERO, A MONSTER, A MINION. In the JS these are three kinds of object
   literal, but they are handed to the same functions (and `player` itself is a
   minion for the length of one monster's update), so here they are one class:
   every field any of them has. A field one kind never sets keeps its default,
   which is what reading it in the JS gives (undefined -> 0/false/null).
   Fields are added per section of the port, in partial class files. */
public sealed partial class Ent : Obj
{
    /* ---------------- the hero (newPlayer, JS 4393) ---------------- */
    public double hp, maxHp, speed;
    public double flatPhys, incMelee, incSpell, incAll, moreAll = 1, critBase, incCrit, critMultPct;
    public double armour, incArmour, suppress;
    public double hpFlat, hpMore = 1, baneDotFlat;
    public double armourFlat, regenFlat, critMultFlat, suppressFlat, reachFlat;
    public double flatElem, ember;
    public NumMap resFlat;
    public double leechFlat, leechChanceFlat, leech, leechChance;
    public SlamPools slam;
    public SlamAct slamAct; public double slamPose, slamAim, slamArc, slamRange, slamSpeed;
    public double tollDelay; public bool noRegen; public double rerollBonus, chestCards = 1; public bool noReroll;
    public double heartsPerInt; public bool noArmour; public double lessSpell; public List<string> metaGrants;
    public double bankMul = 1, studsTotal;
    public double xpInc, xpMore = 1;
    public Conv conv;
    public NumMap inc;
    public double atkRate, atkCd;
    public double combo, comboTimer; public Swing swing; public double reach, xpMul = 1, blademaster; public bool whirl; public double whirlAng;
    public Dash dash;
    public double invuln, flash, xp, xpNext, level, magnetR, magnetT, magnetMax;
    public double kx, ky;
    public double fortuneT, fortuneMul = 1, fortuneMax, slowT;
    public double rageT, rageMul = 1, rageMax;
    public double wardT, wardN, wardMax;
    public DotList burn;
    public NumMap ailCd;
    public double stunT;
    public NumMap brand, brandT;
    public Stacks chill, shock;
    public DotList psn, pbleed;
    public NumMap res;
    public NumMap curse, curseN;
    public double regenRate, regenAcc, regenStall, regenBoostT, heartStacks;
    public double crit, studMul = 1, heartChance;
    public Spell orbit, storm, vortex, freeze, bane, chain, bomb;
    public Bow bow;
    public NumMap pen;
    public double whirlPlus, reavePlus, reaveLegs;
    public double ricochet;
    public MinionPools minions;
    public NumMap splinters, rings; public double breachDepth;
    public double bleedChanceFlat, bleedShareFlat, bleedChance, bleedShare;
    public double ailChanceFlat, ailChance; public NumMap ailTypeFlat, ailType;
    public double reave, reaveCd;
    public JsMap<string, StackSt> stacks; public NumMap stackMax; public HashSet<string> stackOn;
    public double rageOnHurt, rageOnHit;
    public double chargeKill, chargeCrit, chargeHurt;
    public double exposeChanceFlat, exposeChance, prolifR, ailMagInc, dotMore = 1;
    public double accFlat, evaFlat;
    public double esFlat, es, esMax, esT, esFlash, ciEs; public bool chaosInoc;
    public double manaFlat, manaRegenFlat, mana, maxMana, manaRegen;
    public bool critAilAll, noCrit, ailFull;
    public NumMap attr;
    public double incCritMult;
    public NumMap capUp;
    public double studs, kills;          /* the hero's stud count -- a monster's drop range is `studsDrop` */
    public Pt face;
    public double walk; public bool moving; public double blink, blinkT;
    public double deadT, stepT, streak, streakT;
    public double gHearts;
    /* a spell by its key: p[k] in the JS (k in SPELL_KEYS) */
    public Spell Spell(string k) => k switch
    {
        "orbit" => orbit, "storm" => storm, "vortex" => vortex, "freeze" => freeze,
        "bane" => bane, "chain" => chain, "bomb" => bomb, _ => null
    };
}

public sealed class SlamPools { public double flat, arcFlat, rangeInc, spdInc, elem, cycle; }
public sealed class Dash { public double t, cd, cdMax; public Pt dir = new Pt(1, 0); public double dx, dy; }
public sealed class Stacks { public double stacks, t; }
public sealed class Bow { public double n = 1, pierce, flat, more = 1; public string kind; public double forkPlus, blastInc; }
public sealed class OrbPos { public double x, y, ang, r; public int i; public bool outer; public double broken; }
/* every spell's numbers in one shape: the JS reads p[k].n / .rate whatever k is */
public sealed class Spell
{
    public double n, dmg, rate, cd;
    /* orbit */
    public double spd, size, aegis, twin, blocked, rNow; public List<OrbPos> pos = new(); public double[] brk = new double[64], brk2 = new double[64];
    /* storm */
    public double life, count, boomer, queued, qT, qIdx, qOffset;
    /* vortex */
    public double r, dur, hitMul, t, totem, totemCd;
    /* freeze */
    public double stun, flat;
    /* bane */
    public double arc, dot, dotT, stacks, burst;
    /* chain */
    public double zappers, gap, fork; public List<double> cds = new();
    /* bomb */
    public double cluster;
}
public sealed class MinionPools
{
    public double max, perCast = 1, flat, inc, life, spd, more = 1;
    public bool mages, mirror, instab; public List<string> golemTypes = new(); public double golemCap, golemCd; public bool overseer; public double overseerCd;
    public double proj, projMore = 1, volleys; public bool gmp, zombies, horde;
}
