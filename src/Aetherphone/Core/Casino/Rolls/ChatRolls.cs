using System.Globalization;
using System.Runtime.InteropServices;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Runtime;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino.Rolls;

internal sealed class ChatRolls : IDisposable
{
    public const string YellKey = "yell";
    public const string RandomCommand = "/random ";

    private const long SendIntervalMilliseconds = 1200;
    private const int OutboxCapacity = 8;

    private readonly IChatGui chatGui;
    private readonly GameData gameData;
    private readonly FrameworkTicker sender;
    private readonly Queue<string> outbox = new();
    private readonly List<RollPart> parts = new();
    private string localName = string.Empty;

    public ChatRolls(IChatGui chatGui, IFramework framework, GameData gameData)
    {
        this.chatGui = chatGui;
        this.gameData = gameData;
        chatGui.ChatMessage += OnChatMessage;
        sender = new FrameworkTicker(framework, SendIntervalMilliseconds, Tick);
    }

    public RollTable Table { get; } = new();

    public string HouseName => Table.HouseName.Length > 0 ? Table.HouseName : localName;

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
        sender.Dispose();
    }

    public void Open(in RollSettings settings)
    {
        outbox.Clear();
        RefreshLocalName();
        Table.Open(settings);
        Publish();
    }

    public void Close()
    {
        outbox.Clear();
        Table.Close();
    }

    public void Start()
    {
        if (Table.Start())
        {
            Publish();
        }
    }

    public void Rematch()
    {
        if (Table.Rematch())
        {
            Publish();
        }
    }

    public void NewPlayers()
    {
        Table.NewPlayers();
        Publish();
    }

    public void Choose(RollCall call)
    {
        if (Table.Choose(call))
        {
            Publish();
        }
    }

    public void Drop(int index)
    {
        if (Table.Drop(index))
        {
            Publish();
        }
    }

    public void CallForPlayers() => Announce(RollAnnouncements.JoinCall(Table.Kind));

    public void RollForHouse()
    {
        if (Table.WaitsOnHouse)
        {
            Enqueue(string.Concat(RandomCommand, Table.ExpectedMax.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!Table.IsOpen)
        {
            return;
        }

        var randomLog = message.LogKind == XivChatType.RandomNumber;
        if (!randomLog && !HasDiceIcon(message.Message))
        {
            return;
        }

        Collect(message.Message);
        RefreshLocalName();
        if (!RollLines.TryRead(CollectionsMarshal.AsSpan(parts), randomLog, SenderName(message.Sender), localName,
                out var roll))
        {
            return;
        }

        Table.Take(roll);
        Publish();
    }

    private void Publish()
    {
        var houseName = HouseName;
        while (Table.TryTakeNews(out var news))
        {
            if (news.Kind == RollNewsKind.Joined)
            {
                continue;
            }

            Announce(RollAnnouncements.Line(Table, news, houseName));
        }
    }

    private void Announce(string line)
    {
        if (line.Length == 0 || !GameChannels.TryByKey(Table.Settings.Channel, out var channel))
        {
            return;
        }

        Enqueue(string.Concat(channel.Command, " ", line));
    }

    private void Enqueue(string line)
    {
        if (outbox.Count >= OutboxCapacity)
        {
            outbox.Dequeue();
        }

        outbox.Enqueue(line);
    }

    private void Tick()
    {
        if (!Table.IsOpen)
        {
            return;
        }

        RefreshLocalName();
        if (outbox.Count > 0)
        {
            ChatSender.TrySend(outbox.Dequeue());
        }
    }

    private void RefreshLocalName()
    {
        var name = gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        if (!string.Equals(name, localName, StringComparison.Ordinal))
        {
            localName = name;
        }
    }

    private void Collect(SeString message)
    {
        parts.Clear();
        var payloads = message.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            switch (payloads[index])
            {
                case IconPayload icon:
                    parts.Add(RollPart.OfIcon((int)icon.Icon));
                    break;
                case PlayerPayload player:
                    parts.Add(RollPart.OfPlayer(player.PlayerName));
                    break;
                case TextPayload { Text: { } text }:
                    parts.Add(RollPart.OfText(text));
                    break;
            }
        }
    }

    private static bool HasDiceIcon(SeString message)
    {
        var payloads = message.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is IconPayload icon && (int)icon.Icon == RollLines.DiceIcon)
            {
                return true;
            }
        }

        return false;
    }

    private static string SenderName(SeString sender)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player && player.PlayerName.Length > 0)
            {
                return player.PlayerName;
            }
        }

        return sender.TextValue;
    }
}
