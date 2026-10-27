using Aetherphone.Core.Game;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Video;

internal sealed class ScreenController : IDisposable
{
    private readonly Func<bool> hideNameplates;
    private const float NameplateHideRadius = 3.0f;
    private bool hiding;

    internal VideoEngine Engine { get; }

    public ScreenController(Func<bool> hideNameplates)
    {
        this.hideNameplates = hideNameplates;
        Engine = new VideoEngine();
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var hide = hideNameplates() && Engine.IsActive && Engine.ScreenVisible;
        if (hide == hiding)
        {
            return;
        }

        hiding = hide;
        Plugin.NamePlateGui.RequestRedraw();
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!hiding)
        {
            return;
        }

        for (var index = 0; index < handlers.Count; index++)
        {
            var handler = handlers[index];
            var gameObject = handler.GameObject;
            if (gameObject is null)
            {
                continue;
            }

            if (Vector3.Distance(gameObject.Position, Engine.ScreenPosition) <= NameplateHideRadius)
            {
                NamePlateStripper.Strip(handler);
                handler.VisibilityFlags = 0;
            }
        }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        if (hiding)
        {
            Plugin.NamePlateGui.RequestRedraw();
        }

        Engine.Dispose();
    }
}
