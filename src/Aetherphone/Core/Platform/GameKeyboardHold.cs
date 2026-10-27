using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Core.Platform;

internal static class GameKeyboardHold
{
    private const long ReleaseGraceMilliseconds = 150;
    private const long MaximumHoldMilliseconds = 5000;

    private static VirtualKey heldKey;
    private static long holdStartedTick;
    private static long releasedAtTick;
    private static bool hooked;

    public static void ThisFrame() => ImGui.GetIO().WantTextInput = true;

    public static void UntilReleased(VirtualKey key)
    {
        heldKey = key;
        holdStartedTick = Environment.TickCount64;
        releasedAtTick = 0;
        ThisFrame();
        if (hooked)
        {
            return;
        }

        hooked = true;
        Plugin.PluginInterface.UiBuilder.Draw += HoldWhileDown;
    }

    private static void HoldWhileDown()
    {
        var now = Environment.TickCount64;
        if (now - holdStartedTick > MaximumHoldMilliseconds)
        {
            Unhook();
            return;
        }

        if (GameKeys.HeldInGame(heldKey))
        {
            releasedAtTick = 0;
            ThisFrame();
            return;
        }

        if (releasedAtTick == 0)
        {
            releasedAtTick = now;
        }

        if (now - releasedAtTick > ReleaseGraceMilliseconds)
        {
            Unhook();
            return;
        }

        ThisFrame();
    }

    private static void Unhook()
    {
        hooked = false;
        Plugin.PluginInterface.UiBuilder.Draw -= HoldWhileDown;
    }
}
