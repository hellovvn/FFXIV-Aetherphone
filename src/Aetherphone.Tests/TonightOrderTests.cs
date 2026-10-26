using Aetherphone.Apps.LoveIsland;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Notifications;
using Xunit;
using static Aetherphone.Tests.CampaignFixtures;

namespace Aetherphone.Tests;

public sealed class TonightOrderTests
{
    private static readonly TonightSection[] DefaultOrder =
    {
        TonightSection.Status, TonightSection.Night, TonightSection.Live, TonightSection.Share,
        TonightSection.Pinned, TonightSection.Posts, TonightSection.Syncshells, TonightSection.Links,
        TonightSection.About,
    };

    [Fact]
    public void AnEmptyOrderKeepsTheDefaultLayout()
    {
        Assert.Equal(DefaultOrder, TonightOrder.Build(null));
        Assert.Equal(DefaultOrder, TonightOrder.Build(Array.Empty<string>()));
    }

    [Fact]
    public void ThePartnerOrderLeadsAndMissingSectionsFollowInTheirDefaultPlace()
    {
        var order = TonightOrder.Build(new[] { " Links ", "share", "unknown", "share", "about" });

        Assert.Equal(new[]
        {
            TonightSection.Links, TonightSection.Share, TonightSection.About, TonightSection.Status,
            TonightSection.Night, TonightSection.Live, TonightSection.Pinned, TonightSection.Posts,
            TonightSection.Syncshells,
        }, order);
    }

    [Fact]
    public void TheLabelsFollowTheCampaignOrder()
    {
        var labels = new LoveIslandLabels();
        labels.Sync(Campaign() with { TonightOrder = new[] { "posts", "status" } }, new CampaignTexts(),
            "LoveIslandEclipse");

        Assert.Equal(TonightSection.Posts, labels.TonightSections[0]);
        Assert.Equal(TonightSection.Status, labels.TonightSections[1]);
        Assert.Equal(DefaultOrder.Length, labels.TonightSections.Length);
    }

    [Fact]
    public void AServerWithoutFramesStillListsItsSingleFrame()
    {
        var ledger = new CampaignLedger(CampaignDefinition.LoveIsland, new Configuration(), () => { });
        var legacy = Campaign() with
        {
            TonightOrder = null!,
            Reward = new CampaignRewardDto("badge", "Eclipse badge", "frame", "Eclipse frame", 500, null!),
        };

        ledger.ApplyCampaign(legacy, new List<PhoneNotification>());

        var frame = Assert.Single(ledger.Campaign!.Reward.Frames);
        Assert.Equal("frame", frame.Id);
        Assert.Equal("Eclipse frame", frame.Name);
        Assert.Empty(ledger.Campaign.TonightOrder);
    }
}
