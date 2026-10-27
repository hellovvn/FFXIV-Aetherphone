namespace Aetherphone.Core.Casino.Rolls;

internal sealed partial class RollTable
{
    private RollVerdict TakeDeathroll(in ChatRoll roll)
    {
        var house = Step == RollStep.HouseRolls;
        var index = house ? -1 : IndexOf(roll.Name);
        if (house ? !roll.Mine : index != Turn)
        {
            return Misplaced(roll);
        }

        if (roll.Max != Target)
        {
            return RollVerdict.WrongRange;
        }

        if (house)
        {
            HouseName = roll.Name;
        }
        else
        {
            players[index].Roll = roll.Value;
            players[index].Rolled = true;
        }

        if (roll.Value <= 1)
        {
            SettleDeathroll(house, roll);
            return RollVerdict.Counted;
        }

        Target = roll.Value;
        string next;
        if (Settings.AgainstHouse)
        {
            Step = house ? RollStep.PlayerRolls : RollStep.HouseRolls;
            next = house ? players[0].Name : HouseName;
        }
        else
        {
            Turn = (Turn + 1) % players.Count;
            next = players[Turn].Name;
        }

        news.Enqueue(new RollNews(RollNewsKind.Turn, next, Target, roll.Value, Step == RollStep.HouseRolls ? 1 : 0));
        return RollVerdict.Counted;
    }

    private void SettleDeathroll(bool house, in ChatRoll roll)
    {
        if (Settings.AgainstHouse)
        {
            Winner = house ? 0 : -1;
            Loser = house ? -1 : 0;
            Settle(house ? RollOutcome.PlayerWins : RollOutcome.HouseWins, players[0].Name, roll.Value, Target);
            return;
        }

        Loser = Turn;
        Winner = (Turn + 1) % players.Count;
        Settle(RollOutcome.Duel, players[Winner].Name, roll.Value, Target);
    }
}
