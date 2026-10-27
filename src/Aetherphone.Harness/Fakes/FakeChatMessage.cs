using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeChatMessage : IHandleableChatMessage
{
    private SeString sender;
    private SeString message;

    public FakeChatMessage(XivChatType logKind, SeString sender, SeString message, int timestamp)
    {
        LogKind = logKind;
        OriginalSender = new ReadOnlySeString(sender.Encode());
        OriginalMessage = new ReadOnlySeString(message.Encode());
        this.sender = sender;
        this.message = message;
        Timestamp = timestamp;
    }

    public XivChatType LogKind { get; }

    public XivChatRelationKind SourceKind => default;

    public XivChatRelationKind TargetKind => default;

    public ReadOnlySeString OriginalSender { get; }

    public ReadOnlySeString OriginalMessage { get; }

    public SeString Sender
    {
        get => sender;
        set
        {
            sender = value;
            SenderModified = true;
        }
    }

    public SeString Message
    {
        get => message;
        set
        {
            message = value;
            MessageModified = true;
        }
    }

    public bool SenderModified { get; private set; }

    public bool MessageModified { get; private set; }

    public int Timestamp { get; }

    public bool IsHandled { get; private set; }

    public void PreventOriginal() => IsHandled = true;
}
