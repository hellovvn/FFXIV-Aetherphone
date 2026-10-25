using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum GuideOrigin : byte
{
    BeforePlay,
    Intro,
    Pause,
}

internal enum GuideExit : byte
{
    None,
    Play,
    Closed,
}

internal sealed class StageGuide
{
    private const float VeilAlpha = 0.92f;
    private const float FadeSeconds = 0.12f;
    private const float ActiveThreshold = 0.9f;
    private const float PageSpeed = 3.2f;
    private const float PortraitArtAspect = 1f;
    private const float LandscapeArtAspect = 1.2f;
    private const float LandscapeArtShare = 0.56f;
    private const float LandscapeColumnWidth = 400f;
    private const float MinPortraitArtShare = 0.75f;
    private const float ArtInset = 12f;
    private const float TextMaxWidth = 320f;
    private const float BodyAlpha = 0.82f;
    private const float ButtonHeight = 46f;
    private const float ButtonMaxWidth = 168f;
    private const float DotRadius = 3.5f;
    private const float DotActiveWidth = 18f;
    private const float DotGap = 8f;
    private const float LiftDistance = 10f;
    private const float SlideDistance = 28f;
    private const float SwipeDistance = 36f;
    private const string ArtSurfaceId = "stage.guide.art";
    private static readonly TextStyle CaptionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle TitleStyle = TextStyles.Title2;
    private static readonly TextStyle BodyStyle = TextStyles.Callout;
    private static readonly TextStyle CompactTitleStyle = TextStyles.Title3;
    private static readonly TextStyle CompactBodyStyle = TextStyles.Subheadline;

    private GameGuide? guide;
    private Spring veil;
    private GuideOrigin origin;
    private int page;
    private int previousPage = -1;
    private int travel;
    private float seconds;
    private float pageIn;
    private float swipeStartX;
    private bool swiping;
    private bool open;

    public bool IsOpen => open;

    public GuideOrigin Origin => origin;

    public void Open(GameGuide next, GuideOrigin from)
    {
        guide = next;
        origin = from;
        open = true;
        swiping = false;
        ShowPage(0, 0);
    }

    public void Close()
    {
        open = false;
        swiping = false;
    }

    public void Reset()
    {
        open = false;
        swiping = false;
        guide = null;
        veil.SnapTo(0f);
    }

    public GuideExit Draw(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var raw = context.RawDeltaSeconds;
        veil.Step(open ? 1f : 0f, FadeSeconds, raw);
        var alpha = Math.Clamp(veil.Value, 0f, 1f);
        if ((!open && alpha <= 0.01f) || guide is null || guide.Count == 0)
        {
            return GuideExit.None;
        }

        seconds += raw;
        pageIn = GameJuice.Advance(pageIn, raw, PageSpeed);
        var full = context.Full;
        var dim = open && origin != GuideOrigin.Pause
            ? Easing.Lerp(StageIntro.IdleDim, VeilAlpha, alpha)
            : VeilAlpha * alpha;
        Material.Veil(drawList, full.Min, full.Max, dim);
        var frame = Frame(full, scale);
        var layout = full.IsLandscape() ? LandscapeLayout(frame, scale) : PortraitLayout(frame, scale);
        var eased = Easing.EaseOutCubic(pageIn);
        var offset = travel == 0
            ? new Vector2(0f, (1f - eased) * LiftDistance * scale)
            : new Vector2(travel * (1f - eased) * SlideDistance * scale, 0f);
        ref readonly var current = ref guide.Page(page);
        DrawArt(drawList, layout.Art, current.Art, accent, alpha, eased * alpha, offset, scale);
        DrawText(drawList, in layout, in current, eased * alpha, offset, scale);
        var interactive = open && alpha >= ActiveThreshold;
        var exit = DrawFooter(drawList, layout.Footer, accent, context.Theme, alpha, eased, interactive, scale);
        if (exit != GuideExit.None || !interactive)
        {
            return exit;
        }

        var gesture = ReadArtGesture(layout.Art, scale);
        if (gesture > 0 && page < guide.Count - 1)
        {
            UiFeedback.PlayTap(UiSound.Tap);
            return Next();
        }

        if (gesture < 0 && page > 0)
        {
            UiFeedback.PlayTap(UiSound.Tap);
            return Back();
        }

        return ReadKeys();
    }

    private static Rect Frame(Rect full, float scale)
    {
        var side = (StageLayout.SafeSide + Metrics.Space.Xxs) * scale;
        return new Rect(new Vector2(full.Min.X + side, full.Min.Y + StageLayout.ChromeBand * scale),
            new Vector2(full.Max.X - side, full.Max.Y - (StageLayout.SafeBottom + Metrics.Space.Xxs) * scale));
    }

    private GuideLayout PortraitLayout(Rect frame, float scale)
    {
        var footerHeight = FooterHeight(scale);
        var footer = new Rect(new Vector2(frame.Min.X, frame.Max.Y - footerHeight), frame.Max);
        var gap = Metrics.Space.Xl * scale;
        var region = new Rect(frame.Min, new Vector2(frame.Max.X, footer.Min.Y - gap));
        var textWidth = MathF.Min(region.Width, TextMaxWidth * scale);
        var room = region.Height - gap - region.Width * MinPortraitArtShare;
        var compact = ReservedTextHeight(textWidth, false, scale) > room;
        var textHeight = ReservedTextHeight(textWidth, compact, scale);
        var artHeight = MathF.Min(region.Width / PortraitArtAspect,
            MathF.Max(0f, region.Height - textHeight - gap));
        var artWidth = MathF.Min(region.Width, artHeight * PortraitArtAspect);
        var stack = artHeight + gap + textHeight;
        var top = region.Min.Y + MathF.Max(0f, (region.Height - stack) * 0.5f);
        var art = new Rect(new Vector2(region.Center.X - artWidth * 0.5f, top),
            new Vector2(region.Center.X + artWidth * 0.5f, top + artHeight));
        var text = new Rect(new Vector2(region.Min.X, art.Max.Y + gap),
            new Vector2(region.Max.X, art.Max.Y + gap + textHeight));
        return new GuideLayout(art, text, footer, textWidth, compact);
    }

    private GuideLayout LandscapeLayout(Rect frame, float scale)
    {
        var gap = Metrics.Space.Xxl * scale;
        var artHeight = MathF.Min(frame.Height, frame.Width * LandscapeArtShare / LandscapeArtAspect);
        var artWidth = artHeight * LandscapeArtAspect;
        var columnWidth = MathF.Min(frame.Width - artWidth - gap, LandscapeColumnWidth * scale);
        var left = frame.Center.X - (artWidth + gap + columnWidth) * 0.5f;
        var top = frame.Center.Y - artHeight * 0.5f;
        var art = new Rect(new Vector2(left, top), new Vector2(left + artWidth, top + artHeight));
        var column = new Rect(new Vector2(art.Max.X + gap, art.Min.Y),
            new Vector2(art.Max.X + gap + columnWidth, art.Max.Y));
        var footer = new Rect(new Vector2(column.Min.X, column.Max.Y - FooterHeight(scale)), column.Max);
        var room = footer.Min.Y - Metrics.Space.Lg * scale - column.Min.Y;
        var compact = ReservedTextHeight(columnWidth, false, scale) > room;
        var textHeight = ReservedTextHeight(columnWidth, compact, scale);
        var textTop = column.Min.Y + MathF.Max(0f, (room - textHeight) * 0.5f);
        var text = new Rect(new Vector2(column.Min.X, textTop), new Vector2(column.Max.X, textTop + textHeight));
        return new GuideLayout(art, text, footer, columnWidth, compact);
    }

    private static float FooterHeight(float scale) =>
        (ButtonHeight + Metrics.Space.Lg + DotRadius * 2f) * scale;

    private float ReservedTextHeight(float width, bool compact, float scale)
    {
        var tallest = 0f;
        for (var index = 0; index < guide!.Count; index++)
        {
            tallest = MathF.Max(tallest, TextHeight(in guide.Page(index), width, compact, scale));
        }

        return tallest;
    }

    private static float TextHeight(in GuidePage source, float width, bool compact, float scale) =>
        Typography.LineHeight(CaptionStyle) + Metrics.Space.Sm * scale * 2f +
        Typography.MeasureWrappedBlock(Loc.T(source.Title), TitleOf(compact), width).Y +
        Typography.MeasureWrappedBlock(Loc.T(source.Body), BodyOf(compact), width).Y;

    private static TextStyle TitleOf(bool compact) => compact ? CompactTitleStyle : TitleStyle;

    private static TextStyle BodyOf(bool compact) => compact ? CompactBodyStyle : BodyStyle;

    private void ShowPage(int next, int direction)
    {
        previousPage = direction == 0 ? -1 : page;
        page = next;
        travel = direction;
        seconds = 0f;
        pageIn = 0f;
    }

    private void DrawArt(ImDrawListPtr drawList, Rect art, GuideArt draw, Vector4 accent, float alpha, float phase,
        Vector2 offset, float scale)
    {
        var plateVertex = drawList.VtxBuffer.Size;
        BoardPlate.Draw(drawList, art, BoardPlate.Radius * scale, scale, accent, StageInk.Light);
        LayerCompositor.Fade(drawList, plateVertex, alpha);
        var inner = art.Inset(ArtInset * scale);
        var artVertex = drawList.VtxBuffer.Size;
        drawList.PushClipRect(inner.Min, inner.Max, true);
        draw(drawList, inner.Translate(offset), seconds, accent, scale);
        drawList.PopClipRect();
        LayerCompositor.Fade(drawList, artVertex, phase);
    }

    private static void DrawText(ImDrawListPtr drawList, in GuideLayout layout, in GuidePage current, float phase,
        Vector2 offset, float scale)
    {
        var width = layout.TextWidth;
        var titleStyle = TitleOf(layout.Compact);
        var bodyStyle = BodyOf(layout.Compact);
        var title = Loc.T(current.Title);
        var bodyText = Loc.T(current.Body);
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var titleHeight = Typography.MeasureWrappedBlock(title, titleStyle, width).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(bodyText, bodyStyle, width).Y;
        var gap = Metrics.Space.Sm * scale;
        var top = layout.Text.Min.Y + offset.Y;
        var centerX = layout.Text.Center.X + offset.X;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + captionHeight * 0.5f), Loc.T(L.Stage.HowToPlay),
            StageInks.Muted with { W = phase }, CaptionStyle);
        top += captionHeight + gap;
        Typography.DrawWrappedCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f), title,
            StageInks.Strong with { W = phase }, titleStyle, width);
        top += titleHeight + gap;
        Typography.DrawWrappedCentered(drawList, new Vector2(centerX, top + bodyHeight * 0.5f), bodyText,
            StageInks.Strong with { W = BodyAlpha * phase }, bodyStyle, width);
    }

    private GuideExit DrawFooter(ImDrawListPtr drawList, Rect footer, Vector4 accent, PhoneTheme theme,
        float alpha, float eased, bool interactive, float scale)
    {
        var count = guide!.Count;
        var buttonHeight = ButtonHeight * scale;
        var buttonsCenterY = footer.Max.Y - buttonHeight * 0.5f;
        DrawDots(drawList, new Vector2(footer.Center.X, footer.Min.Y + DotRadius * scale), count, accent, alpha,
            eased, scale);
        if (!interactive)
        {
            return GuideExit.None;
        }

        var gap = Metrics.Space.Md * scale;
        var width = MathF.Min(ButtonMaxWidth * scale, (footer.Width - gap) * 0.5f);
        var size = new Vector2(width, buttonHeight);
        var secondary = Palette.Mix(StageInks.Surface, accent, 0.18f);
        var leftCenter = new Vector2(footer.Center.X - gap * 0.5f - width * 0.5f, buttonsCenterY);
        var rightCenter = new Vector2(footer.Center.X + gap * 0.5f + width * 0.5f, buttonsCenterY);
        var last = page == count - 1;
        if (GameHud.Button(leftCenter, size, Loc.T(page == 0 ? SkipLabel() : L.Stage.GuideBack), secondary, theme))
        {
            return page == 0 ? Leave() : Back();
        }

        if (GameHud.Button(rightCenter, size, Loc.T(last ? FinishLabel() : L.Common.Next), accent, theme))
        {
            return last ? Finish() : Next();
        }

        return GuideExit.None;
    }

    private void DrawDots(ImDrawListPtr drawList, Vector2 center, int count, Vector4 accent, float alpha, float eased,
        float scale)
    {
        var radius = DotRadius * scale;
        var active = DotActiveWidth * scale;
        var gap = DotGap * scale;
        var x = center.X - (active + (count - 1) * (radius * 2f + gap)) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var grow = index == page ? eased : index == previousPage ? 1f - eased : 0f;
            var width = radius * 2f + (active - radius * 2f) * grow;
            var ink = Palette.Mix(StageInks.Muted, accent, grow);
            Squircle.Fill(drawList, new Vector2(x, center.Y - radius), new Vector2(x + width, center.Y + radius),
                radius, ImGui.GetColorU32(ink with { W = ink.W * alpha }));
            x += width + gap;
        }
    }

    private int ReadArtGesture(Rect art, float scale)
    {
        var hovered = PressSurface.Claim(ArtSurfaceId, art, out var pressed);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (pressed)
        {
            swiping = true;
            swipeStartX = ImGui.GetMousePos().X;
        }

        if (!swiping || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return 0;
        }

        swiping = false;
        var distance = ImGui.GetMousePos().X - swipeStartX;
        if (MathF.Abs(distance) >= SwipeDistance * scale)
        {
            return distance < 0f ? 1 : -1;
        }

        return hovered ? 1 : 0;
    }

    private LocString SkipLabel() => origin == GuideOrigin.BeforePlay ? L.Stage.GuideSkip : L.Common.Close;

    private LocString FinishLabel() => origin == GuideOrigin.Pause ? L.Stage.GuideDone : L.Games.Play;

    private GuideExit ReadKeys()
    {
        if (GameInput.Pressed(ImGuiKey.Escape))
        {
            open = false;
            return GuideExit.Closed;
        }

        var last = page == guide!.Count - 1;
        if (GameInput.Pressed(ImGuiKey.LeftArrow, ImGuiKey.A) && page > 0)
        {
            return Back();
        }

        if (GameInput.Pressed(ImGuiKey.RightArrow, ImGuiKey.D) && !last)
        {
            return Next();
        }

        if (!GameInput.Pressed(ImGuiKey.Enter, ImGuiKey.Space))
        {
            return GuideExit.None;
        }

        return last ? Finish() : Next();
    }

    private GuideExit Next()
    {
        ShowPage(Math.Min(page + 1, guide!.Count - 1), 1);
        return GuideExit.None;
    }

    private GuideExit Back()
    {
        if (page > 0)
        {
            ShowPage(page - 1, -1);
        }

        return GuideExit.None;
    }

    private GuideExit Leave()
    {
        open = false;
        return origin == GuideOrigin.BeforePlay ? GuideExit.Play : GuideExit.Closed;
    }

    private GuideExit Finish()
    {
        open = false;
        return origin == GuideOrigin.Pause ? GuideExit.Closed : GuideExit.Play;
    }

    private readonly record struct GuideLayout(Rect Art, Rect Text, Rect Footer, float TextWidth, bool Compact);
}
