using System.Text.Json;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KlipyResultsTests
{
    private const string Folder = "https://static.klipy.com/ii/935d7ab9d8c6202580a668421940ec81/14/af/";

    private static string File(string name, int width, int height, long size) =>
        "{\"url\":\"" + Folder + name + "\",\"width\":" + width + ",\"height\":" + height + ",\"size\":" + size + "}";

    private static string Item(string slug, string type = "gif", long mediumGifSize = 1_405_000) =>
        "{\"id\":8041071659142944,\"slug\":\"" + slug + "\",\"title\":\"Hello\",\"type\":\"" + type + "\",\"file\":{"
        + "\"hd\":{\"gif\":" + File("hd.gif", 498, 498, 4_001_918) + "},"
        + "\"md\":{\"gif\":" + File("md.gif", 498, 280, mediumGifSize) + ",\"webp\":" + File("md.webp", 498, 280, 643_490) + "},"
        + "\"sm\":{\"gif\":" + File("sm.gif", 220, 124, 314_884) + ",\"webp\":" + File("sm.webp", 220, 124, 80_118) + "},"
        + "\"xs\":{\"gif\":" + File("xs.gif", 90, 51, 71_468) + ",\"webp\":" + File("xs.webp", 90, 51, 25_340) + "}},"
        + "\"tags\":\"\",\"blur_preview\":\"data:image/jpeg;base64,AA==\"}";

    private static KlipyResponse? Parse(string items, bool hasNext = true, bool result = true) =>
        JsonSerializer.Deserialize("{\"result\":" + (result ? "true" : "false") + ",\"data\":{\"data\":[" + items
                                   + "],\"current_page\":1,\"per_page\":24,\"has_next\":" + (hasNext ? "true" : "false")
                                   + "}}", KlipyJsonContext.Default.KlipyResponse);

    [Fact]
    public void ATrendingPageTrimsToTheSmallWebpPreviewAndTheMediumGif()
    {
        var items = KlipyResults.Read(Parse(Item("hello-hi-662")), out var hasNext);

        Assert.NotNull(items);
        Assert.True(hasNext);
        var item = Assert.Single(items);
        Assert.Equal("hello-hi-662", item.Slug);
        Assert.Equal(Folder + "sm.webp", item.PreviewUrl);
        Assert.Equal(220, item.PreviewWidth);
        Assert.Equal(124, item.PreviewHeight);
        Assert.Equal(Folder + "md.gif", item.Url);
        Assert.Equal(498, item.Width);
        Assert.Equal(280, item.Height);
        Assert.True(GifMedia.IsGif(item.Url));
    }

    [Fact]
    public void AMediumGifOverTheSizeCapFallsBackToTheSmallGif()
    {
        var items = KlipyResults.Read(Parse(Item("big", mediumGifSize: GifMedia.MaxBytes + 1)), out _);

        var item = Assert.Single(items!);
        Assert.Equal(Folder + "sm.gif", item.Url);
        Assert.Equal(220, item.Width);
    }

    [Fact]
    public void AdsAndItemsWithoutUsableFilesAreLeftOut()
    {
        var broken = "{\"slug\":\"broken\",\"type\":\"gif\",\"file\":{\"md\":{\"gif\":{\"url\":\"https://evil.test/a.gif\","
                     + "\"width\":200,\"height\":200,\"size\":1000}}}}";
        var noSlug = "{\"type\":\"gif\",\"file\":{}}";

        var items = KlipyResults.Read(Parse(Item("first") + "," + Item("ad-1", "ad") + "," + broken + "," + noSlug + ","
                                            + Item("last"), hasNext: false), out var hasNext);

        Assert.False(hasNext);
        Assert.Equal(new[] { "first", "last" }, items!.Select(item => item.Slug));
    }

    [Fact]
    public void AFailedOrEmptyResponseReadsAsNothing()
    {
        Assert.Null(KlipyResults.Read(null, out _));
        Assert.Null(KlipyResults.Read(Parse(Item("x"), result: false), out _));
        Assert.Null(KlipyResults.Read(JsonSerializer.Deserialize("{\"result\":true}",
            KlipyJsonContext.Default.KlipyResponse), out _));
        Assert.Empty(KlipyResults.Read(Parse(string.Empty, hasNext: false), out _)!);
    }

    [Fact]
    public void NumbersSentAsStringsStillParse()
    {
        var json = "{\"result\":true,\"data\":{\"data\":[{\"slug\":\"s\",\"file\":{"
                   + "\"md\":{\"gif\":{\"url\":\"" + Folder + "md.gif\",\"width\":\"300\",\"height\":\"200\",\"size\":\"5000\"}},"
                   + "\"sm\":{\"webp\":{\"url\":\"" + Folder + "sm.webp\",\"width\":\"220\",\"height\":\"146\",\"size\":\"900\"}}}}],"
                   + "\"has_next\":false}}";

        var items = KlipyResults.Read(JsonSerializer.Deserialize(json, KlipyJsonContext.Default.KlipyResponse), out _);

        var item = Assert.Single(items!);
        Assert.Equal(300, item.Width);
        Assert.Equal(Folder + "sm.webp", item.PreviewUrl);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(KlipyResults.MaxDimension + 1, 300)]
    [InlineData(900, 100)]
    public void ASendFileOutsideTheServerBoundsIsNotOffered(int width, int height)
    {
        var item = new KlipyItem
        {
            Slug = "odd",
            File = new KlipyFiles
            {
                Medium = new KlipyRendition { Gif = new KlipyFile { Url = Folder + "md.gif", Width = width, Height = height, Size = 1000 } },
                Small = new KlipyRendition { Webp = new KlipyFile { Url = Folder + "sm.webp", Width = 220, Height = 220, Size = 1000 } },
            },
        };

        Assert.False(KlipyResults.TryTrim(item, out _));
    }
}
