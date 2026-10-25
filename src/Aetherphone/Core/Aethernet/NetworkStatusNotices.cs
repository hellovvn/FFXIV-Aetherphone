using Aetherphone.Core.Localization;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class NetworkStatusNotices : IDisposable
{
    private const ushort RestartingUiColor = 577;
    private const ushort LostUiColor = 508;
    private const ushort BackOnlineUiColor = 575;

    private readonly AethernetSession session;
    private readonly RealtimeSignalBus signals;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly Configuration configuration;
    private readonly NetworkOutageTracker tracker = new();
    private volatile bool restartAnnounced;
    private bool tracking;

    public NetworkStatusNotices(AethernetSession session, RealtimeSignalBus signals, IFramework framework,
        IClientState clientState, IChatGui chat, Configuration configuration)
    {
        this.session = session;
        this.signals = signals;
        this.framework = framework;
        this.clientState = clientState;
        this.chat = chat;
        this.configuration = configuration;
        signals.ServerRestarting += OnServerRestarting;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnServerRestarting()
    {
        restartAnnounced = true;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            if (tracking)
            {
                tracking = false;
                tracker.Reset();
            }

            restartAnnounced = false;
            return;
        }

        tracking = true;
        var now = Environment.TickCount64;
        var online = signals.RealtimeActive;
        if (online != tracker.Online)
        {
            if (online)
            {
                tracker.Connected(now);
            }
            else
            {
                tracker.Disconnected(now);
            }
        }

        if (restartAnnounced)
        {
            restartAnnounced = false;
            tracker.RestartAnnounced(now);
        }

        var notice = tracker.Tick(now);
        if (notice == NetworkNotice.None || !configuration.NetworkStatusInChat || !clientState.IsLoggedIn)
        {
            return;
        }

        chat.Print(MessageFor(notice), AepConstants.DisplayName, AepConstants.ChatTagUiColor);
    }

    private static SeString MessageFor(NetworkNotice notice) => notice switch
    {
        NetworkNotice.Restarting => Message(L.Plugin.NetworkRestarting, RestartingUiColor),
        NetworkNotice.Lost => Message(L.Plugin.NetworkLost, LostUiColor),
        _ => Message(L.Plugin.NetworkBack, BackOnlineUiColor),
    };

    private static SeString Message(LocString text, ushort uiColor) => new SeStringBuilder()
        .AddIcon(BitmapFontIcon.Aethernet)
        .AddText(" ")
        .AddUiForeground(Loc.T(text), uiColor)
        .Build();

    public void Dispose()
    {
        signals.ServerRestarting -= OnServerRestarting;
        framework.Update -= OnFrameworkUpdate;
    }
}
