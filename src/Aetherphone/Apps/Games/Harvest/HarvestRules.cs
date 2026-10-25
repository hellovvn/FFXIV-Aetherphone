using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Harvest;

internal static class HarvestRules
{
    public const float Width = 100f;
    public const float SpawnHeight = 178f;
    public const int BiomeCount = 6;
    public const float BiomeSeconds = 30f;
    public const float RampSeconds = 200f;
    public const int MaxMultiplier = 4;
    public const int MaxMisses = 3;
    public const int MaxHits = 3;
    public const float MissFeverLoss = 0.3f;
    public const int DodgePoints = 20;
    public const float FogLine = 82f;
    public const float ShardSpeedFactor = 1.12f;
    public const float FeverSpeedFactor = 1.15f;
    public const float MirageChance = 0.45f;
    private const float WindRampInSeconds = 2f;
    private const float WindRampOutSeconds = 1.5f;
    private const float GustPeriod = 4.5f;
    private const float CrystalChance = 0.16f;
    private const float ShardBaseChance = 0.035f;
    private const float ShardRampChance = 0.035f;
    private const float FeverCrystalChance = 0.55f;
    private const float FeverShardChance = 0.2f;
    private const float MirageShardChance = 0.25f;
    private const float PathWobble = 0.08f;
    private static readonly int[] MultiplierSteps = { 5, 12, 20 };
    private static readonly int[] MaterialPoints = { 10, 10, 25, 60 };
    private static readonly float[] MaterialFever = { 0.02f, 0.02f, 0.04f, 0.1f };

    public static int BiomeVisit(float seconds) => seconds <= 0f ? 0 : (int)(seconds / BiomeSeconds);

    public static HarvestBiome BiomeAt(float seconds) => (HarvestBiome)(BiomeVisit(seconds) % BiomeCount);

    public static int LoopAt(float seconds) => BiomeVisit(seconds) / BiomeCount;

    public static float BiomeElapsed(float seconds) => MathF.Max(0f, seconds - BiomeVisit(seconds) * BiomeSeconds);

    public static float Ramp(float seconds) => Math.Clamp(seconds / RampSeconds, 0f, 1f);

    public static float Overtime(float seconds) => MathF.Max(0f, seconds - RampSeconds);

    public static float FallSpeed(float seconds)
    {
        var speed = Lerp(46f, 82f, Ramp(seconds)) + MathF.Min(30f, Overtime(seconds) * 0.12f);
        return BiomeAt(seconds) switch
        {
            HarvestBiome.Meadow => speed * 0.88f,
            HarvestBiome.Mire => speed * 0.9f,
            _ => speed,
        };
    }

    public static float SpawnGap(float seconds)
    {
        var gap = MathF.Max(0.4f, Lerp(1.1f, 0.52f, Ramp(seconds)) - Overtime(seconds) * 0.0006f);
        return BiomeAt(seconds) == HarvestBiome.Meadow ? gap * 1.1f : gap;
    }

    public static float ReachFactor(float seconds)
    {
        var reach = MathF.Min(1f, Lerp(0.42f, 0.88f, Ramp(seconds)) + Overtime(seconds) * 0.0008f);
        return BiomeAt(seconds) switch
        {
            HarvestBiome.Meadow => reach * 0.9f,
            HarvestBiome.Mire => reach * 0.8f,
            HarvestBiome.Glacier => reach * 0.72f,
            _ => reach,
        };
    }

    public static float HazardGap(float seconds)
    {
        var gap = Lerp(9f, 4.4f, Ramp(seconds));
        return BiomeAt(seconds) == HarvestBiome.Meadow ? gap * 1.35f : gap;
    }

    public static float WarningSeconds(float seconds) => Lerp(1.5f, 1f, Ramp(seconds));

    public static float HazardSpeed(HarvestHazardKind kind, float seconds) => kind == HarvestHazardKind.Bird
        ? Lerp(55f, 85f, Ramp(seconds))
        : Lerp(50f, 66f, Ramp(seconds));

    public static float SparkGap(float seconds) => Lerp(2.6f, 1.7f, Ramp(seconds));

    public static float Wind(float seconds, int firstSign)
    {
        if (BiomeAt(seconds) != HarvestBiome.Highlands)
        {
            return 0f;
        }

        var elapsed = BiomeElapsed(seconds);
        var envelope = MathF.Min(Math.Clamp(elapsed / WindRampInSeconds, 0f, 1f),
            Math.Clamp((BiomeSeconds - elapsed) / WindRampOutSeconds, 0f, 1f));
        var gust = 0.65f + 0.35f * MathF.Sin(elapsed * MathF.Tau / GustPeriod);
        return WindSign(seconds, firstSign) * Lerp(26f, 40f, Ramp(seconds)) * envelope * gust;
    }

    public static int WindSign(float seconds, int firstSign) => LoopAt(seconds) % 2 == 0 ? firstSign : -firstSign;

    public static float PathX(float landX, float drift, float height)
    {
        var share = Math.Clamp(height / SpawnHeight, 0f, 1f);
        return landX + drift * (share + PathWobble * MathF.Sin(share * MathF.PI * 3f));
    }

    public static int Multiplier(int combo)
    {
        var multiplier = 1;
        for (var index = 0; index < MultiplierSteps.Length; index++)
        {
            if (combo >= MultiplierSteps[index])
            {
                multiplier++;
            }
        }

        return Math.Min(MaxMultiplier, multiplier);
    }

    public static int NextMultiplierAt(int combo)
    {
        for (var index = 0; index < MultiplierSteps.Length; index++)
        {
            if (combo < MultiplierSteps[index])
            {
                return MultiplierSteps[index];
            }
        }

        return 0;
    }

    public static int Points(HarvestMaterial material, int multiplier) => MaterialPoints[(int)material] * multiplier;

    public static int BasePoints(HarvestMaterial material) => MaterialPoints[(int)material];

    public static float FeverGain(HarvestMaterial material) => MaterialFever[(int)material];

    public static float AfterMiss(float fever) => MathF.Max(0f, fever - MissFeverLoss);

    public static HarvestMaterial PickMaterial(ref GameRandom random, float seconds)
    {
        var roll = random.NextFloat();
        var shard = ShardBaseChance + ShardRampChance * Ramp(seconds);
        if (roll < shard)
        {
            return HarvestMaterial.Shard;
        }

        if (roll < shard + CrystalChance)
        {
            return HarvestMaterial.Crystal;
        }

        return random.NextFloat() < 0.5f ? HarvestMaterial.Ore : HarvestMaterial.Log;
    }

    public static HarvestMaterial PickBonus(ref GameRandom random)
    {
        var roll = random.NextFloat();
        if (roll < FeverShardChance)
        {
            return HarvestMaterial.Shard;
        }

        if (roll < FeverShardChance + FeverCrystalChance)
        {
            return HarvestMaterial.Crystal;
        }

        return random.NextFloat() < 0.5f ? HarvestMaterial.Ore : HarvestMaterial.Log;
    }

    public static HarvestMaterial PickMirage(ref GameRandom random)
    {
        if (random.NextFloat() < MirageShardChance)
        {
            return HarvestMaterial.Shard;
        }

        return random.NextFloat() < 0.5f ? HarvestMaterial.Crystal : HarvestMaterial.Ore;
    }

    public static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}
