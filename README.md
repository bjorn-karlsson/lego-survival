<div align="center">

# 🧱 BRICKBLADE

### *Smash the waves. Grab the studs. Everything falls apart.*

**A brick-built action RPG that lives in a single HTML file.**

No build step. No bundler. No `node_modules`. Double-click it.

<br>

![The title screen](docs/title.jpg)

<br>

`83 bricks` · `41 monsters` · `8 bosses` · `13 builds` · `50 waves` · `1 file`

</div>

---

## Contents

**Start here** — [Play it](#play-it) · [The loop](#the-loop) · [What's in the box](#whats-in-the-box) · [Controls](#controls)

**The kit** — [Weapons](#weapons) · [Elements & ailments](#elements--ailments)

**Your build** — [The damage model](#the-damage-model-is-path-of-exiles) · [Rarity](#rarity) · [The combination panel](#the-combination-panel) · [Reading your own build](#reading-your-own-build)

**The world** — [Monsters](#monsters) · [Bosses](#the-boss-ladder) · [Drops](#drops) · [Difficulty](#difficulty) · [The world gets colder](#the-world-gets-colder)

**Reference** — [Every buff and debuff](#every-buff-and-debuff) · [Every cap](#every-cap) · [Under the hood](#under-the-hood)

**Between runs** — [The skill tree](#the-skill-tree) · [design notes](docs/PROGRESSION.md)

---

## Play it

```bash
git clone https://github.com/bjorn-karlsson/lego-survival.git
# then open lego-survival/lego-survival.html — double-click it, or drag it onto a browser
```

That is the whole install. One 500 KB HTML file with a single `<script>` in it. The only
thing it reaches for over the network is a Google Fonts stylesheet — block it and the game
runs exactly the same in fallback type.

---

## The loop

You are a minifig in a field of studs. Waves of brick monsters walk at you and come apart
when hit. Each run introduces the roster in a different order — banded by era, so the
opening wave is a coin-toss between four monsters and never a wave-30 horror. Every wave,
every level and every chest hands you a **brick** — one of 83 upgrades — and the bricks
compound into a build. Waves open with a breather so you can heal and read what you took,
and <kbd>F</kbd> skips it. Survive long enough and the ground is a carpet of studs, the
screen is a storm of lightning, and a three-storey skeleton is throwing its own ribcage
at you.

![A wave-22 fight](docs/fight.jpg)

---

## What's in the box

<table>
<tr><td width="33%" valign="top">

### ⚔️ Four spells, your pick

**A run may cast four spells and no more.** Once you have four, the bricks that would hand
you a fifth stop being offered and everything that deepens the four you have keeps coming.

`Guardian Brick` · `Brick Blaster`
`Storm Brick` · `Bladestorm`
`Blade Vortex` · `Block Freeze`
`Bomb Volley` · **`Brickbane`**

Pick a **favourite** at the menu — including `Sword & Steel` or `Tough Bricks`, which spend
no slot at all — and its bricks turn up more often, plus a starting kit to match. The panels
either side of the title box show [what the pairing actually opens
with](#the-combination-panel).

`Runic Tempo` is the one brick that speeds up **all four at once** — see
[ability floors](#ability-floors).

</td><td width="33%" valign="top">

### 🗡️ Pick a weapon

`SWORD` · `AXE` · `MACE` · `STAFF`

Your weapon is a **table row**, not a special case in the code — base damage, swing time,
reach, crit, bleed, and what it is worth defensively. The art follows it: you hold what you
picked, you swing it, and your spells throw it.

The **staff does not swing at all**. It casts fireballs, and it trades the whole melee kit
for the elemental one.

Full numbers in [**Weapons**](#weapons), and hover one on the title screen for the live
comparison against the sword.

### 🩸 Elements & ailments

Fire, frost and lightning all stack, on you and on them, and **the fifth stack breaks
something** — you freeze, they turn BURNT, everything you own hits a shocked body harder.

**Five stacks is a threshold for all three.** Fire brands **BURNT** (+25% MORE fire, ×4),
lightning brands **SHOCKED** (+7% MORE of *everything*, ×3) and frost brands **BRITTLE**
(−12% frost resistance, ×4). Shock never had a brand at all: five stacks was the end of the
line and every further bolt did nothing. Unlike a fire — whose stacks are *fuel* — a shock
brand does **not** spend the stacks, because shock's stacks **are** the multiplier and
spending them would drop a fully shocked body back to nothing the moment your fifth bolt
landed. Stacks and marks are separate MORE multipliers on the body, so a fully shocked,
fully branded monster takes **×1.65** from everything.

**Every ailment lasts longer.** Four seconds was short enough that a stack laid by one source
had usually expired before a second source arrived — which is the whole reason to own two.
Chill and shock **4s → 7s**, burning **6s → 9s**, BURNT **10s → 16s**, bleed **8s → 11s**,
poison **5s → 8s**.

**Poison drains; it does not drip.** Half a second between bites meant a poisoned body's
health bar came down in visible steps with a number over each one, which reads as a series of
little hits rather than as rot. It comes off **thirty times a second** now, silently — no
number, no flash, no shove, just a bar that slides and green coming off the body. The rate is
unchanged; only the grain is. And it drains by *elapsed time*, not by a fixed slice, so a slow
frame takes exactly as much as three fast ones.

Curses, poison and bleed run alongside on their own pools. See
[**Elements & ailments**](#elements--ailments).

</td><td width="33%" valign="top">

### 👑 Bosses every five waves

Five named bosses rotate through waves 5, 15, 25…

Every **tenth** wave is an ULTRA: `OMEGA BRICKTHANE`, `MEGA BONE BARON`, `ULTRA LAVABRICK`
— each with its own signature (nova, bone spiral, meteor), a health bar across the top of
the screen, and a guaranteed chest.

**Wave 50 is the grand raid**, and every boss in the game wakes up at once.

In a hurry? **Hold F mid-fight** to call the next wave down early and fight both at once.

</td></tr>
</table>

![The MEGA BONE BARON](docs/boss.jpg)

---

## Weapons

Your weapon is a **table row**, not a special case in the code. It sets the physical side
of the hero — base damage, swing time, reach, how often and how hard it crits, and how
readily a hit opens a wound — and every brick you own scales it from there.

**What it hits for**

| | base | attack | reach | crit | crit mult | bleed | spin |
|---|--:|--:|--:|--:|--:|--:|--:|
| **SWORD** | 1.00 | 0.34s | 56 | 5% | ×1.50 | ×1.0 | ×1.40 |
| **AXE** | 1.15 | 0.40s | 54 | **10%** | **×2.10** | ×0.45 | ×1.55 |
| **MACE** | **1.70** | 0.54s | *slam* | 3% | ×1.20 | **×1.90** | — |
| **STAFF** | 1.70 | 0.68s | *bolt* | 6% | ×1.60 | — | — |

**Two of the four do not swing at all.** The staff casts a fireball; the mace opens a
**ground slam**. Both trade the whole melee kit — reach, the spin combo, the whirl — for an
attack of their own, and both are offered the sword's bricks no more than a staff is
offered the axe's.

**What it keeps you alive with**

| | less damage taken | spell suppression | lifesteal | resistance |
|---|--:|--:|--:|--:|
| **SWORD** | 40% | 25% | 15% of swings, for 6.0% | — |
| **AXE** | 45% | 25% | **20% of swings, for 9.0%** | — |
| **MACE** | **60%** | **30%** | 10% of swings, for 3.5% | — |
| **STAFF** | — | — | — | **+8% to all three** |

An axe crits twice as often for twice as much, but a clean cut barely bleeds. A mace hits
hardest and opens the worst wounds — 95% bleed on a critical against the sword's 50% — and
is slow and crits rarely.

The axe is **double-bitted** — a crescent to either side of the haft, edges facing opposite
ways — and the hero shoulders it on a lean rather than dead upright, because straight up
buries the inner bit behind the minifig's head.

**Melee is armoured by the fact that it has to be there.** A staff never has to stand where
the monsters are; a sword does, and the numbers were written as if it did not. So the three
melee rows carry their own defence, the mirror of the cast speed the staff gets for standing
back:

- **Less damage taken** — 40 / 45 / **60%**. It is a LESS multiplier applied *last*, after
  armour and after suppression, so it takes its share of whatever got through rather than
  competing with armour for the same pool. It reaches poison, bleed and burn as readily as
  it reaches a hit.
- **Spell suppression** — +25%, or **+30%** on a mace, on top of whatever you have bought.
- **Lifesteal** — see below.

The mace is the one that never leaves contact, so it carries the most of both.

**The staff gets +8% to every resistance** instead. It stands in the fire it starts, and it
is the one weapon that cannot answer a burning floor by walking out of it.

### ⛰️ The ground slam — *what a mace does instead*

A mace hero does not swing. They drive the head into the floor and a **fault** opens in
front of them: lines of stone teeth tearing up out of the ground in a fan, running outward
from the impact. It is the boss slam you have been dodging all run, pointed the other way.

Every tooth is **physical** — it bleeds, it leeches, and it reads every increase a swing
would. It **takes the scenery with it** too: chests, crates and trees inside the fault take
one hit per slam, the same as one swing would, so a wide fault opens every crate it crosses.
It does **not** deflect — a slam is the floor coming up, and nothing about that turns an
arrow aside.

**What it is worth.** Each tooth is **30.7% of a full swing**, and **one body may be bitten
by at most three teeth of the same slam** — so a body held dead centre in the fault takes
**0.92 of one sword swing** per slam, and everything else in the fan takes the same. It
opened at twice that, which made an AoE basic attack out-damage the single-target one it
replaced; at this share the mace trades single-target for coverage instead of getting both.

Read off a fresh hero with no favourite, which is what the character sheet's
**Sustained DPS** row shows you:

| | single target | |
|---|--:|---|
| **SWORD** | **5.38** /s | one body, kept in reach |
| **AXE** | **5.69** /s | one body, kept in reach |
| **MACE** | **3.96** /s | one body held in the fault — *and everything else in it* |

About three-quarters of a sword's single target, spread across everything in a 99° fan.
That is the trade.

| | opens at | ceiling |
|---|--:|--:|
| **Fault width** | 99° | **180°** — or a full **360°** ring |
| **Fault reach** | 200 px | **430 px** |
| **Fault speed** | 620 px/s | **1400 px/s** |

Speed is a real stat, not decoration: a slow fault means the far teeth arrive *after* the
pack has walked off them. And a wider fault throws **more** teeth rather than the same
teeth further apart — the fan never thins as it opens.

Four bricks and a legendary, offered to nobody else:

| | |
|---|---|
| **Fault Hammer** | +FLAT damage to every tooth |
| **Wider Fault** | the fan opens wider |
| **Deep Fault** | increased range |
| **Fault Line** | increased speed |
| 🟧 **Slam of the Elements** | below |

**🟧 SLAM OF THE ELEMENTS.** Every slam takes the next element in turn — 🔥 fire, ⚡
lightning, ❄️ frost — and converts **half of every tooth** into it. The converted half pays
the monster's resistance and leaves that element's ailment behind: **burning**, **shock**,
**chill**. The physical half is untouched, so the slam still opens wounds while it does it.
The stone comes up in that element's colour, with a seam of it running up each tooth, so
which one is loaded is something you read off the floor rather than off a counter.

**Taken twice, the fault closes into a ring.** The 180° ceiling lifts to a full **360°**
and the stone comes up all around you.

`Earthshaker` is its favourite, and it is not offered to a weapon that swings — the same
way `Sword & Steel` is no longer offered to one that does not.

### 🩸 Lifesteal

Every swing has a **chance** to drink a **share** of the physical damage it just landed. Both
halves are stats, both can be bought, and the axe drinks most:

| | chance to drink | share of the hit | average per swing |
|---|--:|--:|--:|
| **AXE** | 20% | 9.0% | **1.80%** |
| **SWORD** | 15% | 6.0% | 0.90% |
| **MACE** | 10% | 3.5% | 0.35% |

- **Vampiric Edge** raises the share, to a ceiling of **25%**.
- **Thirsting Grip** raises the chance, to a ceiling of **60%**.

It leeches off what **actually landed** — after armour, after resistance — so a swing that
barely got through barely heals. Only your own swing and the whirl it becomes drink; a spell
that happens to deal physical damage does not, and neither does a bleed tick.

And no single swing may return more than **3% of your maximum hearts**, which is the cap
that matters: a share of a wave-50 crit is otherwise an entire health bar. A staff never
drinks at all.

**The staff does not swing at all.** It is the first *caster* weapon: its basic attack is a
**fireball** that bursts for 46 and sets what it touches alight. It gives up the entire melee
kit — reach, the spin combo, bleed — and gets **+25% increased spell damage, +30% increased
elemental damage and +20% increased cast speed** for carrying it. Physical bricks are not
offered to a staff hero at all; the elemental ones are not offered to anyone else.

**Fire burns.** Every bolt lays a stack that ticks for 30% of the hit a second over 9s, and
they all burn at once. **Five stacks is a threshold, not a counter.** From there, every fire
that lands has a **30% chance** to brand the body **BURNT** — **+5% per stack above five**,
so a deeper fire brands sooner — and branding costs **five stacks, not the whole fire**.
A BURNT body takes **25% MORE fire**; marks stack to four, each its own multiplier, so a
four-mark body takes **×2.44**. Burning is damage over time, so it reads the same `Rotbrick`
pool poison and bleed do.

> This used to fire on the stack that **overflowed the cap**, which made `Ember Scatter` —
> whose whole job is to deepen the well to 10 stacks and then 20 — quietly a *downgrade* for
> the thing it fed: at rank 2 you needed **21 fires** to see a single mark, and the brand
> then threw **19 of your 20 stacks away**. Five is the threshold at every depth now, and a
> brand costs five.

Five bricks come with it: `Ember Core` (flat elemental), `Elemental Focus` (increased
elemental), `Pyromancy` (increased fire), `Wildfire` (a wider burst), and the legendary
**`Ember Scatter`**.

**Ember Scatter** is the fireball's payoff. Hitting a body that is **already burning**
knocks **2 embers** off it — **5** once upgraded — and each one sets another monster alight
for 45% of the hit that threw it. With nothing in reach they **loop back** into the body
that threw them, so it is never dead weight against a boss on its own. It also deepens the
well: fire piles **10 stacks** instead of five, **20** upgraded, so BURNT comes round rarer
but the burn underneath it is four times the size.

**An ember goes looking for something that is not on fire.** Nearest-first meant a chain
reaction fed itself: in a packed crowd every ember piled onto whatever was closest, which
was almost always a body already alight. The preference is one comparison, expressed in
**pixels of detour an ember will fly**:

| | cost |
|---|--:|
| distance to the body | 1 px per px |
| the body is **already burning** | **+460** — so an unburnt body most of the way across the seek radius still beats a burning one at your feet |
| each ember **already sent** to that body this volley | **+240** — so a volley of five lights five bodies, not five-times-one |
| further than **520 px** | not considered at all — an ember never crosses the map for fresh meat |

A burning body is still a target when it is the only thing standing, so an ember is never
wasted; and the mid-flight retarget asks the same question, so an ember that outlives its
mark does not fall back to nearest-only halfway there.

They no longer leave in an even fan, either. Evenly spaced spokes plus a nudge read as a
machine part firing; embers now come off in **any direction, at any speed between 28% and
95%**, and hang in the air for **different lengths of time** before they start hunting.

A chain travels **3 bodies deep** and one body can only throw embers every 0.25s, so a
packed crowd lights up and then settles rather than running away.

**`Block Freeze` is the staff's second element.** It deals **frost damage** and lays a
**chill stack** — each one drags a body 13% slower, and **the fifth freezes it solid**.

It used to be a wall of frost that arrived rarely, reached most of the screen and dropped
its whole hit in one lump. It is now a **short, frequent pulse**: the opening interval went
**10s → 5.5s** (floor 3.5s → 2.2s), the reach **190 → 128** (cap 420 → 290) and the base hit
**1.6 → 0.85**. Something you feel every few seconds rather than a bomb you wait for.

**And the ring is the spell.** The frost animation expanded at a fixed 420 px/s for a fixed
half-second, which reaches the edge at exactly one radius and misses at every other — a
small pulse drew a ring **three times** the size of the spell, and a maxed one fell **60 px
short** of what it was freezing. Speed is derived *from* the radius now, so the frost always
arrives at the edge, and always at the same moment.

**Frozen leaves a body BRITTLE.** The fifth chill stack still freezes a monster solid — and
now leaves a mark that takes **12% frost resistance off it**, four marks deep. The mark lands
on a **boss** too: a boss cannot be held still, but it can be made easy to freeze, which is
the first thing frost has ever done to one that lasts.

**Every stack of the spell deepens the CHILL, and chill has a ceiling.** Extra stacks used
to buy radius and duration with **no cap on either**: forty-six of them read **1654 px** and
**63 seconds** on the test bench, which is not a spell, it is an oversight. They lay an extra
chill stack now instead, capped at **4** — five freezes a body solid, so one pulse can never
do it alone however you got there. The card stops being offered once it can buy nothing more,
and it prints its own ceiling, so it can never promise a stack it will not hand over.

**It has its own rarity ladder, and it starts at RARE.** Every other spell counts in
projectiles and can afford eight of them; this one counts in chill stacks, so an *uncommon*
handing over two was three quarters of a permanent freeze on one card. A **rare gives +1**,
an **epic +2**, and it is never offered below rare.

| | |
|---|---|
| **`Killing Frost`** | flat frost damage, added to the base *before* every increase |
| **`Absolute Zero`** | every caught foe frozen SOLID for 4.5s **and left BRITTLE** — and anything that **dies frozen SHATTERS**, throwing your own pulse out of the corpse |

`Deepening Winter` is gone: it did the job the spell's own stacks now do, which made picking
the spell up twice and picking it up once the same card in two coats.

**A frozen corpse throws your pulse.** Absolute Zero's payoff is no longer just a stun — a
body that dies frozen shatters for **75% of your own radius**, with the same frost and the
same chill, and what it died **BRITTLE** with is the multiplier: **+50% a mark, four deep**,
so a four-mark corpse hits for **2×** a one-mark corpse. A shatter that kills sets off
another, twice deep and no further. The frost build's own debuff is what makes its kills
worth watching.

The freeze also **holds longer** — the chill 2.5s → **4.5s**, and Absolute Zero 3.0s →
**4.5s** — because a pulse this small and this frequent has to make what it lands stick.

It is only offered to a caster, the way the melee spells are only offered to a melee weapon.

**Nothing is offered to a weapon that cannot use it — and the card says so.** Every brick
whose requirement is narrower than "any weapon" prints the weapons it serves in a small row
under its text, read straight off its own gate so the label can never drift from the rule.

A staff hero is never shown `Sharpened Blade`, `Longsword`, `Blade Mastery`, the legendary
`Blademaster`, the bleed bricks, or **either blade spell and its whole tree** — Bladestorm
and Blade Vortex are for a hero who carries a blade. A melee hero is never shown the frost
tree or the elemental one. The favourites gate the same way: `Sword & Steel`, `Bladestorm`
and `Blade Vortex` do not appear for a caster, and the new **`Elemental Arts`** and
`Block Freeze` favourites do not appear for a melee weapon.

**Bladestorm and Blade Vortex stay physical** and open wounds on their own merits rather
than on the weapon's. A caster's cast interval has its own floor, **0.34s** against melee's
0.10s, because a stream of fireballs is a hosepipe rather than a wizard. The fireball breaks
scenery like any swing — it opens chests and clears crates — but it is the hero's own
projectile and carries no owner, so the deflect path, which only ever touches what monsters
throw, can never bat one back at you.

**Ability names never change.** A mace hero still takes `Bladestorm`, `Blade Vortex` and
`Sword & Steel` — the names are the abilities', not the weapon's. `Sharpened Blade` grants
**flat physical damage** and `Blade Mastery` grants **increased physical damage**, so every
brick works with every weapon and no build is locked to one.

**A weapon's attack speed is attack speed.** The staff's **+20% increased cast speed** is
not a property of its fireball — it sums into the cooldown pool of *every* spell you own, the
way an increase is supposed to. A staff hero's Brick Blaster fires every **2.00s** against a
sword hero's 2.40s, its Storm Brick every 1.83s against 2.20s, Bomb Volley 5.83s against
7.00s, Brickbane 3.50s against 4.20s, and the guardian ring turns at **3.12 rad/s** against
2.60. One pool per spell, the weapon's contribution added to each, so nothing compounds and
nothing is missed.

That change exposed a floor that had been sitting almost on its own base: the blaster's was
**2.00s against a 2.40s base**, 17% of headroom — one brick's worth for a sword hero, and
none at all for a staff, whose cast speed landed it exactly on the floor from the first wave
and made `Rapid Blaster` a card that could never be offered. The floor is **1.20s** now,
which gives the line the same 100% of room the storm brick has (2.20 → 1.00) and is still
nowhere near a machine gun with a volley that size.

**The art follows the weapon too.** You hold what you picked, you swing it, and your spells
throw it: Bladestorm hurls axes for an axe hero and maces for a mace one, and Blade Vortex
orbits the same. Each weapon names a *held* art and a *thrown* art, so the staff can carry
its own stone-headed shaft while still throwing swords until its own spellwork exists.
Hover a weapon on the title screen for the full comparison against the sword.

---

## Elements & ailments

Fire, frost and lightning each stack. **The fifth stack breaks something**: burning
becomes a pool of damage over time, chill freezes you solid, shock leaves every hit
against you critting.

**All three go the other way too.** Fire burns a monster, frost chills it, and lightning
now **SHOCKS** it: a shocked body takes **+7% damage per stack, five deep — +35% from
everything you own**, not just from lightning. It is **drawn**, too — arcs crawl the
silhouette rather than pips sitting beside it, so a shocked body reads as shocked from
across the field while it is moving, and the fifth stack sparks off it. It is a multiplier on the *body*, so it sits
outside the hero's own damage ceiling and it makes every other source in your build land
harder. That is what the storm brick is for now.

Each element also has **its own increase**: `Pyromancy` (fire), `Cryomancy` (frost) and
`Conduction` (lightning), alongside the global `Elemental Focus`. Each is offered only to a
hero who owns the thing it is for, so a fire caster is never handed a lightning card it
cannot use. They **add into the elemental pool**; they never multiply it.

Imp masters lay **curses** on top — SUNDERED, LEADEN, BRITTLE, WITHERED — that strip
resistance, slow your swing, thin your armour and choke your regeneration.

**The Storm Brick is LIGHTNING.** It used to be a plain spell hit, which meant a caster's
whole elemental tree did nothing for the one spell visibly made of lightning. It reads the
**flat elemental** pool the way the fireball does, and both the **spell** and **elemental**
increases apply — so `Elemental Focus`, `Conduction`, `Orb Mastery` and `Ember Core` all
move it, while `Pyromancy` correctly does not. `Conductor` buys its chance to shock.

Its interval is a flat **2.00s** and it has no rate brick of its own any more — that slot
is `Conductor`. The way to make the storm come round faster is to **own more bricks**: the
stagger between them is no longer a fixed 0.2s but **the interval divided by the bank**, so
five bricks fire 0.40s apart, for ever. A bank used to empty itself in the first second of
every two and then stand idle; it reads as a rhythm now rather than a volley.

**The blightspitter** is the monster side of the same idea. Every other ranged monster asks
you to dodge one thing once; this one asks you to give up **ground**. The gobbet it lobs is
nearly harmless — priced at 35% of its damage, and then cut again by armour — but it leaves
a **pool of blight** that bites every 0.55s for as long as you stand in it, for 5.5 seconds.
It leads your movement a little, so walking in a straight line does not beat it, and a few
of them working together can close a corridor and push you back into the pack. It is
deliberately fragile and takes **20% more fire**: the answer is to kill it, not to out-heal
it. The pool **poisons** rather than hitting — armour never touches it, and it goes on
working after you step off, which is what makes giving up the ground actually cost you.

**Brickbane** turns it around: a wedge of poison gas that hits once and then keeps eating
whatever it touched. Poison **stacks nine deep on a monster and twelve on a boss** — count
the green beads orbiting a body. Its own brick is **rare+** and buys *doses*, not damage:
one touch of the gas lays up to **3 stacks at once**, so a full load is three casts instead
of nine.

The legendary, **Spore Burst**, is contagion rather than a bomb: a poisoned corpse comes
apart and hands **every stack it was carrying** to everything within 130 (210 upgraded),
at 70% of the rate. It deals no blast damage at all — nothing dies the instant the cloud
touches it. What spreads is the rot, and a body that dies of it bursts in turn, so a
packed pull comes apart in a slow green wave you can watch travel.

It fires **80%** of the time at the first rank and **every single time** at the second, and
each rank also **doubles how much rot a body can hold** — 9 → 18 → 36 on a monster, 12 → 24
→ 48 on a boss. Any kill sets it off, not just one the gas made: shoot a poisoned monster
and the corpse still bursts.

---

## The damage model is Path of Exile's

Every stat in the game runs through one pool, in one order, every time:

```
(base + all flat added) × (1 + sum of every increase) × each MORE multiplier, one at a time
```

Three consequences, and they are the whole reason the model is worth having:

- A MORE multiplier you picked up at wave 12 keeps multiplying the flat health you add at
  wave 40. **Order never matters** — twenty-seven bricks taken in reverse produce
  byte-identical stats.
- Increases are **additive** with each other. MOREs never are.
- Cooldowns use the mirror of it — `max(floor, base / (1 + increased))` — so attack speed
  has a real ceiling instead of an asymptote.

Every **FLAT damage** brick in the game reads off one shared ladder, so a common is never
a rare wearing a different colour:

| | common | uncommon | rare | epic | legendary |
|---|--:|--:|--:|--:|--:|
| Sharpened Blade, Heavy Orbit, Blaster Power,<br>Storm Surge, Storm Edge, Big Kaboom, Whirling Edge | +0.5 | +1 | +3 | +5 | +8 |

Every **spell brick that grants stacks** reads off a second shared ladder, for the same
reason — rare and epic used to land in the same bucket, so an epic roll of a spell was a
disappointment every time:

| | common | uncommon | rare | epic | legendary |
|---|--:|--:|--:|--:|--:|
| Guardian Brick, Brick Blaster, Storm Brick,<br>Bladestorm, Blade Vortex, Block Freeze, Bomb Volley | +1 | +2 | +3 | +5 | +8 |

**It is checked rather than asserted.** The audit takes twenty-seven bricks three times
over — forward, reverse and shuffled — and compares all **85** stats the game exposes. Every
one must come out byte-identical. It then proves that:

- +100% and +200% increased land on ×2 and ×3, never ×4
- two ×2 MOREs land on ×4
- every ceiling sits on the *result* rather than on the pool

Three deliberately broken builds — a compounding increase, a capped flat pool, an increase
smuggled into a MORE — are each caught by it.

Bladestorm counts in **swords**, so an epic roll is +40 of them, and the ceiling is on
swords in the air — **150** — rather than on ranks.

**Every telegraph is the shape of its own hit test.** A ground circle drawn squashed
reaches a third further north and south than the picture shows, so anything whose hit test
is `dist(a,b) < r` — boss shockwaves, the slam, the lingering vortex, a warden's healing
aura — is drawn as a **true circle**. The boss whirlwind goes further: only the swept arc
of the blade itself hits, not the disc it sweeps, so standing inside the circle between
passes is genuinely safe.

**And a telegraph you cannot read in time is not a telegraph.** The whirlwind braces for a
full **2.4 seconds** before the blade moves, and it *casts* in the last beat of that — the
roar, the shake and the ring go out while the mace is still cocked, so what you hear is the
thing starting rather than the thing already having hit you. Not one frame of the spin is
live before the cast has landed.

**Increased damage over time** is one pool too, not one per ailment. `Rotbrick` sits in the
DAMAGE section of the bench, not under any spell, and every lingering damage you inflict
reads it — Brickbane's poison is simply the first thing that qualifies.

**A ceiling belongs on the finished number, never on a pool that feeds it.** Flat armour,
resistance, regeneration, crit multiplier, reach and suppression all keep counting past
their limit; the cap lands after the increases and the MOREs have had their say. Capping a
pool instead makes an increase quietly worthless the moment the flat pool fills — the exact
trap this model exists to avoid. Armour is the clearest case: 520 flat carrying +200%
increased is **1,560**, and *that* is the wall.

No card writes a derived number. Cards add to pools; a single `syncStats()` rebuilds
everything from base. That is why the character sheet can show you the arithmetic:

![The character sheet](docs/sheet.jpg)

And why every reward can show you exactly what it would change, spell by spell, before you
take it — grey for now, green for better, red for worse:

![The reward comparison](docs/compare.jpg)

---

## Rarity

Five tiers. Measured across 400,000 rolls per cell on the default difficulty:

| Roll source | ⬜ common | 🟩 uncommon | 🟦 rare | 🟪 epic | 🟧 legendary |
|---|--:|--:|--:|--:|--:|
| **Wave end** | 57.8% | 25.9% | 12.0% | 4.0% | **0.44%** |
| **Level up** | 49.7% | 29.7% | 15.0% | 5.0% | **0.56%** |
| **Chest** | 29.7% | 33.6% | 25.7% | 9.9% | **1.11%** |
| **Boss chest** | — | 21.3% | 48.5% | 27.2% | **3.05%** |

A legendary roll is all-or-nothing: it is spent on a legendary-gated brick or nothing at
all, and no lesser roll can ever reach one. A live **fortune brick** is rarity find as well
as a stud multiplier — up to +25% toward the good tiers.

### What a roll is allowed to hand you

**One card leans on your favourite, not all three.** Weighting every slot meant a
favourite's own bricks crowded the whole screen — pick `Elemental Arts` and all three
filled with elemental increases, so a new spell almost never got a look in. The first card
drawn is the weighted one at ×3.2; the other two are drawn flat. Two-or-more favourite
cards on a screen fell from 40% of rolls to 18%, and spells got their slot back.

**A new spell wears a blue `NEW` ribbon**, tilted across the top corner, so a card that
costs you a slot never looks like a card that deepens what you already cast.

**A chest cannot hand you a spell at all.** It is a reward for exploring, not a way to
reroll your build: chests draw from the pool with every spell-granting brick filtered out,
so what they give you always deepens something you already own. Level-ups and wave rewards
still offer the spells themselves.

---

## The boss ladder

A boss every fifth wave, an ULTRA every tenth, and the grand raid at fifty. Spawned for
real and read off the live body — and since a boss rolls its own **traits**, every row is
the mean of twenty of them, which is what you actually meet rather than one run's dice:

| Wave | | Boss | Mob lv | Health | Armour | Hit | Spell suppression |
|--:|---|---|--:|--:|--:|--:|--:|
| 5 | boss | VON BRICKTHANE | 1 | 541 | 49 | 6 | 17% |
| 10 | **ULTRA** | OMEGA BRICKTHANE | 2 | 4,640 | 224 | 30 | 39% |
| 15 | boss | LORD LAVABRICK | 3 | 2,051 | 209 | 29 | 21% |
| 20 | **ULTRA** | MEGA BONE BARON | 4 | 12,219 | 944 | 98 | **68%** |
| 25 | boss | THE DREAD ROOST | 5 | 4,275 | 489 | 62 | 23% |
| 30 | **ULTRA** | ULTRA LAVABRICK | 7 | 19,071 | 829 | 174 | 44% |
| 35 | boss | THE BONE BARON | 9 | 4,334 | 952 | 99 | 51% |
| 40 | **ULTRA** | OMEGA BRICKTHANE | 12 | 47,809 | 1,370 | 461 | 39% |
| 50 | **RAID** | every boss at once | 22 | — | — | — | — |

The **Mob lv** column is the compounding monster level from
[They have a level](#they-have-a-level) — it is most of why the wave-40 ULTRA is four times
the wave-30 one.

Spell suppression is the boss's own — a chance to halve any spell that lands on it. The
skeletons have the most of it, which is what the bones were always for. The **MEGA BONE
BARON** does not only throw bones: it hexes you with a fan of curse bolts, and it blesses
its whole court — and itself — with armour, damage and swing rate.

**No two fights are the same.** Every boss rolls traits when it wakes — one normally, two
from wave ten, three for an ULTRA — drawn from `SWIFT`, `IRONCLAD`, `BRUTAL`, `VITAL`,
`WARDED`, `VENOMOUS`, `RESTLESS`, `LEGION` and `THORNED`. They are printed on its health
bar, so the LORD LAVABRICK you meet this run is not the one you learned last run.

---

## Drops

Every prize is rolled **per monster**, so a wave of a hundred bodies is a hundred rolls.
An **elite rolls every line eight times over**.

| | Per monster | Per elite | What it does |
|---|--:|--:|---|
| ❤️ Heart | 1.00% | 8.0% | ×1.5 regeneration for 30s — **stacks five deep** |
| 🧲 Magnet | 0.34% | 2.7% | hoovers up every **stud** on the field for 30s |
| 🍀 Fortune | 0.34% | 2.7% | ×2 studs and XP, and up to +25% rarity find |
| 📦 Chest | 0.34% | 2.7% | a reward roll on the chest table |
| 💢 Rage | 0.08% | 0.64% | ×2 damage, a MORE multiplier, for 30s |
| 🛡️ Ward | 0.08% | 0.64% | +40 armour and +12% all resistance per stack, to ×2 |
| 💛 Golden heart | 0.11% | 0.88% | **+3 maximum hearts, permanently** |
| ❓ Mystery chest | 0.06% | 0.48% | contents rolled when it breaks — see below |

**A boss always leaves a heart, a magnet and a chest.** On top of that it rolls 28% for a
fortune and for a golden heart (at 0.6× that), and 12% each for rage and ward — the two
that take you straight to a cap stay scarce even here.

**Generous Smith** *increases* the heart chance rather than adding to it — it caps at
**+200% increased**, which is ×3 the base, or 3.00% a monster.

### The mystery chest

Rarer than an epic reward roll and commoner than a legendary one — about **3 a run**,
against 20 ordinary chests. It wears no tier: it cycles every colour on the ladder with a
`?` on the lid, and what is inside is rolled at the moment it comes apart. Measured over
200,000 rolls:

| ⬜ common | 🟩 uncommon | 🟦 rare | 🟪 epic | 🟧 legendary |
|--:|--:|--:|--:|--:|
| — | 20.9% | 33.3% | 35.2% | **10.7%** |

One in six bosses leaves one as well.

### What that actually comes to

Measured over 60 runs of a full 40-wave game — every wave composed for real, every body
rolled, boss spoils included:

| Prizes per wave | waves 1–10 | 11–20 | 21–30 | 31–40 | whole run |
|---|--:|--:|--:|--:|--:|
| Bodies in the wave | 53 | 92 | 102 | 125 | 93 |
| ❓ Mystery chest | 0.04 | 0.08 | 0.09 | 0.11 | **0.08** |
| ❤️ Heart | 0.69 | 1.13 | 1.25 | 1.39 | **1.13** |
| 📦 Chest | 0.34 | 0.50 | 0.60 | 0.65 | **0.52** |
| 🧲 Magnet | 0.39 | 0.52 | 0.53 | 0.64 | **0.51** |
| 🍀 Fortune | 0.27 | 0.38 | 0.39 | 0.47 | **0.36** |
| 💛 Golden heart | 0.10 | 0.14 | 0.13 | 0.18 | **0.14** |
| 💢 Rage | 0.07 | 0.08 | 0.12 | 0.14 | **0.10** |
| 🛡️ Ward | 0.06 | 0.10 | 0.09 | 0.15 | **0.10** |

Across the whole forty waves that is roughly **45 hearts, 21 chests, 20 magnets, 14
fortunes, 6 golden hearts, and four each of rage and ward.** The two doubling bricks are
things that happen to a run, not things it runs on.

A magnet pulls **studs only**. Every prize in that table is something you walk to — and
nothing in it can land where you cannot reach it: studs, prizes and chests are all shoved
back onto walkable floor the moment they touch down, so a lake or a boulder never eats one.

**The floor has a ceiling.** A wave of 130 bodies rolls 130 times, so late waves used to
carpet the ground whatever the odds said. A prize that would be the fourth of its kind
already lying around simply does not appear: **3 of each buff, 10 hearts, 5 chests**. Pick
them up and the tap opens again.

---

## The combination panel

There are four weapons and thirteen favourites, and the interesting question is never what
either one does on its own — it is what the pair opens with. *What if I put Brickbane on a
staff? What does a mace do to Sword & Steel?* The panel on the right-hand edge of the title
screen answers it before you commit, and it answers it by **playing the combination**, not
by describing it.

Picking a weapon or a favourite stands up a **throwaway hero** from `newPlayer()`, puts the
weapon in its hands, applies the favourite's starting kit, runs `syncStats()`, and then reads
the finished numbers off **the same functions the game itself plays with** — `physDamage()`,
`boltDamage()`, `swordDps()`, `critChance()`, `armourValue()`. Nothing in the panel is
estimated, restated or maintained by hand, so it cannot drift from the game the way a written
table would. The live hero is put back untouched afterwards.

Each row carries its difference from **running that same weapon with no favourite at all**,
so the favourite's contribution is never guesswork — and the trade is honest in both
directions. `No Favourite` is not nothing: it hands you a heart, 10 armour, +5% damage and
+4% move speed as a consolation prize, which is why committing to Brickbane reads as
**−14% hearts** and **−100% armour** next to it. That is the actual price of the spell.

**Two panels, not one.** The numbers and the brick list answer different questions — *what
do I open with* and *what will I be offered* — and stacking them in one column made a panel
taller than a laptop window, which is how it grew a scrollbar and started clipping. They sit
either side of the red box now: the stats, the run's **ceilings** and every ability's
**floor** on the left, the bricks on the right. The title screen also scrolls when it has to
and stops centring the moment centring would push the start button off the top, which is the
flex overflow trap that was cutting the box in half on a short window.

The panel also lists **every brick the favourite weights toward you**, shown at RARE, with
each description rendered against that same previewed hero — so Brickbane's `Choking Spread`
reads *"widen the wedge 90° → 159°"*, starting from the 90° the favourite already gave you,
rather than from the 35° a hero who owns no Brickbane would have.

**A brick is judged on the hero the favourite builds.** This matters more than it sounds.
Every Brickbane brick requires that you *own* Brickbane, which the favourite hands you — ask
that gate on the blank hero standing in the menu and all five read as locked, and the panel
blames the weapon for something the weapon had nothing to do with. So the gate is asked of
the previewed hero. When a brick still says no, the same question is put to the **other three
weapons**: a yes anywhere else means the weapon is genuinely the blocker and the brick is
greyed out with *"never offered to a staff"*; a no everywhere means it is simply a brick you
unlock later in the run, which is not a warning and is not greyed.

That wording is deliberately broader than "not for a staff", because two different things
land in the same place: a brick can be the wrong *class* — the blade tree on a caster — or
the weapon can already sit on that stat's **cap**. Either way you will never be offered it,
and that is what the panel promises.

---

## Every buff and debuff

Four separate systems, and they never touch each other's pools:

- **prizes** you pick up
- **ailments** that stack on you
- **curses** that are hexed onto you
- **marks** you put on a monster, and buffs the monsters carry themselves

Everything below is read off the code, not off memory.

<details>
<summary><b>Open the full tables</b> — six of them, every effect and every stack limit</summary>

### 🎁 What you pick up

All four ground prizes run the same **30 seconds**, and picking another one up refreshes
the clock rather than queueing behind it.

| | Effect | Stacks to |
|---|---|---|
| ❤️ **Heart** | Regeneration ×1.5 / 1.85 / 2.10 / 2.30 / **2.45** | 5 — and never ×7.5 |
| 💢 **Rage** | **MORE** damage, its own multiplier outside every increase | ×2 |
| 🛡️ **Ward** | +40 armour and +12% to every resistance per stack — and a curse cannot strip it | ×2 |
| 🍀 **Fortune** | ×2 studs and XP, plus up to **+25% rarity find** on reward rolls | ×2 |
| 🧲 **Magnet** | Hoovers up every **stud** on the field. Studs only — it will not drag a chest to you | — |
| 💛 **Golden heart** | **+3 maximum hearts, permanently.** The only prize that outlives its timer | — |

Maximum hearts come from three rungs: `Sturdy Plates` adds them flat, **`Reinforced Frame`**
is an epic **+40% increased** (+60% at legendary), and `Titanium Chassis` is a legendary
**100% MORE**, twice over.

### 🔥 Ailments on you

Elemental stacks have **no ceiling**, and every **fifth** one is spent *breaking* something.
The break leaves a **brand** behind, and brands stack without limit — so resistance can be
driven below zero, and at **-100% you take double**.

| | What a stack does | What the 5th stack breaks | The brand it leaves |
|---|---|---|---|
| 🔥 **Burning** | 3 damage over 12s, per fire, scaled by fire resistance. Ignores i-frames | becomes a pool of damage over time | **BURNT** — -11% fire resistance, 22s |
| ❄️ **Chill** | -9% move speed and swing rate, floor 30% | **frozen solid for 3s** | **FROSTBITTEN** — -11% frost resistance, 22s |
| ⚡ **Shock** | +10% chance a hit on you crits for **×1.8**, cap 95% | every hit against you crits | **CONDUCTIVE** — -11% lightning resistance, 22s |

Resistance is capped at **80%** — nothing is ever fully immune — and it scales down both the
elemental damage of a hit *and* the strength of the ailment that hit applies. Each element
has its own **0.7s** application cooldown, so standing in a fire does not spam stacks onto
you sixty times a second.

### 🟣 Curses on you

Imp masters and the **MEGA BONE BARON** hex you. All four stack **9 deep**, the magnitude is
read straight off the stack count, and they expire together.

| | Effect | Per stack | Ceiling |
|---|---|--:|--:|
| **SUNDERED** | elemental resistance cut | -12% | -90% |
| **LEADEN** | swings come slower | -11% | -150% |
| **BRITTLE** | armour weakened | -13% | -92% |
| **WITHERED** | regeneration choked | -16% | -95% |

### 🗡️ What you put on a monster

| | How you inflict it | What it does |
|---|---|---|
| ☠️ **Poison** | Brickbane's gas, 1–3 doses a touch | Every dose bites at once. **9 / 18 / 36** deep on a monster, **12 / 24 / 48** on a boss, by Spore Burst rank. Green beads orbit the body; past 12 the count takes over |
| 🩸 **Bleed** | Any melee hit that rolls it — **50%** on every critical, **0%** otherwise until you buy it | 40%/s of the hit that opened it for **8s**, up to 70%/s. **Stacks 8 deep**, all biting together; a 9th cut displaces the shallowest wound or is discarded. Ignores armour, and **40% less while the body stands still** |
| ❄️ **Chilled** | Any frost you deal | The body crawls at **45% speed**. **Five stacks freeze it solid** |
| ⚡ **Shocked** | Any storm bolt that rolls `Conductor` | **+7% damage taken per stack, 5 deep** — from *everything* you own, not just lightning. A multiplier on the body, so it sits outside your own damage ceiling. Arcs crawl its silhouette while it holds |
| 🧊 **Frozen solid** | Block Freeze with *Absolute Zero* | **3s** of no AI, no attacks, no contact damage |

Both lingering damages read the one **increased damage over time** pool that `Rotbrick`
feeds, and each drips its numbers in its own colour — green for the rot, red for a wound.

### 👹 What monsters get

| | Source | Effect |
|---|---|---|
| ⭐ **Elite** | rolled on spawn | ×3.4 health, ×1.5 damage, 12% bigger, 2× studs and XP, **every drop line eight times over** — and it arrives with a **court of 4–9 minions** ringed around it |
| 🟦 **Magic-touched** | rolled on spawn | ×1.18 health, ×1.12 speed, **12% less damage taken** — and it comes as a **band of 6–15**, not three copies |
| ✨ **EMPOWERED** | an **overseer's** call, or the MEGA BONE BARON's | +15% health, +10 armour, +8% spell suppression, +1 damage and **+25% elemental power**, per stack. Caps at **3** |
| ⚡ **Shock arcs** | your storm bolts, with `Conductor` | Lightning crawls the body's silhouette while shock holds on it, deepening with the stack count; at five it sparks off |
| ☣️ **Poison** | standing in a **blight pool** | The pool does not *hit* you, it **poisons** you: a dose every 0.55s, each running 5s, all biting at once, up to 8 deep. It ignores armour, and it keeps working after you have walked off — stepping out stops it *spreading*, not stops it. Listed with your other ailments |
| 💚 **Warden's aura** | the **greenwarden** | Regenerates **5.5% of their own maximum per second** to everything within 240 — but never to itself. He is the fattest body on the field that is not a golem or a boss (**95 base**, against a bastion's 46) and shrugs off chip damage: he deals no damage at all, so the health bar *is* the fight, and at 30 he popped before the aura ever mattered |
| 🦴 **Blessed** | the MEGA BONE BARON, on itself | +18% armour, +12% damage, faster swings and +5% suppression, and it can do it again |

### 👑 Boss traits

Rolled fresh when the boss wakes — **one** normally, **two** from wave ten, **three** for an
ULTRA — never the same one twice, and printed on its health bar.

| | Effect |
|---|---|
| **SWIFT** | ×1.20 move speed, swings 15% quicker |
| **IRONCLAD** | ×1.7 armour, and 10% slower for it |
| **BRUTAL** | ×1.35 damage |
| **VITAL** | ×1.30 maximum health |
| **WARDED** | +25% spell suppression, to a ceiling of 85% |
| **VENOMOUS** | every touch sets a **burning** stack on you — the trait predates poison and still applies fire |
| **RESTLESS** | attacks come round 30% quicker |
| **LEGION** | brings ×1.6 + 1 of its court |
| **THORNED** | standing next to it costs you 25% of its hit |

</details>

---

## Every cap

Nothing in the game is unbounded. A brick that would push past its ceiling stops being
offered at all.

<details>
<summary><b>Open the ceiling table</b> — every number that stops</summary>

| Defence | | Spells | | Prizes | |
|---|--:|---|--:|---|--:|
| Armour reduction | 85% | Guardian bricks | 15 | Rage | ×2 |
| Elemental resistance | 80% | Ring reach | 190 px | Ward | ×2 |
| Critical chance | **80%** | Blaster bricks | 40 | Fortune | ×2 |
| Critical multiplier | **300%** | Blaster pierce | 8 | Heart stacks | 5 |
| Regeneration | 0.70/s* | Storm leaps | 15 | Poison on a monster | 9 |
| Bladestorm swords | 150 | Brickbane doses | 3 | Spore Burst reach | 210 px |
| Bleed chance | 100% | Bleed damage | 70%/s | Poison, Spore Burst ×2 | 36 / 48 |
| Bleed wounds | 8 | Fully opened | 560%/s | | |
| Curse stacks on you | 9 | Poison on a boss | 12 | Shock on a monster | 5 (+35%) |
| Monster level | **100** | Storm shock chance | 100% | Poison on you | 8 doses |
| Buffs on a monster | 9 unique | Monster resistance | 90% | Elite court | 9 |
| | | | | Magic band | 15 |
| Slam width | **180° / 360°** | Slam range | 430 px | Slam speed | 1400 px/s |
| **Skill points** | **60** | Tree cost | 444 | Points per wave | 1 |
| Teeth biting one body | 3 | Slam of the Elements | ×2 | | |
| Flat armour | 520 | Storm bricks | 5 | | |
| Increased armour | +200% | Storm forks | 2 (40 nodes) | | |
| | | Gas reach | 430 px | | |
| | | Bombs | 8 | | |
| | | Blast radius | 110 px | | |
| | | Cluster ranks | 2 | | |

</details>

### The damage ceiling

Increases and MORE multipliers were the two numbers nothing ever stopped. An hour into a
run they were both past **+800%**, which is not a build, it is an overflow — so both now
have a ceiling, and so does every ability's cooldown:

| | ceiling | what it is |
|---|--:|---|
| **Increased damage** | **+400%** | *every* increase that applies to one hit, summed: levels, physical, spell, elemental, all of it |
| **MORE multiplier** | **×3.00** | every permanent MORE together. A timed buff — rage — multiplies on **top** of it, on purpose |
| **Damage over time** | **+400%** | the Rotbrick pool, the same ceiling as a hit |
| **Critical chance** | **80%** | |
| **Critical multiplier** | **300%** | |

The **pools stay uncapped**, as every pool here does — a ceiling on a pool quietly makes
the next brick worthless while the bench goes on selling it. The ceiling sits on the
**derived** number, in one place that every attack in the game reads, and the bricks that
feed it stop being offered once you reach it. The character sheet prints the number the hit
actually uses and says *"— at the cap"* when the pool behind it has run ahead.

### Ability floors

`Quickening` is **attack speed** — your own hands, the swing or the bolt. `Runic Tempo` is
**cast speed**: every *ability* you own, all at once, and the only brick that touches all of
them. They are increases, so an ability's own rate bricks, the global cast-speed pool and
the weapon's attack speed all **sum** into one pool per ability — none of them multiplies.

Each ability then stops at its **own floor**, which is what keeps it recognisable however
much tempo the hero buys:

| | floor | | floor |
|---|--:|---|--:|
| Brick Blaster | 1.20s | Block Freeze | 3.50s |
| Storm Brick | 1.00s\* | Bomb Volley | 2.00s |
| Bladestorm | 2.00s | Brickbane | 1.10s |
| Blade Vortex | 3.00s | Guardian ring | 7.5 rad/s |

Runic Tempo stops being offered once every ability you actually own sits on its floor.

\* The storm brick's **base** is a flat 2.00s and it has no rate brick of its own — only
the global cast speed and a caster weapon can move it, and only down to 1.00s. Owning more
bricks is what makes the storm come round faster, because the stagger is the interval
divided by the bank.

A **favourite** lifts the cap of its own spell by one, so a Storm Brick run reaches six
hovering bricks and a Bomb Volley run nine bombs. The spell its kit hands you counts as
already taken, so it is never offered back to you as a first pick.

\* Regeneration is a **share of the bar, not a number of hearts** — but not one for one.
It scales with your maximum at a fractional power, so a bigger bar does regenerate faster
without healing in the same time regardless of size. Three Repair Bricks: 18 seconds to
full at 6 max hearts, 49 at 60, 102 at 300. Between waves it runs at **×2**.

**Armour** is `Armour / (Armour + 5 × hit)`, capped at 85% — strong against a hail of
small hits, thin against one big one. Fully capped at 1,560 armour value, a 5-damage hit
lands for 0.8 and a 120-damage hit still lands for 33.

**BLEED** is the melee half of damage over time, and it is Path of Exile's rule set. An
ordinary swing never opens a wound on its own — that chance starts at **0%** and is a brick
you buy — but every **critical** swing opens one **half the time** for free, which ties the
mechanic to the crit tree instead of making it a third parallel build.

A wound ticks for **40% of the hit that opened it, every second, for 8 seconds** — 320% of
that hit in total — rising to **70%/s** with Deep Cuts. It reads the same increased
damage-over-time pool Rotbrick feeds, and it ignores armour, because the opening hit
already paid it.

**Wounds stack eight deep** and all of them bite at once — 320%/s of a hit on a fully
opened body, 560%/s with Deep Cuts maxed. Past eight, a new cut has to *earn* its place:
it displaces the **shallowest** wound already open, and if it cannot beat even that one it
is thrown away. So the way to hurt a boss is to park on it and keep swinging, trading your
worst wound up for a better one on every hit. Melee's reward for standing in the dangerous
place.

And a body that has stopped moving bleeds **40% less**. Freezing a bleeding monster is not
a free win.

**You are not invulnerable between hits.** The window after taking damage is **0.10s**,
not a second — everything landing on you counts, and a crowd is a crowd.

Hearts stack at **×1.5 / 1.85 / 2.10 / 2.30 / 2.45** — five of them are worth two and a
half, never five. Blaster pierce costs the brick **15% of its damage per foe** at one
rank, easing to 10% once all eight are bought; a fully pierced brick deals 6.13× one hit
down a full line.

**Scattershot** no longer divides the damage it spreads. Each shard keeps a share of the
whole brick — **50%** at the first rank, **25%** at the second — so three shards are worth
**1.5×** one brick and nine are worth **2.25×**. Coverage *and* power, which is what a
legendary ought to buy.

---

## Difficulty

| | Monster HP | Monster damage | Count | Pack size | Reward rolls |
|---|--:|--:|--:|--:|--:|
| **EASY** | ×0.70 | ×0.75 | ×0.80 | ×0.6 | ×0.85 |
| **HARD** *(baseline)* | ×1.00 | ×1.00 | ×1.00 | ×1.0 | ×1.00 |
| **NIGHTMARE** | ×1.85 | ×1.55 | ×1.45 | ×1.9 | ×1.25 |

Nightmare also starts the roster four waves ahead of you, and pays about 1.7× the
legendary rate of Hard for the privilege.

---

## Monsters

Six things happen to a monster between wave 1 and wave 50, and they compound: it gains a
**level**, it hits for what its own row says it hits for, it can carry up to **nine** named
buffs, it arrives in a **pack**, the whole **wave** gets a modifier, and it **resists** what
it is made of.

### They have a level

The wave curve is a **polynomial**, and a polynomial flattens in relative terms. By wave 50,
ten more waves bought the roster about a third more health — which, against a hero whose own
damage now has a ceiling, read as no change at all. Wave 50 and wave 60 felt the same
because proportionally they nearly were.

A monster now carries a **LEVEL**, separate from the wave it spawned in. Each level is
**+11%** health, damage and armour, and it **compounds** — multiplied on **top** of the wave
curve rather than added into it.

**The cadence has bands.** One rate for the whole run could only be wrong at one end or the
other: slow enough to leave the opening alone meant the late game flattened again, fast
enough to keep the late game climbing put a level on wave 6.

| from | a level every | |
|--:|--:|---|
| **wave 5** | 4 waves | the first level lands *on* wave 5 |
| **wave 20** | 3 waves | |
| **wave 30** | 2 waves | |
| **wave 40** | **every wave** | and it never stops |

Each band counts only the waves inside it, so the schedule is **continuous**: crossing a
boundary never jumps the rank or takes one away.

| | monster level | health, damage and armour |
|---|--:|--:|
| before wave 5 | **0** | ×1.00 — the opening is untouched |
| wave 5 | 1 | ×1.11 |
| wave 13 | 3 | ×1.37 |
| wave 20 | 4 | ×1.52 |
| wave 30 | 7 | ×2.08 |
| wave 40 | 12 | ×3.50 |
| **wave 50** | **22** | **×9.93** |
| **wave 60** | **32** | **×28.21** |
| wave 100 | 72 | ×1,833 |
| wave 128+ | **100** (the ceiling) | ×34,064 |

The ceiling is level **100**, reached at wave 128 — past that a very long run stops getting
harder rather than becoming a wall.

Measured end to end on a real body, a wave-60 brute now has **×3.69 the health and ×3.65
the damage** of a wave-50 one. Before levels existed at all it was ×1.30 and ×1.28, and
under the single-cadence schedule it was ×1.78 and ×1.75.

**This is a large late-game change, on purpose.** Wave 50 went from ×3.15 to ×9.93 and wave
60 from ×3.88 to ×28.21 — roughly **3× and 7× tougher** than the schedule it replaced. The
first twenty waves barely move; everything past forty climbs every single wave.

The level shows beside the wave number in the HUD once it starts to bite, and on the
character sheet with the arithmetic behind it.

---

### They hit for what their own row says

A whole class of monster abilities was landing a **hardcoded 1 or 2** whatever threw them —
so a wave-50 golem's firebomb cost exactly what a wave-1 imp's did, and armour then ate two
thirds of it. That is the *0.33* you kept seeing.

Every thrown thing now **carries its thrower's damage**, worked out when it leaves the hand,
the way the boss bricks already did. Measured on real projectiles, wave 1 against wave 40:

| | wave 1 | wave 40 |
|---|--:|--:|
| Fire golem's bombs | 2.8 | **147** |
| Ice golem's boulder | 3.5 | **178** |
| Blight gobbet and its pool | 1.0 | **53** |
| Bomber's detonation | 2.6 | **395** |

*(The wave-40 column carries the monster level, which is a flat multiplier on health, damage
and armour alike. When the level cadence was rebanded, wave 40 went from level 8 to level 12
— ×1.52 — and every number in that column moved with it.)*

The same applies to the storm golem's orbs and coils, to meteors, and to every fire and lava
pool left behind — a pool now remembers what left it.

Some monsters were also simply too cheap to be hit by:

| | was | now | |
|---|--:|--:|---|
| **Bomber** | flat 1, forever | ×2.6 its own damage | its whole existence is that one detonation |
| **Jugg** | 2 | **4** | a wall that walks should cost something to be caught by |
| **Marksman** | 1 | **3** | a sniper that plinks is just an archer |
| **Stone golem** kit | 2 | **5** | |
| **Ice / Fire golem** kit | 1 / 2 | **4** | |
| **Storm golem** kit | 2 | **5** | |
| Golem **bodies** | 2 | **3–4** | contact damage, per golem |

*(The marksman does spawn — counted: about **5 a wave at 28** and **12 at 48**. It was never a
spawn problem; it did one damage and so never registered.)*

The **revenant's** shockwave reaches **156** instead of 260 — 40% less, so stepping out is a
real answer rather than a tax. The **pumpkin lord** aims at you now: it was leading your
movement by the whole three-second fuse, which is indistinguishable from throwing at random.
It leads the *flight* only, with half the scatter — the fuse is what gives you time to walk
off it, and that is the actual mechanic.

---

### Nine buffs, not three

A monster's buffs used to be **one counter capped at three**, so every aura in the game
fought over the same ceiling: a body standing in an overseer's empowerment and a pyrolord's
warding simply refused the second one.

They are a **map** now, keyed by what granted them. A body may carry **nine different**
buffs, and each stacks to its own depth — three stacks of fire resistance is **one** of the
nine, not three of them.

Which makes the **elemental lords** worth something they were not: a pyrolord, frostlord or
stormlord **wards everything within 320** with resistance to its own element, 25% a stack,
three deep. A pack led by a pyrolord is a pack you do not burn down — and killing the lord
is what unlocks it.

---

### Packs are packs

Three bodies read as *the spawner hiccuped*. A pack should read as a warband walking out of
the trees, so both kinds got rebuilt:

**An elite is one champion with a court**, not three copies of the same thing. Three
identical elites was a damage spike with no shape to it; one genuinely hard body ringed by
**4 to 9 minions** — the cheap end of the roster, or a golem's own minis — is something you
have to fight your way *into*. The elite itself is much harder to pay for it: **×3.4 health
and ×1.5 damage**, up from ×2.2 and +1.

**A magic pack is a band of 6 to 15**, scaling with the wave, and every one of them carries
the modifier — it used to be three copies each carrying the full thing. Each body's share is
smaller to pay for the extra bodies: **×1.18 health, ×1.12 speed, 12% less damage taken**,
down from ×1.5, ×1.3 and 30%.

Magic-touched is **blue** now rather than purple: purple is already the epic rarity band and
the necromancers' own colour, so a magic pack read as *something arcane* instead of as the
pack modifier it is.

---

### Wave modifiers

Every wave from **6** rolls a modifier, and another every twelve waves after that, up to
four. They are rolled **once**, when the wave is composed, and held for its whole life — so
a wave has an identity you can read off the banner and plan around rather than a difficulty
that drifts under you. They sit under the wave number in the HUD until it is cleared, each
in its own colour, and they go in the combat log too.

| | |
|---|---|
| **HARDY** | monsters have 30% more health |
| **BRUTAL** | monsters deal 25% more damage |
| **SWIFT** | monsters move 18% faster |
| **FRENZIED** | monsters attack 25% faster |
| **PLATED** | monsters have double armour |
| **WARDED** | monsters suppress 30% of spell damage |
| **RESISTANT** | monsters gain 35% resistance to every element |
| **SLIPPERY** | monsters evade 12% of your hits |
| **HORDE** | 40% more monsters, each with 15% less health |
| **OPULENT** | monsters are tougher, and pay double |
| **WARBAND** | one monster in six is an elite |

Each one is a plain multiplier applied **at spawn**, after the wave curve, the monster level
and the difficulty — so a modifier multiplies the finished number rather than a base that
has not been built yet, and nothing reaches into a body that is already standing. A body
gets a **copy** of its species' resistance table for exactly this reason: `RESISTANT` writes
to it, and writing to the shared definition would have made the modifier permanent for the
rest of the run. A test asserts the species comes back clean afterwards.

---

### They resist what they are made of

The hero has had elemental resistance since the first commit and monsters never did, which
meant a fire golem was exactly as flammable as a skeleton — every golem was a reskin as far
as your build was concerned.

| | resists | weak to | |
|---|---|---|---|
| **Fire golem** | fire **90%** | frost **−35%** | shock 20% |
| **Ice golem** | frost **90%** | fire **−35%** | shock 20% |
| **Storm golem** | lightning **90%** | frost **−35%** | fire 20% |
| **Stone golem** | 25% of everything | — | and **×3.2 armour** |
| Lords · mages | 75% · 45% of their own element | the opposite one | |

A **negative** resistance is a weakness and multiplies *up*, so a fire golem takes **135**
from a 100-damage frost hit and **10** from the same hit in fire. Which spell you are
holding decides which golem is a problem.

Stone is the exception on purpose: it is rock, so it is **armour** — and armour only ever
touches *attacks*. You cannot out-swing a stone golem; you burn it. Resistance caps at
**90%** even under `RESISTANT`, so max resistance is a wall rather than immunity.

---

## The skill tree

You used to die at wave 15 and nothing happened: the run was the whole reward and the run
was gone. Now every run banks, and **pushing your best** is worth more than grinding a wave
you have already beaten.

![The skill tree](docs/tree.png)

**TWELVE TREES.** Every weapon has its own on every difficulty — three difficulties × four
weapons — and **they share nothing**. Points earned on a staff are a staff's; switch to the
axe and you find the axe's own tree exactly as you left it, and switching back finds the
staff's.

**Everyone starts in the middle, and no two weapons start the same way.** Four doors in the
dead centre, and each one opens into a cluster nobody else can reach cheaply — the sword's
is reach and riposte, the mace's is the weight of the slam, the axe's is bloodscent and wild
swings, the staff's is focus and warding. Your own beginning costs you **3 to 4** points and
costs everybody else **15 to 24**. After that the map is shared, and it fans outward.

### Highways and pockets

**The map used to join cluster to cluster, and that was backwards.** Every journey ran
*through* somebody else's cluster, so a lightning build walking to the far side collected
increased physical damage on the way — and paid a point for each node of it. You were charged
for stats you did not want in order to reach the ones you did.

**Now the roads carry you and the clusters hang off them.** A road is a long run of nodes
that pay **one attribute and nothing else**, so you can cross the entire map on dexterity
alone and arrive with nothing in your build you did not choose:

Three ring roads, twenty spokes across the gaps between them, and four door roads — one per
weapon, each paying a different attribute, so the staff walks out of the middle on
intelligence and the mace on strength.

| Attribute | Which roads pay it | Longest unbroken run | How far it spans |
|---|---|--:|--:|
| **DEXTERITY** | the inner ring, the axe's door, a third of the spokes | **44** nodes | 3065 px |
| **STRENGTH** | the middle ring, the sword's and mace's doors, a third of the spokes | **95** | 3075 px |
| **INTELLIGENCE** | the outer ring, the staff's door, a third of the spokes | **123** | 4323 px |

A road node is worth **2** of its attribute — a fifth of a real node. Walking across the map
is never *nothing*, and it is never as good as arriving.

**A cluster is a POCKET.** It hangs off a single short spur and has no other way in or out,
so **nothing ever routes through one**. Knock any cluster out of the map entirely and every
other cluster is still reachable from every door — which means skipping a cluster you do not
want costs you exactly nothing. That is the whole rework in one sentence.

The cheapest walk to any keystone, from any door, passes through **zero** other clusters, and
**91 to 94% of it is road**. You take the highway out, step into the pocket you came for,
and step back.

**Four hundred and fifty-seven nodes**, in bands: four doors in the dead centre, four
openings around them, twelve clusters between the first and second ring, eight between the
second and third, eight abilities outside the third, and four corners further out still.
**The rim is where the good stuff is** — the twelve keystones all sit on the outside,
**twenty-two to thirty-two points** from your door, so one of them is a real journey and
two of them is your whole ceiling.

**The outer ring is one ABILITY apiece.** THE ORBIT, THE BARREL, THE STORMHEAD, THE
WHIRLING, THE VORTEX, THE COLD SNAP, THE ORCHARD, THE ORDNANCE — `+1 damage with the
Guardian Brick`, `+1 Brick Blaster pierce`, `+1 Storm Brick fork`, `+12% increased Bomb
Volley rate`. A tree that only ever says *+5% damage* has no opinions; a node that names one
of your bricks makes you re-read the map every time the deck hands you something new.

**Resistance is everywhere.** Every crossing has a ward, every ring-B cluster carries one,
every corner stacks them, and **every step of armour or core corridor is +1% to all three** —
so a long walk in any direction leaves you a little harder to burn, freeze and shock.

**One currency you can see: skill points.** They come from two places, and both are
*derived* — nothing is stored as a counter:

**EVERY WAVE IS A POINT, AND STUDS ARE NOT.** Studs were the gate, and the gate was the
problem: a run that got further than any before it paid exactly what one that shuffled round
wave 12 hoovering bricks did, so the tree was a grind rather than a record.

**Your best wave IS your point total.** Reach 38 and you have 38 to place. Reach 40 next
time and you have **two** more — because 40 is your high-water mark, not your fortieth lap.
A worse run pays nothing, however long it was.

| | |
|---|--:|
| **your best wave** *with this weapon* | **1 point each** |
| reaching level 10, 25, 50, 75, 80, 90, 100 | 7 |
| first time smashing each of the 8 bosses | 8 |
| **BRICKBANE MASTER** — every capped Brickbane brick at its cap | 1 |
| **the ceiling on everything** | **60** |

**BRICKBANE MASTER** wants stacks, spread, reach, rate, dose length and Spore Burst all at
their ceilings. Flat poison damage is *not* asked for: it has no cap, so "all of it" could
never be true. The point lands the moment the last cap is met — mid-run, with a banner —
rather than waiting for you to die.

Studs are still banked and still counted. They buy bricks inside a run and **nothing at all**
in here.

**The wave you CLEARED, not the one you died on.** Dying to the wave-30 boss is a wave-29
run and the screen says so.

**You bank the square root of what you picked up** — `floor(4 × √studs)`. A full clear
collects about 15× what a death at wave 15 does; banked raw, the early runs this exists to
reward would feel *worse*. The square root compresses that to about 4×. It banks what you
**collected**, not what you are holding, so rerolling never costs you twice.

**The tree costs 444 points and you can never hold more than 60.** That is the whole design:
at most **14%** of it, ever, so the tree is a set of builds rather than a ladder you finish.
412 nodes — 116 small ones in clusters, 12 notables, 12 keystones, and 268 steps of corridor
between them.

**Every step of corridor is an ATTRIBUTE**, and attributes are the boring numbers you collect
on the way to somewhere interesting:

| | | |
|---|---|--:|
| 🔴 **STRENGTH** | +0.5% increased physical damage, +0.5% increased maximum hearts | *each* |
| 🔵 **INTELLIGENCE** | +0.5% increased spell damage, +0.5% increased regeneration | *each* |
| 🟢 **DEXTERITY** | +0.3% increased critical chance, critical damage and attack speed | *each* |

**Every weapon starts somewhere different on the three** — a mace opens on 20 strength, a
staff on 22 intelligence, an axe on 16 dexterity, and the sword is the only one with a little
of all three. Four bricks hand them over directly and **any weapon and any favourite can be
offered all four**: `Ironbone`, `Runescript`, `Quicksilver`, and `The Whole Brick` for all
three at once.

**What an attribute grants is DERIVED, never written.** A brick that handed you ten strength
*and* poked +5% into the increased-physical pool would double-count the moment a second source
of strength turned up, and the pool would keep the bonus after the strength went away. The
attribute is the pool; the percentage is read off it at the point of use, every rebuild.

> `+0.03 maximum hearts` was a node nobody could feel and nobody could read. Those are
> `+4% increased maximum hearts` now — a percentage of a pool that grows with the build is
> both.

**You cannot drop points wherever you like.** A node has to *touch* something you already
hold, and everything you hold has to trace back to your weapon's door. Every corridor is
walkable from **both** ends and the map is full of loops: knock out any single cluster and
the middle is still reachable from all four doors. There is always more than one route —
each one costs something different.

**Refund is a mode, not a button that eats your tree.** Turn it on and click nodes back one
at a time, free; a node holding up a branch is refused until you unwind the branch. `CLEAR
ALL` is there when you want it.

**Zoom and drag.** The map does not fit on a screen at a size you can read it, so scroll to
zoom toward the cursor, drag to pan, and `FIT` puts the whole thing back. A drag is never
mistaken for a purchase.

> The first version of this captured the pointer on the `<svg>` to follow a drag, which
> retargets the pointer — and the `click` that follows is delivered to the `<svg>` and never
> reaches the node you pressed. **Every node in the tree became unclickable.** `metaplay.js`
> passed anyway, because it drove the map with a synthetic `MouseEvent` that skipped the
> pointer handlers entirely: a test that clicks in a way no hand can is a test that proves
> nothing. It now presses, wobbles two pixels the way a hand does, and releases — and it asks
> the browser, via `elementFromPoint`, whether anything is sitting on top of any node. That
> second check found six more nodes hidden under cluster labels.

**A PATCH NEVER COSTS YOU PROGRESS.** The save carries the version of the tree it was spent
on. Move the tree and every stud, every best wave and every boss stays exactly where it was —
the allocated points simply come **back**, and the tree screen says so in as many words:
*THE TREE CHANGED — your N spent points are back. Spend them again.* A respec, never a reset.

Eight colours of ground — WARFARE, PRECISION, SWIFTNESS, AFFLICTION, VITALITY, ARMOUR,
ELEMENTS, ARCANA — and a gold CORE at the middle. Clusters wear the colour of the ground
they stand on, so the regions read from across the map even before you have taken anything.
Twelve notables sit at the centre of a cluster, and twelve **keystones carry a real
downside**:

| | |
|---|---|
| **Glass Bricks** | +35% increased damage — and **30% less maximum hearts** |
| **Perfect Strike** | +150% increased critical chance — and **half the critical multiplier** |
| **Slow Death** | +60% increased damage over time — and **25% less increased damage** |
| **Slow Burn** | every elemental toll lands 10 waves later — and **30% less XP** |
| **Ironclad Oath** | +50% armour and +10% all resistance — and **20% less move speed** |
| **Bloodthirst** | +20% lifesteal chance and +6% share — and **no regeneration at all** |
| **Hoarder** | **double** the studs you bank — and 20% less increased damage |
| **Scavenger** | chests offer **two** cards — and you can never reroll |
| **Livewire** | the Storm Brick fires twice as fast and forks — and **nothing you do lingers** |
| **Vengeance** | +50% damage over time and a deeper wound — and **60% less critical chance** |
| **Stoneheart** | +35% MORE maximum hearts and +6% all resistance — and **30% less damage** |
| **Conduit** | +50% elemental damage and +8% all resistance — and **35% less cast speed** |

A tree of pure upgrades is a ratchet; a tree of trade-offs is a set of builds. That is why
people still open Path of Exile's tree after ten years.

**Hover anything to read it.** Every node lists exactly what it gives, good and bad, and a
node whose stat does nothing for the weapon you are on says so rather than selling it to
you. A running total of everything you have allocated sits beside the map — the panel and
the hero are read off the same `mods`, so they cannot disagree.

**It does not touch the test bench.** The tree is applied only when a real run starts, so
every preview and throwaway hero reads the raw numbers. A switch on the tree screen turns it
off for real runs too.

### Why it survives being rebalanced

Four invariants, and they are the whole reason the save stays alive through future changes:

1. **The save stores node IDs, never the stats they grant.** The effect is rebuilt from the
   live table every load, so rebalancing a node is free — no migration, no stale saves.
2. **Points spent are derived**, by summing the live table. Change a cost and every save
   re-derives. A node that no longer exists costs nothing, so **a deleted node self-refunds**
   with no migration code at all.
3. **Points earned are derived from the achievements**, never from a counter. Change *"a
   point every 10 waves"* to *"every 5"* and everyone's existing progress re-grants
   retroactively. A counter can never be fixed; a derived value is always right.
4. **Nodes write into the same pools the bricks write into.** Because every ceiling in this
   game lives on the derived value rather than on the pool, the tree is bound by every cap —
   *including caps that do not exist yet*.

A corrupt save resets rather than throwing, and a save claiming nodes it never earned — or
one it cannot reach from its door — is refunded instead of honoured. That is also why the
map could be rebuilt from scratch without a line of migration code: every old node ID is
simply unknown now, so **every existing save got all of its points back to re-spend**.

An older save from before the tree was split by weapon used to be poured into *all four*
weapons, which made the tree look shared when it never was. It now lands on the one weapon
that was selected when it was written, and nowhere else.

**And the budget is a test, not a promise.** `meta.js` walks real routes out of the sword's
own door, spends **exactly** the 60-point ceiling on them — a budget checked on a half-spent
tree is a budget nobody is ever held to — and fails the build if it beats **×2.60 total
power**, damage *times* hearts. Damage alone is the wrong measure: a glass build buys damage
by selling hearts, and a damage-only budget waves it through while punishing an honest one.
The product cannot be gamed, because a node with no downside raises both halves.

Measured at the full 60-point ceiling: a straight build is **×2.09 damage, ×2.39 power**.
Glass Bricks is **×2.79 damage but only ×2.39 power** against **×2.27 / ×2.92** for *the same
walk with the keystone left out* — more damage, less total, which is exactly the trade it
advertises. (Comparing it against a *different* route list measured the two lists as much as
it measured the keystone; dropping the one node out of the one walk measures the keystone.)

The ceiling has gone 26 → 34 → 46 → **60** across four passes. On the second the power did not
move at all, because the tree grew faster than the ceiling did and the extra points went
into corridor. On the third it moved from ×1.60 to ×1.96 — a fully banked tree is now worth
roughly twice a bare hero, and that is what thirty stud points and eight boss firsts buy.

---

## Damage conversion

**Live, on every hit in the game.**

**Five types, one direction.** Conversion only ever runs *down* this list, which is the whole
reason it terminates — there is no arrangement of modifiers that can send damage back round
the loop:

> **PHYSICAL → LIGHTNING → COLD → FIRE → CHAOS**

**Two forms, and the difference is what happens to the source:**

| | |
|---|---|
| `#% of X Converted to Y` | takes the damage **out** of the source type |
| `Gain #% of X as Extra Y` | leaves the source intact and adds a copy as the new type |

**When it happens:** after flat added damage joins the base, and **before** any increased or
more multiplier touches it.

**A converted part keeps the increases for how it was BUILT and gains the ones for what it
BECAME.** A sword swing converted to fire is still a swing — it keeps increased physical
damage and picks up increased fire damage on top. Dropping the source's pools would have made
every conversion brick a trap, which is the one thing the mechanic must not be.

**What the monster does about it: nothing to do with you.**

- A part that arrives as **cold** pays **cold resistance**, whatever it set out as.
- **Only the part that is still physical ever meets armour.** Converting a swing to fire is
  how you walk past a golem's plate, and that is the point of the entire mechanic.
- A **spell** meets suppression however it has been converted — being cold does not stop it
  being a spell.
- **What lingers is not converted again.** A bleed, a burn and a poison were all built the
  moment they were inflicted; running them back through the pipeline on every tick would
  convert the same damage over and over.

**How it is wired, and why nothing upstream had to change.** Every number in this game is
linear in its base — a hit is `base × (1 + increased) × more` — so the *effect* of conversion
does not depend on the base at all. Run the pipeline on a base of **one** and you have both
the ratio between the converted hit and the plain one and the share each type ended up with.
The swing still builds one number the way it always did; `hitEnemy` corrects its size and
mitigates each share as what it became. A hero who converts nothing takes a one-line fast
path that is arithmetically identical to the code that was there before.

**Seven bricks write it**, and five skill-tree nodes do too:

| Brick | |
|---|---|
| **Emberforge** / **Rimeblade** / **Galvanise** | 10–40% of your physical converted to fire, cold or lightning |
| **The Unmaking** | 10–40% of your fire converted to chaos |
| **Creeping Blight** | gain 6–30% of your physical as extra chaos |
| **Catalyst** | gain 6–30% of your cold as extra fire |
| **Entropy** | increased chaos damage — offered only once you convert something |

**Over-conversion is shared, not compounded.** Asking for 80% to cold *and* 80% to fire gets
you half of each, never a hero dealing 160% of their own damage.

**CHAOS** is neither physical nor elemental, so **armour does not stop it and nothing in the
game resists it** — it pays its own increase pool and no other. It is the poison-flavoured
one: where physical can open a bleed, a hit carrying chaos can leave a **dose of poison**
behind, on odds that ride on how much of the hit actually was chaos.

**The character sheet grows a CONVERSION card** the moment you convert anything — one row
per type showing what share of an ordinary hit arrives as it and what it meets on the way in,
plus the multiplier conversion puts on the size of the hit. It reads the *same* split the hit
itself reads, so if the card and the monster ever disagree you can see which one is lying.
The exhaustive EVERY STAT list below it carries the same five rows, always.

`conv.js` proves the pipeline on its own — an uphill pair is refused, a hero with no table is
the safest path through rather than the one that throws, and the worst case (every legal
conversion *and* every gain-as-extra at once) terminates in under a millisecond.
`conv2.js` puts a hero in front of a monster and measures what actually comes off its health
bar: armour walked past, the right resistance paid, half-converted is half-mitigated, a real
swing arriving at exactly `base × every pool that should apply to it`, gain-as-extra leaving
the source alone, chaos resisted by nothing, chaos leaving doses, a drain **not** re-converted,
and the sheet agreeing with the hit. Sixteen break-builds between them, each caught by the
assertion meant for it.

---

## The main menu

**Every stat, and which layer put it there.** The left panel used to show nine opening
numbers and a percentage against "no favourite", which answered one question out of three: a
hero is a **bare hand + a weapon + a favourite + a skill tree**, and the tree was invisible on
this screen entirely.

Four heroes are now stood up from scratch and **every probe on the character sheet** is read
off each of them. The difference between one layer and the next *is* that layer's
contribution, printed beside the finished number as a chip:

> **Bolt damage 3.18** `W+2.64` `T+0.54`

Nothing is estimated, nothing is duplicated, and a stat nobody has thought of yet appears on
this panel the moment somebody adds a probe for it. The panel scrolls, its group headings
stick while it does, and every row explains itself on hover.

> **No Favourite is a favourite.** It hands out +5% increased damage, a heart, ten armour and
> more — so a "weapon only" layer that quietly applied it made every real favourite look like
> it *cost* you five per cent. Which it does, and which is exactly what the `F` chip should
> say out loud rather than hide inside the `W` one.

**ABILITIES, not ability floors.** The panel lists what you actually open with — your weapon's
attack and every spell your favourite grants — with its damage beside it, and hovering one
opens its full damage panel. The floors moved down into CEILINGS where they belong.

**The sword is not the yardstick any more.** Hovering it used to show nothing at all, because
every weapon was written against it. Everything is written against **BARE HANDS** now — a
level-1 hero holding nothing, every multiplier 1 and every defensive perk zero — so the sword
reads −32% swing time, +40% reach, +50% crit multiplier and +40% less damage taken like any
other choice.

And the tooltips **fit**. An absolutely positioned tip anchored above its row has no idea
where the top of the window is, so the mace's and the staff's ran off the screen. They use the
same fixed, viewport-clamped element the stat rows use, drop below the row when there is no
room above, and scroll when the window is shorter than they are.

**The skill tree button says which number it is showing, and whose.** It printed points
*left*, so a hero who had spent all twelve read `SKILL TREE — 0 POINTS` — identical to one who
had never earned any. And it was refreshed when the **difficulty** changed but not when the
**weapon** did, so it went on showing the last weapon's count. Twelve trees, one label, and no
way to tell them apart. It now reads `SKILL TREE — 4 TO PLACE · 15 spent · STAFF · HARD`.

**`Ember Core` was never a fire card.** `flatElem` is read by the bolt, by Block Freeze and by
the Storm Brick alike, so it has always been the single best upgrade a frost build can take —
and it was called Ember Core, wore a flame, and said *"to your fire bolt"*. Being handed an
ember after picking Block Freeze as your favourite was a card lying about itself, not a bad
roll. It is **`Elemental Core`** now and says what it feeds.

---

## Sound

**Music and sound are separate switches, both on the menu, both remembered.** `M` still
takes everything down at once — which is what you want when somebody walks into the room,
and useless when it is only the music you are tired of. The buttons say `MUSIC: OFF` the
moment `M` mutes, so the menu never disagrees with what you can hear.

A silent channel **builds no voices**: turning the music off stops the scheduler rather than
turning its volume down, and the scheduler's clock is caught up when it comes back — otherwise
ten silent minutes return as ten minutes of notes at once.

**`STUD OVERDRIVE` is gone.** 150 bpm, a square-wave lead and a hat on every sixteenth, and
it played for every wave from fifteen to the end of the run; ten minutes of it was exhausting.
**`THE DEEP FOUNDRY`** takes its place and makes the opposite bet: 116 bpm, slower than the
track before it, a descending D-minor walk under a lead that leaves gaps, and the weight in
the low end rather than in the tempo. Something you can hear for an hour.

---

## The world gets colder

A hero who ignored resistance entirely used to be perfectly fine at wave 50, because
nothing ever asked them for it. So the game asks — three times, and then never again:

| | | Your resistances |
|---|---|---|
| **Wave 20** | ❄️ *the cold creeps in* | **−30** |
| **Wave 40** | ❄️ *and again* | **−60** total |
| **Wave 60** | ❄️ *and once more* | **−90** total |

It is a **flat** toll, it is **permanent**, and it lands on the wave that charges it before
a single body walks in — your character sheet has already changed when the banner appears.
Past wave 60 nothing more is taken.

Three points matter about the shape of it:

- It is a tax on **your stat**, not a buff to the monsters. Every point you lose is a point
  a `Flameguard Plates` or an `Elementalist Bricks` puts straight back, so the answer is in
  the reward pool rather than in luck.
- Resistance caps at **80%**, so the ceiling never moves — what moves is how far below it
  you start. A hero at the cap is at **50%** after wave 20 and **−10%** after wave 60.
- Negative resistance is a *weakness*, and it multiplies damage **up**. Ignore it all the
  way to wave 60 and a fireball lands for nearly twice what it used to.

The **Elemental toll** row on the character sheet shows exactly what has been taken.

---

## Reading your own build

Three screens answer three different questions, and all three are driven from **one list of
stat probes** so they can never drift apart.

### The reward comparison — *what would this brick do?*

Hovering a reward, or any card on the test bench, applies it to a **copy** of the hero,
reads all 111 probes again, and lists only what actually moved, as `now → next`. Because
the probes include every spell's damage, one *"+18% increased spell damage"* card shows its
effect on the blaster, the storm brick and the bombs on separate lines.

Every probe carries **one sentence saying what the stat is** — the same sentence, from the
same table, in all three screens below.

On the title screen that sentence used to be a `title=` attribute, which was the same as
not having one: a native tooltip needs about a second of *stationary* hover on an 18px row
before the browser draws anything, so in practice nothing ever appeared. It is a real
hover panel now — it shows the moment the pointer lands, it is styled like the rest of the
screen, and the test asserts the tooltip's **computed box is visible and on screen**
rather than merely that the attribute exists.

**A spell upgrade is never offered for a spell you do not own.** `Storm Edge` carried two
`req:` keys on the same object literal — JavaScript keeps the last one, and the one that
lost was the one checking you owned a Bladestorm — so a sword hero was offered upgrades to
a spell they had never picked up. `gate2.js` now walks every spell tree against a hero who
owns nothing and fails on anything that answers yes; the brick that *grants* the spell is
the only thing allowed through.

A brick that moved **no** probe used to show an empty panel, which is how `Rotbrick` came to
say nothing at all on a build that was bleeding and burning: every bleed probe was a
**share** of the hit, and none of them read the damage-over-time pool. There are real
numbers now — `One wound`, `Damage per wound`, `Fully opened`, and a whole `BURNING` group —
so an increased-damage-over-time card shows its effect on poison, bleed **and** fire at
once. Four legendaries (`Tidal Orbit`, `Crackling Storm`, `Returning Blades`,
`Absolute Zero`) were silent for the same reason and now have probes of their own. A test
walks every brick on every weapon and fails if any of them moves nothing.

The **fireball left `ELEMENTAL`**. Elemental is the *global* tree — the pools every
elemental source reads — and filing the staff's own attack under it made a card that
widened the burst look like a card that changed the whole build. `FIREBALL` and `BURNING`
are their own groups.

### The character sheet — <kbd>C</kbd> — *what am I right now?*

The hand-written blocks at the top carry the deep explanations. Underneath them,
**EVERY STAT** is generated straight from the probe list, grouped, with each row's
explanation on hover — so a probe added for the comparison reaches this screen the same day
rather than months later. A test asserts all 111 appear.

### The spellbook — <kbd>P</kbd> — *what am I carrying?*

Every brick the run holds, drawn as the bench card it came from and **dealt into stacks**:
three of a brick is three cards fanned behind one another with a `×3` badge, and the text is
read off the **live hero**, so a brick taken at common and again at epic shows what the pair
of them are doing now. It is filed in the bench's own order, and a spell's section is headed
with that spell's live damage.

**Click a stack and it fans out.** The ghosts behind a deck tell you *how many* copies you
own but never *which* ones — and two copies of the same brick are rarely the same card,
because each was rolled at its own rarity. Opening one lays them out in the order they were
taken, each drawn at the rarity it actually came in at, with its own numbers. A stack of
three Fault Hammers opens into `+0.5`, `+3` and `+5`. Opening another deck closes the last;
clicking anywhere else closes them all. A brick handed over by a favourite's starting kit
has no pick behind it, so it says *starting kit* rather than inventing a tier.

**It pauses.** It was built not to, and that was simply wrong: reading nine sections of
cards while a wave-40 pack walks into you is not a choice anyone makes twice. It takes its
own state, so the world freezes and stays drawn behind it.

### The boss bar — *what is it carrying?*

The boss block stacks **under** the wave block now rather than being drawn through it: the
wave number, its modifier chips and the bodies-left line all live at the top of the screen,
and the boss's traits were being painted straight over them. One function reports where the
wave block ends, and the boss block starts below whatever it actually used.

Under the health bar, **every debuff on the boss on one line**, left to right — poison,
bleed, burning, BURNT, chill, shock, frozen — as a **symbol and a count**, no names. On a
boss you are reading this mid-fight, and a row of words is not something you can read
mid-fight. The glyphs are drawn rather than typed: the game's font has no dingbats, and a
missing one renders as a tofu box.

### The bench's roster — <kbd>1</kbd><kbd>3</kbd><kbd>3</kbd><kbd>7</kbd>

The monster list was one flat wall of forty names that told you nothing about the shape of a
run. It is filed into **wave bands** — 1–5, 6–10, 11–15 and so on — with the golems and the
bosses in their own bands at the end, because golems are seeded into waves rather than
bought from the budget.

**Hover any of them** and the same panel the bricks use fills with what that body *is*: its
health, contact damage and armour **at the wave you are standing in**, its base numbers, its
move speed and attack interval, its elemental resistances, the wave it starts turning up in,
what it costs the wave budget, and what it pays out. The at-this-wave numbers run through
the same wave curve, monster level and difficulty the spawner uses, so they are what you
would actually meet rather than the table's raw base.

### The DPS tab — <kbd>L</kbd>, then the fourth tab

The run totals answer *"what did the most damage all run"*, which flatters whatever you
picked first and buries a spell that only came online at wave 30. The **DPS** tab answers
the balance question instead: what is each ability doing *right now*.

One bucket a second per damage source, sixty of them, rolled forward as the clock moves —
so a source that has gone quiet ages out of its own accord instead of sitting at its old
number for ever. Each row is the source, its rate over the window, a share bar against the
best one, its percentage, and a **sixty-second sparkline**. Bleed, burning and poison are
their own sources, so you can see exactly what an ailment build is really doing.

The window is only as long as the run has been going, or a spell picked up ten seconds ago
would read as a sixth of what it is actually doing; the header says `(filling)` until it has
a full minute. Every bucket index goes through one wrap helper: JavaScript's `%` keeps the
sign of its left operand, so a plain `x % 60` on anything negative indexes off the end of
the array and silently writes a *property* rather than a slot — a roll that looks like it
cleared thirty buckets and cleared two. Sources past the ninth fold into one `+N more` row rather than being dropped,
so the rows still add up to the total printed above.

---

## Controls

| | |
|---|---|
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> / arrows | move |
| **Left mouse** | swing — *hold* to whirl, once you own Blademaster |
| <kbd>Space</kbd> | roll — invulnerable through it, and a perfect dodge refunds bomb cooldown |
| <kbd>F</kbd> | start the next wave early — every wave opens with a breather (30s after a boss) |
| <kbd>F</kbd> *(held, mid-fight)* | call the next wave down on top of this one — 3 seconds' warning, up to 3 stacked, every wave pays its own reward |
| <kbd>C</kbd> | character sheet |
| <kbd>P</kbd> | **spellbook** — every brick you are carrying, dealt into stacks. **It does not pause the game** |
| <kbd>L</kbd> | combat log · <kbd>Shift</kbd>+<kbd>L</kbd> cycles dealt / taken / events / **DPS** |
| <kbd>R</kbd> / <kbd>X</kbd> | on a reward screen: reroll · decline for studs |
| <kbd>Esc</kbd> | pause — or close the spellbook, if it is open |
| <kbd>M</kbd> | mute |

---

## Under the hood

Everything below is written by hand in one `<script>`, against a 2D canvas.

- **Bodies are capsules, not circles.** Every projectile, sweep, beam and blast tests
  against a segment running from the feet to the top of the head, so a bolt that crosses a
  chest connects with it.
- **A\* on an 8-connected grid**, string-pulled, with a per-frame search budget, local
  steering, and a stuck-detector that rescues anything that has walked itself into a rock.
- **A uniform bucket grid** for every "what is near me" question — separation, sword
  swings, whirls, chain targeting — instead of scanning the whole roster.
- **View-frustum culling** at 1.25× the window for every draw pass.
- **Procedural audio.** Six tracks — `BRICKFIELD MARCH`, `QUARRY STOMP`, `CRYSTAL CAVERN`,
  `STUD OVERDRIVE`, `THE BRICK TYRANT`, `COLOSSUS RISING` — are sequenced live from Web
  Audio oscillators. There are no sound files. Cues run through a compressor on the master
  bus and a low-pass lid on the effects bus, with per-cue throttling so forty monsters
  arriving is one sound.
- **Seven biomes** — the Greenwood, the Dust Flats, the Grey Reach, the Mire, the White
  Waste, the Cinder Fields, the Long Meadow — each with its own props, palette and lakes,
  generated per run.

<details>
<summary><b>Developer note — the test bench</b> (mild spoiler)</summary>

<br>

Type <kbd>1</kbd><kbd>3</kbd><kbd>3</kbd><kbd>7</kbd> during play — in order, unbroken —
and a bench opens that grants any brick at any rarity, jumps to any wave, and switches
difficulty live. Each card opens at its **own lowest rarity**, which is what you want when
you are balancing: you see what it is worth when it actually drops. Hovering shows the full
now → next comparison; <kbd>Shift</kbd>+scroll walks the rarity up.

It also has a **sandbox**: switch it on and no wave ever arrives, then put any of the 37
monsters or 8 bosses in front of you on demand — <kbd>Shift</kbd>-click for an elite,
right-click for five.

It is how the numbers in this README were balanced, and it is deliberately not advertised
anywhere in the game.

</details>

---

<div align="center">

**Built brick by brick.** Everything — the renderer, the AI, the pathfinder, the damage
model, the music — is in [`lego-survival.html`](lego-survival.html).

*LEGO® is a trademark of the LEGO Group, which does not sponsor, authorise or endorse this
project.*

</div>
