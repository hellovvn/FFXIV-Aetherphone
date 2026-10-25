using Aetherphone.Core.Gifs;

namespace Aetherphone.Core.Net;

internal static class LogUrl
{
    public const string Hidden = "***";

    private const string KlipyApiPrefix = "/api/";

    // KLIPY puts the app key in the path, /api/v1/{key}/..., so it is the fourth slash-separated segment.
    private const int KlipyKeySegment = 3;

    public static string Of(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) && KlipyMedia.IsKlipyHost(parsed.Host)
            ? Redact(parsed)
            : url;
    }

    public static string Of(Uri? uri)
    {
        if (uri is null)
        {
            return string.Empty;
        }

        return uri.IsAbsoluteUri && KlipyMedia.IsKlipyHost(uri.Host) ? Redact(uri) : uri.ToString();
    }

    private static string Redact(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (path.StartsWith(KlipyApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/');
            if (segments.Length > KlipyKeySegment)
            {
                segments[KlipyKeySegment] = Hidden;
            }

            path = string.Join('/', segments);
        }

        return uri.Scheme + "://" + uri.Authority + path;
    }
}
