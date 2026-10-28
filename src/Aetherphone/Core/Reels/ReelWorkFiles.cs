namespace Aetherphone.Core.Reels;

internal static class ReelWorkFiles
{
    internal static readonly string Folder = Path.Combine(Path.GetTempPath(), "AetherphoneReels");

    private static readonly TimeSpan StaleAge = TimeSpan.FromDays(1);
    private static int swept;

    // A crash or a closed game mid-share leaves its converted file behind; the first share of a session clears what
    // is old, whichever app it comes from.
    internal static void SweepStaleOnce()
    {
        if (Interlocked.Exchange(ref swept, 1) == 0)
        {
            SweepStale(Folder, StaleAge);
        }
    }

    internal static void SweepStale(string folder, TimeSpan age)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - age;
            var files = Directory.GetFiles(folder);
            for (var index = 0; index < files.Length; index++)
            {
                if (File.GetLastWriteTimeUtc(files[index]) < cutoff)
                {
                    File.Delete(files[index]);
                }
            }
        }
        catch (IOException exception)
        {
            AepLog.Warning(exception, "[Reels] could not clear old converted reels");
        }
        catch (UnauthorizedAccessException exception)
        {
            AepLog.Warning(exception, "[Reels] could not clear old converted reels");
        }
    }
}
