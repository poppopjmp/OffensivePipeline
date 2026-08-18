using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// Behaviour of the template reader against synthetic templates in a temporary installation. The
/// shipped templates are covered separately by <see cref="ShippedTemplateTests"/>.
/// </summary>
public class YmlHelpersTests
{
    private static TempWorkspace WorkspaceWith(params (string Name, string Content)[] templates)
    {
        var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        foreach ((string name, string content) in templates)
        {
            File.WriteAllText(Path.Combine(workspace.Paths.YmlsPath, $"{name}.yml"), content);
        }

        return workspace;
    }

    [Fact]
    public void Reads_Every_Declared_Field()
    {
        using TempWorkspace workspace = WorkspaceWith(("Example", TestFactory.Template(
            name: "Example",
            gitLink: "https://github.com/example/example",
            solutionPath: @"Example\Example.sln",
            plugins: "RandomGuid, BuildCsharp",
            description: "Does a thing.",
            language: "c#",
            authUser: "someone",
            authToken: "abc123",
            toolArguments: "-x -y")));

        var ui = new RecordingConsoleUi();
        ToolConfig tool = Assert.Single(TestFactory.Yml(workspace.Paths, ui).ReadYmls());

        Assert.Equal("Example", tool.Name);
        Assert.Equal("Does a thing.", tool.Description);
        Assert.Equal("https://github.com/example/example", tool.GitLink);
        // Separators are normalised to the running platform - see ToolConfig.SolutionPath.
        Assert.Equal(Path.Combine("Example", "Example.sln"), tool.SolutionPath);
        Assert.Equal("c#", tool.Language);
        Assert.Equal(["RandomGuid", "BuildCsharp"], tool.Plugins);
        Assert.Equal("someone", tool.AuthUser);
        Assert.Equal("abc123", tool.AuthToken);
        Assert.Equal("-x -y", tool.ToolArguments);
    }

    /// <summary>
    /// A blank YAML value must arrive as an empty string, not null. <c>GitHelpers</c> tests
    /// <c>authToken</c> for emptiness and every shipped template leaves it blank, so this is the
    /// difference between cloning anonymously and attempting credentialed authentication.
    /// </summary>
    [Fact]
    public void Blank_Values_Become_Empty_Strings_Not_Null()
    {
        using TempWorkspace workspace = WorkspaceWith(("Example", TestFactory.Template("Example")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls());

        Assert.Equal(string.Empty, tool.AuthUser);
        Assert.Equal(string.Empty, tool.AuthToken);
        Assert.Equal(string.Empty, tool.ToolArguments);
    }

    [Theory]
    [InlineData("RandomGuid", new[] { "RandomGuid" })]
    [InlineData("RandomGuid, BuildCsharp", new[] { "RandomGuid", "BuildCsharp" })]
    [InlineData("RandomGuid,BuildCsharp,Donut", new[] { "RandomGuid", "BuildCsharp", "Donut" })]
    [InlineData("  RandomGuid ,  Donut  ", new[] { "RandomGuid", "Donut" })]
    [InlineData("RandomGuid,,Donut", new[] { "RandomGuid", "Donut" })]
    public void Plugins_Are_Split_On_Commas_And_Trimmed(string plugins, string[] expected)
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Example", TestFactory.Template("Example", plugins: plugins)));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls());

        Assert.Equal(expected, tool.Plugins);
    }

    /// <summary>
    /// A template with no plugins used to produce a single empty module name, which then failed
    /// during resolution rather than being reported as the empty chain it is.
    /// </summary>
    [Fact]
    public void Blank_Plugins_Field_Yields_No_Plugins()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Example", TestFactory.Template("Example", plugins: "")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls());

        Assert.Empty(tool.Plugins);
    }

    [Fact]
    public void Named_Lookup_Is_Case_Insensitive()
    {
        // ExtendedHelpText advertises 'seatbelt' while the file ships as Seatbelt.yml. That worked
        // by accident on Windows and threw on Linux.
        using TempWorkspace workspace = WorkspaceWith(("Seatbelt", TestFactory.Template("Seatbelt")));

        var ui = new RecordingConsoleUi();
        ToolConfig tool = Assert.Single(TestFactory.Yml(workspace.Paths, ui).ReadYmls("seatbelt"));

        Assert.Equal("Seatbelt", tool.Name);
        Assert.Empty(ui.TextOf(UiChannel.Failure));
    }

    [Fact]
    public void Named_Lookup_Selects_Only_The_Requested_Template()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Alpha", TestFactory.Template("Alpha")),
            ("Beta", TestFactory.Template("Beta")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls("Beta"));

        Assert.Equal("Beta", tool.Name);
    }

    [Fact]
    public void Unknown_Tool_Reports_A_Clean_Message_Instead_Of_Throwing()
    {
        using TempWorkspace workspace = WorkspaceWith(("Alpha", TestFactory.Template("Alpha")));

        var ui = new RecordingConsoleUi();
        List<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls("doesnotexist");

        Assert.Empty(tools);
        string failure = Assert.Single(ui.TextOf(UiChannel.Failure));
        Assert.Equal("Tool 'doesnotexist' not found. Run 'list' to see available tools.", failure);
    }

    [Fact]
    public void Missing_Tools_Folder_Is_Reported_Not_Thrown()
    {
        using var workspace = new TempWorkspace();

        var ui = new RecordingConsoleUi();
        List<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls();

        Assert.Empty(tools);
        Assert.Contains("ReadYmls: Folder not found", Assert.Single(ui.TextOf(UiChannel.Failure)), StringComparison.Ordinal);
    }

    [Fact]
    public void Override_Arguments_Replace_The_Declared_Tool_Arguments()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Rubeus", TestFactory.Template("Rubeus", toolArguments: "from-template")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi())
                .ReadYmls("Rubeus", "-c All,GPOLocalGroup -d whatever.local"));

        Assert.Equal("-c All,GPOLocalGroup -d whatever.local", tool.ToolArguments);
    }

    [Fact]
    public void Absent_Override_Keeps_The_Declared_Tool_Arguments()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Rubeus", TestFactory.Template("Rubeus", toolArguments: "from-template")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls("Rubeus"));

        Assert.Equal("from-template", tool.ToolArguments);
    }

    /// <summary>An empty override is still an override: <c>-a ""</c> means "pass no arguments".</summary>
    [Fact]
    public void Empty_Override_Clears_The_Declared_Tool_Arguments()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Rubeus", TestFactory.Template("Rubeus", toolArguments: "from-template")));

        ToolConfig tool = Assert.Single(
            TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls("Rubeus", string.Empty));

        Assert.Equal(string.Empty, tool.ToolArguments);
    }

    [Fact]
    public void Listing_Is_Alphabetical_Regardless_Of_Filesystem_Order()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("zulu", TestFactory.Template("zulu")),
            ("Alpha", TestFactory.Template("Alpha")),
            ("mike", TestFactory.Template("mike")),
            ("Bravo", TestFactory.Template("Bravo")));

        List<ToolConfig> tools = TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls();

        Assert.Equal(["Alpha", "Bravo", "mike", "zulu"], tools.Select(t => t.Name));
    }

    /// <summary>
    /// A template with more than one entry under <c>tool:</c> yields one <see cref="ToolConfig"/>
    /// per entry, and each is read once - the reader used to re-walk the sequence for every root
    /// key and only produced correct output because every shipped template has exactly one.
    /// </summary>
    [Fact]
    public void Multiple_Entries_In_One_Template_Are_Each_Read_Once()
    {
        const string twoTools = """
            tool:
              - name: First
                description: The first.
                gitLink: https://github.com/example/first
                solutionPath: First\First.sln
                language: c#
                plugins: RandomGuid
                authUser:
                authToken:
                toolArguments:
              - name: Second
                description: The second.
                gitLink: https://github.com/example/second
                solutionPath: Second\Second.sln
                language: c#
                plugins: Donut
                authUser:
                authToken:
                toolArguments:
            """;

        using TempWorkspace workspace = WorkspaceWith(("Pair", twoTools));

        List<ToolConfig> tools = TestFactory.Yml(workspace.Paths, new RecordingConsoleUi()).ReadYmls();

        Assert.Equal(["First", "Second"], tools.Select(t => t.Name));
    }

    /// <summary>A malformed entry is reported and skipped; the remaining templates still load.</summary>
    [Fact]
    public void A_Template_Missing_A_Required_Key_Is_Reported_And_Skipped()
    {
        const string missingGitLink = """
            tool:
              - name: Broken
                description: Missing its gitLink.
                solutionPath: Broken\Broken.sln
                language: c#
                plugins: RandomGuid
                authUser:
                authToken:
                toolArguments:
            """;

        using TempWorkspace workspace = WorkspaceWith(
            ("Broken", missingGitLink),
            ("Working", TestFactory.Template("Working")));

        var ui = new RecordingConsoleUi();
        List<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls();

        Assert.Equal(["Working"], tools.Select(t => t.Name));
        Assert.Contains("ReadYmls:", Assert.Single(ui.TextOf(UiChannel.Failure)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"Example\Example.sln")]
    [InlineData("Example/Example.sln")]
    public void The_Solution_Path_Separator_Is_Normalised_To_The_Platform(string declared)
    {
        using TempWorkspace workspace = WorkspaceWith(("Example", TestFactory.Template(
            name: "Example", solutionPath: declared)));

        var ui = new RecordingConsoleUi();
        ToolConfig tool = Assert.Single(TestFactory.Yml(workspace.Paths, ui).ReadYmls());

        // Every shipped template spells this the Windows way. Combining that raw value on Linux
        // produced one file literally named "Example\Example.sln" instead of a real path.
        Assert.Equal(Path.Combine("Example", "Example.sln"), tool.SolutionPath);
        Assert.DoesNotContain(
            Path.DirectorySeparatorChar == '/' ? '\\' : '/', tool.SolutionPath);
    }

    [Fact]
    public void Templates_In_Subfolders_Are_Discovered()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        File.WriteAllText(
            Path.Combine(workspace.Paths.YmlsPath, "TopLevel.yml"),
            TestFactory.Template("TopLevel"));

        string nested = Path.Combine(workspace.Paths.YmlsPath, "vendor");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Nested.yml"), TestFactory.Template("Nested"));

        var ui = new RecordingConsoleUi();
        IReadOnlyList<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls();

        // Discovery must match the csproj copy glob (Tools/**/*.yml), which ships nested files too.
        Assert.Contains(tools, t => t.Name == "TopLevel");
        Assert.Contains(tools, t => t.Name == "Nested");
    }

    [Fact]
    public void A_Malformed_Template_Is_Skipped_Not_Fatal()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        File.WriteAllText(
            Path.Combine(workspace.Paths.YmlsPath, "Good.yml"), TestFactory.Template("Good"));
        File.WriteAllText(
            Path.Combine(workspace.Paths.YmlsPath, "Broken.yml"), "tool:\n  - name: [unterminated");

        var ui = new RecordingConsoleUi();
        // A single malformed template used to throw out of yaml.Load and abort the whole run,
        // taking list/all/validate down with it. It must now be reported and skipped.
        IReadOnlyList<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls();

        Assert.Contains(tools, t => t.Name == "Good");
        Assert.DoesNotContain(tools, t => t.Name == "Broken");
        Assert.NotEmpty(ui.TextOf(UiChannel.Failure));
    }

    [Fact]
    public void An_Empty_Template_File_Is_Skipped_Not_Fatal()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        File.WriteAllText(
            Path.Combine(workspace.Paths.YmlsPath, "Good.yml"), TestFactory.Template("Good"));
        File.WriteAllText(Path.Combine(workspace.Paths.YmlsPath, "Empty.yml"), string.Empty);

        var ui = new RecordingConsoleUi();
        IReadOnlyList<ToolConfig> tools = TestFactory.Yml(workspace.Paths, ui).ReadYmls();

        Assert.Contains(tools, t => t.Name == "Good");
    }
}
