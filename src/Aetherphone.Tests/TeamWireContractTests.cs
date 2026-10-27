using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TeamWireContractTests
{
    [Fact]
    public void APaidConversationCarriesItsDeletePermissionAndClearedBoundary()
    {
        var page = JsonSerializer.Deserialize(
            "{\"messages\":[],\"unread\":0,\"canReply\":true,\"canDelete\":true,\"clearedThroughId\":42}",
            AethernetJsonContext.Default.TeamMessagePage);

        Assert.NotNull(page);
        Assert.True(page.CanDelete);
        Assert.Equal(42, page.ClearedThroughId);
    }

    [Fact]
    public void AnOlderServerKeepsTheOfficialConversationProtected()
    {
        var page = JsonSerializer.Deserialize("{\"messages\":[],\"unread\":0,\"canReply\":true}",
            AethernetJsonContext.Default.TeamMessagePage);

        Assert.NotNull(page);
        Assert.False(page.CanDelete);
        Assert.Equal(0, page.ClearedThroughId);
    }
}
