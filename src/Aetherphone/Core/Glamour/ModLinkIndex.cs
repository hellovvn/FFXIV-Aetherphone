namespace Aetherphone.Core.Glamour;

internal sealed class ModLinkIndex : IDisposable
{
    private const int NeverBuilt = -1;

    private CancellationTokenSource? building;
    private volatile Dictionary<string, string>? links;
    private int builtFor = NeverBuilt;
    private int readySignaled;

    public Dictionary<string, string>? Links => links;

    public bool Stale(int version) => builtFor != version;

    public void Build(int version, string root, Dictionary<string, string> installed)
    {
        builtFor = version;
        if (root.Length == 0)
        {
            return;
        }

        Cancel();
        var folders = new string[installed.Count];
        installed.Keys.CopyTo(folders, 0);
        building = new CancellationTokenSource();
        _ = BuildAsync(root, folders, building.Token);
    }

    public bool TakeReady() => Interlocked.Exchange(ref readySignaled, 0) != 0;

    public void Dispose() => Cancel();

    private void Cancel()
    {
        if (building is not { } running)
        {
            return;
        }

        running.Cancel();
        running.Dispose();
        building = null;
    }

    private async Task BuildAsync(string root, string[] folders, CancellationToken token)
    {
        try
        {
            var read = await Task.Run(() => Read(root, folders, token), token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            links = read;
            Interlocked.Exchange(ref readySignaled, 1);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Could not read the Penumbra mod websites");
        }
    }

    private static Dictionary<string, string> Read(string root, string[] folders, CancellationToken token)
    {
        var read = new Dictionary<string, string>(folders.Length, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < folders.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var link = ModAttribution.Describe(root, folders[index], string.Empty).Link;
            if (link.Length > 0)
            {
                read[folders[index]] = link;
            }
        }

        return read;
    }
}
