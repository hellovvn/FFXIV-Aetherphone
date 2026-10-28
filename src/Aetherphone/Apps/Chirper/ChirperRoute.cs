using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperScreen
{
    Home,
    Compose,
    Profile,
    EditProfile,
    Avatar,
    Banner,
    Discover,
    Thread,
    UserList,
    Activity,
    Hashtag,
    BadgeProgress,
    MutedAccounts,
    MutedWords,
    EditChirp,
    ShareToChat,
}

internal readonly record struct ChirperRoute(
    ChirperScreen Screen,
    string? UserId = null,
    string? PostId = null,
    UserListKind Kind = UserListKind.Followers,
    string? Tag = null)
{
    public static readonly ChirperRoute Home = new(ChirperScreen.Home);
    public static readonly ChirperRoute Compose = new(ChirperScreen.Compose);
    public static readonly ChirperRoute EditProfile = new(ChirperScreen.EditProfile);
    public static readonly ChirperRoute Avatar = new(ChirperScreen.Avatar);
    public static readonly ChirperRoute Banner = new(ChirperScreen.Banner);
    public static readonly ChirperRoute Discover = new(ChirperScreen.Discover);
    public static readonly ChirperRoute Activity = new(ChirperScreen.Activity);
    public static readonly ChirperRoute BadgeProgress = new(ChirperScreen.BadgeProgress);
    public static readonly ChirperRoute MutedAccounts = new(ChirperScreen.MutedAccounts);
    public static readonly ChirperRoute MutedWords = new(ChirperScreen.MutedWords);
    public static ChirperRoute Profile(string userId) => new(ChirperScreen.Profile, userId);
    public static ChirperRoute Thread(string postId) => new(ChirperScreen.Thread, PostId: postId);
    public static ChirperRoute Hashtag(string tag) => new(ChirperScreen.Hashtag, Tag: tag);
    public static ChirperRoute EditChirp(string postId) => new(ChirperScreen.EditChirp, PostId: postId);
    public static ChirperRoute ShareToChat(string postId) => new(ChirperScreen.ShareToChat, PostId: postId);

    public static ChirperRoute UserList(string sourceId, UserListKind kind) =>
        new(ChirperScreen.UserList, UserId: sourceId, Kind: kind);
}
