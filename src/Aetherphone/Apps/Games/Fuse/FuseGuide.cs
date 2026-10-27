using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fuse;

internal static class FuseGuide
{
    private const float SideMargin = 0.45f;
    private const float TopMargin = 0.72f;
    private const float FeetOffset = 0.34f;
    private const float BombLift = 0.02f;
    private const float FlameLift = 0.08f;
    private const float StrideRate = 7.5f;
    private const float PanelShare = 0.3f;
    private const float PanelMinimum = 88f;
    private const float PanelGap = 10f;
    private const float PanelInset = 6f;
    private const float PanelOpacity = 0.9f;
    private const float KeyStep = 38f;
    private const float ClusterHalf = 60f;
    private const float SpaceHalf = 62f;
    private const float HoldPress = 0.2f;
    private const float TapSeconds = 0.5f;
    private const float TapLead = 0.1f;
    private const float PressWindow = 0.09f;
    private const float RingSeconds = 0.4f;
    private const float GlowSeconds = 0.5f;
    private const float BumpScale = 0.25f;
    private const int MaxCells = 35;

    private const int DropColumns = 7;
    private const int DropRows = 3;
    private const float DropLoop = 4.8f;
    private const float DropWalkStart = 0.3f;
    private const float DropAt = 1f;
    private const float DropEscapeStart = 1.4f;
    private const float DropApproachCells = 2f;
    private const float DropBlast = DropAt + FuseBoard.FuseSeconds;
    private const float SafeMark = DropBlast + FuseBoard.FireSeconds;

    private const int ChainColumns = 7;
    private const int ChainRows = 5;
    private const float ChainLoop = 3.6f;
    private const float FirstBombPlaced = -1f;
    private const float SecondBombPlaced = 0.15f;
    private const float FirstBlast = FirstBombPlaced + FuseBoard.FuseSeconds;
    private const float ChainDelay = 0.15f;
    private const float SecondBlast = FirstBlast + ChainDelay;

    private const int PickupColumns = 7;
    private const int PickupRows = 3;
    private const float PickupLoop = 3.8f;
    private const float PickupWalkStart = 0.4f;
    private const int FirstPickupColumn = 2;
    private const int PickupRow = 0;
    private const float RevealedAge = 1f;

    private const int RoundColumns = 5;
    private const int RoundRows = 3;
    private const float RoundLoop = 4.8f;
    private const float FirstFall = 0.9f;
    private const int CrushFall = 4;
    private const float RoundWin = FirstFall + CrushFall * FuseBoard.FallInterval + FuseBoard.SettleSeconds;
    private const float CapsuleHeight = 26f;

    private static readonly FuseTile[] Scene = new FuseTile[MaxCells];
    private static readonly FuseTile[] DropTiles = Layout("....cc." + ".#.#.#." + "..c.c.c");
    private static readonly int[] DropCrates = { 4, 16 };
    private static readonly Vector2[] DropApproach = { new(0.5f, 0.5f), new(2.5f, 0.5f) };
    private static readonly Vector2[] DropEscape = { new(2.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 1.5f) };
    private static readonly Vector2 DropBomb = new(2.5f, 0.5f);

    private static readonly FlameCell[] DropFlames =
    {
        new(2, 0, FuseBoard.ArmCore | FuseBoard.ArmRight | FuseBoard.ArmDown | FuseBoard.ArmLeft, 0),
        new(1, 0, FuseBoard.ArmRight | FuseBoard.ArmLeft, 0),
        new(0, 0, FuseBoard.ArmRight, 0),
        new(3, 0, FuseBoard.ArmLeft | FuseBoard.ArmRight, 0),
        new(4, 0, FuseBoard.ArmLeft, 0),
        new(2, 1, FuseBoard.ArmUp | FuseBoard.ArmDown, 0),
        new(2, 2, FuseBoard.ArmUp, 0),
    };

    private static readonly FuseTile[] ChainTiles =
        Layout("c.c...." + ".#.#.#." + "cc....c" + ".#.#.#." + "....c.c");

    private static readonly int[] FirstBlastCrates = { 2, 15 };
    private static readonly int[] SecondBlastCrates = { 20, 32 };
    private static readonly Vector2 FirstBomb = new(2.5f, 2.5f);
    private static readonly Vector2 SecondBomb = new(4.5f, 2.5f);
    private static readonly Vector2 Watcher = new(0.5f, 4.5f);

    private static readonly FlameCell[] ChainFlames =
    {
        new(2, 2, FuseBoard.ArmCore | FuseBoard.ArmUp | FuseBoard.ArmRight | FuseBoard.ArmDown | FuseBoard.ArmLeft, 0),
        new(2, 1, FuseBoard.ArmUp | FuseBoard.ArmDown, 0),
        new(2, 0, FuseBoard.ArmDown, 0),
        new(1, 2, FuseBoard.ArmRight, 0),
        new(3, 2, FuseBoard.ArmLeft | FuseBoard.ArmRight, 0),
        new(4, 2, FuseBoard.ArmLeft, 0),
        new(2, 3, FuseBoard.ArmUp | FuseBoard.ArmDown, 0),
        new(2, 4, FuseBoard.ArmUp, 0),
        new(4, 2, FuseBoard.ArmCore | FuseBoard.ArmUp | FuseBoard.ArmRight | FuseBoard.ArmDown | FuseBoard.ArmLeft, 1),
        new(3, 2, FuseBoard.ArmLeft | FuseBoard.ArmRight, 1),
        new(2, 2, FuseBoard.ArmRight, 1),
        new(5, 2, FuseBoard.ArmLeft | FuseBoard.ArmRight, 1),
        new(6, 2, FuseBoard.ArmLeft, 1),
        new(4, 1, FuseBoard.ArmUp | FuseBoard.ArmDown, 1),
        new(4, 0, FuseBoard.ArmDown, 1),
        new(4, 3, FuseBoard.ArmUp | FuseBoard.ArmDown, 1),
        new(4, 4, FuseBoard.ArmUp, 1),
    };

    private static readonly FuseTile[] PickupTiles = Layout("......." + ".#.#.#." + "c.c.c.c");
    private static readonly Vector2[] PickupPath = { new(0.5f, 0.5f), new(6.5f, 0.5f) };

    private static readonly PowerUp[] PickupKinds =
    {
        PowerUp.ExtraBomb, PowerUp.Range, PowerUp.Speed, PowerUp.Kick,
    };

    private static readonly int[] PickupBefore = { 1, FuseBoard.StartRange, 1, 0 };

    private static readonly FuseTile[] RoundTiles = Layout("....." + ".#.#." + ".....");
    private static readonly int[] FallOrder = { 0, 1, 2, 3, 4, 9, 14 };
    private static readonly Vector2 RoundPlayer = new(2.5f, 2.5f);
    private static readonly Vector2 RoundRival = new(4.5f, 0.5f);

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Fuse.Guide1Title, L.Fuse.Guide1Body, DrawDropPage),
        new GuidePage(L.Fuse.Guide2Title, L.Fuse.Guide2Body, DrawChainPage),
        new GuidePage(L.Fuse.Guide3Title, L.Fuse.Guide3Body, DrawPickupPage),
        new GuidePage(L.Fuse.Guide4Title, L.Fuse.Guide4Body, DrawRoundPage));

    private static void DrawDropPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % DropLoop;
        var keyboard = (int)(seconds / DropLoop) % 2 == 1;
        var panel = Panel(art, scale);
        var arena = Place(new Rect(art.Min, new Vector2(art.Max.X, panel.Min.Y - PanelGap * scale)), DropColumns,
            DropRows);
        LoadScene(DropTiles);
        if (time >= DropBlast)
        {
            BreakCrates(DropCrates);
        }

        FuseRenderer.DrawFloor(drawList, arena.Min, DropColumns, DropRows, arena.Cell);
        FuseRenderer.DrawFloorEdge(drawList, arena.Min, DropColumns, arena.Cell);
        Vector2 position;
        FuseDirection facing;
        float distance;
        bool walking;
        if (time < DropEscapeStart)
        {
            walking = Walk(DropApproach, time - DropWalkStart, out position, out facing, out distance);
        }
        else
        {
            walking = Walk(DropEscape, time - DropEscapeStart, out position, out facing, out distance);
            distance += DropApproachCells;
        }

        var bombLive = time >= DropAt && time < DropBlast;
        var moogleRow = (int)position.Y;
        for (var row = 0; row < DropRows; row++)
        {
            DrawTiles(drawList, in arena, DropColumns, row);
            if (bombLive && row == (int)DropBomb.Y)
            {
                DrawBomb(drawList, in arena, DropBomb, time - DropAt, time);
            }

            if (row == moogleRow)
            {
                FuseRenderer.DrawMoogleAt(drawList, arena.Feet(position), arena.Cell, FuseBoard.Player, facing,
                    distance * StrideRate, walking, false, time);
            }
        }

        DrawFlames(drawList, in arena, DropFlames, DropBlast, DropBlast, time);
        DrawRing(drawList, arena.Center(DropBomb), arena.Cell, time - DropBlast, scale);
        var safe = time - SafeMark;
        if (safe > 0f)
        {
            var pop = GameJuice.PopIn(Math.Clamp(safe / 0.3f, 0f, 1f));
            GuideKit.Check(drawList, arena.Feet(position) - new Vector2(0f, arena.Cell * 1.45f),
                arena.Cell * 0.5f * pop, scale);
        }

        var pressed = time >= DropAt - PressWindow && time < DropAt + PressWindow;
        var ready = time < DropAt || time >= DropBlast;
        var flash = time >= DropAt ? MathF.Max(0f, 1f - (time - DropAt) * 4f) : 0f;
        var held = walking ? facing : FuseDirection.None;
        var controls = new Controls(panel, scale);
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PanelOpacity);
        if (keyboard)
        {
            DrawKeys(drawList, in controls, held, pressed, accent, scale);
            return;
        }

        FusePad.DrawCross(drawList, controls.PadCenter, controls.PadRadius, held, accent, scale);
        FusePad.DrawBombButton(drawList, controls.BombCenter, controls.BombRadius, ready, pressed, flash, 1,
            ready ? 1 : 0, accent, scale, time);
        if (walking)
        {
            GuideKit.Tap(drawList, controls.Arrow(facing), HoldPress, scale);
            return;
        }

        if (time < DropWalkStart)
        {
            GuideKit.Tap(drawList, controls.Arrow(FuseDirection.Right), 0f, scale);
            return;
        }

        if (time < DropEscapeStart)
        {
            GuideKit.Tap(drawList, controls.BombCenter,
                Math.Clamp((time - DropAt + TapLead) / TapSeconds, 0f, 1f), scale);
        }
    }

    private static void DrawChainPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % ChainLoop;
        var arena = Place(art, ChainColumns, ChainRows);
        LoadScene(ChainTiles);
        if (time >= FirstBlast)
        {
            BreakCrates(FirstBlastCrates);
        }

        if (time >= SecondBlast)
        {
            BreakCrates(SecondBlastCrates);
        }

        FuseRenderer.DrawFloor(drawList, arena.Min, ChainColumns, ChainRows, arena.Cell);
        FuseRenderer.DrawFloorEdge(drawList, arena.Min, ChainColumns, arena.Cell);
        for (var row = 0; row < ChainRows; row++)
        {
            DrawTiles(drawList, in arena, ChainColumns, row);
            if (row != (int)FirstBomb.Y)
            {
                if (row == (int)Watcher.Y)
                {
                    FuseRenderer.DrawMoogleAt(drawList, arena.Feet(Watcher), arena.Cell, FuseBoard.Player,
                        FuseDirection.Up, 0f, false, false, time);
                }

                continue;
            }

            DrawRevealed(drawList, in arena, new Vector2(1.5f, 2.5f), PowerUp.Range,
                time - FirstBlast - FuseBoard.FireSeconds, time);
            DrawRevealed(drawList, in arena, new Vector2(6.5f, 2.5f), PowerUp.ExtraBomb,
                time - SecondBlast - FuseBoard.FireSeconds, time);
            if (time < FirstBlast)
            {
                DrawBomb(drawList, in arena, FirstBomb, time - FirstBombPlaced, time);
            }

            if (time < SecondBlast)
            {
                DrawBomb(drawList, in arena, SecondBomb, time - SecondBombPlaced, time + 1f);
            }
        }

        DrawFlames(drawList, in arena, ChainFlames, FirstBlast, SecondBlast, time);
        DrawRing(drawList, arena.Center(FirstBomb), arena.Cell, time - FirstBlast, scale);
        DrawRing(drawList, arena.Center(SecondBomb), arena.Cell, time - SecondBlast, scale);
    }

    private static void DrawPickupPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % PickupLoop;
        var panel = Panel(art, scale);
        var arena = Place(new Rect(art.Min, new Vector2(art.Max.X, panel.Min.Y - PanelGap * scale)), PickupColumns,
            PickupRows);
        LoadScene(PickupTiles);
        FuseRenderer.DrawFloor(drawList, arena.Min, PickupColumns, PickupRows, arena.Cell);
        FuseRenderer.DrawFloorEdge(drawList, arena.Min, PickupColumns, arena.Cell);
        var walking = Walk(PickupPath, time - PickupWalkStart, out var position, out var facing, out var distance);
        for (var row = 0; row < PickupRows; row++)
        {
            DrawTiles(drawList, in arena, PickupColumns, row);
            if (row != PickupRow)
            {
                continue;
            }

            for (var index = 0; index < PickupKinds.Length; index++)
            {
                if (time >= PickupTime(index))
                {
                    continue;
                }

                var column = FirstPickupColumn + index;
                FuseRenderer.DrawItemAt(drawList, arena.Center(new Vector2(column + 0.5f, row + 0.5f)), arena.Cell,
                    PickupKinds[index], RevealedAge, time, column);
            }

            FuseRenderer.DrawMoogleAt(drawList, arena.Feet(position), arena.Cell, FuseBoard.Player, facing,
                distance * StrideRate, walking, false, time);
        }

        for (var index = 0; index < PickupKinds.Length; index++)
        {
            var center = arena.Center(new Vector2(FirstPickupColumn + index + 0.5f, PickupRow + 0.5f));
            DrawPickupRing(drawList, center, arena.Cell, PickupKinds[index], time - PickupTime(index), scale);
        }

        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PanelOpacity);
        DrawStats(drawList, panel, time, scale);
    }

    private static void DrawRoundPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % RoundLoop;
        var capsuleHeight = CapsuleHeight * scale;
        var capsuleTop = art.Max.Y - capsuleHeight;
        var arena = Place(new Rect(art.Min, new Vector2(art.Max.X, capsuleTop - PanelGap * scale)), RoundColumns,
            RoundRows);
        LoadScene(RoundTiles);
        var settled = time >= RoundWin;
        for (var fall = 0; fall < FallOrder.Length; fall++)
        {
            if (time >= FallLanding(fall))
            {
                Scene[FallOrder[fall]] = FuseTile.Block;
            }
        }

        FuseRenderer.DrawFloor(drawList, arena.Min, RoundColumns, RoundRows, arena.Cell);
        FuseRenderer.DrawFloorEdge(drawList, arena.Min, RoundColumns, arena.Cell);
        var crushed = FallLanding(CrushFall);
        for (var row = 0; row < RoundRows; row++)
        {
            DrawTiles(drawList, in arena, RoundColumns, row);
            if (row == (int)RoundRival.Y && time < crushed)
            {
                FuseRenderer.DrawMoogleAt(drawList, arena.Feet(RoundRival), arena.Cell, 1, FuseDirection.Left, 0f, false,
                    false, time);
            }

            if (row == (int)RoundPlayer.Y)
            {
                FuseRenderer.DrawMoogleAt(drawList, arena.Feet(RoundPlayer), arena.Cell, FuseBoard.Player,
                    FuseDirection.Down, 0f, false, settled, time);
            }
        }

        for (var fall = 0; fall < FallOrder.Length; fall++)
        {
            var progress = 1f - (FallLanding(fall) - time) / FuseBoard.FallWarning;
            if (progress <= 0f || progress >= 1f)
            {
                continue;
            }

            var cell = FallOrder[fall];
            FuseRenderer.DrawFallingBlock(drawList, arena.CellMin(cell % RoundColumns, cell / RoundColumns), arena.Cell,
                progress);
        }

        var knockout = (time - crushed) / FuseBoard.GhostSeconds;
        if (knockout > 0f && knockout < 1f)
        {
            var drift = new Vector2(MathF.Sin(knockout * 9f) * 0.18f, -0.3f - knockout * 1.8f);
            var alpha = (1f - knockout) * MathF.Min(1f, knockout * 8f);
            FuseArt.DrawGhost(drawList, arena.Center(RoundRival + drift), arena.Cell * 0.32f, FuseRenderer.TeamColor(1),
                alpha, time + 1f);
        }

        var capsuleWidth = FuseRenderer.ScoreboardWidthFor(2) * scale;
        var capsule = new Rect(new Vector2(art.Center.X - capsuleWidth * 0.5f, capsuleTop),
            new Vector2(art.Center.X + capsuleWidth * 0.5f, art.Max.Y));
        StageHud.Capsule(drawList, capsule, scale);
        var x = capsule.Min.X + FuseRenderer.ScoreboardPad * scale;
        x = FuseRenderer.DrawScoreEntry(drawList, x, capsule.Center.Y, FuseBoard.Player, true, settled ? 2 : 1, scale);
        FuseRenderer.DrawScoreEntry(drawList, x, capsule.Center.Y, 1, time < crushed || settled, 1, scale);
    }

    private static float FallLanding(int fall) => FirstFall + fall * FuseBoard.FallInterval;

    private static float PickupTime(int index) =>
        PickupWalkStart + (FirstPickupColumn + index - 0.5f) / FuseBoard.BaseSpeed;

    private static void DrawStats(ImDrawListPtr drawList, Rect panel, float time, float scale)
    {
        var inner = panel.Inset(PanelInset * scale);
        var slot = inner.Width / PickupKinds.Length;
        var radius = MathF.Min(inner.Height * 0.26f, slot * 0.2f);
        var style = TextStyles.Title3;
        for (var index = 0; index < PickupKinds.Length; index++)
        {
            var kind = PickupKinds[index];
            var since = time - PickupTime(index);
            var taken = since >= 0f;
            var center = new Vector2(inner.Min.X + slot * (index + 0.5f), inner.Center.Y);
            var icon = kind == PowerUp.Kick ? center : center - new Vector2(radius * 1.1f, 0f);
            var bump = taken && since < GlowSeconds ? MathF.Sin(MathF.PI * since / GlowSeconds) : 0f;
            if (bump > 0f)
            {
                ProgressRing.Glow(icon, radius * 1.3f, FuseArt.PowerUpColor(kind), bump);
            }

            FuseArt.DrawPowerUp(drawList, icon, radius * (1f + BumpScale * bump), kind,
                kind == PowerUp.Kick && !taken ? 0.3f : 1f, time);
            if (kind == PowerUp.Kick)
            {
                continue;
            }

            var label = GameNumber.Label(PickupBefore[index] + (taken ? 1 : 0));
            var height = Typography.LineHeight(style);
            Typography.Draw(drawList, new Vector2(icon.X + radius * 1.5f, center.Y - height * 0.5f), label,
                taken ? FuseArt.Gold : StageInks.Strong, style);
        }
    }

    private static void DrawPickupRing(ImDrawListPtr drawList, Vector2 center, float cell, PowerUp kind, float since,
        float scale)
    {
        if (since < 0f || since >= RingSeconds)
        {
            return;
        }

        var progress = since / RingSeconds;
        var color = FuseArt.PowerUpColor(kind);
        drawList.AddCircle(center, cell * (0.3f + 0.5f * Easing.EaseOutCubic(progress)),
            FuseArt.Color(color, 1f - progress), 24, 3f * scale);
    }

    private static void DrawRing(ImDrawListPtr drawList, Vector2 center, float cell, float since, float scale)
    {
        if (since < 0f || since >= RingSeconds)
        {
            return;
        }

        var progress = since / RingSeconds;
        drawList.AddCircle(center, cell * (0.4f + 1.2f * Easing.EaseOutCubic(progress)),
            FuseArt.Color(Vector4.Lerp(FuseArt.White, FuseArt.Spark, progress), 1f - progress), 32, 3f * scale);
    }

    private static void DrawRevealed(ImDrawListPtr drawList, in Arena arena, Vector2 position, PowerUp kind, float age,
        float time)
    {
        if (age < 0f)
        {
            return;
        }

        FuseRenderer.DrawItemAt(drawList, arena.Center(position), arena.Cell, kind, age, time, (int)position.X);
    }

    private static void DrawBomb(ImDrawListPtr drawList, in Arena arena, Vector2 position, float age, float time)
    {
        FuseRenderer.DrawLiveBomb(drawList, arena.Center(position + new Vector2(0f, BombLift)), arena.Cell,
            FuseBoard.FuseSeconds - age, FuseBoard.PulseAfter(MathF.Max(0f, age)), age, time);
    }

    private static void DrawFlames(ImDrawListPtr drawList, in Arena arena, ReadOnlySpan<FlameCell> flames,
        float firstBlast, float secondBlast, float time)
    {
        for (var layer = 0; layer < FuseRenderer.FlameLayers; layer++)
        {
            for (var index = 0; index < flames.Length; index++)
            {
                var flame = flames[index];
                var age = (time - (flame.Blast == 0 ? firstBlast : secondBlast)) / FuseBoard.FireSeconds;
                if (age < 0f || age >= 1f)
                {
                    continue;
                }

                var center = arena.Center(new Vector2(flame.Column + 0.5f, flame.Row + 0.5f - FlameLift));
                FuseRenderer.DrawFlame(drawList, center, arena.Cell, (byte)flame.Arms, FuseRenderer.FireEnvelope(age), time,
                    flame.Row * FuseBoard.Columns + flame.Column, layer);
            }
        }
    }

    private static void DrawKeys(ImDrawListPtr drawList, in Controls controls, FuseDirection held, bool bomb,
        Vector4 accent, float scale)
    {
        var step = KeyStep * scale;
        var center = new Vector2(controls.Inner.Min.X + ClusterHalf * scale, controls.Inner.Center.Y);
        GuideKit.ArrowKey(drawList, center - new Vector2(0f, step * 0.5f), new Vector2(0f, -1f),
            held == FuseDirection.Up, accent, scale);
        GuideKit.ArrowKey(drawList, center + new Vector2(-step, step * 0.5f), new Vector2(-1f, 0f),
            held == FuseDirection.Left, accent, scale);
        GuideKit.ArrowKey(drawList, center + new Vector2(0f, step * 0.5f), new Vector2(0f, 1f),
            held == FuseDirection.Down, accent, scale);
        GuideKit.ArrowKey(drawList, center + new Vector2(step, step * 0.5f), new Vector2(1f, 0f),
            held == FuseDirection.Right, accent, scale);
        GuideKit.SpaceKey(drawList, new Vector2(controls.Inner.Max.X - SpaceHalf * scale, controls.Inner.Center.Y), bomb,
            accent, scale);
    }

    private static bool Walk(ReadOnlySpan<Vector2> path, float elapsed, out Vector2 position, out FuseDirection facing,
        out float distance)
    {
        var travel = MathF.Max(0f, elapsed) * FuseBoard.BaseSpeed;
        distance = 0f;
        facing = Heading(path[1] - path[0]);
        for (var index = 1; index < path.Length; index++)
        {
            var from = path[index - 1];
            var to = path[index];
            var length = Vector2.Distance(from, to);
            facing = Heading(to - from);
            if (travel < distance + length)
            {
                position = Vector2.Lerp(from, to, (travel - distance) / length);
                distance = travel;
                return elapsed > 0f;
            }

            distance += length;
        }

        position = path[^1];
        return false;
    }

    private static FuseDirection Heading(Vector2 delta)
    {
        if (MathF.Abs(delta.X) >= MathF.Abs(delta.Y))
        {
            return delta.X >= 0f ? FuseDirection.Right : FuseDirection.Left;
        }

        return delta.Y >= 0f ? FuseDirection.Down : FuseDirection.Up;
    }

    private static Rect Panel(Rect art, float scale)
    {
        var height = MathF.Max(art.Height * PanelShare, PanelMinimum * scale);
        return new Rect(new Vector2(art.Min.X, art.Max.Y - height), art.Max);
    }

    private static Arena Place(Rect area, int columns, int rows)
    {
        var width = columns + SideMargin * 2f;
        var height = rows + TopMargin + SideMargin;
        var fit = GuideKit.Fit(area, width / height);
        var cell = fit.Width / width;
        return new Arena(fit.Min + new Vector2(SideMargin, TopMargin) * cell, cell);
    }

    private static void LoadScene(FuseTile[] tiles)
    {
        tiles.AsSpan().CopyTo(Scene);
    }

    private static void BreakCrates(ReadOnlySpan<int> cells)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            Scene[cells[index]] = FuseTile.Floor;
        }
    }

    private static void DrawTiles(ImDrawListPtr drawList, in Arena arena, int columns, int row)
    {
        for (var column = 0; column < columns; column++)
        {
            FuseRenderer.DrawTile(drawList, Scene[row * columns + column], arena.CellMin(column, row), arena.Cell);
        }
    }

    private static FuseTile[] Layout(string cells)
    {
        var tiles = new FuseTile[cells.Length];
        for (var index = 0; index < cells.Length; index++)
        {
            tiles[index] = cells[index] switch
            {
                '#' => FuseTile.Pillar,
                'c' => FuseTile.Crate,
                _ => FuseTile.Floor,
            };
        }

        return tiles;
    }

    private readonly record struct FlameCell(int Column, int Row, int Arms, int Blast);

    private readonly struct Arena
    {
        public readonly Vector2 Min;
        public readonly float Cell;

        public Arena(Vector2 min, float cell)
        {
            Min = min;
            Cell = cell;
        }

        public Vector2 CellMin(int column, int row) => Min + new Vector2(column, row) * Cell;

        public Vector2 Center(Vector2 position) => Min + position * Cell;

        public Vector2 Feet(Vector2 position) => Center(position + new Vector2(0f, FeetOffset));
    }

    private readonly struct Controls
    {
        public readonly Rect Inner;
        public readonly Vector2 PadCenter;
        public readonly float PadRadius;
        public readonly Vector2 BombCenter;
        public readonly float BombRadius;

        public Controls(Rect panel, float scale)
        {
            Inner = panel.Inset(PanelInset * scale);
            PadRadius = MathF.Max(0f, Inner.Height * 0.5f - 6f * scale);
            PadCenter = new Vector2(Inner.Min.X + PadRadius + 12f * scale, Inner.Center.Y);
            BombRadius = Inner.Height * 0.36f;
            BombCenter = new Vector2(Inner.Max.X - BombRadius - 16f * scale, Inner.Center.Y);
        }

        public Vector2 Arrow(FuseDirection direction) => PadCenter + FuseBoard.Vector(direction) * PadRadius * 0.6f;
    }
}
