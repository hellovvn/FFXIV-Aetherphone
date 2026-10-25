using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Shell;

internal sealed class GlassTransformStack
{
    public const int Capacity = 8;

    private readonly LayerTransform[] transforms = new LayerTransform[Capacity];
    private int depth;

    public int Depth => depth;

    public float Scale
    {
        get
        {
            var scale = 1f;
            for (var index = 0; index < depth; index++)
            {
                scale *= transforms[index].Scale;
            }

            return scale;
        }
    }

    public float Alpha
    {
        get
        {
            var alpha = 1f;
            for (var index = 0; index < depth; index++)
            {
                alpha *= transforms[index].Alpha;
            }

            return alpha;
        }
    }

    public bool TryPush(in LayerTransform transform)
    {
        if (depth == Capacity || transform.IsRotated)
        {
            return false;
        }

        transforms[depth] = transform;
        depth++;
        return true;
    }

    public void Pop()
    {
        if (depth > 0)
        {
            depth--;
        }
    }

    public void Clear() => depth = 0;

    public Rect Map(Rect rect)
    {
        for (var index = depth - 1; index >= 0; index--)
        {
            rect = transforms[index].Map(rect);
        }

        return rect;
    }
}
