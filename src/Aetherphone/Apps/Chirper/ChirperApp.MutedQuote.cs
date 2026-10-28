using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float MutedQuotePadX = 12f;
    private const float MutedQuotePadY = 10f;
    private const float MutedQuoteLinkGap = 10f;
    private const float MutedQuoteLinkSlop = 4f;

    private readonly HashSet<string> revealedMutedQuotes = new(StringComparer.Ordinal);

    private bool FoldsMutedQuote(PostDto? quoted, string? hostId) =>
        hostId is not null && quoted is not null && (quoted.WordMuted || store.IsAuthorMuted(quoted))
        && !revealedMutedQuotes.Contains(hostId);

    private string MutedQuoteMessage(PostDto quoted) =>
        Loc.T(store.IsAuthorMuted(quoted) ? L.Social.MutedQuote : L.Social.MutedQuoteWord);

    private static float MutedQuoteHeight(float width, string message)
    {
        var scale = UiScale.Current;
        var linkSize = Typography.Measure(Loc.T(L.Social.MutedQuoteView), QuoteNameStyle);
        var messageWidth = MutedQuoteMessageWidth(width, linkSize.X);
        var messageHeight = Typography.MeasureWrappedBlock(message, QuoteMetaStyle, messageWidth).Y;
        return MutedQuotePadY * 2f * scale + MathF.Max(messageHeight, linkSize.Y);
    }

    private static float MutedQuoteMessageWidth(float width, float linkWidth)
    {
        var scale = UiScale.Current;
        return MathF.Max(1f, width - MutedQuotePadX * 2f * scale - MutedQuoteLinkGap * scale - linkWidth);
    }

    private void DrawMutedQuote(ImDrawListPtr drawList, Vector2 min, float width, float height, string hostId,
        string message)
    {
        var scale = UiScale.Current;
        var max = new Vector2(min.X + width, min.Y + height);
        var rounding = MediaRounding * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.QuoteFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
        var link = Loc.T(L.Social.MutedQuoteView);
        var linkSize = Typography.Measure(link, QuoteNameStyle);
        var padX = MutedQuotePadX * scale;
        var padY = MutedQuotePadY * scale;
        Typography.DrawWrappedLeft(new Vector2(min.X + padX, min.Y + padY), message,
            ChirperInk.MutedInk, QuoteMetaStyle, MutedQuoteMessageWidth(width, linkSize.X));
        var linkMin = new Vector2(max.X - padX - linkSize.X, min.Y + (height - linkSize.Y) * 0.5f);
        var linkMax = linkMin + linkSize;
        var slop = new Vector2(MutedQuoteLinkSlop * scale, MutedQuoteLinkSlop * scale);
        var hovered = UiInteract.Hover(linkMin - slop, linkMax + slop);
        Typography.Draw(drawList, linkMin, link, hovered ? ChirperInk.Accent : ChirperInk.AccentLink, QuoteNameStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin - slop, linkMax + slop, hovered))
        {
            revealedMutedQuotes.Add(hostId);
        }
    }
}
