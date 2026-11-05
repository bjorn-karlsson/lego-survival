using System.Reflection;
using Raylib_cs;

namespace Brickblade;

/* THE WINDOW AND ITS LOOP: what the browser did around the script.

   Each turn of the loop is one browser frame: input is routed into the page
   (hover, clicks, keys, the wheel, typed text), due timers run,
   requestAnimationFrame callbacks run (the game's frame(): update and render),
   MutationObserver callbacks run, and then the page is painted over the field.

   THE FIELD CAN BE DRAWN SMALLER THAN THE WINDOW. The render scale option picks
   the density the field is drawn at; below the screen's own, the field is drawn
   into an offscreen canvas and scaled up onto the window, while the menus, the
   sheets and the text keep the window's full resolution. AUTO starts at full and
   steps down when frames drop during play. */
public static class App
{
    /* the window has the keyboard (document.hasFocus) */
    public static bool Focused = true;
    /* an option the loop reads changed (render scale, vsync) */
    public static void SettingsChanged() { settingsDirty = true; }
    static bool settingsDirty;
    /* the density the field is drawn at: the screen's, times the render scale */
    public static double WorldDensity = 1;
    static double autoScale = 1;          /* AUTO's own step: 1, 0.75, 0.5 */
    static OffCanvas world;               /* the field, when it is drawn below the screen's density */
    static Ctx screen;                    /* the window itself, for the page and the scaled field */
    static bool quit, showFps;
    public static double PageMs, FrameMs, GlMs; public static int LastCalls, LastTris, LastVerts; public static string LastGfx = "";
    static string pendingShot;
    static double fpsShown, fpsAcc; static int fpsN;

    /* ---- the page: the game's own HTML, its stylesheet and its body ---- */
    static (string css, string body) Page()
    {
        string html = null;
        string disk = Program.Opt("--html");
        if (disk != null && File.Exists(disk)) html = File.ReadAllText(disk);
        if (html == null)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("page.html");
            if (s == null) throw new InvalidOperationException("the game's page is not in the build (page.html)");
            using var r = new StreamReader(s);
            html = r.ReadToEnd();
        }
        int s0 = html.IndexOf("<style>") + 7, s1 = html.IndexOf("</style>");
        int b0 = html.IndexOf("<body>") + 6, b1 = html.IndexOf("<script", b0);
        return (html.Substring(s0, s1 - s0), html.Substring(b0, b1 - b0));
    }

    /* ---- sizes: device pixels, the page's CSS pixels, the field's density ---- */
    static int RW => Math.Max(1, Raylib.GetRenderWidth());
    static int RH => Math.Max(1, Raylib.GetRenderHeight());
    static double DpiScale()
    {
        var v = Raylib.GetWindowScaleDPI();
        double k = v.X;
        return double.IsFinite(k) && k > 0.25 ? k : 1;
    }
    /* framebuffer pixels per window coordinate (1 unless the platform scales the window) */
    static double FbScale => (double)RW / Math.Max(1, Raylib.GetScreenWidth());
    static double RenderScaleK()
    {
        string o = Program.Opt("--render-scale") ?? G.OPT.renderScale;
        return o switch { "100" => 1, "75" => 0.75, "50" => 0.5, _ => autoScale };
    }
    static void ApplySize()
    {
        double sc = DpiScale();
        Gpu.ResizeScreen(RW, RH);
        Dom.Resize(RW / sc, RH / sc, sc);
        WorldDensity = sc * RenderScaleK();
        G.resizeNow();
        WorldTarget();
    }
    /* the field's canvas: none when it is drawn at the window's own size */
    static void WorldTarget()
    {
        int w = (int)Math.Round(G.cw * G.dpr), h = (int)Math.Round(G.ch * G.dpr);
        if (Math.Abs(w - RW) <= 1 && Math.Abs(h - RH) <= 1)
        {
            if (world != null) { world.Free(); world = null; }
            Ctx.MainTarget = null;
            return;
        }
        world ??= new OffCanvas();
        world.width = Math.Max(1, w); world.height = Math.Max(1, h);
        Ctx.MainTarget = world.T;
    }
    static void ApplyVsync()
    {
        if (G.OPT.vsync) { Raylib.SetWindowState(ConfigFlags.VSyncHint); Raylib.SetTargetFPS(0); }
        else { Raylib.ClearWindowState(ConfigFlags.VSyncHint); Raylib.SetTargetFPS(0); }
    }

    public static int Run()
    {
        /* --det: the same random numbers and the same clock every run (see G.FakeClock) */
        if (Program.Has("--det")) { Js.Seed(20261105); G.FakeClock = 0; }
        double t0Boot = G.perfNow();
        Raylib.SetTraceLogLevel(TraceLogLevel.Error);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        int iw = int.Parse(Program.Opt("--w") ?? "1280"), ih = int.Parse(Program.Opt("--h") ?? "800");
        Raylib.InitWindow(iw, ih, "BRICKBLADE — Brick-Built ARPG");
        Raylib.SetExitKey(KeyboardKey.Null);              /* Esc is the game's */
        if (Program.Opt("--w") == null && !RestoreWindow())
        {
            /* most of the screen, centred: the size a browser window would have */
            int mon = Raylib.GetCurrentMonitor();
            int mw = Raylib.GetMonitorWidth(mon), mh = Raylib.GetMonitorHeight(mon);
            if (mw > 0 && mh > 0)
            {
                int w = (int)(mw * 0.88), h = (int)(mh * 0.86);
                Raylib.SetWindowSize(w, h);
                var p = Raylib.GetMonitorPosition(mon);
                Raylib.SetWindowPosition((int)p.X + (mw - w) / 2, (int)p.Y + (mh - h) / 2);
            }
        }
        Raylib.SetWindowMinSize(640, 400);
        Gpu.Init(RW, RH);
        Fonts.Init();
        screen = new Ctx(null);

        if (Program.Opt("--make-icon") is string iconOut) { MakeIcon(iconOut); Raylib.CloseWindow(); return 0; }
        SetIcon();
        var (css, body) = Page();
        double sc = DpiScale();
        Dom.Init(css, body);
        Dom.Resize(RW / sc, RH / sc, sc);
        WorldDensity = sc;
        G.ctx = new Ctx(null);
        G.resize(RW / sc, RH / sc, WorldDensity);
        Console.Error.WriteLine($"window + fonts + page: {G.perfNow() - t0Boot:0} ms");
        Dom.FieldEl = Dom.document.getElementById("game");
        G.fieldDone = FieldDone;
        G.boot();
        Console.Error.WriteLine($"ready: {G.perfNow() - t0Boot:0} ms after start");
        ApplySize();
        ApplyVsync();
        settingsDirty = false;

        int lastW = RW, lastH = RH; double lastSc = sc;
        int shotAt = Program.Opt("--shot-frames") is string sf ? int.Parse(sf) : -1, frameNo = 0;
        string shotOut = Program.Opt("--shot") ?? "shot.png";
        double slowT = 0, slowFrames = 0, slowAcc = 0, graceT = 3;
        double prevNow = G.perfNow();
        AutoTest.Load(Program.Opt("--script"));
        GfxStats.On = AutoTest.On;

        while (!quit && !Raylib.WindowShouldClose())
        {
            /* ---- the window ---- */
            if (Raylib.IsWindowResized() || RW != lastW || RH != lastH || DpiScale() != lastSc)
            {
                lastW = RW; lastH = RH; lastSc = DpiScale();
                ApplySize();
                graceT = 2;
            }
            if (settingsDirty) { settingsDirty = false; ApplySize(); ApplyVsync(); graceT = 2; }
            bool foc = Raylib.IsWindowFocused();
            if (foc != Focused) { Focused = foc; if (!foc) { Dom.Blur(); } }
            bool minimised = Raylib.IsWindowMinimized();
            TrackWindow(minimised);

            /* ---- input, into the page and on to the game ---- */
            if (!minimised) Input();
            if (AutoTest.On && !AutoTest.Step(frameNo, fpsShown, f => pendingShot = f)) quit = true;

            if (!double.IsNaN(G.FakeClock)) G.FakeClock += 1000.0 / 60;
            /* ---- timers, then the frame ---- */
            G.RunTimers();
            double now = G.perfNow();
            double frameDt = (now - prevNow) / 1000; prevNow = now;
            Raylib.BeginDrawing();
            if (!minimised)
            {
                Gpu.ClearScreen(new Col(16, 21, 26, 1));
                G.RunFrameCallbacks();
                MutationObserver.Flush();
                long p0 = System.Diagnostics.Stopwatch.GetTimestamp();
                Present(now);
                PageMs += ((System.Diagnostics.Stopwatch.GetTimestamp() - p0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency - PageMs) * 0.05;
            }
            Gpu.EndFrame(); MaskPool.EndFrame();
            OffCanvas.SweepOwned();
            frameNo++;
            if (frameNo == shotAt) { Shot.Save(shotOut); quit = Program.Has("--quit-after-shot") || quit; }
            if (pendingShot != null) { Shot.Save(pendingShot); pendingShot = null; }
            Raylib.EndDrawing();
            LastCalls = Gpu.DrawCalls; LastTris = Gpu.Tris; LastVerts = Gpu.Verts; LastGfx = GfxStats.Line(); GfxStats.Reset();
            GlMs += (Gpu.GlTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency - GlMs) * 0.05;
            Gpu.DrawCalls = 0; Gpu.Tris = 0; Gpu.Verts = 0; Gpu.GlTicks = 0;
            if (minimised) Raylib.WaitTime(0.05);

            /* ---- FPS, and AUTO's step down ---- */
            fpsAcc += frameDt; fpsN++;
            FrameMs += (frameDt * 1000 - FrameMs) * 0.05;
            if (fpsAcc >= 0.5) { fpsShown = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; }
            if ((Program.Opt("--render-scale") ?? G.OPT.renderScale) == "auto" && autoScale > 0.5 && G.state == "play" && Focused && !minimised)
            {
                if (graceT > 0) graceT -= frameDt;
                else
                {
                    slowT += frameDt; slowFrames++; if (frameDt > 1.0 / 45) slowAcc++;
                    if (slowT >= 2.5)
                    {
                        /* most of 2.5 seconds under 45 frames a second: one step down */
                        if (slowAcc / Math.Max(1, slowFrames) > 0.5)
                        {
                            autoScale = autoScale > 0.75 ? 0.75 : 0.5;
                            ApplySize(); graceT = 3;
                        }
                        slowT = 0; slowFrames = 0; slowAcc = 0;
                    }
                }
            }
            else { slowT = 0; slowFrames = 0; slowAcc = 0; }
        }
        if (Program.Opt("--w") == null) SaveWindow();
        SoundOut.Close();
        Raylib.CloseWindow();
        return 0;
    }

    /* the world, drawn small, goes onto the window, and the game's ctx follows it
       there for the screen-space layer (render() calls this once the world is done) */
    static bool fieldOnScreen;
    static void FieldDone()
    {
        if (fieldOnScreen || world == null || Ctx.MainTarget == null) return;
        fieldOnScreen = true;
        Ctx.MainTarget = null;
        screen.Reset();
        screen.imageSmoothingEnabled = true;
        screen.drawImage(world, 0, 0, world.width, world.height, 0, 0, RW, RH);
        double k = RW / Math.Max(1, G.cw);
        G.ctx.setTransform(k, 0, 0, k, 0, 0);
    }
    /* the field (if render() did not get as far as putting it there), then the page over it */
    static void Present(double now)
    {
        if (!fieldOnScreen && world != null) FieldDone();
        Ctx.MainTarget = null;
        screen.Reset();
        Dom.Paint(screen, now / 1000);
        if (showFps)
        {
            screen.Reset();
            double k = Dom.Scale;
            screen.setTransform(k, 0, 0, k, 0, 0);
            screen.font = "600 13px Fredoka";
            string t = Math.Round(fpsShown) + " FPS · field " + Math.Round(RenderScaleK() * 100) + "% (" + Math.Round(G.cw * G.dpr) + "×" + Math.Round(G.ch * G.dpr) + ")" +
                " · game " + G.updMs.ToString("0.0") + " ms, draw " + G.renMs.ToString("0.0") + " ms, page " + PageMs.ToString("0.0") + " ms · " + LastCalls + " batches, " + ((LastVerts * 24 + LastTris * 6) / 1048576.0).ToString("0.0") + " MB · " + G.enemies.Count + " monsters";
            screen.fillStyle = "rgba(0,0,0,0.6)"; screen.fillRect(6, 6, screen.measureText(t).width + 14, 22);
            screen.fillStyle = "#ffcf00"; screen.textAlign = "left"; screen.textBaseline = "middle"; screen.fillText(t, 13, 17);
        }
        Gpu.Flush();
        /* the next frame's world goes back into its canvas */
        fieldOnScreen = false;
        Ctx.MainTarget = world?.T;
    }

    /* ---------------------------------------------------------------- the icon */
    /* a red 2x2 brick in the game's own stud style, drawn by the game's own
       drawing (--make-icon FILE writes it; assets/icon.png is that picture) */
    static void MakeIcon(string path)
    {
        const int S = 1024;               /* drawn at 4x; the icon file is made from it scaled down, smooth */
        var cv = new OffCanvas { width = S, height = S };
        var g = cv.getContext("2d"); g.Reset();
        Gpu.Clear(cv.T);
        g.scale(4, 4);
        Col red = G.PAL.red;
        g.fillStyle = G.sh(red, -0.35); G.roundRect(g, 18, 30, 220, 212, 34); g.fill();
        g.fillStyle = red; G.roundRect(g, 18, 18, 220, 206, 34); g.fill();
        g.strokeStyle = "rgba(0,0,0,0.22)"; g.lineWidth = 6; g.stroke();
        g.fillStyle = "rgba(255,255,255,0.10)"; G.roundRect(g, 30, 28, 196, 70, 26); g.fill();
        foreach (var (x, y) in new[] { (78.0, 78.0), (178.0, 78.0), (78.0, 172.0), (178.0, 172.0) })
        {
            g.save(); g.translate(x, y); g.scale(3.4, 3.4); G.drawStudG(g, 0, 0, 11, red); g.restore();
        }
        Gpu.Flush();
        Image img = Raylib.LoadImageFromTexture(cv.T.rt.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
    }
    static unsafe void SetIcon()
    {
        try
        {
            using var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png");
            if (st == null) return;
            var data = new byte[st.Length]; st.ReadExactly(data);
            fixed (byte* d = data)
            {
                var ext = System.Text.Encoding.ASCII.GetBytes(".png\0");
                fixed (byte* e = ext)
                {
                    Image img = Raylib.LoadImageFromMemory((sbyte*)e, d, data.Length);
                    Raylib.SetWindowIcon(img);
                    Raylib.UnloadImage(img);
                }
            }
        }
        catch { }
    }

    /* ---------------------------------------------------------------- input */
    static readonly (KeyboardKey k, string code, string key, string shifted)[] KEYS = BuildKeys();
    static (KeyboardKey, string, string, string)[] BuildKeys()
    {
        var l = new List<(KeyboardKey, string, string, string)>();
        for (int i = 0; i < 26; i++) { char c = (char)('a' + i); l.Add((KeyboardKey.A + i, "Key" + char.ToUpperInvariant(c), c.ToString(), char.ToUpperInvariant(c).ToString())); }
        string sh = ")!@#$%^&*(";
        for (int i = 0; i < 10; i++) l.Add((KeyboardKey.Zero + i, "Digit" + i, i.ToString(), sh[i].ToString()));
        for (int i = 0; i < 12; i++) l.Add((KeyboardKey.F1 + i, "F" + (i + 1), "F" + (i + 1), "F" + (i + 1)));
        for (int i = 0; i < 10; i++) l.Add((KeyboardKey.Kp0 + i, "Numpad" + i, i.ToString(), i.ToString()));
        l.AddRange(new (KeyboardKey, string, string, string)[]
        {
            (KeyboardKey.Space, "Space", " ", " "), (KeyboardKey.Enter, "Enter", "Enter", "Enter"), (KeyboardKey.KpEnter, "NumpadEnter", "Enter", "Enter"),
            (KeyboardKey.Escape, "Escape", "Escape", "Escape"), (KeyboardKey.Tab, "Tab", "Tab", "Tab"), (KeyboardKey.Backspace, "Backspace", "Backspace", "Backspace"),
            (KeyboardKey.Delete, "Delete", "Delete", "Delete"), (KeyboardKey.Insert, "Insert", "Insert", "Insert"), (KeyboardKey.Home, "Home", "Home", "Home"),
            (KeyboardKey.End, "End", "End", "End"), (KeyboardKey.PageUp, "PageUp", "PageUp", "PageUp"), (KeyboardKey.PageDown, "PageDown", "PageDown", "PageDown"),
            (KeyboardKey.Up, "ArrowUp", "ArrowUp", "ArrowUp"), (KeyboardKey.Down, "ArrowDown", "ArrowDown", "ArrowDown"),
            (KeyboardKey.Left, "ArrowLeft", "ArrowLeft", "ArrowLeft"), (KeyboardKey.Right, "ArrowRight", "ArrowRight", "ArrowRight"),
            (KeyboardKey.LeftShift, "ShiftLeft", "Shift", "Shift"), (KeyboardKey.RightShift, "ShiftRight", "Shift", "Shift"),
            (KeyboardKey.LeftControl, "ControlLeft", "Control", "Control"), (KeyboardKey.RightControl, "ControlRight", "Control", "Control"),
            (KeyboardKey.LeftAlt, "AltLeft", "Alt", "Alt"), (KeyboardKey.RightAlt, "AltRight", "Alt", "Alt"),
            (KeyboardKey.LeftSuper, "MetaLeft", "Meta", "Meta"), (KeyboardKey.RightSuper, "MetaRight", "Meta", "Meta"),
            (KeyboardKey.CapsLock, "CapsLock", "CapsLock", "CapsLock"),
            (KeyboardKey.Minus, "Minus", "-", "_"), (KeyboardKey.Equal, "Equal", "=", "+"),
            (KeyboardKey.LeftBracket, "BracketLeft", "[", "{"), (KeyboardKey.RightBracket, "BracketRight", "]", "}"),
            (KeyboardKey.Backslash, "Backslash", "\\", "|"), (KeyboardKey.Semicolon, "Semicolon", ";", ":"),
            (KeyboardKey.Apostrophe, "Quote", "'", "\""), (KeyboardKey.Grave, "Backquote", "`", "~"),
            (KeyboardKey.Comma, "Comma", ",", "<"), (KeyboardKey.Period, "Period", ".", ">"), (KeyboardKey.Slash, "Slash", "/", "?"),
            (KeyboardKey.KpDecimal, "NumpadDecimal", ".", "."), (KeyboardKey.KpAdd, "NumpadAdd", "+", "+"),
            (KeyboardKey.KpSubtract, "NumpadSubtract", "-", "-"), (KeyboardKey.KpMultiply, "NumpadMultiply", "*", "*"),
            (KeyboardKey.KpDivide, "NumpadDivide", "/", "/"),
        });
        return l.ToArray();
    }
    static readonly (MouseButton rb, int dom)[] BUTTONS = { (MouseButton.Left, 0), (MouseButton.Middle, 1), (MouseButton.Right, 2) };
    static double lastMx = double.NaN, lastMy = double.NaN;
    static void Input()
    {
        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        bool alt = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
        bool meta = Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper);
        Dom.Shift = shift; Dom.Ctrl = ctrl; Dom.Alt = alt;

        /* the pointer, in the page's CSS pixels */
        var mp = Raylib.GetMousePosition();
        double k = FbScale / Dom.Scale;
        double mx = mp.X * k, my = mp.Y * k;
        int buttons = (Raylib.IsMouseButtonDown(MouseButton.Left) ? 1 : 0) | (Raylib.IsMouseButtonDown(MouseButton.Right) ? 2 : 0) | (Raylib.IsMouseButtonDown(MouseButton.Middle) ? 4 : 0);
        if (mx != lastMx || my != lastMy) { lastMx = mx; lastMy = my; Dom.PointerMove(mx, my, buttons); }
        else Dom.PointerRefresh();
        foreach (var (rb, dom) in BUTTONS)
        {
            if (Raylib.IsMouseButtonPressed(rb)) Dom.PointerDown(dom);
            if (Raylib.IsMouseButtonReleased(rb)) Dom.PointerUp(dom);
        }
        var wh = Raylib.GetMouseWheelMoveV();
        if (wh.X != 0 || wh.Y != 0) Dom.Wheel(-wh.X * 100, -wh.Y * 100);

        /* keys, by the code a browser gives them */
        foreach (var (key, code, plain, shifted) in KEYS)
        {
            bool pressed = Raylib.IsKeyPressed(key), rep = !pressed && Raylib.IsKeyPressedRepeat(key);
            if (pressed || rep)
            {
                /* the native window's own: F11 (or Alt+Enter) for fullscreen, F3 for the frame rate */
                if (pressed && (code == "F11" || (code == "Enter" && alt))) { ToggleFullscreen(); continue; }
                if (pressed && code == "F3") { showFps = !showFps; GfxStats.On = showFps || AutoTest.On; continue; }
                var e = new KeyEvt { code = code, key = shift ? shifted : plain, repeat = rep, shiftKey = shift, ctrlKey = ctrl, metaKey = meta };
                Dom.KeyDown(e, G.onKeyDown);
            }
            if (Raylib.IsKeyReleased(key))
            {
                var e = new KeyEvt { code = code, key = shift ? shifted : plain, shiftKey = shift, ctrlKey = ctrl, metaKey = meta };
                Dom.KeyUp(e, G.onKeyUp);
            }
        }
        /* and the text typed into a focused field */
        for (int cp = Raylib.GetCharPressed(); cp != 0; cp = Raylib.GetCharPressed()) Dom.TypeChar(cp);

        /* the CSS cursor under the pointer */
        Cursor(Dom.Cursor());
    }
    static string curNow = "";
    static void Cursor(string c)
    {
        if (c == curNow) return;
        curNow = c;
        if (c == "none") { Raylib.HideCursor(); return; }
        if (Raylib.IsCursorHidden()) Raylib.ShowCursor();
        Raylib.SetMouseCursor(c switch
        {
            "pointer" => MouseCursor.PointingHand,
            "text" => MouseCursor.IBeam,
            "crosshair" => MouseCursor.Crosshair,
            "move" or "grab" or "grabbing" or "all-scroll" => MouseCursor.ResizeAll,
            "not-allowed" or "no-drop" => MouseCursor.NotAllowed,
            "ew-resize" or "col-resize" => MouseCursor.ResizeEw,
            "ns-resize" or "row-resize" => MouseCursor.ResizeNs,
            _ => MouseCursor.Default,
        });
    }
    static void ToggleFullscreen()
    {
        Raylib.ToggleBorderlessWindowed();
    }

    /* ---- THE WINDOW AS YOU LEFT IT: size, place, maximised, fullscreen ---- */
    const string WIN_KEY = "native_window";
    static int winX, winY, winW, winH; static bool winKnown;
    static void TrackWindow(bool minimised)
    {
        if (minimised || Raylib.IsWindowMaximized() || Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode)) return;
        var p = Raylib.GetWindowPosition();
        winX = (int)p.X; winY = (int)p.Y; winW = Raylib.GetScreenWidth(); winH = Raylib.GetScreenHeight(); winKnown = true;
    }
    static void SaveWindow()
    {
        try
        {
            if (!winKnown) return;
            bool max = Raylib.IsWindowMaximized(), full = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
            Store.setItem(WIN_KEY, string.Join(",", winX, winY, winW, winH, max ? 1 : 0, full ? 1 : 0));
        }
        catch { }
    }
    static bool RestoreWindow()
    {
        try
        {
            var v = Store.getItem(WIN_KEY)?.Split(',');
            if (v == null || v.Length < 6) return false;
            int x = int.Parse(v[0]), y = int.Parse(v[1]), w = int.Parse(v[2]), h = int.Parse(v[3]);
            if (w < 640 || h < 400) return false;
            /* only onto a screen that is still there: its middle must be on some monitor */
            double cx = x + w / 2.0, cy = y + h / 2.0; bool onScreen = false;
            for (int m = 0; m < Raylib.GetMonitorCount(); m++)
            {
                var mp = Raylib.GetMonitorPosition(m); int mw = Raylib.GetMonitorWidth(m), mh = Raylib.GetMonitorHeight(m);
                if (cx >= mp.X && cx < mp.X + mw && cy >= mp.Y && cy < mp.Y + mh) { onScreen = true; w = Math.Min(w, mw); h = Math.Min(h, mh); break; }
            }
            if (!onScreen) return false;
            Raylib.SetWindowSize(w, h);
            Raylib.SetWindowPosition(x, y);
            winX = x; winY = y; winW = w; winH = h; winKnown = true;
            if (v[4] == "1") Raylib.MaximizeWindow();
            if (v[5] == "1") Raylib.ToggleBorderlessWindowed();
            return true;
        }
        catch { return false; }
    }

    /* ------------------------------------------------- confirm(): a yes/no box */
    public static bool Confirm(string msg)
    {
        if (!Raylib.IsWindowReady()) return true;
        bool? answer = null;
        while (answer == null)
        {
            if (Raylib.WindowShouldClose()) { quit = true; answer = false; break; }
            double k = Dom.Scale, W = RW / k, H = RH / k;
            var mp = Raylib.GetMousePosition(); double s = FbScale / k; double mx = mp.X * s, my = mp.Y * s;
            double bw = Math.Min(480, W - 40), bx = (W - bw) / 2;
            screen.font = "500 16px Fredoka";
            var lines = Wrap(msg, bw - 48);
            double bh = 74 + lines.Count * 22 + 30, by = (H - bh) / 2;
            double okX = bx + bw - 24 - 100, cnX = okX - 12 - 100, btY = by + bh - 24 - 36;
            bool inOk = mx >= okX && mx <= okX + 100 && my >= btY && my <= btY + 36;
            bool inCn = mx >= cnX && mx <= cnX + 100 && my >= btY && my <= btY + 36;
            if (Raylib.IsMouseButtonReleased(MouseButton.Left)) { if (inOk) answer = true; else if (inCn) answer = false; }
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) answer = true;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) answer = false;
            Cursor(inOk || inCn ? "pointer" : "default");

            Raylib.BeginDrawing();
            Gpu.ClearScreen(new Col(16, 21, 26, 1));
            try { G.render(); if (G.state == "title") G.renderTitle(); } catch { }
            Present(G.perfNow());
            Ctx.MainTarget = null;
            screen.Reset();
            screen.setTransform(k, 0, 0, k, 0, 0);
            screen.fillStyle = "rgba(0,0,0,0.55)"; screen.fillRect(0, 0, W, H);
            screen.fillStyle = "#1b2a34"; G.roundRect(screen, bx, by, bw, bh, 12); screen.fill();
            screen.strokeStyle = "#ffb900"; screen.lineWidth = 3; screen.stroke();
            screen.font = "500 16px Fredoka"; screen.fillStyle = "#eef0ee"; screen.textAlign = "left"; screen.textBaseline = "top";
            for (int i = 0; i < lines.Count; i++) screen.fillText(lines[i], bx + 24, by + 26 + i * 22);
            void Btn(double x, string t, bool hot, bool main)
            {
                screen.fillStyle = main ? (hot ? "#ffcf00" : "#ffb900") : (hot ? "#3a4b57" : "#2b3a45");
                G.roundRect(screen, x, btY, 100, 36, 8); screen.fill();
                screen.font = "600 15px Fredoka"; screen.fillStyle = main ? "#1b2a34" : "#eef0ee";
                screen.textAlign = "center"; screen.textBaseline = "middle"; screen.fillText(t, x + 50, btY + 18);
            }
            Btn(cnX, "Cancel", inCn, false);
            Btn(okX, "OK", inOk, true);
            Gpu.Flush();
            Gpu.EndFrame(); MaskPool.EndFrame();
            Raylib.EndDrawing();
            Ctx.MainTarget = world?.T;
        }
        Ctx.MainTarget = world?.T;
        curNow = "";
        return answer == true;
    }
    static List<string> Wrap(string msg, double maxW)
    {
        var lines = new List<string>();
        foreach (var para in (msg ?? "").Split('\n'))
        {
            string line = "";
            foreach (var w in para.Split(' '))
            {
                string t = line == "" ? w : line + " " + w;
                if (line != "" && screen.measureText(t).width > maxW) { lines.Add(line); line = w; }
                else line = t;
            }
            lines.Add(line);
        }
        return lines;
    }
}
