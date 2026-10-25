using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GifsClient
{
    private readonly AethernetTransport net;

    public GifsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<GifConfigDto?> ConfigAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/gifs/config", AethernetJsonContext.Default.GifConfigDto, token, null, onFailure);
    }
}
