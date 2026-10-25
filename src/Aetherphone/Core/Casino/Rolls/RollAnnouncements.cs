using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino.Rolls;

internal static class RollAnnouncements
{
    public static LocString GameName(RollGameKind kind) => kind switch
    {
        RollGameKind.Deathroll => L.Venue.GameDeathroll,
        RollGameKind.Elimination => L.Rolls.GameElimination,
        RollGameKind.HigherLower => L.Rolls.GameHigherLower,
        _ => L.Rolls.GameCoinFlip,
    };

    public static string JoinCall(RollGameKind kind) => Loc.T(L.Rolls.ChatJoinCall, Loc.T(GameName(kind)));

    public static string Line(RollTable table, in RollNews news, string houseName) => news.Kind switch
    {
        RollNewsKind.Started => Started(table, news),
        RollNewsKind.Turn => Loc.T(L.Rolls.ChatTurn, news.Count > 0 ? houseName : news.Name, Number(news.Value)),
        RollNewsKind.Out => Out(news),
        RollNewsKind.Tie => Loc.T(L.Rolls.ChatTie, Number(news.Value), TiedNames(table), Number(news.Max)),
        RollNewsKind.Base => Loc.T(L.Rolls.ChatBase, Number(news.Value), news.Name),
        RollNewsKind.Called => Called(table, news, houseName),
        RollNewsKind.Settled => Settled(table, news, houseName),
        _ => string.Empty,
    };

    public static string Gil(long amount) => amount.ToString("N0", Loc.Culture);

    private static string Number(int value) => value.ToString(Loc.Culture);

    private static string Started(RollTable table, in RollNews news)
    {
        var game = Loc.T(GameName(table.Kind));
        return table.Kind switch
        {
            RollGameKind.Deathroll when table.Settings.AgainstHouse => Loc.T(L.Rolls.ChatStartHouseDeathroll, game,
                Number(news.Value), news.Name),
            RollGameKind.Deathroll => Loc.T(L.Rolls.ChatStartDuel, game, Number(news.Value), news.Name,
                table.PlayerCount > 1 ? table.PlayerAt(1).Name : string.Empty),
            RollGameKind.Elimination => Loc.T(L.Rolls.ChatStartElimination, game, Number(news.Count),
                Number(news.Value)),
            RollGameKind.HigherLower => Loc.T(L.Rolls.ChatStartHigherLower, game, news.Name),
            _ => Loc.T(L.Rolls.ChatStartCoin, game, news.Name),
        };
    }

    private static string Out(in RollNews news) => news.Value > 0
        ? Loc.T(L.Rolls.ChatOut, news.Name, Number(news.Value), Number(news.Count), Number(news.Max))
        : Loc.T(L.Rolls.ChatDropped, news.Name, Number(news.Count), Number(news.Max));

    private static string Called(RollTable table, in RollNews news, string houseName)
    {
        var word = Loc.T(CallWord((RollCall)news.Value));
        var roller = Flipper(table, houseName);
        return string.Equals(roller, news.Name, StringComparison.Ordinal)
            ? Loc.T(L.Rolls.ChatCalledRoll, news.Name, word, Number(news.Max))
            : Loc.T(L.Rolls.ChatCalled, news.Name, word, roller, Number(news.Max));
    }

    private static string Flipper(RollTable table, string houseName) =>
        table.Step == RollStep.HouseRolls ? houseName : table.PlayerCount > 0 ? table.PlayerAt(0).Name : string.Empty;

    private static LocString CallWord(RollCall call) => call switch
    {
        RollCall.Higher => L.Rolls.WordHigher,
        RollCall.Lower => L.Rolls.WordLower,
        RollCall.Heads => L.Rolls.WordHeads,
        _ => L.Rolls.WordTails,
    };

    private static string TiedNames(RollTable table)
    {
        var names = new List<string>(table.PlayerCount);
        for (var index = 0; index < table.PlayerCount; index++)
        {
            var player = table.PlayerAt(index);
            if (player.InTie && !player.Out)
            {
                names.Add(player.Name);
            }
        }

        return string.Join(", ", names);
    }

    private static string Settled(RollTable table, in RollNews news, string houseName)
    {
        var outcome = (RollOutcome)news.Count;
        var bet = table.Settings.Bet;
        return outcome switch
        {
            RollOutcome.LastStanding => bet > 0
                ? Loc.T(L.Rolls.ChatLastStandingGil, news.Name, Gil(table.Payout))
                : Loc.T(L.Rolls.ChatLastStanding, news.Name),
            RollOutcome.Duel => string.Concat(
                Loc.T(L.Rolls.ChatRolledOne, table.Loser >= 0 ? table.PlayerAt(table.Loser).Name : string.Empty), " ",
                Win(news.Name, bet > 0 ? table.Payout : 0)),
            RollOutcome.Push => Loc.T(L.Rolls.ChatPush, news.Name, Number(news.Value)),
            _ => string.Concat(Lead(table, news, houseName, outcome), " ",
                outcome == RollOutcome.PlayerWins ? Win(news.Name, bet > 0 ? table.Payout : 0)
                    : Loc.T(L.Rolls.ChatHouseWins)),
        };
    }

    private static string Lead(RollTable table, in RollNews news, string houseName, RollOutcome outcome)
    {
        return table.Kind switch
        {
            RollGameKind.CoinFlip => Loc.T(news.Value == RollTable.Heads ? L.Rolls.ChatHeads : L.Rolls.ChatTails),
            RollGameKind.Deathroll when outcome == RollOutcome.PlayerWins => Loc.T(L.Rolls.ChatHouseRolledOne),
            RollGameKind.Deathroll => Loc.T(L.Rolls.ChatRolledOne, news.Name),
            _ => Loc.T(L.Rolls.ChatRolled, news.Name, Number(news.Value)),
        };
    }

    private static string Win(string name, long amount) =>
        amount > 0 ? Loc.T(L.Rolls.ChatWinGil, name, Gil(amount)) : Loc.T(L.Rolls.ChatWin, name);
}
