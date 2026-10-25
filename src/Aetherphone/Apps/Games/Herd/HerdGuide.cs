using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Herd;

internal static class HerdGuide
{
    private const float WalkSpeed = HerdBoard.TicksPerSecond * HerdBoard.CellSize;
    private const float FallSpeed = WalkSpeed * HerdBoard.FallCellsPerTick;
    private const float DigSpeed = WalkSpeed / HerdBoard.DigTicksPerRow;
    private const float FloatOpenDrop = HerdBoard.FloatOpenCells * HerdBoard.CellSize;
    private const float ExitSeconds = HerdBoard.ExitTicks * HerdBoard.TickSeconds;
    private const float SplatSeconds = HerdBoard.SplatTicks * HerdBoard.TickSeconds;
    private const float BodyLift = HerdBoard.BodyCells * HerdBoard.CellSize * 0.5f;
    private const float BlockerReach = HerdBoard.BlockerReach * HerdBoard.CellSize;
    private const float DigHalfWidth = HerdBoard.DigHalfWidth * HerdBoard.CellSize;
    private const float FastFactor = 3f;
    private const float PulseDecay = 2.4f;
    private const float PopDecay = 4f;
    private const float RingSeconds = 0.35f;
    private const float FloatTextSeconds = 0.9f;
    private const float FloatTextRise = 46f;
    private const float TargetRadius = 0.62f;
    private const float TargetIconLift = 1.05f;
    private const float TargetIconSize = 0.42f;
    private const float GrassDepth = 0.09f;
    private const float GrassBlend = 0.06f;
    private const float DeepShare = 0.6f;
    private const float StrataShade = 0.88f;
    private const float TileGap = 6f;
    private const float TileLift = 4f;
    private const float ControlRadius = 20f;
    private const float RowInset = 4f;
    private const float KeyHold = 0.25f;
    private const float FingerOffstage = 30f;
    private const int DirtBits = 6;
    private const int FurBits = 10;
    private const float FurSeconds = 0.6f;
    private const int ControlCount = HerdRenderer.BombControl + 1;

    private const float TrekWidth = 6.4f;
    private const float TrekHeight = 4f;
    private const float TrekDoorX = 1f;
    private const float TrekDoorY = 1f;
    private const float TrekFloor = 2.8f;
    private const float TrekHutX = 5.3f;
    private const int TrekHerd = 3;
    private const float TrekSpacing = 1.5f;

    private const float SkillWidth = 6f;
    private const float SkillHeight = 4.4f;
    private const float LedgeTop = 1.55f;
    private const float LedgeBottom = 2.15f;
    private const float SkillFloor = 4f;
    private const float SkillHutX = 4.9f;
    private const float SkillEnterX = -0.6f;
    private const float SkillKeyX = 5.3f;
    private const float SkillKeyY = 0.6f;
    private const float SkillPickAt = 0.36f;
    private const float SkillDigAt = 1.7f;
    private const float SkillPeriod = 6.8f;
    private const float RefillSeconds = 0.4f;

    private const float DropWidth = 7.2f;
    private const float DropHeight = 5.6f;
    private const float LaneWidth = 3.6f;
    private const float CliffTop = 1.5f;
    private const float CliffEdge = 1.3f;
    private const float DropFloor = 5f;
    private const float DropStartX = 0.25f;
    private const float DropLeaveX = 1.42f;
    private const float DropPeriod = 4.3f;
    private const float MarkDelay = 0.2f;
    private const float MarkGrow = 0.25f;

    private const float PauseWidth = 7.2f;
    private const float PauseHeight = 3.6f;
    private const float PauseFloor = 2.7f;
    private const float WallRight = 0.5f;
    private const float WallTop = 1f;
    private const float WallClearance = 0.3f;
    private const float BlockerClearance = 0.2f;
    private const float BlockerStartX = 2.6f;
    private const float WalkerStartX = 0.9f;
    private const float PauseAt = 0.9f;
    private const float BlockAt = 1.6f;
    private const float ResumeAt = 2.75f;
    private const float FastAt = 4.1f;
    private const float PausePeriod = 6.4f;
    private const float PauseKeyY = 0.6f;

    private const string DigKeyLabel = "2";
    private const string FastKeyLabel = "F";

    private static readonly int[] TrayCounts = { 1, 2, 2, 0, 1, 1 };
    private static readonly Vector4 Earth = TerrainMaterial.Earth.Surface;

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Herd.Guide1Title, L.Herd.Guide1Body, DrawTrekPage),
        new GuidePage(L.Herd.Guide2Title, L.Herd.Guide2Body, DrawSkillPage),
        new GuidePage(L.Herd.Guide3Title, L.Herd.Guide3Body, DrawDropPage),
        new GuidePage(L.Herd.Guide4Title, L.Herd.Guide4Body, DrawPausePage));

    private static void DrawTrekPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var stage = GuideKit.Fit(art, TrekWidth / TrekHeight);
        var unit = stage.Width / TrekWidth;
        Ground(drawList, new Vector2(art.Min.X, At(stage, unit, 0f, TrekFloor).Y), art.Max, unit, true, 1f);
        var period = TrekSpacing * TrekHerd;
        var fall = (TrekFloor - TrekDoorY) / FallSpeed;
        var walk = (TrekHutX - TrekDoorX) / WalkSpeed;
        var doorPulse = 0f;
        var celebrate = 0f;
        for (var moogle = 0; moogle < TrekHerd; moogle++)
        {
            var local = Wrap(seconds - moogle * TrekSpacing, period);
            doorPulse = MathF.Max(doorPulse, 1f - local * PulseDecay);
            var saved = local - fall - walk;
            if (saved >= 0f)
            {
                celebrate = MathF.Max(celebrate, 1f - saved * PulseDecay);
            }
        }

        HerdRenderer.DrawDoor(drawList, At(stage, unit, TrekDoorX, TrekDoorY), unit, 1, 1f, doorPulse);
        var hut = At(stage, unit, TrekHutX, TrekFloor);
        HerdRenderer.DrawExit(drawList, hut, unit, seconds, celebrate, accent);
        for (var moogle = 0; moogle < TrekHerd; moogle++)
        {
            var local = Wrap(seconds - moogle * TrekSpacing, period);
            var variant = (byte)(moogle * 2);
            if (local < fall)
            {
                Moogle(drawList, At(stage, unit, TrekDoorX, TrekDoorY + FallSpeed * local), unit, 1,
                    HerdAction.Falling, seconds, 0f, variant, false, 1f);
                continue;
            }

            if (local < fall + walk)
            {
                Moogle(drawList, At(stage, unit, TrekDoorX + WalkSpeed * (local - fall), TrekFloor), unit, 1,
                    HerdAction.Walking, seconds, 0f, variant, false, 1f);
                continue;
            }

            var exiting = local - fall - walk;
            if (exiting < ExitSeconds)
            {
                Moogle(drawList, hut, unit, 1, HerdAction.Exiting, seconds, exiting, variant, false,
                    1f - exiting / ExitSeconds);
            }

            SavedBurst(drawList, hut - new Vector2(0f, 0.5f * unit), exiting, unit, scale);
        }
    }

    private static void DrawSkillPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % SkillPeriod;
        var gap = TileGap * scale;
        var tileWidth = (art.Width - gap * (HerdLevel.SkillCount - 1)) / HerdLevel.SkillCount;
        var tileHeight = MathF.Min(tileWidth * 0.85f, art.Height * 0.2f);
        var trayTop = art.Max.Y - tileHeight - RowInset * scale;
        var scene = new Rect(art.Min, new Vector2(art.Max.X, trayTop - gap * 2f));
        var stage = GuideKit.Fit(scene, SkillWidth / SkillHeight);
        var unit = stage.Width / SkillWidth;
        var digEnd = SkillDigAt + (LedgeBottom - LedgeTop) / DigSpeed;
        var fallEnd = digEnd + (SkillFloor - LedgeBottom) / FallSpeed;
        var digX = SkillEnterX + WalkSpeed * SkillDigAt;
        var walkEnd = fallEnd + (SkillHutX - digX) / WalkSpeed;
        var carved = time < SkillDigAt ? LedgeTop : MathF.Min(LedgeBottom, LedgeTop + DigSpeed * (time - SkillDigAt));
        var ledgeTop = At(stage, unit, 0f, LedgeTop).Y;
        var ledgeBottom = At(stage, unit, 0f, LedgeBottom).Y;
        var holeLeft = At(stage, unit, digX - DigHalfWidth, 0f).X;
        var holeRight = At(stage, unit, digX + DigHalfWidth, 0f).X;
        Ground(drawList, new Vector2(art.Min.X, At(stage, unit, 0f, SkillFloor).Y), new Vector2(art.Max.X, scene.Max.Y),
            unit, true, 1f);
        Ground(drawList, new Vector2(art.Min.X, ledgeTop), new Vector2(holeLeft, ledgeBottom), unit, true, 1f);
        Ground(drawList, new Vector2(holeRight, ledgeTop), new Vector2(art.Max.X, ledgeBottom), unit, true, 1f);
        if (carved < LedgeBottom)
        {
            Ground(drawList, new Vector2(holeLeft, At(stage, unit, 0f, carved).Y), new Vector2(holeRight, ledgeBottom),
                unit, time < SkillDigAt, 1f);
        }

        var refill = GuideKit.Window(time, SkillPeriod - RefillSeconds, SkillPeriod);
        if (refill > 0f)
        {
            Ground(drawList, new Vector2(holeLeft, ledgeTop), new Vector2(holeRight, ledgeBottom), unit, true, refill);
        }

        var hut = At(stage, unit, SkillHutX, SkillFloor);
        var celebrate = time >= walkEnd ? MathF.Max(0f, 1f - (time - walkEnd) * PulseDecay) : 0f;
        HerdRenderer.DrawExit(drawList, hut, unit, seconds, celebrate, accent);
        var feet = SkillFeet(stage, unit, time, digX, digEnd, fallEnd, walkEnd, out var action, out var actionSeconds);
        if (action != HerdAction.Saved)
        {
            var alpha = action == HerdAction.Exiting ? 1f - actionSeconds / ExitSeconds : 1f;
            Moogle(drawList, feet, unit, 1, action, seconds, actionSeconds, 5, false, alpha);
        }

        if (action == HerdAction.Digging)
        {
            DrawDirt(drawList, feet, time, unit);
        }

        SavedBurst(drawList, hut - new Vector2(0f, 0.5f * unit), time - walkEnd, unit, scale);
        var body = feet - new Vector2(0f, BodyLift * unit);
        if (time >= 1.1f && time < SkillDigAt)
        {
            DrawTarget(drawList, body, HerdSkill.Dig, unit, accent, seconds);
        }

        Shockwave(drawList, body, time - SkillDigAt, unit, accent);
        GuideKit.Key(drawList, At(stage, unit, SkillKeyX, SkillKeyY), DigKeyLabel, time >= 0.24f && time < 0.24f + KeyHold,
            accent, scale);
        var selected = time >= SkillPickAt ? HerdSkill.Dig : HerdSkill.Block;
        var digTile = Vector2.Zero;
        for (var index = 0; index < HerdLevel.SkillCount; index++)
        {
            var skill = (HerdSkill)index;
            var isSelected = skill == selected;
            var center = new Vector2(art.Min.X + tileWidth * (index + 0.5f) + gap * index,
                trayTop + tileHeight * 0.5f - (isSelected ? TileLift * scale : 0f));
            var pop = 1f;
            var count = TrayCounts[index];
            if (skill == HerdSkill.Dig)
            {
                digTile = center;
                pop = MathF.Max(Pop(time - SkillPickAt), Pop(time - SkillDigAt));
                count = time >= SkillDigAt ? count - 1 : count;
            }

            HerdRenderer.DrawSkillTile(drawList, center, new Vector2(tileWidth, tileHeight) * 0.5f * pop, skill, count,
                isSelected, false, accent, scale);
        }

        DrawSkillFinger(drawList, art, time, digTile, body, scale);
    }

    private static Vector2 SkillFeet(Rect stage, float unit, float time, float digX, float digEnd, float fallEnd,
        float walkEnd, out HerdAction action, out float actionSeconds)
    {
        actionSeconds = 0f;
        if (time < SkillDigAt)
        {
            action = HerdAction.Walking;
            return At(stage, unit, SkillEnterX + WalkSpeed * time, LedgeTop);
        }

        if (time < digEnd)
        {
            action = HerdAction.Digging;
            actionSeconds = time - SkillDigAt;
            return At(stage, unit, digX, LedgeTop + DigSpeed * actionSeconds);
        }

        if (time < fallEnd)
        {
            action = HerdAction.Falling;
            return At(stage, unit, digX, LedgeBottom + FallSpeed * (time - digEnd));
        }

        if (time < walkEnd)
        {
            action = HerdAction.Walking;
            return At(stage, unit, digX + WalkSpeed * (time - fallEnd), SkillFloor);
        }

        actionSeconds = time - walkEnd;
        action = actionSeconds < ExitSeconds ? HerdAction.Exiting : HerdAction.Saved;
        return At(stage, unit, SkillHutX, SkillFloor);
    }

    private static void DrawSkillFinger(ImDrawListPtr drawList, Rect art, float time, Vector2 tile, Vector2 body,
        float scale)
    {
        if (time < 1f)
        {
            GuideKit.Tap(drawList, tile, GuideKit.Window(time, 0.2f, 1f), scale);
            return;
        }

        if (time < 1.55f)
        {
            GuideKit.Tap(drawList, Vector2.Lerp(tile, body, Easing.SmoothStep(GuideKit.Window(time, 1f, 1.55f))), 0f,
                scale);
            return;
        }

        if (time < 2.35f)
        {
            GuideKit.Tap(drawList, body, GuideKit.Window(time, 1.55f, 2.35f), scale);
            return;
        }

        if (time < 2.95f)
        {
            var exit = art.Min - new Vector2(FingerOffstage, FingerOffstage) * scale;
            GuideKit.Tap(drawList, Vector2.Lerp(body, exit, Easing.SmoothStep(GuideKit.Window(time, 2.35f, 2.95f))), 0f,
                scale);
        }
    }

    private static void DrawDropPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % DropPeriod;
        var stage = GuideKit.Fit(art, DropWidth / DropHeight);
        var unit = stage.Width / DropWidth;
        var floor = At(stage, unit, 0f, DropFloor).Y;
        Ground(drawList, new Vector2(art.Min.X, floor), art.Max, unit, true, 1f);
        Ground(drawList, new Vector2(art.Min.X, At(stage, unit, 0f, CliffTop).Y), At(stage, unit, CliffEdge, DropFloor),
            unit, true, 1f);
        Ground(drawList, At(stage, unit, LaneWidth, CliffTop), At(stage, unit, LaneWidth + CliffEdge, DropFloor), unit,
            true, 1f);
        var walkEnd = (DropLeaveX - DropStartX) / WalkSpeed;
        var drop = DropFloor - CliffTop;
        DrawSplatLane(drawList, stage, unit, time, seconds, walkEnd, drop, scale);
        DrawFloatLane(drawList, stage, unit, time, seconds, walkEnd, drop, scale);
        var icon = At(stage, unit, LaneWidth + 2.6f, 0.75f);
        var radius = 0.36f * unit;
        var corner = new Vector2(radius, radius);
        Material.Frosted(drawList, icon - corner, icon + corner, radius, scale, 0.95f);
        Squircle.Stroke(drawList, icon - corner, icon + corner, radius, ImGui.GetColorU32(accent), 1.5f * scale);
        HerdArt.DrawSkillIcon(drawList, HerdSkill.Float, icon + new Vector2(0f, 0.04f * unit), radius * 1.1f,
            ImGui.GetColorU32(HerdArt.Ink));
    }

    private static void DrawSplatLane(ImDrawListPtr drawList, Rect stage, float unit, float time, float seconds,
        float walkEnd, float drop, float scale)
    {
        var land = walkEnd + drop / FallSpeed;
        var impact = At(stage, unit, DropLeaveX, DropFloor);
        if (time < walkEnd)
        {
            Moogle(drawList, At(stage, unit, DropStartX + WalkSpeed * time, CliffTop), unit, 1, HerdAction.Walking,
                seconds, 0f, 1, false, 1f);
        }
        else if (time < land)
        {
            Moogle(drawList, At(stage, unit, DropLeaveX, CliffTop + FallSpeed * (time - walkEnd)), unit, 1,
                HerdAction.Falling, seconds, 0f, 1, false, 1f);
        }
        else if (time < land + SplatSeconds)
        {
            Moogle(drawList, impact, unit, 1, HerdAction.Splatting, seconds, time - land, 1, false,
                1f - (time - land) / SplatSeconds);
        }

        var since = time - land;
        if (since < 0f)
        {
            return;
        }

        Ring(drawList, impact - new Vector2(0f, 0.2f * unit), since, 0.8f * unit, HerdRenderer.Danger);
        DrawFur(drawList, impact - new Vector2(0f, 0.3f * unit), since, unit);
        Mark(drawList, At(stage, unit, LaneWidth * 0.62f, CliffTop + drop * 0.45f), since - MarkDelay, unit, scale,
            false);
    }

    private static void DrawFloatLane(ImDrawListPtr drawList, Rect stage, float unit, float time, float seconds,
        float walkEnd, float drop, float scale)
    {
        var open = walkEnd + FloatOpenDrop / FallSpeed;
        var land = open + (drop - FloatOpenDrop) / WalkSpeed;
        if (time < walkEnd)
        {
            Moogle(drawList, At(stage, unit, LaneWidth + DropStartX + WalkSpeed * time, CliffTop), unit, 1,
                HerdAction.Walking, seconds, 0f, 3, true, 1f);
        }
        else if (time < open)
        {
            Moogle(drawList, At(stage, unit, LaneWidth + DropLeaveX, CliffTop + FallSpeed * (time - walkEnd)), unit, 1,
                HerdAction.Falling, seconds, 0f, 3, true, 1f);
        }
        else if (time < land)
        {
            Moogle(drawList, At(stage, unit, LaneWidth + DropLeaveX, CliffTop + FloatOpenDrop + WalkSpeed * (time - open)),
                unit, 1, HerdAction.Floating, seconds, 0f, 3, true, 1f);
        }
        else
        {
            Moogle(drawList, At(stage, unit, LaneWidth + DropLeaveX + WalkSpeed * (time - land), DropFloor), unit, 1,
                HerdAction.Walking, seconds, 0f, 3, true, 1f);
        }

        Mark(drawList, At(stage, unit, LaneWidth * 1.62f, CliffTop + drop * 0.45f), time - land - MarkDelay, unit,
            scale, true);
    }

    private static void DrawPausePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var time = seconds % PausePeriod;
        var radius = MathF.Min(ControlRadius * scale, art.Height * 0.09f);
        var gap = TileGap * scale;
        var rowCenter = art.Max.Y - radius - RowInset * scale;
        var scene = new Rect(art.Min, new Vector2(art.Max.X, rowCenter - radius - gap * 2f));
        var stage = GuideKit.Fit(scene, PauseWidth / PauseHeight);
        var unit = stage.Width / PauseWidth;
        var floor = At(stage, unit, 0f, PauseFloor).Y;
        Ground(drawList, new Vector2(art.Min.X, floor), new Vector2(art.Max.X, scene.Max.Y), unit, true, 1f);
        Ground(drawList, new Vector2(art.Min.X, At(stage, unit, 0f, WallTop).Y), new Vector2(At(stage, unit, WallRight, 0f).X, floor),
            unit, true, 1f);
        var board = BoardSeconds(time);
        var blockerX = BlockerStartX + WalkSpeed * MathF.Min(board, PauseAt);
        var blocking = time >= BlockAt;
        var blocker = At(stage, unit, blockerX, PauseFloor);
        Moogle(drawList, blocker, unit, 1, blocking ? HerdAction.Blocking : HerdAction.Walking, seconds, 0f, 4, false,
            1f);
        var walker = WalkerX(board, blockerX - BlockerReach - BlockerClearance, out var direction);
        Moogle(drawList, At(stage, unit, walker, PauseFloor), unit, direction, HerdAction.Walking, seconds, 0f, 7, false,
            1f);
        var body = blocker - new Vector2(0f, BodyLift * unit);
        if (time >= 1.1f && time < BlockAt)
        {
            DrawTarget(drawList, body, HerdSkill.Block, unit, accent, seconds);
        }

        Shockwave(drawList, body, time - BlockAt, unit, accent);
        var spaceDown = Pressed(time, PauseAt) || Pressed(time, ResumeAt);
        GuideKit.SpaceKey(drawList, At(stage, unit, PauseWidth * 0.36f, PauseKeyY), spaceDown, accent, scale);
        GuideKit.Key(drawList, At(stage, unit, PauseWidth * 0.64f, PauseKeyY), FastKeyLabel, Pressed(time, FastAt),
            accent, scale);
        var paused = time >= PauseAt && time < ResumeAt;
        for (var control = 0; control < ControlCount; control++)
        {
            var center = new Vector2(art.Max.X - radius - (HerdRenderer.BombControl - control) * (radius * 2f + gap),
                rowCenter);
            var active = control == HerdRenderer.PauseControl ? paused : control == HerdRenderer.FastControl && time >= FastAt;
            HerdRenderer.DrawControl(drawList, center, radius, control, active, false, accent, scale);
        }

        DrawPauseFinger(drawList, art, time, body, scale);
    }

    private static void DrawPauseFinger(ImDrawListPtr drawList, Rect art, float time, Vector2 body, float scale)
    {
        var entry = new Vector2(art.Min.X + art.Width * 0.25f, art.Max.Y + FingerOffstage * scale);
        if (time < 1f || time >= 2.85f)
        {
            return;
        }

        if (time < 1.45f)
        {
            GuideKit.Tap(drawList, Vector2.Lerp(entry, body, Easing.SmoothStep(GuideKit.Window(time, 1f, 1.45f))), 0f,
                scale);
            return;
        }

        if (time < 2.25f)
        {
            GuideKit.Tap(drawList, body, GuideKit.Window(time, 1.45f, 2.25f), scale);
            return;
        }

        GuideKit.Tap(drawList, Vector2.Lerp(body, entry, Easing.SmoothStep(GuideKit.Window(time, 2.25f, 2.85f))), 0f,
            scale);
    }

    private static float BoardSeconds(float time)
    {
        if (time < PauseAt)
        {
            return time;
        }

        if (time < ResumeAt)
        {
            return PauseAt;
        }

        if (time < FastAt)
        {
            return PauseAt + time - ResumeAt;
        }

        return PauseAt + FastAt - ResumeAt + (time - FastAt) * FastFactor;
    }

    private static float WalkerX(float board, float right, out int direction)
    {
        var travelled = WalkSpeed * board;
        var firstLeg = right - WalkerStartX;
        if (travelled < firstLeg)
        {
            direction = 1;
            return WalkerStartX + travelled;
        }

        var left = WallRight + WallClearance;
        var span = right - left;
        var bounce = Wrap(travelled - firstLeg, span * 2f);
        if (bounce < span)
        {
            direction = -1;
            return right - bounce;
        }

        direction = 1;
        return left + bounce - span;
    }

    private static bool Pressed(float time, float at) => time >= at - 0.1f && time < at - 0.1f + KeyHold;

    private static void Moogle(ImDrawListPtr drawList, Vector2 feet, float unit, int direction, HerdAction action,
        float seconds, float actionSeconds, byte variant, bool floater, float alpha)
    {
        var moogle = new HerdMoogle
        {
            Direction = (sbyte)direction,
            Action = action,
            ActionTicks = (short)(actionSeconds / HerdBoard.TickSeconds),
            Variant = variant,
            Floater = floater,
        };
        var size = unit * HerdRenderer.MoogleScale;
        HerdArt.DrawMoogle(drawList, feet, size, direction, HerdArt.PoseFor(moogle, seconds), alpha,
            HerdRenderer.PomPomFor(variant));
        if (floater)
        {
            HerdRenderer.DrawBadges(drawList, feet, size, moogle, alpha);
        }
    }

    private static void Ground(ImDrawListPtr drawList, Vector2 min, Vector2 max, float unit, bool grass, float alpha)
    {
        if (max.X <= min.X || max.Y <= min.Y)
        {
            return;
        }

        var material = TerrainMaterial.Earth;
        var top = ImGui.GetColorU32(material.Surface with { W = alpha });
        var bottom = ImGui.GetColorU32(Vector4.Lerp(material.Surface, material.Deep, DeepShare) with { W = alpha });
        drawList.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
        var band = MathF.Max(1f, 0.08f * unit);
        for (var stratum = 1; stratum <= 2; stratum++)
        {
            var y = min.Y + (max.Y - min.Y) * stratum / 3f;
            var shade = Vector4.Lerp(material.Surface, material.Deep, DeepShare * stratum / 3f) * StrataShade;
            drawList.AddRectFilled(new Vector2(min.X, y), new Vector2(max.X, MathF.Min(max.Y, y + band)),
                ImGui.GetColorU32(shade with { W = alpha }));
        }

        if (!grass)
        {
            return;
        }

        var edge = MathF.Min(max.Y, min.Y + GrassDepth * unit);
        drawList.AddRectFilled(min, new Vector2(max.X, edge), ImGui.GetColorU32(material.Edge with { W = alpha }));
        drawList.AddRectFilled(new Vector2(min.X, edge), new Vector2(max.X, MathF.Min(max.Y, edge + GrassBlend * unit)),
            ImGui.GetColorU32(Vector4.Lerp(material.Edge, material.Surface, 0.5f) with { W = alpha }));
    }

    private static void DrawTarget(ImDrawListPtr drawList, Vector2 body, HerdSkill skill, float unit, Vector4 accent,
        float seconds)
    {
        HerdRenderer.DrawTarget(drawList, body, TargetRadius * unit, accent, seconds);
        HerdArt.DrawSkillIcon(drawList, skill, body - new Vector2(0f, TargetIconLift * unit), TargetIconSize * unit,
            ImGui.GetColorU32(accent));
    }

    private static void Shockwave(ImDrawListPtr drawList, Vector2 center, float since, float unit, Vector4 accent)
    {
        Ring(drawList, center, since, 0.9f * unit, accent);
    }

    private static void Ring(ImDrawListPtr drawList, Vector2 center, float since, float reach, Vector4 color)
    {
        if (since < 0f || since >= RingSeconds)
        {
            return;
        }

        var progress = since / RingSeconds;
        drawList.AddCircle(center, reach * Easing.EaseOutCubic(progress), ImGui.GetColorU32(color with { W = 1f - progress }),
            32, MathF.Max(1.5f, reach * 0.08f * (1f - progress)));
    }

    private static void SavedBurst(ImDrawListPtr drawList, Vector2 center, float since, float unit, float scale)
    {
        if (since < 0f || since >= FloatTextSeconds)
        {
            return;
        }

        Ring(drawList, center, since, 1.2f * unit, HerdRenderer.Gold);
        var rise = FloatTextRise * scale * since + 20f * scale;
        Typography.DrawCentered(drawList, center - new Vector2(0f, rise), GameNumber.Signed(1),
            HerdRenderer.Gold with { W = 1f - since / FloatTextSeconds }, TextStyles.Headline);
    }

    private static void DrawDirt(ImDrawListPtr drawList, Vector2 feet, float time, float unit)
    {
        var half = new Vector2(0.04f * unit, 0.04f * unit);
        for (var bit = 0; bit < DirtBits; bit++)
        {
            var life = Wrap(time * 2.4f + bit / (float)DirtBits, 1f);
            var side = bit % 2 == 0 ? -1f : 1f;
            var spread = 0.3f + 0.08f * bit;
            var offset = new Vector2(side * spread * life, -0.9f * life + 1.2f * life * life) * unit;
            drawList.AddRectFilled(feet + offset - half, feet + offset + half,
                ImGui.GetColorU32(Earth with { W = 1f - life }));
        }
    }

    private static void DrawFur(ImDrawListPtr drawList, Vector2 center, float since, float unit)
    {
        if (since >= FurSeconds)
        {
            return;
        }

        var progress = since / FurSeconds;
        for (var bit = 0; bit < FurBits; bit++)
        {
            var angle = MathF.PI * (1.05f + 0.9f * bit / (FurBits - 1));
            var reach = (0.5f + 0.25f * (bit % 3)) * unit * Easing.EaseOutCubic(progress);
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach +
                        new Vector2(0f, 0.6f * unit * progress * progress);
            var color = bit % 3 == 0 ? HerdArt.PomPom : HerdArt.Fur;
            drawList.AddCircleFilled(point, MathF.Max(1f, 0.06f * unit * (1f - progress * 0.5f)),
                ImGui.GetColorU32(color with { W = 1f - progress }), 8);
        }
    }

    private static void Mark(ImDrawListPtr drawList, Vector2 center, float since, float unit, float scale, bool good)
    {
        if (since < 0f)
        {
            return;
        }

        var size = 0.75f * unit * Easing.EaseOutCubic(GuideKit.Window(since, 0f, MarkGrow));
        if (good)
        {
            GuideKit.Check(drawList, center, size, scale);
            return;
        }

        GuideKit.Cross(drawList, center, size, scale);
    }

    private static float Pop(float since)
    {
        var progress = since * PopDecay;
        return progress < 0f || progress > 1f ? 1f : 1f + 0.12f * MathF.Sin(progress * MathF.PI);
    }

    private static Vector2 At(Rect stage, float unit, float x, float y) => stage.Min + new Vector2(x, y) * unit;

    private static float Wrap(float value, float period) => (value % period + period) % period;
}
