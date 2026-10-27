using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlassTransformStackTests
{
    private const float Tolerance = 1e-4f;
    private static readonly Rect Screen = new(new Vector2(0f, 0f), new Vector2(400f, 800f));
    private static readonly Rect Bar = new(new Vector2(20f, 700f), new Vector2(380f, 750f));

    [Fact]
    public void AnEmptyStackLeavesTheRectWhereItWasPainted()
    {
        var stack = new GlassTransformStack();

        Assert.Equal(Bar, stack.Map(Bar));
        Assert.Equal(1f, stack.Scale);
        Assert.Equal(1f, stack.Alpha);
    }

    [Fact]
    public void ATranslationMovesTheGlassWithItsLayer()
    {
        var stack = new GlassTransformStack();
        var offset = new Vector2(120f, 0f);
        var transform = LayerTransform.Translate(offset, Screen);

        Assert.True(stack.TryPush(in transform));
        Assert.Equal(transform.Map(Bar), stack.Map(Bar));
    }

    [Fact]
    public void NestedLayersApplyTheInnerTransformFirstLikeTheirVertices()
    {
        var stack = new GlassTransformStack();
        var outer = LayerTransform.Fit(Screen, new Rect(new Vector2(100f, 200f), new Vector2(300f, 600f)), Screen,
            0.5f);
        var inner = LayerTransform.Translate(new Vector2(40f, 0f), Screen);
        stack.TryPush(in outer);
        stack.TryPush(in inner);

        var mapped = stack.Map(Bar);
        var expected = outer.Map(inner.Map(Bar));

        Assert.Equal(expected.Min.X, mapped.Min.X, Tolerance);
        Assert.Equal(expected.Max.Y, mapped.Max.Y, Tolerance);
        Assert.Equal(outer.Scale * inner.Scale, stack.Scale, Tolerance);
        Assert.Equal(0.5f, stack.Alpha, Tolerance);
    }

    [Fact]
    public void PopUndoesThePush()
    {
        var stack = new GlassTransformStack();
        var transform = LayerTransform.ScaleAbout(Screen.Center, 0.5f, Screen);
        stack.TryPush(in transform);
        stack.Pop();

        Assert.Equal(0, stack.Depth);
        Assert.Equal(Bar, stack.Map(Bar));
    }

    [Fact]
    public void ARotationCannotBeFollowed()
    {
        var stack = new GlassTransformStack();
        var turn = LayerTransform.Turn(Screen.Center, 0.3f, 1f, Screen);

        Assert.False(stack.TryPush(in turn));
        Assert.Equal(0, stack.Depth);
    }

    [Fact]
    public void TheStackRefusesPastItsCapacity()
    {
        var stack = new GlassTransformStack();
        var transform = LayerTransform.Translate(Vector2.One, Screen);
        for (var index = 0; index < GlassTransformStack.Capacity; index++)
        {
            Assert.True(stack.TryPush(in transform));
        }

        Assert.False(stack.TryPush(in transform));
        stack.Clear();
        Assert.Equal(0, stack.Depth);
    }
}
