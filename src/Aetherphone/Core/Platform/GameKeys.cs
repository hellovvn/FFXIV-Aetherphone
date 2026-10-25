using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Core.Platform;

internal static class GameKeys
{
    private const short PhysicallyDown = unchecked((short)0x8000);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public static bool HeldInGame(VirtualKey key) =>
        GameWindowFocus.IsFocused && (GetAsyncKeyState((int)key) & PhysicallyDown) != 0;

    public static void Swallow(VirtualKey key)
    {
        var keyState = Plugin.KeyState;
        if (keyState.IsVirtualKeyValid(key))
        {
            keyState[key] = false;
        }
    }
}
