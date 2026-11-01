using System.Globalization;
using Raylib_cs;

namespace Brickblade;

public static class Program
{
    public static string[] Args = Array.Empty<string>();
    public static bool Has(string flag) => Args.Contains(flag);
    public static string Opt(string name) { int i = Array.IndexOf(Args, name); return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null; }

    [STAThread]
    public static int Main(string[] args)
    {
        Args = args;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (Has("--shimtest")) return ShimTest.Run();
        return App.Run();
    }
}
