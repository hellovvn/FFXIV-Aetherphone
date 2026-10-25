namespace Aetherphone.Apps.Games.Cluster;

internal static class ClusterPace
{
    public const float StartBar = 0.85f;
    public const float BaseDrainPerSecond = 1f / 30f;
    public const float DrainGrowthPerBoard = 0.16f;
    public const float DrainGrowthPerMinute = 0.35f;
    public const float RefillPerPiece = 0.016f;
    public const float BoardClearRefill = 0.25f;
    public const float PerfectClearRefill = 0.45f;
    public const int PointsPerSquare = 5;
    public const int BoardBonusPerLevel = 1000;
    public const int LeftoverPenalty = 100;
    public const int PerfectBonusPerLevel = 1000;
    public const int ChargeStartsAt = 5;
    public const int ChargeNeeded = 30;
    public const int GreatGroup = 8;
    public const int HugeGroup = 12;
    public const int MassiveGroup = 18;
    private static readonly byte[] BoardColors = { 3, 4, 5, 6, 6, 6 };
    private static readonly byte[] BoardColumns = { 7, 7, 7, 7, 8, 9 };
    private static readonly byte[] BoardRows = { 9, 9, 9, 9, 10, 11 };

    public static int ColorsFor(int level) => BoardColors[Slot(level)];

    public static int ColumnsFor(int level) => BoardColumns[Slot(level)];

    public static int RowsFor(int level) => BoardRows[Slot(level)];

    public static float DrainPerSecond(int level, float playSeconds) =>
        BaseDrainPerSecond * (1f + DrainGrowthPerBoard * (Math.Max(1, level) - 1) +
                              DrainGrowthPerMinute * MathF.Max(0f, playSeconds) / 60f);

    public static float Refill(int size) => size < ClusterBoard.MinGroup ? 0f : size * RefillPerPiece;

    public static int Points(int size, int multiplier) =>
        size < ClusterBoard.MinGroup ? 0 : size * size * PointsPerSquare * Math.Max(1, multiplier);

    public static int Charge(int size) => Math.Max(0, size - ChargeStartsAt + 1);

    public static int BoardBonus(int level, int leftovers)
    {
        var bonus = Math.Max(0, BoardBonusPerLevel - leftovers * LeftoverPenalty) * Math.Max(1, level);
        return leftovers == 0 ? bonus + PerfectBonusPerLevel * Math.Max(1, level) : bonus;
    }

    public static int Tier(int size)
    {
        if (size >= MassiveGroup)
        {
            return 3;
        }

        if (size >= HugeGroup)
        {
            return 2;
        }

        return size >= GreatGroup ? 1 : 0;
    }

    private static int Slot(int level) => Math.Clamp(level - 1, 0, BoardColors.Length - 1);
}
