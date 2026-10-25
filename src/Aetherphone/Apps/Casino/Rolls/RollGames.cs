using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Rolls;

internal readonly record struct RollGameInfo(
    RollGameKind Kind,
    LocString Title,
    LocString Meta,
    LocString Line,
    LocString Rules,
    string Sign,
    string GlyphId,
    FontAwesomeIcon Icon,
    Vector4 Tint,
    int DefaultRange);

internal static class RollGames
{
    public const int DefaultPays = 3;
    public const int DefaultChannel = 2;

    public static readonly RollGameInfo[] All =
    {
        new(RollGameKind.Deathroll, L.Venue.GameDeathroll, L.Rolls.MetaDeathroll, L.Rolls.LineDeathroll,
            L.Rolls.RulesDeathroll, "DEATHROLL", CasinoGames.Deathroll, FontAwesomeIcon.Skull, AccentRing.Orange,
            RollLines.MaxRange),
        new(RollGameKind.Elimination, L.Rolls.GameElimination, L.Rolls.MetaElimination, L.Rolls.LineElimination,
            L.Rolls.RulesElimination, "ELIMINATION", string.Empty, FontAwesomeIcon.UserSlash, AccentRing.Rose,
            RollLines.MaxRange),
        new(RollGameKind.HigherLower, L.Rolls.GameHigherLower, L.Rolls.MetaHouse, L.Rolls.LineHigherLower,
            L.Rolls.RulesHigherLower, "HI-LO", CasinoGames.HiLo, FontAwesomeIcon.ExchangeAlt, AccentRing.Teal, 100),
        new(RollGameKind.CoinFlip, L.Rolls.GameCoinFlip, L.Rolls.MetaHouse, L.Rolls.LineCoinFlip,
            L.Rolls.RulesCoinFlip, "COIN FLIP", string.Empty, FontAwesomeIcon.Coins, AccentRing.Gold,
            RollTable.CoinRange),
    };

    public static readonly long[] Ranges = { 6, 10, 20, 50, 100, 250, 500, RollLines.MaxRange };

    public static readonly int[] PayTenths = { 15, 18, 19, 20, 25, 30, 50 };

    public static readonly string[] ChannelKeys =
        { string.Empty, GameChannels.PartyKey, GameChannels.SayKey, ChatRolls.YellKey };

    public static readonly LocString[] ChannelTitles =
        { L.Rolls.AnnounceOff, L.Rolls.ChannelParty, L.Rolls.ChannelSay, L.Rolls.ChannelYell };

    public static readonly FontAwesomeIcon[] ChannelIcons =
        { FontAwesomeIcon.VolumeMute, FontAwesomeIcon.Users, FontAwesomeIcon.Comment, FontAwesomeIcon.Bullhorn };

    public static ref readonly RollGameInfo Of(RollGameKind kind) => ref All[(int)kind];

    public static bool UsesRange(RollGameKind kind) => kind != RollGameKind.CoinFlip;

    public static string PaysText(int tenths) =>
        Loc.T(L.Rolls.PaysValue, (tenths / 10f).ToString(tenths % 10 == 0 ? "0" : "0.0", Loc.Culture));
}
