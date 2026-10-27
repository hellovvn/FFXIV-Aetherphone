using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Cluster;

[Flags]
internal enum ClusterEvents : byte
{
    None = 0,
    Reshuffled = 1,
    BoardStarted = 2,
    Ended = 4,
}

internal enum ClusterPending : byte
{
    None,
    Reshuffle,
    NextBoard,
}

internal readonly record struct ClusterClear(int Size, int Points, int Multiplier, bool MultiplierRose, float Refill,
    bool BoardFinished, int Leftovers, int Bonus, bool ChargeFilled);

internal readonly record struct ClusterFinish(bool Finished, int Leftovers, int Bonus, float Refill);

internal sealed class ClusterRun
{
    public const float ComboWindowSeconds = 1.5f;
    public const float ReshuffleDelay = 0.3f;
    public const float ReshuffleSeconds = 0.5f;
    public const float BoardClearSeconds = 1.3f;
    public const float BoardDropSeconds = 0.5f;
    public const float ShuffleSeconds = 0.55f;

    private readonly int[] group = new int[ClusterBoard.MaxCells];
    private GameRandom random;
    private ComboMeter combo = new(ComboWindowSeconds);
    private ClusterEvents events;
    private float pendingSeconds;
    private float busySeconds;
    private int charge;

    public ClusterBoard Board { get; } = new();

    public int Level { get; private set; }

    public float Bar { get; private set; }

    public int Score { get; private set; }

    public int BestGroup { get; private set; }

    public int BestCombo { get; private set; }

    public int Cleared { get; private set; }

    public int Boards { get; private set; }

    public int Reshuffles { get; private set; }

    public int Shuffles { get; private set; }

    public float PlaySeconds { get; private set; }

    public bool Over { get; private set; }

    public ClusterPending Pending { get; private set; }

    public ComboMeter Combo => combo;

    public float ChargeFraction => Math.Clamp(charge / (float)ClusterPace.ChargeNeeded, 0f, 1f);

    public bool ShuffleReady => charge >= ClusterPace.ChargeNeeded;

    public bool Busy => Pending != ClusterPending.None || busySeconds > 0f;

    public bool AcceptsInput => !Over && !Busy;

    public float DrainPerSecond => ClusterPace.DrainPerSecond(Level, PlaySeconds);

    public void Reset(GameRandom seeded)
    {
        random = seeded;
        Level = 1;
        Bar = ClusterPace.StartBar;
        Score = 0;
        BestGroup = 0;
        BestCombo = 0;
        Cleared = 0;
        Boards = 0;
        Reshuffles = 0;
        Shuffles = 0;
        PlaySeconds = 0f;
        Over = false;
        Pending = ClusterPending.None;
        pendingSeconds = 0f;
        busySeconds = 0f;
        charge = 0;
        events = ClusterEvents.None;
        combo.Reset();
        StartBoard();
        busySeconds = 0f;
        events = ClusterEvents.None;
    }

    public ClusterEvents TakeEvents()
    {
        var taken = events;
        events = ClusterEvents.None;
        return taken;
    }

    public void Step(float deltaSeconds)
    {
        if (Over || deltaSeconds <= 0f)
        {
            return;
        }

        PlaySeconds += deltaSeconds;
        if (Pending != ClusterPending.None)
        {
            pendingSeconds -= deltaSeconds;
            if (pendingSeconds <= 0f)
            {
                Resolve();
            }

            return;
        }

        if (busySeconds > 0f)
        {
            busySeconds = MathF.Max(0f, busySeconds - deltaSeconds);
            return;
        }

        combo.Update(deltaSeconds);
        Bar = MathF.Max(0f, Bar - DrainPerSecond * deltaSeconds);
        if (Bar > 0f)
        {
            return;
        }

        Over = true;
        events |= ClusterEvents.Ended;
    }

    public bool TryClear(int index, out ClusterClear result)
    {
        result = default;
        if (!AcceptsInput)
        {
            return false;
        }

        var size = Board.FindGroup(index, group);
        if (size < ClusterBoard.MinGroup)
        {
            return false;
        }

        var multiplierBefore = combo.Multiplier;
        combo.Hit();
        BestCombo = Math.Max(BestCombo, combo.Count);
        var multiplier = combo.Multiplier;
        var points = ClusterPace.Points(size, multiplier);
        var refill = ClusterPace.Refill(size);
        var chargeBefore = ShuffleReady;
        Board.Remove(group.AsSpan(0, size));
        Score += points;
        Cleared += size;
        BestGroup = Math.Max(BestGroup, size);
        AddCharge(ClusterPace.Charge(size));
        Bar = MathF.Min(1f, Bar + refill);
        var finish = SettleBoard(ReshuffleDelay);
        result = new ClusterClear(size, points, multiplier, multiplier > multiplierBefore, refill + finish.Refill,
            finish.Finished, finish.Leftovers, finish.Bonus, !chargeBefore && ShuffleReady);
        return true;
    }

    public bool TryShuffle(out ClusterFinish finish)
    {
        finish = default;
        if (!AcceptsInput || !ShuffleReady)
        {
            return false;
        }

        charge = 0;
        Shuffles++;
        Board.Regroup(ref random);
        busySeconds = ShuffleSeconds;
        finish = SettleBoard(ShuffleSeconds);
        return true;
    }

    private ClusterFinish SettleBoard(float reshuffleDelay)
    {
        if (Board.Finished)
        {
            var leftovers = Board.Count;
            var bonus = ClusterPace.BoardBonus(Level, leftovers);
            var refill = leftovers == 0 ? ClusterPace.PerfectClearRefill : ClusterPace.BoardClearRefill;
            Score += bonus;
            Boards++;
            Bar = MathF.Min(1f, Bar + refill);
            Hold(ClusterPending.NextBoard, BoardClearSeconds);
            return new ClusterFinish(true, leftovers, bonus, refill);
        }

        if (!Board.HasMove())
        {
            Hold(ClusterPending.Reshuffle, reshuffleDelay);
        }

        return default;
    }

    private void AddCharge(int amount)
    {
        charge = Math.Min(ClusterPace.ChargeNeeded, charge + amount);
    }

    private void Hold(ClusterPending pending, float seconds)
    {
        Pending = pending;
        pendingSeconds = seconds;
    }

    private void Resolve()
    {
        var pending = Pending;
        Pending = ClusterPending.None;
        pendingSeconds = 0f;
        if (pending == ClusterPending.Reshuffle)
        {
            Reshuffles++;
            Board.Reshuffle(ref random);
            busySeconds = ReshuffleSeconds;
            events |= ClusterEvents.Reshuffled;
            return;
        }

        Level++;
        StartBoard();
    }

    private void StartBoard()
    {
        Board.Fill(ClusterPace.ColumnsFor(Level), ClusterPace.RowsFor(Level), ClusterPace.ColorsFor(Level),
            ref random);
        busySeconds = BoardDropSeconds;
        events |= ClusterEvents.BoardStarted;
    }
}
