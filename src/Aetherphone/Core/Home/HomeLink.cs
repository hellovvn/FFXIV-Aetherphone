using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Home;

internal sealed class HomeLink
{
    public HomeLink(IPhoneApp app, IHomeLinkTarget target, string argument)
    {
        App = app;
        Target = target;
        Argument = argument;
        Title = target.LinkTitle(argument);
    }

    public IPhoneApp App { get; }

    public IHomeLinkTarget Target { get; }

    public string Argument { get; }

    public LocString Title { get; }

    public string AppId => App.Id;

    public bool Matches(string appId, string argument) =>
        string.Equals(App.Id, appId, StringComparison.Ordinal) &&
        string.Equals(Argument, argument, StringComparison.Ordinal);

    public void Open(INavigator navigation, Rect origin)
    {
        Target.OpenLink(Argument);
        navigation.OpenAppFrom(App, origin, LaunchOrigin.Surface);
    }
}

internal enum HomeLinkAdd : byte
{
    Added,
    AlreadyThere,
    Unavailable,
}
