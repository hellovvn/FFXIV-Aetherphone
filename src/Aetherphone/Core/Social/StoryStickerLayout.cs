namespace Aetherphone.Core.Social;

internal static class StoryStickerLayout
{
    public const float MinScale = 0.5f;
    public const float MaxScale = 2f;
    public const float WheelStep = 0.1f;
    public const float DefaultX = 0.5f;
    public const float DefaultY = 0.42f;
    public const float CascadeStep = 0.06f;

    // A sticker reads at its design size on a photo this many unscaled pixels wide, and grows or shrinks with the photo.
    public const float ReferencePhotoWidth = 300f;

    public static Rect CoverRect(Rect stage, float photoAspect)
    {
        if (stage.Width <= 0f || stage.Height <= 0f || photoAspect <= 0f)
        {
            return stage;
        }

        var stageAspect = stage.Width / stage.Height;
        var size = stageAspect > photoAspect
            ? new Vector2(stage.Width, stage.Width / photoAspect)
            : new Vector2(stage.Height * photoAspect, stage.Height);
        var min = stage.Center - size * 0.5f;
        return new Rect(min, min + size);
    }

    public static float Zoom(Rect photo, float uiScale, float stickerScale)
    {
        if (uiScale <= 0f)
        {
            return 0f;
        }

        return photo.Width / (ReferencePhotoWidth * uiScale) * ClampScale(stickerScale);
    }

    public static float ClampScale(float scale)
    {
        return float.IsFinite(scale) ? Math.Clamp(scale, MinScale, MaxScale) : 1f;
    }

    public static float Wheel(float scale, float wheel)
    {
        return ClampScale(MathF.Round((scale + wheel * WheelStep) * 100f) / 100f);
    }

    public static Vector2 Drag(Rect photo, Vector2 startNormalized, Vector2 pointerDelta)
    {
        var size = photo.Size;
        if (size.X <= 0f || size.Y <= 0f)
        {
            return startNormalized;
        }

        return new Vector2(
            Math.Clamp(startNormalized.X + pointerDelta.X / size.X, 0f, 1f),
            Math.Clamp(startNormalized.Y + pointerDelta.Y / size.Y, 0f, 1f));
    }

    public static Vector2 Spawn(int existingCount)
    {
        var step = existingCount % 5;
        return new Vector2(DefaultX, DefaultY + step * CascadeStep);
    }
}
