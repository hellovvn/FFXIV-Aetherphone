using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fuse;

internal static class FuseRenderer
{
    public const float ArenaMargin = 0.45f;
    public const float ScoreboardPad = 10f;
    private const float TileLift = 0.16f;
    private const float TileInset = 0.04f;
    private const float WallThickness = 0.32f;
    private const float MoogleRadius = 0.36f;
    private const float BombRadius = 0.33f;
    private const float ItemRadius = 0.3f;
    private const float FallDrop = 5f;
    public const int FlameLayers = 3;
    private const float OuterFlame = 0.82f;
    private const float MiddleFlame = 0.58f;
    private const float CoreFlame = 0.3f;
    private const float DotSize = 10f;
    private const float PipSize = 6f;
    private const float PipGap = 3f;
    private const float EntryGap = 9f;
    private const int MaxShownFalls = 4;
    private static readonly Vector4 SandLight = new(0.91f, 0.84f, 0.64f, 1f);
    private static readonly Vector4 SandDark = new(0.86f, 0.78f, 0.57f, 1f);
    private static readonly Vector4 WallTop = new(0.63f, 0.66f, 0.72f, 1f);
    private static readonly Vector4 WallFront = new(0.42f, 0.45f, 0.53f, 1f);
    private static readonly Vector4 PillarTop = new(0.72f, 0.75f, 0.82f, 1f);
    private static readonly Vector4 PillarFront = new(0.47f, 0.50f, 0.60f, 1f);
    public static readonly Vector4 CrateTop = new(0.82f, 0.58f, 0.32f, 1f);
    public static readonly Vector4 CrateFront = new(0.56f, 0.36f, 0.18f, 1f);
    private static readonly Vector4 CrateLine = new(0.48f, 0.30f, 0.14f, 1f);
    private static readonly Vector4 BlockTop = new(0.38f, 0.40f, 0.49f, 1f);
    private static readonly Vector4 BlockFront = new(0.23f, 0.24f, 0.31f, 1f);
    private static readonly Vector4 Hazard = new(1f, 0.78f, 0.22f, 1f);
    private static readonly Vector4 FireOuter = new(1f, 0.42f, 0.10f, 1f);
    private static readonly Vector4 FireMiddle = new(1f, 0.80f, 0.28f, 1f);
    private static readonly Vector4 FireCore = new(1f, 0.99f, 0.90f, 1f);
    private static readonly Vector4 Scorch = new(0.15f, 0.10f, 0.06f, 0.16f);
    private static readonly Vector4 KnockedOut = new(0.5f, 0.5f, 0.55f, 1f);
    private static readonly int[] FallCells = new int[MaxShownFalls];
    private static readonly float[] FallSeconds = new float[MaxShownFalls];

    public static Rect WorldRect => new(new Vector2(-ArenaMargin, -ArenaMargin * 1.6f),
        new Vector2(FuseBoard.Columns + ArenaMargin, FuseBoard.Rows + ArenaMargin));

    public static float ScoreboardWidth => ScoreboardWidthFor(FuseBoard.MaxMoogles);

    public static float ScoreboardWidthFor(int entries) =>
        ScoreboardPad * 2f + entries * (DotSize + PipGap + FuseBoard.WinsNeeded * (PipSize + PipGap)) +
        (entries - 1) * EntryGap;

    public static Vector4 TeamColor(int index) => GameSeats.Color(index);

    public static void Draw(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, ReadOnlySpan<float> itemAges,
        float time, float scale)
    {
        var cell = camera.Px(1f);
        DrawGround(drawList, board, in camera, cell);
        for (var row = 0; row < FuseBoard.Rows; row++)
        {
            for (var column = 0; column < FuseBoard.Columns; column++)
            {
                var index = FuseBoard.CellIndex(column, row);
                var min = camera.ToScreen(new Vector2(column, row));
                DrawTile(drawList, board.Tile(index), min, cell);
                var item = board.ItemAt(index);
                if (item != PowerUp.None)
                {
                    DrawItem(drawList, in camera, index, item, itemAges[index], cell, time);
                }
            }

            DrawBombsInRow(drawList, board, in camera, row, cell, time);
            DrawMooglesInRow(drawList, board, in camera, row, cell, time);
        }

        DrawFire(drawList, board, in camera, cell, time);
        DrawFalls(drawList, board, in camera, cell);
        DrawGhosts(drawList, board, in camera, cell, time);
    }

    public static void DrawScoreboard(ImDrawListPtr drawList, Rect rect, FuseBoard board, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var x = rect.Min.X + ScoreboardPad * scale;
        for (var index = 0; index < FuseBoard.MaxMoogles; index++)
        {
            ref readonly var moogle = ref board.MoogleAt(index);
            var alive = moogle.Alive || board.Phase != FusePhase.Fighting;
            x = DrawScoreEntry(drawList, x, rect.Center.Y, index, alive, moogle.Wins, scale);
        }
    }

    public static float DrawScoreEntry(ImDrawListPtr drawList, float x, float centerY, int index, bool alive, int wins,
        float scale)
    {
        var dotRadius = DotSize * 0.5f * scale;
        var dotCenter = new Vector2(x + dotRadius, centerY);
        drawList.AddCircleFilled(dotCenter, dotRadius,
            ImGui.GetColorU32(alive ? TeamColor(index) : KnockedOut with { W = 0.6f }), 14);
        if (index == FuseBoard.Player)
        {
            drawList.AddCircle(dotCenter, dotRadius + 1.5f * scale, ImGui.GetColorU32(FuseArt.White with { W = 0.85f }),
                16, 1.2f * scale);
        }

        x += DotSize * scale + PipGap * scale;
        for (var pip = 0; pip < FuseBoard.WinsNeeded; pip++)
        {
            var pipCenter = new Vector2(x + PipSize * 0.5f * scale, centerY);
            var filled = pip < wins;
            drawList.AddCircleFilled(pipCenter, PipSize * 0.5f * scale,
                ImGui.GetColorU32(filled ? FuseArt.Gold : FuseArt.White with { W = 0.22f }), 10);
            x += (PipSize + PipGap) * scale;
        }

        return x + EntryGap * scale;
    }

    private static void DrawGround(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, float cell)
    {
        var arenaMin = camera.ToScreen(Vector2.Zero);
        DrawFloor(drawList, arenaMin, FuseBoard.Columns, FuseBoard.Rows, cell);
        var scorch = ImGui.GetColorU32(Scorch);
        for (var row = 0; row < FuseBoard.Rows; row++)
        {
            for (var column = 0; column < FuseBoard.Columns; column++)
            {
                if (board.FireAt(FuseBoard.CellIndex(column, row)) <= 0f)
                {
                    continue;
                }

                var min = arenaMin + new Vector2(column * cell, row * cell);
                drawList.AddCircleFilled(min + new Vector2(cell * 0.5f, cell * 0.5f), cell * 0.42f, scorch, 14);
            }
        }

        DrawFloorEdge(drawList, arenaMin, FuseBoard.Columns, cell);
    }

    public static void DrawFloor(ImDrawListPtr drawList, Vector2 arenaMin, int columns, int rows, float cell)
    {
        var arenaMax = arenaMin + new Vector2(columns * cell, rows * cell);
        var wall = cell * WallThickness;
        var lift = cell * TileLift;
        var outerMin = arenaMin - new Vector2(wall, wall + lift);
        var outerMax = arenaMax + new Vector2(wall, wall);
        var radius = cell * 0.35f;
        drawList.AddRectFilled(outerMin + new Vector2(cell * 0.12f, cell * 0.22f), outerMax + new Vector2(cell * 0.12f, cell * 0.22f),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)), radius);
        drawList.AddRectFilled(outerMin + new Vector2(0f, lift), outerMax, ImGui.GetColorU32(WallFront), radius);
        drawList.AddRectFilled(outerMin, outerMax - new Vector2(0f, lift), ImGui.GetColorU32(WallTop), radius);
        drawList.AddRectFilled(arenaMin, arenaMax, ImGui.GetColorU32(SandDark), cell * 0.08f);
        var light = ImGui.GetColorU32(SandLight);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (((column + row) & 1) != 0)
                {
                    continue;
                }

                var min = arenaMin + new Vector2(column * cell, row * cell);
                drawList.AddRectFilled(min, min + new Vector2(cell, cell), light);
            }
        }
    }

    public static void DrawFloorEdge(ImDrawListPtr drawList, Vector2 arenaMin, int columns, float cell) =>
        drawList.AddRectFilled(arenaMin, new Vector2(arenaMin.X + columns * cell, arenaMin.Y + cell * 0.12f),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.18f)));

    public static void DrawTile(ImDrawListPtr drawList, FuseTile tile, Vector2 min, float cell)
    {
        switch (tile)
        {
            case FuseTile.Pillar:
                DrawSolid(drawList, min, cell, PillarTop, PillarFront, out var pillarTop);
                var bevel = cell * 0.14f;
                drawList.AddRect(pillarTop.Min + new Vector2(bevel, bevel), pillarTop.Max - new Vector2(bevel, bevel),
                    ImGui.GetColorU32(FuseArt.White with { W = 0.22f }), cell * 0.06f, ImDrawFlags.None, MathF.Max(1f, cell * 0.04f));
                return;
            case FuseTile.Crate:
                DrawSolid(drawList, min, cell, CrateTop, CrateFront, out var crateTop);
                DrawCrateFace(drawList, crateTop, cell);
                return;
            case FuseTile.Block:
                DrawSolid(drawList, min, cell, BlockTop, BlockFront, out var blockTop);
                DrawHazard(drawList, blockTop, cell, 1f);
                return;
            default:
                return;
        }
    }

    private static void DrawSolid(ImDrawListPtr drawList, Vector2 min, float cell, Vector4 top, Vector4 front, out Rect topFace,
        float alpha = 1f)
    {
        var inset = cell * TileInset;
        var lift = cell * TileLift;
        var max = min + new Vector2(cell, cell);
        var rounding = cell * 0.12f;
        drawList.AddRectFilled(new Vector2(min.X + inset + cell * 0.08f, min.Y + inset + cell * 0.1f),
            new Vector2(max.X - inset + cell * 0.08f, max.Y - inset + cell * 0.1f),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.2f * alpha)), rounding);
        drawList.AddRectFilled(new Vector2(min.X + inset, min.Y + inset), new Vector2(max.X - inset, max.Y - inset),
            FuseArt.Color(front, alpha), rounding);
        topFace = new Rect(new Vector2(min.X + inset, min.Y + inset - lift), new Vector2(max.X - inset, max.Y - inset - lift));
        drawList.AddRectFilled(topFace.Min, topFace.Max, FuseArt.Color(top, alpha), rounding);
        drawList.AddRectFilled(topFace.Min, new Vector2(topFace.Max.X, topFace.Min.Y + cell * 0.12f),
            FuseArt.Color(FuseArt.White, 0.16f * alpha), rounding, ImDrawFlags.RoundCornersTop);
    }

    private static void DrawCrateFace(ImDrawListPtr drawList, Rect face, float cell)
    {
        var line = ImGui.GetColorU32(CrateLine);
        var thickness = MathF.Max(1f, cell * 0.06f);
        var inner = cell * 0.1f;
        drawList.AddRect(face.Min + new Vector2(inner, inner), face.Max - new Vector2(inner, inner), line, cell * 0.04f,
            ImDrawFlags.None, thickness);
        drawList.AddLine(face.Min + new Vector2(inner, inner), face.Max - new Vector2(inner, inner), line, thickness);
        drawList.AddLine(new Vector2(face.Min.X + inner, face.Center.Y), new Vector2(face.Max.X - inner, face.Center.Y),
            ImGui.GetColorU32(CrateLine with { W = 0.45f }), MathF.Max(1f, cell * 0.03f));
    }

    private static void DrawHazard(ImDrawListPtr drawList, Rect face, float cell, float alpha)
    {
        var stripe = FuseArt.Color(Hazard, 0.85f * alpha);
        var thickness = MathF.Max(1f, cell * 0.08f);
        var inner = cell * 0.14f;
        for (var band = 0; band < 3; band++)
        {
            var offset = (band - 1) * cell * 0.24f;
            drawList.AddLine(new Vector2(face.Min.X + inner + offset, face.Max.Y - inner),
                new Vector2(face.Min.X + inner + offset + cell * 0.36f, face.Min.Y + inner), stripe, thickness);
        }

        drawList.AddRect(face.Min + new Vector2(inner * 0.5f, inner * 0.5f), face.Max - new Vector2(inner * 0.5f, inner * 0.5f),
            FuseArt.Color(Hazard, 0.6f * alpha), cell * 0.05f, ImDrawFlags.None, MathF.Max(1f, cell * 0.04f));
    }

    private static void DrawItem(ImDrawListPtr drawList, in Camera2D camera, int index, PowerUp kind, float age, float cell,
        float time) =>
        DrawItemAt(drawList, camera.ToScreen(FuseBoard.CellCenter(index)), cell, kind, age, time, index);

    public static void DrawItemAt(ImDrawListPtr drawList, Vector2 cellCenter, float cell, PowerUp kind, float age,
        float time, int seed)
    {
        var pop = GameJuice.PopIn(Math.Clamp(age / 0.35f, 0f, 1f));
        var bob = MathF.Sin(time * 3f + seed) * 0.07f;
        var ground = cellCenter + new Vector2(0f, 0.28f * cell);
        Shapes.FillEllipse(drawList, ground, cell * 0.24f * (1f - bob), cell * 0.08f, FuseArt.Color(FuseArt.Shadow, 0.8f));
        var center = cellCenter + new Vector2(0f, (-0.12f + bob) * cell);
        FuseArt.DrawPowerUp(drawList, center, cell * ItemRadius * MathF.Max(0.01f, pop), kind, 1f, time);
    }

    private static void DrawBombsInRow(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, int row, float cell,
        float time)
    {
        for (var slot = 0; slot < FuseBoard.BombCapacity; slot++)
        {
            ref readonly var bomb = ref board.BombAt(slot);
            if (!bomb.Alive || FuseBoard.RowOf(bomb.Cell) != row)
            {
                continue;
            }

            var center = camera.ToScreen(bomb.Position + new Vector2(0f, 0.02f));
            DrawLiveBomb(drawList, center, cell, bomb.Fuse, bomb.Pulse, bomb.Age, time + slot);
        }
    }

    public static void DrawLiveBomb(ImDrawListPtr drawList, Vector2 center, float cell, float fuse, float pulse, float age,
        float time)
    {
        var progress = 1f - Math.Clamp(fuse / FuseBoard.FuseSeconds, 0f, 1f);
        var wave = MathF.Sin(pulse * MathF.Tau);
        var squash = wave * (0.05f + 0.11f * progress);
        var heat = fuse < 0.6f ? (0.5f + 0.5f * wave) * (1f - fuse / 0.6f) : 0f;
        var pop = GameJuice.PopIn(Math.Clamp(age / 0.2f, 0f, 1f));
        var radius = cell * BombRadius * MathF.Max(0.01f, pop);
        FuseArt.DrawBomb(drawList, center, radius, squash, heat, 1f, time);
    }

    private static void DrawMooglesInRow(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, int row, float cell,
        float time)
    {
        for (var index = 0; index < FuseBoard.MaxMoogles; index++)
        {
            ref readonly var moogle = ref board.MoogleAt(index);
            if (!moogle.Alive || FuseBoard.RowOf(FuseBoard.CellAt(moogle.Position)) != row)
            {
                continue;
            }

            var winner = board.Phase == FusePhase.Ended && board.LastWinner == index;
            var feet = camera.ToScreen(moogle.Position + new Vector2(0f, 0.34f));
            DrawMoogleAt(drawList, feet, cell, index, moogle.Facing, moogle.Stride, moogle.Moving, winner, time);
        }
    }

    public static void DrawMoogleAt(ImDrawListPtr drawList, Vector2 feet, float cell, int index, FuseDirection facing,
        float stride, bool moving, bool winner, float time)
    {
        var hop = winner ? MathF.Abs(MathF.Sin(time * 7f)) * 0.5f : 0f;
        var radius = cell * MoogleRadius;
        if (index == FuseBoard.Player)
        {
            Shapes.FillEllipse(drawList, feet, radius * 1.15f, radius * 0.42f,
                FuseArt.Color(TeamColor(index), 0.35f + 0.15f * MathF.Sin(time * 4f)));
        }

        FuseArt.DrawMoogle(drawList, feet, radius, TeamColor(index), facing, stride, moving, 1f, hop, time + index);
        if (winner)
        {
            FuseArt.DrawCrown(drawList, feet - new Vector2(0f, radius * (2.75f + hop)), radius * 0.9f, 1f);
        }
    }

    private static void DrawFire(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, float cell, float time)
    {
        for (var layer = 0; layer < FlameLayers; layer++)
        {
            DrawFirePass(drawList, board, in camera, cell, time, layer);
        }
    }

    private static void DrawFirePass(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, float cell, float time,
        int layer)
    {
        for (var index = 0; index < FuseBoard.CellCount; index++)
        {
            var seconds = board.FireAt(index);
            if (seconds <= 0f)
            {
                continue;
            }

            var center = camera.ToScreen(FuseBoard.CellCenter(index) - new Vector2(0f, 0.08f));
            DrawFlame(drawList, center, cell, board.FireArmsAt(index), FireEnvelope(1f - seconds / FuseBoard.FireSeconds),
                time, index, layer);
        }
    }

    public static float FireEnvelope(float age) =>
        MathF.Min(1f, age / 0.12f) * (age > 0.55f ? 1f - (age - 0.55f) / 0.45f : 1f);

    public static void DrawFlame(ImDrawListPtr drawList, Vector2 center, float cell, byte arms, float envelope, float time,
        int seed, int layer)
    {
        var widthFraction = layer switch
        {
            0 => OuterFlame,
            1 => MiddleFlame,
            _ => CoreFlame,
        };
        var color = layer switch
        {
            0 => FireOuter,
            1 => FireMiddle,
            _ => FireCore,
        };
        var flicker = 0.84f + 0.16f * MathF.Sin(time * 31f + seed * 1.7f + widthFraction * 9f);
        var width = cell * widthFraction * envelope * flicker;
        if (width < 0.5f)
        {
            return;
        }

        var tint = ImGui.GetColorU32(color with { W = MathF.Min(1f, 0.35f + envelope) });
        var core = (arms & FuseBoard.ArmCore) != 0;
        drawList.AddCircleFilled(center, width * (core ? 0.62f : 0.5f), tint, 16);
        var directions = FuseBoard.Directions;
        for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            var direction = directions[directionIndex];
            if ((arms & FuseBoard.ArmFlag(direction)) == 0)
            {
                continue;
            }

            var edge = center + FuseBoard.Vector(direction) * cell * 0.52f;
            drawList.AddLine(center, edge, tint, width);
        }

        if (core && layer == 0)
        {
            ProgressRing.Glow(center, cell * 0.9f * envelope, FireMiddle, 0.6f);
        }
    }

    private static void DrawFalls(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, float cell)
    {
        if (!board.SuddenDeath && board.Elapsed < FuseBoard.SuddenDeathStart - FuseBoard.FallWarning)
        {
            return;
        }

        var count = board.UpcomingFalls(FallCells, FallSeconds);
        for (var entry = count - 1; entry >= 0; entry--)
        {
            var left = FallSeconds[entry];
            if (left > FuseBoard.FallWarning)
            {
                continue;
            }

            var progress = Math.Clamp(1f - left / FuseBoard.FallWarning, 0f, 1f);
            var target = FallCells[entry];
            var min = camera.ToScreen(new Vector2(FuseBoard.ColumnOf(target), FuseBoard.RowOf(target)));
            DrawFallingBlock(drawList, min, cell, progress);
        }
    }

    public static void DrawFallingBlock(ImDrawListPtr drawList, Vector2 min, float cell, float progress)
    {
        var inset = cell * (0.5f - 0.4f * progress);
        drawList.AddRectFilled(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f * progress)), cell * 0.1f);
        var drop = (1f - progress) * (1f - progress) * FallDrop * cell;
        DrawSolid(drawList, min - new Vector2(0f, drop), cell, BlockTop, BlockFront, out var face,
            MathF.Min(1f, progress * 3f));
        DrawHazard(drawList, face, cell, MathF.Min(1f, progress * 3f));
    }

    private static void DrawGhosts(ImDrawListPtr drawList, FuseBoard board, in Camera2D camera, float cell, float time)
    {
        for (var index = 0; index < FuseBoard.MaxMoogles; index++)
        {
            ref readonly var moogle = ref board.MoogleAt(index);
            if (moogle.Alive || moogle.Knockout <= 0f || moogle.Knockout >= 1f)
            {
                continue;
            }

            var progress = moogle.Knockout;
            var drift = new Vector2(MathF.Sin(progress * 9f) * 0.18f, -0.3f - progress * 1.8f);
            var alpha = (1f - progress) * MathF.Min(1f, progress * 8f);
            FuseArt.DrawGhost(drawList, camera.ToScreen(moogle.Position + drift), cell * 0.32f, TeamColor(index), alpha,
                time + index);
        }
    }
}
