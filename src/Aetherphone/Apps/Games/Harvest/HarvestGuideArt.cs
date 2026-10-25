using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Harvest;

internal static class HarvestGuideArt
{
    private const float UnitsTall = 64f;
    private const float CatchPeriod = 3f;
    private const float DodgePeriod = 4.4f;
    private const float FeverPeriod = 4.2f;
    private const float LandPeriod = 1.4f;
    private const int RainDrops = 9;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Pill = new(0.05f, 0.05f, 0.08f, 0.6f);
    private static readonly Vector4 FeverWash = new(1f, 0.80f, 0.30f, 1f);
    private static readonly HarvestMaterial[] Pile = { HarvestMaterial.Ore, HarvestMaterial.Crystal, HarvestMaterial.Log };
    private static readonly float[] PileSeeds = { 1f, 2f, 3f };
    private static readonly HarvestMaterial[] Falling =
    {
        HarvestMaterial.Log, HarvestMaterial.Crystal, HarvestMaterial.Ore, HarvestMaterial.Shard,
    };

    private static readonly LocString[] Names =
    {
        L.Harvest.Meadow, L.Harvest.Highlands, L.Harvest.Mire, L.Harvest.Glacier, L.Harvest.Storm, L.Harvest.Dunes,
    };

    private static readonly LabelSlot[] ComboLabels = new LabelSlot[HarvestRules.MaxMultiplier];

    public static void DrawCatchPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, CatchPeriod);
        HarvestRenderer.DrawPostcard(drawList, art, HarvestBiome.Meadow, 1f);
        var ground = HarvestRenderer.PostcardGround(art);
        var unit = art.Height / UnitsTall;
        var slide = Easing.SmoothStep(GuideKit.Window(phase, 0.12f, 0.55f));
        var playerX = Lerp(art.Min.X + art.Width * 0.28f, art.Min.X + art.Width * 0.7f, slide);
        var fall = GuideKit.Window(phase, 0f, 0.68f);
        var dropX = art.Min.X + art.Width * 0.7f;
        var rim = ground - HarvestPlayer.StandRim * unit;
        if (phase < 0.68f)
        {
            var dropY = Lerp(art.Min.Y + unit * 3f, rim, fall);
            Shapes.FillEllipse(drawList, new Vector2(dropX, ground + unit), (1.5f + 2f * fall) * unit, (0.5f + 0.4f * fall) * unit,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.15f + 0.35f * fall)), 14);
            HarvestArt.DrawMaterial(drawList, new Vector2(dropX, dropY), unit, HarvestMaterial.Crystal, 1f, seconds, 1f);
        }

        DrawChocobo(drawList, playerX, ground, unit, slide > 0f && slide < 1f ? seconds * 18f : 0f, 0f, 0f, seconds,
            phase >= 0.68f ? 3 : 2);
        var pop = GuideKit.Window(phase, 0.68f, 0.95f);
        if (pop > 0f && pop < 1f)
        {
            var center = new Vector2(dropX, rim);
            drawList.AddCircle(center, (2f + 7f * pop) * unit, ImGui.GetColorU32(accent with { W = 1f - pop }), 24,
                MathF.Max(1f, unit * 0.6f));
            Typography.DrawCentered(drawList, center - new Vector2(0f, (5f + 6f * pop) * unit),
                GameNumber.Signed(HarvestRules.BasePoints(HarvestMaterial.Crystal)), StageInks.Strong with { W = 1f - pop },
                TextStyles.SubheadlineEmphasized);
        }

        var swipeY = art.Max.Y - art.Height * 0.07f;
        GuideKit.Swipe(drawList, new Vector2(art.Min.X + art.Width * 0.28f, swipeY),
            new Vector2(art.Min.X + art.Width * 0.7f, swipeY), GuideKit.Window(phase, 0.02f, 0.62f), scale);
        var keyY = art.Min.Y + art.Height * 0.14f;
        GuideKit.Key(drawList, new Vector2(art.Min.X + art.Width * 0.12f, keyY), "A", false, accent, scale);
        GuideKit.Key(drawList, new Vector2(art.Min.X + art.Width * 0.25f, keyY), "D", slide > 0f && slide < 1f, accent,
            scale);
    }

    public static void DrawDodgePage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, DodgePeriod);
        HarvestRenderer.DrawPostcard(drawList, art, HarvestBiome.Highlands, 1f);
        var ground = HarvestRenderer.PostcardGround(art);
        var unit = art.Height / UnitsTall;
        var playerX = art.Center.X;
        var boulderHalf = phase < 0.5f;
        var local = boulderHalf ? phase * 2f : (phase - 0.5f) * 2f;
        var warn = GuideKit.Window(local, 0f, 0.3f);
        var travel = GuideKit.Window(local, 0.3f, 1f);
        var pulse = 0.5f + 0.5f * MathF.Sin(seconds * 14f);
        var height = 0f;
        var crouch = 0f;
        if (boulderHalf)
        {
            var hop = GuideKit.Window(local, 0.52f, 0.8f);
            height = hop > 0f && hop < 1f ? MathF.Sin(hop * MathF.PI) * 17f : 0f;
            if (warn < 1f)
            {
                HarvestArt.DrawWarning(drawList, new Vector2(art.Min.X + 5f * unit, ground - 9f * unit), 3.4f * unit,
                    HarvestHazardKind.Boulder, 1f - warn, pulse);
            }
            else
            {
                var x = Lerp(art.Min.X - 6f * unit, art.Max.X + 6f * unit, travel);
                HarvestArt.DrawBoulder(drawList, new Vector2(x, ground - HarvestBoard.BoulderRadius * unit), unit,
                    travel * 9f, 1f);
            }

            GuideKit.Key(drawList, new Vector2(art.Max.X - art.Width * 0.16f, art.Min.Y + art.Height * 0.14f), "W",
                hop > 0f && hop < 0.3f, accent, scale);
            if (local > 0.4f && local < 0.62f)
            {
                var swipeX = art.Max.X - art.Width * 0.16f;
                GuideKit.Swipe(drawList, new Vector2(swipeX, art.Max.Y - art.Height * 0.08f),
                    new Vector2(swipeX, art.Max.Y - art.Height * 0.38f), GuideKit.Window(local, 0.4f, 0.62f), scale);
            }
        }
        else
        {
            var duck = GuideKit.Window(local, 0.45f, 0.55f) - GuideKit.Window(local, 0.85f, 0.95f);
            crouch = Math.Clamp(duck, 0f, 1f);
            var birdHeight = HarvestBoard.BirdHeight * unit;
            if (warn < 1f)
            {
                HarvestArt.DrawWarning(drawList, new Vector2(art.Max.X - 5f * unit, ground - birdHeight), 3.4f * unit,
                    HarvestHazardKind.Bird, 1f - warn, pulse);
            }
            else
            {
                var x = Lerp(art.Max.X + 6f * unit, art.Min.X - 6f * unit, travel);
                HarvestArt.DrawBird(drawList, new Vector2(x, ground - birdHeight), unit, -1, seconds * 16f, 1f);
            }

            GuideKit.Key(drawList, new Vector2(art.Min.X + art.Width * 0.16f, art.Min.Y + art.Height * 0.14f), "S",
                crouch > 0.5f, accent, scale);
            if (local > 0.32f && local < 0.56f)
            {
                var swipeX = art.Min.X + art.Width * 0.16f;
                GuideKit.Swipe(drawList, new Vector2(swipeX, art.Max.Y - art.Height * 0.38f),
                    new Vector2(swipeX, art.Max.Y - art.Height * 0.08f), GuideKit.Window(local, 0.32f, 0.56f), scale);
            }
        }

        DrawChocobo(drawList, playerX, ground - height * unit, unit, seconds * 12f, crouch, height > 0f ? 1f : 0f,
            seconds, 2);
    }

    public static void DrawFeverPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, FeverPeriod);
        HarvestRenderer.DrawPostcard(drawList, art, HarvestBiome.Meadow, 1f);
        var fever = GuideKit.Window(phase, 0.55f, 0.62f) - GuideKit.Window(phase, 0.95f, 1f);
        if (fever > 0f)
        {
            drawList.AddRectFilled(art.Min, art.Max, ImGui.GetColorU32(FeverWash with { W = 0.22f * fever }));
        }

        var ground = HarvestRenderer.PostcardGround(art);
        var unit = art.Height / UnitsTall;
        var playerX = art.Center.X + MathF.Sin(seconds * 2.2f) * art.Width * (fever > 0f ? 0.18f : 0f);
        var rim = ground - HarvestPlayer.StandRim * unit;
        var caught = 0;
        for (var drop = 0; drop < Falling.Length; drop++)
        {
            var start = drop * 0.12f;
            var fall = GuideKit.Window(phase, start, start + 0.12f);
            if (fall >= 1f)
            {
                caught++;
                continue;
            }

            if (fall <= 0f)
            {
                continue;
            }

            var dropY = Lerp(art.Min.Y + unit * 3f, rim, fall);
            HarvestArt.DrawMaterial(drawList, new Vector2(art.Center.X, dropY), unit, Falling[drop], drop, seconds, 1f);
        }

        if (fever > 0f)
        {
            for (var drop = 0; drop < RainDrops; drop++)
            {
                var lane = (drop + 0.5f) / RainDrops;
                var speed = 0.7f + 0.3f * Hash(drop);
                var travel = (seconds * speed + Hash(drop * 3f)) % 1f;
                var center = new Vector2(art.Min.X + art.Width * lane, Lerp(art.Min.Y, ground, travel));
                HarvestArt.DrawMaterial(drawList, center, unit * 0.8f,
                    drop % 3 == 0 ? HarvestMaterial.Shard : HarvestMaterial.Crystal, drop, seconds, fever);
            }
        }

        DrawChocobo(drawList, playerX, ground, unit, fever > 0f ? seconds * 18f : 0f, 0f, 0f, seconds, Math.Min(3, caught));
        var multiplier = Math.Clamp(1 + caught, 1, HarvestRules.MaxMultiplier);
        var labelCenter = new Vector2(art.Max.X - art.Width * 0.27f, art.Min.Y + art.Height * 0.25f);
        DrawPill(drawList, labelCenter, ComboLabels[multiplier - 1].Get(L.Stage.Times, multiplier),
            multiplier >= HarvestRules.MaxMultiplier ? HarvestArt.ShardGold : StageInks.Strong, scale);
        var barWidth = art.Width * 0.42f;
        var barHeight = 6f * scale;
        var barMin = new Vector2(art.Max.X - art.Width * 0.06f - barWidth, art.Min.Y + art.Height * 0.12f);
        var fill = fever > 0f ? 1f - GuideKit.Window(phase, 0.62f, 0.95f) : Math.Clamp(caught / (float)Falling.Length, 0f, 1f);
        drawList.AddRectFilled(barMin - new Vector2(2f, 2f) * scale, barMin + new Vector2(barWidth, barHeight) + new Vector2(2f, 2f) * scale,
            ImGui.GetColorU32(Pill), barHeight);
        drawList.AddRectFilled(barMin, barMin + new Vector2(barWidth * fill, barHeight), ImGui.GetColorU32(HarvestArt.ShardGold),
            barHeight * 0.5f);
        if (fever > 0f)
        {
            Typography.DrawCentered(drawList, new Vector2(art.Center.X, art.Min.Y + art.Height * 0.32f),
                Loc.T(L.Harvest.Fever), HarvestArt.ShardGold with { W = fever }, TextStyles.Title2);
        }
    }

    public static void DrawLandsPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var total = LandPeriod * HarvestRules.BiomeCount;
        var clock = seconds % total;
        var index = (int)(clock / LandPeriod);
        var local = clock / LandPeriod - index;
        var biome = (HarvestBiome)index;
        HarvestRenderer.DrawPostcard(drawList, art, biome, 1f);
        var ground = HarvestRenderer.PostcardGround(art);
        var unit = art.Height / UnitsTall;
        DrawTwist(drawList, art, biome, ground, unit, seconds, local);
        var slide = biome == HarvestBiome.Glacier ? MathF.Sin(local * MathF.PI) * art.Width * 0.18f : 0f;
        DrawChocobo(drawList, art.Center.X - art.Width * 0.12f + slide, ground, unit, seconds * 10f, 0f, 0f, seconds, 1);
        var fadeIn = GuideKit.Window(local, 0f, 0.12f);
        DrawPill(drawList, new Vector2(art.Center.X, art.Min.Y + art.Height * 0.12f), Loc.T(Names[index]),
            StageInks.Strong with { W = fadeIn }, scale);
        var dot = 3f * scale;
        var gap = 10f * scale;
        var left = art.Center.X - gap * (HarvestRules.BiomeCount - 1) * 0.5f;
        for (var land = 0; land < HarvestRules.BiomeCount; land++)
        {
            var center = new Vector2(left + land * gap, art.Max.Y - art.Height * 0.07f);
            drawList.AddCircleFilled(center, land == index ? dot * 1.3f : dot,
                ImGui.GetColorU32(land == index ? accent : White with { W = 0.45f }), 12);
        }
    }

    private static void DrawTwist(ImDrawListPtr drawList, Rect art, HarvestBiome biome, float ground, float unit,
        float seconds, float local)
    {
        var dropX = art.Min.X + art.Width * 0.72f;
        var fall = local;
        var dropY = Lerp(art.Min.Y + art.Height * 0.2f, ground - 4f * unit, fall);
        switch (biome)
        {
            case HarvestBiome.Meadow:
                HarvestArt.DrawMaterial(drawList, new Vector2(dropX, Lerp(art.Min.Y + art.Height * 0.2f, ground - 4f * unit, fall * 0.6f)),
                    unit, HarvestMaterial.Log, 1f, seconds, 1f);
                return;
            case HarvestBiome.Highlands:
            {
                for (var streak = 0; streak < 5; streak++)
                {
                    var y = art.Min.Y + art.Height * (0.25f + streak * 0.1f);
                    var x = art.Min.X + (seconds * 160f * unit / 4f + streak * art.Width * 0.37f) % art.Width;
                    drawList.AddLine(new Vector2(x, y), new Vector2(x - 8f * unit, y), ImGui.GetColorU32(White with { W = 0.6f }),
                        MathF.Max(1f, 0.4f * unit));
                }

                var drift = (1f - fall) * art.Width * 0.3f;
                HarvestArt.DrawMaterial(drawList, new Vector2(dropX - drift, dropY), unit, HarvestMaterial.Crystal, 2f, seconds, 1f);
                return;
            }
            case HarvestBiome.Mire:
            {
                HarvestArt.DrawMaterial(drawList, new Vector2(dropX, dropY), unit, HarvestMaterial.Ore, 3f, seconds, 1f);
                var fogLine = art.Min.Y + art.Height * 0.55f;
                var dense = ImGui.GetColorU32(new Vector4(0.72f, 0.80f, 0.76f, 0.86f));
                var clear = ImGui.GetColorU32(new Vector4(0.72f, 0.80f, 0.76f, 0f));
                drawList.AddRectFilled(art.Min, new Vector2(art.Max.X, fogLine - art.Height * 0.08f), dense);
                drawList.AddRectFilledMultiColor(new Vector2(art.Min.X, fogLine - art.Height * 0.08f),
                    new Vector2(art.Max.X, fogLine), dense, dense, clear, clear);
                return;
            }
            case HarvestBiome.Glacier:
                for (var glint = 0; glint < 4; glint++)
                {
                    var x = art.Min.X + art.Width * (0.15f + glint * 0.22f);
                    drawList.AddLine(new Vector2(x, ground + 3f * unit), new Vector2(x + 5f * unit, ground + 3f * unit),
                        ImGui.GetColorU32(White with { W = 0.8f }), MathF.Max(1f, 0.4f * unit));
                }

                return;
            case HarvestBiome.Storm:
                HarvestArt.DrawSpark(drawList, new Vector2(dropX, dropY), unit, seconds, 1f);
                for (var y = dropY; y < ground; y += 4f * unit)
                {
                    drawList.AddLine(new Vector2(dropX, y), new Vector2(dropX, MathF.Min(ground, y + 2f * unit)),
                        ImGui.GetColorU32(HarvestArt.SparkGlow with { W = 0.4f }), MathF.Max(1f, 0.4f * unit));
                }

                return;
            default:
            {
                var realX = art.Min.X + art.Width * 0.56f;
                Shapes.FillEllipse(drawList, new Vector2(realX, ground + unit), (1.5f + 2f * fall) * unit, (0.5f + 0.4f * fall) * unit,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f)), 14);
                HarvestArt.DrawMaterial(drawList, new Vector2(realX, dropY), unit, HarvestMaterial.Crystal, 4f, seconds, 1f);
                var shimmer = 0.5f + 0.2f * MathF.Sin(seconds * 6f);
                HarvestArt.DrawMaterial(drawList, new Vector2(art.Min.X + art.Width * 0.86f + MathF.Sin(seconds * 9f) * unit, dropY),
                    unit, HarvestMaterial.Shard, 5f, seconds, shimmer);
                return;
            }
        }
    }

    private static void DrawChocobo(ImDrawListPtr drawList, float x, float feetY, float unit, float runPhase, float crouch,
        float air, float seconds, int pile)
    {
        var groundY = feetY;
        Shapes.FillEllipse(drawList, new Vector2(x, groundY), 6.5f * unit, 1.3f * unit,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.3f)), 20);
        var pose = new HarvestPose
        {
            Feet = new Vector2(x, feetY),
            Unit = unit,
            Facing = 1,
            RunPhase = runPhase,
            Crouch = crouch,
            Air = air,
            Alpha = 1f,
            Time = seconds,
        };
        var count = Math.Clamp(pile, 0, Pile.Length);
        HarvestArt.DrawChocobo(drawList, in pose, Pile.AsSpan(0, count), PileSeeds.AsSpan(0, count));
    }

    private static void DrawPill(ImDrawListPtr drawList, Vector2 center, string text, Vector4 ink, float scale)
    {
        var size = Typography.Measure(text, TextStyles.SubheadlineEmphasized);
        var half = size * 0.5f + new Vector2(10f, 4f) * scale;
        drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(Pill with { W = Pill.W * ink.W }), half.Y);
        Typography.DrawCentered(drawList, center, text, ink, TextStyles.SubheadlineEmphasized);
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static float Hash(float value)
    {
        var scaled = MathF.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
        return scaled - MathF.Floor(scaled);
    }
}
