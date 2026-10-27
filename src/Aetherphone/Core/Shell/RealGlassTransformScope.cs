using Glasswork.Backend;

namespace Aetherphone.Core.Shell;

internal readonly ref struct RealGlassTransformScope
{
    private readonly GlassTransformStack? stack;
    private readonly GlassSuspendScope suspension;

    public RealGlassTransformScope(GlassTransformStack? stack, GlassSuspendScope suspension)
    {
        this.stack = stack;
        this.suspension = suspension;
    }

    public void Dispose()
    {
        stack?.Pop();
        suspension.Dispose();
    }
}
