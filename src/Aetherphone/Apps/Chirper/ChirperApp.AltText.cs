using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float AltTextPreviewPixels = 96f;

    private readonly InfoSheet altTextViewer = new();
    private readonly string[] altTextViewerParagraphs = new string[1];
    private readonly AltTextEditor altTextEditor = new();
    private readonly Dictionary<string, string> composeAltTexts = new(StringComparer.Ordinal);
    private int altTextEditorPhoto = -1;

    private bool AltTextCapturesPointer => altTextEditor.CapturesPointer;

    private void GateAltText() => altTextViewer.Gate();

    private void CloseAltText()
    {
        altTextEditor.Close();
        altTextViewer.Close();
    }

    private void DrawAltText(Rect screen)
    {
        if (altTextEditor.CapturesPointer)
        {
            var path = altTextEditor.Key;
            var editingPosted = altTextEditorPhoto >= 0;
            var preview = editingPosted
                ? MediaTexture(path)
                : ComposeThumbTexture(path, AltTextPreviewPixels * UiScale.Current);
            if (altTextEditor.Draw(screen, theme, preview, out var saved))
            {
                if (!editingPosted)
                {
                    PostAltTexts.Set(composeAltTexts, path, saved);
                }
                else if (altTextEditorPhoto < chirpEditAltTexts.Length)
                {
                    chirpEditAltTexts[altTextEditorPhoto] = saved;
                }
            }
        }

        if (!altTextViewer.CapturesPointer)
        {
            return;
        }

        altTextViewer.Draw(screen, ChirperInk.Shared, Loc.T(L.Chirper.AltTextTitle), altTextViewerParagraphs,
            Loc.T(L.Chirper.Done));
    }

    private void DrawPostAltBadge(ImDrawListPtr drawList, PostDto post, int photoIndex, Rect tile)
    {
        var text = PostAltTexts.At(post, photoIndex);
        if (text.Length == 0 || !AltBadge.Draw(drawList, tile))
        {
            return;
        }

        altTextViewerParagraphs[0] = text;
        altTextViewer.Open();
    }

    private void DrawQuotedAltBadge(ImDrawListPtr drawList, PostDto quoted, Rect thumb)
    {
        var text = PostAltTexts.At(quoted, 0);
        if (text.Length == 0 || !AltBadge.DrawCompact(drawList, thumb))
        {
            return;
        }

        altTextViewerParagraphs[0] = text;
        altTextViewer.Open();
    }

    private void DrawComposeAltChip(ImDrawListPtr drawList, string path, Vector2 min, Vector2 max)
    {
        if (IsComposeGif(path))
        {
            return;
        }

        var text = PostAltTexts.For(composeAltTexts, path);
        var described = text.Length > 0;
        if (AltBadge.DrawEditable(drawList, new Rect(min, max), described, ChirperInk.Accent,
                Loc.T(described ? L.Chirper.AltTextEdit : L.Chirper.AltTextAdd)))
        {
            altTextEditorPhoto = -1;
            altTextEditor.Open(path, text);
        }
    }

    private void DrawChirpEditAltChip(ImDrawListPtr drawList, string url, int photoIndex, Rect tile)
    {
        var text = chirpEditAltTexts[photoIndex];
        var described = text.Length > 0;
        if (AltBadge.DrawEditable(drawList, tile, described, ChirperInk.Accent,
                Loc.T(described ? L.Chirper.AltTextEdit : L.Chirper.AltTextAdd)))
        {
            altTextEditorPhoto = photoIndex;
            altTextEditor.Open(url, text);
        }
    }

    private static string[] EditableAltTexts(PostDto post)
    {
        if (post.GifMedia is not null || post.Video is not null)
        {
            return Array.Empty<string>();
        }

        var count = PostMedia.Photos(post.MediaUrls, post.MediaUrl).Length;
        var texts = new string[count];
        for (var index = 0; index < count; index++)
        {
            texts[index] = PostAltTexts.At(post, index);
        }

        return texts;
    }

    private string[]? ComposeAltTextInputs(string[] attachments) => PostAltTexts.Aligned(attachments, composeAltTexts);
}
