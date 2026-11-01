# BRICKBLADE native — the port

The browser game (`../lego-survival.html`, ~31,400 lines of JavaScript) rewritten in
**C# on raylib**, built as a standalone Windows executable. The browser version is the
reference: same rules, same numbers, same look. Nothing is meant to be left out.

## Why this shape

- **C# (.NET 8)** compiles to native code at run time, with no browser underneath. It also
  keeps the JS's shape: garbage collection, lambdas and lists, so the port can follow the
  original line by line.
- **raylib** gives a window, input, audio and OpenGL, and stays out of the way. The game
  draws every brick and stud itself through the canvas 2D API. The port keeps that by
  implementing the canvas API (`src/Engine/Ctx.cs`) on a batched GPU renderer
  (`src/Engine/Gpu.cs`), so the draw code ports almost call for call.
- **Single-file `.exe`.** `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`
  bundles the runtime, raylib and the fonts. There is nothing to install.

## Layout

| Path | What |
|---|---|
| `src/Engine/Ctx.cs` | CanvasRenderingContext2D: transforms, paths (arc, ellipse, arcTo, curves), fill (convex fan or LibTess for concave, multi-part and even-odd), stroke (joins, caps, dashes), text, `drawImage`, gradients, `clip`, blend modes, shadows; `OffCanvas` (`document.createElement('canvas')`), `Path2D` |
| `src/Engine/Gpu.cs` | One vertex buffer, two shaders (textured; gradient), premultiplied alpha, render targets, clip masks |
| `src/Engine/Fonts.cs` | Titan One, Fredoka 400–700, DejaVu (symbols), Noto Emoji; sized by the em box like a browser; per-glyph fallback |
| `src/Engine/Col.cs` | Colours: every CSS colour string converts implicitly; `rgba()` builds one without a string |
| `src/Engine/Js.cs` | JS semantics: `Math.random`, `String(n)`, `toFixed`, int32, truthiness |
| `src/Engine/Store.cs` | `localStorage` → `%APPDATA%\Brickblade\storage.json` |
| `src/Game/*.cs` | The game: `static partial class G`, one file per section of the JS, same names |
| `tools/coverage.py` | Every top-level JS declaration, and whether the port has it |
| `tools/coverage-map.txt` | Names deliberately renamed or not carried over, with the reason |
| `tools/shimtest.html` | The canvas test scene as the browser draws it, for comparison with `--shimtest` |

## Conventions

- **Names stay.** A JS function `hitEnemy` is `G.hitEnemy`, and a constant `RAGE_CAP` is
  `G.RAGE_CAP`. That is what lets `tools/coverage.py` check the port.
- **Numbers are `double`** everywhere a JS number was. Indices and counts are `int`. `x|0` is
  `Js.I32(x)`.
- **Colours** are `Col` (a JS colour string converts implicitly). Template-literal colours
  become `rgba(r, g, b, a)` calls.
- **`Math` is the JS's.** `Engine/JsMath.cs` is aliased over `System.Math` for the whole
  project, so `Math.Round` rounds halves up as `Math.round` does (C#'s own rounds them to even).
- **Text and numbers:** `String(n)` and `${n}` → `Js.Num(n)`; `n.toFixed(d)` → `Js.ToFixed(n, d)`.
  The process runs with the invariant culture, so a Windows set to a comma-decimal locale
  changes nothing.
- **Truthiness:** `if (x)` on a number → `if (Js.T(x))`; `a || b` on numbers → `Js.Or(a, b)`.
- **Objects:** each kind of JS object gets a class with the fields the JS gives it (`Ent` for
  hero, monsters and minions, `Proj`, `Prop`, `Fx`…). An undefined field is `0`, `false` or
  `null`, which is how the JS reads it (`e.foo || 0`).
- **`Math.random()`** → `Js.R()`, overridable so tests can make a run reproducible.
- **The screens are the game's own HTML and CSS.** The title, cards, tree, gear, options and
  every tooltip are the page's markup and stylesheet, embedded from `lego-survival.html` and
  run by a small DOM and CSS engine (`src/Web`): the cascade, block, inline, flex and grid
  layout, painting, events, focus, text fields and sliders. The JS that builds them with
  `innerHTML` and `classList` is ported line for line against it.
- **A JS object used as a map** is a `JsMap` (a `Dictionary` whose missing keys read as
  `null`/`0`, never a throw); one used as a map of numbers is a `NumMap`.
- **`for (const x of list)`** over a list the body can change (a hit that kills spawns the
  splits) is `foreach (var x in list.Live())`, which walks by index as JS does.
  `tools/live_loops.py` applies it to every loop over the game's live lists.

## Status

**Every section of the JS is ported.** `tools/coverage.py` finds every top-level JS
declaration in the C# (2414 of 2414; three text-size helpers are done another way and
listed in `tools/coverage-map.txt`).

| Section | JS lines | C# |
|---|---|---|
| Engine (canvas, GPU, fonts, storage, audio out) | — | `src/Engine` |
| Page engine (DOM, CSS, layout, paint, events) | markup 743–1010, CSS 9–742 | `src/Web` |
| UTILS · CONSTANTS | 1014–3366 | `Utils.cs`, `Constants1-3.cs` |
| CANVAS / SETTINGS / TEXT SIZE · AUDIO · INPUT | 3367–4056 | `Canvas.cs`, `Audio.cs`, `Input.cs` |
| STATE · PLAYER · WORLD · PICKUPS | 4057–5411 | `State.cs`, `Player.cs`, `Ent.cs`, `World.cs`, `Pickups.cs` |
| ENEMIES · WAVES · THE ROSTER (leagues) | 5412–11357 | `Enemies1-4.cs`, `Gear.cs`, `Waves.cs`, `Breach.cs`, `Abyss.cs`, `Strongbox.cs`, `Ritual.cs`, `Legion.cs` |
| RARITY & UPGRADES · the skill tree · modes · map | 11358–18277 | `Upgrades.cs`, `Meta1-6.cs`, `Fav.cs`, `Rpg.cs`, `Cave.cs`, `MapRpg.cs` |
| TEST BENCH · WHAT A BRICK DOES · CHARACTER SHEET | 18278–21304 | `Bench.cs`, `Brick.cs`, `Sheet.cs`, `Book.cs`, `Equip*.cs`, `Reward.cs` |
| UPDATE · THE ARROW · THE GROUND SLAM · PATHFINDING | 21305–27346 | `Control.cs`, `Update.cs`, `Arrow.cs`, `Minions.cs`, `Slam.cs`, `Nav*.cs`, `EnemyCore.cs`, `Boss1-2.cs`, `SuperBoss.cs`, `Projectiles.cs`, `Fx.cs`, `MonsterMarks.cs` |
| DRAW · RENDER · COMBAT LOG · HUD | 27347–31835 | `DrawFig.cs`, `DrawProps.cs`, `Render.cs`, `CombatLog.cs`, `Hud.cs` |
| TITLE SCREEN · GAME FLOW · MAIN LOOP | 31836–32430 | `GameFlow.cs`, `Boot.cs` |
| The window and its loop | — | `App.cs` |

## How it runs

`App.Run` does what the browser did around the script. Each turn of its loop is one browser
frame: input is routed into the page (hover, clicks, keys by their browser `code`, the wheel,
typed text), due timers run, `requestAnimationFrame` callbacks run (the game's `frame()`:
`update` and `render`), `MutationObserver` callbacks run, and the page is painted over the
field. `Boot.cs` runs the script's top-level statements once, in JS order.

**Static initialisers run in JS order.** A partial class's field initialisers run file by
file in the order the compiler is given the files; `GameOrder.props` (written by
`tools/compile_order.py`) hands the game's files over in the order of the JS they port, so a
constant is always set up before anything below it reads it, as in the browser. Run the tool
after adding or renaming a file in `src/Game`.

**Render quality.** The field can be drawn below the screen's own resolution: the world goes
into an offscreen canvas and is scaled up, and everything after it (the map, the HUD, the
markers, the reticle) and every menu is drawn on the window at full sharpness. AUTO starts at
full and steps down (75%, then 50%) when most of 2.5 seconds of play runs under 45 frames a
second.

## Testing without a person at the keys

`--script` runs a scripted session (see `src/AutoTest.cs`): clicks by id or CSS selector, keys,
the mouse, drags, calls into the game, a hero kept alive, screenshots and one-line status
reports with the frame's cost.

```
dotnet bin/Release/net8.0/Brickblade.dll --w 960 --h 600 --script \
  "10:click:startBtn; 40:with:startWave=10; 40:set:invuln=9999; 300:status; 300:shot:w10.png; 301:quit"
```

What has been run this way: every weapon in arcade for 5000 frames; boss and super-boss waves
5–40; the RPG Outskirts; the whole test bench (every brick granted, every utility, league,
league-tree node and rare modifier, all 55 monsters in the sandbox) followed by a fight;
every screen opened and screenshotted; the equipment screen's click-to-wear, drag, stash and
bin. None of them logs an exception. Screens were compared with Chromium running the original
page (`tests/DomTest` compares element rectangles; the gameplay field by eye).

## Build and run

```
cd native
dotnet build -c Release
dotnet bin/Release/net8.0/Brickblade.dll               # the game
dotnet bin/Release/net8.0/Brickblade.dll --shimtest --out shim.png
python3 tools/coverage.py --missing                    # anything not carried over
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out/win
```

In a container without a display: `xvfb-run -a dotnet …` (Mesa renders in software, so the
frame rate there says nothing about a real graphics card).
