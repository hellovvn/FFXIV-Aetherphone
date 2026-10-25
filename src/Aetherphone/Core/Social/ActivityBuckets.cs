using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal enum ActivityBucket : byte
{
    None,
    Today,
    Week,
    Month,
    Earlier,
}

internal static class ActivityBuckets
{
    private const long SecondsPerDay = 86400L;
    private const long WeekSeconds = 7L * SecondsPerDay;
    private const long MonthSeconds = 30L * SecondsPerDay;

    public static ActivityBucket For(long createdAtUnix, long now)
    {
        if (TimeText.SameLocalDay(createdAtUnix, now))
        {
            return ActivityBucket.Today;
        }

        var age = now - createdAtUnix;
        if (age < WeekSeconds)
        {
            return ActivityBucket.Week;
        }

        return age < MonthSeconds ? ActivityBucket.Month : ActivityBucket.Earlier;
    }

    public static LocString Label(ActivityBucket bucket) => bucket switch
    {
        ActivityBucket.Today => L.Social.ActivityToday,
        ActivityBucket.Week => L.Social.ActivityThisWeek,
        ActivityBucket.Month => L.Social.ActivityThisMonth,
        _ => L.Social.ActivityEarlier,
    };
}
