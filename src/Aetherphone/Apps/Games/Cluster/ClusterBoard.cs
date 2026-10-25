using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Cluster;

internal sealed class ClusterBoard
{
    public const int MaxColumns = 9;
    public const int MaxRows = 11;
    public const int MaxCells = MaxColumns * MaxRows;
    public const int MaxColors = 6;
    public const int MinGroup = 3;
    public const byte Empty = 0;
    public const int NoSource = -1;
    public const int ChunkMin = 4;
    public const int ChunkMax = 7;
    private const int ReshuffleAttempts = 8;
    private const int FinishDivisor = 4;

    private readonly byte[] cells = new byte[MaxCells];
    private readonly int[] sources = new int[MaxCells];
    private readonly byte[] spareCells = new byte[MaxCells];
    private readonly int[] spareSources = new int[MaxCells];
    private readonly int[] order = new int[MaxCells];
    private readonly int[] stack = new int[MaxCells];
    private readonly int[] queue = new int[MaxCells];
    private readonly int[] permutation = new int[MaxCells];
    private readonly int[] bestPermutation = new int[MaxCells];
    private readonly bool[] visited = new bool[MaxCells];
    private readonly int[] columnTargets = new int[MaxColumns];
    private readonly int[] colorCounts = new int[MaxColors + 1];
    private readonly int[] colorCursors = new int[MaxColors + 1];
    private readonly int[] byColor = new int[MaxCells];
    private readonly byte[] chunkColors = new byte[MaxCells];
    private readonly byte[] chunkSizes = new byte[MaxCells];

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public int Colors { get; private set; }

    public int Count { get; private set; }

    public int CellCount => Columns * Rows;

    public int Version { get; private set; }

    public int FinishThreshold => CellCount / FinishDivisor;

    public bool CanFormGroup
    {
        get
        {
            for (var color = 1; color <= Colors; color++)
            {
                if (colorCounts[color] >= MinGroup)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool Finished => Count == 0 || (!HasMove() && (Count <= FinishThreshold || !CanFormGroup));

    public int Index(int column, int row) => row * Columns + column;

    public int ColumnOf(int index) => index % Columns;

    public int RowOf(int index) => index / Columns;

    public byte At(int index) => cells[index];

    public byte At(int column, int row) => cells[Index(column, row)];

    public int SourceOf(int index) => sources[index];

    public int CountOf(int color) => color >= 1 && color <= MaxColors ? colorCounts[color] : 0;

    public void Fill(int columns, int rows, int colors, ref GameRandom random)
    {
        Resize(columns, rows, colors);
        for (var index = 0; index < CellCount; index++)
        {
            cells[index] = (byte)(1 + random.Next(Colors));
            sources[index] = NoSource;
        }

        Recount();
        if (!HasMove() && CanFormGroup)
        {
            Reshuffle(ref random);
        }

        ClearSources();
        Version++;
    }

    public void Load(int columns, int rows, int colors, ReadOnlySpan<byte> layout)
    {
        Resize(columns, rows, colors);
        for (var index = 0; index < CellCount; index++)
        {
            cells[index] = index < layout.Length ? layout[index] : Empty;
        }

        Recount();
        ClearSources();
        Version++;
    }

    public int FindGroup(int index, Span<int> group)
    {
        if (index < 0 || index >= CellCount || cells[index] == Empty)
        {
            return 0;
        }

        Array.Clear(visited, 0, CellCount);
        return Flood(index, group);
    }

    public int GroupSize(int index) => FindGroup(index, Span<int>.Empty);

    public bool HasMove()
    {
        Array.Clear(visited, 0, CellCount);
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] == Empty || visited[index])
            {
                continue;
            }

            if (Flood(index, Span<int>.Empty) >= MinGroup)
            {
                return true;
            }
        }

        return false;
    }

    public int LargestGroup(out int index)
    {
        Array.Clear(visited, 0, CellCount);
        var best = 0;
        index = -1;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] == Empty || visited[cell])
            {
                continue;
            }

            var size = Flood(cell, Span<int>.Empty);
            if (size <= best)
            {
                continue;
            }

            best = size;
            index = cell;
        }

        return best;
    }

    public int Remove(ReadOnlySpan<int> group)
    {
        var removed = 0;
        for (var slot = 0; slot < group.Length; slot++)
        {
            var index = group[slot];
            if (index < 0 || index >= CellCount || cells[index] == Empty)
            {
                continue;
            }

            colorCounts[cells[index]]--;
            cells[index] = Empty;
            removed++;
        }

        Count -= removed;
        Settle();
        Version++;
        return removed;
    }

    public void Reshuffle(ref GameRandom random)
    {
        ClearSources();
        var occupied = Occupied(order);
        if (occupied == 0)
        {
            return;
        }

        Array.Copy(cells, spareCells, CellCount);
        var bestMoves = -1;
        for (var attempt = 0; attempt < ReshuffleAttempts; attempt++)
        {
            Shuffle(occupied, ref random);
            Apply(occupied);
            var moves = MoveCount();
            if (moves <= bestMoves)
            {
                continue;
            }

            bestMoves = moves;
            Array.Copy(permutation, bestPermutation, occupied);
        }

        Array.Copy(bestPermutation, permutation, occupied);
        Apply(occupied);
        if (bestMoves == 0 && CanFormGroup)
        {
            ForceGroup(occupied);
        }

        Version++;
    }

    public int MoveCount()
    {
        Array.Clear(visited, 0, CellCount);
        var moves = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] == Empty || visited[index])
            {
                continue;
            }

            if (Flood(index, Span<int>.Empty) >= MinGroup)
            {
                moves++;
            }
        }

        return moves;
    }

    public void Regroup(ref GameRandom random)
    {
        ClearSources();
        var occupied = Serpentine(order);
        if (occupied == 0)
        {
            return;
        }

        SortByColor(occupied);
        var chunkCount = 0;
        for (var color = 1; color <= Colors; color++)
        {
            var remaining = colorCounts[color];
            while (remaining > 0)
            {
                var size = remaining <= ChunkMax ? remaining : ChunkMin + random.Next(ChunkMax - ChunkMin + 1);
                if (remaining - size < MinGroup)
                {
                    size = remaining;
                }

                chunkColors[chunkCount] = (byte)color;
                chunkSizes[chunkCount] = (byte)size;
                chunkCount++;
                remaining -= size;
            }
        }

        for (var slot = chunkCount - 1; slot > 0; slot--)
        {
            var pick = random.Next(slot + 1);
            (chunkColors[slot], chunkColors[pick]) = (chunkColors[pick], chunkColors[slot]);
            (chunkSizes[slot], chunkSizes[pick]) = (chunkSizes[pick], chunkSizes[slot]);
        }

        var cursor = 0;
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            var color = chunkColors[chunk];
            for (var piece = 0; piece < chunkSizes[chunk]; piece++)
            {
                var to = order[cursor++];
                cells[to] = color;
                sources[to] = byColor[colorCursors[color]++];
            }
        }

        if (!HasMove() && CanFormGroup)
        {
            ForceGroup(occupied);
        }

        Version++;
    }

    public void ClearSources()
    {
        for (var index = 0; index < MaxCells; index++)
        {
            sources[index] = NoSource;
        }
    }

    private void Resize(int columns, int rows, int colors)
    {
        Columns = Math.Clamp(columns, 1, MaxColumns);
        Rows = Math.Clamp(rows, 1, MaxRows);
        Colors = Math.Clamp(colors, 1, MaxColors);
        Array.Clear(cells);
    }

    private void Recount()
    {
        Array.Clear(colorCounts);
        Count = 0;
        for (var index = 0; index < CellCount; index++)
        {
            var color = cells[index];
            if (color == Empty)
            {
                continue;
            }

            colorCounts[color]++;
            Count++;
        }
    }

    private int Flood(int start, Span<int> group)
    {
        var color = cells[start];
        var top = 0;
        var size = 0;
        stack[top++] = start;
        visited[start] = true;
        while (top > 0)
        {
            var index = stack[--top];
            if (size < group.Length)
            {
                group[size] = index;
            }

            size++;
            var column = index % Columns;
            var row = index / Columns;
            if (column > 0)
            {
                Visit(index - 1, color, ref top);
            }

            if (column < Columns - 1)
            {
                Visit(index + 1, color, ref top);
            }

            if (row > 0)
            {
                Visit(index - Columns, color, ref top);
            }

            if (row < Rows - 1)
            {
                Visit(index + Columns, color, ref top);
            }
        }

        return size;
    }

    private void Visit(int index, byte color, ref int top)
    {
        if (visited[index] || cells[index] != color)
        {
            return;
        }

        visited[index] = true;
        stack[top++] = index;
    }

    private void Settle()
    {
        for (var index = 0; index < CellCount; index++)
        {
            sources[index] = cells[index] == Empty ? NoSource : index;
        }

        for (var column = 0; column < Columns; column++)
        {
            var write = Rows - 1;
            for (var row = Rows - 1; row >= 0; row--)
            {
                var from = Index(column, row);
                if (cells[from] == Empty)
                {
                    continue;
                }

                var to = Index(column, write);
                write--;
                if (to == from)
                {
                    continue;
                }

                cells[to] = cells[from];
                sources[to] = sources[from];
                cells[from] = Empty;
                sources[from] = NoSource;
            }
        }

        CloseColumns();
    }

    private void CloseColumns()
    {
        var middle = Columns / 2;
        var leftCount = 0;
        for (var column = 0; column < middle; column++)
        {
            if (!ColumnEmpty(column))
            {
                leftCount++;
            }
        }

        var leftSlot = middle - leftCount;
        var rightSlot = middle;
        var moved = false;
        for (var column = 0; column < Columns; column++)
        {
            if (ColumnEmpty(column))
            {
                columnTargets[column] = -1;
                continue;
            }

            var target = column < middle ? leftSlot++ : rightSlot++;
            columnTargets[column] = target;
            moved |= target != column;
        }

        if (!moved)
        {
            return;
        }

        Array.Clear(spareCells, 0, CellCount);
        for (var index = 0; index < CellCount; index++)
        {
            spareSources[index] = NoSource;
        }

        for (var column = 0; column < Columns; column++)
        {
            var target = columnTargets[column];
            if (target < 0)
            {
                continue;
            }

            for (var row = 0; row < Rows; row++)
            {
                var from = Index(column, row);
                var to = Index(target, row);
                spareCells[to] = cells[from];
                spareSources[to] = sources[from];
            }
        }

        Array.Copy(spareCells, cells, CellCount);
        Array.Copy(spareSources, sources, CellCount);
    }

    private bool ColumnEmpty(int column) => cells[Index(column, Rows - 1)] == Empty;

    private int Occupied(int[] positions)
    {
        var count = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] != Empty)
            {
                positions[count++] = index;
            }
        }

        return count;
    }

    private void SortByColor(int occupied)
    {
        StartCursors();
        for (var slot = 0; slot < occupied; slot++)
        {
            var index = order[slot];
            byColor[colorCursors[cells[index]]++] = index;
        }

        StartCursors();
    }

    private void StartCursors()
    {
        var offset = 0;
        for (var color = 1; color <= Colors; color++)
        {
            colorCursors[color] = offset;
            offset += colorCounts[color];
        }
    }

    private int Serpentine(int[] positions)
    {
        var count = 0;
        var upward = true;
        for (var column = 0; column < Columns; column++)
        {
            if (ColumnEmpty(column))
            {
                continue;
            }

            for (var step = 0; step < Rows; step++)
            {
                var row = upward ? Rows - 1 - step : step;
                var index = Index(column, row);
                if (cells[index] != Empty)
                {
                    positions[count++] = index;
                }
            }

            upward = !upward;
        }

        return count;
    }

    private void Shuffle(int occupied, ref GameRandom random)
    {
        for (var slot = 0; slot < occupied; slot++)
        {
            permutation[slot] = order[slot];
        }

        for (var slot = occupied - 1; slot > 0; slot--)
        {
            var pick = random.Next(slot + 1);
            (permutation[slot], permutation[pick]) = (permutation[pick], permutation[slot]);
        }
    }

    private void Apply(int occupied)
    {
        for (var slot = 0; slot < occupied; slot++)
        {
            var to = order[slot];
            var from = permutation[slot];
            cells[to] = spareCells[from];
            sources[to] = from;
        }
    }

    private void ForceGroup(int occupied)
    {
        var color = MostCommonColor();
        var start = order[0];
        for (var slot = 0; slot < occupied; slot++)
        {
            if (RowOf(order[slot]) == Rows - 1)
            {
                start = order[slot];
                break;
            }
        }

        Array.Clear(visited, 0, CellCount);
        var trio = stack.AsSpan(0, MinGroup);
        var found = Reach(start, trio);
        for (var slot = 0; slot < found; slot++)
        {
            var cell = trio[slot];
            if (cells[cell] == color)
            {
                continue;
            }

            var donor = FindDonor(color, trio[..found]);
            if (donor < 0)
            {
                return;
            }

            (cells[cell], cells[donor]) = (cells[donor], cells[cell]);
            (sources[cell], sources[donor]) = (sources[donor], sources[cell]);
        }
    }

    private int Reach(int start, Span<int> trio)
    {
        var head = 0;
        var tail = 0;
        queue[tail++] = start;
        visited[start] = true;
        var found = 0;
        while (head < tail && found < trio.Length)
        {
            var index = queue[head++];
            trio[found++] = index;
            var column = index % Columns;
            var row = index / Columns;
            if (column > 0)
            {
                Enqueue(index - 1, ref tail);
            }

            if (column < Columns - 1)
            {
                Enqueue(index + 1, ref tail);
            }

            if (row > 0)
            {
                Enqueue(index - Columns, ref tail);
            }

            if (row < Rows - 1)
            {
                Enqueue(index + Columns, ref tail);
            }
        }

        return found;
    }

    private void Enqueue(int index, ref int tail)
    {
        if (visited[index] || cells[index] == Empty)
        {
            return;
        }

        visited[index] = true;
        queue[tail++] = index;
    }

    private int FindDonor(byte color, ReadOnlySpan<int> trio)
    {
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] != color || trio.Contains(index))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private byte MostCommonColor()
    {
        byte best = 1;
        for (var color = 2; color <= Colors; color++)
        {
            if (colorCounts[color] > colorCounts[best])
            {
                best = (byte)color;
            }
        }

        return best;
    }
}
