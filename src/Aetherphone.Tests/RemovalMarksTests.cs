using Aetherphone.Apps.Prism;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RemovalMarksTests
{
    [Fact]
    public void AGlamThatWasNeverRemovedIsShown()
    {
        var marks = new RemovalMarks();

        Assert.False(marks.Hidden("glam", marks.Current));
        Assert.False(marks.Contains("glam"));
    }

    [Fact]
    public void APageAskedForBeforeTheRemovalStillHidesTheGlam()
    {
        var marks = new RemovalMarks();
        var requestedAfter = marks.Current;

        marks.Mark("glam");

        Assert.True(marks.Hidden("glam", requestedAfter));
        Assert.True(marks.Contains("glam"));
    }

    [Fact]
    public void APageAskedForAfterTheRemovalThatStillHasTheGlamBringsItBack()
    {
        var marks = new RemovalMarks();
        marks.Mark("glam");
        var requestedAfter = marks.Current;

        Assert.False(marks.Hidden("glam", requestedAfter));
        Assert.False(marks.Contains("glam"));
        Assert.False(marks.Hidden("glam", 0));
    }

    [Fact]
    public void ARemovalAgainAfterAPutBackHidesTheGlamAgain()
    {
        var marks = new RemovalMarks();
        marks.Mark("glam");
        Assert.False(marks.Hidden("glam", marks.Current));
        var requestedAfter = marks.Current;

        marks.Mark("glam");

        Assert.True(marks.Hidden("glam", requestedAfter));
    }

    [Fact]
    public void OnlyTheRemovedGlamIsHidden()
    {
        var marks = new RemovalMarks();
        var requestedAfter = marks.Current;

        marks.Mark("removed");

        Assert.True(marks.Hidden("removed", requestedAfter));
        Assert.False(marks.Hidden("kept", requestedAfter));
    }
}
