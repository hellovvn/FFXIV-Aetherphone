namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record TeamMessageDto(
    long Id = 0,
    bool FromTeam = false,
    string Kind = "",
    string Body = "",
    string Args = "",
    string StaffName = "",
    DateTime CreatedAt = default,
    bool Read = false)
{
    public long CreatedAtUnix
    {
        get
        {
            if (CreatedAt == default)
            {
                return 0;
            }

            var utc = CreatedAt.Kind switch
            {
                DateTimeKind.Utc => CreatedAt,
                DateTimeKind.Local => CreatedAt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            };
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }
    }
}

internal sealed record TeamMessagePage(TeamMessageDto[]? Messages = null, int Unread = 0, bool CanReply = false,
    bool CanDelete = false, long ClearedThroughId = 0);

internal sealed record SendTeamMessageRequest(string Body);

internal sealed record TeamConversationClearedDto(long ClearedThroughId = 0);

internal static class TeamMessageKinds
{
    public const string Text = "text";
    public const string PrismGil = "prism_gil";
    public const string PrismGilPaid = "prism_gil_paid";
}
