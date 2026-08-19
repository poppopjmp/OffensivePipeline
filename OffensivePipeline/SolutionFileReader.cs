using System.Text.RegularExpressions;

namespace OffensivePipeline;

/// <summary>
/// Minimal reader for classic Visual Studio <c>.sln</c> files, extracting the projects a solution
/// contains. Replaces a dependency on the full <c>Microsoft.Build</c> engine, which was only ever
/// used for <c>SolutionFile.Parse(...).ProjectsInOrder</c>.
/// </summary>
internal static partial class SolutionFileReader
{
    // Project("{FAE04EC0-301F-11D3-...}") = "Name", "Relative\Path.csproj", "{GUID}"
    [GeneratedRegex(
        @"^Project\("".*?""\)\s*=\s*""[^""]*"",\s*""(?<path>[^""]+)""",
        RegexOptions.Multiline)]
    private static partial Regex ProjectLine();

    /// <summary>
    /// Returns the absolute path of every project in the solution, in declaration order. Solution
    /// folders — which appear as <c>Project(...)</c> entries whose path is just the folder name —
    /// are skipped, as is anything without a <c>*proj</c> extension.
    /// </summary>
    public static IReadOnlyList<string> GetProjectPaths(string solutionPath)
    {
        string fullSolutionPath = Path.GetFullPath(solutionPath);
        string solutionDirectory = Path.GetDirectoryName(fullSolutionPath) ?? Directory.GetCurrentDirectory();

        List<string> projectPaths = [];
        foreach (Match match in ProjectLine().Matches(File.ReadAllText(fullSolutionPath)))
        {
            string relativePath = match.Groups["path"].Value.Trim();
            if (!Path.GetExtension(relativePath).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // .sln files always use Windows separators; normalise so the parser also works on Linux.
            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            projectPaths.Add(Path.GetFullPath(Path.Combine(solutionDirectory, relativePath)));
        }

        return projectPaths;
    }
}
