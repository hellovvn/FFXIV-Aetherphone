using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glasswork.Backend;

namespace Aetherphone.Core.Shell;

internal static class RealGlass
{
    private const string LogPrefix = "[RealGlass] ";

    private static readonly GlassTransformStack Transforms = new();
    private static IDalamudPluginInterface? pluginInterface;
    private static IPluginLog? pluginLog;
    private static Configuration? configuration;
    private static GlassLayer? layer;
    private static bool active;

    public static void Initialize(IDalamudPluginInterface host, IPluginLog log, Configuration settings)
    {
        Dispose();
        pluginInterface = host;
        pluginLog = log;
        configuration = settings;
        layer = new GlassLayer(() => host.UiBuilder.DeviceHandle, Log);
        host.UiBuilder.ResizeBuffers += OnResizeBuffers;
    }

    public static void BeginFrame()
    {
        if (layer is null || pluginInterface is null)
        {
            return;
        }

        if (configuration is not { RealGlass: true })
        {
            if (active)
            {
                active = false;
                layer.ReleaseResources();
            }

            return;
        }

        active = true;
        Transforms.Clear();
        layer.ReducedMotion = pluginInterface.UiBuilder.ShouldUseReducedMotion;
        layer.PointerLight = configuration.GlassTuning.CursorLight;
        layer.BeginFrame();
    }

    public static bool Ready => active && layer is { } glass && glass.CanRecord();

    public static float Amount => configuration?.GlassAmount ?? GlassAmountCurve.Neutral;

    public static GlassGroupScope Group() => active && layer is { } glass ? glass.Group() : default;

    public static GlassSuspendScope Suspend(bool transformed = true) =>
        transformed && active && layer is { } glass ? glass.Suspend() : default;

    public static RealGlassTransformScope Transform(in LayerTransform transform)
    {
        if (!active || layer is not { } glass || transform.IsIdentity)
        {
            return default;
        }

        return Transforms.TryPush(in transform)
            ? new RealGlassTransformScope(Transforms, default)
            : new RealGlassTransformScope(null, glass.Suspend());
    }

    public static bool TryDraw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, RealGlassKind kind,
        GlassTone tone, bool onWallpaper, Vector4 accent, float opacity)
    {
        if (!active || configuration is not { } settings || layer is not { } glass || !glass.CanRecord())
        {
            return false;
        }

        var alpha = Transforms.Alpha;
        if (alpha <= 0f)
        {
            return true;
        }

        var scale = Transforms.Scale;
        var placed = Transforms.Map(new Rect(min, max));
        var shape = RealGlassMaterials.Shape(placed.Min, placed.Max, radius * scale);
        if (shape.HalfSize.X <= 0f || shape.HalfSize.Y <= 0f)
        {
            return false;
        }

        var material = RealGlassMaterials.Build(kind, tone, onWallpaper, accent, opacity * alpha, settings.GlassAmount,
            settings.GlassTuning, shape.Radius, shape.HalfSize * 2f).Scaled(UiScale.Current * scale);
        if (Transforms.Depth == 0)
        {
            return glass.TryDraw(drawList, in shape, in material);
        }

        var clip = new Rect(ImGui.GetClipRectMin(drawList), ImGui.GetClipRectMax(drawList));
        var mappedClip = Transforms.Map(clip);
        drawList.PushClipRect(Vector2.Min(clip.Min, mappedClip.Min), Vector2.Max(clip.Max, mappedClip.Max), false);
        var recorded = glass.TryDraw(drawList, in shape, in material);
        drawList.PopClipRect();
        return recorded;
    }

    public static void Dispose()
    {
        if (pluginInterface is not null)
        {
            pluginInterface.UiBuilder.ResizeBuffers -= OnResizeBuffers;
            pluginInterface = null;
        }

        layer?.Dispose();
        layer = null;
        active = false;
        configuration = null;
        pluginLog = null;
    }

    private static void OnResizeBuffers() => layer?.ReleaseResources();

    private static void Log(string message, Exception? exception)
    {
        if (exception is null)
        {
            pluginLog?.Information(string.Concat(LogPrefix, message));
            return;
        }

        pluginLog?.Warning(exception, string.Concat(LogPrefix, message));
    }
}
