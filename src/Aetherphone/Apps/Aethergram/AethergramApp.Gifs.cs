using Aetherphone.Core;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private readonly GifSearch gifs;
    private GifPickerSheet? gifSheet;
    private GifPick? commentGifRestore;
    private Action? commentGifSearch;

    private GifPickerSheet GifSheet => gifSheet ??= new GifPickerSheet(gifs, images, configuration);

    private bool GifSheetCaptures => gifSheet?.CapturesPointer ?? false;

    private Action CommentGifSearch => commentGifSearch ??= OpenCommentGifPicker;

    private void OpenCommentGifPicker()
    {
        GifSheet.Open();
    }

    private void PrepareCommentGifs()
    {
        if (store.IsSignedIn)
        {
            gifs.EnsureSettings();
        }

        commentAttachment.SearchGifs = gifs.Available ? CommentGifSearch : null;
        if (Interlocked.Exchange(ref commentGifRestore, null) is { } restored)
        {
            commentAttachment.AttachGif(restored);
        }
    }

    private void DrawGifSheet(Rect screen)
    {
        if (gifSheet is null)
        {
            return;
        }

        gifSheet.Draw(screen, ui);
        if (gifSheet.TakePick() is not { } pick)
        {
            return;
        }

        commentAttachment.AttachGif(pick);
        commentFocusPending = true;
    }

    private void SubmitCommentGif(string postId, string text, GifPick gif)
    {
        store.AddGifComment(postId, text, gif.Item, accepted =>
        {
            if (accepted)
            {
                gifs.RegisterShare(gif);
                return;
            }

            commentRestore = text;
            commentGifRestore = gif;
        }, commentFailure.Set);
    }

    private bool HeldGif(string url) => KlipyReveals.ShouldHold(url, configuration.KlipyGifsAllowed);

    private void OpenCommentMedia(string url, bool held)
    {
        if (held)
        {
            KlipyReveals.Reveal(url);
            return;
        }

        photoViewer.Open(this, () => GifMedia.Texture(images, url, ImGui.GetTime()));
    }
}
