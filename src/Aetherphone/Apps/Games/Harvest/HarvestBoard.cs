using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Harvest;

internal sealed class HarvestBoard
{
    public const float StepSeconds = 1f / 120f;
    public const int DropCapacity = 64;
    public const int HazardCapacity = 4;
    public const int EventCapacity = 48;
    public const float FeverSeconds = 5.5f;
    public const float FeverDropInterval = 0.18f;
    public const float InvulnerableSeconds = 1.6f;
    public const float StunSeconds = 0.8f;
    public const float BirdHeight = 15.5f;
    public const float BirdRadius = 3.2f;
    public const float BoulderRadius = 4f;
    public const float SparkRadius = 2.6f;
    public const float DropRadius = 3.6f;
    private const float MaxCatchUpSeconds = 0.25f;
    private readonly HarvestDrop[] drops = new HarvestDrop[DropCapacity];
    private readonly HarvestHazard[] hazards = new HarvestHazard[HazardCapacity];
    private readonly HarvestEvent[] events = new HarvestEvent[EventCapacity];
    private readonly HarvestSpawner spawner = new();
    private FixedStepClock clock = new(StepSeconds, MaxCatchUpSeconds);
    private HarvestPlayer player = HarvestPlayer.Create(HarvestRules.Width * 0.5f);
    private int eventCount;
    private int serial;
    private long steps;
    private int biomeVisit;
    private float feverClock;
    private float feverSpawnClock;
    private bool jumpQueued;

    public int Score { get; private set; }

    public int Combo { get; private set; }

    public int BestCombo { get; private set; }

    public int Multiplier { get; private set; } = 1;

    public int Misses { get; private set; }

    public int Hits { get; private set; }

    public int Caught { get; private set; }

    public int Fevers { get; private set; }

    public int Dodges { get; private set; }

    public float Fever { get; private set; }

    public float FeverLeft { get; private set; }

    public bool GameOver { get; private set; }

    public bool Started { get; private set; }

    public float Time => steps * StepSeconds;

    public float Alpha => clock.Alpha;

    public bool FeverActive => FeverLeft > 0f;

    public HarvestBiome Biome => HarvestRules.BiomeAt(Time);

    public int BiomeVisit => biomeVisit;

    public int WindSign => spawner.WindSign;

    public float Wind => HarvestRules.Wind(Time, spawner.WindSign);

    public ref readonly HarvestPlayer Player => ref player;

    public ReadOnlySpan<HarvestDrop> Drops => drops;

    public ReadOnlySpan<HarvestHazard> Hazards => hazards;

    public ReadOnlySpan<HarvestEvent> Events => events.AsSpan(0, eventCount);

    public HarvestSpawner Spawner => spawner;

    public void StartGame(GameRandom random)
    {
        spawner.Reset(random);
        clock.Reset();
        player = HarvestPlayer.Create(HarvestRules.Width * 0.5f);
        Array.Clear(drops);
        Array.Clear(hazards);
        eventCount = 0;
        serial = 0;
        steps = 0;
        biomeVisit = 0;
        feverClock = 0f;
        feverSpawnClock = 0f;
        jumpQueued = false;
        Score = 0;
        Combo = 0;
        BestCombo = 0;
        Multiplier = 1;
        Misses = 0;
        Hits = 0;
        Caught = 0;
        Fevers = 0;
        Dodges = 0;
        Fever = 0f;
        FeverLeft = 0f;
        GameOver = false;
        Started = true;
    }

    public void Tick(float deltaSeconds, in HarvestInput input)
    {
        eventCount = 0;
        if (!Started || GameOver)
        {
            return;
        }

        jumpQueued |= input.Jump;
        var count = clock.Advance(deltaSeconds);
        for (var step = 0; step < count && !GameOver; step++)
        {
            var stepInput = new HarvestInput(input.Axis, input.HasTarget, input.TargetX, jumpQueued, input.Duck);
            Step(in stepInput);
            jumpQueued = false;
        }
    }

    public void Step(in HarvestInput input)
    {
        if (GameOver)
        {
            return;
        }

        steps++;
        var now = Time;
        TrackBiome(now);
        AdvanceFever(now);
        Spawn(now);
        MovePlayer(in input, now);
        MoveDrops();
        MoveHazards();
        if (Misses >= HarvestRules.MaxMisses || Hits >= HarvestRules.MaxHits)
        {
            GameOver = true;
            Emit(HarvestEventKind.GameOver, player.X, 0f, Score, 0);
        }
    }

    public int AddDrop(in HarvestPlan plan)
    {
        for (var index = 0; index < DropCapacity; index++)
        {
            if (drops[index].Active)
            {
                continue;
            }

            drops[index] = new HarvestDrop
            {
                X = HarvestRules.PathX(plan.LandX, plan.Drift, plan.Height),
                Y = plan.Height,
                LandX = plan.LandX,
                Drift = plan.Drift,
                Speed = plan.Speed,
                Hold = plan.Hold,
                Seed = (steps % 997) * 0.37f + index,
                Serial = ++serial,
                Material = plan.Material,
                Role = plan.Role,
                Active = true,
            };
            drops[index].PreviousX = drops[index].X;
            drops[index].PreviousY = drops[index].Y;
            return index;
        }

        return -1;
    }

    public int AddHazard(in HarvestHazardPlan plan, float warning)
    {
        for (var index = 0; index < HazardCapacity; index++)
        {
            if (hazards[index].Active)
            {
                continue;
            }

            var x = plan.Direction > 0 ? -HarvestSpawner.HazardEntry : HarvestRules.Width + HarvestSpawner.HazardEntry;
            hazards[index] = new HarvestHazard
            {
                X = x,
                PreviousX = x,
                Height = plan.Kind == HarvestHazardKind.Bird ? BirdHeight : BoulderRadius,
                Velocity = plan.Direction * plan.Speed,
                Warning = warning,
                WarningTotal = MathF.Max(warning, 0.01f),
                Kind = plan.Kind,
                Active = true,
            };
            return index;
        }

        return -1;
    }

    public void PlacePlayer(float x)
    {
        player = HarvestPlayer.Create(Math.Clamp(x, HarvestPlayer.MinX, HarvestPlayer.MaxX));
    }

    public void FillFever(float amount)
    {
        Fever = Math.Clamp(amount, 0f, 1f);
    }

    public static bool Catches(float dropX, float dropY, float playerX, float rim)
    {
        return MathF.Abs(dropX - playerX) <= HarvestPlayer.CatchHalfWidth && dropY <= rim &&
               dropY >= rim - HarvestPlayer.BasketDepth;
    }

    public static bool Touches(Vector2 center, float radius, float playerX, float playerY, float height)
    {
        var left = playerX - HarvestPlayer.HalfWidth;
        var right = playerX + HarvestPlayer.HalfWidth;
        var bottom = playerY + HarvestPlayer.FootClearance;
        var top = playerY + height;
        var nearestX = Math.Clamp(center.X, left, right);
        var nearestY = Math.Clamp(center.Y, bottom, top);
        var offsetX = center.X - nearestX;
        var offsetY = center.Y - nearestY;
        return offsetX * offsetX + offsetY * offsetY <= radius * radius;
    }

    private void TrackBiome(float now)
    {
        var visit = HarvestRules.BiomeVisit(now);
        if (visit == biomeVisit)
        {
            return;
        }

        biomeVisit = visit;
        Emit(HarvestEventKind.BiomeStart, 0f, 0f, HarvestRules.LoopAt(now), (int)HarvestRules.BiomeAt(now));
    }

    private void AdvanceFever(float now)
    {
        if (!FeverActive)
        {
            if (Fever < 1f)
            {
                return;
            }

            StartFever(now);
            return;
        }

        feverClock += StepSeconds;
        feverSpawnClock -= StepSeconds;
        FeverLeft = MathF.Max(0f, FeverLeft - StepSeconds);
        Fever = FeverLeft / FeverSeconds;
        if (FeverLeft <= 0f)
        {
            Fever = 0f;
            spawner.Resume(now, player.X);
            Emit(HarvestEventKind.FeverEnd, player.X, 0f, 0, 0);
            return;
        }

        while (feverSpawnClock <= 0f)
        {
            feverSpawnClock += FeverDropInterval;
            AddDrop(spawner.Bonus(now, feverClock));
        }
    }

    private void StartFever(float now)
    {
        FeverLeft = FeverSeconds;
        feverClock = 0f;
        feverSpawnClock = 0f;
        Fevers++;
        spawner.Suspend(now);
        for (var index = 0; index < DropCapacity; index++)
        {
            ref var drop = ref drops[index];
            if (!drop.Active)
            {
                continue;
            }

            if (drop.Role == HarvestRole.Required)
            {
                drop.Role = HarvestRole.Bonus;
            }
            else if (drop.Role != HarvestRole.Bonus)
            {
                drop.Active = false;
                Emit(HarvestEventKind.Fizzle, drop.X, drop.Y, 0, (int)drop.Role);
            }
        }

        Emit(HarvestEventKind.FeverStart, player.X, 0f, Fevers, 0);
    }

    private void Spawn(float now)
    {
        if (FeverActive)
        {
            return;
        }

        spawner.Advance(now);
        while (spawner.TryTakeDue(now, out var plan))
        {
            AddDrop(in plan);
        }

        while (spawner.TryTakeHazard(now, out var hazardPlan))
        {
            if (AddHazard(in hazardPlan, MathF.Max(0f, hazardPlan.EnterTime - now)) >= 0)
            {
                Emit(HarvestEventKind.HazardWarned, hazardPlan.Direction > 0 ? 0f : HarvestRules.Width, 0f,
                    hazardPlan.Direction, (int)hazardPlan.Kind);
            }
        }
    }

    private void MovePlayer(in HarvestInput input, float now)
    {
        var wasGrounded = player.Grounded;
        var icy = HarvestRules.BiomeAt(now) == HarvestBiome.Glacier;
        if (player.Step(in input, icy, StepSeconds))
        {
            Emit(HarvestEventKind.Jump, player.X, 0f, 0, 0);
        }

        if (!wasGrounded && player.Grounded)
        {
            Emit(HarvestEventKind.Land, player.X, 0f, 0, 0);
        }
    }

    private void MoveDrops()
    {
        for (var index = 0; index < DropCapacity; index++)
        {
            ref var drop = ref drops[index];
            if (!drop.Active)
            {
                continue;
            }

            drop.PreviousX = drop.X;
            drop.PreviousY = drop.Y;
            if (drop.Hold > 0f)
            {
                drop.Hold = MathF.Max(0f, drop.Hold - StepSeconds);
                continue;
            }

            drop.Y -= drop.Speed * StepSeconds;
            drop.X = HarvestRules.PathX(drop.LandX, drop.Drift, drop.Y);
            if (drop.Role == HarvestRole.Spark)
            {
                StepSpark(ref drop);
                continue;
            }

            if (Catches(drop.X, drop.Y, player.X, player.Rim))
            {
                Collect(ref drop);
                continue;
            }

            if (drop.Y > 0f)
            {
                continue;
            }

            drop.Active = false;
            Land(in drop);
        }
    }

    private void StepSpark(ref HarvestDrop drop)
    {
        if (!player.Stunned && Touches(new Vector2(drop.X, drop.Y), SparkRadius, player.X, player.Y, player.Height))
        {
            drop.Active = false;
            player.Stun = StunSeconds;
            Emit(HarvestEventKind.Stunned, drop.X, drop.Y, 0, 0);
            return;
        }

        if (drop.Y > 0f)
        {
            return;
        }

        drop.Active = false;
        Emit(HarvestEventKind.Fizzle, drop.X, 0f, 0, (int)HarvestRole.Spark);
    }

    private void Collect(ref HarvestDrop drop)
    {
        drop.Active = false;
        if (drop.Role == HarvestRole.Mirage)
        {
            Emit(HarvestEventKind.MirageCaught, drop.X, drop.Y, 0, 0, drop.Material);
            return;
        }

        Combo++;
        BestCombo = Math.Max(BestCombo, Combo);
        Caught++;
        var multiplier = HarvestRules.Multiplier(Combo);
        if (multiplier > Multiplier)
        {
            Emit(HarvestEventKind.ComboUp, player.X, player.Rim, multiplier, Combo);
        }

        Multiplier = multiplier;
        var points = HarvestRules.Points(drop.Material, multiplier);
        Score += points;
        if (!FeverActive && drop.Role == HarvestRole.Required)
        {
            Fever = MathF.Min(1f, Fever + HarvestRules.FeverGain(drop.Material));
        }

        Emit(HarvestEventKind.Catch, drop.X, drop.Y, points, (int)MathF.Abs(drop.Seed), drop.Material);
    }

    private void Land(in HarvestDrop drop)
    {
        if (drop.Role != HarvestRole.Required)
        {
            Emit(HarvestEventKind.Fizzle, drop.X, 0f, 0, (int)drop.Role, drop.Material);
            return;
        }

        Misses++;
        BreakCombo();
        Fever = HarvestRules.AfterMiss(Fever);
        Emit(HarvestEventKind.Miss, drop.X, 0f, Misses, 0, drop.Material);
    }

    private void BreakCombo()
    {
        Combo = 0;
        Multiplier = 1;
    }

    private void MoveHazards()
    {
        for (var index = 0; index < HazardCapacity; index++)
        {
            ref var hazard = ref hazards[index];
            if (!hazard.Active)
            {
                continue;
            }

            hazard.PreviousX = hazard.X;
            if (hazard.Warning > 0f)
            {
                hazard.Warning = MathF.Max(0f, hazard.Warning - StepSeconds);
                continue;
            }

            hazard.X += hazard.Velocity * StepSeconds;
            hazard.Spin += hazard.Velocity * StepSeconds / BoulderRadius;
            Collide(ref hazard);
            var gone = hazard.Velocity > 0f
                ? hazard.X > HarvestRules.Width + HarvestSpawner.HazardEntry
                : hazard.X < -HarvestSpawner.HazardEntry;
            if (gone)
            {
                hazard.Active = false;
            }
        }
    }

    private void Collide(ref HarvestHazard hazard)
    {
        if (hazard.Struck)
        {
            return;
        }

        var radius = hazard.Kind == HarvestHazardKind.Bird ? BirdRadius : BoulderRadius;
        var center = new Vector2(hazard.X, hazard.Height);
        if (player.Invulnerable <= 0f && Touches(center, radius, player.X, player.Y, player.Height))
        {
            hazard.Struck = true;
            Hits++;
            BreakCombo();
            player.Invulnerable = InvulnerableSeconds;
            player.Knock(hazard.Velocity > 0f ? 1 : -1);
            Emit(HarvestEventKind.Hit, player.X, player.Y + player.Height * 0.5f, Hits, (int)hazard.Kind);
            return;
        }

        if (hazard.Passed)
        {
            return;
        }

        var behind = hazard.Velocity > 0f
            ? hazard.X - radius > player.X + HarvestPlayer.HalfWidth
            : hazard.X + radius < player.X - HarvestPlayer.HalfWidth;
        if (!behind)
        {
            return;
        }

        hazard.Passed = true;
        if (player.Invulnerable > 0f)
        {
            return;
        }

        Dodges++;
        var points = HarvestRules.DodgePoints * Multiplier;
        Score += points;
        Emit(HarvestEventKind.Dodge, player.X, player.Y + player.Height, points, (int)hazard.Kind);
    }

    private void Emit(HarvestEventKind kind, float x, float y, int value, int detail,
        HarvestMaterial material = HarvestMaterial.Ore)
    {
        if (eventCount >= EventCapacity)
        {
            return;
        }

        events[eventCount++] = new HarvestEvent(kind, x, y, value, detail, material);
    }
}
