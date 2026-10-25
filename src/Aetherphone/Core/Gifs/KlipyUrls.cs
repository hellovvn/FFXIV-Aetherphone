using System.Globalization;

namespace Aetherphone.Core.Gifs;

internal static class KlipyUrls
{
    public const string BaseUrl = "https://api.klipy.com/api/v1/";
    public const int PageSize = 24;
    public const string FallbackLocale = "us";
    private const string Formats = "gif,webp";

    public static string Trending(GifSettings settings, int page, string locale)
    {
        return Root(settings) + "/gifs/trending?" + Common(settings, page, locale);
    }

    public static string Search(GifSettings settings, string query, int page, string locale)
    {
        return Root(settings) + "/gifs/search?q=" + Uri.EscapeDataString(query) + "&" + Common(settings, page, locale);
    }

    public static string Share(GifSettings settings, string slug)
    {
        return Root(settings) + "/gifs/share/" + Uri.EscapeDataString(slug);
    }

    public static string Report(GifSettings settings, string slug)
    {
        return Root(settings) + "/gifs/report/" + Uri.EscapeDataString(slug);
    }

    public static string LocaleOf(string cultureName)
    {
        var separator = cultureName.LastIndexOf('-');
        if (separator < 0 || cultureName.Length - separator - 1 != 2)
        {
            return FallbackLocale;
        }

        return cultureName[(separator + 1)..].ToLowerInvariant();
    }

    private static string Root(GifSettings settings)
    {
        return BaseUrl + Uri.EscapeDataString(settings.AppKey);
    }

    private static string Common(GifSettings settings, int page, string locale)
    {
        return "page=" + Math.Max(1, page).ToString(CultureInfo.InvariantCulture)
               + "&per_page=" + PageSize.ToString(CultureInfo.InvariantCulture)
               + "&customer_id=" + Uri.EscapeDataString(settings.CustomerId)
               + "&locale=" + Uri.EscapeDataString(locale)
               + "&content_filter=" + Uri.EscapeDataString(settings.ContentFilter)
               + "&format_filter=" + Formats;
    }
}
