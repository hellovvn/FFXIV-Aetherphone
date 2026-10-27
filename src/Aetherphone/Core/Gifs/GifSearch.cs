using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Gifs;

internal sealed record GifResults(
    string Query,
    GifItem[] Items,
    int Page,
    bool HasMore,
    bool Loading,
    bool LoadingMore,
    AepFailure Failure)
{
    public static readonly GifResults Empty =
        new(string.Empty, Array.Empty<GifItem>(), 0, false, false, false, AepFailure.None);
}

internal sealed class GifSearch : IDisposable
{
    public const int MaxPages = 10;
    public const double DebounceSeconds = 0.45;
    private static readonly TimeSpan SettingsRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SettingsLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan TrendingLifetime = TimeSpan.FromMinutes(10);

    private readonly AethernetSession session;
    private readonly GifsClient gifs;
    private readonly KlipyClient klipy;
    private readonly RetryGate settingsGate = new(SettingsRetry);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private volatile GifSettings? settings;
    private volatile string? settingsToken;
    private volatile bool settingsLoading;
    private DateTime settingsFetchedUtc = DateTime.MinValue;
    private volatile GifResults results = GifResults.Empty;
    private GifResults? trending;
    private DateTime trendingFetchedUtc = DateTime.MinValue;
    private CancellationTokenSource? fetch;
    private int generation;
    private string pendingQuery = string.Empty;
    private double pendingSince = -1d;

    public GifSearch(AethernetSession session, GifsClient gifs, KlipyClient klipy)
    {
        this.session = session;
        this.gifs = gifs;
        this.klipy = klipy;
    }

    public bool Available => settings is not null && session.IsSignedIn
                             && string.Equals(settingsToken, session.Token, StringComparison.Ordinal);

    public GifResults Results => results;

    public void EnsureSettings()
    {
        var token = session.Token;
        if (!session.IsSignedIn || token is null || settingsLoading)
        {
            return;
        }

        if (string.Equals(settingsToken, token, StringComparison.Ordinal)
            && DateTime.UtcNow - settingsFetchedUtc < SettingsLifetime)
        {
            return;
        }

        if (!settingsGate.TryPass())
        {
            return;
        }

        settingsLoading = true;
        var cancel = lifetime.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var config = await gifs.ConfigAsync(cancel).ConfigureAwait(false);
                if (config is null)
                {
                    return;
                }

                settings = GifSettings.From(config);
                settingsFetchedUtc = DateTime.UtcNow;
                settingsToken = token;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Gifs] could not load the GIF search settings");
            }
            finally
            {
                settingsLoading = false;
            }
        });
    }

    public void Open()
    {
        pendingQuery = string.Empty;
        pendingSince = -1d;
        Begin(string.Empty, true);
    }

    public void Close()
    {
        lock (gate)
        {
            generation++;
            CancelFetch();
            results = GifResults.Empty;
        }
    }

    public void SetQuery(string raw, double nowSeconds)
    {
        var normalized = GifQuery.Normalize(raw);
        if (string.Equals(normalized, pendingQuery, StringComparison.Ordinal))
        {
            return;
        }

        pendingQuery = normalized;
        pendingSince = nowSeconds;
    }

    public void Tick(double nowSeconds)
    {
        if (pendingSince < 0d || nowSeconds - pendingSince < DebounceSeconds)
        {
            return;
        }

        pendingSince = -1d;
        Begin(pendingQuery, false);
    }

    public void LoadMore()
    {
        GifResults snapshot;
        int ticket;
        lock (gate)
        {
            snapshot = results;
            if (snapshot.Loading || snapshot.LoadingMore || !snapshot.HasMore || snapshot.Failure.Failed
                || snapshot.Page >= MaxPages)
            {
                return;
            }

            results = snapshot with { LoadingMore = true };
            ticket = generation;
        }

        StartFetch(snapshot.Query, snapshot.Page + 1, ticket);
    }

    public void Retry()
    {
        var snapshot = results;
        if (!snapshot.Failure.Failed)
        {
            return;
        }

        if (snapshot.Items.Length == 0)
        {
            Begin(snapshot.Query, true);
            return;
        }

        lock (gate)
        {
            results = snapshot with { Failure = AepFailure.None };
        }

        LoadMore();
    }

    public void RegisterShare(GifPick pick)
    {
        var current = settings;
        if (current is null)
        {
            return;
        }

        var cancel = lifetime.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await klipy.ShareAsync(current, pick.Item.Slug, pick.Query, cancel).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "[Gifs] could not register a GIF share with KLIPY");
            }
        });
    }

    public void ForwardReport(string slug, string reason)
    {
        var cached = Available ? settings : null;
        var cancel = lifetime.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var current = cached ?? GifSettings.From(await gifs.ConfigAsync(cancel).ConfigureAwait(false));
                if (current is null)
                {
                    return;
                }

                await klipy.ReportAsync(current, slug, reason, cancel).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "[Gifs] could not forward a GIF report to KLIPY");
            }
        });
    }

    private void Begin(string query, bool force)
    {
        var effective = GifQuery.IsSearch(query) ? query : string.Empty;
        int ticket;
        lock (gate)
        {
            var current = results;
            if (!force && string.Equals(current.Query, effective, StringComparison.Ordinal)
                && !current.Failure.Failed && (current.Loading || current.Items.Length > 0))
            {
                return;
            }

            generation++;
            CancelFetch();
            if (effective.Length == 0 && trending is { } cached
                && DateTime.UtcNow - trendingFetchedUtc < TrendingLifetime)
            {
                results = cached;
                return;
            }

            results = new GifResults(effective, Array.Empty<GifItem>(), 0, false, true, false, AepFailure.None);
            ticket = generation;
        }

        StartFetch(effective, 1, ticket);
    }

    private void StartFetch(string query, int page, int ticket)
    {
        var current = settings;
        if (current is null)
        {
            Complete(query, page, ticket, null, false, AepFailure.Transport(AepFailureKind.SignedOut));
            return;
        }

        CancellationToken cancel;
        lock (gate)
        {
            if (ticket != generation)
            {
                return;
            }

            fetch?.Dispose();
            fetch = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            cancel = fetch.Token;
        }

        var locale = KlipyUrls.LocaleOf(Loc.Current.CultureName);
        _ = Task.Run(async () =>
        {
            var reported = AepFailure.None;
            try
            {
                var response = query.Length == 0
                    ? await klipy.TrendingAsync(current, page, locale, cancel, failure => reported = failure)
                        .ConfigureAwait(false)
                    : await klipy.SearchAsync(current, query, page, locale, cancel, failure => reported = failure)
                        .ConfigureAwait(false);
                var items = KlipyResults.Read(response, out var hasNext);
                var failure = items is not null ? AepFailure.None
                    : reported.Failed ? reported : AepFailure.Transport(AepFailureKind.BadResponse);
                Complete(query, page, ticket, items, hasNext, failure);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Gifs] GIF search failed");
                Complete(query, page, ticket, null, false, AepFailure.Transport(AepFailureKind.BadResponse));
            }
        });
    }

    private void Complete(string query, int page, int ticket, GifItem[]? items, bool hasNext, AepFailure failure)
    {
        lock (gate)
        {
            if (ticket != generation)
            {
                return;
            }

            var previous = results;
            if (items is null)
            {
                results = previous with { Loading = false, LoadingMore = false, Failure = failure };
                return;
            }

            var merged = page <= 1 ? items : Append(previous.Items, items);
            results = new GifResults(query, merged, page, hasNext && items.Length > 0, false, false,
                AepFailure.None);
            if (query.Length == 0)
            {
                trending = results;
                trendingFetchedUtc = DateTime.UtcNow;
            }
        }
    }

    private static GifItem[] Append(GifItem[] existing, GifItem[] added)
    {
        var merged = new GifItem[existing.Length + added.Length];
        existing.CopyTo(merged, 0);
        added.CopyTo(merged, existing.Length);
        return merged;
    }

    private void CancelFetch()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            CancelFetch();
        }

        lifetime.Cancel();
        lifetime.Dispose();
    }
}
