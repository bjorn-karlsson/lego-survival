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
