using Aetherphone.Core.Gifs;
using Aetherphone.Core.Report;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KlipyReportReasonsTests
{
    private static readonly string[] KlipyReasons =
    {
        "nudity", "violence", "hate_speech", "harassment", "spam", "misinformation", "copyright", "offensive",
        "illegal", "broken", "low_quality", "not_relevant", "impersonation", "other",
    };

    [Theory]
    [InlineData("explicit", "nudity")]
    [InlineData("hate", "hate_speech")]
    [InlineData("threats", "violence")]
    [InlineData("child", "illegal")]
    [InlineData("creators", "copyright")]
    [InlineData("impersonation", "impersonation")]
    public void AReportAboutTheImageReachesKlipyWithItsReason(string category, string expected)
    {
        Assert.Equal(expected, KlipyReportReasons.For(category));
    }

    [Theory]
    [InlineData("spam")]
    [InlineData("harassment")]
    [InlineData("privacy")]
    [InlineData("ingame")]
    [InlineData("evasion")]
    [InlineData("other")]
    public void AReportAboutThePlayerStaysWithOurModerators(string category)
    {
        Assert.Null(KlipyReportReasons.For(category));
    }

    [Fact]
    public void EveryForwardedReasonIsOneKlipyAccepts()
    {
        for (var index = 0; index < ReportCategories.All.Length; index++)
        {
            var reason = KlipyReportReasons.For(ReportCategories.All[index].Id);
            if (reason is not null)
            {
                Assert.Contains(reason, KlipyReasons);
            }
        }
    }
}
