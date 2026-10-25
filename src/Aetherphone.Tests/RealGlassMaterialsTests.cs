using System.Numerics;
using Aetherphone.Core.Shell;
using Aetherphone.Windows.Components;
using Glasswork.Glass;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RealGlassMaterialsTests
{
    private const float NeutralAmount = 0.5f;
    private const float Radius = 12f;
    private const float Tolerance = 1e-4f;
    private static readonly Vector2 Bar = new(320f, 56f);
    private static readonly Vector4 Accent = new(0.2f, 0.5f, 0.9f, 0.7f);
    private static readonly GlassTuning Neutral = new();
    private const float PanelRadius = 48f;
    private static readonly Vector2 PanelMin = new(100f, 80f);
    private static readonly Vector2 PanelMax = new(500f, 920f);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToneSelectsTheAppearance(bool light)
    {
        var tone = light ? GlassTone.Light : GlassTone.Dark;
        var material = Build(RealGlassKind.Liquid, tone, NeutralAmount);

        Assert.Equal(light, material.LightAppearance);
        Assert.Equal(GlassMaterial.ForAppearance(light).Lift, material.Lift);
        Assert.Equal(GlassMaterial.ForAppearance(light).Specular, material.Specular);
    }

    [Fact]
    public void LiquidAtTheNeutralAmountKeepsThePreset()
    {
        var preset = GlassMaterial.ForAppearance(false);
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, NeutralAmount);

        Assert.Equal(preset.Bezel, material.Bezel);
        Assert.Equal(preset.Depth, material.Depth);
        Assert.Equal(preset.Frost, material.Frost);
        Assert.Equal(preset.BlurLevels, material.BlurLevels);
        Assert.Equal(0f, material.TintAmount);
    }

    [Fact]
    public void FrostedIsFullyFrostedWithTheDeepestBlur()
    {
        var material = Build(RealGlassKind.Frosted, GlassTone.Dark, NeutralAmount);

        Assert.Equal(RealGlassMaterials.FrostedFrost, material.Frost);
        Assert.Equal(RealGlassMaterials.FrostedBlurLevels, material.BlurLevels);
        Assert.False(material.LightAppearance);
    }

    [Fact]
    public void AccentWashesThePresetWithTheOpaqueAccent()
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Accent, GlassTone.Dark, false, Accent, 1f,
            NeutralAmount, Neutral, Radius, Bar);

        Assert.Equal(Accent.X, material.Tint.R);
        Assert.Equal(Accent.Y, material.Tint.G);
        Assert.Equal(Accent.Z, material.Tint.B);
        Assert.Equal(1f, material.Tint.A);
        Assert.Equal(RealGlassMaterials.AccentTintAmount, material.TintAmount);
    }

    [Fact]
    public void AccentOnALightToneTakesTheLightPreset()
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Accent, GlassTone.Light, false, Accent, 1f,
            NeutralAmount, Neutral, Radius, Bar);

        Assert.True(material.LightAppearance);
        Assert.Equal(RealGlassMaterials.AccentTintAmount, material.TintAmount);
    }

    [Theory]
    [InlineData(0.4f, 0.4f)]
    [InlineData(3f, 1f)]
    [InlineData(-1f, 0f)]
    public void OpacityPassesThroughClamped(float opacity, float expected)
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, false, default, opacity,
            NeutralAmount, Neutral, Radius, Bar);

        Assert.Equal(expected, material.Opacity);
    }

    [Fact]
    public void AmountZeroClearsTheGlass()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, 0f);

        Assert.InRange(material.Frost, 0f, 0.05f + Tolerance);
        Assert.Equal(1, material.BlurLevels);
        Assert.Equal(0f, material.TintAmount);
    }

    [Fact]
    public void AmountOneFrostsAndTintsTheGlass()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, 1f);

        Assert.Equal(1f, material.Frost);
        Assert.Equal(4, material.BlurLevels);
        Assert.True(material.TintAmount > 0f);
    }

    [Fact]
    public void AmountOneLiftsALightMaterialInsteadOfTintingIt()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, 1f);

        Assert.True(material.Lift > GlassMaterial.RegularLight.Lift);
        Assert.Equal(0f, material.TintAmount);
    }

    [Theory]
    [InlineData(12f, 320f, 56f, 4.2f)]
    [InlineData(28f, 320f, 56f, 2f)]
    [InlineData(40f, 320f, 56f, 2f)]
    [InlineData(22f, 44f, 44f, 2f)]
    [InlineData(0f, 44f, 44f, 4.2f)]
    public void CornersStayContinuousUntilTheRadiusReachesHalfTheShortSide(float radius, float width, float height,
        float exponent)
    {
        Assert.Equal(exponent, RealGlassMaterials.CornerExponent(radius, new Vector2(width, height)));
    }

    [Fact]
    public void BuildAppliesTheCornerExponentOfTheShape()
    {
        var squircle = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, false, default, 1f,
            NeutralAmount, Neutral, Radius, Bar);
        var capsule = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, false, default, 1f,
            NeutralAmount, Neutral, Bar.Y * 0.5f, Bar);

        Assert.Equal(Glasswork.CornerCurve.ContinuousExponent, squircle.CornerExponent);
        Assert.Equal(Glasswork.CornerCurve.CircularExponent, capsule.CornerExponent);
    }

    [Fact]
    public void LengthsStayUnscaled()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, NeutralAmount);

        Assert.Equal(GlassMaterial.RegularLight.Bezel, material.Bezel);
        Assert.Equal(GlassMaterial.RegularLight.ShadowRadius, material.ShadowRadius);
        Assert.Equal(GlassMaterial.RegularLight.EdgeLineWidth, material.EdgeLineWidth);
    }

    [Fact]
    public void ShapeFillsTheRect()
    {
        var shape = RealGlassMaterials.Shape(PanelMin, PanelMax, PanelRadius);

        Assert.Equal(PanelMin, shape.Min);
        Assert.Equal(PanelMax, shape.Max);
        Assert.Equal(PanelRadius, shape.Radius);
        Assert.Equal(0f, shape.Thickness);
        Assert.False(shape.IsRing);
    }

    [Fact]
    public void LightGlassOnTheWallpaperUsesTheWhiteInkPresetWithASoftFilm()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, NeutralAmount, true);

        Assert.False(material.LightAppearance);
        Assert.Equal(RealGlassMaterials.WallpaperLightLift, material.Lift);
    }

    [Fact]
    public void DarkGlassOnTheWallpaperIsSmoked()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, NeutralAmount, true);

        Assert.False(material.LightAppearance);
        Assert.Equal(RealGlassMaterials.WallpaperSmokeAmount, material.TintAmount);
        Assert.Equal(RealGlassMaterials.WallpaperSmoke.R, material.Tint.R);
    }

    [Fact]
    public void TintedLightGlassOnTheWallpaperSmokesInsteadOfTurningMilky()
    {
        var neutral = Build(RealGlassKind.Liquid, GlassTone.Light, NeutralAmount, true);
        var tinted = Build(RealGlassKind.Liquid, GlassTone.Light, 1f, true);

        Assert.True(tinted.Lift <= neutral.Lift);
        Assert.True(tinted.TintAmount > neutral.TintAmount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TintedGlassOnTheWallpaperKeepsThePresetRim(bool light)
    {
        var tone = light ? GlassTone.Light : GlassTone.Dark;
        var neutral = Build(RealGlassKind.Liquid, tone, NeutralAmount, true);
        var tinted = Build(RealGlassKind.Liquid, tone, 1f, true);

        Assert.Equal(neutral.EdgeLineStrength, tinted.EdgeLineStrength);
        Assert.Equal(neutral.RimFrostRelief, tinted.RimFrostRelief);
    }

    [Fact]
    public void ClearGlassOnTheWallpaperStillThinsTheRimFrost()
    {
        var neutral = Build(RealGlassKind.Frosted, GlassTone.Dark, NeutralAmount, true);
        var clear = Build(RealGlassKind.Frosted, GlassTone.Dark, 0f, true);

        Assert.True(clear.RimFrostRelief < neutral.RimFrostRelief);
    }

    [Fact]
    public void TintedGlassInAnAppKeepsTheBrighterRim()
    {
        var neutral = Build(RealGlassKind.Liquid, GlassTone.Dark, NeutralAmount, false);
        var tinted = Build(RealGlassKind.Liquid, GlassTone.Dark, 1f, false);

        Assert.True(tinted.EdgeLineStrength > neutral.EdgeLineStrength);
    }

    [Fact]
    public void LightGlassInAnAppKeepsTheDarkInkPreset()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, NeutralAmount, false);

        Assert.True(material.LightAppearance);
        Assert.Equal(GlassMaterial.RegularLight.Lift, material.Lift);
    }

    [Fact]
    public void NeutralTuningLeavesTheMaterialAlone()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, NeutralAmount);

        Assert.Equal(GlassMaterial.Regular.Specular, material.Specular);
        Assert.Equal(GlassMaterial.Regular.EdgeLineStrength, material.EdgeLineStrength);
        Assert.Equal(GlassMaterial.Regular.Lift, material.Lift);
        Assert.Equal(GlassMaterial.Regular.Depth, material.Depth);
        Assert.Equal(GlassMaterial.Regular.Dispersion, material.Dispersion);
        Assert.Equal(GlassMaterial.Regular.ShadowOpacity, material.ShadowOpacity);
    }

    [Fact]
    public void TuningScalesEachKnob()
    {
        var tuning = new GlassTuning { Highlight = 0f, Refraction = 1f, ColorSplit = 0.25f, Shadow = 1f };
        var material = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, false, default, 1f,
            NeutralAmount, tuning, Radius, Bar);

        Assert.Equal(0f, material.Specular);
        Assert.Equal(GlassMaterial.Regular.Depth * 2f, material.Depth, Tolerance);
        Assert.Equal(GlassMaterial.Regular.Dispersion * 0.5f, material.Dispersion, Tolerance);
        Assert.Equal(GlassMaterial.Regular.ShadowOpacity * 2f, material.ShadowOpacity, Tolerance);
    }

    [Fact]
    public void EdgesNeverPassAFullLine()
    {
        var tuning = new GlassTuning { Edges = 1f };
        var material = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Light, false, default, 1f, 1f,
            tuning, Radius, Bar);

        Assert.Equal(GlassTuning.MaximumEdgeLine, material.EdgeLineStrength);
    }

    [Theory]
    [InlineData(0f, -GlassTuning.BrightnessReach)]
    [InlineData(1f, GlassTuning.BrightnessReach)]
    public void BrightnessShiftsTheLift(float brightness, float shift)
    {
        var tuning = new GlassTuning { Brightness = brightness };
        var material = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, false, default, 1f,
            NeutralAmount, tuning, Radius, Bar);

        Assert.Equal(GlassMaterial.Regular.Lift + shift, material.Lift, Tolerance);
    }

    private static GlassMaterial Build(RealGlassKind kind, GlassTone tone, float amount, bool onWallpaper = false) =>
        RealGlassMaterials.Build(kind, tone, onWallpaper, default, 1f, amount, Neutral, Radius, Bar);
}
