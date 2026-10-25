using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Harvest;

internal sealed class HarvestRenderer
{
    public const float GroundDepth = 14f;
    public const float ViewHeight = 170f;
    public const float TransitionSeconds = 2.2f;
    private const int Columns = 36;
    private const int MoteCount = 28;
    private const int DecorCount = 18;
    private const int TreeCount = 5;
    private const float FarBase = 34f;
    private const float FarAmplitude = 46f;
    private const float NearBase = 6f;
    private const float NearAmplitude = 22f;
    private const float FarParallax = 0.06f;
    private const float NearParallax = 0.14f;
    private const float FogFade = 14f;
    private const float FogAlpha = 0.92f;
    private const float FadeInHeight = 10f;
    private const float CrouchSpeed = 18f;
    private const float AirSpeed = 12f;
    private const float RunRate = 0.32f;
    private const float LightningPeriod = 5.3f;
    private const float LightningWindow = 0.05f;
    private const float TrailWidth = 1.1f;
    private const float FogLiftSpeed = 2f;
    private const int FogLayers = 5;
    private const float FeverFogShare = 0.15f;
    private const float PostcardGroundShare = 0.16f;
    private const float PostcardHeightScale = 0.62f;
    private const int PostcardColumns = 24;
    private static readonly Vector4[] SkyTops =
    {
        new(0.34f, 0.58f, 0.90f, 1f), new(0.40f, 0.52f, 0.72f, 1f), new(0.18f, 0.24f, 0.26f, 1f),
        new(0.26f, 0.42f, 0.68f, 1f), new(0.08f, 0.08f, 0.17f, 1f), new(0.84f, 0.52f, 0.36f, 1f),
    };

    private static readonly Vector4[] SkyBottoms =
    {
        new(0.80f, 0.91f, 0.98f, 1f), new(0.92f, 0.84f, 0.70f, 1f), new(0.44f, 0.52f, 0.48f, 1f),
        new(0.80f, 0.90f, 0.97f, 1f), new(0.24f, 0.20f, 0.36f, 1f), new(0.99f, 0.84f, 0.58f, 1f),
    };

    private static readonly Vector4[] FarColors =
    {
        new(0.48f, 0.70f, 0.62f, 1f), new(0.52f, 0.54f, 0.64f, 1f), new(0.22f, 0.30f, 0.28f, 1f),
        new(0.78f, 0.87f, 0.96f, 1f), new(0.16f, 0.14f, 0.27f, 1f), new(0.88f, 0.62f, 0.42f, 1f),
    };

    private static readonly Vector4[] NearColors =
    {
        new(0.32f, 0.58f, 0.32f, 1f), new(0.42f, 0.46f, 0.36f, 1f), new(0.16f, 0.24f, 0.20f, 1f),
        new(0.60f, 0.74f, 0.90f, 1f), new(0.11f, 0.10f, 0.19f, 1f), new(0.82f, 0.56f, 0.32f, 1f),
    };

    private static readonly Vector4[] GroundTops =
    {
        new(0.44f, 0.68f, 0.30f, 1f), new(0.56f, 0.56f, 0.38f, 1f), new(0.26f, 0.34f, 0.24f, 1f),
        new(0.92f, 0.96f, 1f, 1f), new(0.24f, 0.21f, 0.30f, 1f), new(0.94f, 0.78f, 0.50f, 1f),
    };

    private static readonly Vector4[] GroundBottoms =
    {
        new(0.26f, 0.42f, 0.18f, 1f), new(0.36f, 0.34f, 0.24f, 1f), new(0.13f, 0.18f, 0.13f, 1f),
        new(0.64f, 0.76f, 0.90f, 1f), new(0.11f, 0.09f, 0.15f, 1f), new(0.74f, 0.56f, 0.32f, 1f),
    };

    private static readonly Vector4[] Lips =
    {
        new(0.62f, 0.84f, 0.40f, 1f), new(0.72f, 0.70f, 0.48f, 1f), new(0.38f, 0.48f, 0.34f, 1f),
        new(1f, 1f, 1f, 1f), new(0.40f, 0.34f, 0.52f, 1f), new(1f, 0.88f, 0.62f, 1f),
    };

    private static readonly Vector4[] Motes =
    {
        new(1f, 0.92f, 0.96f, 0.8f), new(1f, 1f, 1f, 0.5f), new(0.75f, 1f, 0.55f, 0.9f),
        new(1f, 1f, 1f, 0.9f), new(0.70f, 0.76f, 0.95f, 0.45f), new(1f, 0.90f, 0.70f, 0.6f),
    };

    private static readonly Vector4[] Suns =
    {
        new(1f, 0.95f, 0.72f, 1f), new(1f, 0.93f, 0.80f, 1f), new(0.86f, 0.92f, 0.84f, 1f),
        new(1f, 1f, 0.94f, 1f), new(0f, 0f, 0f, 0f), new(1f, 0.70f, 0.40f, 1f),
    };

    private static readonly Vector2[] SunSpots =
    {
        new(0.2f, 0.16f), new(0.78f, 0.2f), new(0.72f, 0.24f), new(0.3f, 0.18f), new(0.5f, 0.2f), new(0.62f, 0.5f),
    };

    private static readonly float[] SunSizes = { 0.07f, 0.06f, 0.05f, 0.06f, 0f, 0.12f };
    private static readonly float[] TreeSpots = { 0.08f, 0.27f, 0.49f, 0.71f, 0.9f };
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 TreeTrunk = new(0.30f, 0.22f, 0.14f, 1f);
    private static readonly Vector4 TreeLeaves = new(0.24f, 0.50f, 0.26f, 1f);
    private static readonly Vector4 DeadWood = new(0.10f, 0.14f, 0.12f, 1f);
    private static readonly Vector4 FogColor = new(0.72f, 0.80f, 0.76f, 1f);
    private static readonly Vector4 Bolt = new(0.88f, 0.86f, 1f, 1f);
    private static readonly Vector4 Aurora = new(0.45f, 1f, 0.78f, 1f);
    private static readonly Vector4 FeverGold = new(1f, 0.82f, 0.32f, 1f);
    private static readonly Vector4 MirageTint = new(1f, 0.95f, 0.85f, 1f);

    private readonly Ribbon[] trails = new Ribbon[HarvestBoard.DropCapacity];
    private float time;
    private float runPhase;
    private float crouch;
    private float air;
    private float feverLift;

    public HarvestRenderer()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index] = new Ribbon();
        }
    }

    public float Time => time;

    public void Reset()
    {
        runPhase = 0f;
        crouch = 0f;
        air = 0f;
        feverLift = 0f;
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Release();
        }
    }

    public void Animate(HarvestBoard board, float rawDeltaSeconds, float simulationSeconds)
    {
        time += rawDeltaSeconds;
        if (simulationSeconds <= 0f)
        {
            return;
        }

        ref readonly var player = ref board.Player;
        runPhase += MathF.Abs(player.VelocityX) * simulationSeconds * RunRate;
        crouch = Approach(crouch, player.Ducking ? 1f : 0f, CrouchSpeed * simulationSeconds);
        air = Approach(air, player.Grounded ? 0f : 1f, AirSpeed * simulationSeconds);
        feverLift = Approach(feverLift, board.FeverActive ? 1f : 0f, FogLiftSpeed * simulationSeconds);
        var drops = board.Drops;
        for (var index = 0; index < drops.Length; index++)
        {
            ref readonly var drop = ref drops[index];
            var trail = trails[index];
            if (!drop.Active || drop.Hold > 0f || !(drop.Material == HarvestMaterial.Shard || drop.Role == HarvestRole.Bonus) ||
                drop.Role is HarvestRole.Mirage or HarvestRole.Spark)
            {
                trail.Release();
                continue;
            }

            trail.Claim(drop.Serial);
            trail.PushSpaced(World(drop.X, drop.Y), 1.2f);
        }
    }

    public static void Blend(float seconds, out HarvestBiome current, out HarvestBiome previous, out float blend)
    {
        var visit = HarvestRules.BiomeVisit(seconds);
        current = HarvestRules.BiomeAt(seconds);
        if (visit == 0)
        {
            previous = current;
            blend = 1f;
            return;
        }

        previous = (HarvestBiome)((visit - 1) % HarvestRules.BiomeCount);
        blend = Easing.SmoothStep(Math.Clamp(HarvestRules.BiomeElapsed(seconds) / TransitionSeconds, 0f, 1f));
    }

    public static float Weight(HarvestBiome biome, HarvestBiome current, HarvestBiome previous, float blend) =>
        (current == biome ? blend : 0f) + (previous == biome ? 1f - blend : 0f);

    public static Vector2 World(float x, float height) => new(x, -height);

    public static float PostcardGround(Rect rect) => rect.Max.Y - rect.Height * PostcardGroundShare;

    public static void DrawPostcard(ImDrawListPtr drawList, Rect rect, HarvestBiome biome, float alpha)
    {
        var index = (int)biome;
        var top = ImGui.GetColorU32(SkyTops[index] with { W = alpha });
        var bottom = ImGui.GetColorU32(SkyBottoms[index] with { W = alpha });
        drawList.AddRectFilledMultiColor(rect.Min, rect.Max, top, top, bottom, bottom);
        if (SunSizes[index] > 0f)
        {
            var sun = rect.Min + rect.Size * SunSpots[index];
            var radius = rect.Width * SunSizes[index];
            drawList.AddCircleFilled(sun, radius * 1.6f, ImGui.GetColorU32(Suns[index] with { W = 0.2f * alpha }), 28);
            drawList.AddCircleFilled(sun, radius, ImGui.GetColorU32(Suns[index] with { W = alpha }), 28);
        }

        var ground = PostcardGround(rect);
        var unit = rect.Height / 100f * PostcardHeightScale;
        PostcardLayer(drawList, rect, biome, false, ground, unit, alpha);
        PostcardLayer(drawList, rect, biome, true, ground, unit, alpha);
        var groundTop = ImGui.GetColorU32(GroundTops[index] with { W = alpha });
        var groundBottom = ImGui.GetColorU32(GroundBottoms[index] with { W = alpha });
        drawList.AddRectFilledMultiColor(new Vector2(rect.Min.X, ground), rect.Max, groundTop, groundTop, groundBottom,
            groundBottom);
        drawList.AddLine(new Vector2(rect.Min.X, ground), new Vector2(rect.Max.X, ground),
            ImGui.GetColorU32(Lips[index] with { W = alpha }), MathF.Max(1f, rect.Height * 0.012f));
    }

    private static void PostcardLayer(ImDrawListPtr drawList, Rect rect, HarvestBiome biome, bool near, float ground,
        float unit, float alpha)
    {
        var color = ImGui.GetColorU32((near ? NearColors : FarColors)[(int)biome] with { W = alpha });
        var baseHeight = near ? NearBase : FarBase;
        var amplitude = near ? NearAmplitude : FarAmplitude;
        var previous = new Vector2(rect.Min.X, ground - (baseHeight + amplitude * Profile(biome, near, 0f)) * unit);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var column = 1; column <= PostcardColumns; column++)
        {
            var share = column / (float)PostcardColumns;
            var current = new Vector2(rect.Min.X + rect.Width * share,
                ground - (baseHeight + amplitude * Profile(biome, near, share)) * unit);
            drawList.AddQuadFilled(previous, current, new Vector2(current.X, ground), new Vector2(previous.X, ground), color);
            previous = current;
        }

        drawList.Flags = flags;
    }

    public void DrawBackground(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, Rect full, float scale)
    {
        Blend(board.Time, out var current, out var previous, out var blend);
        var top = Mix(SkyTops, current, previous, blend);
        var bottom = Mix(SkyBottoms, current, previous, blend);
        if (board.FeverActive)
        {
            top = Vector4.Lerp(top, FeverGold, 0.12f);
            bottom = Vector4.Lerp(bottom, FeverGold, 0.22f);
        }

        var topColor = ImGui.GetColorU32(top);
        var bottomColor = ImGui.GetColorU32(bottom);
        drawList.AddRectFilledMultiColor(full.Min, full.Max, topColor, topColor, bottomColor, bottomColor);
        DrawSky(drawList, full, current, previous, blend, board.Wind, scale);
        var parallax = board.Player.X - HarvestRules.Width * 0.5f;
        var left = camera.ToWorld(full.Min).X - 2f;
        var right = camera.ToWorld(full.Max).X + 2f;
        if (blend < 1f)
        {
            DrawLayer(drawList, camera, previous, false, left, right, parallax * FarParallax, 1f);
        }

        DrawLayer(drawList, camera, current, false, left, right, parallax * FarParallax, blend);
        if (blend < 1f)
        {
            DrawLayer(drawList, camera, previous, true, left, right, parallax * NearParallax, 1f);
        }

        DrawLayer(drawList, camera, current, true, left, right, parallax * NearParallax, blend);
        DrawGround(drawList, camera, full, current, previous, blend, left, right);
        DrawMotes(drawList, full, current, previous, blend, board.Wind, scale);
    }

    public void DrawWorld(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, float scale,
        ReadOnlySpan<HarvestMaterial> pile, ReadOnlySpan<float> pileSeeds)
    {
        Blend(board.Time, out var current, out var previous, out var blend);
        var fog = Weight(HarvestBiome.Mire, current, previous, blend) * (1f - (1f - FeverFogShare) * feverLift);
        var alpha = board.Alpha;
        var unit = camera.Px(1f);
        var drops = board.Drops;
        for (var index = 0; index < drops.Length; index++)
        {
            ref readonly var drop = ref drops[index];
            if (drop.Active)
            {
                DrawShadow(drawList, camera, in drop, fog, unit);
            }
        }

        DrawWarnings(drawList, board, camera, unit);
        for (var index = 0; index < drops.Length; index++)
        {
            ref readonly var drop = ref drops[index];
            if (!drop.Active)
            {
                continue;
            }

            if (trails[index].Count > 1)
            {
                var gold = drop.Role == HarvestRole.Bonus ? 0.4f : 0.6f;
                trails[index].Draw(drawList, in camera, HarvestArt.ShardGold with { W = gold }, TrailWidth * unit, true);
            }

            DrawDrop(drawList, camera, in drop, alpha, unit);
        }

        DrawHazards(drawList, board, camera, alpha, unit);
        DrawPlayer(drawList, board, camera, alpha, unit, pile, pileSeeds);
    }

    public void DrawOverlay(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, Rect full, float scale)
    {
        Blend(board.Time, out var current, out var previous, out var blend);
        var fog = Weight(HarvestBiome.Mire, current, previous, blend) * (1f - (1f - FeverFogShare) * feverLift);
        if (fog > 0.01f)
        {
            DrawFog(drawList, camera, full, fog, scale);
        }

        var storm = Weight(HarvestBiome.Storm, current, previous, blend);
        if (storm <= 0.01f)
        {
            return;
        }

        var phase = time % LightningPeriod / LightningPeriod;
        if (phase >= LightningWindow)
        {
            return;
        }

        var flash = (1f - phase / LightningWindow) * storm;
        drawList.AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(Bolt with { W = 0.16f * flash }));
    }

    private void DrawSky(ImDrawListPtr drawList, Rect full, HarvestBiome current, HarvestBiome previous, float blend,
        float wind, float scale)
    {
        for (var biome = 0; biome < HarvestRules.BiomeCount; biome++)
        {
            var weight = Weight((HarvestBiome)biome, current, previous, blend);
            if (weight <= 0.01f)
            {
                continue;
            }

            if (SunSizes[biome] > 0f)
            {
                var center = new Vector2(full.Min.X + full.Width * SunSpots[biome].X, full.Min.Y + full.Height * SunSpots[biome].Y);
                var radius = full.Width * SunSizes[biome];
                for (var layer = 3; layer >= 1; layer--)
                {
                    drawList.AddCircleFilled(center, radius * (1f + layer * 0.5f),
                        ImGui.GetColorU32(Suns[biome] with { W = 0.07f * (4 - layer) * weight }), 36);
                }

                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Suns[biome] with { W = weight }), 36);
            }

            switch ((HarvestBiome)biome)
            {
                case HarvestBiome.Meadow:
                    DrawClouds(drawList, full, weight, 3f, 0.14f, scale);
                    break;
                case HarvestBiome.Highlands:
                    DrawClouds(drawList, full, weight, 3f + wind * 0.6f, 0.2f, scale);
                    break;
                case HarvestBiome.Glacier:
                    DrawAurora(drawList, full, weight);
                    break;
                case HarvestBiome.Storm:
                    DrawBolt(drawList, full, weight, scale);
                    break;
            }
        }
    }

    private void DrawClouds(ImDrawListPtr drawList, Rect full, float weight, float drift, float row, float scale)
    {
        const int Puffs = 5;
        var span = full.Width * 1.5f;
        for (var puff = 0; puff < Puffs; puff++)
        {
            var travel = Wrap(puff * span / Puffs + time * drift * scale, span);
            var x = full.Min.X - full.Width * 0.25f + travel;
            var y = full.Min.Y + full.Height * (row + Hash(puff * 3.3f) * 0.12f);
            var radius = (16f + Hash(puff * 1.7f) * 12f) * scale;
            var color = ImGui.GetColorU32(White with { W = 0.55f * weight });
            drawList.AddCircleFilled(new Vector2(x, y), radius, color, 24);
            drawList.AddCircleFilled(new Vector2(x + radius * 0.85f, y + radius * 0.2f), radius * 0.7f, color, 24);
            drawList.AddCircleFilled(new Vector2(x - radius * 0.8f, y + radius * 0.25f), radius * 0.6f, color, 24);
        }
    }

    private void DrawAurora(ImDrawListPtr drawList, Rect full, float weight)
    {
        const int Bands = 3;
        for (var band = 0; band < Bands; band++)
        {
            var y = full.Min.Y + full.Height * (0.12f + band * 0.06f) + MathF.Sin(time * 0.4f + band) * full.Height * 0.02f;
            var height = full.Height * 0.05f;
            var lit = ImGui.GetColorU32(Aurora with { W = 0.12f * weight });
            var clear = ImGui.GetColorU32(Aurora with { W = 0f });
            drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y - height), new Vector2(full.Max.X, y), clear, clear,
                lit, lit);
            drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y), new Vector2(full.Max.X, y + height), lit, lit,
                clear, clear);
        }
    }

    private void DrawBolt(ImDrawListPtr drawList, Rect full, float weight, float scale)
    {
        var phase = time % LightningPeriod / LightningPeriod;
        if (phase >= LightningWindow * 3f)
        {
            return;
        }

        var strength = (1f - phase / (LightningWindow * 3f)) * weight;
        var cycle = MathF.Floor(time / LightningPeriod);
        var x = full.Min.X + full.Width * (0.2f + Hash(cycle) * 0.6f);
        var y = full.Min.Y + full.Height * 0.12f;
        var color = ImGui.GetColorU32(Bolt with { W = 0.85f * strength });
        for (var segment = 0; segment < 5; segment++)
        {
            var nextX = x + (Hash(cycle + segment * 2.1f) - 0.5f) * 26f * scale;
            var nextY = y + full.Height * 0.06f;
            drawList.AddLine(new Vector2(x, y), new Vector2(nextX, nextY), color, MathF.Max(1f, 2f * scale));
            x = nextX;
            y = nextY;
        }
    }

    private static void DrawLayer(ImDrawListPtr drawList, in Camera2D camera, HarvestBiome biome, bool near, float left,
        float right, float shift, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var color = ImGui.GetColorU32((near ? NearColors : FarColors)[(int)biome] with { W = alpha });
        var baseHeight = near ? NearBase : FarBase;
        var amplitude = near ? NearAmplitude : FarAmplitude;
        var step = (right - left) / Columns;
        var bottom = camera.ToScreen(World(0f, 0f)).Y + 1f;
        var previousX = left;
        var previousHeight = baseHeight + amplitude * Profile(biome, near, (left + shift) / HarvestRules.Width);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var column = 1; column <= Columns; column++)
        {
            var x = left + column * step;
            var height = baseHeight + amplitude * Profile(biome, near, (x + shift) / HarvestRules.Width);
            var from = camera.ToScreen(World(previousX, previousHeight));
            var to = camera.ToScreen(World(x, height));
            drawList.AddQuadFilled(from, to, new Vector2(to.X, bottom), new Vector2(from.X, bottom), color);
            previousX = x;
            previousHeight = height;
        }

        drawList.Flags = flags;

        if (!near)
        {
            return;
        }

        if (biome == HarvestBiome.Meadow)
        {
            DrawTrees(drawList, camera, shift, alpha, false);
        }
        else if (biome == HarvestBiome.Mire)
        {
            DrawTrees(drawList, camera, shift, alpha, true);
        }
    }

    private static void DrawTrees(ImDrawListPtr drawList, in Camera2D camera, float shift, float alpha, bool dead)
    {
        var unit = camera.Px(1f);
        var biome = dead ? HarvestBiome.Mire : HarvestBiome.Meadow;
        for (var tree = 0; tree < TreeCount; tree++)
        {
            var x = TreeSpots[tree] * HarvestRules.Width - shift;
            var groundHeight = NearBase + NearAmplitude * Profile(biome, true, TreeSpots[tree]) - 1f;
            var height = 10f + Hash(tree * 4.1f) * 8f;
            var root = camera.ToScreen(World(x, groundHeight));
            var crown = camera.ToScreen(World(x, groundHeight + height));
            if (dead)
            {
                var wood = ImGui.GetColorU32(DeadWood with { W = alpha });
                drawList.AddLine(root, crown, wood, 1.6f * unit);
                drawList.AddLine(Vector2.Lerp(root, crown, 0.55f), Vector2.Lerp(root, crown, 0.55f) + new Vector2(4f, -3f) * unit,
                    wood, 0.8f * unit);
                drawList.AddLine(Vector2.Lerp(root, crown, 0.75f), Vector2.Lerp(root, crown, 0.75f) + new Vector2(-3.5f, -2.5f) * unit,
                    wood, 0.7f * unit);
                continue;
            }

            drawList.AddLine(root, Vector2.Lerp(root, crown, 0.6f), ImGui.GetColorU32(TreeTrunk with { W = alpha }), 1.6f * unit);
            drawList.AddCircleFilled(crown + new Vector2(0f, height * 0.25f * unit), height * 0.42f * unit,
                ImGui.GetColorU32(TreeLeaves with { W = alpha }), 20);
        }
    }

    private static float Profile(HarvestBiome biome, bool near, float u)
    {
        switch (biome)
        {
            case HarvestBiome.Meadow:
                return near
                    ? 0.35f + 0.25f * MathF.Sin(u * MathF.Tau * 1.6f + 0.7f) + 0.1f * MathF.Sin(u * MathF.Tau * 4.1f)
                    : 0.45f + 0.3f * MathF.Sin(u * MathF.Tau * 1.2f) + 0.15f * MathF.Sin(u * MathF.Tau * 3.1f + 1f);
            case HarvestBiome.Highlands:
                return near
                    ? 0.3f + 0.25f * Triangle(u * 2.3f + 0.2f) + 0.08f * MathF.Sin(u * 31f)
                    : 0.25f + 0.7f * Triangle(u * 2.6f + 0.3f * MathF.Sin(u * 7f)) + 0.04f * MathF.Sin(u * 53f);
            case HarvestBiome.Mire:
                return near
                    ? 0.12f + 0.05f * MathF.Sin(u * 17f)
                    : 0.25f + 0.08f * MathF.Sin(u * 9f) + 0.05f * MathF.Sin(u * 23f);
            case HarvestBiome.Glacier:
                return near
                    ? 0.25f + 0.2f * MathF.Sin(u * MathF.Tau * 1.3f + 2f)
                    : 0.15f + 0.85f * MathF.Pow(Triangle(u * 3.1f + 0.15f), 1.6f);
            case HarvestBiome.Storm:
                return near
                    ? 0.2f + 0.35f * Triangle(u * 4.2f) * Triangle(u * 1.3f + 0.4f)
                    : 0.1f + 0.9f * MathF.Pow(Triangle(u * 4.6f + 0.1f), 3f);
            default:
                return near
                    ? 0.3f + 0.3f * MathF.Sin(u * MathF.Tau * 1.1f + 1.3f) + 0.06f * MathF.Sin(u * MathF.Tau * 3f)
                    : Math.Clamp(0.2f + 0.9f * MathF.Sin(u * MathF.Tau * 0.9f + 0.6f), 0.15f, 0.55f);
        }
    }

    private void DrawGround(ImDrawListPtr drawList, in Camera2D camera, Rect full, HarvestBiome current,
        HarvestBiome previous, float blend, float left, float right)
    {
        var lipY = camera.ToScreen(World(0f, 0f)).Y;
        var top = ImGui.GetColorU32(Mix(GroundTops, current, previous, blend));
        var bottom = ImGui.GetColorU32(Mix(GroundBottoms, current, previous, blend));
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, lipY), full.Max, top, top, bottom, bottom);
        var unit = camera.Px(1f);
        drawList.AddRectFilled(new Vector2(full.Min.X, lipY - 0.4f * unit), new Vector2(full.Max.X, lipY + 0.9f * unit),
            ImGui.GetColorU32(Mix(Lips, current, previous, blend)));
        for (var biome = 0; biome < HarvestRules.BiomeCount; biome++)
        {
            var weight = Weight((HarvestBiome)biome, current, previous, blend);
            if (weight > 0.01f)
            {
                DrawDecor(drawList, camera, (HarvestBiome)biome, weight, left, right, unit);
            }
        }
    }

    private static void DrawDecor(ImDrawListPtr drawList, in Camera2D camera, HarvestBiome biome, float weight,
        float left, float right, float unit)
    {
        var span = right - left;
        var lip = Lips[(int)biome];
        var dark = GroundBottoms[(int)biome];
        for (var index = 0; index < DecorCount; index++)
        {
            var x = left + Hash(index * 7.3f + (int)biome) * span;
            var depth = 1.5f + Hash(index * 2.9f) * (GroundDepth - 4f);
            var at = camera.ToScreen(World(x, -depth));
            var size = (0.6f + Hash(index * 5.1f)) * unit;
            switch (biome)
            {
                case HarvestBiome.Meadow:
                    drawList.AddTriangleFilled(at + new Vector2(-size, 0f), at + new Vector2(size * 0.2f, -size * 2.4f),
                        at + new Vector2(size, 0f), ImGui.GetColorU32(lip with { W = 0.8f * weight }));
                    break;
                case HarvestBiome.Highlands:
                case HarvestBiome.Storm:
                    Shapes.FillEllipse(drawList, at, size * 1.3f, size * 0.7f, ImGui.GetColorU32(dark with { W = 0.8f * weight }), 10);
                    break;
                case HarvestBiome.Mire:
                    drawList.AddLine(at, at + new Vector2(size * 0.4f, -size * 3f), ImGui.GetColorU32(lip with { W = 0.7f * weight }),
                        MathF.Max(1f, 0.3f * unit));
                    break;
                case HarvestBiome.Glacier:
                    drawList.AddLine(at - new Vector2(size * 1.6f, 0f), at + new Vector2(size * 1.6f, 0f),
                        ImGui.GetColorU32(White with { W = 0.75f * weight }), MathF.Max(1f, 0.3f * unit));
                    break;
                default:
                    drawList.AddBezierQuadratic(at - new Vector2(size * 2f, 0f), at - new Vector2(0f, size * 0.8f),
                        at + new Vector2(size * 2f, 0f), ImGui.GetColorU32(dark with { W = 0.6f * weight }), MathF.Max(1f, 0.3f * unit), 6);
                    break;
            }
        }
    }

    private void DrawMotes(ImDrawListPtr drawList, Rect full, HarvestBiome current, HarvestBiome previous, float blend,
        float wind, float scale)
    {
        for (var biome = 0; biome < HarvestRules.BiomeCount; biome++)
        {
            var weight = Weight((HarvestBiome)biome, current, previous, blend);
            if (weight <= 0.01f)
            {
                continue;
            }

            var color = Motes[biome];
            for (var index = 0; index < MoteCount; index++)
            {
                var seedX = Hash(index * 1.31f + biome);
                var seedY = Hash(index * 2.71f + biome * 3f);
                var speed = 0.6f + Hash(index * 3.7f);
                var x = full.Min.X + seedX * full.Width;
                var y = full.Min.Y + seedY * full.Height;
                var tint = ImGui.GetColorU32(color with { W = color.W * weight });
                switch ((HarvestBiome)biome)
                {
                    case HarvestBiome.Meadow:
                        x = full.Min.X + Wrap(seedX * full.Width + time * 9f * speed * scale, full.Width);
                        y = full.Min.Y + Wrap(seedY * full.Height + MathF.Sin(time * speed + index) * 12f * scale, full.Height);
                        drawList.AddCircleFilled(new Vector2(x, y), 1.4f * scale, tint, 8);
                        break;
                    case HarvestBiome.Highlands:
                    {
                        var push = MathF.Abs(wind) / 30f;
                        x = full.Min.X + Wrap(seedX * full.Width + time * (40f + 90f * push) * speed * scale * MathF.Sign(wind + 0.01f),
                            full.Width);
                        var length = (10f + 22f * push) * scale * MathF.Sign(wind + 0.01f);
                        drawList.AddLine(new Vector2(x, y), new Vector2(x - length, y), tint, MathF.Max(1f, scale));
                        break;
                    }
                    case HarvestBiome.Mire:
                    {
                        var glow = 0.5f + 0.5f * MathF.Sin(time * 2.3f * speed + index * 1.7f);
                        y = full.Min.Y + full.Height * (0.5f + seedY * 0.45f) + MathF.Sin(time * 0.7f + index) * 6f * scale;
                        drawList.AddCircleFilled(new Vector2(x, y), 3f * scale, ImGui.GetColorU32(color with { W = 0.15f * glow * weight }), 10);
                        drawList.AddCircleFilled(new Vector2(x, y), 1.2f * scale, ImGui.GetColorU32(color with { W = glow * weight }), 8);
                        break;
                    }
                    case HarvestBiome.Glacier:
                        y = full.Min.Y + Wrap(seedY * full.Height + time * 26f * speed * scale, full.Height);
                        x += MathF.Sin(time * speed + index) * 8f * scale;
                        drawList.AddCircleFilled(new Vector2(x, y), (1f + speed) * scale, tint, 8);
                        break;
                    case HarvestBiome.Storm:
                    {
                        y = full.Min.Y + Wrap(seedY * full.Height + time * 260f * speed * scale, full.Height);
                        var slant = new Vector2(-4f, 14f) * scale;
                        drawList.AddLine(new Vector2(x, y), new Vector2(x, y) + slant, tint, MathF.Max(1f, scale));
                        break;
                    }
                    default:
                    {
                        x = full.Min.X + Wrap(seedX * full.Width + time * 14f * speed * scale, full.Width);
                        y = full.Min.Y + full.Height * (0.45f + seedY * 0.5f);
                        drawList.AddCircleFilled(new Vector2(x, y), 1f * scale, tint, 6);
                        if (index % 4 == 0)
                        {
                            var waveY = full.Min.Y + full.Height * (0.55f + seedX * 0.35f);
                            var shimmer = ImGui.GetColorU32(White with { W = 0.08f * weight });
                            drawList.AddBezierCubic(new Vector2(full.Min.X, waveY),
                                new Vector2(full.Min.X + full.Width * 0.33f, waveY - 4f * scale * MathF.Sin(time * 3f + index)),
                                new Vector2(full.Min.X + full.Width * 0.66f, waveY + 4f * scale * MathF.Sin(time * 3f + index)),
                                new Vector2(full.Max.X, waveY), shimmer, MathF.Max(1f, scale), 16);
                        }

                        break;
                    }
                }
            }
        }
    }

    private static void DrawShadow(ImDrawListPtr drawList, in Camera2D camera, in HarvestDrop drop, float fog, float unit)
    {
        if (drop.Role == HarvestRole.Mirage)
        {
            return;
        }

        var share = 1f - Math.Clamp(drop.Y / HarvestRules.SpawnHeight, 0f, 1f);
        var hidden = drop.Y > HarvestRules.FogLine ? fog : 0f;
        var visible = 1f - hidden;
        if (visible <= 0.01f)
        {
            return;
        }

        var ground = camera.ToScreen(World(drop.LandX, 0f));
        if (drop.Role == HarvestRole.Spark)
        {
            var column = camera.ToScreen(World(drop.LandX, drop.Y));
            var dash = 3f * unit;
            var violet = ImGui.GetColorU32(HarvestArt.SparkGlow with { W = 0.35f * visible });
            for (var y = column.Y; y < ground.Y; y += dash * 2f)
            {
                drawList.AddLine(new Vector2(ground.X, y), new Vector2(ground.X, MathF.Min(ground.Y, y + dash)), violet,
                    MathF.Max(1f, 0.4f * unit));
            }

            Shapes.FillEllipse(drawList, ground, (2f + 2f * share) * unit, (0.6f + 0.5f * share) * unit,
                ImGui.GetColorU32(HarvestArt.SparkGlow with { W = (0.2f + 0.4f * share) * visible }), 16);
            return;
        }

        Shapes.FillEllipse(drawList, ground, (2.2f + 2.4f * share) * unit, (0.6f + 0.6f * share) * unit,
            ImGui.GetColorU32(Shadow with { W = (0.12f + 0.38f * share) * visible }), 16);
    }

    private void DrawDrop(ImDrawListPtr drawList, in Camera2D camera, in HarvestDrop drop, float alpha, float unit)
    {
        var x = Easing.Lerp(drop.PreviousX, drop.X, alpha);
        var y = Easing.Lerp(drop.PreviousY, drop.Y, alpha);
        var fade = Math.Clamp((HarvestRules.SpawnHeight - y) / FadeInHeight, 0f, 1f);
        if (drop.Role == HarvestRole.Spark)
        {
            var charge = drop.Hold > 0f ? 0.5f + 0.5f * (1f - drop.Hold / HarvestSpawner.SparkHoldSeconds) : 1f;
            HarvestArt.DrawSpark(drawList, camera.ToScreen(World(x, y)), unit, time + drop.Seed, 1f, charge);
            return;
        }

        if (drop.Role == HarvestRole.Mirage)
        {
            var wobble = MathF.Sin(time * 9f + drop.Seed) * 0.7f;
            var shimmer = 0.5f + 0.2f * MathF.Sin(time * 6f + drop.Seed * 2f);
            var center = camera.ToScreen(World(x + wobble, y));
            drawList.AddCircleFilled(center, HarvestBoard.DropRadius * 1.6f * unit,
                ImGui.GetColorU32(MirageTint with { W = 0.12f * fade }), 20);
            HarvestArt.DrawMaterial(drawList, center, unit, drop.Material, drop.Seed, time, shimmer * fade);
            return;
        }

        var screen = camera.ToScreen(World(x, y));
        if (drop.Role == HarvestRole.Bonus)
        {
            drawList.AddCircleFilled(screen, HarvestBoard.DropRadius * 1.9f * unit,
                ImGui.GetColorU32(FeverGold with { W = 0.22f * fade }), 20);
        }

        HarvestArt.DrawMaterial(drawList, screen, unit, drop.Material, drop.Seed, time, fade);
    }

    private void DrawWarnings(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, float unit)
    {
        var hazards = board.Hazards;
        for (var index = 0; index < hazards.Length; index++)
        {
            ref readonly var hazard = ref hazards[index];
            if (!hazard.Active || hazard.Warning <= 0f)
            {
                continue;
            }

            var fromLeft = hazard.Velocity > 0f;
            var edgeX = fromLeft ? 5f : HarvestRules.Width - 5f;
            var height = hazard.Kind == HarvestHazardKind.Bird ? hazard.Height : HarvestBoard.BoulderRadius + 5f;
            var remaining = hazard.Warning / hazard.WarningTotal;
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 14f);
            if (hazard.Kind == HarvestHazardKind.Bird)
            {
                var from = camera.ToScreen(World(0f, hazard.Height));
                var to = camera.ToScreen(World(HarvestRules.Width, hazard.Height));
                var dash = 4f * unit;
                var line = ImGui.GetColorU32(HarvestArt.BirdWarning with { W = 0.3f + 0.2f * pulse });
                for (var x = from.X; x < to.X; x += dash * 2f)
                {
                    drawList.AddLine(new Vector2(x, from.Y), new Vector2(MathF.Min(to.X, x + dash), from.Y), line,
                        MathF.Max(1f, 0.45f * unit));
                }
            }
            else
            {
                var dust = camera.ToScreen(World(fromLeft ? 1f : HarvestRules.Width - 1f, 1f));
                drawList.AddCircleFilled(dust, (2f + pulse * 1.5f) * unit,
                    ImGui.GetColorU32(HarvestArt.BoulderWarning with { W = 0.25f }), 16);
            }

            HarvestArt.DrawWarning(drawList, camera.ToScreen(World(edgeX, height)), 3.4f * unit, hazard.Kind, remaining,
                pulse);
        }
    }

    private void DrawHazards(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, float alpha, float unit)
    {
        var hazards = board.Hazards;
        for (var index = 0; index < hazards.Length; index++)
        {
            ref readonly var hazard = ref hazards[index];
            if (!hazard.Active || hazard.Warning > 0f)
            {
                continue;
            }

            var x = Easing.Lerp(hazard.PreviousX, hazard.X, alpha);
            var direction = hazard.Velocity > 0f ? 1 : -1;
            if (hazard.Kind == HarvestHazardKind.Bird)
            {
                var swoop = MathF.Sin(time * 3f + index) * 0.6f;
                HarvestArt.DrawBird(drawList, camera.ToScreen(World(x, hazard.Height + swoop)), unit, direction,
                    time * 16f + index, 1f);
                continue;
            }

            var ground = camera.ToScreen(World(x, 0f));
            Shapes.FillEllipse(drawList, ground, 3.6f * unit, 0.8f * unit, ImGui.GetColorU32(Shadow with { W = 0.3f }), 16);
            HarvestArt.DrawBoulder(drawList, camera.ToScreen(World(x, hazard.Height)), unit, hazard.Spin, 1f);
        }
    }

    private void DrawPlayer(ImDrawListPtr drawList, HarvestBoard board, in Camera2D camera, float alpha, float unit,
        ReadOnlySpan<HarvestMaterial> pile, ReadOnlySpan<float> pileSeeds)
    {
        ref readonly var player = ref board.Player;
        var x = Easing.Lerp(player.PreviousX, player.X, alpha);
        var y = Easing.Lerp(player.PreviousY, player.Y, alpha);
        var ground = camera.ToScreen(World(x, 0f));
        var shrink = 1f / (1f + y * 0.06f);
        Shapes.FillEllipse(drawList, ground, 6.5f * shrink * unit, 1.3f * shrink * unit,
            ImGui.GetColorU32(Shadow with { W = 0.3f * shrink }), 20);
        var blink = player.Invulnerable > 0f && MathF.Sin(time * 40f) < 0f ? 0.35f : 1f;
        var pose = new HarvestPose
        {
            Feet = camera.ToScreen(World(x, y)),
            Unit = unit,
            Facing = player.Facing,
            RunPhase = runPhase,
            Crouch = crouch,
            Air = air,
            Stun = player.Stun,
            Alpha = blink,
            Time = time,
            Lean = player.VelocityX / HarvestPlayer.MaxSpeed,
        };
        HarvestArt.DrawChocobo(drawList, in pose, pile, pileSeeds);
    }

    private void DrawFog(ImDrawListPtr drawList, in Camera2D camera, Rect full, float fog, float scale)
    {
        var line = camera.ToScreen(World(0f, HarvestRules.FogLine)).Y;
        var solid = camera.ToScreen(World(0f, HarvestRules.FogLine + FogFade)).Y;
        var dense = ImGui.GetColorU32(FogColor with { W = FogAlpha * fog });
        var clear = ImGui.GetColorU32(FogColor with { W = 0f });
        drawList.AddRectFilled(full.Min, new Vector2(full.Max.X, solid), dense);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, solid), new Vector2(full.Max.X, line), dense, dense, clear,
            clear);
        const int Puffs = 7;
        var span = full.Width * 1.4f;
        for (var puff = 0; puff < Puffs; puff++)
        {
            var travel = Wrap(puff * span / Puffs + time * (6f + puff) * scale, span);
            var center = new Vector2(full.Min.X - full.Width * 0.2f + travel,
                line + MathF.Sin(time * 0.8f + puff) * 6f * scale);
            var radius = (22f + Hash(puff) * 14f) * scale;
            for (var layer = FogLayers; layer >= 1; layer--)
            {
                drawList.AddCircleFilled(center, radius * layer / FogLayers,
                    ImGui.GetColorU32(FogColor with { W = 0.07f * fog }), 24);
            }
        }
    }

    private static Vector4 Mix(Vector4[] table, HarvestBiome current, HarvestBiome previous, float blend) =>
        Vector4.Lerp(table[(int)previous], table[(int)current], blend);

    private static float Triangle(float value)
    {
        var fraction = value - MathF.Floor(value);
        return 1f - MathF.Abs(fraction * 2f - 1f);
    }

    private static float Approach(float value, float target, float step) =>
        value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);

    private static float Wrap(float value, float size) => size <= 0f ? 0f : value - MathF.Floor(value / size) * size;

    private static float Hash(float value)
    {
        var scaled = MathF.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
        return scaled - MathF.Floor(scaled);
    }
}
