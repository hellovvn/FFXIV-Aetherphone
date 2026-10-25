using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Shell.Spotlight;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class HomeScreen
{
    private const float FolderLabelAlpha = 0.35f;

    private readonly HomeLayoutService layout;
    private readonly Pager pager = new();
    private readonly FolderOverlay folder;
    private readonly WidgetContextMenu widgetMenu;
    private readonly WidgetGallery gallery;
    private readonly TilePoseCache poses = new();
    private readonly HomeInteractionController interaction;
    private readonly HomeGridRenderer renderer;
    private readonly HomeChrome chrome;
    private readonly SpotlightOverlay spotlight;
    private readonly Configuration configuration;
    private readonly HomeLaunchTrace launches = new();
    private float sidePaddingUnits = PhoneTheme.Default.SidePadding;

    public HomeScreen(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets, WidgetActions widgetActions,
        ShortcutStore shortcuts, ShortcutRunner runner, Configuration configuration, ConfirmService confirm,
        SpotlightIndex spotlightIndex)
    {
        this.configuration = configuration;
        var widgetHost = new WidgetHost(widgetActions, configuration);
        layout = new HomeLayoutService(apps, widgets, shortcuts, configuration);
        folder = new FolderOverlay(layout, shortcuts, runner, configuration, launches);
        widgetMenu = new WidgetContextMenu(layout, widgetHost);
        gallery = new WidgetGallery(layout, widgets, widgetHost);
        spotlight = new SpotlightOverlay(spotlightIndex, configuration);
        interaction = new HomeInteractionController(layout, pager, folder, widgetMenu, gallery, spotlight, poses,
            runner, widgetHost, launches);
        renderer = new HomeGridRenderer(layout, pager, poses, interaction, shortcuts, confirm, configuration,
            widgetHost);
        chrome = new HomeChrome(pager, interaction, spotlight);
    }

    public bool Editing => interaction.Editing;

    public float EditPresence => chrome.EditPresence;

    public HomeLayoutService Layout => layout;

    public void Draw(Rect screen, Rect content, PhoneTheme theme, INavigator navigation, in HomeMotion motion)
    {
        layout.EnsureCurrent();
        var delta = FrameClock.Delta;
        interaction.Advance(delta);
        sidePaddingUnits = theme.SidePadding;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, UiScale.Current,
            motion, sidePaddingUnits);
        pager.Step(delta, interaction.DisplayPageCount());
        var chromeAlpha = 1f - motion.Recession;
        if (motion.Interactive)
        {
            interaction.HandleInput(screen, content, metrics, navigation, theme, delta);
        }
        else
        {
            interaction.Suspend();
        }

        interaction.AdvanceTap(delta);
        interaction.UpdatePointer(content, metrics, motion, delta);
        if (chromeAlpha > 0.01f)
        {
            var labelAlpha = Easing.Lerp(1f, FolderLabelAlpha, folder.Presence);
            var tileDrawList = ImGui.GetWindowDrawList();
            var tileVertexStart = tileDrawList.VtxBuffer.Size;
            renderer.DrawPages(metrics, theme, delta, labelAlpha, configuration.ShowAppNames, motion);
            renderer.DrawDock(metrics, theme, delta, 1f, motion);
            chrome.DrawPageControls(metrics, theme, 1f, motion.Interactive);
            LayerCompositor.Fade(tileDrawList, tileVertexStart, chromeAlpha);
        }

        chrome.DrawEditChrome(screen, theme, interaction.RemoveBadgesLive(motion));

        folder.Draw(screen, content, metrics, theme, navigation, interaction.Editing, pager.Page, delta);
        var ghostDrawList = ImGui.GetWindowDrawList();
        ghostDrawList.PushClipRect(screen.Min, screen.Max, true);
        renderer.DrawDragGhost(metrics, theme, delta);
        ghostDrawList.PopClipRect();
        DrawWidgetMenu(content, metrics, theme, delta);
        widgetMenu.DrawSheets(screen, theme, delta);
        gallery.Draw(screen, theme, delta, metrics);
        if (gallery.TryTakePlacement(out var placed, out var placedCenter, out var placedScale))
        {
            interaction.SettleFrom(placed, placedCenter, placedScale, metrics);
        }

        ghostDrawList.PushClipRect(screen.Min, screen.Max, true);
        renderer.DrawSettleGhost(metrics, theme, delta);
        ghostDrawList.PopClipRect();
        spotlight.Draw(screen, content, theme, navigation, delta, metrics.Scale);
        if (!motion.Interactive)
        {
            spotlight.Close();
        }
    }

    public void ForgetLaunch(string _) => launches.Forget();

    public void PrepareReveal(string appId)
    {
        gallery.CloseImmediate();
        spotlight.CloseImmediate();
        widgetMenu.CloseImmediate();
        interaction.ResetForReveal();
        if (FindRevealTile(appId, out var page, out _))
        {
            pager.SnapTo(page, layout.PageCount);
        }
    }

    public Rect? RevealRect(string appId, Rect content, out LaunchOrigin kind)
    {
        kind = LaunchOrigin.Icon;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, UiScale.Current,
            HomeMotion.Rest, sidePaddingUnits);
        if (FindLaunchedLink(appId, out var linkPage, out var linkIndex))
        {
            return PageTileRect(metrics, linkPage, linkIndex, out kind);
        }

        var dock = layout.Dock;
        for (var index = 0; index < dock.Count; index++)
        {
            if (dock[index].App!.Id == appId)
            {
                return metrics.DockSlotRect(dock.Count, index);
            }
        }

        return FindRevealTile(appId, out var page, out var tileIndex)
            ? PageTileRect(metrics, page, tileIndex, out kind)
            : null;
    }

    private Rect? PageTileRect(in HomeMetrics metrics, int page, int index, out LaunchOrigin kind)
    {
        kind = LaunchOrigin.Icon;
        var tiles = layout.Page(page);
        var cells = layout.Placements(page);
        if (index >= tiles.Count || index >= cells.Count)
        {
            return null;
        }

        var tile = tiles[index];
        kind = tile.App is not null ? LaunchOrigin.Icon : LaunchOrigin.Surface;
        return metrics.TileRect(page, pager.Value, cells[index], tile);
    }

    private bool FindRevealTile(string appId, out int page, out int index)
    {
        if (FindLaunchedLink(appId, out page, out index))
        {
            return true;
        }

        page = Math.Clamp((int)MathF.Round(pager.Value), 0, Math.Max(0, layout.PageCount - 1));
        index = page < layout.PageCount ? IndexOnPage(page, appId) : -1;
        if (index >= 0)
        {
            return true;
        }

        for (page = 0; page < layout.PageCount; page++)
        {
            index = IndexOnPage(page, appId);
            if (index >= 0)
            {
                return true;
            }
        }

        page = -1;
        index = -1;
        return false;
    }

    private bool FindLaunchedLink(string appId, out int page, out int index)
    {
        if (launches.Launched is { } launched && string.Equals(launched.AppId, appId, StringComparison.Ordinal))
        {
            for (page = 0; page < layout.PageCount; page++)
            {
                var tiles = layout.Page(page);
                for (index = 0; index < tiles.Count; index++)
                {
                    if (HoldsLink(tiles[index], launched))
                    {
                        return true;
                    }
                }
            }
        }

        page = -1;
        index = -1;
        return false;
    }

    private static bool HoldsLink(HomeTile tile, HomeLink launched)
    {
        if (tile.Link is { } link)
        {
            return link.Matches(launched.AppId, launched.Argument);
        }

        for (var index = 0; index < tile.Members.Count; index++)
        {
            if (tile.Members[index].Link is { } member && member.Matches(launched.AppId, launched.Argument))
            {
                return true;
            }
        }

        return false;
    }

    private int IndexOnPage(int page, string appId)
    {
        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (TileTargets(tiles[index], appId))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TileTargets(HomeTile tile, string appId)
    {
        if (tile.App is not null)
        {
            return tile.App.Id == appId;
        }

        if (tile.IsWidget)
        {
            return tile.Widget!.AppId == appId;
        }

        if (tile.Link is { } link)
        {
            return link.AppId == appId;
        }

        for (var index = 0; index < tile.Members.Count; index++)
        {
            var member = tile.Members[index];
            if (member.App?.Id == appId || member.Link?.AppId == appId)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawWidgetMenu(Rect content, in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (widgetMenu.Tile is not { } tile)
        {
            return;
        }

        var anchor = interaction.CommittedRect(metrics, tile);
        if (anchor is not { } rect)
        {
            widgetMenu.CloseImmediate();
            return;
        }

        widgetMenu.Draw(content, rect, theme, delta, metrics.Scale);
    }
}
