// What drawFigure (DRAW: MINIFIGURE, JS 27348) is handed: one shape for the hero,
// a monster, a minion, the title's figure. A field the JS leaves undefined is NaN
// (whirlA, bladeAng, slam), false or null.
namespace Brickblade;

public sealed class Fig
{
    public double x, y, z, s = 1, walk, phase, flash, blink, bowPull, wScale;
    public double whirlA = double.NaN, bladeAng = double.NaN, slam = double.NaN;
    public Pt face;
    public bool moving, angry, armsUp, bigImp, castGlow, crown, elite, fat, hero, horns, lava, lord, magic,
        noHelm, staffUpright, wings, zombieArms, skip;
    public FigCols colors;
    public string style, weapon, orbCol, hoodCol;
}
