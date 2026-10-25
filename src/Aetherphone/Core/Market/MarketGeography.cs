using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Market;

internal sealed class MarketDataCenter
{
    public MarketDataCenter(string name, string region, string[] worlds)
    {
        Name = name;
        Region = region;
        Worlds = worlds;
    }

    public string Name { get; }
    public string Region { get; }
    public string[] Worlds { get; }
}

internal sealed class MarketRegion
{
    public MarketRegion(uint id, string apiName, LocString label, MarketDataCenter[] dataCenters)
    {
        Id = id;
        ApiName = apiName;
        Label = label;
        DataCenters = dataCenters;
    }

    public uint Id { get; }
    public string ApiName { get; }
    public LocString Label { get; }
    public MarketDataCenter[] DataCenters { get; }
}

internal sealed class MarketGeography
{
    private const uint JapanId = 1;
    private const uint NorthAmericaId = 2;
    private const uint EuropeId = 3;
    private const uint OceaniaId = 4;
    private const uint ChinaId = 5;

    private static readonly uint[] RegionOrder = { NorthAmericaId, EuropeId, OceaniaId, JapanId, ChinaId };

    public static readonly MarketGeography Empty = new(Array.Empty<MarketRegion>());

    private readonly Dictionary<string, MarketRegion> regionsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MarketDataCenter> dataCentersByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MarketDataCenter> dataCentersByWorld = new(StringComparer.OrdinalIgnoreCase);

    private MarketGeography(MarketRegion[] regions)
    {
        Regions = regions;
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var region = regions[regionIndex];
            regionsByName[region.ApiName] = region;
            for (var dataCenterIndex = 0; dataCenterIndex < region.DataCenters.Length; dataCenterIndex++)
            {
                var dataCenter = region.DataCenters[dataCenterIndex];
                dataCentersByName[dataCenter.Name] = dataCenter;
                for (var worldIndex = 0; worldIndex < dataCenter.Worlds.Length; worldIndex++)
                {
                    dataCentersByWorld[dataCenter.Worlds[worldIndex]] = dataCenter;
                }
            }
        }
    }

    public MarketRegion[] Regions { get; }

    public static MarketGeography Build(ReadOnlySpan<WorldSheetRow> worldRows,
        ReadOnlySpan<DataCenterSheetRow> dataCenterRows)
    {
        var worldsByDataCenter = new Dictionary<uint, List<string>>();
        for (var worldIndex = 0; worldIndex < worldRows.Length; worldIndex++)
        {
            var world = worldRows[worldIndex];
            if (!world.IsPublic || world.DataCenterId == 0 || world.Name.Length == 0)
            {
                continue;
            }

            if (!worldsByDataCenter.TryGetValue(world.DataCenterId, out var names))
            {
                names = new List<string>();
                worldsByDataCenter[world.DataCenterId] = names;
            }

            names.Add(world.Name);
        }

        var dataCentersByRegion = new Dictionary<uint, List<MarketDataCenter>>();
        for (var dataCenterIndex = 0; dataCenterIndex < dataCenterRows.Length; dataCenterIndex++)
        {
            var row = dataCenterRows[dataCenterIndex];
            var regionName = GameData.RegionNameFromId(row.RegionId);
            if (row.Name.Length == 0 || regionName.Length == 0 ||
                !worldsByDataCenter.TryGetValue(row.Id, out var names))
            {
                continue;
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (!dataCentersByRegion.TryGetValue(row.RegionId, out var dataCenters))
            {
                dataCenters = new List<MarketDataCenter>();
                dataCentersByRegion[row.RegionId] = dataCenters;
            }

            dataCenters.Add(new MarketDataCenter(row.Name, regionName, names.ToArray()));
        }

        var regions = new List<MarketRegion>(RegionOrder.Length);
        for (var orderIndex = 0; orderIndex < RegionOrder.Length; orderIndex++)
        {
            var regionId = RegionOrder[orderIndex];
            if (!dataCentersByRegion.TryGetValue(regionId, out var dataCenters))
            {
                continue;
            }

            dataCenters.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            regions.Add(new MarketRegion(regionId, GameData.RegionNameFromId(regionId), LabelFor(regionId),
                dataCenters.ToArray()));
        }

        return regions.Count == 0 ? Empty : new MarketGeography(regions.ToArray());
    }

    public bool Contains(MarketScopeKind kind, string name) =>
        kind switch
        {
            MarketScopeKind.World => dataCentersByWorld.ContainsKey(name),
            MarketScopeKind.DataCenter => dataCentersByName.ContainsKey(name),
            MarketScopeKind.Region => regionsByName.ContainsKey(name),
            _ => false,
        };

    public MarketDataCenter? DataCenterOfWorld(string world) =>
        dataCentersByWorld.TryGetValue(world, out var dataCenter) ? dataCenter : null;

    public MarketRegion? Region(string apiName) =>
        regionsByName.TryGetValue(apiName, out var region) ? region : null;

    public string ParentOf(MarketScopeKind kind, string name)
    {
        if (kind == MarketScopeKind.World)
        {
            return DataCenterOfWorld(name)?.Name ?? string.Empty;
        }

        if (kind == MarketScopeKind.DataCenter && dataCentersByName.TryGetValue(name, out var dataCenter))
        {
            return dataCenter.Region;
        }

        return string.Empty;
    }

    private static LocString LabelFor(uint regionId) =>
        regionId switch
        {
            JapanId => L.Venues.RegionJapan,
            EuropeId => L.Venues.RegionEurope,
            OceaniaId => L.Venues.RegionOceania,
            ChinaId => L.News.RegionChina,
            _ => L.Venues.RegionNorthAmerica,
        };
}
