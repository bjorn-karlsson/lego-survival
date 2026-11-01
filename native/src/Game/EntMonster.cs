namespace Brickblade;

/* THE MONSTER'S FIELDS (spawnEnemy, JS 6234, and everything that writes to a
   monster afterwards). The hero never sets them, so on the hero they keep their
   defaults -- which is what reading them on the hero gives in the JS. Fields the
   JS tests with `=== undefined` default to NaN here. */
public sealed partial class Ent
{
    public string state, mode, style, bkey, sig, golem, mini, touchElem, mimicRar, orbAura;
    public bool boss, super, elite, magic, docile, shade, zorb, escaped, dormant, bannered;
    public FigCols col;
    public double scale = 1, btier, mlvl, atkS, touchCd, chaosShare, faceA, buildT, hitCd, modeT;
    public double strafe, strafeT, phase, aimT, wx, wy, wanderT, orbCd, vortCd, stuckT, avoidT, avoidA;
    public double _lx, _ly, slideT, slideSign, nextAtk, cycle, stompT, totCd;
    public double evasion, accuracy, block, fire;
    public DropRange studsDrop;
    public BossRing ring;
    /* the path it is walking (PATHFINDING) */
    public List<Pt> path; public int pathI; public double pathT; public Pt pathGoal;
    /* buffs an overseer or a shrine laid on it, and its ward */
    public NumMap buffs; public double buffStacks, wardLit;
    /* what is eating it */
    public DotList eburn, bleed; public EPoison poison; public Frozen frozen; public Mark burnt, shocked;
    public double chillN, chillT, shockN, shockT, emberCd, spdNow;
    public NumMap expose;
    /* where it belongs: a breach (its lord's key), an abyss, a strongbox, a ritual, a legion, an RPG pack */
    public string breach; public Breach breachB;
    public bool abyss; public Abyss abyssA;
    public Strongbox sbox;
    public Ritual ritual, ritualGuard;
    public Legion legion;
    public double rpgPack;
    /* boss traits */
    public List<MonMod> traits; public bool venom, trRest, trLegion, thorns;
    /* rare modifiers */
    public List<string> rmods; public double rmRegen, rmBerserk, rmNear, rmNearT, rmSoul = double.NaN;
    public bool rmUnstop, rmVolatile, rmVenom;
    /* a kindred pack */
    public int kindred; public double kinN;
    /* the specials */
    public double gobT, gobDrop = double.NaN, gobPortal = double.NaN, orbAuraT = double.NaN;
    public Ent banner, bearer; public double plantCd;
    public double strikeT, z;
    /* the hero, as a monster's update sees it: a minion standing in for the hero */
    public bool isProxy; public Ent minion;
    /* the hero's own: where it was a frame ago (leadPlayer), a level's heal, the gear it wears */
    public double leadX = double.NaN, leadY = double.NaN, leadDt;
    public bool lvlHeal, ghostReaver;
    public List<(string stat, double v)> gearMods;
}
