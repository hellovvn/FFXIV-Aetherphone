using System.Security.Cryptography;
using System.Text.Json;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

internal sealed record MutedWordVectorEntry(
    string Phrase,
    bool InFeeds = true,
    bool InNotifications = true,
    bool OnlyNotFollowing = false,
    long? ExpiresInSeconds = null);

internal sealed record MutedWordVectorCase(
    string Name,
    MutedWordVectorEntry[] Entries,
    string Text,
    string Scope,
    bool Followed,
    bool Hidden);

internal sealed record MutedWordVectorPhrase(string Input, string Normalized, bool Valid);

internal sealed record MutedWordVectors(int Version, MutedWordVectorCase[] Cases, MutedWordVectorPhrase[] Phrases);

// The server's Aethernet.Tests/Vectors/muted-words.json, copied byte for byte. The hash pins the copy, so a rule
// change lands in both repos with the same new hash.
public sealed class MutedWordMatcherTests
{
    private const string FileHash = "7a03f0f95e9f7fdca768572c7a8eadaf5c8f9aa53bb8c214bf371f55b19e42ee";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static string VectorPath => Path.Combine(AppContext.BaseDirectory, "Vectors", "muted-words.json");

    [Fact]
    public void TheVectorsAreTheServersCopy()
    {
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(VectorPath))).ToLowerInvariant();
        Assert.Equal(FileHash, hash);
    }

    [Fact]
    public void EveryCaseHidesExactlyWhatTheVectorSays()
    {
        var vectors = Load();
        Assert.True(vectors.Cases.Length >= 50);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var failures = new List<string>();
        foreach (var vector in vectors.Cases)
        {
            var rules = vector.Entries.Select(entry => new MutedWordRule(entry.Phrase, entry.InFeeds,
                entry.InNotifications, entry.OnlyNotFollowing,
                entry.ExpiresInSeconds is { } seconds ? now + seconds : null)).ToArray();
            var matcher = MutedWordMatcher.Compile(rules, now);
            var scope = vector.Scope == "notifications" ? MutedWordScope.Notifications : MutedWordScope.Feeds;
            if (matcher.Hides(vector.Text, scope, vector.Followed) != vector.Hidden)
            {
                failures.Add(vector.Name);
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void EveryPhraseNormalizesAndValidatesAsTheVectorSays()
    {
        foreach (var phrase in Load().Phrases)
        {
            var normalized = MutedWordMatcher.Normalize(phrase.Input);
            Assert.Equal(phrase.Normalized, normalized);
            Assert.Equal(phrase.Valid, MutedWordMatcher.IsValid(normalized));
        }
    }

    [Fact]
    public void MatchingAPostAllocatesNothing()
    {
        var matcher = MutedWordMatcher.Compile(new[]
        {
            new MutedWordRule("final fantasy", true, true, false, null),
            new MutedWordRule("#raid", true, true, true, null),
            new MutedWordRule("零式", true, true, false, null),
        }, 0);
        const string shortText = "Anyone up for the Raid tonight? Bring potions and a good attitude #static";
        var longText = new string('x', 900) + " final   Fantasy";
        Assert.False(matcher.Hides(shortText, MutedWordScope.Feeds, false));
        Assert.True(matcher.Hides(longText, MutedWordScope.Feeds, false));

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 1000; pass++)
        {
            matcher.Hides(shortText, MutedWordScope.Feeds, false);
            matcher.Hides(longText, MutedWordScope.Feeds, true);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static MutedWordVectors Load()
    {
        return JsonSerializer.Deserialize<MutedWordVectors>(File.ReadAllText(VectorPath), Options)!;
    }
}
