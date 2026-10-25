namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GifConfigDto(
    bool Enabled,
    string? AppKey = null,
    string? CustomerId = null,
    string? ContentFilter = null);
