namespace Aetherphone.Core.Gifs;

internal static class KlipyReveals
{
    private static readonly HashSet<string> Revealed = new(StringComparer.Ordinal);

    public static bool ShouldHold(string? url, bool allowed)
    {
        return !allowed && KlipyMedia.IsHosted(url) && !Revealed.Contains(url!);
    }

    public static void Reveal(string url)
    {
        Revealed.Add(url);
    }
}
