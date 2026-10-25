namespace Aetherphone.Core.Casino.Rolls;

internal sealed partial class RollTable
{
    public int Alive
    {
        get
        {
            var count = 0;
            for (var index = 0; index < players.Count; index++)
            {
                if (!players[index].Out)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int StillToRoll
    {
        get
        {
            var count = 0;
            for (var index = 0; index < players.Count; index++)
            {
                if (Expected(players[index]) && !players[index].Rolled)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool Expected(RollPlayer player) =>
        !player.Out && (Step == RollStep.Everyone || (Step == RollStep.Tiebreak && player.InTie));

    private RollVerdict TakeElimination(in ChatRoll roll)
    {
        var index = IndexOf(roll.Name);
        if (index < 0)
        {
            return RollVerdict.NotInGame;
        }

        var player = players[index];
        if (!Expected(player))
        {
            return RollVerdict.NotTheirTurn;
        }

        if (player.Rolled)
        {
            return RollVerdict.AlreadyRolled;
        }

        if (roll.Max != Settings.Range)
        {
            return RollVerdict.WrongRange;
        }

        player.Roll = roll.Value;
        player.Rolled = true;
        if (StillToRoll == 0)
        {
            ResolveElimination();
        }

        return RollVerdict.Counted;
    }

    private void ResolveElimination()
    {
        var lowest = int.MaxValue;
        var lowestIndex = -1;
        var tied = 0;
        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            if (!Expected(player))
            {
                continue;
            }

            if (player.Roll < lowest)
            {
                lowest = player.Roll;
                lowestIndex = index;
                tied = 1;
            }
            else if (player.Roll == lowest)
            {
                tied++;
            }
        }

        if (lowestIndex < 0)
        {
            return;
        }

        if (tied > 1)
        {
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var inTie = Expected(player) && player.Roll == lowest;
                player.InTie = inTie;
                player.Rolled = !inTie;
            }

            Step = RollStep.Tiebreak;
            TieAt = lowest;
            news.Enqueue(new RollNews(RollNewsKind.Tie, string.Empty, lowest, Settings.Range, tied));
            return;
        }

        Eliminate(lowestIndex, lowest);
    }

    private void Eliminate(int index, int value)
    {
        players[index].Out = true;
        var alive = Alive;
        if (alive <= 1)
        {
            Winner = LastAlive();
            Loser = index;
            Settle(RollOutcome.LastStanding, Winner >= 0 ? players[Winner].Name : string.Empty, value, Settings.Range);
            return;
        }

        news.Enqueue(new RollNews(RollNewsKind.Out, players[index].Name, value, Settings.Range, alive));
        Round++;
        Step = RollStep.Everyone;
        for (var player = 0; player < players.Count; player++)
        {
            players[player].Ready();
        }
    }

    private int LastAlive()
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (!players[index].Out)
            {
                return index;
            }
        }

        return -1;
    }
}
