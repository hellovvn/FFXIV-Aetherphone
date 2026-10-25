using Aetherphone.Core;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class GifPickerSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float HeaderHeight = 36f;
    private const float SearchHeight = 46f;
    private const float SectionHeight = 26f;
    private const float FooterHeight = 30f;
    private const float GridGap = 6f;
    private const float TileRounding = 10f;
    private const float MinTileAspect = 0.55f;
    private const float MaxTileAspect = 1.6f;
    private const float LoadMoreDistance = 520f;
    private const float CullMargin = 160f;
    private const float SpinnerRadius = 12f;
    private const float HeroSize = 64f;
    private const float MutedAlpha = 0.62f;
    private const float TileFillAlpha = 0.08f;
    private const float HoverAlpha = 0.12f;
    private const int Columns = 2;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly Sheet sheet = new();
    private readonly GifSearch search;
    private readonly RemoteImageCache images;
    private readonly Configuration configuration;
    private readonly FailureSlot failure = new();
    private readonly float[] columnBottoms = new float[Columns];
    private string draft = string.Empty;
    private string seenDraft = string.Empty;
    private bool focusQuery;
    private GifPick? picked;
    private GifItem[] laidOut = Array.Empty<GifItem>();
    private float laidOutWidth = -1f;
    private float[] tileTops = Array.Empty<float>();
    private float[] tileHeights = Array.Empty<float>();
    private byte[] tileColumns = Array.Empty<byte>();
    private float gridHeight;

    public GifPickerSheet(GifSearch search, RemoteImageCache images, Configuration configuration)
    {
        this.search = search;
        this.images = images;
        this.configuration = configuration;
    }

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open()
    {
        draft = string.Empty;
        seenDraft = string.Empty;
        picked = null;
        focusQuery = configuration.KlipyGifsAllowed;
        if (configuration.KlipyGifsAllowed)
        {
            search.Open();
        }

        sheet.Open();
    }

    public void Close()
    {
        picked = null;
        sheet.Close();
    }

    public GifPick? TakePick()
    {
        var taken = picked;
        picked = null;
        return taken;
    }

    public void Draw(Rect screen, in AppSkin ui)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##gifPickerSheet", screen.Size, false, OverlayFlags))
        {
            var veil = SheetMetrics.VeilFor(true);
            var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, ui.Theme,
                SheetDetents.Fitted(screen.Height * SheetMetrics.LargeFraction), veil);
            if (!frame.Visible)
            {
                return;
            }

            if (configuration.KlipyGifsAllowed)
            {
                DrawPicker(in frame, in ui);
            }
            else
            {
                DrawConsent(in frame, in ui);
            }

            sheet.End(in frame);
        }

        if (!sheet.IsOpen)
        {
            search.Close();
        }
    }

    private void DrawConsent(in SheetFrame frame, in AppSkin ui)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var side = Metrics.Space.Xl * scale;
        var buttonHeight = Button.LargeHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var bottom = frame.Panel.Max.Y - Metrics.Size.HomeIndicatorInset * scale;
        var declineRect = new Rect(new Vector2(content.Min.X + side, bottom - buttonHeight),
            new Vector2(content.Max.X - side, bottom));
        var acceptRect = new Rect(new Vector2(declineRect.Min.X, declineRect.Min.Y - gap - buttonHeight),
            new Vector2(declineRect.Max.X, declineRect.Min.Y - gap));
        var body = new Rect(new Vector2(content.Min.X + side, content.Min.Y),
            new Vector2(content.Max.X - side, acceptRect.Min.Y - Metrics.Space.Md * scale));
        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bodyBottom = DrawConsentBody(ImGui.GetWindowDrawList(), origin, width, ink, in ui);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, bodyBottom - origin.Y)));
        }

        var accepted = Button.Draw(frame.DrawList, acceptRect, Loc.T(L.Gifs.ConsentAccept), ui.Ink,
            ButtonStyle.Prominent, id: "gifs.consent.accept", opacity: frame.Opacity);
        var declined = Button.Draw(frame.DrawList, declineRect, Loc.T(L.Gifs.ConsentDecline), ui.Ink,
            ButtonStyle.Gray, id: "gifs.consent.decline", opacity: frame.Opacity);
        if (!frame.Interactive)
        {
            return;
        }

        if (accepted)
        {
            configuration.KlipyGifsAllowed = true;
            configuration.Save();
            focusQuery = true;
            search.Open();
            return;
        }

        if (declined)
        {
            sheet.Close();
        }
    }

    private static float DrawConsentBody(ImDrawListPtr drawList, Vector2 origin, float width, Vector4 ink,
        in AppSkin ui)
    {
        var scale = UiScale.Current;
        var muted = Palette.WithAlpha(ink, ink.W * MutedAlpha);
        var center = origin.X + width * 0.5f;
        var y = origin.Y + Metrics.Space.Sm * scale;
        var hero = HeroSize * scale;
        var heroMin = new Vector2(center - hero * 0.5f, y);
        var heroMax = heroMin + new Vector2(hero, hero);
        Squircle.Fill(drawList, heroMin, heroMax, hero * Metrics.Radius.TileFactor, ImGui.GetColorU32(ui.Accent));
        Typography.DrawCentered(drawList, (heroMin + heroMax) * 0.5f, "GIF", White, TextStyles.Title2);
        y += hero + Metrics.Space.Lg * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Gifs.ConsentTitle), ink,
            TextStyles.Title3, width);
        y += Metrics.Space.Sm * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Gifs.ConsentBody), muted,
            TextStyles.Subheadline, width);
        y += Metrics.Space.Xl * scale;
        y = InfoLine.Draw(drawList, PhoneIcons.Search, Loc.T(L.Gifs.ConsentSearches), ui.Accent, ink, origin.X, y,
            width, scale) + Metrics.Space.Md * scale;
        y = InfoLine.Draw(drawList, PhoneIcons.Eye, Loc.T(L.Gifs.ConsentImages), ui.Accent, ink, origin.X, y,
            width, scale) + Metrics.Space.Md * scale;
        y = InfoLine.Draw(drawList, PhoneIcons.Settings, Loc.T(L.Gifs.ConsentSettings), ui.Accent, ink, origin.X, y,
            width, scale);
        return y + Metrics.Space.Sm * scale;
    }

    private void DrawPicker(in SheetFrame frame, in AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var muted = Palette.WithAlpha(ink, ink.W * MutedAlpha);
        var headerHeight = HeaderHeight * scale;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.4f),
            Loc.T(L.Gifs.Title), ink, TextStyles.Headline);

        var side = Metrics.Space.Lg * scale;
        var searchRect = new Rect(new Vector2(content.Min.X + side, content.Min.Y + headerHeight),
            new Vector2(content.Max.X - side, content.Min.Y + headerHeight + SearchHeight * scale));
        var focus = focusQuery && frame.Interactive;
        if (focus)
        {
            focusQuery = false;
        }

        SearchField.Draw(searchRect, "##gifPickerQuery", Loc.T(L.Gifs.SearchHint), ref draft, Surfaces.Fill(ink,
                FillLevel.Tertiary), muted, ink, Palette.WithAlpha(ink, ink.W * HoverAlpha), ui.Theme.AppBackground,
            GifQuery.MaxLength, focus);
        var now = ImGui.GetTime();
        if (!string.Equals(draft, seenDraft, StringComparison.Ordinal))
        {
            seenDraft = draft;
            search.SetQuery(draft, now);
        }

        search.Tick(now);

        var footerHeight = FooterHeight * scale;
        var footerBottom = frame.Panel.Max.Y - Metrics.Size.HomeIndicatorInset * scale;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, footerBottom - footerHeight * 0.5f),
            Loc.T(L.Gifs.PoweredBy), muted, TextStyles.FootnoteEmphasized);

        var body = new Rect(new Vector2(content.Min.X, searchRect.Max.Y),
            new Vector2(content.Max.X, footerBottom - footerHeight));
        if (!search.Available)
        {
            DrawMessage(body, Loc.T(L.Gifs.Unavailable), muted);
            return;
        }

        var results = search.Results;
        if (results.Items.Length == 0)
        {
            DrawEmptyState(body, results, ink, muted, in ui, frame.Opacity, frame.Interactive);
            return;
        }

        var gridTop = body.Min.Y;
        if (results.Query.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(body.Min.X + side, gridTop + Metrics.Space.Xxs * scale),
                Loc.T(L.Gifs.Trending), muted, TextStyles.FootnoteEmphasized);
            gridTop += SectionHeight * scale;
        }

        DrawGrid(new Rect(new Vector2(body.Min.X + side, gridTop), new Vector2(body.Max.X - side, body.Max.Y)),
            results, ink, in ui, frame.Opacity, frame.Interactive);
    }

    private void DrawEmptyState(Rect body, GifResults results, Vector4 ink, Vector4 muted, in AppSkin ui,
        float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        if (results.Loading)
        {
            LoadingPulse.Spinner(new Vector2(body.Center.X, body.Min.Y + SpinnerRadius * 4f * scale),
                SpinnerRadius * scale, ui.Accent, opacity, ImGui.GetWindowDrawList());
            return;
        }

        if (!results.Failure.Failed)
        {
            if (results.Query.Length > 0)
            {
                DrawMessage(body, Loc.T(L.Gifs.NoResults), muted);
            }

            return;
        }

        var width = MathF.Max(1f, body.Width - Metrics.Space.Xl * 2f * scale);
        var y = body.Min.Y + Metrics.Space.Xxl * scale;
        y += Typography.DrawWrappedCentered(new Vector2(body.Center.X, y), Loc.T(L.Gifs.LoadFailed), ink,
            TextStyles.Headline, width);
        if (results.Failure.Kind != AepFailureKind.Server)
        {
            failure.Set(results.Failure);
            y += Metrics.Space.Xs * scale;
            y += Typography.DrawWrappedCentered(new Vector2(body.Center.X, y), failure.Text(), muted,
                TextStyles.Subheadline, width);
        }

        y += Metrics.Space.Lg * scale;
        if (RetryButton(new Vector2(body.Center.X, y), in ui, opacity) && interactive)
        {
            search.Retry();
        }
    }

    private void DrawGrid(Rect area, GifResults results, Vector4 ink, in AppSkin ui, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        using (AppSurface.Begin(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var gap = GridGap * scale;
            Layout(results.Items, width, gap);
            var origin = ImGui.GetCursorScreenPos();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = CullMargin * scale;
            var columnWidth = (width - gap) / Columns;
            var rounding = TileRounding * scale;
            var time = ImGui.GetTime();
            var tileFill = ImGui.GetColorU32(Palette.WithAlpha(ink, TileFillAlpha * opacity));
            var hoverFill = ImGui.GetColorU32(Palette.WithAlpha(ink, HoverAlpha * opacity));
            for (var index = 0; index < results.Items.Length; index++)
            {
                var top = tileTops[index];
                var height = tileHeights[index];
                if (top + height < scrollY - margin || top > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + tileColumns[index] * (columnWidth + gap), origin.Y + top);
                var max = new Vector2(min.X + columnWidth, min.Y + height);
                var item = results.Items[index];
                var texture = images.SizedAt(item.PreviewUrl, columnWidth, time);
                if (texture is null)
                {
                    Squircle.Fill(drawList, min, max, rounding, tileFill);
                }
                else
                {
                    var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
                    drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                        ImDrawFlags.RoundCornersAll);
                }

                var hovered = interactive && UiInteract.Hover(min, max);
                if (hovered)
                {
                    Squircle.Fill(drawList, min, max, rounding, hoverFill);
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(min, max, hovered))
                {
                    picked = new GifPick(item, results.Query);
                    sheet.Close();
                }
            }

            var bottom = gridHeight;
            if (results.LoadingMore)
            {
                LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + bottom + SpinnerRadius * 2f * scale),
                    SpinnerRadius * scale, ui.Accent, opacity, drawList);
                bottom += SpinnerRadius * 4f * scale;
            }
            else if (results.Failure.Failed)
            {
                var retryTop = origin.Y + bottom + Metrics.Space.Sm * scale;
                if (RetryButton(new Vector2(origin.X + width * 0.5f, retryTop), in ui, opacity) && interactive)
                {
                    search.Retry();
                }

                bottom += Button.SmallHeight * scale + Metrics.Space.Lg * scale;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, bottom)));
            if (interactive && scrollY + viewHeight >= gridHeight - LoadMoreDistance * scale)
            {
                search.LoadMore();
            }
        }
    }

    private static bool RetryButton(Vector2 topCenter, in AppSkin ui, float opacity)
    {
        var label = Loc.T(L.Common.Retry);
        var scale = UiScale.Current;
        var width = Button.WidthFor(label, ButtonSize.Small);
        var rect = new Rect(new Vector2(topCenter.X - width * 0.5f, topCenter.Y),
            new Vector2(topCenter.X + width * 0.5f, topCenter.Y + Button.SmallHeight * scale));
        return Button.Draw(ImGui.GetWindowDrawList(), rect, label, ui.Ink, ButtonStyle.Tinted,
            id: "gifs.retry", opacity: opacity);
    }

    private static void DrawMessage(Rect body, string message, Vector4 muted)
    {
        var scale = UiScale.Current;
        Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Min.Y + Metrics.Space.Xxl * scale), message,
            muted, TextStyles.Subheadline, MathF.Max(1f, body.Width - Metrics.Space.Xl * 2f * scale));
    }

    private void Layout(GifItem[] items, float width, float gap)
    {
        if (ReferenceEquals(items, laidOut) && MathF.Abs(width - laidOutWidth) < 0.5f)
        {
            return;
        }

        laidOut = items;
        laidOutWidth = width;
        if (tileTops.Length < items.Length)
        {
            tileTops = new float[items.Length];
            tileHeights = new float[items.Length];
            tileColumns = new byte[items.Length];
        }

        Array.Clear(columnBottoms);
        var columnWidth = (width - gap) / Columns;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var aspect = item.PreviewWidth > 0 ? (float)item.PreviewHeight / item.PreviewWidth : 1f;
            var height = columnWidth * Math.Clamp(aspect, MinTileAspect, MaxTileAspect);
            var column = columnBottoms[1] < columnBottoms[0] ? 1 : 0;
            tileTops[index] = columnBottoms[column];
            tileHeights[index] = height;
            tileColumns[index] = (byte)column;
            columnBottoms[column] += height + gap;
        }

        gridHeight = MathF.Max(0f, MathF.Max(columnBottoms[0], columnBottoms[1]) - gap);
    }
}
