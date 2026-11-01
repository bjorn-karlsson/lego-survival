// Port of lego-survival.html lines 19716-20311: the CHARACTER SHEET -- SIMPLE
// (the basic probes) and NERD (every pool, formula and ceiling, hand-written,
// then every probe generated), its rows, hover cards and blocks.
using System.Text.RegularExpressions;

namespace Brickblade;

sealed class ProbeGroup { public string g; public List<string> rows = new(); }

public static partial class G
{
    /* =============================== CHARACTER SHEET =============================== */
    /* every string headed for the page comes through here, which makes it the one
       place a negative "increased" can be caught -- see signWords */
    public static string esc(string t) => signWords(t ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    public static string esc(object t) => esc(t is string s ? s : S(t));
    /* `tip` turns the row into a hover card: what the stat IS, and what it is doing
       for you right now. The cards themselves stay terse — the glossary lives here. */
    public static string sRow(object label0, object value0, string sub = null, string tip = null)
    {
        var lv = signLabel(label0 is string ls ? ls : S(label0), value0 is string vs ? vs : S(value0)); string label = lv[0], value = lv[1];
        string cls = Js.T(tip) ? " class=\"srow tip\" data-tip=\"" + esc(tip) + "\"" : " class=\"srow\"";
        return $"<div{cls}><span>{label}{(Js.T(sub) ? $"<small>{esc(sub)}</small>" : "")}</span><b>{esc(value)}</b></div>";
    }
    public static string sBlock(string title, IEnumerable<string> rows, string extra = "") =>
        $"<div class=\"sblock\"><h4>{title}</h4>{extra}{string.Join("", rows)}</div>";
    public static string sBar(double frac, string col) =>
        $"<div class=\"sbar\"><i style=\"width:{Js.ToFixed(clamp(frac, 0, 1) * 100, 1)}%;background:{col}\"></i></div>";
    /* SIMPLE IS WHERE IT OPENS. The sheet was written for somebody tuning a build
       -- every pool, every formula, every ceiling -- and that is still here, one
       click away, as NERD. SIMPLE is the handful of numbers a player reads to
       play: the same basic probes the menu shows, under the same headings, only
       the ones this hero actually has, then the spells and the bricks. Remembered
       between runs, and N flips it without reaching for the mouse. */
    static string[] Cat(params object[] parts)
    {
        var l = new List<string>();
        foreach (var o in parts) { if (o is IEnumerable<string> e && o is not string) l.AddRange(e); else l.Add(o as string); }
        return l.ToArray();
    }
    public static bool sheetNerd = false;
    public static void SheetNerdLoad() { try { sheetNerd = Store.getItem("bb_sheetnerd") == "1"; } catch { } }
    public static void setSheetNerd(bool on)
    {
        sheetNerd = on;
        try { Store.setItem("bb_sheetnerd", sheetNerd ? "1" : "0"); } catch { }
        if (state == "char") renderCharSheet();
    }
    public static void sheetModeWire()
    {
        var m = document.getElementById("sheetMode");
        if (m == null) return;
        foreach (var b in m.children)
        {
            b.classList.toggle("on", (b.dataset["mode"] == "nerd") == sheetNerd);
            if (b.dataset["wiredNative"] == null)
            {
                b.dataset["wiredNative"] = "1";
                var bb = b;
                b.addEventListener("click", _ => { SFX.click(); setSheetNerd(bb.dataset["mode"] == "nerd"); });
            }
        }
    }
    /* the short list as sheet blocks: basic probes, grouped by their simple
       heading, a stat this hero does not have is left out rather than dashed */
    public static string simpleStatBlocks()
    {
        var by = new Dictionary<string, List<string>>();
        foreach (var sp in STAT_PROBES)
        {
            if (!Js.T(sp.basic)) continue;
            double v;
            try { v = sp.get(); } catch { v = double.NaN; }
            if (double.IsNaN(v) || !double.IsFinite(v) || v == 0) continue;
            if (!by.TryGetValue(sp.basic, out var l)) by[sp.basic] = l = new();
            l.Add(sRow(esc(sp.k), sp.fmt(v), "", statWhy(sp)));
        }
        return string.Join("", STAT_BASIC_ORDER.Where(h => by.ContainsKey(h)).Select(h => sBlock(h, by[h])));
    }
    /* Everything here is read straight off the live player, and the derived numbers use
       the same formulas the update loop does — see updatePlayer / hitEnemy / gainXp. */
    public static void renderCharSheet()
    {
  var p = player;
  if(p == null) return;
  Func<double,string> pct = v=> Math.round(v*100)+"%";
  double mm = Math.floor(runTime/60.0), ss = Math.floor(runTime%60);
  double best = 0; try{ best = Js.ToNumber(Js.Or(Store.getItem("bb_best"), "0")); if (double.IsNaN(best)) best = 0; }catch{}

  document.getElementById("charSub").textContent =
    $"Level {S(p.level)} · Wave {S(wave.n)} · {S(p.kills)} smashed · {S(p.studs)} studs · {S(mm)}:{S(ss).PadLeft(2,'0')}";

  /* --- melee maths, mirroring startSwing + updatePlayer --- */
  var light = swordDamage();
  var spin  = light * 1.4;
  var swingT = p.atkRate / Math.max(0.3, chillMul()) * curseSwingMul();
  var swings = 1.0/swingT;
  var cc = critChance();
  var critAvg = 1 + cc*(critMult()-1);           /* expected multiplier per hit */
  var comboAvg = (light*2 + spin)/3.0;             /* every third swing is the spin */

  var vitals = sBlock("VITALS", [
    sRow("Hearts", $"{S(fmtHp(p.hp))} / {S(p.maxHp)}",
      p.maxHp>=heartCap() ? "at the maximum"
        : $"({S(HP_BASE)} + {S(p.hpFlat)} flat){S(p.hpMore>1?$" × {S(p.hpMore)} MORE":"")}",
      $"Hearts are continuous, not whole steps: a hit for 1.4 takes 1.40 off you, and a "+
      $"heart that is part full is drawn part lit.\n\nRegeneration fills the heart you "+
      $"are standing on from 0.00 upward instead of waiting to bank a whole point."),
    sRow("Armour", Math.round(armourValue()),
      p.curse["armor"]>0 ? $"BRITTLE — cut from {S(Math.round((p.armour+wardArmour())*(1+p.incArmour)))}"
        : (p.armour+wardArmour())>0
          ? $"{S(p.armour)}{S(wardArmour()!=0?$" + {S(wardArmour())} ward":"")} flat × (1 + {S(Math.round(p.incArmour*100))}% increased)"
        : "no armour — attack hits land in full",
      $"ARMOUR reduces ATTACK hits — never spells.\n"+
      $"Damage reduction = Armour / (Armour + 5 × hit damage),\n"+
      $"so a big hit punches through armour that shrugs off a small one.\n"+
      $"Flat armour adds to the total; increased armour is additive in its own pool.\n\n"+
      $"Yours: {S(p.armour + wardArmour())} flat × (1 + {S(Math.round(p.incArmour*100))}% increased) = {S(Math.round(armourValue()))}\n"+
      $"A 6-damage hit: {S(Math.round(armourValue()))} / ({S(Math.round(armourValue()))} + 30) = "+
      $"{S(Math.round(armourDR(armourValue(),6)*100))}% less → you take {S((6*(1-armourDR(armourValue(),6))).toFixed(1))}"),
    sRow("Attack mitigation",
      p.armour>0 ? $"{S(Math.round(armourDR(armourValue(),2)*100))}% / {S(Math.round(armourDR(armourValue(),6)*100))}% / {S(Math.round(armourDR(armourValue(),12)*100))}%" : "—",
      "against a 2 / 6 / 12 damage hit",
      $"The same armour is worth less the harder you are hit.\n\n"+
      string.Join("\n", new double[]{2,6,12,30}.Select(h=>$"{S(h)} damage → {S((h*(1-armourDR(armourValue(),h))).toFixed(1))} taken ({S(Math.round(armourDR(armourValue(),h)*100))}% less)"))+
      $"\n\nCapped at {S(Math.round(ARMOUR_DR_CAP*100))}% — nothing is ever immune."),
    sRow("Spell suppression", Math.round(suppressChance()*100)+"%",
      suppressChance()>0 ? $"a suppressed spell hit deals {S(Math.round(SUPPRESS_TAKEN*100))}%" : "spell hits land in full",
      $"SPELL SUPPRESSION is a separate layer from armour and resistance.\n"+
      $"It is a CHANCE to suppress an incoming spell hit; a suppressed hit\n"+
      $"deals {S(Math.round(SUPPRESS_TAKEN*100))}% of its damage. At 100% every spell hit is suppressed.\n"+
      $"Attack hits are never suppressed — those meet armour instead.\n\n"+
      $"Yours: {S(Math.round(suppressChance()*100))}% chance.\n"+
      $"A 10-damage spell hit averages {S((10*(1 - suppressChance()*(1-SUPPRESS_TAKEN))).toFixed(1))} against you."),
    sRow("Regeneration", cursedRegen()>0 ? cursedRegen().toFixed(2)+" hearts/s" : "—",
      wave != null && wave.state=="ready" ? $"×{S(REGEN_BREAK_MUL)} between waves — this is the time to heal"
        : p.regenBoostT>0 ? $"{S(p.heartStacks)} heart{S(p.heartStacks>1?"s":"")} have it at ×{S(HEART_STACK_MUL[p.heartStacks])} "+
        $"(max {S(HEART_STACK_MAX)} for ×{S(HEART_STACK_MUL[HEART_STACK_MAX])}) — {S(Math.ceil(p.regenBoostT))}s left"
        : p.curse["regen"]>0 ? $"WITHERED — choked from {S(p.regenRate.toFixed(2))}/s"
        : p.regenRate>0 ? $"one heart every {S((1.0/cursedRegen()).toFixed(1))}s" : "take Repair Bricks to regenerate"),
    sRow("Hit invulnerability", IFRAME_T.toFixed(2)+"s",
      "after taking a hit — everything landing on you counts"),
    sRow("Spell slots", spellsOwned()+" / "+SPELL_MAX,
      spellsOwned() >= SPELL_MAX ? "full — only bricks that deepen these four are offered now"
        : "a new spell takes a slot; deepening one never does"),
    sRow("Heart drop rate", (heartDropChance()*100).toFixed(2)+"%",
      $"per monster · ×{S(DROP_ELITE)} from an elite · a boss always leaves one")
  ], sBar(p.hp/Math.max(1,p.maxHp), "#d3242b"));

  var move = sBlock("MOVEMENT", [
    sRow("Move speed", Math.round(p.speed)+" px/s",
      $"{S(SPEED_BASE)} base × (1 + {S(Math.round(p.inc["speed"]*100))}% increased)"),
    sRow("Roll cooldown", p.dash.cdMax.toFixed(2)+"s", "SPACE — invulnerable while rolling"),
    sRow("Roll distance", Math.round(640*0.18)+" px", "a perfect dodge refunds bomb cooldown"),
    sRow("Chilled", p.chill.stacks>0 ? "×"+Math.round(p.chill.stacks) : "no",
      p.chill.stacks>0 ? $"moving at {S(Math.round(chillMul()*100))}% speed" : "")
  ]);

  var sword = sBlock(weapon.name, [
    sRow("Weapon", weapon.name,
      $"{S(weapon.dmg)} base · {S(weapon.rate.toFixed(2))}s · {S(Math.round(weapon.crit*100))}% crit",
      $"{S(weapon.blurb)}\n\n"+
      $"Base damage {S(weapon.dmg)} · swing {S(weapon.rate.toFixed(2))}s · reach {S(weapon.reach)}\n"+
      $"Crit {S(Math.round(weapon.crit*100))}% for ×{S((weapon.critMult/100.0).toFixed(2))}\n"+
      $"Spin swing ×{S(weapon.spin.toFixed(2))} · knockback ×{S(weapon.kb.toFixed(2))}\n"+
      $"Bleed chance ×{S(weapon.bleed.toFixed(2))}\n\n"+
      $"Every brick you own works with every weapon — the weapon sets the base,\n"+
      $"your bricks scale it. Ability names never change: a mace still swings\n"+
      $"Bladestorm, it just swings it like a mace."),
    ..(isCaster() ? Cat(
      sRow("Bolt damage", boltDamage().toFixed(1)+" dmg",
        $"{S(weapon.elem)} · bursts for {S(Math.round(boltRadius()))}",
        $"A staff does not swing. Its basic attack is a bolt of its element, and it\n"+
        $"is BOTH a spell and elemental damage, so both pools apply.\n\n"+
        $"({S(weapon.dmg)} base + {S(fmtHp(p.flatElem))} flat)\n"+
        $" × (1 + {S(Math.round(incTotal("spell", weapon.elem)*100))}% increased"+
        $"{S(atCap(incTotal("spell", weapon.elem), INC_DMG_CAP))})"+
        $" × {S(moreShown().toFixed(2))} more = {S(boltDamage().toFixed(2))}"),
      sRow("Burst radius", Math.round(boltRadius())+" px",
        $"everything caught takes the hit and may catch fire (cap {S(BOLT_R_CAP)})"),
      (p.ember != 0 ? new string[] {sRow("Ember Scatter", "\u00d7"+S(p.ember),
        S(EMBER_SHOTS[Js.I32(p.ember)])+" embers per hit on a burning body",
        $"Hitting something already alight knocks {S(EMBER_SHOTS[Js.I32(p.ember)])} embers off it.\n"+
        $"Each finds the nearest OTHER monster and sets that alight too, for\n"+
        $"{S(Math.round(EMBER_SHARE*100))}% of the hit that threw it.\n\n"+
        $"With nothing else standing they LOOP BACK into the body that threw\n"+
        $"them, so it is never dead weight against a boss on its own.\n\n"+
        $"A chain travels {S(EMBER_DEPTH)} bodies deep, and one body can only throw\n"+
        $"embers every {S(EMBER_CD)}s — otherwise a crowd would never stop.")} : new string[0]),
      sRow("Burning", burnCap()+" stacks", "then BURNT",
        $"A bolt has {S(Math.round(ailChanceOf("fire","fire",false)*100))}% to light a fire on what it "+
        $"hits — {S(Math.round(AIL_NATIVE_CHANCE["fire"]*100))}% of its own, plus\n"+
        $"every chance you have bought, and a critical always does. Each stack burns on its own for\n"+
        $"{S(Math.round(EBURN_SHARE*100))}% of the hit a second over {S(EBURN_DUR)}s, and they all burn at once.\n\n"+
        $"THE LAST ONE BREAKS: the fires go out and the body is BURNT, taking\n"+
        $"{S(Math.round(BURNT_STEP*100))}% MORE fire from then on. Marks stack to {S(BURNT_MAX)} and each is its own\n"+
        $"multiplier — four marks is ×{S(Math.pow(1+BURNT_STEP,BURNT_MAX).toFixed(2))} fire taken.\n\n"+
        $"Burning is damage over time, so it reads your increased damage over\n"+
        $"time ({S(Math.round(p.inc["dot"]*100))}%) exactly like poison and bleed.")
    ) : isSummoner() ? new string[] {
      sRow("Legion", legion().Count()+" / "+minionMax()+" standing",
        $"{S(Math.round(minionMaxHp()))} life · {S(fmtHp(minionDamage(p.minions.mages)))} a hit · "+
        $"{S((1.0/minionRate(p.minions.mages)).toFixed(2))}/s"+(p.minions.mages ? " · SKELETON MAGES" : ""),
        $"The scepter does not swing. Its SUMMON AURA raises skeletons where you point, "+
        $"{S(p.minions.perCast)} a raising, until there are {S(minionMax())}, and refills any that fall. "+
        $"LEFT CLICK is CONVOCATION: every minion you own is called back to your side and mends "+
        $"{S(Math.round(CONVO_REGEN*100))}% of its life a second for {S(CONVO_REGEN_T)}s ({S(CONVO_CD)}s cooldown).\n\n"+
        $"They rise as spearmen, swordsmen or archers."+(golemMax() ? $" {S(golemMax())} BONE GOLEMS stand beside them, their own pool." : "")+
        (p.minions.overseer ? " A BONE OVERSEER at your shoulder drives on every one near it." : "")+
        $"\n\nMinion damage is its OWN pool: "+Math.round(minionInc()*100)+$"% increased now"+
        (p.minions.mirror ? ", three quarters of yours mirrored into it." : ".")+
        (p.minions.instab ? $"\n\nMINION INSTABILITY: a skeleton that falls bursts for {S(Math.round(INSTAB_SHARE*100))}% of its life as fire." : ""))
    } : isArcher() ? new string[] {
      sRow("Arrow", arrowDamage().toFixed(1)+" dmg",
        $"{S(clamp(Js.I32(p.bow.n),1,ARROW_MAX_N))} a shot \u00b7 "+
        (p.bow.kind == "chaos" ? "pierces every body" : $"pierces {S(p.bow.pierce)}")+
        (p.bow.kind != null ? $" \u00b7 {S(ARROW_KINDS[p.bow.kind].name)}" : ""),
        $"A bow does not swing. Every draw looses {S(clamp(Js.I32(p.bow.n),1,ARROW_MAX_N))} arrow"+
        $"{S((Js.I32(p.bow.n)) > 1 ? "s, fanned," : "")} at what you aim at. An arrow is an ATTACK: it\n"+
        $"rolls to hit against evasion, reads your increased melee damage, and bleeds.\n\n"+
        (p.bow.kind == "light"
          ? $"LIGHTNING ARROW: {S(Math.round(ARROW_KINDS["light"].conv*100))}% of it arrives as lightning, and every arrow that\n"+
            $"lands forks to up to {S(ARROW_FORK_N)} bodies within {S(ARROW_FORK_R)}px for {S(Math.round(ARROW_FORK_SHARE*100))}% each.\n\n"
        : p.bow.kind == "fire"
          ? $"FIRE ARROW: {S(Math.round(ARROW_KINDS["fire"].conv*100))}% of it arrives as fire, and it bursts where it lands:\n"+
            $"everything within {S(ARROW_BLAST_R)}px takes it {S(Math.round(ARROW_BLAST_LESS*100))}% LESS.\n\n"
        : p.bow.kind == "chaos"
          ? $"CHAOS ARROW: {S(Math.round(ARROW_KINDS["chaos"].conv*100))}% of it arrives as chaos, and it passes through\n"+
            $"every body in its line without losing anything.\n\n"
        : "")+
        (p.ricochet > 0 ? $"RICOCHET: {S(Math.round(p.ricochet*100))}% for an arrow that would stop to leap on, up to {S(RICO_MAX)} times." : ""))
    } : isSlammer() ? new string[] {
      sRow("One tooth", slamSpikeDamage().toFixed(1)+" dmg",
        $"{S(Math.round(p.slamArc*57.3))}\u00b0 fault \u00b7 {S(Math.round(p.slamRange))} px \u00b7 "+
        $"{S(slamRays(p.slamArc)*Math.max(2,Math.round(p.slamRange/SLAM_STEP)))} teeth a slam",
        $"A mace does not swing. It drives the head into the floor and a FAULT opens:\n"+
        $"lines of stone teeth tearing up out of the ground in a fan, running outward\n"+
        $"at {S(Math.round(p.slamSpeed))} px/s.\n\n"+
        $"Each tooth is {S(Math.round(SLAM_SHARE*100))}% of a full swing and is PHYSICAL, so it bleeds and\n"+
        $"reads every increase a swing would.\n\n"+
        $"ONE BODY may be bitten by at most {S(SLAM_MAX_HITS)} teeth of the same slam, so standing in\n"+
        $"the middle of the fault is a reward and not an execution.\n\n"+
        $"Ceilings: {S(Math.round(slamArcCap()*57.3))}\u00b0 wide, {S(SLAM_RANGE_MAX)} px deep, {S(SLAM_SPEED_MAX)} px/s."),
      ..(p.slam.elem>0 ? new string[] {sRow("Slam of the Elements", "\u00d7"+p.slam.elem,
        $"next: {S(SLAM_AIL[slamElement()])} \u00b7 {S(Math.round(SLAM_ELEM_SHARE*100))}% of every tooth converted",
        $"Each slam takes the next element in turn \u2014 fire, lightning, frost \u2014 and converts\n"+
        $"{S(Math.round(SLAM_ELEM_SHARE*100))}% of every tooth into it. The converted half pays the monster's\n"+
        $"resistance and leaves that element's ailment behind: burning, shock, chill.\n\n"+
        $"The physical half is untouched, so the slam still opens wounds.\n\n"+
        (p.slam.elem>=SLAM_ELEM_MAX
          ? $"TAKEN TWICE: the fault closes into a full {S(Math.round(SLAM_ARC_FULL*57.3))}\u00b0 ring."
          : $"Take it again and the fault opens all the way to {S(Math.round(SLAM_ARC_FULL*57.3))}\u00b0."))} : new string[0])
    } : new string[] {
      sRow("Light swing", light.toFixed(1)+" dmg", $"{S(Math.round(weapon.reach+p.reach))} reach · arc swing"),
      sRow("Spin swing", spin.toFixed(1)+" dmg", $"{S(Math.round(weapon.spinReach+p.reach))} reach · every 3rd swing, ×{S(weapon.spin.toFixed(2))} MORE")
    }),
    ..(p.blademaster>0 ? new string[] {sRow("Blademaster", "×"+p.blademaster,
      $"hold to whirl · {S(Math.round(WHIRL_SLOW[p.blademaster]*100))}% move speed · "+
      $"{S(Math.round(WHIRL_DEFLECT[p.blademaster]*100))}% deflect · no roll",
      $"Holding the swing button spins you up instead of swinging. Everything inside "+
      $"{S(Math.round(whirlReach()))} reach (half as much again as a swing) takes {S(Math.round(WHIRL_MUL*100))}% of a light swing "+
      $"every {S(WHIRL_TICK.toFixed(2))}s — that is {S((swordDamage([WHIRL_MUL])/WHIRL_TICK).toFixed(1))} "+
      $"per second, held.\n\nYou move at {S(Math.round(WHIRL_SLOW[p.blademaster]*100))}% speed while "+
      $"it is up and cannot roll out of anything. The blade turns aside "+
      $"{S(Math.round(WHIRL_DEFLECT[p.blademaster]*100))}% of the missiles that reach it.")} : new string[0]),
    sRow(isCaster() ? "Cast rate" : isSlammer() ? "Slam rate" : isArcher() ? "Shot rate" : isSummoner() ? "Raise rate" : "Swing rate", swings.toFixed(2)+"/s",
      (p.chill.stacks>0 || p.curse["haste"]>0)
        ? $"{S(swingT.toFixed(2))}s per {S(swingWord())} — slowed from {S(p.atkRate.toFixed(2))}s"
        : $"{S(p.atkRate.toFixed(2))}s per {S(swingWord())} — {S(weapon.rate.toFixed(2))}s {S(weapon.name.ToLowerInvariant())} base / (1 + {S(Math.round(p.inc["atk"]*100))}%)"),
    sRow("Sustained DPS", ((isSlammer() || isArcher() || isSummoner()) ? swordDps() : comboAvg*swings*critAvg).toFixed(1),
      isSummoner() ? "a full legion on one body, crits included"
      : isSlammer() ? "one body held in the fault, crits included"
      : isArcher() ? "one arrow a shot into one body, crits included" : "average combo, crits included, one target"),
    /* the second speed stat, and the one that is easy to confuse with the first */
    sRow("Cast speed", Math.round(castInc(0)*100)+"%",
      "every ABILITY, not the hands",
      $"ATTACK SPEED above is your own hands — the swing or the bolt.\n"+
      $"CAST SPEED is every ability you own: guardian ring, storm brick,\n"+
      $"bladestorm, vortex, block freeze, bombs, brickbane.\n\n"+
      $"They are increases, so they SUM into one pool per ability and none of them\n"+
      $"multiplies: an ability's own rate bricks + {S(Math.round(p.inc["cast"]*100))}% global cast speed"+
      $"{S(weapon.incAtk != 0 ? $" + {S(Math.round(weapon.incAtk*100))}% from the {S(weapon.name.ToLowerInvariant())}" : "")}.\n\n"+
      $"Every one of them then stops at its OWN floor, which is why a bomb volley\n"+
      $"never becomes a machine gun however much tempo you buy:\n"+
      string.Join("\n", SPELL_FLOOR.Keys.Select(k=>
        $"  {S((SPELL_NAME.GetValueOrDefault(k)??k).PadRight(15))} {S(SPELL_FLOOR[k].toFixed(2))}s"+
        $"{S(p.Spell(k) != null && p.Spell(k).n>0 ? $"   (now {S(p.Spell(k).rate.toFixed(2))}s"+
          $"{S(p.Spell(k).rate <= SPELL_FLOOR[k]+1e-9 ? ", at the floor" : "")})" : "")}")))
  ]);

  var crit = sBlock("CRITICALS", [
    sRow("Crit chance", (cc*100).toFixed(1)+"%",
      $"{S((weapon.crit*100).toFixed(0))}% {S(weapon.name.ToLowerInvariant())} base × (1 + {S(Math.round(p.incCrit*100))}% increased)"
      + (cc>=CRIT_CHANCE_CAP ? " — at the cap" : ""),
      $"Final crit chance = Base × (1 + total increased).\n"+
      $"Increases are additive with one another, then multiply the base once.\n\n"+
      $"Yours: {S((weapon.crit*100).toFixed(0))}% {S(weapon.name.ToLowerInvariant())} base × (1 + {S(Math.round(p.incCrit*100))}%) = {S((cc*100).toFixed(1))}%\n"+
      $"Capped at {S(Math.round(CRIT_CHANCE_CAP*100))}%."),
    sRow("Crit multiplier", critMultPct()+"%",
      $"a crit deals {S(critMultPct())}% of the hit"
      + (critMultPct()>=CRIT_MULT_CAP ? " — at the cap" : ""),
      $"Crit multiplier is how much of the hit a critical strike deals.\n"+
      $"{S(weapon.critMult)}% is the {S(weapon.name.ToLowerInvariant())}'s base; +X% to multiplier adds points to it.\n\n"+
      $"Yours: {S(weapon.critMult)}% base + {S(critMultPct()-weapon.critMult)}% = {S(critMultPct())}%\n"+
      $"Your {S(light.toFixed(1))} sword hit crits for {S((light*critMult()).toFixed(1))}.\n"+
      $"Capped at {S(CRIT_MULT_CAP)}%."),
    sRow("Average multiplier", "×"+critAvg.toFixed(2), "expected damage across all hits")
  ], sBar(cc, "#ffd166"));

  var power = sBlock("DAMAGE PIPELINE", [
    sRow("Physical base + flat", physBase().toFixed(2),
      $"{S(weapon.name.ToLowerInvariant())} {S(weapon.dmg)} base + {S(fmtHp(p.flatPhys))} flat added",
      $"FLAT added damage is summed with the base BEFORE anything scales it,\n"+
      $"so flat is worth most when you already have big increases, and vice versa.\n\n"+
      $"The BASE is your weapon's. Every flat brick adds to the same pool whatever\n"+
      $"you are holding, so a build is never locked to one weapon.\n\n"+
      $"Damage = (Base + Flat) × (1 + increased) × each more multiplier\n"+
      $"Yours:  ({S(weapon.dmg)} + {S(fmtHp(p.flatPhys))}) × (1 + {S(Math.round(incTotal("melee")*100))}%) × {S(moreShown().toFixed(2))} = {S(light.toFixed(2))}"),
    sRow("Increased damage", Math.round(incTotal("melee")*100)+"%"
        + atCap(incTotal("melee"), INC_DMG_CAP),
      $"+5% per level, and everything else that says \"increased\" (cap {S(Math.round(INC_DMG_CAP*100))}%)",
      $"INCREASED modifiers of the same kind are ADDITIVE with one another,\n"+
      $"then multiply the base once.\n"+
      $"50% increased + 30% increased = ×1.80, NOT ×1.50 × 1.30.\n\n"+
      $"Yours: {S(Math.round(p.incAll*100))}% from levels + {S(Math.round(p.incMelee*100))}% physical"+
      $" = ×{S((1+incTotal("melee")).toFixed(2))} on a weapon hit."),
    sRow("Increased physical", Math.round(p.incMelee*100)+"%", "your weapon, bladestorm & vortex",
      $"Adds into the same pool as your increased damage, for physical only.\n\n"+
      $"{S(weapon.name)}: ({S(weapon.dmg)} + {S(fmtHp(p.flatPhys))} flat) × (1 + {S(Math.round(incTotal("melee")*100))}%) = "+
      $"{S(((weapon.dmg+p.flatPhys)*(1+incTotal("melee"))).toFixed(2))} before more multipliers."),
    sRow("Increased spell", Math.round(p.incSpell*100)+"%", "orbit, bombs & storm bricks",
      $"The spell half of the same pool. Bladestorm and vortex are melee, not spells.\n\n"+
      $"A 5-base spell hit: 5 × (1 + {S(Math.round(incTotal("spell")*100))}%) × {S(moreShown().toFixed(2))} = "+
      $"{S(calcDamage(5,"spell").toFixed(2))}"),
    sRow("Increased damage over time", Math.round(p.inc["dot"]*100)+"%",
      hasAnyDot() ? "every lingering damage you inflict" : "nothing you own deals damage over time yet"),
    sRow("Bleed stacks", BLEED_STACK_MAX+" deep",
      Math.round(p.bleedShare*BLEED_STACK_MAX*100)+"%/s of a hit, fully opened",
      $"A body holds {S(BLEED_STACK_MAX)} wounds at once, each with its own damage and its own\n"+
      $"{S(BLEED_T)}s clock, all biting together.\n\n"+
      $"Past {S(BLEED_STACK_MAX)} a new cut takes the SHALLOWEST wound's place — and only if it is\n"+
      $"deeper. A weaker cut on a fully opened body is thrown away.\n\n"+
      $"Which means hitting OFTEN beats hitting hard once: parked on a boss, every\n"+
      $"swing trades your worst wound up for a better one."),
    sRow("Ailment chance", Math.round(ailChanceOf("fire", null, false)*100)+"% / "
        + Math.round(ailChanceOf("fire", null, true)*100)+"%",
      "a hit that is ALL one type / the same hit on a crit",
      $"An ailment belongs to the DAMAGE TYPE that landed, not to the spell that threw\n"+
      $"it. A hit is checked against each type it arrived as, and the chance rides on\n"+
      $"that type's share: a swing that is half fire rolls half of this for an ignite.\n\n"+
      ailTableText()+$"\n"+
      $"A CRITICAL always ignites, chills and shocks. It never opens a wound and never\n"+
      $"leaves a dose — BLEED and POISON are bought, or they do not happen.\n\n"+
      $"BLEED is the only one that cares where the hit came from: an ATTACK can cut,\n"+
      $"a spell cannot, however physical it is.\n\n"+
      $"THREE SPELLS CARRY THEIR OWN CHANCE on top of the table, the way a gem does:\n"+
      $"FIREBALL ignites {S(Math.round(AIL_NATIVE_CHANCE["fire"]*100))}% of the time before anything you buy "+
      $"({S(Math.round(ailChanceOf("fire","fire",false)*100))}% now),\n"+
      $"BLOCK FREEZE always chills, and SLAM OF THE ELEMENTS leaves all three.\n"+
      $"Nothing else does — not the storm brick, and not a converted swing."),
    sRow("Bleed damage", Math.round(p.bleedShare*100)+"%/s",
      Math.round(p.bleedShare*BLEED_T*100)+"% of the hit over "+BLEED_T+"s",
      $"A wound ticks for {S(Math.round(p.bleedShare*100))}% of the damage that opened it, "+
      $"every second, for {S(BLEED_T)}s — "+
      $"{S(Math.round(p.bleedShare*BLEED_T*100))}% of that hit in total.\n\n"+
      $"It reads your increased damage over time ({S(Math.round(p.inc["dot"]*100))}%) like every "+
      $"other lingering damage, and it ignores armour: the opening hit already paid it.\n"+
      $"A body standing STILL bleeds {S(Math.round(BLEED_STILL_LESS*100))}% less. Freezing a "+
      $"bleeding monster is not a free win.\n\n"+
      $"WOUNDS STACK {S(BLEED_STACK_MAX)} DEEP and all of them bite at once — "+
      $"{S(Math.round(p.bleedShare*BLEED_STACK_MAX*100))}%/s of a hit on a fully opened body.\n"+
      $"Past {S(BLEED_STACK_MAX)} a new cut displaces the SHALLOWEST wound, and only if it is "+
      $"deeper than that one. Stay on a boss and keep swinging: every hit trades your "+
      $"worst wound up for a better one."),
    sRow("More multipliers", "×"+moreShown().toFixed(2) + atCap(player.moreAll, MORE_DMG_CAP),
      $"each MORE modifier multiplies on its own (cap ×{S(MORE_DMG_CAP.toFixed(2))})",
      $"MORE modifiers are NOT added together — each one is its own multiplier,\n"+
      $"applied after every increase.\n"+
      $"50% more + 30% more = ×1.50 × 1.30 = ×1.95, not ×1.80.\n"+
      $"That is why they are rare and why they stay strong on a big build.\n\n"+
      $"Yours: ×{S(moreShown().toFixed(3))} on every hit"+
      $"{S(player.moreAll > MORE_DMG_CAP + 1e-9 ? $" (the pool holds ×{S(player.moreAll.toFixed(2))}, "+
        $"stopped at the ×{S(MORE_DMG_CAP.toFixed(2))} ceiling)" : "")}.\n"+
      $"An ability's own more multiplier, and the spin swing's 40%, sit on top of it."),
    isSlammer()
      ? sRow("One tooth", slamSpikeDamage().toFixed(2),
          $"({S(weapon.dmg)} + {S(fmtHp(p.flatPhys + p.slam.flat))}) × (1 + {S(Math.round(incTotal("melee")*100))}%) "+
          $"× {S(moreShown().toFixed(2))} × {S(SLAM_SHARE)} share")
      : isSummoner()
      ? sRow("Minion hit", minionDamage(p.minions.mages).toFixed(2),
          $"({S(weapon.dmg)} + {S(fmtHp(Js.Or(p.minions.flat, 0)))}) × (1 + {S(Math.round(minionInc()*100))}%)"+
          $"{S(p.minions.more != 1 ? " × "+p.minions.more.toFixed(2) : "")}{S(p.minions.mages ? " × "+MAGE_SHARE+" mage" : "")}")
      : isArcher()
      ? sRow("Arrow hit", arrowDamage().toFixed(2),
          $"({S(weapon.dmg)} + {S(fmtHp(p.flatPhys + p.bow.flat))}) × (1 + {S(Math.round(incTotal("melee")*100))}%) "+
          $"× {S(moreShown().toFixed(2))}{S(p.bow.more != 1 ? " × "+p.bow.more.toFixed(2)+" arrow" : "")}")
      : sRow(isCaster() ? "Bolt hit" : "Sword hit", light.toFixed(2),
          $"({S(weapon.dmg)} + {S(fmtHp(p.flatPhys))}) × (1 + {S(Math.round(incTotal("melee")*100))}%) × {S(moreShown().toFixed(2))}")
  ]);

  var breachRows = BREACH_LORD_KEYS
    .Where(k=>(p.splinters != null && p.splinters[k] != 0) || (p.rings != null && p.rings[k] != 0))
    .Select(k=>{ var L = BREACH_LORDS[k]; var rk = Js.I32(p.rings != null ? p.rings[k] : 0);
      return sRow(L.ring, rk != 0 ? "rank "+RING_ROMAN[rk] : "\u2014",
        S(p.splinters != null ? p.splinters[k] : 0)+" / "+S(breachStoneCost(rk))+" splinters",
        $"{S(L.name)}, {S(L.title)}. Splinters drop from {S(L.name)}'s breach monsters; {S(BREACH_STONE)} of them "+
        $"fuse into a breachstone, and a breachstone is the ring \u2014 or one rank deeper, to {S(LORD_RING_PLAIN)}. "+
        $"Ranks IV and V are BLESSINGS: {S(breachStoneCost(3))} and {S(breachStoneCost(4))} splinters. "+
        $"The Hand of {S(L.name)} drops the ring outright, and with rank III in hand a deep breach calls "+
        $"{S(L.name)} itself through, for a rare hoard.\n\n"+
        (rk != 0 ? "Yours now: "+ringText(k, rk)+"." : "Each rank: "+ringText(k, 1)+".")); }).ToList();
  var breachBlock = breachRows.Count > 0 ? sBlock("BREACH", breachRows) : "";
  var loot = sBlock("LOOT & XP", [
    sRow("Studs held", S(p.studs), $"next reroll costs {S(rerollCost())}"),
    sRow("Stud gain", "×"+p.studMul.toFixed(2), ""),
    sRow("XP gain", "×"+p.xpMul.toFixed(2), $"wave bonus ×{S((1+wave.n/6.0).toFixed(2))} on top"),
    sRow("Magnet range", Math.round(p.magnetR)+" px", p.magnetR>=420 ? "at the cap" : ""),
    sRow("Fortune", p.fortuneT>0 ? $"×{S(p.fortuneMul)} · {S(Math.ceil(p.fortuneT))}s" : "—",
      p.fortuneT>0 ? $"studs and XP ×{S(p.fortuneMul)}, and ×{S(fortuneRarity().toFixed(2))} rarity find"
        : "a fortune brick doubles studs and XP and leans the reward roll"),
    sRow("Rage brick", p.rageT>0 ? $"×{S(p.rageMul)} · {S(Math.ceil(p.rageT))}s" : "—",
      p.rageT>0 ? "MORE damage, on top of everything else" : "a rage brick doubles all your damage"),
    sRow("Ward", p.wardT>0 ? $"×{S(p.wardN)} · {S(Math.ceil(p.wardT))}s" : "—",
      p.wardT>0 ? $"+{S(wardArmour())} armour and +{S(Math.round(wardRes()*100))}% to every resistance"
        : "a ward brick plates you in armour and resistance")
  ]);

  /* WHAT A HIT IS MADE OF. Only shown to a hero who converts something — a card
     of five zeroes teaches nobody anything. It reads the SAME split the hit
     itself reads, so if this card and the monster ever disagree you can see it. */
  var csp = convSplit(isCaster() ? (weapon.elem ?? "fire") : isArcher() ? "arrow" : isSummoner() ? "minion" : "sword");
  var conversion = convAny() ? sBlock("CONVERSION", CONV_ORDER
    .filter(k=>csp.share[k] > 0)
    .map(k=>sRow(CONV_NAME[k], Math.round(csp.share[k]*100)+"%",
      k == "phys" ? "meets armour, and is the only part that does"
      : k == "chaos" ? "neither physical nor elemental — nothing resists it"
      : "mitigated as "+CONV_NAME[k].toLowerCase()+", whatever it set out as",
      $"Of one ordinary hit, {S(Math.round(csp.share[k]*100))}% arrives as "+
      $"{S(CONV_NAME[k])}.\n\n"+
      $"Conversion happens AFTER flat damage joins the base and BEFORE any\n"+
      $"increase touches it. Each part then pays the increases for what it\n"+
      $"BECAME on top of the ones for how it was BUILT — a swing converted to\n"+
      $"fire is still a swing.\n\n"+
      $"And each part is mitigated on its own: {S(k == "phys"
        ? "this is the only part armour ever stops."
        : k == "chaos" ? "nothing in the game resists chaos."
        : "this part pays "+CONV_NAME[k].toLowerCase()+" resistance and walks past armour.")}"))
    .concat([sRow("Size of the hit", "×"+csp.mul.toFixed(2),
      "what conversion does before any monster sees it",
      $"Above ×1.00 means the increases on what your damage BECAME beat the ones\n"+
      $"on what it was. GAIN AS EXTRA always pushes it up, because it never gives\n"+
      $"anything back.\n\n"+
      $"Yours: ×{S(csp.mul.toFixed(3))} on every hit, before mitigation.")])) : "";
  /* PENETRATION: only when there is any, like the conversion card */
  var penAny = PEN_KEYS.some(el=>penOf(el) > 0);
  var penBlock = penAny ? sBlock("PENETRATION", PEN_KEYS.filter(el=>penOf(el) > 0).map(el=>
    sRow(PEN_NAME[el], pct(penOf(el)), "of their resistance your hits go through",
      $"PENETRATION: a body's {S(PEN_NAME[el].toLowerCase())} resistance counts this much lower against\n"+
      $"your HITS -- never below nothing, never your burns, bleeds or poison,\n"+
      $"never your minions'. Capped at {S(Math.round(PEN_MAX*100))}%.\n\n"+
      $"Against a body resisting 50%, a 10-damage {S(PEN_NAME[el].toLowerCase())} hit lands for "+
      $"{S((10*(1 - Math.max(0, 0.5 - penOf(el)))).toFixed(1))} instead of 5.0."))) : "";
  var resist = sBlock("ELEMENTAL RESISTANCE", [
    sRow("Fire", pct((1-elemTake("fire"))),
      p.curse["resist"]>0 ? $"SUNDERED — cut from {S(pct(p.res["fire"]))}" : "burn damage and duration",
      $"RESISTANCE scales down both the elemental damage of a hit AND the\n"+
      $"strength of the ailment it applies. Capped at {S(Math.round(RES_CAP*100))}%.\n\n"+
      $"Yours: {S(pct(1-elemTake("fire")))} fire.\n"+
      $"A 10-damage fire hit lands for {S((10*elemTake("fire")).toFixed(1))},\n"+
      $"and a burn that would be 3 damage applies as {S((3*elemTake("fire")).toFixed(1))}."),
    sRow("Frost", pct((1-elemTake("frost"))),
      p.curse["resist"]>0 ? $"SUNDERED — cut from {S(pct(p.res["frost"]))}" : "chill stacks applied"),
    sRow("Lightning", pct((1-elemTake("shock"))),
      p.curse["resist"]>0 ? $"SUNDERED — cut from {S(pct(p.res["shock"]))}" : "shock stacks applied"),
    /* CHAOS IS IN THIS CARD AND NOT IN ITS TITLE, because it is not an element
       and the difference is the whole point of the type. */
    sRow("Chaos", pct(Math.min(RES_CAP, Js.Or(p.res["chaos"], 0))),
      "poison — NOT covered by \"every elemental resistance\"",
      $"CHAOS is neither physical nor elemental. Nothing that reads \"to every\n"+
      $"elemental resistance\" touches this number, and no curse that sunders\n"+
      $"your elemental resistances reaches it either.\n\n"+
      $"Poison is chaos damage over time, so this is what stands between you\n"+
      $"and a dose. Yours: {S(pct(Math.min(RES_CAP, Js.Or(p.res["chaos"], 0))))}.\n"+
      $"A poison ticking for 3 a second reaches you as "+
      $"{S((3*chaosTaken()).toFixed(2))}."),
    sRow("Cap", pct(RES_CAP), "nothing is ever fully immune")
  ]);

  var burnNow = burnStacks()>0
    ? $"×{S(burnStacks())} · {S(burnDps().toFixed(2))}/s · {S(burnLeft().toFixed(1))} left"
    : "none";
  var ail = sBlock("AILMENTS", [
    sRow("Burning", burnNow, "damage over time, ignores i-frames",
      $"Every fire that lands on you burns on its own: 3 damage over {S(BURN_DUR)}s, scaled "+
      $"by your fire resistance.\n\nThey STACK — the damage per second is the sum of every "+
      $"fire still alight — there is no ceiling on how many — not one pool stretched thinner.\n\n"+
      $"Right now: {S(burnStacks())} burning for {S(burnDps().toFixed(2))} per second."),
    sRow("Chill", p.chill.stacks>0 ? "×"+Math.round(p.chill.stacks) : "none",
      $"-{S(Math.round((1-chillMul())*100))}% move speed and swing rate"),
    sRow("Shock", p.shock.stacks>0 ? "×"+Math.round(p.shock.stacks) : "none",
      $"+{S(Math.round(shockCritChance()*100))}% chance a hit on you crits for ×1.8"),
    ..ELEMENTS.map(el=> sRow(BRAND_NAME[el],
      player.brand[el]>0 ? "×"+player.brand[el]+" · -"+Math.round(brandLoss(el)*100)+"%" : "none",
      player.brand[el]>0 ? Math.ceil(player.brandT[el])+"s left" : el+" resistance intact",
      $"Every {S(AIL_BREAK)}th {S(el)} stack is spent breaking something, and leaves a "+
      $"{S(BRAND_NAME[el])} mark behind: -{S(Math.round(BRAND_STEP*100))}% {S(el)} resistance for "+
      $"{S(BRAND_T)}s.\n\nThey STACK without limit and each new one refreshes the timer, so "+
      $"resistance can be driven below zero — at -100% you take double.\n\nRight now: "+
      $"{S(Js.Or(player.brand[el], 0))} stack{S(player.brand[el]==1?"":"s")}, "+
      $"{S(el)} resistance effectively {S(Math.round((1-elemTake(el))*100))}%.")),
    sRow("Whose effect is that?", "pale = yours", "deep = theirs",
      $"Anything YOU put on the floor is drawn bleached toward white — your bombs "+
      $"land as pale gold craters, your lightning is pale yellow.\n\n"+
      $"Anything a MONSTER puts down is deep and saturated: ember-red "+
      $"fire, dark amber lightning.\n\nSame hue for the same element, so a firebomb "+
      $"still reads as fire — the brightness tells you whether to stand in it.")
  ]);

  var curseRows = CURSE_KEYS.map(k=>
    sRow(CURSES[k].name.charAt(0)+CURSES[k].name.slice(1).toLowerCase(),
      p.curse[k]>0 ? "-"+Math.round(p.curse[k]*100)+"%" : "none", CURSES[k].tip));
  curseRows.push(sRow("Time left", p.curse["t"]>0 ? p.curse["t"].toFixed(1)+"s" : "—",
    "imp master hexes; they expire together"));
  var curseBlock = sBlock("CURSES", curseRows);

  var run = sBlock("THIS RUN", [
    sRow("Level", S(p.level), $"{S(Math.floor(p.xp))} / {S(p.xpNext)} XP"),
    sRow("Wave", S(wave.n), best>0 ? $"personal best: wave {S(best)}" : ""),
    sRow("Monster level", S(mobRank())+(mobRank()>=MOB_LEVEL_MAX ? " (max)" : ""),
      mobRank()>0 ? $"×{S(mobRankMul().toFixed(2))} health, damage and armour · "+
                    (mobLevelEvery()==1 ? "a level every wave now"
                                         : $"one level every {S(mobLevelEvery())} waves now")
                  : $"starts at wave {S(MOB_LEVEL_FROM)}, then one level every {S(mobLevelEvery(MOB_LEVEL_FROM))} waves",
      $"A monster's LEVEL is separate from the wave it spawned in.\n\n"+
      $"The wave curve is a polynomial, and a polynomial flattens: by wave 50,\n"+
      $"ten more waves bought the roster about a third more health, which against\n"+
      $"a capped hero read as no change at all.\n\n"+
      $"A level COMPOUNDS instead — {S(Math.round(MOB_LEVEL_STEP*100))}% more health, damage and armour each,\n"+
      $"multiplied on top of the wave curve rather than added into it.\n"+
      $"Nothing before wave {S(MOB_LEVEL_FROM)} carries one, so the opening is untouched.\n\n"+
      $"THEY COME FASTER AS THE RUN GOES ON:\n"+
      string.Join("\n", MOB_LEVEL_BANDS.Select((b,i)=>{
        var next = i+1 < MOB_LEVEL_BANDS.Length ? MOB_LEVEL_BANDS[i+1] : null;
        return $"  wave {S(b.from)}{S(next != null?"\u2013"+S(next.from-1):"+")}: "+
               (b.every==1 ? "a level EVERY WAVE" : $"one level every {S(b.every)} waves");
      }))+$"\n\nThe ceiling is level {S(MOB_LEVEL_MAX)}."),
    sRow("Monsters smashed", S(p.kills), ""),
    sRow("Time alive", $"{S(mm)}:{S(ss).PadLeft(2,'0')}", ""),
    sRow("Favourite", favourite.id=="none" ? "none" : favourite.name,
      favourite.id=="none" ? "no bricks weighted" : "its bricks appear more often")
  ], sBar(p.xp/Math.max(1,p.xpNext), "#ffb900"));

  /* --- spells, with the same live numbers the reward screen shows --- */
  var spells = spellSummaries();
  var spellHtml = spells.Count() > 0 ? string.Join("", spells.Select(sp=>{
    var stacks = taken[sp.id];
    var lines = string.Join("", sp.tip.Split("\n").Select(l=>$"<li>{S(esc(l))}</li>"));
    return $"<div class=\"spellcard\">\n      <div class=\"schead\">\n        <svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#d6a6ff\" stroke-width=\"2\"\n          stroke-linecap=\"round\" stroke-linejoin=\"round\">{S(ICONS[sp.icon]??"")}</svg>\n        <h5>{S(esc(sp.name))}</h5>\n        {S(stacks != 0?$"<div class=\"scstack\">×{S(stacks)}</div>":"")}\n      </div>\n      <div class=\"scdmg\">{S(esc(sp.head))}</div>\n      <ul>{S(lines)}</ul>\n    </div>";
  })) : "<div class=\"nospell\">No spells yet — spell bricks come from wave, level and boss rewards.</div>";

  /* --- every upgrade taken, grouped by class --- */
  var byCls = new Dictionary<string, List<string>>();
  foreach (var id in taken.Keys){
    var u = UPGRADES.Find(x=>x.id==id);
    if(u == null) continue;
    if (!byCls.TryGetValue(u.cls, out var bl)) byCls[u.cls] = bl = new();
    bl.Add($"<div class=\"chip\">{S(esc(u.name))} ×{S(taken[id])}</div>");
  }
  var clsOrder = new[]{"OFFENSE","DEFENSE","UTILITY","SPELL"};
  var upHtml = string.Join("", clsOrder.Where(c=>byCls.ContainsKey(c)).Select(c=>
    $"<div class=\"chipgroup\"><span class=\"chiphead\">{S(c)}</span>{S(string.Join("", byCls[c]))}</div>"
  ));
  if (upHtml == "") upHtml = "<div class=\"chipgroup\"><div class=\"chip\">Fresh build — no bricks taken yet</div></div>";

  /* EVERY STAT, GENERATED FROM THE PROBES. The hand-written blocks above carry
     the deep explanations, but they are hand-written — a probe added to the
     comparison could sit there for months without ever reaching this screen.
     This section is the SAME list the reward comparison reads, so the coverage
     cannot drift by construction, and each row carries its own explanation. */
  var probeGroups = new List<(string g, List<string> rows)>();
  foreach (var sp in STAT_PROBES){
    var gi = probeGroups.FindIndex(x=>x.g==sp.g);
    if(gi < 0){ probeGroups.Add((sp.g, new List<string>())); gi = probeGroups.Count-1; }
    double v;
    try { v = sp.get(); } catch { v = double.NaN; }
    var dead = double.IsNaN(v) || !double.IsFinite(v) || (sp.zero && v == 0);
    probeGroups[gi].rows.Add(sRow(esc(sp.k), dead ? "—" : sp.fmt(v), "", statWhy(sp)));
  }
  var allStats = string.Join("", probeGroups.Select(grp => sBlock(esc(grp.g), grp.rows)));

  sheetModeWire();
  if(!sheetNerd){
    document.getElementById("charBody").innerHTML =
      $"<div class=\"seclabel\">STATS &mdash; HOVER ANY ROW</div>\n       <div class=\"sgrid\">{S(simpleStatBlocks())}{S(breachBlock)}</div>\n       <div class=\"seclabel\">SPELLS</div>\n       <div class=\"spellgrid\">{S(spellHtml)}</div>\n       <div class=\"seclabel\">BRICKS TAKEN</div>\n       <div id=\"charChips\" style=\"display:flex;flex-direction:column;gap:10px;align-items:center\">{S(upHtml)}</div>";
    return;
  }
  document.getElementById("charBody").innerHTML =
    $"<div class=\"seclabel\">STATS</div>\n     <div class=\"sgrid\">{S(vitals)}{S(move)}{S(sword)}{S(crit)}{S(power)}{S(conversion)}{S(penBlock)}{S(resist)}{S(ail)}{S(curseBlock)}{S(breachBlock)}{S(loot)}{S(run)}</div>\n     <div class=\"seclabel\">SPELLS</div>\n     <div class=\"spellgrid\">{S(spellHtml)}</div>\n     <div class=\"seclabel\">EVERY STAT &mdash; HOVER ANY ROW</div>\n     <div class=\"sgrid\">{S(allStats)}</div>\n     <div class=\"seclabel\">BRICKS TAKEN</div>\n     <div id=\"charChips\" style=\"display:flex;flex-direction:column;gap:10px;align-items:center\">{S(upHtml)}</div>";

    }
}
