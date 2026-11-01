namespace Brickblade;

/* =============================== ENEMIES (JS 7864–8646) ===============================
   Experience and levels; the damage formula and every pool it reads; the three
   attributes and mana; armour, resistance and the curses; the hero's ailments;
   the hit that lands on the hero; the second chance and the death. */
public sealed class RespawnState { public double free = 1, paid; public bool banked; }
public sealed class RespawnOffer { public bool free, can; public double cost; }
public sealed class AttrDef { public string id, name, @short, col, what; }

public static partial class G
{
    /* Levelling used to outrun the roster: monsters now pay half as much */
    public const double MOB_XP_SCALE = 0.5;
    public const double MOB_XP_WAVE_DIV = 10;
    public const double STUD_XP_SCALE = 0.45;
    public const double STUD_XP_WAVE_DIV = 10;
    /* the step eases from 1.30 down towards 1.12 */
    public static double xpStep(double lvl) => 1.12 + 0.18 * Math.Pow(0.94, Math.Max(0, lvl - 1));
    /* IN THE ARCADE A LEVEL IS A HEART */
    public const double ARCADE_LEVEL_HEART = 1;
    public static bool arcadeMode() => GAME_MODE != "rpg";
    public static double levelHearts(Ent p) => arcadeMode() ? Math.Max(0, Js.I32(p.level) - 1) * ARCADE_LEVEL_HEART : 0;
    public static void gainXp(double n, bool fromMob = false)
    {
        var p = player;
        double waveBonus = 1 + wave.n / (fromMob ? MOB_XP_WAVE_DIV : STUD_XP_WAVE_DIV);
        p.xp += n * waveBonus * p.xpMul;
        double lv0 = p.level;
        while (p.xp >= p.xpNext)
        {
            p.xp -= p.xpNext; p.level++; p.xpNext = Math.Round(p.xpNext * xpStep(p.level) + 5);
            p.incAll += 0.05;      /* additive with every other increase, PoE style */
            if (arcadeMode()) { p.hpFlat += ARCADE_LEVEL_HEART; p.lvlHeal = true; }
            else if (p.hp < p.maxHp) p.hp++;
            addRing(p.x, p.y, 10, 300, 110, "#ffb900", 6, 0.5);
            addDmg(p.x, p.y - 50, "LEVEL UP!", "#ffb900", 22);
            logEvent("LEVEL " + Js.Num(p.level), "#ffb900");
            SFX.levelup();
            queuePick("level", null);
            metaLiveBank();
        }
        /* a level is worth more MANA, so the pools are rebuilt -- and a level fills the mana pool */
        if (p.level != lv0 && rpgOn()) rpgCharSave();
        if (p.level != lv0)
        {
            syncStats(); p.mana = p.maxMana;
            if (p.lvlHeal) { p.lvlHeal = false; p.hp = p.maxHp; if (p.esMax > 0) p.es = p.esMax; }
        }
    }
    /* Damage = (Base + Flat) x (1 + increased) x prod(1 + more) */
    public static double rageMul() => player.rageT > 0 ? player.rageMul : 1;
    /* Every increase that applies to ONE hit, summed into one pool, then stopped at the ceiling */
    public static double incTotal(string kind, string el = null)
    {
        var p = player;
        if (p == null) return 0;
        return Math.Min(INC_DMG_CAP,
            p.incAll
            + (kind == "melee" ? incMeleeAll() : kind == "spell" ? incSpellAll() : 0)
            + (el != null ? incElemAll(el) : 0));
    }
    /* CHARGES MULTIPLY ON TOP OF THE CEILING, like the rage brick */
    public static double chargeMore() => 1 + stackMod("moreAll");
    public static double moreTotal() => player != null ? Math.Min(MORE_DMG_CAP, player.moreAll) * rageMul() * chargeMore() : 1;
    /* what the SHEET should print for the more multiplier */
    public static double moreShown() => player != null ? Math.Min(MORE_DMG_CAP, player.moreAll) : 1;
    public static double dotInc() => player != null ? Math.Min(INC_DMG_CAP, player.inc["dot"]) : 0;
    public static string atCap(double v, double cap) => v >= cap - 1e-9 ? " — at the cap" : "";
    /* Is there anything left to sell? */
    public static bool incRoom(string kind, string el = null) => incTotal(kind, el) < INC_DMG_CAP - 1e-9;
    public static string incCapTag(string kind, string el = null) =>
        "(now " + Js.Num(Math.Round(incTotal(kind, el) * 100)) + "%, cap " + Js.Num(Math.Round(INC_DMG_CAP * 100)) + "%)";
    public static double calcDamage(double b, string kind, IEnumerable<double> more = null)
    {
        double d = b * (1 + incTotal(kind)) * moreTotal();
        /* RAGE IS ATTACK DAMAGE: only on what you SWING */
        if (kind == "melee") d *= rageMore();
        if (more != null) foreach (var m in more) d *= m;
        return Math.Max(0, d);
    }
    /* The weapon's own base plus every flat PHYSICAL brick */
    public static double physBase() => weapon.dmg + player.flatPhys;
    /* ---- WHAT THE HERO BRINGS TO THE CONTEST ---- */
    public const double ACC_BASE = 320;
    public static double accuracyValue()
    {
        var p = player;
        if (p == null) return ACC_BASE;
        double flat = ACC_BASE + p.accFlat + dexOf() * ATTR_DEX_ACC;
        return Math.Max(0, flat * (1 + p.inc["acc"]) * (weapon != null ? Js.Or(weapon.acc, 1) : 1));
    }
    public static double evasionValue()
    {
        var p = player;
        if (p == null) return 0;
        return Math.Max(0, (p.evaFlat + (weapon != null ? weapon.evaBase : 0))
                           * (1 + p.inc["eva"] + dexOf() * ATTR_DEX_EVA));
    }
    /* what a hero actually lands against a body, and what a body lands on them */
    public static double heroHitChance(Ent e) => hitChance(accuracyValue(), e != null ? e.evasion : 0);
    public static double mobHitChance(Obj src)
    {
        double acc = src is Ent se ? Js.Or(se.accuracy, ACC_MOB_BASE) : ACC_MOB_BASE;
        return hitChance(acc, evasionValue());
    }
    public static double physDamage(IEnumerable<double> more = null) => calcDamage(physBase(), "melee", more);
    /* ============================ THE THREE ATTRIBUTES ========================== */
    /* WHAT A POISON DOES TO YOU: chaos resistance is what stands between you and it */
    public static double chaosTaken() => 1 - Math.Min(RES_CAP, player != null && player.res != null ? player.res["chaos"] : 0);
    public static readonly List<AttrDef> ATTRS = new()
    {
        new AttrDef { id = "str", name = "STRENGTH", @short = "STR", col = "#ff5a4a",
            what = "+0.25% increased physical damage and +0.25% increased maximum hearts, each" },
        new AttrDef { id = "int", name = "INTELLIGENCE", @short = "INT", col = "#5aa8ff",
            what = "+0.25% increased spell damage and +0.25% increased regeneration, each" },
        new AttrDef { id = "dex", name = "DEXTERITY", @short = "DEX", col = "#6ee06e",
            what = "+1 accuracy and +0.1% increased evasion, each — and +0.15% increased " +
                   "critical chance, critical damage and attack speed on top" },
    };
    /* ATTRIBUTE POINTS ARE PoE-SIZED */
    public const double ATTR_K = 2;
    public const double ATTR_INT_ES = 0.002 / ATTR_K;
    public const double ATTR_DEX_ACC = 2.0 / ATTR_K;
    public const double ATTR_DEX_EVA = 0.002 / ATTR_K;
    public const double ATTR_STR_DMG = 0.005 / ATTR_K, ATTR_STR_HP = 0.005 / ATTR_K;
    public const double ATTR_INT_SPELL = 0.005 / ATTR_K, ATTR_INT_REGEN = 0.005 / ATTR_K;
    public const double ATTR_DEX_CRIT = 0.003 / ATTR_K, ATTR_DEX_CMULT = 0.003 / ATTR_K, ATTR_DEX_ATK = 0.003 / ATTR_K;
    /* the weapon's own attributes plus every one a brick or a node handed over */
    public static double attrOf(string id, Ent p = null, Weapon w = null)
    {
        p ??= player; w ??= weapon;
        if (p == null) return 0;
        return Math.Max(0, (w != null && w.attr != null ? w.attr[id] : 0) + (p.attr != null ? p.attr[id] : 0));
    }
    public static double strOf(Ent p = null, Weapon w = null) => attrOf("str", p, w);
    public static double intOf(Ent p = null, Weapon w = null) => attrOf("int", p, w);
    /* ================================ MANA ================================ */
    public const double MANA_BASE = 40, MANA_PER_LVL = 4, MANA_PER_INT = 0.5 / ATTR_K, MANA_REGEN_PCT = 0.0175;
    public const string MANA_COL = "#3f7bff";
    public static bool spendMana(double n)
    {
        var p = player;
        if (p == null || !(n >= 0) || p.mana + 1e-9 < n) return false;
        p.mana = Math.Max(0, p.mana - n);
        return true;
    }
    public static double dexOf(Ent p = null, Weapon w = null) => attrOf("dex", p, w);
    public static double incSpellAll() => player.incSpell + weapon.incSpell + intOf() * ATTR_INT_SPELL;
    public static double incMeleeAll() => player.incMelee + weapon.incMelee + strOf() * ATTR_STR_DMG;
    public static double incElemAll(string el)
    {
        /* CHAOS IS NOT AN ELEMENT */
        if (el == "chaos") return player.inc["chaos"];
        return player.inc["elem"] + player.inc[el] + weapon.incElem;
    }
    /* an elemental attack's base: the weapon, plus the flat ELEMENTAL pool */
    public static double boltBase() => weapon.dmg + player.flatElem;
    public static double boltDamage(IEnumerable<double> more = null)
    {
        double d = boltBase() * (1 + incTotal("spell", weapon.elem ?? "fire")) * moreTotal();
        if (more != null) foreach (var m in more) d *= m;
        return Math.Max(0, d);
    }
    public static bool isCaster() => weapon != null && weapon.cast;
    public static bool isArcher() => weapon != null && weapon.shoot;
    public static bool isSummoner() => weapon != null && weapon.summon;
    /* what one use of the attack button is called, for the sheet's sentences */
    public static string swingWord() => isCaster() ? "cast" : isArcher() ? "shot" : isSummoner() ? "raising" : isSlammer() ? "slam" : "swing";
    public static bool isMelee() => !isCaster() && !isArcher() && !isSummoner();
    /* the mace, and anything else that ever earns the ground slam */
    public static bool isSlammer() => weapon != null && weapon.slam;
    /* Block Freeze's own damage */
    public const double FREEZE_DMG_BASE = 0.85;
    public static double frostDamage() =>
        Math.Max(0, (FREEZE_DMG_BASE + player.flatElem + player.freeze.flat) * (1 + incTotal("spell", "frost")) * moreTotal());
    /* HOW MANY CHILL STACKS ONE PULSE LAYS */
    public static double freezeChill()
    {
        var fz = player?.freeze;
        if (fz == null) return 1;
        return Math.Min(FREEZE_CHILL_MAX, 1 + Math.Max(0, fz.n - 1));
    }
    public static bool freezeDeeper() => freezeChill() < FREEZE_CHILL_MAX;
    /* ONE SENTENCE, WRITTEN ONCE */
    public static string freezeChillDesc(double add)
    {
        double now = freezeChill(), then = Math.Min(FREEZE_CHILL_MAX, now + add);
        string tail = "— five freezes a body solid and leaves it BRITTLE";
        if (then <= now) return "Already laying " + Js.Num(now) + " CHILL stacks, the deepest a pulse goes " + tail;
        return "Every pulse lays " + Js.Num(then) + " CHILL stack" + (then > 1 ? "s" : "") + " instead of " + Js.Num(now) + " " +
               "(cap " + Js.Num(FREEZE_CHILL_MAX) + ") " + tail;
    }
    /* THE STORM BRICK IS LIGHTNING */
    public static double shockDamage(double b = double.NaN) =>
        Math.Max(0, ((double.IsNaN(b) ? player.chain.dmg : b) + player.flatElem) * (1 + incTotal("spell", "shock")) * moreTotal());
    public static double chainShockChance() => ailChanceOf("shock", "chain", false);
    public const double BOLT_R_MAX_INC = 1.2;
    public const double BOLT_R_CAP = 120;
    public static double boltRadius() =>
        Math.Min(BOLT_R_CAP, pool(Js.Or(weapon.boltR, 46), 0, Math.Min(BOLT_R_MAX_INC, player != null ? player.inc["boltR"] : 0), 1));
    /* kept under their old names so nothing that reads "the sword's damage" breaks */
    public static double swordBase() => physBase();
    public static double swordDamage(IEnumerable<double> more = null) => physDamage(more);
    public static double critChance()
    {
        /* THE OVERLOAD takes criticals away entirely */
        if (player.noCrit) return 0;
        return clamp(player.critBase *
            (1 + player.incCrit + dexOf() * ATTR_DEX_CRIT + stackMod("crit")), 0, CRIT_CHANCE_CAP);
    }
    public static double critMultPct() => Math.Min(CRIT_MULT_CAP, player.critMultPct);
    public static double critMult() => critMultPct() / 100;
    /* BONE FINDS THE GAPS: a minion's hit ignores half of the armour in front of it */
    public const double MINION_ARMOUR_PEN = 0.5;
    public static double armourVs(Ent e, string source)
    {
        double a = e != null ? e.armour : 0;
        return source != null && MINION_SOURCES.Contains(source) ? a * (1 - MINION_ARMOUR_PEN) : a;
    }
    public static double armourDR(double armour, double hit)
    {
        if (armour <= 0 || hit <= 0) return 0;
        return Math.Min(ARMOUR_DR_CAP, armour / (armour + 5 * hit));
    }
    /* WARD: flat armour and flat resistance, stacking while the brick is live */
    public const double WARD_ARMOUR = 40, WARD_RES = 0.12, WARD_MAX = 2;
    public static double wardArmour() => player.wardT > 0 ? WARD_ARMOUR * player.wardN : 0;
    public static double wardRes() => player.wardT > 0 ? WARD_RES * player.wardN : 0;
    /* THE OPEN GUARD throws away armour entirely */
    public static double armourValue()
    {
        if (player != null && player.noArmour) return 0;
        return Math.Min(ARMOUR_VALUE_MAX, Math.Max(0,
            (player.armour + wardArmour()) * (1 + player.incArmour) * (1 - player.curse["armor"])));
    }
    public static double suppressChance() => clamp(player.suppress + weaponSuppress(), 0, 1);
    /* THE WORLD GETS COLDER: three tolls of 30 points off every resistance */
    public static readonly Tbl RES_TAX_WAVES = new double[] { 20, 40, 60 };
    public const double RES_TAX_STEP = 0.30;
    public static double resTaxSteps(double n = double.NaN)
    {
        double w = double.IsNaN(n) ? (wave != null ? wave.n : 0) : n;
        /* SLOW BURN pushes every toll back */
        w -= player != null ? player.tollDelay : 0;
        int k = 0;
        foreach (var t in RES_TAX_WAVES) if (w >= t) k++;
        return k;
    }
    public static double resTax(double n = double.NaN) => resTaxSteps(n) * RES_TAX_STEP;
    public static double elemTake(string elem)
    {
        /* FLAT POOL FIRST, THEN the one LESS multiplier, a SUNDERED curse */
        double flat = player.res[elem] + weaponRes() + wardRes()
                    + stackMod("res")
                    - BRAND_STEP * player.brand[elem] - resTax();
        double r = flat > 0 ? flat * (1 - player.curse["resist"]) : flat;
        return 1 - clamp(r, RES_FLOOR, RES_CAP);
    }
    /* what the brand pile is costing you, for the sheet */
    public static double brandLoss(string elem) => BRAND_STEP * player.brand[elem];
    public static void addBrand(string elem)
    {
        var p = player;
        p.brand[elem] = p.brand[elem] + 1;
        p.brandT[elem] = BRAND_T;
        addHitNum(p.x, p.y - 84, BRAND_NAME[elem] + " ×" + Js.Num(p.brand[elem]), BRAND_COL[elem], 18);
        logEvent(BRAND_NAME[elem] + " ×" + Js.Num(p.brand[elem]) + " — " + elem + " resistance " +
            Js.Num(Math.Round((1 - elemTake(elem)) * 100)) + "%", BRAND_COL[elem]);
    }
    public static void applyCurse(string kind, Obj src)
    {
        var p = player; var C = kind != null ? CURSES.GetValueOrDefault(kind) : null;
        if (p.dead || C == null) return;
        double beforeN = p.curseN[kind];
        if (beforeN < CURSE_STACK_MAX) p.curseN[kind] = beforeN + 1;
        p.curse[kind] = Math.Min(C.cap, C.step * elemPower(src) * p.curseN[kind]);
        p.curse["t"] = Math.Max(p.curse["t"], 14);
        if (p.curseN[kind] > beforeN)
        {
            string nm = C.name + (p.curseN[kind] > 1 ? " ×" + Js.Num(p.curseN[kind]) : "");
            addHitNum(p.x, p.y - 62, nm + "!", C.col, 17);
            logEvent(nm + " — -" + Js.Num(Math.Round(p.curse[kind] * 100)) + "% for " + Js.Num(Math.Ceiling(p.curse["t"])) + "s", C.col);
            addRing(p.x, p.y, 10, 260, 60, C.col, 4, 0.45);
        }
    }
    public static void updateCurses(double dt)
    {
        var p = player;
        if (p.curse["t"] <= 0) return;
        p.curse["t"] -= dt;
        if (p.curse["t"] <= 0)
        {
            foreach (var k in CURSE_KEYS) { p.curse[k] = 0; p.curseN[k] = 0; }
            p.curse["t"] = 0;
            addHitNum(p.x, p.y - 62, "CURSE LIFTED", "#a5e06e", 16);
        }
        else if (Js.R() < 0.25)
        {
            var live = CURSE_KEYS.Where(k => p.curse[k] > 0).ToList();
            if (live.Count > 0)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-16, 16), y = p.y - rnd(4, 44), vx = rnd(-10, 10), vy = -30,
                    life = 0.5, max = 0.5, color = CURSES[pick(live)].col });
        }
    }
    /* A heart lifts what you regenerate by half again for half a minute, with a floor */
    public const double HEART_BOOST_MIN = 0.08;
    public static double cursedRegen()
    {
        double sc = regenScale();
        double b = player.regenRate * (1 - player.curse["regen"]) * sc;
        int st = player.regenBoostT > 0 ? (int)Math.Min(HEART_STACK_MAX, player.heartStacks) : 0;
        double r = st != 0 ? Math.Max(b * HEART_STACK_MUL[st], HEART_BOOST_MIN * sc * HEART_STACK_MUL[st] / HEART_REGEN_MUL)
                           : b;
        if (wave != null && wave.state == "ready") r *= REGEN_BREAK_MUL;
        return r;
    }
    public static double curseSwingMul() => 1 + player.curse["haste"];
    /* Monsters an overseer has blessed hit harder with their element */
    public static double elemPower(Obj src) => 1 + 0.25 * (src is Ent se ? se.buffStacks : 0);
    /* At AIL_BREAK stacks each element overloads into something far worse */
    public static void ailmentBreak(string elem, Obj src)
    {
        var p = player; double take = elemTake(elem);
        if (take <= 0.05) return;                       /* near-immune: nothing happens */
        if (elem == "frost")
        {
            double t = STUN_T * take;
            if (t < 0.25) return;
            p.stunT = Math.Max(p.stunT, t);
            p.chill.stacks = Math.Max(0, p.chill.stacks - AIL_BREAK);   /* five are spent */
            if (p.chill.stacks <= 0) p.chill.t = 0;
            addBrand("frost");
            addHitNum(p.x, p.y - 70, "FROZEN SOLID!", "#bfe3ff", 22);
            addRing(p.x, p.y, 10, 320, 90, "#bfe3ff", 7, 0.6);
            SFX.freeze(); addShake(0.3);
        }
        else if (elem == "fire")
        {
            addBrand("fire");
            addHitNum(p.x, p.y - 70, "SCORCHED!", "#ff8a3c", 22);
            addRing(p.x, p.y, 10, 320, 90, "#ff8a3c", 7, 0.6);
            SFX.fireball(); addShake(0.25);
        }
        else if (elem == "shock")
        {
            /* electrocution: the monster's own hit, amplified */
            double power = (src != null && Js.T(src.dmg) ? src.dmg : 2) * elemPower(src);
            double hit = Math.Max(1, Math.Round(power * 4 * take));
            p.shock.stacks = Math.Max(0, p.shock.stacks - AIL_BREAK);
            if (p.shock.stacks <= 0) p.shock.t = 0;
            addBrand("shock");
            addHitNum(p.x, p.y - 76, "ELECTROCUTED!", "#ffe95a", 24);
            addRing(p.x, p.y, 8, 420, 130, "#ffe95a", 9, 0.6);
            for (int i = 0; i < 7; i++) addZap(p.x + rnd(-70, 70), p.y - 90, p.x, p.y - 14, 0);
            SFX.thunder(); addShake(0.55);
            ailmentDamage(hit);
        }
    }
    public static void applyAilment(string elem, Obj src, double stacks = 0)
    {
        var p = player;
        double many = Math.Max(1, Js.Or(stacks, 1));
        if (p.dead) return;
        if (p.ailCd[elem] > 0) return;   /* standing in a fire shouldn't spam stacks */
        p.ailCd[elem] = 0.7;
        double take = elemTake(elem) * elemPower(src);
        if (take <= 0.02) return;
        if (elem == "fire")
        {
            /* one more fire on you: 3 damage over 12 seconds */
            var b = p.burn;
            b.list ??= new List<Dot>();
            b.list.Add(new Dot { dps = 3 * take / BURN_DUR, t = BURN_DUR });
            addHitNum(p.x, p.y - 58, "BURNING ×" + b.list.Count, "#ff8a3c", 15);
            logEventK("ail|fire", "burning ×" + b.list.Count, "#ff8a3c");
            /* the fifth fire is spent branding you */
            if (b.list.Count >= AIL_BREAK)
            {
                b.list.Splice(0, (int)AIL_BREAK);
                ailmentBreak("fire", src);
            }
        }
        else if (elem == "frost")
        {
            p.chill.stacks += (take >= 0.5 ? 1 : 0.5);
            p.chill.t = Math.Max(p.chill.t, 5 * take);
            addHitNum(p.x, p.y - 58, "CHILLED ×" + Js.Num(Math.Round(p.chill.stacks)), "#bfe3ff", 15);
            logEventK("ail|frost", "chilled ×" + Js.Num(Math.Round(p.chill.stacks)), "#bfe3ff");
            if (p.chill.stacks >= AIL_BREAK) ailmentBreak("frost", src);
        }
        else if (elem == "shock")
        {
            /* a storm orb dumps a whole handful at once, thinned by your resistance */
            double add = many > 1 ? many * clamp(take, 0, 1) : (take >= 0.5 ? 1 : 0.5);
            p.shock.stacks += add;
            p.shock.t = Math.Max(p.shock.t, 6 * take);
            addHitNum(p.x, p.y - 58, "SHOCKED ×" + Js.Num(Math.Round(p.shock.stacks)), "#ffe95a", 15);
            logEventK("ail|shock", "shocked ×" + Js.Num(Math.Round(p.shock.stacks)), "#ffe95a");
            if (p.shock.stacks >= AIL_BREAK) ailmentBreak("shock", src);
        }
    }
    static readonly Obj AILMENT_SRC = new() { type = "ailment" };
    /* `n` may be a fraction of a heart; `type` chaos walks past an energy shield */
    public static void ailmentDamage(double n, bool quiet = false, string type = null)
    {
        var p = player;
        if (godMode || p.dead || state != "play") return;
        /* Chaos Inoculation: a dose is chaos over time, and chaos does not arrive */
        if (type == "chaos" && p.chaosInoc) return;
        n = n * weaponLessTaken();
        /* AN AILMENT IS DAMAGE TAKEN, so the shield stands in front of it too */
        if (p.esMax > 0) n = esAbsorb(p, n, type, 0);
        else p.esT = ES_DELAY;
        if (n <= 0.001) return;
        p.hp = Math.Round((p.hp - n) * 100) / 100;
        p.regenStall = REGEN_STALL;
        p.flash = 1; redFlash = Math.Max(redFlash, 0.35);
        if (!quiet) addHitNum(p.x, p.y - 44, "-" + fmtHp(n), "#ff8a3c", 16);
        logTaken(n, AILMENT_SRC, null, false, false);
        if (p.hp <= 0) { p.hp = 0; playerDie(); }
    }
    /* Stacks are uncapped now, so the EFFECTS need floors */
    public const double CHILL_FLOOR = 0.30;
    public static double chillMul() => Math.Max(CHILL_FLOOR, 1 - 0.09 * player.chill.stacks);
    public static double shockCritChance() => Math.Min(0.95, 0.10 * player.shock.stacks);
    /* one dot list on the hero, ticked: every stack bites at once, the number pops per whole heart */
    static double HeroDotTick(DotList L, double dt, double mul = 1)
    {
        double deal = 0;
        foreach (var f in L.list) { double step = Math.Min(f.t, dt); deal += f.dps * step * mul; f.t -= dt; }
        return deal;
    }
    public static void updateAilments(double dt)
    {
        var p = player;
        updateCurses(dt);
        p.stunT = Math.Max(0, p.stunT - dt);
        foreach (var el in ELEMENTS)
        {
            if (p.brandT[el] > 0)
            {
                p.brandT[el] -= dt;
                if (p.brandT[el] <= 0) { p.brandT[el] = 0; p.brand[el] = 0; }
            }
        }
        if (p.stunT > 0 && Js.R() < 0.4)
            particles.Add(new Particle { kind = "spark", x = p.x + rnd(-18, 18), y = p.y - rnd(4, 44), vx = rnd(-20, 20), vy = rnd(-20, 20),
                life = 0.3, max = 0.3, color = "#dff2ff" });
        foreach (var el in ELEMENTS)
            if (p.brand[el] > 0 && Js.R() < 0.06 * p.brand[el])
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-16, 16), y = p.y - rnd(4, 40), vx = 0, vy = -50,
                    life = 0.4, max = 0.4, color = BRAND_COL[el] });
        foreach (var el in ELEMENTS) p.ailCd[el] = Math.Max(0, p.ailCd[el] - dt);
        p.burn.list ??= new List<Dot>();
        p.burn.brkCd = Math.Max(0, p.burn.brkCd - dt);
        if (p.burn.list.Count > 0)
        {
            /* every fire burns at once */
            double deal = HeroDotTick(p.burn, dt);
            p.burn.list = p.burn.list.Filter(f => f.t > 0);
            p.burn.acc += deal;
            bool show = p.burn.acc >= 1;
            if (show) p.burn.acc -= 1;
            if (deal > 0) ailmentDamage(deal, !show);
            if (show) addHitNum(p.x, p.y - 44, "-1", "#ff8a3c", 16);
            if (Js.R() < 0.5)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-12, 12), y = p.y - rnd(6, 40), vx = rnd(-14, 14), vy = rnd(-80, -30),
                    life = 0.4, max = 0.4, color = Js.R() < 0.5 ? "#ff9a3c" : "#ffd166" });
        }
        else p.burn.acc = 0;
        /* the rot, exactly as the fire above */
        p.psn ??= new DotList();
        if (p.psn.list.Count > 0)
        {
            double deal = HeroDotTick(p.psn, dt);
            /* A DOSE IS CHAOS DAMAGE OVER TIME */
            deal *= chaosTaken();
            p.psn.list = p.psn.list.Filter(f => f.t > 0);
            p.psn.acc += deal;
            bool show = p.psn.acc >= 1;
            if (show) p.psn.acc -= 1;
            if (deal > 0) ailmentDamage(deal, !show, "chaos");
            if (show) addHitNum(p.x, p.y - 44, "-1", "#8ac24a", 16);
            if (Js.R() < 0.4)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-12, 12), y = p.y - rnd(6, 40),
                    vx = rnd(-12, 12), vy = rnd(-60, -20), life = 0.45, max = 0.45,
                    color = Js.R() < 0.5 ? "#8ac24a" : "#5f9a2e" });
        }
        else p.psn.acc = 0;
        p.pbleed ??= new DotList();
        if (p.pbleed.list.Count > 0)
        {
            double mv = p.moving ? PBLEED_MOVE : 1;
            double deal = HeroDotTick(p.pbleed, dt, mv);
            p.pbleed.list = p.pbleed.list.Filter(f => f.t > 0);
            p.pbleed.acc += deal;
            bool show = p.pbleed.acc >= 1;
            if (show) p.pbleed.acc -= 1;
            if (deal > 0) ailmentDamage(deal, !show, "phys");
            if (show) addHitNum(p.x, p.y - 44, "-1", "#ff5a5a", 16);
            if (Js.R() < 0.3 * mv)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-10, 10), y = p.y - rnd(10, 34), vx = rnd(-10, 10), vy = rnd(10, 50), life = 0.45, max = 0.45, color = "#c0182a" });
        }
        else p.pbleed.acc = 0;
        if (p.chill.stacks > 0)
        {
            p.chill.t -= dt;
            if (p.chill.t <= 0) { p.chill.stacks = Math.Max(0, p.chill.stacks - 1); p.chill.t = p.chill.stacks > 0 ? 2.2 : 0; }
            if (Js.R() < 0.08 * p.chill.stacks)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-14, 14), y = p.y - rnd(10, 40), vx = rnd(-12, 12), vy = -24,
                    life = 0.5, max = 0.5, color = "#dff2ff" });
        }
        p.slowT = p.chill.stacks > 0 ? Math.Max(0.1, p.chill.t) : 0;
        if (p.shock.stacks > 0)
        {
            p.shock.t -= dt;
            if (p.shock.t <= 0) { p.shock.stacks = Math.Max(0, p.shock.stacks - 1); p.shock.t = p.shock.stacks > 0 ? 2.2 : 0; }
            if (Js.R() < 0.10 * p.shock.stacks)
                particles.Add(new Particle { kind = "spark", x = p.x + rnd(-16, 16), y = p.y - rnd(6, 44), vx = rnd(-40, 40), vy = rnd(-30, 30),
                    life = 0.18, max = 0.18, color = "#ffe95a" });
        }
    }
    /* what a source's chaos share is: its own, or its thrower's (a hexbolt records it in `src`) */
    static double SrcChaosShare(Obj src)
    {
        if (src is Ent se && Js.T(se.chaosShare)) return se.chaosShare;
        if (src is Proj pr && pr.src != null) return pr.src.chaosShare;
        return 0;
    }
    /* `elem` tags fire/frost/shock damage; `spell` marks a hit as spell damage and
       defaults to true for anything elemental. Spell hits meet suppression, attack
       hits meet armour -- never both. */
    public static void playerDamage(double n, Obj src, string elem = null, bool? spell = null)
    {
        var p = player;
        if (src != null && ritualOutside(src)) return;           /* from beyond a running ritual's wall */
        if (p != null && p.isProxy) { minionHurt(p.minion, n, elem); return; }
        if (godMode) return;
        var se = src as Ent;
        /* a VENOMOUS boss leaves something behind on every hit */
        if (se != null && se.venom && !p.dead && p.invuln <= 0 && p.dash.t <= 0)
            applyAilment("fire", src);
        /* an ENVENOMED rare poisons with every hit it lands */
        if (se != null && se.rmVenom && !p.dead && p.invuln <= 0 && p.dash.t <= 0)
            poisonPlayer(Math.Max(0.2, Js.Or(se.dmg, 1) * 0.35), PSN_DUR);
        if (p.dead || p.invuln > 0 || p.dash.t > 0 || state != "play") return;
        bool isSpell = spell ?? (elem != null);
        /* AND THE OTHER HALF OF THE CONTEST: evasion, attacks only */
        if (!isSpell && evasionValue() > 0 && Js.R() > mobHitChance(src))
        {
            addHitNum(p.x, p.y - 56, "EVADED", "#c9b8e8", 16);
            addSparks(p.x, p.y - 18, 4, "#c9b8e8");
            clogRun.evaded++;
            p.invuln = IFRAME_T * 0.5;
            return;
        }
        double d = n * ringLessFrom(src);        /* your rings turn a breach body's blows, and nobody else's */
        /* A RAGING MONSTER HITS HARDER, and it is read off the ATTACKER */
        if (se != null && se.stacks != null) d *= rageMore(se);
        if (elem != null) d *= elemTake(elem) * elemPower(src);
        /* shock leaves you open -- hits against a shocked hero crit */
        bool scrit = false, supp = false;
        if (p.shock.stacks > 0 && Js.R() < shockCritChance())
        {
            d *= 1.8; scrit = true;
            addHitNum(p.x, p.y - 64, "SHOCK CRIT!", "#ffe95a", 18);
            addShake(0.2);
        }
        if (isSpell)
        {
            if (Js.R() < suppressChance())
            {
                d *= SUPPRESS_TAKEN; supp = true;
                addHitNum(p.x, p.y - 70, "SUPPRESSED", "#9fd4ff", 16);
            }
            /* a LESS multiplier, after suppression */
            d *= (1 - p.lessSpell);
        }
        else
        {
            d *= (1 - armourDR(armourValue(), d));
        }
        /* LAST: what you are holding, and every ENDURANCE charge */
        d *= weaponLessTaken();
        d *= Math.Max(0.1, 1 - stackMod("less"));
        n = Math.Max(0.05, Math.Round(d * 100) / 100);
        logTaken(n, src, elem, supp, scrit);
        /* RAGE IS BUILT BY BEING HIT */
        if (p.rageOnHurt > 0) stackAdd("rage", p.rageOnHurt);
        /* CHAOS INOCULATION: the chaos part of a hit simply does not arrive */
        if (p.chaosInoc)
        {
            double ci = (elem == "chaos") ? 1 : clamp(SrcChaosShare(src), 0, 1);
            n = n * (1 - ci);
            if (n <= 0.001) { addHitNum(p.x, p.y - 56, "IMMUNE", "#8ede4a", 15); p.invuln = IFRAME_T; return; }
        }
        /* THE SHIELD TAKES IT, and a monster's chaos walks straight past */
        if (p.esMax > 0)
        {
            double was = p.es;
            double cs = p.chaosInoc ? 0 : SrcChaosShare(src);
            n = esAbsorb(p, n, null, cs);
            double ate = was - p.es;
            if (ate > 0.01)
            {
                p.esFlash = 1;
                addHitNum(p.x, p.y - 50, "-" + fmtHp(ate), ES_COL, 14);
                addSparks(p.x, p.y - 20, 4, ES_COL);
            }
            if (n <= 0.001) { p.invuln = IFRAME_T; p.flash = 1; return; }
        }
        else p.esT = ES_DELAY;
        /* AN ENDURANCE CHARGE IS EARNED BY BEING HIT */
        if (p.chargeHurt > 0) stackAdd("endurance", p.chargeHurt);
        if (elem != null) applyAilment(elem, src);
        p.hp = Math.Round((p.hp - n) * 100) / 100;
        p.regenStall = REGEN_STALL;
        p.invuln = IFRAME_T; p.flash = 1; redFlash = 0.6;
        double a = angTo(src, p);
        p.kx = Math.Cos(a) * 280 * KB_SCALE; p.ky = Math.Sin(a) * 280 * KB_SCALE;
        addShake(0.5); SFX.hurt();
        if (p.hp <= 0) { p.hp = 0; playerDie(); }
    }
    public static void perfectDodge(double x, double y)
    {
        tScale = 0.35;
        addHitNum(player.x, player.y - 52, "PERFECT!", "#9fdc7e", 20);
        addRing(x, y, 8, 260, 70, "#9fdc7e", 5, 0.4);
        spawnStuds(player.x, player.y, 3);
        player.bomb.cd = Math.Max(0, player.bomb.cd - 2);
        SFX.deflect();
    }
    /* ============================ THE SECOND CHANCE ============================ */
    public const double RESPAWN_FINE_BASE = 150, RESPAWN_FINE_WAVE = 25, RESPAWN_FINE_MUL = 3;
    public const double RESPAWN_GRACE = 3, RESPAWN_PUSH_R = 320;
    public static RespawnState respawn = new();
    public static void respawnReset() { respawn = new RespawnState { free = 1, paid = 0, banked = false }; }
    public static double respawnFine() => Math.Round((RESPAWN_FINE_BASE + RESPAWN_FINE_WAVE * Math.Max(1, wave.n)) * Math.Pow(RESPAWN_FINE_MUL, respawn.paid));
    /* what the death screen may offer: null when nothing (not the arcade, a bench run) */
    public static RespawnOffer respawnOffer()
    {
        if (!arcadeMode() || sandbox || player == null) return null;
        if (respawn.free > 0) return new RespawnOffer { free = true, cost = 0, can = true };
        double cost = respawnFine();
        return new RespawnOffer { free = false, cost = cost, can = player.studs >= cost };
    }
    /* a boss down gives the free one back */
    public static void respawnOnBoss()
    {
        if (!arcadeMode() || respawn.free > 0) return;
        respawn.free = 1;
        showBanner("SECOND CHANCE", "READY AGAIN", "#ffd76e", 1.8, 38);
        logEvent("second chance ready again", "#ffd76e");
    }
    public static bool respawnNow()
    {
        var o = respawnOffer();
        if (o == null || !o.can || player == null || !player.dead) return false;
        var p = player;
        if (o.free) respawn.free = 0;
        else { p.studs -= o.cost; respawn.paid++; }
        Ui.Hide("overScreen");
        p.dead = false; p.deadT = 0; p.hp = p.maxHp; if (p.esMax > 0) p.es = p.esMax;
        /* the ailments are CLEARED, not deleted */
        p.invuln = RESPAWN_GRACE; p.stunT = 0; p.kx = 0; p.ky = 0;
        p.psn = new DotList(); p.pbleed = new DotList(); p.burn = new DotList();
        p.chill = new Stacks(); p.shock = new Stacks(); p.slowT = 0;
        ctlReset();   /* no walk or target from before the fall carries over */
        tScale = 1;
        /* room to breathe: everything close is thrown back, every shot in the air near you goes */
        foreach (var e in enemies)
        {
            if (e.dead || e.state != "active") continue;
            double d = hypot(e.x - p.x, e.y - p.y);
            if (d < RESPAWN_PUSH_R) { double a = Math.Atan2(e.y - p.y, e.x - p.x), k = 1 - d / RESPAWN_PUSH_R; e.kx = e.kx + Math.Cos(a) * 700 * k; e.ky = e.ky + Math.Sin(a) * 700 * k; }
        }
        foreach (var pr in projectiles) if (pr.owner == "enemy" && hypot(pr.x - p.x, pr.y - p.y) < RESPAWN_PUSH_R * 1.5) pr.dead = true;
        addRing(p.x, p.y, 12, 420, RESPAWN_PUSH_R, "#ffd76e", 8, 0.6); addShake(0.3);
        showBanner("BACK ON YOUR FEET", o.free ? "SECOND CHANCE" : "FINE PAID · " + Js.Num(o.cost) + " STUDS", "#ffd76e", 1.8, 40);
        logEvent(o.free ? "second chance taken" : "respawned for " + Js.Num(o.cost) + " studs", "#ffd76e");
        state = "play"; Ui.Cursor(clickMode() ? "default" : "none");
        return true;
    }
    public static void playerDie()
    {
        closeBook();
        var p = player; p.dead = true; p.deadT = 0;
        spawnBricks(p.x, p.y, new[] { HERO_COLORS.head, HERO_COLORS.torso, HERO_COLORS.legs, PAL.hyellow }, 18, 280);
        SFX.brick(9); SFX.hurt(); addShake(0.55); tScale = 0.3;
        schedule(1.6, () => { if (rpgOn()) rpgDeath(); else showGameOver(); });
    }
}
