using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StridePagerLoopTests
{
    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(7, 8, 7)]
    [InlineData(8, 8, 0)]
    [InlineData(17, 8, 1)]
    [InlineData(-1, 8, 7)]
    [InlineData(-9, 8, 7)]
    public void CardIndexWrapsBothWays(int virtualIndex, int count, int card) =>
        Assert.Equal(card, StridePager.CardIndex(virtualIndex, count));

    [Theory]
    [InlineData(0f, 8, 8)]
    [InlineData(7.4f, 8, 8)]
    [InlineData(7.6f, 8, 0)]
    [InlineData(15.4f, 8, 0)]
    [InlineData(15.6f, 8, -8)]
    [InlineData(23f, 8, -8)]
    [InlineData(1.4f, 2, 2)]
    [InlineData(3.6f, 2, -2)]
    public void WrapShiftKeepsTheValueInTheMiddleCopy(float value, int count, int shift) =>
        Assert.Equal(shift, StridePager.WrapShift(value, count));

    [Fact]
    public void WrappingNeverChangesTheCardShown()
    {
        for (var page = 0; page < 24; page++)
        {
            var shifted = page + StridePager.WrapShift(page, 8);
            Assert.Equal(StridePager.CardIndex(page, 8), StridePager.CardIndex(shifted, 8));
        }
    }

    [Fact]
    public void EmptyAndSingleRailsNeverShift()
    {
        Assert.Equal(0, StridePager.CardIndex(5, 0));
        Assert.Equal(0, StridePager.WrapShift(5f, 0));
        Assert.Equal(0, StridePager.WrapShift(5f, 1));
    }
}
