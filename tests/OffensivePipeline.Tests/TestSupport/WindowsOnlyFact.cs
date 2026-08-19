using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OffensivePipeline.Tests.TestSupport;

/// <summary>
/// A fact that is skipped off Windows. The build and obfuscation pipeline shells out through
/// <c>cmd.exe</c>, so the handful of assertions that need a real child process cannot run on the
/// Linux CI leg; everything else in this suite is deliberately platform-neutral.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Requires cmd.exe; the pipeline's process runner is Windows-bound.";
        }
    }
}
