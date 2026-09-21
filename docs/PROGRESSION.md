# Meta-progression — design

*Status: **BUILT** — phases 1 to 3. What shipped differs from the design below in four
places, all decided by the author; the differences are listed under
[What changed on the way in](#what-changed-on-the-way-in). The design text is kept as it was
written so the reasoning survives. Two rounds of playtesting have since changed it again —
see [Second pass](#second-pass--what-playtesting-changed) and
[Third pass](#third-pass--the-wheel-became-a-map) and
[Fourth pass](#fourth-pass--out-of-the-middle-and-one-ability-at-a-time).*

---

## The problem

You die at wave 15, or 25, and nothing happened. The run was the whole reward and the run is
gone. There is no reason to press start again except that the moment-to-moment is fun, which
carries a session and not a month.

Everything below exists to make **the wave you died on** worth something, and to make
*pushing your best* worth more than grinding a wave you have already beaten.

---

## Shape

Three tiers, two currencies, free respec.

| | bought with | gated by | what it does |
|---|---|---|---|
| **Small nodes** | studs | the ring before it | small, boring, additive — the connective tissue |
| **Notables** | studs | a wave milestone | a real change to one system |
| **Keystones** | marks | a wave milestone | large, **with a downside** |

The keystone downside is the whole point. A tree of pure upgrades is a ratchet: every run is
strictly easier than the last, the game gets solved, and you stop playing. A tree of
trade-offs is a set of *builds*, which is why people still open PoE's tree after ten years.

**Respec is free and instant.** Regret orbs are a monetisation artifact, not a design one.
Free respec makes the tree a place to experiment, and it removes the one thing that would
make a player resent a bad purchase.

---

## The currency, with real numbers

Studs already exist, already scale, and are already spent in-run on rerolls. Measured by
summing each wave's composed roster against every body's own stud range — no `studMul`, no
elites, no fortune, so a real run earns roughly 1.3–2× these:

| die on wave | that wave alone | cumulative for the run |
|--:|--:|--:|
| 5 | 140 | **496** |
| 10 | 247 | **1,977** |
| 15 | 254 | **4,211** |
| 20 | 379 | **7,750** |
| 25 | 549 | **12,383** |
| 30 | 647 | **18,709** |
| 40 | 1,344 | **36,316** |
| 50 | 2,857 | **64,563** |

A full clear is worth **15×** a death at wave 15. Bank that raw and the early runs you are
trying to reward become the ones that feel most pointless.

### Banking formula (proposed)

```
banked = floor(4 * sqrt(studsCollectedThisRun))
```

| die on wave | banked |
|--:|--:|
| 5 | 89 |
| 10 | 178 |
| 15 | **260** |
| 20 | 352 |
| 25 | 445 |
| 30 | 547 |
| 40 | 762 |
| 50 | **1,016** |

A full clear is now worth **~4** bad runs instead of 15. It explains in one line — *you bank
the square root of what you picked up* — and `K = 4` is the single dial for the whole economy.

> **Bank what you COLLECTED, not what you are holding.** `player.studs` is reduced by
> rerolls. Banking it would punish rerolling twice. This needs a new lifetime-per-run
> accumulator (`p.studsTotal`) incremented where studs are awarded.

### Marks

Marks come only from **first-time** achievements, never from repetition. This is what makes a
death at 15 pay when you have died at 15 forty times — because *16* is worth a mark and the
fortieth 15 is not.

| | marks |
|---|--:|
| First time reaching wave 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 | 10 |
| First kill of each of the 8 bosses | 8 |
| First wave-20 run with each of the 4 weapons | 4 |
| **Total available** | **22** |

Keystones cost 3 marks, so the 22 available buy 7 of them — enough to commit, never enough
to take them all. That scarcity is the build.

### Costs and pacing (proposed)

| ring | nodes | each | subtotal |
|---|--:|--:|--:|
| 1 | 6 | 60 | 360 |
| 2 | 10 | 150 | 1,500 |
| 3 | 10 | 350 | 3,500 |
| 4 | 8 | 700 | 5,600 |
| notables | 6 | 1,200 | 7,200 |
| | | **total** | **~18,200** |

Your **first death buys two nodes** (a wave-10 death banks 178, ring 1 costs 60). The full
tree is ~30 runs at wave-25 skill, ~18 at wave-50 skill. `K` and the ring costs tune this
without touching anything else.

---

## The save

One key, `bb_meta`, alongside the existing `bb_best` / `bb_fav` / `bb_diff` / `bb_weapon`.

```json
{
  "v": 1,
  "best":        { "wave": 27, "byWeapon": { "sword": 27, "axe": 12, "mace": 5, "staff": 0 } },
  "bosses":      ["brickthane", "lavabrick"],
  "studsEarned": 14820,
  "nodes":       ["hp1", "hp2", "res1", "glassbricks"]
}
```

### Four invariants — this is what keeps it alive through future changes

**1. Store node IDs, never the stats they grant.**
Rebuild the effect from the live table on every load. Rebalancing a node is then free: no
migration, no stale saves, no "my old character has the old numbers".

**2. Never store points spent. Derive it.**
`spent = sum(cost of each owned node, read from the live table)`. Change a cost later and
every existing save re-derives correctly.

**3. Never store points *earned* as a counter. Derive that too.**
Store the **achievements** — `best.wave`, `bosses`, `best.byWeapon` — and compute marks from
them at load. This is the one that matters most: if you later change *"a mark every 5 waves"*
to *"every 3"*, everyone's existing progress re-grants correctly and retroactively. An
incrementing counter can never be fixed; a derived value is always right.

**4. Nodes write into the same pools the cards write into, before the run starts.**
A node does `p.armourFlat += 4`, never `p.armour = …` — exactly as `FAV_PERKS.apply(p)`
already does. Because **every ceiling in this game lives on the derived value, not the pool**,
the tree is automatically bounded by rules that do not exist yet. Add `ARMOUR_VALUE_MAX`
tomorrow and the tree obeys it retroactively, for free. This is the single strongest
structural defence available and it costs nothing to adopt.

### Consequences that fall out for free

- **A deleted node self-refunds.** Spent is derived from the live table, so a node ID that no
  longer exists contributes 0 cost and its studs come back with zero migration code.
- **A cost increase can make `spent > earned`.** Handle it once on load: refund
  most-recently-taken nodes until the balance is non-negative, and say so in the UI.
- `v` is for *structural* changes to the JSON only — not for balance.

### Practicalities

- Wrap every read in `try/catch` and **validate-or-reset**: a corrupt save must start you
  empty, never throw. The game already does this for `bb_best`.
- **Ship export/import early** — base64 of the JSON with a short checksum, so a typo'd paste
  is rejected rather than silently loading garbage. *"I cleared my cache and lost 40 hours"*
  is the one failure that makes someone quit for good.
- **Do not build anti-cheat.** `localStorage` is trivially editable, it is a single-player
  game, and the effort belongs in the tree instead. Only guarantee that a tampered save
  cannot crash the game.

---

## Where it plugs in

| what | where | note |
|---|---|---|
| Apply the tree | `applyFavPerks()` call sites — `lego-survival.html:1830` and `:15568` | one new `applyMetaTree()` immediately before it, same shape, same pools |
| Bank the studs | `showGameOver()` `:15602` | already writes `bb_best`; extend it |
| Count studs collected | where studs are awarded, `:3406` | new `p.studsTotal` accumulator |
| Record the boss kill | `killEnemy()` boss branch `:3410` | push `e.bkey` into `bosses` |
| Tree screen | title screen, plus read-only from the character sheet | |

Rendering: **SVG in a DOM overlay**, reusing the `.sheet` chrome the character screen and
spellbook already use. Nodes are circles, prerequisites are lines, hover reuses the existing
tooltip panel. Canvas would mean rebuilding hit-testing and text layout for nothing.

Topology: each node carries `x`, `y` and `req: [nodeIds]`. That is already a graph — the only
thing separating it from PoE's is click-to-path, which a 40-node tree does not need and which
can be added later without touching the data.

---

## Starter node set (proposed)

Written in the game's own vocabulary, so every one of these lands in an existing pool.

**Ring 1 — 60 studs, no gate**

`+2 flat armour` · `+0.3 max hearts` · `+3% increased physical` · `+3% increased elemental` ·
`+4% move speed` · `+6% stud gain`

**Ring 2 — 150 studs, best wave 10**

`+4% increased attack speed` · `+2% crit chance` · `+8% crit multiplier` · `+3% to all
resistances` · `+0.01 regeneration` · `+10% pickup range` · `+4% increased spell` ·
`+3% bleed chance` · `+6% XP` · `+1 flat elemental`

**Ring 3 — 350 studs, best wave 20**

`+6% increased damage over time` · `+5% spell suppression` · `+1 flat physical` ·
`+8% increased armour` · `+2% lifesteal chance` · `+5% increased cast speed` ·
`+0.5% heart drop chance` · `+4% increased fire / frost / lightning` (three separate nodes)

**Ring 4 — 700 studs, best wave 30**

`+2 flat elemental` · `+2 flat physical` · `+6% MORE damage` · `+1 max heart` ·
`+10% increased armour` · `+4% lifesteal share` · `+8% all resistances` · `+8% increased all`

**Notables — 1,200 studs, gated individually**

| | gate | |
|---|--:|---|
| **Deep Cuts** | 25 | bleed wounds stack 9 instead of 8 |
| **Emberwright** | 25 | Ember Scatter starts at rank 1 |
| **Fault Finder** | 25 | the slam opens 15° wider |
| **Quartermaster** | 30 | one extra reroll per reward screen |
| **Cartographer** | 30 | wave modifiers roll one fewer until wave 30 |
| **Stonecutter** | 35 | +15% damage against armoured monsters |

---

## Keystones — 3 marks each

Every one is a real trade. Names are placeholders.

| | effect | cost |
|---|---|---|
| **Glass Bricks** | +40% increased damage · **−30% max hearts** | 3 |
| **Slow Burn** | the elemental toll lands 10 waves later · **−30% XP** | 3 |
| **Scavenger** | chests offer two cards · **you can never reroll** | 3 |
| **Oathbound** | start with your favourite's spell at rank 3 · **you can never take a second spell** | 3 |
| **Ironclad Oath** | +50% armour and +10% all resistance · **−25% move speed** | 3 |
| **Hoarder** | ×2 studs banked · **−20% damage** | 3 |
| **Bloodthirst** | lifesteal chance and share both ×2 · **no regeneration at all** | 3 |

The **elemental toll** is the single best keystone hook in the game — it is the thing that
currently ends runs, so a node that moves it is felt immediately and understood instantly.

---

## The run-end screen

**This matters more than the tree's contents.** The tree is the reward; this screen is what
makes you feel it. Right now you die and a stat block appears.

```
                    WAVE 24          best 27

        +340 studs banked                    1,240 total
        NEW — wave 20 with an axe            +1 mark

        Thirsting Grip is 160 studs away         [ SPEND ]
```

Four rules for it:

- Show **progress toward the next thing**, not just what happened.
- Name the nearest affordable-soon node by name, with one click to go spend.
- Call out anything that is a **first**, loudly — that is the mark economy made visible.
- If nothing new happened, say so honestly rather than inventing a reward. A screen that
  celebrates every run teaches you to ignore it.

---

## Power budget, and the audit that enforces it

Declare it and hold to it:

> **The finished tree is worth about one extra favourite.**

Without a stated budget, every future node is a small yes and the sum quietly trivialises
wave 25. With one, you can add content for years.

Then make it a **test**, in the same style as the rest of the suite: stand up a probe hero,
apply the whole tree, and compare a basket of derived stats against a no-tree hero. Fail the
build if the total exceeds the declared budget. That is what stops the creep — not
discipline, which does not survive six months.

Everything the tree grants is already inside the game's existing ceilings, because of
invariant 4. A fully-specced hero still cannot exceed `RES_CAP`, `INC_DMG_CAP`,
`MORE_DMG_CAP`, `CRIT_CHANCE_CAP` or `ARMOUR_VALUE_MAX`.

**Optional, if you want the PoE feel harder:** let the world scale with investment — a small
monster-level bonus proportional to tree power. It makes the tree a *choice* rather than a
ratchet. It also risks feeling like the game is taking back what it gave, so it should be
built last, behind a toggle, and measured.

---

## Phases

Each phase is shippable on its own.

1. **Save layer + banking + run-end screen.** No tree at all. Studs accumulate, achievements
   record, the death screen shows what you earned. This alone fixes "no reward for dying" and
   is maybe a tenth of the work.
2. **The tree.** Data table, `applyMetaTree()`, SVG screen, free respec. Small nodes and
   notables only.
3. **Marks and keystones.** The milestone economy and the trade-off nodes.
4. **Unlocks.** New favourites, a fifth weapon, biomes behind milestones. The safest
   progression that exists, because it cannot break per-run balance — worth mixing in
   generously alongside the stat nodes.

---

## Open questions

1. **Difficulty and the tree.** Should progress be shared across Easy / Hard / Nightmare, or
   per-difficulty? Shared is friendlier; per-difficulty stops you farming Easy for a
   Nightmare advantage. *Leaning: shared, but marks only from Hard and above.*
2. **Does the tree apply in the test bench?** It should be toggleable there, or every balance
   number you read is contaminated by your save.
3. **Is `K = 4` right?** It sets the whole economy. Easy to change later — that is the point
   of deriving everything — but worth playing with.
4. **How visible should the tree be on run one?** Showing a locked 40-node tree to a new
   player is either a promise or a wall. *Leaning: show ring 1 only until the first death.*


---

## What changed on the way in

Built across phases 1–3. Four decisions moved:

**1. Progress is PER DIFFICULTY, not shared.** A tree earned on Easy is not a tree earned on
Nightmare — `bb_meta` holds three independent records and switching the difficulty on the
title screen switches which one you are spending. The design leaned the other way; the
author's call was that they are different games.

**2. One visible currency, not two.** Marks were folded into **skill points**. Points come
from two sources — `floor(studsEarned / 600)`, capped at 14, plus one for each first-time
achievement — and the total is capped at **26**. Studs are the progress bar toward the next
point rather than a second thing to spend.

**3. The tree cannot be finished, ever.** It costs **54 points** and the ceiling is **26**,
so the most you can ever hold is 48% of it. This is the change that makes the tree a set of
builds rather than a ladder, and it is asserted in `meta.js` rather than left to drift.
Respec is free and instant.

**4. The whole tree is visible from the first run**, locked nodes included. A tree you
cannot see is a list.

The test bench does **not** carry the tree: `applyMetaTree()` runs only when a real run
starts, so every preview and throwaway hero reads the raw numbers. A switch on the tree
screen turns it off for real runs too, for clean balance readings.

### What the audit caught

The power-budget test earned its place on its first run. The greediest 26 points of pure
offence came out at **×3.31 damage** — except that was the probe applying the tree twice on
top of `startGame`, which already applies it. The true figure was **×1.97**, still over
budget: `Heft` and `Charged` were +0.5 flat damage on a 1.5 base, which is +33% at wave 1 and
nothing by wave 40. Both dropped to +0.3, and the greediest build now measures **×1.77**
against a declared ceiling of ×1.80. For scale, `Sword & Steel` alone is worth about ×1.7 —
which is what *"about one extra favourite"* was supposed to mean.

### The hole invariant 4 does not cover

Pools are bound by the ceilings on the numbers they feed, so a node that writes a pool is
safe by construction. **Flags are not pools.** `tollDelay`, `rerollBonus`, `chestCards` and
`bankMul` are switches nothing rebuilds and nothing caps, so `metaClampFlags()` bounds them
by hand where the tree is applied — the one place the architecture needed help rather than
trust.


---

## Second pass — what playtesting changed

Four things came back from actually playing it, and one of them was a bug.

**The wave you died on is not the wave you cleared.** Dying to the wave-30 boss was banked
as a wave-30 run and paid a wave-30 point. A run now banks `wavesCleared()` — a counter set
where a wave actually completes — and the screen reads *"CLEARED 29 · died on 30"*.

**A tree you can drop points anywhere in is a shopping list.** Nodes now carry `req` edges
and a node has to *touch* something you already hold, with everything you hold tracing back
to your door. The inner ring is a circle you can walk around and ring three links sideways,
so there is always more than one route out — each with a different price.

**Respec became a mode.** Turning it on lets you click nodes back one at a time, free; a
node holding up a branch is refused until the branch is unwound. `CLEAR ALL` survives as its
own button. *(The first version of this shipped with the button relabelled and the old
handler still wired underneath, so "REFUND MODE" silently wiped the whole tree. The driven
UI test caught it.)*

**One tree per WEAPON, per difficulty — twelve.** The layout is identical and the four
weapons enter it at four different doors: a sword between WARFARE and PRECISION, a staff
between ELEMENTS and ARCANA. Points are earned by the weapon that earned them. This is what
makes all four worth playing, and it came from the author, not the design.

The tree grew to **8 spokes × (5 small + notable + keystone) + 4 doors = 60 nodes, 84
points**, against the same ceiling of 26 — so the most anyone can hold fell from 48% to
**31%**.

### The budget measure was wrong

The first audit compared **damage**. That quietly rewarded exactly the wrong thing: a glass
build buys damage by selling hearts, so a damage-only ceiling waves it through while
punishing an honest build that took none of the downside.

It now measures **total power — damage × hearts**, which cannot be gamed, because a node
with no downside raises both halves. Measured:

| | damage | total power |
|---|--:|--:|
| straight 19-point build | ×1.63 | **×1.62** |
| Glass Bricks 20-point build | ×2.08 | **×1.48** |

More damage, *less* total — which is exactly the trade Glass Bricks advertises, and the
number that proves it is now an assertion.

### One source of truth for every node

Nodes are declarative: each one carries `mods`, and a `META_STATS` registry holds the
writer, the wording and the formatting together. The hero, the hover card and the running
stat panel are all read off the same list, so they cannot drift — and a stat that does
nothing for the weapon you are on is dropped from the panel and labelled in the tooltip
rather than silently sold to you.

---

## Third pass — the wheel became a map

The wheel was legible and dull. Eight spokes radiating from a hub is a *menu* drawn in a
circle: every route is the same length, every weapon's journey is a rotation of every other
weapon's, and the only decision is which spoke. The second round of playtesting asked for
the thing Path of Exile actually does — **mess**.

### The four doors were not far enough apart

They sat between adjacent spokes, which put them within a couple of nodes of each other. A
weapon's identity in a shared tree is *distance*, and there was none. The doors now sit on
four sides of the map, ~640 units from the middle and nowhere near one another.

The test for this is not "the doors are far apart" — that is a fact about coordinates. It is
**the price list**: the cheapest route from every door to every prize, for all four weapons.
Those four vectors have to be different, every far corner has to cost somebody at least 6
points more than it costs somebody else, and the dead centre has to cost everybody about the
same. A corner belongs to a weapon; the middle belongs to nobody.

### Clusters and corridors, instead of rings

Twenty-five clusters — a little ring of minor nodes you can walk around, sometimes with a
notable or a keystone in the middle of it — joined by seventy corridors of cheap travel
nodes. The corridors are what makes the tree cost something: **130 of the 225 nodes are
corridor**, so the ceiling rose from 26 to 34 and the power it buys did not move at all.

Three things fall out of that shape and none of them had to be built:

- **Only every other node of a cluster touches its middle**, so you always walk part of a
  cluster to earn the thing at its centre, and which side you come in on decides what you
  pick up on the way.
- **`req` is the adjacency, both ways.** A corridor is walkable from either end, which is
  what makes there be more than one route to anywhere. The four doors are the exception —
  nothing leads *to* a door.
- **No single cluster is a choke point.** Knock any one of them out and the middle of the
  map is still reachable from all four doors. That is an assertion, and it fails against a
  build with only one way in.

### The nudge

A corridor drawn straight through the middle of a cluster reads as a node belonging to it,
and clicking for one and getting the other is the kind of bug nobody reports. Rather than
hand-tuning every corridor, a **relaxation pass** shoves travel nodes off whatever they
landed on — a little, and never more than 46 units from where the corridor put them. The
next cluster anyone adds cannot quietly bury one.

`meta.js` asserts no two nodes overlap at all; with the relaxation disabled it reports nine
collisions, one of them a dead-centre hit.

### The leak that was not a leak

Playtesting reported the tree being "shared" between weapons. The live flow was already
correct — a staff run moved only the staff's record, and points placed on the mace were
still there after a trip through the axe. The leak was in the **version-1 migration**: a save
from before the tree was split by weapon was copied into *all four* weapon records.

It now lands on the one weapon that was selected when it was written (`bb_weapon`), and
nowhere else. Nothing earned after the fix can cross between weapons, and nothing ever
could — only the one-time import could.

### The rebuild cost no migration code

Every node ID from the wheel is gone. Because the save stores node IDs and derives
everything else (invariants 1–3), every existing save simply dropped its unknown nodes and
handed back all of its points to re-spend. There is no migration code for this, and there
was never going to need to be.

---

## Fourth pass — out of the middle, and one ability at a time

The third pass put the four doors on four sides of the rim. That made distance mean
something, but it meant the weapons never met: a sword's early tree and a staff's early tree
had nothing in common, and neither did the players. This pass turns it inside out.

### Everyone starts in the middle; nobody starts the same way

Four doors in the dead centre, each opening into **a cluster only that weapon can afford** —
reach and riposte for the sword, bloodscent and wild swings for the axe, the weight of the
slam for the mace, focus and warding for the staff. Your own beginning costs 4 points; it
costs everybody else 14 to 16.

That is the whole trick, and it is worth stating as an assertion rather than a paragraph:
*every weapon's own opening is strictly cheaper for it than for any other weapon.* Wire all
four doors into the same opening and the test names the weapon that lost its identity.

Between each pair of doors sits a **crossing** both of them can reach, so your neighbour's
half of the map is a detour rather than a wall.

### The prizes moved to the rim

Twelve keystones, all of them on the outside, 17 to 25 points from any door. One is a
journey in a single direction; two is most of a ceiling. The test that holds this is the
**union** of four real walks to the four corner keystones — 84 to 86 points against a
ceiling of 46 — which is a different claim from adding four prices up, because the routes
share their inner corridor.

### One ability at a time

The outer ring is now eight clusters that each name a single brick: `+1 damage with the
Guardian Brick`, `+1 Brick Blaster pierce`, `+1 Storm Brick fork`, `+12% increased Bomb
Volley rate`, a wider Block Freeze, a heavier bomb. A tree that only ever says *+5% damage*
has no opinions about anything; a node that names one of your bricks makes the map worth
re-reading every time the deck hands you something new.

Every one of those writers clamps **exactly where the brick that grants it clamps** —
`BLAST_MAX_PIERCE`, `CHAIN_FORK_MAX`, `CLUSTER_MAX`, `ORBIT_MAX_R` — and the rates go
through the same `paceOf` floors the cards do. Invariant 4 holds: the ceilings still bind,
including ones added later.

### Resistance, everywhere

It used to be three nodes in a corner. It is now in every crossing, every ring-B cluster,
every far corner, and — the part that actually changes how the tree feels — **every step of
armour or core corridor is +1% to all three resistances**. A long walk in any direction
leaves you a little harder to burn, freeze and shock, whatever you were walking toward.

### A patch is a respec, not a reset

The save now carries `tv`, the version of the tree its points were spent on. When the layout
moves, every record whose `tv` is stale hands its points **back** — keeping every stud,
every best wave and every boss — and the tree screen says so in as many words.

This was always *nearly* true: the save stores node IDs and derives the rest, so `metaTrim`
would have dropped the unknown nodes one at a time anyway. The version stamp does not change
the outcome; it makes the outcome **legible**, and it is the difference between a player
seeing "your points are back, spend them again" and a player quietly wondering what happened
to their tree.

### Zoom

Four hundred nodes do not fit on a screen at a size anybody can read. Scroll zooms toward
the cursor, drag pans, `FIT` restores the whole map. It is one `viewBox` attribute written
straight onto the `<svg>` — re-rendering four hundred nodes on every wheel tick would make
the whole thing feel like mud — and a drag is never mistaken for a purchase.

### What it costs

| | third pass | fourth pass |
|---|--:|--:|
| nodes | 225 | **412** |
| of which corridor | 130 | **268** |
| tree cost | 246 | **444** |
| ceiling | 34 | **46** |
| most of it anyone can hold | 14% | **10%** |
| straight build, total power | ×1.60 | **×1.96** |
| Glass Bricks, damage / power | ×2.08 / ×1.48 | **×2.46 / ×1.76** |

The declared budget rose from ×1.80 to ×2.00 with the ceiling. That is a deliberate choice,
not a drift: thirty stud points is eighteen thousand banked studs, and a player who has done
that should be about twice the hero they started as.

### The tree nobody could click

The zoom shipped broken: `setPointerCapture` on the `<svg>` to follow a drag retargets the
pointer, so the `click` that follows is delivered to the `<svg>` and never reaches the node
that was pressed. Every node in the tree was dead.

`metaplay.js` passed. It drove the map with `dispatchEvent(new MouseEvent('click'))`, which
skips the pointer handlers the map installs — **a test that clicks in a way no hand can is a
test that proves nothing**, and it had been doing that since the tree was first built. It
now moves a real mouse, presses, wobbles a pixel or two the way a hand does, and releases.

The drag is tracked on `window` instead, with a six-pixel slop so a wobble stays a click, and
nothing is captured.

Then a second question worth asking the browser rather than the author: *is anything sitting
on top of a node?* `elementFromPoint` at the centre of every visible node, compared against
the node itself. It found six more nodes buried under cluster labels — the labels are painted
over the map deliberately, and were swallowing clicks as well as pixels. `pointer-events:
none` on every decorative element fixed those.


---

## Fifth pass — highways and pockets

The fourth-pass map joined **cluster to cluster**. Every corridor ran from one cluster's
nearest node to another's, which meant every long journey went *through* other people's
clusters. A lightning build walking to the far side of the map collected increased physical
damage on the way and paid a point for each node of it.

That is exactly backwards. In the game this borrows from, the thing you walk along gives you
**one attribute and nothing else**, and the interesting nodes hang *off* it. You skip what
you do not want by driving past it, not by paying for it.

### Roads carry you; clusters hang off them

Three **ring roads**, one attribute each, all the way round the map. Twenty **spokes** across
the gaps between them, cycling through all three attributes so whatever you are collecting
there is a way further out still paying you in your own colour. Four **door roads**, one per
weapon, each paying a different attribute — the staff walks out of the middle on
intelligence, the mace on strength.

A ring is a **jittered polygon**, not a circle, and a spoke begins and ends on a ring's own
**vertex** — so where two roads meet they meet exactly. Every polyline is then sampled at a
fixed step, and any two samples landing within `ROAD_MERGE` of each other become **one node**.
That merge is the whole trick: it is what turns a pile of independent polylines into a network
with real junctions rather than pairs of nodes sitting four pixels apart pretending to be one.

A cluster attaches by **one short spur** to the road node nearest it, at the cluster node
nearest that road, and by nothing else. It is a **pocket**: one way in, the same way out.

### The property that had to be proved, not asserted

*Nothing routes through a pocket.* It is easy to say and easy to break — one stray `add()`
and a cluster is on somebody's shortest path again. So `roads.js` deletes **every node of
every cluster in turn** and checks that every *other* cluster is still reachable from all four
doors. Thirty-six clusters × four weapons × thirty-five targets, and not one of them is a
choke point.

Then the thing a player would actually notice: the **cheapest walk from any door to any
keystone**, and how much of it is somebody else's cluster. Zero, in all forty-eight of them.
Between 91% and 94% of each walk is road.

And the highway itself has to be long, and it has to *go* somewhere — a knot of forty
dexterity nodes in one corner is not a highway. So: the longest **connected run of travel
nodes all paying the same attribute**, and the distance between the two furthest-apart nodes
of that attribute.

### What it costs

| | fourth pass | fifth pass |
|---|--:|--:|
| nodes | 412 | **457** |
| of which road or spur | 268 | **300** |
| clusters | 36 | **36** |
| cluster-to-cluster links | 104 | **0** |
| longest one-attribute run | — | **44 / 95 / 123** (dex / str / int) |
| how far that attribute spans | — | **3065 / 3075 / 4323** px |
| a keystone, from your door | 17–25 | **22–32** |
| your own beginning | 4 | **3–4** |
| ceiling | 46 | **60** |
| a road node is worth | 3 attribute | **2 attribute** |
| straight build, total power | ×1.96 | **×2.42** |
| Glass Bricks, damage / power | ×2.46 / ×1.76 | **×1.92 / ×1.37** |

`META_TREE_VER` goes to **3**, so every existing save is refunded in full and respec'd rather
than reset — the same migration the fourth pass used, and still the only one the tree needs.

### The break-builds

Five, one per property, each caught only by the assertion meant for it:

| broken build | what it broke | which assertion caught it |
|---|---|---|
| `links` | cluster-to-cluster corridors put back | *a walk collects somebody else's cluster* |
| `nodoor` | the doors' private roads removed | *a pocket is on the way to somewhere* |
| `onering` | dexterity survives only on the inner ring | *a highway does not cross the map* |
| `mixed` | every road node pays a different attribute from the one before | *no long single-attribute highway* |
| `nowayin` | a door no longer opens into its own beginning | *your own beginning is a trek* |

### Conversion, on the map

Five nodes across the elemental and affliction side now hand over **damage conversion**
itself — `10% of physical converted to fire` in THE FORGEFIRE, `12% to lightning` in FLUX,
`15% to cold` in THE CONFLUENCE, and chaos gain in OPEN WOUNDS and THE ORCHARD. They go
through `convAdd`/`convGain` like every brick does, so the legality rule lives in exactly one
place and a nonsense pair simply does not go in.

### The test that could not fail

`meta.js` ended with `console.log('ERRORS:', …)` and nothing else. It never set an exit code,
so the runner called it green no matter how many invariants it had just printed as broken —
and it *was* printing one: the fifth-pass tree came in at **×3.04 total power** against a
declared budget of ×2.60, and the suite reported `ok meta` anyway.

Two fixes, not one. The runner now fails on a non-empty `ERRORS` or any page error, which is
what made the budget visible. And the budget itself: a road node was worth **3** of an
attribute in a map where a walk was fifteen nodes, and the fifth pass made a walk thirty. Two
per node puts a straight build back at **×2.42**, under the ceiling it is held to, without
touching the point ceiling the tree is built around.

---

## Sixth pass — three countries

Two complaints, one screenshot each. *"There is like 12 attribute nodes in a row here, make
the spacing bigger."* And, next to a picture of Path of Exile's tree cut into a red, a green
and a blue third: *"separate the attribute nodes more, like this."*

### The stride

The fifth pass laid road nodes 120px apart, and between two pockets on the outer ring that is
a dozen of them nose to tail. The fix is the obvious one — a longer stride — and the cost of
it is that the map gets cheaper to cross, which is most of what the rest of this pass is
about.

| | fifth pass | sixth pass |
|---|--:|--:|
| road nodes | 267 | **134** |
| median gap between two of them | 119px | **210px** |
| most you cross with no choice in them | 7 | **5** |
| nodes | 457 | **328** |
| tree cost | 489 | **360** |

### The ground decides

A road used to name the attribute it paid. Now it does not: `roadNode` reads the wedge the
node is standing in, so a ring is red on the red side and blue on the blue side and changes
colour exactly at the border. Nothing has to be kept in step by hand, and adding a road
anywhere gets the right colour for free.

That has one sharp edge. The nudge pass that keeps travel nodes off cluster nodes can shove a
road node thirty-four pixels, which near a border is enough to move it into the next country
while it is still carrying the last one's attribute. So the ground is read **again**, after
the nudge, and that is the reading that sticks. The break-build that set every road to
strength caught nothing at first — the second reading quietly repaired it, which is its own
small proof that it is doing something.

### The border had to cost something

Cutting the map into countries and then leaving the ring roads whole is decoration. With all
three rings complete, every keystone on the map sat within **two points** of every door: half
a lap of the inner ring is eight points, and from anywhere on it you can take any spoke
outward. `meta.js` said so — *"a prize costs everyone the same"* — the moment the sectors went
in.

So the inner and middle rings are **cut at every border**: laid as loops, then the three edges
that cross a frontier are dropped. The only road between countries is the outer ring, and
going the long way round it is a journey.

| | inner ring whole | both inner rings cut |
|---|--:|--:|
| sword to its own abilities | 19 | **19** |
| sword to somebody else's | 21 | **30** |
| axe, home / away | 16 / 21 | **16 / 25** |
| staff, home / away | 18 / 19 | **18 / 22** |

The abilities also had to move **off** the borders. An ability ten degrees from a frontier
belongs to both sides, and with three of them sitting on one the staff's advantage in its own
country came out at a single point.

### What the budget caught this time

The hand-written route lists that `meta.js` measured its power budget with were written for a
map with no countries in it, so they measure a **wanderer** — and this pass made wandering the
expensive way to play. A build that buys its own third of the map and nothing else was never
being looked at, and it was **×2.75 total power** against a declared ×2.60 while the test
reported green.

`meta.js` now walks each country, nearest node first, to the ceiling, and takes the worst of
the three. The red country came in over because every point of increased hearts in the game
is now within twenty points of the sword's door — which is the clustering working exactly as
asked, so the numbers gave instead: Warden 12% → 8%, Hale 6% → 5%, Stoic 5% → 4%, Sturdy
4% → 3%, Warmonger and Swordsman a point of increased physical each.

| stay-at-home build | power |
|---|--:|
| red (sword) | **×2.52** |
| green (axe's country, measured on a sword) | ×1.32 |
| blue | ×1.21 |

### The adjacency that read double

`roads.js` built its adjacency by pushing both ends of every `req` — but `req` already holds
the edge from both sides, so every neighbour appeared twice and every degree came out even.
The run-length check looked for nodes with exactly two neighbours and found four in the whole
map. It was asserting nothing at all, and had been since it was written an hour earlier.
Deduped through a `Set`, it reads 5.

---

## Seventh pass — shapes, branches, and the suite that could not fail

### The suite that could not fail

`meta.js` was fixed two passes ago for printing its errors and exiting 0. It was not the only
one. **Thirty-eight of the forty-one tests did exactly the same thing** — they end with

```js
console.log('ERRORS:', JSON.stringify(errs));
```

and nothing else, so the runner, which only ever looked at the exit code, called them green
however many assertions they had just printed as broken. Every "failure" this suite has ever
reported was a *crash*: a missing file, a page error, a Playwright timeout.

Fixing thirty-eight files one at a time would leave the thirty-ninth free to do it again, so
the fix is in the runner: a run fails if it exits non-zero, **or** prints a non-empty
`ERRORS` list, **or** prints a non-empty `FAILURES` list, **or** reports a page error.

It found exactly one thing, and it had been hiding since attributes went in.

### The life assertion that never held

`poe3.js` checks that increased and more scale max life the way the pool rule says. It
compared `maxHp` with `+100% increased` against `Math.round(hpBase * 2)` — where `hpBase` is
itself `Math.round(pool(...))`. A sword starts with ten strength, which is +5% increased
hearts, so the raw pool is 6.3 and `maxHp` is 6. Doubling the *rounded* number and comparing
it against the game's rounded *double* was never going to hold: 6 × 2 is 12, and
round(6.3 × 2) is 13.

The expectation now comes from the same rule the game documents — increases add into one pool,
more multiplies separately — so `+100% increased` on a hero already carrying +5% is ×2.05 and
not ×2 × 1.05. It still fails on a build where increased hearts multiply instead of adding,
which is what it is there for.

### Five shapes of pocket

Thirty-six clusters that were all the same object is a map with one idea in it. A pocket now
says what shape it is:

| | |
|---|---|
| **ring** | a closed ring; the prize touches every *other* node |
| **wheel** | a closed ring and the prize touches all of it — one step in, the rest is yours to leave |
| **fan** | no ring. The prize is the hub and every leaf hangs off it alone |
| **arc** | an *open* ring: a long way round and a short way, prize across the gap |
| **chain** | a tendril; the minors run in a line and the prize is at the far end |

A fan with no notable or keystone in it is a handful of nodes joined to nobody — eight of them
were, the first time this ran. A hubless fan is a ring.

### Three ways out of a door

A door used to have exactly two: into your own beginning, and onto your own road. Two is not a
decision, you take both. Each door now fans into **three short branches** of its own, one
either side of the way into your beginning, two nodes long and dead ends. They are cheap and
you cannot have all of them at once. Other weapons' branches are hidden, or they are four
pairs of nodes floating in the middle wired to nothing visible.

### More connections, and a stride that is not a ruler

Rings and spokes alone make a grid of big empty quads with two ways round each. A **cross-link**
is an extra edge between two road nodes of different roads that are near each other. Never
across a border and never along one road, because those are the two things holding the
countries up.

The reach matters more than it looks. **An edge is free** — only nodes cost points — so a
cross-link that reaches far is a free shortcut, and at 470px they took the walk to a far
keystone from twenty-seven points to **ten**. A long jump as a free edge was tried and thrown
away for the same reason: one node for a thousand pixels is four strides of ground for one
point. Cross-links reach 285px now, one stride and a bit, which joins what is already
touching and skips nothing.

The variety went into the **stride** instead. Each segment of a road picks its own step out of
a hash of where it is, ±42%, so some stretches are three short hops and the next is one long
reach across an empty quarter. Same number of nodes, same price, and it stops looking machined.

### Conversion carries its whole history

A converted part is scaled by **every type it has ever been** — physical to cold to fire is
increased by increased physical, increased cold *and* increased fire. Totals cannot carry
that: cold arriving from two different chains would have to share one provenance, and
whichever union you picked would overpay the half that did not take that road.

So damage travels the pipeline as **parcels**. Each one remembers its own chain and splits
when it converts, and `convParts` asks the caller what each parcel is owed given where it has
been. The global elemental pool is added once however many elements a chain touched — per
element it would be a free multiplier for anyone converting twice, and the break-build that
does exactly that is caught.

Every legal pair in the chain now has a brick behind it in both forms: fifteen of them, where
there were seven.

### Chaos, properly

Chaos was "the type nothing resists", which is a hole in the rules rather than a damage type.
It now has a resistance of its own that almost nothing in the roster carries, and the rule
that makes it interesting: **nothing that says "to every elemental resistance" covers it.** Not
a brick, not a tree node, not a curse that sunders them. `ELEMENTS` stays three long and a new
`RES_KINDS` is the one that includes chaos.

And **poison is chaos damage over time**, the way bleed is physical and burn is fire. It reads
increased chaos damage as well as increased damage over time, it pays the monster's chaos
resistance, and the dose you are carrying is reduced by yours — which is the first thing in
the game that makes chaos resistance worth a brick on its own.

---

## Eighth pass — the lattice

The seventh-pass map was rings with pockets hanging off them, and it showed. Clusters had one
way in, borders had gaps you could see but not cross, and the whole thing read as a diagram of
a tree rather than a tree. This pass throws the road/pocket model away.

### Hubs and lanes

Five rings of **hubs**. Every hub joins its neighbours around its own ring, the nearest hub on
the ring outside it, *and* the nearest hub on the ring inside it — that last one is what
leaves no hub with a single way out and turns a wheel-with-spokes into a web.

A hub is either a plain **junction** (one travel node, one attribute) or a **cluster**. A
**lane** is a short chain of travel nodes between two hubs, and it attaches to whichever node
of a cluster is nearest the other end. So a cluster with four lanes meeting it has four ways
in, you enter where you arrive, and routing *through* one is an ordinary move.

That kills the old "nothing routes through a pocket" invariant — deliberately. The property
that actually mattered was never "a cluster is a dead end", it was **"there is always a way
round"**, and that is what `roads.js` asserts now: knock out every node of any cluster and
every other cluster is still reachable. Clusters average **2.6 ways in** and not one of the 74
has fewer than two.

### Nothing is placed by hand

Hubs come off a ring table. Clusters come off a template library. Both are chosen by a hash of
where they are, so the map is identical every time and **adding a weapon, a spell or a damage
type extends it without anybody moving a coordinate** — which is the thing that was asked for
and the thing the old hand-placed 36 could never do.

- Every legal conversion pair gets a cluster, generated from `CONV_ORDER`.
- Every weapon gets a cluster of its own nodes, generated from `WEAPON_KEYS`.
- Every spell gets a five-point legendary on the outer ring, generated from `SPELL_KEYS`.

The one-of-a-kind things claim their sites first, deepest first, each taking the free hub in
its own country whose hash is lowest. Then the repeatables fill a share of what is left, and
whatever is still free **stays a junction**. That last clause is most of the map.

### Three things the generator got wrong, and how

**Four conversions were nowhere.** Left to the random fill, `cv_phys_shock` and
`cv_frost_chaos` (and their gain-as-extra twins) never landed on a hub at all — the promise
"every damage type can be converted" was true of the *stat table* and false of the *map*.
Templates can now be marked `must`, and those claim a site before the dice are rolled.
`nodes.js` checks the map, not the table.

**The same cluster four times in one corner.** Picked purely by hash, WHETSTONE turned up four
times within one screen and the red country read as a copy-paste. A template already standing
within 1500px is off the list; the closest pair of copies is now 1825px apart.

**Packing clusters onto every other hub left nowhere to walk.** At 46% density the longest run
of plain attribute nodes in the green country fell to 24, and a country stopped being
something you could cross without shopping. 40% density, and most of the map is lane again.

### The budget, again

A tree 2.4× bigger means sixty points buy a much better sixty. A stay-at-home strength build
came in at **×6.37 total power** against a declared ×2.60 — and almost all of it was one
thing: **flat damage on a weapon whose base is 1.** Three flat nodes and a weapon oath tripled
the base of every swing before a single increase touched it.

Flat damage is the deck's job. Sharpened Blade hands over half a point at common and eight at
legendary *because it is one brick*; a tree node you can buy four copies of hands over a tenth.
That, one attribute per lane node instead of two, and a 15% trim of the repeatable groups —
which are not one node each, they are four — put it at **×2.38**.

| | seventh pass | eighth pass |
|---|--:|--:|
| nodes | 343 | **770** |
| of which lane | 128 | **482** |
| clusters | 38 | **74** |
| tree cost | 373 | **859** |
| a lane node is worth | 2 attribute | **1 attribute** |
| flat physical, per node | 0.4 | **0.08** |
| stay-at-home power | ×2.48 | **×2.38** |

### And two more tests that were lying

`meta.js` asserted that rebalancing a node moved the hero by `0.99 − 0.04`, with the `0.04`
typed in. The node it names is no longer worth 0.04 and was never going to be forever; the
delta now comes off the node's own old value, which is the only version of this test that
survives a rebalance — the exact thing it exists to allow.

`roads.js` asserted a flat "at least 28 nodes in the longest single-attribute run". That number
moves every time the library grows, and chasing it down each time is a ratchet rather than a
test. It is a proportion now: a country's biggest walkable piece must be a real share of the
lanes it has, and never under twenty nodes whatever else changes.

---

## Ninth pass — the highway rule

The eighth pass put clusters ON the lattice: a lane ran hub to hub, and where a hub held a
cluster the lane terminated on one of its nodes. It made clusters properly connected — three
or four ways in — and it broke the thing that matters more. **To travel past a cluster you had
to buy into it.** The road went through the shop.

### One rule, and everything follows from it

> **Every hub is a travel node, every lane node between them is a travel node, and the whole
> of it is one connected piece.**

Clusters go in the **empty middle of a quad** — between two rings and two radial lanes, where
nothing else is — and join the road by **connectors**: two or three short edges, each from a
different stretch of lane, each landing on the cluster node nearest it. One connector is a
pocket you must leave the way you came. Two makes a cluster a **detour**: off the road, take
what you came for, back on further along. A cluster on the outer rim gets one, because there
is no further along.

### The measurement that says it worked

Not "the clusters look attached" — the property, on the real graph, for every door and every
prize:

| | |
|---|--:|
| lane nodes | 677 |
| in the largest connected piece of them | **677** |
| largest walkable single-attribute piece, per country | **269 / 205 / 201** of 270 / 205 / 202 |
| prizes reachable on the highway alone | **180 of 180** |
| worst extra cost of staying on it | **3 points** |
| mean extra cost | **0.29** |
| lane nodes with one way out | **0** |

Cutting through a cluster is still sometimes the cheapest path by a point or two, and that is
fine — PoE is the same. What is not fine is being *forced* to, and that is what the
`blocked` count being zero means.

### And the doors

Four ways out of a door was right; two of them ending after two nodes was not. Each of the
four is a lane to a different hub of the inner ring now, and the first node of each carries
the weapon's own flavour instead of a plain attribute — so the beginnings are still different
and none of them stops. The rule is: **a dead end is allowed on the rim and nowhere else.**

### What it cost to check

The break-build that matters here swaps either end of every lane for a nearby cluster node —
which is exactly the eighth-pass map — and it trips four assertions at once: the highway
splits into pieces, four prizes become unreachable without walking through somebody else's
cluster, the detour cost doubles, and every door's ways out stop dead. Three more breaks cover
the connectors and the doors on their own.

| | eighth pass | ninth pass |
|---|--:|--:|
| nodes | 770 | **990** |
| of which highway | 482 | **677** |
| clusters | 74 | **98** |
| tree cost | 859 | **1072** |
| stay-at-home power | ×2.38 | **×2.27** |

### Hidden is not owned

A door's four first steps carry the weapon's own flavour and belong to it alone. The map has
always drawn somebody else's nowhere — and that was the whole of the enforcement. The graph
walked straight through them, so the axe could path round the inner ring and quietly buy the
sword's Edge Work, invisibly, out of a node it could never see.

`metaMine` is the rule now, and it is in both places that matter: `metaCanBuy` refuses one,
and `metaConnected` will not path through one. Asked of the game's own surface — the axe
holding every node it legally can — none of the sword's four is buyable or even connected.

It surfaced because the ninth pass made those nodes part of a lane that continues, instead of
the stub it used to be. The bug was older than the change that exposed it.

---

## Tenth pass — shapes, icons, and nothing on top of anything

> *"You got nodes that are overlapping i dont like that. Furthermore, you seem to only have
> triangles all the time... please add some icons to the already existing nodes, for all
> attribute nodes add a small + symbol and their respective color. Make the skill tree
> interesting, not just nodes everywhere."*

Three complaints, and the first one was not what it looked like.

### "The nodes are overlapping"

They were not. The closest pair of node circles on the ninth-pass map was **14px clear**, and
the assertion that says so had been passing for two passes. So either the assertion was wrong
or it was measuring the wrong thing, and it was the second: it measured node circles, and a
player does not see node circles. They see the whole picture. Measured on what is actually
drawn, on the same map:

| | ninth pass |
|---|--:|
| node circles overlapping | 0 |
| **edges drawn through an unrelated node** | **23** |
| **nodes standing inside a foreign cluster's blot** | **25** |
| **cluster names printed over a node** | **15** |

All three are "overlapping" to anybody looking at the screen, and none of them was a node
circle. The lesson is the one this project keeps relearning: **an assertion has to read the
surface the player reads.** A node-radius test is a proxy for "does this look clean", and the
proxy passed while the thing it stood for was plainly broken in a screenshot.

The fixes, in the order the picture needed them:

- **An edge through a node.** Straight node-against-node shoving never sees this one — the two
  ends of the edge are far away and the node sitting on top of it is touching nothing at all.
  The layout pass now also shoves a node off any edge it is standing on. And a slip road off
  the highway picks the cluster node it can reach *without* crossing another node of that
  cluster, instead of the nearest one full stop.
- **The blot.** It was a fixed circle round where the cluster was *asked* to go. After the
  layout pass settled, that was the wrong place and the wrong size, and it shaded whatever
  highway ran past — and a stretch of road inside a shop's shadow reads as part of the shop.
  It is measured off where the nodes ended up now, then pulled in until it holds nothing that
  is not its own and touches no other blot.
- **The name.** It went straight out from the middle of the map and landed where it landed.
  Twenty-four directions are tried at two distances each now, and the first that lands on no
  node and no other name wins. All 98 find one.
- **And room to grow into.** A six-node figure with its prize hung off the end reaches half
  again as far as the circle it was drawn on, and two of them on neighbouring rim sites grew
  into each other. A cluster is never drawn wider than the gap to the site beside it.

### "Only triangles"

True, and the cause was one line. Every template held **three** minors, and a cluster placed
its minors on a circle — and three points on a circle is a triangle. Ring, wheel and arc all
came out the same shape because the shape had nothing to work with.

So the **figure decides how many nodes there are**, not the list of stats. The stats are dealt
round the figure and repeat if the figure is the longer of the two, which is what a six-node
arc of *Smoulder, Rimebite, Arcing, Smoulder, Rimebite, Arcing* is for — and repeating a minor
inside one group is what the tree this is borrowed from does everywhere.

Ten figures instead of five: **ring, wheel, arc, crescent, diamond, line, chain, hook, star,
pair.** And a repeated group whose neighbour within 1100px already wears its shape takes the
next one along, so THE MACE'S OATH reads differently in each of the three places it lands.

| cluster size | 2 | 3 | 4 | 5 | 6 | 7 |
|---|--:|--:|--:|--:|--:|--:|
| ninth pass | 33 | 33 | 32 | — | — | — |
| tenth pass | 6 | 8 | 18 | 25 | 30 | 11 |

One thing fell out of writing it: `layout` returns the points, and the count is read back off
*that* rather than off the number that asked for it. A diamond has four corners and a pair has
two however many were requested, and the first break-build written against this pass — every
figure forced back to three nodes — crashed the page reaching for a point that was never put
down. The figure is the authority now.

### "Add some icons"

Every node wears a picture, and the picture is **read off the stat** rather than typed in
beside it — `metaIconKey` maps a stat name to one of the drawings the bricks and the upgrade
cards already use, with the conversions and the per-weapon stats matched by pattern. A stat
written tomorrow arrives with an icon already on it, and a stat written *without* one fails a
test rather than quietly drawing a fallback: the break-build that deletes `incFrost` from the
table is caught by name.

The road wears a plus in the colour of the country it runs through and nothing else, because
that is the whole of what a travel node does.

Rank became the frame rather than the glyph — a keystone wears a broken ring, a legendary a
whole second one — which freed the middle of the disc for the picture.

One trap: the icons are 24-unit drawings and the obvious way to place one is a nested `<svg>`
with a `viewBox`. The DOM comes out perfectly correct and Chrome renders it the size of a
county, inside a tree that is already being scaled. A `<g>` with a `transform` does the same
job and behaves.

### What it cost to check

`shapes.js`, and ten break-builds. Every assertion in it was proved to fail against a map
deliberately broken in the matching way: every figure back to three nodes, every cluster a
ring, the blot back to a fixed circle, the name placed blind, the edge-clearance force
removed, nothing drawn on a node at all, every plus the same colour, a stat with no icon, a
star with nothing in its middle, and the layout pass back to every node against every other.

That last one is a performance break, and it is in there because this pass caused it: 1187
nodes against each other, forty passes deep, is 56 million sums between opening the game and
seeing the title. It cost **3.7 seconds**. The neighbours worth shoving against are gathered
once off a 300px grid instead — nothing can reach further than the two movement caps plus the
widest pair of circles, which is well inside the window — and the map that comes out is
identical, node for node, at **0.67 seconds**.

| | ninth pass | tenth pass |
|---|--:|--:|
| nodes | 990 | **1187** |
| of which highway | 677 | 677 |
| clusters | 98 | 98 |
| distinct figures | 5 | **10** |
| literal triangles | 37 | **0** |
| edges through a node | 23 | **0** |
| names over a node | 15 | **0** |
| tree cost | 1072 | **1269** |
| layout time | 3700ms | **670ms** |

### One thing that was not asked for

`roads.js` had an assertion that a weapon's own country is at least 15% cheaper to its prizes
than anybody else's, and the axe came out at 14% — a real regression, or so it looked. It was
not. Averaging every prize in your own country against every prize abroad measures *how deep
your country's prizes sit* as much as it measures distance, and green held two of the four
keystones pinned to the outermost ring while red and blue held one each.

Measured the way the design actually means it — **one prize at a time**, whoever lives in its
country against whoever does not — every single prize on the map is nearer to its own, by
22.0 points against 26.9. The assertion was rewritten to say that instead, and proved to fail
against a build where all four doors open in the same place. The keystone distribution was
evened up anyway, because two of four in one country is still lopsided.

---

## Eleventh pass — plain

> *"There are still some attribute nodes that go 'over' other nodes, or some of the lines do at
> least, i dont like that, keep everything plain... there are really too many attribute nodes in
> a row sometimes, please remove a few, do longer lines... some nodes only got like 2 upgrades,
> like FULMINATE, it doesnt make sense."*

Three complaints again, and this time all three were exactly what they looked like.

### The lines

The tenth pass asserted that no edge runs *through* a node, and that was true. It was also the
wrong bar. Measured against what a player sees:

| | tenth pass |
|---|--:|
| edges through the middle of a node | 0 |
| **edges passing within 12px of one** | **4** (one of them 1px) |
| **edges within 20px** | **~15** |
| **pairs of lines crossing each other** | **45** |

A line shaving the edge of a circle reads as touching it, and a line crossing another line is
the thing "keep everything plain" is asking about. Both are now zero, and there are tests for
both. Four things got them there:

- **The clearance went from `R+14` to `R+26`**, and the line gives way too: shoving only the
  node that is standing on an edge asks one end of the problem to solve all of it, and a
  cluster's prize can barely move. Letting the two ends of the lane bow away halves what the
  node has to find.
- **Slip roads are picked after the layout pass, not before.** Chosen before it, every one of
  them was aimed at where its two ends were about to stop being, and a dozen ended up laid
  across a lane. Chosen last, against final positions, a way in can be *refused* — three
  sweeps, each worse than the last: no crossing and no grazing, then crossing allowed, then
  anything, and an untidy way in is only ever taken to get a cluster to two ways in at all.
- **A cluster is never grown wide enough to touch the road.** How far a figure reaches per unit
  of radius is asked of the figure — laid out once at a notional hundred and measured — rather
  than read off a table of guesses, because while it was a table fourteen lanes still ran across
  a cluster's own lines.
- **And if the middle of the quad is not the roomiest part of it, the site moves.** The quads
  are not squares: a hub wanders off its ring and the lane between two of them bows, so the
  point half way between four corners can sit far closer to one wall than another. A cluster
  that would not fit there had nowhere to go but over the road. It walks away from the nearest
  lane until there is room. That one change took the crossings from 15 to 4.

The last one was two door branches drawn over each other in the middle of the map — the one
place everybody looks. Two doors in the red country both reached for the same pair of inner
hubs; they take the next hub along now.

### The queues

LANE_STEP went from 215 to 392, and a lane is capped at **four** attribute nodes between two
junctions. A lane that would need a fifth does not get one, it gets longer jumps — which is the
same note from two passes ago, *"it's okay to jump long distances sometimes"*, finally applied
to the spacing rather than to the connections.

That had a consequence worth writing down, because it is the kind that only a test finds. A
longer jump is also *fewer* nodes, and the highway's length in points is the only thing keeping
the outer keystones apart. With the map at its old size, one walk round the rim collected
**every keystone on it** for 55 points against a ceiling of 60. So the rings grew by half —
1180, 2230, 3380, 4590, 5830 — and the node count came back while the jumps stayed long. The
map is bigger and reads emptier, which is what was asked for; it is not cheaper.

### FULMINATE

Nothing holds fewer than four nodes now. Two stats with a name over them is not a group worth
leaving the road for, and fourteen clusters were that small. The stats repeat round the figure
to fill it.

| | tenth pass | eleventh pass |
|---|--:|--:|
| nodes | 1187 | **1126** |
| of which highway | 677 | 592 |
| median jump along a lane | 215px | **336px** |
| longest run with no junction | 6 | **4** |
| clusters under four nodes | 14 | **0** |
| edges within 20px of a node | ~15 | **0** |
| crossed lines | 45 | **0** |
| neighbouring clusters sharing a figure | 9 | **0** |
| worst highway detour | 3 points | **0** |
| tree cost | 1269 | **1208** |

### What it cost to check

Seventeen break-builds, five of them new: the lane cap removed, the slip roads picked before
the layout pass again, the cluster pinned to the dead centre of its quad at whatever size its
figure wants, the doors taking the nearest hubs whatever they cross, and the minimum group size
back down to two. Each one trips the assertion it is aimed at and nothing else fires spuriously.

One old assertion was loosened and should be recorded as such: the clearance test used to ask
whether an edge passed through a node's *centre* circle. It now asks for 20px of daylight. That
is a tightening of the bar, not a loosening — but the number 20 is a judgement, and the honest
version of it is: the worst edge on the map clears by 24px, and the test exists to catch a
return to 1px rather than to police the difference between 20 and 24.

---

## Twelfth pass — two numbers, and four beginnings

### A hit made of two things says two numbers

The eleventh pass gave every number the colour of what it arrived as, and picked the largest
share when a hit was split. That is fine for a conversion, which moves damage from one type to
another — but **gain as extra** does not move anything. Twenty per cent of your physical gained
as cold is a real part of every swing that the screen simply never mentioned: one white figure,
and the cold only ever visible as a row in a chart in a panel.

Each part that is worth reading gets a figure of its own now, biggest first, stacked. Two
thresholds decide what is worth reading — 0.4 damage, or 6% of the hit — because four numbers
off one swing, two of them unreadable, is worse than one number that is nearly right. What
falls under them is *added back to the largest part*, so the figures on the screen always total
the damage the monster took. That last bit is the one a test caught: dropping the slivers
instead of putting them back is invisible in a two-part hit and only shows up when a third,
tiny part exists, which is exactly the case the probe now builds.

### Four beginnings, and all four on the map

Two passes ago this project added `metaMine` and wrote *"hidden is not owned"*: a door's first
steps belonged to that weapon, the map drew nobody else's, and the graph refused to walk
through them. That was the right fix for the bug it fixed — they were invisible and still
buyable — but it was the wrong rule. Four beginnings that only one weapon could ever touch are
four quarters of the middle of the map wasted, and the middle of the map is where everybody
looks first.

`metaMine` is gone. What replaced it is narrower and is the thing that actually matters: **a
door is where you begin, not ground you own.** `metaConnected` returns true for a start node
only if it is yours, so nothing paths through anybody else's — which means the only way into
the staff's beginning, for a sword, is from the far end, up the lane it hangs off.

Measured: your own beginning is 3 points away; it is 5 to 13 for everybody else, averaging more
than double. The nearest neighbour paying 5 is the mechanic working, not a leak — leaning into
the beginning next door is a real option.

The assertion that guarded the old rule was rewritten rather than deleted, and it is now three
questions instead of one:

| | |
|---|---|
| standing on any neighbour, another weapon **can** buy it | it is not walled off |
| standing in its own door and nothing else, it **cannot** | nothing paths through a foreign door |
| **holding** the foreign door — which an old save can claim — it still cannot | a door is not ground |

That third row is the one that keeps the guard honest. Without it the check that refuses a
foreign door as a stepping stone is dead code: you can never buy one, so you can never hold one
— *unless a save written under different rules says you do.*

### Three steps, not one

Each of a door's four ways out is three flavoured nodes now rather than one. 12 nodes per
weapon, 48 on the map, and the fan is spread deliberately: the door sits *inside* the innermost
ring, so every hub on it is "outward", and picked nearest-first all four ways left within a
fifty degree slice. Each way is now kept at least 27 degrees off the ones already taken.

Two things fell out of that, both caught by tests rather than by eye:

- **Red went over the power budget.** The sword and the mace both live in the red country, so
  a build that never leaves it walks into *twenty-four* cheap opening nodes where green and
  blue get twelve — ×2.66 against a ceiling of ×2.60. Red's openings are the smaller ones
  now, and the flat physical node went altogether, for the reason flat damage always goes:
  on a weapon whose base is one it is a multiplier in disguise.
- **Spreading the two red doors apart made it worse, not better.** Pushing the sword and the
  mace out towards the edges of their wedge gave the fans room and put each within 25 degrees
  of a border; two of red's own prizes came out nearer to the axe than to either weapon that
  lives there. The doors went back to evenly spaced and the fan is spread by the fan.

One honest note on the fan assertion: the spread rule measurably moves only one of the four
doors (the mace's, from 27 degrees between its tightest pair to 46). The 24-degree floor in
`roads.js` is a floor, not a proof of the feature, and the break-build that removes the rule is
caught by the prize-distance test rather than by it. Said here because a test that cannot fail
is worse than no test, and a test that passes for a reason other than the one it claims is the
same thing wearing a hat.
