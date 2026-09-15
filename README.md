<div align="center">

# 🧱 BRICKBLADE

### *Smash the waves. Grab the studs. Everything falls apart.*

**A brick-built action RPG that lives in a single HTML file.**

No build step. No bundler. No `node_modules`. Double-click it.

<br>

![The title screen](docs/title.jpg)

<br>

`66 bricks` · `41 monsters` · `8 bosses` · `11 builds` · `50 waves` · `1 file`

</div>

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
every level and every chest hands you a **brick** — one of 66 upgrades — and the bricks
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
no slot at all — and its bricks turn up more often, plus a starting kit to match.

</td><td width="33%" valign="top">

### 🩸 Elements & ailments

Fire, frost and lightning each stack. **The fifth stack breaks something**: burning
becomes a pool of damage over time, chill freezes you solid, shock leaves every hit
against you critting.

Imp masters lay **curses** on top — SUNDERED, LEADEN, BRITTLE, WITHERED — that strip
resistance, slow your swing, thin your armour and choke your regeneration.

**Brickbane** turns it around: a wedge of poison gas that hits once and then keeps eating
whatever it touched. Poison **stacks nine deep on a monster and twelve on a boss** — count
the green beads orbiting a body — and the legendary makes a poisoned corpse detonate for
the opening hit **+25% per stack** (+50% upgraded), which seeds the neighbours, which can
kill them, which sets *them* off.

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

## The damage model is Path of Exile's

Every stat in the game runs through one pool, in one order, every time:

```
(base + all flat added) × (1 + sum of every increase) × each MORE multiplier, one at a time
```

That means a MORE multiplier you picked up at wave 12 keeps multiplying the flat health
you add at wave 40, and taking twenty-seven bricks in reverse order produces byte-identical
stats. Increases are additive with each other; MOREs never are. Cooldowns use the mirror of
it — `max(floor, base / (1 + increased))` — so attack speed has a real ceiling instead of an
asymptote.

Every **FLAT damage** brick in the game reads off one shared ladder, so a common is never
a rare wearing a different colour:

| | common | uncommon | rare | epic | legendary |
|---|--:|--:|--:|--:|--:|
| Sharpened Blade, Heavy Orbit, Blaster Power,<br>Storm Surge, Storm Edge, Big Kaboom, Whirling Edge | +0.5 | +1 | +3 | +5 | +8 |

**Increased damage over time** is one pool too, not one per ailment. `Rotbrick` sits in the
DAMAGE section of the bench, not under any spell, and every lingering damage you inflict
reads it — Brickbane's poison is simply the first thing that qualifies.

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

---

## The boss ladder

A boss every fifth wave, an ULTRA every tenth, and the grand raid at fifty. Spawned for
real and read off the live build:

| Wave | | Boss | Health | Armour | Hit | Spell suppression |
|--:|---|---|--:|--:|--:|--:|
| 5 | boss | VON BRICKTHANE | 510 | 49 | 6 | 15% |
| 10 | **ULTRA** | OMEGA BRICKTHANE | 3,318 | 185 | 22 | 30% |
| 15 | boss | LORD LAVABRICK | 1,908 | 183 | 27 | 15% |
| 20 | **ULTRA** | MEGA BONE BARON | 6,999 | 780 | 57 | **62%** |
| 25 | boss | THE DREAD ROOST | 3,766 | 429 | 60 | 15% |
| 30 | **ULTRA** | ULTRA LAVABRICK | 8,544 | 685 | 77 | 30% |
| 35 | boss | THE BONE BARON | 4,147 | 787 | 96 | **45%** |
| 40 | **ULTRA** | OMEGA BRICKTHANE | 12,367 | 1,070 | 112 | 30% |
| 50 | **RAID** | every boss at once | — | — | — | — |

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

## Every cap

Nothing in the game is unbounded. A brick that would push past its ceiling stops being
offered at all.

| Defence | | Spells | | Prizes | |
|---|--:|---|--:|---|--:|
| Armour reduction | 85% | Guardian bricks | 15 | Rage | ×2 |
| Elemental resistance | 80% | Ring reach | 190 px | Ward | ×2 |
| Critical chance | 95% | Blaster bricks | 40 | Fortune | ×2 |
| Critical multiplier | 600% | Blaster pierce | 8 | Heart stacks | 5 |
| Regeneration | 0.70/s* | Storm leaps | 15 | Poison on a monster | 9 |
| Curse stacks on you | 9 | Poison on a boss | 12 | | |
| Flat armour | 520 | Storm bricks | 5 | | |
| Increased armour | +200% | Storm forks | 2 (40 nodes) | | |
| | | Gas reach | 430 px | | |
| | | Bombs | 8 | | |
| | | Blast radius | 110 px | | |
| | | Cluster ranks | 2 | | |

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

**You are not invulnerable between hits.** The window after taking damage is **0.10s**,
not a second — everything landing on you counts, and a crowd is a crowd.

Hearts stack at **×1.5 / 1.85 / 2.10 / 2.30 / 2.45** — five of them are worth two and a
half, never five. Blaster pierce costs the brick **15% of its damage per foe** at one
rank, easing to 10% once all eight are bought; a fully pierced brick deals 6.13× one hit
down a full line.

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

## Controls

| | |
|---|---|
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> / arrows | move |
| **Left mouse** | swing — *hold* to whirl, once you own Blademaster |
| <kbd>Space</kbd> | roll — invulnerable through it, and a perfect dodge refunds bomb cooldown |
| <kbd>F</kbd> | start the next wave early — every wave opens with a breather (30s after a boss) |
| <kbd>F</kbd> *(held, mid-fight)* | call the next wave down on top of this one — 3 seconds' warning, up to 3 stacked, every wave pays its own reward |
| <kbd>C</kbd> | character sheet |
| <kbd>L</kbd> | combat log · <kbd>Shift</kbd>+<kbd>L</kbd> cycles taken / dealt / events |
| <kbd>R</kbd> / <kbd>X</kbd> | on a reward screen: reroll · decline for studs |
| <kbd>P</kbd> or <kbd>Esc</kbd> | pause |
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

<kbd>F1</kbd> during play opens a bench that grants any brick at any rarity, jumps to any
wave, and switches difficulty live. Hovering a card shows the full now → next comparison;
<kbd>Shift</kbd>+scroll on one changes its rarity between that card's own minimum and
maximum. It is how the numbers in this README were balanced. It is deliberately not
advertised on the menu.

</details>

---

<div align="center">

**Built brick by brick.** Everything — the renderer, the AI, the pathfinder, the damage
model, the music — is in [`lego-survival.html`](lego-survival.html).

*LEGO® is a trademark of the LEGO Group, which does not sponsor, authorise or endorse this
project.*

</div>
