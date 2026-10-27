using System.Collections.Concurrent;
using System.Globalization;

namespace Aetherphone.Core.Gifs;

internal readonly record struct SharedGif(string Slug, int Width, int Height, string Url);

internal static class GifShare
{
    private const string TokenPrefix = "[aep.gif.v1:";
    private const char TokenSuffix = ']';
    private const char FieldSeparator = ';';
    private const int FieldCount = 4;
    private const int MaxSlugLength = 128;
    private const int MaxDimension = 4096;
    private const int ParseCacheCapacity = 256;

    private static readonly ConcurrentDictionary<string, (bool Ok, SharedGif Gif)> ParseCache =
        new(StringComparer.Ordinal);

    public static string Compose(in GifItem gif)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{TokenPrefix}{gif.Slug};{gif.Width};{gif.Height};{gif.Url}{TokenSuffix}");
    }

    public static bool IsToken(string? body) => TryParse(body, out _);

    public static bool TryParse(string? body, out SharedGif gif)
    {
        gif = default;
        if (body is null || body.Length <= TokenPrefix.Length + 1 || body[^1] != TokenSuffix
            || !body.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (ParseCache.TryGetValue(body, out var cached))
        {
            gif = cached.Gif;
            return cached.Ok;
        }

        var ok = TryParseFields(body.AsSpan(TokenPrefix.Length, body.Length - TokenPrefix.Length - 1), out gif);
        if (ParseCache.Count >= ParseCacheCapacity)
        {
            ParseCache.Clear();
        }

        ParseCache[body] = (ok, gif);
        return ok;
    }

    private static bool TryParseFields(ReadOnlySpan<char> inner, out SharedGif gif)
    {
        gif = default;
        Span<Range> fieldRanges = stackalloc Range[FieldCount + 1];
        if (inner.Split(fieldRanges, FieldSeparator) != FieldCount)
        {
            return false;
        }

        var slug = inner[fieldRanges[0]];
        var invariant = CultureInfo.InvariantCulture;
        if (!IsSlug(slug)
            || !int.TryParse(inner[fieldRanges[1]], NumberStyles.None, invariant, out var width)
            || !int.TryParse(inner[fieldRanges[2]], NumberStyles.None, invariant, out var height)
            || width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
        {
            return false;
        }

        var url = inner[fieldRanges[3]].ToString();
        if (!KlipyMedia.IsGifUrl(url))
        {
            return false;
        }

        gif = new SharedGif(slug.ToString(), width, height, url);
        return true;
    }

    private static bool IsSlug(ReadOnlySpan<char> slug)
    {
        if (slug.Length == 0 || slug.Length > MaxSlugLength)
        {
            return false;
        }

        for (var index = 0; index < slug.Length; index++)
        {
            if (!KlipyMedia.IsPathCharacter(slug[index]))
            {
                return false;
            }
        }

        return true;
    }
}
