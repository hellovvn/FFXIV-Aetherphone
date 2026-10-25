namespace Aetherphone.Windows.Components;

internal static class LineBreakRules
{
    private const string NoLineStart = "。、，．！？：；）」』】〕〉》｝］〙〗’”…‥ー・ゝゞヽヾぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ々〻";
    private const string NoLineEnd = "（「『【〔〈《｛［〘〖‘“";

    public static bool CannotStartLine(char value) => NoLineStart.Contains(value);

    public static bool CannotEndLine(char value) => NoLineEnd.Contains(value);

    public static bool KeepsWithNext(char last, string next) =>
        (next.Length == 1 && CannotStartLine(next[0])) || CannotEndLine(last);
}
