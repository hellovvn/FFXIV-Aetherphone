namespace Aetherphone.Core.Collections;

internal static class GameOrderKey
{
    private const long MajorWeight = 1_000_000L;

    public static long Of(long major, long minor) => major * MajorWeight + minor;
}
