using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Cluster;

internal sealed class ClusterApp : IMiniGame
{
    private const string GameId = "cluster";
    private const string BoardSurfaceId = "##clusterBoard";
    private const float BarBand = 26f;
    private const float DockBand = 60f;
    private const float UrgentBar = 0.25f;
    private const float TickBar = 0.15f;
    private const float TickSeconds = 0.5f;
    private const float BannerSeconds = 1.5f;
    private const float EndSeconds = 1.3f;
    private const float EndFadeSeconds = 0.6f;
    private const float GainFollow = 6f;
    private const float GainFade = 1.4f;
    private const float LeftoverFade = 0.5f;
    private const float HoverLift = 2.5f;
    private const float FlashDecay = 2f;
    private const int BurstPerPiece = 5;
    private const int BannerLow = 1;
    private const int BannerMid = 2;
    private const int BannerHigh = 3;
    private const int BannerTop = 4;
    private static readonly GameGuide Guide = new(
        new GuidePage(L.Cluster.Guide1Title, L.Cluster.Guide1Body, ClusterGuide.DrawTapPage),
        new GuidePage(L.Cluster.Guide2Title, L.Cluster.Guide2Body, ClusterGuide.DrawClosePage),
        new GuidePage(L.Cluster.Guide3Title, L.Cluster.Guide3Body, ClusterGuide.DrawBarPage),
        new GuidePage(L.Cluster.Guide4Title, L.Cluster.Guide4Body, ClusterGuide.DrawShufflePage));
    private static readonly GameSpec StageSpec = new(GameId, L.Cluster.Title, GameGenre.Puzzle, L.Cluster.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true,
        guide: Guide);
    private static readonly Vector4 Danger = new(0.97f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.86f, 0.42f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HotTint = new(1f, 0.45f, 0.5f, 1f);

    private readonly ClusterRun run = new();
    private readonly ClusterMotions motions = new();
    private readonly ClusterPops pops = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly int[] group = new int[ClusterBoard.MaxCells];
    private readonly bool[] hoverMask = new bool[ClusterBoard.MaxCells];
    private readonly LabelSlot[] multiplierLabels = new LabelSlot[ComboMeter.MaxMultiplier + 1];
    private GameGrid grid;
    private Rect plate;
    private Rect bar;
    private Rect dock;
    private LabelSlot levelBanner;
    private string? bannerText;
    private Vector4 bannerColor;
    private float bannerProgress = 1f;
    private int bannerPriority;
    private int hoverIndex = -1;
    private int hoverFocus = -1;
    private int hoverVersion = -1;
    private int hoverSize;
    private int cursorColumn;
    private int cursorRow;
    private bool cursorShown;
    private bool buttonHovered;
    private float shownBar;
    private float gainFrom;
    private float gainAlpha;
    private float leftoverAge;
    private float tickTimer;
    private float endTimer;
    private float buttonFlash;
    private int lastColors;
    private int lastCells;
    private ulong idleSeed;
    private bool idleSynced;
    private bool ending;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        run.Reset(start.Random);
        idleSeed = start.Seed;
        idleSynced = true;
        motions.Reset();
        motions.DropIn(run.Board);
        pops.Clear();
        particles.Clear();
        fx.Clear();
        bannerText = null;
        bannerProgress = 1f;
        bannerPriority = 0;
        hoverIndex = -1;
        hoverFocus = -1;
        hoverVersion = -1;
        hoverSize = 0;
        cursorColumn = run.Board.Columns / 2;
        cursorRow = run.Board.Rows - 1;
        cursorShown = false;
        buttonHovered = false;
        shownBar = run.Bar;
        gainFrom = run.Bar;
        gainAlpha = 0f;
        leftoverAge = 0f;
        tickTimer = 0f;
        endTimer = 0f;
        buttonFlash = 0f;
        lastColors = run.Board.Colors;
        lastCells = run.Board.CellCount;
        ending = false;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        pops.Clear();
        idleSynced = false;
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || run.Score <= 0)
        {
            return;
        }

        Finish(session);
    }

    public void DrawIdle(in GameContext context)
    {
        SyncIdle(context.Session.Seed);
        motions.Advance(context.RawDeltaSeconds);
        var scale = UiScale.Current;
        Layout(context.Safe, scale);
        DrawBoard(ImGui.GetWindowDrawList(), context, scale, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var session = context.Session;
        var raw = context.RawDeltaSeconds;
        var simulation = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(raw);
        fx.Update(raw);
        motions.Advance(raw);
        pops.Update(raw, particles, BurstPerPiece);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        buttonFlash = MathF.Max(0f, buttonFlash - raw * FlashDecay);
        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        Layout(area, scale);
        var playing = session.State == StageFlow.Playing;
        if (!finished)
        {
            if (ending)
            {
                endTimer += playing ? context.DeltaSeconds : 0f;
                if (endTimer >= EndSeconds)
                {
                    Finish(session);
                }
            }
            else
            {
                run.Step(simulation);
                HandleEvents(run.TakeEvents(), context);
                if (!ending && playing)
                {
                    HandleInput(context, scale);
                    WarnLowBar(context, simulation);
                }
            }
        }

        if (run.Pending == ClusterPending.NextBoard)
        {
            leftoverAge += raw;
        }

        TrackBar(raw);
        DrawStage(drawList, context, scale, playing && !ending && !finished);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (bannerText is not null)
        {
            GameBanner.Draw(drawList, grid.Center, bannerText, bannerColor, context.Theme, bannerProgress,
                TextStyles.Title1);
        }

        context.Hud.Score(run.Score);
        context.Hud.Level(run.Level);
        context.Hud.Combo(run.Combo);
        context.Hud.Best(session.Best);
        session.Report(run.Score);
    }

    private void SyncIdle(ulong seed)
    {
        if (idleSynced && idleSeed == seed)
        {
            return;
        }

        run.Reset(GameRandom.FromSeed(seed));
        motions.Reset();
        motions.DropIn(run.Board);
        shownBar = run.Bar;
        gainAlpha = 0f;
        hoverSize = 0;
        idleSeed = seed;
        idleSynced = true;
    }

    private void Layout(Rect area, float scale)
    {
        var board = run.Board;
        var inset = BoardPlate.Padding * scale;
        var top = BarBand * scale;
        var bottom = DockBand * scale;
        var gridArea = new Rect(new Vector2(area.Min.X + inset, area.Min.Y + inset + top),
            new Vector2(area.Max.X - inset, area.Max.Y - inset - bottom));
        grid = GameGrid.Centered(gridArea, board.Columns, board.Rows, ClusterRenderer.GapFraction);
        var bounds = grid.Bounds;
        var barHeight = ClusterRenderer.BarHeight * scale;
        var barY = bounds.Min.Y - top * 0.5f;
        bar = new Rect(new Vector2(bounds.Min.X + grid.Gap, barY - barHeight * 0.5f),
            new Vector2(bounds.Max.X - grid.Gap, barY + barHeight * 0.5f));
        dock = new Rect(new Vector2(bounds.Min.X, bounds.Max.Y + inset * 0.5f),
            new Vector2(bounds.Max.X, bounds.Max.Y + bottom));
        plate = new Rect(new Vector2(bounds.Min.X - inset, bounds.Min.Y - inset - top),
            new Vector2(bounds.Max.X + inset, bounds.Max.Y + inset + bottom));
    }

    private void HandleEvents(ClusterEvents events, in GameContext context)
    {
        if (events.HasFlag(ClusterEvents.Reshuffled))
        {
            motions.Fly(run.Board);
            ShowBanner(Loc.T(L.Cluster.NoGroups), Accent, BannerMid);
            UiFeedback.Play(UiSound.GameShuffle);
            fx.AddTrauma(0.08f);
            particles.Sparkle(grid.Center, 14, GamePalette.Lighten(Accent, 0.45f), grid.Width * 0.9f, 2.6f, 0.6f);
        }

        if (events.HasFlag(ClusterEvents.BoardStarted))
        {
            OnBoardStarted();
        }

        if (events.HasFlag(ClusterEvents.Ended))
        {
            BeginEnding(context);
        }
    }

    private void OnBoardStarted()
    {
        var board = run.Board;
        motions.DropIn(board);
        leftoverAge = 0f;
        cursorColumn = Math.Clamp(cursorColumn, 0, board.Columns - 1);
        cursorRow = Math.Clamp(cursorRow, 0, board.Rows - 1);
        hoverVersion = -1;
        if (board.Colors > lastColors)
        {
            ShowBanner(Loc.T(L.Cluster.NewColor), ClusterRenderer.ColorOf(board.Colors), BannerHigh);
        }
        else if (board.CellCount > lastCells)
        {
            ShowBanner(Loc.T(L.Cluster.BiggerBoard), Accent, BannerHigh);
        }
        else
        {
            ShowBanner(levelBanner.Get(L.Stage.LevelNumber, run.Level), Accent, BannerHigh);
        }

        lastColors = board.Colors;
        lastCells = board.CellCount;
        UiFeedback.Play(UiSound.GameCardPlace);
    }

    private void BeginEnding(in GameContext context)
    {
        ending = true;
        endTimer = 0f;
        hoverSize = 0;
        ShowBanner(Loc.T(L.Cluster.OutOfTime), Danger, BannerTop);
        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.Flash(Danger, 0.35f);
        fx.AddTrauma(0.45f);
    }

    private void Finish(GameSession session)
    {
        finished = true;
        ending = false;
        session.Finish(new GameOutcome(run.Score, ScoreKind.Score, GameId)
            .WithStat(L.Cluster.Boards, GameNumber.Label(run.Boards))
            .WithStat(L.Cluster.BestGroup, GameNumber.Label(run.BestGroup))
            .WithStat(L.Games.Combo, GameNumber.Label(run.BestCombo))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)run.PlaySeconds)));
    }

    private void HandleInput(in GameContext context, float scale)
    {
        var board = run.Board;
        var accepts = run.AcceptsInput;
        if (MoveCursor(board))
        {
            cursorShown = true;
        }

        if (GameInput.Pressed(ImGuiKey.E) && accepts)
        {
            Shuffle(context, scale);
        }

        if (cursorShown && GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter) && accepts)
        {
            Clear(board.Index(cursorColumn, cursorRow), context, scale);
        }

        var button = ClusterRenderer.ButtonRect(dock, scale);
        buttonHovered = UiInteract.Hover(button.Min, button.Max);
        if (buttonHovered && run.ShuffleReady)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(button.Min, button.Max, buttonHovered, false) && accepts)
        {
            Shuffle(context, scale);
        }

        var hovered = PressSurface.Claim(BoardSurfaceId, grid.Bounds, out var pressed);
        var mouse = ImGui.GetMousePos();
        if (!hovered || context.ChromeHit(mouse))
        {
            hoverIndex = -1;
            return;
        }

        var cell = CellAt(mouse);
        if (ImGui.GetIO().MouseDelta.LengthSquared() > 0f && cell >= 0)
        {
            cursorShown = false;
            cursorColumn = board.ColumnOf(cell);
            cursorRow = board.RowOf(cell);
        }

        hoverIndex = cell;
        if (cell >= 0 && accepts && board.GroupSize(cell) >= ClusterBoard.MinGroup)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (pressed && cell >= 0 && accepts)
        {
            Clear(cell, context, scale);
        }
    }

    private bool MoveCursor(ClusterBoard board)
    {
        var column = cursorColumn;
        var row = cursorRow;
        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow, true))
        {
            column--;
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow, true))
        {
            column++;
        }

        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow, true))
        {
            row--;
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow, true))
        {
            row++;
        }

        if (column == cursorColumn && row == cursorRow)
        {
            return false;
        }

        cursorColumn = Math.Clamp(column, 0, board.Columns - 1);
        cursorRow = Math.Clamp(row, 0, board.Rows - 1);
        UiFeedback.Play(UiSound.GameTick);
        return true;
    }

    private int CellAt(Vector2 point)
    {
        var board = run.Board;
        var half = grid.Pitch * 0.5f;
        for (var index = 0; index < board.CellCount; index++)
        {
            if (board.At(index) == ClusterBoard.Empty)
            {
                continue;
            }

            var center = VisualCenter(index);
            if (MathF.Abs(point.X - center.X) <= half && MathF.Abs(point.Y - center.Y) <= half)
            {
                return index;
            }
        }

        return -1;
    }

    private Vector2 VisualCenter(int index)
    {
        var board = run.Board;
        return grid.CellCenter(board.ColumnOf(index), board.RowOf(index)) + motions.Offset(index) * grid.Pitch;
    }

    private float PieceHalf() => (grid.Pitch - grid.Gap) * 0.5f;

    private void Clear(int index, in GameContext context, float scale)
    {
        var board = run.Board;
        var size = board.FindGroup(index, group);
        if (size < ClusterBoard.MinGroup)
        {
            for (var slot = 0; slot < size; slot++)
            {
                motions.Wobble(group[slot]);
            }

            if (size > 0)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
            }

            return;
        }

        var color = board.At(index);
        var tapped = VisualCenter(index);
        var half = PieceHalf();
        var tapColumn = board.ColumnOf(index);
        var tapRow = board.RowOf(index);
        var centroid = Vector2.Zero;
        for (var slot = 0; slot < size; slot++)
        {
            var cell = group[slot];
            var center = VisualCenter(cell);
            centroid += center;
            var distance = Math.Abs(board.ColumnOf(cell) - tapColumn) + Math.Abs(board.RowOf(cell) - tapRow);
            pops.Add(center, half, board.At(cell), MathF.Min(ClusterPops.MaxRipple, distance * ClusterPops.RippleStep));
        }

        centroid /= size;
        var barBefore = run.Bar;
        if (!run.TryClear(index, out var clear))
        {
            return;
        }

        motions.Settle(board);
        hoverVersion = -1;
        gainFrom = MathF.Min(shownBar, barBefore);
        gainAlpha = 1f;
        OnCleared(in clear, color, tapped, centroid, context, scale);
    }

    private void OnCleared(in ClusterClear clear, byte color, Vector2 tapped, Vector2 centroid, in GameContext context,
        float scale)
    {
        var tint = ClusterRenderer.ColorOf(color);
        var bright = GamePalette.Lighten(tint, 0.4f);
        var tier = ClusterPace.Tier(clear.Size);
        UiFeedback.Play(tier switch
        {
            0 => clear.Size >= 5 ? UiSound.GameMatch : UiSound.GamePop,
            1 => UiSound.GameMatch,
            2 => UiSound.GameBreak,
            _ => UiSound.GameExplosion,
        });
        fx.AddTrauma(MathF.Min(0.55f, 0.03f + clear.Size * 0.012f));
        fx.Shockwave(tapped, grid.Pitch * MathF.Min(4.5f, 0.9f + clear.Size * 0.18f), bright, 0.45f, 3f);
        fx.AddText(GameNumber.Signed(clear.Points), centroid - new Vector2(0f, grid.Pitch * 0.3f),
            GamePalette.Lighten(tint, 0.5f), 1f + tier * 0.18f);
        if (clear.Multiplier > 1)
        {
            fx.AddText(multiplierLabels[clear.Multiplier].Get(L.Stage.Times, clear.Multiplier),
                centroid + new Vector2(0f, grid.Pitch * 0.35f), Gold, 0.9f);
        }

        if (clear.MultiplierRose)
        {
            GameSfx.ComboTierUp();
        }

        switch (tier)
        {
            case 1:
                fx.AddText(Loc.T(L.Cluster.Great), centroid - new Vector2(0f, grid.Pitch), Gold, 1.25f);
                fx.HitStop(0.03f);
                break;
            case 2:
                ShowBanner(Loc.T(L.Cluster.Huge), tint, BannerLow);
                context.Fx.Flash(tint, 0.16f);
                context.Fx.Punch(0.03f);
                fx.HitStop(0.05f);
                particles.Streaks(centroid, 14, bright, 380f * scale, 2.6f, 0.5f);
                break;
            case 3:
                ShowBanner(Loc.T(L.Cluster.Massive), Gold, BannerMid);
                context.Fx.Flash(White, 0.25f);
                context.Fx.Sweep();
                context.Fx.Punch(0.06f);
                context.Fx.SlowMo(0.6f, 0.3f);
                fx.HitStop(0.06f);
                particles.Confetti(centroid, 40, ClusterRenderer.Palette, 420f * scale, 3.2f, 1.1f);
                break;
        }

        if (clear.ChargeFilled)
        {
            OnShuffleReady(scale);
        }

        if (clear.BoardFinished)
        {
            OnBoardFinished(clear.Leftovers, clear.Bonus, context, scale);
        }
    }

    private void OnShuffleReady(float scale)
    {
        var button = ClusterRenderer.ButtonRect(dock, scale);
        buttonFlash = 1f;
        ShowBanner(Loc.T(L.Cluster.ShuffleReady), Accent, BannerLow);
        UiFeedback.Play(UiSound.GamePowerUp);
        fx.Shockwave(button.Center, button.Width * 0.7f, GamePalette.Lighten(Accent, 0.45f), 0.55f, 3.5f);
        particles.Sparkle(button.Center, 14, GamePalette.Lighten(Accent, 0.5f), 180f * scale, 2.4f, 0.6f);
    }

    private void OnBoardFinished(int leftovers, int bonus, in GameContext context, float scale)
    {
        var perfect = leftovers == 0;
        var color = perfect ? Gold : GamePalette.Lighten(Accent, 0.3f);
        ShowBanner(Loc.T(perfect ? L.Cluster.PerfectClear : L.Cluster.BoardClear), color, BannerHigh);
        UiFeedback.Play(perfect ? UiSound.GameWin : UiSound.GameClear);
        context.Fx.Flash(color, 0.22f);
        context.Fx.Sweep();
        particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), perfect ? 70 : 36, ClusterRenderer.Palette,
            520f * scale, 3.4f, 1.3f);
        if (bonus > 0)
        {
            fx.AddText(GameNumber.Signed(bonus), grid.Center + new Vector2(0f, grid.Pitch * 1.3f), Gold, 1.4f);
        }

        leftoverAge = 0f;
    }

    private void Shuffle(in GameContext context, float scale)
    {
        var barBefore = run.Bar;
        if (!run.TryShuffle(out var finish))
        {
            return;
        }

        var button = ClusterRenderer.ButtonRect(dock, scale);
        motions.Fly(run.Board);
        hoverVersion = -1;
        bannerProgress = 1f;
        UiFeedback.Play(UiSound.GameShuffle);
        GameSfx.ComboTierUp();
        context.Fx.Flash(Accent, 0.18f);
        context.Fx.Punch(0.03f);
        fx.Shockwave(grid.Center, grid.Width * 0.65f, GamePalette.Lighten(Accent, 0.4f), 0.55f, 4f);
        particles.Streaks(button.Center, 16, GamePalette.Lighten(Accent, 0.5f), 420f * scale, 2.4f, 0.5f,
            MathF.PI * 0.8f, -MathF.PI * 0.5f);
        if (!finish.Finished)
        {
            return;
        }

        gainFrom = MathF.Min(shownBar, barBefore);
        gainAlpha = 1f;
        OnBoardFinished(finish.Leftovers, finish.Bonus, context, scale);
    }

    private void WarnLowBar(in GameContext context, float deltaSeconds)
    {
        if (run.Bar >= UrgentBar || run.Busy)
        {
            tickTimer = 0f;
            return;
        }

        var danger = 1f - run.Bar / UrgentBar;
        context.Fx.Vignette(Danger, (0.06f + 0.2f * danger) * (0.6f + 0.4f * Pulse.Wave(Pulse.Fast)), 0.3f);
        if (run.Bar >= TickBar)
        {
            return;
        }

        tickTimer -= deltaSeconds;
        if (tickTimer > 0f)
        {
            return;
        }

        tickTimer = TickSeconds;
        UiFeedback.Play(UiSound.GameTick);
    }

    private void TrackBar(float deltaSeconds)
    {
        var target = run.Bar;
        shownBar = target < shownBar
            ? target
            : shownBar + (target - shownBar) * MathF.Min(1f, deltaSeconds * GainFollow);
        gainAlpha = MathF.Max(0f, gainAlpha - deltaSeconds * GainFade);
    }

    private void ShowBanner(string text, Vector4 color, int priority)
    {
        if (bannerProgress < 1f && priority < bannerPriority)
        {
            return;
        }

        bannerText = text;
        bannerColor = color;
        bannerPriority = priority;
        bannerProgress = 0f;
    }

    private void DrawStage(ImDrawListPtr drawList, in GameContext context, float scale, bool interactive)
    {
        DrawBoard(drawList, context, scale, interactive);
        var button = ClusterRenderer.ButtonRect(dock, scale);
        ClusterRenderer.DrawButton(drawList, button, Loc.T(L.Cluster.Shuffle), run.ChargeFraction, run.ShuffleReady,
            interactive && buttonHovered, buttonFlash, Accent, scale);
    }

    private void DrawBoard(ImDrawListPtr drawList, in GameContext context, float scale, bool interactive)
    {
        DrawHeat();
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, Accent, context.Backdrop.Ink);
        DrawWells(drawList, scale);
        ClusterRenderer.DrawBar(drawList, bar, shownBar, gainFrom, gainAlpha, run.Busy && !run.Over, Accent, scale);
        DrawPieces(drawList, scale, interactive);
        pops.Draw(drawList, scale);
        if (interactive && cursorShown && run.AcceptsInput)
        {
            var cursor = run.Board.Index(cursorColumn, cursorRow);
            ClusterRenderer.DrawCursor(drawList, grid.CellCenter(cursorColumn, cursorRow) +
                motions.Offset(cursor) * grid.Pitch, PieceHalf(), Accent, scale);
        }
    }

    private void DrawHeat()
    {
        var heat = run.Combo.Heat;
        if (heat <= 0.01f)
        {
            return;
        }

        var pulse = Pulse.Wave(Pulse.Fast);
        var hot = Vector4.Lerp(Accent, HotTint, heat);
        ProgressRing.Glow(grid.Center, grid.Width * (0.7f + 0.1f * pulse * heat), hot, heat * (1.1f + 0.5f * pulse));
    }

    private void DrawWells(ImDrawListPtr drawList, float scale)
    {
        var board = run.Board;
        var rounding = PieceHalf() * 0.6f;
        var fill = GamePalette.CellSunken with { W = 0.45f };
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < board.Columns; column++)
            {
                StageCell.Draw(drawList, grid.Cell(column, row), fill, CellDepth.Sunken, rounding, scale);
            }
        }
    }

    private void DrawPieces(ImDrawListPtr drawList, float scale, bool interactive)
    {
        var board = run.Board;
        var half = PieceHalf();
        var pointed = cursorShown ? board.Index(cursorColumn, cursorRow) : hoverIndex;
        var focus = interactive && run.AcceptsInput ? pointed : -1;
        RefreshHover(focus);
        var leftover = run.Pending == ClusterPending.NextBoard ? Easing.Clamp01(leftoverAge / LeftoverFade) : 0f;
        var fade = ending ? Easing.Clamp01(endTimer / EndFadeSeconds) : 0f;
        var lift = HoverLift * scale;
        var margin = grid.Pitch * 0.25f;
        var bounds = grid.Bounds;
        drawList.PushClipRect(bounds.Min - new Vector2(margin, margin), bounds.Max + new Vector2(margin, margin), true);
        for (var index = 0; index < board.CellCount; index++)
        {
            var color = board.At(index);
            if (color == ClusterBoard.Empty)
            {
                continue;
            }

            var center = VisualCenter(index);
            var fill = ClusterRenderer.ColorOf(color);
            var alpha = 1f;
            if (leftover > 0f)
            {
                fill = ClusterRenderer.Faded(color, 0.7f * leftover);
                alpha = 1f - leftover * leftover;
            }
            else if (fade > 0f)
            {
                fill = ClusterRenderer.Faded(color, 0.75f * fade);
            }

            var grouped = hoverSize >= ClusterBoard.MinGroup && hoverMask[index] && fade <= 0f;
            if (grouped)
            {
                ProgressRing.Glow(center, half * 1.25f, fill, 0.45f);
                center.Y -= lift;
            }

            ClusterRenderer.DrawPiece(drawList, center, half, fill, color, alpha, grouped ? 1f : 0f, scale);
        }

        drawList.PopClipRect();
        DrawPreview(drawList, focus, scale);
    }

    private void RefreshHover(int focus)
    {
        var board = run.Board;
        if (focus == hoverFocus && hoverVersion == board.Version)
        {
            return;
        }

        if (hoverSize > 0)
        {
            Array.Clear(hoverMask);
        }

        hoverFocus = focus;
        hoverVersion = board.Version;
        hoverSize = focus < 0 ? 0 : board.FindGroup(focus, group);
        if (hoverSize < ClusterBoard.MinGroup)
        {
            return;
        }

        for (var slot = 0; slot < hoverSize; slot++)
        {
            hoverMask[group[slot]] = true;
        }
    }

    private void DrawPreview(ImDrawListPtr drawList, int focus, float scale)
    {
        if (focus < 0 || hoverSize < ClusterBoard.MinGroup)
        {
            return;
        }

        var board = run.Board;
        var top = focus;
        for (var index = 0; index < board.CellCount; index++)
        {
            if (hoverMask[index] && board.RowOf(index) < board.RowOf(top))
            {
                top = index;
            }
        }

        var points = ClusterPace.Points(hoverSize, ComboMeter.MultiplierFor(run.Combo.Count + 1));
        var label = GameNumber.Signed(points);
        var anchor = VisualCenter(top) - new Vector2(0f, grid.Pitch * 0.62f + HoverLift * scale);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var pad = new Vector2(8f * scale, 3f * scale);
        var min = anchor - size * 0.5f - pad;
        var max = anchor + size * 0.5f + pad;
        StageHud.Capsule(drawList, new Rect(min, max), scale, 0.85f);
        Typography.DrawCentered(drawList, anchor, label, StageInks.Strong, TextStyles.FootnoteEmphasized);
    }
}
