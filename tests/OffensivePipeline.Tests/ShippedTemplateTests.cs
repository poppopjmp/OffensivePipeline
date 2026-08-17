using Microsoft.Extensions.DependencyInjection;
using OffensivePipeline.Config;
using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The machine-checked form of the project's first hard constraint: every one of the shipped
/// <c>Tools/*.yml</c> templates must keep loading, and must keep describing a tool the pipeline can
/// actually process.
/// </summary>
/// <remarks>
/// The templates are linked into this project's output from <c>OffensivePipeline/Tools</c> rather
/// than copied, so these tests assert against the exact files that ship. Nothing here writes to
/// them.
/// </remarks>
public class ShippedTemplateTests
{
    /// <summary>The number of templates the project ships. A change here is a deliberate decision.</summary>
    private const int ExpectedTemplateCount = 79;

    private static string ToolsFolder => Path.Combine(AppContext.BaseDirectory, "Tools");

    public static TheoryData<string> TemplateNames()
    {
        TheoryData<string> data = [];
        foreach (string file in Directory.GetFiles(ToolsFolder, "*.yml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TemplateNames))]
    public void Template_Parses_And_Describes_A_Usable_Tool(string templateName)
    {
        var ui = new RecordingConsoleUi();
        List<ToolConfig> tools = TestFactory.Yml(TestFactory.ShippedPaths(), ui).ReadYmls(templateName);

        Assert.Empty(ui.TextOf(UiChannel.Failure));
        ToolConfig tool = Assert.Single(tools);

        Assert.False(string.IsNullOrWhiteSpace(tool.Name), "name must not be empty");
        Assert.False(string.IsNullOrWhiteSpace(tool.Description), "description must not be empty");
        Assert.False(string.IsNullOrWhiteSpace(tool.Language), "language must not be empty");

        Assert.False(string.IsNullOrWhiteSpace(tool.GitLink), "gitLink must not be empty");
        Assert.True(
            Uri.TryCreate(tool.GitLink, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            $"gitLink '{tool.GitLink}' must be an absolute http(s) URI");

        Assert.False(string.IsNullOrWhiteSpace(tool.SolutionPath), "solutionPath must not be empty");
        Assert.EndsWith(".sln", tool.SolutionPath, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(tool.Plugins);
        foreach (string plugin in tool.Plugins)
        {
            Assert.Contains(plugin, ModuleNames.All, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Every_Template_On_Disk_Is_Loaded()
    {
        int filesOnDisk = Directory.GetFiles(ToolsFolder, "*.yml").Length;
        Assert.Equal(ExpectedTemplateCount, filesOnDisk);

        var ui = new RecordingConsoleUi();
        List<ToolConfig> tools = TestFactory.Yml(TestFactory.ShippedPaths(), ui).ReadYmls();

        Assert.Empty(ui.TextOf(UiChannel.Failure));
        Assert.Equal(ExpectedTemplateCount, tools.Count);
    }

    [Fact]
    public void Tool_Names_Are_Unique()
    {
        List<ToolConfig> tools = TestFactory.Yml(TestFactory.ShippedPaths(), new RecordingConsoleUi()).ReadYmls();

        string[] duplicates = tools
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Listing_Order_Is_Stable_And_Alphabetical()
    {
        var ymlHelpers = TestFactory.Yml(TestFactory.ShippedPaths(), new RecordingConsoleUi());

        string[] first = [.. ymlHelpers.ReadYmls().Select(t => t.Name)];
        string[] second = [.. ymlHelpers.ReadYmls().Select(t => t.Name)];

        Assert.Equal(first, second);
        Assert.Equal([.. first.Order(StringComparer.OrdinalIgnoreCase)], first);
    }

    /// <summary>
    /// Every plugin name used by a shipped template must resolve against the real container. This
    /// is the check that catches a keyed registration being renamed: the keys are the public
    /// contract of the template files, so a mismatch silently breaks tools at run time.
    /// </summary>
    [Fact]
    public void Every_Plugin_Used_By_A_Template_Resolves_From_The_Real_Container()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        using ServiceProvider services = CompositionRoot.BuildServiceProvider(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        var factory = services.GetRequiredService<IModuleFactory>();

        string[] used =
        [
            .. TestFactory.Yml(TestFactory.ShippedPaths(), new RecordingConsoleUi())
                .ReadYmls()
                .SelectMany(t => t.Plugins)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.NotEmpty(used);
        foreach (string plugin in used)
        {
            IModule? module = factory.Resolve(plugin);
            Assert.NotNull(module);
            Assert.Equal(plugin, module.Name);
        }
    }

    [Fact]
    public void Shipped_Resource_Templates_Are_Present_And_Contain_Their_Placeholders()
    {
        PipelinePaths paths = TestFactory.ShippedPaths();

        string build = File.ReadAllText(paths.TemplateBuildPath);
        foreach (string token in
            (string[])["{{MSBUILD_PATH}}", "{{SOLUTION_PATH}}", "{{BUILD_OPTIONS}}", "{{OUTPUT_DIR}}", "{{OUTPUT_FILENAME}}"])
        {
            Assert.Contains(token, build, StringComparison.Ordinal);
        }

        string confuser = File.ReadAllText(paths.ConfuserTemplateFile);
        foreach (string token in (string[])["{{BASE_DIR}}", "{{OUTPUT_DIR}}", "{{EXE_FILE}}"])
        {
            Assert.Contains(token, confuser, StringComparison.Ordinal);
        }
    }
}
