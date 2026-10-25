using Aetherphone.Core.Media;

namespace Aetherphone.Core.Gifs;

internal static class KlipyResults
{
    public const int MaxDimension = 1024;
    public const int MaxAspectRatio = 8;
    private const string GifType = "gif";

    public static GifItem[]? Read(KlipyResponse? response, out bool hasNext)
    {
        hasNext = false;
        var page = response?.Data;
        if (response is null || !response.Result || page is null)
        {
            return null;
        }

        hasNext = page.HasNext;
        var source = page.Items ?? Array.Empty<KlipyItem>();
        var items = new GifItem[source.Length];
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (TryTrim(source[index], out var item))
            {
                items[count++] = item;
            }
        }

        if (count == items.Length)
        {
            return items;
        }

        Array.Resize(ref items, count);
        return items;
    }

    public static bool TryTrim(KlipyItem? item, out GifItem trimmed)
    {
        trimmed = default;
        if (item?.File is not { } files || string.IsNullOrEmpty(item.Slug)
            || (item.Type is not null && !string.Equals(item.Type, GifType, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var preview = FirstUsable(files.Small?.Webp, files.Small?.Gif, files.Tiny?.Webp, files.Tiny?.Gif);
        var send = Sendable(files.Medium?.Gif) ? files.Medium!.Gif : Sendable(files.Small?.Gif) ? files.Small!.Gif : null;
        if (preview is null || send is null)
        {
            return false;
        }

        trimmed = new GifItem(item.Slug, preview.Url!, preview.Width, preview.Height, send.Url!, send.Width,
            send.Height);
        return true;
    }

    private static KlipyFile? FirstUsable(KlipyFile? first, KlipyFile? second, KlipyFile? third, KlipyFile? fourth)
    {
        if (Usable(first))
        {
            return first;
        }

        if (Usable(second))
        {
            return second;
        }

        return Usable(third) ? third : Usable(fourth) ? fourth : null;
    }

    private static bool Usable(KlipyFile? file)
    {
        return file is not null && KlipyMedia.IsHosted(file.Url) && FitsBounds(file.Width, file.Height);
    }

    private static bool Sendable(KlipyFile? file)
    {
        return Usable(file) && file!.Size > 0 && file.Size <= GifMedia.MaxBytes
               && file.Url!.EndsWith(".gif", StringComparison.Ordinal);
    }

    private static bool FitsBounds(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
        {
            return false;
        }

        return Math.Max(width, height) <= Math.Min(width, height) * MaxAspectRatio;
    }
}
