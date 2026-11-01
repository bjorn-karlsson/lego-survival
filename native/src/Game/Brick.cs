// Port of lego-survival.html lines 18845-19715: WHAT A BRICK DOES -- every number
// a brick can move is a PROBE; hovering a card applies it to a copy of the hero,
// reads every probe again and lists what moved, now -> next. Also what a monster
// is (the bench's spawn tips), the price of a reroll and the reward cards.
namespace Brickblade;

public sealed partial class StatProbe { }
public sealed class CmpRow { public string g, k, why, from, to; public bool up, fresh; }
public sealed class MobRow { public string g, k, v; }

public static partial class G
{
    /* =============================== WHAT A BRICK DOES =============================== */
    /* Cards used to end in "(now 47%)", which told you where you ended up but never
       what you were comparing it against. Instead every number a brick can move is
       a PROBE: hovering a reward applies it to a copy of the hero, reads every probe
       again, and lists only what actually moved, as now -> next. Because the probes
       include every spell's damage, one "+18% increased spell damage" card shows its
       effect on the guardian ring, the storm brick and the bombs all at once, on separate
       lines, which is the only honest way to read it. */
    public static double swordDps()
    {
        var p = player;
        double swingT = p.atkRate / Math.Max(0.3, chillMul()) * curseSwingMul();
        double critAvg = 1 + critChance() * (critMult() - 1);
        /* A SLAMMER HAS NO SWING to average. Its basic attack is a fan of teeth, and
           what one body takes from one fan is however many of them reach it — capped
           at SLAM_MAX_HITS, and only worth counting for a body actually standing in
           the fault, which is what this number is for. */
        if (isSlammer()) return slamSpikeDamage() * SLAM_MAX_HITS * (1 / swingT) * critAvg;
        /* AN ARCHER HAS NO COMBO either. One body in the line takes one arrow a
           shot; the volley's other arrows fan out to other bodies, so they are not
           this number's to count. */
        if (isArcher()) return arrowDamage() * (1 / swingT) * critAvg;
        /* A SUMMONER's number is the LEGION's: a full legion, each at its own rate */
        if (isSummoner()) return minionDps();
        double light = physDamage(), spin = light * weapon.spin;
        return ((light * 2 + spin) / 3) * (1 / swingT) * critAvg;
    }
    /* WHAT EVERY STAT ACTUALLY MEANS, in one table keyed "GROUP|Key". It lives
       beside the probes rather than inside them so one sentence serves all three
       places a stat is read: the reward comparison, the character sheet, and the
       title screen's stats panel. A probe may carry its own `why:` instead — this
       fills in everything that does not. Anything with no entry shows no tooltip
       rather than a wrong one; statWhy() is the only reader. */
    public static readonly Dictionary<string, string> STAT_WHY = new()
    {
        ["PHYSICAL|Weapon hit"] = "one ordinary swing, before crits. (Base + flat) × (1 + increased) × each more",
        ["PHYSICAL|Spin swing"] = "every third swing is a spin — a MORE multiplier of its own, on top of everything",
        ["PHYSICAL|Sustained DPS"] = "the real number: the 2-light-1-spin combo, at your swing rate, with crits averaged in",
        ["PHYSICAL|Whirl DPS"] = "Blademaster: what standing inside the whirl costs a body that stays there",
        ["PHYSICAL|Base + flat"] = "your weapon plus every FLAT physical brick — the number every increase then multiplies",
        ["PHYSICAL|Swing rate"] = "swings a second. This is ATTACK SPEED — the hands. CAST SPEED is the abilities",
        ["PHYSICAL|Reach"] = "how far the arc reaches. A spin reaches further still",
        ["PHYSICAL|Crit chance"] = "your weapon base × (1 + increased). A crit also rolls for bleed",
        ["SWORD|Crit damage"] = "what a critical strike deals, as a share of the hit. 150% means one and a half times",
        ["DAMAGE|Increased melee"] = "the POOL: physical only — weapon, bladestorm, vortex. Increases ADD to one another",
        ["DAMAGE|Increased spell"] = "the POOL: everything you cast. Adds into the same sum, never multiplies it",
        ["DAMAGE|Increased all"] = "the POOL from levels (+5% each) — it applies to every kind of damage you deal",
        ["DAMAGE|On a weapon hit"] = "the DERIVED number a physical hit is multiplied by: all + melee, stopped at the ceiling",
        ["DAMAGE|On a spell hit"] = "the DERIVED number a spell hit is multiplied by: all + spell, stopped at the ceiling",
        ["DAMAGE|Damage over time"] = "one pool for every lingering damage: poison, bleed and burning alike",
        ["DAMAGE|More damage"] = "MORE multipliers never add — each is its own multiplier, applied after every increase",
        ["DEFENCE|Max hearts"] = "how much you can take. Regeneration is a share of this, so a bigger bar refills faster",
        ["DEFENCE|Armour"] = "flat armour × (1 + increased). It reduces ATTACKS only, never spells",
        ["DEFENCE|Hit taken (6)"] = "what a 6-damage attack actually costs you after armour — armour is worth less the harder the hit",
        ["DEFENCE|Suppression"] = "chance a SPELL hit lands for a reduced share instead of in full. Armour does not touch spells",
        ["DEFENCE|Regeneration"] = "hearts a second, out of combat and in. It runs at ×2 between waves",
        ["DEFENCE|Fire res"] = "less fire damage taken, and shorter burning",
        ["DEFENCE|Frost res"] = "less frost damage taken, and fewer chill stacks landing",
        ["DEFENCE|Lightning res"] = "less shock damage taken, and less chance a hit on you crits",
        ["DEFENCE|Heart drops"] = "chance a monster leaves a heart. A boss always leaves one",
        ["UTILITY|Move speed"] = "pixels a second. The single most valuable defensive stat in the game",
        ["UTILITY|Roll cooldown"] = "seconds between rolls. You are invulnerable for the whole roll",
        ["UTILITY|Pickup range"] = "how far studs come to you. It pulls STUDS only — every prize is walked to",
        ["UTILITY|Stud gain"] = "studs are the reroll currency, so this is really \"how many second looks do I get\"",
        ["UTILITY|XP gain"] = "levels are +5% increased damage each, so XP gain is damage at one remove",
        ["GUARDIAN BRICK|Damage per hit"] = "one brick touching one body. It is a SPELL, so it reads the spell pool",
        ["GUARDIAN BRICK|Bricks"] = "how many orbit you. They are spread evenly around the ring",
        ["GUARDIAN BRICK|Ring reach"] = "the radius they hold. Wider covers more but spends longer away from any one body",
        ["GUARDIAN BRICK|Brick size"] = "a bigger brick overlaps more of a body, so it connects more reliably",
        ["GUARDIAN BRICK|Spin speed"] = "radians a second. CAST SPEED turns the ring faster, up to its own cap",
        ["STORM BRICK|Targets per bolt"] = "how many bodies one bolt walks through in total",
        ["STORM BRICK|1st leap"] = "each leap carries less than the one before it — this is the second body",
        ["STORM BRICK|2nd leap"] = "the third body, and so on down the chain",
        ["STORM BRICK|3rd leap"] = "the fourth body. The falloff has a floor, so a long chain never reaches zero",
        ["STORM BRICK|Damage down a bolt"] = "every body one bolt touches, added up — the number to compare against a single-target spell",
        ["STORM BRICK|Hovering bricks"] = "each one fires its own full bolt, so this multiplies everything above",
        ["STORM BRICK|Interval"] = "seconds between volleys. CAST SPEED shortens it, down to its own floor",
        ["BLADESTORM|Damage per blade"] = "one blade, one body. Bladestorm is PHYSICAL, so it can open a bleed",
        ["BLADESTORM|Blades per burst"] = "they fly outward in a ring, so the burst is area damage that also single-targets well up close",
        ["BLADESTORM|Burst damage"] = "every blade into one body — the point-blank number",
        ["BLADESTORM|Blade size"] = "a bigger blade connects more reliably at range, where the ring has spread out",
        ["BLADESTORM|Burst DPS"] = "sustained against one body, at your current interval",
        ["BLADE VORTEX|Damage per tick"] = "the vortex hits on a timer, not on contact. PHYSICAL, so it bleeds",
        ["BLADE VORTEX|Duration"] = "how long one vortex lasts once cast",
        ["BLADE VORTEX|Radius"] = "everything inside is hit on every tick, so radius is a straight damage multiplier in a crowd",
        ["BLADE VORTEX|Interval"] = "seconds between casts. CAST SPEED shortens it, down to its own floor",
        ["BLADE VORTEX|DPS to one foe"] = "one body standing in it for the whole duration",
        ["BOMB VOLLEY|Damage per blast"] = "one bomb going off, to everything inside the radius",
        ["BOMB VOLLEY|Bombs"] = "they land on a compass around you, so the volley covers ground rather than stacking",
        ["BOMB VOLLEY|Blast radius"] = "the whole circle takes the full blast — there is no falloff toward the edge",
        ["BOMB VOLLEY|Interval"] = "seconds between volleys. CAST SPEED shortens it, down to its own floor",
        ["BOMB VOLLEY|Volley DPS"] = "every bomb into one body, sustained. In a crowd it is worth far more than this",
        ["BLOCK FREEZE|Radius"] = "everything inside is hit and chilled. Each extra rank widens it",
        ["BLOCK FREEZE|Freeze duration"] = "how long the chill holds. Five chill stacks freeze a body solid",
        ["BLOCK FREEZE|Chill a pulse"] = "how deep one pulse chills. Five stacks freeze a body solid and leave it BRITTLE",
        ["BLOCK FREEZE|Shatter damage"] = "what a corpse that died with one brittle mark throws — your own pulse, out of the body",
        ["BLOCK FREEZE|Shatter at full brittle"] = "the same shatter from a body that died four marks deep",
        ["BLOCK FREEZE|Shatter radius"] = "how far a shattering corpse reaches, from wherever it fell",
        ["SHOCK|Per stack"] = "MORE damage taken from every source, per stack",
        ["SHOCK|SHOCKED mark"] = "the brand a bolt leaves on a body already five stacks deep. It does not spend the stacks",
        ["SHOCK|Brand chance"] = "odds one bolt on a fully shocked body leaves a mark",
        ["SHOCK|At full shock"] = "stacks and marks multiply together, because both are MORE on the body",
        ["CHILL|Slower per stack"] = "a chilled body crawls. The fifth stack freezes it solid",
        ["CHILL|BRITTLE mark"] = "frost resistance taken off whatever the fifth stack froze. It lands on bosses too",
        ["BURNING|Brand chance"] = "odds a fire on a body five stacks deep brands it BURNT",
        ["BURNING|Brand at full depth"] = "the same roll on a body burning at your full depth",
        ["BLOCK FREEZE|Flat frost"] = "Killing Frost, added to the base before every increase",
        ["BRICKBANE|Opening hit"] = "the cloud hits once as it passes, and THEN poisons. This is the hit",
        ["BRICKBANE|Doses per touch"] = "how many poison stacks one pass lays. More doses is more damage over time, not a bigger hit",
        ["BRICKBANE|Poison per second"] = "one dose, per second. It reads the increased DAMAGE OVER TIME pool",
        ["BRICKBANE|Poison duration"] = "how long one dose runs before it wears off",
        ["BRICKBANE|Damage per dose"] = "one dose over its whole life",
        ["BRICKBANE|Stack ceiling"] = "the most doses one body can carry. Spore Burst doubles it, then doubles it again",
        ["BRICKBANE|At full depth"] = "a fully poisoned body, every dose ticking at once — the number bosses die to",
        ["BRICKBANE|Burst reach"] = "Spore Burst: when a poisoned body dies its stacks TRANSFER to everything in here",
        ["BRICKBANE|Spread"] = "the wedge the gas comes out in. At 360° it comes off you in every direction",
        ["BRICKBANE|Reach"] = "how far the cloud travels before it thins out",
        ["BRICKBANE|Interval"] = "seconds between clouds. CAST SPEED shortens it, down to its own floor",
    };
    public static string statWhy(StatProbe s) => s == null ? "" : (s.why ?? STAT_WHY.GetValueOrDefault(s.g + "|" + s.k) ?? "");
    public static string statWhy(string g, string k)
    {
        /* looked up BY NAME, which has to reach a probe's own inline `why:` too, or
           the stats the fireball carries on the probe itself come back blank */
        var hit = STAT_WHY.GetValueOrDefault(g + "|" + k);
        if (Js.T(hit)) return hit;
        var pr = STAT_PROBES?.Find(x => x.g == g && x.k == k);
        return pr?.why ?? "";
    }
    /* `hi:false` marks a stat where SMALLER is better — cooldowns, mostly. */
    /* THE SHORT LIST. A new player opening the menu or pressing C was handed 168
       numbers, most of them the inside of a mechanic they have not met yet. A
       probe with `basic` is one a player needs to read to play -- how hard, how
       often, how tough, how fast -- and its value is the heading it sits under in
       the SIMPLE views. It is a field on the probe rather than a second list, so
       the simple view and the full one can never disagree about a number: they
       are the same row, and one of them just shows fewer. */
    public static readonly string[] STAT_BASIC_ORDER = { "OFFENCE", "DEFENCE", "RESISTANCE", "ATTRIBUTES", "MOVEMENT" };
    public static List<StatProbe> STAT_PROBES;
    /* run from boot(): its explanations read tables from every other file */
    public static void StatProbesInit()
    {
        STAT_PROBES =
        [
      /* THE FIREBALL IS NOT "ELEMENTAL". Elemental is the GLOBAL tree — the pools
         every elemental source in the game reads. The staff's own attack is one
         ability like any other, and filing it under the global tree made a card
         that widened the burst look like a card that changed the whole build. */
      new StatProbe { g = "FIREBALL", k = "Bolt damage", basic = "OFFENCE",        get = ()=> isCaster() ? boltDamage() : 0, fmt = v=>fmtHp(v), zero = true,
        why = "what one fireball lands for on impact, before the fire it leaves behind" },
      new StatProbe { g = "FIREBALL", k = "Bolt base + flat",   get = ()=> isCaster() ? boltBase() : 0,   fmt = v=>v.toFixed(2), zero = true,
        why = "the weapon base plus every FLAT elemental brick — the number every increase then multiplies" },
      new StatProbe { g = "FIREBALL", k = "Burst radius",       get = ()=> isCaster() ? boltRadius() : 0, fmt = v=>Math.round(v)+" px", zero = true,
        why = "everything inside this takes the hit and catches fire, so it is area AND ignition" },
      new StatProbe { g = "FIREBALL", k = "Casts a second", basic = "OFFENCE",     get = ()=> isCaster() ? 1.0/player.atkRate : 0, fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "your own hands — ATTACK SPEED, not the CAST SPEED that shortens your abilities" },
      new StatProbe { g = "FIREBALL", k = "Bolt DPS", basic = "OFFENCE",           get = ()=> isCaster() ? boltDamage()/player.atkRate : 0, fmt = v=>v.toFixed(1), zero = true,
        why = "impact damage only, one target, before burning and before crits" },
      new StatProbe { g = "FIREBALL", k = "Embers per trigger", get = ()=> EMBER_SHOTS[player != null ? player.ember : 0], fmt = v=>Math.round(v)+"", zero = true,
        why = "hitting an ALREADY BURNING body throws this many embers, each seeking a body that is not yet alight" },
      /* BURNING is the ailment the fireball causes, and it is damage over time, so
         it reads the Rotbrick pool — which is exactly what no probe said before. */
      new StatProbe { g = "BURNING", k = "Per stack",      get = ()=> isCaster() ? dotRate(boltDamage()*EBURN_SHARE, 0) : 0,
        fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "one stack of fire, per second. "+Math.round(EBURN_SHARE*100)+"% of the hit that lit it, then the increased DAMAGE OVER TIME pool" },
      new StatProbe { g = "BURNING", k = "Stack cap",      get = ()=> isCaster() ? burnCap() : 0, fmt = v=>Math.round(v)+"", zero = true,
        why = "stacks all burn at once. Reaching the cap breaks them: the fires go out and the body is BURNT" },
      new StatProbe { g = "BURNING", k = "At the cap",     get = ()=> isCaster() ? dotRate(boltDamage()*EBURN_SHARE, 0)*burnCap() : 0,
        fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "every stack burning together — the most fire one body can be carrying" },
      new StatProbe { g = "BURNING", k = "Total per stack",get = ()=> isCaster() ? dotRate(boltDamage()*EBURN_SHARE, 0)*EBURN_DUR : 0,
        fmt = v=>v.toFixed(1), zero = true,
        why = "what one stack deals over its whole "+EBURN_DUR+"s life, if nothing refreshes it" },
      new StatProbe { g = "BURNING", k = "BURNT mark",     get = ()=> isCaster() ? (1+BURNT_STEP)*100 : 0, fmt = v=>Math.round(v)+"%", zero = true,
        why = "fire taken per mark, stacking to "+BURNT_MAX+" marks (×"+Math.pow(1+BURNT_STEP,BURNT_MAX).toFixed(2)+" at full)" },
      new StatProbe { g = "BURNING", k = "Brand chance",   get = ()=> isCaster() ? burntChance(EBURN_BRAND_AT)*100 : 0,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "odds a fire landing on a body already "+EBURN_BRAND_AT+" stacks deep brands it BURNT. "+
            "+"+Math.round(BURNT_CHANCE_PER*100)+"% a stack above that, so a deeper fire brands sooner — "+
            "and branding costs "+EBURN_BRAND_AT+" stacks, never the whole fire" },
      new StatProbe { g = "BURNING", k = "Brand at full depth", get = ()=> isCaster() ? burntChance(burnCap())*100 : 0,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "the same roll on a body burning at your full depth of "+burnCap()+" stacks" },
      new StatProbe { g = "SHOCK", k = "Per stack",        get = ()=> ESHOCK_STEP*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "MORE damage taken from EVERY source, per stack, stacking "+ESHOCK_MAX+" deep" },
      new StatProbe { g = "SHOCK", k = "SHOCKED mark",     get = ()=> SHOCKED_STEP*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "a bolt landing on a body already "+ESHOCK_BRAND_AT+" stacks deep can BRAND it: MORE of "+
            "everything per mark, "+SHOCKED_MAX+" deep (×"+Math.pow(1+SHOCKED_STEP,SHOCKED_MAX).toFixed(2)+
            " at full). Unlike a fire, it does NOT spend the stacks" },
      new StatProbe { g = "SHOCK", k = "Brand chance",     get = ()=> shockedChance(ESHOCK_BRAND_AT)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "odds one bolt on a fully shocked body leaves a SHOCKED mark" },
      new StatProbe { g = "SHOCK", k = "At full shock",    get = ()=> (1+ESHOCK_STEP*ESHOCK_MAX)*Math.pow(1+SHOCKED_STEP,SHOCKED_MAX)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "stacks and marks are separate MORE multipliers on the body, so they multiply together" },
      new StatProbe { g = "CHILL", k = "Slower per stack", get = ()=> ECHILL_STEP*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "a chilled body crawls, "+ECHILL_MAX+" stacks deep, and the fifth freezes it SOLID" },
      new StatProbe { g = "CHILL", k = "BRITTLE mark",     get = ()=> FROZEN_RES_STEP*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "frost resistance taken OFF whatever the fifth stack froze, "+FROZEN_MAX+" marks deep. "+
            "It lands on a BOSS too — a boss cannot be held still, but it can be made brittle" },
      new StatProbe { g = "ELEMENTAL", k = "Increased elemental",get = ()=> incElemAll(weapon.elem??"fire")*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "the GLOBAL elemental pool: fire, frost and lightning alike, from whatever source" },
      new StatProbe { g = "ELEMENTAL", k = "Increased fire",     get = ()=> (player != null ? player.inc["fire"] : 0)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "fire only — it adds into the same pool as the elemental one, it does not multiply it" },
      new StatProbe { g = "ELEMENTAL", k = "Increased frost",    get = ()=> (player != null ? player.inc["frost"] : 0)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "frost only — Block Freeze and every chill it lays. Adds into the elemental pool, never multiplies it" },
      new StatProbe { g = "ELEMENTAL", k = "Increased lightning",get = ()=> (player != null ? player.inc["shock"] : 0)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "lightning only — every storm bolt and every leap it makes. Adds into the elemental pool, never multiplies it" },
      new StatProbe { g = "ELEMENTAL", k = "Chaos resistance", basic = "RESISTANCE",   get = ()=> Math.min(RES_CAP, player != null && player.res != null ? player.res["chaos"] : 0)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "chaos is not an element — nothing that says \"every elemental resistance\" covers this one, "+
            "and no curse that sunders them reaches it. Poison is chaos damage over time, so this is what "+
            "stands between you and a dose" },
      new StatProbe { g = "ELEMENTAL", k = "Increased chaos",    get = ()=> (player != null ? player.inc["chaos"] : 0)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "chaos only. Chaos is neither physical nor elemental — the elemental pool does not touch it, "+
            "and no monster in the game resists it" },
      ..PEN_KEYS.Select(el=>(new StatProbe { g = "ELEMENTAL", k = PEN_NAME[el]+" penetration", get = ()=> penOf(el)*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "how much of a body's "+PEN_NAME[el].toLowerCase()+" resistance your hits go straight through -- "+
            "down to nothing and never below it, hits only (not burns, bleeds or poison), and never your minions'. "+
            (el == "chaos" ? "" : "Elemental penetration counts here too. ")+"Cap "+Math.round(PEN_MAX*100)+"%" })),
      /* WHAT A SWING IS MADE OF. Five rows, one per type, reading the same split
         the hit itself uses — so if the sheet and the monster ever disagree, one of
         them is wrong and you can see which. */
      ..CONV_ORDER.Select(k=>(new StatProbe { g = "CONVERSION", k = CONV_NAME[k]+" share",
        get = ()=> (player != null ? convSplit(isCaster() ? "fire" : isArcher() ? "arrow" : "sword").share[k] : 0)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "how much of one ordinary hit arrives as "+CONV_NAME[k].toLowerCase()+
            ". Each part pays its OWN resistance, and only the PHYSICAL part ever meets armour" })),
      new StatProbe { g = "CONVERSION", k = "Conversion multiplier",
        get = ()=> player != null ? convSplit(isCaster() ? "fire" : isArcher() ? "arrow" : "sword").mul : 1,
        fmt = v=>v.toFixed(2)+"×", zero = true,
        why = "what conversion does to the SIZE of a hit, before any monster sees it. Above one means the "+
            "increases on what it became beat the increases on what it was; GAIN AS EXTRA always pushes it up" },
      new StatProbe { g = "PHYSICAL", k = "Weapon hit", basic = "OFFENCE",     get = ()=> (isCaster()||isSlammer()||isArcher()||isSummoner()) ? 0 : physDamage(),             fmt = v=>fmtHp(v), zero = true },
      new StatProbe { g = "PHYSICAL", k = "Spin swing",     get = ()=> (isCaster()||isSlammer()||isArcher()||isSummoner()) ? 0 : physDamage()*weapon.spin, fmt = v=>fmtHp(v), zero = true },
      new StatProbe { g = "PHYSICAL", k = "Sustained DPS", basic = "OFFENCE",  get = ()=> isCaster() ? 0 : swordDps(),               fmt = v=>v.toFixed(1), zero = true,
        why = "one body, kept in reach, crits folded in. For a slammer it is a body standing in the fault and taking its full share of teeth; "+
            "for a bow it is one arrow a shot into one body" },
      new StatProbe { g = "PHYSICAL", k = "Whirl DPS",      get = ()=> (!isCaster() && !isSlammer() && player.blademaster>0) ? physDamage([whirlShare()])/WHIRL_TICK : 0,
        fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "PHYSICAL", k = "Base + flat",    get = ()=> isCaster() ? 0 : (isSlammer() ? slamBase() : physBase()), fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "PHYSICAL", k = "Swing rate", basic = "OFFENCE",     get = ()=> 1.0/player.atkRate,         fmt = v=>v.toFixed(2)+"/s" },
      new StatProbe { g = "PHYSICAL", k = "Reach",          get = ()=> (isCaster()||isSlammer()||isArcher()||isSummoner()) ? 0 : weapon.reach+player.reach, fmt = v=>Math.round(v)+" px", zero = true },
      /* THE LEGION. Zero on every weapon but the scepter */
      new StatProbe { g = "MINIONS", k = "Minion hit", basic = "OFFENCE", get = ()=> isSummoner() ? minionDamage(player.minions.mages) : 0, fmt = v=>fmtHp(v), zero = true,
        why = "one skeleton hit: (scepter base + minion flat) x (1 + the level pool + the minion pool) x the minion more. A mage throws 80% of it" },
      /* THE OVERSEER'S SHARE, where you can see it: the same hit, standing in its aura */
      new StatProbe { g = "MINIONS", k = "...in the Overseer's aura", get = ()=> isSummoner() && player.minions.overseer ? minionDamage(player.minions.mages)*OVERSEER_MORE : 0,
        fmt = v=>fmtHp(v)+" · "+Math.round((OVERSEER_SPD - 1)*100)+"% faster", zero = true,
        why = "a minion inside the Bone Overseer's aura (520px round it) deals 30% MORE damage and attacks 25% faster; a gold ring round its feet shows which are" },
      new StatProbe { g = "MINIONS", k = "Legion", basic = "OFFENCE", get = ()=> isSummoner() ? minionMax() : 0, fmt = v=>Math.round(v)+" skeletons", zero = true,
        why = "how many skeletons may stand at once (cap 15). At a full legion a raising replaces the oldest" },
      new StatProbe { g = "MINIONS", k = "Golems", get = ()=> isSummoner() ? golemMax() : 0, fmt = v=>Math.round(v)+" of "+S(golemCap())+" · "+
          Js.Or(string.Join(", ", golemTypesOwned().Select(t=>GOLEM_TYPES[t].name.Replace(" Golem", ""))), "none"), zero = true,
        why = "their own pool, on top of the legion: one of each kind you have raised, two at most (three with The Foundry). Each grants you something while it stands. One that falls rises again after 10s" },
      new StatProbe { g = "MINIONS", k = "Minion life", get = ()=> isSummoner() ? minionMaxHp() : 0, fmt = v=>Math.round(v)+"", zero = true,
        why = "what a skeleton rises with: ten, and 12% more for every hero level, times the minion life pool" },
      new StatProbe { g = "MINIONS", k = "Minion projectiles", get = ()=> isSummoner() ? minionProj() : 0, fmt = v=>Math.round(v)+" a shot", zero = true,
        why = "what an archer or a mage looses at once: one, +1 a Bone Volley, +4 from Greater Multiple Projectiles, and the tree" },
      new StatProbe { g = "MINIONS", k = "Minion attacks a second", get = ()=> isSummoner() ? 1.0/minionRate(player.minions.mages) : 0, fmt = v=>v.toFixed(2)+"/s", zero = true },
      new StatProbe { g = "MINIONS", k = "Increased minion damage", get = ()=> isSummoner() ? minionInc()*100 : 0, fmt = v=>Math.round(v)+"%", zero = true,
        why = "the minion pool plus the level pool, and three quarters of yours under Mirrored Legion" },
      new StatProbe { g = "MINIONS", k = "Legion DPS", basic = "OFFENCE", get = ()=> minionDps(), fmt = v=>v.toFixed(1), zero = true,
        why = "a full legion on one body, crits folded in" },
      /* THE ARROW. Zero on everything that is not a bow, like the fireball's rows
         on everything that is not a staff. */
      new StatProbe { g = "ARROW", k = "Arrow hit", basic = "OFFENCE", get = ()=> isArcher() ? arrowDamage() : 0, fmt = v=>fmtHp(v), zero = true,
        why = "one arrow, before crits: (base + flat + barbs) × (1 + increased) × each more. Every arrow of a volley is this" },
      new StatProbe { g = "ARROW", k = "Arrows a shot", basic = "OFFENCE", get = ()=> isArcher() ? clamp(Js.I32(player.bow.n), 1, ARROW_MAX_N) : 0,
        fmt = v=>Math.round(v)+"", zero = true,
        why = "how many arrows one draw looses, fanned. They go to different bodies; one body in the line takes one" },
      new StatProbe { g = "ARROW", k = "Pierce", get = ()=> isArcher() ? (player.bow.kind == "chaos" ? ARROW_PIERCE_ALL : player.bow.pierce) : 0,
        fmt = v=>v >= ARROW_PIERCE_ALL ? "every body" : Math.round(v)+"", zero = true,
        why = "bodies an arrow goes THROUGH before it stops. Each one it passes costs it a little of its damage; Chaos Arrow passes them all unworn" },
      new StatProbe { g = "ARROW", k = "Barbs (flat)", get = ()=> isArcher() ? Js.Or(player.bow.flat, 0) : 0, fmt = v=>v.toFixed(2), zero = true,
        why = "flat physical added to arrows only, before any increase multiplies it" },
      new StatProbe { g = "ARROW", k = "Ricochet chance", get = ()=> isArcher() ? player.ricochet*100 : 0, fmt = v=>Math.round(v)+"%", zero = true,
        why = "the Ricochet keystone: an arrow that would stop rolls this to leap to the nearest body it has not hit, up to "+RICO_MAX+" times" },
      new StatProbe { g = "PHYSICAL", k = "Crit chance", basic = "OFFENCE",    get = ()=> critChance()*100,         fmt = v=>v.toFixed(1)+"%" },
      /* ONE ROW PER AILMENT, and the row is the chance the hit actually rolls. These
         read ailChanceOf, so nothing here can drift away from what applyAilments does
         — which is how shock spent a build being the storm brick's private stat. */
      ..CONV_ORDER.Select(k=>(new StatProbe { g = "AILMENT", k = AIL_NAME[k]+" on a hit",
        get = ()=> player != null ? ailChanceOf(k, null, false)*100 : 0,
        fmt = v=>v.toFixed(0)+"%", zero = true,
        why = "a hit that arrived ENTIRELY as "+CONV_NAME[k].toLowerCase()+" leaves a "+
            AIL_NAME[k]+" this often — half the hit, half the chance"+
            (k=="phys" ? ". A wound is an ATTACK ailment: a physical SPELL never opens one"
                        : "") })),
      /* HOW BIG THE HIT HAS TO BE. Two of the five are counters, and a counter that
         ignored the size of the blow was the thing that made a one-damage tick worth
         as much as a slam. These read the real function, so the sheet cannot drift
         from the rule. */
      new StatProbe { g = "AILMENT", k = "CHILL/SHOCK floor", get = ()=> AIL_MAG_MIN*100.0/(1+(player != null ? player.ailMagInc : 0)),
        fmt = v=>v.toFixed(1)+"%", zero = true,
        why = "a hit smaller than this share of the body\u2019s pool leaves no chill or shock at all. "+
            "A boss\u2019s pool is "+Math.round(AIL_POOL_BOSS*100)+"% of its life, so late hits still count as big ones" },
      new StatProbe { g = "AILMENT", k = "CHILL/SHOCK full at", get = ()=> AIL_MAG_FULL*100.0/(1+(player != null ? player.ailMagInc : 0)),
        fmt = v=>v.toFixed(1)+"%", zero = true,
        why = "a hit this big lays all "+AIL_STACK_HIT+" stacks at once. Between the floor and here it scales" },
      new StatProbe { g = "AILMENT", k = "Increased magnitude", get = ()=> (player != null ? player.ailMagInc : 0)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "brings the whole scale closer: the same hit counts for more, so smaller blows still chill and shock" },
      /* ---- THE CONTEST, on the sheet, as a CHANCE and not a rating. Nobody can
         read 840 against 420; everybody can read 87%. ---- */
      new StatProbe { g = "ACCURACY", k = "Accuracy", basic = "OFFENCE", get = ()=> accuracyValue(), fmt = v=>Math.round(v)+"",
        why = "attacks only \u2014 a spell arrives where it was aimed. The weapon is a MORE "+
            "multiplier over the whole pool, and dexterity pays "+ATTR_DEX_ACC+" a point into it" },
      new StatProbe { g = "ACCURACY", k = "vs an ordinary body", get = ()=> hitChance(accuracyValue(), EVA_BASE)*100,
        fmt = v=>v.toFixed(1)+"%",
        why = "how often a swing lands on something of average speed and size. Fast small monsters "+
            "dodge more and big slow ones barely dodge at all" },
      new StatProbe { g = "ACCURACY", k = "vs a hard dodger", get = ()=> hitChance(accuracyValue(), EVA_BASE*3)*100,
        fmt = v=>v.toFixed(1)+"%",
        why = "a shade, or anything under SLIPPERY. This is the number accuracy is bought for" },
      new StatProbe { g = "MANA", k = "Maximum", get = ()=> player != null ? player.maxMana : 0, fmt = v=>Math.round(v)+"", zero = true,
        why = "40, 4 more a level, 1 for every 2 INTELLIGENCE, times increased mana. Nothing spends it yet: it is the pool the active skills will cost" },
      new StatProbe { g = "MANA", k = "Regeneration", get = ()=> player != null ? player.manaRegen : 0, fmt = v=>v.toFixed(1)+"/s", zero = true,
        why = "1.75% of your maximum a second, times increased mana regeneration" },
      new StatProbe { g = "ENERGY SHIELD", k = "Maximum", basic = "DEFENCE", get = ()=> player != null ? player.esMax : 0,
        fmt = v=>fmtHp(v), zero = true,
        why = "a second bar in front of your hearts. It takes the hit first, and CHAOS goes "+
            "straight through it \u2014 a dose is the one thing it cannot stop" },
      new StatProbe { g = "ENERGY SHIELD", k = "Standing", get = ()=> player != null ? player.es : 0,
        fmt = v=>fmtHp(v), zero = true,
        why = "what is left of it right now" },
      new StatProbe { g = "ENERGY SHIELD", k = "Comes back after", get = ()=> ES_DELAY, fmt = v=>v.toFixed(1)+"s",
        why = "seconds untouched before it starts refilling. ANY damage restarts the clock, "+
            "including a hit that walked past the shield entirely" },
      new StatProbe { g = "ENERGY SHIELD", k = "Refills at", get = ()=> player != null ? player.esMax*ES_RATE : 0,
        fmt = v=>fmtHp(v)+"/s", zero = true,
        why = Math.round(ES_RATE*100)+"% of the maximum a second \u2014 so a bigger shield comes "+
            "back faster as well as lasting longer" },
      new StatProbe { g = "ENERGY SHIELD", k = "From intelligence", get = ()=> intOf()*ATTR_INT_ES*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "+"+(ATTR_INT_ES*100)+"% increased maximum energy shield per point of INT" },
      new StatProbe { g = "EVASION", k = "Evasion", basic = "DEFENCE", get = ()=> evasionValue(), fmt = v=>Math.round(v)+"", zero = true,
        why = "you start with NONE \u2014 armour is the defence you begin with, evasion is the one "+
            "you choose. It does nothing against a spell" },
      new StatProbe { g = "EVASION", k = "An ordinary swing lands", get = ()=> hitChance(ACC_MOB_BASE, evasionValue())*100,
        fmt = v=>v.toFixed(1)+"%",
        why = "how often a monster\u2019s ATTACK finds you. A boss is far more accurate than this" },
      new StatProbe { g = "EXPOSURE", k = "Chance", get = ()=> (player != null ? player.exposeChance : 0)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "per hit, per ELEMENT it arrived as \u2014 half a hit of fire exposes fire at half of this" },
      new StatProbe { g = "EXPOSURE", k = "Resistance stripped", get = ()=> EXPOSE_STEP*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "taken OFF the body for "+EXPOSE_T+"s. It is not in your damage pool, so your increased-damage "+
            "ceiling does not stop it \u2014 and it can drag a resistance below zero, which is a weakness" },
      /* ---- THE CHARGES. One row for what you hold and one for what it is worth,
         because a charge you cannot see the value of is a chevron. ---- */
      ..CHARGE_IDS.Select(id=>(new StatProbe { g = "CHARGES", k = HERO_STACKS[id].name+" held",
        get = ()=> player != null ? stackOf(id) : 0, fmt = v=>Math.round(v)+" / "+S(player != null ? stackCap(id) : 0), zero = true,
        why = (id=="frenzy" ? "one for every monster you kill, if you have bought a reason. "+
                Math.round(HERO_STACKS["frenzy"].mods["atk"]*100)+"% attack and cast speed and "+
                Math.round(HERO_STACKS["frenzy"].mods["moreAll"]*100)+"% MORE damage each"
           : id=="power" ? "one for every critical, if you have bought a reason. "+
                Math.round(HERO_STACKS["power"].mods["crit"]*100)+"% INCREASED critical chance each, into the one pool"
           : "one for every hit you take, if you have bought a reason. "+
                Math.round(HERO_STACKS["endurance"].mods["less"]*100)+"% LESS damage taken and +"+
                Math.round(HERO_STACKS["endurance"].mods["res"]*100)+"% to every elemental resistance each")+
           ", falling off one every "+HERO_STACKS[id].decay+"s" })),
      new StatProbe { g = "CHARGES", k = "Gained by", get = ()=> player != null ?
          ((player.chargeKill>0?1:0)+(player.chargeCrit>0?1:0)+(player.chargeHurt>0?1:0)) : 0,
        fmt = v=>Math.round(v)+" of 3", zero = true,
        why = "killing, critting and being hit are three different charges. Nothing generates one until "+
            "a brick or a node hands you the reason" },
      ..CONV_ORDER.Select(k=>(new StatProbe { g = "AILMENT", k = AIL_NAME[k]+" on a crit",
        get = ()=> player != null ? ailChanceOf(k, null, true)*100 : 0,
        fmt = v=>v.toFixed(0)+"%", zero = true,
        why = (AIL_CRIT[k] >= 1
              ? "a critical ALWAYS leaves this one — every point of "+CONV_NAME[k].toLowerCase()+
                " in the hit "+(k=="fire"?"ignites":k=="frost"?"chills":"shocks")
              : "a critical buys you nothing here: "+AIL_NAME[k]+" is bought with "+
                (k=="phys" ? "`Serrated Edge`" : "`Venom Glands`")+" or the tree, never with crit") })),
      new StatProbe { g = "BLEED", k = "Share of the hit",get = ()=> player.bleedShare*100,    fmt = v=>v.toFixed(0)+"%/s",
        why = "one wound bleeds this share of the hit that opened it, per second — before the damage-over-time pool" },
      /* THE NUMBERS THE GAME ACTUALLY DEALS. Every row above is a SHARE, so none of
         them moved when an increased-damage-over-time brick was hovered — which is
         how Rotbrick came to show an empty panel on a bleeding build. These read
         dotRate() against a real weapon hit, so they move with everything: flat
         physical, increased physical, the more pool AND the Rotbrick pool. */
      new StatProbe { g = "BLEED", k = "One wound",       get = ()=> isCaster() ? 0 : dotRate(physDamage()*player.bleedShare, 0),
        fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "what one open wound costs a body per second, on a real hit from your weapon" },
      new StatProbe { g = "BLEED", k = "Damage per wound",get = ()=> isCaster() ? 0 : dotRate(physDamage()*player.bleedShare, 0)*BLEED_T,
        fmt = v=>v.toFixed(1), zero = true,
        why = "one wound over its whole "+BLEED_T+"s, if it runs to the end" },
      new StatProbe { g = "BLEED", k = "Fully opened",    get = ()=> isCaster() ? 0 : dotRate(physDamage()*player.bleedShare, 0)*BLEED_STACK_MAX,
        fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "all "+BLEED_STACK_MAX+" wounds bleeding at once. This is why staying on a boss beats hitting it harder once" },
      new StatProbe { g = "BLEED", k = "Wounds at once",  get = ()=> BLEED_STACK_MAX, fmt = v=>Math.round(v)+"",
        why = "past this a new cut displaces the SHALLOWEST wound, and only if it is deeper than that one" },
      new StatProbe { g = "SWORD", k = "Crit damage", basic = "OFFENCE",     get = ()=> critMultPct(),            fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DAMAGE", k = "Increased melee",get = ()=> player.incMelee*100,      fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DAMAGE", k = "Increased spell",get = ()=> player.incSpell*100,      fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DAMAGE", k = "Increased all",  get = ()=> player.incAll*100,        fmt = v=>Math.round(v)+"%" },
      /* the two DERIVED numbers, the ones a hit is actually multiplied by */
      new StatProbe { g = "DAMAGE", k = "On a weapon hit",get = ()=> incTotal("melee")*100,    fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DAMAGE", k = "On a spell hit", get = ()=> incTotal("spell")*100,    fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DAMAGE", k = "Damage over time", get = ()=> dotInc(),               fmt = v=>Math.round(v*100)+"%" },
      new StatProbe { g = "DAMAGE", k = "More damage",    get = ()=> moreShown(),              fmt = v=>"×"+v.toFixed(2) },
      new StatProbe { g = "GROUND SLAM", k = "Damage a tooth", basic = "OFFENCE", get = ()=> isSlammer() ? slamSpikeDamage() : 0, fmt = v=>v.toFixed(2), zero = true,
        why = "one stone tooth. A body in the fan is bitten by up to "+SLAM_MAX_HITS+" of them, so a slam landed dead centre is worth several" },
      new StatProbe { g = "GROUND SLAM", k = "Base + flat",    get = ()=> isSlammer() ? slamBase() : 0, fmt = v=>v.toFixed(2), zero = true,
        why = "the weapon plus every FLAT physical brick and every Fault Hammer — the number the increases then multiply" },
      new StatProbe { g = "GROUND SLAM", k = "Fault width",    get = ()=> isSlammer() ? player.slamArc*57.2958 : 0, fmt = v=>Math.round(v)+"\u00b0", zero = true,
        why = "how wide the fan opens. 180\u00b0 is the ceiling until SLAM OF THE ELEMENTS is taken twice, which opens it to a full ring" },
      new StatProbe { g = "GROUND SLAM", k = "Fault reach",    get = ()=> isSlammer() ? player.slamRange : 0, fmt = v=>Math.round(v)+" px", zero = true,
        why = "how far in front of you the fault runs before it closes. Deepening it adds ROWS of teeth, "+
            "so reach is damage as much as it is range — it stops at "+SLAM_RANGE_MAX+"px" },
      new StatProbe { g = "GROUND SLAM", k = "Fault speed",    get = ()=> isSlammer() ? player.slamSpeed : 0, fmt = v=>Math.round(v)+" px/s", zero = true,
        why = "how fast the fault runs outward. Slow, and the far teeth arrive after the pack has walked off them" },
      new StatProbe { g = "GROUND SLAM", k = "Teeth a slam",   get = ()=> isSlammer() ? slamRays(player.slamArc)*Math.max(2, Math.round(player.slamRange/SLAM_STEP)) : 0,
        fmt = v=>Math.round(v)+"", zero = true,
        why = "lines of stone times rows. Widening the fault adds lines, deepening it adds rows" },
      new StatProbe { g = "GROUND SLAM", k = "Slams a second", basic = "OFFENCE", get = ()=> isSlammer() ? 1.0/player.atkRate : 0, fmt = v=>v.toFixed(2)+"/s", zero = true,
        why = "how often the fault opens. It is the ATTACK SPEED pool, so every brick that speeds a "+
            "swing speeds the slam — and it meets the same floor a swing does" },
      new StatProbe { g = "GROUND SLAM", k = "Element cycle",  get = ()=> (player.slam != null ? player.slam.elem : 0), fmt = v=>v>=2?"ring + cycle":(v != 0 ? "cycling" : "\u2014"), zero = true,
        why = "SLAM OF THE ELEMENTS: each slam converts half of every tooth into fire, then lightning, then frost, and brings that ailment with it" },
      new StatProbe { g = "ATTRIBUTES", k = "Strength", basic = "ATTRIBUTES",     get = ()=> strOf(), fmt = v=>Math.round(v)+"", zero = true,
        why = "the weapon\u2019s own plus every brick and node that handed some over. "+
            "Each point is +"+(ATTR_STR_DMG*100)+"% increased PHYSICAL damage and "+
            "+"+(ATTR_STR_HP*100)+"% increased MAXIMUM HEARTS" },
      new StatProbe { g = "ATTRIBUTES", k = "Intelligence", basic = "ATTRIBUTES", get = ()=> intOf(), fmt = v=>Math.round(v)+"", zero = true,
        why = "each point is +"+(ATTR_INT_SPELL*100)+"% increased SPELL damage and "+
            "+"+(ATTR_INT_REGEN*100)+"% increased REGENERATION" },
      new StatProbe { g = "ATTRIBUTES", k = "Dexterity", basic = "ATTRIBUTES",    get = ()=> dexOf(), fmt = v=>Math.round(v)+"", zero = true,
        why = "each point is +"+(ATTR_DEX_CRIT*100)+"% increased CRITICAL CHANCE, "+
            "+"+(ATTR_DEX_CMULT*100)+"% increased CRITICAL DAMAGE and "+
            "+"+(ATTR_DEX_ATK*100)+"% increased ATTACK SPEED" },
      new StatProbe { g = "ATTRIBUTES", k = "From strength",     get = ()=> strOf()*ATTR_STR_DMG*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "increased physical damage and increased maximum hearts, both, from STRENGTH alone. "+
            "It is DERIVED every rebuild and never written into the pool, so two sources of "+
            "strength can never double-count" },
      new StatProbe { g = "ATTRIBUTES", k = "From intelligence", get = ()=> intOf()*ATTR_INT_SPELL*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "increased spell damage, and the same again as increased regeneration, from INTELLIGENCE" },
      new StatProbe { g = "ATTRIBUTES", k = "From dexterity",    get = ()=> dexOf()*ATTR_DEX_CRIT*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "increased critical chance from DEXTERITY, and the same again on critical damage and attack speed" },
      new StatProbe { g = "DEFENCE", k = "Max hearts", basic = "DEFENCE",    get = ()=> player.maxHp,             fmt = v=>Math.round(v)+"" },
      new StatProbe { g = "DEFENCE", k = "Armour", basic = "DEFENCE",        get = ()=> armourValue(),            fmt = v=>Math.round(v)+"" },
      new StatProbe { g = "DEFENCE", k = "Hit taken (6)", get = ()=> 6*(1-armourDR(armourValue(),6)), fmt = v=>v.toFixed(2), hi = false },
      new StatProbe { g = "DEFENCE", k = "Suppression",   get = ()=> suppressChance()*100,     fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DEFENCE", k = "Weapon mitigation", get = ()=> (weapon != null ? weapon.lessTaken : 0)*100,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "LESS damage taken from everything, just for carrying a melee weapon — a staff buys its safety with range instead" },
      new StatProbe { g = "DEFENCE", k = "Regeneration", basic = "DEFENCE",  get = ()=> cursedRegen(),            fmt = v=>v.toFixed(3)+"/s" },
      new StatProbe { g = "DEFENCE", k = "Lifesteal chance", get = ()=> player.leechChance*100, fmt = v=>Math.round(v)+"%", zero = true,
        why = "how often one of your own swings drinks. The staff never does — lifesteal is a melee stat" },
      new StatProbe { g = "DEFENCE", k = "Lifesteal share",  get = ()=> player.leech*100, fmt = v=>v.toFixed(1)+"%", zero = true,
        why = "how much of the damage a drinking swing ACTUALLY LANDED comes back as hearts, capped at "+
            Math.round(LEECH_HIT_CAP*100)+"% of your maximum per swing" },
      new StatProbe { g = "DEFENCE", k = "Lifesteal a swing", get = ()=> player.leechChance*player.leech*100,
        fmt = v=>v.toFixed(2)+"%", zero = true,
        why = "chance times share — the average share of every swing that comes back, which is the number to compare builds on" },
      new StatProbe { g = "DEFENCE", k = "Elemental toll", get = ()=> resTax()*100, fmt = v=>"\u2212"+Math.round(v)+"%", zero = true, hi = false,
        why = "the world\u2019s own tax on resistance, 30 points at wave 20, 40 and 60. It is flat and permanent — the only answer is to carry more resistance" },
      new StatProbe { g = "DEFENCE", k = "Fire res", basic = "RESISTANCE",      get = ()=> (1-elemTake("fire"))*100, fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DEFENCE", k = "Frost res", basic = "RESISTANCE",     get = ()=> (1-elemTake("frost"))*100,fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DEFENCE", k = "Lightning res", basic = "RESISTANCE", get = ()=> (1-elemTake("shock"))*100,fmt = v=>Math.round(v)+"%" },
      new StatProbe { g = "DEFENCE", k = "Heart drops",   get = ()=> heartDropChance()*100, fmt = v=>v.toFixed(2)+"%" },
      new StatProbe { g = "UTILITY", k = "Move speed", basic = "MOVEMENT",    get = ()=> player.speed,             fmt = v=>Math.round(v)+" px/s" },
      new StatProbe { g = "UTILITY", k = "Roll cooldown", get = ()=> player.dash.cdMax,        fmt = v=>v.toFixed(2)+"s", hi = false },
      new StatProbe { g = "UTILITY", k = "Pickup range",  get = ()=> player.magnetR,           fmt = v=>Math.round(v)+" px" },
      new StatProbe { g = "UTILITY", k = "Stud gain",     get = ()=> player.studMul,           fmt = v=>"×"+v.toFixed(2) },
      new StatProbe { g = "UTILITY", k = "XP gain",       get = ()=> player.xpMul,             fmt = v=>"×"+v.toFixed(2) },
      /* ---- the spells. A spell you do not own reads as a dash, so taking one for
         the first time shows up as "— -> 2.40" rather than a silent zero. ---- */
      new StatProbe { g = "GUARDIAN BRICK", k = "Damage per hit", get = ()=> player.orbit.n != 0 ? calcDamage(player.orbit.dmg,"spell") : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "GUARDIAN BRICK", k = "Bricks",         get = ()=> player.orbit.n, fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "GUARDIAN BRICK", k = "Ring reach",     get = ()=> player.orbit.n != 0 ? player.orbit.r : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "GUARDIAN BRICK", k = "Brick size",     get = ()=> player.orbit.n != 0 ? player.orbit.size : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "GUARDIAN BRICK", k = "Spin speed",     get = ()=> player.orbit.n != 0 ? player.orbit.spd : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "GUARDIAN BRICK", k = "DPS to one foe",  get = ()=> orbitDps(), fmt = v=>v.toFixed(1), zero = true,
        why = "one body standing in the ring, struck as often as the ring allows" },
      new StatProbe { g = "GUARDIAN BRICK", k = "Shots it can stop", get = ()=> (player.orbit.n != 0 && player.orbit.aegis != 0)
          ? player.orbit.n * (player.orbit.twin != 0 ? 2 : 1) : 0, fmt = v=>Math.round(v)+"", zero = true,
        why = "Aegis Ring: every brick in the ring stops one hostile projectile, then rebuilds" },
      new StatProbe { g = "GUARDIAN BRICK", k = "Rebuilds in",    get = ()=> (player.orbit.n != 0 && player.orbit.aegis != 0) ? AEGIS_REFORM : 0,
        fmt = v=>v.toFixed(1)+"s", zero = true,
        why = "a brick that stopped a shot neither blocks nor strikes until it is whole again" },
      new StatProbe { g = "GUARDIAN BRICK", k = "Twin ring at",   get = ()=> (player.orbit.n != 0 && player.orbit.twin != 0) ? twinRadius(player.orbit) : 0,
        fmt = v=>Math.round(v)+" px", zero = true,
        why = "Twin Ring: a second ring, turning the other way, far enough out never to touch the first" },
      new StatProbe { g = "STORM BRICK", k = "Opening strike",    get = ()=> player.chain.n != 0 ? shockDamage() : 0, fmt = v=>v.toFixed(2), zero = true,
        why = "LIGHTNING damage, so it reads the flat elemental pool and both the spell and elemental increases — not just spell damage" },
      new StatProbe { g = "STORM BRICK", k = "Targets per bolt",  get = ()=> player.chain.n, fmt = v=>Math.round(v)+"", zero = true },
      /* the leaps written out one by one, so a chain brick shows you what the second
         and third foe in the line are actually going to take */
      new StatProbe { g = "STORM BRICK", k = "1st leap",          get = ()=> chainLeapDmg(1), fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "STORM BRICK", k = "2nd leap",          get = ()=> chainLeapDmg(2), fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "STORM BRICK", k = "3rd leap",          get = ()=> chainLeapDmg(3), fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "STORM BRICK", k = "Damage down a bolt",get = ()=> chainBoltTotal(), fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "STORM BRICK", k = "Hovering bricks",   get = ()=> Math.min(chainMaxZappers(), player.chain.zappers), fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "STORM BRICK", k = "Interval",          get = ()=> player.chain.n != 0 ? player.chain.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true },
      new StatProbe { g = "STORM BRICK", k = "Shock chance",      get = ()=> player.chain.n != 0 ? chainShockChance()*100 : 0,
        fmt = v=>Math.round(v)+"%", zero = true,
        why = "how often a bolt SHOCKS what it touches. A shocked body takes more damage from everything you own, not just from lightning" },
      new StatProbe { g = "STORM BRICK", k = "Shocked takes",     get = ()=> (player.chain.n != 0 && chainShockChance() > 0)
          ? (1 + ESHOCK_STEP*ESHOCK_MAX)*100 : 0, fmt = v=>Math.round(v)+"%", zero = true,
        why = "a fully shocked body, all "+ESHOCK_MAX+" stacks — this multiplies every source you have, which is the point of the storm brick" },
      new StatProbe { g = "STORM BRICK", k = "DPS, all bricks",   get = ()=> player.chain.n != 0 ? chainBoltTotal()*Math.min(chainMaxZappers(), player.chain.zappers)/player.chain.rate : 0, fmt = v=>v.toFixed(1), zero = true,
        why = "every hovering brick firing a full bolt down a packed line" },
      /* Crackling Storm turns one leap into a branching tree — a number, and a very
         large one, that no probe was reading */
      new StatProbe { g = "STORM BRICK", k = "Ways per leap",     get = ()=> player.chain.n != 0 ? 1 + player.chain.fork + (player.chain.fork != 0 ? 1 : 0) : 0,
        fmt = v=>Math.round(v)+"", zero = true,
        why = "Crackling Storm: each leap FORKS this many ways instead of one, so a bolt walks the pack as a tree" },
      new StatProbe { g = "STORM BRICK", k = "Bodies a bolt reaches", get = ()=>{
          if(player.chain.n == 0) return 0;
          double ways = 1 + player.chain.fork + (player.chain.fork != 0 ? 1 : 0), leaps = player.chain.n - 1;
          double n = 1, tier = 1;
          for(var i=0;i<leaps;i++){ tier *= ways; n += tier; }
          return Math.min(CHAIN_MAX_NODES, n); },
        fmt = v=>Math.round(v)+"", zero = true,
        why = "what one bolt actually touches once it has finished branching (hard cap "+CHAIN_MAX_NODES+" nodes)" },
      new StatProbe { g = "BLADESTORM", k = "Damage per blade",   get = ()=> player.storm.n != 0 ? calcDamage(player.storm.dmg,"melee") : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "BLADESTORM", k = "Blades per burst",   get = ()=> player.storm.n*player.storm.count, fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "BLADESTORM", k = "Burst damage",       get = ()=> player.storm.n != 0 ? calcDamage(player.storm.dmg,"melee")*player.storm.n*player.storm.count : 0, fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "BLADESTORM", k = "Blade size",         get = ()=> player.storm.n != 0 ? player.storm.size : 0, fmt = v=>"×"+v.toFixed(2), zero = true },
      new StatProbe { g = "BLADESTORM", k = "Interval",           get = ()=> player.storm.n != 0 ? player.storm.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true,
        why = "seconds between bursts. Every CAST SPEED increase shortens it, down to its own floor" },
      /* Returning Blades multiplies how many times one blade can cut. That is the
         whole brick, and no probe said it. */
      new StatProbe { g = "BLADESTORM", k = "Passes per blade",   get = ()=> player.storm.n != 0 ? 1 + player.storm.boomer*(player.storm.boomer>1?1.5:1) : 0,
        fmt = v=>v.toFixed(1), zero = true,
        why = "Returning Blades: a blade wheels around and cuts its way back instead of fading" },
      new StatProbe { g = "BLADESTORM", k = "Burst, every pass",  get = ()=> player.storm.n != 0
          ? calcDamage(player.storm.dmg,"melee")*player.storm.n*player.storm.count
            * (1 + player.storm.boomer*(player.storm.boomer>1?1.5:1)) : 0,
        fmt = v=>v.toFixed(1), zero = true,
        why = "the whole burst counting every return pass — what a burst is worth against something that cannot dodge" },
      new StatProbe { g = "BLADESTORM", k = "Burst DPS",          get = ()=> player.storm.n != 0 ? calcDamage(player.storm.dmg,"melee")*player.storm.n*player.storm.count/player.storm.rate : 0, fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "Damage per tick",  get = ()=> player.vortex.n != 0 ? calcDamage(player.vortex.dmg,"melee") : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "Duration",         get = ()=> player.vortex.n != 0 ? player.vortex.dur + (player.vortex.n-1)*0.4 : 0, fmt = v=>v.toFixed(1)+"s", zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "Radius",           get = ()=> player.vortex.n != 0 ? player.vortex.r + (player.vortex.n-1)*10 : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "Interval",         get = ()=> player.vortex.n != 0 ? player.vortex.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "Radius",           get = ()=> player.vortex.n != 0 ? player.vortex.r : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BLADE VORTEX", k = "DPS to one foe",   get = ()=> vortexDps(), fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "BOMB VOLLEY", k = "Damage per blast",  get = ()=> player.bomb.n != 0 ? calcDamage(player.bomb.dmg,"spell") : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "BOMB VOLLEY", k = "Bombs",             get = ()=> player.bomb.n, fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "BOMB VOLLEY", k = "Blast radius",      get = ()=> player.bomb.n != 0 ? player.bomb.r : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BOMB VOLLEY", k = "Interval",          get = ()=> player.bomb.n != 0 ? player.bomb.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true },
      new StatProbe { g = "BOMB VOLLEY", k = "Volley DPS",        get = ()=> player.bomb.n != 0 ? calcDamage(player.bomb.dmg,"spell")*player.bomb.n/player.bomb.rate : 0, fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "BLOCK FREEZE", k = "Radius",           get = ()=> player.freeze.n != 0 ? player.freeze.r : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BLOCK FREEZE", k = "Freeze duration",  get = ()=> player.freeze.n != 0 ? player.freeze.dur : 0, fmt = v=>v.toFixed(1)+"s", zero = true },
      new StatProbe { g = "BLOCK FREEZE", k = "Interval",         get = ()=> player.freeze.n != 0 ? player.freeze.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true,
        why = "seconds between blasts. CAST SPEED shortens it, down to its own floor" },
      new StatProbe { g = "BLOCK FREEZE", k = "Frost damage",     get = ()=> player.freeze.n != 0 ? frostDamage() : 0, fmt = v=>v.toFixed(2), zero = true,
        why = "it is a real elemental hit, not just a slow — it reads the spell AND the frost pools" },
      new StatProbe { g = "BLOCK FREEZE", k = "Chill a pulse",    get = ()=> player.freeze.n != 0 ? freezeChill() : 0,
        fmt = v=>Math.round(v)+" stack"+(v==1?"":"s"), zero = true,
        why = "how deep one pulse chills. Five stacks freeze a body solid and leave it BRITTLE" },
      new StatProbe { g = "BLOCK FREEZE", k = "Flat frost",       get = ()=> player.freeze.n != 0 ? player.freeze.flat : 0,
        fmt = v=>v.toFixed(2), zero = true,
        why = "Killing Frost, added to the base before every increase — the same shape as flat physical" },
      new StatProbe { g = "BLOCK FREEZE", k = "Shatter damage",   get = ()=> (player.freeze.n != 0 && player.freeze.stun != 0) ? zeroShatterDmg(1) : 0,
        fmt = v=>fmtHp(v), zero = true,
        why = "what a corpse that died with ONE brittle mark throws. It is your own pulse: "+
            "the same frost damage and the same chill, out of the body" },
      new StatProbe { g = "BLOCK FREEZE", k = "Shatter at full brittle", get = ()=> (player.freeze.n != 0 && player.freeze.stun != 0) ? zeroShatterDmg(FROZEN_MAX) : 0,
        fmt = v=>fmtHp(v), zero = true,
        why = "+"+Math.round(ZERO_BRITTLE_MUL*100)+"% a mark, "+FROZEN_MAX+" deep — the debuff frost "+
            "lays is what makes its kills worth watching" },
      new StatProbe { g = "BLOCK FREEZE", k = "Shatter radius",   get = ()=> (player.freeze.n != 0 && player.freeze.stun != 0) ? zeroShatterR() : 0,
        fmt = v=>Math.round(v)+" px", zero = true,
        why = Math.round(ZERO_BURST_R_MUL*100)+"% of your own pulse, thrown from wherever the body fell" },
      new StatProbe { g = "BLOCK FREEZE", k = "Held solid for",   get = ()=> (player.freeze.n != 0 && player.freeze.stun != 0) ? FREEZE_STUN_T : 0,
        fmt = v=>v.toFixed(1)+"s", zero = true,
        why = "Absolute Zero: every caught body is frozen SOLID this long, the way a fifth chill stack freezes you" },
      new StatProbe { g = "BRICKBANE", k = "Opening hit",      get = ()=> player.bane.n != 0 ? calcDamage(player.bane.dmg,"spell") : 0, fmt = v=>v.toFixed(2), zero = true },
      new StatProbe { g = "BRICKBANE", k = "Doses per touch",  get = ()=> player.bane.n != 0 ? player.bane.stacks : 0, fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Poison per second",get = ()=> player.bane.n != 0 ? player.bane.dot : 0, fmt = v=>v.toFixed(2)+"/s", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Poison duration",  get = ()=> player.bane.n != 0 ? player.bane.dotT : 0, fmt = v=>v.toFixed(1)+"s", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Damage per dose",  get = ()=> player.bane.n != 0 ? player.bane.dot*player.bane.dotT : 0, fmt = v=>v.toFixed(1), zero = true },
      new StatProbe { g = "BRICKBANE", k = "Stack ceiling",    get = ()=> player.bane.n != 0 ? POISON_MAX*POISON_CAP_MUL[player.bane.burst] : 0, fmt = v=>Math.round(v)+"", zero = true },
      new StatProbe { g = "BRICKBANE", k = "At full depth",    get = ()=> player.bane.n != 0 ? player.bane.dot*POISON_MAX*POISON_CAP_MUL[player.bane.burst] : 0, fmt = v=>v.toFixed(2)+"/s", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Burst reach",      get = ()=> (player.bane.n != 0 && player.bane.burst != 0)
          ? BANE_BURST_R[player.bane.burst] : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Spread",           get = ()=> player.bane.n != 0 ? Math.min(BANE_ARC_MAX,player.bane.arc)*180.0/Math.PI : 0, fmt = v=>Math.round(v)+"°", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Reach",            get = ()=> player.bane.n != 0 ? player.bane.r : 0, fmt = v=>Math.round(v)+" px", zero = true },
      new StatProbe { g = "BRICKBANE", k = "Interval",         get = ()=> player.bane.n != 0 ? player.bane.rate : 0, fmt = v=>v.toFixed(2)+"s", hi = false, zero = true }

        ];
    }
    /* what the Nth leap of a bolt lands for, in the chain you would have */
    public static double chainLeapDmg(double i)
    {
        var p = player;
        double leaps = p.chain.n - 1;
        if (p.chain.n == 0 || leaps < i) return 0;
        return shockDamage() * chainLeapFrac(i, leaps);
    }
    /* a guardian brick can strike the same foe every hitCd, and there are n of them */
    public static double orbitDps()
    {
        var p = player;
        if (p.orbit.n == 0) return 0;
        double hitCd = clamp(0.5 * 2.6 / p.orbit.spd, 0.05, 0.5);
        return calcDamage(p.orbit.dmg, "spell") / hitCd;
    }
    /* the vortex ticks while it is up, then waits for its cooldown */
    public static double vortexDps()
    {
        var p = player;
        if (p.vortex.n == 0) return 0;
        double tick = 0.24 * p.vortex.hitMul;
        double dur = p.vortex.dur + (p.vortex.n - 1) * 0.4;
        return calcDamage(p.vortex.dmg, "melee") * Math.Floor(dur / tick) / p.vortex.rate;
    }
    /* everything one bolt deals down the whole chain, leaps included */
    public static double chainBoltTotal()
    {
        var p = player;
        if (p.chain.n == 0) return 0;
        double d = shockDamage();
        double leaps = p.chain.n - 1;
        double total = d;
        for (int i = 1; i <= leaps; i++) total += d * chainLeapFrac(i, leaps);
        return total;
    }
    /* each probe's number, or NaN where it could not be read (null in the JS) */
    public static double[] statSnapshot()
    {
        var @out = new double[STAT_PROBES.Count];
        for (int i = 0; i < STAT_PROBES.Count; i++)
        {
            double v;
            try { v = STAT_PROBES[i].get(); } catch { v = double.NaN; }
            @out[i] = double.IsFinite(v) ? v : double.NaN;
        }
        return @out;
    }
    /* Try a brick on without keeping it: clone the hero, apply, read, put it back.
       structuredClone copes with the Sets and arrays the player carries. */
    public static Ent heroClone()
    {
        try { return DeepCopy.Clone(player); } catch { return null; }
    }
    public static List<CmpRow> previewPick(Upgrade u, Calc c)
    {
        var before = statSnapshot();
        var save = heroClone();
        if (save == null) return new();
        /* THE WORLD IS PUT BACK TOO, not only the hero. A card's apply can reach past
           the hero -- a golem brick RAISES its golem, Skeleton Mages turns the legion
           it finds into mages -- and hovering a card you then did not take left that
           golem standing. Every list a card can add to is restored, and every minion
           standing gets its own fields back. */
        var wMinions = minions.ToList(); var wProps = props.ToList(); var wProj = projectiles.ToList(); var wPick = pickups.ToList();
        var wEnemies = enemies.ToList(); var wCorpses = corpses.ToList(); var wPending = pendingPicks.ToList(); var wSched = sched.ToList();
        var wHaz = hazards.ToList(); var wOrbs = bloodOrbs.ToList();
        var mins = minions.Select(m => (m, DeepCopy.Shallow(m))).ToList();
        var after = before;
        previewing = true;
        try { u.apply(c); syncStats(); after = statSnapshot(); } catch { }
        previewing = false;
        DeepCopy.Assign(player, save);
        minions = wMinions; props = wProps; projectiles = wProj; pickups = wPick;
        enemies = wEnemies; corpses = wCorpses; pendingPicks = wPending; sched = wSched;
        hazards = wHaz; bloodOrbs = wOrbs;
        foreach (var (m, was) in mins) DeepCopy.Assign(m, was);
        syncStats();
        var rows = new List<CmpRow>();
        for (int i = 0; i < STAT_PROBES.Count; i++)
        {
            var s = STAT_PROBES[i]; double a = before[i], b = after[i];
            if (double.IsNaN(a) || double.IsNaN(b)) continue;
            if (Math.Abs(b - a) < 1e-9) continue;
            /* A spell you do not own yet has no "before" to compare against, so the row
               is neutral — an interval of 10s on a freeze you are picking up for the
               first time is not a downgrade, it is simply what it costs. */
            bool fresh = s.zero && (a == 0 || b == 0);
            bool better = (b > a) == s.hi;
            rows.Add(new CmpRow
            {
                g = s.g, k = s.k, why = statWhy(s),
                from = (s.zero && a == 0) ? "—" : s.fmt(a),
                to = (s.zero && b == 0) ? "—" : s.fmt(b),
                up = better, fresh = fresh,
            });
        }
        return rows;
    }
    /* The floating panel. One per document, moved and refilled as you hover. */
    public static Element cmpEl = null;
    public static Element cmpPanel()
    {
        if (cmpEl == null)
        {
            cmpEl = document.createElement("div");
            cmpEl.id = "cmpPanel";
            cmpEl.className = "hidden";
            document.body.appendChild(cmpEl);
        }
        return cmpEl;
    }
    public static void cmpHide() { if (cmpEl != null) cmpEl.classList.add("hidden"); }
    /* WHAT A MONSTER IS, in the panel the bricks already use. It reads the same
       EDEF the spawner reads and puts it through the same wave curve, monster level
       and difficulty, so the numbers are what you would actually meet at the wave
       you are standing in — not the table's raw base. */
    public static List<MobRow> mobStatRows(string key)
    {
        bool isBoss = BOSSES.ContainsKey(key) || SUPERBOSSES.ContainsKey(key);
        var rows = new List<MobRow>();
        void add(string g, string k, string v) => rows.Add(new MobRow { g = g, k = k, v = v });
        if (isBoss)
        {
            var B = BOSSES.GetValueOrDefault(key) ?? SUPERBOSSES[key];
            double st = Math.Max(1, Math.Floor(wave.n / 5));
            double bhp = Math.Round((B.hp * st * 1.2 + st * 1250 + 1300) * Js.Or(B.hpMul, 1) * difficulty.hp * mobRankMul() * bossLateMul());
            add("BOSS", "Name", B.name);
            add("BOSS", "Health here", Math.Round(bhp).toLocaleString());
            add("BOSS", "Contact damage",
              S(Math.Round((B.dmg * (1 + st * 1.5) + Math.Pow(st, 1.55) * 9) * Js.Or(B.dmgMul, 1) * mobRankMul())));
            add("BOSS", "Speed", S(Math.Round(B.speed)) + " px/s");
            add("BOSS", "XP", S(Math.Round(B.xp * (1 + (st - 1) * 0.8))));
            return rows;
        }
        var d = EDEF.GetValueOrDefault(key ?? "");
        if (d == null) return rows;
        double lvl = mobLevel(), rank = mobRank(), rankMul = mobRankMul(rank);
        double hpS = (1 + (lvl - 1) * 0.30 + Math.Pow(Math.Max(0, lvl - 5), 1.6) * 0.05) * difficulty.hp * rankMul;
        double dmgB = ((lvl - 1) * 0.55 + Math.Pow(Math.Max(0, lvl - 8), 1.45) * 0.14) * rankMul;
        double armour = lvl * 1.1 * (key == "bastion" ? 1.8 : 1) * Js.Or(d.armourMul, 1)
          * (RANGED_SET.Contains(key) ? 0.45 : 1) * (Js.T(d.golem) ? 1.6 : 1) * rankMul;
        var r = ROSTER.FirstOrDefault(x => x.k == key);
        string AW = "AT WAVE " + S(wave.n);
        add(AW, "Health", Math.Round(Math.Ceiling(d.hp * hpS)).toLocaleString());
        add(AW, "Contact damage", S(Math.Max(1, Math.Round((d.dmg + dmgB) * difficulty.dmg))));
        add(AW, "Armour", S(Math.Round(armour)));
        add(AW, "Monster level", S(rank) + (rank != 0 ? "  (×" + Js.ToFixed(rankMul, 2) + ")" : ""));
        add("BASE", "Base health", S(d.hp));
        add("BASE", "Base damage", S(d.dmg));
        add("BASE", "Move speed", S(Math.Round(d.speed)) + " px/s");
        if (d.fire != 0) add("BASE", "Attack every", Js.ToFixed(d.fire, 1) + "s");
        /* THE CONTEST, BOTH WAYS ROUND, and both of them in the numbers the hero
           actually carries -- a rating on its own means nothing to anybody. */
        if (d.es != 0) add("BASE", "Energy shield", S(Math.Round(d.es * 100)) + "% of its health, on top of it" + " — chaos goes straight through");
        if (d.chaos != 0) add("BASE", "Chaos in its hits", S(Math.Round(d.chaos * 100)) + "% — that share walks past YOUR shield");
        {
            double ev = mobEvasionOf(d), ac = mobAccuracyOf(d);
            add("BASE", "Evasion", S(ev) + "  (you hit " + S(Math.Round(hitChance(accuracyValue(), ev) * 100)) + "%)");
            add("BASE", "Accuracy", S(ac) + (evasionValue() > 0
                ? "  (it hits " + S(Math.Round(hitChance(ac, evasionValue()) * 100)) + "%)"
                : "  (you have no evasion)"));
        }
        if (d.shield) add("BASE", "Block", S(Math.Round((!double.IsNaN(d.block) ? d.block : BLOCK_BASE) * 100))
            + "% — carries a shield: a hit on its shield side may be stopped outright");
        if (d.shade) add("BASE", "Shade", "fades in and out of reach");
        if (Js.T(d.golem)) add("BASE", "Golem kit", GOLEM_CONF.TryGetValue(d.golem, out var gc) ? gc.kit : d.golem);
        if (Js.T(d.mini)) add("BASE", "Mini", "spawned by a " + d.mini + " golem");
        if (Js.T(d.elem)) add("BASE", "Touch element", d.elem);
        if (d.res != null)
            foreach (var el in ELEMENTS)
            {
                double v = d.res[el];
                if (v != 0) add("RESISTANCE", el, S(Math.Round(v * 100)) + "%" + (v < 0 ? "  (weak)" : ""));
            }
        if (r != null)
        {
            add("SPAWNING", "First wave", S(r.at));
            add("SPAWNING", "Budget cost", S(r.cost) + " bricks");
            add("SPAWNING", "Weight", Js.ToFixed(double.IsNaN(r.flavour) ? 1 : r.flavour, 2));
        }
        else add("SPAWNING", "From", "seeded into waves, not bought");
        add("LOOT", "XP", S(d.xp));
        add("LOOT", "Studs", S(d.studs[0]) + "–" + S(d.studs[1]));
        return rows;
    }
    public static void mobTipShow(Element anchor, string key)
    {
        var el = cmpPanel();
        var rows = mobStatRows(key);
        string name = (BOSSES.GetValueOrDefault(key) ?? SUPERBOSSES.GetValueOrDefault(key))?.name ?? key;
        string html = $"<div class=\"cmphead\">{esc(S(name).ToUpperInvariant())}</div>";
        if (rows.Count == 0) html += "<div class=\"cmpnone\">nothing on file for this one</div>";
        string g = null;
        foreach (var r in rows)
        {
            if (r.g != g) { g = r.g; html += $"<div class=\"cmpg\">{esc(g)}</div>"; }
            html += $"<div class=\"cmprow\"><span>{esc(r.k)}</span><b>{esc(r.v)}</b></div>";
        }
        el.innerHTML = html;
        el.classList.remove("hidden");
        var a = anchor.getBoundingClientRect();
        double w = el.offsetWidth, h = el.offsetHeight;
        double x = a.right + 14;
        if (x + w > window.innerWidth - 8) x = a.left - w - 14;
        if (x < 8) x = Math.Max(8, window.innerWidth - w - 8);
        double y = a.top - 8;
        if (y + h > window.innerHeight - 8) y = window.innerHeight - h - 8;
        el.style.left = S(Math.Round(x)) + "px";
        el.style.top = S(Math.Round(Math.Max(8, y))) + "px";
    }
    public static void cmpShow(Upgrade u, Calc c, Element anchor, string title = null, Element avoid = null)
    {
        var el = cmpPanel();
        var rows = previewPick(u, c);
        string html = $"<div class=\"cmphead\">{esc(title ?? u.name)}</div>";
        if (rows.Count == 0)
            html += "<div class=\"cmpnone\">changes how the spell behaves rather than a number — read the card</div>";
        else
        {
            string g = null;
            foreach (var r in rows)
            {
                if (r.g != g) { g = r.g; html += $"<div class=\"cmpg\">{esc(g)}</div>"; }
                /* the explanation rides along as a native tooltip: the panel is already
                   a hover, so a second hover layer inside it would fight the first */
                html += $"<div class=\"cmprow\"{(Js.T(r.why) ? $" title=\"{esc(r.why)}\"" : "")}><span>{esc(r.k)}</span>" +
                  $"<b><i class=\"was\">{esc(r.from)}</i> → <i class=\"{(r.fresh ? "fresh" : (r.up ? "up" : "down"))}\">{esc(r.to)}</i></b></div>";
            }
        }
        el.innerHTML = html;
        el.classList.remove("hidden");
        /* Anchored beside the card — but on the reward screen it is anchored outside
           the whole ROW of cards, on whichever side the hovered card is furthest from,
           so reading one card never hides the two you are choosing between. */
        var a = anchor.getBoundingClientRect();
        var box = avoid != null ? avoid.getBoundingClientRect() : a;
        double w = el.offsetWidth, h = el.offsetHeight, Wd = window.innerWidth, Hd = window.innerHeight;
        bool fitsX(double x) => x >= 8 && x + w <= Wd - 8;
        double clampY(double y) => Math.Max(8, Math.Min(y, Hd - h - 8));
        /* NEVER OVER THE CARD YOU ARE READING. Outside the whole row first; when the
           window is too narrow for that (four boss cards on a laptop), beside the
           hovered card itself -- over a neighbour, which you are not reading -- and
           failing that, under it or over it. The old last resort pinned the panel to
           the window's edge, which on the right-hand card is exactly on top of it. */
        bool leftSide = (a.left + a.right) / 2 > Wd / 2;
        var tries = leftSide
          ? new[] { (box.left - w - 16, a.top), (box.right + 16, a.top), (a.left - w - 12, a.top), (a.right + 12, a.top) }
          : new[] { (box.right + 16, a.top), (box.left - w - 16, a.top), (a.right + 12, a.top), (a.left - w - 12, a.top) };
        (double, double)? at = null;
        foreach (var t in tries) if (fitsX(t.Item1)) { at = (t.Item1, clampY(t.Item2)); break; }
        if (at == null)
        {
            double x = Math.Max(8, Math.Min(Wd - w - 8, (a.left + a.right) / 2 - w / 2));
            at = (a.bottom + 8 + h <= Hd - 8) ? (x, a.bottom + 8)
               : (a.top - h - 8 >= 8) ? (x, a.top - h - 8)
               : (x, clampY(a.top));
        }
        el.style.left = S(Math.Round(at.Value.Item1)) + "px";
        el.style.top = S(Math.Round(at.Value.Item2)) + "px";
    }
    public static void cmpWire(Element el, Upgrade u, Calc c, string title = null, Element avoid = null)
    {
        el.addEventListener("mouseenter", _ => { try { cmpShow(u, c, el, title, avoid); } catch { cmpHide(); } });
        el.addEventListener("mouseleave", _ => cmpHide());
    }
    /* THE PRICE OF A SECOND LOOK. A flat 25 studs was nothing by wave 20 — a late
       wave pays thousands, so rerolling was free and every reward screen turned
       into a slot machine. The base climbs with the wave you have reached, so a
       reroll always costs a real share of what that wave pays, and each one you
       have already bought this run still makes the next dearer. */
    public const double REROLL_BASE = 30;      /* wave 1 */
    public const double REROLL_WAVE = 3.5;     /* × wave^1.35 — roughly how stud income grows */
    public const double REROLL_USED = 0.35;    /* every reroll bought this run adds this much again */
    public static double rerollBase() => Math.Round(REROLL_BASE + REROLL_WAVE * Math.Pow(Math.Max(1, wave.n), 1.35));
    public static double rerollCost() => Math.Round(rerollBase() * (1 + rerollsUsed * REROLL_USED));
    public static void renderCards()
    {
        var wrap = document.getElementById("cards");
        wrap.innerHTML = "";
        /* priced off the cards actually on screen, so a reroll repricing is automatic */
        var rj = document.getElementById("rejectBtn");
        if (rj != null) rj.textContent = $"NO THANKS — TAKE {S(rejectValue())} STUDS (X)";
        bool chest = upgradeMode == "chest";
        for (int i = 0; i < choices.Count; i++)
        {
            var card = choices[i]; int idx = i;
            var R = RARITY[card.rar];
            var el = document.createElement("div");
            el.className = "card";
            double owned = taken[card.t.id];
            string stacks = owned != 0 ? $"<div class=\"stack\">x{S(owned)}</div>" : "";
            /* a card that hands you a SPELL you do not have is a different kind of
               decision from one that deepens something you already cast — it should not
               take reading the text to notice which */
            string isNew = grantsNewSpell(card.t) ? "<div class=\"newtag\">NEW</div>" : "";
            el.innerHTML = $@"{stacks}{isNew}<div class=""band"" style=""background:{R.col}""><i></i><i></i><i></i></div>
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""{R.col}"" stroke-width=""2""
        stroke-linecap=""round"" stroke-linejoin=""round"">{ICONS.GetValueOrDefault(card.t.icon ?? "")}</svg>
      <h3>{card.t.name}</h3><p>{signWords(card.t.desc(card.c, owned))}</p>
      <div class=""rtag"" style=""background:{R.col}"">{R.name}</div>
      {weaponTagHtml(card.t)}
      <div class=""ctag"" style=""background:{CLS_COL.GetValueOrDefault(card.t.cls ?? "") ?? "#555"}"">{card.t.cls}</div>
      <div class=""key"">{i + 1}</div>";
            if (card.rar == "legendary")
                el.style.boxShadow = $"0 8px 0 rgba(0,0,0,.35), 0 0 40px {R.col}, 0 0 90px {R.col}";
            else if (card.rar == "rare" || card.rar == "epic")
                el.style.boxShadow = $"0 8px 0 rgba(0,0,0,.35), 0 0 22px {R.col}";
            el.addEventListener("click", _ => { cmpHide(); chooseUpgrade(idx); });
            cmpWire(el, card.t, card.c, card.t.name, wrap);
            wrap.appendChild(el);
        }
        if (chest) return;   /* no reroll UI on chest rewards — the single card is the pick */
        var btn = document.getElementById("rerollBtn");
        double cost = rerollCost();
        double left = rerollsLeft();
        btn.disabled = player.studs < cost || left <= 0;
        document.getElementById("rerollCost").textContent =
          left <= 0 ? "no rerolls left for this reward"
          : $"costs {S(cost)} studs · {S(left)} reroll{(left > 1 ? "s" : "")} left · you have {S(player.studs)}"
            + (rerollsUsed > 0 ? $" · {S(rerollsUsed)} bought so far" : "");
    }
}
