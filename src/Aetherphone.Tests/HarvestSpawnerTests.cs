using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Harvest;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HarvestSpawnerTests
{
    private const float Tick = 1f / 120f;

    [Fact]
    public void TheSameSeedPlansTheSameSchedule()
    {
        var first = Schedule(7UL, 200f, out var firstHazards);
        var second = Schedule(7UL, 200f, out var secondHazards);
        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].SpawnTime, second[index].SpawnTime);
            Assert.Equal(first[index].LandX, second[index].LandX);
            Assert.Equal(first[index].Material, second[index].Material);
            Assert.Equal(first[index].Role, second[index].Role);
            Assert.Equal(first[index].Drift, second[index].Drift);
        }

        Assert.Equal(firstHazards.Count, secondHazards.Count);
        for (var index = 0; index < firstHazards.Count; index++)
        {
            Assert.Equal(firstHazards[index].EnterTime, secondHazards[index].EnterTime);
            Assert.Equal(firstHazards[index].Kind, secondHazards[index].Kind);
            Assert.Equal(firstHazards[index].Direction, secondHazards[index].Direction);
        }
    }

    [Fact]
    public void DifferentSeedsPlanDifferentSchedules()
    {
        var first = Schedule(1UL, 60f, out _);
        var second = Schedule(2UL, 60f, out _);
        var differences = 0;
        var count = Math.Min(first.Count, second.Count);
        for (var index = 0; index < count; index++)
        {
            if (MathF.Abs(first[index].LandX - second[index].LandX) > 0.01f)
            {
                differences++;
            }
        }

        Assert.True(differences > count / 2);
    }

    [Fact]
    public void EveryRequiredDropIsReachableFromTheOneBefore()
    {
        for (var seed = 1UL; seed <= 12UL; seed++)
        {
            var plans = Schedule(seed, 420f, out _);
            var required = plans.Where(plan => plan.Role == HarvestRole.Required).OrderBy(plan => plan.LandTime).ToList();
            Assert.True(required.Count > 300);
            for (var index = 1; index < required.Count; index++)
            {
                var gap = required[index].LandTime - required[index - 1].LandTime;
                var distance = MathF.Abs(required[index].LandX - required[index - 1].LandX);
                Assert.True(gap > 0.3f, $"seed {seed} drop {index} lands {gap:F2}s after the last");
                Assert.True(distance <= HarvestPlayer.MaxSpeed * gap + HarvestSpawner.CatchSlack + 0.01f,
                    $"seed {seed} drop {index} needs {distance:F1} units in {gap:F2}s");
            }
        }
    }

    [Fact]
    public void DropsLandInsideTheField()
    {
        var plans = Schedule(3UL, 300f, out _);
        Assert.All(plans, plan => Assert.InRange(plan.LandX, HarvestSpawner.Margin, HarvestRules.Width - HarvestSpawner.Margin));
    }

    [Fact]
    public void SparksFallOnlyInTheStormlandsAndMiragesOnlyInTheDunes()
    {
        for (var seed = 1UL; seed <= 6UL; seed++)
        {
            var plans = Schedule(seed, 400f, out _);
            var sparks = plans.Where(plan => plan.Role == HarvestRole.Spark).ToList();
            var mirages = plans.Where(plan => plan.Role == HarvestRole.Mirage).ToList();
            Assert.NotEmpty(sparks);
            Assert.NotEmpty(mirages);
            Assert.All(sparks, plan => Assert.True(HarvestRules.BiomeAt(plan.LandTime - 0.35f) == HarvestBiome.Storm ||
                                                   HarvestRules.BiomeAt(plan.LandTime - 0.12f) == HarvestBiome.Storm));
            Assert.All(mirages, plan => Assert.True(HarvestRules.BiomeAt(plan.LandTime) == HarvestBiome.Dunes ||
                                                    HarvestRules.BiomeAt(plan.LandTime + 1.5f) == HarvestBiome.Dunes));
            Assert.DoesNotContain(plans, plan => plan.Role == HarvestRole.Bonus);
        }
    }

    [Fact]
    public void SparksKeepClearOfTheDropsAroundThem()
    {
        for (var seed = 1UL; seed <= 12UL; seed++)
        {
            var plans = Schedule(seed, 400f, out _);
            var required = plans.Where(plan => plan.Role == HarvestRole.Required).ToList();
            foreach (var spark in plans.Where(plan => plan.Role == HarvestRole.Spark))
            {
                Assert.Equal(HarvestSpawner.SparkHeight, spark.Height);
                Assert.Equal(HarvestSpawner.SparkHoldSeconds, spark.Hold);
                foreach (var drop in required.Where(drop => MathF.Abs(drop.LandTime - spark.LandTime) <= 0.3f))
                {
                    Assert.True(MathF.Abs(drop.LandX - spark.LandX) >= HarvestSpawner.SparkClearance - 0.01f,
                        $"seed {seed} spark at {spark.LandX:F1} lands beside a drop at {drop.LandX:F1}");
                }
            }
        }
    }

    [Fact]
    public void ABirdComesFirstThenABoulderAndEveryHazardIsWarned()
    {
        Schedule(5UL, 300f, out var hazards);
        Assert.True(hazards.Count > 20);
        Assert.Equal(HarvestHazardKind.Bird, hazards[0].Kind);
        Assert.Equal(HarvestHazardKind.Boulder, hazards[1].Kind);
        Assert.Equal(HarvestSpawner.FirstBirdTime, hazards[0].EnterTime, 3);
        Assert.All(hazards, hazard => Assert.True(hazard.EnterTime - hazard.WarnTime >= 1f - 0.001f));
    }

    [Fact]
    public void HazardsNeverOverlapAndNeverRepeatThreeTimes()
    {
        for (var seed = 1UL; seed <= 8UL; seed++)
        {
            Schedule(seed, 400f, out var hazards);
            var run = 1;
            for (var index = 1; index < hazards.Count; index++)
            {
                var previousExit = hazards[index - 1].EnterTime + HarvestSpawner.CrossDistance / hazards[index - 1].Speed;
                Assert.True(hazards[index].WarnTime >= previousExit, $"seed {seed} hazard {index} overlaps");
                run = hazards[index].Kind == hazards[index - 1].Kind ? run + 1 : 1;
                Assert.True(run <= 2, $"seed {seed} repeats a hazard three times");
            }
        }
    }

    [Fact]
    public void FeverSuspendsPlanningAndResumesBesideThePlayer()
    {
        var spawner = new HarvestSpawner();
        spawner.Reset(GameRandom.FromSeed(9UL));
        spawner.Advance(20f);
        Assert.True(spawner.PlanCount > 0);
        spawner.Suspend(20f);
        Assert.Equal(0, spawner.PlanCount);
        spawner.Advance(22f);
        Assert.Equal(0, spawner.PlanCount);
        Assert.False(spawner.TryTakeHazard(40f, out _));
        spawner.Resume(26f, 80f);
        Assert.Equal(80f, spawner.LastLandX);
        spawner.Advance(26f);
        Assert.True(spawner.PlanCount > 0);
        var first = spawner.Plans.ToArray().Where(plan => plan.Role == HarvestRole.Required).MinBy(plan => plan.LandTime);
        Assert.True(first.LandTime >= 26f + HarvestSpawner.ResumeDelaySeconds);
        Assert.True(MathF.Abs(first.LandX - 80f) <= HarvestPlayer.MaxSpeed * (first.LandTime - 26f) + HarvestSpawner.CatchSlack);
    }

    [Fact]
    public void TheBonusRainSweepsAcrossTheField()
    {
        var spawner = new HarvestSpawner();
        spawner.Reset(GameRandom.FromSeed(4UL));
        var minimum = float.MaxValue;
        var maximum = float.MinValue;
        for (var step = 0; step < 40; step++)
        {
            var plan = spawner.Bonus(12f, step * 0.14f);
            Assert.Equal(HarvestRole.Bonus, plan.Role);
            Assert.Equal(HarvestRules.SpawnHeight, plan.Height);
            minimum = MathF.Min(minimum, plan.LandX);
            maximum = MathF.Max(maximum, plan.LandX);
        }

        Assert.True(maximum - minimum > HarvestRules.Width * 0.6f);
    }

    private static List<HarvestPlan> Schedule(ulong seed, float seconds, out List<HarvestHazardPlan> hazards)
    {
        var spawner = new HarvestSpawner();
        spawner.Reset(GameRandom.FromSeed(seed));
        var plans = new List<HarvestPlan>();
        hazards = new List<HarvestHazardPlan>();
        var steps = (int)(seconds / Tick);
        for (var step = 1; step <= steps; step++)
        {
            var now = step * Tick;
            spawner.Advance(now);
            while (spawner.TryTakeDue(now, out var plan))
            {
                plans.Add(plan);
            }

            while (spawner.TryTakeHazard(now, out var hazard))
            {
                hazards.Add(hazard);
            }
        }

        return plans;
    }
}
