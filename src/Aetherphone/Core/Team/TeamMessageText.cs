using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Team;

internal sealed record TeamPrizeArgs(
    string? WeekId = null,
    string? Stage = null,
    string? Track = null,
    int Rank = 0,
    long Gil = 0);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TeamPrizeArgs))]
internal sealed partial class TeamJsonContext : JsonSerializerContext;

internal static class TeamMessageText
{
    private const string StageFinal = "final";
    private const string StageHeat = "heat";
    private const string TrackVanilla = "vanilla";
    private const string TrackModded = "modded";
    private const string WeekMarker = "-W";

    public static bool IsAutomated(TeamMessageDto message) =>
        !string.Equals(message.Kind, TeamMessageKinds.Text, StringComparison.Ordinal);

    public static string Render(TeamMessageDto message)
    {
        switch (message.Kind)
        {
            case TeamMessageKinds.Text:
                return message.Body;
            case TeamMessageKinds.PrismGil:
                return PrizeWon(ParseArgs(message.Args)) ?? Loc.T(L.TeamInbox.AutomatedFallback);
            case TeamMessageKinds.PrismGilPaid:
                return PrizePaid(ParseArgs(message.Args)) ?? Loc.T(L.TeamInbox.AutomatedFallback);
            default:
                return message.Body.Length > 0 ? message.Body : Loc.T(L.TeamInbox.AutomatedFallback);
        }
    }

    public static TeamPrizeArgs? ParseArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(args, TeamJsonContext.Default.TeamPrizeArgs);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public static string WeekLabel(string? weekId)
    {
        if (string.IsNullOrWhiteSpace(weekId))
        {
            return string.Empty;
        }

        var marker = weekId.IndexOf(WeekMarker, StringComparison.OrdinalIgnoreCase);
        if (marker >= 0 && int.TryParse(weekId.AsSpan(marker + WeekMarker.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out var week) && week > 0)
        {
            return week.ToString(Loc.Culture);
        }

        return weekId.Trim();
    }

    public static string Place(int rank) => rank switch
    {
        1 => Loc.T(L.TeamInbox.PlaceFirst),
        2 => Loc.T(L.TeamInbox.PlaceSecond),
        3 => Loc.T(L.TeamInbox.PlaceThird),
        _ => Loc.T(L.TeamInbox.PlaceOther, rank),
    };

    private static string? PrizeWon(TeamPrizeArgs? args)
    {
        if (args is null || args.Rank <= 0 || args.Gil <= 0 || TrackName(args.Track) is not { } track)
        {
            return null;
        }

        var week = WeekLabel(args.WeekId);
        if (week.Length == 0)
        {
            return null;
        }

        var template = StageTemplate(args.Stage);
        if (template is not { } stageTemplate)
        {
            return null;
        }

        return string.Format(Loc.Culture, Loc.T(stageTemplate), Place(args.Rank), track, week,
            NumberText.Group(args.Gil));
    }

    private static string? PrizePaid(TeamPrizeArgs? args)
    {
        if (args is null || args.Gil <= 0)
        {
            return null;
        }

        var week = WeekLabel(args.WeekId);
        if (week.Length == 0)
        {
            return null;
        }

        return Loc.T(L.TeamInbox.GilPaid, NumberText.Group(args.Gil), week);
    }

    private static LocString? StageTemplate(string? stage)
    {
        if (string.Equals(stage, StageFinal, StringComparison.OrdinalIgnoreCase))
        {
            return L.TeamInbox.GilWonFinal;
        }

        if (string.Equals(stage, StageHeat, StringComparison.OrdinalIgnoreCase))
        {
            return L.TeamInbox.GilWonHeat;
        }

        return null;
    }

    private static string? TrackName(string? track)
    {
        if (string.Equals(track, TrackVanilla, StringComparison.OrdinalIgnoreCase))
        {
            return Loc.T(L.Prism.TrackVanilla);
        }

        if (string.Equals(track, TrackModded, StringComparison.OrdinalIgnoreCase))
        {
            return Loc.T(L.Prism.TrackModded);
        }

        return null;
    }
}
