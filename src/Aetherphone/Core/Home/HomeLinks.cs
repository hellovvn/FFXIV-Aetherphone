namespace Aetherphone.Core.Home;

internal sealed class HomeLinks
{
    private HomeLayoutService? layout;

    public void Bind(HomeLayoutService service) => layout = service;

    public bool Has(string appId, string argument) => layout?.HasLink(appId, argument) ?? false;

    public HomeLinkAdd Add(string appId, string argument) =>
        layout?.AddLink(appId, argument) ?? HomeLinkAdd.Unavailable;
}
