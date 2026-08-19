using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>The per-run state a module is handed, and the result it hands back.</summary>
public class ModuleContractTests
{
    [Fact]
    public void The_Checkout_Path_Is_The_Tool_Name_Under_The_Git_Root()
    {
        var context = new ModuleContext(
            TestFactory.Tool(name: "Seatbelt"), "/out", Path.Combine("/install", "Git"));

        Assert.Equal(Path.Combine("/install", "Git", "Seatbelt"), context.ToolCheckoutPath);
    }

    /// <summary>
    /// The declared <c>solutionPath</c> is relative to the git root, not to the checkout, because
    /// every template already begins it with the repository folder name.
    /// </summary>
    [Fact]
    public void The_Solution_Path_Is_Resolved_Against_The_Git_Root()
    {
        var context = new ModuleContext(
            TestFactory.Tool(name: "Seatbelt", solutionPath: "Seatbelt/Seatbelt.sln"),
            "/out",
            Path.Combine("/install", "Git"));

        Assert.Equal(
            Path.Combine("/install", "Git", "Seatbelt", "Seatbelt.sln"), context.SolutionPath);
    }

    [Fact]
    public void A_Module_Result_Succeeds_Unless_Told_Otherwise()
    {
        var result = new ModuleResult { Name = "RandomGuid" };

        Assert.True(result.Status);
        Assert.Null(result.OutputPath);
    }

    /// <summary>
    /// The runner copies a result with <c>with</c> to mark it failed; every other field must
    /// survive that.
    /// </summary>
    [Fact]
    public void A_Module_Result_Can_Be_Copied_With_A_Changed_Status()
    {
        var result = new ModuleResult { Name = "Donut", OutputPath = "/out/Donut", Status = true };

        ModuleResult failed = result with { Status = false };

        Assert.False(failed.Status);
        Assert.Equal("Donut", failed.Name);
        Assert.Equal("/out/Donut", failed.OutputPath);
    }

    /// <summary>
    /// The registration keys are the public contract of every <c>Tools/*.yml</c> file. Renaming one
    /// breaks every template that names it, so the list is asserted literally.
    /// </summary>
    [Fact]
    public void The_Known_Module_Names_Are_Exactly_The_Five_Templates_May_Use()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        using Microsoft.Extensions.DependencyInjection.ServiceProvider services =
            CompositionRoot.BuildServiceProvider(
                workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        var factory = (IModuleFactory)services.GetService(typeof(IModuleFactory))!;

        Assert.Equal(
            ["BuildCsharp", "ConfuserEx", "Donut", "RandomAssemblyInfo", "RandomGuid"],
            factory.KnownModules.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_Unknown_Module_Name_Resolves_To_Null()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        using Microsoft.Extensions.DependencyInjection.ServiceProvider services =
            CompositionRoot.BuildServiceProvider(
                workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        var factory = (IModuleFactory)services.GetService(typeof(IModuleFactory))!;

        Assert.Null(factory.Resolve("RandomManifest"));
        Assert.Null(factory.Resolve("randomguid"));
    }

    /// <summary>
    /// Resolving a module must have no side effects: the directory-creating work used to happen in
    /// the constructor, which is why nothing here was testable before.
    /// </summary>
    [Fact]
    public void Resolving_Every_Module_Writes_Nothing_To_Disk()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        using Microsoft.Extensions.DependencyInjection.ServiceProvider services =
            CompositionRoot.BuildServiceProvider(
                workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        var factory = (IModuleFactory)services.GetService(typeof(IModuleFactory))!;
        foreach (string name in factory.KnownModules)
        {
            Assert.NotNull(factory.Resolve(name));
        }

        Assert.False(Directory.Exists(workspace.Paths.OutputPath));
        Assert.False(Directory.Exists(workspace.Paths.GitToolsPath));
        Assert.False(Directory.Exists(workspace.Paths.ResourcesPath));
    }
}
