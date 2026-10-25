using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ActivityBucketsTests
{
    private static readonly DateTimeOffset Noon = new(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Local));

    private static ActivityBucket BucketAgo(TimeSpan age) =>
        ActivityBuckets.For(Noon.Subtract(age).ToUnixTimeSeconds(), Noon.ToUnixTimeSeconds());

    [Fact]
    public void Earlier_the_same_day_is_today()
    {
        Assert.Equal(ActivityBucket.Today, BucketAgo(TimeSpan.FromHours(3)));
    }

    [Fact]
    public void Yesterday_falls_in_this_week()
    {
        Assert.Equal(ActivityBucket.Week, BucketAgo(TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Ten_days_ago_falls_in_this_month()
    {
        Assert.Equal(ActivityBucket.Month, BucketAgo(TimeSpan.FromDays(10)));
    }

    [Fact]
    public void Six_weeks_ago_is_earlier()
    {
        Assert.Equal(ActivityBucket.Earlier, BucketAgo(TimeSpan.FromDays(42)));
    }

    [Fact]
    public void Every_bucket_has_its_own_label()
    {
        Assert.Equal(L.Social.ActivityToday.Key, ActivityBuckets.Label(ActivityBucket.Today).Key);
        Assert.Equal(L.Social.ActivityThisWeek.Key, ActivityBuckets.Label(ActivityBucket.Week).Key);
        Assert.Equal(L.Social.ActivityThisMonth.Key, ActivityBuckets.Label(ActivityBucket.Month).Key);
        Assert.Equal(L.Social.ActivityEarlier.Key, ActivityBuckets.Label(ActivityBucket.Earlier).Key);
    }
}
