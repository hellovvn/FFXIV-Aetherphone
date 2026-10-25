using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlassAmountCurveTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void TheNeutralAmountLeavesTheClassicTintUnchanged()
    {
        Assert.Equal(1f, GlassAmountCurve.ClassicTint(GlassAmountCurve.Neutral), Tolerance);
    }

    [Fact]
    public void TheEndsReachTheClearAndTintedScales()
    {
        Assert.Equal(GlassAmountCurve.ClearTint, GlassAmountCurve.ClassicTint(0f), Tolerance);
        Assert.Equal(GlassAmountCurve.TintedTint, GlassAmountCurve.ClassicTint(1f), Tolerance);
    }

    [Theory]
    [InlineData(-2f, 0f)]
    [InlineData(3f, 1f)]
    public void AmountsOutsideTheSliderClampToItsEnds(float amount, float clamped)
    {
        Assert.Equal(GlassAmountCurve.ClassicTint(clamped), GlassAmountCurve.ClassicTint(amount), Tolerance);
    }

    [Fact]
    public void TheTintRisesWithTheAmount()
    {
        var previous = GlassAmountCurve.ClassicTint(0f);
        for (var step = 1; step <= 20; step++)
        {
            var current = GlassAmountCurve.ClassicTint(step / 20f);
            Assert.True(current > previous);
            previous = current;
        }
    }
}
