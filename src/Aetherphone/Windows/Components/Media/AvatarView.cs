using Aetherphone.Core.Animation;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AvatarView
{
    public const float MinFrameRadius = 15f;

    private const float FadeDurationSeconds = 0.34f;
    private const float PulsePeriodSeconds = 1.15f;
    private const float PulseFloor = 0.72f;
    private const float SettleOvershoot = 0.06f;
    // Measured on Lodestone full-body portraits (640x873): a 42% wide window centered 23% down frames
    // head and shoulders for every race, Lalafell included.
    private const float FaceSpanOfWidth = 0.42f;
    private const float FaceCenterOfHeight = 0.23f;
    private const float FullBodyAspect = 640f / 873f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, float> fadeByKey = new(StringComparer.Ordinal);

    public static void DrawRemote(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, string name,
        string world, string? avatarUrl, RemoteImageCache images, LodestoneService lodestone, float monogramScale,
        int segments, float alpha = 1f, FrameStyle? frame = null)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            Draw(drawList, center, radius, theme.Accent, Initials.Of(name), monogramScale,
                lodestone.Avatar(name, world, radius * 2f), segments, alpha);
            DrawFrame(drawList, center, radius, images, frame, alpha);
            return;
        }

        var handle = images.Avatar(avatarUrl, radius * 2f);
        if (handle.Texture is { } texture)
        {
            var backdrop = theme.SurfaceMuted with { W = theme.SurfaceMuted.W * alpha };
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(backdrop), segments);
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            var corner = new Vector2(radius, radius);
            var tint = ((uint)(alpha * 255f) << 24) | 0x00FFFFFFu;
            drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1, tint,
                radius, ImDrawFlags.RoundCornersAll);
            DrawFrame(drawList, center, radius, images, frame, alpha);
            return;
        }

        Draw(drawList, center, radius, theme.Accent, Initials.Of(name), monogramScale, handle, segments, alpha);
        DrawFrame(drawList, center, radius, images, frame, alpha);
    }

    public static void DrawFrame(ImDrawListPtr drawList, Vector2 center, float radius, RemoteImageCache images,
        FrameStyle? frame, float alpha = 1f)
    {
        if (frame is null || frame.AssetUrl.Length == 0 || radius < MinFrameRadius * UiScale.Current)
        {
            return;
        }

        var half = radius * frame.Scale;
        if (images.SizedAt(frame.AssetUrl, half * 2f, ImGui.GetTime()) is not { } texture)
        {
            return;
        }

        var corner = new Vector2(half, half);
        var tint = ((uint)(alpha * 255f) << 24) | 0x00FFFFFFu;
        drawList.AddImage(texture.Handle, center - corner, center + corner, Vector2.Zero, Vector2.One, tint);
    }

    public static float Reserve(FrameStyle? frame, float radius)
    {
        if (frame is null || frame.AssetUrl.Length == 0 || radius < MinFrameRadius * UiScale.Current)
        {
            return 0f;
        }

        return radius * (frame.Scale - 1f);
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 baseColor, string monogram,
        float monogramScale, AvatarHandle handle, int segments, float alpha = 1f) =>
        Draw(drawList, center, radius, baseColor, monogram, monogramScale, handle, segments, alpha, false);

    public static void DrawFullBodyFace(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 baseColor,
        string monogram, float monogramScale, AvatarHandle handle, int segments) =>
        Draw(drawList, center, radius, baseColor, monogram, monogramScale, handle, segments, 1f, true);

    public static float FullBodyFacePixels(float drawnPixels) =>
        drawnPixels / (FaceSpanOfWidth * FullBodyAspect);

    private static (Vector2 Uv0, Vector2 Uv1) FaceCrop(Vector2 imageSize)
    {
        if (imageSize.X <= 0f || imageSize.Y <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var spanX = FaceSpanOfWidth;
        var spanY = spanX * imageSize.X / imageSize.Y;
        if (spanY > 1f)
        {
            spanX /= spanY;
            spanY = 1f;
        }

        var left = 0.5f - spanX * 0.5f;
        var top = Math.Clamp(FaceCenterOfHeight - spanY * 0.5f, 0f, 1f - spanY);
        return (new Vector2(left, top), new Vector2(left + spanX, top + spanY));
    }

    private static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 baseColor, string monogram,
        float monogramScale, AvatarHandle handle, int segments, float alpha, bool fullBodyFace)
    {
        var circleColor = handle.State == AvatarLoadState.Loading ? Pulse(baseColor) : baseColor;
        circleColor.W *= alpha;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(circleColor), segments);
        var fade = handle.Texture is not null && handle.Key.Length > 0 ? Advance(handle.Key) : 0f;
        if (fade < 1f)
        {
            Typography.DrawCentered(drawList, center, monogram, White with { W = (1f - fade) * alpha }, monogramScale);
        }

        if (handle.Texture is { } texture && fade > 0f)
        {
            var settledRadius = radius * (1f + SettleOvershoot * (1f - fade));
            var corner = new Vector2(settledRadius, settledRadius);
            var tint = ((uint)(fade * alpha * 255f) << 24) | 0x00FFFFFFu;
            var (uv0, uv1) = fullBodyFace ? FaceCrop(texture.Size) : (Vector2.Zero, Vector2.One);
            drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1, tint, settledRadius);
        }
    }

    private static Vector4 Pulse(Vector4 color)
    {
        var milliseconds = Environment.TickCount64 % (long)(PulsePeriodSeconds * 1000f);
        var phase = milliseconds / (PulsePeriodSeconds * 1000f);
        var wave = 0.5f + 0.5f * MathF.Sin(phase * MathF.PI * 2f);
        var brightness = PulseFloor + (1f - PulseFloor) * wave;
        return new Vector4(color.X * brightness, color.Y * brightness, color.Z * brightness, color.W);
    }

    private static float Advance(string key)
    {
        fadeByKey.TryGetValue(key, out var progress);
        if (progress < 1f)
        {
            progress = MathF.Min(1f, progress + ImGui.GetIO().DeltaTime / FadeDurationSeconds);
            fadeByKey[key] = progress;
        }

        return progress;
    }
}
