# Meta-progression — design

*Status: **BUILT** — phases 1 to 3. What shipped differs from the design below in four
places, all decided by the author; the differences are listed under
[What changed on the way in](#what-changed-on-the-way-in). The design text is kept as it was
written so the reasoning survives.*

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
