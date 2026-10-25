using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Claim;

internal static class ClaimGuide
{
    private const int WorldWidth = 64;
    private const int WorldHeight = 96;
    private const int InnerMin = 1;
    private const int InnerRight = WorldWidth - 1;
    private const int InnerBottom = WorldHeight - 1;
    private const float FieldAspect = (float)WorldWidth / WorldHeight;
    private const float FieldMargin = 8f;
    private const float ControlsWidth = 118f;
    private const float ControlsStretch = 1.5f;
    private const float ControlsGap = 12f;
    private const float ToggleRadius = 24f;
    private const float HaloReach = 1.9f;
    private const float ToggleGap = 10f;
    private const float StackGap = 16f;
    private const float KeyCap = 34f;
    private const float KeyStep = 39f;
    private const float KeyDrop = 3f;
    private const string UpKey = "W";
    private const string LeftKey = "A";
    private const string DownKey = "S";
    private const string RightKey = "D";
    private const float FingerFade = 0.025f;
    private const float RevealRate = 45f;
    private const float BossHalf = 11f;
    private const int BossTraces = 8;
    private const float BossTraceSeconds = 0.04f;
    private const float BossSurge = 2.2f;
    private const float MarkSize = 26f;
    private const float RecoverStart = 0.9f;
    private const float FadeOutStart = 0.88f;
    private const float FadeOutEnd = 0.97f;
    private const float FadeInEnd = 0.05f;
    private const float WalkStart = 0.02f;
    private const float BossPeriod = 4.8f;
    private const float BossDrawStart = 0.12f;
    private const float BossLineSpeed = 26f;
    private const float BossLineTop = 12.5f;
    private const int ContactSteps = 960;
    private const float FusePeriod = 5.6f;
    private const float FuseDrawStart = 0.1f;
    private const float FuseDrawEnd = 0.28f;
    private const float FuseLit = 0.36f;
    private const float FuseCaught = 0.76f;
    private const float SparkEnd = 0.6f;
    private const float SparkFadeStart = 0.53f;
    private const float SafeStart = 0.29f;
    private const float SafeEnd = 0.44f;
    private const float DoubleRise = 9f;
    private const float DoubleScale = 1.15f;
    private const float ShadowDrop = 1.5f;
    private const uint MosaicSeed = 0x434C41u;
    private static readonly Rect WorldRect = new(Vector2.Zero, new Vector2(WorldWidth, WorldHeight));
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.6f);
    private static readonly ClaimMosaic Mosaic = BuildMosaic();
    private static readonly CutScript FastCut = new(new Vector2(12.5f, 95.5f), new Vector2(24.5f, 95.5f),
        new Vector2(24.5f, 52.5f), new Vector2(63.5f, 52.5f), true, 5.2f, 0.12f, 0.14f, 0.62f, false,
        ClaimMove.Right, ClaimMove.Up, ClaimMove.Right, 0.07f, 0.19f,
        new Orbit(new Vector2(34f, 27f), new Vector2(9f, 6f), 0.4f));
    private static readonly CutScript SlowCut = new(new Vector2(0.5f, 58.5f), new Vector2(0.5f, 70.5f),
        new Vector2(40.5f, 70.5f), new Vector2(40.5f, 95.5f), false, 6.2f, 0.1f, 0.12f, 0.74f, true,
        ClaimMove.Down, ClaimMove.Right, ClaimMove.Down, 0.05f, 0.17f,
        new Orbit(new Vector2(36f, 30f), new Vector2(10f, 7f), 1.1f));
    private static readonly Vector2 BossLineStart = new(30.5f, 95.5f);
    private static readonly Orbit BossOrbit = new(new Vector2(47f, 70f), new Vector2(8f, 6f), MathF.PI * 0.5f);
    private static readonly BossHit Contact = FindContact();
    private static readonly Orbit FarOrbit = new(new Vector2(34f, 25f), new Vector2(9f, 5f), 0.7f);
    private static readonly Vector2 FuseLineStart = new(30.5f, 95.5f);
    private static readonly Vector2 FuseLineEnd = new(30.5f, 58.5f);
    private static readonly Vector2 SparkFrom = new(2.5f, 95.5f);
    private static readonly Vector2 SparkTo = new(62.5f, 95.5f);
    private static readonly Vector2 SafeOffset = new(8f, -5f);

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Claim.Guide1Title, L.Claim.Guide1Body, DrawCutPage),
        new GuidePage(L.Claim.Guide2Title, L.Claim.Guide2Body, DrawSlowPage),
        new GuidePage(L.Claim.Guide3Title, L.Claim.Guide3Body, DrawBossPage),
        new GuidePage(L.Claim.Guide4Title, L.Claim.Guide4Body, DrawFusePage));

    private static void DrawCutPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale) =>
        DrawCut(drawList, art, seconds, accent, scale, in FastCut);

    private static void DrawSlowPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale) =>
        DrawCut(drawList, art, seconds, accent, scale, in SlowCut);

    private static void DrawCut(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale,
        in CutScript script)
    {
        var stage = new Stage(art, scale);
        var phase = GuideKit.Loop(seconds, script.Period);
        var legOne = Vector2.Distance(script.Start, script.Corner);
        var total = legOne + Vector2.Distance(script.Corner, script.End);
        var travel = GuideKit.Window(phase, script.DrawStart, script.ClaimAt) * total;
        var drawing = phase >= script.DrawStart && phase < script.ClaimAt;
        var claimed = phase >= script.ClaimAt;
        var front = claimed ? (phase - script.ClaimAt) * script.Period * RevealRate : -1f;
        var notch = new Notch((int)script.Corner.X, (int)script.Corner.Y, script.Right);
        DrawField(drawList, in stage, accent, seconds, scale, in notch, front, GuideKit.Window(phase, RecoverStart, 1f));
        if (drawing)
        {
            DrawLine(drawList, in stage, script.Start, script.Corner, script.End, travel, legOne,
                ClaimRenderer.LineColor(accent, script.Slow, false));
        }

        DrawBoss(drawList, in stage, accent, seconds, script.Period, script.Boss);
        var marker = MarkerPosition(in script, phase, travel, legOne);
        DrawMarker(drawList, in stage, marker, accent, drawing, script.Slow, seconds, scale, MarkerAlpha(phase));
        if (script.Slow && claimed)
        {
            DrawDouble(drawList, in stage, in notch, phase - script.ClaimAt, scale);
        }

        var pressAt = script.TapStart + (script.TapEnd - script.TapStart) * 0.2f;
        var toggled = phase >= pressAt && phase < RecoverStart;
        DrawControls(drawList, stage.Controls, accent, scale, script.Slow, toggled, HeldKey(in script, phase, travel,
            legOne), phase, script.TapStart, script.TapEnd);
    }

    private static void DrawBossPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var stage = new Stage(art, scale);
        var phase = GuideKit.Loop(seconds, BossPeriod);
        var caught = phase >= Contact.Phase;
        var notch = default(Notch);
        DrawField(drawList, in stage, accent, seconds, scale, in notch, -1f, 0f);
        var marker = BossMarker(MathF.Min(phase, Contact.Phase));
        var first = drawList.VtxBuffer.Size;
        if (phase >= BossDrawStart)
        {
            DrawStraight(drawList, in stage, BossLineStart, marker, ClaimRenderer.LineColor(accent, false, caught));
        }

        LayerCompositor.Fade(drawList, first, LineAlpha(phase));
        DrawBoss(drawList, in stage, accent, seconds, BossPeriod, in BossOrbit);
        if (!caught)
        {
            DrawMarker(drawList, in stage, marker, accent, phase >= BossDrawStart, false, seconds, scale,
                MarkerAlpha(phase));
        }
        else
        {
            DrawCross(drawList, stage.At(Contact.Point), phase, Contact.Phase, scale);
        }

        var held = phase >= BossDrawStart && !caught ? ClaimMove.Up : ClaimMove.None;
        DrawControls(drawList, stage.Controls, accent, scale, false, phase >= 0.06f && phase < RecoverStart, held,
            phase, 0.03f, 0.15f);
    }

    private static void DrawFusePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var stage = new Stage(art, scale);
        var phase = GuideKit.Loop(seconds, FusePeriod);
        var caught = phase >= FuseCaught;
        var notch = default(Notch);
        DrawField(drawList, in stage, accent, seconds, scale, in notch, -1f, 0f);
        var marker = Vector2.Lerp(FuseLineStart, FuseLineEnd, Easing.SmoothStep(
            GuideKit.Window(phase, FuseDrawStart, FuseDrawEnd)));
        var first = drawList.VtxBuffer.Size;
        if (phase >= FuseDrawStart)
        {
            DrawStraight(drawList, in stage, FuseLineStart, marker, ClaimRenderer.LineColor(accent, false, caught));
        }

        if (phase >= FuseLit && !caught)
        {
            var fuse = Vector2.Lerp(FuseLineStart, marker, GuideKit.Window(phase, FuseLit, FuseCaught));
            Span<Vector2> burnt = stackalloc Vector2[2];
            burnt[0] = stage.At(FuseLineStart);
            burnt[1] = stage.At(fuse);
            ClaimRenderer.StrokeAsh(drawList, burnt, in stage.Camera);
            ClaimRenderer.DrawFuse(drawList, burnt[1], in stage.Camera, ClaimRenderer.FuseFlicker(seconds));
        }

        LayerCompositor.Fade(drawList, first, LineAlpha(phase));
        DrawBoss(drawList, in stage, accent, seconds, FusePeriod, FarOrbit);
        DrawSpark(drawList, in stage, phase, seconds, scale);
        if (!caught)
        {
            DrawMarker(drawList, in stage, marker, accent, phase >= FuseDrawStart, false, seconds, scale,
                MarkerAlpha(phase));
        }
        else
        {
            DrawCross(drawList, stage.At(marker), phase, FuseCaught, scale);
        }

        if (phase >= SafeStart && phase < SafeEnd)
        {
            var pop = GameJuice.PopIn(GuideKit.Window(phase, SafeStart, SafeStart + 0.05f));
            var fade = 1f - GuideKit.Window(phase, SafeEnd - 0.03f, SafeEnd);
            var start = drawList.VtxBuffer.Size;
            GuideKit.Check(drawList, stage.At(marker + SafeOffset), MarkSize * scale * pop, scale);
            LayerCompositor.Fade(drawList, start, fade);
        }

        var held = phase >= FuseDrawStart && phase < FuseDrawEnd ? ClaimMove.Up : ClaimMove.None;
        DrawControls(drawList, stage.Controls, accent, scale, false, phase >= 0.055f && phase < RecoverStart, held,
            phase, 0.03f, 0.15f);
    }

    private static void DrawField(ImDrawListPtr drawList, in Stage stage, Vector4 accent, float seconds, float scale,
        in Notch notch, float front, float recover)
    {
        drawList.PushClipRect(stage.Field.Min, stage.Field.Max, true);
        ClaimRenderer.DrawPicture(drawList, Mosaic, in stage.Camera, accent, seconds, scale);
        if (front < 0f)
        {
            Cover(drawList, in stage.Camera, InnerMin, InnerMin, InnerRight, InnerBottom, accent, 1f);
        }
        else
        {
            CoverClaim(drawList, in stage.Camera, in notch, front, accent, recover);
        }

        drawList.PopClipRect();
        Span<Vector2> outline = stackalloc Vector2[6];
        if (front < 0f)
        {
            Outline(drawList, in stage.Camera, Ring(outline), accent, 1f, scale);
        }
        else
        {
            Outline(drawList, in stage.Camera, notch.Outline(outline), accent, 1f - recover, scale);
            Outline(drawList, in stage.Camera, Ring(outline), accent, recover, scale);
        }

        ClaimRenderer.DrawFrame(drawList, stage.Field, accent, 1f, scale);
    }

    private static void CoverClaim(ImDrawListPtr drawList, in Camera2D camera, in Notch notch, float front,
        Vector4 accent, float recover)
    {
        var row = notch.Row;
        Cover(drawList, in camera, InnerMin, InnerMin, InnerRight, row, accent, 1f);
        var lead = MathF.Max(0f, front - ClaimRenderer.RevealBand);
        var below = row + 1f;
        var wave = ClaimRenderer.WaveInk(accent);
        if (notch.Right)
        {
            var edge = notch.Column + 1f;
            Cover(drawList, in camera, InnerMin, row, notch.Column, InnerBottom, accent, 1f);
            Cover(drawList, in camera, edge + front, below + front, InnerRight, InnerBottom, accent, 1f);
            Fill(drawList, in camera, edge + lead, below + lead, MathF.Min(edge + front, InnerRight), InnerBottom,
                wave);
            Fill(drawList, in camera, edge + front, below + lead, InnerRight, MathF.Min(below + front, InnerBottom),
                wave);
            Cover(drawList, in camera, notch.Column, row, InnerRight, InnerBottom, accent, recover);
            return;
        }

        Cover(drawList, in camera, notch.Column + 1f, row, InnerRight, InnerBottom, accent, 1f);
        Cover(drawList, in camera, InnerMin, below + front, notch.Column - front, InnerBottom, accent, 1f);
        Fill(drawList, in camera, MathF.Max(notch.Column - front, InnerMin), below + lead, notch.Column - lead,
            InnerBottom, wave);
        Fill(drawList, in camera, InnerMin, below + lead, notch.Column - front, MathF.Min(below + front, InnerBottom),
            wave);
        Cover(drawList, in camera, InnerMin, row, notch.Column + 1f, InnerBottom, accent, recover);
    }

    private static void Cover(ImDrawListPtr drawList, in Camera2D camera, float left, float top, float right,
        float bottom, Vector4 accent, float alpha)
    {
        if (right <= left || bottom <= top || alpha <= 0f)
        {
            return;
        }

        ClaimRenderer.CoverInks(top / WorldHeight, bottom / WorldHeight, alpha, out var topInk, out var bottomInk);
        drawList.AddRectFilledMultiColor(camera.ToScreen(new Vector2(left, top)),
            camera.ToScreen(new Vector2(right, bottom)), topInk, topInk, bottomInk, bottomInk);
        ClaimRenderer.DrawGrid(drawList, in camera, (int)MathF.Ceiling(left), (int)right, (int)MathF.Ceiling(top),
            (int)bottom, ClaimRenderer.GridInk(accent, alpha));
    }

    private static void Fill(ImDrawListPtr drawList, in Camera2D camera, float left, float top, float right,
        float bottom, uint ink)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        drawList.AddRectFilled(camera.ToScreen(new Vector2(left, top)), camera.ToScreen(new Vector2(right, bottom)),
            ink);
    }

    private static Span<Vector2> Ring(Span<Vector2> points)
    {
        points[0] = new Vector2(InnerMin, InnerMin);
        points[1] = new Vector2(InnerRight, InnerMin);
        points[2] = new Vector2(InnerRight, InnerBottom);
        points[3] = new Vector2(InnerMin, InnerBottom);
        return points[..4];
    }

    private static void Outline(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points,
        Vector4 accent, float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        ClaimRenderer.EdgeStyle(in camera, accent, alpha, scale, out var glow, out var core, out var glowWidth,
            out var coreWidth);
        for (var index = 0; index < points.Length; index++)
        {
            var next = points[(index + 1) % points.Length];
            ClaimRenderer.Edge(drawList, camera.ToScreen(points[index]), camera.ToScreen(next), glow, core, glowWidth,
                coreWidth);
        }
    }

    private static void DrawLine(ImDrawListPtr drawList, in Stage stage, Vector2 start, Vector2 corner, Vector2 end,
        float travel, float legOne, Vector4 color)
    {
        Span<Vector2> points = stackalloc Vector2[3];
        points[0] = stage.At(start);
        var count = 1;
        if (travel > legOne)
        {
            points[count++] = stage.At(corner);
        }

        points[count++] = stage.At(Along(start, corner, end, travel, legOne));
        ClaimRenderer.StrokeLine(drawList, points[..count], color, in stage.Camera);
    }

    private static void DrawStraight(ImDrawListPtr drawList, in Stage stage, Vector2 start, Vector2 end,
        Vector4 color)
    {
        Span<Vector2> points = stackalloc Vector2[2];
        points[0] = stage.At(start);
        points[1] = stage.At(end);
        ClaimRenderer.StrokeLine(drawList, points, color, in stage.Camera);
    }

    private static Vector2 Along(Vector2 start, Vector2 corner, Vector2 end, float travel, float legOne)
    {
        if (travel <= legOne)
        {
            return legOne <= 0f ? start : Vector2.Lerp(start, corner, travel / legOne);
        }

        var legTwo = Vector2.Distance(corner, end);
        return legTwo <= 0f ? corner : Vector2.Lerp(corner, end, MathF.Min(1f, (travel - legOne) / legTwo));
    }

    private static Vector2 MarkerPosition(in CutScript script, float phase, float travel, float legOne)
    {
        if (phase < script.WalkEnd)
        {
            return Vector2.Lerp(script.WalkFrom, script.Start, GuideKit.Window(phase, WalkStart, script.WalkEnd));
        }

        if (phase < script.DrawStart)
        {
            return script.Start;
        }

        return phase < script.ClaimAt ? Along(script.Start, script.Corner, script.End, travel, legOne) : script.End;
    }

    private static ClaimMove HeldKey(in CutScript script, float phase, float travel, float legOne)
    {
        if (phase >= WalkStart && phase < script.WalkEnd)
        {
            return script.WalkKey;
        }

        if (phase < script.DrawStart || phase >= script.ClaimAt)
        {
            return ClaimMove.None;
        }

        return travel <= legOne ? script.FirstKey : script.SecondKey;
    }

    private static void DrawMarker(ImDrawListPtr drawList, in Stage stage, Vector2 world, Vector4 accent,
        bool drawing, bool slow, float seconds, float scale, float alpha)
    {
        var first = drawList.VtxBuffer.Size;
        ClaimRenderer.DrawMarker(drawList, stage.At(world), in stage.Camera, accent, drawing, slow, seconds, scale);
        LayerCompositor.Fade(drawList, first, alpha);
    }

    private static float MarkerAlpha(float phase) =>
        MathF.Min(GuideKit.Window(phase, 0f, FadeInEnd), 1f - GuideKit.Window(phase, FadeOutStart, FadeOutEnd));

    private static float LineAlpha(float phase) => 1f - GuideKit.Window(phase, FadeOutStart, FadeOutEnd);

    private static void DrawBoss(ImDrawListPtr drawList, in Stage stage, Vector4 accent, float seconds, float period,
        in Orbit orbit)
    {
        var colorPhase = seconds * BossSurge;
        var time = GuideKit.Loop(seconds, period) * period;
        for (var age = BossTraces; age >= 1; age--)
        {
            orbit.Pose(time - age * BossTraceSeconds, period, out var head, out var tail);
            ClaimRenderer.DrawBossTrace(drawList, stage.At(head), stage.At(tail), in stage.Camera, accent, colorPhase,
                age, BossTraces + 1, 1f);
        }

        orbit.Pose(time, period, out var liveHead, out var liveTail);
        ClaimRenderer.DrawBossBody(drawList, stage.At(liveHead), stage.At(liveTail), in stage.Camera, accent,
            colorPhase, 1f);
    }

    private static Vector2 BossMarker(float phase)
    {
        var climb = GuideKit.Window(phase, BossDrawStart, 1f) * BossPeriod * BossLineSpeed;
        return new Vector2(BossLineStart.X, MathF.Max(BossLineTop, BossLineStart.Y - climb));
    }

    private static BossHit FindContact()
    {
        for (var step = 0; step <= ContactSteps; step++)
        {
            var phase = BossDrawStart + (1f - BossDrawStart) * step / ContactSteps;
            BossOrbit.Pose(phase * BossPeriod, BossPeriod, out var head, out var tail);
            var marker = BossMarker(phase);
            if (Geometry2D.SegmentSegment(head, tail, BossLineStart, marker, out var along))
            {
                return new BossHit(phase, Vector2.Lerp(head, tail, along));
            }

            if (Geometry2D.SegmentCircle(head, tail, marker, ClaimBoard.BossTouchRadius))
            {
                return new BossHit(phase, marker);
            }
        }

        return new BossHit(1f, BossLineStart);
    }

    private static void DrawSpark(ImDrawListPtr drawList, in Stage stage, float phase, float seconds, float scale)
    {
        if (phase >= SparkEnd)
        {
            return;
        }

        var first = drawList.VtxBuffer.Size;
        var position = Vector2.Lerp(SparkFrom, SparkTo, phase / SparkEnd);
        ClaimRenderer.DrawSpark(drawList, stage.At(position), in stage.Camera, seconds, 0, scale);
        LayerCompositor.Fade(drawList, first,
            MathF.Min(GuideKit.Window(phase, 0f, FadeInEnd), 1f - GuideKit.Window(phase, SparkFadeStart, SparkEnd)));
    }

    private static void DrawCross(ImDrawListPtr drawList, Vector2 center, float phase, float at, float scale)
    {
        var first = drawList.VtxBuffer.Size;
        var pop = GameJuice.PopIn(GuideKit.Window(phase, at, at + 0.06f));
        GuideKit.Cross(drawList, center, MarkSize * scale * pop, scale);
        LayerCompositor.Fade(drawList, first, LineAlpha(phase));
    }

    private static void DrawDouble(ImDrawListPtr drawList, in Stage stage, in Notch notch, float since, float scale)
    {
        var alpha = MathF.Min(GuideKit.Window(since, 0f, 0.03f), 1f - GuideKit.Window(since, 0.13f, 0.18f));
        if (alpha <= 0f)
        {
            return;
        }

        var left = notch.Right ? notch.Column : InnerMin;
        var right = notch.Right ? InnerRight : notch.Column + 1f;
        var rise = Easing.EaseOutCubic(GuideKit.Window(since, 0f, 0.18f)) * DoubleRise;
        var center = stage.At(new Vector2((left + right) * 0.5f, (notch.Row + InnerBottom) * 0.5f - rise));
        var text = Loc.T(L.Claim.Double);
        Typography.DrawCenteredLine(drawList, center + new Vector2(0f, ShadowDrop * scale), text,
            Shadow with { W = Shadow.W * alpha }, DoubleScale, FontWeight.Bold);
        Typography.DrawCenteredLine(drawList, center, text, ClaimRenderer.SlowColor with { W = alpha }, DoubleScale,
            FontWeight.Bold);
    }

    private static void DrawControls(ImDrawListPtr drawList, Rect controls, Vector4 accent, float scale,
        bool slowMode, bool toggled, ClaimMove held, float phase, float tapStart, float tapEnd)
    {
        var gap = ToggleGap * scale;
        var step = KeyStep * scale;
        var cap = KeyCap * scale;
        var stack = StackGap * scale;
        var keysHeight = cap + step + KeyDrop * scale;
        var radius = MathF.Min(ToggleRadius * scale, MathF.Min(controls.Width / (HaloReach * 2f),
            (controls.Height - keysHeight - stack - gap) * 0.25f));
        var height = radius * 4f + gap + stack + keysHeight;
        var top = controls.Center.Y - height * 0.5f;
        var centerX = controls.Center.X;
        var fastCenter = new Vector2(centerX, top + radius);
        var slowCenter = new Vector2(centerX, top + radius * 3f + gap);
        ClaimPad.DrawToggle(drawList, fastCenter, radius, Loc.T(L.Claim.Fast), accent, !slowMode && toggled, false,
            scale);
        ClaimPad.DrawToggle(drawList, slowCenter, radius, Loc.T(L.Claim.Slow), ClaimRenderer.SlowColor,
            slowMode && toggled, false, scale);
        var keysY = top + radius * 4f + gap + stack + cap * 0.5f;
        GuideKit.Key(drawList, new Vector2(centerX, keysY), UpKey, held == ClaimMove.Up, accent, scale);
        GuideKit.Key(drawList, new Vector2(centerX - step, keysY + step), LeftKey, held == ClaimMove.Left, accent,
            scale);
        GuideKit.Key(drawList, new Vector2(centerX, keysY + step), DownKey, held == ClaimMove.Down, accent, scale);
        GuideKit.Key(drawList, new Vector2(centerX + step, keysY + step), RightKey, held == ClaimMove.Right, accent,
            scale);
        DrawFinger(drawList, slowMode ? slowCenter : fastCenter, phase, tapStart, tapEnd, scale);
    }

    private static void DrawFinger(ImDrawListPtr drawList, Vector2 center, float phase, float start, float end,
        float scale)
    {
        if (phase < start || phase > end)
        {
            return;
        }

        var first = drawList.VtxBuffer.Size;
        GuideKit.Tap(drawList, center, GuideKit.Window(phase, start, end), scale);
        LayerCompositor.Fade(drawList, first,
            MathF.Min(GuideKit.Window(phase, start, start + FingerFade), 1f - GuideKit.Window(phase, end - FingerFade, end)));
    }

    private static ClaimMosaic BuildMosaic()
    {
        var mosaic = new ClaimMosaic();
        mosaic.Build(MosaicSeed, WorldWidth, WorldHeight);
        return mosaic;
    }

    private readonly record struct BossHit(float Phase, Vector2 Point);

    private readonly record struct Orbit(Vector2 Center, Vector2 Radius, float Spin)
    {
        public void Pose(float time, float period, out Vector2 head, out Vector2 tail)
        {
            var turn = time / period * MathF.Tau;
            var middle = Center + new Vector2(MathF.Cos(turn) * Radius.X, MathF.Sin(turn * 2f) * Radius.Y);
            var angle = Spin + turn * 0.5f;
            var reach = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * BossHalf;
            head = middle + reach;
            tail = middle - reach;
        }
    }

    private readonly record struct CutScript(Vector2 WalkFrom, Vector2 Start, Vector2 Corner, Vector2 End, bool Right,
        float Period, float WalkEnd, float DrawStart, float ClaimAt, bool Slow, ClaimMove WalkKey, ClaimMove FirstKey,
        ClaimMove SecondKey, float TapStart, float TapEnd, Orbit Boss);

    private readonly record struct Notch(int Column, int Row, bool Right)
    {
        public Span<Vector2> Outline(Span<Vector2> points)
        {
            points[0] = new Vector2(InnerMin, InnerMin);
            points[1] = new Vector2(InnerRight, InnerMin);
            if (Right)
            {
                points[2] = new Vector2(InnerRight, Row);
                points[3] = new Vector2(Column, Row);
                points[4] = new Vector2(Column, InnerBottom);
                points[5] = new Vector2(InnerMin, InnerBottom);
                return points;
            }

            points[2] = new Vector2(InnerRight, InnerBottom);
            points[3] = new Vector2(Column + 1f, InnerBottom);
            points[4] = new Vector2(Column + 1f, Row);
            points[5] = new Vector2(InnerMin, Row);
            return points;
        }
    }

    private readonly struct Stage
    {
        public readonly Camera2D Camera;
        public readonly Rect Field;
        public readonly Rect Controls;

        public Stage(Rect art, float scale)
        {
            var gap = ControlsGap * scale;
            var margin = FieldMargin * scale;
            var minimum = ControlsWidth * scale;
            var fieldHeight = MathF.Max(1f, MathF.Min(art.Height - margin * 2f,
                (art.Width - margin - gap - minimum) / FieldAspect));
            var fieldWidth = fieldHeight * FieldAspect;
            var controlsWidth = MathF.Max(minimum, MathF.Min(art.Width - margin - fieldWidth - gap,
                minimum * ControlsStretch));
            var left = art.Center.X - (margin + fieldWidth + gap + controlsWidth) * 0.5f + margin;
            var top = art.Center.Y - fieldHeight * 0.5f;
            Field = new Rect(new Vector2(left, top), new Vector2(left + fieldWidth, top + fieldHeight));
            Controls = new Rect(new Vector2(Field.Max.X + gap, art.Min.Y),
                new Vector2(Field.Max.X + gap + controlsWidth, art.Max.Y));
            var camera = default(Camera2D);
            camera.Fit(Field, WorldRect, FitMode.Contain);
            Camera = camera;
        }

        public Vector2 At(Vector2 world) => Camera.ToScreen(world);
    }
}
