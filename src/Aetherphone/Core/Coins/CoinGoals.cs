using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal static class CoinGoals
{
    private const string WelcomeRuleId = "coin.welcome";
    private const string CallRuleId = "call.connected";
    private const string ReelDailyRuleId = "reel.daily";

    public static void Count(CoinRuleStatusDto[] rules, bool weekly, out int done, out int total)
    {
        done = 0;
        total = 0;
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (!Counts(rule, weekly))
            {
                continue;
            }

            total++;
            if (IsComplete(rule))
            {
                done++;
            }
        }
    }

    public static float Progress(CoinRuleStatusDto[] rules, bool weekly)
    {
        var sum = 0f;
        var total = 0;
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (!Counts(rule, weekly))
            {
                continue;
            }

            total++;
            sum += Progress(rule);
        }

        return total == 0 ? 0f : sum / total;
    }

    public static float Progress(CoinRuleStatusDto rule)
    {
        if (IsComplete(rule))
        {
            return 1f;
        }

        var byCoins = rule.PeriodCap > 0 ? (float)rule.EarnedThisPeriod / rule.PeriodCap : 0f;
        var byAwards = rule.PeriodLimit > 0 ? (float)rule.Awards / rule.PeriodLimit : 0f;
        return Math.Clamp(MathF.Max(byCoins, byAwards), 0f, 1f);
    }

    public static bool IsComplete(CoinRuleStatusDto rule) =>
        rule.PeriodCap > 0
        && (rule.EarnedThisPeriod >= rule.PeriodCap || (rule.PeriodLimit > 0 && rule.Awards >= rule.PeriodLimit));

    private static bool Counts(CoinRuleStatusDto rule, bool weekly)
    {
        if (rule.Weekly != weekly || rule.PeriodCap <= 0)
        {
            return false;
        }

        if (string.Equals(rule.RuleId, WelcomeRuleId, StringComparison.Ordinal))
        {
            return false;
        }

        return !JoinsOnlyWhenEarned(rule.RuleId) || rule.Awards > 0;
    }

    // A call needs someone on the other end and a reel needs a Windows video encoder, so neither may hold
    // back a player's daily goal on days they did not do it.
    private static bool JoinsOnlyWhenEarned(string ruleId) =>
        string.Equals(ruleId, CallRuleId, StringComparison.Ordinal)
        || string.Equals(ruleId, ReelDailyRuleId, StringComparison.Ordinal);
}
