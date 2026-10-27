using System.Text.Json;
using Aetherphone.Core.Collections;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CollectionsLogicTests
{
    private static CollectionItem Item(int id, string name, string? owned, string patch, int gameOrder)
    {
        var dto = new CollectionItemDto { Id = id, Name = name, Owned = owned, Patch = patch, Order = gameOrder };
        return new CollectionItem(CollectionCategory.Facewear, dto, id);
    }

    private static CollectionItem Mount(int id, string name, int orderGroup, int gameOrder)
    {
        var dto = new CollectionItemDto { Id = id, Name = name, Order = gameOrder, OrderGroup = orderGroup };
        return new CollectionItem(CollectionCategory.Mounts, dto, id);
    }

    private static CollectionItem Roll(int id, string name, int categoryOrder, string number)
    {
        var dto = new CollectionItemDto { Id = id, Name = name, Number = number };
        return new CollectionItem(CollectionCategory.Orchestrions, dto, 0, OrchestrionOrder.Key(dto, categoryOrder));
    }

    private static CollectionItem Minion(int id, string name, long gameOrder)
    {
        var dto = new CollectionItemDto { Id = id, Name = name };
        return new CollectionItem(CollectionCategory.Minions, dto, id, gameOrder);
    }

    private static CollectionItem Card(int id, string name, int gameOrder, int orderGroup)
    {
        var dto = new CollectionItemDto { Id = id, Name = name, Order = gameOrder, OrderGroup = orderGroup };
        return new CollectionItem(CollectionCategory.TriadCards, dto, id);
    }

    private static CollectionItem Item(int id, string name, string? owned = null, string patch = "",
        params string[] sourceTypes)
    {
        var sources = new CollectionSource[sourceTypes.Length];
        for (var index = 0; index < sourceTypes.Length; index++)
        {
            sources[index] = new CollectionSource { Type = sourceTypes[index], Text = "text" };
        }

        var dto = new CollectionItemDto { Id = id, Name = name, Owned = owned, Patch = patch, Sources = sources };
        return new CollectionItem(CollectionCategory.Mounts, dto, id);
    }

    [Theory]
    [InlineData("46.4%", 46.4f)]
    [InlineData(" 0% ", 0f)]
    [InlineData("100%", 100f)]
    [InlineData("3.25", 3.25f)]
    [InlineData("250%", 100f)]
    public void RarityParsesPercentStrings(string text, float expected)
    {
        Assert.Equal(expected, CollectionRarity.Parse(text), 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("n/a")]
    [InlineData("NaN%")]
    public void RarityIsUnknownForMissingOrBrokenValues(string? text)
    {
        Assert.Equal(CollectionRarity.Unknown, CollectionRarity.Parse(text));
    }

    [Fact]
    public void RarityDisplayKeepsOneDecimalBelowTen()
    {
        Assert.Equal(4.6, CollectionRarity.Display(4.56f), 3);
        Assert.Equal(47.0, CollectionRarity.Display(46.5f), 3);
    }

    [Theory]
    [InlineData("7.1", 7100)]
    [InlineData("6.55", 6550)]
    [InlineData("6.05", 6050)]
    [InlineData("6.5", 6500)]
    [InlineData("2.0", 2000)]
    [InlineData("4", 4000)]
    [InlineData("", 0)]
    [InlineData("soon", 0)]
    public void PatchOrderSortsVersionsNumerically(string patch, int expected)
    {
        Assert.Equal(expected, CollectionPatch.Order(patch));
    }

    [Fact]
    public void SuggestionsSkipOwnedUnobtainableAndUnratedItems()
    {
        var items = new[]
        {
            Item(1, "Owned", "90%", "", "Dungeon"),
            Item(2, "Store", "80%", "", "Premium"),
            Item(3, "Event", "70%", "", "Event"),
            Item(4, "Common", "60%", "", "Quest"),
            Item(5, "Rare", "5%", "", "Raid"),
            Item(6, "Unrated", null, "", "Quest"),
            Item(7, "Mixed", "75%", "", "Limited", "Purchase"),
        };
        var owned = new HashSet<int> { 1 };
        var candidates = new List<CollectionItem>();

        CollectionSuggestions.Collect(items, owned, candidates);
        CollectionSuggestions.Rank(candidates, 2);

        Assert.Equal(new[] { 7, 4 }, candidates.ConvertAll(item => item.Id));
    }

    [Fact]
    public void FilterSortsRarestWithUnknownLast()
    {
        var items = new[]
        {
            Item(1, "A", null),
            Item(2, "B", "50%"),
            Item(3, "C", "2%"),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.Rarest);

        Assert.Equal(new[] { 3, 2, 1 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void FilterSortsNewestPatchFirstAndKeepsGameOrderForTies()
    {
        var items = new[]
        {
            Item(1, "A", null, "6.5"),
            Item(2, "B", null, "7.0"),
            Item(3, "C", null, "6.55"),
            Item(4, "D", null, "7.0"),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.Newest);

        Assert.Equal(new[] { 2, 4, 3, 1 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void FilterSortsGameOrderByTheCatalogOrderFieldWithUnorderedLast()
    {
        var items = new[]
        {
            Item(1, "A", null, "7.0", 30),
            Item(2, "B", null, "6.0", 10),
            Item(3, "C", null, "6.5"),
            Item(4, "D", null, "5.0", 20),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.GameOrder);

        Assert.Equal(new[] { 2, 4, 1, 3 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void GameOrderPutsCrossoverCardsAfterTheNumberedCards()
    {
        var items = new[]
        {
            Card(1, "Warrior of Light", 1, 5),
            Card(2, "Tonberry", 2, 0),
            Card(3, "Dodo", 1, 0),
            Card(4, "Clive Rosfield", 15, 5),
            Card(5, "Promathia", 460, 0),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.GameOrder);

        Assert.Equal(new[] { 3, 2, 5, 1, 4 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void MountGameOrderFollowsTheMountGuideByOrderGroupThenOrder()
    {
        var items = new[]
        {
            Mount(1, "Rhiyes", 40900, 268),
            Mount(2, "Magitek Armor", 10000, 3),
            Mount(3, "Company Chocobo", 100, 0),
            Mount(4, "Midgardsormr", 35000, 35),
            Mount(5, "Draught Chocobo", 200, 21),
            Mount(6, "Black Chocobo", 102, 40),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.GameOrder);

        Assert.Equal(new[] { 3, 6, 5, 2, 4, 1 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void OrchestrionGameOrderFollowsTheTabOrderThenTheRollNumberThenTheRowIdForUnnumberedRolls()
    {
        var items = new[]
        {
            Roll(100, "Pulper of Inquiry", 51, "086"),
            Roll(867, "Zanarkand (Modulation)", 61, "�"),
            Roll(1, "Wailers and Waterwheels", 1, "001"),
            Roll(865, "Matoya's Cave (Modulation)", 61, "�"),
            Roll(3, "Indulgence", 2, "002"),
            Roll(4, "From the Depths", 11, "001"),
            Roll(657, "Everybody Falls (Fall Guys Theme)", 61, "�"),
            Roll(5, "Born to Ride", 52, "001"),
            Roll(2, "A New Hope", 1, "003"),
            Roll(866, "Eternal Wind (Modulation)", 61, "�"),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.GameOrder);

        Assert.Equal(new[] { 1, 2, 3, 4, 100, 5, 657, 865, 866, 867 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void MinionGameOrderSortsByCompanionOrderThenRowId()
    {
        var items = new[]
        {
            Minion(1, "Cherry Bomb", GameOrderKey.Of(1001, 1)),
            Minion(2, "Black Chocobo Chick", GameOrderKey.Of(5, 20)),
            Minion(3, "Wayward Hatchling", GameOrderKey.Of(1, 3)),
            Minion(4, "Chocobo Chick Courier", GameOrderKey.Of(5, 10)),
            Minion(5, "Pegwin", GameOrderKey.Of(60001, 500)),
        };
        var output = new List<CollectionItem>();

        CollectionFilter.Apply(items, output, string.Empty, OwnershipFilter.All, string.Empty, null,
            CollectionSort.GameOrder);

        Assert.Equal(new[] { 3, 4, 2, 1, 5 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void CatalogOrderFieldSurvivesTheDiskCacheRoundTrip()
    {
        var json = "{\"count\":2,\"results\":[{\"id\":1,\"order\":7,\"order_group\":5},{\"id\":2}]}";

        var response = JsonSerializer.Deserialize(json, CollectionJsonContext.Default.CollectionResponse)!;
        var cached = JsonSerializer.SerializeToUtf8Bytes(response, CollectionJsonContext.Default.CollectionResponse);
        var restored = JsonSerializer.Deserialize(cached, CollectionJsonContext.Default.CollectionResponse)!;

        Assert.Equal(7, restored.Results![0].Order);
        Assert.Equal(5, restored.Results[0].OrderGroup);
        Assert.Null(restored.Results[1].Order);
    }

    [Fact]
    public void OnlyVerifiedCategoriesOfferGameOrder()
    {
        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            var category = CollectionCategories.All[index];
            var options = CollectionCategories.SortOptions(category);

            Assert.InRange(options.Length, 1, CollectionFilter.SortCount);
            Assert.Equal(CollectionSort.Newest, options[0]);
            var verified = category is CollectionCategory.Facewear or CollectionCategory.Mounts
                or CollectionCategory.Minions or CollectionCategory.Emotes or CollectionCategory.Orchestrions
                or CollectionCategory.TriadCards;
            Assert.Equal(verified, Array.IndexOf(options, CollectionSort.GameOrder) >= 0);
        }
    }

    [Fact]
    public void FilterAppliesOwnershipSourceAndQuery()
    {
        var items = new[]
        {
            Item(1, "Black Chocobo", null, "", "Quest"),
            Item(2, "Chocobo Carriage", null, "", "Purchase"),
            Item(3, "Fat Chocobo", null, "", "Quest"),
        };
        var output = new List<CollectionItem>();
        var owned = new HashSet<int> { 3 };

        CollectionFilter.Apply(items, output, CollectionFilter.Normalize("  CHOCOBO "), OwnershipFilter.Missing,
            "quest", owned, CollectionSort.Newest);

        Assert.Equal(new[] { 1 }, output.ConvertAll(item => item.Id));
    }

    [Fact]
    public void FirstObservationOnlySetsTheBaseline()
    {
        var ledger = new CollectionLedger();
        var gained = new List<int>();

        var outcome = ledger.Observe(CollectionCategory.Mounts, new HashSet<int> { 1, 2 }, 100, gained, true);

        Assert.Equal(UnlockScanOutcome.Baseline, outcome);
        Assert.Empty(ledger.Recent);
        Assert.Equal(new[] { 1, 2 }, ledger.Owned[CollectionLedger.KeyOf(CollectionCategory.Mounts)]);
    }

    [Fact]
    public void LiveGainIsRecordedNewestFirst()
    {
        var ledger = new CollectionLedger();
        var gained = new List<int>();
        ledger.Observe(CollectionCategory.Mounts, new HashSet<int> { 1 }, 100, gained, true);

        var outcome = ledger.Observe(CollectionCategory.Mounts, new HashSet<int> { 1, 5, 3 }, 200, gained, true);

        Assert.Equal(UnlockScanOutcome.Gained, outcome);
        Assert.Equal(new[] { 3, 5 }, gained);
        Assert.Equal(5, ledger.Recent[0].Id);
        Assert.Equal(200, ledger.Recent[0].UnlockedUnix);
    }

    [Fact]
    public void SilentObservationRebaselinesWithoutRecording()
    {
        var ledger = new CollectionLedger();
        var gained = new List<int>();
        ledger.Observe(CollectionCategory.Minions, new HashSet<int> { 1 }, 100, gained, true);

        var outcome = ledger.Observe(CollectionCategory.Minions, new HashSet<int> { 1, 2 }, 200, gained, false);

        Assert.Equal(UnlockScanOutcome.Rebaseline, outcome);
        Assert.Empty(gained);
        Assert.Empty(ledger.Recent);
        Assert.Equal(new[] { 1, 2 }, ledger.Owned[CollectionLedger.KeyOf(CollectionCategory.Minions)]);
    }

    [Fact]
    public void ScanThatLostMostOfTheBaselineIsSkipped()
    {
        var baseline = new List<int>();
        for (var id = 0; id < 40; id++)
        {
            baseline.Add(id);
        }

        var gained = new List<int>();
        var outcome = CollectionLedger.Diff(new HashSet<int> { 1, 2, 3 }, baseline, gained);

        Assert.Equal(UnlockScanOutcome.Skipped, outcome);
    }

    [Fact]
    public void HugeJumpRebaselinesInsteadOfFlooding()
    {
        var baseline = new List<int> { 1 };
        var current = new HashSet<int> { 1 };
        for (var id = 100; id < 100 + CollectionLedger.MaxGainPerScan + 1; id++)
        {
            current.Add(id);
        }

        var gained = new List<int>();
        var outcome = CollectionLedger.Diff(current, baseline, gained);

        Assert.Equal(UnlockScanOutcome.Rebaseline, outcome);
        Assert.Empty(gained);
    }

    [Fact]
    public void RecentListIsCappedAndDeduplicated()
    {
        var ledger = new CollectionLedger();
        var ids = new List<int>();
        for (var id = 0; id < CollectionLedger.MaxRecent + 5; id++)
        {
            ids.Add(id);
        }

        ledger.Record(CollectionCategory.Emotes, ids, 10);
        ledger.Record(CollectionCategory.Emotes, new List<int> { 44 }, 20);

        Assert.Equal(CollectionLedger.MaxRecent, ledger.Recent.Count);
        Assert.Equal(44, ledger.Recent[0].Id);
        Assert.Single(ledger.Recent.FindAll(unlock => unlock.Id == 44));
    }

    [Fact]
    public void TogglePinAddsThenRemoves()
    {
        var ledger = new CollectionLedger();

        Assert.True(ledger.TogglePin(CollectionCategory.TriadCards, 7, 1));
        Assert.True(ledger.IsPinned(CollectionCategory.TriadCards, 7));
        Assert.False(ledger.IsPinned(CollectionCategory.Mounts, 7));
        Assert.False(ledger.TogglePin(CollectionCategory.TriadCards, 7, 2));
        Assert.Empty(ledger.Pins);
    }
}
