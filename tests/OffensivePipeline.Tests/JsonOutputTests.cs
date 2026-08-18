using System.Text.Json;
using OffensivePipeline.Output;
using OffensivePipeline.Services;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The <c>--json</c> machine output: a stable, camelCase shape with no secret ever serialised.
/// </summary>
public class JsonOutputTests
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

    [Fact]
    public void List_Json_Uses_Camel_Case_And_Omits_Credentials()
    {
        using TempWorkspace workspace = WorkspaceWith(("Secret", TestFactory.Template(
            "Secret", plugins: "RandomGuid", authUser: "agent", authToken: "s3cr3t")));

        var ui = new RecordingConsoleUi();
        var catalog = new ToolCatalog(ui, TestFactory.Yml(workspace.Paths, ui));

        string json = JsonOutput.Serialize(catalog.Collect());

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement tool = Assert.Single(doc.RootElement.EnumerateArray().ToArray());

        Assert.Equal("Secret", tool.GetProperty("name").GetString());
        Assert.Equal("RandomGuid", Assert.Single(tool.GetProperty("plugins").EnumerateArray().ToArray()).GetString());

        // Credentials must never reach machine output, whatever their casing.
        Assert.DoesNotContain("s3cr3t", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authUser", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Json_Reports_A_Clean_Run()
    {
        using TempWorkspace workspace = WorkspaceWith(("Good", TestFactory.Template("Good")));
        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, workspace.Paths, TestFactory.Yml(workspace.Paths, ui), new StubModuleFactory());

        ValidationReport report = validator.Run();

        Assert.True(report.Valid);
        Assert.Equal(1, report.TemplateCount);
        Assert.Empty(report.Invalid);
    }

    [Fact]
    public void Validate_Json_Lists_Each_Problem()
    {
        using TempWorkspace workspace = WorkspaceWith(
            ("Bad", TestFactory.Template("Bad", solutionPath: "Bad.txt", plugins: "Nope")));
        var ui = new RecordingConsoleUi();
        var validator = new TemplateValidator(
            ui, workspace.Paths, TestFactory.Yml(workspace.Paths, ui), new StubModuleFactory());

        ValidationReport report = validator.Run();

        Assert.False(report.Valid);
        InvalidTemplate bad = Assert.Single(report.Invalid);
        Assert.Equal("Bad", bad.Name);
        Assert.Contains(bad.Problems, p => p.Contains(".sln", StringComparison.Ordinal));
        Assert.Contains(bad.Problems, p => p.Contains("Nope", StringComparison.Ordinal));

        // The report round-trips through the serializer.
        string json = JsonOutput.Serialize(report);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
    }
}
