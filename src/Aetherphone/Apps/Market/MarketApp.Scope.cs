using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private const float ScopeTrackHeight = 30f;
    private const float ScopeButtonGap = 8f;
    private const float ScopePillPad = 12f;
    private const float ScopeGlyphSize = 16f;
    private const float ScopeChevronSize = 12f;
    private const float ScopeBackGlyphSize = 11f;
    private const float ScopeLabelSlack = 1f;
    private const string ScopeBackId = "market.scope.back";
    private const string ScopeSeparator = " · ";
    private const float PickerHeaderHeight = 44f;
    private const float PickerPadX = 20f;
    private const float PickerSectionHeight = 34f;
    private const float PickerSectionBottom = 6f;
    private const float PickerRowHeight = 52f;
    private const float PickerCompactRowHeight = 46f;
    private const float PickerWorldRowHeight = 44f;
    private const float PickerWorldIndent = 22f;
    private const float PickerCheckSize = 16f;
    private const float PickerChevronReach = 16f;
    private const float PickerChevronGlyph = 14f;
    private const float PickerHoverAlpha = 0.07f;
    private const float PickerHairlineAlpha = 0.10f;
    private const float PickerMutedAlpha = 0.62f;
    private const float PickerLeadShare = 0.3f;
    private const int PickerScrollFrames = 2;

    private readonly Sheet scopeSheet = new();
    private readonly Dictionary<string, string> worldCountLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, string> allOfLabels = new();
    private CultureInfo? scopeLabelCulture;
    private string expandedDataCenter = string.Empty;
    private int pickerScrollPending;
    private float pickerSelectedTop;
    private CachedText backLabel;

    private enum PickerAction : byte
    {
        None,
        Pick,
        Expand,
    }

    private readonly record struct PickerInk(ImDrawListPtr DrawList, Vector4 Ink, Vector4 Muted, Vector4 Accent,
        bool Interactive, float Scale);

    private readonly record struct ScopePick(bool Picked, int LocalIndex, MarketScopeKind Kind, string Name);

    private float DrawScopeStrip(Vector2 origin, float width, string? anchor, float scale)
    {
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + ScopeStripHeight * scale));
        if (watchlist.IsRemote)
        {
            DrawRemoteScope(row, anchor, scale);
            return row.Max.Y;
        }

        var scopes = watchlist.Scopes;
        if (scopes.Count == 0)
        {
            return origin.Y;
        }

        scopeLabels.Clear();
        for (var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++)
        {
            scopeLabels.Add(scopes[scopeIndex].ApiName);
        }

        var radius = RoundButton.SmallRadius * scale;
        var strip = new Rect(row.Min, new Vector2(row.Max.X - radius * 2f - ScopeButtonGap * scale, row.Max.Y));
        if (anchor is not null)
        {
            UiAnchors.Report(anchor, strip);
        }

        var current = watchlist.ScopeIndex;
        var selected = SegmentStrip.Draw(anchor ?? "market.scope.root", strip, scopeLabels, current, ui.Palette);
        if (selected != current && selected >= 0)
        {
            UiFeedback.Play(UiSound.Tap);
            watchlist.SetScope(selected);
        }

        var center = new Vector2(row.Max.X - radius, row.Center.Y);
        UiAnchors.Report("market.scope.picker",
            new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)));
        if (RoundButton.Icon(ImGui.GetWindowDrawList(), center, radius, PhoneIcons.World, ui.Ink, ButtonStyle.Gray,
                Loc.T(L.Market.OtherScopes)))
        {
            OpenScopePicker();
        }

        return row.Max.Y;
    }

    private void DrawRemoteScope(Rect row, string? anchor, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var trackHeight = ScopeTrackHeight * scale;
        var top = row.Center.Y - trackHeight * 0.5f;
        var bottom = top + trackHeight;
        var gap = ScopeButtonGap * scale;
        var scope = Scope;
        var title = ScopeTitle(in scope);
        var pillRight = row.Max.X;
        var local = watchlist.LocalScope;
        if (local.IsValid)
        {
            var localTitle = ScopeTitle(in local);
            var label = BackLabel(localTitle);
            var labelStyle = Button.LabelStyle(trackHeight);
            var slack = ScopeLabelSlack * scale;
            var fullWidth = MathF.Ceiling(Typography.Measure(label, labelStyle).X) + slack + trackHeight;
            var compact = row.Width - fullWidth - gap < PillMinimumWidth(title, scale);
            var buttonWidth = compact
                ? trackHeight + ScopeBackGlyphSize * scale + Metrics.Space.Xs * scale +
                  MathF.Ceiling(Typography.Measure(localTitle, labelStyle).X) + slack
                : fullWidth;
            var button = new Rect(new Vector2(row.Max.X - buttonWidth, top), new Vector2(row.Max.X, bottom));
            UiAnchors.Report(ScopeBackId, button);
            var pressed = compact
                ? DrawCompactBack(drawList, button, localTitle, label, in labelStyle, scale)
                : Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Tinted, id: ScopeBackId,
                    labelRoom: buttonWidth - trackHeight);
            if (pressed)
            {
                watchlist.ReturnToLocal();
            }

            pillRight = button.Min.X - gap;
        }

        var pill = new Rect(new Vector2(row.Min.X, top), new Vector2(pillRight, bottom));
        UiAnchors.Report("market.scope.remote", pill);
        if (anchor is not null)
        {
            UiAnchors.Report(anchor, pill);
        }

        var hovered = UiInteract.Hover(pill.Min, pill.Max);
        var fill = Surfaces.Fill(ui.TitleInk, hovered ? FillLevel.Secondary : FillLevel.Tertiary);
        Squircle.Fill(drawList, pill.Min, pill.Max, trackHeight * 0.5f, ImGui.GetColorU32(fill));
        var pad = ScopePillPad * scale;
        var glyph = ScopeGlyphSize * scale;
        PhoneIcon.Draw(drawList, new Vector2(pill.Min.X + pad + glyph * 0.5f, pill.Center.Y), PhoneIcons.World,
            ui.Accent, glyph);
        var chevron = ScopeChevronSize * scale;
        var chevronCenter = new Vector2(pill.Max.X - pad - chevron * 0.5f, pill.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronDown, ui.MutedInk, chevron);
        var textLeft = pill.Min.X + pad + glyph + Metrics.Space.Sm * scale;
        var textRight = chevronCenter.X - chevron * 0.5f - Metrics.Space.Sm * scale;
        DrawScopeName(drawList, textLeft, textRight, pill.Center.Y, title, ScopeParent(in scope));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pill.Min, pill.Max, hovered))
        {
            OpenScopePicker();
        }
    }

    private static float PillMinimumWidth(string title, float scale) =>
        (ScopePillPad * 2f + ScopeGlyphSize + ScopeChevronSize + Metrics.Space.Sm * 2f) * scale +
        Typography.Measure(title, TextStyles.SubheadlineEmphasized).X;

    private bool DrawCompactBack(ImDrawListPtr drawList, Rect button, string name, string tooltip,
        in TextStyle style, float scale)
    {
        var key = ImGui.GetID(ScopeBackId);
        var hovered = UiInteract.Hover(button.Min, button.Max);
        var face = Button.Surface(drawList, button, ui.Ink, ButtonStyle.Tinted, ButtonRole.Normal, true, hovered,
            key);
        var glyph = ScopeBackGlyphSize * scale;
        var spacing = Metrics.Space.Xs * scale;
        var nameWidth = Typography.Measure(name, style).X;
        var left = face.Face.Center.X - (glyph + spacing + nameWidth) * 0.5f;
        var centerY = face.Face.Center.Y;
        ProgressRing.CenterIcon(drawList, new Vector2(left + glyph * 0.5f, centerY), FontAwesomeIcon.LocationArrow,
            face.LabelInk, glyph);
        Typography.Draw(drawList, new Vector2(left + glyph + spacing, centerY - Typography.LineHeight(style) * 0.5f),
            name, face.LabelInk, style);
        HoverTooltip.Show(key, button, tooltip, hovered, HoverLabelSide.Above);
        return UiInteract.Click(button.Min, button.Max, hovered);
    }

    private void DrawScopeName(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string parent)
    {
        var room = MathF.Max(1f, right - left);
        var nameStyle = TextStyles.SubheadlineEmphasized;
        var name = Typography.FitText(title, room, nameStyle);
        var nameWidth = Typography.Measure(name, nameStyle).X;
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(nameStyle) * 0.5f), name,
            ui.TitleInk, nameStyle);
        if (parent.Length == 0)
        {
            return;
        }

        var separatorWidth = Typography.Measure(ScopeSeparator, TextStyles.Subheadline).X;
        if (nameWidth + separatorWidth + Typography.Measure(parent, TextStyles.Subheadline).X > room)
        {
            return;
        }

        var parentTop = centerY - Typography.LineHeight(TextStyles.Subheadline) * 0.5f;
        Typography.Draw(drawList, new Vector2(left + nameWidth, parentTop), ScopeSeparator, ui.MutedInk,
            TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left + nameWidth + separatorWidth, parentTop), parent, ui.MutedInk,
            TextStyles.Subheadline);
    }

    private string ScopeTitle(in MarketScope scope) => watchlist.Title(in scope);

    private string ScopeParent(in MarketScope scope)
    {
        var parent = watchlist.Geography.ParentOf(scope.Kind, scope.ApiName);
        return scope.Kind == MarketScopeKind.DataCenter && parent.Length > 0 ? watchlist.RegionTitle(parent) : parent;
    }

    private string BackLabel(string localName)
    {
        var key = (long)localName.GetHashCode();
        return backLabel.IsCurrent(key) ? backLabel.Value : backLabel.Store(key, Loc.T(L.Market.BackTo, localName));
    }

    private void OpenScopePicker()
    {
        var scope = Scope;
        expandedDataCenter = string.Empty;
        if (watchlist.IsRemote && scope.Kind == MarketScopeKind.World)
        {
            expandedDataCenter = watchlist.Geography.DataCenterOfWorld(scope.ApiName)?.Name ?? string.Empty;
        }

        pickerScrollPending = watchlist.IsRemote ? PickerScrollFrames : 0;
        scopeSheet.Open();
    }

    private void DrawScopeSheet(Rect screen)
    {
        if (!scopeSheet.CapturesPointer)
        {
            return;
        }

        using var layer = ScreenLayer.Begin("market.scopeSheet", screen, false);
        var frame = scopeSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Standard(screen.Height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var pick = DrawScopeSheetBody(in frame);
        scopeSheet.End(in frame);
        if (!pick.Picked)
        {
            return;
        }

        if (pick.LocalIndex >= 0)
        {
            watchlist.SetScope(pick.LocalIndex);
        }
        else
        {
            watchlist.Choose(pick.Kind, pick.Name);
        }

        scopeSheet.Close();
    }

    private ScopePick DrawScopeSheetBody(in SheetFrame frame)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * PickerMutedAlpha };
        var accent = ui.Accent with { W = ui.Accent.W * frame.Opacity };
        var headerHeight = PickerHeaderHeight * scale;
        Typography.DrawCentered(frame.DrawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.5f),
            Loc.T(L.Market.ScopeTitle), ink, TextStyles.Headline);
        var list = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        if (list.Height <= 0f)
        {
            return default;
        }

        var pick = default(ScopePick);
        pickerSelectedTop = -1f;
        using (UiScope.PushId("market.scopePicker"))
        using (var surface = AppSurface.BeginEdgeToEdge(list))
        {
            var paint = new PickerInk(ImGui.GetWindowDrawList(), ink, muted, accent, frame.Interactive, scale);
            using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                DrawNearYou(in paint, ref pick);
                var regions = watchlist.Geography.Regions;
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    DrawRegion(in paint, regions[regionIndex], ref pick);
                }

                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            if (pickerScrollPending > 0)
            {
                pickerScrollPending--;
                if (pickerScrollPending == 0 && pickerSelectedTop >= 0f)
                {
                    surface.JumpTo(MathF.Max(0f, pickerSelectedTop - list.Height * PickerLeadShare));
                }
            }
        }

        return pick;
    }

    private void DrawNearYou(in PickerInk paint, ref ScopePick pick)
    {
        var local = watchlist.Scopes;
        if (local.Count == 0)
        {
            return;
        }

        PickerSection(in paint, Loc.T(L.Venues.NearYouSection));
        var remote = watchlist.IsRemote;
        for (var scopeIndex = 0; scopeIndex < local.Count; scopeIndex++)
        {
            var scope = local[scopeIndex];
            var title = ScopeTitle(in scope);
            var subtitle = Loc.T(scope.Kind switch
            {
                MarketScopeKind.World => L.Venues.YourWorld,
                MarketScopeKind.DataCenter => L.Venues.YourDataCenter,
                _ => L.Venues.YourRegion,
            });
            var selected = !remote && watchlist.ScopeIndex == scopeIndex;
            if (PickerRow(in paint, PickerRowHeight, title, subtitle, selected, 0f, scopeIndex < local.Count - 1))
            {
                pick = new ScopePick(true, scopeIndex, scope.Kind, scope.ApiName);
            }
        }
    }

    private void DrawRegion(in PickerInk paint, MarketRegion region, ref ScopePick pick)
    {
        PickerSection(in paint, Loc.T(region.Label));
        var scope = Scope;
        var remote = watchlist.IsRemote;
        if (PickerRow(in paint, PickerCompactRowHeight, AllOfLabel(region), string.Empty,
                remote && IsScope(in scope, MarketScopeKind.Region, region.ApiName), 0f, true))
        {
            pick = new ScopePick(true, -1, MarketScopeKind.Region, region.ApiName);
        }

        var dataCenters = region.DataCenters;
        for (var dataCenterIndex = 0; dataCenterIndex < dataCenters.Length; dataCenterIndex++)
        {
            var dataCenter = dataCenters[dataCenterIndex];
            var expanded = string.Equals(expandedDataCenter, dataCenter.Name, StringComparison.Ordinal);
            var lastDataCenter = dataCenterIndex == dataCenters.Length - 1;
            var action = DataCenterRow(in paint, dataCenter,
                remote && IsScope(in scope, MarketScopeKind.DataCenter, dataCenter.Name), expanded,
                !lastDataCenter || expanded);
            if (action == PickerAction.Pick)
            {
                pick = new ScopePick(true, -1, MarketScopeKind.DataCenter, dataCenter.Name);
            }
            else if (action == PickerAction.Expand)
            {
                expandedDataCenter = expanded ? string.Empty : dataCenter.Name;
            }

            if (!expanded)
            {
                continue;
            }

            var worlds = dataCenter.Worlds;
            for (var worldIndex = 0; worldIndex < worlds.Length; worldIndex++)
            {
                var separator = !lastDataCenter || worldIndex < worlds.Length - 1;
                if (PickerRow(in paint, PickerWorldRowHeight, worlds[worldIndex], string.Empty,
                        remote && IsScope(in scope, MarketScopeKind.World, worlds[worldIndex]), PickerWorldIndent,
                        separator))
                {
                    pick = new ScopePick(true, -1, MarketScopeKind.World, worlds[worldIndex]);
                }
            }
        }
    }

    private static bool IsScope(in MarketScope scope, MarketScopeKind kind, string name) =>
        scope.Kind == kind && string.Equals(scope.ApiName, name, StringComparison.OrdinalIgnoreCase);

    private static void PickerSection(in PickerInk paint, string label)
    {
        var scale = paint.Scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = PickerSectionHeight * scale;
        ImGui.Dummy(new Vector2(width, height));
        if (!ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            return;
        }

        var padX = PickerPadX * scale;
        var top = origin.Y + height - ListSection.OverlineHeight - PickerSectionBottom * scale;
        ListSection.PaintOverline(paint.DrawList, new Vector2(origin.X + padX, top), label, paint.Muted,
            MathF.Max(1f, width - padX * 2f));
    }

    private bool PickerRow(in PickerInk paint, float heightUnits, string title, string subtitle, bool selected,
        float indentUnits, bool separator)
    {
        var scale = paint.Scale;
        var origin = ImGui.GetCursorScreenPos();
        var localTop = ImGui.GetCursorPosY();
        var width = ScrollLayout.StableContentWidth();
        var height = heightUnits * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ImGui.Dummy(new Vector2(width, height));
        if (selected)
        {
            pickerSelectedTop = localTop;
        }

        if (!ImGui.IsRectVisible(origin, max))
        {
            return false;
        }

        var hovered = paint.Interactive && UiInteract.Hover(origin, max);
        PickerHover(in paint, origin, max, hovered);
        var padX = PickerPadX * scale;
        var left = origin.X + padX + indentUnits * scale;
        var right = max.X - padX;
        var centerY = origin.Y + height * 0.5f;
        if (selected)
        {
            var check = PickerCheckSize * scale;
            PhoneIcon.Draw(paint.DrawList, new Vector2(right - check * 0.5f, centerY), PhoneIcons.Check, paint.Accent,
                check);
            right -= check + Metrics.Space.Sm * scale;
        }

        PickerText(in paint, left, right, centerY, title, subtitle, selected);
        if (separator)
        {
            PickerHairline(in paint, left, max.X - padX, max.Y);
        }

        return paint.Interactive && UiInteract.Click(origin, max, hovered);
    }

    private PickerAction DataCenterRow(in PickerInk paint, MarketDataCenter dataCenter, bool selected, bool expanded,
        bool separator)
    {
        var scale = paint.Scale;
        var origin = ImGui.GetCursorScreenPos();
        var localTop = ImGui.GetCursorPosY();
        var width = ScrollLayout.StableContentWidth();
        var height = PickerRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ImGui.Dummy(new Vector2(width, height));
        if (selected)
        {
            pickerSelectedTop = localTop;
        }

        if (!ImGui.IsRectVisible(origin, max))
        {
            return PickerAction.None;
        }

        var padX = PickerPadX * scale;
        var centerY = origin.Y + height * 0.5f;
        var reach = PickerChevronReach * scale;
        var chevronCenter = new Vector2(max.X - padX - reach, centerY);
        var chevronMin = chevronCenter - new Vector2(reach, reach);
        var chevronMax = chevronCenter + new Vector2(reach, reach);
        var overChevron = paint.Interactive && UiInteract.Hover(chevronMin, chevronMax);
        var hovered = paint.Interactive && !overChevron && UiInteract.Hover(origin, max);
        PickerHover(in paint, origin, max, hovered);
        if (overChevron)
        {
            paint.DrawList.AddCircleFilled(chevronCenter, reach,
                ImGui.GetColorU32(paint.Ink with { W = paint.Ink.W * PickerHoverAlpha }), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(paint.DrawList, chevronCenter, expanded ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown,
            overChevron ? paint.Ink : paint.Muted, PickerChevronGlyph * scale);
        var left = origin.X + padX;
        var right = chevronMin.X - Metrics.Space.Xs * scale;
        if (selected)
        {
            var check = PickerCheckSize * scale;
            PhoneIcon.Draw(paint.DrawList, new Vector2(right - check * 0.5f, centerY), PhoneIcons.Check, paint.Accent,
                check);
            right -= check + Metrics.Space.Sm * scale;
        }

        PickerText(in paint, left, right, centerY, dataCenter.Name, WorldCountLabel(dataCenter), selected);
        if (separator)
        {
            PickerHairline(in paint, left, max.X - padX, max.Y);
        }

        if (paint.Interactive && UiInteract.Click(chevronMin, chevronMax, overChevron))
        {
            return PickerAction.Expand;
        }

        return paint.Interactive && UiInteract.Click(origin, max, hovered) ? PickerAction.Pick : PickerAction.None;
    }

    private static void PickerHover(in PickerInk paint, Vector2 min, Vector2 max, bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        var inset = new Vector2(Metrics.Space.Sm * paint.Scale, 0f);
        Squircle.Fill(paint.DrawList, min + inset, max - inset, Metrics.Radius.Md * paint.Scale,
            ImGui.GetColorU32(paint.Ink with { W = paint.Ink.W * PickerHoverAlpha }));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
    }

    private static void PickerText(in PickerInk paint, float left, float right, float centerY, string title,
        string subtitle, bool selected)
    {
        var width = MathF.Max(1f, right - left);
        var titleStyle = selected ? TextStyles.BodyEmphasized : TextStyles.Body;
        var titleHeight = Typography.LineHeight(titleStyle);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(paint.DrawList, new Vector2(left, top), Typography.FitText(title, width, titleStyle),
            selected ? paint.Accent : paint.Ink, titleStyle);
        if (subtitle.Length > 0)
        {
            Typography.Draw(paint.DrawList, new Vector2(left, top + titleHeight),
                Typography.FitText(subtitle, width, TextStyles.Footnote), paint.Muted, TextStyles.Footnote);
        }
    }

    private static void PickerHairline(in PickerInk paint, float left, float right, float y) =>
        FeedCell.Hairline(paint.DrawList, left, right, y, paint.Ink with { W = paint.Ink.W * PickerHairlineAlpha });

    private string WorldCountLabel(MarketDataCenter dataCenter)
    {
        SyncScopeLabels();
        if (worldCountLabels.TryGetValue(dataCenter.Name, out var label))
        {
            return label;
        }

        label = Loc.T(L.Venues.WorldCount, dataCenter.Worlds.Length.ToString(Loc.Culture));
        worldCountLabels[dataCenter.Name] = label;
        return label;
    }

    private string AllOfLabel(MarketRegion region)
    {
        SyncScopeLabels();
        if (allOfLabels.TryGetValue(region.Id, out var label))
        {
            return label;
        }

        label = Loc.T(L.Venues.AllOfRegion, Loc.T(region.Label));
        allOfLabels[region.Id] = label;
        return label;
    }

    private void SyncScopeLabels()
    {
        if (ReferenceEquals(scopeLabelCulture, Loc.Culture))
        {
            return;
        }

        scopeLabelCulture = Loc.Culture;
        worldCountLabels.Clear();
        allOfLabels.Clear();
    }
}
