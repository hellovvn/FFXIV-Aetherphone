using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal enum WardrobeAction : byte
{
    Import,
    Surprise,
    Select,
    ZoomIn,
    ZoomOut,
}

internal sealed partial class PrismApp
{
    private const string FitLogoArtwork = "prism.fg";
    private const float FitLogoShare = 0.6f;
    private const float FitLogoAlpha = 0.9f;
    private const float GridLogoShare = 0.42f;
    private const float GridLogoHeightShare = 0.42f;
    private const int SheetCapacity = 5;
    private const int WardrobeColumnsMin = 2;
    private const int WardrobeColumnsMax = 4;
    private const float NewOutfitGlyphUnits = 14f;

    private readonly WardrobeStore wardrobe;
    private readonly Random roulette = new();
    private readonly ActionSheet wardrobeSheet = new();
    private readonly ActionSheet.Item[] sheetItems = new ActionSheet.Item[SheetCapacity];
    private readonly WardrobeAction[] sheetActions = new WardrobeAction[SheetCapacity];
    private readonly ChipRail wardrobeRail = new();
    private readonly string[] wardrobeChips = new string[2];
    private readonly bool[] wardrobeChipActive = new bool[2];
    private WardrobeOutfit? fitFor;
    private string? fitLanguage;
    private string fitMeta = string.Empty;
    private string outfitId = string.Empty;
    private bool favouritesOnly;
    private CachedText outfitNameText;
    private CachedText allChipText;
    private CachedText favouriteChipText;
    private string coverFor = string.Empty;
    private int sheetCount;
    private PrismScreen sheetScreen;
    private Spring outfitPinReveal;

    private void DrawWardrobe(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        wardrobe.Ensure();
        GateWardrobeSheet();
        var imported = wardrobe.PumpImport(glamour);
        if (imported >= 0)
        {
            ShellToast.Show(Loc.T(L.Prism.DesignsAdded, imported));
        }

        SyncShownOutfits();
        var top = selectingOutfits
            ? DrawSelectHeader(drawList, content, scale)
            : DrawWardrobeHeader(drawList, content, scale);
        if (wardrobe.Outfits.Length > 0)
        {
            top = DrawSearchField(drawList, content, top + Metrics.Space.Sm * scale, WardrobeSearchId,
                Loc.T(L.Prism.WardrobeSearchHint), ref wardrobeQuery, WardrobeQueryMaxLength, ref wardrobeSearchFocus,
                ref wardrobeSearchEdge, scale);
        }

        top = DrawWardrobeChips(content, top, scale);
        var bottom = selectingOutfits ? DrawSelectActions(drawList, content, scale) : content.Max.Y;
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, bottom));
        SyncShownOutfits();
        if (wardrobe.Loading || wardrobe.Importing || shownOutfitCount > 0)
        {
            DrawOutfitGrid(body, scale);
        }
        else if (CountOutfits(favouritesOnly) > 0)
        {
            DrawEmpty(drawList, new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, bottom)),
                PhoneIcons.Search, string.Empty, Loc.T(L.Prism.WardrobeNoMatch), string.Empty, ButtonStyle.Solid,
                scale, out _);
        }
        else
        {
            DrawEmpty(drawList, new Rect(new Vector2(content.Min.X, top),
                    new Vector2(content.Max.X, content.Max.Y - bottomChrome)),
                favouritesOnly ? PhoneIcons.Star : IconGlyph.Of(FontAwesomeIcon.Tshirt),
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyTitle : L.Prism.WardrobeEmptyTitle),
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyHint : L.Prism.WardrobeEmptyHint),
                favouritesOnly ? string.Empty : Loc.T(L.Prism.NewOutfit), ButtonStyle.Solid, scale, out var create);
            if (create)
            {
                StartOutfitEdit(null);
            }
        }

        DrawWardrobeSheet(full);
    }

    private float DrawWardrobeHeader(ImDrawListPtr drawList, Rect content, float scale)
    {
        var height = Metrics.Size.TapTarget * scale;
        var top = content.Min.Y + Metrics.Space.Sm * scale;
        var middle = top + height * 0.5f;
        var left = content.Min.X;
        var right = content.Max.X;
        var canImport = glamour.HasGlamourer && !wardrobe.Importing;
        var canShuffle = wardrobe.Outfits.Length > 1;
        var canSelect = wardrobe.Outfits.Length > 0 && !wardrobe.Loading && !wardrobe.Importing;
        if (canImport || canShuffle || canSelect)
        {
            if (IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Dots, Loc.T(L.Prism.More), scale))
            {
                var columns = WardrobeColumns;
                sheetCount = 0;
                AddSheetAction(canSelect, WardrobeAction.Select);
                AddSheetAction(canImport, WardrobeAction.Import);
                AddSheetAction(canShuffle, WardrobeAction.Surprise);
                AddSheetAction(canSelect && columns > WardrobeColumnsMin, WardrobeAction.ZoomIn);
                AddSheetAction(canSelect && columns < WardrobeColumnsMax, WardrobeAction.ZoomOut);
                OpenWardrobeSheet();
            }

            right -= height + Metrics.Space.Xs * scale;
        }

        var create = Loc.T(L.Prism.NewOutfit);
        var heading = Loc.T(L.Prism.TabWardrobe);
        var pillHeight = Button.RegularHeight * scale;
        var labelStyle = Button.LabelStyle(pillHeight);
        var glyph = NewOutfitGlyphUnits * scale;
        var gap = Metrics.Space.Xs * scale;
        var textWidth = Typography.Measure(create, labelStyle).X;
        var createWidth = glyph + gap + textWidth + Metrics.Space.Md * 2f * scale;
        var labelled = left + Typography.Measure(heading, PrismType.Title).X + Metrics.Space.Md * scale <=
                       right - createWidth;
        var createLeft = labelled ? right - createWidth : right - height;
        bool created;
        if (labelled)
        {
            var hit = new Rect(new Vector2(createLeft, top), new Vector2(right, top + height));
            var hovered = UiInteract.Hover(hit.Min, hit.Max);
            var pill = new Rect(new Vector2(createLeft, middle - pillHeight * 0.5f),
                new Vector2(right, middle + pillHeight * 0.5f));
            var face = Button.Surface(drawList, pill, controls, ButtonStyle.Solid, ButtonRole.Normal, true, hovered,
                ImGui.GetID("prism.newOutfit"));
            var x = face.Face.Center.X - (glyph + gap + textWidth) * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(x + glyph * 0.5f, face.Face.Center.Y), PhoneIcons.Plus, face.LabelInk,
                glyph);
            Typography.Draw(drawList,
                new Vector2(x + glyph + gap, face.Face.Center.Y - Typography.LineHeight(labelStyle) * 0.5f), create,
                face.LabelInk, labelStyle);
            created = UiInteract.Click(hit.Min, hit.Max, hovered);
        }
        else
        {
            created = IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Plus, create, scale);
        }

        if (created)
        {
            StartOutfitEdit(null);
        }

        var title = Typography.FitText(heading, MathF.Max(1f, createLeft - Metrics.Space.Md * scale - left),
            PrismType.Title);
        Typography.Draw(drawList, new Vector2(left, middle - Typography.LineHeight(PrismType.Title) * 0.5f), title,
            PrismArt.Fore, PrismType.Title);
        return top + height;
    }

    private float DrawWardrobeChips(Rect content, float top, float scale)
    {
        var all = wardrobe.Outfits.Length;
        var favourites = CountOutfits(true);
        var allKey = HashCode.Combine(all, Loc.Current.Code);
        wardrobeChips[0] = allChipText.IsCurrent(allKey)
            ? allChipText.Value
            : allChipText.Store(allKey, string.Concat(Loc.T(L.Prism.FilterAll), " ", CountLabel(all)));
        var favouriteKey = HashCode.Combine(favourites, Loc.Current.Code);
        wardrobeChips[1] = favouriteChipText.IsCurrent(favouriteKey)
            ? favouriteChipText.Value
            : favouriteChipText.Store(favouriteKey,
                string.Concat(Loc.T(L.Prism.Favourites), " ", CountLabel(favourites)));
        wardrobeChipActive[0] = !favouritesOnly;
        wardrobeChipActive[1] = favouritesOnly;
        var row = new Rect(new Vector2(content.Min.X, top + Metrics.Space.Xs * scale),
            new Vector2(content.Max.X, top + Metrics.Space.Xs * scale + Metrics.Size.TapTarget * scale));
        var picked = wardrobeRail.Draw(row, ui, wardrobeChips, wardrobeChipActive);
        if (picked >= 0)
        {
            favouritesOnly = picked == 1;
        }

        return row.Max.Y + Metrics.Space.Xs * scale;
    }

    private int CountOutfits(bool favourites)
    {
        var outfits = wardrobe.Outfits;
        if (!favourites)
        {
            return outfits.Length;
        }

        var count = 0;
        for (var index = 0; index < outfits.Length; index++)
        {
            if (outfits[index].Favourite)
            {
                count++;
            }
        }

        return count;
    }

    private void DrawOutfitGrid(Rect body, float scale)
    {
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Xxs * scale;
        var gridWidth = width - gap * 2f;
        SyncShownOutfits();
        var outfits = shownOutfitsFor ?? Array.Empty<WardrobeOutfit>();
        var busy = wardrobe.Loading || wardrobe.Importing;
        var columns = WardrobeColumns;
        var count = busy ? PlaceholderRows * columns : shownOutfitCount;
        var pitch = MathF.Max(1f, GridPitch(gridWidth, scale, columns));
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - origin.Y) / pitch) * columns, 0, count);
        var last = Math.Clamp(
            ((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - origin.Y) / pitch) + 1) * columns, first,
            count);
        for (var index = first; index < last; index++)
        {
            var photo = GridCell(origin.X + gap, origin.Y, gridWidth, index, scale, columns);
            if (busy)
            {
                PrismArt.Placeholder(list, photo, false, scale);
                continue;
            }

            var outfit = outfits[shownOutfits[index]];
            var mark = PrismMotion.Mark(list);
            if (!DrawOutfitCover(list, photo, outfit.Cover))
            {
                PrismArt.Placeholder(list, photo, false, scale);
                AppIconTextures.TryDrawArtwork(list, FitLogoArtwork,
                    new Vector2(photo.Center.X, photo.Min.Y + photo.Height * GridLogoHeightShare),
                    photo.Width * GridLogoShare, Vector4.One with { W = FitLogoAlpha });
            }

            if (DrawGridTile(list, mark, photo, string.Empty, outfit.Name, string.Empty, PieceLabel(outfit.Gear),
                    scale))
            {
                if (selectingOutfits)
                {
                    ToggleOutfit(outfit.Id);
                }
                else
                {
                    OpenOutfit(outfit.Id);
                }
            }

            if (selectingOutfits)
            {
                DrawSelectMark(list, photo, selectedOutfits.Contains(outfit.Id), scale);
            }
        }

        ImGui.Dummy(new Vector2(width, GridHeight(gridWidth, count, scale, columns) + PrismArt.BottomMargin(scale)));
    }

    private int WardrobeColumns => configuration.PrismWardrobeColumns == 0
        ? GridColumns
        : Math.Clamp(configuration.PrismWardrobeColumns, WardrobeColumnsMin, WardrobeColumnsMax);

    private void SetWardrobeColumns(int columns)
    {
        configuration.PrismWardrobeColumns = Math.Clamp(columns, WardrobeColumnsMin, WardrobeColumnsMax);
        configuration.Save();
    }

    private void OpenOutfit(string id)
    {
        Push(PrismScreen.WardrobeOutfit);
        outfitId = id;
        outfitPinReveal.SnapTo(0f);
        pillLabel = string.Empty;
        ResetPieces();
    }

    private string PieceLabel(GlamGearDto[] gear)
    {
        var pieces = CountPieces(gear);
        return pieces > 0 ? CountLabel(pieces) : string.Empty;
    }

    private void DrawWardrobeOutfit(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (wardrobe.Find(outfitId) is not { } outfit)
        {
            GoBack();
            return;
        }

        GateWardrobeSheet();
        SyncFit(outfit);
        var covered = outfit.Cover is { } cover && !wallpaperImages.Failed(cover.Path);
        DrawFit(new Rect(new Vector2(full.Min.X, content.Min.Y), new Vector2(full.Max.X, content.Max.Y)), content.Min.Y,
            outfit, covered, scale);
        DrawWardrobeSheet(full);
    }

    private void DrawFit(Rect body, float pinTop, WardrobeOutfit outfit, bool covered, float scale)
    {
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var heroTop = new Vector2(origin.X, BodyTop());
        var hero = new Rect(heroTop, heroTop + new Vector2(width, width / GlamStore.PhotoRatio));
        var inset = Metrics.Space.Lg * scale;
        if (covered)
        {
            if (!DrawOutfitCover(list, hero, outfit.Cover))
            {
                PrismArt.Placeholder(list, hero, false, scale);
            }

            if (UiInteract.Click(hero.Min, hero.Max, piecesOpen && UiInteract.Hover(hero.Min, hero.Max)))
            {
                piecesOpen = false;
            }

            DrawPiecesLayer(list, hero, outfit.Gear, scale);
            DrawPiecesChip(list, hero, scale);
        }
        else
        {
            PrismArt.Placeholder(list, hero, false, scale);
            AppIconTextures.TryDrawArtwork(list, FitLogoArtwork, hero.Center, hero.Width * FitLogoShare,
                Vector4.One with { W = FitLogoAlpha });
            var radius = Metrics.Size.TapTarget * 0.5f * scale;
            if (RoundButton.Icon(list, new Vector2(hero.Center.X, hero.Max.Y - inset - radius), radius,
                    PhoneIcons.PhotoPlus, controls, ButtonStyle.Gray, Loc.T(L.Prism.AddPhoto)))
            {
                PickOutfitCover(outfit);
            }
        }

        DrawPhotoBack(list, hero, scale);

        var left = origin.X + inset;
        var right = origin.X + width - inset;
        var rowTop = hero.Max.Y + Metrics.Space.Sm * scale;
        var nameTop = rowTop + (Metrics.Size.TapTarget + Metrics.Space.Sm) * scale;
        var cursor = nameTop + PrismType.DrawHero(list, new Vector2(left, nameTop), outfit.Name, right - left,
            PrismArt.Fore) + Metrics.Space.Xxs * scale;
        Typography.Draw(list, new Vector2(left, cursor), Typography.FitText(fitMeta, right - left, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        cursor += Typography.LineHeight(PrismType.Caption);
        if (!glamour.HasGlamourer)
        {
            cursor += Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(left, cursor + Metrics.Space.Sm * scale),
                Loc.T(L.Prism.TryOnWithoutGlamourer), PrismArt.Muted, PrismType.Caption, right - left);
        }

        cursor = DrawPieceSection(list, left, right, cursor + Metrics.Space.Xl * scale, outfit.Gear, scale);
        cursor = DrawModSection(list, left, right, cursor, outfit.Gear, scale);
        DrawFitActionRow(list, left, right, rowTop, pinTop, outfit, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void DrawFitActionRow(ImDrawListPtr drawList, float start, float right, float naturalTop, float pinTop,
        WardrobeOutfit outfit, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var pinned = naturalTop < pinTop;
        var reveal = Math.Clamp(outfitPinReveal.Step(pinned ? 1f : 0f, Motion.Appear,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds)), 0f, 1f);
        var top = pinned ? pinTop : naturalTop;
        var slide = (pinned ? reveal - 1f : reveal) * tap;
        var blocked = pinned && UiInteract.InputBlocked;
        var left = start;
        if (pinned)
        {
            var windowLeft = ImGui.GetWindowPos().X;
            var bar = new Rect(new Vector2(windowLeft, ImGui.GetWindowPos().Y),
                new Vector2(windowLeft + ImGui.GetWindowWidth(), top + tap + Metrics.Space.Sm * scale));
            UiInteract.HoverOverlay(bar);
            drawList.AddRectFilled(bar.Min, bar.Max, ImGui.GetColorU32(PrismArt.Back));
            var backMark = PrismMotion.Mark(drawList);
            if (RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f, top + radius), radius,
                    PhoneIcons.ChevronLeft, controls, ButtonStyle.Bare, Loc.T(L.Prism.Back), HoverLabelSide.Below,
                    overlay: true) && !UiInteract.InputBlocked)
            {
                LeaveScreen();
            }

            if (reveal < PrismMotion.Settled)
            {
                PrismMotion.Shift(drawList, backMark, new Vector2(slide, 0f), reveal);
            }

            left += tap;
        }

        var iconsMark = PrismMotion.Mark(drawList);
        if (RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f, top + radius), radius,
                outfit.Favourite ? PhoneIcons.StarFilled : PhoneIcons.Star, controls, ButtonStyle.Bare,
                Loc.T(outfit.Favourite ? L.Prism.Unfavourite : L.Prism.Favourite), HoverLabelSide.Below,
                overlay: pinned) && !blocked)
        {
            wardrobe.Update(outfit with { Favourite = !outfit.Favourite });
        }

        if (RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f + tap, top + radius), radius,
                PhoneIcons.Pencil, controls, ButtonStyle.Bare, Loc.T(L.Prism.EditOutfit), HoverLabelSide.Below,
                overlay: pinned) && !blocked)
        {
            StartOutfitEdit(outfit);
        }

        var canPost = glams.IsSignedIn;
        if (canPost && RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f + tap * 2f, top + radius), radius,
                PhoneIcons.Send, controls, ButtonStyle.Bare, Loc.T(L.Prism.PostGlam), HoverLabelSide.Below,
                overlay: pinned) && !blocked)
        {
            PostOutfit(outfit);
        }

        if (MathF.Abs(slide) > PrismMotion.GrowEpsilon)
        {
            PrismMotion.Shift(drawList, iconsMark, new Vector2(slide, 0f), 1f);
        }

        var putOn = Loc.T(L.Prism.PutOn);
        var takeOff = Loc.T(L.Prism.TakeOff);
        var cap = MathF.Max(1f, right - start - tap * (canPost ? 4f : 3f));
        var width = MathF.Min(ButtonWidth([putOn, takeOff], cap, scale), cap);
        var pill = new Rect(new Vector2(right - width, top), new Vector2(right, top + tap));
        var wearing = Wearing(outfit.Id);
        PrismLight.Primary(drawList, pill, !blocked, scale);
        if (!DrawPill(drawList, pill, wearing ? takeOff : putOn, false, pinned, blocked, scale))
        {
            return;
        }

        if (wearing)
        {
            glamour.Revert();
        }
        else
        {
            WearOutfit(outfit);
        }
    }

    private void SyncFit(WardrobeOutfit outfit)
    {
        RefreshPieces(outfit.Id, outfit.Gear, tryOn: true);
        if (ReferenceEquals(outfit, fitFor) && ReferenceEquals(fitLanguage, Loc.Current.Code))
        {
            return;
        }

        fitFor = outfit;
        fitLanguage = Loc.Current.Code;
        var saved = Loc.T(L.Prism.SavedOn, TimeText.MonthDay(outfit.CreatedAtUnix));
        fitMeta = totalPieces == 0 && totalMods == 0 ? saved
            : totalMods == 0 ? string.Concat(PieceCountLine(), GlamMetaSeparator, saved)
            : totalPieces == 0 ? string.Concat(ModCountLine(), GlamMetaSeparator, saved)
            : string.Concat(PieceCountLine(), GlamMetaSeparator, ModCountLine(), GlamMetaSeparator, saved);
    }

    private bool DrawOutfitCover(ImDrawListPtr drawList, Rect rect, WardrobeCover? cover)
    {
        if (cover is null || wallpaperImages.Get(cover.Path) is not { } texture)
        {
            return false;
        }

        var (uv0, uv1) = new WallpaperCrop(cover.Zoom, cover.CenterX, cover.CenterY)
            .ComputeUv(texture.Size, rect.Width / MathF.Max(1f, rect.Height));
        drawList.AddImage(texture.Handle, rect.Min, rect.Max, uv0, uv1);
        return true;
    }

    private void PickOutfitCover(WardrobeOutfit outfit)
    {
        coverForEditor = false;
        Push(PrismScreen.WardrobeCover);
        coverFor = outfit.Id;
        OpenPhotoPick();
    }

    private void AddSheetAction(bool shown, WardrobeAction action)
    {
        if (shown && sheetCount < SheetCapacity)
        {
            sheetActions[sheetCount++] = action;
        }
    }

    private void OpenWardrobeSheet()
    {
        sheetScreen = screen;
        wardrobeSheet.Open();
    }

    private void GateWardrobeSheet()
    {
        if (sheetScreen == screen)
        {
            wardrobeSheet.Gate();
        }
        else if (wardrobeSheet.IsOpen)
        {
            wardrobeSheet.Close();
        }
    }

    private void DrawWardrobeSheet(Rect full)
    {
        if (sheetCount == 0 || sheetScreen != screen || !wardrobeSheet.CapturesPointer)
        {
            return;
        }

        for (var index = 0; index < sheetCount; index++)
        {
            sheetItems[index] = new ActionSheet.Item(Loc.T(SheetLabel(sheetActions[index])));
        }

        var picked = wardrobeSheet.Draw(full, ActionSheetStyle.From(ui),
            new ReadOnlySpan<ActionSheet.Item>(sheetItems, 0, sheetCount), Loc.T(L.Common.Cancel), false);
        if (picked >= 0)
        {
            RunSheetAction(sheetActions[picked]);
        }
    }

    private static LocString SheetLabel(WardrobeAction action) => action switch
    {
        WardrobeAction.Import => L.Prism.ImportDesigns,
        WardrobeAction.Select => L.Photos.Select,
        WardrobeAction.ZoomIn => L.Photos.ZoomIn,
        WardrobeAction.ZoomOut => L.Photos.ZoomOut,
        _ => L.Prism.SurpriseMe,
    };

    private void RunSheetAction(WardrobeAction action)
    {
        switch (action)
        {
            case WardrobeAction.Import:
                OpenDesignPicker();
                return;
            case WardrobeAction.Select:
                StartOutfitSelect();
                return;
            case WardrobeAction.ZoomIn:
                SetWardrobeColumns(WardrobeColumns - 1);
                return;
            case WardrobeAction.ZoomOut:
                SetWardrobeColumns(WardrobeColumns + 1);
                return;
        }

        var outfits = wardrobe.Outfits;
        if (outfits.Length > 0)
        {
            WearOutfit(outfits[roulette.Next(outfits.Length)]);
        }
    }

    private void DrawWardrobeCover(ImDrawListPtr drawList, Rect content, float scale)
    {
        var outfit = coverForEditor ? null : wardrobe.Find(coverFor);
        if (!coverForEditor && outfit is null)
        {
            GoBack();
            return;
        }

        if (!DrawPhotoPick(drawList, content, Loc.T(L.Prism.AddPhoto), GlamStore.PhotoRatio, scale))
        {
            return;
        }

        var crop = composeSession.CropsArray()[0];
        var cover = new WardrobeCover(composeSession.FirstSelected, crop.Zoom, crop.CenterX, crop.CenterY);
        if (outfit is null)
        {
            outfitCoverDraft = cover;
        }
        else
        {
            wardrobe.Update(outfit with { Cover = cover });
        }

        GoBack();
    }

    private void WearOutfit(WardrobeOutfit outfit)
    {
        StartWearing(outfit.Id, outfit.Name);
        if (outfit.State.Length > 0 && glamour.ApplyState(outfit.State, outfit.Gear))
        {
            return;
        }

        if (outfit.DesignId != Guid.Empty && glamour.ApplyDesign(outfit.DesignId, outfit.Gear))
        {
            return;
        }

        glamour.TryOn(outfit.Gear);
    }

    private void SaveToGlamourer(WardrobeOutfit outfit)
    {
        if (outfit.DesignId != Guid.Empty && glamour.HasDesign(outfit.DesignId))
        {
            ShellToast.Show(Loc.T(L.Prism.Saved));
            return;
        }

        var designId = glamour.SaveDesign(outfit.State, outfit.Name);
        if (designId == Guid.Empty)
        {
            return;
        }

        wardrobe.Update(outfit with { DesignId = designId });
        ShellToast.Show(Loc.T(L.Prism.Saved));
    }

    private string OutfitName(int number)
    {
        var key = HashCode.Combine(number, Loc.Current.Code);
        return outfitNameText.IsCurrent(key)
            ? outfitNameText.Value
            : outfitNameText.Store(key, Loc.T(L.Prism.OutfitNumber, number));
    }
}
