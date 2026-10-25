using Aetherphone.Core;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private readonly ChatRolls chatRolls;
    private readonly Rolls.RollHostSection rollHost = new();
    private readonly Rolls.RollSetupPage rollSetup;
    private readonly Rolls.RollGamePage rollGame;

    private float DrawRollGames(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var bottom = rollHost.Draw(drawList, ui, origin, width, chatRolls, !router.IsTransitioning, scale,
            out var tap, out var game);
        switch (tap)
        {
            case Rolls.RollHostTap.Resume:
                routes.Push(new CasinoRoute(CasinoScreen.RollGame));
                break;
            case Rolls.RollHostTap.Game when chatRolls.Table.IsOpen && chatRolls.Table.Kind == game:
                routes.Push(new CasinoRoute(CasinoScreen.RollGame));
                break;
            case Rolls.RollHostTap.Game:
                rollSetup.Enter(game);
                routes.Push(new CasinoRoute(CasinoScreen.RollSetup));
                break;
        }

        return bottom;
    }

    private void DrawRollSetup(Rect body)
    {
        if (rollSetup.Draw(body, ui))
        {
            router.Replace(new CasinoRoute(CasinoScreen.RollGame));
        }
    }

    private void DrawRollGame(Rect body)
    {
        if (rollGame.Draw(body, ui))
        {
            PopRoute();
        }
    }

    private string RollTitle(CasinoScreen screen)
    {
        var kind = screen == CasinoScreen.RollSetup || !chatRolls.Table.IsOpen ? rollSetup.Kind : chatRolls.Table.Kind;
        return Loc.T(Rolls.RollGames.Of(kind).Title);
    }
}
