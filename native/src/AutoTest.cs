using System.Reflection;

namespace Brickblade;

/* A SCRIPTED SESSION, for checking the build without a person at the keys:
     --script "30:click:startBtn; 60:keydown:KeyD; 200:shot:a.png; 260:status; 400:quit"
   Each step runs on its frame. Steps:
     click:ID            the element's click()
     keydown:CODE / keyup:CODE / key:CODE (down and up)
     move:X,Y            the pointer to CSS px X,Y
     down:B / up:B       a mouse button (0 left, 2 right) at the pointer
     call:NAME           a static method of the game with no arguments (G.NAME())
     with:NAME=ARG       one with a string argument (with:setWeapon=axe)
     set:FIELD=NUMBER    a number on the hero (player.FIELD), e.g. set:invuln=9999
     shot:FILE           a screenshot
     status              one line: state, wave, monsters, hero, frame rate
     quit                close the window */
public static class AutoTest
{
    static readonly SortedDictionary<int, List<string>> steps = new();
    public static bool On => steps.Count > 0;
    public static void Load(string script)
    {
        if (string.IsNullOrWhiteSpace(script)) return;
        foreach (var raw in script.Split(';'))
        {
            var s = raw.Trim(); if (s == "") continue;
            int i = s.IndexOf(':'); if (i < 0) continue;
            int f = int.Parse(s.Substring(0, i));
            if (!steps.TryGetValue(f, out var l)) steps[f] = l = new();
            l.Add(s.Substring(i + 1));
        }
    }
    static double px = 400, py = 300;
    /* runs before the frame: returns false to quit */
    public static bool Step(int frame, double fps, Action<string> shot)
    {
        if (!steps.TryGetValue(frame, out var l)) return true;
        foreach (var s in l)
        {
            int i = s.IndexOf(':');
            string cmd = i < 0 ? s : s.Substring(0, i), arg = i < 0 ? "" : s.Substring(i + 1);
            try
            {
                switch (cmd)
                {
                    case "click": Dom.document.getElementById(arg)?.click(); break;
                    case "keydown": Dom.KeyDown(new KeyEvt { code = arg, key = KeyName(arg) }, G.onKeyDown); break;
                    case "keyup": Dom.KeyUp(new KeyEvt { code = arg, key = KeyName(arg) }, G.onKeyUp); break;
                    case "key": Dom.KeyDown(new KeyEvt { code = arg, key = KeyName(arg) }, G.onKeyDown); Dom.KeyUp(new KeyEvt { code = arg, key = KeyName(arg) }, G.onKeyUp); break;
                    case "move": { var p = arg.Split(','); px = double.Parse(p[0]); py = double.Parse(p[1]); Dom.PointerMove(px, py, 0); break; }
                    case "down": Dom.PointerDown(int.Parse(arg)); break;
                    case "up": Dom.PointerUp(int.Parse(arg)); break;
                    case "call":
                    {
                        var m = typeof(G).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == arg && x.GetParameters().All(q => q.IsOptional));
                        if (m == null) Console.WriteLine("[script] no G." + arg + "()");
                        else m.Invoke(null, m.GetParameters().Select(q => q.DefaultValue).ToArray());
                        break;
                    }
                    case "with":
                    {
                        /* with:NAME=ARG -- G.NAME("ARG"), e.g. with:setWeapon=axe */
                        var kv = arg.Split('=', 2);
                        var m = typeof(G).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == kv[0] && x.GetParameters().Length >= 1
                            && (x.GetParameters()[0].ParameterType == typeof(string) || x.GetParameters()[0].ParameterType == typeof(double) || x.GetParameters()[0].ParameterType == typeof(int) || x.GetParameters()[0].ParameterType == typeof(bool))
                            && x.GetParameters().Skip(1).All(q => q.IsOptional));
                        if (m == null) { Console.WriteLine("[script] no G." + kv[0] + "(x)"); break; }
                        var t0 = m.GetParameters()[0].ParameterType;
                        object a0 = t0 == typeof(string) ? kv[1] : t0 == typeof(double) ? double.Parse(kv[1]) : t0 == typeof(int) ? int.Parse(kv[1]) : (object)(kv[1] == "true");
                        m.Invoke(null, new object[] { a0 }.Concat(m.GetParameters().Skip(1).Select(q => q.DefaultValue)).ToArray());
                        break;
                    }
                    case "set":
                    {
                        var kv = arg.Split('='); var fi = typeof(Ent).GetField(kv[0]);
                        if (fi != null && G.player != null) fi.SetValue(G.player, double.Parse(kv[1]));
                        break;
                    }
                    case "spawn":
                    {
                        /* spawn:TYPE,DX,DY -- a monster beside the hero */
                        var p = arg.Split(',');
                        G.spawnEnemy(p[0], G.player.x + double.Parse(p[1]), G.player.y + double.Parse(p[2]));
                        break;
                    }
                    case "probe":
                    {
                        var near = G.enemies.OrderBy(e => G.dist(e, G.player)).Take(3).Select(e => $"{e.type}@{G.dist(e, G.player):0} hp={e.hp:0.#}/{e.maxHp:0.#} st={e.state}");
                        Console.WriteLine($"[probe] frame {frame} hero=({G.player.x:0},{G.player.y:0}) mouse=({G.mouse.wx:0},{G.mouse.wy:0}) down={G.mouse.down} proj={G.projectiles.Count}:{string.Join(",", G.projectiles.Take(4).Select(q => q.kind))} near: {string.Join(" | ", near)}");
                        break;
                    }
                    case "shot": shot(arg); break;
                    case "status":
                        Console.WriteLine($"[status] frame {frame} state={G.state} wave={G.wave?.n}/{G.wave?.state} enemies={G.enemies?.Count} " +
                            $"hp={G.player?.hp:0.#}/{G.player?.maxHp:0.#} lvl={G.player?.level} kills={G.player?.kills} studs={G.player?.studs} " +
                            $"picks={G.pendingPicks?.Count} fps={fps:0} upd={G.updMs:0.0}ms ren={G.renMs:0.0}ms page={App.PageMs:0.0}ms gl={App.GlMs:0.0}ms calls={App.LastCalls} tris={App.LastTris} errors={Dom.errors.Count}");
                        break;

                    case "quit": return false;
                }
            }
            catch (Exception e) { Console.WriteLine("[script] " + s + ": " + e); }
        }
        return true;
    }
    static string KeyName(string code) =>
        code.StartsWith("Key") ? code.Substring(3).ToLowerInvariant() : code.StartsWith("Digit") ? code.Substring(5) : code == "Space" ? " " : code;
}
