using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Media;

internal static class PostMedia
{
    public const int MaxPhotos = 8;

    public static string[] Photos(string[]? mediaUrls, string? mediaUrl)
    {
        if (mediaUrls is { Length: > 0 })
        {
            return mediaUrls;
        }

        return string.IsNullOrEmpty(mediaUrl) ? Array.Empty<string>() : new[] { mediaUrl };
    }

    public static string[] Photos(PostDto post)
    {
        return post.GifMedia ?? Photos(post.MediaUrls, post.MediaUrl);
    }
}
