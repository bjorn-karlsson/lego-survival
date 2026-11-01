namespace Brickblade;

/* =============================== PLAYER (JS 4392–4553) =============================== */
public static partial class G
{
    static NumMap NM(params string[] keys) { var m = new NumMap(); foreach (var k in keys) m[k] = 0; return m; }
    public static Ent newPlayer()
    {
        return new Ent
        {
            x = CX, y = CY - 40, r = 13, hp = HP_BASE, maxHp = HP_BASE, speed = SPEED_BASE,
            flatPhys = 0, incMelee = 0, incSpell = 0, incAll = 0, moreAll = 1,
            critBase = CRIT_BASE, incCrit = 0, critMultPct = CRIT_MULT_BASE,
            armour = 0, incArmour = 0, suppress = 0,
            hpFlat = 0, hpMore = 1, baneDotFlat = 0,
            armourFlat = 0, regenFlat = 0, critMultFlat = 0, suppressFlat = 0, reachFlat = 0,
            flatElem = 0, ember = 0,
            resFlat = NM("fire", "frost", "shock", "chaos"),
            leechFlat = 0, leechChanceFlat = 0,
            slam = new SlamPools(),
            slamAct = null, slamPose = 0, slamAim = 0,
            tollDelay = 0, noRegen = false, rerollBonus = 0, chestCards = 1, noReroll = false,
            heartsPerInt = 0, noArmour = false, lessSpell = 0, metaGrants = new List<string>(),
            bankMul = 1, studsTotal = 0,
            xpInc = 0, xpMore = 1,
            conv = blankConv(),
            inc = NM("hp", "speed", "magnet", "studs", "atk", "cast", "roll", "regen", "heart",
                     "acc", "eva", "es", "mana", "manaRegen",
                     "elem", "fire", "frost", "shock", "boltR",
                     "chaos",
                     "orbitSpd", "chainRate", "stormRate",
                     "vortexRate", "freezeRate", "bombRate", "bombR", "baneRate", "baneR",
                     "dot"),
            atkRate = ATK_BASE, atkCd = 0,
            combo = 0, comboTimer = 0, swing = null, reach = 0, xpMul = 1, blademaster = 0, whirl = false, whirlAng = 0,
            dash = new Dash { t = 0, cd = 0, cdMax = DASH_BASE, dir = new Pt(1, 0) },
            invuln = 0, flash = 0, xp = 0, xpNext = 10, level = 1, magnetR = MAGNET_BASE, magnetT = 0,
            kx = 0, ky = 0,
            fortuneT = 0, fortuneMul = 1, slowT = 0,
            rageT = 0, rageMul = 1,
            wardT = 0, wardN = 0,
            burn = new DotList(),
            ailCd = NM("fire", "frost", "shock"),
            stunT = 0,
            brand = NM("fire", "frost", "shock"),
            brandT = NM("fire", "frost", "shock"),
            chill = new Stacks(),
            psn = new DotList(),
            shock = new Stacks(),
            res = NM("fire", "frost", "shock"),
            curse = NM("resist", "haste", "armor", "regen", "t"),
            curseN = NM("resist", "haste", "armor", "regen"),
            regenRate = 0, regenAcc = 0, regenStall = 0, regenBoostT = 0, heartStacks = 0,
            crit = 0.1, studMul = 1, heartChance = 0,
            orbit = new Spell { n = 0, dmg = 2, spd = ORBIT_SPD_BASE, r = 78, size = 9, aegis = 0, twin = 0 },
            bow = new Bow { n = 1, pierce = 0, flat = 0, more = 1, kind = null, forkPlus = 0, blastInc = 0 },
            pen = NM("fire", "frost", "shock", "chaos", "elem"),
            whirlPlus = 0, reavePlus = 0, reaveLegs = 0,
            ricochet = 0,
            minions = new MinionPools
            {
                max = MINION_MAX_BASE, perCast = 1, flat = 0, inc = 0, life = 0, spd = 0, more = 1,
                mages = false, mirror = false, instab = false, golemCap = 0, golemCd = 0, overseer = false, overseerCd = 0,
                proj = 0, projMore = 1, volleys = 0, gmp = false, zombies = false
            },
            splinters = NM("xoph", "tul", "esh", "uul", "chayula"), rings = new NumMap(), breachDepth = 0,
            storm = new Spell { n = 0, dmg = 2, rate = STORM_RATE_BASE, cd = 2, spd = 255, life = 1.5, count = 8, size = 1, boomer = 0,
                queued = 0, qT = 0, qIdx = 0, qOffset = 0 },
            vortex = new Spell { n = 0, dmg = 3, rate = VORTEX_RATE_BASE, cd = 3, r = 80, dur = 1.4, hitMul = 1, t = 0, totem = 0, totemCd = 0 },
            freeze = new Spell { n = 0, r = 128, dur = 4.5, rate = FREEZE_RATE_BASE, cd = 1.2, stun = 0, flat = 0 },
            bane = new Spell { n = 0, dmg = 2, rate = BANE_RATE_BASE, cd = 1.5, r = BANE_R_BASE, arc = BANE_ARC_BASE,
                dot = BANE_DOT_BASE, dotT = BANE_DOT_T, stacks = 1, burst = 0 },
            bleedChanceFlat = 0, bleedShareFlat = 0, bleedChance = 0, bleedShare = BLEED_SHARE_BASE,
            ailChanceFlat = 0, ailChance = 0, ailTypeFlat = NM("fire", "frost", "shock", "chaos"), ailType = NM("fire", "frost", "shock", "chaos"),
            reave = 0, reaveCd = 0,
            stacks = new JsMap<string, StackSt>(), stackMax = NM("momentum", "rage", "frenzy", "power", "endurance"),
            rageOnHurt = 0, rageOnHit = 0,
            chargeKill = 0, chargeCrit = 0, chargeHurt = 0,
            exposeChanceFlat = 0, exposeChance = 0, prolifR = 0, ailMagInc = 0, dotMore = 1,
            accFlat = 0, evaFlat = 0,
            esFlat = 0, es = 0, esMax = 0, esT = 0, esFlash = 0,
            manaFlat = 0, manaRegenFlat = 0, mana = 0, maxMana = 0, manaRegen = 0,
            critAilAll = false, noCrit = false, ailFull = false,
            attr = NM("str", "int", "dex"),
            incCritMult = 0,
            chain = new Spell { n = 0, zappers = 0, dmg = 2, rate = CHAIN_RATE_BASE, r = 430, cds = new List<double> { 1.5 }, gap = 0, fork = 0 },
            bomb = new Spell { n = 0, dmg = 2, r = BOMB_R_BASE, rate = BOMB_RATE_BASE, cd = 3, cluster = 0 },
            capUp = NM("orbit", "chain", "bomb"),
            studs = 0, kills = 0, face = new Pt(0, 1), walk = 0, moving = false, blink = 0, blinkT = rnd(2, 4),
            dead = false, deadT = 0, stepT = 0, streak = 0, streakT = 0
        };
    }
    public static readonly FigCols HERO_COLORS =
        new FigCols { head = PAL.hyellow, torso = PAL.red, legs = "#1a4f8a", hands = PAL.hyellow, hair = PAL.brown };
}
