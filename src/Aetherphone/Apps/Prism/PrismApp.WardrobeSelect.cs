using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const string WardrobeSearchId = "##wardrobeSearch";
    private const int WardrobeQueryMaxLength = 64;
    private const float SelectMarkUnits = 24f;
    private const float SelectMarkShade = 0.4f;
    private const float SelectActionShare = 0.34f;

    private readonly HashSet<string> selectedOutfits = new(StringComparer.Ordinal);
    private readonly HashSet<string> shownOutfitIds = new(StringComparer.Ordinal);
    private int[] shownOutfits = Array.Empty<int>();
    private WardrobeOutfit[]? shownOutfitsFor;
    private string shownOutfitsQuery = string.Empty;
    private string wardrobeQuery = string.Empty;
    private int shownOutfitCount;
    private bool shownOutfitsFavourites;
    private bool wardrobeSearchFocus;
    private bool selectingOutfits;
    private bool allShownSelected;
    private bool selectionStale = true;
    private Spring wardrobeSearchEdge;
    private CachedText selectedOutfitsText;
    private CachedText deleteOutfitsText;

    private void SyncShownOutfits()
    {
        var outfits = wardrobe.Outfits;
        var changed = !ReferenceEquals(outfits, shownOutfitsFor);
        if (!changed && shownOutfitsFavourites == favouritesOnly &&
            string.Equals(shownOutfitsQuery, wardrobeQuery, StringComparison.Ordinal))
        {
            return;
        }

        shownOutfitsFor = outfits;
        shownOutfitsFavourites = favouritesOnly;
        shownOutfitsQuery = wardrobeQuery;
        if (shownOutfits.Length < outfits.Length)
        {
            shownOutfits = new int[outfits.Length];
        }

        var query = wardrobeQuery.Trim();
        var compare = Loc.Culture.CompareInfo;
        var count = 0;
        shownOutfitIds.Clear();
        for (var index = 0; index < outfits.Length; index++)
        {
            var outfit = outfits[index];
            if (favouritesOnly && !outfit.Favourite)
            {
                continue;
            }

            if (query.Length > 0 && compare.IndexOf(outfit.Name, query, DesignPicker.MatchOptions) < 0)
            {
                continue;
            }

            shownOutfits[count++] = index;
            shownOutfitIds.Add(outfit.Id);
        }

        shownOutfitCount = count;
        if (selectedOutfits.Count > 0)
        {
            selectedOutfits.IntersectWith(shownOutfitIds);
        }

        selectionStale = true;
    }

    private void StartOutfitSelect()
    {
        selectedOutfits.Clear();
        selectingOutfits = true;
        selectionStale = true;
    }

    private void ExitOutfitSelect()
    {
        if (!selectingOutfits && selectedOutfits.Count == 0)
        {
            return;
        }

        selectedOutfits.Clear();
        selectingOutfits = false;
        selectionStale = true;
    }

    private void ToggleOutfit(string id)
    {
        if (!selectedOutfits.Remove(id))
        {
            selectedOutfits.Add(id);
        }

        selectionStale = true;
    }

    private bool AllShownSelected()
    {
        if (!selectionStale)
        {
            return allShownSelected;
        }

        selectionStale = false;
        var outfits = shownOutfitsFor ?? Array.Empty<WardrobeOutfit>();
        allShownSelected = shownOutfitCount > 0;
        for (var index = 0; index < shownOutfitCount && allShownSelected; index++)
        {
            allShownSelected = selectedOutfits.Contains(outfits[shownOutfits[index]].Id);
        }

        return allShownSelected;
    }

    private void SelectShownOutfits()
    {
        var outfits = shownOutfitsFor ?? Array.Empty<WardrobeOutfit>();
        for (var index = 0; index < shownOutfitCount; index++)
        {
            selectedOutfits.Add(outfits[shownOutfits[index]].Id);
        }

        selectionStale = true;
    }

    private float DrawSelectHeader(ImDrawListPtr drawList, Rect content, float scale)
    {
        var height = Metrics.Size.TapTarget * scale;
        var top = content.Min.Y + Metrics.Space.Sm * scale;
        var middle = top + height * 0.5f;
        var cap = content.Width * SelectActionShare;
        var cancel = Loc.T(L.Common.Cancel);
        var cancelRect = PlainRect(new Vector2(content.Min.X, middle), cancel, cap, 0f, scale);
        if (DrawButton(drawList, cancelRect, cancel, ButtonStyle.Plain, scale, id: "prism.wardrobe.cancelSelect"))
        {
            ExitOutfitSelect();
            return top + height;
        }

        var all = AllShownSelected();
        var toggle = Loc.T(all ? L.Prism.DeselectAll : L.Prism.SelectAll);
        var toggleRect = PlainRect(new Vector2(content.Max.X, middle), toggle, cap, 1f, scale);
        if (DrawButton(drawList, toggleRect, toggle, ButtonStyle.Plain, scale, enabled: all || shownOutfitCount > 0,
                id: "prism.wardrobe.selectAll"))
        {
            if (all)
            {
                selectedOutfits.Clear();
                selectionStale = true;
            }
            else
            {
                SelectShownOutfits();
            }
        }

        var center = content.Center.X;
        var room = MathF.Max(1f,
            2f * MathF.Min(center - cancelRect.Max.X, toggleRect.Min.X - center) - Metrics.Space.Sm * 2f * scale);
        var title = Typography.FitText(SelectedOutfitsLabel(), room, PrismType.Headline);
        var size = Typography.Measure(title, PrismType.Headline);
        Typography.Draw(drawList, new Vector2(center - size.X * 0.5f, middle - size.Y * 0.5f), title, PrismArt.Fore,
            PrismType.Headline);
        return top + height;
    }

    private float DrawSelectActions(ImDrawListPtr drawList, Rect content, float scale)
    {
        var button = ActionRect(content, scale);
        var count = selectedOutfits.Count;
        if (Button.Draw(drawList, button, DeleteOutfitsLabel(count), controls, ButtonStyle.Gray,
                ButtonRole.Destructive, enabled: count > 0, id: "prism.wardrobe.deleteSelected"))
        {
            AskDeleteSelected();
        }

        return button.Min.Y - Metrics.Space.Md * scale;
    }

    private static void DrawSelectMark(ImDrawListPtr drawList, Rect photo, bool picked, float scale)
    {
        if (picked)
        {
            PrismLight.Edge(drawList, photo, 0f, PrismGlow.Spectrum, PrismLight.EdgeAlpha, scale);
        }

        var size = SelectMarkUnits * scale;
        var inset = Metrics.Space.Sm * scale;
        var center = new Vector2(photo.Max.X - inset - size * 0.5f, photo.Min.Y + inset + size * 0.5f);
        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(PrismArt.Ink with { W = SelectMarkShade }));
        PhoneIcon.Draw(drawList, center, picked ? PhoneIcons.CircleCheck : PhoneIcons.Circle, PrismArt.PhotoFore,
            size);
    }

    private string SelectedOutfitsLabel()
    {
        var count = selectedOutfits.Count;
        var key = HashCode.Combine(count, Loc.Current.Code);
        return selectedOutfitsText.IsCurrent(key)
            ? selectedOutfitsText.Value
            : selectedOutfitsText.Store(key, Loc.Plural(L.Prism.OutfitsSelected, count));
    }

    private string DeleteOutfitsLabel(int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        if (deleteOutfitsText.IsCurrent(key))
        {
            return deleteOutfitsText.Value;
        }

        return deleteOutfitsText.Store(key,
            count > 0 ? Loc.Plural(L.Prism.DeleteOutfits, count) : Loc.T(L.Common.Delete));
    }

    private void AskDeleteSelected() =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.Plural(L.Prism.DeleteOutfitsTitle, selectedOutfits.Count),
            Message = Loc.T(L.Prism.DeleteOutfitsBody),
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Sheet = true,
            Confirm = DeleteSelectedOutfits,
        });

    private void DeleteSelectedOutfits()
    {
        wardrobe.RemoveMany(selectedOutfits);
        ExitOutfitSelect();
    }
}
