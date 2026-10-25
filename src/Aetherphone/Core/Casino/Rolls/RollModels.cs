namespace Aetherphone.Core.Casino.Rolls;

internal enum RollGameKind : byte
{
    Deathroll,
    Elimination,
    HigherLower,
    CoinFlip,
}

internal enum RollPhase : byte
{
    Closed,
    Joining,
    Playing,
    Settled,
}

internal enum RollStep : byte
{
    None,
    PlayerRolls,
    HouseRolls,
    AwaitCall,
    Everyone,
    Tiebreak,
}

internal enum RollCall : byte
{
    None,
    Higher,
    Lower,
    Heads,
    Tails,
}

internal enum RollOutcome : byte
{
    None,
    PlayerWins,
    HouseWins,
    Push,
    LastStanding,
    Duel,
}

internal enum RollVerdict : byte
{
    Counted,
    Joined,
    AlreadyIn,
    TableFull,
    NotInGame,
    NotTheirTurn,
    WrongRange,
    AlreadyRolled,
    NotWaiting,
}

internal enum RollNewsKind : byte
{
    Joined,
    Started,
    Turn,
    Out,
    Tie,
    Base,
    Called,
    Settled,
}

internal readonly record struct RollSettings(
    RollGameKind Kind,
    int Range,
    long Bet,
    int PayTenths,
    bool AgainstHouse,
    bool HouseFlips,
    string Channel);

internal readonly record struct RollLogEntry(string Name, int Value, int Max, RollVerdict Verdict, bool House);

internal readonly record struct RollNews(RollNewsKind Kind, string Name, int Value, int Max, int Count);

internal sealed class RollPlayer
{
    public RollPlayer(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool Out { get; set; }

    public bool Rolled { get; set; }

    public int Roll { get; set; }

    public bool InTie { get; set; }

    public void Ready()
    {
        Rolled = false;
        InTie = false;
    }

    public void Reset()
    {
        Out = false;
        Rolled = false;
        Roll = 0;
        InTie = false;
    }
}
