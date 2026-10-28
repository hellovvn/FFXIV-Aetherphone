using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperStore
{
    private volatile ScheduledPostDto[] scheduled = Array.Empty<ScheduledPostDto>();
    private volatile AepFailureBox? scheduledFailure;
    private volatile bool scheduledLoading;
    private volatile bool scheduledLoaded;
    private int scheduledVersion;

    public ScheduledPostDto[] Scheduled => scheduled;

    public bool ScheduledLoading => scheduledLoading;

    public bool ScheduledLoaded => scheduledLoaded;

    public AepFailure ScheduledFailure => scheduledFailure?.Failure ?? AepFailure.None;

    public int ScheduledVersion => Volatile.Read(ref scheduledVersion);

    public void LoadScheduled()
    {
        if (scheduledLoading)
        {
            return;
        }

        scheduledLoading = true;
        work.Run("scheduled list", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.ScheduledPostsAsync(token, failure => reported = failure).ConfigureAwait(false);
            if (page is null)
            {
                scheduledFailure = new AepFailureBox(reported.Failed
                    ? reported
                    : AepFailure.Transport(AepFailureKind.Offline));
                return;
            }

            scheduledFailure = null;
            scheduledLoaded = true;
            PublishScheduled(page.Items);
        }, () => scheduledLoading = false);
    }

    public void SchedulePost(CreatePostRequest post, long publishAtUnix, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        work.Run("schedule chirp", async token =>
        {
            var created = await client.SchedulePostAsync(post, publishAtUnix, token, onFailure).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            PublishScheduled(InsertByTime(scheduled, created));
            return true;
        }, onComplete, () => posting = false);
    }

    // The server answers an edit with a new id (it replaces the row), so the old entry is swapped out.
    public void UpdateScheduled(string scheduledId, CreatePostRequest post, long publishAtUnix, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        work.Run("edit scheduled chirp", async token =>
        {
            var updated = await client.UpdateScheduledPostAsync(scheduledId, post, publishAtUnix, token, onFailure)
                .ConfigureAwait(false);
            if (updated is null)
            {
                LoadScheduled();
                return false;
            }

            PublishScheduled(InsertByTime(CopyOnWrite.RemoveWhere(scheduled, item => item.Id == scheduledId), updated));
            return true;
        }, onComplete, () => posting = false);
    }

    public void DeleteScheduled(string scheduledId)
    {
        PublishScheduled(CopyOnWrite.RemoveWhere(scheduled, item => item.Id == scheduledId));
        work.Run("scheduled delete", async token =>
        {
            if (!await client.DeleteScheduledPostAsync(scheduledId, token).ConfigureAwait(false))
            {
                LoadScheduled();
            }
        });
    }

    private void ResetScheduled()
    {
        scheduledFailure = null;
        scheduledLoaded = false;
        PublishScheduled(Array.Empty<ScheduledPostDto>());
    }

    private void PublishScheduled(ScheduledPostDto[] items)
    {
        scheduled = items;
        Interlocked.Increment(ref scheduledVersion);
    }

    private static ScheduledPostDto[] InsertByTime(ScheduledPostDto[] source, ScheduledPostDto item)
    {
        var result = new ScheduledPostDto[source.Length + 1];
        var written = 0;
        var placed = false;
        for (var index = 0; index < source.Length; index++)
        {
            if (!placed && item.PublishAtUnix < source[index].PublishAtUnix)
            {
                result[written++] = item;
                placed = true;
            }

            result[written++] = source[index];
        }

        if (!placed)
        {
            result[written] = item;
        }

        return result;
    }
}
