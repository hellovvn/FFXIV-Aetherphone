namespace Aetherphone.Core.Casino;

internal static class CasinoChatCodes
{
    private const string TagOpen = "[Gamba ";
    private const char TagClose = ']';

    public static string Tag(string code) => string.Concat(TagOpen, code, "]");

    public static bool TryFind(string text, out string code)
    {
        code = string.Empty;
        var start = text.IndexOf(TagOpen, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var codeStart = start + TagOpen.Length;
        var close = codeStart + CasinoRoomCodes.Length;
        if (close >= text.Length || text[close] != TagClose)
        {
            return false;
        }

        code = CasinoRoomCodes.Normalized(text.Substring(codeStart, CasinoRoomCodes.Length));
        return code.Length > 0;
    }
}
