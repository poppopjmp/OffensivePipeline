using System.IO.Compression;
using System.Security.Cryptography;

namespace OffensivePipeline;

/// <summary>
/// Filesystem and hashing utilities.
/// </summary>
/// <remarks>
/// Deliberately free of console and logging concerns: the methods here report failure through their
/// return value or by throwing, and the caller - which has an <c>IConsoleUi</c> and an
/// <c>ILogger</c> injected - decides how to present it. Downloading and shelling out used to live
/// here too; they are now <c>IResourceDownloader</c> and <c>IProcessRunner</c>.
/// </remarks>
internal static class Helpers
{
    public static string GetRandomString() => Path.GetRandomFileName().Replace(".", string.Empty, StringComparison.Ordinal);

    /// <summary>Hex SHA-256 of a file's contents.</summary>
    public static string ComputeSha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Extracts an archive.</summary>
    /// <exception cref="InvalidDataException">The archive is corrupt.</exception>
    /// <exception cref="IOException">The archive could not be read or written.</exception>
    public static void UnzipFile(string filePath, string outputFolder) =>
        ZipFile.ExtractToDirectory(filePath, outputFolder);

    /// <summary>Writes a <c>sha256.txt</c> manifest covering every file under <paramref name="folder"/>.</summary>
    public static void CalculateSha256Files(string folder)
    {
        string[] fileList = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        List<string> hashes = [];
        foreach (string filename in fileList)
        {
            hashes.Add($"{filename} - {ComputeSha256File(filename)}");
        }

        File.WriteAllLines(Path.Combine(folder, "sha256.txt"), hashes);
    }

    /// <summary>
    /// Recursively deletes a directory as well as any subdirectories and files. If the files are read-only, they are flagged as normal and then deleted.
    /// </summary>
    /// <param name="directory">The name of the directory to remove.</param>
    public static void DeleteReadOnlyDirectory(string directory)
    {
        foreach (string subdirectory in Directory.EnumerateDirectories(directory))
        {
            DeleteReadOnlyDirectory(subdirectory);
        }

        foreach (string fileName in Directory.EnumerateFiles(directory))
        {
            var fileInfo = new FileInfo(fileName) { Attributes = FileAttributes.Normal };
            fileInfo.Delete();
        }

        Directory.Delete(directory);
    }

    /// <summary>
    /// Copies a directory tree, continuing past a failed subtree so the caller learns about every
    /// problem rather than only the first.
    /// </summary>
    /// <param name="errors">Collects a description of each failure; the caller reports them.</param>
    /// <returns>False if any file or subdirectory could not be copied.</returns>
    public static bool CopyDirectory(
        string sourceDir, string destinationDir, bool recursive, ICollection<string> errors)
    {
        bool status = true;

        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            errors.Add($"Source directory not found: {dir.FullName}");
            return false;
        }

        try
        {
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    status &= CopyDirectory(subDir.FullName, newDestinationDir, true, errors);
                }
            }
        }
        catch (Exception e)
        {
            errors.Add(e.ToString());
            status = false;
        }

        return status;
    }

    public static void CheckFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }
}
