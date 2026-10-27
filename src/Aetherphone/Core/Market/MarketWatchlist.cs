using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Market;

internal sealed class MarketWatchSeries
{
    public readonly uint ItemId;
    public readonly float[] Points = new float[MarketTrend.WatchPoints];
    public readonly int[] Volumes = new int[MarketTrend.WatchPoints];
    public MarketHistory? Source;
    public bool Hq;
    public int Filled;
    public long Median;

    public MarketWatchSeries(uint itemId)
    {
        ItemId = itemId;
    }

    public bool HasLine => Filled >= 2;
}

internal sealed class MarketWatchlist
{
    public const int MaxItems = 40;

    private readonly MarketboardService market;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly List<MarketScope> scopes = new();
    private readonly Dictionary<uint, MarketWatchSeries> series = new();
    private long[] scratch = new long[256];
    private uint scopesWorld;
    private MarketGeography? geography;
    private MarketScopeChoice choice = MarketScopeChoice.None;
    private int scopesVersion = 1;
    private int choiceVersion;
    private MarketScopeKind choiceLocalKind;
    private MarketScopeKind choicePinnedKind;
    private string? choicePinnedName;

    public MarketWatchlist(MarketboardService market, Configuration configuration, GameData gameData)
    {
        this.market = market;
        this.configuration = configuration;
        this.gameData = gameData;
    }

    public IReadOnlyList<MarketScope> Scopes => scopes;

    public List<uint> Items => configuration.MarketFavorites;

    public MarketGeography Geography => geography ??= MarketGeography.Build(gameData.WorldRows(),
        gameData.DataCenterRows());

    public int ScopeIndex => Choice.LocalIndex;

    public MarketScope Scope => Choice.Scope;

    public bool IsRemote => Choice.Remote;

    public string Title(in MarketScope scope) =>
        scope.Kind == MarketScopeKind.Region ? RegionTitle(scope.ApiName) : scope.ApiName;

    public string RegionTitle(string apiName)
    {
        var region = Geography.Region(apiName);
        return region is null ? apiName : Loc.T(region.Label);
    }

    public MarketScope LocalScope
    {
        get
        {
            var index = MarketScopes.IndexOfKind(scopes, configuration.MarketScope);
            return index >= 0 ? scopes[index] : MarketScope.None;
        }
    }

    private MarketScopeChoice Choice
    {
        get
        {
            var pinnedName = configuration.MarketPinnedScope;
            if (choiceVersion == scopesVersion && choiceLocalKind == configuration.MarketScope &&
                choicePinnedKind == configuration.MarketPinnedScopeKind &&
                ReferenceEquals(choicePinnedName, pinnedName))
            {
                return choice;
            }

            var catalog = string.IsNullOrEmpty(pinnedName) ? MarketGeography.Empty : Geography;
            choice = MarketScopes.Resolve(scopes, configuration.MarketScope, configuration.MarketPinnedScopeKind,
                pinnedName, catalog);
            choiceVersion = scopesVersion;
            choiceLocalKind = configuration.MarketScope;
            choicePinnedKind = configuration.MarketPinnedScopeKind;
            choicePinnedName = pinnedName;
            return choice;
        }
    }

    public void RefreshScopes()
    {
        var worldId = gameData.LocalCurrentWorldId;
        if (worldId == scopesWorld && scopes.Count > 0)
        {
            return;
        }

        var previousWorld = scopesWorld;
        var previousCount = scopes.Count;
        scopesWorld = worldId;
        MarketScopes.Build(scopes, gameData);
        if (worldId != previousWorld || scopes.Count != previousCount)
        {
            scopesVersion++;
        }
    }

    public void SetScope(int index)
    {
        if (index < 0 || index >= scopes.Count)
        {
            return;
        }

        if (MarketScopes.ChooseLocal(configuration, scopes[index].Kind))
        {
            configuration.Save();
        }
    }

    public void Choose(MarketScopeKind kind, string name)
    {
        if (MarketScopes.Choose(configuration, scopes, kind, name))
        {
            configuration.Save();
        }
    }

    public void ReturnToLocal()
    {
        if (MarketScopes.ClearPin(configuration))
        {
            configuration.Save();
        }
    }

    public bool Contains(uint itemId) => configuration.MarketFavorites.Contains(itemId);

    public bool Toggle(uint itemId)
    {
        var items = configuration.MarketFavorites;
        var added = !items.Remove(itemId);
        if (added)
        {
            items.Insert(0, itemId);
            while (items.Count > MaxItems)
            {
                items.RemoveAt(items.Count - 1);
            }
        }

        configuration.Save();
        return added;
    }

    public void Sync()
    {
        var scope = Scope;
        var items = configuration.MarketFavorites;
        if (!scope.IsValid || items.Count == 0)
        {
            return;
        }

        market.PrefetchAggregated(items, scope);
        market.PrefetchWatchHistory(items, scope);
    }

    public bool Loading(uint itemId) => market.HistoryLoading(itemId, Scope);

    public MarketWatchSeries? SeriesFor(uint itemId)
    {
        var scope = Scope;
        var history = market.WatchHistory(itemId, scope);
        if (history is null)
        {
            return null;
        }

        if (!series.TryGetValue(itemId, out var entry))
        {
            entry = new MarketWatchSeries(itemId);
            series[itemId] = entry;
        }

        if (!ReferenceEquals(entry.Source, history))
        {
            Rebuild(entry, history);
        }

        return entry;
    }

    public long CurrentPrice(uint itemId, MarketWatchSeries? watch)
    {
        var scope = Scope;
        if (watch is { Source: not null } &&
            market.TryGetAggregated(itemId, scope, watch.Hq, out var qualityPrice) && qualityPrice > 0)
        {
            return qualityPrice;
        }

        return market.AggregatedMin(itemId, scope);
    }

    private void Rebuild(MarketWatchSeries entry, MarketHistory history)
    {
        entry.Source = history;
        var trades = history.Trades;
        if (scratch.Length < trades.Length)
        {
            scratch = new long[Math.Max(trades.Length, scratch.Length * 2)];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = now - MarketTrend.WatchSeconds;
        entry.Hq = MarketTrend.DominantHq(trades, from);
        entry.Filled = MarketTrend.Bucket(trades, entry.Hq, from, now, entry.Points, entry.Volumes, scratch);
        entry.Median = MarketTrend.MedianPrice(trades, entry.Hq, from, scratch);
    }
}
