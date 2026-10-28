using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class StoryPollTally
{
    public static bool ShowsResults(StoryStickerDto sticker) => sticker.Counts is { Length: > 0 };

    public static bool CanVote(StoryStickerDto sticker, bool isAuthor) =>
        !isAuthor && sticker.Kind == StoryStickerKinds.Poll && sticker.MyVote < 0;

    public static int Total(int[]? counts)
    {
        if (counts is null)
        {
            return 0;
        }

        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            total += Math.Max(0, counts[index]);
        }

        return total;
    }

    public static float Fraction(int[] counts, int option)
    {
        var total = Total(counts);
        if (total <= 0 || option < 0 || option >= counts.Length)
        {
            return 0f;
        }

        return Math.Max(0, counts[option]) / (float)total;
    }
}
