using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeLaunchTrace
{
    private HomeLink? launched;

    public HomeLink? Launched => launched;

    public void Open(HomeLink link, INavigator navigation, Rect origin)
    {
        link.Open(navigation, origin);
        launched = link;
    }

    public void Forget() => launched = null;
}
