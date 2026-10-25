using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Team;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class TeamClient : ITeamInboxApi
{
    private const string MessagesPath = "/team/messages";
    private const string ReadPath = "/team/messages/read";

    private readonly AethernetTransport net;

    public TeamClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<TeamMessagePage?> ListAsync(long before, int limit, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = before > 0
            ? $"{MessagesPath}?before={before}&limit={limit}"
            : $"{MessagesPath}?limit={limit}";
        return net.GetAsync(path, AethernetJsonContext.Default.TeamMessagePage, token, null, onFailure);
    }

    public Task<TeamMessageDto?> SendAsync(string body, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(MessagesPath, new SendTeamMessageRequest(body),
            AethernetJsonContext.Default.SendTeamMessageRequest, AethernetJsonContext.Default.TeamMessageDto, token,
            null, onFailure);
    }

    public Task<bool> MarkReadAsync(CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, ReadPath, token);
    }
}
