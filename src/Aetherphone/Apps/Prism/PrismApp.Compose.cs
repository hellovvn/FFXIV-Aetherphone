using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Game;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;

namespace Aetherphone.Apps.Prism;

internal readonly record struct CapturedMod(string Key, string Name, string Caption);

internal sealed partial class PrismApp
{
    private const int TitleMaxLength = 60;
    private const int TagsMaxLength = 80;
    private const int MaxTags = 6;
    private const double HeaderSweepMs = 900d;
    private const float HeaderSweepArc = 1.6f;
    private const float HeaderGlyphShare = 0.475f;
    private const float ComposePhotoShare = 0.5f;
    private const float ComposePhotoHeightShare = 0.36f;
    private const int ModNameMaxLength = 80;
    private const int MaxNamedMods = 20;
    private const float LockedTrackAlpha = 0.5f;
    private const string ComposeRaceId = "prism.composeRace";
    private const string ComposeGenderId = "prism.composeGender";

    private readonly PhotoComposeSession composeSession;
    private readonly ImageIntake composeIntake = new();
    private readonly HashSet<string> pickedMods = new(StringComparer.Ordinal);
    private readonly ModDraft composeMods = new();
    private readonly ChipRail modTypeRail = new();
    private readonly string[] modTypeLabels = new string[GearSlots.All.Length + 1];
    private readonly bool[] modTypeActive = new bool[GearSlots.All.Length + 1];
    private readonly int[] modTypeSlots = new int[GearSlots.All.Length + 1];
    private readonly string[] kindLabels = new string[2];
    private CapturedMod[] capturedMods = Array.Empty<CapturedMod>();
    private GlamGearDto[]? capturedModsFor;
    private string? capturedModsLanguage;
    private readonly Action<bool> postSettled;
    private readonly Action<bool> glamEditSettled;
    private GlamGearDto[] captured = Array.Empty<GlamGearDto>();
    private GlamDto? editingGlam;
    private volatile int glamEditOutcome;
    private int savingDraft;
    private bool editPicking;
    private bool editPhotosChanged;
    private bool composeDetails;
    private bool focusTitle;
    private bool? composeVanilla;
    private string glamTitle = string.Empty;
    private string glamTags = string.Empty;
    private volatile int postOutcome;
    private string postingCover = string.Empty;
    private string postedCover = string.Empty;
    private string postedId = string.Empty;
    private int composeDraft;
    private int postingDraft;

    private void StartCompose()
    {
        composeDraft++;
        composeIntake.Discard();
        editingGlam = null;
        editPicking = false;
        editPhotosChanged = false;
        composeSession.Open(false);
        composeSession.MaxPhotos = GlamStore.MaxPhotos;
        composeDetails = false;
        glamGear.Clear();
        composeMods.Clear();
        composeVanilla = null;
        composeRace = 0;
        composeGender = NoGender;
        glamTitle = string.Empty;
        glamTags = string.Empty;
        gearCatalog.EnsureBuilt();
        Push(PrismScreen.Compose);
    }

    private void StartGlamEdit(GlamDto glam)
    {
        composeDraft++;
        editingGlam = glam;
        editPicking = false;
        editPhotosChanged = false;
        glamEditOutcome = OutcomePending;
        composeDetails = true;
        focusTitle = false;
        var gear = glam.Gear ?? Array.Empty<GlamGearDto>();
        LoadGear(glamGear, gear);
        composeMods.Load(gear);
        composeVanilla = glam.Vanilla;
        composeRace = glam.Race;
        composeGender = glam.Race > 0 && glam.Gender is MaleGender or FemaleGender ? glam.Gender : NoGender;
        glamTitle = glam.Title;
        glamTags = glam.Tags is { Length: > 0 } tags ? string.Join(", ", tags) : string.Empty;
        gearCatalog.EnsureBuilt();
        Push(PrismScreen.Compose);
    }

    private void SettlePost(bool succeeded) => postOutcome = succeeded ? OutcomeSent : OutcomeFailed;

    private void SettleGlamEdit(bool succeeded) => glamEditOutcome = succeeded ? OutcomeSent : OutcomeFailed;

    private void DrawCompose(ImDrawListPtr drawList, Rect content, float scale)
    {
        composeSession.ConsumePendingImport();
        if (editingGlam is not null && editPicking)
        {
            DrawEditPhotoPick(drawList, content, scale);
            return;
        }

        if (composeDetails || editingGlam is not null)
        {
            DrawComposeDetails(drawList, content, scale);
            return;
        }

        TakeComposeIntake(content);
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), Loc.T(L.Prism.PostGlam),
            PhoneIcons.ChevronRight, Loc.T(L.Common.Next), composeSession.HasSelection, string.Empty, UiSound.Tap,
            scale, out var advance) + Metrics.Space.Sm * scale;
        if (advance)
        {
            composeDetails = true;
            focusTitle = true;
        }

        DrawComposePickBody(content, top, Loc.T(L.Prism.PostGlam), scale);
    }

    private void DrawEditPhotoPick(ImDrawListPtr drawList, Rect content, float scale)
    {
        TakeComposeIntake(content);
        var title = Loc.T(L.Prism.ChangePhoto);
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), title, PhoneIcons.Check,
            Loc.T(L.Prism.Done), composeSession.HasSelection, string.Empty, UiSound.Tap, scale, out var done) +
            Metrics.Space.Sm * scale;
        if (done)
        {
            editPicking = false;
            editPhotosChanged = true;
            return;
        }

        DrawComposePickBody(content, top, title, scale);
    }

    private void StartEditPhotos()
    {
        if (!editPhotosChanged)
        {
            composeIntake.Discard();
            composeSession.Open(false);
            composeSession.MaxPhotos = GlamStore.MaxPhotos;
        }

        editPicking = true;
    }

    private void DrawComposePickBody(Rect content, float top, string title, float scale)
    {
        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), Loc.T(L.Prism.PhotosHint), PrismArt.Muted,
            PrismType.Body, content.Width) + Metrics.Space.Md * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, GlamStore.RevealsWholePhoto, true,
            title);
        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, content.Max.Y - PrismArt.BottomMargin(scale)));
        using (AppSurface.BeginEdgeToEdge(gridRect, smoothWheel: true))
        {
            if (composeSession.DrawPickGrid(gridRect, scale, PickStyle, true, Loc.T(L.Common.ImportFromPc),
                    title, ComposePasteLabel()))
            {
                composeIntake.Paste(library);
            }
        }
    }

    private void TakeComposeIntake(Rect content)
    {
        while (composeIntake.TryTake(out var taken))
        {
            composeSession.TakeImported(taken);
        }

        if (UiInteract.Hover(content.Min, content.Max) && GameFocus.Active && ImageIntake.PasteRequested())
        {
            composeIntake.Paste(library);
        }

        composeIntake.AcceptDrop(content, library, PrismArt.Fore);
    }

    private string? ComposePasteLabel() => composeIntake.ClipboardHasImage() ? Loc.T(L.Common.PasteImage) : null;

    private void OpenPhotoPick()
    {
        composeIntake.Discard();
        composeSession.Open(true);
    }

    private bool DrawPhotoPick(ImDrawListPtr drawList, Rect content, string title, float aspect, float scale)
    {
        composeSession.ConsumePendingImport();
        TakeComposeIntake(content);
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), title, PhoneIcons.Check,
            Loc.T(L.Prism.UsePhoto), composeSession.HasSelection, string.Empty, UiSound.Tap, scale, out var chosen) +
                  Metrics.Space.Sm * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, aspect, false, true, title);
        if (chosen)
        {
            return true;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, content.Max.Y - PrismArt.BottomMargin(scale)));
        using (AppSurface.BeginEdgeToEdge(gridRect, smoothWheel: true))
        {
            if (composeSession.DrawPickGrid(gridRect, scale, PickStyle, false, Loc.T(L.Common.ImportFromPc), title,
                    ComposePasteLabel()))
            {
                composeIntake.Paste(library);
            }
        }

        return false;
    }

    private float DrawActionHeader(ImDrawListPtr drawList, Rect content, string leaveGlyph, string leave,
        string heading, string actionGlyph, string action, bool enabled, string busy, UiSound tapSound, float scale,
        out bool acted)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var row = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + tap));
        if (IconButton(drawList, new Vector2(row.Min.X + tap * 0.5f, row.Center.Y), leaveGlyph, leave, scale,
                HoverLabelSide.Below))
        {
            LeaveScreen();
        }

        acted = DrawHeaderAction(drawList, new Vector2(row.Max.X - tap * 0.5f, row.Center.Y), actionGlyph, action,
            enabled, busy, tapSound, scale);
        var room = MathF.Max(1f, row.Width - (tap + Metrics.Space.Sm * scale) * 2f);
        var style = FitStyle(heading, room, PrismType.Headline);
        var title = Typography.FitText(heading, room, style);
        var size = Typography.Measure(title, style);
        Typography.Draw(drawList, new Vector2(row.Center.X - size.X * 0.5f, row.Center.Y - size.Y * 0.5f), title,
            PrismArt.Fore, style);
        return row.Max.Y;
    }

    private bool DrawHeaderAction(ImDrawListPtr drawList, Vector2 center, string glyph, string label, bool enabled,
        string busy, UiSound tapSound, float scale)
    {
        var radius = Metrics.Size.TapTarget * 0.5f * scale;
        var reach = new Vector2(radius, radius);
        var rect = new Rect(center - reach, center + reach);
        if (enabled)
        {
            var key = ImGui.GetID(label);
            var hovered = UiInteract.Hover(rect.Min, rect.Max);
            var face = RoundButton.Surface(drawList, rect, controls, ButtonStyle.Prominent, true, hovered, key);
            PhoneIcon.Draw(drawList, center, glyph, PrismArt.OnFore, face.Face.Width * HeaderGlyphShare);
            HoverTooltip.Show(key, rect, label, hovered, HoverLabelSide.Below);
            return UiInteract.Click(rect.Min, rect.Max, hovered, tapSound);
        }

        RoundButton.Icon(drawList, center, radius, glyph, controls, ButtonStyle.Bare, enabled: false);
        if (busy.Length > 0)
        {
            ProgressRing.Sweep(center, radius - Metrics.Stroke.Ring * scale, Metrics.Stroke.Ring * scale,
                PrismArt.Fore, HeaderSweepMs, HeaderSweepArc, 1f, drawList);
        }

        HoverTooltip.Show(rect, busy.Length > 0 ? busy : label, HoverLabelSide.Below);
        return false;
    }

    private void DrawComposeDetails(ImDrawListPtr drawList, Rect content, float scale)
    {
        bodyRaceMenu.Gate();
        var editing = editingGlam is not null;
        var busy = editing ? glams.Saving : glams.Posting;
        var bodyChosen = BodyChosen(composeRace, composeGender);
        var gearReady = composeVanilla != true || DraftPieces() > 0;
        var photosReady = !editing || !editPhotosChanged || composeSession.SelectedCount > 0;
        var ready = !string.IsNullOrWhiteSpace(glamTitle) && !busy && composeVanilla is not null &&
                    bodyChosen && gearReady && photosReady;
        var busyLabel = !busy ? string.Empty : Loc.T(editing ? L.Prism.SavingGlam : L.Prism.Posting);
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(editing ? L.Prism.EditGlamTitle : L.Prism.PostGlam), PhoneIcons.Check,
            Loc.T(editing ? L.Prism.Save : L.Prism.Post), ready, busyLabel, editing ? UiSound.Tap : UiSound.Shutter,
            scale, out var post) + Metrics.Space.Sm * scale;
        if (post && editing)
        {
            SaveGlamEdit();
        }
        else if (post)
        {
            PostGlam();
        }

        var left = content.Min.X;
        var width = content.Width;
        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        var failureHeight = 0f;
        var notice = string.Empty;
        var failed = editing
            ? glams.SaveFailed && !glams.Saving && savingDraft == composeDraft
            : glams.PostFailed && !glams.Posting && postingDraft == composeDraft;
        if (failed)
        {
            var reason = editing ? glams.SaveFailureText : glams.PostFailureText;
            notice = reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint);
        }
        else if (composeVanilla is null)
        {
            notice = Loc.T(L.Prism.TrackNeeded);
        }
        else if (!bodyChosen)
        {
            notice = Loc.T(L.Prism.BodyNeeded);
        }
        else if (!gearReady)
        {
            notice = Loc.T(L.Prism.GearNeeded);
        }

        if (notice.Length > 0)
        {
            failureHeight = Typography.MeasureWrappedBlock(notice, PrismType.Body, width).Y;
            Typography.DrawWrappedLeft(new Vector2(left, bottom - failureHeight),
                notice, PrismArt.Muted, PrismType.Body, width);
            failureHeight += Metrics.Space.Lg * scale;
        }

        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, bottom - failureHeight));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var bodyWidth = ScrollLayout.StableContentWidth();
            var photoHeight = content.Height * ComposePhotoHeightShare;
            var photosBottom = editingGlam is { } glam
                ? DrawEditPhotoBlock(list, origin.X, origin.Y, bodyWidth, photoHeight, glam, scale)
                : DrawComposePhotos(list, origin.X, origin.Y, bodyWidth, photoHeight, scale);
            var cursor = photosBottom + Metrics.Space.Lg * scale;
            if (focusTitle)
            {
                ImGui.SetKeyboardFocusHere();
                focusTitle = false;
            }

            cursor = DrawField(list, origin.X, cursor, bodyWidth, Loc.T(L.Prism.TitleLabel), Loc.T(L.Prism.TitleHint),
                "##glamTitle", ref glamTitle, TitleMaxLength, scale);
            cursor = DrawField(list, origin.X, cursor, bodyWidth, Loc.T(L.Prism.TagsLabel), Loc.T(L.Prism.TagsHint),
                "##glamTags", ref glamTags, TagsMaxLength, scale);
            cursor = DrawTrackChoice(list, origin.X, cursor, bodyWidth, editing, scale);
            cursor = DrawBodyField(list, origin.X, cursor, bodyWidth, ComposeRaceId, ComposeGenderId,
                ref composeRace, ref composeGender, scale);
            cursor = DrawChosenGear(list, origin.X, cursor, bodyWidth, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(bodyWidth, cursor - origin.Y));
        }

        DrawBodyRaceMenu(content, ref composeRace);
    }

    private float DrawComposePhotos(ImDrawListPtr drawList, float left, float top, float width, float maxHeight,
        float scale)
    {
        var count = composeSession.SelectedCount;
        if (count == 0)
        {
            return top;
        }

        var gap = Metrics.Space.Xs * scale;
        var cardWidth = MathF.Min(MathF.Min(width * ComposePhotoShare, maxHeight * GlamStore.PhotoRatio),
            (width - gap * (count - 1)) / count);
        var cardHeight = cardWidth / GlamStore.PhotoRatio;
        var start = left + (width - cardWidth * count - gap * (count - 1)) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(start + index * (cardWidth + gap), top);
            var card = new Rect(min, min + new Vector2(cardWidth, cardHeight));
            if (composeSession.TryGetPreviewUv(index, GlamStore.PhotoRatio, GlamStore.RevealsWholePhoto,
                    out var texture, out var uv0, out var uv1))
            {
                drawList.AddImage(texture.Handle, card.Min, card.Max, uv0, uv1);
            }
            else
            {
                PrismArt.Placeholder(drawList, card, false, scale);
            }

            var hovered = HoverWash(drawList, card);
            if (!UiInteract.Click(card.Min, card.Max, hovered))
            {
                continue;
            }

            composeSession.SelectCurrent(index);
            if (editingGlam is not null)
            {
                editPicking = true;
            }
            else
            {
                composeDetails = false;
            }
        }

        return top + cardHeight;
    }

    private float DrawEditPhotoBlock(ImDrawListPtr drawList, float left, float top, float width, float maxHeight,
        GlamDto glam, float scale)
    {
        if (editPhotosChanged && composeSession.SelectedCount == 0)
        {
            editPhotosChanged = false;
        }

        var bottom = (editPhotosChanged
            ? DrawComposePhotos(drawList, left, top, width, maxHeight, scale)
            : DrawEditPhotos(drawList, left, top, width, maxHeight, glam, scale)) + Metrics.Space.Sm * scale;
        if (glam.WeekId.Length > 0)
        {
            return bottom + Typography.DrawWrappedLeft(new Vector2(left, bottom),
                Loc.T(L.Failure.PrismGlamPhotosLocked), PrismArt.Muted, PrismType.Body, width);
        }

        var label = Loc.T(L.Prism.ChangePhoto);
        var buttonWidth = ButtonWidth([label], width, scale);
        var rect = new Rect(new Vector2(left + (width - buttonWidth) * 0.5f, bottom),
            new Vector2(left + (width + buttonWidth) * 0.5f, bottom + Button.LargeHeight * scale));
        if (DrawButton(drawList, rect, label, ButtonStyle.Plain, scale, enabled: !glams.Saving, id: "prism.editPhotos"))
        {
            StartEditPhotos();
        }

        return rect.Max.Y;
    }

    private float DrawEditPhotos(ImDrawListPtr drawList, float left, float top, float width, float maxHeight,
        GlamDto glam, float scale)
    {
        var photos = glam.Photos ?? Array.Empty<GlamPhotoDto>();
        var count = Math.Min(photos.Length, GlamStore.MaxPhotos);
        if (count == 0)
        {
            return top;
        }

        var gap = Metrics.Space.Xs * scale;
        var cardWidth = MathF.Min(MathF.Min(width * ComposePhotoShare, maxHeight * GlamStore.PhotoRatio),
            (width - gap * (count - 1)) / count);
        var cardHeight = cardWidth / GlamStore.PhotoRatio;
        var start = left + (width - cardWidth * count - gap * (count - 1)) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(start + index * (cardWidth + gap), top);
            DrawLook(drawList, new Rect(min, min + new Vector2(cardWidth, cardHeight)), photos[index].Url, false);
        }

        return top + cardHeight;
    }

    private float DrawField(ImDrawListPtr drawList, float left, float top, float width, string label, string hint,
        string id, ref string text, int maxLength, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, width, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        return DrawInput(drawList, left, top, width, hint, id, ref text, maxLength, scale) + Metrics.Space.Md * scale;
    }

    private float DrawInput(ImDrawListPtr drawList, float left, float top, float width, string hint, string id,
        ref string text, int maxLength, float scale)
    {
        var field = new Rect(new Vector2(left, top), new Vector2(left + width, top + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, controls);
        GlassField.Text(field, id, hint, ref text, PrismArt.Fore, PrismArt.Muted, Metrics.Space.Md * scale, maxLength);
        return field.Max.Y;
    }

    private float DrawTrackChoice(ImDrawListPtr drawList, float left, float top, float width, bool locked,
        float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        kindLabels[0] = Loc.T(L.Prism.ModdedStamp);
        kindLabels[1] = Loc.T(L.Prism.VanillaStamp);
        var shown = composeVanilla switch
        {
            true => 1,
            false => 0,
            null => -1,
        };
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + tap));
        if (locked)
        {
            var mark = PrismMotion.Mark(drawList);
            using (InputShield.Engage())
            {
                DrawTrackSegments("prism.composeKind", row, kindLabels, shown, scale);
            }

            PrismMotion.Shift(drawList, mark, Vector2.Zero, LockedTrackAlpha);
            return row.Max.Y + Metrics.Space.Xs * scale + Typography.DrawWrappedLeft(
                new Vector2(left, row.Max.Y + Metrics.Space.Xs * scale), Loc.T(L.Prism.TrackLocked), PrismArt.Muted,
                PrismType.Caption, width) + Metrics.Space.Lg * scale;
        }

        var picked = DrawTrackSegments("prism.composeKind", row, kindLabels, shown, scale);
        if (picked >= 0)
        {
            composeVanilla = picked == 1;
        }

        return top + tap + Metrics.Space.Lg * scale;
    }

    private float DrawChosenGear(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top = DrawSectionTitle(drawList, left, left + width, top + Metrics.Space.Sm * scale,
            Loc.T(L.Prism.PiecesHeading), string.Empty, scale) + Metrics.Space.Xs * scale;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.GlamGearNote), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Md * scale;
        var fill = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.RegularHeight * scale));
        if (DrawButton(drawList, fill, Loc.T(L.Prism.FillFromWearing), ButtonStyle.Tinted, scale,
                id: "prism.compose.fill"))
        {
            FillFromWearing(glamGear);
        }

        var fromWardrobe = new Rect(new Vector2(left, fill.Max.Y + Metrics.Space.Sm * scale),
            new Vector2(left + width, fill.Max.Y + Metrics.Space.Sm * scale + Button.RegularHeight * scale));
        if (DrawButton(drawList, fromWardrobe, Loc.T(L.Prism.FillFromWardrobe), ButtonStyle.Tinted, scale,
                id: "prism.compose.wardrobe"))
        {
            OpenWardrobePick();
        }

        top = DrawGearSlots(drawList, left, fromWardrobe.Max.Y + Metrics.Space.Lg * scale, width, scale);
        if (composeVanilla != false)
        {
            return top + PrismArt.BottomMargin(scale);
        }

        top = DrawSectionTitle(drawList, left, left + width, top + Metrics.Space.Lg * scale,
            Loc.T(L.Prism.ModsHeading), string.Empty, scale) + Metrics.Space.Md * scale;
        top = DrawPickModsButton(drawList, left, top, width, composeMods, "prism.compose.pickMods", scale);
        return DrawNamedMods(drawList, left, top, width, composeMods, glamGear, "##glamMod", scale) +
               PrismArt.BottomMargin(scale);
    }

    private int SyncModTypes(GearDraft gear, ModDraft mods)
    {
        modTypeLabels[0] = Loc.T(L.Prism.CharacterHeading);
        modTypeSlots[0] = GearSlots.None;
        var count = 1;
        var found = mods.Slot == GearSlots.None;
        for (var index = 0; index < gear.Items.Length; index++)
        {
            if (gear.Items[index] == 0)
            {
                continue;
            }

            modTypeLabels[count] = Loc.T(SlotNames[index]);
            modTypeSlots[count] = (int)GearSlots.All[index];
            found |= modTypeSlots[count] == mods.Slot;
            count++;
        }

        if (!found)
        {
            mods.Slot = GearSlots.None;
        }

        for (var index = 0; index < count; index++)
        {
            modTypeActive[index] = modTypeSlots[index] == mods.Slot;
        }

        return count;
    }

    private string ModTypeName(int slot)
    {
        var index = Array.IndexOf(GearSlots.All, (GearSlot)slot);
        return Loc.T(index >= 0 ? SlotNames[index] : L.Prism.CharacterHeading);
    }

    private float DrawNamedMods(ImDrawListPtr drawList, float left, float top, float width, ModDraft mods,
        GearDraft gear, string inputId, float scale)
    {
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.ModTypeHint), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Sm * scale;
        var types = SyncModTypes(gear, mods);
        var chips = new Rect(new Vector2(left, top), new Vector2(left + width, top + ChipRail.RowHeight * scale));
        var tapped = modTypeRail.Draw(chips, ui, modTypeLabels.AsSpan(0, types), modTypeActive.AsSpan(0, types));
        if (tapped >= 0)
        {
            mods.Slot = modTypeSlots[tapped];
        }

        top = chips.Max.Y + Metrics.Space.Sm * scale;
        var add = Loc.T(L.Prism.AddMod);
        var addWidth = ButtonWidth([add], width * 0.5f, scale);
        var gap = Metrics.Space.Sm * scale;
        var name = mods.Name;
        var bottom = DrawInput(drawList, left, top, width - addWidth - gap, Loc.T(L.Prism.ModHint), inputId,
            ref name, ModNameMaxLength, scale);
        mods.Name = name;
        var ready = !string.IsNullOrWhiteSpace(name) && mods.Count < MaxNamedMods;
        if (DrawButton(drawList, new Rect(new Vector2(left + width - addWidth, top), new Vector2(left + width, bottom)),
                add, ButtonStyle.Tinted, scale, enabled: ready, id: "prism.addMod"))
        {
            mods.Add(new GlamModDto(name.Trim()), mods.Slot);
            mods.Name = string.Empty;
        }

        return DrawModList(drawList, left, bottom + Metrics.Space.Sm * scale, width, mods, scale);
    }

    private float DrawModList(ImDrawListPtr drawList, float left, float top, float width, ModDraft mods, float scale)
    {
        var removeWidth = Metrics.Size.TapTarget * scale;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var rowHeight = MathF.Max(Metrics.Size.TapTarget * scale, nameHeight + captionHeight);
        var room = MathF.Max(1f, width - removeWidth - Metrics.Space.Md * scale);
        for (var index = 0; index < mods.Count; index++)
        {
            var textTop = top + (rowHeight - nameHeight - captionHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(left, textTop),
                Typography.FitText(mods.Mods[index].Name, room, PrismType.Headline), PrismArt.Fore,
                PrismType.Headline);
            Typography.Draw(drawList, new Vector2(left, textTop + nameHeight),
                Typography.FitText(ModTypeName(mods.Slots[index]), room, PrismType.Caption), PrismArt.Muted,
                PrismType.Caption);
            ImGui.PushID(index);
            var removed = RoundButton.Icon(drawList,
                new Vector2(left + width - removeWidth * 0.5f, top + rowHeight * 0.5f), removeWidth * 0.5f,
                PhoneIcons.X, controls, ButtonStyle.Bare, Loc.T(L.Prism.RemovePiece), glyphInk: PrismArt.Muted);
            ImGui.PopID();
            top += rowHeight;
            if (removed)
            {
                mods.RemoveAt(index--);
            }
        }

        return top;
    }

    private GlamGearDto[] ChosenGear()
    {
        var pieces = DraftOutfit();
        return composeVanilla == true ? pieces : composeMods.AttachTo(pieces);
    }

    private float DrawModRows(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        for (var index = 0; index < capturedMods.Length; index++)
        {
            var mod = capturedMods[index];
            var picked = pickedMods.Contains(mod.Key);
            top = DrawModTick(drawList, left, top, width, mod.Name, mod.Caption, picked, scale, out var toggled);
            if (!toggled)
            {
                continue;
            }

            if (picked)
            {
                pickedMods.Remove(mod.Key);
            }
            else
            {
                pickedMods.Add(mod.Key);
            }
        }

        if (capturedMods.Length > 0)
        {
            top += Metrics.Space.Lg * scale;
        }

        return top;
    }

    private float DrawModTick(ImDrawListPtr drawList, float left, float top, float width, string name, string caption,
        bool picked, float scale, out bool toggled)
    {
        toggled = false;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var height = MathF.Max(Metrics.Size.TapTarget * scale, nameHeight + captionHeight) + Metrics.Space.Sm * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var check = WornCheckUnits * scale;
        var hovered = HoverWash(drawList, row);
        PhoneIcon.Draw(drawList, new Vector2(left + check * 0.5f, row.Center.Y),
            picked ? PhoneIcons.CircleCheck : PhoneIcons.Circle, picked ? PrismArt.Fore : PrismArt.Muted, check);
        var textLeft = left + check + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, row.Max.X - textLeft);
        var textTop = row.Center.Y - (nameHeight + captionHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(name, room, PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(caption, room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        toggled = UiInteract.Click(row.Min, row.Max, hovered);
        return row.Max.Y;
    }

    private string ModCaption(int slot, string author)
    {
        var place = ModTypeName(slot);
        return author.Length > 0 ? string.Concat(place, GlamMetaSeparator, author) : place;
    }

    private void SyncCapturedMods()
    {
        if (ReferenceEquals(captured, capturedModsFor) && ReferenceEquals(Loc.Current.Code, capturedModsLanguage))
        {
            return;
        }

        capturedModsFor = captured;
        capturedModsLanguage = Loc.Current.Code;
        var worn = PostedMods.GearMods(captured);
        var mods = new CapturedMod[worn.Length];
        for (var index = 0; index < worn.Length; index++)
        {
            var mod = worn[index].Mod;
            mods[index] = new CapturedMod(PostedMods.Key(mod), mod.Name, ModCaption(worn[index].Slot, mod.Author));
        }

        capturedMods = mods;
    }

    private GlamPhotoDraft[] ComposePhotoDrafts()
    {
        var paths = composeSession.SelectedArray();
        var crops = composeSession.CropsArray();
        var count = Math.Min(paths.Length, GlamStore.MaxPhotos);
        var photos = new GlamPhotoDraft[count];
        for (var index = 0; index < count; index++)
        {
            photos[index] = new GlamPhotoDraft(paths[index], index < crops.Length ? crops[index] : default);
        }

        return photos;
    }

    private void PostGlam()
    {
        var vanilla = composeVanilla == true;
        var gear = ChosenGear();
        var photos = ComposePhotoDrafts();
        postOutcome = OutcomePending;
        postingDraft = composeDraft;
        postingCover = photos.Length > 0 ? photos[0].Path : string.Empty;
        glams.Post(photos, gear, glamTitle.Trim(), SplitTags(glamTags), composeRace, composeGender, vanilla,
            glamour.TryingOn ? wearingGlamId : string.Empty, postSettled);
    }

    private void SaveGlamEdit()
    {
        if (editingGlam is not { } glam)
        {
            return;
        }

        glamEditOutcome = OutcomePending;
        savingDraft = composeDraft;
        glams.Update(glam, ChosenGear(), glamTitle.Trim(), SplitTags(glamTags), composeRace, composeGender,
            editPhotosChanged ? ComposePhotoDrafts() : null, glamEditSettled);
    }

    private void ConsumeGlamEditOutcome()
    {
        var outcome = glamEditOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        glamEditOutcome = OutcomePending;
        if (outcome != OutcomeSent || glams.Updated is not { } saved)
        {
            return;
        }

        RefreshGlamCopies(saved);
        UiFeedback.Play(UiSound.Success);
        if (screen != PrismScreen.Compose || savingDraft != composeDraft || editingGlam?.Id != saved.Id)
        {
            return;
        }

        editingGlam = null;
        GoBack();
        if (screen == PrismScreen.GlamDetail)
        {
            ShowGlam(saved);
        }
    }

    private static string[] SplitTags(string text)
    {
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = new List<string>(Math.Min(parts.Length, MaxTags));
        for (var index = 0; index < parts.Length && kept.Count < MaxTags; index++)
        {
            var tag = parts[index].TrimStart('#').Trim().ToLowerInvariant();
            if (tag.Length > 0 && !kept.Contains(tag))
            {
                kept.Add(tag);
            }
        }

        return kept.ToArray();
    }

    private (int Race, int Gender) WornBody() =>
        glamour.TryBody(out var race, out var gender) ? (race, gender) : Appearance();

    private static (int Race, int Gender) Appearance()
    {
        var player = GameMemory.Attached ? Plugin.ObjectTable.LocalPlayer : null;
        return player is null
            ? (0, 0)
            : (player.Customize[(int)CustomizeIndex.Race], player.Customize[(int)CustomizeIndex.Gender]);
    }

    private void ConsumePostOutcome()
    {
        var outcome = postOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        postOutcome = OutcomePending;
        if (outcome != OutcomeSent || glams.Posted is not { } created)
        {
            return;
        }

        postedId = created.Id;
        postedCover = postingCover;
        UiFeedback.Play(UiSound.Success);
        if (screen != PrismScreen.Compose || postingDraft != composeDraft)
        {
            return;
        }

        Go(PrismScreen.GlamDetail);
        ShowGlam(created);
    }
}
