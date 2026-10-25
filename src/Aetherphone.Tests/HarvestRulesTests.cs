using Aetherphone.Apps.Games.Harvest;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HarvestRulesTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(29.9f, 0)]
    [InlineData(30f, 1)]
    [InlineData(61f, 2)]
    [InlineData(95f, 3)]
    [InlineData(121f, 4)]
    [InlineData(179f, 5)]
    [InlineData(180f, 0)]
    [InlineData(215f, 1)]
    public void BiomesCycleEveryThirtySecondsThroughAllSix(float seconds, int expected)
    {
        Assert.Equal((HarvestBiome)expected, HarvestRules.BiomeAt(seconds));
    }

    [Fact]
    public void TheBiomesRunInTheirFixedOrder()
    {
        Assert.Equal(HarvestBiome.Meadow, HarvestRules.BiomeAt(1f));
        Assert.Equal(HarvestBiome.Highlands, HarvestRules.BiomeAt(31f));
        Assert.Equal(HarvestBiome.Mire, HarvestRules.BiomeAt(61f));
        Assert.Equal(HarvestBiome.Glacier, HarvestRules.BiomeAt(91f));
        Assert.Equal(HarvestBiome.Storm, HarvestRules.BiomeAt(121f));
        Assert.Equal(HarvestBiome.Dunes, HarvestRules.BiomeAt(151f));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(179.9f, 0)]
    [InlineData(180f, 1)]
    [InlineData(400f, 2)]
    public void LoopsCountWholeCycles(float seconds, int expected)
    {
        Assert.Equal(expected, HarvestRules.LoopAt(seconds));
    }

    [Fact]
    public void WindBlowsOnlyInTheHighlandsAndRampsIn()
    {
        Assert.Equal(0f, HarvestRules.Wind(10f, 1));
        Assert.Equal(0f, HarvestRules.Wind(75f, 1));
        Assert.Equal(0f, HarvestRules.Wind(30f, 1), 3);
        Assert.True(MathF.Abs(HarvestRules.Wind(30.5f, 1)) < MathF.Abs(HarvestRules.Wind(32f, 1)));
        Assert.True(HarvestRules.Wind(45f, 1) > 0f);
    }

    [Fact]
    public void WindFlipsDirectionEveryLoop()
    {
        Assert.True(HarvestRules.Wind(45f, 1) > 0f);
        Assert.True(HarvestRules.Wind(225f, 1) < 0f);
        Assert.True(HarvestRules.Wind(45f, -1) < 0f);
        Assert.True(HarvestRules.Wind(405f, 1) > 0f);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(11, 2)]
    [InlineData(12, 3)]
    [InlineData(19, 3)]
    [InlineData(20, 4)]
    [InlineData(500, 4)]
    public void TheMultiplierClimbsToFourAndStops(int combo, int expected)
    {
        Assert.Equal(expected, HarvestRules.Multiplier(combo));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 12)]
    [InlineData(12, 20)]
    [InlineData(20, 0)]
    public void NextMultiplierAtNamesTheNextStep(int combo, int expected)
    {
        Assert.Equal(expected, HarvestRules.NextMultiplierAt(combo));
    }

    [Fact]
    public void PointsScaleWithMaterialAndMultiplier()
    {
        Assert.Equal(10, HarvestRules.Points(HarvestMaterial.Ore, 1));
        Assert.Equal(10, HarvestRules.Points(HarvestMaterial.Log, 1));
        Assert.Equal(50, HarvestRules.Points(HarvestMaterial.Crystal, 2));
        Assert.Equal(240, HarvestRules.Points(HarvestMaterial.Shard, 4));
    }

    [Fact]
    public void RareMaterialsFillTheFeverMeterFaster()
    {
        Assert.Equal(HarvestRules.FeverGain(HarvestMaterial.Ore), HarvestRules.FeverGain(HarvestMaterial.Log));
        Assert.True(HarvestRules.FeverGain(HarvestMaterial.Crystal) > HarvestRules.FeverGain(HarvestMaterial.Ore));
        Assert.True(HarvestRules.FeverGain(HarvestMaterial.Shard) > HarvestRules.FeverGain(HarvestMaterial.Crystal));
        var oreToFill = (int)MathF.Ceiling(1f / HarvestRules.FeverGain(HarvestMaterial.Ore));
        Assert.InRange(oreToFill, 30, 60);
    }

    [Theory]
    [InlineData(0.5f, 0.2f)]
    [InlineData(0.1f, 0f)]
    [InlineData(0f, 0f)]
    public void AMissDrainsTheFeverMeterButNeverBelowEmpty(float fever, float expected)
    {
        Assert.Equal(expected, HarvestRules.AfterMiss(fever), 4);
    }

    [Fact]
    public void DifficultyKeepsClimbingAcrossLoops()
    {
        Assert.True(HarvestRules.FallSpeed(225f) > HarvestRules.FallSpeed(45f));
        Assert.True(HarvestRules.SpawnGap(225f) < HarvestRules.SpawnGap(45f));
        Assert.True(HarvestRules.ReachFactor(225f) > HarvestRules.ReachFactor(45f));
        Assert.True(HarvestRules.FallSpeed(405f) > HarvestRules.FallSpeed(225f));
        Assert.True(HarvestRules.HazardGap(225f) < HarvestRules.HazardGap(45f));
        Assert.True(HarvestRules.WarningSeconds(600f) >= 1f);
    }

    [Fact]
    public void TheTwistsShapeTheirBiomes()
    {
        Assert.True(HarvestRules.FallSpeed(15f) < HarvestRules.FallSpeed(45f));
        Assert.True(HarvestRules.ReachFactor(105f) < HarvestRules.ReachFactor(135f));
        Assert.True(HarvestRules.ReachFactor(75f) < HarvestRules.ReachFactor(135f));
        Assert.True(HarvestRules.HazardGap(5f) > HarvestRules.HazardGap(35f));
    }

    [Fact]
    public void ADriftingPathLandsExactlyOnItsMark()
    {
        Assert.Equal(40f, HarvestRules.PathX(40f, 30f, 0f), 4);
        Assert.Equal(70f, HarvestRules.PathX(40f, 30f, HarvestRules.SpawnHeight), 3);
        Assert.Equal(40f, HarvestRules.PathX(40f, 0f, 90f), 4);
    }
}
