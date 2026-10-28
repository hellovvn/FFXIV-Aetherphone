using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinGoalsTests
{
    [Fact]
    public void AnySpinFinishesTheDailySpinGoal()
    {
        var spin = Rule("casino.daily", 5, 60, earned: 10, awards: 1, limit: 1);

        Assert.True(CoinGoals.IsComplete(spin));
        Assert.Equal(1f, CoinGoals.Progress(spin));
    }

    [Fact]
    public void APartlyEarnedRuleWithAwardsLeftIsNotComplete()
    {
        var chat = Rule("chat.conversation", 8, 32, earned: 16, awards: 2, limit: 4);

        Assert.False(CoinGoals.IsComplete(chat));
        Assert.Equal(0.5f, CoinGoals.Progress(chat));
    }

    [Fact]
    public void TheWelcomeRuleNeverCountsAsAGoal()
    {
        var rules = new[]
        {
            Rule("coin.checkin", 20, 20, earned: 20, awards: 1, limit: 1),
            Rule("coin.welcome", 200, 200, earned: 0, awards: 0, limit: 1),
        };

        CoinGoals.Count(rules, false, out var done, out var total);

        Assert.Equal(1, total);
        Assert.Equal(1, done);
        Assert.Equal(1f, CoinGoals.Progress(rules, false));
    }

    [Fact]
    public void CallsOnlyCountOnDaysWithACall()
    {
        var quiet = new[] { Rule("call.connected", 15, 30, earned: 0, awards: 0, limit: 2) };
        var called = new[] { Rule("call.connected", 15, 30, earned: 15, awards: 1, limit: 2) };

        CoinGoals.Count(quiet, false, out _, out var quietTotal);
        CoinGoals.Count(called, false, out var calledDone, out var calledTotal);

        Assert.Equal(0, quietTotal);
        Assert.Equal(1, calledTotal);
        Assert.Equal(0, calledDone);
    }

    [Fact]
    public void TheReelQuestOnlyCountsOnDaysWithAReel()
    {
        var quiet = new[] { Rule("reel.daily", 35, 35, earned: 0, awards: 0, limit: 1) };
        var posted = new[] { Rule("reel.daily", 35, 35, earned: 35, awards: 1, limit: 1) };

        CoinGoals.Count(quiet, false, out _, out var quietTotal);
        CoinGoals.Count(posted, false, out var postedDone, out var postedTotal);

        Assert.Equal(0, quietTotal);
        Assert.Equal(1, postedTotal);
        Assert.Equal(1, postedDone);
    }

    [Fact]
    public void BothReelRulesHaveTheirOwnLabelAndHint()
    {
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For("reel.first").Key);
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For("reel.daily").Key);
        Assert.True(CoinRuleLabels.TryHint("reel.first", out _));
        Assert.True(CoinRuleLabels.TryHint("reel.daily", out _));
    }

    [Fact]
    public void ProgressAveragesOnlyTheRequestedPeriod()
    {
        var rules = new[]
        {
            Rule("coin.checkin", 20, 20, earned: 20, awards: 1, limit: 1),
            Rule("game.session", 8, 40, earned: 0, awards: 0, limit: 5),
            Rule("story.survived", 20, 20, earned: 0, awards: 0, limit: 1, weekly: true),
        };

        Assert.Equal(0.5f, CoinGoals.Progress(rules, false));
        Assert.Equal(0f, CoinGoals.Progress(rules, true));
    }

    private static CoinRuleStatusDto Rule(string ruleId, long amount, long cap, long earned, int awards, int limit,
        bool weekly = false) =>
        new(ruleId, "coin", amount, cap, earned, awards, limit, weekly);
}
