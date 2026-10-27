using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private readonly List<RankingRow> rankingRows = new();
    private PrismResultsDto? rankingsFor;
    private RankingRow? ownRanking;
    private string rankingQuery = string.Empty;
    private string rankingFilter = string.Empty;
    private string rankingLanguage = string.Empty;
    private CachedText rankingsTitle;

    private sealed record RankingRow(PrismPlacingDto Placing, string Rank, string Handle, string Votes);

    private void OpenRankings()
    {
        rankingQuery = string.Empty;
        tab = PrismTab.Week;
        Push(PrismScreen.Rankings);
    }

    private float DrawRankingsButton(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (ShownResults() is null)
        {
            return top;
        }

        var button = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.LargeHeight * scale));
        if (DrawButton(drawList, button, Loc.T(L.Prism.AllRankings), ButtonStyle.Solid, scale))
        {
            OpenRankings();
        }

        return button.Max.Y + Metrics.Space.Lg * scale;
    }

    private void SyncRankings(PrismResultsDto? results)
    {
        if (ReferenceEquals(rankingsFor, results) && rankingQuery == rankingFilter &&
            rankingLanguage == Loc.Current.Code)
        {
            return;
        }

        rankingsFor = results;
        rankingFilter = rankingQuery;
        rankingLanguage = Loc.Current.Code;
        rankingRows.Clear();
        ownRanking = null;
        var query = rankingQuery.Trim().TrimStart('@');
        var rankings = results?.Rankings ?? Array.Empty<PrismPlacingDto>();
        for (var index = 0; index < rankings.Length; index++)
        {
            var placing = rankings[index];
            var row = new RankingRow(placing, "#" + placing.Rank,
                placing.Handle.Length == 0 ? string.Empty : "@" + placing.Handle.TrimStart('@'),
                Loc.T(L.Prism.RankingVotes, placing.TotalVotes));
            if (placing.IsMe)
            {
                ownRanking = row;
            }

            if (query.Length == 0 || placing.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                placing.Handle.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                rankingRows.Add(row);
            }
        }
    }

    private void DrawRankings(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var results = ShownResults();
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.AllRankings), scale);
        var field = new Rect(new Vector2(content.Min.X, top + Metrics.Space.Sm * scale),
            new Vector2(content.Max.X, top + (Metrics.Space.Sm + Metrics.Size.TapTarget) * scale));
        SearchBar.Surface(drawList, field, controls);
        var inset = Metrics.Space.Md * scale;
        var glyph = PrismArt.RowIconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(field.Min.X + inset + glyph * 0.5f, field.Center.Y), PhoneIcons.Search,
            PrismArt.Muted, glyph);
        var previousQuery = rankingQuery;
        GlassField.Text(field, "##prismRankingsSearch", Loc.T(L.Prism.SearchRankings), ref rankingQuery,
            PrismArt.Fore, PrismArt.Muted, inset + glyph + Metrics.Space.Sm * scale, QueryMaxLength);
        if (previousQuery != rankingQuery)
        {
            pendingScroll = 0f;
        }

        SyncRankings(results);
        top = field.Max.Y + Metrics.Space.Md * scale;
        if (results is not null)
        {
            var title = results.Track.Length == 0 ? results.Title
                : FactLine(ref rankingsTitle, results.Title, PrismTracks.Name(results.Track));
            Typography.Draw(drawList, new Vector2(content.Min.X, top),
                Typography.FitText(title, content.Width, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
            top += Typography.LineHeight(PrismType.Headline) + Metrics.Space.Sm * scale;
        }

        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), Loc.T(L.Prism.RankingsOrder),
            PrismArt.Muted, PrismType.Caption, content.Width) + Metrics.Space.Md * scale;
        if (ownRanking is { } own)
        {
            top = DrawRankingRow(drawList, content.Min.X, top, content.Width, own, scale) + Metrics.Space.Md * scale;
        }

        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y - bottomChrome));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X + content.Min.X - full.Min.X;
        var cursor = origin.Y;
        if (rankingRows.Count == 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(results?.Rankings is null ? L.Prism.RankingsUnavailable : L.Prism.RankingsEmpty),
                PrismArt.Muted, PrismType.Body, content.Width);
        }

        for (var index = 0; index < rankingRows.Count; index++)
        {
            cursor = DrawRankingRow(list, left, cursor, content.Width, rankingRows[index], scale) + Metrics.Space.Sm * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(content.Width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawRankingRow(ImDrawListPtr drawList, float left, float top, float width, RankingRow row, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var headline = Typography.LineHeight(PrismType.Headline);
        var height = MathF.Max(76f * scale, headline + line * 2f + pad * 2f);
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(card.Min, card.Max))
        {
            return card.Max.Y;
        }

        var own = row.Placing.IsMe;
        var tint = own ? PrismLight.Hues[4] : PrismArt.Fore;
        drawList.AddRectFilled(card.Min, card.Max, ImGui.GetColorU32(Palette.WithAlpha(tint, own ? 0.18f : 0.05f)),
            Metrics.Radius.Md * scale);
        if (own)
        {
            PrismLight.Edge(drawList, card, Metrics.Radius.Md * scale, PrismGlow.Spectrum, 0.65f, scale);
        }

        var rankWidth = MathF.Max(42f * scale, Typography.Measure(row.Rank, PrismType.Headline).X);
        Typography.Draw(drawList, new Vector2(left + pad, card.Center.Y - headline * 0.5f), row.Rank,
            PrismArt.Fore, PrismType.Headline);
        var photoLeft = left + pad + rankWidth + Metrics.Space.Sm * scale;
        var photoWidth = RowThumbUnits * scale;
        var photo = new Rect(new Vector2(photoLeft, card.Center.Y - photoWidth / PrismStore.EntryRatio * 0.5f),
            new Vector2(photoLeft + photoWidth, card.Center.Y + photoWidth / PrismStore.EntryRatio * 0.5f));
        DrawLook(drawList, photo, row.Placing.ImageUrl, false);
        TapForOutfit(photo.Min, photo.Max, row.Placing);
        var textLeft = photo.Max.X + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, card.Max.X - pad - textLeft);
        var textTop = card.Center.Y - (headline + line * 2f) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(own ? Loc.T(L.Prism.RankingYou) : row.Placing.DisplayName, room, PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + headline),
            Typography.FitText(row.Handle, room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + headline + line),
            Typography.FitText(row.Votes, room, PrismType.Caption), PrismArt.Fore, PrismType.Caption);
        return card.Max.Y;
    }
}
