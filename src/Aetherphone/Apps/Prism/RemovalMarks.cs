using System.Collections.Concurrent;

namespace Aetherphone.Apps.Prism;

internal sealed class RemovalMarks
{
    private readonly ConcurrentDictionary<string, long> marks = new(StringComparer.Ordinal);
    private long stamp;

    public long Current => Interlocked.Read(ref stamp);

    public void Mark(string id) => marks[id] = Interlocked.Increment(ref stamp);

    public bool Contains(string id) => marks.ContainsKey(id);

    public bool Hidden(string id, long requestedAfter)
    {
        if (!marks.TryGetValue(id, out var marked))
        {
            return false;
        }

        if (marked > requestedAfter)
        {
            return true;
        }

        marks.TryRemove(new KeyValuePair<string, long>(id, marked));
        return false;
    }
}
