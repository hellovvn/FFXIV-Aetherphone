namespace Aetherphone.Core.Casino.Rolls;

internal readonly record struct ChatRoll(string Name, int Value, int Max, bool Mine);

internal enum RollPartKind : byte
{
    Text,
    Icon,
    Player,
}

internal readonly record struct RollPart(RollPartKind Kind, int Icon, string Text)
{
    public static RollPart OfText(string text) => new(RollPartKind.Text, 0, text);

    public static RollPart OfIcon(int icon) => new(RollPartKind.Icon, icon, string.Empty);

    public static RollPart OfPlayer(string name) => new(RollPartKind.Player, 0, name);
}

// The roll lines come from LogMessage 856 and 3887 (/random) and Addon 3567 and 3568 (/dice). In every client
// language the rolled value follows icon 73, a /dice line opens with icon 54, and a custom range is the only
// other number on the line. Character and world names never contain digits.
internal static class RollLines
{
    public const int DefaultMax = 999;
    public const int MaxRange = 999;
    public const int ValueIcon = 73;
    public const int DiceIcon = 54;

    private const char FirstPrivateUse = '';
    private const char LastPrivateUse = '';

    public static bool TryRead(ReadOnlySpan<RollPart> parts, bool randomLog, string sender, string localName,
        out ChatRoll roll)
    {
        roll = default;
        var value = -1;
        var max = 0;
        var dice = false;
        var afterValueIcon = false;
        var player = string.Empty;
        for (var index = 0; index < parts.Length; index++)
        {
            ref readonly var part = ref parts[index];
            switch (part.Kind)
            {
                case RollPartKind.Icon:
                    dice |= part.Icon == DiceIcon;
                    afterValueIcon = part.Icon == ValueIcon;
                    continue;
                case RollPartKind.Player:
                    if (player.Length == 0)
                    {
                        player = part.Text;
                    }

                    break;
                default:
                    ReadNumbers(part.Text, afterValueIcon && value < 0, ref value, ref max);
                    break;
            }

            afterValueIcon = false;
        }

        if (value < 0 || (!randomLog && !dice))
        {
            return false;
        }

        var name = randomLog ? player : CleanName(sender);
        if (name.Length == 0)
        {
            name = localName;
        }

        if (name.Length == 0)
        {
            return false;
        }

        roll = new ChatRoll(name, value, max > 0 ? max : DefaultMax,
            localName.Length > 0 && string.Equals(name, localName, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    public static string CleanName(string raw)
    {
        var start = 0;
        var end = raw.Length;
        while (start < end && Skippable(raw[start]))
        {
            start++;
        }

        while (end > start && Skippable(raw[end - 1]))
        {
            end--;
        }

        return start == 0 && end == raw.Length ? raw : raw[start..end];
    }

    private static bool Skippable(char character) =>
        char.IsWhiteSpace(character) || character is >= FirstPrivateUse and <= LastPrivateUse;

    private static void ReadNumbers(string text, bool valueFirst, ref int value, ref int max)
    {
        var index = 0;
        var leading = true;
        while (index < text.Length)
        {
            if (!char.IsAsciiDigit(text[index]))
            {
                leading &= char.IsWhiteSpace(text[index]);
                index++;
                continue;
            }

            var number = 0;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                number = Math.Min(number * 10 + (text[index] - '0'), int.MaxValue / 10);
                index++;
            }

            if (valueFirst && leading)
            {
                value = number;
            }
            else
            {
                max = Math.Max(max, number);
            }

            leading = false;
        }
    }
}
