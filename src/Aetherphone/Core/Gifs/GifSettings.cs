using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Gifs;

internal sealed record GifSettings(string AppKey, string CustomerId, string ContentFilter)
{
    public static GifSettings? From(GifConfigDto? config)
    {
        if (config is not { Enabled: true } || string.IsNullOrWhiteSpace(config.AppKey)
            || string.IsNullOrWhiteSpace(config.CustomerId) || string.IsNullOrWhiteSpace(config.ContentFilter))
        {
            return null;
        }

        return new GifSettings(config.AppKey, config.CustomerId, config.ContentFilter);
    }
}
