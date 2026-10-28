using Aetherphone.Core.Media;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Reels;

// The pictures the reel editor shows: a strip of frames across the whole clip and one larger frame at the time the
// editor points at. Requests coalesce, so dragging a handle decodes at most one frame at a time.
internal sealed class ReelEditorFrames : IDisposable
{
    public const int StripCount = 8;

    private const int StripLongSide = 160;
    private const int PreviewLongSide = 720;
    private const double SameFrameSeconds = 0.04d;

    private readonly IReelMedia media;
    private readonly IDalamudTextureWrap?[] strip = new IDalamudTextureWrap?[StripCount];
    private CancellationTokenSource cancellation = new();
    private volatile IDalamudTextureWrap? preview;
    private string path = string.Empty;
    private int generation;
    private double shownSeconds = double.NaN;
    private bool shownExact;
    private volatile bool previewBusy;

    public ReelEditorFrames(IReelMedia media)
    {
        this.media = media;
    }

    public bool IsOpen => path.Length > 0;

    public IDalamudTextureWrap? Preview => preview;

    public IDalamudTextureWrap? StripAt(int index) => Volatile.Read(ref strip[index]);

    public void Open(string source, double durationSeconds)
    {
        Close();
        path = source;
        var ticket = generation;
        var times = new double[StripCount];
        for (var index = 0; index < StripCount; index++)
        {
            times[index] = durationSeconds * (index + 0.5d) / StripCount;
        }

        var token = cancellation.Token;
        _ = Task.Run(() => LoadStripAsync(source, times, ticket, token), CancellationToken.None);
    }

    // While a control is held the frame comes from the nearest keyframe, which decodes at once; the exact frame
    // follows when it is let go.
    public void Show(double seconds, bool exact)
    {
        var same = Math.Abs(seconds - shownSeconds) < SameFrameSeconds && (shownExact || !exact);
        if (!IsOpen || (same && preview is not null))
        {
            return;
        }

        if (previewBusy)
        {
            return;
        }

        previewBusy = true;
        shownSeconds = seconds;
        shownExact = exact;
        var ticket = generation;
        var source = path;
        var token = cancellation.Token;
        _ = Task.Run(() => LoadPreviewAsync(source, seconds, exact, ticket, token), CancellationToken.None);
    }

    public void Close()
    {
        Interlocked.Increment(ref generation);
        cancellation.Cancel();
        cancellation.Dispose();
        cancellation = new CancellationTokenSource();
        path = string.Empty;
        shownSeconds = double.NaN;
        previewBusy = false;
        DeferredDispose.Later(Interlocked.Exchange(ref preview, null));
        for (var index = 0; index < StripCount; index++)
        {
            DeferredDispose.Later(Interlocked.Exchange(ref strip[index], null));
        }
    }

    public void Dispose()
    {
        Close();
        cancellation.Dispose();
    }

    private async Task LoadStripAsync(string source, double[] times, int ticket, CancellationToken token)
    {
        var result = await media.ThumbnailsAsync(source, times, StripLongSide, false, token).ConfigureAwait(false);
        var frames = result.Frames;
        for (var index = 0; index < frames.Length && index < StripCount; index++)
        {
            var texture = await TextureOf(frames[index], token).ConfigureAwait(false);
            if (texture is null)
            {
                continue;
            }

            if (Volatile.Read(ref generation) != ticket)
            {
                texture.Dispose();
                return;
            }

            DeferredDispose.Later(Interlocked.Exchange(ref strip[index], texture));
        }
    }

    private async Task LoadPreviewAsync(string source, double seconds, bool exact, int ticket,
        CancellationToken token)
    {
        try
        {
            var result = await media.ThumbnailsAsync(source, [seconds], PreviewLongSide, exact, token)
                .ConfigureAwait(false);
            if (result.Frames.Length == 0)
            {
                return;
            }

            var texture = await TextureOf(result.Frames[0], token).ConfigureAwait(false);
            if (texture is null)
            {
                return;
            }

            if (Volatile.Read(ref generation) != ticket)
            {
                texture.Dispose();
                return;
            }

            DeferredDispose.Later(Interlocked.Exchange(ref preview, texture));
        }
        finally
        {
            if (Volatile.Read(ref generation) == ticket)
            {
                previewBusy = false;
            }
        }
    }

    private static async Task<IDalamudTextureWrap?> TextureOf(PixelImage frame, CancellationToken token)
    {
        if (frame.IsEmpty || token.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            return await Plugin.TextureProvider.CreateFromRawAsync(RawImageSpecification.Rgba32(frame.Width,
                frame.Height), frame.Pixels, "Reel editor frame", token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Reels] could not make a texture for a reel frame");
            return null;
        }
    }
}
