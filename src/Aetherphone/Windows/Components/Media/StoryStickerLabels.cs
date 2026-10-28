using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Windows.Components;

internal sealed class StoryStickerLabels
{
    private readonly int[] counts = new int[2];
    private readonly int[] percents = new int[2];
    private StoryDto? source;
    private string language = string.Empty;
    private bool authored;
    private string[] labels = Array.Empty<string>();
    private string[] firstPercents = Array.Empty<string>();
    private string[] secondPercents = Array.Empty<string>();
    private float[] firstFractions = Array.Empty<float>();
    private float[] secondFractions = Array.Empty<float>();

    public void Sync(StoryDto story, bool isAuthor)
    {
        var code = Loc.Current.Code;
        if (ReferenceEquals(source, story) && authored == isAuthor
            && string.Equals(language, code, StringComparison.Ordinal))
        {
            return;
        }

        source = story;
        authored = isAuthor;
        language = code;
        var stickers = story.Stickers ?? Array.Empty<StoryStickerDto>();
        if (labels.Length != stickers.Length)
        {
            labels = new string[stickers.Length];
            firstPercents = new string[stickers.Length];
            secondPercents = new string[stickers.Length];
            firstFractions = new float[stickers.Length];
            secondFractions = new float[stickers.Length];
        }

        for (var index = 0; index < stickers.Length; index++)
        {
            Rebuild(index, stickers[index], isAuthor);
        }
    }

    public string Label(int index) => labels[index];

    public string FirstPercent(int index) => firstPercents[index];

    public string SecondPercent(int index) => secondPercents[index];

    public float FirstFraction(int index) => firstFractions[index];

    public float SecondFraction(int index) => secondFractions[index];

    private void Rebuild(int index, StoryStickerDto sticker, bool isAuthor)
    {
        firstPercents[index] = string.Empty;
        secondPercents[index] = string.Empty;
        firstFractions[index] = 0f;
        secondFractions[index] = 0f;
        switch (sticker.Kind)
        {
            case StoryStickerKinds.Poll:
                labels[index] = string.Empty;
                if (sticker.Counts is not { Length: 2 } tallies)
                {
                    return;
                }

                counts[0] = tallies[0];
                counts[1] = tallies[1];
                VoteShares.Percentages(counts, percents);
                firstPercents[index] = Loc.T(L.Polls.Percent, percents[0]);
                secondPercents[index] = Loc.T(L.Polls.Percent, percents[1]);
                firstFractions[index] = StoryPollTally.Fraction(tallies, 0);
                secondFractions[index] = StoryPollTally.Fraction(tallies, 1);
                return;
            case StoryStickerKinds.Question:
                labels[index] = isAuthor ? Loc.Plural(L.Story.ResponseCount, sticker.AnswerCount)
                    : sticker.Answered ? Loc.T(L.Story.AnswerSent)
                    : Loc.T(L.Story.AnswerHint);
                return;
            default:
                labels[index] = "@" + sticker.Text;
                return;
        }
    }
}
