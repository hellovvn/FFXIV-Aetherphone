using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// The pieces every full-screen post video draws over its picture, shared by the Reels viewer and Chirper's player.
internal static class ReelChrome
{
    public const float ChipHeight = 32f;

    private const float ChipPadX = 14f;
    private const float ProgressHeight = 2f;
    private const float CoverSlack = 1.15f;

    public static readonly Vector4 ChipFill = new(0f, 0f, 0f, 0.6f);

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Track = new(1f, 1f, 1f, 0.28f);
    private static readonly TextStyle ChipStyle = TextStyles.SubheadlineEmphasized;

    public static Rect Chip(ImDrawListPtr drawList, Vector2 center, string text)
    {
        var scale = UiScale.Current;
        var height = ChipHeight * scale;
        var width = Typography.Measure(text, ChipStyle).X + ChipPadX * 2f * scale;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = min + new Vector2(width, height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(ChipFill), height * 0.5f);
        Typography.DrawCentered(drawList, center, text, White, ChipStyle);
        return new Rect(min, max);
    }

    // A picture close enough to the frame's shape fills it edge to edge; anything wider sits letterboxed.
    public static bool FillsFrame(float width, float height, Rect rect)
    {
        if (width <= 0f || height <= 0f || rect.Height <= 0f)
        {
            return true;
        }

        return width / height <= rect.Width / rect.Height * CoverSlack;
    }

    public static Rect Contain(float width, float height, Rect rect)
    {
        var aspect = width / height;
        if (aspect >= rect.Width / rect.Height)
        {
            var fittedHeight = rect.Width / aspect;
            var top = rect.Center.Y - fittedHeight * 0.5f;
            return new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, top + fittedHeight));
        }

        var fittedWidth = rect.Height * aspect;
        var left = rect.Center.X - fittedWidth * 0.5f;
        return new Rect(new Vector2(left, rect.Min.Y), new Vector2(left + fittedWidth, rect.Max.Y));
    }

    public static void DrawTexture(ImDrawListPtr drawList, ImTextureID texture, float width, float height, Rect target,
        bool fills)
    {
        if (!fills)
        {
            drawList.AddImage(texture, target.Min, target.Max);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(width, height, target.Width, target.Height);
        drawList.AddImage(texture, target.Min, target.Max, uv0, uv1);
    }

    public static void Progress(ImDrawListPtr drawList, Rect overlay, double positionSeconds, double durationSeconds)
    {
        var fraction = durationSeconds > 0d ? Math.Clamp((float)(positionSeconds / durationSeconds), 0f, 1f) : 0f;
        var top = overlay.Max.Y - ProgressHeight * UiScale.Current;
        drawList.AddRectFilled(new Vector2(overlay.Min.X, top), overlay.Max, ImGui.GetColorU32(Track));
        if (fraction <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(new Vector2(overlay.Min.X, top),
            new Vector2(overlay.Min.X + overlay.Width * fraction, overlay.Max.Y), ImGui.GetColorU32(White));
    }
}
