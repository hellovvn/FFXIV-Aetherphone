namespace Aetherphone.Core.Casino.Rolls;

internal sealed partial class RollTable
{
    public const int LogCapacity = 40;
    public const int MaxPlayers = 24;
    public const int CoinRange = 2;
    public const int Heads = 1;

    private readonly List<RollPlayer> players = new(MaxPlayers);
    private readonly RollLogEntry[] log = new RollLogEntry[LogCapacity];
    private readonly Queue<RollNews> news = new();
    private int logCount;
    private int logHead;

    public RollSettings Settings { get; private set; }

    public RollPhase Phase { get; private set; }

    public RollStep Step { get; private set; }

    public RollCall Call { get; private set; }

    public RollOutcome Outcome { get; private set; }

    public int Target { get; private set; }

    public int Turn { get; private set; }

    public int Round { get; private set; }

    public int Winner { get; private set; } = -1;

    public int Loser { get; private set; } = -1;

    public int Entrants { get; private set; }

    public int TieAt { get; private set; }

    public int Serial { get; private set; }

    public int Version { get; private set; }

    public string HouseName { get; private set; } = string.Empty;

    public bool IsOpen => Phase != RollPhase.Closed;

    public RollGameKind Kind => Settings.Kind;

    public int PlayerCount => players.Count;

    public int LogCount => logCount;

    public bool HouseGame => Settings.Kind switch
    {
        RollGameKind.Deathroll => Settings.AgainstHouse,
        RollGameKind.Elimination => false,
        _ => true,
    };

    public int MinPlayers => Settings.Kind switch
    {
        RollGameKind.Deathroll when !Settings.AgainstHouse => 2,
        RollGameKind.Elimination => 2,
        _ => 1,
    };

    public int Capacity => Settings.Kind switch
    {
        RollGameKind.Deathroll => Settings.AgainstHouse ? 1 : 2,
        RollGameKind.Elimination => MaxPlayers,
        _ => 1,
    };

    public bool CanStart => Phase == RollPhase.Joining && players.Count >= MinPlayers;

    public int ExpectedMax => Settings.Kind switch
    {
        RollGameKind.Deathroll => Target,
        RollGameKind.CoinFlip => CoinRange,
        _ => Settings.Range,
    };

    public bool WaitsOnHouse => Phase == RollPhase.Playing && Step == RollStep.HouseRolls;

    public long Pot => Settings.Bet * Entrants;

    public long Payout => Outcome switch
    {
        RollOutcome.PlayerWins => Settings.Bet * Settings.PayTenths / 10,
        RollOutcome.Push => Settings.Bet,
        RollOutcome.LastStanding => Pot,
        RollOutcome.Duel => Settings.Bet,
        _ => 0,
    };

    public RollPlayer PlayerAt(int index) => players[index];

    public RollLogEntry LogAt(int newestFirst) => log[(logHead - 1 - newestFirst + LogCapacity) % LogCapacity];

    public bool TryTakeNews(out RollNews item) => news.TryDequeue(out item);

    public void Open(in RollSettings settings)
    {
        Settings = settings with { Range = Math.Clamp(settings.Range, CoinRange, RollLines.MaxRange) };
        players.Clear();
        news.Clear();
        logCount = 0;
        logHead = 0;
        HouseName = string.Empty;
        BackToJoining();
    }

    public void Close()
    {
        players.Clear();
        news.Clear();
        Phase = RollPhase.Closed;
        Step = RollStep.None;
        Version++;
    }

    public void NewPlayers()
    {
        if (!IsOpen)
        {
            return;
        }

        players.Clear();
        BackToJoining();
    }

    public RollVerdict Take(in ChatRoll roll)
    {
        var verdict = Phase switch
        {
            RollPhase.Joining => Join(roll),
            RollPhase.Playing => Play(roll),
            _ => RollVerdict.NotWaiting,
        };

        Remember(roll, verdict);
        return verdict;
    }

    public bool Start()
    {
        if (!CanStart)
        {
            return false;
        }

        Phase = RollPhase.Playing;
        Entrants = players.Count;
        Serial++;
        TieAt = 0;
        Round = 1;
        Turn = 0;
        Target = 0;
        Call = RollCall.None;
        Outcome = RollOutcome.None;
        Winner = -1;
        Loser = -1;
        for (var index = 0; index < players.Count; index++)
        {
            players[index].Reset();
        }

        switch (Settings.Kind)
        {
            case RollGameKind.Deathroll:
                Target = Settings.Range;
                Step = RollStep.PlayerRolls;
                break;
            case RollGameKind.Elimination:
                Step = RollStep.Everyone;
                break;
            case RollGameKind.HigherLower:
                Step = RollStep.HouseRolls;
                break;
            default:
                Step = RollStep.AwaitCall;
                break;
        }

        news.Enqueue(new RollNews(RollNewsKind.Started, players[0].Name, ExpectedMax, 0, players.Count));
        Version++;
        return true;
    }

    public bool Rematch()
    {
        if (Phase != RollPhase.Settled)
        {
            return false;
        }

        Phase = RollPhase.Joining;
        return Start();
    }

    public bool Drop(int index)
    {
        if (index < 0 || index >= players.Count)
        {
            return false;
        }

        if (Phase == RollPhase.Joining)
        {
            players.RemoveAt(index);
            Version++;
            return true;
        }

        if (Phase != RollPhase.Playing || Settings.Kind != RollGameKind.Elimination || players[index].Out)
        {
            return false;
        }

        Eliminate(index, 0);
        Version++;
        return true;
    }

    public bool Choose(RollCall call)
    {
        if (Phase != RollPhase.Playing || Step != RollStep.AwaitCall || !Accepts(call))
        {
            return false;
        }

        Call = call;
        Step = Settings.Kind == RollGameKind.CoinFlip && Settings.HouseFlips ? RollStep.HouseRolls : RollStep.PlayerRolls;
        news.Enqueue(new RollNews(RollNewsKind.Called, players[0].Name, (int)call, ExpectedMax, 0));
        Version++;
        return true;
    }

    public int IndexOf(string name)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (string.Equals(players[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private bool Accepts(RollCall call) => Settings.Kind switch
    {
        RollGameKind.HigherLower => call is RollCall.Higher or RollCall.Lower,
        RollGameKind.CoinFlip => call is RollCall.Heads or RollCall.Tails,
        _ => false,
    };

    private void BackToJoining()
    {
        Phase = RollPhase.Joining;
        Step = RollStep.None;
        Call = RollCall.None;
        Outcome = RollOutcome.None;
        Target = 0;
        Turn = 0;
        Round = 0;
        Winner = -1;
        Loser = -1;
        Entrants = 0;
        Version++;
    }

    private RollVerdict Join(in ChatRoll roll)
    {
        if (roll.Mine && HouseGame)
        {
            return RollVerdict.NotInGame;
        }

        if (IndexOf(roll.Name) >= 0)
        {
            return RollVerdict.AlreadyIn;
        }

        if (players.Count >= Capacity)
        {
            return RollVerdict.TableFull;
        }

        players.Add(new RollPlayer(roll.Name));
        news.Enqueue(new RollNews(RollNewsKind.Joined, roll.Name, 0, 0, players.Count));
        return RollVerdict.Joined;
    }

    private RollVerdict Play(in ChatRoll roll) => Settings.Kind switch
    {
        RollGameKind.Deathroll => TakeDeathroll(roll),
        RollGameKind.Elimination => TakeElimination(roll),
        RollGameKind.HigherLower => TakeHigherLower(roll),
        _ => TakeCoinFlip(roll),
    };

    private RollVerdict Misplaced(in ChatRoll roll) =>
        roll.Mine || IndexOf(roll.Name) >= 0 ? RollVerdict.NotTheirTurn : RollVerdict.NotInGame;

    private void Settle(RollOutcome outcome, string name, int value, int against)
    {
        Outcome = outcome;
        Phase = RollPhase.Settled;
        Step = RollStep.None;
        news.Enqueue(new RollNews(RollNewsKind.Settled, name, value, against, (int)outcome));
    }

    private void Remember(in ChatRoll roll, RollVerdict verdict)
    {
        log[logHead] = new RollLogEntry(roll.Name, roll.Value, roll.Max, verdict, roll.Mine && HouseGame);
        logHead = (logHead + 1) % LogCapacity;
        logCount = Math.Min(logCount + 1, LogCapacity);
        Version++;
    }
}
