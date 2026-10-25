using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Rolls;

internal sealed class RollSetupPage
{
    private static readonly LocString[] AgainstTitles = { L.Rolls.AgainstPlayers, L.Rolls.AgainstHouse };
    private static readonly FontAwesomeIcon[] AgainstIcons = { FontAwesomeIcon.Users, FontAwesomeIcon.Landmark };
    private static readonly LocString[] FlipTitles = { L.Rolls.FlipPlayer, L.Rolls.AgainstHouse };
    private static readonly FontAwesomeIcon[] FlipIcons = { FontAwesomeIcon.User, FontAwesomeIcon.Landmark };

    private readonly ChatRolls rolls;
    private readonly CasinoTextCache texts = new();
    private RollGameKind kind;
    private int range;
    private int bet;
    private int pays = RollGames.DefaultPays;
    private int channel = RollGames.DefaultChannel;
    private bool againstHouse;
    private bool houseFlips;
    private int paysShown = -1;
    private string paysText = string.Empty;
    private LanguageInfo? paysLanguage;

    public RollSetupPage(ChatRolls rolls)
    {
        this.rolls = rolls;
    }

    public RollGameKind Kind => kind;

    private long RangeValue => RollGames.Ranges[range];

    private long BetValue => HostLadders.DiceStakes[bet];

    private bool HouseGame => kind switch
    {
        RollGameKind.Deathroll => againstHouse,
        RollGameKind.Elimination => false,
        _ => true,
    };

    public void Enter(RollGameKind game)
    {
        kind = game;
        range = HostLadders.FloorIndex(RollGames.Ranges, RollGames.Of(game).DefaultRange);
    }

    public bool Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var line = Loc.T(L.Rolls.SetupLine);
        var block = HostCards.FooterBlock(string.Empty, line, string.Empty, body.Width, scale);
        var scroll = new Rect(body.Min,
            new Vector2(body.Max.X, body.Max.Y - HostFlowLayout.FooterHeight(scale, block)));
        using (UiScope.PushId("casino.rolls.setup"))
        {
            using (AppSurface.Begin(scroll))
            using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                DrawContent(ui, scale);
            }

            var footer = new Rect(new Vector2(body.Min.X, scroll.Max.Y), body.Max);
            if (!HostCards.Footer(ui, footer, string.Empty, line, string.Empty, Loc.T(L.Rolls.OpenGame), true,
                    "casino.rolls.open", scale))
            {
                return false;
            }
        }

        rolls.Open(new RollSettings(kind, (int)RangeValue, BetValue, RollGames.PayTenths[pays],
            kind == RollGameKind.Deathroll && againstHouse, houseFlips, RollGames.ChannelKeys[channel]));
        return true;
    }

    private void DrawContent(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        ref readonly var info = ref RollGames.Of(kind);
        HostCards.Gap(HostFlowLayout.LineGap, scale);
        RollHeader.Draw(ui, kind, Loc.T(info.Line), width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        HostCards.InfoCard(ui, texts.Number(info.Rules, RollGames.UsesRange(kind) ? RangeValue : RollTable.CoinRange),
            width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawSides(ui, width, scale);
        if (RollGames.UsesRange(kind))
        {
            HostCards.Ladder(ui, "##rollRange",
                Loc.T(kind == RollGameKind.Deathroll ? L.HostSetup.StartFrom : L.HostSetup.RollsUpTo),
                NumberText.Group(RangeValue), string.Empty, HostLadders.All(RollGames.Ranges), ref range, width,
                scale);
            HostCards.Gap(HostFlowLayout.CardGap, scale);
        }

        HostCards.Ladder(ui, "##rollBet", Loc.T(L.Rolls.Bet),
            BetValue > 0 ? TableAmounts.Amount(texts, BetValue, CasinoCurrencies.Gil) : Loc.T(L.Venue.NoStake),
            Loc.T(L.Rolls.BetHint), HostLadders.All(HostLadders.DiceStakes), ref bet, width, scale);
        if (HouseGame)
        {
            HostCards.Gap(HostFlowLayout.CardGap, scale);
            HostCards.Ladder(ui, "##rollPays", Loc.T(L.Rolls.Pays), PaysText(), Loc.T(L.Rolls.PaysHint),
                new LadderSpan(0, RollGames.PayTenths.Length - 1), ref pays, width, scale);
        }

        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        HostCards.Heading(ui, Loc.T(L.Rolls.AnnounceHeading), string.Empty, width, scale);
        var picked = HostCards.IconChoices(ui, "rollChannel", RollGames.ChannelTitles, RollGames.ChannelIcons, channel,
            width, scale);
        if (picked >= 0)
        {
            channel = picked;
        }

        HostCards.Gap(HostFlowLayout.CardGap, scale);
        HostCards.Hint(ui, Loc.T(L.Rolls.AnnounceHint), width);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
    }

    private void DrawSides(AppSkin ui, float width, float scale)
    {
        if (kind == RollGameKind.Deathroll)
        {
            HostCards.Heading(ui, Loc.T(L.Rolls.AgainstHeading), string.Empty, width, scale);
            var picked = HostCards.IconChoices(ui, "rollAgainst", AgainstTitles, AgainstIcons, againstHouse ? 1 : 0,
                width, scale);
            if (picked >= 0)
            {
                againstHouse = picked == 1;
            }

            HostCards.Gap(HostFlowLayout.SectionGap, scale);
            return;
        }

        if (kind != RollGameKind.CoinFlip)
        {
            return;
        }

        HostCards.Heading(ui, Loc.T(L.Rolls.FlipHeading), string.Empty, width, scale);
        var flipper = HostCards.IconChoices(ui, "rollFlip", FlipTitles, FlipIcons, houseFlips ? 1 : 0, width, scale);
        if (flipper >= 0)
        {
            houseFlips = flipper == 1;
        }

        HostCards.Gap(HostFlowLayout.SectionGap, scale);
    }

    private string PaysText()
    {
        if (pays != paysShown || !ReferenceEquals(paysLanguage, Loc.Current))
        {
            paysShown = pays;
            paysLanguage = Loc.Current;
            paysText = RollGames.PaysText(RollGames.PayTenths[pays]);
        }

        return paysText;
    }
}
