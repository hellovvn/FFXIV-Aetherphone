using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private void OpenWardrobePick()
    {
        wardrobe.Ensure();
        Push(PrismScreen.WardrobePick);
    }

    private void DrawWardrobePick(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        wardrobe.Ensure();
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.FillFromWardrobe), scale) + Metrics.Space.Sm * scale;
        var outfits = wardrobe.Outfits;
        if (!wardrobe.Loading && outfits.Length == 0)
        {
            DrawEmpty(drawList, new Rect(new Vector2(content.Min.X, top), content.Max),
                IconGlyph.Of(FontAwesomeIcon.Tshirt), Loc.T(L.Prism.WardrobeEmptyTitle),
                Loc.T(L.Prism.WardrobeEmptyHint), string.Empty, ButtonStyle.Solid, scale, out _);
            return;
        }

        WardrobeOutfit? picked = null;
        using (BeginBody(new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y))))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = Metrics.Space.Xxs * scale;
            var gridWidth = width - gap * 2f;
            var loading = wardrobe.Loading;
            var count = loading ? PlaceholderRows * GridColumns : outfits.Length;
            var pitch = MathF.Max(1f, GridPitch(gridWidth, scale, GridColumns));
            var windowTop = ImGui.GetWindowPos().Y;
            var first = Math.Clamp((int)MathF.Floor((windowTop - origin.Y) / pitch) * GridColumns, 0, count);
            var last = Math.Clamp(
                ((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - origin.Y) / pitch) + 1) * GridColumns,
                first, count);
            for (var index = first; index < last; index++)
            {
                var photo = GridCell(origin.X + gap, origin.Y, gridWidth, index, scale);
                if (loading)
                {
                    PrismArt.Placeholder(list, photo, false, scale);
                    continue;
                }

                var outfit = outfits[index];
                var mark = PrismMotion.Mark(list);
                DrawOutfitTileCover(list, photo, outfit, scale);
                if (DrawGridTile(list, mark, photo, string.Empty, outfit.Name, string.Empty, PieceLabel(outfit.Gear),
                        scale))
                {
                    picked = outfit;
                }
            }

            ImGui.Dummy(new Vector2(width, GridHeight(gridWidth, count, scale) + PrismArt.BottomMargin(scale)));
        }

        if (picked is null)
        {
            return;
        }

        FillFromOutfit(picked);
        GoBack();
    }

    private void DrawOutfitTileCover(ImDrawListPtr drawList, Rect photo, WardrobeOutfit outfit, float scale)
    {
        if (DrawOutfitCover(drawList, photo, outfit.Cover))
        {
            return;
        }

        PrismArt.Placeholder(drawList, photo, false, scale);
        AppIconTextures.TryDrawArtwork(drawList, FitLogoArtwork,
            new Vector2(photo.Center.X, photo.Min.Y + photo.Height * GridLogoHeightShare),
            photo.Width * GridLogoShare, Vector4.One with { W = FitLogoAlpha });
    }

    private void PostOutfit(WardrobeOutfit outfit)
    {
        StartCompose();
        FillFromOutfit(outfit);
        if (composeMods.Count > 0)
        {
            composeVanilla = false;
        }

        if (outfit.Cover is { } cover && !wallpaperImages.Failed(cover.Path) && File.Exists(cover.Path))
        {
            composeSession.TakeImported(cover.Path);
        }
    }

    private void FillFromOutfit(WardrobeOutfit outfit)
    {
        LoadGear(glamGear, outfit.Gear);
        if (composeVanilla == true)
        {
            return;
        }

        composeMods.Clear();
        var mods = PostedMods.GearMods(outfit.Gear);
        for (var index = 0; index < mods.Length && composeMods.Count < MaxNamedMods; index++)
        {
            composeMods.Add(mods[index].Mod, mods[index].Slot);
        }
    }
}
