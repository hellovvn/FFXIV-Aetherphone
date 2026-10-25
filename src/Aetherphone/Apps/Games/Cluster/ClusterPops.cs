using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Cluster;

internal sealed class ClusterPops
{
    public const float PopSeconds = 0.24f;
    public const float RippleStep = 0.022f;
    public const float MaxRipple = 0.26f;
    private const int Capacity = ClusterBoard.MaxCells * 2;
    private const float SwellShare = 0.3f;
    private const float Swell = 0.28f;

    private struct Pop
    {
        public Vector2 Center;
        public float Half;
        public float Delay;
        public float Age;
        public byte Color;
        public bool Burst;
    }

    private readonly Pop[] pops = new Pop[Capacity];
    private int count;

    public int Count => count;

    public void Clear()
    {
        count = 0;
    }

    public void Add(Vector2 center, float half, byte color, float delay)
    {
        if (count >= Capacity)
        {
            return;
        }

        ref var pop = ref pops[count++];
        pop.Center = center;
        pop.Half = half;
        pop.Delay = delay;
        pop.Age = 0f;
        pop.Color = color;
        pop.Burst = false;
    }

    public void Update(float deltaSeconds, ParticleSystem particles, int burstCount)
    {
        for (var index = count - 1; index >= 0; index--)
        {
            ref var pop = ref pops[index];
            pop.Age += deltaSeconds;
            if (!pop.Burst && pop.Age >= pop.Delay)
            {
                pop.Burst = true;
                var color = ClusterRenderer.ColorOf(pop.Color);
                particles.Burst(pop.Center, burstCount, color, pop.Half * 7f, 2.6f, 0.45f, 380f, MathF.PI * 2f, 0f,
                    ParticleShape.Square);
                particles.Sparkle(pop.Center, 1, GamePalette.Lighten(color, 0.5f), pop.Half * 4f, 2.2f, 0.5f);
            }

            if (pop.Age < pop.Delay + PopSeconds)
            {
                continue;
            }

            pops[index] = pops[count - 1];
            count--;
        }
    }

    public void Draw(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < count; index++)
        {
            ref readonly var pop = ref pops[index];
            if (pop.Age < pop.Delay)
            {
                var anticipation = pop.Delay <= 0f ? 1f : pop.Age / pop.Delay;
                ClusterRenderer.DrawPiece(drawList, pop.Center, pop.Half * (1f + 0.06f * anticipation), pop.Color, 1f,
                    anticipation, scale);
                continue;
            }

            var progress = (pop.Age - pop.Delay) / PopSeconds;
            var size = progress < SwellShare
                ? 1f + Swell * (progress / SwellShare)
                : (1f + Swell) * (1f - (progress - SwellShare) / (1f - SwellShare));
            var alpha = 1f - progress * progress;
            var tint = ClusterRenderer.ColorOf(pop.Color);
            var flash = Vector4.Lerp(tint, new Vector4(1f, 1f, 1f, 1f), 0.6f * (1f - progress));
            ClusterRenderer.DrawPiece(drawList, pop.Center, pop.Half * MathF.Max(0f, size), flash, pop.Color, alpha, 1f,
                scale);
            drawList.AddCircle(pop.Center, pop.Half * (1.1f + 0.9f * progress),
                ImGui.GetColorU32(GamePalette.Lighten(tint, 0.4f) with { W = 0.6f * (1f - progress) }), 24,
                MathF.Max(1f, 2f * scale * (1f - progress)));
        }
    }
}
