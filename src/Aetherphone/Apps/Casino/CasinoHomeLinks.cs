namespace Aetherphone.Apps.Casino;

internal static class CasinoHomeLinks
{
    public static bool IsLinkable(string gameId) => gameId switch
    {
        CasinoGames.DailySpin or CasinoGames.Blackjack or CasinoGames.Holdem or CasinoGames.DealerHoldem
            or CasinoGames.Scratch or CasinoGames.Barkeep or CasinoGames.Wheel or CasinoGames.Bingo
            or CasinoGames.Race or CasinoGames.Plinko => true,
        _ => Machines.MachineCabinet.Owns(gameId) || Originals.OriginalsCabinet.Owns(gameId),
    };
}
