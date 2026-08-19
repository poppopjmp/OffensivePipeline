using OffensivePipeline.Config;
using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The build module, exercised through the process-runner seam so nothing is executed.
/// </summary>
/// <remarks>
/// This is the only place the rendered <c>buildSolution.bat</c> is verified at all: on a real run it
/// is handed straight to <c>cmd.exe</c> on a Windows box with Build Tools installed, so a broken
/// placeholder substitution would otherwise surface as an opaque MSBuild failure in the field.
/// </remarks>
public class BuildCsharpTests
{
    private const string LegacyProject = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <ItemGroup>
            <Reference Include="Vendor">
              <HintPath>Vendor.dll</HintPath>
            </Reference>
            <Reference Include="VendorTwo">
              <HintPath>..\VendorTwo.dll</HintPath>
            </Reference>
          </ItemGroup>
        </Project>
        """;

    private static PipelineOptions Options(string buildTools) => new()
    {
        NugetUrl = "https://example.invalid/nuget.exe",
        NugetSha256 = "0f".PadRight(64, '0'),
        BuildCsharpOptions = "/p:Configuration=Release /p:Platform=\"Any CPU\"",
        BuildCSharpTools = buildTools,
        ConfuserExUrl = "https://example.invalid/confuser.zip",
    };

    private static BuildCsharp Module(
        TempWorkspace workspace,
        RecordingConsoleUi ui,
        RecordingProcessRunner runner,
        FakeResourceDownloader downloader,
        string buildTools) =>
        new(ui, TestFactory.Log<BuildCsharp>(), workspace.Paths, Options(buildTools), runner, downloader);

    private static ModuleContext Arrange(TempWorkspace workspace, out string outputPath)
    {
        workspace.CopyShippedResources();
        workspace.File("Git/MyTool/MyTool.sln", TestFactory.Solution("MyTool", @"MyTool.csproj"));
        workspace.File("Git/MyTool/MyTool.csproj", LegacyProject);
        workspace.File("Git/MyTool/Vendor.dll", "vendor bytes");
        workspace.File("Git/MyTool/VendorTwo.dll", "vendor two bytes");

        outputPath = workspace.Dir("Output", "MyTool_abc123");
        return new ModuleContext(
            TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/MyTool.sln"),
            outputPath,
            workspace.Paths.GitToolsPath);
    }

    [Fact]
    public void Restores_Packages_Then_Runs_The_Generated_Script()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string outputPath);
        var runner = new RecordingProcessRunner();

        ModuleResult result = Module(
            workspace, new RecordingConsoleUi(), runner, new FakeResourceDownloader(), "/tools/VsDevCmd.bat")
            .Run(context);

        Assert.True(result.Status);
        Assert.Equal(outputPath, result.OutputPath);
        Assert.Equal(2, runner.Commands.Count);

        string solutionPath = Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "MyTool.sln");
        Assert.Equal($"{workspace.Paths.NugetPath} restore {solutionPath}", runner.Commands[0]);
        Assert.Equal(
            Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "buildSolution.bat"),
            runner.Commands[1]);
    }

    /// <summary>
    /// Every placeholder in <c>Resources/template_build.bat</c> must be substituted. An unreplaced
    /// token becomes a literal <c>{{...}}</c> on an MSBuild command line.
    /// </summary>
    [Fact]
    public void The_Generated_Script_Has_Every_Placeholder_Substituted()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string outputPath);

        Module(
            workspace,
            new RecordingConsoleUi(),
            new RecordingProcessRunner(),
            new FakeResourceDownloader(),
            @"C:\BuildTools\VsDevCmd.bat")
            .Run(context);

        string script = File.ReadAllText(
            Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "buildSolution.bat"));

        Assert.DoesNotContain("{{", script, StringComparison.Ordinal);
        Assert.Contains(@"call ""C:\BuildTools\VsDevCmd.bat""", script, StringComparison.Ordinal);
        Assert.Contains(
            $@"msbuild.exe ""{Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "MyTool.sln")}""",
            script,
            StringComparison.Ordinal);
        Assert.Contains(@"/p:Configuration=Release /p:Platform=""Any CPU""", script, StringComparison.Ordinal);
        Assert.Contains($@"/p:OutputPath=""{outputPath}""", script, StringComparison.Ordinal);
        Assert.Contains("/p:AssemblyName=", script, StringComparison.Ordinal);
        Assert.Contains("/p:DebugSymbols=false", script, StringComparison.Ordinal);
    }

    /// <summary>The built assembly must not be named after the tool it came from.</summary>
    [Fact]
    public void The_Output_Assembly_Name_Is_Randomised()
    {
        using var workspace = new TempWorkspace();

        string ReadAssemblyName()
        {
            ModuleContext context = Arrange(workspace, out _);
            Module(workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader(), "/vs.bat")
                .Run(context);
            string script = File.ReadAllText(
                Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "buildSolution.bat"));
            return script[(script.IndexOf("/p:AssemblyName=", StringComparison.Ordinal) + 17)..].TrimEnd('"', '\r', '\n');
        }

        string first = ReadAssemblyName();
        string second = ReadAssemblyName();

        Assert.NotEmpty(first);
        Assert.DoesNotContain("MyTool", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// A failed restore used to be invisible: <c>ExecuteCommand</c> always returned true, so the
    /// build ran against unrestored packages and the summary still said OK.
    /// </summary>
    [Fact]
    public void A_Failed_Restore_Fails_The_Module_And_Skips_The_Build()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out _);
        var runner = new RecordingProcessRunner(1);
        var ui = new RecordingConsoleUi();

        ModuleResult result = Module(workspace, ui, runner, new FakeResourceDownloader(), "/vs.bat").Run(context);

        Assert.False(result.Status);
        Assert.Single(runner.Commands);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "buildSolution.bat")));
        Assert.NotEmpty(ui.TextOf(UiChannel.Failure));
    }

    [Fact]
    public void A_Failed_Build_Fails_The_Module()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out _);
        var runner = new RecordingProcessRunner(0, 1);
        var ui = new RecordingConsoleUi();

        ModuleResult result = Module(workspace, ui, runner, new FakeResourceDownloader(), "/vs.bat").Run(context);

        Assert.False(result.Status);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("msbuild.exe", Assert.Single(ui.TextOf(UiChannel.Failure)), StringComparison.Ordinal);
        Assert.DoesNotContain("No errors!", ui.AllText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Referenced third-party assemblies have to land next to the built executable, or ConfuserEx
    /// obfuscates something that cannot then be loaded.
    /// </summary>
    [Fact]
    public void Project_Hint_Path_References_Are_Copied_Into_The_Output_Folder()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string outputPath);

        Module(workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader(), "/vs.bat")
            .Run(context);

        Assert.Equal("vendor bytes", File.ReadAllText(Path.Combine(outputPath, "Vendor.dll")));

        // A "..\" prefix is stripped and the reference resolved against the solution folder.
        Assert.Equal("vendor two bytes", File.ReadAllText(Path.Combine(outputPath, "VendorTwo.dll")));
    }

    [Fact]
    public void CheckStart_Downloads_Nuget_With_The_Configured_Url_And_Hash()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        string buildTools = workspace.File("VsDevCmd.bat", "@echo off");
        var downloader = new FakeResourceDownloader(succeed: true, writeContent: "nuget");

        ModuleResult result = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), downloader, buildTools)
            .CheckStart(new ModuleContext(TestFactory.Tool(), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.True(result.Status);
        (string url, string name, string path, string? sha) = Assert.Single(downloader.Requests);
        Assert.Equal("https://example.invalid/nuget.exe", url);
        Assert.Equal("nuget.exe", name);
        Assert.Equal(workspace.Paths.ResourcesPath, path);
        Assert.Equal("0f".PadRight(64, '0'), sha);
    }

    [Fact]
    public void CheckStart_Does_Not_Redownload_An_Existing_Nuget()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        File.WriteAllText(workspace.Paths.NugetPath, "already here");
        string buildTools = workspace.File("VsDevCmd.bat", "@echo off");
        var downloader = new FakeResourceDownloader();

        ModuleResult result = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), downloader, buildTools)
            .CheckStart(new ModuleContext(TestFactory.Tool(), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.True(result.Status);
        Assert.Empty(downloader.Requests);
    }

    [Fact]
    public void CheckStart_Fails_When_Nuget_Cannot_Be_Downloaded()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        var ui = new RecordingConsoleUi();

        ModuleResult result = Module(
            workspace, ui, new RecordingProcessRunner(), new FakeResourceDownloader(succeed: false), "/vs.bat")
            .CheckStart(new ModuleContext(TestFactory.Tool(), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.False(result.Status);
        Assert.Contains("Download ERROR", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckStart_Fails_When_The_Build_Tools_Path_Does_Not_Exist()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        File.WriteAllText(workspace.Paths.NugetPath, "already here");
        var ui = new RecordingConsoleUi();

        ModuleResult result = Module(
            workspace, ui, new RecordingProcessRunner(), new FakeResourceDownloader(), "/no/such/VsDevCmd.bat")
            .CheckStart(new ModuleContext(TestFactory.Tool(), workspace.Dir("Output"), workspace.Paths.GitToolsPath));

        Assert.False(result.Status);
        Assert.Contains(
            "File not found: /no/such/VsDevCmd.bat",
            Assert.Single(ui.TextOf(UiChannel.Failure)),
            StringComparison.Ordinal);
    }

    /// <summary>A template whose solution never arrived must not run anything at all.</summary>
    [Fact]
    public void A_Missing_Solution_Runs_No_Commands()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        string outputPath = workspace.Dir("Output", "MyTool_abc123");
        var runner = new RecordingProcessRunner();

        ModuleResult result = Module(workspace, new RecordingConsoleUi(), runner, new FakeResourceDownloader(), "/vs.bat")
            .Run(new ModuleContext(
                TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/Missing.sln"),
                outputPath,
                workspace.Paths.GitToolsPath));

        Assert.Empty(runner.Commands);
        Assert.True(result.Status);
        Assert.Equal(outputPath, result.OutputPath);
    }
}
