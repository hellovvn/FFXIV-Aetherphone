using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Harvest;

internal sealed class HarvestApp : IMiniGame
{
    private const string GameId = "harvest";
    private const string SurfaceId = "##harvest.field";
    private const float BiomeBannerSeconds = 2.4f;
    private const float FeverBannerSeconds = 1.4f;
    private const float JumpSwipe = 22f;
    private const float DuckSwipe = 26f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float PipSize = 9f;
    private const float PipGap = 3f;
    private const float BannerHeightShare = 0.26f;
    private const float SubtitleGap = 8f;
    private const float IdleSkill = 1f;
    private const int PileSize = 3;
    private const int ConfettiCount = 70;
    private const ulong IdleSeed = 0x484152564553UL;
    private static readonly GameGuide Guide = new(
        new GuidePage(L.Harvest.Guide1Title, L.Harvest.Guide1Body, HarvestGuideArt.DrawCatchPage),
        new GuidePage(L.Harvest.Guide2Title, L.Harvest.Guide2Body, HarvestGuideArt.DrawDodgePage),
        new GuidePage(L.Harvest.Guide3Title, L.Harvest.Guide3Body, HarvestGuideArt.DrawFeverPage),
        new GuidePage(L.Harvest.Guide4Title, L.Harvest.Guide4Body, HarvestGuideArt.DrawLandsPage));

    private static readonly GameSpec StageSpec = new(GameId, L.Harvest.Title, GameGenre.Arcade, L.Harvest.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true,
        guide: Guide);

    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Dust = new(0.86f, 0.80f, 0.68f, 0.8f);
    private static readonly Vector4 Feather = new(1f, 0.86f, 0.36f, 1f);
    private static readonly Vector4 Muted = new(0.75f, 0.75f, 0.80f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.86f, 0.36f, 1f), new(1f, 0.62f, 0.30f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
        new(0.56f, 0.86f, 1f, 1f), new(0.46f, 0.94f, 0.58f, 1f), new(0.80f, 0.58f, 1f, 1f),
    };

    private static readonly LocString[] BiomeNames =
    {
        L.Harvest.Meadow, L.Harvest.Highlands, L.Harvest.Mire, L.Harvest.Glacier, L.Harvest.Storm, L.Harvest.Dunes,
    };

    private static readonly LocString[] BiomeTwists =
    {
        L.Harvest.MeadowTwist, L.Harvest.HighlandsTwist, L.Harvest.MireTwist, L.Harvest.GlacierTwist,
        L.Harvest.StormTwist, L.Harvest.DunesTwist,
    };

    private static readonly ParticleSpec LandingDust = new(Dust, Dust with { W = 0f }, 0.9f, 14f, 0.4f, 20f, 2f,
        spread: MathF.PI * 0.8f, direction: -MathF.PI * 0.5f);

    private static readonly ParticleSpec SparkBurst = new(HarvestArt.SparkCore, HarvestArt.SparkGlow, 0.5f, 42f, 0.45f,
        0f, 2.4f, shape: ParticleShape.Streak, additive: true);

    private static readonly ParticleSpec Shimmer = new(new Vector4(1f, 0.95f, 0.85f, 0.7f), new Vector4(1f, 1f, 1f, 0f),
        0.8f, 10f, 0.5f, -6f, 2f, shape: ParticleShape.GlowCircle);

    private static readonly ParticleSpec FeatherBurst = new(Feather, Feather with { W = 0f }, 1f, 30f, 0.8f, 40f, 1.6f, 8f,
        shape: ParticleShape.Shard);

    private readonly HarvestBoard board = new();
    private readonly HarvestBoard idleBoard = new();
    private readonly HarvestRenderer renderer = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly HarvestMaterial[] pile = new HarvestMaterial[PileSize];
    private readonly float[] pileSeeds = new float[PileSize];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot comboLabel;
    private LabelSlot comboUpLabel;
    private int pileCount;
    private bool finished;
    private bool idleReady;
    private bool pointerMode;
    private bool pressing;
    private float pointerTarget;
    private float swipeAnchor;
    private float bannerProgress = 1f;
    private float bannerSeconds = BiomeBannerSeconds;
    private string bannerText = string.Empty;
    private string bannerSubtitle = string.Empty;
    private Vector4 bannerAccent;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        renderer.Reset();
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        finished = false;
        pointerMode = false;
        pressing = false;
        pointerTarget = HarvestRules.Width * 0.5f;
        pileCount = 0;
        ShowBanner(Loc.T(BiomeNames[0]), Loc.T(BiomeTwists[0]), Accent, BiomeBannerSeconds);
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        pressing = false;
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || board.Score <= 0 || !board.Started)
        {
            return;
        }

        finished = true;
        Finish(session);
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver)
        {
            idleBoard.StartGame(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        PlaceCamera(context, scale);
        idleBoard.Tick(context.RawDeltaSeconds, HarvestAutopilot.Think(idleBoard, IdleSkill));
        renderer.Animate(idleBoard, context.RawDeltaSeconds, context.RawDeltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        var full = context.Full;
        drawList.PushClipRect(full.Min, full.Max, true);
        renderer.DrawBackground(drawList, idleBoard, in camera, full, scale);
        renderer.DrawWorld(drawList, idleBoard, in camera, scale, ReadOnlySpan<HarvestMaterial>.Empty,
            ReadOnlySpan<float>.Empty);
        renderer.DrawOverlay(drawList, idleBoard, in camera, full, scale);
        drawList.PopClipRect();
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simulation = fx.ScaleDelta(context.DeltaSeconds);
        PlaceCamera(context, scale);
        var input = context.Session.State == StageFlow.Playing && !finished ? ReadInput(context, scale) : HarvestInput.None;
        if (!finished)
        {
            board.Tick(simulation, in input);
            ReactToEvents(context, scale);
        }

        renderer.Animate(board, raw, simulation);
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, bannerSeconds);
        WarnDanger(context);
        if (board.FeverActive)
        {
            context.Fx.EdgeGlow(0.6f + 0.4f * MathF.Sin(renderer.Time * 8f));
        }

        if (board.GameOver && !finished)
        {
            finished = true;
            context.Fx.SlowMo(0.4f, 0.5f);
            Finish(context.Session);
        }

        var full = context.Full;
        drawList.PushClipRect(full.Min, full.Max, true);
        renderer.DrawBackground(drawList, board, in camera, full, scale);
        renderer.DrawWorld(drawList, board, in camera, scale, pile.AsSpan(0, pileCount), pileSeeds.AsSpan(0, pileCount));
        particles.Draw(drawList, in camera);
        renderer.DrawOverlay(drawList, board, in camera, full, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawBanner(drawList, context, scale);
        drawList.PopClipRect();
        DrawMisses(drawList, context, scale);
        DrawCombo(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Lives(HarvestRules.MaxHits - board.Hits, HarvestRules.MaxHits);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(MissesWidth());
        context.Hud.Custom(ComboWidth(scale));
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var view = new Rect(new Vector2(full.Min.X, context.Safe.Min.Y), full.Max);
        camera.Fit(view, HarvestRules.Width, HarvestRenderer.ViewHeight + HarvestRenderer.GroundDepth, FitMode.Contain);
        var halfHeight = view.Height / (2f * MathF.Max(0.0001f, camera.Zoom));
        camera.Place(new Vector2(HarvestRules.Width * 0.5f, -(halfHeight - HarvestRenderer.GroundDepth)));
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private HarvestInput ReadInput(in GameContext context, float scale)
    {
        var left = GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var jump = GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow) || GameInput.Pressed(ImGuiKey.Space);
        var duck = GameInput.Held(ImGuiKey.S, ImGuiKey.DownArrow);
        if (left || right)
        {
            pointerMode = false;
        }

        PressSurface.Claim(SurfaceId, context.Full, out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated && !context.ChromeHit(mouse))
        {
            pressing = true;
            pointerMode = true;
            swipeAnchor = mouse.Y;
        }

        if (pressing && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressing = false;
        }

        if (pressing)
        {
            pointerTarget = camera.ToWorld(mouse).X;
            if (mouse.Y <= swipeAnchor - JumpSwipe * scale)
            {
                jump = true;
                swipeAnchor = mouse.Y;
            }

            duck |= mouse.Y >= swipeAnchor + DuckSwipe * scale;
        }

        if (!pointerMode)
        {
            return HarvestInput.Keys((right ? 1f : 0f) - (left ? 1f : 0f), jump, duck);
        }

        return HarvestInput.Pointer(pointerTarget, jump, duck);
    }

    private void ReactToEvents(in GameContext context, float scale)
    {
        var events = board.Events;
        for (var index = 0; index < events.Length; index++)
        {
            ref readonly var item = ref events[index];
            var world = HarvestRenderer.World(item.X, item.Y);
            var screen = camera.ToScreen(world);
            switch (item.Kind)
            {
                case HarvestEventKind.Catch:
                    OnCatch(in item, world, screen, context);
                    break;
                case HarvestEventKind.Miss:
                    OnMiss(in item, world, screen, context);
                    break;
                case HarvestEventKind.MirageCaught:
                    UiFeedback.Play(UiSound.GamePop);
                    particles.Emit(Shimmer, world, 10);
                    fx.AddText(Loc.T(L.Harvest.Mirage), screen, Muted, 0.9f);
                    break;
                case HarvestEventKind.Stunned:
                    UiFeedback.Play(UiSound.GameHitSoft);
                    particles.Emit(SparkBurst, world, 14);
                    fx.Shockwave(screen, camera.Px(9f), HarvestArt.SparkGlow, 0.4f, 3f);
                    fx.AddText(Loc.T(L.Harvest.Stunned), screen - new Vector2(0f, 18f * scale), HarvestArt.SparkCore, 1.1f);
                    fx.HitStop(0.05f);
                    camera.Shake(0.3f);
                    context.Fx.Flash(HarvestArt.SparkGlow, 0.2f);
                    break;
                case HarvestEventKind.Fizzle:
                    particles.Emit(item.Detail == (int)HarvestRole.Spark ? SparkBurst : Shimmer, world, 6);
                    break;
                case HarvestEventKind.Hit:
                    OnHit(in item, world, screen, context);
                    break;
                case HarvestEventKind.Dodge:
                    UiFeedback.Play(UiSound.GamePop);
                    fx.AddText(Loc.T(L.Harvest.Dodge), screen - new Vector2(0f, 10f * scale), StageInks.Strong, 0.9f);
                    fx.AddText(GameNumber.Signed(item.Value), screen + new Vector2(0f, 6f * scale),
                        GamePalette.Lighten(Accent, 0.3f), 0.8f);
                    break;
                case HarvestEventKind.Jump:
                    UiFeedback.Play(UiSound.GameJump);
                    particles.Emit(LandingDust, world, 5);
                    break;
                case HarvestEventKind.Land:
                    particles.Emit(LandingDust, world, 4);
                    break;
                case HarvestEventKind.ComboUp:
                    GameSfx.ComboTierUp();
                    fx.AddText(comboUpLabel.Get(L.Stage.Times, item.Value), screen - new Vector2(0f, 26f * scale),
                        HarvestArt.ShardGold, 1.3f);
                    fx.Shockwave(screen, camera.Px(14f), HarvestArt.ShardGold with { W = 0.7f }, 0.45f, 3f);
                    context.Fx.Punch(0.03f);
                    break;
                case HarvestEventKind.FeverStart:
                    OnFever(context);
                    break;
                case HarvestEventKind.FeverEnd:
                    context.Fx.Sweep();
                    break;
                case HarvestEventKind.BiomeStart:
                    OnBiome(in item, context);
                    break;
                case HarvestEventKind.HazardWarned:
                    UiFeedback.Play(UiSound.GameTick);
                    break;
            }
        }
    }

    private void OnCatch(in HarvestEvent item, Vector2 world, Vector2 screen, in GameContext context)
    {
        var color = HarvestArt.MaterialColor(item.Material, item.Detail);
        UiFeedback.Play(item.Material == HarvestMaterial.Shard ? UiSound.GamePowerUp : UiSound.GameCollect);
        particles.Burst(world, 8, color, 34f, 0.9f, 0.45f, 70f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Shard);
        particles.Sparkle(world, 4, GamePalette.Lighten(color, 0.4f), 18f, 0.9f, 0.5f, 10f);
        fx.AddText(GameNumber.Signed(item.Value), screen - new Vector2(0f, 14f * UiScale.Current), GamePalette.Lighten(color, 0.3f),
            0.8f + 0.1f * board.Multiplier);
        if (item.Material == HarvestMaterial.Shard)
        {
            fx.Shockwave(screen, camera.Px(12f), HarvestArt.ShardGold, 0.4f, 3f);
            fx.HitStop(0.03f);
            context.Fx.Punch(0.03f);
        }

        PushPile(item.Material, item.Detail);
    }

    private void OnMiss(in HarvestEvent item, Vector2 world, Vector2 screen, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameWrong);
        particles.Emit(LandingDust, world, 8);
        fx.AddText(Loc.T(L.Harvest.Miss), screen - new Vector2(0f, 16f * UiScale.Current), Danger, 1f);
        fx.Shockwave(screen, camera.Px(7f), Danger with { W = 0.7f }, 0.35f, 2.5f);
        camera.Shake(0.18f);
        context.Fx.Flash(Danger, 0.12f);
    }

    private void OnHit(in HarvestEvent item, Vector2 world, Vector2 screen, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitSoft);
        particles.Emit(FeatherBurst, world, 14);
        fx.AddText(Loc.T(L.Harvest.Ouch), screen - new Vector2(0f, 22f * UiScale.Current), Danger, 1.2f);
        camera.Shake(0.55f);
        fx.HitStop(0.08f);
        context.Fx.Flash(Danger, 0.3f);
        context.Fx.Punch(0.05f);
    }

    private void OnFever(in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        context.Fx.Sweep();
        context.Fx.Flash(HarvestArt.ShardGold, 0.25f);
        particles.Confetti(HarvestRenderer.World(HarvestRules.Width * 0.5f, HarvestRenderer.ViewHeight * 0.75f), ConfettiCount,
            CelebrationPalette, 60f, 1f, 1.4f, 120f);
        ShowBanner(Loc.T(L.Harvest.Fever), string.Empty, HarvestArt.ShardGold, FeverBannerSeconds);
    }

    private void OnBiome(in HarvestEvent item, in GameContext context)
    {
        var biome = Math.Clamp(item.Detail, 0, HarvestRules.BiomeCount - 1);
        GameSfx.LevelClear();
        context.Fx.Sweep();
        ShowBanner(Loc.T(BiomeNames[biome]), Loc.T(BiomeTwists[biome]), Accent, BiomeBannerSeconds);
    }

    private void ShowBanner(string text, string subtitle, Vector4 accent, float seconds)
    {
        bannerText = text;
        bannerSubtitle = subtitle;
        bannerAccent = accent;
        bannerSeconds = seconds;
        bannerProgress = 0f;
    }

    private void DrawBanner(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (bannerProgress >= 1f)
        {
            return;
        }

        var center = new Vector2(context.Full.Center.X, context.Safe.Min.Y + context.Safe.Height * BannerHeightShare);
        GameBanner.Draw(drawList, center, bannerText, bannerAccent, context.Theme, bannerProgress);
        if (bannerSubtitle.Length == 0)
        {
            return;
        }

        var offset = GameBanner.HalfHeight(TextStyles.Title2) + GameBanner.HalfHeight(TextStyles.SubheadlineEmphasized) +
                     SubtitleGap * scale;
        GameBanner.Draw(drawList, center + new Vector2(0f, offset), bannerSubtitle, bannerAccent, context.Theme,
            bannerProgress, TextStyles.SubheadlineEmphasized);
    }

    private void WarnDanger(in GameContext context)
    {
        if (finished)
        {
            return;
        }

        var close = board.Misses == HarvestRules.MaxMisses - 1 || board.Hits == HarvestRules.MaxHits - 1;
        if (close)
        {
            context.Fx.Vignette(Danger, 0.1f + 0.08f * Pulse.Wave(Pulse.Breath), 0.3f);
        }
    }

    private void PushPile(HarvestMaterial material, float seed)
    {
        if (pileCount < PileSize)
        {
            pile[pileCount] = material;
            pileSeeds[pileCount] = seed;
            pileCount++;
            return;
        }

        for (var index = 1; index < PileSize; index++)
        {
            pile[index - 1] = pile[index];
            pileSeeds[index - 1] = pileSeeds[index];
        }

        pile[PileSize - 1] = material;
        pileSeeds[PileSize - 1] = seed;
    }

    private static float MissesWidth() =>
        CapsulePadX * 2f + HarvestRules.MaxMisses * PipSize + (HarvestRules.MaxMisses - 1) * PipGap;

    private float ComboWidth(float scale)
    {
        var text = Typography.Measure(comboLabel.Get(L.Stage.Times, board.Multiplier), CapsuleStyle).X / scale;
        return CapsulePadX * 2f + IconSize + IconGap + text;
    }

    private void DrawMisses(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        var rect = context.Hud.CustomRect(0);
        StageHud.Capsule(drawList, rect, scale);
        var size = PipSize * scale;
        var left = rect.Min.X + CapsulePadX * scale + size * 0.5f;
        for (var pip = 0; pip < HarvestRules.MaxMisses; pip++)
        {
            var center = new Vector2(left + pip * (PipSize + PipGap) * scale, rect.Center.Y);
            var used = pip >= HarvestRules.MaxMisses - board.Misses;
            var half = size * 0.5f;
            var color = used ? StageInks.Muted with { W = 0.4f } : HarvestArt.CrystalColor(pip + 1f);
            drawList.AddQuadFilled(center - new Vector2(0f, half), center + new Vector2(half * 0.7f, 0f),
                center + new Vector2(0f, half), center - new Vector2(half * 0.7f, 0f), ImGui.GetColorU32(color));
            if (!used)
            {
                continue;
            }

            var cross = ImGui.GetColorU32(Danger);
            var arm = half * 0.75f;
            var thickness = MathF.Max(1f, 1.5f * scale);
            drawList.AddLine(center - new Vector2(arm, arm), center + new Vector2(arm, arm), cross, thickness);
            drawList.AddLine(center + new Vector2(-arm, arm), center + new Vector2(arm, -arm), cross, thickness);
        }
    }

    private void DrawCombo(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!context.Hud.CustomPlaced(1))
        {
            return;
        }

        var rect = context.Hud.CustomRect(1);
        StageHud.Capsule(drawList, rect, scale);
        var tier = (board.Multiplier - 1) / (float)(HarvestRules.MaxMultiplier - 1);
        var color = board.FeverActive
            ? Vector4.Lerp(HarvestArt.ShardGold, new Vector4(1f, 1f, 1f, 1f), 0.5f + 0.5f * MathF.Sin(renderer.Time * 10f))
            : Vector4.Lerp(StageInks.Strong, HarvestArt.ShardGold, tier);
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Fire,
            board.FeverActive || board.Multiplier > 1 ? HarvestArt.ShardGold : Accent, iconSize);
        var origin = new Vector2(left + iconSize + IconGap * scale, rect.Center.Y - Typography.LineHeight(CapsuleStyle) * 0.5f);
        Typography.Draw(drawList, origin, comboLabel.Get(L.Stage.Times, board.Multiplier), color, CapsuleStyle);
        StageHud.Bar(drawList, rect, board.Fever, HarvestArt.ShardGold, scale);
    }

    private void Finish(GameSession session)
    {
        session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Harvest.Caught, GameNumber.Label(board.Caught))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.Harvest.Fevers, GameNumber.Label(board.Fevers))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.Time)));
    }
}
