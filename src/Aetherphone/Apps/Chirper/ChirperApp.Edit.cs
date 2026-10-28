using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const string ChirpEditFieldId = "##chirpEditBody";

    private static readonly TextStyle ChirpEditNoteStyle = TextStyles.Footnote;

    private readonly EmojiComposer chirpEditEmoji = new() { IconPainter = PaintEmojiIcon };
    private readonly FailureSlot chirpEditFailure = new();
    private MentionAutocomplete? chirpEditMentions;
    private PostDto? chirpEditTarget;
    private string chirpEditDraft = string.Empty;
    private string[] chirpEditAltTexts = Array.Empty<string>();
    private string chirpEditStatus = string.Empty;
    private CachedText chirpEditsLeftLabel;
    private bool chirpEditFocus;
    private bool chirpEditFieldActive;
    private volatile int chirpEditOutcome;

    private static TranslationKey ChirpTranslationKey(PostDto post) =>
        new(TranslationSurface.Post, post.Id, post.EditedAtUnix ?? 0);

    private bool CanEditChirp(PostDto post) =>
        ChirperEdits.CanEdit(post, store.Me?.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private void BeginChirpEdit(PostDto post)
    {
        chirpEditMentions ??= new MentionAutocomplete(store.NewMentionSuggestions());
        chirpEditTarget = post;
        chirpEditDraft = post.Text;
        chirpEditAltTexts = EditableAltTexts(post);
        chirpEditStatus = string.Empty;
        chirpEditFailure.Clear();
        chirpEditEmoji.Close();
        chirpEditOutcome = 0;
        chirpEditFocus = true;
        router.Push(ChirperRoute.EditChirp(post.Id));
    }

    private void DrawChirpEdit(Rect area, string postId)
    {
        if (chirpEditTarget is not { } post || post.Id != postId)
        {
            return;
        }

        if (chirpEditOutcome == 1)
        {
            chirpEditOutcome = 0;
            chirpEditEmoji.Close();
            translation.Forget(ChirpTranslationKey(post));
            toast.Show(Loc.T(L.Chirper.EditedToast));
            router.Pop();
            return;
        }

        if (chirpEditOutcome == 2)
        {
            chirpEditOutcome = 0;
            chirpEditStatus = ChirperEdits.RefusalText(chirpEditFailure);
        }

        var scale = UiScale.Current;
        DrawChirpEditHeader(area, post);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var toolbarHeight = ComposeToolbarHeight * scale;
            var panelTop = area.Max.Y - toolbarHeight - chirpEditEmoji.PanelHeight(scale);
            var padX = CellPadX * scale;
            var avatarRadius = FeedAvatarRadius * scale;
            var me = store.Me;
            var displayName = me is null ? string.Empty : SocialIdentity.Name(me.DisplayName, me.Handle);
            var inputX = origin.X + padX + avatarRadius * 2f + AvatarGap * scale;
            var inputWidth = MathF.Max(1f, origin.X + width - padX - inputX);
            var nameHeight = displayName.Length > 0 ? Typography.LineHeight(NameStyle) : 0f;
            var inputTop = origin.Y + CellPadTop * scale + nameHeight + 4f * scale;
            var wrapWidth = inputWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
            var photos = ChirpPhotos(post);
            var video = post.Video;
            var tileCount = video is null ? photos.Length : 1;
            var gap = 8f * scale;
            var tile = tileCount > 0
                ? MathF.Min(ComposeTileMax * scale, (inputWidth - gap * (tileCount - 1)) / MathF.Max(2f, tileCount))
                : 0f;
            var stripHeight = tileCount > 0 ? tile + 10f * scale : 0f;
            var quoteHeight = post.QuotedPostId is not null ? QuotedCardHeight(post.ReferencedPost, inputWidth) : 0f;
            var quoteBlock = quoteHeight > 0f ? quoteHeight + 10f * scale : 0f;
            var keepsAttachments = tileCount > 0 || quoteHeight > 0f;
            var noteLine = Typography.LineHeight(ChirpEditNoteStyle);
            var notesHeight = noteLine * (keepsAttachments ? 2f : 1f) + 6f * scale;
            var measuredText = chirpEditDraft.Length == 0
                ? Typography.Measure("Ag", ComposeInputStyle).Y
                : Typography.MeasureWrapped(chirpEditDraft, wrapWidth, ComposeInputStyle.Scale);
            var statusHeight = chirpEditStatus.Length > 0
                ? Typography.MeasureWrapped(chirpEditStatus, inputWidth, TextStyles.Subheadline.Scale) + 6f * scale
                : 0f;
            var availableInput = panelTop - 8f * scale - inputTop - stripHeight - quoteBlock - notesHeight
                - statusHeight - 8f * scale;
            var desiredInput = MathF.Max(measuredText + 34f * scale, 96f * scale);
            var inputHeight = MathF.Max(40f * scale, MathF.Min(desiredInput, availableInput));
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(origin.X + padX + avatarRadius, origin.Y + CellPadTop * scale + avatarRadius),
                    avatarRadius, me.Name, me.World, me.AvatarUrl, 0.95f, 48, Frames.Of(me.FrameId));
            }

            if (displayName.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(inputX, origin.Y + CellPadTop * scale),
                    Typography.FitText(displayName, inputWidth, NameStyle), ChirperInk.TitleInk, NameStyle);
            }

            DrawChirpEditField(area, inputX, inputTop, inputWidth, inputHeight, wrapWidth);
            var cursorY = inputTop + inputHeight + 10f * scale;
            if (video is not null)
            {
                var thumbMin = new Vector2(inputX, cursorY);
                DrawVideoThumb(drawList, post, video, new Rect(thumbMin, thumbMin + new Vector2(tile, tile)),
                    ComposeTileRounding * scale);
                cursorY += stripHeight;
            }
            else if (photos.Length > 0)
            {
                DrawChirpEditPhotos(drawList, photos, post.GifMedia is null, inputX, cursorY, tile, gap, scale);
                cursorY += stripHeight;
            }

            if (quoteHeight > 0f)
            {
                DrawQuotedCard(drawList, new Vector2(inputX, cursorY), inputWidth, quoteHeight, post.ReferencedPost,
                    false, "chirper.edit.quote");
                cursorY += quoteBlock;
            }

            cursorY = DrawChirpEditNotes(drawList, post, inputX, cursorY, inputWidth, keepsAttachments);
            if (chirpEditStatus.Length > 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(inputX, cursorY + 4f * scale));
                using (Typography.WrapAt(inputX + inputWidth))
                using (Plugin.Fonts.Push(TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight))
                using (UiScope.PushColor(ImGuiCol.Text, ChirperInk.Danger))
                {
                    Typography.Wrapped(chirpEditStatus);
                }
            }

            if (chirpEditEmoji.Open)
            {
                var panel = new Rect(new Vector2(area.Min.X, panelTop), new Vector2(area.Max.X, area.Max.Y - toolbarHeight));
                chirpEditEmoji.DrawPanel(panel, ui, ref chirpEditDraft, MaxPostLength);
            }

            var typed = new Rect(new Vector2(inputX, inputTop),
                new Vector2(inputX + inputWidth, inputTop + measuredText + ImGui.GetStyle().FramePadding.Y * 2f));
            var room = new Rect(area.Min, new Vector2(area.Max.X, area.Max.Y - toolbarHeight));
            if (chirpEditEmoji.DrawSuggestions(typed, room, ui, ref chirpEditDraft, MaxPostLength, chirpEditFieldActive,
                    true))
            {
                chirpEditFocus = true;
            }

            DrawChirpEditToolbar(area, toolbarHeight);
        }
    }

    private void DrawChirpEditHeader(Rect area, PostDto post)
    {
        var saving = store.EditingChirp;
        var actionLabel = Loc.T(saving ? L.Chirper.Saving : L.Chirper.Save);
        var hasWords = !chirpEditDraft.AsSpan().Trim().IsEmpty;
        var altChanged = !PostAltTexts.Same(chirpEditAltTexts, post.AltTexts, chirpEditAltTexts.Length);
        var canSave = !saving && chirpEditDraft.Length <= MaxPostLength
            && (hasWords || ChirpPhotos(post).Length > 0 || post.Video is not null)
            && (altChanged || !ChirperEdits.Unchanged(chirpEditDraft, post.Text));
        var action = HeaderActionRect(area, actionLabel);
        DrawScreenHeader(area, Loc.T(L.Chirper.EditChirp), action.Width + HeaderActionGap * UiScale.Current);
        if (!Button.Draw(action, actionLabel, ui.Ink, ButtonStyle.Prominent, enabled: canSave, id: "chirper.edit.save"))
        {
            return;
        }

        chirpEditStatus = string.Empty;
        chirpEditFailure.Clear();
        store.EditChirp(post.Id, chirpEditDraft, altChanged ? chirpEditAltTexts : null,
            ok => chirpEditOutcome = ok ? 1 : 2, chirpEditFailure.Set);
    }

    private void DrawChirpEditField(Rect area, float inputX, float inputTop, float inputWidth, float inputHeight,
        float wrapWidth)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
        ImGui.SetNextItemWidth(inputWidth);
        if (chirpEditFocus)
        {
            ImGui.SetKeyboardFocusHere();
            SoftWrapField.CaretToEnd(ChirpEditFieldId);
            chirpEditFocus = false;
        }

        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
        using (Plugin.Fonts.Push(ComposeInputStyle.Scale))
        {
            SoftWrapField.Paragraphs(ChirpEditFieldId, ref chirpEditDraft, MaxPostLength,
                new Vector2(inputWidth, inputHeight), wrapWidth, chirpEditMentions);
            chirpEditFieldActive = ImGui.IsItemActive();
        }

        if (chirpEditMentions is { } mentions)
        {
            var picked = mentionPopup.Draw(mentions, area, theme, images, lodestone);
            if (picked >= 0)
            {
                mentions.Pick(picked);
            }

            mentionPopup.Gate(mentions);
        }

        if (chirpEditDraft.Length == 0)
        {
            Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(inputX + 4f * scale, inputTop + 2f * scale),
                Typography.FitText(Loc.T(L.Chirper.Compose), inputWidth, ComposeInputStyle), ChirperInk.MutedInk,
                ComposeInputStyle);
        }
    }

    private void DrawChirpEditPhotos(ImDrawListPtr drawList, string[] photos, bool describable, float x, float y,
        float tile, float gap, float scale)
    {
        var rounding = ComposeTileRounding * scale;
        for (var index = 0; index < photos.Length; index++)
        {
            var min = new Vector2(x + (tile + gap) * index, y);
            var max = min + new Vector2(tile, tile);
            var texture = MediaTexture(photos[index]);
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipFill));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                    ImDrawFlags.RoundCornersAll);
            }

            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
            if (GifMedia.IsGif(photos[index]))
            {
                GifBadge.Draw(drawList, new Rect(min, max));
            }

            if (describable && index < chirpEditAltTexts.Length)
            {
                DrawChirpEditAltChip(drawList, photos[index], index, new Rect(min, max));
            }
        }
    }

    private float DrawChirpEditNotes(ImDrawListPtr drawList, PostDto post, float left, float top, float width,
        bool keepsAttachments)
    {
        var editsLeft = ChirperEdits.EditsLeft(post);
        var label = chirpEditsLeftLabel.IsCurrent(editsLeft)
            ? chirpEditsLeftLabel.Value
            : chirpEditsLeftLabel.Store(editsLeft, Loc.Plural(L.Chirper.EditsLeft, editsLeft));
        var lineHeight = Typography.LineHeight(ChirpEditNoteStyle);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, width, ChirpEditNoteStyle),
            ChirperInk.MutedInk, ChirpEditNoteStyle);
        var cursorY = top + lineHeight;
        if (keepsAttachments)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY),
                Typography.FitText(Loc.T(L.Chirper.EditKeepsAttachments), width, ChirpEditNoteStyle), ChirperInk.MutedInk,
                ChirpEditNoteStyle);
            cursorY += lineHeight;
        }

        return cursorY;
    }

    private void DrawChirpEditToolbar(Rect area, float height)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var barMin = new Vector2(area.Min.X, area.Max.Y - height);
        PaintBarBackdrop(drawList, new Rect(barMin, area.Max));
        drawList.AddLine(barMin, new Vector2(area.Max.X, barMin.Y), ImGui.GetColorU32(ChirperInk.Hairline), 1f);
        var centerY = barMin.Y + height * 0.5f;
        var iconRadius = 17f * scale;
        var emojiCenter = new Vector2(area.Min.X + CellPadX * scale + iconRadius, centerY);
        var emojiExtent = new Vector2(iconRadius, iconRadius);
        if (UiInteract.Hover(emojiCenter - emojiExtent, emojiCenter + emojiExtent))
        {
            drawList.AddCircleFilled(emojiCenter, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
        }

        chirpEditEmoji.DrawToggle(ui, emojiCenter, iconRadius, ChirperInk.MineInk, ChirperInk.AccentLink,
            Loc.T(L.Common.Emoji));
        DrawComposeLengthRing(drawList, area, centerY, chirpEditDraft.Length);
    }

    private static void DrawComposeLengthRing(ImDrawListPtr drawList, Rect area, float centerY, int length)
    {
        var scale = UiScale.Current;
        var remaining = MaxPostLength - length;
        var ringRadius = ComposeRingRadius * scale;
        var ringCenter = new Vector2(area.Max.X - CellPadX * scale - 13f * scale, centerY);
        var ringColor = remaining < 0 ? ChirperInk.Danger
            : remaining < ComposeWarnRemaining ? ChirperInk.Warning
            : ChirperInk.Accent;
        ProgressRing.Track(ringCenter, ringRadius, ComposeRingStroke * scale, RingTrack);
        ProgressRing.Fill(ringCenter, ringRadius, ComposeRingStroke * scale, length / (float)MaxPostLength, ringColor);
        if (remaining >= ComposeWarnRemaining)
        {
            return;
        }

        var remainingText = remaining.ToString(Loc.Culture);
        var remainingSize = Typography.Measure(remainingText, RemainingStyle);
        Typography.Draw(drawList,
            new Vector2(ringCenter.X - 13f * scale - 8f * scale - remainingSize.X, centerY - remainingSize.Y * 0.5f),
            remainingText, ringColor, RemainingStyle);
    }
}
