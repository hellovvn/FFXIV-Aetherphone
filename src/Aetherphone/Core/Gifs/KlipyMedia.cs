namespace Aetherphone.Core.Gifs;

internal static class KlipyMedia
{
    public const int MaxUrlLength = 300;
    private const string Scheme = "https://";
    private const string RootDomain = "klipy.com";
    private const string GifExtension = ".gif";

    private static readonly string[] MediaHosts = { "static.klipy.com", "static1.klipy.com", "static2.klipy.com" };

    public static bool IsGifUrl(string? url)
    {
        if (url is null || url.Length > MaxUrlLength || !url.StartsWith(Scheme, StringComparison.Ordinal)
            || !url.EndsWith(GifExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = url.AsSpan(Scheme.Length, url.Length - Scheme.Length - GifExtension.Length);
        var slash = rest.IndexOf('/');
        if (slash <= 0 || !IsMediaHost(rest[..slash]))
        {
            return false;
        }

        var path = rest[(slash + 1)..];
        var segmentLength = 0;
        for (var index = 0; index < path.Length; index++)
        {
            var character = path[index];
            if (character == '/')
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            if (!IsPathCharacter(character))
            {
                return false;
            }

            segmentLength++;
        }

        return segmentLength > 0;
    }

    public static bool IsPathCharacter(char character)
    {
        return character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_';
    }

    private static bool IsMediaHost(ReadOnlySpan<char> host)
    {
        for (var index = 0; index < MediaHosts.Length; index++)
        {
            if (host.Equals(MediaHosts[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsHosted(string? url)
    {
        if (url is null || !url.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = url.AsSpan(Scheme.Length);
        var end = rest.IndexOfAny('/', ':', '?');
        return IsKlipyHost(end < 0 ? rest : rest[..end]);
    }

    public static bool IsKlipyHost(ReadOnlySpan<char> host)
    {
        if (host.Equals(RootDomain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.Length > RootDomain.Length + 1
               && host.EndsWith(RootDomain, StringComparison.OrdinalIgnoreCase)
               && host[host.Length - RootDomain.Length - 1] == '.';
    }
}
