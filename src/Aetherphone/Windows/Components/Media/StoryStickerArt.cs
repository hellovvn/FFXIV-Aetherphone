using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct StickerPollFace(
    string Question,
    string FirstOption,
    string SecondOption,
    string FirstPercent,
    string SecondPercent,
    float FirstFraction,
    float SecondFraction,
    bool ShowResults,
    int Chosen,
    int Hovered,
    Vector4 Accent);

internal readonly record struct StickerQuestionFace(string Prompt, string Footer, bool FooterDone, Vector4 Accent);

internal static class StoryStickerArt
{
    public const float CardWidth = 196f;

    private const float Padding = 12f;
    private const float CardRadius = 16f;
    private const float OptionHeight = 34f;
    private const float OptionGap = 6f;
    private const float QuestionGap = 8f;
    private const float FieldHeight = 30f;
    private const float FieldInset = 10f;
    private const float MentionHeight = 30f;
    private const float MentionPadding = 12f;
    private const float ShadowOffset = 2f;

    private static readonly Vector4 CardFill = new(1f, 1f, 1f, 0.97f);
    private static readonly Vector4 CardInk = new(0.09f, 0.09f, 0.11f, 1f);
    private static readonly Vector4 MutedInk = new(0.44f, 0.44f, 0.48f, 1f);
    private static readonly Vector4 OptionFill = new(0.93f, 0.93f, 0.95f, 1f);
    private static readonly Vector4 ShadowFill = new(0f, 0f, 0f, 0.22f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static TextStyle QuestionStyle(float zoom) => new(TextStyles.Headline.Scale * zoom, FontWeight.SemiBold);

    public static TextStyle OptionStyle(float zoom) =>
        new(TextStyles.SubheadlineEmphasized.Scale * zoom, FontWeight.SemiBold);

    public static TextStyle FooterStyle(float zoom) => new(TextStyles.Footnote.Scale * zoom, FontWeight.Regular);

    public static Vector2 PollSize(string question, float zoom, float uiScale)
    {
        var unit = zoom * uiScale;
        var width = CardWidth * unit;
        var height = (Padding * 2f + OptionHeight * 2f + OptionGap) * unit;
        if (question.Length > 0)
        {
            height += Typography.MeasureWrappedBlock(question, QuestionStyle(zoom), width - Padding * 2f * unit).Y
                      + QuestionGap * unit;
        }

        return new Vector2(width, height);
    }

    public static Vector2 QuestionSize(string prompt, float zoom, float uiScale)
    {
        var unit = zoom * uiScale;
        var width = CardWidth * unit;
        var promptHeight = Typography.MeasureWrappedBlock(prompt, QuestionStyle(zoom), width - Padding * 2f * unit).Y;
        return new Vector2(width, promptHeight + (Padding * 3f + FieldHeight) * unit);
    }

    public static Vector2 MentionSize(string label, float zoom, float uiScale)
    {
        var unit = zoom * uiScale;
        var textWidth = Typography.Measure(label, QuestionStyle(zoom)).X;
        return new Vector2(textWidth + MentionPadding * 2f * unit, MentionHeight * unit);
    }

    public static Rect Centered(Vector2 center, Vector2 size) => new(center - size * 0.5f, center + size * 0.5f);

    public static Rect DrawPoll(ImDrawListPtr drawList, Vector2 center, float zoom, float uiScale,
        in StickerPollFace face, out Rect firstOption, out Rect secondOption)
    {
        var unit = zoom * uiScale;
        var card = Centered(center, PollSize(face.Question, zoom, uiScale));
        DrawCard(drawList, card, unit);
        var inner = card.Width - Padding * 2f * unit;
        var top = card.Min.Y + Padding * unit;
        if (face.Question.Length > 0)
        {
            top += Typography.DrawWrappedCentered(new Vector2(card.Center.X, top), face.Question, CardInk,
                QuestionStyle(zoom), inner) + QuestionGap * unit;
        }

        var left = card.Min.X + Padding * unit;
        firstOption = new Rect(new Vector2(left, top), new Vector2(left + inner, top + OptionHeight * unit));
        var secondTop = firstOption.Max.Y + OptionGap * unit;
        secondOption = new Rect(new Vector2(left, secondTop), new Vector2(left + inner, secondTop + OptionHeight * unit));
        DrawOption(drawList, firstOption, zoom, unit, face.FirstOption, face.FirstPercent, face.FirstFraction,
            face, 0);
        DrawOption(drawList, secondOption, zoom, unit, face.SecondOption, face.SecondPercent, face.SecondFraction,
            face, 1);
        return card;
    }

    public static Rect DrawQuestion(ImDrawListPtr drawList, Vector2 center, float zoom, float uiScale,
        in StickerQuestionFace face, bool hovered)
    {
        var unit = zoom * uiScale;
        var card = Centered(center, QuestionSize(face.Prompt, zoom, uiScale));
        DrawCard(drawList, card, unit);
        var inner = card.Width - Padding * 2f * unit;
        var fieldTop = card.Max.Y - (Padding + FieldHeight) * unit;
        var bandMax = new Vector2(card.Max.X, fieldTop - Padding * 0.5f * unit);
        drawList.AddRectFilled(card.Min, bandMax, ImGui.GetColorU32(face.Accent), CardRadius * unit,
            ImDrawFlags.RoundCornersTop);
        Typography.DrawWrappedCentered(new Vector2(card.Center.X, card.Min.Y + Padding * unit), face.Prompt, White,
            QuestionStyle(zoom), inner);
        var fieldMin = new Vector2(card.Min.X + Padding * unit, fieldTop);
        var fieldMax = new Vector2(card.Max.X - Padding * unit, fieldTop + FieldHeight * unit);
        var fieldFill = hovered ? Palette.WithAlpha(face.Accent, 0.16f) : OptionFill;
        Squircle.Fill(drawList, fieldMin, fieldMax, FieldHeight * 0.5f * unit, ImGui.GetColorU32(fieldFill));
        var footerStyle = FooterStyle(zoom);
        var footer = Typography.FitText(face.Footer, fieldMax.X - fieldMin.X - FieldInset * 2f * unit, footerStyle);
        var footerSize = Typography.Measure(footer, footerStyle);
        Typography.Draw(drawList,
            new Vector2((fieldMin.X + fieldMax.X - footerSize.X) * 0.5f, (fieldMin.Y + fieldMax.Y - footerSize.Y) * 0.5f),
            footer, face.FooterDone ? face.Accent : MutedInk, footerStyle);
        return card;
    }

    public static Rect DrawMention(ImDrawListPtr drawList, Vector2 center, float zoom, float uiScale, string label,
        Vector4 accent, bool hovered)
    {
        var unit = zoom * uiScale;
        var pill = Centered(center, MentionSize(label, zoom, uiScale));
        var radius = pill.Height * 0.5f;
        Squircle.Fill(drawList, pill.Min + new Vector2(0f, ShadowOffset * unit), pill.Max + new Vector2(0f, ShadowOffset * unit),
            radius, ImGui.GetColorU32(ShadowFill));
        Squircle.Fill(drawList, pill.Min, pill.Max, radius, ImGui.GetColorU32(hovered ? White : CardFill));
        var style = QuestionStyle(zoom);
        var size = Typography.Measure(label, style);
        Typography.Draw(drawList, pill.Center - size * 0.5f, label, accent, style);
        return pill;
    }

    private static void DrawCard(ImDrawListPtr drawList, Rect card, float unit)
    {
        var radius = CardRadius * unit;
        var drop = new Vector2(0f, ShadowOffset * unit);
        Squircle.Fill(drawList, card.Min + drop, card.Max + drop, radius, ImGui.GetColorU32(ShadowFill));
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(CardFill));
    }

    private static void DrawOption(ImDrawListPtr drawList, Rect row, float zoom, float unit, string label,
        string percent, float fraction, in StickerPollFace face, int optionIndex)
    {
        var radius = row.Height * 0.5f;
        var hovered = face.Hovered == optionIndex && !face.ShowResults && face.Chosen < 0;
        Squircle.Fill(drawList, row.Min, row.Max, radius,
            ImGui.GetColorU32(hovered ? Palette.WithAlpha(face.Accent, 0.18f) : OptionFill));
        var chosen = face.Chosen == optionIndex;
        if (face.ShowResults && fraction > 0f)
        {
            var fillMax = new Vector2(row.Min.X + MathF.Max(row.Height, row.Width * Math.Clamp(fraction, 0f, 1f)), row.Max.Y);
            Squircle.Fill(drawList, row.Min, fillMax, radius,
                ImGui.GetColorU32(Palette.WithAlpha(face.Accent, chosen ? 0.38f : 0.18f)));
        }

        var style = OptionStyle(zoom);
        var inset = Padding * unit;
        var ink = chosen && !face.ShowResults ? face.Accent : CardInk;
        if (!face.ShowResults)
        {
            var fitted = Typography.FitText(label, row.Width - inset * 2f, style);
            var size = Typography.Measure(fitted, style);
            Typography.Draw(drawList, row.Center - size * 0.5f, fitted, ink, style);
            return;
        }

        var percentSize = Typography.Measure(percent, style);
        var labelWidth = row.Width - inset * 3f - percentSize.X;
        var labelText = Typography.FitText(label, MathF.Max(1f, labelWidth), style);
        var labelSize = Typography.Measure(labelText, style);
        Typography.Draw(drawList, new Vector2(row.Min.X + inset, row.Center.Y - labelSize.Y * 0.5f), labelText, ink,
            style);
        Typography.Draw(drawList, new Vector2(row.Max.X - inset - percentSize.X, row.Center.Y - percentSize.Y * 0.5f),
            percent, ink, style);
    }
}
