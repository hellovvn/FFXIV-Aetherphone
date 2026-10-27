using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float RevealNumeralUnits = 128f;
    private const float PodiumNumeralUnits = 48f;
    private const float PodiumShadeShare = 0.45f;
    private const float RowThumbUnits = 36f;
    private const float CardThumbUnits = 48f;
    private const float RankColumnUnits = 32f;
    private const int PodiumSize = 3;
    private const float RevealZoom = 0.12f;
    private const float PodiumLightDelay = 0.26f;
    private const float PodiumLightStagger = 0.18f;
    private const float PodiumDimFloor = 0.26f;
    private const float PodiumRimDelay = 0.95f;
    private const float PlacedPearlAlpha = 0.42f;
    private const float NumeralDelay = 0.15f;
    private const float RevealPageSeconds = 5f;
    private const float RevealHoldSeconds = 0.25f;
    private const float RevealBackShare = 0.33f;
    private const float RevealTapTopUnits = 56f;
    private const float StoryBarGapUnits = 8f;
    private const float StoryRowGapUnits = 12f;
    private const float CreditDelay = 0.3f;
    private const float HintDelay = 0.5f;
    private const float RevealPitchTop = 1.3f;
    private const float RevealPitchStep = 0.12f;

    private readonly List<PrismPlacingDto> finalOrder = new();
    private readonly List<PrismPlacingDto> heatOrder = new();
    private readonly List<PrismPlacingDto> summaryPhotos = new();
    private static readonly string[] RevealRanks = { "", "#1", "#2", "#3" };
    private CachedText revealHandle;
    private CachedText revealOwnRank;

    private static readonly string PodiumGlyph = IconGlyph.Of(FontAwesomeIcon.Trophy);
    private static readonly Vector4 PodiumShade = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 PodiumClear = new(0f, 0f, 0f, 0f);

    private PrismResultsDto? orderedFor;
    private string profileGlamId = string.Empty;
    private int revealIndex;
    private double podiumLitAt = PrismMotion.LongAgo;
    private int soundedPage = -1;
    private float revealPageSeconds;
    private float revealHoldSeconds;
    private CachedText placingText;
    private CachedText revealTitle;
    private CachedText heatFact;
    private CachedText rankFact;
    private CachedText topFact;
    private CachedText coinFact;

    private void StartReveal()
    {
        revealIndex = 0;
        revealPageSeconds = 0f;
        soundedPage = -1;
        revealHoldSeconds = 0f;
        Go(PrismScreen.Reveal, 0);
    }

    private void TurnRevealPage(int delta, int pages)
    {
        revealIndex = Math.Clamp(revealIndex + delta, 0, pages - 1);
        revealPageSeconds = 0f;
        Go(PrismScreen.Reveal, 0);
    }

    private void DrawReveal(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (ShownResults() is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        SyncOrder(results);
        var pages = finalOrder.Count + 1;
        revealIndex = Math.Min(revealIndex, pages - 1);
        var summary = revealIndex == pages - 1;
        if (soundedPage != revealIndex)
        {
            soundedPage = revealIndex;
            PlayRevealSound(summary ? 0 : finalOrder[finalOrder.Count - 1 - revealIndex].Rank);
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var held = !summary && ImGui.IsMouseDown(ImGuiMouseButton.Left) && UiInteract.Hover(content.Min, content.Max);
        if (held)
        {
            revealHoldSeconds += delta;
        }
        else if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            revealHoldSeconds = 0f;
        }
        if (!summary && revealHoldSeconds < RevealHoldSeconds)
        {
            revealPageSeconds += delta;
        }

        if (!summary && revealPageSeconds >= RevealPageSeconds)
        {
            TurnRevealPage(1, pages);
            return;
        }

        if (summary)
        {
            DrawRevealSummary(drawList, full, content, results, scale);
        }
        else
        {
            DrawRevealPlacing(drawList, full, content, results, finalOrder[finalOrder.Count - 1 - revealIndex], scale);
            DrawRevealTaps(drawList, content, pages, scale);
        }

        PrismArt.OverPhoto(true);
        PrismArt.StoryBars(drawList, content.Min.X, content.Max.X, content.Min.Y + StoryBarGapUnits * scale, pages,
            revealIndex, summary ? 1f : revealPageSeconds / RevealPageSeconds, scale);
        DrawRevealTop(drawList, content, results, summary, scale);
        PrismArt.OverPhoto(false);
    }

    private void DrawRevealTaps(ImDrawListPtr drawList, Rect content, int pages, float scale)
    {
        var top = content.Min.Y + RevealTapTopUnits * scale;
        var split = content.Min.X + content.Width * RevealBackShare;
        var backZone = new Rect(new Vector2(content.Min.X, top), new Vector2(split, content.Max.Y));
        var nextZone = new Rect(new Vector2(split, top), content.Max);
        var tap = revealHoldSeconds < RevealHoldSeconds;
        if (UiInteract.Click(backZone.Min, backZone.Max, UiInteract.Hover(backZone.Min, backZone.Max)) && tap)
        {
            TurnRevealPage(-1, pages);
            return;
        }

        if (UiInteract.Click(nextZone.Min, nextZone.Max, UiInteract.Hover(nextZone.Min, nextZone.Max)) && tap)
        {
            TurnRevealPage(1, pages);
            return;
        }

        if (revealIndex != 0 || configuration.PrismRevealedWeek.Length > 0)
        {
            return;
        }

        var hint = PrismMotion.Mark(drawList);
        var label = Loc.T(L.Prism.TapHint);
        var labelPixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(content.Max.X - size.X, content.Center.Y), label, PrismArt.Fore,
            PrismFace.Label, labelPixels);
        PrismMotion.Rise(drawList, hint, PrismMotion.At(screenAge, HintDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawRevealSummary(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results, float scale)
    {
        PrismArt.OverPhoto(true);
        onPhoto = true;
        PrismLight.Week(drawList, full, results.WeekId, new Vector4(0.035f, 0.025f, 0.075f, 1f),
            PrismArt.PhotoFore, 0f, 0f);
        var final = ActionRect(content, scale, 1);
        var thanks = Loc.T(L.Prism.RevealThanks);
        var message = Loc.T(L.Prism.RevealNext);
        var titleHeight = PrismType.HeroHeight(thanks, content.Width);
        var bodyHeight = Typography.MeasureWrappedBlock(message, PrismType.Body, content.Width).Y;
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var own = RevealOwnRanking(results);
        var textHeight = titleHeight + bodyHeight + captionLine + Metrics.Space.Lg * scale * 2f;
        if (own.Length > 0)
        {
            textHeight += Typography.MeasureWrappedBlock(own, PrismType.Caption, content.Width).Y + Metrics.Space.Md * scale;
        }

        var textTop = final.Min.Y - Metrics.Space.Xl * scale - textHeight;
        var photoTop = content.Min.Y + RevealTapTopUnits * scale;
        var photoBottom = textTop - Metrics.Space.Lg * scale;
        DrawRevealCollage(drawList, new Rect(new Vector2(content.Min.X, photoTop),
            new Vector2(content.Max.X, MathF.Max(photoTop, photoBottom))), scale);
        PrismCelebrationRenderer.Draw(drawList, full, screenAge, 0, scale);
        var mark = PrismMotion.Mark(drawList);
        Typography.Draw(drawList, new Vector2(content.Min.X, textTop),
            Typography.FitText(results.Title, content.Width, PrismType.Caption), PrismArt.PhotoMuted, PrismType.Caption);
        textTop += captionLine + Metrics.Space.Md * scale;
        textTop += PrismType.DrawHero(drawList, new Vector2(content.Min.X, textTop), thanks, content.Width,
            PrismArt.PhotoFore) + Metrics.Space.Md * scale;
        textTop += Typography.DrawWrappedLeft(new Vector2(content.Min.X, textTop), message, PrismArt.PhotoMuted,
            PrismType.Body, content.Width) + Metrics.Space.Md * scale;
        if (own.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, textTop), own, PrismArt.PhotoFore,
                PrismType.Caption, content.Width);
        }

        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, 0.1f, Motion.Sheet), PrismMotion.RiseUnits * scale);
        if (DrawButton(drawList, final, Loc.T(L.Prism.AllRankings), ButtonStyle.Solid, scale))
        {
            FinishReveal(results);
            OpenRankings();
        }

        var replay = Loc.T(L.Prism.ReplayReveal);
        if (DrawButton(drawList, PlainRect(ActionRect(content, scale).Center, replay, content.Width, 0.5f, scale),
                replay, ButtonStyle.Plain, scale))
        {
            StartReveal();
        }
    }

    private string RevealOwnRanking(PrismResultsDto results)
    {
        var rankings = results.Rankings ?? Array.Empty<PrismPlacingDto>();
        for (var index = 0; index < rankings.Length; index++)
        {
            if (!rankings[index].IsMe)
            {
                continue;
            }

            var own = rankings[index];
            var key = HashCode.Combine(own.Rank, own.TotalVotes, Loc.Current.Code);
            return revealOwnRank.IsCurrent(key) ? revealOwnRank.Value
                : revealOwnRank.Store(key, Loc.T(L.Prism.RevealOwnRank, own.Rank, own.TotalVotes));
        }

        return string.Empty;
    }

    private void DrawRevealCollage(ImDrawListPtr drawList, Rect area, float scale)
    {
        if (summaryPhotos.Count == 0 || area.Height < Metrics.Size.TapTarget * scale)
        {
            return;
        }

        var columns = Math.Min(3, summaryPhotos.Count);
        var rows = area.Height > 190f * scale && summaryPhotos.Count > columns ? 2 : 1;
        var gap = Metrics.Space.Sm * scale;
        var width = (area.Width - gap * (columns - 1)) / columns;
        var height = (area.Height - gap * (rows - 1)) / rows;
        for (var index = 0; index < Math.Min(summaryPhotos.Count, columns * rows); index++)
        {
            var min = area.Min + new Vector2(index % columns * (width + gap), index / columns * (height + gap));
            var photo = new Rect(min, min + new Vector2(width, height));
            var mark = PrismMotion.Mark(drawList);
            DrawLook(drawList, photo, summaryPhotos[index].ImageUrl, false, focusY: CoverFocus);
            PrismLight.Edge(drawList, photo, 0f, PrismGlow.Spectrum, 0.5f, scale);
            PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, index * PrismMotion.Stagger, Motion.Sheet),
                PrismMotion.RiseUnits * scale);
        }
    }

    private void DrawRevealTop(ImDrawListPtr drawList, Rect content, PrismResultsDto results, bool summary,
        float scale)
    {
        var top = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var glyph = PrismArt.IconUnits * scale;
        var title = RevealTitle(results, summary);
        if (title.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(content.Min.X, top),
                Typography.FitText(title, MathF.Max(1f, content.Width - glyph - Metrics.Space.Sm * scale),
                    PrismType.Caption), PrismArt.Fore, PrismType.Caption);
        }

        var close = new Vector2(content.Max.X - glyph * 0.5f, top + line * 0.5f);
        var reach = new Vector2(Metrics.Size.TapTarget * 0.5f * scale, Metrics.Size.TapTarget * 0.5f * scale);
        HoverTooltip.Show("prism.reveal.close", new Rect(close - reach, close + reach),
            Loc.T(L.Common.Close));
        if (PrismArt.Icon(drawList, close, PhoneIcons.X, scale))
        {
            if (summary)
            {
                FinishReveal(results);
                return;
            }

            Go(PrismScreen.Cover, -1);
        }
    }

    private void DrawRevealPlacing(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results,
        PrismPlacingDto placing, float scale)
    {
        PrismArt.OverPhoto(true);
        onPhoto = true;
        var settle = PrismMotion.At(screenAge, 0f, Motion.Sheet);
        var zoom = Plugin.PluginInterface.UiBuilder.ShouldUseReducedMotion ? 1f : 1f + RevealZoom * (1f - settle);
        DrawLook(drawList, full, placing.ImageUrl, false, 1f, zoom, focusY: CoverFocus);
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        var shade = ImGui.GetColorU32(new Vector4(0.015f, 0.01f, 0.035f, 0.96f));
        var topShade = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f));
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, content.Min.Y + 100f * scale),
            topShade, topShade, clear, clear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, full.Min.Y + full.Height * 0.48f), full.Max,
            clear, clear, shade, shade);
        PrismCelebrationRenderer.Draw(drawList, full, screenAge, placing.Rank, scale);
        var color = PrismCelebrationRenderer.Color(placing.Rank);
        var left = content.Min.X;
        var nameHeight = PrismType.HeroHeight(placing.DisplayName, content.Width);
        var labelLine = Typography.LineHeight(PrismType.Body);
        var creditTop = content.Max.Y - PrismArt.BottomMargin(scale) - nameHeight - labelLine - Metrics.Space.Xl * scale;
        var numeralPixels = MathF.Min(RevealNumeralUnits * scale, content.Height * 0.22f);
        var numeralTop = creditTop - PrismType.LineHeight(PrismFace.Numerals, numeralPixels) - Metrics.Space.Lg * scale;
        var mark = PrismMotion.Mark(drawList);
        var numeral = RevealRanks[Math.Clamp(placing.Rank, 1, PodiumSize)];
        PrismType.Draw(drawList, new Vector2(left + 2f * scale, numeralTop + 2f * scale), numeral,
            new Vector4(0f, 0f, 0f, 0.45f), PrismFace.Numerals, numeralPixels);
        PrismType.Draw(drawList, new Vector2(left, numeralTop), numeral, color, PrismFace.Numerals, numeralPixels);
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, NumeralDelay, Motion.Sheet), 28f * scale);
        var credit = PrismMotion.Mark(drawList);
        PrismArt.Hairline(drawList, left, left + 48f * scale, creditTop - Metrics.Space.Md * scale, color, scale);
        PrismType.DrawHero(drawList, new Vector2(left, creditTop), placing.DisplayName, content.Width, PrismArt.PhotoFore);
        var handleKey = HashCode.Combine(placing.Handle, placing.World);
        var handle = revealHandle.IsCurrent(handleKey) ? revealHandle.Value : revealHandle.Store(handleKey,
            placing.Handle.Length > 0 ? "@" + placing.Handle.TrimStart('@') : placing.World);
        Typography.Draw(drawList, new Vector2(left, creditTop + nameHeight + Metrics.Space.Sm * scale),
            Typography.FitText(handle, content.Width, PrismType.Body), PrismArt.PhotoMuted, PrismType.Body);
        PrismMotion.Rise(drawList, credit, PrismMotion.At(screenAge, CreditDelay, Motion.Sheet), PrismMotion.RiseUnits * scale);
    }

    private static void PlayRevealSound(int rank)
    {
        if (rank == 1)
        {
            UiFeedback.Play(UiSound.GameWin);
            return;
        }

        if (rank > 1)
        {
            UiFeedback.PlayPitched(UiSound.Tap, RevealPitchTop - RevealPitchStep * rank);
            return;
        }

        UiFeedback.Play(UiSound.Shutter);
    }

    private void FinishReveal(PrismResultsDto results)
    {
        configuration.PrismRevealedWeek = results.WeekId;
        configuration.PrismRevealedRevision = results.Revision;
        configuration.Save();
        podiumLitAt = ImGui.GetTime();
        Go(PrismScreen.Final);
    }

    private void DrawFinal(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (ShownResults() is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        FollowPlacingProfile();
        SyncOrder(results);
        if (finalOrder.Count == 0 && heatOrder.Count == 0 && results.MyHeatRank <= 0)
        {
            DrawNoPodium(drawList, full, content, results, scale);
            return;
        }

        var top = DrawBackHeader(drawList, content, results.Title, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = content.Width;
        var left = origin.X + content.Min.X - full.Min.X;
        var right = left + width;
        var cursor = origin.Y + Metrics.Space.Sm * scale;
        if (finalOrder.Count > 0)
        {
            cursor = DrawSectionTitle(list, left, right, cursor, Loc.T(L.Prism.FinalTitle), string.Empty, scale) +
                     Metrics.Space.Md * scale;
            cursor = DrawPodium(list, left, cursor, width, scale);
            if (HasOutfit(finalOrder[0].Outfit) || finalOrder[0].GlamId.Length > 0)
            {
                var outfit = Loc.T(L.Prism.WinnersOutfit);
                var button = new Rect(new Vector2(left, cursor),
                    new Vector2(left + ButtonWidth([outfit], width, scale), cursor + Button.LargeHeight * scale));
                if (DrawButton(list, button, outfit, ButtonStyle.Plain, scale))
                {
                    OpenGlam(finalOrder[0]);
                }

                cursor = button.Max.Y + Metrics.Space.Lg * scale;
            }

            cursor += Metrics.Space.Xl * scale;
        }

        if (results.MyHeatRank > 0)
        {
            cursor = DrawPlacingRow(list, left, right, cursor, results, scale) + Metrics.Space.Xl * scale;
        }

        if (heatOrder.Count > 0)
        {
            cursor = DrawSectionTitle(list, left, right, cursor, Loc.T(L.Prism.YourHeat),
                results.HeatLabel.Length > 0 ? HeatFact(results) : string.Empty, scale) + Metrics.Space.Sm * scale;
            for (var index = 0; index < heatOrder.Count; index++)
            {
                cursor = DrawRankRow(list, left, cursor, width, heatOrder[index], scale);
            }

            cursor += Metrics.Space.Lg * scale;
        }

        var tap = Metrics.Size.TapTarget * scale;
        var replay = Loc.T(L.Prism.ReplayReveal);
        if (finalOrder.Count > 0)
        {
            if (DrawButton(list, PlainRect(new Vector2(left, cursor + tap * 0.5f), replay, width, 0f, scale), replay,
                    ButtonStyle.Plain, scale))
            {
                StartReveal();
            }

            cursor += tap;
        }

        var winners = Loc.T(L.Prism.WhatWinnersGet);
        if (DrawButton(list, PlainRect(new Vector2(left, cursor + tap * 0.5f), winners, width, 0f, scale), winners,
                ButtonStyle.Plain, scale))
        {
            OpenRewards();
        }

        cursor += tap + Metrics.Space.Lg * scale;
        cursor = DrawRankingsButton(list, left, cursor, width, scale) + PrismArt.BottomMargin(scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }

    private void DrawNoPodium(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        PrismLight.Week(drawList,
            new Rect(full.Min, new Vector2(full.Max.X, content.Min.Y + tap + Metrics.Space.Sm * scale)),
            results.WeekId, PrismArt.Page, PrismArt.Fore, 0f, 0f);
        var top = DrawBackHeader(drawList, content, results.Title, scale);
        var gallery = results.EntryCount > 0;
        var links = (gallery ? 2f : 1f) * tap + PrismArt.BottomMargin(scale);
        var area = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, content.Max.Y - bottomChrome - links));
        var bottom = DrawEmpty(drawList, area, PodiumGlyph, Loc.T(L.Prism.NoPodiumTitle), Loc.T(L.Prism.NoPodium),
            ContestAction(store.Week), ButtonStyle.Solid, scale, out var acted) + Metrics.Space.Sm * scale;
        if (acted)
        {
            OpenTab(PrismTab.Week);
        }

        if (gallery && CenteredLink(drawList, content, ref bottom, Loc.T(L.Prism.AllGlams), scale))
        {
            OpenGallery(results.WeekId, store.Track, results.Title);
        }

        if (CenteredLink(drawList, content, ref bottom, Loc.T(L.Prism.WhatWinnersGet), scale))
        {
            OpenRewards();
        }
    }

    private bool CenteredLink(ImDrawListPtr drawList, Rect content, ref float top, string label, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var rect = PlainRect(new Vector2(content.Center.X, top + tap * 0.5f), label, content.Width, 0.5f, scale);
        top += tap;
        return DrawButton(drawList, rect, label, ButtonStyle.Plain, scale);
    }

    private static string ContestAction(PrismWeekDto? week) => week is not { Id.Length: > 0 } ? string.Empty
        : week.Phase switch
        {
            PrismPhases.Entering when week.MyEntry is null => Loc.T(L.Prism.EnterThisWeek),
            PrismPhases.Entering => Loc.T(L.Prism.SeeYourGlam),
            PrismPhases.Judging => Loc.T(L.Prism.JudgeHeat),
            PrismPhases.Final => Loc.T(L.Prism.JudgeFinal),
            _ => string.Empty,
        };

    private float DrawPodium(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var photoHeight = column / PrismStore.EntryRatio;
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var numeralPixels = PodiumNumeralUnits * scale;
        var count = Math.Min(PodiumSize, finalOrder.Count);
        var age = (float)(ImGui.GetTime() - podiumLitAt);
        for (var index = 0; index < count; index++)
        {
            var placing = finalOrder[index];
            var x = left + index * (column + gap);
            var photo = new Rect(new Vector2(x, top), new Vector2(x + column, top + photoHeight));
            var mark = PrismMotion.Mark(drawList);
            DrawLook(drawList, photo, placing.ImageUrl, false);
            TapForOutfit(photo.Min, photo.Max, placing);
            var shade = new Vector2(photo.Min.X, photo.Max.Y - photoHeight * PodiumShadeShare);
            drawList.AddRectFilledMultiColor(shade, photo.Max, ImGui.GetColorU32(PodiumClear),
                ImGui.GetColorU32(PodiumClear), ImGui.GetColorU32(PodiumShade), ImGui.GetColorU32(PodiumShade));
            PrismType.Draw(drawList, new Vector2(photo.Min.X + Metrics.Space.Sm * scale, photo.Max.Y - numeralPixels),
                Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)], PrismArt.PhotoFore, PrismFace.Numerals,
                numeralPixels);
            var creditTop = photo.Max.Y + Metrics.Space.Sm * scale;
            var names = new Rect(new Vector2(x, photo.Max.Y), new Vector2(x + column,
                creditTop + nameLine + Typography.LineHeight(PrismType.Caption) + Metrics.Space.Sm * scale));
            TapForProfile(drawList, names, placing, scale);
            Typography.Draw(drawList, new Vector2(x, creditTop),
                Typography.FitText(placing.DisplayName, column, PrismType.Headline), NameInk(placing),
                PrismType.Headline);
            Typography.Draw(drawList, new Vector2(x, creditTop + nameLine),
                Typography.FitText(PlacingDetail(placing), column, PrismType.Caption), PrismArt.Muted,
                PrismType.Caption);
            var lit = PrismMotion.At(age, PodiumLightDelay + PodiumLightStagger * (count - 1 - index), Motion.Sheet);
            if (lit < PrismMotion.Settled)
            {
                PrismMotion.Shift(drawList, mark, Vector2.Zero, PodiumDimFloor + (1f - PodiumDimFloor) * lit);
            }

            var rim = PrismMotion.At(age, PodiumRimDelay, Motion.Sheet);
            if (placing.Rank == 1)
            {
                PrismLight.Edge(drawList, photo, 0f, PrismGlow.Spectrum, PrismLight.EdgeAlpha * rim, scale);
            }
            else if (placing.Rank is > 1 and <= PodiumSize)
            {
                PrismLight.Edge(drawList, photo, 0f, PrismGlow.Pearl, PlacedPearlAlpha * rim, scale);
            }
        }

        return top + photoHeight + Metrics.Space.Sm * scale + nameLine + Typography.LineHeight(PrismType.Caption) +
               Metrics.Space.Lg * scale;
    }

    private float DrawPlacingRow(ImDrawListPtr drawList, float left, float right, float top, PrismResultsDto results,
        float scale)
    {
        top = DrawSectionTitle(drawList, left, right, top, Loc.T(L.Prism.YourPlacing), CoinFact(results), scale) +
              Metrics.Space.Sm * scale;
        var thumbWidth = CardThumbUnits * scale;
        var thumb = new Rect(new Vector2(left, top),
            new Vector2(left + thumbWidth, top + thumbWidth / PrismStore.EntryRatio));
        DrawLook(drawList, thumb, results.MyImageUrl, false);
        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var placing = PlacingLine(results);
        Typography.Draw(drawList, new Vector2(textLeft, top), placing, PrismArt.Fore, PrismType.Title);
        var factsHeight = DrawFacts(drawList, new Vector2(textLeft, top + Typography.LineHeight(PrismType.Title)),
            right - textLeft, results, PrismArt.Muted, scale);
        return MathF.Max(thumb.Max.Y, top + Typography.LineHeight(PrismType.Title) + factsHeight);
    }

    private float DrawFacts(ImDrawListPtr drawList, Vector2 topLeft, float width, PrismResultsDto results,
        Vector4 color, float scale)
    {
        var cursor = topLeft;
        DrawFact(drawList, HeatFact(results), topLeft.X, width, color, ref cursor, scale);
        DrawFact(drawList, Fact(ref rankFact, L.Prism.FinalRank, results.MyFinalRank), topLeft.X, width, color,
            ref cursor, scale);
        DrawFact(drawList, Fact(ref topFact, L.Prism.TopPercent, results.Percentile), topLeft.X, width, color,
            ref cursor, scale);
        return cursor.Y + Typography.LineHeight(PrismType.Caption) - topLeft.Y;
    }

    private static void DrawFact(ImDrawListPtr drawList, string text, float left, float width, Vector4 color,
        ref Vector2 cursor, float scale)
    {
        if (text.Length == 0)
        {
            return;
        }

        var fitted = Typography.FitText(text, width, PrismType.Caption);
        var textWidth = Typography.Measure(fitted, PrismType.Caption).X;
        if (cursor.X > left && cursor.X + textWidth > left + width)
        {
            cursor = new Vector2(left, cursor.Y + Typography.LineHeight(PrismType.Caption));
        }

        Typography.Draw(drawList, cursor, fitted, color, PrismType.Caption);
        cursor.X += textWidth + Metrics.Space.Lg * scale;
    }

    private float DrawRankRow(ImDrawListPtr drawList, float left, float top, float width, PrismPlacingDto placing,
        float scale)
    {
        var thumbWidth = RowThumbUnits * scale;
        var thumbHeight = thumbWidth / PrismStore.EntryRatio;
        var pad = Metrics.Space.Sm * scale;
        var bottom = top + thumbHeight + pad * 2f;
        var middle = (top + bottom) * 0.5f;
        var rank = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        Typography.Draw(drawList, new Vector2(left, middle - Typography.LineHeight(PrismType.Headline) * 0.5f), rank,
            PrismArt.Fore, PrismType.Headline);
        var thumbLeft = left + RankColumnUnits * scale;
        DrawLook(drawList,
            new Rect(new Vector2(thumbLeft, top + pad), new Vector2(thumbLeft + thumbWidth, bottom - pad)),
            placing.ImageUrl, false);
        var textLeft = thumbLeft + thumbWidth + Metrics.Space.Md * scale;
        var outfit = new Rect(new Vector2(left, top), new Vector2(thumbLeft + thumbWidth, bottom));
        if (HasOutfit(placing.Outfit) || placing.GlamId.Length > 0)
        {
            HoverWash(drawList, outfit, outfit, Metrics.Radius.Sm * scale);
        }

        TapForOutfit(outfit.Min, outfit.Max, placing);
        TapForProfile(drawList, new Rect(new Vector2(outfit.Max.X, top), new Vector2(left + width, bottom)), placing,
            scale);
        var room = MathF.Max(1f, left + width - textLeft);
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var textTop = middle - (nameLine + Typography.LineHeight(PrismType.Caption)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(placing.DisplayName, room, PrismType.Headline), NameInk(placing), PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameLine),
            Typography.FitText(PlacingDetail(placing), room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        return bottom;
    }

    private void TapForProfile(ImDrawListPtr drawList, Rect hit, PrismPlacingDto placing, float scale)
    {
        if (placing.GlamId.Length == 0 || profileGlamId.Length > 0)
        {
            return;
        }

        if (UiInteract.Click(hit.Min, hit.Max, HoverWash(drawList, hit, hit, Metrics.Radius.Sm * scale)))
        {
            profileGlamId = placing.GlamId;
            glams.Open(new GlamDto(placing.GlamId));
        }
    }

    private Vector4 NameInk(PrismPlacingDto placing) =>
        profileGlamId.Length > 0 && profileGlamId == placing.GlamId ? PrismArt.Muted : PrismArt.Fore;

    private void FollowPlacingProfile()
    {
        if (profileGlamId.Length == 0 || glams.DetailPending)
        {
            return;
        }

        var author = glams.Detail is { } glam && glam.Id == profileGlamId ? glam.Author : null;
        profileGlamId = string.Empty;
        if (author is { Id.Length: > 0 })
        {
            OpenProfile(author);
        }
    }

    private void SyncOrder(PrismResultsDto results)
    {
        if (ReferenceEquals(orderedFor, results))
        {
            return;
        }

        orderedFor = results;
        Fill(finalOrder, results.FinalPodium);
        Fill(heatOrder, results.HeatPodium);
        summaryPhotos.Clear();
        var photos = results.Rankings ?? results.FinalPodium ?? results.HeatPodium ?? Array.Empty<PrismPlacingDto>();
        for (var index = 0; index < photos.Length; index++)
        {
            if (photos[index].ImageUrl.Length == 0)
            {
                continue;
            }

            if (summaryPhotos.Count < 6)
            {
                summaryPhotos.Add(photos[index]);
            }
            else
            {
                var slot = Random.Shared.Next(index + 1);
                if (slot < summaryPhotos.Count)
                {
                    summaryPhotos[slot] = photos[index];
                }
            }
        }
    }

    private static void Fill(List<PrismPlacingDto> into, PrismPlacingDto[]? podium)
    {
        into.Clear();
        if (podium is not null)
        {
            into.AddRange(podium);
        }

        into.Sort(static (left, right) => left.Rank.CompareTo(right.Rank));
    }

    private string PlacingLine(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.MyHeatRank, results.HeatSize);
        return placingText.IsCurrent(key)
            ? placingText.Value
            : placingText.Store(key, results.MyHeatRank + "/" + results.HeatSize);
    }

    private string RevealTitle(PrismResultsDto results, bool summary)
    {
        var label = Loc.T(summary ? L.Apps.Prism : L.Prism.FinalTitle);
        return results.Title.Length == 0 ? label : FactLine(ref revealTitle, label, results.Title);
    }

    private static string PlacingDetail(PrismPlacingDto placing) =>
        placing.World.Length > 0 ? placing.World : placing.Handle;

    private string HeatFact(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.HeatLabel, Loc.Current.Code);
        return heatFact.IsCurrent(key)
            ? heatFact.Value
            : heatFact.Store(key, results.HeatLabel.Length > 0
                ? Loc.T(L.Prism.HeatName, results.HeatLabel)
                : Loc.T(L.Prism.YourHeat));
    }

    private string CoinFact(PrismResultsDto results) => Fact(ref coinFact, L.Prism.CoinsWon, results.CoinsAwarded);

    private static string Fact(ref CachedText cache, LocString format, long value)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(value, Loc.Current.Code);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(format, value));
    }
}
