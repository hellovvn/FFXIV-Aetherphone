using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Team;

internal sealed class TeamThreadState
{
    private readonly object gate = new();
    private TeamMessageDto[] messages = Array.Empty<TeamMessageDto>();
    private int unread;
    private bool canReply;
    private bool canDelete;
    private long clearedThroughId;
    private bool hasMoreOlder;
    private bool loaded;
    private int version;
    private int generation;

    public TeamMessageDto[] Messages => Volatile.Read(ref messages);

    public int Unread => Volatile.Read(ref unread);

    public bool CanReply => Volatile.Read(ref canReply);

    public bool CanDelete => Volatile.Read(ref canDelete);

    public bool HasMoreOlder => Volatile.Read(ref hasMoreOlder);

    public bool Loaded => Volatile.Read(ref loaded);

    public int Version => Volatile.Read(ref version);

    public int Generation => Volatile.Read(ref generation);

    public bool HasThread => Messages.Length > 0;

    public long OldestId
    {
        get
        {
            var snapshot = Messages;
            return snapshot.Length == 0 ? 0 : snapshot[0].Id;
        }
    }

    public long NewestId
    {
        get
        {
            var snapshot = Messages;
            return snapshot.Length == 0 ? 0 : snapshot[snapshot.Length - 1].Id;
        }
    }

    public TeamMessageDto? Newest
    {
        get
        {
            var snapshot = Messages;
            return snapshot.Length == 0 ? null : snapshot[snapshot.Length - 1];
        }
    }

    public bool ApplyLatest(TeamMessagePage page, int pageSize, int expectedGeneration, long knownNewestId)
    {
        var incoming = Ascending(page.Messages);
        lock (gate)
        {
            if (expectedGeneration != generation || page.ClearedThroughId < clearedThroughId)
            {
                return false;
            }

            clearedThroughId = page.ClearedThroughId;
            var current = NewerThan(messages, clearedThroughId, out _);
            incoming = NewerThan(incoming, clearedThroughId, out _);
            var pageNewest = NewestOf(incoming);
            var settledThrough = Math.Max(pageNewest, knownNewestId);
            var arrived = NewerThan(current, settledThrough, out var arrivedUnread);
            var fullPage = incoming.Length >= pageSize;
            var detached = current.Length > 0 && fullPage && incoming[0].Id > NewestOf(current);
            if (!loaded || detached)
            {
                hasMoreOlder = fullPage;
                messages = Merge(arrived, incoming);
            }
            else
            {
                var coveredFrom = fullPage ? incoming[0].Id : long.MinValue;
                messages = Merge(Prune(current, coveredFrom, settledThrough, incoming), incoming);
                if (!fullPage)
                {
                    hasMoreOlder = false;
                }
            }

            unread = Math.Max(0, page.Unread) + arrivedUnread;
            canReply = page.CanReply || HasTeamMessage(arrived);
            canDelete = page.CanDelete && !HasTeamMessage(arrived);
            loaded = true;
            version++;
            return true;
        }
    }

    public bool ApplyOlder(TeamMessagePage page, int pageSize, int expectedGeneration)
    {
        var incoming = Ascending(page.Messages);
        lock (gate)
        {
            if (expectedGeneration != generation || page.ClearedThroughId != clearedThroughId)
            {
                return false;
            }

            messages = Merge(messages, NewerThan(incoming, clearedThroughId, out _));
            hasMoreOlder = incoming.Length >= pageSize;
            version++;
            return true;
        }
    }

    public bool ApplyIncoming(TeamMessageDto message, int expectedGeneration)
    {
        lock (gate)
        {
            if (expectedGeneration != generation || message.Id <= clearedThroughId)
            {
                return false;
            }

            var current = messages;
            var position = IndexOf(current, message.Id);
            if (position >= 0)
            {
                var updated = new TeamMessageDto[current.Length];
                Array.Copy(current, updated, current.Length);
                updated[position] = message;
                messages = updated;
                version++;
                return false;
            }

            messages = Merge(current, new[] { message });
            if (message.FromTeam)
            {
                canReply = true;
                canDelete = false;
                if (!message.Read)
                {
                    unread++;
                }
            }

            version++;
            return true;
        }
    }

    public bool MarkAllRead()
    {
        lock (gate)
        {
            var current = messages;
            var changed = unread > 0;
            TeamMessageDto[]? updated = null;
            for (var index = 0; index < current.Length; index++)
            {
                var message = current[index];
                if (!message.FromTeam || message.Read)
                {
                    continue;
                }

                if (updated is null)
                {
                    updated = new TeamMessageDto[current.Length];
                    Array.Copy(current, updated, current.Length);
                }

                updated[index] = message with { Read = true };
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            if (updated is not null)
            {
                messages = updated;
            }

            unread = 0;
            version++;
            return true;
        }
    }

    public void CloseReplies()
    {
        lock (gate)
        {
            if (!canReply)
            {
                return;
            }

            canReply = false;
            version++;
        }
    }

    public bool ApplyCleared(long through, int expectedGeneration)
    {
        lock (gate)
        {
            if (expectedGeneration != generation)
            {
                return false;
            }

            clearedThroughId = Math.Max(clearedThroughId, through);
            messages = NewerThan(messages, clearedThroughId, out unread);
            canReply = HasTeamMessage(messages);
            canDelete = false;
            hasMoreOlder = false;
            version++;
            generation++;
            return true;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            messages = Array.Empty<TeamMessageDto>();
            unread = 0;
            canReply = false;
            canDelete = false;
            clearedThroughId = 0;
            hasMoreOlder = false;
            loaded = false;
            version++;
            generation++;
        }
    }

    private static TeamMessageDto[] NewerThan(TeamMessageDto[] items, long settledThrough, out int unreadCount)
    {
        unreadCount = 0;
        var first = items.Length;
        while (first > 0 && items[first - 1].Id > settledThrough)
        {
            first--;
        }

        if (first == items.Length)
        {
            return Array.Empty<TeamMessageDto>();
        }

        var newer = new TeamMessageDto[items.Length - first];
        Array.Copy(items, first, newer, 0, newer.Length);
        for (var index = 0; index < newer.Length; index++)
        {
            if (newer[index].FromTeam && !newer[index].Read)
            {
                unreadCount++;
            }
        }

        return newer;
    }

    private static TeamMessageDto[] Prune(TeamMessageDto[] items, long coveredFrom, long coveredThrough,
        TeamMessageDto[] page)
    {
        TeamMessageDto[]? kept = null;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var covered = item.Id >= coveredFrom && item.Id <= coveredThrough;
            if (covered && IndexOf(page, item.Id) < 0)
            {
                if (kept is null)
                {
                    kept = new TeamMessageDto[items.Length];
                    Array.Copy(items, kept, index);
                    count = index;
                }

                continue;
            }

            if (kept is not null)
            {
                kept[count] = item;
            }

            count++;
        }

        if (kept is null)
        {
            return items;
        }

        var trimmed = new TeamMessageDto[count];
        Array.Copy(kept, trimmed, count);
        return trimmed;
    }

    private static bool HasTeamMessage(TeamMessageDto[] items)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].FromTeam)
            {
                return true;
            }
        }

        return false;
    }

    private static long NewestOf(TeamMessageDto[] items) => items.Length == 0 ? 0 : items[items.Length - 1].Id;

    private static int IndexOf(TeamMessageDto[] items, long id)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static TeamMessageDto[] Ascending(TeamMessageDto[]? source)
    {
        if (source is null || source.Length == 0)
        {
            return Array.Empty<TeamMessageDto>();
        }

        var copy = new TeamMessageDto[source.Length];
        Array.Copy(source, copy, source.Length);
        Array.Sort(copy, CompareById);
        return Distinct(copy);
    }

    private static TeamMessageDto[] Merge(TeamMessageDto[] existing, TeamMessageDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var combined = new TeamMessageDto[existing.Length + incoming.Length];
        var count = 0;
        var existingIndex = 0;
        var incomingIndex = 0;
        while (existingIndex < existing.Length || incomingIndex < incoming.Length)
        {
            if (incomingIndex >= incoming.Length)
            {
                combined[count++] = existing[existingIndex++];
                continue;
            }

            if (existingIndex >= existing.Length)
            {
                combined[count++] = incoming[incomingIndex++];
                continue;
            }

            var existingId = existing[existingIndex].Id;
            var incomingId = incoming[incomingIndex].Id;
            if (existingId < incomingId)
            {
                combined[count++] = existing[existingIndex++];
                continue;
            }

            if (existingId == incomingId)
            {
                existingIndex++;
            }

            combined[count++] = incoming[incomingIndex++];
        }

        if (count == combined.Length)
        {
            return combined;
        }

        var trimmed = new TeamMessageDto[count];
        Array.Copy(combined, trimmed, count);
        return trimmed;
    }

    private static int CompareById(TeamMessageDto left, TeamMessageDto right) => left.Id.CompareTo(right.Id);

    private static TeamMessageDto[] Distinct(TeamMessageDto[] sorted)
    {
        var count = 0;
        for (var index = 0; index < sorted.Length; index++)
        {
            if (count > 0 && sorted[count - 1].Id == sorted[index].Id)
            {
                continue;
            }

            sorted[count++] = sorted[index];
        }

        if (count == sorted.Length)
        {
            return sorted;
        }

        var trimmed = new TeamMessageDto[count];
        Array.Copy(sorted, trimmed, count);
        return trimmed;
    }
}
