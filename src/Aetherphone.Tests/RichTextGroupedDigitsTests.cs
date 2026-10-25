using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RichTextGroupedDigitsTests
{
    [Theory]
    [InlineData("You won 100,000 gil")]
    [InlineData("Dein Preis von 100.000 Gil")]
    [InlineData("about 2.5 seconds")]
    [InlineData("1,000,000")]
    public void NumbersWithASeparatorBetweenDigitsAreKeptTogether(string text)
    {
        Assert.True(RichText.HasGroupedDigits(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",")]
    [InlineData("Hello, world.")]
    [InlineData("Week 43, final")]
    [InlineData("at 20:00 on Saturday")]
    [InlineData("100 000 gil")]
    public void TextWithoutASeparatorBetweenDigitsIsLeftAlone(string text)
    {
        Assert.False(RichText.HasGroupedDigits(text));
    }
}
