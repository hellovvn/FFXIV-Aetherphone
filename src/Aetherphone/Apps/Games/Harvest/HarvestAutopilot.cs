namespace Aetherphone.Apps.Games.Harvest;

internal static class HarvestAutopilot
{
    private const float DuckLead = 0.4f;
    private const float JumpLead = 0.1f;
    private const float SightLoss = 110f;
    private const float AimShare = 0.55f;
    private const float PassedReach = 18f;

    public static HarvestInput Think(HarvestBoard board, float skill)
    {
        ref readonly var player = ref board.Player;
        var sight = HarvestRules.SpawnHeight - (1f - Math.Clamp(skill, 0f, 1f)) * SightLoss;
        var targetX = player.X;
        var bestTime = float.MaxValue;
        var drops = board.Drops;
        for (var index = 0; index < drops.Length; index++)
        {
            ref readonly var drop = ref drops[index];
            if (!drop.Active || drop.Role is HarvestRole.Mirage or HarvestRole.Spark || drop.Y > sight)
            {
                continue;
            }

            var rise = drop.Y - player.Rim;
            if (rise < -HarvestPlayer.BasketDepth)
            {
                continue;
            }

            var seconds = drop.Hold + MathF.Max(0f, rise) / drop.Speed;
            var landing = HarvestRules.PathX(drop.LandX, drop.Drift, MathF.Max(0f, player.Rim));
            var distance = MathF.Abs(landing - player.X) - HarvestPlayer.CatchHalfWidth * AimShare;
            if (distance > HarvestPlayer.MaxSpeed * seconds || seconds >= bestTime)
            {
                continue;
            }

            bestTime = seconds;
            targetX = landing;
        }

        if (board.Biome == HarvestBiome.Glacier)
        {
            targetX -= player.VelocityX * MathF.Abs(player.VelocityX) / (2f * HarvestPlayer.IceAcceleration);
        }

        var duck = false;
        var jump = false;
        var hazards = board.Hazards;
        for (var index = 0; index < hazards.Length; index++)
        {
            ref readonly var hazard = ref hazards[index];
            if (!hazard.Active || hazard.Warning > 0f || hazard.Struck)
            {
                continue;
            }

            var heading = MathF.Sign(hazard.Velocity);
            var gap = (player.X - hazard.X) * heading - HarvestPlayer.HalfWidth - HarvestBoard.BoulderRadius;
            var closing = MathF.Max(1f, MathF.Abs(hazard.Velocity) - player.VelocityX * heading);
            var seconds = gap / closing;
            if (hazard.Kind == HarvestHazardKind.Bird)
            {
                duck |= seconds < DuckLead && gap > -PassedReach;
                continue;
            }

            jump |= seconds >= 0f && seconds < JumpLead && player.Grounded;
        }

        return HarvestInput.Pointer(targetX, jump, duck);
    }
}
