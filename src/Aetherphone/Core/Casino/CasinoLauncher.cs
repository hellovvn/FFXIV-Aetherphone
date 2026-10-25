namespace Aetherphone.Core.Casino;

internal enum CasinoLaunchKind
{
    Floor,
    Table,
    Game,
    Ledger,
    Code,
}

internal readonly record struct CasinoLaunch(CasinoLaunchKind Kind, string TableId = "", string GameId = "",
    string Code = "");

internal sealed class CasinoLauncher
{
    public const string LedgerGroupPrefix = "casino:ledger:";
    public const string CodeGroupPrefix = "casino:code:";

    private CasinoLaunch? pending;

    public bool HasPending => pending is not null;

    public static string LedgerGroupKey(string tableId) => string.Concat(LedgerGroupPrefix, tableId);

    public static string CodeGroupKey(string code) => string.Concat(CodeGroupPrefix, code);

    public static bool RoutesWithoutCard(string tableId) =>
        CasinoRoomIds.IsBlackjackHouse(tableId) || HoldemRules.IsHouseRoom(tableId);

    public void RequestGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Game, GameId: gameId);
    }

    public void RequestTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Table, tableId);
    }

    public void RequestLedger(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Ledger, tableId);
    }

    public void RequestCode(string raw)
    {
        var code = CasinoRoomCodes.Normalized(raw);
        if (code.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Code, Code: code);
    }

    public void RequestNotification(string groupKey)
    {
        if (groupKey.StartsWith(LedgerGroupPrefix, StringComparison.Ordinal))
        {
            RequestLedger(groupKey[LedgerGroupPrefix.Length..]);
            return;
        }

        if (groupKey.StartsWith(CodeGroupPrefix, StringComparison.Ordinal))
        {
            RequestCode(groupKey[CodeGroupPrefix.Length..]);
            return;
        }

        if (groupKey.StartsWith(CasinoTurnNotifier.GroupPrefix, StringComparison.Ordinal))
        {
            RequestTable(groupKey[CasinoTurnNotifier.GroupPrefix.Length..]);
        }
    }

    public bool TryConsume(bool stateLoaded, out CasinoLaunch launch)
    {
        if (pending is not { } held || (held.Kind == CasinoLaunchKind.Game && !stateLoaded))
        {
            launch = default;
            return false;
        }

        pending = null;
        launch = held;
        return true;
    }
}
