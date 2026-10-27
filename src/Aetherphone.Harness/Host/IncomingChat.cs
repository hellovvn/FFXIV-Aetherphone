using System.Globalization;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Aetherphone.Harness.Host;

internal static class IncomingChat
{
    public const string OwnRoll = "me";
    private const int RollValueIcon = 73;
    private const int DefaultRollMax = 999;

    public static bool TryChannel(string name, out XivChatType channel)
    {
        channel = name.ToLowerInvariant() switch
        {
            "say" => XivChatType.Say,
            "yell" => XivChatType.Yell,
            "shout" => XivChatType.Shout,
            "party" => XivChatType.Party,
            "crossparty" => XivChatType.CrossParty,
            "tell" => XivChatType.TellIncoming,
            "echo" => XivChatType.Echo,
            "system" => XivChatType.SystemMessage,
            "random" => XivChatType.RandomNumber,
            _ => XivChatType.None,
        };
        return channel != XivChatType.None;
    }

    public static SeString Sender(string name) => new(new TextPayload(name));

    public static SeString Text(string text) => new(new TextPayload(text));

    public static SeString Roll(string name, int value, int max)
    {
        var valueText = value.ToString(CultureInfo.InvariantCulture);
        var range = max > 0 && max != DefaultRollMax
            ? string.Concat(" (out of ", max.ToString(CultureInfo.InvariantCulture), ").")
            : ".";
        if (string.Equals(name, OwnRoll, StringComparison.OrdinalIgnoreCase))
        {
            return new SeString(new TextPayload("Random! You roll a "), new IconPayload((BitmapFontIcon)RollValueIcon),
                new TextPayload(valueText + range));
        }

        return new SeString(new TextPayload("Random! "), new PlayerPayload(name, 0), new TextPayload(name),
            new TextPayload(" rolls a "), new IconPayload((BitmapFontIcon)RollValueIcon),
            new TextPayload(valueText + range));
    }
}
