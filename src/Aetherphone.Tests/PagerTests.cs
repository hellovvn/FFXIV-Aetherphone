using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PagerTests
{
    private const float Width = 100f;
    private const int Pages = 3;
    private const float Frame = 1f / 60f;
    private const float GrabAt = 0.3f;

    [Fact]
    public void FlickAfterAMidSettleGrabMovesOnePageFromWhereThePageIs()
    {
        var pager = new Pager();
        pager.AnimateTo(1, Pages);
        while (pager.Value < GrabAt)
        {
            pager.Step(Frame, Pages);
        }

        pager.Begin(0f);
        pager.Drag(-Width * 0.15f, Width, Pages, Frame);
        pager.Release(Width, Pages);

        Assert.Equal(1, pager.Page);
    }

    [Fact]
    public void ReleasingAMultiCardDragSettlesNearWhereItWasLetGo()
    {
        const int railPages = 12;
        var pager = new Pager();
        pager.SnapTo(4, railPages);
        pager.Begin(0f);
        for (var step = 1; step <= 60; step++)
        {
            pager.Drag(-Width * 3f * step / 60f, Width, railPages, Frame);
        }

        pager.ReleaseNearest(Width, railPages);

        Assert.Equal(7, pager.Page);
    }

    [Fact]
    public void AFastFlickMidCardGoesToTheNextCardOnly()
    {
        const int railPages = 6;
        var pager = new Pager();
        pager.SnapTo(2, railPages);
        pager.Begin(0f);
        for (var step = 1; step <= 5; step++)
        {
            pager.Drag(-Width * 0.5f * step / 5f, Width, railPages, Frame);
        }

        pager.ReleaseNearest(Width, railPages);

        Assert.Equal(3, pager.Page);
    }

    [Fact]
    public void AFastMultiCardFlingStopsOneCardPastWhereItWasLetGo()
    {
        const int railPages = 24;
        var pager = new Pager();
        pager.SnapTo(4, railPages);
        pager.Begin(0f);
        for (var step = 1; step <= 10; step++)
        {
            pager.Drag(-Width * 3.3f * step / 10f, Width, railPages, Frame);
        }

        pager.ReleaseNearest(Width, railPages);

        Assert.Equal(8, pager.Page);
    }

    [Fact]
    public void AQuickShortFlickStillMovesOneCard()
    {
        const int railPages = 12;
        var pager = new Pager();
        pager.SnapTo(4, railPages);
        pager.Begin(0f);
        pager.Drag(-Width * 0.1f, Width, railPages, Frame);
        pager.ReleaseNearest(Width, railPages);

        Assert.Equal(5, pager.Page);
    }

    [Fact]
    public void ShiftingMidDragKeepsTheGestureContinuous()
    {
        const int railPages = 24;
        var pager = new Pager();
        pager.SnapTo(9, railPages);
        pager.Begin(0f);
        pager.Drag(-Width * 0.5f, Width, railPages, Frame);
        var before = pager.Value;
        pager.Shift(-8);
        pager.Drag(-Width * 0.5f, Width, railPages, Frame);

        Assert.Equal(before - 8f, pager.Value, 3);
    }
}
