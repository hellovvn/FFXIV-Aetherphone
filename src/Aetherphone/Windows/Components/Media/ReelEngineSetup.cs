using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

// The prompt to download MogCast's video engine before a post video can play, shared by the Aethergram Reels tab
// and Chirper's video player. Each app words the title and body for what it plays.
internal sealed class ReelEngineSetup : IDisposable
{
    private const float TileSize = 64f;
    private const float CardHeight = 74f;

    private readonly StoreWork work;
    private bool checkedSizes;
    private long installLabelBytes = -1;
    private string installLabel = string.Empty;

    public ReelEngineSetup(string workName)
    {
        work = new StoreWork(workName);
    }

    // Returns true when the player chose Not now.
    public bool Draw(Rect area, AppSkin ui, PhoneTheme theme, MediaDependencies dependencies, LocString title,
        LocString body)
    {
        var scale = UiScale.Current;
        if (!checkedSizes)
        {
            checkedSizes = true;
            work.Run("check reel components", async token =>
                await dependencies.CheckReelSizesAsync(token).ConfigureAwait(false));
        }

        var margin = Metrics.Space.Xl * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, area.Min.Y + margin),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var drawList = ImGui.GetWindowDrawList();
        var tileSize = TileSize * scale;
        var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, content.Min.Y + Metrics.Space.Xl * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
            FontAwesomeIcon.Film, AccentRing.Ink, tileSize * 0.5f);

        var titleY = tileMin.Y + tileSize + Metrics.Space.Lg * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, titleY + titleHeight * 0.5f), Loc.T(title),
            ui.TitleInk, TextStyles.Title2);
        var bodyY = titleY + titleHeight + Metrics.Space.Sm * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY), Loc.T(body), ui.MutedInk,
            TextStyles.Subheadline, content.Width);
        var cardTop = bodyY + bodyHeight + Metrics.Space.Xl * scale;
        DependencySetup.Card(ui, theme, new Rect(new Vector2(content.Min.X, cardTop),
                new Vector2(content.Max.X, cardTop + CardHeight * scale)), dependencies.VideoLibrary,
            L.AetherStream.SetupVideoEngine, L.AetherStream.SetupVideoEngineDetail, scale);

        var buttonHeight = Button.LargeHeight * scale;
        var buttonTop = content.Max.Y - buttonHeight - Typography.LineHeight(TextStyles.Subheadline)
            - Metrics.Space.Lg * scale;
        return DrawAction(new Rect(new Vector2(content.Min.X, buttonTop),
            new Vector2(content.Max.X, buttonTop + buttonHeight)), ui, dependencies, scale);
    }

    public void Install(MediaDependencies dependencies)
    {
        if (DependencySetup.IsBusy(dependencies.VideoLibrary.Snapshot()))
        {
            return;
        }

        work.Run("install reel components",
            async token => await dependencies.EnsureReelsReadyAsync(token).ConfigureAwait(false));
    }

    public void Dispose()
    {
        work.Dispose();
    }

    private bool DrawAction(Rect button, AppSkin ui, MediaDependencies dependencies, float scale)
    {
        var library = dependencies.VideoLibrary.Snapshot();
        var label = DependencySetup.IsInstalling(library)
            ? Loc.T(L.AetherStream.SetupInstalling)
            : DependencySetup.IsBusy(library)
                ? Loc.T(L.AetherStream.SetupChecking)
                : library.State == DependencyState.Failed
                    ? Loc.T(L.AetherStream.SetupRetry)
                    : InstallLabel(dependencies.PendingReelBytes);
        if (Button.Draw(button, label, ui.Ink, ButtonStyle.Prominent, enabled: !DependencySetup.IsBusy(library)))
        {
            Install(dependencies);
        }

        var linkTop = button.Max.Y + Metrics.Space.Sm * scale;
        var linkRect = new Rect(new Vector2(button.Min.X, linkTop),
            new Vector2(button.Max.X, linkTop + Typography.LineHeight(TextStyles.Subheadline)));
        var hovered = UiInteract.Hover(linkRect.Min, linkRect.Max);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), linkRect.Center, Loc.T(L.AetherStream.SetupNotNow),
            hovered ? ui.TitleInk : ui.MutedInk, TextStyles.Subheadline);
        return UiInteract.Click(linkRect.Min, linkRect.Max, hovered);
    }

    private string InstallLabel(long pendingBytes)
    {
        if (pendingBytes <= 0)
        {
            return Loc.T(L.AetherStream.SetupInstall);
        }

        if (pendingBytes != installLabelBytes)
        {
            installLabelBytes = pendingBytes;
            installLabel = string.Format(Loc.T(L.AetherStream.SetupInstallSized),
                DependencySetup.FormatMegabytes(pendingBytes));
        }

        return installLabel;
    }
}
