using OffensivePipeline.Services;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The <c>validate</c> verb: every shipped template passes, and each way a template can be broken
/// is reported and counted so the process exits non-zero.
/// </summary>
public class TemplateValidatorTests
{
    private static TempWorkspace WorkspaceWith(params (string Name, string Content)[] files)
    {
        var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        foreach ((string name, string content) in files)
        {
            File.WriteAllText(Path.Combine(workspace.Paths.YmlsPath, $"{name}.yml"), content);
        }

        return workspace;
    }

    private static (int Result, RecordingConsoleUi Ui) Validate(TempWorkspace workspace)
    {
        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, workspace.Paths, TestFactory.Yml(workspace.Paths, ui), new StubModuleFactory());
        return (validator.Validate(), ui);
    }

    [Fact]
    public void Every_Shipped_Template_Is_Valid()
    {
        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, TestFactory.ShippedPaths(), TestFactory.Yml(TestFactory.ShippedPaths(), ui),
            new StubModuleFactory());

        Assert.Equal(0, validator.Validate());
        Assert.Contains(ui.TextOf(UiChannel.Success), t => t.Contains("are valid", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Valid_Template_Passes()
    {
        using TempWorkspace workspace = WorkspaceWith(("Good", TestFactory.Template("Good")));

        (int result, _) = Validate(workspace);

        Assert.Equal(0, result);
    }

    [Fact]
    public void An_Unknown_Plugin_Is_Reported_And_Counted()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Bad", TestFactory.Template("Bad", plugins: "RandomGuid, NotARealPlugin")));

        (int result, RecordingConsoleUi ui) = Validate(workspace);

        Assert.Equal(1, result);
        Assert.Contains(
            ui.TextOf(UiChannel.Plain),
            t => t.Contains("NotARealPlugin", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Non_Sln_Solution_Path_Is_Reported()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Bad", TestFactory.Template("Bad", solutionPath: "Bad/Bad.csproj")));

        (int result, RecordingConsoleUi ui) = Validate(workspace);

        Assert.Equal(1, result);
        Assert.Contains(ui.TextOf(UiChannel.Plain), t => t.Contains(".sln", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Non_Http_Git_Link_That_Looks_Like_A_Url_Is_Reported()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Bad", TestFactory.Template("Bad", gitLink: "ftp://example.com/repo.git")));

        (int result, RecordingConsoleUi ui) = Validate(workspace);

        Assert.Equal(1, result);
        Assert.Contains(ui.TextOf(UiChannel.Plain), t => t.Contains("http", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Local_Folder_Git_Link_Is_Accepted()
    {
        // The pipeline copies a non-URL gitLink as a local folder, so a bare path is valid.
        using TempWorkspace workspace = WorkspaceWith(
            ("Local", TestFactory.Template("Local", gitLink: @"C:\tools\MyTool")));

        (int result, _) = Validate(workspace);

        Assert.Equal(0, result);
    }

    [Fact]
    public void A_Template_That_Fails_To_Parse_Counts_As_Invalid()
    {
        using TempWorkspace workspace = WorkspaceWith(("Broken", "tool:\n  - name: [unterminated"));

        (int result, RecordingConsoleUi ui) = Validate(workspace);

        // A malformed template is reported and skipped by ReadYmls, then counted here as a
        // parse failure so validate still exits non-zero.
        Assert.True(result >= 1);
        Assert.Contains(
            ui.TextOf(UiChannel.Failure),
            t => t.Contains("could not be parsed", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Count_Reflects_Every_Broken_Template()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Good", TestFactory.Template("Good")),
            ("BadPlugin", TestFactory.Template("BadPlugin", plugins: "Nope")),
            ("BadSln", TestFactory.Template("BadSln", solutionPath: "x.txt")));

        (int result, _) = Validate(workspace);

        Assert.Equal(2, result);
    }

    /// <summary>
    /// A template legally declares a <em>sequence</em> of tools. parseFailures used to be derived
    /// as (files - tools), so a valid two-tool template produced parseFailures = -1, valid = false
    /// and exit 1, with an empty invalid array naming no problem at all.
    /// </summary>
    [Fact]
    public void A_Template_Declaring_Two_Tools_Is_Valid_And_Counted_Correctly()
    {
        const string twoTools = """
            tool:
              - name: AlphaTool
                description: First tool.
                gitLink: https://github.com/example/alpha
                solutionPath: Alpha/Alpha.sln
                language: c#
                plugins: RandomGuid
                authUser:
                authToken:
                toolArguments:
              - name: BetaTool
                description: Second tool.
                gitLink: https://github.com/example/beta
                solutionPath: Beta/Beta.sln
                language: c#
                plugins: RandomGuid
                authUser:
                authToken:
                toolArguments:
            """;

        using TempWorkspace workspace = WorkspaceWith(("Pair", twoTools));
        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, workspace.Paths, TestFactory.Yml(workspace.Paths, ui), new StubModuleFactory());

        Output.ValidationReport report = validator.Run();

        Assert.True(report.Valid);
        Assert.Equal(0, report.ParseFailures);
        Assert.Equal(1, report.TemplateCount);   // one file
        Assert.Equal(2, report.ToolCount);       // two tools in it
        Assert.Empty(report.Invalid);
    }

    /// <summary>A genuinely unparseable file must still be counted, alongside a multi-tool one.</summary>
    [Fact]
    public void Parse_Failures_Are_Counted_Per_File_Not_Inferred_By_Subtraction()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Good", TestFactory.Template("Good")),
            ("Broken", "tool:\n  - name: [unterminated"));

        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, workspace.Paths, TestFactory.Yml(workspace.Paths, ui), new StubModuleFactory());

        Output.ValidationReport report = validator.Run();

        Assert.False(report.Valid);
        Assert.Equal(1, report.ParseFailures);
        Assert.Equal(1, report.ToolCount);
    }
}
