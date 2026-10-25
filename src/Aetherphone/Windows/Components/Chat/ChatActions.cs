using Aetherphone.Core.Gifs;
using Aetherphone.Core.Maps;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ChatActions
{
    public static void CopyMessageText(ReadOnlySpan<TranscriptMessage> messages, string id, Func<string, bool> canReveal)
    {
        for (var index = 0; index < messages.Length; index++)
        {
            if (messages[index].Id != id)
            {
                continue;
            }

            if (!canReveal(id))
            {
                return;
            }

            ImGui.SetClipboardText(ClipboardText(messages[index].Body ?? string.Empty));
            ShellToast.Show();
            return;
        }
    }

    private static string ClipboardText(string body)
    {
        if (LocationShare.TryParse(body, out var location))
        {
            return LocationShare.Summary(location);
        }

        return GifShare.TryParse(body, out var gif) ? gif.Url : body;
    }
}
