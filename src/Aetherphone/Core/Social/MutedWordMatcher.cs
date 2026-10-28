using System.Buffers;

namespace Aetherphone.Core.Social;

internal enum MutedWordScope : byte
{
    Feeds,
    Notifications,
}

internal readonly record struct MutedWordRule(
    string Phrase,
    bool InFeeds,
    bool InNotifications,
    bool OnlyNotFollowing,
    long? ExpiresAtUnix);

// One account's muted words, compiled once and then matched against post and reply text. The server keeps a
// line-for-line copy (Aethernet.Api/Common/MutedWordMatcher.cs) and both repos replay Vectors/muted-words.json, so a
// post the client hides at once is the post the server stops sending.
internal sealed class MutedWordMatcher
{
    public const int MaxPhraseLength = 60;

    public const int MaxEntries = 100;

    private const int StackBufferLength = 512;

    public static readonly MutedWordMatcher Empty = new(Array.Empty<CompiledRule>(), long.MaxValue);

    private readonly CompiledRule[] rules;

    private MutedWordMatcher(CompiledRule[] rules, long nextExpiryUnix)
    {
        this.rules = rules;
        NextExpiryUnix = nextExpiryUnix;
        for (var index = 0; index < rules.Length; index++)
        {
            HasFeedRules |= rules[index].InFeeds;
            HasNotificationRules |= rules[index].InNotifications;
        }
    }

    public bool IsEmpty => rules.Length == 0;

    public bool HasFeedRules { get; }

    public bool HasNotificationRules { get; }

    public long NextExpiryUnix { get; }

    public static MutedWordMatcher Compile(IReadOnlyList<MutedWordRule> entries, long nowUnix)
    {
        if (entries.Count == 0)
        {
            return Empty;
        }

        var compiled = new List<CompiledRule>(entries.Count);
        var nextExpiry = long.MaxValue;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.ExpiresAtUnix is { } expires && expires <= nowUnix)
            {
                continue;
            }

            var phrase = Normalize(entry.Phrase);
            if (!IsValid(phrase) || (!entry.InFeeds && !entry.InNotifications))
            {
                continue;
            }

            compiled.Add(new CompiledRule(phrase.ToLowerInvariant().Split(' '), ContainsCjk(phrase), entry.InFeeds,
                entry.InNotifications, entry.OnlyNotFollowing));
            if (entry.ExpiresAtUnix is { } expiry && expiry < nextExpiry)
            {
                nextExpiry = expiry;
            }
        }

        return compiled.Count == 0 ? Empty : new MutedWordMatcher(compiled.ToArray(), nextExpiry);
    }

    public static string Normalize(string? phrase)
    {
        if (string.IsNullOrEmpty(phrase))
        {
            return string.Empty;
        }

        var span = phrase.AsSpan().Trim();
        var collapsed = new char[span.Length];
        var length = 0;
        var pendingSpace = false;
        for (var index = 0; index < span.Length; index++)
        {
            var character = span[index];
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                collapsed[length++] = ' ';
                pendingSpace = false;
            }

            collapsed[length++] = character;
        }

        return new string(collapsed, 0, length);
    }

    // A hashtag entry is the tag alone: # and then only letters, digits and underscores, as HashtagText reads them.
    public static bool IsValid(string normalized)
    {
        if (normalized.Length == 0 || normalized.Length > MaxPhraseLength)
        {
            return false;
        }

        if (normalized[0] != '#')
        {
            return true;
        }

        if (normalized.Length == 1)
        {
            return false;
        }

        for (var index = 1; index < normalized.Length; index++)
        {
            if (!IsWordCharacter(normalized[index]))
            {
                return false;
            }
        }

        return true;
    }

    public bool Hides(string? text, MutedWordScope scope, bool authorFollowed)
    {
        if (string.IsNullOrEmpty(text) || !Covers(scope, authorFollowed))
        {
            return false;
        }

        if (text.Length <= StackBufferLength)
        {
            Span<char> buffer = stackalloc char[text.Length];
            text.AsSpan().ToLowerInvariant(buffer);
            return HidesLowered(buffer, scope, authorFollowed);
        }

        var rented = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            var lowered = rented.AsSpan(0, text.Length);
            text.AsSpan().ToLowerInvariant(lowered);
            return HidesLowered(lowered, scope, authorFollowed);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    public bool HidesLowered(ReadOnlySpan<char> lowered, MutedWordScope scope, bool authorFollowed)
    {
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (rule.Applies(scope, authorFollowed) && Matches(lowered, rule.Words, rule.Substring))
            {
                return true;
            }
        }

        return false;
    }

    // True when some rule would hide this text only because the author is not followed, so the caller knows a
    // follow lookup can change the answer.
    public bool HasNotFollowingRules(MutedWordScope scope)
    {
        for (var index = 0; index < rules.Length; index++)
        {
            if (rules[index].OnlyNotFollowing && rules[index].Applies(scope, false))
            {
                return true;
            }
        }

        return false;
    }

    private bool Covers(MutedWordScope scope, bool authorFollowed)
    {
        for (var index = 0; index < rules.Length; index++)
        {
            if (rules[index].Applies(scope, authorFollowed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(ReadOnlySpan<char> text, string[] words, bool substring)
    {
        var first = words[0];
        var from = 0;
        while (from <= text.Length - first.Length)
        {
            var found = text[from..].IndexOf(first.AsSpan());
            if (found < 0)
            {
                return false;
            }

            var start = from + found;
            if (MatchesAt(text, start, words, substring))
            {
                return true;
            }

            from = start + 1;
        }

        return false;
    }

    private static bool MatchesAt(ReadOnlySpan<char> text, int start, string[] words, bool substring)
    {
        if (!substring && start > 0 && IsWordCharacter(text[start - 1]))
        {
            return false;
        }

        var position = start + words[0].Length;
        for (var wordIndex = 1; wordIndex < words.Length; wordIndex++)
        {
            if (position >= text.Length || !char.IsWhiteSpace(text[position]))
            {
                return false;
            }

            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            var word = words[wordIndex];
            if (!text[position..].StartsWith(word.AsSpan()))
            {
                return false;
            }

            position += word.Length;
        }

        return substring || position >= text.Length || !IsWordCharacter(text[position]);
    }

    private static bool IsWordCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character == '_';
    }

    // Japanese and Chinese run words together without spaces, so an entry in either script matches anywhere.
    private static bool ContainsCjk(string phrase)
    {
        for (var index = 0; index < phrase.Length; index++)
        {
            var character = phrase[index];
            if ((character >= '぀' && character <= 'ヿ')
                || (character >= 'ㇰ' && character <= 'ㇿ')
                || (character >= '㐀' && character <= '䶿')
                || (character >= '一' && character <= '鿿')
                || (character >= '豈' && character <= '﫿')
                || (character >= 'ｦ' && character <= 'ﾟ')
                || character == '々')
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct CompiledRule(
        string[] Words,
        bool Substring,
        bool InFeeds,
        bool InNotifications,
        bool OnlyNotFollowing)
    {
        public bool Applies(MutedWordScope scope, bool authorFollowed)
        {
            var inScope = scope == MutedWordScope.Feeds ? InFeeds : InNotifications;
            return inScope && !(OnlyNotFollowing && authorFollowed);
        }
    }
}
