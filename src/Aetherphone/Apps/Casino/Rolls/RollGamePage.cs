using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Rolls;

internal enum RollPageAction : byte
{
    None,
    Start,
    CallPlayers,
    RollHouse,
    ChooseFirst,
    ChooseSecond,
    PlayAgain,
    NewPlayers,
    Record,
    End,
}

internal sealed class RollGamePage
{
    private const int MaxSecondary = 3;
    private const float RowHeight = 44f;
    private const float LogRowHeight = 40f;
    private const float PromptPad = 16f;
    private const float PromptWashAlpha = 0.16f;
    private const float PillPadX = 8f;
    private const float PillPadY = 3f;
    private const float PillWashAlpha = 0.14f;
    private const float HairlineAlpha = 0.08f;
    private const float DimAlpha = 0.55f;

    private readonly ChatRolls rolls;
    private readonly CasinoTablesStore tables;
    private readonly RollGameView view = new();
    private readonly CasinoTextCache texts = new();
    private readonly RollPageAction[] secondary = new RollPageAction[MaxSecondary];
    private int recordedSerial = -1;

    public RollGamePage(ChatRolls rolls, CasinoTablesStore tables)
    {
        this.rolls = rolls;
        this.tables = tables;
    }

    public bool Draw(Rect body, AppSkin ui)
    {
        var table = rolls.Table;
        if (!table.IsOpen)
        {
            return true;
        }

        var scale = UiScale.Current;
        view.Sync(table, rolls.HouseName);
        var secondaryCount = Secondary(table);
        var footerHeight = FooterHeight(table, secondaryCount, scale);
        var scroll = new Rect(body.Min, new Vector2(body.Max.X, body.Max.Y - footerHeight));
        var action = RollPageAction.None;
        using (UiScope.PushId("casino.rolls.game"))
        {
            using (AppSurface.Begin(scroll))
            using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                DrawContent(ui, table, scale);
            }

            var footer = new Rect(new Vector2(body.Min.X, scroll.Max.Y), body.Max);
            action = DrawFooter(ui, table, footer, secondaryCount, scale);
        }

        return Apply(action, table);
    }

    private void DrawContent(AppSkin ui, RollTable table, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        HostCards.Gap(HostFlowLayout.LineGap, scale);
        RollHeader.Draw(ui, table.Kind, view.Summary, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawPrompt(ui, table, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        HostCards.Heading(ui, Loc.T(L.Rolls.PlayersHeading), string.Empty, width, scale);
        DrawPlayers(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        HostCards.Heading(ui, Loc.T(L.Rolls.RollsHeading), string.Empty, width, scale);
        DrawLog(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
    }

    private void DrawPrompt(AppSkin ui, RollTable table, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = PromptPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var height = Typography.MeasureWrappedBlock(view.Prompt, TextStyles.Title3, inner).Y + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Grouped * scale;
        var tint = RollGames.Of(table.Kind).Tint;
        ui.Card(drawList, origin, max, radius);
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(tint with { W = PromptWashAlpha }));
        Typography.DrawWrappedCentered(drawList, view.Prompt, TextStyles.Title3, ui.TitleInk,
            new Vector2(origin.X + width * 0.5f, origin.Y + pad), inner);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawPlayers(AppSkin ui, float width, float scale)
    {
        if (view.PlayerRows == 0)
        {
            HostCards.InfoCard(ui, Loc.T(L.Rolls.PlayersEmpty), width, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = RowHeight * scale;
        var pad = HostFlowLayout.CardPad * scale;
        var height = rowHeight * view.PlayerRows;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var dropped = -1;
        var houseOffset = rolls.Table.HouseGame && rolls.Table.Phase != RollPhase.Joining ? 1 : 0;
        for (var index = 0; index < view.PlayerRows; index++)
        {
            var row = view.PlayerAt(index);
            var top = origin.Y + rowHeight * index;
            var centerY = top + rowHeight * 0.5f;
            if (index > 0)
            {
                Hairline(drawList, ui, origin.X + pad, origin.X + width - pad, top, scale);
            }

            var right = origin.X + width - pad;
            if (row.Action != RollRowAction.None)
            {
                var label = Loc.T(row.Action == RollRowAction.Remove ? L.Rolls.Remove : L.Rolls.Drop);
                var buttonWidth = Button.WidthFor(label, ButtonSize.Small);
                var buttonHeight = Button.SmallHeight * scale;
                var button = new Rect(new Vector2(right - buttonWidth, centerY - buttonHeight * 0.5f),
                    new Vector2(right, centerY + buttonHeight * 0.5f));
                using (UiScope.PushId(index))
                {
                    if (Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Gray, id: "casino.rolls.drop"))
                    {
                        dropped = index - houseOffset;
                    }
                }

                right = button.Min.X - Metrics.Space.Sm * scale;
            }

            right = DrawPill(drawList, ui, row.Status, row.Lit, right, centerY, scale) - Metrics.Space.Sm * scale;
            var nameWidth = MathF.Max(1f, right - origin.X - pad);
            var nameHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - nameHeight * 0.5f),
                Typography.FitText(row.Name, nameWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (dropped >= 0)
        {
            rolls.Drop(dropped);
        }
    }

    private void DrawLog(AppSkin ui, float width, float scale)
    {
        if (view.LogRows == 0)
        {
            HostCards.InfoCard(ui, Loc.T(L.Rolls.RollsEmpty), width, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = LogRowHeight * scale;
        var pad = HostFlowLayout.CardPad * scale;
        var height = rowHeight * view.LogRows;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var valueWidth = Typography.Measure("999", TextStyles.Headline).X;
        for (var index = 0; index < view.LogRows; index++)
        {
            var row = view.LogAt(index);
            var top = origin.Y + rowHeight * index;
            var centerY = top + rowHeight * 0.5f;
            if (index > 0)
            {
                Hairline(drawList, ui, origin.X + pad, origin.X + width - pad, top, scale);
            }

            var ink = row.Counted ? ui.TitleInk : Palette.WithAlpha(ui.BodyInk, DimAlpha);
            var right = origin.X + width - pad;
            if (row.Verdict.Length > 0)
            {
                right = DrawPill(drawList, ui, row.Verdict, false, right, centerY, scale) - Metrics.Space.Sm * scale;
            }
            else
            {
                var check = new Vector2(right - Metrics.Space.Sm * scale, centerY);
                ProgressRing.CenterIcon(drawList, check, FontAwesomeIcon.Check, ui.Accent, HostFlowLayout.CheckSize * scale);
                right = check.X - Metrics.Space.Md * scale;
            }

            var rangeWidth = Typography.Measure(row.Range, TextStyles.Footnote).X;
            var footHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(right - rangeWidth, centerY - footHeight * 0.5f), row.Range,
                ui.MutedInk, TextStyles.Footnote);
            right -= rangeWidth + Metrics.Space.Sm * scale;
            var headHeight = Typography.LineHeight(TextStyles.Headline);
            var valueSize = Typography.Measure(row.Value, TextStyles.Headline).X;
            Typography.Draw(drawList, new Vector2(right - valueSize, centerY - headHeight * 0.5f), row.Value, ink,
                TextStyles.Headline);
            right -= valueWidth + Metrics.Space.Sm * scale;
            var nameWidth = MathF.Max(1f, right - origin.X - pad);
            var nameHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - nameHeight * 0.5f),
                Typography.FitText(row.Name, nameWidth, TextStyles.Subheadline), ink, TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static float DrawPill(ImDrawListPtr drawList, AppSkin ui, string text, bool lit, float right,
        float centerY, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(text, style);
        var padX = PillPadX * scale;
        var padY = PillPadY * scale;
        var min = new Vector2(right - size.X - padX * 2f, centerY - size.Y * 0.5f - padY);
        var max = new Vector2(right, centerY + size.Y * 0.5f + padY);
        var fill = lit ? ui.Accent : Palette.WithAlpha(ui.TitleInk, PillWashAlpha);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, new Vector2(min.X + padX, min.Y + padY), text, lit ? CasinoArt.White : ui.BodyInk,
            style);
        return min.X;
    }

    private static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y, float scale)
    {
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), MathF.Max(1f, 0.5f * scale));
    }

    private int Secondary(RollTable table)
    {
        var count = 0;
        if (table.Phase == RollPhase.Joining && table.Settings.Channel.Length > 0)
        {
            secondary[count++] = RollPageAction.CallPlayers;
        }

        if (table.Phase == RollPhase.Settled)
        {
            secondary[count++] = RollPageAction.NewPlayers;
            if (CanRecord(table, out _))
            {
                secondary[count++] = RollPageAction.Record;
            }
        }

        secondary[count++] = RollPageAction.End;
        return count;
    }

    private static int PrimaryCount(RollTable table) => table.Phase switch
    {
        RollPhase.Joining or RollPhase.Settled => 1,
        RollPhase.Playing when table.Step == RollStep.AwaitCall => 2,
        RollPhase.Playing when table.WaitsOnHouse => 1,
        _ => 0,
    };

    private static float FooterHeight(RollTable table, int secondaryCount, float scale)
    {
        var height = HostFlowLayout.FooterPad * 2f * scale;
        if (PrimaryCount(table) > 0)
        {
            height += HostFlowLayout.Touch * scale + HostFlowLayout.FooterGap * scale;
        }

        return height + Button.SmallHeight * scale * (secondaryCount > 0 ? 1f : 0f);
    }

    private RollPageAction DrawFooter(AppSkin ui, RollTable table, Rect footer, int secondaryCount, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bandMin = new Vector2(footer.Min.X - AppSurface.SidePadding * scale, footer.Min.Y);
        var bandMax = new Vector2(footer.Max.X + AppSurface.SidePadding * scale, footer.Max.Y);
        Material.ThemedGlass(drawList, bandMin, bandMax, 0f, scale, ui.BackdropColor, TabBar.GlassOpacity);
        drawList.AddLine(bandMin, new Vector2(bandMax.X, bandMin.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), MathF.Max(1f, 0.5f * scale));
        var top = footer.Min.Y + HostFlowLayout.FooterPad * scale;
        var action = RollPageAction.None;
        var primaries = PrimaryCount(table);
        if (primaries > 0)
        {
            var gap = HostFlowLayout.ChoiceGap * scale;
            var buttonWidth = (footer.Width - gap * (primaries - 1)) / primaries;
            for (var index = 0; index < primaries; index++)
            {
                var left = footer.Min.X + index * (buttonWidth + gap);
                var rect = new Rect(new Vector2(left, top),
                    new Vector2(left + buttonWidth, top + HostFlowLayout.Touch * scale));
                var primary = PrimaryAt(table, index);
                using (UiScope.PushId(index))
                {
                    if (Button.Draw(drawList, rect, PrimaryLabel(table, primary), ui.Ink, ButtonStyle.Prominent,
                            enabled: primary != RollPageAction.Start || table.CanStart, id: "casino.rolls.primary"))
                    {
                        action = primary;
                    }
                }
            }

            top += HostFlowLayout.Touch * scale + HostFlowLayout.FooterGap * scale;
        }

        var cursor = footer.Min.X;
        for (var index = 0; index < secondaryCount; index++)
        {
            var label = SecondaryLabel(secondary[index]);
            var buttonWidth = Button.WidthFor(label, ButtonSize.Small);
            var rect = new Rect(new Vector2(cursor, top), new Vector2(cursor + buttonWidth, top + Button.SmallHeight * scale));
            using (UiScope.PushId(primaries + index))
            {
                if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Gray, id: "casino.rolls.secondary"))
                {
                    action = secondary[index];
                }
            }

            cursor = rect.Max.X + Metrics.Space.Sm * scale;
        }

        return action;
    }

    private static RollPageAction PrimaryAt(RollTable table, int index) => table.Phase switch
    {
        RollPhase.Joining => RollPageAction.Start,
        RollPhase.Settled => RollPageAction.PlayAgain,
        _ when table.Step == RollStep.AwaitCall => index == 0 ? RollPageAction.ChooseFirst : RollPageAction.ChooseSecond,
        _ => RollPageAction.RollHouse,
    };

    private string PrimaryLabel(RollTable table, RollPageAction action) => action switch
    {
        RollPageAction.Start => Loc.T(L.Rolls.Start),
        RollPageAction.PlayAgain => Loc.T(L.Rolls.PlayAgain),
        RollPageAction.ChooseFirst => Loc.T(table.Kind == RollGameKind.CoinFlip ? L.Rolls.Heads : L.Rolls.Higher),
        RollPageAction.ChooseSecond => Loc.T(table.Kind == RollGameKind.CoinFlip ? L.Rolls.Tails : L.Rolls.Lower),
        _ => texts.Number(L.Rolls.RollFor, table.ExpectedMax),
    };

    private static string SecondaryLabel(RollPageAction action) => action switch
    {
        RollPageAction.CallPlayers => Loc.T(L.Rolls.CallPlayers),
        RollPageAction.NewPlayers => Loc.T(L.Rolls.NewPlayers),
        RollPageAction.Record => Loc.T(L.Rolls.Record),
        _ => Loc.T(L.Rolls.End),
    };

    private bool CanRecord(RollTable table, out string tableId)
    {
        tableId = string.Empty;
        if (table.Serial == recordedSerial || table.Payout <= 0
            || table.Outcome is not (RollOutcome.PlayerWins or RollOutcome.LastStanding))
        {
            return false;
        }

        tableId = tables.HostedTableId();
        return tableId.Length > 0 && tables.CardFor(tableId)?.Currency == CasinoCurrencies.Gil;
    }

    private bool Apply(RollPageAction action, RollTable table)
    {
        switch (action)
        {
            case RollPageAction.Start:
                rolls.Start();
                break;
            case RollPageAction.CallPlayers:
                rolls.CallForPlayers();
                break;
            case RollPageAction.RollHouse:
                rolls.RollForHouse();
                break;
            case RollPageAction.ChooseFirst:
                rolls.Choose(table.Kind == RollGameKind.CoinFlip ? RollCall.Heads : RollCall.Higher);
                break;
            case RollPageAction.ChooseSecond:
                rolls.Choose(table.Kind == RollGameKind.CoinFlip ? RollCall.Tails : RollCall.Lower);
                break;
            case RollPageAction.PlayAgain:
                rolls.Rematch();
                break;
            case RollPageAction.NewPlayers:
                rolls.NewPlayers();
                break;
            case RollPageAction.Record:
                Record(table);
                break;
            case RollPageAction.End:
                rolls.Close();
                return true;
        }

        return false;
    }

    private void Record(RollTable table)
    {
        if (!CanRecord(table, out var tableId))
        {
            return;
        }

        var name = table.Outcome == RollOutcome.PlayerWins && table.PlayerCount > 0
            ? table.PlayerAt(0).Name
            : RollGameView.WinnerName(table);
        tables.RecordEntry(tableId, CasinoLedgerKinds.Payout, string.Empty, name, table.Payout);
        recordedSerial = table.Serial;
        ShellToast.Show(Loc.T(L.Rolls.Recorded));
    }
}
