using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Harvest;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HarvestBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void ACaughtDropScoresItsPointsAndStartsACombo()
    {
        var board = Fresh();
        board.AddDrop(Drop(50f, 30f, HarvestMaterial.Crystal));
        var events = Run(board, 0.6f, HarvestInput.None);
        Assert.Equal(1, board.Caught);
        Assert.Equal(1, board.Combo);
        Assert.Equal(HarvestRules.BasePoints(HarvestMaterial.Crystal), board.Score);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.Catch && item.Value == board.Score &&
                                        item.Material == HarvestMaterial.Crystal);
    }

    [Fact]
    public void ADropThatReachesTheGroundIsAMiss()
    {
        var board = Fresh();
        board.AddDrop(Drop(90f, 20f, HarvestMaterial.Ore));
        var events = Run(board, 0.6f, HarvestInput.None);
        Assert.Equal(1, board.Misses);
        Assert.Equal(0, board.Score);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.Miss);
    }

    [Fact]
    public void TheBasketCatchesWithinItsWidthAndDepthOnly()
    {
        Assert.True(HarvestBoard.Catches(50f, 13f, 50f, 13f));
        Assert.True(HarvestBoard.Catches(50f + HarvestPlayer.CatchHalfWidth, 12f, 50f, 13f));
        Assert.False(HarvestBoard.Catches(50f + HarvestPlayer.CatchHalfWidth + 0.5f, 12f, 50f, 13f));
        Assert.False(HarvestBoard.Catches(50f, 14f, 50f, 13f));
        Assert.False(HarvestBoard.Catches(50f, 13f - HarvestPlayer.BasketDepth - 0.5f, 50f, 13f));
    }

    [Fact]
    public void CatchingClimbsTheMultiplierAndAMissResetsIt()
    {
        var board = Fresh();
        for (var drop = 0; drop < 5; drop++)
        {
            board.AddDrop(Drop(50f, 20f + drop * 6f, HarvestMaterial.Ore));
        }

        var climb = Run(board, 1.2f, HarvestInput.None);
        Assert.Equal(5, board.Combo);
        Assert.Equal(2, board.Multiplier);
        Assert.Equal(4 * 10 + 1 * 20, board.Score);
        Assert.Contains(climb, item => item.Kind == HarvestEventKind.ComboUp && item.Value == 2);
        board.AddDrop(Drop(90f, 20f, HarvestMaterial.Ore));
        Run(board, 0.6f, HarvestInput.None);
        Assert.Equal(0, board.Combo);
        Assert.Equal(1, board.Multiplier);
        Assert.Equal(5, board.BestCombo);
    }

    [Fact]
    public void BonusAndMirageDropsNeverCountAsMisses()
    {
        var board = Fresh();
        board.AddDrop(Drop(90f, 20f, HarvestMaterial.Shard, HarvestRole.Bonus));
        board.AddDrop(Drop(10f, 20f, HarvestMaterial.Shard, HarvestRole.Mirage));
        Run(board, 0.6f, HarvestInput.None);
        Assert.Equal(0, board.Misses);
    }

    [Fact]
    public void CatchingAMirageScoresNothing()
    {
        var board = Fresh();
        board.AddDrop(Drop(50f, 25f, HarvestMaterial.Shard, HarvestRole.Mirage));
        var events = Run(board, 0.6f, HarvestInput.None);
        Assert.Equal(0, board.Score);
        Assert.Equal(0, board.Caught);
        Assert.Equal(0, board.Combo);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.MirageCaught);
    }

    [Fact]
    public void ThreeMissesEndTheRun()
    {
        var board = Fresh();
        board.AddDrop(Drop(90f, 20f, HarvestMaterial.Ore));
        board.AddDrop(Drop(85f, 24f, HarvestMaterial.Log));
        board.AddDrop(Drop(5f, 22f, HarvestMaterial.Crystal));
        var events = Run(board, 1f, HarvestInput.None);
        Assert.Equal(HarvestRules.MaxMisses, board.Misses);
        Assert.True(board.GameOver);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.GameOver);
    }

    [Fact]
    public void ABirdStrikesAStandingChocoboButPassesOverADuckingOne()
    {
        var standing = Fresh();
        standing.AddHazard(Hazard(HarvestHazardKind.Bird, 1, 80f), 0f);
        Run(standing, 2f, HarvestInput.None);
        Assert.Equal(1, standing.Hits);
        var ducking = Fresh();
        ducking.AddHazard(Hazard(HarvestHazardKind.Bird, 1, 80f), 0f);
        var events = Run(ducking, 2f, HarvestInput.Keys(0f, false, true));
        Assert.Equal(0, ducking.Hits);
        Assert.Equal(1, ducking.Dodges);
        Assert.Equal(HarvestRules.DodgePoints, ducking.Score);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.Dodge);
    }

    [Fact]
    public void AJumpingChocoboHitsABirdItCouldHaveDucked()
    {
        var board = Fresh();
        board.AddHazard(Hazard(HarvestHazardKind.Bird, -1, 80f), 0f);
        var jumped = false;
        var elapsed = 0f;
        while (elapsed < 2f)
        {
            var hazard = board.Hazards[0];
            var close = MathF.Abs(hazard.X - board.Player.X) < 18f;
            board.Tick(Frame, HarvestInput.Keys(0f, close && !jumped, false));
            jumped |= close;
            elapsed += Frame;
        }

        Assert.Equal(1, board.Hits);
    }

    [Fact]
    public void ABoulderStrikesAGroundedChocoboButNotAJumpingOne()
    {
        var grounded = Fresh();
        grounded.AddHazard(Hazard(HarvestHazardKind.Boulder, 1, 55f), 0f);
        Run(grounded, 2.5f, HarvestInput.None);
        Assert.Equal(1, grounded.Hits);
        var jumping = Fresh();
        jumping.AddHazard(Hazard(HarvestHazardKind.Boulder, 1, 55f), 0f);
        var elapsed = 0f;
        var dodged = false;
        while (elapsed < 2.5f)
        {
            var hazard = jumping.Hazards[0];
            var gap = jumping.Player.X - HarvestPlayer.HalfWidth - (hazard.X + HarvestBoard.BoulderRadius);
            var jump = hazard.Active && gap > 0f && gap < 55f * 0.1f;
            jumping.Tick(Frame, HarvestInput.Keys(0f, jump, false));
            dodged |= jumping.Events.ToArray().Any(item => item.Kind == HarvestEventKind.Dodge);
            elapsed += Frame;
        }

        Assert.Equal(0, jumping.Hits);
        Assert.True(dodged);
    }

    [Fact]
    public void AHitGrantsInvulnerabilityAgainstTheNextHazard()
    {
        var board = Fresh();
        board.AddHazard(Hazard(HarvestHazardKind.Bird, 1, 80f), 0f);
        board.AddHazard(Hazard(HarvestHazardKind.Bird, 1, 80f), 0.3f);
        Run(board, 2.5f, HarvestInput.None);
        Assert.Equal(1, board.Hits);
        Assert.Equal(0, board.Dodges);
    }

    [Fact]
    public void ThreeHitsEndTheRun()
    {
        var board = Fresh();
        for (var hazard = 0; hazard < HarvestRules.MaxHits; hazard++)
        {
            board.AddHazard(Hazard(HarvestHazardKind.Bird, 1, 80f), hazard * (HarvestBoard.InvulnerableSeconds + 0.3f));
        }

        Run(board, 8f, HarvestInput.None);
        Assert.Equal(HarvestRules.MaxHits, board.Hits);
        Assert.True(board.GameOver);
    }

    [Fact]
    public void ASparkStunsTheChocoboWithoutCostingALife()
    {
        var board = Fresh();
        board.AddDrop(new HarvestPlan
        {
            LandX = 50f,
            Height = 22f,
            Speed = 60f,
            Material = HarvestMaterial.Crystal,
            Role = HarvestRole.Spark,
        });
        var events = Run(board, 0.3f, HarvestInput.None);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.Stunned);
        Assert.True(board.Player.Stunned);
        var before = board.Player.X;
        Run(board, 0.3f, HarvestInput.Keys(1f, true, false));
        Assert.Equal(before, board.Player.X);
        Assert.True(board.Player.Grounded);
        Run(board, HarvestBoard.StunSeconds, HarvestInput.Keys(1f, false, false));
        Assert.True(board.Player.X > before);
        Assert.Equal(0, board.Hits);
        Assert.Equal(0, board.Misses);
        Assert.Equal(0, board.Combo);
    }

    [Fact]
    public void AFullMeterStartsTheFeverAndTurnsWaitingDropsIntoBonus()
    {
        var board = Fresh();
        board.FillFever(0.99f);
        board.AddDrop(Drop(90f, 120f, HarvestMaterial.Ore));
        board.AddDrop(Drop(50f, 20f, HarvestMaterial.Crystal));
        var events = Run(board, 0.4f, HarvestInput.None);
        Assert.Contains(events, item => item.Kind == HarvestEventKind.FeverStart);
        Assert.True(board.FeverActive);
        Assert.Equal(1, board.Fevers);
        var bonus = 0;
        foreach (var drop in board.Drops.ToArray())
        {
            Assert.True(!drop.Active || drop.Role == HarvestRole.Bonus);
            bonus += drop.Active ? 1 : 0;
        }

        Assert.True(bonus > 1);
        var rest = Run(board, HarvestBoard.FeverSeconds + 3f, HarvestInput.None);
        Assert.Contains(rest, item => item.Kind == HarvestEventKind.FeverEnd);
        Assert.False(board.FeverActive);
        Assert.Equal(0, board.Misses);
        Assert.Equal(0f, board.Fever);
    }

    [Fact]
    public void BonusDropsDoNotRefillTheMeter()
    {
        var board = Fresh();
        board.AddDrop(Drop(50f, 20f, HarvestMaterial.Shard, HarvestRole.Bonus));
        Run(board, 0.5f, HarvestInput.None);
        Assert.Equal(1, board.Caught);
        Assert.Equal(0f, board.Fever);
        board.AddDrop(Drop(50f, 20f, HarvestMaterial.Shard));
        Run(board, 0.5f, HarvestInput.None);
        Assert.Equal(HarvestRules.FeverGain(HarvestMaterial.Shard), board.Fever, 4);
    }

    [Fact]
    public void TheLandChangesEveryThirtySeconds()
    {
        var board = Fresh(4UL);
        var biomes = new List<int>();
        var elapsed = 0f;
        while (elapsed < 95f && !board.GameOver)
        {
            board.Tick(Frame, HarvestAutopilot.Think(board, 1f));
            biomes.AddRange(board.Events.ToArray().Where(item => item.Kind == HarvestEventKind.BiomeStart)
                .Select(item => item.Detail));
            elapsed += Frame;
        }

        Assert.False(board.GameOver);
        Assert.Equal(new[] { 1, 2, 3 }, biomes);
        Assert.Equal(HarvestBiome.Glacier, board.Biome);
    }

    [Fact]
    public void TheSameSeedReplaysIdentically()
    {
        var first = Replay(11UL, 150f, out var firstHash);
        var second = Replay(11UL, 150f, out var secondHash);
        Assert.Equal(firstHash, secondHash);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Caught, second.Caught);
        Assert.Equal(first.Misses, second.Misses);
        Assert.Equal(first.Hits, second.Hits);
        Assert.Equal(first.Time, second.Time);
        Replay(12UL, 150f, out var otherHash);
        Assert.NotEqual(firstHash, otherHash);
    }

    [Fact]
    public void AGoodRunHarvestsForMinutesButNotForever()
    {
        var times = new List<float>();
        for (var seed = 1UL; seed <= 8UL; seed++)
        {
            var board = Replay(seed, 900f, out _);
            Assert.True(board.GameOver);
            times.Add(board.Time);
        }

        times.Sort();
        var median = (times[3] + times[4]) * 0.5f;
        Assert.InRange(median, 90f, 400f);
    }

    [Fact]
    public void IceKeepsTheChocoboSliding()
    {
        var grass = HarvestPlayer.Create(30f);
        var ice = HarvestPlayer.Create(30f);
        grass.VelocityX = HarvestPlayer.MaxSpeed;
        ice.VelocityX = HarvestPlayer.MaxSpeed;
        for (var step = 0; step < 24; step++)
        {
            grass.Step(HarvestInput.None, false, HarvestBoard.StepSeconds);
            ice.Step(HarvestInput.None, true, HarvestBoard.StepSeconds);
        }

        Assert.Equal(0f, grass.VelocityX);
        Assert.True(ice.VelocityX > 0f);
        Assert.True(ice.X > grass.X + 2f);
    }

    [Fact]
    public void DuckingSlowsTheChocoboAndLowersTheBasket()
    {
        var player = HarvestPlayer.Create(50f);
        for (var step = 0; step < 60; step++)
        {
            player.Step(HarvestInput.Keys(1f, false, true), false, HarvestBoard.StepSeconds);
        }

        Assert.Equal(HarvestPlayer.MaxSpeed * HarvestPlayer.DuckSpeedFactor, player.VelocityX, 3);
        Assert.Equal(HarvestPlayer.DuckHeight, player.Height);
        Assert.Equal(HarvestPlayer.DuckRim, player.Rim);
    }

    [Fact]
    public void AJumpPressedJustBeforeLandingStillJumps()
    {
        var player = HarvestPlayer.Create(50f);
        player.Step(HarvestInput.Keys(0f, true, false), false, HarvestBoard.StepSeconds);
        Assert.False(player.Grounded);
        var pressed = false;
        var jumpedAgain = false;
        for (var step = 0; step < 240 && !jumpedAgain; step++)
        {
            var press = !pressed && player.VelocityY < 0f && player.Y < 3f;
            pressed |= press;
            var landed = player.Step(HarvestInput.Keys(0f, press, false), false, HarvestBoard.StepSeconds);
            jumpedAgain = pressed && landed;
        }

        Assert.True(jumpedAgain);
    }

    [Fact]
    public void AJumpClearsABoulderAndTheBirdFliesAboveADuck()
    {
        var apex = HarvestPlayer.JumpVelocity * HarvestPlayer.JumpVelocity / (2f * HarvestPlayer.Gravity);
        Assert.True(apex > HarvestBoard.BoulderRadius * 2f + HarvestPlayer.FootClearance);
        Assert.True(HarvestBoard.BirdHeight - HarvestBoard.BirdRadius > HarvestPlayer.DuckHeight + 1f);
        Assert.True(HarvestBoard.BirdHeight - HarvestBoard.BirdRadius < HarvestPlayer.StandHeight);
        Assert.True(HarvestBoard.BirdHeight + HarvestBoard.BirdRadius > apex + HarvestPlayer.FootClearance);
    }

    private static HarvestBoard Fresh(ulong seed = 1UL)
    {
        var board = new HarvestBoard();
        board.StartGame(GameRandom.FromSeed(seed));
        board.PlacePlayer(50f);
        return board;
    }

    private static HarvestPlan Drop(float x, float height, HarvestMaterial material,
        HarvestRole role = HarvestRole.Required) => new()
    {
        LandX = x,
        Height = height,
        Speed = 50f,
        Material = material,
        Role = role,
    };

    private static HarvestHazardPlan Hazard(HarvestHazardKind kind, int direction, float speed) => new()
    {
        Kind = kind,
        Direction = direction,
        Speed = speed,
    };

    private static List<HarvestEvent> Run(HarvestBoard board, float seconds, HarvestInput input)
    {
        var events = new List<HarvestEvent>();
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            board.Tick(Frame, input);
            events.AddRange(board.Events.ToArray());
            elapsed += Frame;
        }

        return events;
    }

    private static HarvestBoard Replay(ulong seed, float seconds, out long hash)
    {
        var board = new HarvestBoard();
        board.StartGame(GameRandom.FromSeed(seed));
        hash = 17;
        var elapsed = 0f;
        while (elapsed < seconds && !board.GameOver)
        {
            board.Tick(Frame, HarvestAutopilot.Think(board, 1f));
            foreach (var item in board.Events.ToArray())
            {
                hash = hash * 31 + (long)item.Kind * 1009 + item.Value + (long)(item.X * 100f);
            }

            elapsed += Frame;
        }

        return board;
    }
}
