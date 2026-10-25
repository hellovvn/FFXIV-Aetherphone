using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Cluster;

internal struct ClusterMotion
{
    public Vector2 From;
    public float Age;
    public float DelayY;
    public float DurationY;
    public float DelayX;
    public float DurationX;
    public float Swirl;
    public bool Fly;

    public readonly Vector2 Offset
    {
        get
        {
            if (From == Vector2.Zero)
            {
                return Vector2.Zero;
            }

            if (Fly)
            {
                var eased = Easing.EaseInOutCubic(Progress(Age, DelayY, DurationY));
                var normal = new Vector2(-From.Y, From.X);
                return From * (1f - eased) + normal * (Swirl * MathF.Sin(MathF.PI * eased));
            }

            return new Vector2(Settle(From.X, Progress(Age, DelayX, DurationX)),
                Settle(From.Y, Progress(Age, DelayY, DurationY)));
        }
    }

    private static float Progress(float age, float delay, float duration) =>
        duration <= 0f ? 1f : Easing.Clamp01((age - delay) / duration);

    private static float Settle(float distance, float progress)
    {
        if (distance == 0f || progress >= 1f)
        {
            return 0f;
        }

        var eased = GameJuice.PopIn(progress);
        return eased <= 1f ? distance * (1f - eased) : -(eased - 1f) * Math.Clamp(distance, -1f, 1f);
    }
}

internal sealed class ClusterMotions
{
    public const float FallDelay = 0.05f;
    public const float RowStagger = 0.012f;
    public const float FallBase = 0.16f;
    public const float FallPerRoot = 0.06f;
    public const float SlideSeconds = 0.2f;
    public const float FlySeconds = 0.42f;
    public const float FlyStagger = 0.0015f;
    public const float FlySwirl = 0.16f;
    public const float DropSeconds = 0.4f;
    public const float DropColumnStagger = 0.035f;
    public const float DropRowStagger = 0.012f;
    public const float WobbleSeconds = 0.28f;
    private const float WobbleAmount = 0.07f;
    private const float WobbleSpeed = 48f;

    private ClusterMotion[] motions = new ClusterMotion[ClusterBoard.MaxCells];
    private ClusterMotion[] next = new ClusterMotion[ClusterBoard.MaxCells];
    private readonly float[] wobbles = new float[ClusterBoard.MaxCells];

    public Vector2 Offset(int index)
    {
        var offset = motions[index].Offset;
        var wobble = wobbles[index];
        if (wobble <= 0f)
        {
            return offset;
        }

        var fade = wobble / WobbleSeconds;
        return offset + new Vector2(MathF.Sin(wobble * WobbleSpeed) * WobbleAmount * fade, 0f);
    }

    public void Reset()
    {
        Array.Clear(motions);
        Array.Clear(next);
        Array.Clear(wobbles);
    }

    public void Advance(float deltaSeconds)
    {
        for (var index = 0; index < motions.Length; index++)
        {
            motions[index].Age += deltaSeconds;
            wobbles[index] = MathF.Max(0f, wobbles[index] - deltaSeconds);
        }
    }

    public void Wobble(int index)
    {
        wobbles[index] = WobbleSeconds;
    }

    public void Settle(ClusterBoard board)
    {
        var rows = board.Rows;
        for (var index = 0; index < board.CellCount; index++)
        {
            var source = board.SourceOf(index);
            if (source < 0)
            {
                next[index] = default;
                continue;
            }

            if (source == index)
            {
                next[index] = motions[index];
                continue;
            }

            var from = Travel(board, source, index) + motions[source].Offset;
            var row = board.RowOf(index);
            var delayY = FallDelay + (rows - 1 - row) * RowStagger;
            var durationY = FallBase + FallPerRoot * MathF.Sqrt(MathF.Abs(from.Y));
            next[index] = new ClusterMotion
            {
                From = from,
                DelayY = delayY,
                DurationY = durationY,
                DelayX = MathF.Abs(from.Y) > 0.01f ? delayY + durationY * 0.75f : delayY,
                DurationX = SlideSeconds,
            };
        }

        Swap(board.CellCount);
    }

    public void Fly(ClusterBoard board)
    {
        for (var index = 0; index < board.CellCount; index++)
        {
            var source = board.SourceOf(index);
            if (source < 0)
            {
                next[index] = default;
                continue;
            }

            next[index] = new ClusterMotion
            {
                From = Travel(board, source, index) + motions[source].Offset,
                DelayY = index * FlyStagger,
                DurationY = FlySeconds,
                Swirl = index % 2 == 0 ? FlySwirl : -FlySwirl,
                Fly = true,
            };
        }

        Swap(board.CellCount);
    }

    public void DropIn(ClusterBoard board)
    {
        Array.Clear(motions);
        Array.Clear(wobbles);
        var rows = board.Rows;
        for (var index = 0; index < board.CellCount; index++)
        {
            var row = board.RowOf(index);
            motions[index] = new ClusterMotion
            {
                From = new Vector2(0f, -(rows + 1.5f)),
                DelayY = board.ColumnOf(index) * DropColumnStagger + (rows - 1 - row) * DropRowStagger,
                DurationY = DropSeconds,
                DurationX = 0f,
            };
        }
    }

    private static Vector2 Travel(ClusterBoard board, int source, int index) =>
        new(board.ColumnOf(source) - board.ColumnOf(index), board.RowOf(source) - board.RowOf(index));

    private void Swap(int count)
    {
        for (var index = count; index < next.Length; index++)
        {
            next[index] = default;
        }

        (motions, next) = (next, motions);
    }
}
