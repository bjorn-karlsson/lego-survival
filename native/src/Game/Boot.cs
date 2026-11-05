namespace Brickblade;

/* THE JS'S TOP-LEVEL STATEMENTS. A browser runs every statement of the script
   at load, in order; the ones that DO something (load a save, size the canvas,
   wire a button) are collected here, in the order the JS has them, and run
   once from App before the first frame. */
public static partial class G
{
    /* each start-up step, timed: a slow one is named in the log (all with --boot-times) */
    static void BootStep(string name, Action step)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        step();
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (ms > 30 || Program.Has("--boot-times")) Console.Error.WriteLine($"boot: {name} {ms:0} ms");
    }
    public static void boot()
    {
        /* JS 3420-3426: the window's resize, the title's fit and its watcher */
        BootStep("TitleFitWire", TitleFitWire);
        /* JS 3974-4056: keys reach onKeyDown through the window (App), the mouse here */
        BootStep("InputWire", InputWire);
        /* JS 11357 */
        BootStep("ltreeLoad", () => ltreeLoad());
        /* JS 13010-13030: the generated conversion and weapon writers */
        BootStep("MetaGenStats", () => MetaGenStats());
        /* JS 13440-13491, 13207, 13895: the generated groups, the doors, the legends */
        BootStep("MetaGenTemplates", () => MetaGenTemplates());
        /* JS 13958: (function buildTree(){ ... })() */
        BootStep("buildTree", () => buildTree());
        /* JS 15718: TREE_VIEW, from the built map; 15733: the country washes remembered */
        BootStep("TreeViewInit", () => TreeViewInit()); BootStep("TreeSectorsLoad", () => TreeSectorsLoad());
        /* JS 16965: the title's long stat list remembered */
        BootStep("MenuStatsLoad", () => MenuStatsLoad());
        /* JS 18980: every stat a brick can move */
        BootStep("StatProbesInit", () => StatProbesInit());
        /* JS 19740: the sheet's simple / nerd view remembered */
        BootStep("SheetNerdLoad", () => SheetNerdLoad());
        /* JS 21303-21304: the reward screen's two buttons */
        BootStep("RewardButtonsWire", () => RewardButtonsWire());
        /* JS 31848-31873: tcv, tctx and (function buildTitle(){ ... })() */
        BootStep("buildTitle", () => buildTitle());
        /* JS 32243-32328: the title, pause, death and options buttons; audioLoad, optLoad, applyTextSize */
        BootStep("GameFlowWire", () => GameFlowWire());
        /* JS 32431-32443: the saves, the demo run, the title's rows, the first frame, a run link */
        BootStep("GameBoot", () => GameBoot());
    }
}
