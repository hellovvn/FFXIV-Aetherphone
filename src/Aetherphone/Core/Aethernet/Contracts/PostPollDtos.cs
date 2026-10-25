namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record PostPollInput(string[] Options, int DurationHours);

internal sealed record PostPollDto(
    string[] Options,
    int[]? Counts = null,
    int TotalVotes = 0,
    int MyVote = -1,
    long EndsAtUnix = 0,
    bool Ended = false);
