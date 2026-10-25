using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class DisplayPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.PhoneSize),
        new(L.Settings.TextSize),
        new(L.Settings.LiquidGlass),
        new(L.Settings.RealGlass, L.Settings.LiquidGlass),
        new(L.ControlCenter.LockPosition),
    };

    public static readonly Vector4 PageTint = new(0.13f, 0.56f, 0.96f, 1f);

    public string Title => Loc.T(L.Settings.Display);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Sun;
    public Vector4 Tint => PageTint;
    public string? GuideAnchor => "settings.row.display";
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private const float CardGap = Metrics.Space.Xl;
    private const float PercentScale = 100f;
    private string widthReadout = string.Empty;
    private int widthReadoutValue = -1;
    private LanguageInfo? widthReadoutLanguage;
    private string zoomReadout = string.Empty;
    private int zoomReadoutPercent = -1;
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly LiquidGlassPage liquidGlass;

    public DisplayPage(Configuration configuration, ISettingsNavigator navigator, WallpaperLibrary wallpapers)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        liquidGlass = new LiquidGlassPage(configuration, wallpapers);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawPhoneSizeCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawTextSizeCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLiquidGlassCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLockPositionCard(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawPhoneSizeCard(PhoneTheme theme)
    {
        var smallest = PhoneSizeCatalog.MinimumWidth;
        var largest = MathF.Max(PhoneBounds.ClampWidth(PhoneSizeCatalog.MaximumWidth), smallest + 1f);
        var span = largest - smallest;
        var effective = PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var card = GroupCard.Begin(theme, 2);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.PhoneSize), WidthReadout(effective), theme);
        var slider = Slider.Draw("settings.phoneSize", card.NextRow(), (effective - smallest) / span, theme, 0f, 0f);
        card.End();
        var width = PhoneSizeCatalog.Snap(PhoneBounds.ClampWidth(smallest + slider.Value * span));
        if ((slider.Dragging || slider.Released) && MathF.Abs(width - configuration.PhoneWidth) > 0.01f)
        {
            configuration.PhoneWidth = width;
        }

        if (slider.Released)
        {
            configuration.Save();
        }
    }

    private void DrawTextSizeCard(PhoneTheme theme)
    {
        const float smallest = TextZoomCatalog.MinimumZoom;
        const float span = TextZoomCatalog.MaximumZoom - TextZoomCatalog.MinimumZoom;
        var effective = TextZoomCatalog.Clamp(configuration.TextZoom);
        var card = GroupCard.Begin(theme, 2);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.TextSize), ZoomReadout(effective), theme);
        var slider = Slider.Draw("settings.textZoom", card.NextRow(), (effective - smallest) / span, theme, 0f, 0f);
        card.End();
        var zoom = TextZoomCatalog.Snap(TextZoomCatalog.Clamp(smallest + slider.Value * span));
        if ((slider.Dragging || slider.Released) && MathF.Abs(zoom - configuration.TextZoom) > 0.001f)
        {
            configuration.TextZoom = zoom;
            Plugin.Fonts.SetZoom(zoom);
        }

        if (slider.Released)
        {
            configuration.Save();
        }
    }

    private void DrawLiquidGlassCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var opened = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.LiquidGlass), string.Empty, theme);
        card.End();
        if (opened)
        {
            navigator.Open(liquidGlass);
        }
    }

    private void DrawLockPositionCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var lockPosition = SettingsRow.Bool(card.NextRow(), Loc.T(L.ControlCenter.LockPosition),
            configuration.LockPosition, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.LockPositionHint), theme);
        if (lockPosition == configuration.LockPosition)
        {
            return;
        }

        configuration.LockPosition = lockPosition;
        configuration.Save();
    }

    private string WidthReadout(float width)
    {
        var rounded = (int)MathF.Round(width);
        if (rounded == widthReadoutValue && ReferenceEquals(widthReadoutLanguage, Loc.Current))
        {
            return widthReadout;
        }

        widthReadoutValue = rounded;
        widthReadoutLanguage = Loc.Current;
        widthReadout = Loc.T(L.Settings.PhoneWidthReadout, rounded);
        return widthReadout;
    }

    private string ZoomReadout(float zoom)
    {
        var percent = (int)MathF.Round(zoom * PercentScale);
        if (percent == zoomReadoutPercent)
        {
            return zoomReadout;
        }

        zoomReadoutPercent = percent;
        zoomReadout = PercentLabel(percent);
        return zoomReadout;
    }

    private static string PercentLabel(int percent) =>
        string.Concat(percent.ToString(CultureInfo.InvariantCulture), "%");
}
