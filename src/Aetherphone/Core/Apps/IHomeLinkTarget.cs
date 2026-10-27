using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Apps;

internal interface IHomeLinkTarget
{
    bool HasLink(string argument);

    LocString LinkTitle(string argument);

    void DrawLinkIcon(string argument, Vector2 min, Vector2 max, bool elevated);

    void OpenLink(string argument);
}
