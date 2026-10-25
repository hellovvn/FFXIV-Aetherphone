using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LiquidGlassPage : ISettingsPage
{
    private const float CardGap = Metrics.Space.Xl;
    private const float SliderRowHeight = 38f;
    private const float CaptionBottom = Metrics.Space.Md;
    private const float CaptionWidthFraction = 0.45f;
    private const float AmountEpsilon = 0.001f;
    private const int TuningRows = 8;
    private const float TuningLabelFraction = 0.42f;
    private readonly Configuration configuration;
    private readonly WallpaperLibrary wallpapers;
    private float cardWidth;

    public LiquidGlassPage(Configuration configuration, WallpaperLibrary wallpapers)
    {
        this.configuration = configuration;
        this.wallpapers = wallpapers;
    }

    public string Title => Loc.T(L.Settings.LiquidGlass);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Tint;
    public Vector4 Tint => DisplayPage.PageTint;

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var scrollTop = DrawPreview(body, theme, scale) + Metrics.Space.Sm * scale;
        using (AppSurface.Begin(new Rect(new Vector2(body.Min.X, scrollTop), body.Max)))
        {
            cardWidth = ImGui.GetContentRegionAvail().X;
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawRealGlassCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawAmountCard(theme, scale);
            if (configuration.RealGlass)
            {
                ImGui.Dummy(new Vector2(0f, CardGap * scale));
                DrawTuningCard(theme, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private float DrawPreview(Rect body, PhoneTheme theme, float scale)
    {
        var min = new Vector2(body.Min.X, body.Min.Y + Metrics.Space.Md * scale);
        var width = cardWidth > 0f ? MathF.Min(cardWidth, body.Width) : body.Width;
        var picture = new Rect(min, min + new Vector2(width, width * LiquidGlassPreview.HeightFraction));
        LiquidGlassPreview.Draw(ImGui.GetWindowDrawList(), picture, theme, wallpapers, scale);
        return picture.Max.Y;
    }

    private void DrawAmountCard(PhoneTheme theme, float scale)
    {
        var cardHeight = SliderRowHeight + Typography.LineHeight(TextStyles.Footnote) / scale + CaptionBottom;
        var card = GroupCard.Begin(theme, cardHeight);
        var row = card.NextRow(cardHeight);
        var sliderRow = new Rect(row.Min, new Vector2(row.Max.X, row.Min.Y + SliderRowHeight * scale));
        var slider = Slider.Draw("settings.glassAmount", sliderRow, configuration.GlassAmount, theme, 0f, 0f);
        DrawCaptions(sliderRow, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.LiquidGlassHint), theme);
        if ((slider.Dragging || slider.Released) && MathF.Abs(slider.Value - configuration.GlassAmount) > AmountEpsilon)
        {
            configuration.GlassAmount = slider.Value;
        }

        if (slider.Released)
        {
            configuration.Save();
        }
    }

    private static void DrawCaptions(Rect sliderRow, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = sliderRow.Width * CaptionWidthFraction;
        var clear = Typography.FitText(Loc.T(L.Settings.LiquidGlassClear), maxWidth, TextStyles.Footnote);
        var tinted = Typography.FitText(Loc.T(L.Settings.LiquidGlassTinted), maxWidth, TextStyles.Footnote);
        var tintedWidth = Typography.Measure(tinted, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(sliderRow.Min.X, sliderRow.Max.Y), clear, theme.TextMuted,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(sliderRow.Max.X - tintedWidth, sliderRow.Max.Y), tinted,
            theme.TextMuted, TextStyles.Footnote);
    }

    private void DrawTuningCard(PhoneTheme theme, float scale)
    {
        var tuning = configuration.GlassTuning;
        SettingsSection.Header(Loc.T(L.Settings.GlassFineTuning), theme);
        var card = GroupCard.Begin(theme, TuningRows);
        var cursorLight = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.GlassCursorLight), tuning.CursorLight,
            theme);
        var changed = cursorLight != tuning.CursorLight;
        tuning.CursorLight = cursorLight;
        var released = false;
        tuning.Highlight = TuningSlider(card.NextRow(), "settings.glassHighlight", Loc.T(L.Settings.GlassHighlight),
            tuning.Highlight, theme, scale, ref released);
        tuning.Edges = TuningSlider(card.NextRow(), "settings.glassEdges", Loc.T(L.Settings.GlassEdges), tuning.Edges,
            theme, scale, ref released);
        tuning.Brightness = TuningSlider(card.NextRow(), "settings.glassBrightness",
            Loc.T(L.Settings.GlassBrightness), tuning.Brightness, theme, scale, ref released);
        tuning.Refraction = TuningSlider(card.NextRow(), "settings.glassRefraction",
            Loc.T(L.Settings.GlassRefraction), tuning.Refraction, theme, scale, ref released);
        tuning.ColorSplit = TuningSlider(card.NextRow(), "settings.glassColorSplit",
            Loc.T(L.Settings.GlassColorSplit), tuning.ColorSplit, theme, scale, ref released);
        tuning.Shadow = TuningSlider(card.NextRow(), "settings.glassShadow", Loc.T(L.Settings.GlassShadow),
            tuning.Shadow, theme, scale, ref released);
        var reset = SettingsRow.Action(card.NextRow(), Loc.T(L.Settings.GlassResetTuning), theme.Accent, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.GlassFineTuningHint), theme);
        if (reset)
        {
            configuration.GlassTuning = new GlassTuning();
            changed = true;
        }

        if (changed || released)
        {
            configuration.Save();
        }
    }

    private static float TuningSlider(Rect row, string id, string label, float value, PhoneTheme theme, float scale,
        ref bool released)
    {
        var labelWidth = row.Width * TuningLabelFraction;
        var fitted = Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized);
        var labelHeight = Typography.Measure(fitted, TextStyles.BodyEmphasized).Y;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f), fitted,
            theme.TextStrong, TextStyles.BodyEmphasized);
        var slider = Slider.Draw(id, row, value, theme, labelWidth + Metrics.Space.Md * scale, 0f);
        released |= slider.Released;
        return slider.Dragging || slider.Released ? slider.Value : value;
    }

    private void DrawRealGlassCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var realGlass = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.RealGlass), configuration.RealGlass, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.RealGlassHint), theme);
        if (realGlass == configuration.RealGlass)
        {
            return;
        }

        configuration.RealGlass = realGlass;
        configuration.Save();
    }
}
