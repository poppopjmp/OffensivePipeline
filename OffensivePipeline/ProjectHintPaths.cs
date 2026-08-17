using System.Xml.Linq;

namespace OffensivePipeline;

/// <summary>
/// Reads <c>&lt;Reference&gt;&lt;HintPath&gt;</c> values out of a legacy (non-SDK) MSBuild project,
/// so third-party binaries a tool depends on can be copied next to the built executable.
/// </summary>
internal static class ProjectHintPaths
{
    private static readonly XNamespace MsBuild = "http://schemas.microsoft.com/developer/msbuild/2003";

    public static IEnumerable<string> Read(string projectPath)
    {
        XDocument projDefinition = XDocument.Load(projectPath);
        XElement? project = projDefinition.Element(MsBuild + "Project");
        if (project is null)
        {
            return [];
        }

        return project
            .Elements(MsBuild + "ItemGroup")
            .Elements(MsBuild + "Reference")
            .Elements(MsBuild + "HintPath")
            .Select(refElem => refElem.Value);
    }
}
