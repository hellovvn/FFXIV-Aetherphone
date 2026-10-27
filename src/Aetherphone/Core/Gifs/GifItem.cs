namespace Aetherphone.Core.Gifs;

internal readonly record struct GifItem(
    string Slug,
    string PreviewUrl,
    int PreviewWidth,
    int PreviewHeight,
    string Url,
    int Width,
    int Height);

internal sealed record GifPick(GifItem Item, string Query);
