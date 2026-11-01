using Raylib_cs;

namespace Brickblade;

/* a screenshot of the window, to any path */
public static unsafe class Shot
{
    public static void Save(string path)
    {
        Gpu.Flush();
        Image img = Raylib.LoadImageFromScreen();
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        var bytes = System.Text.Encoding.UTF8.GetBytes(full + "\0");
        fixed (byte* b = bytes) Raylib.ExportImage(img, (sbyte*)b);
        Raylib.UnloadImage(img);
    }
}
