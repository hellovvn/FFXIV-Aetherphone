using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Rolls;

internal static class RollHeader
{
    private const float WashAlpha = 0.24f;
    private const float RimAlpha = 0.45f;

    public static void Tile(ImDrawListPtr drawList, RollGameKind kind, Vector2 center, float size)
    {
        ref readonly var info = ref RollGames.Of(kind);
        if (info.GlyphId.Length > 0)
        {
            CasinoArt.GameTile(drawList, info.GlyphId, info.Tint, center, size);
            return;
        }

        CasinoArt.IconTileAt(drawList, center, size, info.Tint, info.Icon);
    }

    public static void Draw(AppSkin ui, RollGameKind kind, string line, float width, float scale)
    {
        ref readonly var info = ref RollGames.Of(kind);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.HeaderPad * scale;
        var tile = HostFlowLayout.HeaderTile * scale;
        var textLeft = origin.X + pad + tile + HostFlowLayout.HeaderGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Footnote, textWidth).Y;
        var height = HostFlowLayout.HeaderHeight(scale, nameHeight, lineHeight);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.FillHorizontalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(info.Tint with { W = WashAlpha }), ImGui.GetColorU32(info.Tint with { W = 0f }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(info.Tint with { W = RimAlpha }),
            scale);
        Tile(drawList, kind, new Vector2(origin.X + pad + tile * 0.5f, card.Center.Y), tile);
        var textTop = card.Center.Y - (nameHeight + HostFlowLayout.LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(info.Title), textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        Typography.DrawWrappedLeft(new Vector2(textLeft, textTop + nameHeight + HostFlowLayout.LineGap * scale), line,
            ui.BodyInk, TextStyles.Footnote, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
