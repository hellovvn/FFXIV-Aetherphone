using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Social;

internal sealed record ChirpCard(string PostId, string AuthorName, string HandleLabel, string Text, string? PhotoUrl,
    bool Sensitive);

internal readonly record struct ChirpCardResolution(ChirpCard? Card, bool Missed);

internal sealed class ChirpCardStore : IDisposable
{
    private const int ChirpKind = 0;
    private const int TooManyRequestsStatus = 429;
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(30);

    private readonly AethernetSession session;
    private readonly SocialClient client;
    private readonly RemoteImageCache images;
    private readonly StoreWork work = new("Chirp cards");
    private readonly object gate = new();
    private readonly HashSet<string> requested = new(StringComparer.Ordinal);
    private readonly HashSet<string> misses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> retryAtTicks = new(StringComparer.Ordinal);
    private volatile Dictionary<string, ChirpCard> cards = new(StringComparer.Ordinal);
    private string? lastAccountId;
    private int generation;

    public ChirpCardStore(AethernetSession session, SocialClient client, RemoteImageCache images)
    {
        this.session = session;
        this.client = client;
        this.images = images;
        lastAccountId = session.CurrentUser?.Id;
        session.Changed += OnSessionChanged;
    }

    public IDalamudTextureWrap? Thumbnail(string url) => images.Get(url);

    public ChirpCardResolution Resolve(string postId)
    {
        if (cards.TryGetValue(postId, out var known))
        {
            return new ChirpCardResolution(known, Missed: false);
        }

        if (!session.IsSignedIn)
        {
            return new ChirpCardResolution(null, Missed: true);
        }

        lock (gate)
        {
            if (misses.Contains(postId))
            {
                return new ChirpCardResolution(null, Missed: true);
            }

            if (retryAtTicks.TryGetValue(postId, out var retryAt) && DateTime.UtcNow.Ticks < retryAt)
            {
                return new ChirpCardResolution(null, Missed: false);
            }

            if (!requested.Add(postId))
            {
                return new ChirpCardResolution(null, Missed: false);
            }
        }

        var startedGeneration = Volatile.Read(ref generation);
        work.Run("chirp card", async token =>
        {
            var failure = AepFailure.None;
            var post = await client.PostAsync(postId, token, reported => failure = reported).ConfigureAwait(false);
            Settle(postId, post, failure, startedGeneration);
        }, () => Release(postId));
        return new ChirpCardResolution(null, Missed: false);
    }

    private void Settle(string postId, PostDto? post, AepFailure failure, int startedGeneration)
    {
        if (startedGeneration != Volatile.Read(ref generation))
        {
            return;
        }

        if (post is not null)
        {
            var shown = post.RepostOfId is null ? post : post.ReferencedPost;
            if (shown is { Kind: ChirpKind })
            {
                Remember(postId, ToCard(shown));
                return;
            }

            Miss(postId);
            return;
        }

        if (IsRefusal(failure))
        {
            Miss(postId);
            return;
        }

        lock (gate)
        {
            retryAtTicks[postId] = (DateTime.UtcNow + RetryAfterFailure).Ticks;
        }
    }

    // The server answers 404 for a chirp that is deleted, removed, private to this viewer or behind a block,
    // so any refusal reads as unavailable; offline, timeouts and rate limits only mean try again later.
    private static bool IsRefusal(AepFailure failure)
    {
        return failure.Kind == AepFailureKind.Server
            && failure.StatusCode is >= 400 and < 500
            && failure.StatusCode != TooManyRequestsStatus;
    }

    private static ChirpCard ToCard(PostDto post)
    {
        var photos = PostMedia.Photos(post);
        return new ChirpCard(post.Id, SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle),
            post.AuthorHandle.Length > 0 ? "@" + post.AuthorHandle : string.Empty, post.Text,
            post.Video?.PosterUrl ?? (photos.Length > 0 ? photos[0] : null), post.Sensitive);
    }

    private void Remember(string postId, ChirpCard card)
    {
        lock (gate)
        {
            cards = new Dictionary<string, ChirpCard>(cards, StringComparer.Ordinal)
            {
                [postId] = card,
            };
        }
    }

    private void Miss(string postId)
    {
        lock (gate)
        {
            misses.Add(postId);
        }
    }

    private void Release(string postId)
    {
        lock (gate)
        {
            requested.Remove(postId);
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        Interlocked.Increment(ref generation);
        lock (gate)
        {
            requested.Clear();
            misses.Clear();
            retryAtTicks.Clear();
            cards = new Dictionary<string, ChirpCard>(StringComparer.Ordinal);
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
