using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp : IHomeLinkTarget
{
    private const string HomeChipTooltipId = "games.homeChip";

    private IMiniGame? routedGame;
    private string gameRoute = string.Empty;

    public bool HasLink(string argument) => LinkedGame(argument) is not null;

    public LocString LinkTitle(string argument) => LinkedGame(argument)?.Spec.Title ?? L.Apps.Games;

    public void DrawLinkIcon(string argument, Vector2 min, Vector2 max, bool elevated)
    {
        if (LinkedGame(argument) is not { } game)
        {
            return;
        }

        GameIconArt.Draw(ImGui.GetWindowDrawList(), game.Id, game.Accent, min, max, elevated);
    }

    public void OpenLink(string argument) => OpenTab(argument);

    private IMiniGame? LinkedGame(string argument) =>
        argument.StartsWith(PlayRoutePrefix, StringComparison.Ordinal)
            ? FindGame(argument.AsSpan(PlayRoutePrefix.Length))
            : null;

    private float DrawHomeChip(ImDrawListPtr drawList, Rect full, PhoneTheme theme, float radius, float scale,
        float rightEdge)
    {
        if (currentGame is not { } game || guide.IsOpen || levelSelect.IsOpen)
        {
            return rightEdge;
        }

        var route = RouteOf(game);
        var onHome = homeLinks.Has(Id, route);
        var center = StageLayout.PauseChipCenter(full, scale);
        if (chrome.HomeChip(drawList, center, radius, theme, scale, onHome))
        {
            AddToHome(game, route);
        }

        var hit = new Vector2(MathF.Max(radius, Metrics.Size.TapTarget * 0.5f * scale));
        HoverTooltip.Show(HomeChipTooltipId, new Rect(center - hit, center + hit),
            Loc.T(onHome ? L.HomeLinks.OnHome : L.HomeLinks.AddToHome), HoverLabelSide.Left);
        return center.X - radius - StageLayout.CoinChipGap * scale;
    }

    private string RouteOf(IMiniGame game)
    {
        if (!ReferenceEquals(routedGame, game))
        {
            routedGame = game;
            gameRoute = PlayRoute(game.Id);
        }

        return gameRoute;
    }

    private void AddToHome(IMiniGame game, string route)
    {
        var added = homeLinks.Add(Id, route);
        if (added == HomeLinkAdd.Unavailable)
        {
            return;
        }

        ShellToast.Show(Loc.T(added == HomeLinkAdd.Added ? L.HomeLinks.Added : L.HomeLinks.AlreadyThere, game.Title));
    }
}
