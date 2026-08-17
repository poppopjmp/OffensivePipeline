namespace OffensivePipeline.Modules;

/// <summary>
/// A pipeline stage. Implementations are registered in the container under the exact name used in
/// the <c>plugins:</c> list of a tool template, so those names are part of the public contract of
/// <c>Tools/*.yml</c>.
/// </summary>
public interface IModule
{
    /// <summary>Display name of the module, matching the name it is registered under.</summary>
    string Name { get; }

    /// <summary>
    /// Verifies the module's prerequisites, downloads anything missing and creates the folders it
    /// will write to. Returning a result whose status is false stops the chain for this tool.
    /// </summary>
    ModuleResult CheckStart(ModuleContext context);

    /// <summary>Performs the module's work.</summary>
    ModuleResult Run(ModuleContext context);
}
