using System.Text;

namespace Aetherphone.Core.Gifs;

internal static class GifQuery
{
    public const int MaxLength = 50;
    public const int MinSearchLength = 2;

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(raw.Length, MaxLength));
        Span<char> units = stackalloc char[2];
        var pendingSpace = false;
        foreach (var rune in raw.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            var written = rune.EncodeToUtf16(units);
            if (builder.Length + written + (pendingSpace ? 1 : 0) > MaxLength)
            {
                break;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(units[..written]);
        }

        return builder.ToString();
    }

    public static bool IsSearch(string normalized)
    {
        return normalized.Length >= MinSearchLength;
    }
}
