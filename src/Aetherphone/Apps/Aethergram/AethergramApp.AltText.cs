using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float AltTextPreviewPixels = 96f;

    private readonly InfoSheet altTextViewer = new();
    private readonly string[] altTextViewerParagraphs = new string[1];
    private readonly AltTextEditor altTextEditor = new();
    private readonly Dictionary<string, string> composeAltTexts = new(StringComparer.Ordinal);
    private string[] editPostAltTexts = Array.Empty<string>();
    private int altTextPhotoIndex;
    private bool altTextEditsPost;

    private bool AltTextCapturesPointer => altTextEditor.CapturesPointer;

    private void GateAltText() => altTextViewer.Gate();

    private void CloseAltText()
    {
        altTextEditor.Close();
        altTextViewer.Close();
    }

    private void DrawAltText(Rect screen)
    {
        if (altTextEditor.CapturesPointer && altTextEditor.Draw(screen, theme, AltTextPreview(), out var saved))
        {
            if (altTextEditsPost)
            {
                if (altTextPhotoIndex < editPostAltTexts.Length)
                {
                    editPostAltTexts[altTextPhotoIndex] = saved;
                }
            }
            else
            {
                PostAltTexts.Set(composeAltTexts, altTextEditor.Key, saved);
            }
        }

        if (!altTextViewer.CapturesPointer)
        {
            return;
        }

        altTextViewer.Draw(screen, Ink, Loc.T(L.Chirper.AltTextTitle), altTextViewerParagraphs,
            Loc.T(L.Aethergram.Done));
    }

    private IDalamudTextureWrap? AltTextPreview()
    {
        if (altTextEditsPost)
        {
            return images.Sized(altTextEditor.Key, AltTextPreviewPixels * UiScale.Current);
        }

        if (composeReelMode)
        {
            return reelEditor.Frames.Preview;
        }

        return composeSession.TryGetPreviewUv(altTextPhotoIndex, ComposeAspect, ComposeAllowsReveal, out var texture,
            out _, out _)
            ? texture
            : null;
    }

    private bool DrawGramAltBadge(Rect imageRect, PostDto post, int photoIndex)
    {
        var text = PostAltTexts.At(post, photoIndex);
        if (text.Length == 0 || !AltBadge.Draw(ImGui.GetWindowDrawList(), imageRect))
        {
            return false;
        }

        altTextViewerParagraphs[0] = text;
        altTextViewer.Open();
        return true;
    }

    private void DrawComposeAltChip(Rect preview)
    {
        if (composeStoryMode || composeAvatarMode || preview.Width <= 0f || preview.Height <= 0f)
        {
            return;
        }

        var index = composeSession.ClampedPreviewIndex;
        var path = composeSession.PathAt(index);
        if (path.Length == 0)
        {
            return;
        }

        var text = PostAltTexts.For(composeAltTexts, path);
        if (DrawAltChip(preview, text))
        {
            altTextEditsPost = false;
            altTextPhotoIndex = index;
            altTextEditor.Open(path, text);
        }
    }

    private void DrawEditPostAltChip(Rect preview, string[] photos, int page)
    {
        if (page < 0 || page >= photos.Length || page >= editPostAltTexts.Length)
        {
            return;
        }

        var text = editPostAltTexts[page];
        if (DrawAltChip(preview, text))
        {
            altTextEditsPost = true;
            altTextPhotoIndex = page;
            altTextEditor.Open(photos[page], text);
        }
    }

    private bool DrawAltChip(Rect preview, string text)
    {
        var described = text.Length > 0;
        return AltBadge.DrawEditable(ImGui.GetWindowDrawList(), preview, described, Accent,
            Loc.T(described ? L.Chirper.AltTextEdit : L.Chirper.AltTextAdd));
    }

    private void BeginEditPostAltTexts(PostDto post, int photoCount)
    {
        editPostAltTexts = new string[photoCount];
        for (var index = 0; index < photoCount; index++)
        {
            editPostAltTexts[index] = PostAltTexts.At(post, index);
        }
    }

    private string[]? EditPostAltTextChanges(PostDto post) =>
        PostAltTexts.Same(post.AltTexts, editPostAltTexts, editPostAltTexts.Length)
            ? null
            : (string[])editPostAltTexts.Clone();
}
