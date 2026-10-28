using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class SocialClient
{
    public Task<MutedWordList?> MutedWordsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/muted-words", AethernetJsonContext.Default.MutedWordList, token, null, onFailure);
    }

    public Task<MutedWordDto?> AddMutedWordAsync(SaveMutedWordRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/muted-words", request, AethernetJsonContext.Default.SaveMutedWordRequest,
            AethernetJsonContext.Default.MutedWordDto, token, null, onFailure);
    }

    public Task<MutedWordDto?> UpdateMutedWordAsync(string wordId, SaveMutedWordRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/muted-words/{Uri.EscapeDataString(wordId)}", request,
            AethernetJsonContext.Default.SaveMutedWordRequest, AethernetJsonContext.Default.MutedWordDto, token, null,
            onFailure);
    }

    public Task<bool> DeleteMutedWordAsync(string wordId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/muted-words/{Uri.EscapeDataString(wordId)}", token, null, onFailure);
    }
}
