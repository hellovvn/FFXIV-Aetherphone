using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoChatInvites : IDisposable
{
    internal const long RepeatWindowMilliseconds = 15 * 60 * 1000;
    internal const int HeardCapacity = 32;

    private readonly IChatGui chatGui;
    private readonly AethernetSession session;
    private readonly NotificationService notifications;
    private readonly GameData gameData;
    private readonly AppGate installed;
    private readonly Vector4 accent;
    private readonly Dictionary<string, long> heardAt = new(StringComparer.Ordinal);

    public CasinoChatInvites(IChatGui chatGui, AethernetSession session, NotificationService notifications,
        GameData gameData, AppGate installed, Vector4 accent)
    {
        this.chatGui = chatGui;
        this.session = session;
        this.notifications = notifications;
        this.gameData = gameData;
        this.installed = installed;
        this.accent = accent;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;

    internal static bool Listens(XivChatType kind) =>
        kind is XivChatType.Party or XivChatType.CrossParty or XivChatType.Say or XivChatType.Yell;

    internal static bool FirstHeard(Dictionary<string, long> heard, string code, long nowMilliseconds)
    {
        if (heard.TryGetValue(code, out var previous) && nowMilliseconds - previous < RepeatWindowMilliseconds)
        {
            return false;
        }

        if (heard.Count >= HeardCapacity && !heard.ContainsKey(code))
        {
            heard.Clear();
        }

        heard[code] = nowMilliseconds;
        return true;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!Listens(message.LogKind) || !installed.Open || !session.IsSignedIn)
        {
            return;
        }

        if (!CasinoChatCodes.TryFind(message.Message.TextValue, out var code)
            || !TryOtherPlayer(message.Sender, out var name)
            || !FirstHeard(heardAt, code, Environment.TickCount64))
        {
            return;
        }

        notifications.Notify(new PhoneNotification(CasinoTurnNotifier.AppId, Loc.T(L.Casino.NotifyInviteTitle),
            Loc.T(L.Casino.NotifyInviteBody, name), DateTime.Now, accent, CasinoLauncher.CodeGroupKey(code))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    private bool TryOtherPlayer(SeString sender, out string name)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                name = player.PlayerName;
                return !gameData.IsLocalPlayer(name, gameData.WorldName(player.World.RowId));
            }
        }

        name = string.Empty;
        return false;
    }
}
