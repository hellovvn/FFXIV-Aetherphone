using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Team;

internal interface ITeamInboxApi
{
    Task<TeamMessagePage?> ListAsync(long before, int limit, CancellationToken token,
        Action<AepFailure>? onFailure = null);

    Task<TeamMessageDto?> SendAsync(string body, CancellationToken token, Action<AepFailure>? onFailure = null);

    Task<bool> MarkReadAsync(CancellationToken token);
}
