using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Team;

internal sealed class TeamInboxStore : IDisposable
{
    public const string ThreadKey = "aetherphone.team";
    public const int PageSize = 30;
    public const int MessageMax = 1000;
    public const string ThreadClosedCode = "team_thread_closed";
    private const int ForbiddenStatus = 403;
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan ReadRetryFirst = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadRetryLongest = TimeSpan.FromMinutes(5);

    private readonly AethernetSession session;
    private readonly ITeamInboxApi api;
    private readonly IFramework framework;
    private readonly NotificationService notifications;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly string notificationAppId;
    private readonly Vector4 accent;
    private readonly TeamThreadState state = new();
    private readonly ViewingMark viewing = new();
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool refreshing;
    private volatile bool loadingOlder;
    private volatile bool sending;
    private volatile bool deleting;
    private volatile bool markingRead;
    private volatile bool readOwed;
    private volatile bool readAgain;
    private volatile bool primed;
    private volatile AepFailureBox? sendFailure;
    private volatile string? returnedDraft;
    private long notifiedThroughId;
    private long readRetryAtTicks;
    private long readRetryDelayTicks = ReadRetryFirst.Ticks;
    private string? lastAccountId;

    public TeamInboxStore(AethernetSession session, ITeamInboxApi api, IFramework framework,
        NotificationService notifications, PhoneVisibility visibility, RealtimeSignalBus signals,
        string notificationAppId, Vector4 accent)
    {
        this.session = session;
        this.api = api;
        this.framework = framework;
        this.notifications = notifications;
        this.signals = signals;
        this.notificationAppId = notificationAppId;
        this.accent = accent;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval, signals);
        signals.TeamMessaged += OnTeamMessage;
        signals.ConnectedChanged += OnRealtimeConnected;
        session.Changed += OnSessionChanged;
        framework.Update += OnFrameworkTick;
    }

    public TeamMessageDto[] Messages => state.Messages;

    public TeamMessageDto? Newest => state.Newest;

    public int Unread => session.IsSignedIn ? state.Unread : 0;

    public bool CanReply => state.CanReply;

    public bool CanDelete => session.IsSignedIn && state.CanDelete && !deleting;

    public bool HasThread => session.IsSignedIn && state.HasThread;

    public bool Loaded => state.Loaded;

    public bool HasMoreOlder => state.HasMoreOlder;

    public bool LoadingOlder => loadingOlder;

    public bool Loading => refreshing && !state.Loaded;

    public bool Sending => sending;

    public int Version => state.Version;

    public int AccountGeneration => state.Generation;

    public AepFailureBox? SendFailure => sendFailure;

    public static bool IsThreadClosed(in AepFailure failure) =>
        string.Equals(failure.Code, ThreadClosedCode, StringComparison.Ordinal)
        || failure.StatusCode == ForbiddenStatus;

    public static bool ShouldReset(string? previousAccountId, string? currentAccountId) =>
        !string.Equals(previousAccountId, currentAccountId, StringComparison.Ordinal);

    public static bool ShouldPoll(bool refreshing, PollCadence cadence, DateTime nowUtc) =>
        !refreshing && cadence.Due(nowUtc);

    public static bool RaiseTo(ref long location, long candidate)
    {
        var current = Interlocked.Read(ref location);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref location, candidate, current);
            if (observed == current)
            {
                return true;
            }

            current = observed;
        }

        return false;
    }

    public void Refresh()
    {
        if (session.IsSignedIn)
        {
            cadence.RequestImmediate();
        }
    }

    public void NoteViewed()
    {
        viewing.Note(ThreadKey);
        cadence.NoteWatched();
        if (state.Unread > 0)
        {
            MarkRead();
        }
    }

    public void OnThreadOpened()
    {
        viewing.Note(ThreadKey);
        notifications.RemoveGroup(notificationAppId, ThreadKey);
        Refresh();
        MarkRead();
    }

    public string? TakeReturnedDraft()
    {
        var draft = returnedDraft;
        returnedDraft = null;
        return draft;
    }

    public void ClearSendFailure()
    {
        sendFailure = null;
    }

    public void DeleteThread(long through, Action<bool, AepFailure> onDone)
    {
        if (!CanDelete || through <= 0)
        {
            return;
        }

        deleting = true;
        var generation = state.Generation;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            TeamConversationClearedDto? deleted = null;
            var failure = AepFailure.None;
            try
            {
                deleted = await api.DeleteAsync(through, token, value => failure = value).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Team] delete failed");
            }
            finally
            {
                await framework.RunOnFrameworkThread(() =>
                {
                    deleting = false;
                    if (generation != state.Generation)
                    {
                        return;
                    }

                    if (deleted is not null)
                    {
                        state.ApplyCleared(deleted.ClearedThroughId, generation);
                        returnedDraft = null;
                        sendFailure = null;
                        notifications.RemoveGroup(notificationAppId, ThreadKey);
                    }

                    cadence.RequestImmediate();
                    if (deleted is null && !failure.Failed)
                    {
                        failure = AepFailure.Transport(AepFailureKind.BadResponse);
                    }

                    onDone(deleted is not null, failure);
                }).ConfigureAwait(false);
            }
        });
    }

    public void LoadOlder()
    {
        if (loadingOlder || !state.HasMoreOlder || !session.IsSignedIn)
        {
            return;
        }

        var before = state.OldestId;
        if (before <= 0)
        {
            return;
        }

        loadingOlder = true;
        var generation = state.Generation;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await api.ListAsync(before, PageSize, token).ConfigureAwait(false);
                if (page is not null)
                {
                    state.ApplyOlder(page, PageSize, generation);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Team] older page failed");
            }
            finally
            {
                loadingOlder = false;
            }
        });
    }

    public bool Send(string text)
    {
        var body = text.Trim();
        if (body.Length == 0 || body.Length > MessageMax || sending || !state.CanReply || !session.IsSignedIn)
        {
            return false;
        }

        sending = true;
        sendFailure = null;
        var generation = state.Generation;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var failure = AepFailure.None;
            try
            {
                var sent = await api.SendAsync(body, token, value => failure = value).ConfigureAwait(false);
                if (generation != state.Generation)
                {
                    return;
                }

                if (sent is not null)
                {
                    state.ApplyIncoming(sent, generation);
                    return;
                }

                if (IsThreadClosed(failure))
                {
                    state.CloseReplies();
                }

                returnedDraft = body;
                sendFailure = new AepFailureBox(failure.Failed
                    ? failure
                    : AepFailure.Transport(AepFailureKind.BadResponse));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Team] send failed");
                returnedDraft = body;
                sendFailure = new AepFailureBox(AepFailure.Transport(AepFailureKind.BadResponse));
            }
            finally
            {
                sending = false;
            }
        });
        return true;
    }

    private void MarkRead()
    {
        if (!session.IsSignedIn || !state.MarkAllRead())
        {
            return;
        }

        notifications.RemoveGroup(notificationAppId, ThreadKey);
        readOwed = true;
        SendRead();
    }

    private void SendRead()
    {
        if (markingRead)
        {
            readAgain = true;
            return;
        }

        markingRead = true;
        var generation = state.Generation;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var acknowledged = false;
            try
            {
                acknowledged = await api.MarkReadAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Team] mark read failed");
            }
            finally
            {
                if (generation == state.Generation)
                {
                    SettleRead(acknowledged);
                }

                markingRead = false;
            }
        });
    }

    private void SettleRead(bool acknowledged)
    {
        if (acknowledged)
        {
            Interlocked.Exchange(ref readRetryDelayTicks, ReadRetryFirst.Ticks);
            if (readAgain)
            {
                readAgain = false;
                Interlocked.Exchange(ref readRetryAtTicks, 0);
                return;
            }

            readOwed = false;
            return;
        }

        var delay = Interlocked.Read(ref readRetryDelayTicks);
        Interlocked.Exchange(ref readRetryAtTicks, DateTime.UtcNow.Ticks + delay);
        Interlocked.Exchange(ref readRetryDelayTicks, Math.Min(delay * 2, ReadRetryLongest.Ticks));
    }

    private void OnTeamMessage(TeamMessageDto? message)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        cadence.RequestImmediate();

        if (message is null)
        {
            return;
        }

        if (!state.ApplyIncoming(message, state.Generation) || !message.FromTeam)
        {
            return;
        }

        _ = framework.RunOnFrameworkThread(() => Announce(message));
    }

    private void Announce(TeamMessageDto message)
    {
        if (!RaiseTo(ref notifiedThroughId, message.Id))
        {
            return;
        }

        if (viewing.Covers(ThreadKey))
        {
            MarkRead();
            return;
        }

        if (message.Read)
        {
            return;
        }

        notifications.Notify(new PhoneNotification(notificationAppId, Loc.T(L.TeamInbox.Name),
            TeamMessageText.Render(message), DateTime.Now, accent, ThreadKey));
    }

    private void OnRealtimeConnected(bool active)
    {
        if (active)
        {
            cadence.RequestAfterReconnect();
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!ShouldReset(lastAccountId, accountId))
        {
            return;
        }

        lastAccountId = accountId;
        ResetForAccount();
        cadence.RequestImmediate();
    }

    private void ResetForAccount()
    {
        state.Clear();
        primed = false;
        readOwed = false;
        readAgain = false;
        sendFailure = null;
        returnedDraft = null;
        Interlocked.Exchange(ref notifiedThroughId, 0);
        Interlocked.Exchange(ref readRetryDelayTicks, ReadRetryFirst.Ticks);
        viewing.Clear();
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            if (state.Loaded || state.HasThread)
            {
                lastAccountId = null;
                ResetForAccount();
            }

            return;
        }

        var now = DateTime.UtcNow;
        if (readOwed && !markingRead && now.Ticks >= Interlocked.Read(ref readRetryAtTicks))
        {
            SendRead();
        }

        if (!ShouldPoll(refreshing, cadence, now))
        {
            return;
        }

        Poll();
    }

    private void Poll()
    {
        refreshing = true;
        var generation = state.Generation;
        var knownNewestId = state.NewestId;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await api.ListAsync(0, PageSize, token).ConfigureAwait(false);
                if (page is null || !state.ApplyLatest(page, PageSize, generation, knownNewestId))
                {
                    return;
                }

                if (readOwed)
                {
                    state.MarkAllRead();
                }

                if (!primed)
                {
                    primed = true;
                    RaiseTo(ref notifiedThroughId, state.NewestId);
                    return;
                }

                if (state.Newest is { FromTeam: true, Read: false } newest)
                {
                    _ = framework.RunOnFrameworkThread(() => Announce(newest));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Team] poll failed");
            }
            finally
            {
                refreshing = false;
            }
        });
    }

    public void Dispose()
    {
        signals.TeamMessaged -= OnTeamMessage;
        signals.ConnectedChanged -= OnRealtimeConnected;
        session.Changed -= OnSessionChanged;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
