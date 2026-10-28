using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Reels;

// The server half of sharing a reel: two upload slots, two uploads, one create. A seam so the share pipeline runs
// against a fake in tests.
internal interface IReelPublisher
{
    Task<UploadUrlResponse?> ReserveAsync(string contentType, string scope, CancellationToken token,
        Action<AepFailure> onFailure);

    Task<bool> UploadFileAsync(string uploadUrl, string path, string contentType, IProgress<float> progress,
        CancellationToken token, Action<AepFailure> onFailure);

    Task<bool> UploadBytesAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken token,
        Action<AepFailure> onFailure);

    Task<PostDto?> CreateAsync(CreateReelRequest request, CancellationToken token, Action<AepFailure> onFailure);
}

// The create step is handed in, so a reel posts as a gram and a chirp video posts as a chirp over the same uploads.
internal sealed class AethernetReelPublisher : IReelPublisher
{
    private readonly MediaClient media;
    private readonly Func<CreateReelRequest, CancellationToken, Action<AepFailure>, Task<PostDto?>> create;

    public AethernetReelPublisher(MediaClient media,
        Func<CreateReelRequest, CancellationToken, Action<AepFailure>, Task<PostDto?>> create)
    {
        this.media = media;
        this.create = create;
    }

    public Task<UploadUrlResponse?> ReserveAsync(string contentType, string scope, CancellationToken token,
        Action<AepFailure> onFailure) =>
        media.UploadUrlAsync(contentType, scope, token, onFailure);

    public Task<bool> UploadFileAsync(string uploadUrl, string path, string contentType, IProgress<float> progress,
        CancellationToken token, Action<AepFailure> onFailure) =>
        media.UploadFileAsync(uploadUrl, path, contentType, progress, token, onFailure);

    public Task<bool> UploadBytesAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken token,
        Action<AepFailure> onFailure) =>
        media.UploadImageAsync(uploadUrl, bytes, contentType, token, onFailure);

    public Task<PostDto?> CreateAsync(CreateReelRequest request, CancellationToken token,
        Action<AepFailure> onFailure) =>
        create(request, token, onFailure);
}
