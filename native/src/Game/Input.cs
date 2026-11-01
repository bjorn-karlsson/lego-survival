namespace Brickblade;

/* =============================== INPUT (JS 3970–4056) =============================== */
/* what a browser KeyboardEvent / MouseEvent carried, as the native loop fills it in */
public sealed class MouseEvt { public int button; public double clientX, clientY; public bool onCanvas; }
public sealed class Mouse { public double x, y; public bool down; public double wx, wy; public bool rdown, lpress, rpress, fresh; }

public static partial class G
{
    public static readonly Dictionary<string, bool> keys = new();
    public static bool key(string code) => keys.TryGetValue(code, out var v) && v;
    public static readonly Mouse mouse = new();
    public static bool everInput = false;
    public static void onKeyDown(KeyEvt e)
    {
        /* A TEXT FIELD OWNS ITS KEYS (the UI routes them there first; Esc leaves it) */
        var tg = Dom.focused != null && Dom.focused.isConnected ? Dom.focused.tagName : null;
        if (tg == "INPUT" || tg == "TEXTAREA")
        {
            if (e.code == "Escape") Dom.focused.blur();
            return;
        }
        /* CTRL+F, or /, while the tree is open goes to its search box */
        if (treeOn && ((e.code == "KeyF" && (e.ctrlKey || e.metaKey)) || e.key == "/"))
        {
            var inp = document.getElementById("treeSearch");
            if (inp != null) { e.preventDefault(); inp.focus(); inp.select(); return; }
        }
        everInput = true;
        if (e.code is "Space" or "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight" or "Tab") e.preventDefault();
        if (e.code == "Tab" && !e.repeat && (state == "play" || state == "pause" || state == "book")) mapToggle();
        keys[e.code] = true;
        if (e.code == "KeyF" && !e.repeat && state == "play" && wave.state == "ready") waveSkip();
        else if (e.code == "KeyF" && !e.repeat && state == "play" && wave.state == "cleared" && wave.hold) waveGoOn();
        if (e.code == "KeyM") toggleMute();
        /* P IS THE SPELLBOOK, and it does not stop the wave */
        if (e.code == "KeyP" && (state == "play" || state == "book")) toggleBook();
        /* OPTIONS EATS ESC FIRST */
        if (e.code == "Escape" && optOn) { closeOptions(); }
        else if (e.code == "Escape" && state == "title" && rpgScreenOn()) { toggleRpgScreen(false); }
        else if (e.code == "Escape" && bookOn) { toggleBook(); }
        else if (e.code == "Escape" && state == "gear") { toggleGear(); }
        else if (e.code == "KeyI" && ((state == "play" && modeHasGear()) || state == "gear")) { toggleGear(); }
        else if (e.code == "Escape" && state == "ritual") { toggleRitual(); }
        else if (e.code == "KeyU" && (state == "play" || state == "ritual")) { toggleRitual(); }
        else if (e.code == "Escape" && (state == "play" || state == "pause")) togglePause();
        if (e.code == "KeyC" && (state == "play" || state == "char")) toggleChar();
        if (e.code == "KeyN" && state == "char") setSheetNerd(!sheetNerd);
        if (e.code == "KeyL" && (state == "play" || state == "pause")) { if (e.shiftKey) clogCycle(); else clogToggle(); }
        if (e.code == "Escape" && state == "char") toggleChar();
        if (e.code == "Escape" && treeOn) toggleTree();
        else if (e.code == "KeyT" && (state == "play" || state == "tree") && !rpgOn()) toggleTree();
        if (state == "play") cheatCode(e.code);
        if (e.code == "Escape" && state == "cheat") toggleCheat();
        /* a HELD number key must not pick on the screen that opens straight after */
        if (state == "upgrade" && !e.repeat && (e.code == "Digit1" || e.code == "Digit2" || e.code == "Digit3" || e.code == "Digit4"))
            chooseUpgrade(int.Parse(e.code.Substring(5)) - 1);
        if (state == "upgrade" && e.code == "KeyR" && !e.repeat && !lootHeld) doReroll();
        if (state == "play" && e.code == "KeyR" && !e.repeat && !OPT.autoLoot && pendingPicks.Count > 0) { lootHeld = true; lootChain = true; lootNow(); }
        if (state == "upgrade" && e.code == "KeyX" && !e.repeat) rejectReward();
    }
    public static void onKeyUp(KeyEvt e) { keys[e.code] = false; if (e.code == "KeyR") lootHeld = false; }
    public static void onMouseMove(double x, double y) { mouse.x = x; mouse.y = y; }
    public static void onMouseDown(MouseEvt e)
    {
        everInput = true;
        if (e.button == 0)
        {
            /* the combat log swallows its own clicks */
            if (clogHit(e.clientX, e.clientY))
            {
                if (clogClearAt(e.clientX, e.clientY))
                {
                    clogReset();
                    clogPush(CLOG_EVT, null, "log cleared", "#9fdc7e");
                    SFX.pick();
                    return;
                }
                int t = clogTabAt(e.clientX, e.clientY);
                if (t >= 0) { clogView = t; SFX.pick(); }
                return;
            }
            mouse.down = true;
            /* the scepter's click is latched only ON THE FIELD */
            if (state == "play" && e.onCanvas) { mouse.fresh = true; mouse.lpress = true; }
        }
        if (e.button == 2) { mouse.rdown = true; if (state == "play" && e.onCanvas) mouse.rpress = true; }
    }
    public static void onMouseUp(MouseEvt e) { if (e.button == 0) mouse.down = false; if (e.button == 2) mouse.rdown = false; }
    public static void onBlur()
    {
        foreach (var k in keys.Keys.ToList()) keys[k] = false;
        mouse.down = false; mouse.rdown = false; lootHeld = false;
    }
}
