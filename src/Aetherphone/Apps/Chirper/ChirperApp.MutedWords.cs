using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float HashtagMuteIconSize = 18f;

    private readonly MutedWordsScreen mutedWords = new();
    private string hashtagMuteTag = string.Empty;
    private string hashtagMuteLabel = string.Empty;
    private LanguageInfo? hashtagMuteLanguage;

    private bool MutedWordSheetCaptures => mutedWords.CapturesPointer;

    private void OpenMutedWords()
    {
        actions.Reset();
        mutedWords.Open(store);
        router.Push(ChirperRoute.MutedWords);
    }

    private void DrawMutedWords(Rect area) =>
        mutedWords.Draw(area, store, ui, ChirperInk.Shared, back, ScreenTitleStyle, false);

    private void DrawMutedWordSheet(Rect screen) =>
        mutedWords.DrawSheet(screen, theme, ChirperInk.Shared, store, toast);

    private void DrawHashtagMute(Rect area, string tag)
    {
        var scale = UiScale.Current;
        if (SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, 0),
                SocialChrome.HeaderIconRadius * scale, PhoneIcons.BellOff, HashtagMuteIconSize,
                HashtagMuteLabel(tag), ChirperInk.Shared, ChirperInk.BodyInk))
        {
            MutedWordsScreen.MuteHashtag(store, toast, tag);
        }
    }

    private string HashtagMuteLabel(string tag)
    {
        if (!string.Equals(hashtagMuteTag, tag, StringComparison.Ordinal)
            || !ReferenceEquals(hashtagMuteLanguage, Loc.Current))
        {
            hashtagMuteTag = tag;
            hashtagMuteLanguage = Loc.Current;
            hashtagMuteLabel = Loc.T(L.Social.MuteNamed, "#" + tag);
        }

        return hashtagMuteLabel;
    }
}
