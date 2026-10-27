namespace Aetherphone.Apps.Games.Harvest;

internal struct HarvestPlayer
{
    public const float HalfWidth = 4.5f;
    public const float CatchHalfWidth = 8f;
    public const float StandHeight = 15.5f;
    public const float DuckHeight = 8f;
    public const float StandRim = 13f;
    public const float DuckRim = 8.5f;
    public const float BasketDepth = 5f;
    public const float FootClearance = 1f;
    public const float MaxSpeed = 72f;
    public const float DuckSpeedFactor = 0.45f;
    public const float Acceleration = 720f;
    public const float IceAcceleration = 180f;
    public const float PointerGain = 11f;
    public const float JumpVelocity = 105f;
    public const float Gravity = 320f;
    public const float FastFallGravity = 760f;
    public const float JumpBufferSeconds = 0.12f;
    public const float CoyoteSeconds = 0.08f;
    public const float MinX = HalfWidth;
    public const float MaxX = HarvestRules.Width - HalfWidth;
    private const float FacingThreshold = 6f;

    public float X;
    public float Y;
    public float VelocityX;
    public float VelocityY;
    public float PreviousX;
    public float PreviousY;
    public float Stun;
    public float Invulnerable;
    public float JumpBuffer;
    public float Coyote;
    public bool Ducking;
    public bool Grounded;
    public int Facing;

    public static HarvestPlayer Create(float x) => new()
    {
        X = x,
        PreviousX = x,
        Grounded = true,
        Facing = 1,
    };

    public readonly float Height => Ducking && Grounded ? DuckHeight : StandHeight;

    public readonly float Rim => Y + (Ducking ? DuckRim : StandRim);

    public readonly bool Stunned => Stun > 0f;

    public bool Step(in HarvestInput input, bool icy, float deltaSeconds)
    {
        PreviousX = X;
        PreviousY = Y;
        Stun = MathF.Max(0f, Stun - deltaSeconds);
        Invulnerable = MathF.Max(0f, Invulnerable - deltaSeconds);
        var controlled = !Stunned;
        Ducking = controlled && input.Duck;
        if (controlled && input.Jump)
        {
            JumpBuffer = JumpBufferSeconds;
        }
        else
        {
            JumpBuffer = MathF.Max(0f, JumpBuffer - deltaSeconds);
        }

        var wanted = controlled ? WantedVelocity(input) : 0f;
        if (Ducking && Grounded)
        {
            wanted *= DuckSpeedFactor;
        }

        var acceleration = icy ? IceAcceleration : Acceleration;
        VelocityX = MoveTowards(VelocityX, wanted, acceleration * deltaSeconds);
        X += VelocityX * deltaSeconds;
        if (X < MinX)
        {
            X = MinX;
            VelocityX = MathF.Max(0f, VelocityX);
        }
        else if (X > MaxX)
        {
            X = MaxX;
            VelocityX = MathF.Min(0f, VelocityX);
        }

        if (VelocityX > FacingThreshold)
        {
            Facing = 1;
        }
        else if (VelocityX < -FacingThreshold)
        {
            Facing = -1;
        }

        return StepVertical(controlled, deltaSeconds);
    }

    public void Knock(int direction)
    {
        VelocityX = direction * MaxSpeed * 0.6f;
        if (!Grounded)
        {
            return;
        }

        VelocityY = JumpVelocity * 0.45f;
        Grounded = false;
        Coyote = 0f;
    }

    private bool StepVertical(bool controlled, float deltaSeconds)
    {
        var jumped = false;
        if (controlled && JumpBuffer > 0f && (Grounded || Coyote > 0f))
        {
            VelocityY = JumpVelocity;
            Grounded = false;
            Coyote = 0f;
            JumpBuffer = 0f;
            jumped = true;
        }

        if (Grounded)
        {
            Coyote = CoyoteSeconds;
            return jumped;
        }

        Coyote = MathF.Max(0f, Coyote - deltaSeconds);
        var gravity = Ducking ? FastFallGravity : Gravity;
        VelocityY -= gravity * deltaSeconds;
        Y += VelocityY * deltaSeconds;
        if (Y > 0f)
        {
            return jumped;
        }

        Y = 0f;
        VelocityY = 0f;
        Grounded = true;
        return jumped;
    }

    private readonly float WantedVelocity(in HarvestInput input)
    {
        if (!input.HasTarget)
        {
            return Math.Clamp(input.Axis, -1f, 1f) * MaxSpeed;
        }

        return Math.Clamp((input.TargetX - X) * PointerGain, -MaxSpeed, MaxSpeed);
    }

    private static float MoveTowards(float value, float target, float maxStep)
    {
        if (MathF.Abs(target - value) <= maxStep)
        {
            return target;
        }

        return value + MathF.Sign(target - value) * maxStep;
    }
}
