using Aetherphone.Core.Casino;
using Dalamud.Game.Text;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoChatInvitesTests
{
    [Fact]
    public void APostedTagReadsBackItsCode()
    {
        var line = string.Concat("Join my table: ", CasinoChatCodes.Tag("K7PQ2M"), ".");
        Assert.True(CasinoChatCodes.TryFind(line, out var code));
        Assert.Equal("K7PQ2M", code);
    }

    [Theory]
    [InlineData("テーブルに参加: [Gamba K7PQ2M]。")]
    [InlineData("[Gamba K7PQ2M]")]
    [InlineData("Tisch [Gamba K7PQ2M] betreten")]
    public void TheTagIsFoundWhateverLanguageSurroundsIt(string line)
    {
        Assert.True(CasinoChatCodes.TryFind(line, out var code));
        Assert.Equal("K7PQ2M", code);
    }

    [Theory]
    [InlineData("Join my table: K7PQ2M")]
    [InlineData("[Gamba K7PQ2]")]
    [InlineData("[Gamba K7PQ2MX]")]
    [InlineData("[Gamba K7PQ2M")]
    [InlineData("[Gamba K7PQ0M]")]
    [InlineData("[gamba K7PQ2M]")]
    [InlineData("")]
    public void AnythingButAWellFormedTagIsIgnored(string line)
    {
        Assert.False(CasinoChatCodes.TryFind(line, out var code));
        Assert.Equal(string.Empty, code);
    }

    [Theory]
    [InlineData(XivChatType.Party)]
    [InlineData(XivChatType.CrossParty)]
    [InlineData(XivChatType.Say)]
    [InlineData(XivChatType.Yell)]
    public void PartyAndNearbyChatAreHeard(XivChatType kind)
    {
        Assert.True(CasinoChatInvites.Listens(kind));
    }

    [Theory]
    [InlineData(XivChatType.Shout)]
    [InlineData(XivChatType.TellIncoming)]
    [InlineData(XivChatType.FreeCompany)]
    [InlineData(XivChatType.Echo)]
    public void OtherChannelsAreNot(XivChatType kind)
    {
        Assert.False(CasinoChatInvites.Listens(kind));
    }

    [Fact]
    public void TheSameCodeNotifiesOnceUntilItsWindowPasses()
    {
        var heard = new Dictionary<string, long>(StringComparer.Ordinal);
        Assert.True(CasinoChatInvites.FirstHeard(heard, "K7PQ2M", 1_000));
        Assert.False(CasinoChatInvites.FirstHeard(heard, "K7PQ2M", 1_000 + CasinoChatInvites.RepeatWindowMilliseconds - 1));
        Assert.True(CasinoChatInvites.FirstHeard(heard, "K7PQ2M", 1_000 + CasinoChatInvites.RepeatWindowMilliseconds));
        Assert.True(CasinoChatInvites.FirstHeard(heard, "ABCDEF", 2_000));
    }

    [Fact]
    public void TheMemoryOfHeardCodesStaysBounded()
    {
        var heard = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoChatInvites.HeardCapacity * 3; index++)
        {
            Assert.True(CasinoChatInvites.FirstHeard(heard, index.ToString("D6"), 5_000));
            Assert.True(heard.Count <= CasinoChatInvites.HeardCapacity);
        }
    }

    [Fact]
    public void ACodeNoticeAsksToJoinByCode()
    {
        var launcher = new CasinoLauncher();
        launcher.RequestNotification(CasinoLauncher.CodeGroupKey("K7PQ2M"));
        Assert.True(launcher.TryConsume(true, out var launch));
        Assert.Equal(CasinoLaunchKind.Code, launch.Kind);
        Assert.Equal("K7PQ2M", launch.Code);
        Assert.Equal(string.Empty, launch.TableId);
    }

    [Theory]
    [InlineData("casino:code:")]
    [InlineData("casino:code:K7PQ")]
    [InlineData("casino:code:K7PQ0M")]
    public void ACodeNoticeWithoutAValidCodeAsksForNothing(string groupKey)
    {
        var launcher = new CasinoLauncher();
        launcher.RequestNotification(groupKey);
        Assert.False(launcher.HasPending);
    }
}
