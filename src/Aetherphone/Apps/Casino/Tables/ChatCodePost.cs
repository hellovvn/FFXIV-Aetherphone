using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class ChatCodePost
{
    private const string YellKey = "yell";

    private static readonly string[] ChannelKeys = [GameChannels.PartyKey, GameChannels.SayKey, YellKey];
    private static readonly LocString[] ChannelLabels = [L.Tables.PostParty, L.Tables.PostSay, L.Tables.PostYell];

    private readonly ActionSheet sheet = new();
    private readonly ActionSheet.Item[] items = new ActionSheet.Item[ChannelKeys.Length + 1];
    private string code = string.Empty;
    private string invite = string.Empty;

    public bool IsOpen => sheet.IsOpen;

    public void Gate() => sheet.Gate();

    public void Close() => sheet.Close();

    public void Open(string roomCode, string inviteToken = "")
    {
        code = roomCode.Length == CasinoRoomCodes.Length ? roomCode : string.Empty;
        invite = inviteToken;
        if (code.Length == 0 && invite.Length == 0)
        {
            return;
        }

        sheet.Open();
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        if (!sheet.IsOpen && !sheet.CapturesPointer)
        {
            return;
        }

        var channels = code.Length > 0 ? ChannelKeys.Length : 0;
        for (var index = 0; index < channels; index++)
        {
            items[index] = new ActionSheet.Item(Loc.T(ChannelLabels[index]));
        }

        var count = channels;
        if (code.Length > 0 || invite.Length > 0)
        {
            items[count++] = new ActionSheet.Item(Loc.T(L.Casino.DoorCopyInvite));
        }

        var picked = sheet.Draw(screen, ActionSheetStyle.From(ui), items.AsSpan(0, count), Loc.T(L.Common.Cancel),
            false, Loc.T(L.Tables.InviteTitle));
        if (picked < 0)
        {
            return;
        }

        if (picked < channels)
        {
            Post(ChannelKeys[picked]);
            return;
        }

        ImGui.SetClipboardText(code.Length > 0 ? Invitation() : invite);
        ShellToast.Show(Loc.T(L.Common.Copied));
    }

    private string Invitation() => Loc.T(L.Tables.ChatInvite, CasinoChatCodes.Tag(code), Loc.T(L.Apps.Casino));

    private void Post(string channelKey)
    {
        if (!GameChannels.TryByKey(channelKey, out var channel))
        {
            return;
        }

        var posted = ChatSender.TrySend(string.Concat(channel.Command, " ", Invitation()));
        UiFeedback.Play(posted ? UiSound.MessageSent : UiSound.Caution);
        ShellToast.Show(Loc.T(posted ? L.Tables.CodePosted : L.Tables.CodePostFailed));
    }
}
