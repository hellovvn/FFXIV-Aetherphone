using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal readonly record struct StoryStickerUpdate(string StoryId, StoryStickerDto Sticker);

internal static class StoryStickerList
{
    public static StoryDto WithSticker(StoryDto story, StoryStickerDto sticker)
    {
        if (story.Stickers is not { } stickers)
        {
            return story;
        }

        for (var index = 0; index < stickers.Length; index++)
        {
            if (stickers[index].Id != sticker.Id)
            {
                continue;
            }

            var replaced = (StoryStickerDto[])stickers.Clone();
            replaced[index] = sticker;
            return story with { Stickers = replaced };
        }

        return story;
    }

    public static StoryDto[] Patch(StoryDto[] stories, string storyId, StoryStickerDto sticker)
    {
        for (var index = 0; index < stories.Length; index++)
        {
            if (stories[index].Id != storyId)
            {
                continue;
            }

            var updated = WithSticker(stories[index], sticker);
            if (ReferenceEquals(updated, stories[index]))
            {
                return stories;
            }

            var patched = (StoryDto[])stories.Clone();
            patched[index] = updated;
            return patched;
        }

        return stories;
    }

    public static StoryStickerDto? Question(StoryDto story)
    {
        if (story.Stickers is not { } stickers)
        {
            return null;
        }

        for (var index = 0; index < stickers.Length; index++)
        {
            if (stickers[index].Kind == StoryStickerKinds.Question)
            {
                return stickers[index];
            }
        }

        return null;
    }

    public static bool HasResults(StoryDto story)
    {
        if (story.Stickers is not { } stickers)
        {
            return false;
        }

        for (var index = 0; index < stickers.Length; index++)
        {
            if (stickers[index].Kind is StoryStickerKinds.Poll or StoryStickerKinds.Question)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class StoryResponseList
{
    public static StoryResponseDto[] Append(StoryResponseDto[] source, StoryResponseDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return source;
        }

        var fresh = new List<StoryResponseDto>(incoming.Length);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (!Contains(source, incoming[index]))
            {
                fresh.Add(incoming[index]);
            }
        }

        if (fresh.Count == 0)
        {
            return source;
        }

        var result = new StoryResponseDto[source.Length + fresh.Count];
        Array.Copy(source, result, source.Length);
        fresh.CopyTo(result, source.Length);
        return result;
    }

    public static StoryResponseDto[] Without(StoryResponseDto[] source, string responseId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Kind != StoryStickerKinds.Question || source[index].Id != responseId)
            {
                continue;
            }

            var kept = new StoryResponseDto[source.Length - 1];
            Array.Copy(source, 0, kept, 0, index);
            Array.Copy(source, index + 1, kept, index, source.Length - index - 1);
            return kept;
        }

        return source;
    }

    public static StoryResponseDto[] WithoutUser(StoryResponseDto[] source, string userId)
    {
        var kept = new List<StoryResponseDto>(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].UserId != userId)
            {
                kept.Add(source[index]);
            }
        }

        return kept.Count == source.Length ? source : kept.ToArray();
    }

    private static bool Contains(StoryResponseDto[] source, StoryResponseDto candidate)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Kind == candidate.Kind && source[index].Id == candidate.Id)
            {
                return true;
            }
        }

        return false;
    }
}
