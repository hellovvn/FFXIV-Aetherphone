using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core;
using Aetherphone.Core.Casino.Rolls;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Rolls;

internal enum RollHostTap : byte
{
    None,
    Resume,
    Game,
}

internal sealed class RollHostSection
{
    private const int Columns = 2;
    private const float HeaderGap = 10f;
    private const float SublineGap = 8f;
    private const float OpenCardGap = 12f;

    private readonly string[] metas = new string[RollGames.All.Length];
    private readonly RollGameView view = new();
    private LanguageInfo? language;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, ChatRolls rolls,
        bool interactive, float scale, out RollHostTap tap, out RollGameKind game)
    {
        RefreshTexts();
        tap = RollHostTap.None;
        game = RollGameKind.Deathroll;
        var top = origin.Y;
        if (rolls.Table.IsOpen)
        {
            top = DrawOpenGame(drawList, ui, new Vector2(origin.X, top), width, rolls, interactive, scale,
                out var resume) + OpenCardGap * scale;
            if (resume)
            {
                tap = RollHostTap.Resume;
            }
        }

        top = DrawHeader(drawList, ui, new Vector2(origin.X, top), width, scale) + HeaderGap * scale;
        var layout = PosterTile.Grid;
        var tileWidth = HostHomeLayout.GridTileWidth(width, Columns, scale);
        var textBlock = PosterTile.TextBlock(layout, scale);
        var tileHeight = PosterTile.TileHeight(layout, textBlock, scale);
        var glyphTileWidth = HostHomeLayout.GridTileWidth(width, scale);
        var phase = (float)ImGui.GetTime();
        var gridOrigin = new Vector2(origin.X, top);
        for (var index = 0; index < RollGames.All.Length; index++)
        {
            ref readonly var info = ref RollGames.All[index];
            var tile = HostHomeLayout.GridCell(gridOrigin, tileWidth, tileHeight, index, Columns, scale);
            using (UiScope.PushId(index))
            {
                if (DrawPoster(drawList, info, tile, metas[index], layout, textBlock, glyphTileWidth, phase,
                        interactive, scale))
                {
                    tap = RollHostTap.Game;
                    game = info.Kind;
                }
            }
        }

        return top + HostHomeLayout.GridHeight(RollGames.All.Length, tileHeight, Columns, scale);
    }

    private float DrawOpenGame(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, ChatRolls rolls,
        bool interactive, float scale, out bool resume)
    {
        view.Sync(rolls.Table, rolls.HouseName);
        var pad = HostFlowLayout.CardPad * scale;
        var tile = HostFlowLayout.HeaderTile * scale;
        var label = Loc.T(L.Rolls.Resume);
        var buttonWidth = Button.WidthFor(label, ButtonSize.Regular);
        var buttonHeight = Button.RegularHeight * scale;
        var textLeft = origin.X + pad + tile + HostFlowLayout.HeaderGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - buttonWidth - Metrics.Space.Sm * scale - textLeft);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var promptHeight = Typography.MeasureWrappedBlock(view.Prompt, TextStyles.Footnote, textWidth).Y;
        var height = MathF.Max(tile, eyebrowHeight + titleHeight + promptHeight) + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var centerY = origin.Y + height * 0.5f;
        RollHeader.Tile(drawList, rolls.Table.Kind, new Vector2(origin.X + pad + tile * 0.5f, centerY), tile);
        var textTop = centerY - (eyebrowHeight + titleHeight + promptHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(L.Rolls.OpenEyebrow), textWidth, TextStyles.FootnoteEmphasized), ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + eyebrowHeight),
            Typography.FitText(Loc.T(RollGames.Of(rolls.Table.Kind).Title), textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, textTop + eyebrowHeight + titleHeight), view.Prompt,
            ui.BodyInk, TextStyles.Footnote, textWidth);
        var button = new Rect(new Vector2(max.X - pad - buttonWidth, centerY - buttonHeight * 0.5f),
            new Vector2(max.X - pad, centerY + buttonHeight * 0.5f));
        resume = Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Prominent, enabled: interactive,
            id: "casino.rolls.resume");
        return max.Y;
    }

    private static bool DrawPoster(ImDrawListPtr drawList, in RollGameInfo info, Rect tile, string meta,
        in PosterLayout layout, float textBlock, float glyphTileWidth, float phase, bool interactive, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID("rollPoster"), tile.Min, tile.Max, out var pressedMin,
            out var pressedMax, interactive);
        var pressed = new Rect(pressedMin, pressedMax);
        PosterTile.DrawFrame(drawList, pressed, info.Tint, layout, scale);
        if (info.GlyphId.Length > 0)
        {
            PosterTile.DrawGlyph(drawList, pressed, info.GlyphId, layout, textBlock, glyphTileWidth, scale);
        }
        else
        {
            AppSkin.Icon(drawList, PosterTile.ArtCenter(pressed, layout, textBlock, scale), IconGlyph.Of(info.Icon),
                CasinoArt.White, PosterTile.IconScale);
        }

        PosterTile.DrawSign(drawList, pressed, info.Sign, phase, layout, scale);
        PosterTile.DrawText(drawList, pressed, Loc.T(info.Title), new PosterInfo(true, 0, meta, string.Empty,
            string.Empty), layout, textBlock, scale);
        PosterTile.DrawRim(drawList, pressed, info.Tint, hovered, layout, scale);
        return interactive && UiInteract.Click(tile.Min, tile.Max, hovered);
    }

    private static float DrawHeader(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        var title = Loc.T(L.Rolls.SectionTitle);
        var subline = Loc.T(L.Rolls.SectionLine);
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        var sublineSize = Typography.Measure(subline, TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var sideBySide = titleSize.X + SublineGap * scale + sublineSize.X <= width;
        Typography.Draw(drawList, origin, Typography.FitText(title, width, TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
        if (sideBySide)
        {
            var baseline = origin.Y + titleHeight - Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(origin.X + width - sublineSize.X, baseline), subline, ui.MutedInk,
                TextStyles.Footnote);
            return origin.Y + titleHeight;
        }

        return PitText.Draw(drawList, subline, TextStyles.Footnote, ui.MutedInk, origin.X, origin.Y + titleHeight,
            width, false);
    }

    private void RefreshTexts()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        for (var index = 0; index < metas.Length; index++)
        {
            metas[index] = Loc.T(RollGames.All[index].Meta);
        }
    }
}
