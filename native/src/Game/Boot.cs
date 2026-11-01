namespace Brickblade;

/* THE JS'S TOP-LEVEL STATEMENTS. A browser runs every statement of the script
   at load, in order; the ones that DO something (load a save, size the canvas,
   wire a button) are collected here, in the order the JS has them, and run
   once from App before the first frame. */
public static partial class G
{
    public static void boot()
    {
        /* JS 11357 */ ltreeLoad();
        /* JS 13010-13030: the generated conversion and weapon writers */ MetaGenStats();
        /* JS 13440-13491, 13207, 13895: the generated groups, the doors, the legends */ MetaGenTemplates();
        /* JS 13958: (function buildTree(){ ... })() */ buildTree();
        /* the rest are added as the sections that hold them are ported (GAME FLOW, MAIN LOOP) */
    }
}
