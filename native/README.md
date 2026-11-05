# BRICKBLADE — the Windows game

The same game as `lego-survival.html`, as a program of its own: no browser, one `.exe`.
Same rules, same numbers, same screens; it draws with your graphics card directly, and
lets you trade sharpness for speed on a slow machine or a big screen.

## Make the .exe (once)

On Windows, with the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed:

```
cd native
build-windows.cmd
```

That leaves **`native\out\win\Brickblade.exe`** — a single file (about 40 MB) with
everything inside it. Copy it anywhere and double-click it. Nothing else needs installing.

(From Linux or macOS the same command line works: see `build-windows.cmd`.)

## Playing

Everything is as in the browser version: the title, the two modes, the tree, the bench
(type 1337 in a run), run links. Three things are the native build's own:

| | |
|---|---|
| **F11** or **Alt+Enter** | fullscreen (borderless) and back |
| **F3** | frame rate, the field's resolution, and what each frame costs |
| **OPTIONS → RENDER QUALITY** | FULL, 75%, 50% or AUTO (the default) |
| **OPTIONS → VSYNC** | on by default; off draws up to twice your display's rate (at least 240 a second) |

**If the game is slow**, set RENDER QUALITY to 75% or 50%. The battlefield is then drawn
with fewer pixels and scaled up; the menus, the HUD and all text stay sharp. AUTO does
this by itself when frames start to drop. **F3** shows where the time goes.

A run link copied in the browser opens here too: start the game with
`Brickblade.exe --run <link>`.

## Where things are kept

`%APPDATA%\Brickblade\`:

- `storage.json` — your progress, gear, options (what the browser kept in localStorage;
  the two are separate, so a browser save does not carry over)
- `storage.json.bak` — the save before the last one. If `storage.json` is ever damaged
  (a power cut while saving), the game restores this one by itself and keeps the damaged
  file beside it as `storage.json.unreadable-<date>`
- `brickblade.log` — what went wrong, if anything did (fresh each start)
- `crash.log` — written only if the game stops on an error

## For developers

How the port is built, its conventions and its tools: [PORTING.md](PORTING.md).
