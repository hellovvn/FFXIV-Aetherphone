using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp : IHomeLinkTarget
{
    public bool HasLink(string argument) => CasinoHomeLinks.IsLinkable(argument);

    public LocString LinkTitle(string argument) => CasinoGameNames.Of(argument);

    public void DrawLinkIcon(string argument, Vector2 min, Vector2 max, bool elevated) =>
        CasinoArt.HomeTile(ImGui.GetWindowDrawList(), argument, min, max, elevated);

    public void OpenLink(string argument)
    {
        if (casino.State is not null && !CasinoGameGate.IsOpen(casino.Features, argument))
        {
            return;
        }

        launcher.RequestGame(argument);
    }

    private void AddToHome(string gameId)
    {
        var added = homeLinks.Add(Id, gameId);
        if (added == HomeLinkAdd.Unavailable)
        {
            return;
        }

        ShellToast.Show(Loc.T(added == HomeLinkAdd.Added ? L.HomeLinks.Added : L.HomeLinks.AlreadyThere,
            Loc.T(CasinoGameNames.Of(gameId))));
    }
}
