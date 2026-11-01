// Port of lego-survival.html lines 15568-15717: FINDING THINGS ON THE MAP -- every
// node's tags, the text a search reads, and the query as a regular expression.
using System.Text.RegularExpressions;

namespace Brickblade;

public static partial class G
{
    /* ======================= FINDING THINGS ON THE MAP ========================
       Fifteen hundred nodes is a map you SEARCH, not one you read. Every node wears
       TAGS -- words for what it is FOR, not just what it says -- and the search box
       matches a regular expression against the name, the lines on the node, the
       cluster it sits in and those tags. So "minion" finds every node a summoner
       wants, "attr" every strength, dexterity and intelligence, "sword" every node
       a sword needs or is paid by, and "fire res" finds +every resistance too:
       a node that gives fire resistance SECOND-HAND is still a node that gives it.

       The tags come off the STAT, like the icon does, so a stat written tomorrow
       needs one line here and every node carrying it is findable from then on. */
    public static readonly JsMap<string, string> META_TAG_DMG = new()
    {
        ["phys"] = "physical", ["fire"] = "fire elemental", ["frost"] = "cold frost elemental",
        ["shock"] = "lightning shock elemental", ["chaos"] = "chaos",
    };
    /* what each spell brick deals, so "lightning" finds the Storm Brick's nodes */
    public static readonly JsMap<string, string> META_TAG_SPELL = new()
    {
        ["orbit"] = "physical guardian orbit", ["chain"] = "lightning shock elemental chain",
        ["storm"] = "physical bladestorm", ["vortex"] = "physical vortex", ["freeze"] = "cold frost elemental freeze chill",
        ["bomb"] = "fire elemental bomb area", ["bane"] = "chaos damage over time poison",
    };
    public const string META_TAG_RES = "defence resistance elemental resistance ";
    public const string META_TAG_LIFE = "defence life hearts health ";
    public static readonly JsMap<string, string> META_TAGS = new()
    {
        ["incMelee"] = "offence damage physical attack melee",
        ["incSpell"] = "offence damage spell caster",
        ["incAll"] = "offence damage generic",
        ["moreAll"] = "offence damage more multiplier",
        ["flatPhys"] = "offence damage physical attack flat",
        ["flatElem"] = "offence damage elemental fire cold frost lightning flat",
        ["incAtk"] = "offence attack speed",
        ["incCast"] = "offence spell caster cast speed",
        ["incCrit"] = "offence critical crit",
        ["critMult"] = "offence critical crit multiplier",
        ["incCritMult"] = "offence critical crit multiplier",
        ["reach"] = "offence melee attack area range",
        ["incDot"] = "offence damage over time dot ailment",
        ["lessDot"] = "damage over time dot ailment",
        ["bleedChance"] = "offence physical bleed bleeding damage over time dot ailment",
        ["bleedShare"] = "offence physical bleed bleeding damage over time dot ailment",
        ["poisonDot"] = "offence chaos poison damage over time dot ailment",
        ["ailChance"] = "offence ailment ignite burn chill shock bleed poison damage over time",
        ["rageHit"] = "offence rage melee attack",
        ["rageHurt"] = "offence rage",
        ["rageMax"] = "offence rage",
        ["chargeKill"] = "charge frenzy offence",
        ["chargeCrit"] = "charge power critical crit offence",
        ["chargeHurt"] = "charge endurance defence",
        ["chargeMax"] = "charge frenzy power endurance",
        ["chargeAll"] = "charge frenzy power endurance",
        ["acc"] = "offence accuracy attack",
        ["incAcc"] = "offence accuracy attack",
        ["eva"] = "defence evasion",
        ["incEva"] = "defence evasion",
        ["es"] = "defence energy shield es",
        ["incEs"] = "defence energy shield es",
        ["ailMag"] = "offence ailment chill shock elemental cold frost lightning",
        ["incElem"] = "offence damage elemental fire cold frost lightning",
        ["incFire"] = "offence damage elemental fire",
        ["incFrost"] = "offence damage elemental cold frost",
        ["incShock"] = "offence damage elemental lightning shock",
        ["incChaos"] = "offence damage chaos",
        ["chaosRes"] = "defence resistance chaos resistance",
        ["heartsPerInt"] = META_TAG_LIFE+"intelligence attribute",
        ["noArmour"] = "defence armour armor",
        ["lessSpell"] = "defence spell damage taken",
        ["boltR"] = "offence spell area fireball fire elemental",
        ["ember"] = "offence spell fire elemental",
        ["armour"] = "defence armour armor",
        ["incArmour"] = "defence armour armor",
        ["suppress"] = "defence spell suppression",
        ["allRes"] = META_TAG_RES+"fire resistance cold resistance frost resistance lightning resistance all resistances",
        ["fireRes"] = META_TAG_RES+"fire resistance",
        ["frostRes"] = META_TAG_RES+"cold resistance frost resistance",
        ["shockRes"] = META_TAG_RES+"lightning resistance",
        ["str"] = "attribute strength",
        ["dex"] = "attribute dexterity",
        ["int"] = "attribute intelligence",
        ["hearts"] = META_TAG_LIFE,
        ["incHp"] = META_TAG_LIFE,
        ["moreHp"] = META_TAG_LIFE,
        ["mana"] = "mana",
        ["incMana"] = "mana",
        ["manaRegen"] = "mana regeneration recovery",
        ["incManaRegen"] = "mana regeneration recovery",
        ["regen"] = META_TAG_LIFE+"regeneration regen recovery",
        ["incRegen"] = META_TAG_LIFE+"regeneration regen recovery",
        ["leech"] = META_TAG_LIFE+"leech lifesteal recovery attack",
        ["leechRate"] = META_TAG_LIFE+"leech lifesteal recovery attack",
        ["heartDrop"] = META_TAG_LIFE+"recovery utility",
        ["speed"] = "utility movement move speed",
        ["roll"] = "utility movement roll dodge",
        ["magnet"] = "utility loot pickup",
        ["studs"] = "utility loot studs",
        ["xp"] = "utility experience xp",
        ["slamDmg"] = "offence damage physical attack slam area",
        ["slamArc"] = "offence slam area",
        ["slamRange"] = "offence slam area",
        ["spellCap"] = "spell brick",
        ["orbitDmg"] = "offence damage spell brick",
        ["orbitR"] = "offence spell brick area",
        ["orbitSpd"] = "offence spell brick speed",
        ["chainDmg"] = "offence damage spell brick",
        ["chainFork"] = "offence spell brick",
        ["chainRate"] = "offence spell brick speed",
        ["stormDmg"] = "offence damage spell brick",
        ["stormSize"] = "offence spell brick area",
        ["stormRate"] = "offence spell brick speed",
        ["vortexDmg"] = "offence damage spell brick",
        ["vortexR"] = "offence spell brick area",
        ["vortexRate"] = "offence spell brick speed",
        ["freezeR"] = "offence spell brick area",
        ["freezeRate"] = "offence spell brick speed",
        ["bombDmg"] = "offence damage spell brick",
        ["bombCluster"] = "offence spell brick",
        ["bombRate"] = "offence spell brick speed",
        ["bombR"] = "offence spell brick area",
        ["baneDmg"] = "offence damage spell brick",
        ["baneR"] = "offence spell brick area",
        ["baneRate"] = "offence spell brick speed",
        ["tollDelay"] = "utility elemental toll",
        ["reroll"] = "utility reroll",
        ["noReroll"] = "utility reroll",
        ["chestCards"] = "utility loot chest cards",
        ["bankMul"] = "utility loot studs bank",
        ["noRegen"] = META_TAG_LIFE+"regeneration regen",
        ["minionDmg"] = "minion summon offence damage",
        ["minionLife"] = "minion summon defence life",
        ["minionSpd"] = "minion summon attack cast speed",
        ["minionZombies"] = "minion summon zombie corpse",
        ["minionProj"] = "minion summon skeleton projectile",
        ["golemCap"] = "minion summon golem",
        ["minionMax"] = "minion summon skeleton",
        ["minionLess"] = "minion summon damage",
        ["minionMirror"] = "minion summon damage conversion",
        ["minionInstab"] = "minion summon skeleton fire elemental area",
        ["chaosInoc"] = "defence chaos resistance energy shield es life hearts",
        ["ghostReaver"] = "defence energy shield es leech lifesteal recovery",
        ["zealot"] = "defence energy shield es regeneration regen recovery",
        ["arrowFlat"] = "offence damage physical attack projectile arrow flat",
        ["arrowPierce"] = "offence attack projectile arrow pierce",
        ["arrowN"] = "offence attack projectile arrow multiple extra additional",
        ["ricochet"] = "offence attack projectile arrow",
        ["arrowLess"] = "attack projectile arrow damage",
        ["arrowFork"] = "offence attack projectile arrow lightning fork",
        ["arrowBlast"] = "offence attack projectile arrow fire burst area radius",
        ["whirlShare"] = "offence attack melee whirl spin blademaster damage",
        ["reaveShare"] = "offence attack melee axe reave throw damage",
        ["reaveBounce"] = "offence attack melee axe reave throw bounce",
        ["penFire"] = "offence damage fire penetration resistance",
        ["penFrost"] = "offence damage cold frost penetration resistance",
        ["penShock"] = "offence damage lightning shock penetration resistance",
        ["penChaos"] = "offence damage chaos penetration resistance",
        ["penElem"] = "offence damage elemental fire cold lightning penetration resistance",
        ["prolif"] = "offence ailment area ignite burn poison bleed damage over time",
        ["critAilAll"] = "offence critical crit ailment",
        ["noCrit"] = "critical crit",
        ["ailFull"] = "offence ailment chill shock elemental",
        ["exposeChance"] = "offence exposure elemental penetration",
    };
    /* which spell brick a stat belongs to, off its prefix: "chainRate" is the Storm Brick's */
    public static string metaStatSpell(string k)
    {
        foreach (var sk in SPELL_KEYS) if (k.IndexOf(sk, StringComparison.Ordinal) == 0) return sk;
        return null;
    }
    public static string metaWeapTag(string wk)
    {
        var W = WEAPONS.GetValueOrDefault(wk ?? ""); string nm = (W != null && Js.T(W.name) ? W.name : wk).ToLowerInvariant();
        return nm == wk ? wk : wk + " " + nm;
    }
    static readonly Regex CV_KEY = new(@"^(cv|gn)_([a-z]+)_([a-z]+)$"), W_KEY = new(@"^(?:dmg|inc)_(.+)$");
    public static string metaStatTags(string k, string v)
    {
        var @out = new List<string> { META_TAGS.GetValueOrDefault(k) ?? "" };
        var st = META_STATS.GetValueOrDefault(k);
        if (st != null && st.only != null) foreach (var wk in st.only) @out.Add(metaWeapTag(wk));
        if (k == "grant" && Js.T(v)) @out.Add("spell brick legendary " + (META_TAG_SPELL.GetValueOrDefault(v) ?? "") + " " + (SPELL_NAME.GetValueOrDefault(v) ?? "").ToLowerInvariant());
        string sp = metaStatSpell(k);
        if (sp != null) { @out.Add(META_TAG_SPELL.GetValueOrDefault(sp) ?? ""); @out.Add((SPELL_NAME.GetValueOrDefault(sp) ?? "").ToLowerInvariant()); }
        var cv = CV_KEY.Match(k);
        if (cv.Success) @out.Add("offence damage conversion " + (cv.Groups[1].Value == "gn" ? "gain extra " : "converted ") +
                        (META_TAG_DMG.GetValueOrDefault(cv.Groups[2].Value) ?? cv.Groups[2].Value) + " " + (META_TAG_DMG.GetValueOrDefault(cv.Groups[3].Value) ?? cv.Groups[3].Value));
        var w = W_KEY.Match(k);
        if (w.Success)
        {
            var W = WEAPONS.GetValueOrDefault(w.Groups[1].Value);
            @out.Add("offence damage " + metaWeapTag(w.Groups[1].Value) + " " + (W != null && W.cast ? "spell caster" : "physical attack"));
        }
        return string.Join(" ", @out);
    }
    /* a mod's value as the tag reader sees it: the word, or a number stringified */
    static string ModVal(Mod m) => m.s ?? (m.num ? Js.Num(m.v) : null);
    /* EVERY NODE'S TAGS, as one string of words. Read once, the first time anybody
       searches, and kept: nothing about a node's tags changes during a session. */
    public static string metaNodeTags(MetaNode n)
    {
        if (n._tags != null) return n._tags;
        var t = new List<string>();
        t.Add(n.kind == "travel" ? (n.road == "attr" ? "attribute break" : "path travel highway")
             : n.kind == "start" ? "start class beginning" : n.kind == "legend" ? "legendary" : n.kind);
        if (n.kind == "start" && Js.T(n.weap)) t.Add(metaWeapTag(n.weap));
        if (Js.T(n.start)) t.Add(metaWeapTag(n.start));                /* a door's own opening steps */
        foreach (var m in n.mods ?? new()) t.Add(metaStatTags(m.k, ModVal(m)));
        return n._tags = WS_RUN.Replace(string.Join(" ", t), " ").Trim();
    }
    /* what the search reads: the tags, and everything the card says */
    public static string metaNodeHay(MetaNode n)
    {
        if (n._hay != null) return n._hay;
        var cl = n.cl != null ? META_CLUSTER_BY_ID.GetValueOrDefault(n.cl) : null;
        var parts = new List<string> { n.name, n.note ?? "", cl != null ? cl.name : "", metaKindName(n) };
        parts.AddRange((n.mods ?? new()).Select(metaModText));
        parts.Add(metaNodeTags(n));
        return n._hay = string.Join(" · ", parts);
    }
    /* THE QUERY IS A REGULAR EXPRESSION, case-blind, found anywhere in the text --
       "attr" is *attr*. Something that is not a legal expression is not an error:
       "*attr*" is read as the wildcard the hand meant, and anything else that still
       will not compile is searched for as the letters typed. */
    const RegexOptions JS_I = RegexOptions.ECMAScript | RegexOptions.IgnoreCase;
    static readonly Regex STAR_ENDS = new(@"^\*+|\*+$"), RE_SPECIAL = new(@"[.*+?^${}()|[\]\\]");
    public static Regex metaSearchRe(string q)
    {
        q = (q ?? "").Trim();
        if (q.Length == 0) return null;
        try { return new Regex(q, JS_I); } catch { }
        try { return new Regex(STAR_ENDS.Replace(q, "").Replace("*", ".*"), JS_I); } catch { }
        return new Regex(RE_SPECIAL.Replace(q, "\\$&"), JS_I);
    }
    public static List<string> metaSearch(string q)
    {
        var re = metaSearchRe(q);
        if (re == null) return new();
        return META_NODES.Where(n => re.IsMatch(metaNodeHay(n))).Select(n => n.id).ToList();
    }
}
