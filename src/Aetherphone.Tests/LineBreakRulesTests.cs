using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LineBreakRulesTests
{
    [Theory]
    [InlineData('。')]
    [InlineData('、')]
    [InlineData('，')]
    [InlineData('！')]
    [InlineData('？')]
    [InlineData('」')]
    [InlineData('）')]
    [InlineData('ー')]
    [InlineData('っ')]
    [InlineData('ャ')]
    public void ClosingMarksAndSmallKanaNeverStartALine(char value)
    {
        Assert.True(LineBreakRules.CannotStartLine(value));
        Assert.True(LineBreakRules.KeepsWithNext('字', value.ToString()));
    }

    [Theory]
    [InlineData('「')]
    [InlineData('（')]
    [InlineData('『')]
    [InlineData('【')]
    public void OpeningBracketsNeverEndALine(char value)
    {
        Assert.True(LineBreakRules.CannotEndLine(value));
        Assert.True(LineBreakRules.KeepsWithNext(value, "字"));
    }

    [Theory]
    [InlineData('字', "字")]
    [InlineData('字', "Play")]
    [InlineData('a', "b")]
    [InlineData('。', "字")]
    public void OrdinaryNeighboursMayBreak(char last, string next)
    {
        Assert.False(LineBreakRules.KeepsWithNext(last, next));
    }

    [Fact]
    public void AWordThatOnlyStartsWithAClosingMarkMayStillBreak()
    {
        Assert.False(LineBreakRules.KeepsWithNext('字', "。abc"));
    }
}
