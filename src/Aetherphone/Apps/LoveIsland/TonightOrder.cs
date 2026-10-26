namespace Aetherphone.Apps.LoveIsland;

internal enum TonightSection : byte
{
    Status,
    Night,
    Live,
    Share,
    Pinned,
    Posts,
    Syncshells,
    Links,
    About,
}

internal static class TonightOrder
{
    private static readonly string[] Keys =
    {
        "status", "night", "live", "share", "pinned", "posts", "syncshells", "links", "about",
    };

    public static readonly TonightSection[] Default = Build(null);

    public static TonightSection[] Build(string[]? order)
    {
        var sections = new TonightSection[Keys.Length];
        Span<bool> placed = stackalloc bool[Keys.Length];
        var count = 0;
        if (order is not null)
        {
            for (var index = 0; index < order.Length; index++)
            {
                var keyIndex = KeyIndex(order[index]);
                if (keyIndex < 0 || placed[keyIndex])
                {
                    continue;
                }

                placed[keyIndex] = true;
                sections[count++] = (TonightSection)keyIndex;
            }
        }

        for (var keyIndex = 0; keyIndex < Keys.Length; keyIndex++)
        {
            if (!placed[keyIndex])
            {
                sections[count++] = (TonightSection)keyIndex;
            }
        }

        return sections;
    }

    private static int KeyIndex(string? key)
    {
        if (key is null)
        {
            return -1;
        }

        var trimmed = key.AsSpan().Trim();
        for (var index = 0; index < Keys.Length; index++)
        {
            if (trimmed.Equals(Keys[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
