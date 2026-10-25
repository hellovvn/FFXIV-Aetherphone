using Aetherphone.Core.Net;

namespace Aetherphone.Core.Gifs;

internal sealed class KlipyClient
{
    private readonly HttpService http;

    public KlipyClient(HttpService http)
    {
        this.http = http;
    }

    public Task<KlipyResponse?> TrendingAsync(GifSettings settings, int page, string locale, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return http.GetJsonAsync(KlipyUrls.Trending(settings, page, locale), KlipyJsonContext.Default.KlipyResponse,
            null, token, onFailure: onFailure);
    }

    public Task<KlipyResponse?> SearchAsync(GifSettings settings, string query, int page, string locale,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return http.GetJsonAsync(KlipyUrls.Search(settings, query, page, locale),
            KlipyJsonContext.Default.KlipyResponse, null, token, onFailure: onFailure);
    }

    public Task<bool> ShareAsync(GifSettings settings, string slug, string query, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, KlipyUrls.Share(settings, slug),
            new KlipyShareRequest(settings.CustomerId, query), KlipyJsonContext.Default.KlipyShareRequest, null,
            token);
    }

    public Task<bool> ReportAsync(GifSettings settings, string slug, string reason, CancellationToken token)
    {
        return http.SendJsonForStatusAsync(HttpMethod.Post, KlipyUrls.Report(settings, slug),
            new KlipyReportRequest(settings.CustomerId, reason), KlipyJsonContext.Default.KlipyReportRequest, null,
            token);
    }
}
