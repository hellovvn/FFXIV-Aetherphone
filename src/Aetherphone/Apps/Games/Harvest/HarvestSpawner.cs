using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Harvest;

internal sealed class HarvestSpawner
{
    public const int PlanCapacity = 48;
    public const int SparkCapacity = 8;
    public const float LookaheadSeconds = 6f;
    public const float HazardLeadSeconds = 7.5f;
    public const float FirstLandTime = 3.4f;
    public const float FirstBirdTime = 10f;
    public const float SparkHoldSeconds = 0.7f;
    public const float SparkClearance = 16f;
    public const float Margin = 7f;
    public const float CatchSlack = 5f;
    public const float SparkHeight = 156f;
    public const float HazardEntry = 10f;
    public const float HazardReachFactor = 0.6f;
    public const float FeverSweepSpeed = 1.7f;
    public const float FeverSweepAmplitude = 38f;
    public const float FeverJitter = 9f;
    public const float ResumeDelaySeconds = 1.2f;
    private const float HazardWindowPad = 0.3f;
    private const float SparkOffsetMin = 20f;
    private const float SparkOffsetMax = 42f;
    private const float SparkDelayMin = 0.12f;
    private const float SparkDelayMax = 0.35f;
    private const float SparkLookBack = 0.2f;
    private const float SparkLookAhead = 0.4f;
    private const float SparkPrune = 2f;
    private const int PickAttempts = 10;
    private readonly HarvestPlan[] plans = new HarvestPlan[PlanCapacity];
    private readonly float[] sparkLandTimes = new float[SparkCapacity];
    private readonly float[] sparkXs = new float[SparkCapacity];
    private GameRandom random;
    private HarvestHazardPlan hazard;
    private int planCount;
    private int sparkCount;
    private int hazardsPlanned;
    private int sameKindRun;
    private float lastLandTime;
    private float lastLandX;
    private float nextSparkTime;
    private float nextHazardEnter;
    private float lastHazardEnter;
    private float lastHazardExit;
    private bool hazardPlanned;
    private bool suspended;

    public int WindSign { get; private set; } = 1;

    public int PlanCount => planCount;

    public bool HazardPending => hazardPlanned;

    public HarvestHazardPlan PendingHazard => hazard;

    public float LastLandTime => lastLandTime;

    public float LastLandX => lastLandX;

    public ReadOnlySpan<HarvestPlan> Plans => plans.AsSpan(0, planCount);

    public static float CrossDistance => HarvestRules.Width + HazardEntry * 2f;

    public static float FallSeconds(float speed, float height = HarvestRules.SpawnHeight) => height / speed;

    public void Reset(GameRandom seeded)
    {
        random = seeded;
        planCount = 0;
        sparkCount = 0;
        hazardsPlanned = 0;
        sameKindRun = 0;
        hazardPlanned = false;
        suspended = false;
        WindSign = random.Sign();
        lastLandTime = FirstLandTime - HarvestRules.SpawnGap(0f);
        lastLandX = HarvestRules.Width * 0.5f;
        nextSparkTime = 0f;
        nextHazardEnter = FirstBirdTime;
        lastHazardEnter = -1f;
        lastHazardExit = -1f;
    }

    public void Advance(float now)
    {
        if (suspended)
        {
            return;
        }

        if (!hazardPlanned && now >= nextHazardEnter - HazardLeadSeconds)
        {
            PlanHazard();
        }

        PruneSparks(now);
        while (planCount < PlanCapacity - 3 && lastLandTime < now + LookaheadSeconds)
        {
            PlanNext(now);
        }
    }

    public bool TryTakeDue(float now, out HarvestPlan plan)
    {
        var found = -1;
        for (var index = 0; index < planCount; index++)
        {
            if (plans[index].SpawnTime > now)
            {
                continue;
            }

            if (found < 0 || plans[index].SpawnTime < plans[found].SpawnTime)
            {
                found = index;
            }
        }

        if (found < 0)
        {
            plan = default;
            return false;
        }

        plan = plans[found];
        plans[found] = plans[--planCount];
        return true;
    }

    public bool TryTakeHazard(float now, out HarvestHazardPlan taken)
    {
        if (suspended || !hazardPlanned || now < hazard.WarnTime)
        {
            taken = default;
            return false;
        }

        taken = hazard;
        hazardPlanned = false;
        lastHazardEnter = hazard.EnterTime;
        lastHazardExit = hazard.EnterTime + CrossDistance / hazard.Speed;
        nextHazardEnter = lastHazardExit + HarvestRules.HazardGap(hazard.EnterTime) * random.Range(0.8f, 1.2f);
        return true;
    }

    public void Suspend(float now)
    {
        suspended = true;
        planCount = 0;
        sparkCount = 0;
        if (hazardPlanned)
        {
            hazardPlanned = false;
            hazardsPlanned--;
        }
    }

    public void Resume(float now, float playerX)
    {
        suspended = false;
        lastLandTime = now + ResumeDelaySeconds;
        lastLandX = Math.Clamp(playerX, Margin, HarvestRules.Width - Margin);
        nextHazardEnter = MathF.Max(nextHazardEnter, now + HazardLeadSeconds * 0.5f);
        nextSparkTime = MathF.Max(nextSparkTime, now + ResumeDelaySeconds);
    }

    public HarvestPlan Bonus(float now, float feverElapsed)
    {
        var center = HarvestRules.Width * 0.5f + FeverSweepAmplitude * MathF.Sin(feverElapsed * FeverSweepSpeed);
        var x = Math.Clamp(center + random.Range(-FeverJitter, FeverJitter), Margin, HarvestRules.Width - Margin);
        var speed = HarvestRules.FallSpeed(now) * HarvestRules.FeverSpeedFactor;
        return new HarvestPlan
        {
            SpawnTime = now,
            LandTime = now + FallSeconds(speed),
            LandX = x,
            Height = HarvestRules.SpawnHeight,
            Drift = HarvestRules.Wind(now, WindSign),
            Speed = speed,
            Material = HarvestRules.PickBonus(ref random),
            Role = HarvestRole.Bonus,
        };
    }

    public float HazardFactor(float landTime)
    {
        if (Within(landTime, lastHazardEnter, lastHazardExit))
        {
            return HazardReachFactor;
        }

        if (!hazardPlanned)
        {
            return 1f;
        }

        var exit = hazard.EnterTime + CrossDistance / hazard.Speed;
        return Within(landTime, hazard.EnterTime, exit) ? HazardReachFactor : 1f;
    }

    private static bool Within(float time, float start, float end) =>
        end > start && time >= start - HazardWindowPad && time <= end + HazardWindowPad;

    private void PlanHazard()
    {
        var enter = nextHazardEnter;
        HarvestHazardKind kind;
        if (hazardsPlanned == 0)
        {
            kind = HarvestHazardKind.Bird;
        }
        else if (hazardsPlanned == 1)
        {
            kind = HarvestHazardKind.Boulder;
        }
        else
        {
            kind = random.Chance(0.5f) ? HarvestHazardKind.Bird : HarvestHazardKind.Boulder;
            if (sameKindRun >= 2 && kind == hazard.Kind)
            {
                kind = kind == HarvestHazardKind.Bird ? HarvestHazardKind.Boulder : HarvestHazardKind.Bird;
            }
        }

        sameKindRun = hazardsPlanned > 0 && kind == hazard.Kind ? sameKindRun + 1 : 1;
        hazard = new HarvestHazardPlan
        {
            WarnTime = enter - HarvestRules.WarningSeconds(enter),
            EnterTime = enter,
            Speed = HarvestRules.HazardSpeed(kind, enter),
            Direction = random.Sign(),
            Kind = kind,
        };
        hazardPlanned = true;
        hazardsPlanned++;
    }

    private void PlanNext(float now)
    {
        var gap = HarvestRules.SpawnGap(lastLandTime) * random.Range(0.85f, 1.15f);
        var landTime = lastLandTime + gap;
        var material = HarvestRules.PickMaterial(ref random, landTime);
        var speed = HarvestRules.FallSpeed(landTime) * (material == HarvestMaterial.Shard ? HarvestRules.ShardSpeedFactor : 1f);
        var fall = FallSeconds(speed);
        var spawnTime = landTime - fall;
        if (spawnTime < now)
        {
            spawnTime = now;
            landTime = now + fall;
            gap = landTime - lastLandTime;
        }

        var previousTime = lastLandTime;
        var previousX = lastLandX;
        var x = PickRequiredX(landTime, gap);
        Add(new HarvestPlan
        {
            SpawnTime = spawnTime,
            LandTime = landTime,
            LandX = x,
            Height = HarvestRules.SpawnHeight,
            Drift = HarvestRules.Wind(spawnTime, WindSign),
            Speed = speed,
            Material = material,
            Role = HarvestRole.Required,
        });
        lastLandTime = landTime;
        lastLandX = x;
        PlanSpark(now, landTime, x, previousX);
        PlanMirage(now, previousTime, landTime);
    }

    private float PickRequiredX(float landTime, float gap)
    {
        var reach = HarvestRules.ReachFactor(landTime) * HarvestPlayer.MaxSpeed * gap * HazardFactor(landTime) +
                    CatchSlack;
        var low = MathF.Max(Margin, lastLandX - reach);
        var high = MathF.Min(HarvestRules.Width - Margin, lastLandX + reach);
        for (var attempt = 0; attempt < PickAttempts; attempt++)
        {
            var candidate = random.Range(low, high);
            if (ClearOfSparks(candidate, landTime))
            {
                return candidate;
            }
        }

        return lastLandX;
    }

    private bool ClearOfSparks(float x, float landTime)
    {
        for (var index = 0; index < sparkCount; index++)
        {
            var sparkTime = sparkLandTimes[index];
            if (sparkTime < lastLandTime - SparkLookBack || sparkTime > landTime + SparkLookAhead)
            {
                continue;
            }

            var sparkX = sparkXs[index];
            if (MathF.Abs(x - sparkX) < SparkClearance || (x - sparkX) * (lastLandX - sparkX) < 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void PlanSpark(float now, float landTime, float x, float previousX)
    {
        if (HarvestRules.BiomeAt(landTime) != HarvestBiome.Storm || landTime < nextSparkTime ||
            sparkCount >= SparkCapacity || planCount >= PlanCapacity - 1)
        {
            return;
        }

        var side = random.Sign();
        var offset = random.Range(SparkOffsetMin, SparkOffsetMax);
        var sparkX = 0f;
        var placed = false;
        for (var attempt = 0; attempt < 2 && !placed; attempt++)
        {
            var candidate = x + side * offset;
            side = -side;
            if (candidate < Margin || candidate > HarvestRules.Width - Margin)
            {
                continue;
            }

            if ((candidate - x) * (candidate - previousX) < 0f || MathF.Abs(candidate - previousX) < SparkClearance)
            {
                continue;
            }

            sparkX = candidate;
            placed = true;
        }

        if (!placed)
        {
            return;
        }

        var sparkLand = landTime + random.Range(SparkDelayMin, SparkDelayMax);
        var speed = HarvestRules.FallSpeed(sparkLand);
        var spawnTime = MathF.Max(now, sparkLand - FallSeconds(speed, SparkHeight) - SparkHoldSeconds);
        Add(new HarvestPlan
        {
            SpawnTime = spawnTime,
            LandTime = sparkLand,
            LandX = sparkX,
            Height = SparkHeight,
            Speed = speed,
            Hold = SparkHoldSeconds,
            Material = HarvestMaterial.Crystal,
            Role = HarvestRole.Spark,
        });
        sparkLandTimes[sparkCount] = sparkLand;
        sparkXs[sparkCount] = sparkX;
        sparkCount++;
        nextSparkTime = landTime + HarvestRules.SparkGap(landTime) * random.Range(0.85f, 1.2f);
    }

    private void PlanMirage(float now, float previousTime, float landTime)
    {
        if (HarvestRules.BiomeAt(landTime) != HarvestBiome.Dunes || planCount >= PlanCapacity - 1 ||
            !random.Chance(HarvestRules.MirageChance))
        {
            return;
        }

        var mirageLand = (previousTime + landTime) * 0.5f;
        var speed = HarvestRules.FallSpeed(mirageLand);
        Add(new HarvestPlan
        {
            SpawnTime = MathF.Max(now, mirageLand - FallSeconds(speed)),
            LandTime = mirageLand,
            LandX = random.Range(Margin, HarvestRules.Width - Margin),
            Height = HarvestRules.SpawnHeight,
            Speed = speed,
            Material = HarvestRules.PickMirage(ref random),
            Role = HarvestRole.Mirage,
        });
    }

    private void PruneSparks(float now)
    {
        for (var index = sparkCount - 1; index >= 0; index--)
        {
            if (sparkLandTimes[index] >= now - SparkPrune)
            {
                continue;
            }

            sparkCount--;
            sparkLandTimes[index] = sparkLandTimes[sparkCount];
            sparkXs[index] = sparkXs[sparkCount];
        }
    }

    private void Add(in HarvestPlan plan)
    {
        if (planCount >= PlanCapacity)
        {
            return;
        }

        plans[planCount++] = plan;
    }
}
