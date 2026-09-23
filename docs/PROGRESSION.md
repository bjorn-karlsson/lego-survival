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

---

## Thirteenth pass — a deck that knows what you are

Two rules, both of them about not wasting a player's screen.

### Only conversions you can use

The gate on every physical conversion brick was `!isCaster()`. Which is nearly right and
completely wrong: a staff casts fire so it has no physical *from its weapon*, but Guardian
Brick, Brick Blaster, Bomb Volley, Bladestorm and Blade Vortex are all physical, so a staff
carrying any of them deals physical and was refused all seven of those cards anyway. And in
the other direction nothing was gated at all — a sword hero with no fire on the map was shown
FIRE TO CHAOS as often as anything else.

`dealtTypes()` answers the question properly, off three things:

| the weapon in your hands | a staff casts fire, everything else swings physical |
| every spell in your book | the Storm Brick is lightning whatever you are holding |
| what you already convert into | take Brand Iron and you *now* deal fire |

That third row is the one that makes it a mechanic rather than a filter: a chain is built one
link at a time, and FIRE TO CHAOS becomes a real card the moment something of yours makes fire
and not a moment before. `CONV_ORDER` is downhill-only, so one sweep in order closes the whole
chain — no fixpoint loop needed.

A spell key *is* its damage source (`convFrom('chain') === 'shock'`), so the one table that
already says what a source is made of answers this too; adding a spell tomorrow needs no line
here.

### What you threw back

A brick rerolled away is weighted to a fifth of its chance for 3 to 5 waves, rolled per brick.
**Scarce, not banned** — and the test says so in both directions, because both failure modes
are real: a ban turns the reroll into a way to delete cards from a run, and no penalty at all
means the reroll does not work.

A weight cannot be asserted from one draw, so the probe draws four hundred screens either side
of the expiry: a shunned brick turns up on 1.0% of them and an ordinary one on 4.5%.

### What the tests caught

Three things, none of which the eye would have:

- `wave` is an **object**, `{ n, state, t, … }`, not a number. `wave + dur` is
  `"[object Object]3"` and every comparison against it is false — the feature would have done
  nothing at all, silently, and the only tell would have been a reroll that did not work.
- The `banned` break-build — filtering shunned bricks out of the candidate list instead of
  weighting them — passes every assertion about rarity and about the reroll *appearing* to
  work. It is caught only by the one that insists a shunned brick still turns up sometimes.
- Ten break-builds in total, and the one worth keeping is `casterGate`: it restores the old
  `!isCaster()` gate exactly, and the probe names the case that was broken all along — a staff
  carrying Bomb Volley.

---

## Fourteenth pass — one rule for five ailments, and the axe gets its own

### The bug, and how much bigger it was than it looked

*"Shock chance is only for storm bolt."* True — and the same was true of three of the other
four. An ailment was a property of the **spell that threw it** rather than of the **damage
type**, and each one had been written into its own call site:

| | where it came from | what that meant |
|---|---|---|
| SHOCK | the Storm Brick, via a stat called *"chance for a storm bolt to SHOCK"* | convert a whole build into lightning and you cannot shock with any of it |
| IGNITE | the staff's bolt and the ember | a sword converted entirely to fire burned nothing |
| CHILL | Block Freeze | same |
| BLEED | any *melee* source | Brick Blaster is physical and never opened a wound |
| POISON | the chaos that landed, wherever from | **correct** |

Poison was the odd one out because it was written last, after conversion existed. Every other
one is now poison's rule, in one place — `applyAilments`, called once from `hitEnemy`, which is
the only place that knows what a hit actually arrived as. Six call sites lost their private
ailment code.

Two design decisions worth recording:

- **Nothing is quietly buffed.** Base chance is zero for four of five, chaos keeps its 35%
  dose, and a crit still opens a wound half the time. The old Storm Brick stat became
  **Afflict**, one chance added to all five and capped at 75% — a cap because a build that
  ignites, chills, shocks, bleeds and poisons every hit is not a build, it is a status bar.
- **The sources that ARE their element keep their certainty.** A fireball that did not ignite
  would not be a fireball. `AIL_NATIVE` is the whole of that exception, and it is still scaled
  by how much of the element survived your own conversions.

### Reaving

The axe was the only weapon whose legendary was somebody else's. Its stats say what it should
be — 10% base crit against the sword's 5%, 210% multiplier against 150% — and then `bleed: 0.45`
locks it out of the one tree that crit feeds. So the legendary makes critting itself the
engine: every critical throws the axe, it reaps up to three more bodies, each bounce rolls its
own crit, and it comes back. You cannot swing while it is away.

Rank two pays off the 0.45 directly: a bleeding body costs the blade none of its bounces.
Rank one reaps 4 off a ring of nine; rank two over the same ring, bleeding, reaps all nine.

### What the tests caught

- **`wave` is an object**, caught last pass — this pass, `syncStats()` derives `critBase` from
  the weapon, so a probe that set `critBase = 1` *before* calling it measured the sword's
  natural 5% and reported "a critical does not open a wound". Set derived stats after the
  thing that derives them.
- **`enemiesNear` reads a spatial grid rebuilt once a frame.** A probe that places monsters by
  hand and immediately asks what is near finds nothing, and calls a working blade broken. The
  fix is in the probe (let a frame run), and it is worth writing down: in a real fight the grid
  is always current, because the blade is thrown from inside a swing, mid-frame.
- **A statistical assertion on a rare card is a coin toss.** The reroll test watched one brick;
  when the reroll happened to throw back a spell card at 1.3% of screens, four hundred draws at
  a fifth of that expects *one* appearance, and zero is ordinary luck rather than a ban. It
  watches all three now, over two thousand draws.
- **The guard against a bounce throwing a second blade was invisible for the right reason.**
  The 0.55s cooldown masks it on a short flight. The probe had to be moved to the longest
  flight the game can produce — rank two over a field that is already bleeding — and it now
  asserts *first* that the blade was out longer than the cooldown, so it cannot pass by
  measuring nothing.

---

## Fifteenth pass — the axe swings twice, and stacks become a thing

### The backswing

The axe ran the sword's three-step combo with different numbers, which is the one place a
weapon's feel actually lives. It swings twice now: a 0.20s forward chop, then a 0.33s **270°
backswing** out of it at spin reach, always a critical on the first body it touches.

Two decisions inside that:

- **Only the first body.** A 270° sweep that crit everything in it is a legendary, not a combo
  step. `s.crited` is consumed once per swing, and the break-build that removes it reports four
  identical crits where there should be one and three ordinary hits.
- **A combo is a table row.** `SWING_COMBO` and `SWING_STEPS` replaced the `p.combo === 2`
  branch, so the fifth weapon swings differently by being written down. The sword's three steps
  are now data too, and the test asserts they are unchanged.

The guaranteed critical is not decoration: Reaving fires on criticals, so the rhythm of the
weapon and the trigger of its legendary are the same beat.

### Momentum, and why it is not a momentum system

The interesting part of this pass is what is *not* in it. `HERO_STACKS` is a table of named,
self-expiring counters — a cap, a decay, what one stack is worth, and optionally which weapons
may carry it — and nothing in the machinery knows what momentum is. `stackMod('atk')` sums
across every live kind; a consumer never learns which exist.

The test proves that by inventing two stacks that are not momentum (`testrage`, `testcalm`),
asserting the cap, the bleed, the clock refresh, the weapon gate and the summing on *those*,
and only then asking the real swing about the real one. If any of the machinery had needed the
word "momentum", that half of the test could not have been written.

**It bleeds, it does not drop.** One falls off every 2s without a new one, rather than the lot
at once. A cliff is a thing you avoid; a bleed is a thing you play with. The break-build that
zeroes the count on expiry is caught by an assertion that reads the *shape* of the decay —
`[4,0,0,0,0]` against `[4,3,2,2,1]` — rather than the end state, which both produce.

**Built by connecting, once a swing.** Per-body would make one step into a crowd the whole
ramp. Both failure modes have their own break-build: per-body, and free-on-every-swing.

The whirl **reads** momentum's reach and never builds it, because a spin that fed the ramp it
benefits from would sit at six stacks for ever.

### Where the numbers are read

Momentum is not poured into `p.atkRate`. It is read at the point of use, in `startSwing`,
because a stack that changed a pool would have to be taken back out again and that is exactly
where double counting starts — the same rule that made attributes derived rather than written
back in the sixth pass.

---

## Sixteenth pass — rage, and three things that were wrong in the fiction

### Three fixes first

- **Blademaster was offered to the axe.** The whirl is the spin held down, and the axe has no
  spin in its combo any more — it chops and backswings. The gate reads the combo table
  (`comboSteps().indexOf('spin') >= 0`) rather than a list of weapon names, so a fifth weapon
  opts in by having a spin rather than by being remembered here.
- **The axe had no opening hand of its own.** Sword & Steel — flat damage, swing rate, reach —
  is a *sword's*, and it was the only melee favourite an axe could take, which made the crit
  weapon open like the balanced one. **Reaver's Edge**: +40% increased critical chance, +25%
  critical damage, +1 to the Momentum ceiling. The two swinging hands are now mutually
  exclusive by a flag, and a test walks every favourite's brick list to prove none of them
  points at a card that does not exist — which caught `chainshock`, renamed two passes ago,
  still sitting in the Storm Brick's hand.
- **A conversion is not weapon-locked, and the card said it was.** `SWORD AXE MACE` under
  Emberforge is what you get when the tag row asks the gate once per weapon: those are the
  weapons that deal physical *with nothing else equipped*. The rule is "you need a source of
  physical damage", which a staff carrying Bomb Volley or the Guardian Brick has, so the card
  says **needs PHYSICAL damage** now. Same field drives the gate and the tag, so they cannot
  drift.

### Rage

The interesting part is that it is a second row in the stacks table and one new field.
Momentum falls off one at a time; rage sits still for a grace and then pours:

```js
rage: { max:30, grace:2.0, rate:10, frac:true, mob:true, mods:{ moreDmg:0.01 } }
```

That is the whole of *"you lose 10 rage every second if you have not been hit or gained rage in
the past 2 seconds"*. Nothing gains it by default — ceiling 30, generation zero — so a hero
buys it (Warpath, Spite, Deep Well; three groups and a notable in the red country) and a
monster is given it (the `WRATHFUL` wave modifier).

**Monsters needed the helpers to take an entity.** `stackOf(id, ent)`, `stackAdd(id, n, ent)`,
`stackTick(dt, ent)`, `stackMod(key, ent)` — defaulting to the hero, so nothing that already
called them changed. A raging monster hits harder through one line in `playerDamage`, which is
the single place every monster attack passes, so contact, rings and thrown things all pay it
without any of those call sites learning that rage exists.

### The bug that would have shipped silently

There was **already a `rageMul()`** — the rage *brick*, the drop that doubles your damage for a
few seconds. Declaring a second function by the same name does not error: the later one simply
wins, so `calcDamage` called the pickup's version, the counter multiplied nothing, and *every
test that did not read a real damage number passed*. The definitional assertions were all
green; only `physDamage()` before and after 30 rage caught it.

The new one is `rageMore()`. And the character sheet row for the pickup now says **Rage brick**,
because two things called rage in the same UI is a defect even when the code is right.

### One more shape of test bug

The `ragebleeds` break — swap the pour for a one-at-a-time bleed — passed at first. The break
left `st.t = d.decay`, which is `undefined` for rage, so every comparison against the clock was
false and the counter drained *a point a frame*: faster than the thing it was meant to slow
down. The break was rewritten to fall back to `d.grace`, and the real code got the same
fallback, because a row with neither field would have done exactly that in production while
looking like it worked.

## Seventeenth pass — the ailment table, and a chance that was quietly zero

> *"I play with axe now and 50% of my physical damage is converted to fire, why don't I ignite
> the boss when I'm hitting him? Or is the chance set to 0."*

It was zero. Exactly zero, and not by accident — by the shape of the table the fourteenth pass
had left behind.

An axe swing reports as a weapon source. `ailChanceOf('fire', 'sword', false)` read
`AIL_BASE.fire` (0) plus `AIL_CRIT.fire` (0) plus whatever **Afflict** had been bought (0), and
returned nothing. Half the hit was arriving as fire and every point of it was ignoring the
ailment step, at any amount of conversion, forever. The one thing that *did* carry a certainty
was `AIL_NATIVE` — the fireball, Block Freeze and Slam of the Elements — and an axe is none of
those.

So the fourteenth pass had unified the *rule* and left the *numbers* arbitrary. Four of the five
started at zero, chaos carried a free 35% dose nobody had to pay for, and a critical bought a
50% bleed and nothing else. Nothing said why.

### What the table says now

| damage type | ailment | base | on a critical |
|---|---|---|---|
| Physical | BLEED | 0% | 0% — *and attacks only* |
| Fire | IGNITE | 0% | **100%** |
| Cold | CHILL | 0% | **100%** |
| Lightning | SHOCK | 0% | **100%** |
| Chaos | POISON | 0% | 0% |

Three changes, each of them one line:

- **Chaos pays like everything else.** The free 35% dose made chaos the only damage type whose
  ailment came in the box, which is exactly the kind of exception this whole thread has been
  removing.
- **A critical buys the three ELEMENTS, and only those.** That is the PoE rule and it is the one
  that fixes the report: the axe now ignites on every crit, on the fire half of the hit.
- **A critical no longer opens a wound.** The old 50% made the crit tree quietly the bleed tree
  as well; bleed is bought with `Serrated Edge` or `Afflict`, or it does not happen.

And one gate: **BLEED is an attack ailment.** The fourteenth pass widened it to *any* physical
hit, on the reasoning that an ailment belongs to the type. That was one step too far — a wound
is a thing an edge does to a body. A swing, its whirl, the vortex, bladestorm's blades and the
slam can cut; an orbiting brick that has bought physical damage hits for physical and does not.

`AIL_NATIVE` stays, because a fireball that sets nothing alight is not a fireball and Block
Freeze without the chill would leave Absolute Zero upgrading nothing. But it is no longer a
hidden override: the comment names it, the character sheet names it, and the set is exactly
three spells — pointedly not the storm brick, which is what started all of this.

### One place, one number

The old `bleedChance(crit, src)` kept its own arithmetic — its own crit term, its own weapon
multiplier — and every display in the game read *it* rather than the thing the hit rolls
against. That is the same defect as the storm brick's private shock chance, one layer up. It now
delegates to `ailChanceOf('phys', src, crit)` and does nothing else, and the `sheetdrift` break
(give it back its own sums) fails the suite.

The character sheet grew an **AILMENT** block: one row per ailment, on a hit and on a crit, ten
rows all reading `ailChanceOf`. The `Ailment chance` row's tooltip prints the five-line table
above from the same call. A player who wonders why they are not igniting can now read the answer
off the sheet instead of filing it as a bug.

### The test that passed for the wrong reason

The first version of the sheet-row assertion set up a bare **sword** with 30% Afflict and
checked each row against `ailChanceOf`. Every row read 30%. So did the `rowsdrift` break, which
replaced the row's getter with a flat `player.ailChance` — because on a bare sword with no bleed
brick, `p.ailChance` *is* the answer for all five.

The probe was moved to an **axe** with a `Serrated Edge` on top of the Afflict, so the physical
row can only match if the weapon's own bleed multiplier and the bleed brick are both in it. Then
the break failed. A test whose setup makes the right answer and the wrong answer coincide is not
a test, and this is the third time in this file that the fix has been *make the fixture less
convenient* rather than add an assertion.

Twelve breaks for `ail3`, and `conv2`'s poison case — which asserted the free dose — was
rewritten to assert both halves: nothing bought, nothing poisoned; 40% bought, 40% poisoned.

## Eighteenth pass — a screen for the switches, and two points a level

### The options screen

Music and sound already had buttons in the corner of the title plate. Everything else a player
might want off was a keypress they had to remember every run, or not a switch at all. So: one
screen, reached from the title plate and from the pause menu, with the two audio toggles and
two new ones beside them, plus the wipe.

It deliberately does **not** touch `state`. A paused run stays `'pause'` underneath it and the
title stays a title, which is the only reason the same screen can be opened from both without a
second state machine. The cost of that is one ordering rule: the options screen has to eat `ESC`
*first*, because the pause toggle is listening for the same key and would otherwise close the
options **and** un-pause the run in one press. That is `escboth` in the break set.

### Two switches, and where a switch belongs

**DAMAGE NUMBERS** is the interesting one. The obvious place for it is `addDmg`, which is the
function that makes a floater — and that is the wrong place, because `addDmg` also throws
`FROZEN SOLID`, `CURSE LIFTED`, `LEVEL UP!`, `THE SWARM!` and every other thing the game shouts
at you. A player who turned off damage numbers asked for a quieter screen in a wave of sixty;
they did not ask to stop being told a boss just froze solid.

So the gate is a new `addHitNum`, used by exactly the three calls in `hitEnemy` that throw an
outgoing damage figure. `dmgall` — move the gate up to `addDmg` — is a break, and the test that
catches it asserts both halves: twenty hits throw zero numbers, and an event floater still
lands.

**START WITH THE COMBAT LOG** is three words in `startGame` and one of them matters:

```js
if(OPT.autoLog && !clogOn) clogToggle();
```

`clogReset()` clears the buffers but does not close the log, so a log left open by the last run
is already open when the next one starts. Without the `!clogOn` guard the second run of a
session would *toggle it shut* — which is the `autologtoggles` break, and is exactly the kind of
thing that looks fine in one manual test.

### A wipe is a wipe

`metaWipe()` calls `metaBlankSave()`, which builds the empty record for every difficulty and
every weapon. Nothing is walked by hand, so a weapon added tomorrow cannot be missed. Two
breaks live here: `wipeone` (only the weapon you happen to be holding) and `wipevolatile`
(clears the object in memory but never writes it, so the tree comes back on the next reload).

And a third, `wipesettings`, which is the one that taught something. Progress and preferences
are different things and live under different keys; the break deletes both. It **passed at
first**, because the test checked the settings after the wipe while both of them happened to be
sitting on their defaults — deleting the key restored exactly the values under assertion. Same
shape as the `rowsdrift` bug one pass ago, and the same fix: move the fixture off the values
where right and wrong coincide. Both settings are now flipped away from their defaults before
the wipe.

### Two points a level

The level grant was seven milestones — 10, 25, 50, 75, 80, 90, 100 — which paid seven points for
a hundred levels, and left a dead stretch of twenty-four between the second and the third where
levelling paid nothing at all. It is a rate now: `META_POINTS_PER_LEVEL * best.level`, two a
level, so level 25 is fifty points.

`META_POINT_MAX` stays at 60. Two a level is generous on purpose and a deep run can now fill the
tree on levels alone, but the tree's power budget is built on that ceiling and `noceiling` is a
break.

Three things in `meta.js` had to move, and all three are worth writing down:

- **The "earned is derived" case** seeded wave 35 + level 30. At two points a level that is 95,
  clamped to 60 before and after the change — a total that *cannot move* proves nothing about
  whether it is stored. Reseeded well under the ceiling.
- **The "clearing 29 is worth 29 points" case** left the hero on level 1, which is now worth two
  points of its own, so the answer was 31. The hero is explicitly on level 0 now, with a comment
  saying why, because this case is about what a *wave* is worth.
- **The old derivation proof** mutated `META_LEVEL_POINTS` at runtime to show the total moved
  with the rule. There is no list to mutate any more, so the proof runs the other way: move the
  record, round-trip it through `localStorage`, and the total has to follow. A stored counter
  would freeze.

`firsts` also stopped being a list of bare strings. The run-end screen printed `+1 skill point`
under every one of them, which was already a lie whenever a wave first was worth two, and is a
bigger one now a level first is worth ten. Each entry carries its own `pts`.

Sixteen breaks for `opts`, 51 tests green, wave-50 soak clean.

## Nineteenth pass — you keep the axe, the mace stops apologising, lightning is yellow

### The legendary that punished its own build

> *"Instead of leaving the player empty handed when the axe is going around, make the player
> keep his axe and he may trigger another axe toss, so we can have multiple axes flying around,
> no problem, just good old fun."*

The first cut of Reaving cost you the axe until the blade came home, and the design note said
so proudly: *"that gets worse the more attack speed you stack, which is the tension the axe was
missing."* It was the wrong tension. The axe's identity is **twice a sword's crit chance and a
far bigger multiplier**; a legendary that takes the weapon out of your hands every time that
identity fires is a legendary that punishes you for building into it. Stack attack speed, crit
more, swing less.

What flies is a copy now. `reaveT` — a rolling timer on the hero, refreshed every frame a blade
was alive — is gone entirely, and with it the `heldArt()` null and the `startSwing` early
return. `reaveOut()` survives as a **count of live blades**, which is the only thing it is
still asked.

The brake moved from your hands to two numbers: `REAVE_CD` cut from 0.55s to **0.16s**, and a
hard ceiling of **4** blades in the air (**6** at rank two). The cooldown still does the one job
it was written for — forty criticals landing in a single frame throw one axe, not forty — and
the guard that stops a *bounce* starting a blade of its own is untouched, because several axes
are supposed to come from several criticals.

Five breaks: `emptyhands` (put the old rule back), `oneaxe` (the ceiling is 1, which is the old
rule wearing a hat), `noceiling`, `nocd`, `bouncethrows`.

### A fixture that passed without doing anything

The test for "a second critical throws a second axe" used the file's own `critHit` helper,
which returns the moment any live blade exists — and one already did. It would have reported a
pass without throwing a thing. It counts blades before and after now, and hits until the count
moves.

### The mace was paying twice

`SLAM_SHARE` has moved twice. It opened at **0.62** a tooth, which made an AoE basic attack beat
the single-target one it replaced. The cut to **0.307** fixed that and overshot: measured, the
mace came out at **4.37**/s against the sword's **5.79** and the axe's **6.28** — a quarter
behind on one body while *also* being the slowest weapon in the game. It was not trading
single-target for coverage. It was paying for coverage twice.

**0.36** puts it at **5.12**/s, about 88% of a sword. And the test rule changed with it. The old
assertion was `SLAM_SHARE * SLAM_MAX_HITS < 1.0` — "a slam must be worth less than a swing" —
which is the wrong comparison, because the swing it is measured against is one the mace does
not have. The rule is now stated where a player feels it: **sustained single-target DPS against
the weapons it competes with**, strictly under a sword and strictly over 0.82 of one. Two
breaks, `slamcut` and `slamfat`, sit on either side of it.

`mlvl.js` had `Math.abs(R.share - 0.307) < 1e-9` — a second copy of the constant, which fails
whenever balance moves and tells you nothing about whether the balance is right. It asserts the
ratio now.

### Two rendering rules, asserted in pixels

**Lightning is yellow.** The Storm Brick was a blue 2×2 with navy studs throwing pale blue-white
bolts — this game's *cold* — while the damage number floating off the body came up in
`CONV_COL.shock`. Everything about it reads off that one constant now.

**You can see through your own teeth.** A fully-fanned slam is twenty-six rows of stone between
you and the pack you are standing in; the thing you needed to read was behind the effect you
cast to deal with it. Hero spikes draw at `SLAM_SPIKE_A = 0.52`. Monster spikes do not — a
hazard you have to see is a hazard.

Both are asserted off **the pixels on the canvas**, not off the constants, because a palette
nothing draws with is a palette that is wrong and passing. `brickdrift` is exactly that break:
the table says yellow, the draw call still says `#2e7dd1`.

The transparency assertion took a rewrite to be worth anything. "The pixel is not the rock
colour" depends on which part of a tooth the sample lands on — body, highlight, outline, seam.
So instead: **draw the same tooth over two different floors.** If it is translucent the two
patches still differ, in proportion to how different the floors were; drawn opaque they collapse
to the same stone. The assertion is `spikeGap / floorGap > 0.25`, and the fixture asserts the
two floors differ in the first place.

And the first version of that fixture hand-built a `pspike` without an `h` field. `spikeHeight`
returned `NaN`, `h < 1` was **false** (every comparison with NaN is), the path was drawn with
NaN coordinates, and nothing landed on the canvas at all — a tooth that was never there would
have sailed through the transparency check. The fixture now carries every field the renderer
reads.

### Two flakes and a dead break

`ail3`'s axe-crit case measured a **product of two rolls** — an 80% crit and a 50% share — over
400 samples with a ±0.09 window, which is under four sigma. It passed alone and failed in a full
suite run. It counts the criticals it actually got in the same loop and divides them out now, so
what is asserted is the thing it claims: a fire critical ignites the fire **half** of the hit.
The test also freezes the world first — `waveMods`, the spawn queue, the live enemies — because
a run continuing underneath a measurement is a second source of randomness nobody declared.

`stacks`'s momentum case swung into five bodies that the **previous** case had just thrown with
a `kb: 2.0` backswing. Whether they were still inside a plain swing's 54px depended on how much
real time passed during a `sleep(500)`, which is to say on how busy the machine was. It puts
them back and pins them.

And `mkbrk13.py` had two breaks — `perbody` and `freemomentum` — that had been **unbuildable**
since the rage pass reformatted the block they patched. A break that no longer applies is a
missing guard wearing a passing suite, and nothing in the runner would ever have said so. Both
repaired, both caught.

52 tests green, wave-50 soak clean.

## Twentieth pass — four things borrowed from Path of Exile

Ailment thresholds, exposure, the three charges, and proliferation as a keystone — plus ten
notables and five new sentences in the tree.

### The two ailments that could not tell

Three of the five already scaled with the hit: bleed ticks for a share of the wound, burn for a
share of the fire, poison for a share of the chaos. **Chill and shock are counters**, so a
one-damage orbit tick chilled a body exactly as hard as a mace slam. The two ailments that most
want you to hit hard were the two that could not tell whether you had.

The magnitude scale runs from `AIL_MAG_MIN` (1.2% of the pool — under it, nothing) to
`AIL_MAG_FULL` (10% — all three stacks in one blow). Two things about it were not obvious:

**A boss's pool is not its life.** A wave-50 boss holds 240,000 health. Measured against that,
nothing a hero can swing is ever 1% of it, and bosses would be flatly immune to two of the five
ailments — the opposite of what a storm build wants from the fight it exists for. A boss's pool
is 5% of its life. `bosslife` is the break.

**A spell that IS its ailment needs a floor.** Block Freeze exists to chill, and a threshold
that can stop it doing so is a rule eating a spell. The three `AIL_NATIVE` sources always land
one stack; how many *more* still depends on the hit. `nonative` is the break.

### Exposure, and why it is not an ailment

An ailment is damage or a debuff the body carries. Exposure is **resistance taken off**, which
puts it outside the hero's own increased-damage pool and outside the MORE ceiling with it — the
one damage increase available to a build that has already bought everything. It does not stack
with itself (a stacking resistance strip is a hole with no bottom) and it can drag a resistance
below zero, which is a weakness and multiplies up.

It is rolled per element that landed, at the chance times that element's share, because that is
the rule every ailment already follows and a second rule would be a second thing to get wrong.

### Three mechanics as three rows

The stack table was made abstract in the fifteenth pass "because I might use it elsewhere". This
is the elsewhere: frenzy, power and endurance are three rows in `HERO_STACKS` and five one-line
reads at the points of use — `moreTotal`, `critChance`, `castInc`, `elemTake`, `playerDamage`.
Nothing else in the file learned that charges exist.

The one design choice worth writing down: **each is earned by a different verb**. Killing,
critting, being hit. That means which charge a build runs on says what that build *does*, and it
is why the tree puts one in each country — red takes hits, green kills, blue picks its moment.

### Proliferation, priced

> *"I like the idea of ailment proliferation, but I find it too strong to just add — make it
> into a costly node in the tree somewhere."*

Correct, and it is a keystone on the outer rim with **−25% increased damage** attached. Two
things had to be right in the implementation:

**The stack is copied, not re-derived.** `burnEnemy` and `bleedEnemy` take a *hit* and run it
through `dotRate` to get a rate. What a corpse is carrying is already a rate — handing it back
in as a hit would put the whole increased-damage-over-time pool through a second time. The
copies go straight onto the list, through the same ceilings the originals obey.

**The copy is weaker** (`PROLIF_KEEP = 0.6`), so a chain across a packed wave fades out instead
of compounding. And nothing dies to the spread itself, so a proliferated burn cannot set off
another proliferation inside its own call and there is no depth guard to get wrong.

### The keystone that had to step over the spine

"Every critical leaves every ailment" is a sentence, and the share rule — *the chance rides on
how much of the hit arrived as that type* — is the spine of the whole ailment system. The first
cut of **Blood and Salt** set the chance to 1 and left the share in place, so a hit that was half
fire ignited half the time and the keystone did not keep its promise. It steps over the share
too, now. The ailment still lands on the *part* of the damage that was that type; only the odds
stop caring how big that part was.

### The budget, and a 7% surprise

`meta.js` reported a stay-at-home red build at **×2.731** total power against a declared ceiling
of ×2.60. The obvious suspects were the new red clusters — so the first four experiments
stripped them, one at a time and then all three together, and **the number did not move at
all**. Stripping every new cluster on the map, in all three countries, took it to ×2.551.

The mechanism was not any node's values. Fifteen new clusters made the whole map denser, the
relax pulled everything closer, **every route on the map got shorter**, and red's sixty points
simply reached further. Adding a cluster in the blue sector made a red build stronger.

The fix is the one the file already understands: **red pays for the density.** `heft` and
`whet` sit against the red door, they are instanced several times around the ring, and flat
physical on a weapon whose base is 1.0 is a multiplier wearing a disguise. Trimming those two
brought it to ×2.526 with real headroom, which the next pass will need.

Worth keeping: the first instinct — *the new nodes must be too strong* — was wrong, and four
experiments were needed to prove it wrong rather than one to confirm it.

### Three test bugs

**`noMit` does not mean "skip armour".** It skips the conversion pipeline entirely, so `mix`
stays `{phys:1}` and a hero converted wholly to cold lands a physical hit. Every threshold case
in the new file passed it, so every one of them measured a physical hit and reported that chill
was broken. The mechanic was fine.

**A magnitude is deterministic; the roll is not.** The ailment chance is capped at 75%, so one
hit per size is a case that fails one time in four and blames the mechanic. The number of stacks
a given hit is worth never varies — so take the most any of twelve landed.

**An assertion that reads its expectation from the table it is testing proves nothing.** Every
charge case asked whether the measured value equalled `HERO_STACKS.frenzy.mods.moreAll * 3`. Zero
the whole row and that is still true while the charge does nothing — `chargenothing` passed. The
table itself is asserted now, with a floor, and so is what a full stack actually moves. That is
the third time this file has recorded the same shape of bug.

Twenty-four breaks for `poe4`, 53 tests green, wave-50 soak clean.

### Still open

`ember` failed once inside a full suite run and has not reproduced in fifteen tries since. Its
assertions are wall-clock ones — *did the chain settle within ten seconds* — which is the same
shape as the `stacks` flake fixed last pass. Recorded rather than guessed at.

## Twenty-first pass — the contest, and six beginnings

### A dodge you cannot answer

A shade evaded 32% of your hits. Nothing in the game moved that number — no brick, no node, no
weapon — which makes it either irrelevant or infuriating and never interesting. PoE's shape is
two ratings pulling against each other, and the whole reason it works is that both halves are
things you can buy.

Three decisions worth writing down:

**It is checked on attacks and never on spells.** A swing has to find a body that is moving; a
fireball arrives where it was aimed. That one line is what makes accuracy a melee stat, gives the
staff a reason not to want it, and keeps the caster's damage exactly as reliable as it was. A
damage-over-time tick does not re-roll either — the wound was landed when it was opened.

**A monster's two ratings are derived, not authored.** Forty rows is forty chances to forget one,
and a stat nobody can predict is a stat nobody plays around. A body dodges because it is fast and
small; `evade` on a row is the shade's own gift on top of that. The test walks the whole roster
and asserts every monster has both and neither is absurd.

**Nothing scales with the wave.** This is the deliberate departure from PoE, where accuracy is a
treadmill: monster evasion climbs with level and you buy accuracy every ten levels to stand
still. A wave-50 monster here is already dangerous for three reasons. A monster's evasion is a
property of what it *is*, so the stat is a choice made once rather than a tax paid forever.

Evasion on the hero starts at zero on purpose — armour is the defence you begin with, evasion is
the one you choose — so a hero who buys none is missed by nothing, exactly as before this existed.

### The fixtures that had been zeroing a field that no longer exists

`e.evade = 0` appears in fifteen test fixtures, next to `e.armour = 0` and `e.suppress = 0`, for
the same reason: a probe measuring bleed should not have its answer eaten by a dodge. The field is
`e.evasion` now, so every one of those lines was zeroing nothing, and spawned imps — which are
fast and small, so they dodge well — started slipping the swings those probes were measuring.

`stacks` caught it: "no single body took a certain critical" on the axe's *guaranteed* backswing
crit, because the guaranteed crit had missed. Every fixture in the suite now zeroes both.

### Six doors

Four weapons is not a symmetrical number: red held two doors, green one, blue one. Worse, every
door sat inside a 670px circle round the middle of a 6,000px map, so the six ways into the tree
were one tangle. Two more doors — a bow in green and a wand in blue, neither of which exists as a
weapon yet — and the circle pushed out to 900.

An unfinished door gets a hollow dashed ring, a tooltip that says what it is, and **one short
spur** instead of a fan. Three more ways of three would be nine nodes nobody can ever take, in
exactly the part of the map that could least afford them. `soonfans` is the break.

And the fan itself went from four ways to three. Every exit from a door inside the ring points
outward, so the fourth was a spare.

### Two numbers that had to move together

Moving the doors out broke the fan geometry: at 670 a fan of 330–470px stopped in open ground,
and at 900 the same fan walked its last node past the hub it was aimed at and laid its line across
the ring's own edges. Three crossings. The fan is a *fraction* of the way there now and never a
fixed distance.

Then `roads` reported a red keystone cheaper from the axe's door than from either red door. The
doors had been spread evenly across their wedge, which put one 27 degrees from a border.
`DOOR_SPREAD` pulls the pair in towards the middle of their own country; at 0.70 they are 38
degrees apart, 593px, and every corner of the map is cheapest from a door that lives in it.

### Two thresholds that were measuring the wrong thing

`meta` asserted a keystone's cost spread across the four doors was at least **4 points**. With
`LANE_STEP` at 560 the whole map is fewer points across and every spread shrank with it — which
says nothing about whether the countries still mean anything. It is a *proportion* now.

And beside it, the claim itself with no threshold in it at all: **the cheapest way to a corner is
a door that lives in that corner's country.** That is either true or it is not, it needs no
number, and it is what the spread was always standing in for.

`roads` and `nodes` had the same shape of problem — "four ways" and "every weapon has a flat
damage node" were descriptions of the old tree, not rules. Both restated.

### The card that would not run off the screen, and could not be tested

The tree's own pane stops 330px short of the window, so no node is ever near the right edge and
the flip that keeps the tooltip on screen cannot be reached by moving a real mouse. The handler
reads `ev.clientX`, so the corner it was written for is reachable by *sending* one.

The first version of that test dispatched three corner moves and measured once — and the last of
the three was the bottom left, which fits whatever the code does. A card that had run a thousand
pixels off the right edge two moves earlier came back inside before anybody looked. It measures
after each corner now.

### And a coin flip that had been sitting in poe4 since it was written

"A real late-game hit cannot shock a boss" failed once in a suite run. The ailment chance is
capped at 75%, so a single hit says that one time in four. The same file had already fixed this
exact shape for its magnitude cases three sections earlier and missed it here. Twelve tries, take
the most any of them landed.

23 breaks for `acc`, 54 tests green, wave-50 soak clean.

## Twenty-second pass — a card that could not be used, and a ceiling that was reached

### The card with no gate

> *"There is no cap for upgrades that give me endurance charges etc. I have 3/3 gain Set Jaw
> but I still get presented with it."*

`chgkill`, `chgcrit` and `chghurt` had no `req` at all. A card that fills a three-deep bar three
at a time has nothing left to sell, and one that fills it *four* at a time is selling a point
that falls on the floor. Worse than useless: a card that cannot move a number is a card eating
one of your three choices, which is the most expensive kind of card in the game.

The rule is `chargeGenRoom(id, have)` — you may buy up to what the bar holds and not one past it
— and because it reads `stackCap`, **raising the bar unlocks the card again**, which is the loop
those two cards were always supposed to make together. An audit of every card with no `req` found
these three and nothing else; `attstr`, `greed` and `eva` are genuinely unbounded and fine.

Rage had the same hole with a different shape: `Warpath` and `Spite` were gated on *being melee*
and nothing else, so they could sell generation well past the thirty-deep well. And the two cards
that RAISE a ceiling had no ceiling of their own, which is a pool with no bottom — `CHARGE_CAP_MAX`
and `RAGE_CAP_MAX` now stop them.

### Twice the points

> *"I just got to wave 39 but still only got 60 points... increase that to 120."*

`META_POINT_MAX` 60 → 120, and levels back to **one point each** (they were one, then two, and
one is the middle: a level is worth what a wave is worth and neither of the two things you do in
a run outweighs the other).

Two things followed that were not in the request, and both were found by tests rather than by
thinking about it:

**The power budget.** At 60 a stay-at-home red build was ×2.53 against a declared ×2.60. At 120
it is ×3.47 — 37% more for double the points, because the increased-damage caps do their job. The
declaration moves with the ceiling it was always about and keeps the same slack. A budget that
silently doubled would catch nothing, which is the whole reason it is written down.

**The rim stopped being a wall.** `meta` asserted that no single walk collects every keystone on
the rim; at 60 points the four corners were simply too far apart and the *distance* was the
limit. At 120 the union of four real walks cost 67. Raising a keystone from 3 points to 5 took it
to 75 — still collectible.

So the rule changed rather than the number. At 120 the limit is no longer "you cannot", it is
"that is three fifths of everything you will ever have and you will have nothing else" — which is
a real trade, and the thing the old rule was protecting. Pretending otherwise would have meant a
keystone price nobody would ever pay. The power budget is what actually bounds a rim-walker, and
it measures the focused build, which is stronger.

### The third flake, caught with its text

`wavemod` had failed once in a suite run and not reproduced; this time the output was captured:

```
shocking a body changed the rendered frame by only -187 yellow pixels
SHOCK IS IN THE FRAME: {"before":524,"after":337}
```

A normal `before` is 3 to 90. 524 is the **gold wave banner**, which is drawn across the middle
of the screen for a second and a half and floods the very region this probe samples. Under a full
suite run the earlier cases take longer, the banner is still up when `before` is grabbed, and the
test then reports that shocking a body made the frame *less* yellow.

One frame was never a measurement anyway — the arcs animate off `vtime`, so a single grab can
land in a trough. It takes the quietest of eight frames for `before` and the brightest of eight
for `after`, spanning long enough for any banner to clear: 1–3 against 429–598, and the break
that removes the draw call still fails at 3 against 11.

That is three flakes now with one root — a fixed `sleep()` standing in for a condition — and the
third one found by capturing the failure instead of guessing at it.

6 new breaks, 54 tests green, wave-50 soak clean.

## Twenty-third pass — the bar in front of the bar, and one curve instead of two

> *"I want to implement ES as it's done in PoE. Make sure chaos damage goes through Energy
> Shield. Make sure some bosses also have Energy Shield, and give the higher tier bosses some
> chaos damage. Right now the lvl 20-25-30-35-40 bosses are extremely easy to kill... They get
> oneshotted at times, i dont want that."*

### A defence that pays you for the hits you don't take

Armour pays you for every hit you take. Evasion makes some of them miss. Energy shield is the
third shape and the only one that **comes back**: a pool in front of the health pool that refills
at 22% of its maximum a second once you have gone 2.5 seconds untouched.

Four rules, all in one twelve-line function:

```
· it eats the hit first; what it cannot eat spills through
· chaos walks past it
· any damage restarts the recharge clock — including the chaos that walked past
· it is a SHARE, not a yes-or-no
```

The fourth is the one that makes it a system rather than a gate. A hit here is already a mix
carrying its own provenance, so a swing half converted to chaos puts *half of itself* past the
shield and leaves the other half to be eaten. Nothing needed a special case: `chaosShareOf` reads
the mix the conversion pipeline already built. It means you never need a chaos build to fight a
shielded boss — you need *some* chaos, which is a much better thing to want.

The same function runs the hero's shield, a monster's, and a boss's, because the only thing it
needs is an object with `es` and `esMax`. "Some bosses have one" is therefore a column in a table
rather than a second system.

Poison is chaos over time, so `ailmentDamage` passes its type through and a dose walks past a
shield while a burn meets it — the one place where the ailment table and the shield had to agree,
and they agree by construction rather than by a rule written twice.

### The thing that was actually wrong with the bosses

The complaint was that the bosses at 20/25/30/35/40 fold. Measuring first, before touching a
number:

```
w10 OMEGA BRICKTHANE  ULTRA  5,314        w35 THE BONE BARON   4,147
w20 MEGA BONE BARON   ULTRA 13,813        w45 THE ASH TYRANT   8,213
w30 ULTRA LAVABRICK   ULTRA 17,740
```

A wave-45 boss with 8,200 health next to a wave-40 ultra's 56,000. Not a number that wanted
raising — a **second curve**. The ultras climbed 2.3× a tier with `mobRankMul()` on top; the
bosses between them climbed a flat 420 a tier with **no monster level at all**, while every
ordinary skeleton in the game has one. Two formulas for one idea, drifting apart a little more
every tier for the whole run.

The first repair raised the in-between curve — quadratic in tier, with the monster level added.
It fixed wave 45 and broke something else: a wave-35 boss came out at ×1.31 of the wave-30 ultra
it followed. An ultra that is not a wall is not an ultra. Steeper was not the claim.

So there is one curve now. `bossPool(baseHp, st)` is *the* curve; the ultras read it at whole
steps and the ordinary bosses read the same one at the half-steps between, at `BOSS_SHARE` of it.
A wave-15 boss is half of what wave 15 is worth, and stays half forever with nobody keeping two
formulas in step.

```
w10 ULTRA  5,713          w15  3,063  ×0.54
w20 ULTRA 16,947          w25  8,230  ×0.49
w30 ULTRA 24,481          w35 14,623  ×0.60
w40 ULTRA 64,683
```

The wave-45 boss went from 8,200 to 44,700. The wave-50 raid is 40% deeper than it was, which is
the cost of the five ordinary bosses in it no longer evaporating.

### A test that called a correct game broken

The first version of the curve assertion walked the sampled waves in order and demanded each be
deeper than the last. It failed on a correct build:

```
wave 15 (LORD LAVABRICK, 3637) is no deeper than wave 10 (6908)
```

Waves 10/20/30/40 are ultras and 15/25/35 are not, so the pool is *supposed* to dip on the wave
after an ultra. The comparison crossed the boundary between two kinds of thing. It compares
within kind now, with the reason written above it.

Then the replacement was wrong in the other direction. "The ordinary bosses must climb at least
as steeply as the ultras" passes for a build where they climb steeply *past* them — which is
exactly the overshoot above. The claim that is actually true of a share is a **band**: every
ordinary boss between 0.40 and 0.90 of the ultra before it, and the share must not drift across
the run (max/min < 1.6). That catches the original bug at ×0.19, the overshoot at ×1.28, and a
`BOSS_SHARE` set wrong in either direction — four breaks where the slope version caught one.

### Two more of the same three bugs

**A one-sample measurement.** The curve was read from one spawn a wave. Which boss stands up is a
roll and so is its rank, and one run put wave 15 at ×0.405 against a floor of 0.40 — a pass by a
hair and a red suite next run for no reason at all. Twelve spawns a wave, averaged.

**A patch that averaged the signal away.** `stormcol` failed in a suite run at a margin of exactly
10 against a threshold of 10. A bolt is drawn *jagged*, so it does not sit on the straight line
between its own endpoints — a 6px patch at the midpoint catches it on some frames and clean
ground on others. Widening the patch made it **worse**: a thin bright line averaged over a bigger
box is a line averaged away. What says "a yellow line crossed here" is the **hottest pixel**, not
the mean one. Margin went from 10±2 to 91±1, and every colour break still fails.

That is four flakes now with one root — a fixed sample standing in for a condition — and two of
the four were found by reading the captured failure rather than by re-running until it passed.

29 new breaks, 55 tests green, wave-50 soak clean.
