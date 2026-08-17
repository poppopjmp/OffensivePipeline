using System.Text.RegularExpressions;
using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The GUID rewriter, which is a pure text transform and therefore fully testable off Windows.
/// </summary>
public partial class RandomGuidTests
{
    private const string SharedGuid = "aaaaaaaa-1111-2222-3333-444444444444";
    private const string SolutionOnlyGuid = "bbbbbbbb-1111-2222-3333-444444444444";
    private const string ProjectOnlyGuid = "cccccccc-1111-2222-3333-444444444444";

    [GeneratedRegex("[a-fA-F0-9]{8}-([a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}")]
    private static partial Regex AnyGuid();

    private sealed record Scenario(
        RandomGuid Module, ModuleContext Context, string Solution, string Project, string AssemblyInfo);

    private static Scenario Arrange(TempWorkspace workspace)
    {
        ToolConfig tool = TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/MyTool.sln");
        string git = workspace.Dir("Git");

        string solution = workspace.File(
            "Git/MyTool/MyTool.sln",
            $"Project(\"{{{SharedGuid}}}\") = \"MyTool\", \"MyTool.csproj\", \"{{{SolutionOnlyGuid}}}\"\r\n");
        string project = workspace.File(
            "Git/MyTool/MyTool.csproj",
            $"<Project><ProjectGuid>{{{SharedGuid}}}</ProjectGuid><Other>{ProjectOnlyGuid}</Other></Project>");
        string assemblyInfo = workspace.File(
            "Git/MyTool/Properties/AssemblyInfo.cs",
            $"[assembly: Guid(\"{SharedGuid}\")]\n");

        return new Scenario(
            new RandomGuid(new RecordingConsoleUi(), TestFactory.Log<RandomGuid>()),
            new ModuleContext(tool, workspace.Dir("Output"), git),
            solution,
            project,
            assemblyInfo);
    }

    [Fact]
    public void Every_Guid_In_Every_Project_File_Is_Replaced()
    {
        using var workspace = new TempWorkspace();
        Scenario scenario = Arrange(workspace);

        ModuleResult result = scenario.Module.Run(scenario.Context);

        Assert.True(result.Status);
        string everything = File.ReadAllText(scenario.Solution)
            + File.ReadAllText(scenario.Project)
            + File.ReadAllText(scenario.AssemblyInfo);

        Assert.DoesNotContain(SharedGuid, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SolutionOnlyGuid, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProjectOnlyGuid, everything, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A GUID that appears in several files must be rewritten to the <em>same</em> replacement in
    /// all of them, or the project reference and the solution entry stop agreeing and the tool no
    /// longer builds.
    /// </summary>
    [Fact]
    public void The_Same_Old_Guid_Maps_To_The_Same_New_Guid_Across_Files()
    {
        using var workspace = new TempWorkspace();
        Scenario scenario = Arrange(workspace);

        scenario.Module.Run(scenario.Context);

        string solutionText = File.ReadAllText(scenario.Solution);
        string projectText = File.ReadAllText(scenario.Project);
        string assemblyInfoText = File.ReadAllText(scenario.AssemblyInfo);

        // The shared GUID is the first in the solution, the first in the project and the only one
        // in AssemblyInfo.cs, so each file's occurrence of it is locatable by position.
        string inSolution = AnyGuid().Matches(solutionText)[0].Value;
        string inProject = AnyGuid().Matches(projectText)[0].Value;
        string inAssemblyInfo = Assert.Single(AnyGuid().Matches(assemblyInfoText)).Value;

        Assert.Equal(inSolution, inProject);
        Assert.Equal(inSolution, inAssemblyInfo);
    }

    [Fact]
    public void Distinct_Old_Guids_Map_To_Distinct_New_Guids()
    {
        using var workspace = new TempWorkspace();
        Scenario scenario = Arrange(workspace);

        scenario.Module.Run(scenario.Context);

        MatchCollection solutionGuids = AnyGuid().Matches(File.ReadAllText(scenario.Solution));
        Assert.Equal(2, solutionGuids.Count);
        Assert.NotEqual(solutionGuids[0].Value, solutionGuids[1].Value);
    }

    [Fact]
    public void Replacements_Are_Parseable_Guids()
    {
        using var workspace = new TempWorkspace();
        Scenario scenario = Arrange(workspace);

        scenario.Module.Run(scenario.Context);

        foreach (Match match in AnyGuid().Matches(File.ReadAllText(scenario.Project)))
        {
            Assert.True(Guid.TryParse(match.Value, out _), $"'{match.Value}' should be a GUID");
        }
    }

    [Fact]
    public void Surrounding_Text_Is_Left_Intact()
    {
        using var workspace = new TempWorkspace();
        Scenario scenario = Arrange(workspace);

        scenario.Module.Run(scenario.Context);

        string projectText = File.ReadAllText(scenario.Project);
        Assert.StartsWith("<Project><ProjectGuid>{", projectText, StringComparison.Ordinal);
        Assert.EndsWith("</Other></Project>", projectText, StringComparison.Ordinal);
    }

    /// <summary>
    /// A template naming a solution that never arrived must fail the module, not be quietly skipped
    /// and then reported as a success by the summary.
    /// </summary>
    [Fact]
    public void A_Missing_Solution_File_Fails_The_Module()
    {
        using var workspace = new TempWorkspace();
        var ui = new RecordingConsoleUi();
        var module = new RandomGuid(ui, TestFactory.Log<RandomGuid>());
        workspace.Dir("Git", "MyTool");

        var context = new ModuleContext(
            TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/Missing.sln"),
            workspace.Dir("Output"),
            workspace.Paths.GitToolsPath);

        ModuleResult result = module.Run(context);

        Assert.False(result.Status);
        Assert.Contains("File not found", Assert.Single(ui.TextOf(UiChannel.Failure)), StringComparison.Ordinal);
    }

    [Fact]
    public void CheckStart_Passes_The_Output_Path_Through_Without_Touching_Disk()
    {
        using var workspace = new TempWorkspace();
        var module = new RandomGuid(new RecordingConsoleUi(), TestFactory.Log<RandomGuid>());
        string outputPath = Path.Combine(workspace.Root, "Output", "never-created");

        ModuleResult result = module.CheckStart(
            new ModuleContext(TestFactory.Tool(), outputPath, workspace.Paths.GitToolsPath));

        Assert.True(result.Status);
        Assert.Equal(outputPath, result.OutputPath);
        Assert.Equal("RandomGuid", result.Name);
        Assert.False(Directory.Exists(outputPath));
    }
}
