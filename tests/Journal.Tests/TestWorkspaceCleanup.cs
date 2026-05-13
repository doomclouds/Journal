using System.Runtime.ExceptionServices;

namespace Journal.Tests;

internal static class TestWorkspaceCleanup
{
    private const int MaxAttempts = 8;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    public static void DeleteDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
                if (attempt == MaxAttempts)
                {
                    firstFailure.Throw();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
