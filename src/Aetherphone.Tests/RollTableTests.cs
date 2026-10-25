using Aetherphone.Core.Casino.Rolls;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RollTableTests
{
    private const string Host = "Host Person";
    private const string Ana = "Ana Bell";
    private const string Bo = "Bo Raine";
    private const string Cy = "Cy Dorn";

    private static ChatRoll Roll(string name, int value, int max) => new(name, value, max, name == Host);

    private static RollTable Opened(RollGameKind kind, int range = 999, long bet = 0, bool againstHouse = false,
        bool houseFlips = false)
    {
        var table = new RollTable();
        table.Open(new RollSettings(kind, range, bet, 20, againstHouse, houseFlips, string.Empty));
        return table;
    }

    private static List<RollNews> News(RollTable table)
    {
        var all = new List<RollNews>();
        while (table.TryTakeNews(out var item))
        {
            all.Add(item);
        }

        return all;
    }

    [Fact]
    public void ADeathrollDuelPassesTheRangeDownUntilSomeoneRollsOne()
    {
        var table = Opened(RollGameKind.Deathroll, 100, 5000);
        Assert.Equal(RollVerdict.Joined, table.Take(Roll(Ana, 3, 999)));
        Assert.Equal(RollVerdict.Joined, table.Take(Roll(Bo, 8, 999)));
        Assert.Equal(RollVerdict.TableFull, table.Take(Roll(Cy, 8, 999)));
        Assert.True(table.CanStart);
        table.Start();

        Assert.Equal(RollVerdict.NotTheirTurn, table.Take(Roll(Bo, 40, 100)));
        Assert.Equal(RollVerdict.WrongRange, table.Take(Roll(Ana, 40, 999)));
        Assert.Equal(RollVerdict.Counted, table.Take(Roll(Ana, 40, 100)));
        Assert.Equal(40, table.ExpectedMax);
        Assert.Equal(RollVerdict.Counted, table.Take(Roll(Bo, 1, 40)));

        Assert.Equal(RollPhase.Settled, table.Phase);
        Assert.Equal(RollOutcome.Duel, table.Outcome);
        Assert.Equal(Ana, table.PlayerAt(table.Winner).Name);
        Assert.Equal(5000, table.Payout);
    }

    [Fact]
    public void ADeathrollAgainstTheHouseTakesTheHostsRollsAsTheHouse()
    {
        var table = Opened(RollGameKind.Deathroll, 50, 1000, againstHouse: true);
        Assert.Equal(RollVerdict.NotInGame, table.Take(Roll(Host, 7, 999)));
        table.Take(Roll(Ana, 7, 999));
        table.Start();

        table.Take(Roll(Ana, 20, 50));
        Assert.True(table.WaitsOnHouse);
        Assert.Equal(RollVerdict.NotTheirTurn, table.Take(Roll(Ana, 3, 20)));
        Assert.Equal(RollVerdict.Counted, table.Take(Roll(Host, 1, 20)));

        Assert.Equal(RollOutcome.PlayerWins, table.Outcome);
        Assert.Equal(2000, table.Payout);
    }

    [Fact]
    public void EliminationDropsTheLowestEachRoundAndRerollsTies()
    {
        var table = Opened(RollGameKind.Elimination, 100, 10_000);
        table.Take(Roll(Ana, 1, 999));
        table.Take(Roll(Bo, 1, 999));
        table.Take(Roll(Cy, 1, 999));
        table.Start();
        News(table);

        table.Take(Roll(Ana, 50, 100));
        Assert.Equal(RollVerdict.AlreadyRolled, table.Take(Roll(Ana, 60, 100)));
        table.Take(Roll(Bo, 10, 100));
        table.Take(Roll(Cy, 10, 100));
        Assert.Equal(RollStep.Tiebreak, table.Step);
        Assert.Equal(RollVerdict.NotTheirTurn, table.Take(Roll(Ana, 5, 100)));

        table.Take(Roll(Bo, 70, 100));
        table.Take(Roll(Cy, 30, 100));
        Assert.True(table.PlayerAt(2).Out);
        Assert.Equal(2, table.Round);
        Assert.Equal(RollStep.Everyone, table.Step);

        table.Take(Roll(Ana, 90, 100));
        table.Take(Roll(Bo, 20, 100));

        Assert.Equal(RollOutcome.LastStanding, table.Outcome);
        Assert.Equal(Ana, table.PlayerAt(table.Winner).Name);
        Assert.Equal(30_000, table.Payout);
        var kinds = News(table).ConvertAll(item => item.Kind);
        Assert.Equal(new[] { RollNewsKind.Tie, RollNewsKind.Out, RollNewsKind.Settled }, kinds);
    }

    [Fact]
    public void DroppingAnIdlePlayerEndsTheirRoundForEveryone()
    {
        var table = Opened(RollGameKind.Elimination);
        table.Take(Roll(Ana, 1, 999));
        table.Take(Roll(Bo, 1, 999));
        table.Take(Roll(Cy, 1, 999));
        table.Start();
        table.Take(Roll(Ana, 400, 999));

        Assert.True(table.Drop(1));
        Assert.Equal(2, table.Alive);
        Assert.False(table.PlayerAt(0).Rolled);
        Assert.Equal(2, table.Round);
    }

    [Theory]
    [InlineData((byte)RollCall.Higher, 80, (byte)RollOutcome.PlayerWins)]
    [InlineData((byte)RollCall.Higher, 20, (byte)RollOutcome.HouseWins)]
    [InlineData((byte)RollCall.Lower, 20, (byte)RollOutcome.PlayerWins)]
    [InlineData((byte)RollCall.Lower, 50, (byte)RollOutcome.Push)]
    public void HigherOrLowerComparesThePlayersRollWithTheHouses(byte callValue, int value, byte expectedValue)
    {
        var call = (RollCall)callValue;
        var expected = (RollOutcome)expectedValue;
        var table = Opened(RollGameKind.HigherLower, 100, 1000);
        table.Take(Roll(Ana, 9, 999));
        table.Start();
        Assert.Equal(RollVerdict.NotTheirTurn, table.Take(Roll(Ana, 30, 100)));
        table.Take(Roll(Host, 50, 100));
        Assert.Equal(RollStep.AwaitCall, table.Step);
        Assert.False(table.Choose(RollCall.Heads));
        Assert.True(table.Choose(call));

        Assert.Equal(RollVerdict.Counted, table.Take(Roll(Ana, value, 100)));
        Assert.Equal(expected, table.Outcome);
    }

    [Theory]
    [InlineData((byte)RollCall.Heads, 1, false, (byte)RollOutcome.PlayerWins)]
    [InlineData((byte)RollCall.Tails, 1, false, (byte)RollOutcome.HouseWins)]
    [InlineData((byte)RollCall.Tails, 2, true, (byte)RollOutcome.PlayerWins)]
    public void ACoinFlipIsSlashRandomTwo(byte callValue, int value, bool houseFlips, byte expectedValue)
    {
        var call = (RollCall)callValue;
        var expected = (RollOutcome)expectedValue;
        var table = Opened(RollGameKind.CoinFlip, bet: 500, houseFlips: houseFlips);
        table.Take(Roll(Ana, 4, 999));
        table.Start();
        table.Choose(call);
        var flipper = houseFlips ? Host : Ana;

        Assert.Equal(RollVerdict.WrongRange, table.Take(Roll(flipper, 1, 6)));
        Assert.Equal(RollVerdict.Counted, table.Take(Roll(flipper, value, 2)));
        Assert.Equal(expected, table.Outcome);
    }

    [Fact]
    public void ARematchKeepsThePlayersAndClearsTheBoard()
    {
        var table = Opened(RollGameKind.CoinFlip);
        table.Take(Roll(Ana, 4, 999));
        table.Start();
        table.Choose(RollCall.Heads);
        table.Take(Roll(Ana, 2, 2));

        Assert.True(table.Rematch());
        Assert.Equal(RollStep.AwaitCall, table.Step);
        Assert.Equal(RollOutcome.None, table.Outcome);
        Assert.Equal(1, table.PlayerCount);
    }

    [Fact]
    public void TheLogKeepsTheNewestRollsFirst()
    {
        var table = Opened(RollGameKind.Elimination);
        for (var index = 0; index < RollTable.LogCapacity + 3; index++)
        {
            table.Take(Roll(Ana, index + 1, 999));
        }

        Assert.Equal(RollTable.LogCapacity, table.LogCount);
        Assert.Equal(RollTable.LogCapacity + 3, table.LogAt(0).Value);
    }
}
