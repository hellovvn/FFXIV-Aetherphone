using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Cluster;

internal static class ClusterGuide
{
    private const int TapColumns = 5;
    private const int TapRows = 4;
    private const int CloseColumns = 5;
    private const int CloseRows = 3;
    private const int ShuffleColumns = 5;
    private const int ShuffleRows = 4;
    private const float HalfShare = 0.43f;
    private const float BoardShare = 0.74f;

    private static readonly byte[] TapLayout =
    {
        2, 3, 1, 3, 2,
        3, 1, 1, 2, 3,
        1, 1, 2, 2, 3,
        2, 3, 4, 3, 2,
    };

    private static readonly bool[] TapGroup = Mark(TapLayout, TapColumns, 1);

    private static readonly int[] TapShift = Shifts(TapGroup, TapColumns, TapRows);

    private static readonly byte[] CloseLayout =
    {
        3, 4, 2, 1, 2,
        1, 4, 3, 2, 3,
        2, 4, 4, 3, 1,
    };

    private static readonly bool[] CloseGroup = Mark(CloseLayout, CloseColumns, 4);

    private static readonly int[] CloseShift = Shifts(CloseGroup, CloseColumns, CloseRows);

    private static readonly byte[] BarLayout =
    {
        2, 3, 2, 1, 3,
        1, 4, 4, 1, 2,
        3, 4, 2, 3, 1,
    };

    private static readonly bool[] BarGroup = Mark(BarLayout, CloseColumns, 4);

    private static readonly int[] BarShift = Shifts(BarGroup, CloseColumns, CloseRows);

    private static readonly byte[] Scattered =
    {
        1, 2, 3, 1, 4,
        3, 4, 1, 2, 3,
        2, 1, 4, 3, 2,
        4, 3, 2, 4, 1,
    };

    private static readonly byte[] Clustered =
    {
        1, 1, 3, 3, 3,
        1, 1, 2, 3, 3,
        4, 1, 2, 2, 2,
        4, 4, 4, 4, 2,
    };

    private static readonly int[] ShuffleSources = MatchSources(Scattered, Clustered);

    public static void DrawTapPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, 3.2f);
        var grid = GameGrid.Centered(art, TapColumns, TapRows, ClusterRenderer.GapFraction);
        var highlight = GuideKit.Window(phase, 0.12f, 0.24f) * (1f - GuideKit.Window(phase, 0.36f, 0.4f));
        var pop = GuideKit.Window(phase, 0.36f, 0.5f);
        var fall = GameJuice.PopIn(GuideKit.Window(phase, 0.48f, 0.68f));
        var restore = GuideKit.Window(phase, 0.88f, 1f);
        DrawFalling(drawList, grid, TapLayout, TapGroup, TapShift, TapColumns, highlight, pop, fall, 0f, restore,
            scale);
        var tapCell = grid.CellCenter(1, 2);
        TapBetween(drawList, tapCell + new Vector2(grid.Pitch * 0.18f, grid.Pitch * 0.22f), phase, 0.2f, 0.62f, scale);
    }

    public static void DrawClosePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, 3.4f);
        var band = art.Height / (CloseRows + 1);
        var boardArea = new Rect(new Vector2(art.Min.X, art.Min.Y + band), art.Max);
        var grid = GameGrid.Centered(boardArea, CloseColumns, CloseRows, ClusterRenderer.GapFraction);
        var pop = GuideKit.Window(phase, 0.22f, 0.34f);
        var fall = GameJuice.PopIn(GuideKit.Window(phase, 0.34f, 0.52f));
        var slide = GameJuice.PopIn(GuideKit.Window(phase, 0.54f, 0.72f));
        var restore = GuideKit.Window(phase, 0.9f, 1f);
        DrawFalling(drawList, grid, CloseLayout, CloseGroup, CloseShift, CloseColumns, 0f, pop, fall, slide, restore,
            scale);
        var arrowAlpha = GuideKit.Window(phase, 0.5f, 0.56f) * (1f - GuideKit.Window(phase, 0.78f, 0.86f));
        if (arrowAlpha > 0f)
        {
            var y = grid.Bounds.Min.Y - grid.Pitch * 0.45f;
            var color = GamePalette.Lighten(accent, 0.4f) with { W = arrowAlpha };
            GuideKit.Arrow(drawList, new Vector2(grid.CellCenter(0, 0).X - grid.Pitch * 0.2f, y),
                new Vector2(grid.CellCenter(1, 0).X + grid.Pitch * 0.2f, y), color, scale);
        }

        TapBetween(drawList, grid.CellCenter(1, 1) + new Vector2(grid.Pitch * 0.2f, grid.Pitch * 0.2f), phase, 0.08f,
            0.5f, scale);
    }

    public static void DrawBarPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, 3.6f);
        var barHeight = ClusterRenderer.BarHeight * scale;
        var boardArea = new Rect(new Vector2(art.Min.X, art.Min.Y + barHeight * 3f), art.Max);
        var grid = GameGrid.Centered(boardArea, CloseColumns, CloseRows, ClusterRenderer.GapFraction);
        var bar = new Rect(new Vector2(grid.Bounds.Min.X, art.Min.Y + barHeight * 0.5f),
            new Vector2(grid.Bounds.Max.X, art.Min.Y + barHeight * 1.5f));
        var drain = GuideKit.Window(phase, 0f, 0.42f);
        var refill = Easing.EaseOutCubic(GuideKit.Window(phase, 0.48f, 0.62f));
        var low = 0.72f - 0.5f * drain;
        var fraction = low + (0.8f - low) * refill;
        var gainAlpha = refill > 0f ? 1f - GuideKit.Window(phase, 0.62f, 0.86f) : 0f;
        ClusterRenderer.DrawBar(drawList, bar, fraction, low, gainAlpha, false, accent, scale);
        var pop = GuideKit.Window(phase, 0.44f, 0.56f);
        var fall = GameJuice.PopIn(GuideKit.Window(phase, 0.56f, 0.72f));
        var restore = GuideKit.Window(phase, 0.86f, 1f);
        DrawFalling(drawList, grid, BarLayout, BarGroup, BarShift, CloseColumns,
            GuideKit.Window(phase, 0.26f, 0.34f) * (1f - pop), pop, fall, 0f, restore, scale);
        TapBetween(drawList, grid.CellCenter(2, 1) + new Vector2(grid.Pitch * 0.18f, grid.Pitch * 0.2f), phase, 0.3f,
            0.7f, scale);
    }

    public static void DrawShufflePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, 3.6f);
        var buttonHeight = ClusterRenderer.ButtonHeight * scale;
        var boardArea = new Rect(art.Min, new Vector2(art.Max.X, art.Max.Y - buttonHeight * 1.3f));
        var grid = GameGrid.Centered(boardArea, ShuffleColumns, ShuffleRows, ClusterRenderer.GapFraction);
        var dock = new Rect(new Vector2(art.Min.X, art.Max.Y - buttonHeight * 1.15f), art.Max);
        var button = ClusterRenderer.ButtonRect(dock, scale);
        var charge = GuideKit.Window(phase, 0f, 0.22f);
        var spent = phase >= 0.34f && phase < 0.92f;
        ClusterRenderer.DrawButton(drawList, button, Loc.T(L.Cluster.Shuffle), spent ? 0f : charge,
            !spent && charge >= 1f, false, 0f, accent, scale);
        var fly = Easing.EaseInOutCubic(GuideKit.Window(phase, 0.36f, 0.62f));
        var back = GuideKit.Window(phase, 0.9f, 1f);
        var half = grid.Pitch * HalfShare;
        for (var cell = 0; cell < Clustered.Length; cell++)
        {
            var source = ShuffleSources[cell];
            var from = grid.CellCenter(source % ShuffleColumns, source / ShuffleColumns);
            var to = grid.CellCenter(cell % ShuffleColumns, cell / ShuffleColumns);
            var travel = to - from;
            var swirl = new Vector2(-travel.Y, travel.X) * (0.35f * MathF.Sin(MathF.PI * fly));
            var center = Vector2.Lerp(from, to, fly) + swirl;
            var alpha = back > 0f ? 1f - back : 1f;
            ClusterRenderer.DrawPiece(drawList, center, half, Clustered[cell], alpha, 0f, scale);
            if (back > 0f)
            {
                ClusterRenderer.DrawPiece(drawList, from, half, Clustered[cell], back, 0f, scale);
            }
        }

        TapBetween(drawList, new Vector2(button.Center.X, button.Center.Y + button.Height * 0.1f), phase, 0.22f, 0.6f,
            scale);
    }

    private static void TapBetween(ImDrawListPtr drawList, Vector2 center, float phase, float start, float end,
        float scale)
    {
        if (phase < start || phase > end)
        {
            return;
        }

        GuideKit.Tap(drawList, center, GuideKit.Window(phase, start, end), scale);
    }

    private static void DrawFalling(ImDrawListPtr drawList, in GameGrid grid, byte[] layout, bool[] group, int[] shifts,
        int columns, float highlight, float pop, float fall, float slide, float restore, float scale)
    {
        var half = grid.Pitch * HalfShare;
        var rows = layout.Length / columns;
        for (var cell = 0; cell < layout.Length; cell++)
        {
            var column = cell % columns;
            var row = cell / columns;
            var center = grid.CellCenter(column, row);
            if (group[cell])
            {
                if (pop >= 1f)
                {
                    continue;
                }

                var swell = 1f + 0.25f * MathF.Sin(MathF.PI * pop);
                var lift = highlight * grid.Pitch * 0.06f;
                ProgressRing.Glow(center, half * 1.2f, ClusterRenderer.ColorOf(layout[cell]), 0.6f * highlight);
                ClusterRenderer.DrawPiece(drawList, center - new Vector2(0f, lift), half * swell * (1f - pop * pop),
                    layout[cell], 1f - pop * pop * pop, highlight, scale);
                continue;
            }

            var drop = DropBelow(group, columns, rows, column, row);
            var moved = center + new Vector2(shifts[column] * grid.Pitch * slide, drop * grid.Pitch * fall);
            if (restore > 0f)
            {
                ClusterRenderer.DrawPiece(drawList, moved, half, layout[cell], 1f - restore, 0f, scale);
                ClusterRenderer.DrawPiece(drawList, center, half * GameJuice.PopIn(restore), layout[cell], restore, 0f,
                    scale);
                continue;
            }

            ClusterRenderer.DrawPiece(drawList, moved, half, layout[cell], 1f, 0f, scale);
        }

        if (restore <= 0f || pop < 1f)
        {
            return;
        }

        for (var cell = 0; cell < layout.Length; cell++)
        {
            if (!group[cell])
            {
                continue;
            }

            var center = grid.CellCenter(cell % columns, cell / columns);
            ClusterRenderer.DrawPiece(drawList, center, half * GameJuice.PopIn(restore), layout[cell], restore, 0f,
                scale);
        }
    }

    private static int DropBelow(bool[] group, int columns, int rows, int column, int row)
    {
        var drop = 0;
        for (var below = row + 1; below < rows; below++)
        {
            if (group[below * columns + column])
            {
                drop++;
            }
        }

        return drop;
    }

    private static int[] Shifts(bool[] group, int columns, int rows)
    {
        var shifts = new int[columns];
        var middle = columns / 2;
        var leftCount = 0;
        for (var column = 0; column < middle; column++)
        {
            if (!Cleared(group, columns, rows, column))
            {
                leftCount++;
            }
        }

        var leftSlot = middle - leftCount;
        var rightSlot = middle;
        for (var column = 0; column < columns; column++)
        {
            if (Cleared(group, columns, rows, column))
            {
                continue;
            }

            var target = column < middle ? leftSlot++ : rightSlot++;
            shifts[column] = target - column;
        }

        return shifts;
    }

    private static bool Cleared(bool[] group, int columns, int rows, int column)
    {
        for (var row = 0; row < rows; row++)
        {
            if (!group[row * columns + column])
            {
                return false;
            }
        }

        return true;
    }

    private static bool[] Mark(byte[] layout, int columns, int color)
    {
        var start = Array.IndexOf(layout, (byte)color);
        var marked = new bool[layout.Length];
        var rows = layout.Length / columns;
        var stack = new Stack<int>();
        stack.Push(start);
        marked[start] = true;
        while (stack.Count > 0)
        {
            var index = stack.Pop();
            var column = index % columns;
            var row = index / columns;
            Visit(layout, marked, stack, column > 0 ? index - 1 : -1, color);
            Visit(layout, marked, stack, column < columns - 1 ? index + 1 : -1, color);
            Visit(layout, marked, stack, row > 0 ? index - columns : -1, color);
            Visit(layout, marked, stack, row < rows - 1 ? index + columns : -1, color);
        }

        return marked;
    }

    private static void Visit(byte[] layout, bool[] marked, Stack<int> stack, int index, int color)
    {
        if (index < 0 || marked[index] || layout[index] != color)
        {
            return;
        }

        marked[index] = true;
        stack.Push(index);
    }

    private static int[] MatchSources(byte[] from, byte[] to)
    {
        var sources = new int[to.Length];
        var used = new bool[from.Length];
        for (var cell = 0; cell < to.Length; cell++)
        {
            for (var source = 0; source < from.Length; source++)
            {
                if (used[source] || from[source] != to[cell])
                {
                    continue;
                }

                used[source] = true;
                sources[cell] = source;
                break;
            }
        }

        return sources;
    }
}
