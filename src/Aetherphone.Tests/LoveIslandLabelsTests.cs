using Aetherphone.Apps.LoveIsland;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Xunit;
using static Aetherphone.Tests.CampaignFixtures;

namespace Aetherphone.Tests;

public sealed class LoveIslandLabelsTests
{
    private static ExperienceLabels Labels(params CampaignExperienceDto[] experiences)
    {
        var labels = new LoveIslandLabels();
        labels.Sync(Campaign(experiences), new CampaignTexts(), "LoveIslandEclipse");
        return labels.Experiences[0];
    }

    [Fact]
    public void AHousingPlotTeleportsToThePlot()
    {
        var labels = Labels(Experience("doors", Start, Start + Hour));

        Assert.True(labels.CanTeleport);
        Assert.Equal("/li Sephirot, Mist, W24, P32", labels.Command);
        Assert.Equal("Sephirot, Mist, W24, P32", labels.Destination);
    }

    [Fact]
    public void APlaceNearAWardTeleportsToTheWardEntrance()
    {
        var labels = Labels(Elsewhere("beach", "The Lavender Beds", 1));

        Assert.True(labels.CanTeleport);
        Assert.Equal("/li Sephirot, Lavender Beds, W1, P0", labels.Command);
        Assert.Equal("Sephirot · Amethyst Shallows", labels.Address);
        Assert.Equal("The Lavender Beds · Ward 1", labels.WardPlot);
        Assert.Equal("Sephirot · Amethyst Shallows · The Lavender Beds · Ward 1", labels.TravelLine);
    }

    [Fact]
    public void APlaceWithoutAWardHasNoTeleport()
    {
        var labels = Labels(Elsewhere("field", spot: "By the lake"));

        Assert.False(labels.CanTeleport);
        Assert.Equal(string.Empty, labels.Command);
        Assert.Equal("Sephirot · Amethyst Shallows · By the lake", labels.TravelLine);
    }
}
