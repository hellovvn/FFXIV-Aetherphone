using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private readonly MutedWordsScreen mutedWords = new();

    private bool MutedWordSheetCaptures => mutedWords.CapturesPointer;

    private void OpenMutedWords()
    {
        mutedWords.Open(store);
        router.Push(AethergramRoute.MutedWords);
    }

    private void DrawMutedWords(Rect area) => mutedWords.Draw(area, store, ui, Ink, back, ScreenTitleStyle, true);

    private void DrawMutedWordSheet(Rect screen) => mutedWords.DrawSheet(screen, theme, Ink, store, toast);
}
