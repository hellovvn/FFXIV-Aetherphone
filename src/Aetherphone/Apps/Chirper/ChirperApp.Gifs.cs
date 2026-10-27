using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private enum GifTarget : byte
    {
        Compose,
        Comment,
    }

    private readonly GifSearch gifSearch;
    private readonly ActionSheet gifSourceSheet = new();
    private readonly ActionSheet.Item[] gifSourceItems = new ActionSheet.Item[2];
    private GifPickerSheet? gifSheet;
    private GifTarget gifTarget;
    private GifPick? composeGif;
    private GifPick? commentGifRestore;
    private Action? commentGifSearch;

    private GifPickerSheet GifSheet => gifSheet ??= new GifPickerSheet(gifSearch, images, configuration);

    private bool GifSheetsCapture => gifSourceSheet.CapturesPointer || (gifSheet?.CapturesPointer ?? false);

    private Action CommentGifSearch => commentGifSearch ??= OpenCommentGifPicker;

    private void DrawGifSheets(Rect screen)
    {
        if (store.IsSignedIn)
        {
            gifSearch.EnsureSettings();
        }

        DrawGifSource(screen);
        if (gifSheet is null)
        {
            return;
        }

        gifSheet.Draw(screen, ui);
        if (gifSheet.TakePick() is { } pick)
        {
            AttachGif(pick);
        }
    }

    private void DrawGifSource(Rect screen)
    {
        if (!gifSourceSheet.CapturesPointer)
        {
            return;
        }

        gifSourceItems[0] = new ActionSheet.Item(Loc.T(L.Gifs.Search), IconGlyph.Of(FontAwesomeIcon.Search));
        gifSourceItems[1] = new ActionSheet.Item(Loc.T(L.Gifs.FromPc), IconGlyph.Of(FontAwesomeIcon.Desktop));
        var picked = gifSourceSheet.Draw(screen, SheetStyle, gifSourceItems, Loc.T(L.Common.Cancel), false);
        if (picked == 0)
        {
            OpenGifPicker(gifTarget);
        }
        else if (picked == 1)
        {
            OpenComposePicker(true);
        }
    }

    private void CloseGifSheets()
    {
        gifSourceSheet.Close();
        gifSheet?.Close();
    }

    private void OpenComposeGifSource()
    {
        if (composePoll.Active)
        {
            toast.Show(ComposeGifRefusal());
            return;
        }

        if (!gifSearch.Available)
        {
            OpenComposePicker(true);
            return;
        }

        gifTarget = GifTarget.Compose;
        gifSourceSheet.Open();
    }

    private void OpenCommentGifPicker()
    {
        OpenGifPicker(GifTarget.Comment);
    }

    private void OpenGifPicker(GifTarget target)
    {
        gifTarget = target;
        GifSheet.Open();
    }

    private void AttachGif(GifPick pick)
    {
        if (gifTarget == GifTarget.Comment)
        {
            commentAttachment.AttachGif(pick);
            replyFocusPending = true;
            return;
        }

        if (composePoll.Active)
        {
            toast.Show(ComposeGifRefusal());
            return;
        }

        composeAttachments.Clear();
        composeAttachments.Add(pick.Item.Url);
        composeGif = pick;
        composePicking = false;
        composeStatus = string.Empty;
    }

    private GifPick? AttachedComposeGif()
    {
        return composeGif is { } gif && composeAttachments.Count == 1
               && string.Equals(composeAttachments[0], gif.Item.Url, StringComparison.Ordinal)
            ? gif
            : null;
    }

    private IDalamudTextureWrap? ComposeThumbTexture(string path, float drawnPixels)
    {
        if (composeGif is { } gif && string.Equals(path, gif.Item.Url, StringComparison.Ordinal))
        {
            return images.SizedAt(gif.Item.PreviewUrl, drawnPixels, ImGui.GetTime());
        }

        return wallpaperImages.Get(path);
    }

    private void SubmitComposeGif(GifPick gif)
    {
        if (store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        composeFailure.Clear();
        store.ComposeGif(draft, quoteTargetId, gif.Item, composeSensitive, ok =>
        {
            if (ok)
            {
                gifSearch.RegisterShare(gif);
            }

            composeOutcome = ok ? 1 : 2;
        }, composeFailure.Set);
    }

    private void PrepareCommentGifs()
    {
        commentAttachment.SearchGifs = gifSearch.Available ? CommentGifSearch : null;
        if (Interlocked.Exchange(ref commentGifRestore, null) is { } restored)
        {
            commentAttachment.AttachGif(restored);
        }
    }

    private void SubmitCommentGif(string postId, string text, GifPick gif)
    {
        store.AddGifComment(postId, text, gif.Item, accepted =>
        {
            if (accepted)
            {
                gifSearch.RegisterShare(gif);
                return;
            }

            commentRestore = text;
            commentGifRestore = gif;
        }, commentFailure.Set);
    }

    private bool HeldGif(string? url) => KlipyReveals.ShouldHold(url, configuration.KlipyGifsAllowed);

    private Action<ReportReason>? GifReportForwarder(PostDto post)
    {
        var slug = post.GifSlug;
        if (slug is null || post.GifUrl is null || HeldGif(post.GifUrl))
        {
            return null;
        }

        return reason =>
        {
            if (KlipyReportReasons.For(reason.Category) is { } klipyReason)
            {
                gifSearch.ForwardReport(slug, klipyReason);
            }
        };
    }

    private bool DrawHeldGifTile(ImDrawListPtr drawList, string url, Rect rect, float rounding, ImDrawFlags corners)
    {
        if (!HeldGif(url))
        {
            return false;
        }

        GifHold.Draw(drawList, rect, rounding, corners, ChirperInk.ChipFill);
        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            KlipyReveals.Reveal(url);
        }

        return true;
    }

    private void OpenCommentMedia(string url, bool held)
    {
        if (held)
        {
            KlipyReveals.Reveal(url);
            return;
        }

        photoViewer.Open(this, () => MediaTexture(url));
    }
}
