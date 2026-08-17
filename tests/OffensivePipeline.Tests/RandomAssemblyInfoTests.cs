using System.Text.RegularExpressions;
using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The AssemblyInfo rewriter. Another pure text transform, and the one whose output an analyst is
/// most likely to look at, so the values it stamps matter.
/// </summary>
public partial class RandomAssemblyInfoTests
{
    private const string OriginalAssemblyInfo = """
        using System.Reflection;
        using System.Runtime.InteropServices;

        [assembly: AssemblyTitle("Seatbelt")]
        [assembly: AssemblyDescription("A security survey tool")]
        [assembly: AssemblyConfiguration("Debug")]
        [assembly: AssemblyCompany("GhostPack")]
        [assembly: AssemblyProduct("Seatbelt")]
        [assembly: AssemblyCopyright("Copyright ©  2018")]
        [assembly: AssemblyTrademark("GhostPack")]
        [assembly: AssemblyCulture("en-GB")]
        [assembly: ComVisible(false)]
        [assembly: AssemblyVersion("1.2.3.4")]
        """;

    [GeneratedRegex(@"\[assembly: AssemblyTitle\(""(?<value>[^""]*)""\)]")]
    private static partial Regex Title();

    [GeneratedRegex(@"\[assembly: AssemblyProduct\(""(?<value>[^""]*)""\)]")]
    private static partial Regex Product();

    [GeneratedRegex(@"\[assembly: AssemblyCopyright\(""Copyright ©  (?<year>\d{4})""\)]")]
    private static partial Regex Copyright();

    private static string Rewrite(TempWorkspace workspace, out ModuleResult result)
    {
        string file = workspace.File("Git/MyTool/Properties/AssemblyInfo.cs", OriginalAssemblyInfo);
        var module = new RandomAssemblyInfo(new RecordingConsoleUi(), TestFactory.Log<RandomAssemblyInfo>());

        result = module.Run(new ModuleContext(
            TestFactory.Tool(name: "MyTool"),
            workspace.Dir("Output"),
            workspace.Paths.GitToolsPath));

        return File.ReadAllText(file);
    }

    [Fact]
    public void Every_Identifying_Attribute_Is_Overwritten()
    {
        using var workspace = new TempWorkspace();
        string rewritten = Rewrite(workspace, out ModuleResult result);

        Assert.True(result.Status);
        Assert.DoesNotContain("Seatbelt", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("GhostPack", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("A security survey tool", rewritten, StringComparison.Ordinal);
        Assert.Contains(@"[assembly: AssemblyDescription("""")]", rewritten, StringComparison.Ordinal);
        Assert.Contains(@"[assembly: AssemblyConfiguration("""")]", rewritten, StringComparison.Ordinal);
        Assert.Contains(@"[assembly: AssemblyCompany("""")]", rewritten, StringComparison.Ordinal);
        Assert.Contains(@"[assembly: AssemblyTrademark("""")]", rewritten, StringComparison.Ordinal);
        Assert.Contains(@"[assembly: AssemblyCulture("""")]", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Title_And_Product_Get_The_Same_Random_Name()
    {
        using var workspace = new TempWorkspace();
        string rewritten = Rewrite(workspace, out _);

        string title = Title().Match(rewritten).Groups["value"].Value;
        string product = Product().Match(rewritten).Groups["value"].Value;

        Assert.NotEmpty(title);
        Assert.Equal(title, product);
        Assert.NotEqual("Seatbelt", title);
    }

    /// <summary>
    /// The year used to be hard-coded to 2018-2021, which on a binary built today is exactly the
    /// kind of anomaly the module exists to avoid.
    /// </summary>
    [Fact]
    public void The_Copyright_Year_Is_Recent()
    {
        using var workspace = new TempWorkspace();
        string rewritten = Rewrite(workspace, out _);

        Match match = Copyright().Match(rewritten);
        Assert.True(match.Success, "the copyright attribute should have been rewritten");

        int year = int.Parse(match.Groups["year"].Value, System.Globalization.CultureInfo.InvariantCulture);
        int currentYear = DateTime.UtcNow.Year;

        Assert.InRange(year, currentYear - 3, currentYear);
    }

    [Fact]
    public void Attributes_Outside_The_Rule_Set_Are_Left_Alone()
    {
        using var workspace = new TempWorkspace();
        string rewritten = Rewrite(workspace, out _);

        Assert.Contains(@"[assembly: AssemblyVersion(""1.2.3.4"")]", rewritten, StringComparison.Ordinal);
        Assert.Contains("[assembly: ComVisible(false)]", rewritten, StringComparison.Ordinal);
        Assert.Contains("using System.Reflection;", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_AssemblyInfo_In_The_Checkout_Is_Rewritten()
    {
        using var workspace = new TempWorkspace();
        string first = workspace.File("Git/MyTool/Properties/AssemblyInfo.cs", OriginalAssemblyInfo);
        string second = workspace.File("Git/MyTool/sub/Properties/AssemblyInfo.cs", OriginalAssemblyInfo);

        var module = new RandomAssemblyInfo(new RecordingConsoleUi(), TestFactory.Log<RandomAssemblyInfo>());
        module.Run(new ModuleContext(
            TestFactory.Tool(name: "MyTool"), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.DoesNotContain("Seatbelt", File.ReadAllText(first), StringComparison.Ordinal);
        Assert.DoesNotContain("Seatbelt", File.ReadAllText(second), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Checkout_With_No_AssemblyInfo_Is_Not_An_Error()
    {
        using var workspace = new TempWorkspace();
        workspace.Dir("Git", "MyTool");

        var module = new RandomAssemblyInfo(new RecordingConsoleUi(), TestFactory.Log<RandomAssemblyInfo>());
        ModuleResult result = module.Run(new ModuleContext(
            TestFactory.Tool(name: "MyTool"), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.True(result.Status);
    }
}
