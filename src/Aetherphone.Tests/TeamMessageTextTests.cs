using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Team;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TeamMessageTextTests
{
    private const string FinalArgs =
        "{\"weekId\":\"2026-W43\",\"stage\":\"final\",\"track\":\"vanilla\",\"rank\":1,\"gil\":100000}";

    private static TeamMessageDto Automated(string kind, string args) =>
        new(1, true, kind, string.Empty, args, string.Empty, DateTime.UtcNow, false);

    private static string Fallback => Loc.T(L.TeamInbox.AutomatedFallback);

    [Fact]
    public void AStaffTextShowsAsWritten()
    {
        var message = new TeamMessageDto(1, true, TeamMessageKinds.Text, "See you at the Gold Saucer", string.Empty,
            "Xeldar", DateTime.UtcNow, false);

        Assert.Equal("See you at the Gold Saucer", TeamMessageText.Render(message));
        Assert.False(TeamMessageText.IsAutomated(message));
    }

    [Fact]
    public void AFinalGilPrizeNamesThePlaceTrackWeekAndAmount()
    {
        var text = TeamMessageText.Render(Automated(TeamMessageKinds.PrismGil, FinalArgs));

        Assert.Contains("1st", text);
        Assert.Contains(Loc.T(L.Prism.TrackVanilla), text);
        Assert.Contains("week 43", text);
        Assert.Contains(NumberText.Group(100000), text);
        Assert.StartsWith("You placed 1st in the Prism final", text);
    }

    [Fact]
    public void AHeatPrizeUsesTheHeatLine()
    {
        var args = "{\"weekId\":\"2026-W44\",\"stage\":\"heat\",\"track\":\"modded\",\"rank\":2,\"gil\":5000}";

        var text = TeamMessageText.Render(Automated(TeamMessageKinds.PrismGil, args));

        Assert.StartsWith("You placed 2nd in your Prism heat", text);
        Assert.Contains(Loc.T(L.Prism.TrackModded), text);
    }

    [Fact]
    public void APaidPrizeNamesTheAmountAndWeek()
    {
        var text = TeamMessageText.Render(Automated(TeamMessageKinds.PrismGilPaid, FinalArgs));

        Assert.Equal(Loc.T(L.TeamInbox.GilPaid, NumberText.Group(100000), "43"), text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"weekId\":")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"weekId\":\"2026-W43\",\"stage\":\"final\",\"track\":\"vanilla\",\"rank\":0,\"gil\":100}")]
    [InlineData("{\"weekId\":\"2026-W43\",\"stage\":\"final\",\"track\":\"vanilla\",\"rank\":1,\"gil\":0}")]
    [InlineData("{\"weekId\":\"2026-W43\",\"stage\":\"semi\",\"track\":\"vanilla\",\"rank\":1,\"gil\":100}")]
    [InlineData("{\"weekId\":\"2026-W43\",\"stage\":\"final\",\"track\":\"glitter\",\"rank\":1,\"gil\":100}")]
    [InlineData("{\"stage\":\"final\",\"track\":\"vanilla\",\"rank\":1,\"gil\":100}")]
    [InlineData("{\"weekId\":\"2026-W43\",\"rank\":\"first\"}")]
    public void BadOrMissingArgsFallBackWithoutThrowing(string args)
    {
        Assert.Equal(Fallback, TeamMessageText.Render(Automated(TeamMessageKinds.PrismGil, args)));
    }

    [Fact]
    public void APaidMessageWithoutAWeekFallsBack()
    {
        Assert.Equal(Fallback, TeamMessageText.Render(Automated(TeamMessageKinds.PrismGilPaid, "{\"gil\":100}")));
    }

    [Fact]
    public void AnUnknownKindShowsItsBodyOrTheFallback()
    {
        var withBody = new TeamMessageDto(1, true, "future_kind", "Hello", string.Empty, string.Empty, DateTime.UtcNow);
        var empty = new TeamMessageDto(2, true, "future_kind", string.Empty, "{}", string.Empty, DateTime.UtcNow);

        Assert.Equal("Hello", TeamMessageText.Render(withBody));
        Assert.Equal(Fallback, TeamMessageText.Render(empty));
        Assert.True(TeamMessageText.IsAutomated(empty));
    }

    [Theory]
    [InlineData("2026-W43", "43")]
    [InlineData("2026-w07", "7")]
    [InlineData("custom-week", "custom-week")]
    [InlineData("", "")]
    public void WeekLabelsReadTheIsoWeekNumber(string weekId, string expected)
    {
        Assert.Equal(expected, TeamMessageText.WeekLabel(weekId));
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "#4")]
    public void PlacesUseOrdinals(int rank, string expected)
    {
        Assert.Equal(expected, TeamMessageText.Place(rank));
    }
}
