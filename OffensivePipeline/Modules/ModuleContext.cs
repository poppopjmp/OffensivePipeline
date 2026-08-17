namespace OffensivePipeline.Modules;

/// <summary>
/// Everything a module needs to know about the run it is part of.
/// </summary>
/// <remarks>
/// Per-run state is a parameter rather than constructor arguments, which is what allows modules to
/// be ordinary singleton-free DI services resolved by name. Modules that nest their output
/// (ConfuserEx, Donut) derive their own folder from <paramref name="OutputPath"/> in both
/// <see cref="IModule.CheckStart"/> and <see cref="IModule.Run"/> and report it back through
/// <see cref="ModuleResult.OutputPath"/>; the runner threads that into the next module's context.
/// </remarks>
/// <param name="Tool">The tool template being processed.</param>
/// <param name="OutputPath">Folder the previous stage wrote to, and this stage reads from.</param>
/// <param name="GitWorkingPath">Root under which tool repositories are checked out.</param>
public sealed record ModuleContext(ToolConfig Tool, string OutputPath, string GitWorkingPath)
{
    /// <summary>The checkout folder for this run's tool.</summary>
    public string ToolCheckoutPath => Path.Combine(GitWorkingPath, Tool.Name);

    /// <summary>The tool's solution file, resolved against the checkout root.</summary>
    public string SolutionPath => Path.Combine(GitWorkingPath, Tool.SolutionPath);
}
