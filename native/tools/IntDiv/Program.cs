using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var root = args.Length > 0 ? args[0] : "src";
var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
var opts = new CSharpParseOptions(LanguageVersion.Latest);
var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), opts, f)).ToList();
/* the project's global usings and the Math alias, as the build has them */
var proj = File.ReadAllText(Directory.GetFiles(".", "*.csproj").First());
var glob = new List<string>();
foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(proj, "<Using Include=\"([^\"]+)\"(?: Alias=\"([^\"]+)\")?(?: Static=\"true\")?\\s*/>"))
    glob.Add(m.Groups[2].Success ? $"global using {m.Groups[2].Value} = {m.Groups[1].Value};" : $"global using {m.Groups[1].Value};");
foreach (var u in new[] { "System", "System.Collections.Generic", "System.Linq", "System.IO", "System.Threading.Tasks" }) glob.Add($"global using {u};");
trees.Add(CSharpSyntaxTree.ParseText(string.Join("\n", glob.Distinct()), opts, "globals.cs"));
var refs = new List<MetadataReference>();
var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
foreach (var p in tpa) refs.Add(MetadataReference.CreateFromFile(p));
foreach (var dll in Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages"), "*.dll", SearchOption.AllDirectories)
    .Where(d => d.Contains("/lib/net") && (d.Contains("raylib") || d.Contains("libtess"))))
    refs.Add(MetadataReference.CreateFromFile(dll));
var comp = CSharpCompilation.Create("Check", trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
static bool Integral(ITypeSymbol t) => t != null && t.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
    or SpecialType.System_Byte or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_SByte or SpecialType.System_UInt16;
int n = 0;
foreach (var tree in trees)
{
    if (tree.FilePath == "globals.cs") continue;
    var model = comp.GetSemanticModel(tree);
    foreach (var node in tree.GetRoot().DescendantNodes())
    {
        ExpressionSyntax a = null, b = null;
        if (node is BinaryExpressionSyntax be && be.IsKind(SyntaxKind.DivideExpression)) { a = be.Left; b = be.Right; }
        else if (node is AssignmentExpressionSyntax ae && ae.IsKind(SyntaxKind.DivideAssignmentExpression)) { a = ae.Left; b = ae.Right; }
        else continue;
        var ta = model.GetTypeInfo(a).Type; var tb = model.GetTypeInfo(b).Type;
        if (!Integral(ta) || !Integral(tb)) continue;
        /* two constants are the author's own arithmetic; skip them */
        if (model.GetConstantValue(a).HasValue && model.GetConstantValue(b).HasValue) continue;
        var pos = tree.GetLineSpan(node.Span).StartLinePosition;
        var rel = Path.GetRelativePath(".", tree.FilePath);
        Console.WriteLine($"{rel}:{pos.Line + 1}: {node.ToString().Replace("\n", " ")}");
        n++;
    }
}
Console.Error.WriteLine($"{n} integer divisions");
