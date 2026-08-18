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

    /// <summary>
    /// Copies every third-party assembly referenced by a solution's projects next to the build
    /// output, so an obfuscated or relocated binary still has its dependencies beside it.
    /// </summary>
    /// <param name="solutionPath">Absolute path to the <c>.sln</c>.</param>
    /// <param name="referenceRoot">
    /// Absolute directory that <c>HintPath</c> values are resolved against - normally the folder
    /// holding the solution. It must be absolute: a relative root silently resolves against the
    /// process working directory, so nothing is found and no dependency is copied.
    /// </param>
    /// <param name="outputPath">Directory to copy the assemblies into.</param>
    public static void CopyReferencedAssemblies(
        string solutionPath, string referenceRoot, string outputPath)
    {
        foreach (string projectPath in SolutionFileReader.GetProjectPaths(solutionPath))
        {
            if (!File.Exists(projectPath))
            {
                continue;
            }

            foreach (string reference in Read(projectPath))
            {
                string referenceFile = reference
                    .Replace(@"..\", string.Empty, StringComparison.Ordinal)
                    .Replace('\\', Path.DirectorySeparatorChar);

                string sourceFile = Path.Combine(referenceRoot, referenceFile);
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, Path.Combine(outputPath, Path.GetFileName(referenceFile)), true);
                }
            }
        }
    }
}
