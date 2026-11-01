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
- **Text and numbers:** `String(n)` and `${n}` → `Js.Num(n)`; `n.toFixed(d)` → `Js.ToFixed(n, d)`.
  The process runs with the invariant culture, so a Windows set to a comma-decimal locale
  changes nothing.
- **Truthiness:** `if (x)` on a number → `if (Js.T(x))`; `a || b` on numbers → `Js.Or(a, b)`.
- **Objects:** each kind of JS object gets a class with the fields the JS gives it (`Ent` for
  hero, monsters and minions, `Proj`, `Prop`, `Fx`…). An undefined field is `0`, `false` or
  `null`, which is how the JS reads it (`e.foo || 0`).
- **`Math.random()`** → `Js.R()`, overridable so tests can make a run reproducible.
- **DOM screens** (title, cards, tree, gear, options…) are drawn natively in `src/Game/Ui*.cs`.
  The HTML strings the JS builds for tooltips and cards are rendered by a small rich-text
  renderer, so the text logic itself is ported unchanged.

## Status

Updated as the port proceeds. "JS lines" are lines of `lego-survival.html`. The
**Next line** column records where porting resumes in each section.

| Section | JS lines | State |
|---|---|---|
| Engine (canvas, GPU, fonts, storage) | — | done; `--shimtest` matches Chromium |
| UTILS | 1014–1084 | ported (`Game/Utils.cs`) |
| CONSTANTS | 1085–3366 | ported (`Game/Constants1-3.cs`) |
| CANVAS / SETTINGS / TEXT SIZE | 3367–3552 | ported (`Game/Canvas.cs`; adds RENDER QUALITY and VSYNC options) |
| AUDIO | 3553–3969 | ported (`Game/Audio.cs` on `Engine/WebAudio.cs`, a Web Audio work-alike; `Engine/SoundOut.cs`) |
| INPUT | 3970–4056 | ported (`Game/Input.cs`) |
| STATE | 4057–4391 | ported (`Game/State.cs`; run links are byte-compatible with the browser's) |
| PLAYER | 4392–4553 | ported (`Game/Player.cs`, `Game/Ent.cs`) |
| WORLD | 4554–4938 | ported (`Game/World.cs`) |
| PICKUPS / DEBRIS / PARTICLES | 4939–5411 | ported (`Game/Pickups.cs`) |
| ENEMIES | 5412–8647 | |
| WAVES / THE ROSTER | 8648–11357 | |
| RARITY & UPGRADES | 11358–18277 | |
| TEST BENCH | 18278–18844 | |
| WHAT A BRICK DOES | 18845–19715 | |
| CHARACTER SHEET | 19716–21304 | |
| UPDATE | 21305–22179 | |
| THE ARROW | 22180–23086 | |
| THE GROUND SLAM | 23087–23268 | |
| PATHFINDING | 23269–27346 | |
| DRAW: MINIFIGURE | 27347–28128 | |
| DRAW: PROPS & WORLD | 28129–28696 | |
| RENDER | 28697–30517 | |
| COMBAT LOG | 30518–30991 | |
| HUD | 30992–31835 | |
| TITLE SCREEN | 31836–31908 | |
| GAME FLOW | 31909–32329 | |
| MAIN LOOP | 32330–32430 | |
| HTML screens (markup 743–1010, CSS 9–742) | | |

**While the port is in progress the project does not compile**: a section calls functions
from sections not yet ported. `dotnet build` errors of the kind "name does not exist" are the
list of what is left; any other kind of error is a bug in what has been ported.

**Resume at JS line 5412** (ENEMIES).

## Build and run

```
cd native
dotnet build -c Release
dotnet bin/Release/net8.0/Brickblade.dll               # the game
dotnet bin/Release/net8.0/Brickblade.dll --shimtest --out shim.png
python3 tools/coverage.py --missing                    # what is not ported yet
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o out/win
```

In a container without a display: `LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a dotnet …`.
