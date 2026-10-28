namespace Aetherphone.Core.Social;

internal readonly record struct MuteChange(string UserId, bool Muted, bool Restored);

// The mutes made this session, held once for Chirper and Aethergram so a mute in either app relabels
// both menus and clears both feeds at once. Anything not recorded here falls back to the server's flag.
internal sealed class SocialMuteLedger
{
    private static readonly Dictionary<string, bool> NoChanges = new(StringComparer.Ordinal);

    private readonly object gate = new();
    private volatile Dictionary<string, bool> changes = NoChanges;

    public event Action<MuteChange>? Changed;

    public SocialMutedWords Words { get; } = new();

    public bool TryGet(string userId, out bool muted) => changes.TryGetValue(userId, out muted);

    public void Record(string userId, bool muted) => Apply(new MuteChange(userId, muted, false));

    public void Restore(string userId, bool muted) => Apply(new MuteChange(userId, muted, true));

    public void Reset()
    {
        changes = NoChanges;
    }

    private void Apply(MuteChange change)
    {
        lock (gate)
        {
            changes = new Dictionary<string, bool>(changes, StringComparer.Ordinal) { [change.UserId] = change.Muted };
        }

        Changed?.Invoke(change);
    }
}
