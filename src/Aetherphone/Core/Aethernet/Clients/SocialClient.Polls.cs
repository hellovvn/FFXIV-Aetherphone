using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class SocialClient
{
    public Task<PostDto?> CreatePollPostAsync(string text, PostPollInput poll, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/posts", new CreatePostRequest(text, Poll: poll),
            AethernetJsonContext.Default.CreatePostRequest, AethernetJsonContext.Default.PostDto, token, null,
            onFailure);
    }

    public Task<PostPollDto?> VotePostPollAsync(string postId, int option, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}/poll/vote",
            new PollVoteRequest(option), AethernetJsonContext.Default.PollVoteRequest,
            AethernetJsonContext.Default.PostPollDto, token, null, onFailure);
    }
}
