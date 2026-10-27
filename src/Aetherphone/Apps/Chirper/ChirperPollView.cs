using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperPollView
{
    private const long SecondsPerMinute = 60L;
    private const string Separator = " · ";

    private int[] counts = Array.Empty<int>();
    private int[] percentValues = Array.Empty<int>();
    private string languageCode = string.Empty;
    private long timeKey = long.MinValue;

    public PostPollDto? Source { get; private set; }

    public string[] Percents { get; private set; } = Array.Empty<string>();

    public float[] Fractions { get; private set; } = Array.Empty<float>();

    public bool[] Leaders { get; private set; } = Array.Empty<bool>();

    public Spring[] Fills { get; private set; } = Array.Empty<Spring>();

    public Spring Reveal;

    public bool Primed;

    public bool Ended { get; private set; }

    public string Footer { get; private set; } = string.Empty;

    public void Sync(PostPollDto poll, long nowUnix)
    {
        var code = Loc.Current.Code;
        var contentChanged = Source is null || !ChirperPolls.SameState(Source, poll)
            || !string.Equals(languageCode, code, StringComparison.Ordinal);
        if (contentChanged)
        {
            Source = poll;
            languageCode = code;
            RebuildContent(poll);
        }

        var key = TimeKey(poll, nowUnix);
        if (!contentChanged && key == timeKey)
        {
            return;
        }

        timeKey = key;
        Ended = ChirperPolls.HasEnded(poll, nowUnix);
        Footer = string.Concat(Loc.Plural(L.Polls.Votes, poll.TotalVotes), Separator,
            Ended ? Loc.T(L.Polls.FinalResults) : Loc.T(L.Chirper.PollEnds, TimeText.Until(poll.EndsAtUnix)));
    }

    private static long TimeKey(PostPollDto poll, long nowUnix) =>
        nowUnix / SecondsPerMinute * 2 + (ChirperPolls.HasEnded(poll, nowUnix) ? 1 : 0);

    private void RebuildContent(PostPollDto poll)
    {
        var optionCount = poll.Options.Length;
        if (Percents.Length != optionCount)
        {
            Percents = new string[optionCount];
            Fractions = new float[optionCount];
            Leaders = new bool[optionCount];
            Fills = new Spring[optionCount];
            counts = new int[optionCount];
            percentValues = new int[optionCount];
            Primed = false;
        }

        for (var index = 0; index < optionCount; index++)
        {
            counts[index] = ChirperPolls.CountAt(poll, index);
        }

        VoteShares.Percentages(counts, percentValues);
        VoteShares.Leaders(counts, Leaders);
        var total = 0;
        for (var index = 0; index < optionCount; index++)
        {
            total += counts[index];
        }

        for (var index = 0; index < optionCount; index++)
        {
            Percents[index] = Loc.T(L.Polls.Percent, percentValues[index]);
            Fractions[index] = total > 0 ? (float)counts[index] / total : 0f;
        }
    }
}
