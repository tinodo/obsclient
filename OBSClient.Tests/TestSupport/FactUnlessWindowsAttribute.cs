namespace OBSStudioClient.Tests.TestSupport
{
    using Xunit;

    /// <summary>
    /// A fact that is skipped on Windows. It is for a test that needs the TCP stack to block a write when nothing reads: the loopback of
    /// Windows takes tens of megabytes without blocking, however small the buffer of the receiver is, so there such a test would say
    /// nothing. It runs on Linux and macOS, and what it tests is not specific to a platform.
    /// </summary>
    internal sealed class FactUnlessWindowsAttribute : FactAttribute
    {
        public FactUnlessWindowsAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                this.Skip = "The loopback of Windows does not block a write of tens of megabytes when nothing reads from it.";
            }
        }
    }
}
