using Aetherphone.Windows.Components;
using Glasswork.Glass;

namespace Aetherphone.Core.Shell;

internal static class RealGlassMaterials
{
    public const float AccentTintAmount = 0.55f;
    public const float FrostedFrost = 1f;
    public const int FrostedBlurLevels = 4;
    public const float WallpaperLightLift = 0.2f;
    public const float WallpaperSmokeAmount = 0.4f;
    public static readonly Glasswork.Color WallpaperSmoke = new(0.05f, 0.06f, 0.09f);
    private const float CapsuleTolerance = 0.5f;

    public static GlassMaterial Build(RealGlassKind kind, GlassTone tone, bool onWallpaper, Vector4 accent,
        float opacity, float amount, GlassTuning tuning, float radius, Vector2 size)
    {
        var preset = Base(kind, tone, onWallpaper, accent);
        var material = preset.WithAmount(amount);
        if (onWallpaper)
        {
            KeepPresetRim(ref material, in preset);
        }

        Tune(ref material, tuning);
        material.Opacity = Math.Clamp(opacity, 0f, 1f);
        material.CornerExponent = CornerExponent(radius, size);
        return material;
    }

    public static GlassShape Shape(Vector2 min, Vector2 max, float radius) => GlassShape.FromRect(min, max - min, radius);

    public static float CornerExponent(float radius, Vector2 size) =>
        radius >= MathF.Min(size.X, size.Y) * 0.5f - CapsuleTolerance
            ? Glasswork.CornerCurve.CircularExponent
            : Glasswork.CornerCurve.ContinuousExponent;

    private static GlassMaterial Base(RealGlassKind kind, GlassTone tone, bool onWallpaper, Vector4 accent)
    {
        switch (kind)
        {
            case RealGlassKind.Frosted:
                return Surface(tone, onWallpaper) with { Frost = FrostedFrost, BlurLevels = FrostedBlurLevels };
            case RealGlassKind.Accent:
                return GlassMaterial.Tinted(Glasswork.Color.FromVector4(accent), AccentTintAmount,
                    InkIsDark(tone, onWallpaper));
            default:
                return Surface(tone, onWallpaper);
        }
    }

    private static GlassMaterial Surface(GlassTone tone, bool onWallpaper)
    {
        if (!onWallpaper)
        {
            return GlassMaterial.ForAppearance(tone == GlassTone.Light);
        }

        return tone == GlassTone.Light
            ? GlassMaterial.Regular with { Lift = WallpaperLightLift }
            : GlassMaterial.Tinted(WallpaperSmoke, WallpaperSmokeAmount);
    }

    private static bool InkIsDark(GlassTone tone, bool onWallpaper) => tone == GlassTone.Light && !onWallpaper;

    private static void KeepPresetRim(ref GlassMaterial material, in GlassMaterial preset)
    {
        material.EdgeLineStrength = MathF.Min(material.EdgeLineStrength, preset.EdgeLineStrength);
        material.RimFrostRelief = MathF.Min(material.RimFrostRelief, preset.RimFrostRelief);
    }

    private static void Tune(ref GlassMaterial material, GlassTuning tuning)
    {
        material.Specular *= GlassTuning.Multiplier(tuning.Highlight);
        material.EdgeLineStrength = MathF.Min(material.EdgeLineStrength * GlassTuning.Multiplier(tuning.Edges),
            GlassTuning.MaximumEdgeLine);
        material.Lift += GlassTuning.LiftShift(tuning.Brightness);
        material.Depth *= GlassTuning.Multiplier(tuning.Refraction);
        material.Dispersion *= GlassTuning.Multiplier(tuning.ColorSplit);
        material.ShadowOpacity *= GlassTuning.Multiplier(tuning.Shadow);
    }
}
