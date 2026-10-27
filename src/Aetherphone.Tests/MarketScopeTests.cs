using Aetherphone.Core.Game;
using Aetherphone.Core.Market;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MarketScopeTests
{
    private static readonly DataCenterSheetRow[] DataCenters =
    {
        new(1, "Elemental", 1),
        new(4, "Primal", 2),
        new(5, "Aether", 2),
        new(6, "Chaos", 3),
        new(7, "Light", 3),
        new(9, "Vacant", 2),
        new(12, "Materia", 4),
        new(101, "陆行鸟", 5),
        new(150, "NA Cloud DC (Beta)", 7),
        new(151, string.Empty, 3),
    };

    private static readonly WorldSheetRow[] Worlds =
    {
        new(40, "Twintania", 7, true),
        new(33, "Shiva", 7, true),
        new(80, "Cerberus", 6, true),
        new(97, "Ragnarok", 6, true),
        new(79, "Cactuar", 5, true),
        new(73, "Adamantoise", 5, true),
        new(35, "Famfrit", 4, true),
        new(45, "Carbuncle", 1, true),
        new(21, "Ravana", 12, true),
        new(1042, "拉诺西亚", 101, true),
        new(4001, "Cloudtest01", 150, true),
        new(4002, "Nameless", 151, true),
        new(3000, "Hidden", 7, false),
        new(3001, string.Empty, 6, true),
        new(3002, "Orphan", 0, true),
    };

    private static readonly MarketScope[] Local =
    {
        new(MarketScopeKind.World, "Cerberus", "Cerberus"),
        new(MarketScopeKind.DataCenter, "Chaos", "Chaos"),
        new(MarketScopeKind.Region, "Europe", "Europe"),
    };

    private static MarketGeography Geography() => MarketGeography.Build(Worlds, DataCenters);

    [Fact]
    public void BuildListsRegionsInAFixedOrderWithSortedDataCentersAndWorlds()
    {
        var regions = Geography().Regions;

        Assert.Equal(new[] { "North-America", "Europe", "Oceania", "Japan", "中国" },
            Array.ConvertAll(regions, region => region.ApiName));
        Assert.Equal(new[] { "Aether", "Primal" }, Array.ConvertAll(regions[0].DataCenters, center => center.Name));
        Assert.Equal(new[] { "Adamantoise", "Cactuar" }, regions[0].DataCenters[0].Worlds);
        Assert.Equal(new[] { "Shiva", "Twintania" }, regions[1].DataCenters[1].Worlds);
        Assert.Equal("Europe", regions[1].DataCenters[1].Region);
    }

    [Fact]
    public void BuildLeavesOutPrivateNamelessAndOrphanWorldsAndUnknownRegions()
    {
        var geography = Geography();

        Assert.False(geography.Contains(MarketScopeKind.World, "Hidden"));
        Assert.False(geography.Contains(MarketScopeKind.World, "Orphan"));
        Assert.False(geography.Contains(MarketScopeKind.World, "Cloudtest01"));
        Assert.False(geography.Contains(MarketScopeKind.World, "Nameless"));
        Assert.False(geography.Contains(MarketScopeKind.DataCenter, "NA Cloud DC (Beta)"));
        Assert.False(geography.Contains(MarketScopeKind.DataCenter, "Vacant"));
        Assert.Equal(2, geography.Regions[1].DataCenters.Length);
    }

    [Fact]
    public void ContainsChecksTheKindAndIgnoresCase()
    {
        var geography = Geography();

        Assert.True(geography.Contains(MarketScopeKind.World, "twintania"));
        Assert.True(geography.Contains(MarketScopeKind.DataCenter, "Light"));
        Assert.True(geography.Contains(MarketScopeKind.Region, "Oceania"));
        Assert.False(geography.Contains(MarketScopeKind.World, "Light"));
        Assert.False(geography.Contains(MarketScopeKind.Region, "Light"));
        Assert.False(geography.Contains(MarketScopeKind.DataCenter, "Europe"));
    }

    [Fact]
    public void ParentOfNamesTheNextLevelUp()
    {
        var geography = Geography();

        Assert.Equal("Light", geography.ParentOf(MarketScopeKind.World, "Twintania"));
        Assert.Equal("Europe", geography.ParentOf(MarketScopeKind.DataCenter, "Light"));
        Assert.Equal(string.Empty, geography.ParentOf(MarketScopeKind.Region, "Europe"));
        Assert.Equal(string.Empty, geography.ParentOf(MarketScopeKind.World, "Atlantis"));
        Assert.Equal("Light", geography.DataCenterOfWorld("Shiva")?.Name);
    }

    [Fact]
    public void BuildWithoutSheetsIsEmpty()
    {
        var geography = MarketGeography.Build(ReadOnlySpan<WorldSheetRow>.Empty,
            ReadOnlySpan<DataCenterSheetRow>.Empty);

        Assert.Same(MarketGeography.Empty, geography);
        Assert.False(geography.Contains(MarketScopeKind.Region, "Europe"));
    }

    [Fact]
    public void WithoutAPinTheChosenLocalKindIsPriced()
    {
        var choice = MarketScopes.Resolve(Local, MarketScopeKind.DataCenter, MarketScopeKind.World, string.Empty,
            Geography());

        Assert.False(choice.Remote);
        Assert.Equal(1, choice.LocalIndex);
        Assert.Equal("Chaos", choice.Scope.ApiName);
    }

    [Fact]
    public void AWorldInAnotherRegionIsARemoteScope()
    {
        var choice = MarketScopes.Resolve(Local, MarketScopeKind.DataCenter, MarketScopeKind.World, "Adamantoise",
            Geography());

        Assert.True(choice.Remote);
        Assert.Equal(-1, choice.LocalIndex);
        Assert.Equal(MarketScopeKind.World, choice.Scope.Kind);
        Assert.Equal("Adamantoise", choice.Scope.ApiName);
        Assert.False(choice.Scope.IsMultiWorld);
    }

    [Fact]
    public void APinThatMatchesWhereYouAreReadsAsLocal()
    {
        var choice = MarketScopes.Resolve(Local, MarketScopeKind.World, MarketScopeKind.DataCenter, "Chaos",
            Geography());

        Assert.False(choice.Remote);
        Assert.Equal(1, choice.LocalIndex);
        Assert.Equal(MarketScopeKind.DataCenter, choice.Scope.Kind);
    }

    [Fact]
    public void APinTheGameNoLongerListsFallsBackToTheLocalScope()
    {
        var choice = MarketScopes.Resolve(Local, MarketScopeKind.Region, MarketScopeKind.World, "Atlantis",
            Geography());

        Assert.False(choice.Remote);
        Assert.Equal("Europe", choice.Scope.ApiName);
    }

    [Fact]
    public void APinStillPricesWhileLoggedOut()
    {
        var choice = MarketScopes.Resolve(Array.Empty<MarketScope>(), MarketScopeKind.DataCenter,
            MarketScopeKind.DataCenter, "Light", Geography());

        Assert.True(choice.Remote);
        Assert.True(choice.Scope.IsValid);
        Assert.Equal("Light", choice.Scope.ApiName);
    }

    [Fact]
    public void NothingIsPricedWhileLoggedOutWithoutAPin()
    {
        var choice = MarketScopes.Resolve(Array.Empty<MarketScope>(), MarketScopeKind.DataCenter,
            MarketScopeKind.World, string.Empty, Geography());

        Assert.False(choice.Scope.IsValid);
        Assert.Equal(-1, choice.LocalIndex);
    }

    [Fact]
    public void ChoosingAnotherDataCenterPinsItAndKeepsTheLocalKind()
    {
        var configuration = new Configuration { MarketScope = MarketScopeKind.World };

        var changed = MarketScopes.Choose(configuration, Local, MarketScopeKind.DataCenter, "Light");

        Assert.True(changed);
        Assert.Equal(MarketScopeKind.DataCenter, configuration.MarketPinnedScopeKind);
        Assert.Equal("Light", configuration.MarketPinnedScope);
        Assert.Equal(MarketScopeKind.World, configuration.MarketScope);
        Assert.False(MarketScopes.Choose(configuration, Local, MarketScopeKind.DataCenter, "Light"));
    }

    [Fact]
    public void ChoosingWhereYouAreDropsThePin()
    {
        var configuration = new Configuration
        {
            MarketScope = MarketScopeKind.World,
            MarketPinnedScopeKind = MarketScopeKind.World,
            MarketPinnedScope = "Twintania",
        };

        var changed = MarketScopes.Choose(configuration, Local, MarketScopeKind.DataCenter, "Chaos");

        Assert.True(changed);
        Assert.Equal(string.Empty, configuration.MarketPinnedScope);
        Assert.Equal(MarketScopeKind.DataCenter, configuration.MarketScope);
    }

    [Fact]
    public void ChoosingALocalKindDropsThePin()
    {
        var configuration = new Configuration
        {
            MarketScope = MarketScopeKind.Region,
            MarketPinnedScopeKind = MarketScopeKind.DataCenter,
            MarketPinnedScope = "Light",
        };

        Assert.True(MarketScopes.ChooseLocal(configuration, MarketScopeKind.Region));
        Assert.Equal(string.Empty, configuration.MarketPinnedScope);
        Assert.Equal(MarketScopeKind.Region, configuration.MarketScope);
        Assert.False(MarketScopes.ChooseLocal(configuration, MarketScopeKind.Region));
    }

    [Fact]
    public void ClearingThePinReturnsToTheLocalKind()
    {
        var configuration = new Configuration
        {
            MarketScope = MarketScopeKind.World,
            MarketPinnedScopeKind = MarketScopeKind.Region,
            MarketPinnedScope = "Japan",
        };

        Assert.True(MarketScopes.ClearPin(configuration));
        Assert.Equal(MarketScopeKind.World, configuration.MarketScope);
        Assert.False(MarketScopes.ClearPin(configuration));
        var choice = MarketScopes.Resolve(Local, configuration.MarketScope, configuration.MarketPinnedScopeKind,
            configuration.MarketPinnedScope, Geography());
        Assert.Equal("Cerberus", choice.Scope.ApiName);
    }

    [Fact]
    public void AFreshConfigurationFollowsYourDataCenter()
    {
        var configuration = JsonConvert.DeserializeObject<Configuration>("{}")!;

        Assert.Equal(MarketScopeKind.DataCenter, configuration.MarketScope);
        Assert.Equal(string.Empty, configuration.MarketPinnedScope);
    }

    [Fact]
    public void AnOlderConfigurationKeepsItsScopeKindAndHasNoPin()
    {
        var configuration = JsonConvert.DeserializeObject<Configuration>("""{"MarketScope":2}""")!;

        Assert.Equal(MarketScopeKind.Region, configuration.MarketScope);
        Assert.Equal(string.Empty, configuration.MarketPinnedScope);
    }

    [Fact]
    public void APinSurvivesASaveAndLoad()
    {
        var configuration = new Configuration();
        MarketScopes.Choose(configuration, Local, MarketScopeKind.World, "Adamantoise");

        var restored = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(configuration))!;

        Assert.Equal(MarketScopeKind.World, restored.MarketPinnedScopeKind);
        Assert.Equal("Adamantoise", restored.MarketPinnedScope);
        Assert.Equal(MarketScopeKind.DataCenter, restored.MarketScope);
        Assert.True(MarketScopes.Resolve(Local, restored.MarketScope, restored.MarketPinnedScopeKind,
            restored.MarketPinnedScope, Geography()).Remote);
    }
}
