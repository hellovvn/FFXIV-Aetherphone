using Aetherphone.Core.Casino.Rolls;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RollLinesTests
{
    private const string Me = "Lily Example";

    private static RollPart Text(string text) => RollPart.OfText(text);

    private static RollPart Icon(int icon) => RollPart.OfIcon(icon);

    private static RollPart Player(string name) => RollPart.OfPlayer(name);

    [Fact]
    public void AnotherPlayersRandomReadsTheLinkedNameAndTheRange()
    {
        RollPart[] line =
        {
            Text("Random! "), Player("Mira Vale"), Text("Mira Vale"), Text(" rolls a "), Icon(73),
            Text("57 (out of 391)."),
        };

        Assert.True(RollLines.TryRead(line, true, string.Empty, Me, out var roll));
        Assert.Equal(new ChatRoll("Mira Vale", 57, 391, false), roll);
    }

    [Fact]
    public void YourOwnRandomHasNoLinkAndUsesTheDefaultRange()
    {
        RollPart[] line = { Text("Random! You roll a "), Icon(73), Text("734.") };

        Assert.True(RollLines.TryRead(line, true, string.Empty, Me, out var roll));
        Assert.Equal(new ChatRoll(Me, 734, RollLines.DefaultMax, true), roll);
    }

    [Theory]
    [MemberData(nameof(ForeignLines))]
    public void EveryClientLanguageYieldsTheValueAndRange(string before, string after, int value, int max)
    {
        RollPart[] line = { Text(before), Player("Mira Vale"), Text("Mira Vale"), Icon(73), Text(after) };

        Assert.True(RollLines.TryRead(line, true, string.Empty, Me, out var roll));
        Assert.Equal(value, roll.Value);
        Assert.Equal(max, roll.Max);
    }

    public static TheoryData<string, string, int, int> ForeignLines() => new()
    {
        { " hat mit dem 391-seitigen Würfel eine ", "57 gewürfelt!", 57, 391 },
        { " würfelt eine ", " 57.", 57, RollLines.DefaultMax },
        { "Lancer d'un dé 391 ! ", "57 !", 57, 391 },
        { "391面ダイス！ ", "57を出した！", 57, 391 },
    };

    [Fact]
    public void DiceInPartyReadsTheSenderAndTheRange()
    {
        RollPart[] line = { Icon(54), Text("Random! (1-100) "), Icon(73), Text("42"), Icon(55) };

        Assert.True(RollLines.TryRead(line, false, "Mira Vale", Me, out var roll));
        Assert.Equal(new ChatRoll("Mira Vale", 42, 100, false), roll);
    }

    [Fact]
    public void TypedTextThatLooksLikeARollIsIgnored()
    {
        RollPart[] said = { Text("Random! You roll a 999 (out of 999).") };
        RollPart[] noDiceIcon = { Text("Random! (1-100) "), Icon(73), Text("42") };

        Assert.False(RollLines.TryRead(said, true, string.Empty, Me, out _));
        Assert.False(RollLines.TryRead(noDiceIcon, false, "Mira Vale", Me, out _));
    }

    [Fact]
    public void ANameWeCannotPlaceIsDropped()
    {
        RollPart[] line = { Text("Random! You roll a "), Icon(73), Text("12.") };

        Assert.False(RollLines.TryRead(line, true, string.Empty, string.Empty, out _));
    }
}
