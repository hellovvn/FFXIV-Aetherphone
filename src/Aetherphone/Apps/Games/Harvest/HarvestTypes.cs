namespace Aetherphone.Apps.Games.Harvest;

internal enum HarvestMaterial : byte
{
    Ore,
    Log,
    Crystal,
    Shard,
}

internal enum HarvestRole : byte
{
    Required,
    Bonus,
    Mirage,
    Spark,
}

internal enum HarvestHazardKind : byte
{
    Bird,
    Boulder,
}

internal enum HarvestBiome : byte
{
    Meadow,
    Highlands,
    Mire,
    Glacier,
    Storm,
    Dunes,
}

internal enum HarvestEventKind : byte
{
    Catch,
    Miss,
    MirageCaught,
    Stunned,
    Fizzle,
    Hit,
    Dodge,
    Jump,
    Land,
    ComboUp,
    FeverStart,
    FeverEnd,
    BiomeStart,
    HazardWarned,
    GameOver,
}

internal struct HarvestDrop
{
    public float X;
    public float Y;
    public float PreviousX;
    public float PreviousY;
    public float LandX;
    public float Drift;
    public float Speed;
    public float Hold;
    public float Seed;
    public int Serial;
    public HarvestMaterial Material;
    public HarvestRole Role;
    public bool Active;
}

internal struct HarvestHazard
{
    public float X;
    public float PreviousX;
    public float Height;
    public float Velocity;
    public float Warning;
    public float WarningTotal;
    public float Spin;
    public HarvestHazardKind Kind;
    public bool Active;
    public bool Struck;
    public bool Passed;
}

internal struct HarvestPlan
{
    public float SpawnTime;
    public float LandTime;
    public float LandX;
    public float Height;
    public float Drift;
    public float Speed;
    public float Hold;
    public HarvestMaterial Material;
    public HarvestRole Role;
}

internal struct HarvestHazardPlan
{
    public float WarnTime;
    public float EnterTime;
    public float Speed;
    public int Direction;
    public HarvestHazardKind Kind;
}

internal readonly struct HarvestEvent
{
    public readonly float X;
    public readonly float Y;
    public readonly int Value;
    public readonly int Detail;
    public readonly HarvestEventKind Kind;
    public readonly HarvestMaterial Material;

    public HarvestEvent(HarvestEventKind kind, float x, float y, int value, int detail, HarvestMaterial material)
    {
        Kind = kind;
        X = x;
        Y = y;
        Value = value;
        Detail = detail;
        Material = material;
    }
}

internal readonly struct HarvestInput
{
    public readonly float Axis;
    public readonly float TargetX;
    public readonly bool HasTarget;
    public readonly bool Jump;
    public readonly bool Duck;

    public HarvestInput(float axis, bool hasTarget, float targetX, bool jump, bool duck)
    {
        Axis = axis;
        HasTarget = hasTarget;
        TargetX = targetX;
        Jump = jump;
        Duck = duck;
    }

    public static HarvestInput None => default;

    public static HarvestInput Keys(float axis, bool jump, bool duck) => new(axis, false, 0f, jump, duck);

    public static HarvestInput Pointer(float targetX, bool jump, bool duck) => new(0f, true, targetX, jump, duck);
}
