using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// Golden tests for the hand-written <c>.sln</c> reader that replaced the <c>Microsoft.Build</c>
/// dependency. Both consumers - the build module's reference copying and ConfuserEx's dependency
/// solving - are Windows-only at run time, so this parser would otherwise never be verified.
/// </summary>
public class SolutionFileReaderTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Reads_Every_Project_In_Declaration_Order_From_A_Crlf_Solution()
    {
        string solution = Fixture("MultiProject.crlf.sln");
        string root = Path.GetDirectoryName(solution)!;

        IReadOnlyList<string> projects = SolutionFileReader.GetProjectPaths(solution);

        Assert.Equal(
            [
                Path.Combine(root, "Alpha", "Alpha.csproj"),
                Path.Combine(root, "Beta", "Beta.csproj"),
            ],
            projects);
    }

    /// <summary>
    /// Solution folders are <c>Project(...)</c> entries whose "path" is just the folder name. They
    /// must be skipped, or the caller tries to read a directory as a project file.
    /// </summary>
    [Fact]
    public void Skips_Solution_Folders_And_Handles_Lf_Line_Endings()
    {
        string solution = Fixture("SolutionFolders.lf.sln");
        string root = Path.GetDirectoryName(solution)!;

        IReadOnlyList<string> projects = SolutionFileReader.GetProjectPaths(solution);

        Assert.Equal(
            [
                Path.Combine(root, "src", "Gamma", "Gamma.csproj"),
                Path.Combine(root, "src", "Delta", "Delta.fsproj"),
            ],
            projects);
    }

    [Fact]
    public void A_Solution_With_No_Projects_Yields_No_Paths() =>
        Assert.Empty(SolutionFileReader.GetProjectPaths(Fixture("NoProjects.sln")));

    /// <summary>
    /// Windows separators in the <c>.sln</c> must be translated, or every returned path is a single
    /// nonsense filename on Linux and the whole reference-copying step silently does nothing.
    /// </summary>
    [Fact]
    public void Windows_Separators_Are_Translated_To_The_Host_Separator()
    {
        using var workspace = new TempWorkspace();
        string solution = workspace.File(
            "Tool/Tool.sln", TestFactory.Solution("Nested", @"src\Nested\Nested.csproj"));

        string project = Assert.Single(SolutionFileReader.GetProjectPaths(solution));

        Assert.Equal(
            Path.Combine(workspace.Root, "Tool", "src", "Nested", "Nested.csproj"), project);
        Assert.True(Path.IsPathRooted(project));
    }

    [Fact]
    public void A_Relative_Solution_Path_Still_Yields_Absolute_Project_Paths()
    {
        string absolute = Fixture("MultiProject.crlf.sln");
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), absolute);

        Assert.Equal(
            SolutionFileReader.GetProjectPaths(absolute),
            SolutionFileReader.GetProjectPaths(relative));
    }

    /// <summary>The project's own solution, which is the one file the reader must never get wrong.</summary>
    [Fact]
    public void Reads_This_Repositorys_Own_Solution()
    {
        string? repositoryRoot = FindRepositoryRoot();
        Assert.SkipWhen(repositoryRoot is null, "Test is running outside the source tree.");

        string solution = Path.Combine(repositoryRoot!, "OffensivePipeline.sln");

        IReadOnlyList<string> projects = SolutionFileReader.GetProjectPaths(solution);

        Assert.Contains(
            Path.Combine(repositoryRoot!, "OffensivePipeline", "OffensivePipeline.csproj"),
            projects);
        Assert.All(projects, p => Assert.True(File.Exists(p), $"{p} should exist"));
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OffensivePipeline.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
