using System.Numerics;
using Aetherphone.Apps.Games.Cluster;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ClusterBoardTests
{
    private const byte Red = 1;
    private const byte Blue = 2;
    private const byte Yellow = 3;
    private const byte Green = 4;

    [Fact]
    public void AGroupFollowsOrthogonalNeighborsOfOneColorOnly()
    {
        var board = Board(
            "RRB",
            "BRB",
            "RBR");
        var group = new int[ClusterBoard.MaxCells];

        var size = board.FindGroup(board.Index(0, 0), group);

        Assert.Equal(3, size);
        Assert.Equal(new[] { board.Index(0, 0), board.Index(1, 0), board.Index(1, 1) },
            group.Take(size).Order().ToArray());
        Assert.Equal(1, board.GroupSize(board.Index(0, 2)));
        Assert.Equal(1, board.GroupSize(board.Index(2, 2)));
        Assert.Equal(2, board.GroupSize(board.Index(2, 0)));
    }

    [Fact]
    public void AnEmptyCellHasNoGroup()
    {
        var board = Board(
            "R..",
            "RRR");

        Assert.Equal(0, board.GroupSize(board.Index(1, 0)));
        Assert.Equal(4, board.GroupSize(board.Index(0, 0)));
    }

    [Fact]
    public void PiecesAboveAClearedGroupFallIntoTheGap()
    {
        var board = Board(
            "BY",
            "RY",
            "RB",
            "RY");
        var group = new int[ClusterBoard.MaxCells];
        var size = board.FindGroup(board.Index(0, 3), group);

        var removed = board.Remove(group.AsSpan(0, size));

        Assert.Equal(3, removed);
        Assert.Equal(Rows(".Y", ".Y", ".B", "BY"), Snapshot(board));
        Assert.Equal(board.Index(0, 0), board.SourceOf(board.Index(0, 3)));
        Assert.Equal(board.Index(1, 3), board.SourceOf(board.Index(1, 3)));
        Assert.Equal(ClusterBoard.NoSource, board.SourceOf(board.Index(0, 0)));
        Assert.Equal(5, board.Count);
    }

    [Fact]
    public void EmptyColumnsCloseInTowardTheMiddle()
    {
        var board = Board(
            "BRYGBYB",
            "BRYGBYB");

        board.Remove(Cells(board, 1, 0, 1, 1));

        Assert.Equal(Rows(".BYGBYB", ".BYGBYB"), Snapshot(board));
        Assert.Equal(board.Index(0, 0), board.SourceOf(board.Index(1, 0)));
        Assert.Equal(board.Index(2, 1), board.SourceOf(board.Index(2, 1)));

        board.Remove(Cells(board, 5, 0, 5, 1));

        Assert.Equal(Rows(".BYGBB.", ".BYGBB."), Snapshot(board));
        Assert.Equal(board.Index(6, 1), board.SourceOf(board.Index(5, 1)));
        Assert.Equal(board.Index(3, 0), board.SourceOf(board.Index(3, 0)));
        Assert.Equal(ClusterBoard.NoSource, board.SourceOf(board.Index(6, 0)));
    }

    [Fact]
    public void AnOuterColumnThatEmptiesLeavesTheRestInPlace()
    {
        var board = Board(
            "RBYG",
            "RBYG",
            "RBYG");
        var group = new int[ClusterBoard.MaxCells];
        var size = board.FindGroup(board.Index(0, 0), group);

        board.Remove(group.AsSpan(0, size));

        Assert.Equal(Rows(".BYG", ".BYG", ".BYG"), Snapshot(board));
        Assert.Equal(board.Index(1, 2), board.SourceOf(board.Index(1, 2)));
    }

    [Fact]
    public void ClosingColumnsKeepsEveryPieceAndItsColor()
    {
        var random = GameRandom.FromSeed(42);
        var board = new ClusterBoard();
        board.Fill(9, 11, 6, ref random);
        var group = new int[ClusterBoard.MaxCells];
        for (var turn = 0; turn < 40 && board.HasMove(); turn++)
        {
            var before = Snapshot(board);
            var beforeColumns = board.Columns;
            board.LargestGroup(out var index);
            var size = board.FindGroup(index, group);
            var cleared = group.Take(size).ToHashSet();
            board.Remove(group.AsSpan(0, size));
            for (var cell = 0; cell < board.CellCount; cell++)
            {
                var source = board.SourceOf(cell);
                if (board.At(cell) == ClusterBoard.Empty)
                {
                    Assert.Equal(ClusterBoard.NoSource, source);
                    continue;
                }

                Assert.DoesNotContain(source, cleared);
                Assert.Equal(before[source / beforeColumns][source % beforeColumns], Letter(board.At(cell)));
            }

            AssertSettled(board);
        }
    }

    [Fact]
    public void ACheckeredBoardHasNoMoveButIsNotFinishedWhileManyPiecesRemain()
    {
        var board = Board(
            "RBRB",
            "BRBR",
            "RBRB",
            "BRBR");

        Assert.False(board.HasMove());
        Assert.True(board.CanFormGroup);
        Assert.False(board.Finished);
    }

    [Fact]
    public void ADeadBoardWithFewPiecesLeftIsFinished()
    {
        var board = Board(
            "......",
            "......",
            "......",
            "RB....",
            "BRYYG.");

        Assert.False(board.HasMove());
        Assert.True(board.Count <= board.FinishThreshold);
        Assert.True(board.Finished);
    }

    [Fact]
    public void ABoardWithAGroupLeftIsNeverFinished()
    {
        var board = Board(
            "......",
            "......",
            "......",
            "......",
            "RRR...");

        Assert.True(board.HasMove());
        Assert.False(board.Finished);
    }

    [Fact]
    public void AnEmptyBoardIsAPerfectFinish()
    {
        var board = Board(
            "RRR",
            "BBB");
        board.Remove(Cells(board, 0, 0, 1, 0, 2, 0));
        board.Remove(Cells(board, 0, 1, 1, 1, 2, 1));

        Assert.Equal(0, board.Count);
        Assert.True(board.Finished);
    }

    [Fact]
    public void AReshuffleAlwaysLeavesAMoveAndKeepsEveryPiece()
    {
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var board = DeadBoard(ref random);
            var before = Snapshot(board);
            var counts = ColorCounts(board);
            var columns = board.Columns;

            board.Reshuffle(ref random);

            Assert.True(board.HasMove(), $"seed {seed}");
            Assert.Equal(counts, ColorCounts(board));
            var seen = new HashSet<int>();
            for (var cell = 0; cell < board.CellCount; cell++)
            {
                var occupiedBefore = before[cell / columns][cell % columns] != '.';
                Assert.Equal(occupiedBefore, board.At(cell) != ClusterBoard.Empty);
                if (!occupiedBefore)
                {
                    continue;
                }

                var source = board.SourceOf(cell);
                Assert.True(seen.Add(source));
                Assert.Equal(before[source / columns][source % columns], Letter(board.At(cell)));
            }
        }
    }

    [Fact]
    public void ARegroupBuildsLargerClustersFromTheSamePieces()
    {
        var larger = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var board = new ClusterBoard();
            board.Fill(9, 11, 6, ref random);
            var counts = ColorCounts(board);
            var groupsBefore = GroupSizes(board);

            board.Regroup(ref random);

            var groupsAfter = GroupSizes(board);
            Assert.Equal(counts, ColorCounts(board));
            Assert.True(groupsAfter.Count < groupsBefore.Count, $"seed {seed}");
            Assert.True(groupsAfter.Where(size => size >= ClusterBoard.MinGroup).Sum() >
                        groupsBefore.Where(size => size >= ClusterBoard.MinGroup).Sum());
            if (groupsAfter.Average() > groupsBefore.Average() * 2f)
            {
                larger++;
            }
        }

        Assert.True(larger >= 55);
    }

    [Fact]
    public void ARegroupOnALateBoardNeverLeavesItDead()
    {
        var group = new int[ClusterBoard.MaxCells];
        var shuffled = 0;
        for (ulong seed = 1; seed <= 6000; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var picker = GameRandom.FromSeed(seed * 31 + 7);
            var level = 1 + (int)(seed % 8);
            var board = new ClusterBoard();
            board.Fill(ClusterPace.ColumnsFor(level), ClusterPace.RowsFor(level), ClusterPace.ColorsFor(level),
                ref random);
            if (!ClearDownToLateBoard(board, ref random, ref picker, group))
            {
                continue;
            }

            var counts = ColorCounts(board);
            board.Regroup(ref random);
            shuffled++;

            Assert.Equal(counts, ColorCounts(board));
            Assert.True(board.HasMove(), $"seed {seed} level {level} left {board.Count}");
            Assert.False(board.Finished, $"seed {seed}");
        }

        Assert.True(shuffled > 1000, $"only {shuffled} late boards were shuffled");
    }

    [Fact]
    public void AShuffleNeverLeavesTheRunOnADeadBoard()
    {
        var shuffled = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var run = new ClusterRun();
            run.Reset(GameRandom.FromSeed(seed));
            var picker = GameRandom.FromSeed(seed * 13 + 1);
            for (var turn = 0; turn < 600 && !run.Over; turn++)
            {
                SkipBusy(run);
                var late = run.Board.Count * 4 <= run.Board.CellCount + run.Board.CellCount / 2;
                if (run.ShuffleReady && late)
                {
                    Assert.True(run.TryShuffle(out _));
                    shuffled++;
                    Assert.True(run.Board.HasMove() || run.Pending != ClusterPending.None, $"seed {seed}");
                    break;
                }

                run.Board.LargestGroup(out var largest);
                var cell = run.ShuffleReady ? RandomGroupCell(run.Board, ref picker) : largest;
                if (!run.TryClear(cell, out _))
                {
                    run.Step(1f / 60f);
                }
            }
        }

        Assert.True(shuffled > 50, $"only {shuffled} shuffles reached a late board");
    }

    [Fact]
    public void ARegroupOnAFullBoardLeavesNoLonePiece()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var board = new ClusterBoard();
            board.Fill(7, 9, 3, ref random);

            board.Regroup(ref random);

            Assert.True(GroupSizes(board).Min() >= ClusterBoard.MinGroup, $"seed {seed}");
        }
    }

    [Fact]
    public void AFreshBoardIsFullAndAlwaysPlayable()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            for (var level = 1; level <= 7; level++)
            {
                var random = GameRandom.FromSeed(seed);
                var board = new ClusterBoard();
                board.Fill(ClusterPace.ColumnsFor(level), ClusterPace.RowsFor(level), ClusterPace.ColorsFor(level),
                    ref random);

                Assert.Equal(board.CellCount, board.Count);
                Assert.True(board.HasMove());
                for (var cell = 0; cell < board.CellCount; cell++)
                {
                    Assert.InRange(board.At(cell), 1, ClusterPace.ColorsFor(level));
                }
            }
        }
    }

    [Fact]
    public void PointsGrowWithTheSquareOfTheGroup()
    {
        Assert.Equal(0, ClusterPace.Points(2, 1));
        Assert.Equal(45, ClusterPace.Points(3, 1));
        Assert.Equal(180, ClusterPace.Points(6, 1));
        Assert.Equal(4 * ClusterPace.Points(5, 1), ClusterPace.Points(10, 1));
        Assert.Equal(3 * ClusterPace.Points(7, 1), ClusterPace.Points(7, 3));
    }

    [Fact]
    public void TheRefillGrowsWithTheGroupAndTheChargeOnlyWithBigOnes()
    {
        Assert.Equal(0f, ClusterPace.Refill(2));
        Assert.Equal(ClusterPace.Refill(3) * 2f, ClusterPace.Refill(6), 4);
        Assert.Equal(0, ClusterPace.Charge(4));
        Assert.Equal(1, ClusterPace.Charge(5));
        Assert.Equal(6, ClusterPace.Charge(10));
    }

    [Fact]
    public void TheBoardBonusRewardsACleanFinish()
    {
        Assert.Equal(2 * ClusterPace.BoardBonusPerLevel + 2 * ClusterPace.PerfectBonusPerLevel,
            ClusterPace.BoardBonus(2, 0));
        Assert.Equal(2 * (ClusterPace.BoardBonusPerLevel - 3 * ClusterPace.LeftoverPenalty),
            ClusterPace.BoardBonus(2, 3));
        Assert.Equal(0, ClusterPace.BoardBonus(5, 40));
    }

    [Fact]
    public void ColorsRiseToSixBeforeTheBoardGrows()
    {
        var colors = Enumerable.Range(1, 8).Select(ClusterPace.ColorsFor).ToArray();
        var cells = Enumerable.Range(1, 8).Select(level => ClusterPace.ColumnsFor(level) * ClusterPace.RowsFor(level))
            .ToArray();

        Assert.Equal(new[] { 3, 4, 5, 6, 6, 6, 6, 6 }, colors);
        Assert.Equal(cells[0], cells[3]);
        Assert.True(cells[4] > cells[3]);
        Assert.True(cells[5] > cells[4]);
        Assert.Equal(ClusterBoard.MaxColumns, ClusterPace.ColumnsFor(20));
        Assert.Equal(ClusterBoard.MaxRows, ClusterPace.RowsFor(20));
    }

    [Fact]
    public void TheDrainQuickensWithEveryBoardAndOverTime()
    {
        Assert.Equal(ClusterPace.BaseDrainPerSecond, ClusterPace.DrainPerSecond(1, 0f));
        Assert.True(ClusterPace.DrainPerSecond(2, 0f) > ClusterPace.DrainPerSecond(1, 0f));
        Assert.True(ClusterPace.DrainPerSecond(1, 60f) > ClusterPace.DrainPerSecond(1, 0f));
        Assert.True(ClusterPace.DrainPerSecond(10, 180f) > 3f * ClusterPace.BaseDrainPerSecond);
    }

    [Fact]
    public void TheBigClearTiersClimbWithTheGroup()
    {
        Assert.Equal(0, ClusterPace.Tier(7));
        Assert.Equal(1, ClusterPace.Tier(ClusterPace.GreatGroup));
        Assert.Equal(2, ClusterPace.Tier(ClusterPace.HugeGroup));
        Assert.Equal(3, ClusterPace.Tier(40));
    }

    [Fact]
    public void AClearScoresRefillsTheBarAndFeedsTheCombo()
    {
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(7));
        SkipBusy(run);
        run.Step(3f);
        var barBefore = run.Bar;
        var index = run.Board.LargestGroup(out var cell) > 0 ? cell : -1;
        var size = run.Board.GroupSize(index);

        Assert.True(run.TryClear(index, out var first));

        Assert.Equal(size, first.Size);
        Assert.Equal(ClusterPace.Points(size, 1), first.Points);
        Assert.Equal(first.Points, run.Score);
        Assert.Equal(MathF.Min(1f, barBefore + ClusterPace.Refill(size)), run.Bar, 4);
        Assert.Equal(size, run.Cleared);
        Assert.Equal(size, run.BestGroup);
    }

    [Fact]
    public void ALonePieceOrPairCannotBeCleared()
    {
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(3));
        SkipBusy(run);
        for (var cell = 0; cell < run.Board.CellCount; cell++)
        {
            if (run.Board.GroupSize(cell) >= ClusterBoard.MinGroup)
            {
                continue;
            }

            Assert.False(run.TryClear(cell, out _));
            Assert.Equal(0, run.Score);
            return;
        }
    }

    [Fact]
    public void TheRunEndsWhenTheBarRunsDry()
    {
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(11));
        SkipBusy(run);
        run.TakeEvents();
        for (var frame = 0; frame < 60 * 60 && !run.Over; frame++)
        {
            run.Step(1f / 60f);
        }

        Assert.True(run.Over);
        Assert.Equal(0f, run.Bar);
        Assert.True(run.TakeEvents().HasFlag(ClusterEvents.Ended));
        Assert.False(run.TryClear(0, out _));
        Assert.InRange(run.PlaySeconds, ClusterPace.StartBar / ClusterPace.DrainPerSecond(1, 60f), 60f);
    }

    [Fact]
    public void FinishingABoardHoldsTheBarThenDealsTheNextOne()
    {
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(5));
        SkipBusy(run);
        var finished = PlayUntil(run, clear => clear.BoardFinished);
        var bar = run.Bar;

        Assert.True(finished.BoardFinished);
        Assert.Equal(ClusterPending.NextBoard, run.Pending);
        Assert.Equal(1, run.Boards);
        Assert.False(run.AcceptsInput);
        run.Step(ClusterRun.BoardClearSeconds * 0.5f);
        Assert.Equal(bar, run.Bar);
        Assert.Equal(1, run.Level);
        run.Step(ClusterRun.BoardClearSeconds);

        Assert.Equal(2, run.Level);
        Assert.Equal(ClusterPace.ColorsFor(2), run.Board.Colors);
        Assert.Equal(run.Board.CellCount, run.Board.Count);
        Assert.True(run.TakeEvents().HasFlag(ClusterEvents.BoardStarted));
    }

    [Fact]
    public void ADeadBoardReshufflesForFreeAfterAShortBeat()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var run = new ClusterRun();
            run.Reset(GameRandom.FromSeed(seed));
            SkipBusy(run);
            var clear = PlayUntil(run, _ => run.Pending == ClusterPending.Reshuffle);
            if (run.Pending != ClusterPending.Reshuffle)
            {
                continue;
            }

            Assert.False(clear.BoardFinished);
            var bar = run.Bar;
            var score = run.Score;
            run.TakeEvents();
            run.Step(ClusterRun.ReshuffleDelay + 0.01f);

            Assert.True(run.TakeEvents().HasFlag(ClusterEvents.Reshuffled));
            Assert.True(run.Board.HasMove());
            Assert.Equal(bar, run.Bar);
            Assert.Equal(score, run.Score);
            Assert.Equal(1, run.Reshuffles);
            return;
        }

        Assert.Fail("no seed reached a dead board");
    }

    [Fact]
    public void ShuffleNeedsAFullChargeAndSpendsIt()
    {
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(9));
        SkipBusy(run);

        Assert.False(run.ShuffleReady);
        Assert.False(run.TryShuffle(out _));
        var filled = PlayUntil(run, clear => clear.ChargeFilled);
        SkipBusy(run);

        Assert.True(filled.ChargeFilled);
        Assert.True(run.ShuffleReady);
        Assert.True(run.TryShuffle(out _));
        Assert.False(run.ShuffleReady);
        Assert.Equal(0f, run.ChargeFraction);
        Assert.Equal(1, run.Shuffles);
    }

    [Fact]
    public void AFallingPieceStartsFromItsOldCellAndComesToRest()
    {
        var board = Board(
            "BY",
            "RY",
            "RB",
            "RY");
        var motions = new ClusterMotions();
        var group = new int[ClusterBoard.MaxCells];
        var size = board.FindGroup(board.Index(0, 3), group);
        board.Remove(group.AsSpan(0, size));

        motions.Settle(board);

        Assert.Equal(new Vector2(0f, -3f), motions.Offset(board.Index(0, 3)));
        Assert.Equal(Vector2.Zero, motions.Offset(board.Index(1, 3)));
        motions.Advance(2f);
        Assert.Equal(Vector2.Zero, motions.Offset(board.Index(0, 3)));
    }

    [Fact]
    public void AShuffledPieceFliesFromItsOldCellAndLands()
    {
        var random = GameRandom.FromSeed(17);
        var board = new ClusterBoard();
        board.Fill(7, 9, 4, ref random);
        var motions = new ClusterMotions();

        board.Regroup(ref random);
        motions.Fly(board);

        for (var cell = 0; cell < board.CellCount; cell++)
        {
            var source = board.SourceOf(cell);
            var travel = new Vector2(board.ColumnOf(source) - board.ColumnOf(cell),
                board.RowOf(source) - board.RowOf(cell));
            Assert.Equal(travel, motions.Offset(cell));
        }

        motions.Advance(ClusterMotions.FlySeconds + board.CellCount * ClusterMotions.FlyStagger);
        for (var cell = 0; cell < board.CellCount; cell++)
        {
            Assert.Equal(0f, motions.Offset(cell).Length(), 4);
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, 0.8f);
        var second = Play(1234, 0.8f);
        var other = Play(99, 0.8f);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.PlaySeconds, second.PlaySeconds);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Cleared, second.Cleared);
        Assert.Equal(Snapshot(first.Board), Snapshot(second.Board));
        Assert.True(first.Score != other.Score || first.Cleared != other.Cleared);
    }

    [Fact]
    public void ASteadyPlayerLastsTwoToFourMinutes()
    {
        var steady = Seeds().Select(seed => Play(seed, 0.9f).PlaySeconds).Average();
        var quick = Seeds().Select(seed => Play(seed, 0.6f).PlaySeconds).Average();
        var slow = Seeds().Select(seed => Play(seed, 1.6f).PlaySeconds).Average();

        Assert.InRange(steady, 120f, 240f);
        Assert.InRange(quick, steady, 300f);
        Assert.InRange(slow, 60f, steady);
    }

    private static IEnumerable<ulong> Seeds() => Enumerable.Range(1, 12).Select(seed => (ulong)seed);

    private static ClusterRun Play(ulong seed, float thinkSeconds)
    {
        const float frame = 1f / 60f;
        var run = new ClusterRun();
        run.Reset(GameRandom.FromSeed(seed));
        var wait = 0f;
        while (!run.Over && run.PlaySeconds < 900f)
        {
            run.Step(frame);
            run.TakeEvents();
            wait += frame;
            if (!run.AcceptsInput || wait < thinkSeconds)
            {
                continue;
            }

            wait = 0f;
            if (run.ShuffleReady)
            {
                run.TryShuffle(out _);
                continue;
            }

            run.Board.LargestGroup(out var index);
            run.TryClear(index, out _);
        }

        return run;
    }

    private static bool ClearDownToLateBoard(ClusterBoard board, ref GameRandom random, ref GameRandom picker,
        int[] group)
    {
        var target = (int)(board.CellCount * picker.Range(0.08f, 0.4f));
        while (board.Count > target)
        {
            var cell = RandomGroupCell(board, ref picker);
            if (cell < 0)
            {
                return false;
            }

            var size = board.FindGroup(cell, group);
            board.Remove(group.AsSpan(0, size));
            if (board.Finished)
            {
                return false;
            }

            if (!board.HasMove())
            {
                board.Reshuffle(ref random);
            }
        }

        return board.HasMove();
    }

    private static int RandomGroupCell(ClusterBoard board, ref GameRandom picker)
    {
        var candidates = new List<int>();
        for (var cell = 0; cell < board.CellCount; cell++)
        {
            if (board.At(cell) != ClusterBoard.Empty && board.GroupSize(cell) >= ClusterBoard.MinGroup)
            {
                candidates.Add(cell);
            }
        }

        return candidates.Count == 0 ? -1 : candidates[picker.Next(candidates.Count)];
    }

    private static ClusterClear PlayUntil(ClusterRun run, Func<ClusterClear, bool> stop)
    {
        for (var turn = 0; turn < 400 && !run.Over; turn++)
        {
            SkipBusy(run);
            run.Board.LargestGroup(out var index);
            if (!run.TryClear(index, out var clear))
            {
                continue;
            }

            if (stop(clear))
            {
                return clear;
            }
        }

        return default;
    }

    private static void SkipBusy(ClusterRun run)
    {
        for (var frame = 0; frame < 600 && run.Busy; frame++)
        {
            run.Step(1f / 60f);
        }
    }

    private static ClusterBoard DeadBoard(ref GameRandom random)
    {
        var board = new ClusterBoard();
        while (true)
        {
            board.Fill(7, 9, 6, ref random);
            var group = new int[ClusterBoard.MaxCells];
            while (board.HasMove())
            {
                board.LargestGroup(out var index);
                var size = board.FindGroup(index, group);
                board.Remove(group.AsSpan(0, size));
            }

            if (board.CanFormGroup)
            {
                return board;
            }
        }
    }

    private static List<int> GroupSizes(ClusterBoard board)
    {
        var sizes = new List<int>();
        var seen = new HashSet<int>();
        var group = new int[ClusterBoard.MaxCells];
        for (var cell = 0; cell < board.CellCount; cell++)
        {
            if (board.At(cell) == ClusterBoard.Empty || seen.Contains(cell))
            {
                continue;
            }

            var size = board.FindGroup(cell, group);
            for (var slot = 0; slot < size; slot++)
            {
                seen.Add(group[slot]);
            }

            sizes.Add(size);
        }

        return sizes;
    }

    private static int[] ColorCounts(ClusterBoard board) =>
        Enumerable.Range(1, ClusterBoard.MaxColors).Select(board.CountOf).ToArray();

    private static void AssertSettled(ClusterBoard board)
    {
        var occupiedColumns = new List<int>();
        for (var column = 0; column < board.Columns; column++)
        {
            var seenPiece = false;
            for (var row = 0; row < board.Rows; row++)
            {
                var filled = board.At(column, row) != ClusterBoard.Empty;
                Assert.False(seenPiece && !filled, "a piece floats over a gap");
                seenPiece |= filled;
            }

            if (seenPiece)
            {
                occupiedColumns.Add(column);
            }
        }

        for (var slot = 1; slot < occupiedColumns.Count; slot++)
        {
            Assert.Equal(occupiedColumns[slot - 1] + 1, occupiedColumns[slot]);
        }
    }

    private static ReadOnlySpan<int> Cells(ClusterBoard board, params int[] columnRowPairs)
    {
        var cells = new int[columnRowPairs.Length / 2];
        for (var slot = 0; slot < cells.Length; slot++)
        {
            cells[slot] = board.Index(columnRowPairs[slot * 2], columnRowPairs[slot * 2 + 1]);
        }

        return cells;
    }

    private static ClusterBoard Board(params string[] rows)
    {
        var columns = rows[0].Length;
        var layout = new byte[columns * rows.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                layout[row * columns + column] = rows[row][column] switch
                {
                    'R' => Red,
                    'B' => Blue,
                    'Y' => Yellow,
                    'G' => Green,
                    _ => ClusterBoard.Empty,
                };
            }
        }

        var board = new ClusterBoard();
        board.Load(columns, rows.Length, 4, layout);
        return board;
    }

    private static string[] Snapshot(ClusterBoard board)
    {
        var rows = new string[board.Rows];
        for (var row = 0; row < board.Rows; row++)
        {
            var line = new char[board.Columns];
            for (var column = 0; column < board.Columns; column++)
            {
                line[column] = Letter(board.At(column, row));
            }

            rows[row] = new string(line);
        }

        return rows;
    }

    private static char Letter(byte color) => color switch
    {
        Red => 'R',
        Blue => 'B',
        Yellow => 'Y',
        Green => 'G',
        ClusterBoard.Empty => '.',
        _ => (char)('0' + color),
    };

    private static string[] Rows(params string[] rows) => rows;
}
