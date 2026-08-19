using Microsoft.Extensions.DependencyInjection;

namespace OffensivePipeline.Modules;

/// <summary>
/// Resolves a module by the name a tool template gives it.
/// </summary>
/// <remarks>
/// Replaces the previous <c>Assembly.GetTypes()</c> scan followed by
/// <c>Activator.CreateInstance</c>, which ran once per plugin per tool - 395 reflection scans for a
/// single <c>all</c> run - and could not give a module a dependency. Names now resolve against
/// explicit container registrations, so an unknown plugin is a lookup miss the caller can report
/// rather than a null cast.
/// </remarks>
public interface IModuleFactory
{
    /// <summary>Every module name a template may legally use.</summary>
    IReadOnlyList<string> KnownModules { get; }

    /// <summary>The module registered under <paramref name="name"/>, or null if there is none.</summary>
    IModule? Resolve(string name);
}

/// <summary>Resolves modules from the container's keyed registrations.</summary>
public sealed class ModuleFactory(IServiceProvider services) : IModuleFactory
{
    /// <summary>
    /// The registration keys, which are also the literal strings accepted in a template's
    /// <c>plugins:</c> field. Changing one of these breaks every template that names it.
    /// </summary>
    private static readonly string[] RegisteredModules =
    [
        "RandomGuid",
        "RandomAssemblyInfo",
        "BuildCsharp",
        "ConfuserEx",
        "Donut",
    ];

    public IReadOnlyList<string> KnownModules => RegisteredModules;

    public IModule? Resolve(string name) => services.GetKeyedService<IModule>(name);
}
