namespace Brickblade;

/* The game's window and main loop (filled in as the port lands). */
public static class App
{
    /* the window has the keyboard (document.hasFocus) */
    public static bool Focused = true;
    /* an option the loop reads changed (render scale, vsync, fps cap) */
    public static void SettingsChanged() { settingsDirty = true; }
    static bool settingsDirty;
    /* the density the field is drawn at: the screen's, times the render scale */
    public static double WorldDensity = 1;
    /* confirm(): a blocking yes/no box over the last frame */
    public static bool Confirm(string msg) => true;
    public static int Run()
    {
        return 0;
    }
}
