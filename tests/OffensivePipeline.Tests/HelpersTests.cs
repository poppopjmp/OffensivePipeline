using System.Text;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

public class HelpersTests
{
    /// <summary>Known-answer test: SHA-256 of the three bytes "abc".</summary>
    private const string Sha256OfAbc =
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void ComputeSha256File_Matches_The_Published_Digest()
    {
        using var workspace = new TempWorkspace();
        string file = workspace.File("payload.bin", "abc");

        Assert.Equal(Sha256OfAbc, Helpers.ComputeSha256File(file));
    }

    [Fact]
    public void ComputeSha256File_Is_Lowercase_Hex_Of_The_Expected_Length()
    {
        using var workspace = new TempWorkspace();
        string hash = Helpers.ComputeSha256File(workspace.File("payload.bin", "anything"));

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.All(hash, c => Assert.Contains(c, "0123456789abcdef"));
    }

    [Fact]
    public void ComputeSha256File_Reads_Bytes_Not_Text()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.Root, "bytes.bin");
        File.WriteAllBytes(path, [0x00, 0xFF, 0x10, 0x80]);

        // Independent of any text encoding the file might be mistaken for.
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                new byte[] { 0x00, 0xFF, 0x10, 0x80 })),
            Helpers.ComputeSha256File(path));
    }

    [Fact]
    public void CalculateSha256Files_Writes_A_Manifest_Covering_Every_File()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.Dir("Output");
        File.WriteAllText(Path.Combine(folder, "one.exe"), "abc");
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        File.WriteAllText(Path.Combine(folder, "nested", "two.dll"), "abc");

        Helpers.CalculateSha256Files(folder);

        string[] manifest = File.ReadAllLines(Path.Combine(folder, "sha256.txt"));
        Assert.Equal(2, manifest.Length);
        Assert.All(manifest, line => Assert.Contains($" - {Sha256OfAbc}", line, StringComparison.Ordinal));
        Assert.Contains(manifest, line => line.Contains("one.exe", StringComparison.Ordinal));
        Assert.Contains(manifest, line => line.Contains("two.dll", StringComparison.Ordinal));
    }

    [Fact]
    public void GetRandomString_Is_Alphanumeric_And_Not_Repeated()
    {
        string[] values = [.. Enumerable.Range(0, 50).Select(_ => Helpers.GetRandomString())];

        Assert.All(values, v => Assert.DoesNotContain('.', v));
        Assert.All(values, v => Assert.NotEmpty(v));
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CheckFolder_Creates_A_Missing_Folder_And_Leaves_An_Existing_One_Alone()
    {
        using var workspace = new TempWorkspace();
        string folder = Path.Combine(workspace.Root, "Output");

        Helpers.CheckFolder(folder);
        Assert.True(Directory.Exists(folder));

        File.WriteAllText(Path.Combine(folder, "keep.txt"), "keep");
        Helpers.CheckFolder(folder);
        Assert.True(File.Exists(Path.Combine(folder, "keep.txt")));
    }

    [Fact]
    public void CopyDirectory_Copies_The_Whole_Tree()
    {
        using var workspace = new TempWorkspace();
        workspace.File("src/top.txt", "top");
        workspace.File("src/a/one.txt", "one");
        workspace.File("src/a/b/two.txt", "two");

        List<string> errors = [];
        bool copied = Helpers.CopyDirectory(
            Path.Combine(workspace.Root, "src"), Path.Combine(workspace.Root, "dst"), true, errors);

        Assert.True(copied);
        Assert.Empty(errors);
        Assert.Equal("top", File.ReadAllText(Path.Combine(workspace.Root, "dst", "top.txt")));
        Assert.Equal("one", File.ReadAllText(Path.Combine(workspace.Root, "dst", "a", "one.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(workspace.Root, "dst", "a", "b", "two.txt")));
    }

    [Fact]
    public void CopyDirectory_Without_Recursion_Copies_Only_The_Top_Level()
    {
        using var workspace = new TempWorkspace();
        workspace.File("src/top.txt", "top");
        workspace.File("src/a/one.txt", "one");

        List<string> errors = [];
        bool copied = Helpers.CopyDirectory(
            Path.Combine(workspace.Root, "src"), Path.Combine(workspace.Root, "dst"), false, errors);

        Assert.True(copied);
        Assert.True(File.Exists(Path.Combine(workspace.Root, "dst", "top.txt")));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "dst", "a")));
    }

    /// <summary>
    /// This used to return true unconditionally, so a tool whose source never arrived was handed
    /// straight to the build module.
    /// </summary>
    [Fact]
    public void CopyDirectory_Reports_Failure_When_The_Source_Is_Missing()
    {
        using var workspace = new TempWorkspace();

        List<string> errors = [];
        bool copied = Helpers.CopyDirectory(
            Path.Combine(workspace.Root, "nope"), Path.Combine(workspace.Root, "dst"), true, errors);

        Assert.False(copied);
        Assert.Contains("Source directory not found", Assert.Single(errors), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "dst")));
    }

    /// <summary>
    /// A failure in a nested subtree must propagate: the recursive call's result used to be
    /// discarded, so a half-copied tree reported success.
    /// </summary>
    [Fact]
    public void CopyDirectory_Reports_Failure_When_A_Nested_Copy_Fails()
    {
        using var workspace = new TempWorkspace();
        workspace.File("src/top.txt", "top");
        workspace.File("src/a/one.txt", "one");

        // A file already occupying the destination sub-directory's name makes exactly one branch of
        // the recursion fail while the top level succeeds.
        Directory.CreateDirectory(Path.Combine(workspace.Root, "dst"));
        File.WriteAllText(Path.Combine(workspace.Root, "dst", "a"), "in the way");

        List<string> errors = [];
        bool copied = Helpers.CopyDirectory(
            Path.Combine(workspace.Root, "src"), Path.Combine(workspace.Root, "dst"), true, errors);

        Assert.False(copied);
        Assert.NotEmpty(errors);

        // The rest of the tree was still copied, which is why the caller is given every error
        // rather than only the first.
        Assert.True(File.Exists(Path.Combine(workspace.Root, "dst", "top.txt")));
    }

    [Fact]
    public void DeleteReadOnlyDirectory_Removes_Read_Only_Files()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.Dir("Git", "Tool");
        string file = Path.Combine(folder, "locked.txt");
        File.WriteAllText(file, "content");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        Helpers.DeleteReadOnlyDirectory(Path.Combine(workspace.Root, "Git"));

        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "Git")));
    }

    [Fact]
    public void DeleteReadOnlyDirectory_Removes_Nested_Trees()
    {
        using var workspace = new TempWorkspace();
        workspace.File("Git/Tool/a/b/c/deep.txt", "deep");

        Helpers.DeleteReadOnlyDirectory(Path.Combine(workspace.Root, "Git"));

        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "Git")));
    }

    [Fact]
    public void UnzipFile_Extracts_An_Archive()
    {
        using var workspace = new TempWorkspace();
        string zipPath = Path.Combine(workspace.Root, "archive.zip");

        using (var archiveStream = new FileStream(zipPath, FileMode.Create))
        using (var archive = new System.IO.Compression.ZipArchive(
            archiveStream, System.IO.Compression.ZipArchiveMode.Create))
        {
            System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry("Confuser.CLI.exe");
            using Stream entryStream = entry.Open();
            entryStream.Write(Encoding.UTF8.GetBytes("payload"));
        }

        string destination = Path.Combine(workspace.Root, "extracted");
        Helpers.UnzipFile(zipPath, destination);

        Assert.Equal("payload", File.ReadAllText(Path.Combine(destination, "Confuser.CLI.exe")));
    }
}
