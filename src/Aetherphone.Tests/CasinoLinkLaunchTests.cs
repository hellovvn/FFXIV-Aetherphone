using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLinkLaunchTests
{
    [Fact]
    public void AGameLaunch_WaitsUntilGambaHasItsState()
    {
        var launcher = new CasinoLauncher();
        launcher.RequestGame("plinko");

        Assert.False(launcher.TryConsume(false, out _));
        Assert.True(launcher.HasPending);

        Assert.True(launcher.TryConsume(true, out var launch));
        Assert.Equal(CasinoLaunchKind.Game, launch.Kind);
        Assert.Equal("plinko", launch.GameId);
        Assert.False(launcher.HasPending);
    }

    [Fact]
    public void ATableLaunch_DoesNotWaitForGambaState()
    {
        var launcher = new CasinoLauncher();
        launcher.RequestTable("table-7");

        Assert.True(launcher.TryConsume(false, out var launch));
        Assert.Equal(CasinoLaunchKind.Table, launch.Kind);
    }
}
