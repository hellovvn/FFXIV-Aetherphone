using Aetherphone.Core.Game;

namespace Aetherphone.Core.Market;

internal enum MarketScopeKind : byte
{
    World,
    DataCenter,
    Region,
}

internal readonly struct MarketScope
{
    public readonly MarketScopeKind Kind;
    public readonly string ApiName;
    public readonly string Label;

    public MarketScope(MarketScopeKind kind, string apiName, string label)
    {
        Kind = kind;
        ApiName = apiName;
        Label = label;
    }

    public bool IsValid => ApiName.Length > 0;
    public bool IsMultiWorld => Kind != MarketScopeKind.World;
    public string Key => $"{(byte)Kind}:{ApiName}";
    public static readonly MarketScope None = new(MarketScopeKind.World, string.Empty, "Unknown");
}

internal readonly struct MarketScopeChoice
{
    public readonly MarketScope Scope;
    public readonly int LocalIndex;
    public readonly bool Remote;

    public MarketScopeChoice(MarketScope scope, int localIndex, bool remote)
    {
        Scope = scope;
        LocalIndex = localIndex;
        Remote = remote;
    }

    public static readonly MarketScopeChoice None = new(MarketScope.None, -1, false);
}

internal static class MarketScopes
{
    public static void Build(List<MarketScope> scopes, GameData gameData)
    {
        scopes.Clear();
        var worldId = gameData.LocalCurrentWorldId;
        if (worldId == 0)
        {
            return;
        }

        var world = gameData.WorldName(worldId);
        if (world.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.World, world, "World"));
        }

        var dataCenter = gameData.DataCenterName(worldId);
        if (dataCenter.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.DataCenter, dataCenter, "Data Center"));
        }

        var region = gameData.RegionName(worldId);
        if (region.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.Region, region, "Region"));
        }
    }

    public static int IndexOfKind(IReadOnlyList<MarketScope> scopes, MarketScopeKind kind)
    {
        var index = IndexOfExactKind(scopes, kind);
        if (index >= 0)
        {
            return index;
        }

        return scopes.Count > 0 ? 0 : -1;
    }

    public static MarketScopeChoice Resolve(IReadOnlyList<MarketScope> local, MarketScopeKind localKind,
        MarketScopeKind pinnedKind, string pinnedName, MarketGeography geography)
    {
        if (!string.IsNullOrEmpty(pinnedName) && geography.Contains(pinnedKind, pinnedName))
        {
            var match = IndexOfName(local, pinnedKind, pinnedName);
            return match >= 0
                ? new MarketScopeChoice(local[match], match, false)
                : new MarketScopeChoice(new MarketScope(pinnedKind, pinnedName, pinnedName), -1, true);
        }

        var index = IndexOfKind(local, localKind);
        return index >= 0 ? new MarketScopeChoice(local[index], index, false) : MarketScopeChoice.None;
    }

    public static bool ChooseLocal(Configuration configuration, MarketScopeKind kind)
    {
        if (configuration.MarketScope == kind && string.IsNullOrEmpty(configuration.MarketPinnedScope))
        {
            return false;
        }

        configuration.MarketScope = kind;
        configuration.MarketPinnedScope = string.Empty;
        return true;
    }

    public static bool Choose(Configuration configuration, IReadOnlyList<MarketScope> local, MarketScopeKind kind,
        string name)
    {
        if (IndexOfName(local, kind, name) >= 0)
        {
            return ChooseLocal(configuration, kind);
        }

        if (configuration.MarketPinnedScopeKind == kind &&
            string.Equals(configuration.MarketPinnedScope, name, StringComparison.Ordinal))
        {
            return false;
        }

        configuration.MarketPinnedScopeKind = kind;
        configuration.MarketPinnedScope = name;
        return true;
    }

    public static bool ClearPin(Configuration configuration)
    {
        if (string.IsNullOrEmpty(configuration.MarketPinnedScope))
        {
            return false;
        }

        configuration.MarketPinnedScope = string.Empty;
        return true;
    }

    private static int IndexOfExactKind(IReadOnlyList<MarketScope> scopes, MarketScopeKind kind)
    {
        for (var index = 0; index < scopes.Count; index++)
        {
            if (scopes[index].Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfName(IReadOnlyList<MarketScope> scopes, MarketScopeKind kind, string name)
    {
        var index = IndexOfExactKind(scopes, kind);
        return index >= 0 && string.Equals(scopes[index].ApiName, name, StringComparison.OrdinalIgnoreCase)
            ? index
            : -1;
    }
}
