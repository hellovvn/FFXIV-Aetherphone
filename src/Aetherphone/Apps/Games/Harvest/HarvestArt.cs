using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Harvest;

internal struct HarvestPose
{
    public Vector2 Feet;
    public float Unit;
    public int Facing;
    public float RunPhase;
    public float Crouch;
    public float Air;
    public float Stun;
    public float Alpha;
    public float Time;
    public float Lean;
}

internal static class HarvestArt
{
    public const float BasketHalfWidth = 7f;
    public static readonly Vector4 Plumage = new(1f, 0.82f, 0.26f, 1f);
    public static readonly Vector4 SparkGlow = new(0.72f, 0.48f, 1f, 1f);
    public static readonly Vector4 SparkCore = new(0.96f, 0.92f, 1f, 1f);
    public static readonly Vector4 BirdWarning = new(1f, 0.38f, 0.28f, 1f);
    public static readonly Vector4 BoulderWarning = new(1f, 0.74f, 0.22f, 1f);
    public static readonly Vector4 ShardGold = new(1f, 0.86f, 0.36f, 1f);
    public static readonly Vector4 Outline = new(0.07f, 0.05f, 0.09f, 0.9f);
    private const int CrystalElements = 6;
    private const int SparkArcs = 5;
    private const int ShardRays = 4;
    private static readonly Vector4 PlumageShade = new(0.90f, 0.60f, 0.14f, 1f);
    private static readonly Vector4 PlumageLight = new(1f, 0.94f, 0.64f, 1f);
    private static readonly Vector4 ChocoboOutline = new(0.22f, 0.12f, 0.04f, 0.85f);
    private static readonly Vector4 Beak = new(0.98f, 0.56f, 0.16f, 1f);
    private static readonly Vector4 Leg = new(0.97f, 0.64f, 0.22f, 1f);
    private static readonly Vector4 Eye = new(0.10f, 0.08f, 0.10f, 1f);
    private static readonly Vector4 Wicker = new(0.64f, 0.43f, 0.22f, 1f);
    private static readonly Vector4 WickerDark = new(0.42f, 0.26f, 0.12f, 1f);
    private static readonly Vector4 WickerLight = new(0.86f, 0.66f, 0.40f, 1f);
    private static readonly Vector4 Strap = new(0.30f, 0.22f, 0.40f, 1f);
    private static readonly Vector4 DizzyStar = new(1f, 0.92f, 0.45f, 1f);
    private static readonly Vector4 OreBody = new(0.44f, 0.44f, 0.52f, 1f);
    private static readonly Vector4 OreFacet = new(0.66f, 0.66f, 0.74f, 1f);
    private static readonly Vector4 OreFleck = new(0.98f, 0.64f, 0.30f, 1f);
    private static readonly Vector4 Bark = new(0.50f, 0.32f, 0.17f, 1f);
    private static readonly Vector4 BarkLight = new(0.66f, 0.45f, 0.25f, 1f);
    private static readonly Vector4 Heartwood = new(0.92f, 0.74f, 0.47f, 1f);
    private static readonly Vector4 Ring = new(0.70f, 0.50f, 0.28f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Halo = new(1f, 1f, 1f, 0.28f);
    private static readonly Vector4 BirdBody = new(0.17f, 0.19f, 0.32f, 1f);
    private static readonly Vector4 BirdWing = new(0.25f, 0.28f, 0.46f, 1f);
    private static readonly Vector4 BirdBelly = new(0.86f, 0.88f, 0.96f, 1f);
    private static readonly Vector4 BirdEye = new(1f, 0.30f, 0.24f, 1f);
    private static readonly Vector4 Stone = new(0.50f, 0.42f, 0.36f, 1f);
    private static readonly Vector4 StoneShade = new(0.32f, 0.26f, 0.22f, 1f);
    private static readonly Vector4 StoneLight = new(0.70f, 0.62f, 0.54f, 1f);
    private static readonly Vector4[] CrystalColors =
    {
        new(1f, 0.44f, 0.32f, 1f), new(0.56f, 0.86f, 1f, 1f), new(0.46f, 0.94f, 0.58f, 1f),
        new(0.96f, 0.80f, 0.36f, 1f), new(1f, 0.56f, 0.78f, 1f), new(0.38f, 0.62f, 1f, 1f),
    };

    public static Vector4 MaterialColor(HarvestMaterial material, float seed) => material switch
    {
        HarvestMaterial.Ore => OreFacet,
        HarvestMaterial.Log => BarkLight,
        HarvestMaterial.Crystal => CrystalColor(seed),
        _ => ShardGold,
    };

    public static Vector4 CrystalColor(float seed) => CrystalColors[(int)MathF.Abs(seed) % CrystalElements];

    public static void DrawMaterial(ImDrawListPtr drawList, Vector2 center, float unit, HarvestMaterial material,
        float seed, float time, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var radius = HarvestBoard.DropRadius * unit;
        drawList.AddCircleFilled(center, radius * 1.45f, Color(Halo, alpha), 20);
        switch (material)
        {
            case HarvestMaterial.Ore:
                DrawOre(drawList, center, radius, seed, alpha);
                return;
            case HarvestMaterial.Log:
                DrawLog(drawList, center, radius, seed + time * 0.8f, alpha);
                return;
            case HarvestMaterial.Crystal:
                DrawCrystal(drawList, center, radius, CrystalColor(seed), time + seed, alpha);
                return;
            default:
                DrawShard(drawList, center, radius * 1.1f, time + seed, alpha);
                return;
        }
    }

    public static void DrawSpark(ImDrawListPtr drawList, Vector2 center, float unit, float time, float alpha,
        float charge = 1f)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var radius = HarvestBoard.SparkRadius * unit;
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(center, radius * (0.6f + layer * 0.45f) * charge,
                Color(SparkGlow, 0.16f * (4 - layer) * alpha), 20);
        }

        var thickness = MathF.Max(1f, 0.32f * unit);
        for (var arc = 0; arc < SparkArcs; arc++)
        {
            var angle = time * 7f + arc * MathF.Tau / SparkArcs + MathF.Sin(time * 23f + arc) * 0.4f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = new Vector2(-direction.Y, direction.X);
            var reach = radius * (1.1f + 0.5f * charge);
            var kink = MathF.Sin(time * 31f + arc * 2.3f) * radius * 0.35f;
            drawList.PathLineTo(center + direction * radius * 0.4f);
            drawList.PathLineTo(center + direction * reach * 0.6f + normal * kink);
            drawList.PathLineTo(center + direction * reach - normal * kink * 0.6f);
            drawList.PathStroke(Color(SparkCore, 0.85f * alpha), ImDrawFlags.None, thickness);
        }

        drawList.AddCircleFilled(center, radius * 0.55f * charge, Color(SparkCore, alpha), 16);
    }

    public static void DrawBird(ImDrawListPtr drawList, Vector2 center, float unit, int direction, float flap,
        float alpha)
    {
        var beat = MathF.Sin(flap);
        drawList.AddCircleFilled(center, 5.4f * unit, Color(BirdWarning, 0.22f * alpha), 24);
        DrawWing(drawList, center, unit, direction, -beat * 0.8f, Color(BirdBody, alpha));
        Shapes.FillEllipse(drawList, center, 4.1f * unit, 2.6f * unit, Color(BirdBelly, 0.75f * alpha), 20);
        Shapes.FillEllipse(drawList, center, 3.6f * unit, 2.1f * unit, Color(Outline, alpha), 20);
        Shapes.FillEllipse(drawList, center, 3.2f * unit, 1.7f * unit, Color(BirdBody, alpha), 20);
        Shapes.FillEllipse(drawList, center + new Vector2(direction * 0.3f * unit, 0.7f * unit), 2.2f * unit,
            0.8f * unit, Color(BirdBelly, alpha), 16);
        var tailRoot = center + new Vector2(-direction * 2.8f * unit, 0f);
        drawList.AddTriangleFilled(tailRoot + new Vector2(0f, -0.6f * unit),
            tailRoot + new Vector2(-direction * 2.4f * unit, -1.2f * unit),
            tailRoot + new Vector2(-direction * 2.2f * unit, 0.9f * unit), Color(BirdBody, alpha));
        var head = center + new Vector2(direction * 3f * unit, -0.8f * unit);
        drawList.AddCircleFilled(head, 1.55f * unit, Color(Outline, alpha), 14);
        drawList.AddCircleFilled(head, 1.3f * unit, Color(BirdBody, alpha), 14);
        drawList.AddTriangleFilled(head + new Vector2(direction * 1.1f * unit, -0.5f * unit),
            head + new Vector2(direction * 3f * unit, 0.15f * unit),
            head + new Vector2(direction * 1.1f * unit, 0.55f * unit), Color(Beak, alpha));
        drawList.AddCircleFilled(head + new Vector2(direction * 0.45f * unit, -0.3f * unit), 0.42f * unit,
            Color(BirdEye, alpha), 10);
        DrawWing(drawList, center, unit, direction, beat, Color(BirdWing, alpha));
    }

    public static void DrawBoulder(ImDrawListPtr drawList, Vector2 center, float unit, float spin, float alpha)
    {
        var radius = HarvestBoard.BoulderRadius * unit;
        drawList.AddCircleFilled(center, radius + 0.5f * unit, Color(Outline, alpha), 24);
        drawList.AddCircleFilled(center, radius, Color(StoneShade, alpha), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.12f, radius * 0.12f), radius * 0.86f,
            Color(Stone, alpha), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.38f, radius * 0.4f), radius * 0.3f,
            Color(StoneLight, 0.8f * alpha), 14);
        var crack = Color(StoneShade, alpha);
        var thickness = MathF.Max(1f, 0.35f * unit);
        for (var line = 0; line < 3; line++)
        {
            var angle = spin + line * MathF.Tau / 3f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = new Vector2(-direction.Y, direction.X);
            var start = center + direction * radius * 0.25f;
            drawList.AddLine(start, start + direction * radius * 0.45f + normal * radius * 0.15f, crack, thickness);
        }
    }

    public static void DrawWarning(ImDrawListPtr drawList, Vector2 center, float size, HarvestHazardKind kind,
        float remaining, float pulse)
    {
        var color = kind == HarvestHazardKind.Bird ? BirdWarning : BoulderWarning;
        var radius = size * (1f + 0.12f * pulse);
        drawList.AddCircleFilled(center, radius * 1.6f, Color(color, 0.18f + 0.12f * pulse), 28);
        drawList.AddCircleFilled(center, radius + size * 0.12f, Color(Outline, 0.9f), 28);
        drawList.AddCircleFilled(center, radius, Color(color, 0.95f), 28);
        if (remaining > 0f)
        {
            var start = -MathF.PI * 0.5f;
            drawList.PathArcTo(center, radius + size * 0.32f, start, start + MathF.Tau * Math.Clamp(remaining, 0f, 1f),
                24);
            drawList.PathStroke(Color(White, 0.85f), ImDrawFlags.None, MathF.Max(1f, size * 0.14f));
        }

        var arm = size * 0.48f;
        var sign = kind == HarvestHazardKind.Bird ? 1f : -1f;
        var tip = center + new Vector2(0f, sign * arm * 0.75f);
        var thickness = MathF.Max(1.5f, size * 0.2f);
        var ink = Color(Outline, 1f);
        drawList.AddLine(tip, tip + new Vector2(-arm * 0.85f, -sign * arm * 0.85f), ink, thickness);
        drawList.AddLine(tip, tip + new Vector2(arm * 0.85f, -sign * arm * 0.85f), ink, thickness);
        drawList.AddLine(tip, tip - new Vector2(0f, sign * arm * 1.5f), ink, thickness);
    }

    public static void DrawChocobo(ImDrawListPtr drawList, in HarvestPose pose, ReadOnlySpan<HarvestMaterial> pile,
        ReadOnlySpan<float> pileSeeds)
    {
        if (pose.Alpha <= 0.01f)
        {
            return;
        }

        var crouch = pose.Crouch;
        var bob = (1f - pose.Air) * (1f - crouch) * 0.5f * MathF.Abs(MathF.Sin(pose.RunPhase));
        var drop = (HarvestPlayer.StandRim - HarvestPlayer.DuckRim) * crouch;
        var bodyY = 8f - drop + bob;
        DrawLegs(drawList, pose, 6.2f - drop + bob);
        DrawTail(drawList, pose, bodyY);
        Ellipse(drawList, pose, -0.4f, bodyY, 6.05f, 4.35f, ChocoboOutline);
        Ellipse(drawList, pose, -0.4f, bodyY, 5.6f, 3.9f, PlumageShade);
        Ellipse(drawList, pose, -0.4f, bodyY + 0.45f, 5.25f, 3.35f, Plumage);
        Ellipse(drawList, pose, 1.6f, bodyY - 1.2f, 2.6f, 1.6f, PlumageLight with { W = 0.7f });
        DrawHead(drawList, pose, bodyY, crouch, bob);
        var flap = pose.Air * MathF.Sin(pose.Time * 22f) * 0.6f;
        Ellipse(drawList, pose, -1.2f, bodyY + 0.6f + flap, 3.3f, 1.8f, PlumageShade);
        var rim = HarvestPlayer.StandRim - drop + bob;
        DrawBasket(drawList, pose, rim, pile, pileSeeds);
        if (pose.Stun > 0f)
        {
            DrawDizzy(drawList, pose, rim + 5f);
        }
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 center, float unit, int direction, float beat,
        uint color)
    {
        var shoulder = center + new Vector2(direction * 0.4f * unit, -0.6f * unit);
        var tip = shoulder + new Vector2(-direction * 2.6f * unit, -beat * 4.6f * unit);
        var back = shoulder + new Vector2(-direction * 3.4f * unit, -beat * 1.6f * unit + 0.8f * unit);
        drawList.AddTriangleFilled(shoulder + new Vector2(direction * 1.2f * unit, 0f), tip, back, color);
    }

    private static void DrawOre(ImDrawListPtr drawList, Vector2 center, float radius, float seed, float alpha)
    {
        const int Corners = 6;
        var outline = Color(Outline, alpha);
        for (var pass = 0; pass < 2; pass++)
        {
            var grow = pass == 0 ? 1.18f : 1f;
            for (var corner = 0; corner < Corners; corner++)
            {
                var angle = corner * MathF.Tau / Corners + seed * 0.7f;
                var reach = radius * grow * (0.82f + 0.18f * MathF.Sin(seed * 3.1f + corner * 1.9f));
                drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach);
            }

            drawList.PathFillConvex(pass == 0 ? outline : Color(OreBody, alpha));
        }

        drawList.AddTriangleFilled(center + new Vector2(-radius * 0.6f, -radius * 0.2f),
            center + new Vector2(radius * 0.1f, -radius * 0.75f), center + new Vector2(radius * 0.25f, -radius * 0.05f),
            Color(OreFacet, alpha));
        drawList.AddCircleFilled(center + new Vector2(radius * 0.35f, radius * 0.3f), radius * 0.2f,
            Color(OreFleck, alpha), 8);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.3f, radius * 0.4f), radius * 0.13f,
            Color(OreFleck, alpha), 8);
    }

    private static void DrawLog(ImDrawListPtr drawList, Vector2 center, float radius, float angle, float alpha)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var normal = new Vector2(-direction.Y, direction.X);
        var half = direction * radius * 1.05f;
        var thick = normal * radius * 0.62f;
        var pad = radius * 0.16f;
        var padded = direction * pad + normal * pad;
        drawList.AddQuadFilled(center - half - thick - padded, center + half - thick - normal * pad + direction * pad,
            center + half + thick + padded, center - half + thick + normal * pad - direction * pad, Color(Outline, alpha));
        drawList.AddQuadFilled(center - half - thick, center + half - thick, center + half + thick, center - half + thick,
            Color(Bark, alpha));
        drawList.AddLine(center - half - thick * 0.35f, center + half - thick * 0.35f, Color(BarkLight, alpha),
            MathF.Max(1f, radius * 0.18f));
        var end = center + half;
        Shapes.FillEllipse(drawList, end, radius * 0.3f, radius * 0.66f, angle, Color(Outline, alpha), 14);
        Shapes.FillEllipse(drawList, end, radius * 0.24f, radius * 0.58f, angle, Color(Heartwood, alpha), 14);
        Shapes.FillEllipse(drawList, end, radius * 0.1f, radius * 0.26f, angle, Color(Ring, alpha), 10);
    }

    private static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint, float time,
        float alpha)
    {
        var tall = radius * 1.25f;
        var wide = radius * 0.72f;
        var lean = MathF.Sin(time * 1.3f) * 0.12f;
        var top = center + new Vector2(lean * radius, -tall);
        var bottom = center + new Vector2(-lean * radius, tall);
        var left = center + new Vector2(-wide, -radius * 0.15f);
        var right = center + new Vector2(wide, -radius * 0.15f);
        var outline = Color(Outline, alpha);
        var grow = radius * 0.22f;
        drawList.AddQuadFilled(top - new Vector2(0f, grow), right + new Vector2(grow, 0f), bottom + new Vector2(0f, grow),
            left - new Vector2(grow, 0f), outline);
        drawList.AddQuadFilled(top, right, bottom, left, Color(Palette(tint, -0.25f), alpha));
        drawList.AddTriangleFilled(top, right, center, Color(tint, alpha));
        drawList.AddTriangleFilled(top, center, left, Color(Palette(tint, 0.35f), alpha));
        drawList.AddLine(top + new Vector2(-wide * 0.25f, tall * 0.35f), top + new Vector2(-wide * 0.05f, tall * 0.1f),
            Color(White, 0.9f * alpha), MathF.Max(1f, radius * 0.16f));
    }

    private static void DrawShard(ImDrawListPtr drawList, Vector2 center, float radius, float time, float alpha)
    {
        for (var ray = 0; ray < ShardRays; ray++)
        {
            var angle = time * 1.6f + ray * MathF.PI / ShardRays;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var length = radius * (1.6f + 0.25f * MathF.Sin(time * 5f + ray));
            drawList.AddLine(center - direction * length, center + direction * length, Color(ShardGold, 0.35f * alpha),
                MathF.Max(1f, radius * 0.16f));
        }

        var outline = Color(Outline, alpha);
        var pad = radius * 0.24f;
        Diamond(drawList, center, radius * 0.62f + pad, radius * 1.3f + pad, outline);
        Diamond(drawList, center, radius * 1.15f + pad, radius * 0.5f + pad, outline);
        Diamond(drawList, center, radius * 0.62f, radius * 1.3f, Color(ShardGold, alpha));
        Diamond(drawList, center, radius * 1.15f, radius * 0.5f, Color(ShardGold, alpha));
        Diamond(drawList, center, radius * 0.3f, radius * 0.62f, Color(White, 0.95f * alpha));
    }

    private static void Diamond(ImDrawListPtr drawList, Vector2 center, float halfWidth, float halfHeight, uint color)
    {
        drawList.AddQuadFilled(center - new Vector2(0f, halfHeight), center + new Vector2(halfWidth, 0f),
            center + new Vector2(0f, halfHeight), center - new Vector2(halfWidth, 0f), color);
    }

    private static void DrawLegs(ImDrawListPtr drawList, in HarvestPose pose, float hipY)
    {
        var leg = Color(Leg, pose.Alpha);
        var shade = Color(PlumageShade, pose.Alpha);
        var thickness = MathF.Max(1.5f, 0.85f * pose.Unit);
        for (var side = 0; side < 2; side++)
        {
            var phase = pose.RunPhase + side * MathF.PI;
            var stride = MathF.Sin(phase) * 2.6f * (1f - pose.Air) * (1f - pose.Crouch * 0.6f);
            var lift = MathF.Max(0f, MathF.Cos(phase)) * 1.6f * (1f - pose.Air);
            var hip = new Vector2(0.4f + (side == 0 ? -0.7f : 0.7f), hipY);
            var footX = Lerp(0.8f + stride, -0.4f, pose.Air);
            var footY = Lerp(lift, hipY - 3.4f, pose.Air);
            var knee = new Vector2((hip.X + footX) * 0.5f + 1.3f + pose.Crouch * 1.4f, (hipY + footY) * 0.5f);
            var color = side == 0 ? shade : leg;
            drawList.AddLine(Point(pose, hip.X, hip.Y), Point(pose, knee.X, knee.Y), color, thickness * 1.2f);
            drawList.AddLine(Point(pose, knee.X, knee.Y), Point(pose, footX, footY), color, thickness);
            var foot = Point(pose, footX, footY);
            drawList.AddLine(foot, Point(pose, footX + 1.3f, footY), color, thickness * 0.8f);
            drawList.AddLine(foot, Point(pose, footX - 0.8f, footY), color, thickness * 0.8f);
        }
    }

    private static void DrawTail(ImDrawListPtr drawList, in HarvestPose pose, float bodyY)
    {
        const int Feathers = 4;
        var sway = MathF.Sin(pose.RunPhase) * 0.12f * (1f - pose.Air);
        var root = new Vector2(-5.2f, bodyY + 0.8f);
        for (var feather = 0; feather < Feathers; feather++)
        {
            var angle = 2.2f + feather * 0.26f + sway;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var middle = root + direction * 1.6f;
            Feather(drawList, pose, middle, 2.1f, 0.75f, direction, Color(ChocoboOutline, pose.Alpha));
            Feather(drawList, pose, middle, 1.8f, 0.55f, direction, Color(PlumageLight, pose.Alpha));
        }
    }

    private static void DrawHead(ImDrawListPtr drawList, in HarvestPose pose, float bodyY, float crouch, float bob)
    {
        var head = new Vector2(Lerp(6.4f, 8.8f, crouch) + pose.Lean * 0.7f, Lerp(15.6f, 9.4f, crouch) + bob);
        var neckBase = new Vector2(3.4f, bodyY + 1.2f);
        var neck = Color(Plumage, pose.Alpha);
        drawList.AddLine(Point(pose, neckBase.X, neckBase.Y), Point(pose, head.X - 0.4f, head.Y - 0.6f),
            Color(ChocoboOutline, pose.Alpha), 2.9f * pose.Unit);
        drawList.AddLine(Point(pose, neckBase.X, neckBase.Y), Point(pose, head.X - 0.4f, head.Y - 0.6f), neck,
            2.2f * pose.Unit);
        for (var crest = 0; crest < 3; crest++)
        {
            var angle = 1.9f + crest * 0.32f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tip = head + direction * 3.6f;
            var side = new Vector2(-direction.Y, direction.X) * 0.45f;
            drawList.AddTriangleFilled(Point(pose, head.X + side.X, head.Y + side.Y), Point(pose, tip.X, tip.Y),
                Point(pose, head.X - side.X, head.Y - side.Y), Color(PlumageShade, pose.Alpha));
        }

        var center = Point(pose, head.X, head.Y);
        drawList.AddCircleFilled(center, 2.8f * pose.Unit, Color(ChocoboOutline, pose.Alpha), 20);
        drawList.AddCircleFilled(center, 2.45f * pose.Unit, neck, 20);
        drawList.AddTriangleFilled(Point(pose, head.X + 1.8f, head.Y + 0.7f), Point(pose, head.X + 4.3f, head.Y - 0.3f),
            Point(pose, head.X + 1.8f, head.Y - 0.9f), Color(Beak, pose.Alpha));
        var eye = Point(pose, head.X + 0.9f, head.Y + 0.6f);
        if (pose.Stun > 0f)
        {
            var size = 0.6f * pose.Unit;
            var ink = Color(Eye, pose.Alpha);
            drawList.AddLine(eye - new Vector2(size, size), eye + new Vector2(size, size), ink, MathF.Max(1f, 0.3f * pose.Unit));
            drawList.AddLine(eye + new Vector2(-size, size), eye + new Vector2(size, -size), ink,
                MathF.Max(1f, 0.3f * pose.Unit));
            return;
        }

        drawList.AddCircleFilled(eye, 0.6f * pose.Unit, Color(Eye, pose.Alpha), 10);
        drawList.AddCircleFilled(eye + new Vector2(0.2f, -0.2f) * pose.Unit, 0.2f * pose.Unit, Color(White, pose.Alpha), 6);
    }

    private static void DrawBasket(ImDrawListPtr drawList, in HarvestPose pose, float rim,
        ReadOnlySpan<HarvestMaterial> pile, ReadOnlySpan<float> pileSeeds)
    {
        var count = Math.Min(pile.Length, pileSeeds.Length);
        for (var index = 0; index < count; index++)
        {
            var x = (index - (count - 1) * 0.5f) * 3.6f;
            var center = Point(pose, x, rim + 0.6f + (index % 2) * 0.5f);
            DrawMaterial(drawList, center, pose.Unit * 0.55f, pile[index], pileSeeds[index], 0f, pose.Alpha);
        }

        var bottom = rim - 3.8f;
        var outline = Color(ChocoboOutline, pose.Alpha);
        var pad = 0.45f;
        drawList.AddQuadFilled(Point(pose, -BasketHalfWidth - pad, rim + pad), Point(pose, BasketHalfWidth + pad, rim + pad),
            Point(pose, 5.2f + pad, bottom - pad), Point(pose, -5.2f - pad, bottom - pad), outline);
        drawList.AddQuadFilled(Point(pose, -BasketHalfWidth, rim), Point(pose, BasketHalfWidth, rim),
            Point(pose, 5.2f, bottom), Point(pose, -5.2f, bottom), Color(Wicker, pose.Alpha));
        var weave = Color(WickerDark, pose.Alpha);
        var thickness = MathF.Max(1f, 0.3f * pose.Unit);
        for (var row = 1; row < 3; row++)
        {
            var y = rim - row * 1.27f;
            var half = Lerp(BasketHalfWidth, 5.2f, row / 3f);
            drawList.AddLine(Point(pose, -half, y), Point(pose, half, y), weave, thickness);
        }

        for (var slat = -2; slat <= 2; slat++)
        {
            drawList.AddLine(Point(pose, slat * 2.6f, rim), Point(pose, slat * 2f, bottom), weave, thickness);
        }

        drawList.AddLine(Point(pose, -BasketHalfWidth - 0.2f, rim), Point(pose, BasketHalfWidth + 0.2f, rim),
            Color(WickerLight, pose.Alpha), MathF.Max(1.5f, 0.75f * pose.Unit));
        drawList.AddLine(Point(pose, -2.2f, bottom + 0.2f), Point(pose, -1.2f, bottom - 2.4f), Color(Strap, pose.Alpha),
            MathF.Max(1f, 0.6f * pose.Unit));
    }

    private static void DrawDizzy(ImDrawListPtr drawList, in HarvestPose pose, float height)
    {
        for (var star = 0; star < 3; star++)
        {
            var angle = pose.Time * 6f + star * MathF.Tau / 3f;
            var center = Point(pose, 4f + MathF.Cos(angle) * 3.4f, height + MathF.Sin(angle) * 1.1f);
            var size = 0.9f * pose.Unit;
            var color = Color(DizzyStar, pose.Alpha);
            drawList.AddLine(center - new Vector2(size, 0f), center + new Vector2(size, 0f), color, MathF.Max(1f, 0.3f * pose.Unit));
            drawList.AddLine(center - new Vector2(0f, size), center + new Vector2(0f, size), color, MathF.Max(1f, 0.3f * pose.Unit));
        }
    }

    private static void Ellipse(ImDrawListPtr drawList, in HarvestPose pose, float x, float y, float radiusX,
        float radiusY, Vector4 color)
    {
        Shapes.FillEllipse(drawList, Point(pose, x, y), radiusX * pose.Unit, radiusY * pose.Unit,
            Color(color, pose.Alpha), 24);
    }

    private static void Feather(ImDrawListPtr drawList, in HarvestPose pose, Vector2 middle, float halfLength,
        float halfWidth, Vector2 direction, uint color)
    {
        var screenDirection = new Vector2(direction.X * pose.Facing, -direction.Y);
        Shapes.FillEllipse(drawList, Point(pose, middle.X, middle.Y), halfLength * pose.Unit, halfWidth * pose.Unit,
            MathF.Atan2(screenDirection.Y, screenDirection.X), color, 12);
    }

    private static Vector2 Point(in HarvestPose pose, float x, float y) =>
        pose.Feet + new Vector2(x * pose.Facing * pose.Unit, -y * pose.Unit);

    private static Vector4 Palette(Vector4 color, float amount) =>
        amount >= 0f ? GamePalette.Lighten(color, amount) : GamePalette.Darken(color, -amount);

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}
