using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

// The account's muted words, held once for Chirper and Aethergram so a word muted in either app clears both apps'
// feeds at once. The server filters everything it sends; this list only serves the Muted words screen and the
// immediate sweep of what is already on screen.
internal sealed class SocialMutedWords
{
    private readonly object gate = new();
    private volatile MutedWordDto[] entries = Array.Empty<MutedWordDto>();
    private volatile bool loaded;
    private volatile bool loading;
    private volatile bool failed;
    private int generation;

    public event Action<MutedWordMatcher>? Applied;

    public event Action? Reverted;

    public MutedWordDto[] Entries => entries;

    public bool Loaded => loaded;

    public bool Loading => loading;

    public bool Failed => failed;

    public int Generation => Volatile.Read(ref generation);

    public static MutedWordRule RuleFor(SaveMutedWordRequest request) =>
        new(request.Phrase, request.InFeeds, request.InNotifications,
            request.Audience == MutedWordAudiences.NotFollowing, null);

    public bool TryBeginLoad()
    {
        lock (gate)
        {
            if (loading)
            {
                return false;
            }

            loading = true;
            return true;
        }
    }

    public void Load(MutedWordDto[]? items)
    {
        if (items is null)
        {
            failed = true;
            return;
        }

        lock (gate)
        {
            entries = items;
            failed = false;
            loaded = true;
            Bump();
        }
    }

    public void EndLoad()
    {
        loading = false;
    }

    public void Apply(SaveMutedWordRequest request)
    {
        var matcher = MutedWordMatcher.Compile(new[] { RuleFor(request) }, 0);
        if (!matcher.IsEmpty)
        {
            Applied?.Invoke(matcher);
        }
    }

    public void Saved(MutedWordDto entry)
    {
        lock (gate)
        {
            var current = entries;
            var replaced = CopyOnWrite.Replace(current, entry);
            entries = ReferenceEquals(replaced, current) ? CopyOnWrite.Prepend(current, entry) : replaced;
            Bump();
        }
    }

    public void Removed(string wordId)
    {
        lock (gate)
        {
            entries = CopyOnWrite.RemoveById(entries, wordId);
            Bump();
        }
    }

    public void Revert()
    {
        Reverted?.Invoke();
    }

    public void Reset()
    {
        lock (gate)
        {
            entries = Array.Empty<MutedWordDto>();
            loaded = false;
            failed = false;
            Bump();
        }
    }

    private void Bump()
    {
        Interlocked.Increment(ref generation);
    }
}
