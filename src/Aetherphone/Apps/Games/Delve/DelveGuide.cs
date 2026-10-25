using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Delve;

internal static class DelveGuide
{
    private const int Columns = 7;
    private const int NoCell = -1;
    private const int Up = 0;
    private const int Right = 1;
    private const int Down = 2;
    private const int Left = 3;
    private const float StepSeconds = 0.32f;
    private const float FallSeconds = 0.16f;
    private const float DigLead = 0.35f;
    private const float LoopFade = 0.05f;
    private const float ControlsShare = 0.45f;
    private const float ControlsHeight = 86f;
    private const float KeyPitch = 40f;
    private const float StickRing = 26f;
    private const float StickReach = 20f;
    private const float StickPress = 0.2f;
    private const float MarkShare = 0.72f;
    private const float PipShare = 0.17f;
    private const float PopSteps = 1.2f;
    private const float DigPeriod = 3.6f;
    private const float DigStart = 0.5f;
    private const float ExitPeriod = 4.4f;
    private const float ExitStart = 0.4f;
    private const int ExitPauseAt = 5;
    private const float ExitPauseSteps = 2f;
    private const int Quota = 3;
    private const float BoulderPeriod = 4f;
    private const float BoulderStart = 0.4f;
    private const int StrainAt = 3;
    private const float StrainSteps = 1.8f;
    private const float FoePeriod = 4f;
    private const float FoeStart = 0.3f;
    private const int FoeDropAt = 3;
    private const float SlimeStepSeconds = 0.55f;
    private const float BlastSeconds = 0.5f;
    private static readonly Vector4 StickInk = new(1f, 1f, 1f, 0.35f);

    private static readonly string[] DigCave =
    {
        "..#....",
        "-@..*..",
        ".....*.",
        "#......",
    };

    private static readonly int[] DigPath = { At(1, 1), At(2, 1), At(3, 1), At(4, 1), At(5, 1), At(5, 2), At(5, 3) };

    private static readonly string[] ExitCave =
    {
        "#......",
        "-@*.*..",
        ".....*X",
        "#......",
    };

    private static readonly int[] ExitPath = { At(1, 1), At(2, 1), At(3, 1), At(4, 1), At(5, 1), At(5, 2), At(6, 2) };

    private static readonly string[] BoulderCave =
    {
        "#..-..#",
        "@...---",
        "...-...",
        ".......",
    };

    private static readonly int[] BoulderPath = { At(0, 1), At(1, 1), At(2, 1), At(3, 1), At(4, 1) };

    private static readonly string[] FoeCave =
    {
        "#.....#",
        "...-...",
        "@......",
        "...-...",
        "#-----#",
    };

    private static readonly int[] FoePath = { At(0, 2), At(1, 2), At(2, 2), At(3, 2), At(4, 2), At(5, 2) };

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Delve.Guide1Title, L.Delve.Guide1Body, DrawDigPage),
        new GuidePage(L.Delve.Guide2Title, L.Delve.Guide2Body, DrawExitPage),
        new GuidePage(L.Delve.Guide3Title, L.Delve.Guide3Body, DrawBoulderPage),
        new GuidePage(L.Delve.Guide4Title, L.Delve.Guide4Body, DrawFoePage));

    private static void DrawDigPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, DigPeriod);
        var controls = MathF.Min(ControlsHeight * scale, art.Height * ControlsShare);
        var scene = new Rect(art.Min, new Vector2(art.Max.X, art.Max.Y - controls));
        var travel = (phase * DigPeriod - DigStart) / StepSeconds;
        var walk = Follow(DigPath, travel);
        var firstVertex = drawList.VtxBuffer.Size;
        var cave = new Cave(scene, DigCave, DigPath.AsSpan(0, walk.Dug), NoCell);
        cave.Draw(drawList, seconds, accent, false, 0f);
        DelveRenderer.DrawMiner(drawList, cave.Center(walk.Position), cave.Cell, walk.Facing, 0f, seconds, 0f);
        FadeLoop(drawList, firstVertex, phase);
        var last = DigPath.Length - 1;
        var grip = GuideKit.Window(travel, -0.5f, 0f) - GuideKit.Window(travel, last, last + 0.5f);
        var moving = travel > 0f && travel < last;
        DrawControls(drawList, new Rect(new Vector2(art.Min.X, art.Max.Y - controls), art.Max),
            moving ? walk.Facing : DelveBoard.NoDirection, Heading(DigPath, travel) * grip, grip > 0.5f, accent,
            scale);
    }

    private static void DrawExitPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, ExitPeriod);
        var time = phase * ExitPeriod;
        var pip = MathF.Min(art.Height * PipShare, art.Width * 0.12f);
        var scene = new Rect(new Vector2(art.Min.X, art.Min.Y + pip * 1.3f), art.Max);
        var raw = (time - ExitStart) / StepSeconds;
        var travel = Paused(raw, ExitPauseAt, ExitPauseSteps);
        var walk = Follow(ExitPath, travel);
        var firstVertex = drawList.VtxBuffer.Size;
        var dug = ExitPath.AsSpan(0, walk.Dug);
        var openTravel = Collected(ExitCave, ExitPath, Quota);
        var open = travel >= openTravel;
        var openTime = ExitStart + openTravel * StepSeconds;
        var flash = open ? 1f - GuideKit.Window(time, openTime, openTime + 0.9f) : 0f;
        var cave = new Cave(scene, ExitCave, dug, NoCell);
        cave.Draw(drawList, seconds, accent, open, flash);
        var last = ExitPath.Length - 1;
        if (travel < last)
        {
            var shrink = travel > last - 1 ? GuideKit.Window(travel - (last - 1), 0.25f, 1f) : 0f;
            DelveRenderer.DrawMiner(drawList, cave.Center(walk.Position), cave.Cell * (1f - 0.85f * shrink),
                walk.Facing, 0f, seconds, flash);
        }

        DrawPips(drawList, new Vector2(cave.Board.Center.X, cave.Board.Min.Y - pip * 0.7f), pip, cave.Cell, ExitCave,
            ExitPath, travel, seconds);
        var escaped = ExitStart + (last + ExitPauseSteps) * StepSeconds;
        Check(drawList, cave.Center(Spot(ExitPath[last]) - new Vector2(0f, 1f)), cave.Cell, time - escaped, scale);
        FadeLoop(drawList, firstVertex, phase);
    }

    private static void DrawBoulderPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, BoulderPeriod);
        var time = phase * BoulderPeriod;
        var raw = (time - BoulderStart) / StepSeconds;
        var travel = Paused(raw, StrainAt, StrainSteps);
        var walk = Follow(BoulderPath, travel);
        var firstVertex = drawList.VtxBuffer.Size;
        var cave = new Cave(art, BoulderCave, BoulderPath.AsSpan(0, walk.Dug), NoCell);
        cave.Draw(drawList, seconds, accent, false, 0f);
        var push = Math.Clamp(travel - StrainAt, 0f, 1f);
        var pushed = new Vector2(4f + Easing.SmoothStep(push), 1f);
        DelveRenderer.DrawBoulder(drawList, cave.Center(pushed), cave.Cell, Seed(4, 1));
        var released = BoulderStart + (StrainAt + StrainSteps) * StepSeconds;
        var drop = GuideKit.Window(time, released, released + 2f * FallSeconds);
        var falling = new Vector2(3f, 2f * drop * drop);
        DelveRenderer.DrawBoulder(drawList, cave.Center(falling), cave.Cell, Seed(3, 0));
        var landed = released + 2f * FallSeconds;
        Dust(drawList, cave.Center(new Vector2(3f, 2.42f)), cave.Cell, time - landed);
        var squash = Strain(raw) + (push > 0f ? 1f - push : 0f);
        DelveRenderer.DrawMiner(drawList, cave.Center(walk.Position), cave.Cell, walk.Facing, squash, seconds, 0f);
        Check(drawList, cave.Center(walk.Position - new Vector2(0f, 1f)), cave.Cell, time - landed - 0.15f, scale);
        FadeLoop(drawList, firstVertex, phase);
    }

    private static void DrawFoePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, FoePeriod);
        var time = phase * FoePeriod;
        var travel = (time - FoeStart) / StepSeconds;
        var walk = Follow(FoePath, travel);
        var released = FoeStart + FoeDropAt * StepSeconds;
        var crushed = released + 2f * FallSeconds;
        var blasted = time >= crushed;
        var center = new Vector2(3f, 4f);
        var firstVertex = drawList.VtxBuffer.Size;
        var cave = new Cave(art, FoeCave, FoePath.AsSpan(0, walk.Dug), blasted ? At(3, 4) : NoCell);
        cave.Draw(drawList, seconds, accent, false, 0f);
        if (!blasted)
        {
            var drop = GuideKit.Window(time, released, crushed);
            DelveRenderer.DrawBoulder(drawList, cave.Center(new Vector2(3f, 1f + 2f * drop * drop)), cave.Cell,
                Seed(3, 1));
            var crawl = Math.Clamp((time - (crushed - 2f * SlimeStepSeconds)) / SlimeStepSeconds, 0f, 2f);
            var step = Math.Min((int)crawl, 1);
            var slime = new Vector2(5f - step - Easing.SmoothStep(crawl - step), center.Y);
            DelveRenderer.DrawSlime(drawList, cave.Center(slime), cave.Cell, seconds, Seed(5, 4));
        }
        else
        {
            DrawBurst(drawList, cave, center, 1f - GuideKit.Window(time, crushed, crushed + BlastSeconds), seconds);
            Shockwave(drawList, cave.Center(center), cave.Cell, time - crushed);
        }

        DelveRenderer.DrawMiner(drawList, cave.Center(walk.Position), cave.Cell, walk.Facing, 0f, seconds, 0f);
        FadeLoop(drawList, firstVertex, phase);
    }

    private static void DrawBurst(ImDrawListPtr drawList, in Cave cave, Vector2 center, float life, float seconds)
    {
        for (var row = -1; row <= 1; row++)
        {
            for (var column = -1; column <= 1; column++)
            {
                var spot = center + new Vector2(column, row);
                if (!cave.Blastable((int)spot.X, (int)spot.Y))
                {
                    continue;
                }

                if (life > 0f)
                {
                    DelveRenderer.DrawBlast(drawList, cave.Center(spot), cave.Cell, life, true);
                    continue;
                }

                DelveRenderer.DrawGem(drawList, cave.Center(spot), cave.Cell, seconds, Seed((int)spot.X, (int)spot.Y));
            }
        }
    }

    private static void DrawControls(ImDrawListPtr drawList, Rect strip, int direction, Vector2 lean, bool pressed,
        Vector4 accent, float scale)
    {
        var pitch = KeyPitch * scale;
        var keys = new Vector2(strip.Min.X + strip.Width * 0.32f, strip.Center.Y);
        var lower = keys.Y + pitch * 0.5f;
        GuideKit.ArrowKey(drawList, new Vector2(keys.X, keys.Y - pitch * 0.5f), DelveBoard.Step(Up),
            direction == Up, accent, scale);
        GuideKit.ArrowKey(drawList, new Vector2(keys.X - pitch, lower), DelveBoard.Step(Left), direction == Left,
            accent, scale);
        GuideKit.ArrowKey(drawList, new Vector2(keys.X, lower), DelveBoard.Step(Down), direction == Down, accent,
            scale);
        GuideKit.ArrowKey(drawList, new Vector2(keys.X + pitch, lower), DelveBoard.Step(Right), direction == Right,
            accent, scale);
        var stick = new Vector2(strip.Min.X + strip.Width * 0.72f, strip.Center.Y);
        drawList.AddCircle(stick, StickRing * scale, ImGui.GetColorU32(StickInk), 32, 2f * scale);
        var finger = stick + lean * StickReach * scale;
        drawList.AddLine(stick, finger, ImGui.GetColorU32(accent with { W = 0.7f }), 3f * scale);
        GuideKit.Tap(drawList, finger, pressed ? StickPress : 0f, scale);
    }

    private static void DrawPips(ImDrawListPtr drawList, Vector2 center, float size, float cell, string[] layout,
        int[] path, float travel, float seconds)
    {
        var spacing = size * 1.2f;
        var left = center.X - spacing * (Quota - 1) * 0.5f;
        var half = new Vector2(size, size) * 0.42f;
        var slot = 0;
        for (var index = 1; index < path.Length && slot < Quota; index++)
        {
            if (Symbol(layout, path[index]) != '*')
            {
                continue;
            }

            var pip = new Vector2(left + slot * spacing, center.Y);
            Squircle.Fill(drawList, pip - half, pip + half, size * 0.22f, ImGui.GetColorU32(DelveRenderer.Tunnel));
            Squircle.Stroke(drawList, pip - half, pip + half, size * 0.22f,
                ImGui.GetColorU32(DelveRenderer.GemCore with { W = 0.35f }), MathF.Max(1f, cell * 0.03f));
            var age = travel - (index - 1 + DigLead);
            if (age >= 0f)
            {
                var pop = Easing.EaseOutBack(GuideKit.Window(age, 0f, PopSteps));
                DelveRenderer.DrawGem(drawList, pip, size * 0.9f * pop, seconds, index * 97);
            }

            slot++;
        }
    }

    private static void Check(ImDrawListPtr drawList, Vector2 center, float cell, float age, float scale)
    {
        if (age <= 0f)
        {
            return;
        }

        GuideKit.Check(drawList, center, cell * MarkShare * Easing.EaseOutBack(GuideKit.Window(age, 0f, 0.35f)),
            scale);
    }

    private static void Dust(ImDrawListPtr drawList, Vector2 ground, float cell, float age)
    {
        var puff = GuideKit.Window(age, 0f, 0.45f);
        if (puff <= 0f || puff >= 1f)
        {
            return;
        }

        var color = ImGui.GetColorU32(DelveRenderer.Rock with { W = 0.6f * (1f - puff) });
        var spread = cell * (0.3f + 0.45f * Easing.EaseOutCubic(puff));
        var radius = cell * (0.08f + 0.1f * puff);
        drawList.AddCircleFilled(ground + new Vector2(-spread, 0f), radius, color, 12);
        drawList.AddCircleFilled(ground + new Vector2(spread, 0f), radius, color, 12);
        drawList.AddCircleFilled(ground + new Vector2(-spread * 0.55f, -radius), radius * 0.8f, color, 12);
        drawList.AddCircleFilled(ground + new Vector2(spread * 0.55f, -radius), radius * 0.8f, color, 12);
    }

    private static void Shockwave(ImDrawListPtr drawList, Vector2 center, float cell, float age)
    {
        var wave = GuideKit.Window(age, 0f, 0.6f);
        if (wave <= 0f || wave >= 1f)
        {
            return;
        }

        drawList.AddCircle(center, cell * (0.5f + 2f * Easing.EaseOutCubic(wave)),
            ImGui.GetColorU32(DelveRenderer.GemLight with { W = 0.8f * (1f - wave) }), 40,
            MathF.Max(1.5f, cell * 0.08f * (1f - wave)));
    }

    private static void FadeLoop(ImDrawListPtr drawList, int firstVertex, float phase)
    {
        var alpha = MathF.Min(GuideKit.Window(phase, 0f, LoopFade), 1f - GuideKit.Window(phase, 1f - LoopFade, 1f));
        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }

    private static float Strain(float raw)
    {
        var strain = (raw - StrainAt) / StrainSteps;
        if (strain <= 0f || strain >= 1f)
        {
            return 0f;
        }

        var beat = strain * 2f % 1f;
        return 0.55f * (1f - beat);
    }

    private static float Paused(float raw, int at, float steps) =>
        raw < at ? raw : raw < at + steps ? at : raw - steps;

    private static float Collected(string[] layout, int[] path, int quota)
    {
        var found = 0;
        for (var index = 1; index < path.Length; index++)
        {
            if (Symbol(layout, path[index]) != '*')
            {
                continue;
            }

            found++;
            if (found == quota)
            {
                return index - 1 + DigLead;
            }
        }

        return float.MaxValue;
    }

    private static Walk Follow(ReadOnlySpan<int> path, float travel)
    {
        var last = path.Length - 1;
        var clamped = Math.Clamp(travel, 0f, last);
        var step = Math.Min((int)clamped, last - 1);
        var from = Spot(path[step]);
        var to = Spot(path[step + 1]);
        var position = Vector2.Lerp(from, to, Easing.SmoothStep(clamped - step));
        var dug = Math.Clamp((int)MathF.Floor(travel + 1f - DigLead) + 1, 1, path.Length);
        return new Walk(position, Direction(path[step], path[step + 1]), dug);
    }

    private static Vector2 Heading(ReadOnlySpan<int> path, float travel)
    {
        var last = path.Length - 1;
        var clamped = Math.Clamp(travel, 0f, last);
        var step = Math.Min((int)clamped, last - 1);
        var current = DelveBoard.Step(Direction(path[step], path[step + 1]));
        if (step == 0)
        {
            return current;
        }

        var previous = DelveBoard.Step(Direction(path[step - 1], path[step]));
        var heading = Vector2.Lerp(previous, current, Easing.SmoothStep(GuideKit.Window(clamped - step, 0f, 0.5f)));
        return heading / MathF.Max(0.0001f, heading.Length());
    }

    private static int Direction(int from, int to)
    {
        var delta = Spot(to) - Spot(from);
        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
        {
            return delta.X > 0f ? Right : Left;
        }

        return delta.Y > 0f ? Down : Up;
    }

    private static int At(int column, int row) => row * Columns + column;

    private static Vector2 Spot(int cell) => new(cell % Columns, cell / Columns);

    private static char Symbol(string[] layout, int cell) => layout[cell / Columns][cell % Columns];

    private static int Seed(int column, int row) => (column * 73856093) ^ (row * 19349663);

    private readonly struct Walk
    {
        public readonly Vector2 Position;
        public readonly int Facing;
        public readonly int Dug;

        public Walk(Vector2 position, int facing, int dug)
        {
            Position = position;
            Facing = facing;
            Dug = dug;
        }
    }

    private readonly ref struct Cave
    {
        public readonly Rect Board;
        public readonly float Cell;
        private readonly string[] layout;
        private readonly ReadOnlySpan<int> dug;
        private readonly int blastCenter;

        public Cave(Rect area, string[] layout, ReadOnlySpan<int> dug, int blastCenter)
        {
            this.layout = layout;
            this.dug = dug;
            this.blastCenter = blastCenter;
            Board = GuideKit.Fit(area, Columns / (float)layout.Length);
            Cell = Board.Width / Columns;
        }

        public Vector2 Center(Vector2 spot) => Board.Min + (spot + new Vector2(0.5f, 0.5f)) * Cell;

        public bool Blastable(int column, int row) =>
            InBounds(column, row) && layout[row][column] is not '#' and not 'X';

        public void Draw(ImDrawListPtr drawList, float seconds, Vector4 accent, bool exitOpen, float exitFlash)
        {
            drawList.AddRectFilled(Board.Min, Board.Max, ImGui.GetColorU32(DelveRenderer.Tunnel));
            for (var row = 0; row < layout.Length; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var min = Board.Min + new Vector2(column, row) * Cell;
                    var center = min + new Vector2(Cell, Cell) * 0.5f;
                    switch (layout[row][column])
                    {
                        case '#':
                            DelveRenderer.DrawWall(drawList, min, Cell);
                            break;
                        case 'X':
                            DelveRenderer.DrawExit(drawList, center, Cell, exitOpen, seconds, accent, exitFlash);
                            break;
                        case '.' when !Cleared(column, row):
                            DelveRenderer.DrawDirt(drawList, column, row, min, Cell, !Solid(column, row - 1),
                                !Solid(column, row + 1));
                            break;
                        case '*' when !Cleared(column, row):
                            DelveRenderer.DrawGem(drawList, center, Cell, seconds, Seed(column, row));
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private bool InBounds(int column, int row) =>
            column >= 0 && row >= 0 && column < Columns && row < layout.Length;

        private bool Solid(int column, int row)
        {
            if (!InBounds(column, row))
            {
                return true;
            }

            var symbol = layout[row][column];
            return symbol == '#' || symbol == '.' && !Cleared(column, row);
        }

        private bool Cleared(int column, int row)
        {
            var cell = At(column, row);
            if (blastCenter != NoCell && Math.Abs(column - blastCenter % Columns) <= 1 &&
                Math.Abs(row - blastCenter / Columns) <= 1)
            {
                return true;
            }

            for (var index = 0; index < dug.Length; index++)
            {
                if (dug[index] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
