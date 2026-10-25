namespace Aetherphone.Core.Casino.Rolls;

internal sealed partial class RollTable
{
    private RollVerdict TakeHigherLower(in ChatRoll roll)
    {
        if (Step == RollStep.HouseRolls)
        {
            if (!roll.Mine)
            {
                return Misplaced(roll);
            }

            if (roll.Max != Settings.Range)
            {
                return RollVerdict.WrongRange;
            }

            HouseName = roll.Name;
            Target = roll.Value;
            Step = RollStep.AwaitCall;
            news.Enqueue(new RollNews(RollNewsKind.Base, players[0].Name, Target, Settings.Range, 0));
            return RollVerdict.Counted;
        }

        if (Step != RollStep.PlayerRolls || IndexOf(roll.Name) != 0)
        {
            return Misplaced(roll);
        }

        if (roll.Max != Settings.Range)
        {
            return RollVerdict.WrongRange;
        }

        var player = players[0];
        player.Roll = roll.Value;
        player.Rolled = true;
        var outcome = roll.Value == Target
            ? RollOutcome.Push
            : (Call == RollCall.Higher) == (roll.Value > Target) ? RollOutcome.PlayerWins : RollOutcome.HouseWins;
        Winner = outcome == RollOutcome.PlayerWins ? 0 : -1;
        Settle(outcome, player.Name, roll.Value, Target);
        return RollVerdict.Counted;
    }

    private RollVerdict TakeCoinFlip(in ChatRoll roll)
    {
        var house = Step == RollStep.HouseRolls;
        var flipper = house ? roll.Mine : Step == RollStep.PlayerRolls && IndexOf(roll.Name) == 0;
        if (!flipper)
        {
            return Misplaced(roll);
        }

        if (roll.Max != CoinRange)
        {
            return RollVerdict.WrongRange;
        }

        if (house)
        {
            HouseName = roll.Name;
        }
        else
        {
            players[0].Roll = roll.Value;
            players[0].Rolled = true;
        }

        Target = roll.Value;
        var heads = roll.Value == Heads;
        var outcome = (Call == RollCall.Heads) == heads ? RollOutcome.PlayerWins : RollOutcome.HouseWins;
        Winner = outcome == RollOutcome.PlayerWins ? 0 : -1;
        Settle(outcome, players[0].Name, roll.Value, CoinRange);
        return RollVerdict.Counted;
    }
}
