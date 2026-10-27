using Aetherphone.Core;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Rolls;

internal enum RollRowAction : byte
{
    None,
    Remove,
    Drop,
}

internal readonly record struct RollPlayerRow(string Name, string Status, bool Lit, RollRowAction Action);

internal readonly record struct RollLogRow(string Name, string Value, string Range, string Verdict, bool Counted,
    bool House);

internal sealed class RollGameView
{
    private const string Separator = " · ";

    private readonly RollPlayerRow[] players = new RollPlayerRow[RollTable.MaxPlayers + 1];
    private readonly RollLogRow[] log = new RollLogRow[RollTable.LogCapacity];
    private int builtVersion = -1;
    private LanguageInfo? builtLanguage;
    private string builtHouse = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string Prompt { get; private set; } = string.Empty;

    public int PlayerRows { get; private set; }

    public int LogRows { get; private set; }

    public RollPlayerRow PlayerAt(int index) => players[index];

    public RollLogRow LogAt(int index) => log[index];

    public void Sync(RollTable table, string houseName)
    {
        if (table.Version == builtVersion && ReferenceEquals(builtLanguage, Loc.Current)
            && string.Equals(builtHouse, houseName, StringComparison.Ordinal))
        {
            return;
        }

        builtVersion = table.Version;
        builtLanguage = Loc.Current;
        builtHouse = houseName;
        Summary = SummaryOf(table);
        Prompt = PromptOf(table, houseName);
        BuildPlayers(table, houseName);
        BuildLog(table);
    }

    public static string WinnerName(RollTable table) => table.Winner >= 0 && table.Winner < table.PlayerCount
        ? table.PlayerAt(table.Winner).Name
        : string.Empty;

    private static string SummaryOf(RollTable table)
    {
        var settings = table.Settings;
        var parts = new List<string>(4);
        parts.Add(Loc.T(L.Rolls.RolledOutOf, NumberText.Group(table.Kind == RollGameKind.CoinFlip
            ? RollTable.CoinRange
            : settings.Range)));
        parts.Add(settings.Bet > 0
            ? Loc.T(L.Tables.GilAmount, NumberText.Group(settings.Bet))
            : Loc.T(L.Venue.NoStake));
        if (table.HouseGame && settings.Bet > 0)
        {
            parts.Add(RollGames.PaysText(settings.PayTenths));
        }

        var channel = Array.IndexOf(RollGames.ChannelKeys, settings.Channel);
        if (channel > 0)
        {
            parts.Add(Loc.T(RollGames.ChannelTitles[channel]));
        }

        return string.Join(Separator, parts);
    }

    private static string PromptOf(RollTable table, string houseName)
    {
        var first = table.PlayerCount > 0 ? table.PlayerAt(0).Name : string.Empty;
        switch (table.Phase)
        {
            case RollPhase.Joining:
                return Loc.T(table.CanStart ? L.Rolls.PromptReady : L.Rolls.PromptJoin);
            case RollPhase.Settled:
                return Result(table);
        }

        if (table.WaitsOnHouse)
        {
            return Loc.T(L.Rolls.PromptHouseRoll, NumberText.Group(table.ExpectedMax));
        }

        return table.Kind switch
        {
            RollGameKind.Elimination when table.Step == RollStep.Tiebreak => Loc.T(L.Rolls.PromptTie,
                NumberText.Group(table.TieAt), NumberText.Group(table.Settings.Range)),
            RollGameKind.Elimination => Loc.T(L.Rolls.PromptEveryone, NumberText.Group(table.Round),
                NumberText.Group(table.StillToRoll), NumberText.Group(table.Settings.Range)),
            RollGameKind.HigherLower when table.Step == RollStep.AwaitCall => Loc.T(L.Rolls.PromptCallHigherLower,
                first, NumberText.Group(table.Target)),
            RollGameKind.CoinFlip when table.Step == RollStep.AwaitCall => Loc.T(L.Rolls.PromptCallCoin, first),
            RollGameKind.Deathroll => Loc.T(L.Rolls.PromptTurn,
                table.Turn < table.PlayerCount ? table.PlayerAt(table.Turn).Name : houseName,
                NumberText.Group(table.ExpectedMax)),
            _ => Loc.T(L.Rolls.PromptTurn, first, NumberText.Group(table.ExpectedMax)),
        };
    }

    private static string Result(RollTable table)
    {
        switch (table.Outcome)
        {
            case RollOutcome.HouseWins:
                return Loc.T(L.Rolls.PromptHouseWins);
            case RollOutcome.Push:
                return Loc.T(L.Rolls.PromptPush);
        }

        var name = table.Outcome == RollOutcome.PlayerWins && table.PlayerCount > 0
            ? table.PlayerAt(0).Name
            : WinnerName(table);
        return table.Payout > 0 && table.Outcome != RollOutcome.Push
            ? Loc.T(L.Rolls.PromptWinsGil, name, NumberText.Group(table.Payout))
            : Loc.T(L.Rolls.PromptWins, name);
    }

    private void BuildPlayers(RollTable table, string houseName)
    {
        var count = 0;
        if (table.HouseGame && table.Phase != RollPhase.Joining)
        {
            var houseStatus = table.WaitsOnHouse
                ? Loc.T(L.Rolls.StatusTurn)
                : table.Kind == RollGameKind.HigherLower && table.Target > 0
                    ? NumberText.Group(table.Target)
                    : Loc.T(L.Rolls.HouseTag);
            players[count++] = new RollPlayerRow(houseName, houseStatus, table.WaitsOnHouse, RollRowAction.None);
        }

        for (var index = 0; index < table.PlayerCount && count < players.Length; index++)
        {
            var player = table.PlayerAt(index);
            players[count++] = new RollPlayerRow(player.Name, StatusOf(table, index, player, out var lit), lit,
                ActionOf(table, player));
        }

        PlayerRows = count;
    }

    private static RollRowAction ActionOf(RollTable table, RollPlayer player)
    {
        if (table.Phase == RollPhase.Joining)
        {
            return RollRowAction.Remove;
        }

        return table.Phase == RollPhase.Playing && table.Kind == RollGameKind.Elimination && !player.Out
            ? RollRowAction.Drop
            : RollRowAction.None;
    }

    private static string StatusOf(RollTable table, int index, RollPlayer player, out bool lit)
    {
        lit = false;
        if (table.Phase == RollPhase.Joining)
        {
            return Loc.T(L.Rolls.StatusIn);
        }

        if (table.Phase == RollPhase.Settled && index == table.Winner)
        {
            lit = true;
            return Loc.T(L.Rolls.StatusWinner);
        }

        if (player.Out)
        {
            return Loc.T(L.Rolls.StatusOut);
        }

        if (table.Phase == RollPhase.Playing && Waiting(table, index, player))
        {
            lit = true;
            return Loc.T(player.InTie ? L.Rolls.StatusTie : table.Kind == RollGameKind.Elimination
                ? L.Rolls.StatusToRoll
                : L.Rolls.StatusTurn);
        }

        return player.Rolled ? NumberText.Group(player.Roll) : Loc.T(L.Rolls.StatusIn);
    }

    private static bool Waiting(RollTable table, int index, RollPlayer player) => table.Kind switch
    {
        RollGameKind.Elimination => table.Expected(player) && !player.Rolled,
        RollGameKind.Deathroll => table.Step == RollStep.PlayerRolls && index == table.Turn,
        _ => table.Step == RollStep.PlayerRolls && index == 0,
    };

    private void BuildLog(RollTable table)
    {
        var count = Math.Min(table.LogCount, log.Length);
        for (var index = 0; index < count; index++)
        {
            var entry = table.LogAt(index);
            var name = entry.House ? string.Concat(entry.Name, Separator, Loc.T(L.Rolls.HouseTag)) : entry.Name;
            log[index] = new RollLogRow(name, NumberText.Group(entry.Value),
                Loc.T(L.Rolls.RolledOutOf, NumberText.Group(entry.Max)), VerdictText(entry.Verdict),
                entry.Verdict is RollVerdict.Counted or RollVerdict.Joined, entry.House);
        }

        LogRows = count;
    }

    private static string VerdictText(RollVerdict verdict) => verdict switch
    {
        RollVerdict.Counted => string.Empty,
        RollVerdict.Joined => Loc.T(L.Rolls.VerdictJoined),
        RollVerdict.AlreadyIn => Loc.T(L.Rolls.VerdictAlreadyIn),
        RollVerdict.TableFull => Loc.T(L.Rolls.VerdictTableFull),
        RollVerdict.NotInGame => Loc.T(L.Rolls.VerdictNotInGame),
        RollVerdict.NotTheirTurn => Loc.T(L.Rolls.VerdictNotTheirTurn),
        RollVerdict.WrongRange => Loc.T(L.Rolls.VerdictWrongRange),
        RollVerdict.AlreadyRolled => Loc.T(L.Rolls.VerdictAlreadyRolled),
        _ => Loc.T(L.Rolls.VerdictNotWaiting),
    };
}
