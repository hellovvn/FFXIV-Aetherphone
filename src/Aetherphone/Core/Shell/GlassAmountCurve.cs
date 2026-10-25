namespace Aetherphone.Core.Shell;

internal static class GlassAmountCurve
{
    public const float Neutral = 0.5f;
    public const float ClearTint = 0.55f;
    public const float TintedTint = 1.6f;

    public static float ClassicTint(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return amount < Neutral
            ? float.Lerp(ClearTint, 1f, amount / Neutral)
            : float.Lerp(1f, TintedTint, (amount - Neutral) / (1f - Neutral));
    }
}
