using System.Text.Json.Serialization;

namespace Aetherphone.Core.Gifs;

internal sealed class KlipyResponse
{
    [JsonPropertyName("result")] public bool Result { get; set; }

    [JsonPropertyName("data")] public KlipyPage? Data { get; set; }
}

internal sealed class KlipyPage
{
    [JsonPropertyName("data")] public KlipyItem[]? Items { get; set; }

    [JsonPropertyName("current_page")] public int CurrentPage { get; set; }

    [JsonPropertyName("has_next")] public bool HasNext { get; set; }
}

internal sealed class KlipyItem
{
    [JsonPropertyName("slug")] public string? Slug { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("file")] public KlipyFiles? File { get; set; }
}

internal sealed class KlipyFiles
{
    [JsonPropertyName("md")] public KlipyRendition? Medium { get; set; }

    [JsonPropertyName("sm")] public KlipyRendition? Small { get; set; }

    [JsonPropertyName("xs")] public KlipyRendition? Tiny { get; set; }
}

internal sealed class KlipyRendition
{
    [JsonPropertyName("gif")] public KlipyFile? Gif { get; set; }

    [JsonPropertyName("webp")] public KlipyFile? Webp { get; set; }
}

internal sealed class KlipyFile
{
    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("width")] public int Width { get; set; }

    [JsonPropertyName("height")] public int Height { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }
}

internal sealed record KlipyShareRequest(
    [property: JsonPropertyName("customer_id")] string CustomerId,
    [property: JsonPropertyName("q")] string Query);

internal sealed record KlipyReportRequest(
    [property: JsonPropertyName("customer_id")] string CustomerId,
    [property: JsonPropertyName("reason")] string Reason);

[JsonSourceGenerationOptions(NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(KlipyResponse))]
[JsonSerializable(typeof(KlipyShareRequest))]
[JsonSerializable(typeof(KlipyReportRequest))]
internal sealed partial class KlipyJsonContext : JsonSerializerContext
{
}
