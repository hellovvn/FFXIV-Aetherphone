namespace Aetherphone.Core.Shell;

internal sealed class GlassTuning
{
    public const float Neutral = 0.5f;
    public const float MaximumMultiplier = 2f;
    public const float BrightnessReach = 0.3f;
    public const float MaximumEdgeLine = 1f;

    public bool CursorLight { get; set; } = true;
    public float Highlight { get; set; } = Neutral;
    public float Edges { get; set; } = Neutral;
    public float Brightness { get; set; } = Neutral;
    public float Refraction { get; set; } = Neutral;
    public float ColorSplit { get; set; } = Neutral;
    public float Shadow { get; set; } = Neutral;

    public static float Multiplier(float position) => Math.Clamp(position, 0f, 1f) * MaximumMultiplier;

    public static float LiftShift(float position) => (Math.Clamp(position, 0f, 1f) - Neutral) * 2f * BrightnessReach;
}
