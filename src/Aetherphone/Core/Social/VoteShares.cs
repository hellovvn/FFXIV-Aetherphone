namespace Aetherphone.Core.Social;

internal static class VoteShares
{
    public static void Percentages(ReadOnlySpan<int> counts, Span<int> percents)
    {
        percents.Clear();
        var optionCount = Math.Min(counts.Length, percents.Length);
        var total = 0;
        for (var index = 0; index < optionCount; index++)
        {
            total += Math.Max(0, counts[index]);
        }

        if (total <= 0)
        {
            return;
        }

        var assigned = 0;
        for (var index = 0; index < optionCount; index++)
        {
            percents[index] = Math.Max(0, counts[index]) * 100 / total;
            assigned += percents[index];
        }

        Span<bool> rounded = stackalloc bool[optionCount];
        while (assigned < 100)
        {
            var bestIndex = -1;
            var bestRemainder = 0;
            for (var index = 0; index < optionCount; index++)
            {
                var remainder = Math.Max(0, counts[index]) * 100 % total;
                if (!rounded[index] && remainder > bestRemainder)
                {
                    bestRemainder = remainder;
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            rounded[bestIndex] = true;
            percents[bestIndex]++;
            assigned++;
        }
    }

    public static int Leaders(ReadOnlySpan<int> counts, Span<bool> leaders)
    {
        var best = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            best = Math.Max(best, counts[index]);
        }

        var leaderCount = 0;
        for (var index = 0; index < leaders.Length; index++)
        {
            leaders[index] = best > 0 && index < counts.Length && counts[index] == best;
            if (leaders[index])
            {
                leaderCount++;
            }
        }

        return leaderCount;
    }
}
