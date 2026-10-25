using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal delegate void GuideArt(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale);

internal readonly record struct GuidePage(LocString Title, LocString Body, GuideArt Art);

internal sealed class GameGuide
{
    public const int MinPages = 2;
    public const int MaxPages = 5;

    private readonly GuidePage[] pages;

    public GameGuide(params GuidePage[] pages)
    {
        this.pages = pages;
    }

    public int Count => pages.Length;

    public ref readonly GuidePage Page(int index) => ref pages[index];
}
