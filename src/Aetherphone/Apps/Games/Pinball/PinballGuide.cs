using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pinball;

internal static class PinballGuide
{
    private const ulong IdleSeed = 0x4755494445UL;
    private const float LaunchPeriod = 3.2f;
    private const float FlipPeriod = 3.4f;
    private const float JackpotPeriod = 5f;
    private const float FeverPeriod = 6f;
    private const float PullStart = 0.06f;
    private const float PullEnd = 0.42f;
    private const float SwipeSpan = 0.6f;
    private const float LaunchRelease = 0.52f;
    private const float LaunchLanded = 0.6f;
    private const float KickStrength = 1.4f;
    private const float KickShare = 0.09f;
    private const float ServeAt = 0.84f;
    private const float ServeGlow = 0.12f;
    private const float FlipRise = 0.03f;
    private const float FlipFall = 0.06f;
    private const float LeftFlipAt = 0.3f;
    private const float LeftFlipHold = 0.46f;
    private const float RightFlipAt = 0.75f;
    private const float RightFlipHold = 0.91f;
    private const float ShotShare = 0.12f;
    private const float TapLead = 0.03f;
    private const float TapLength = 0.3f;
    private const float ThumbBefore = 0.12f;
    private const float ThumbAfter = 0.16f;
    private const float JackpotApproachAt = 0.56f;
    private const float JackpotRampAt = 0.62f;
    private const float JackpotRampEnd = 0.86f;
    private const float BankResetShare = PinballBoard.BankResetSeconds / JackpotPeriod;
    private const float FlashShare = 0.12f;
    private const float BurstShare = 0.1f;
    private const float RampAt = 0.07f;
    private const float RampEnd = 0.24f;
    private const float SaucerAt = 0.58f;
    private const float SaucerHit = 0.66f;
    private const float SaucerKick = 0.8f;
    private const float SaucerGone = 0.88f;
    private const float FeverAt = 0.7f;
    private const float ChargeShare = 0.04f;
    private const float FeverDrain = 0.3f;
    private const float SpinnerTurns = 3f;
    private const float SpinShare = 0.2f;
    private const float LiftStart = 0.27f;
    private const float LiftEnd = 0.34f;
    private const float DropStart = 0.41f;
    private const float DropEnd = 0.48f;
    private const float ApronY = 11.96f;
    private const float ThumbY = 10.95f;
    private const float LeftThumbX = 0.85f;
    private const float RightThumbX = 4.55f;
    private const float FloatSeconds = 0.9f;
    private const float FloatRise = 0.45f;
    private const float TextScale = 1.1f;
    private const float BigTextScale = 1.4f;
    private const float TrailShare = 0.12f;
    private const int TrailSteps = 7;
    private const float TrailWidth = 0.16f;

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Pinball.Guide1Title, L.Pinball.Guide1Body, DrawLaunchPage),
        new GuidePage(L.Pinball.Guide2Title, L.Pinball.Guide2Body, DrawFlipPage),
        new GuidePage(L.Pinball.Guide3Title, L.Pinball.Guide3Body, DrawJackpotPage),
        new GuidePage(L.Pinball.Guide4Title, L.Pinball.Guide4Body, DrawFeverPage));

    private static readonly Vector2 TableMin = new(-0.1f, -0.06f);
    private static readonly Vector2 TableMax = new(6.1f, 12.32f);
    private static readonly Rect LaunchFocus = new(new Vector2(3.2f, 9.2f), new Vector2(6.1f, 12.32f));
    private static readonly Rect FlipFocus = new(new Vector2(0f, 8.1f), new Vector2(5.4f, 12.32f));
    private static readonly Rect JackpotFocus = new(new Vector2(0f, 2f), new Vector2(6.1f, 7.7f));
    private static readonly Rect FeverFocus = new(new Vector2(0f, 2.2f), new Vector2(6.1f, 8.3f));
    private static readonly Rect OrbitFocus = new(Vector2.Zero, new Vector2(6.1f, 6.1f));
    private static readonly Vector2 LaunchTop = new(PinballTable.LaneCenterX, 7.4f);
    private static readonly Vector2 SwipeFrom = new(PinballTable.LaneCenterX, 10.25f);
    private static readonly Vector2 SwipeTo = new(PinballTable.LaneCenterX, 11.55f);
    private static readonly Vector2[] LeftFeed = { new(0.74f, 7.4f), new(0.74f, 9.3f), new(1.55f, 10.2f), new(2.32f, 10.71f) };
    private static readonly Vector2[] RightFeed = { new(4.66f, 7.4f), new(4.66f, 9.3f), new(3.85f, 10.2f), new(3.08f, 10.71f) };
    private static readonly Vector2[] LeftShot = { new(2.32f, 10.71f), new(3.7f, 7.4f) };
    private static readonly Vector2[] RightShot = { new(3.08f, 10.71f), new(1.7f, 7.4f) };

    private static readonly Vector2[] BankRun =
    {
        new(2.7f, 8f), new(0.9f, 5.6f), new(0.9f, 4.3f), new(4.08f, 6.3f), new(4.08f, 4.85f), new(3.3f, 8f),
    };

    private static readonly float[] BankRunTimes = { 0.02f, 0.12f, 0.2f, 0.32f, 0.4f, 0.48f };

    private static readonly Vector2[] OrbitRun =
    {
        new(2.2f, 8.6f), new(0.3f, 6.2f), new(0.3f, 1.8f), new(1.3f, 0.5f), new(3f, 0.2f), new(4.7f, 0.5f),
        new(5.12f, 1.8f), new(5.12f, 6.1f), new(4.7f, 6.95f), new(4f, 8.6f),
    };

    private static readonly float[] OrbitRunTimes = { 0.27f, 0.31f, 0.36f, 0.37f, 0.38f, 0.39f, 0.4f, 0.45f, 0.47f, 0.5f };
    private static readonly Vector2 RampApproach = new(3.7f, 8f);
    private static readonly Vector2 SaucerApproach = new(2.1f, 8.6f);
    private static readonly Vector2 SaucerExit = new(3.2f, 8.6f);
    private static readonly Vector2 BannerPoint = new(2.7f, 6.4f);
    private static readonly Vector2 FeverPoint = new(2.7f, 5.1f);
    private static readonly Vector2 ComboTwoPoint = new(4.1f, 4.4f);
    private static readonly Vector2 ComboThreePoint = new(2.4f, 3.7f);
    private static readonly float RampMadeShare = PathShare(PinballTable.RampPath, PinballTable.RampMade);
    private static readonly float SpinnerPass = RunTime(OrbitRun, OrbitRunTimes, PinballTable.Spinner);
    private static readonly float OrbitMade = RunTime(OrbitRun, OrbitRunTimes, PinballTable.OrbitSensors[1]);
    private static readonly float[] DropTimes = BuildDropTimes();

    private static PinballBoard? idleBoard;
    private static LabelSlot comboTwoLabel;
    private static LabelSlot comboThreeLabel;
    private static LabelSlot timesTwoLabel;

    private static void DrawLaunchPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, LaunchPeriod);
        var camera = Frame(art, LaunchFocus);
        var pose = TablePose.Rest();
        var pulling = phase < LaunchRelease;
        var served = phase >= ServeAt;
        pose.Pull = pulling ? Easing.SmoothStep(GuideKit.Window(phase, PullStart, PullEnd)) : 0f;
        pose.Kick = pulling ? 0f : KickStrength * (1f - GuideKit.Window(phase, LaunchRelease, LaunchRelease + KickShare));
        pose.Waiting = pulling || served;
        DrawTable(drawList, in camera, in pose, accent, seconds);
        var rest = PinballTable.LanePlunger;
        if (pulling || served)
        {
            if (served)
            {
                var glow = 1f - GuideKit.Window(phase, ServeAt, ServeAt + ServeGlow);
                ProgressRing.Glow(camera.ToScreen(rest), camera.Px(0.5f), accent, glow);
            }

            PinballRenderer.DrawBall(drawList, in camera, rest + new Vector2(0f, pose.Pull * PinballRenderer.PlungerTravel),
                accent, false);
        }
        else if (phase < LaunchLanded)
        {
            var flight = Easing.EaseOutCubic(GuideKit.Window(phase, LaunchRelease, LaunchLanded));
            var start = rest + new Vector2(0f, PinballRenderer.PlungerTravel);
            DrawTrail(drawList, in camera, start, LaunchTop, flight, accent);
            PinballRenderer.DrawBall(drawList, in camera, Vector2.Lerp(start, LaunchTop, flight), accent, false);
        }

        var visible = camera.VisibleWorld;
        var keyX = Math.Clamp((visible.Min.X + PinballTable.LaneInnerX) * 0.5f, 1f, 4.4f);
        GuideKit.SpaceKey(drawList, camera.ToScreen(new Vector2(keyX, ApronY)), pulling && pose.Pull > 0f, accent,
            scale);
        if (pulling)
        {
            GuideKit.Swipe(drawList, camera.ToScreen(SwipeFrom), camera.ToScreen(SwipeTo),
                GuideKit.Window(phase, 0f, SwipeSpan), scale);
        }
    }

    private static void DrawFlipPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, FlipPeriod);
        var camera = Frame(art, FlipFocus);
        var pose = TablePose.Rest();
        var leftSwing = Swing(phase, LeftFlipAt, LeftFlipHold);
        var rightSwing = Swing(phase, RightFlipAt, RightFlipHold);
        pose.Flip(PinballTable.LeftFlipper, leftSwing);
        pose.Flip(PinballTable.RightFlipper, rightSwing);
        DrawTable(drawList, in camera, in pose, accent, seconds);
        if (phase < LeftFlipAt)
        {
            var fall = Easing.EaseInCubic(GuideKit.Window(phase, 0.02f, LeftFlipAt));
            PinballRenderer.DrawBall(drawList, in camera, Along(LeftFeed, fall), accent, false);
        }
        else if (phase < LeftFlipAt + ShotShare)
        {
            DrawShot(drawList, in camera, LeftShot, GuideKit.Window(phase, LeftFlipAt, LeftFlipAt + ShotShare), accent);
        }
        else if (phase >= LeftFlipHold && phase < RightFlipAt)
        {
            var fall = Easing.EaseInCubic(GuideKit.Window(phase, LeftFlipHold, RightFlipAt));
            PinballRenderer.DrawBall(drawList, in camera, Along(RightFeed, fall), accent, false);
        }
        else if (phase >= RightFlipAt && phase < RightFlipAt + ShotShare)
        {
            DrawShot(drawList, in camera, RightShot, GuideKit.Window(phase, RightFlipAt, RightFlipAt + ShotShare),
                accent);
        }

        DrawThumb(drawList, in camera, new Vector2(LeftThumbX, ThumbY), phase, LeftFlipAt, scale);
        DrawThumb(drawList, in camera, new Vector2(RightThumbX, ThumbY), phase, RightFlipAt, scale);
        GuideKit.Key(drawList, camera.ToScreen(new Vector2(LeftThumbX, ApronY)), "A", leftSwing > 0f, accent, scale);
        GuideKit.Key(drawList, camera.ToScreen(new Vector2(RightThumbX, ApronY)), "D", rightSwing > 0f, accent, scale);
    }

    private static void DrawJackpotPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, JackpotPeriod);
        var camera = Frame(art, JackpotFocus);
        var lit = BankRunTimes[4];
        var collected = JackpotRampAt + (JackpotRampEnd - JackpotRampAt) * RampMadeShare;
        var pose = TablePose.Rest();
        pose.TargetsDown = phase < lit + BankResetShare ? DroppedTargets(phase) : 0;
        pose.JackpotLit = phase >= lit && phase < collected;
        pose.RampFlash = phase >= collected ? 1f - GuideKit.Window(phase, collected, collected + FlashShare) : 0f;
        DrawTable(drawList, in camera, in pose, accent, seconds);
        if (pose.JackpotLit)
        {
            PinballRenderer.DrawArrowLamp(drawList, in camera, PinballRenderer.JackpotLamp, -Vector2.UnitY, 0.24f,
                PinballRenderer.Ember, LampState.Blink, seconds, 0f, 0f);
        }

        if (phase >= BankRunTimes[0] && phase < BankRunTimes[^1])
        {
            PinballRenderer.DrawBall(drawList, in camera, Run(BankRun, BankRunTimes, phase), accent, false);
        }

        if (phase >= JackpotApproachAt && phase < JackpotRampAt)
        {
            var approach = Easing.EaseOutCubic(GuideKit.Window(phase, JackpotApproachAt, JackpotRampAt));
            PinballRenderer.DrawBall(drawList, in camera, Vector2.Lerp(RampApproach, PinballTable.RampPath[0], approach),
                accent, false);
        }

        PinballRenderer.DrawRamp(drawList, in camera, pose.RampFlash, pose.JackpotLit, seconds);
        if (phase >= JackpotRampAt && phase < JackpotRampEnd)
        {
            var ride = GuideKit.Window(phase, JackpotRampAt, JackpotRampEnd);
            PinballRenderer.DrawBall(drawList, in camera, Along(PinballTable.RampPath, ride), accent, true);
        }

        if (phase >= lit && phase < JackpotRampAt)
        {
            Float(drawList, in camera, BannerPoint, Loc.T(L.Pinball.JackpotLit), PinballRenderer.Ember,
                (phase - lit) * JackpotPeriod, TextScale);
        }

        if (phase < collected)
        {
            return;
        }

        var burst = GuideKit.Window(phase, collected, collected + BurstShare);
        ProgressRing.Glow(camera.ToScreen(PinballTable.RampMade), camera.Px(0.4f + 1.2f * burst), PinballRenderer.Ember,
            1f - burst);
        Float(drawList, in camera, PinballTable.RampMade - new Vector2(1.2f, 0f), Loc.T(L.Pinball.Jackpot),
            PinballRenderer.Ember, (phase - collected) * JackpotPeriod, BigTextScale);
    }

    private static void DrawFeverPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, FeverPeriod);
        var lift = Easing.SmoothStep(GuideKit.Window(phase, LiftStart, LiftEnd)) -
                   Easing.SmoothStep(GuideKit.Window(phase, DropStart, DropEnd));
        var camera = Frame(art, Rect.Lerp(FeverFocus, OrbitFocus, lift));
        var rampMade = RampAt + (RampEnd - RampAt) * RampMadeShare;
        var pose = TablePose.Rest();
        pose.FeverActive = phase >= FeverAt;
        pose.Fever = pose.FeverActive
            ? 1f - FeverDrain * GuideKit.Window(phase, FeverAt, 1f)
            : 0.33f * Easing.EaseOutCubic(GuideKit.Window(phase, rampMade, rampMade + ChargeShare)) +
              0.33f * Easing.EaseOutCubic(GuideKit.Window(phase, OrbitMade, OrbitMade + ChargeShare)) +
              0.34f * Easing.EaseOutCubic(GuideKit.Window(phase, SaucerHit, FeverAt));
        pose.PlayfieldText = phase >= SaucerHit ? timesTwoLabel.Get(L.Pinball.Times, 2) : string.Empty;
        pose.SpinnerAngle = MathF.Tau * SpinnerTurns *
                            Easing.EaseOutCubic(GuideKit.Window(phase, SpinnerPass, SpinnerPass + SpinShare));
        pose.RampFlash = phase >= rampMade ? 1f - GuideKit.Window(phase, rampMade, rampMade + FlashShare) : 0f;
        var tint = pose.FeverActive ? PinballRenderer.FeverInk(seconds, 0f) : accent;
        DrawTable(drawList, in camera, in pose, accent, seconds);
        if (phase < RampAt)
        {
            var approach = Easing.EaseOutCubic(GuideKit.Window(phase, 0.02f, RampAt));
            PinballRenderer.DrawBall(drawList, in camera, Vector2.Lerp(RampApproach, PinballTable.RampPath[0], approach),
                tint, false);
        }
        else if (phase >= OrbitRunTimes[0] && phase < OrbitRunTimes[^1])
        {
            PinballRenderer.DrawBall(drawList, in camera, Run(OrbitRun, OrbitRunTimes, phase), tint, false);
        }
        else if (phase >= SaucerAt && phase < SaucerGone)
        {
            var ball = phase < SaucerHit
                ? Vector2.Lerp(SaucerApproach, PinballTable.Saucer,
                    Easing.EaseOutCubic(GuideKit.Window(phase, SaucerAt, SaucerHit)))
                : Vector2.Lerp(PinballTable.Saucer, SaucerExit,
                    Easing.EaseInCubic(GuideKit.Window(phase, SaucerKick, SaucerGone)));
            if (phase >= SaucerHit && phase < SaucerKick)
            {
                ProgressRing.Glow(camera.ToScreen(PinballTable.Saucer), camera.Px(0.5f), PinballRenderer.Violet, 0.9f);
            }

            PinballRenderer.DrawBall(drawList, in camera, ball, tint, false);
        }

        PinballRenderer.DrawRamp(drawList, in camera, pose.RampFlash, false, seconds);
        if (phase >= RampAt && phase < RampEnd)
        {
            PinballRenderer.DrawBall(drawList, in camera,
                Along(PinballTable.RampPath, GuideKit.Window(phase, RampAt, RampEnd)), tint, true);
        }

        if (phase >= OrbitMade && phase < SaucerAt)
        {
            Float(drawList, in camera, ComboTwoPoint, comboTwoLabel.Get(L.Pinball.Combo, 2), PinballRenderer.Gold,
                (phase - OrbitMade) * FeverPeriod, TextScale);
        }

        if (phase >= SaucerHit && phase < FeverAt + BurstShare)
        {
            Float(drawList, in camera, ComboThreePoint, comboThreeLabel.Get(L.Pinball.Combo, 3), PinballRenderer.Gold,
                (phase - SaucerHit) * FeverPeriod, TextScale);
        }

        if (pose.FeverActive)
        {
            Float(drawList, in camera, FeverPoint, Loc.T(L.Pinball.Fever), tint, (phase - FeverAt) * FeverPeriod,
                BigTextScale);
        }
    }

    private static void DrawTable(ImDrawListPtr drawList, in Camera2D camera, in TablePose pose, Vector4 accent,
        float time)
    {
        var board = IdleBoard();
        var tint = pose.FeverActive ? PinballRenderer.FeverInk(time, 0f) : accent;
        var gold = pose.FeverActive ? PinballRenderer.FeverInk(time, 0.15f) : GamePalette.Lighten(tint, 0.3f);
        var show = pose.FeverActive ? PinballRenderer.FeverShowLevel : 0f;
        PinballRenderer.DrawPlayfield(drawList, in camera, tint, time);
        PinballRenderer.DrawEmblem(drawList, in camera, tint, time, pose.PlayfieldText, pose.PlayfieldText.Length > 0,
            false, pose.Fever, pose.FeverActive);
        PinballRenderer.DrawLamps(drawList, in camera, board, gold, time, show);
        PinballRenderer.DrawWalls(drawList, in camera, gold);
        for (var sling = 0; sling < PinballTable.SlingCount; sling++)
        {
            PinballRenderer.DrawSling(drawList, in camera, sling, 0f);
        }

        PinballRenderer.DrawPosts(drawList, in camera, gold);
        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            PinballRenderer.DrawTarget(drawList, in camera, target, (pose.TargetsDown & (1 << target)) != 0);
        }

        PinballRenderer.DrawSaucer(drawList, in camera, board, time);
        PinballRenderer.DrawSpinner(drawList, in camera, pose.SpinnerAngle, gold);
        for (var bumper = 0; bumper < PinballTable.BumperCount; bumper++)
        {
            PinballRenderer.DrawBumper(drawList, in camera, bumper, 0f, gold);
        }

        PinballRenderer.DrawPlunger(drawList, in camera, pose.Pull, pose.Waiting, gold, pose.Kick);
        PinballRenderer.DrawApron(drawList, in camera, board, gold, time, show);
        PinballRenderer.DrawFlipper(drawList, in camera, PinballTable.LeftFlipper, pose.LeftAngle, pose.LeftLift, gold);
        PinballRenderer.DrawFlipper(drawList, in camera, PinballTable.RightFlipper, pose.RightAngle, pose.RightLift,
            gold);
    }

    private static PinballBoard IdleBoard()
    {
        if (idleBoard is not null)
        {
            return idleBoard;
        }

        idleBoard = new PinballBoard();
        idleBoard.Reset(GameRandom.FromSeed(IdleSeed), false);
        return idleBoard;
    }

    private static Camera2D Frame(Rect art, Rect focus)
    {
        var camera = default(Camera2D);
        camera.Fit(art, focus, FitMode.Contain);
        var visible = art.Size / camera.Zoom;
        camera.Place(new Vector2(Settle(focus.Center.X, visible.X, TableMin.X, TableMax.X),
            Settle(focus.Center.Y, visible.Y, TableMin.Y, TableMax.Y)));
        return camera;
    }

    private static float Settle(float center, float visible, float min, float max) =>
        visible >= max - min ? (min + max) * 0.5f : Math.Clamp(center, min + visible * 0.5f, max - visible * 0.5f);

    private static float Swing(float phase, float at, float hold) =>
        GuideKit.Window(phase, at, at + FlipRise) - GuideKit.Window(phase, hold, hold + FlipFall);

    private static void DrawThumb(ImDrawListPtr drawList, in Camera2D camera, Vector2 world, float phase, float at,
        float scale)
    {
        if (phase < at - ThumbBefore || phase > at + ThumbAfter)
        {
            return;
        }

        GuideKit.Tap(drawList, camera.ToScreen(world), GuideKit.Window(phase, at - TapLead, at - TapLead + TapLength),
            scale);
    }

    private static void DrawShot(ImDrawListPtr drawList, in Camera2D camera, Vector2[] path, float progress,
        Vector4 accent)
    {
        var travel = Easing.EaseOutCubic(progress);
        DrawTrail(drawList, in camera, path[0], path[^1], travel, accent);
        PinballRenderer.DrawBall(drawList, in camera, Along(path, travel), accent, false);
    }

    private static void DrawTrail(ImDrawListPtr drawList, in Camera2D camera, Vector2 from, Vector2 to,
        float progress, Vector4 accent)
    {
        var color = GamePalette.Lighten(accent, 0.7f);
        var width = camera.Px(TrailWidth);
        for (var step = 0; step < TrailSteps; step++)
        {
            var near = progress - TrailShare * step / TrailSteps;
            var far = progress - TrailShare * (step + 1) / TrailSteps;
            if (far < 0f)
            {
                return;
            }

            var weight = 1f - step / (float)TrailSteps;
            drawList.AddLine(camera.ToScreen(Vector2.Lerp(from, to, far)), camera.ToScreen(Vector2.Lerp(from, to, near)),
                ImGui.GetColorU32(color with { W = 0.6f * weight }), MathF.Max(1f, width * weight));
        }
    }

    private static void Float(ImDrawListPtr drawList, in Camera2D camera, Vector2 world, string text, Vector4 color,
        float age, float size)
    {
        if (age < 0f || age > FloatSeconds)
        {
            return;
        }

        var life = age / FloatSeconds;
        var alpha = life < 0.4f ? 1f : 1f - (life - 0.4f) / 0.6f;
        var pop = age < 0.12f ? 1.18f : 1f;
        var position = camera.ToScreen(world - new Vector2(0f, FloatRise * Easing.EaseOutCubic(life)));
        Typography.DrawCenteredLine(drawList, position, text, color with { W = alpha }, size * pop, FontWeight.Bold);
    }

    private static int DroppedTargets(float phase)
    {
        var mask = 0;
        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            if (phase >= DropTimes[target])
            {
                mask |= 1 << target;
            }
        }

        return mask;
    }

    private static float[] BuildDropTimes()
    {
        var times = new float[PinballTable.TargetCount];
        for (var target = 0; target < times.Length; target++)
        {
            times[target] = RunTime(BankRun, BankRunTimes, PinballTable.Targets[target]);
        }

        return times;
    }

    private static Vector2 Run(Vector2[] points, float[] times, float phase)
    {
        for (var index = 1; index < points.Length; index++)
        {
            if (phase < times[index])
            {
                return Vector2.Lerp(points[index - 1], points[index],
                    GuideKit.Window(phase, times[index - 1], times[index]));
            }
        }

        return points[^1];
    }

    private static float RunTime(Vector2[] points, float[] times, Vector2 mark)
    {
        var leg = Closest(points, mark, out var along);
        return times[leg] + (times[leg + 1] - times[leg]) * along;
    }

    private static Vector2 Along(Vector2[] points, float progress)
    {
        var remaining = Math.Clamp(progress, 0f, 1f) * Length(points);
        for (var index = 1; index < points.Length; index++)
        {
            var length = Vector2.Distance(points[index - 1], points[index]);
            if (remaining <= length || index == points.Length - 1)
            {
                return Vector2.Lerp(points[index - 1], points[index], length <= 0f ? 1f : MathF.Min(1f, remaining / length));
            }

            remaining -= length;
        }

        return points[^1];
    }

    private static float PathShare(Vector2[] points, Vector2 mark)
    {
        var leg = Closest(points, mark, out var along);
        var walked = 0f;
        for (var index = 1; index <= leg; index++)
        {
            walked += Vector2.Distance(points[index - 1], points[index]);
        }

        var total = Length(points);
        return total <= 0f ? 0f : (walked + Vector2.Distance(points[leg], points[leg + 1]) * along) / total;
    }

    private static float Length(Vector2[] points)
    {
        var total = 0f;
        for (var index = 1; index < points.Length; index++)
        {
            total += Vector2.Distance(points[index - 1], points[index]);
        }

        return total;
    }

    private static int Closest(Vector2[] points, Vector2 mark, out float along)
    {
        var closest = 0;
        var best = float.MaxValue;
        along = 0f;
        for (var index = 1; index < points.Length; index++)
        {
            var from = points[index - 1];
            var segment = points[index] - from;
            var lengthSquared = segment.LengthSquared();
            var share = lengthSquared <= 0f ? 0f : Math.Clamp(Vector2.Dot(mark - from, segment) / lengthSquared, 0f, 1f);
            var distance = Vector2.DistanceSquared(from + segment * share, mark);
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            closest = index - 1;
            along = share;
        }

        return closest;
    }

    private struct TablePose
    {
        public float LeftAngle;
        public float RightAngle;
        public float LeftLift;
        public float RightLift;
        public float Pull;
        public float Kick;
        public bool Waiting;
        public int TargetsDown;
        public float SpinnerAngle;
        public float RampFlash;
        public bool JackpotLit;
        public float Fever;
        public bool FeverActive;
        public string PlayfieldText;

        public static TablePose Rest() => new()
        {
            LeftAngle = PinballTable.RestAngle(PinballTable.LeftFlipper),
            RightAngle = PinballTable.RestAngle(PinballTable.RightFlipper),
            PlayfieldText = string.Empty,
        };

        public void Flip(int flipper, float swing)
        {
            if (flipper == PinballTable.LeftFlipper)
            {
                LeftAngle = PinballTable.RestAngle(flipper) - PinballTable.FlipperSwing * swing;
                LeftLift = swing;
                return;
            }

            RightAngle = PinballTable.RestAngle(flipper) + PinballTable.FlipperSwing * swing;
            RightLift = swing;
        }
    }
}
