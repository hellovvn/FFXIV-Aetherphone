using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float ConnectionsRowHeight = 64f;
    private const float ConnectionsAvatarRadius = 22f;
    private const int ConnectionsSearchMaxLength = 40;

    private static readonly SocialUserRowStyle ConnectionsRowStyle = new(ConnectionsRowHeight,
        ConnectionsAvatarRadius, SocialChrome.CellPadX, 0f, 0f, TextStyles.Headline, TextStyles.Subheadline,
        VelvetTheme.HoverWash);

    private readonly List<VelvetConnectionDto> connectionMatches = new();
    private string connectionsQuery = string.Empty;
    private string connectionsMatchedQuery = string.Empty;
    private VelvetConnectionDto[]? connectionsMatchedSource;

    private void OpenConnections()
    {
        connectionsQuery = string.Empty;
        connectionsMatchedQuery = string.Empty;
        connectionsMatchedSource = null;
        connectionMatches.Clear();
        store.RefreshConnections();
        router.Push(VelvetView.Connections);
    }

    private void DrawConnections(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.ProfileConnections)))
        {
            router.Pop();
            return;
        }

        if (!store.ConnectionsLoaded && !store.LoadingConnections)
        {
            store.RefreshConnections();
        }

        var pad = SocialChrome.CellPadX * scale;
        var fieldTop = area.Min.Y + VHeader.Height * scale + SearchFieldTop * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + pad, fieldTop),
            new Vector2(area.Max.X - pad, fieldTop + SearchFieldHeight * scale));
        var palette = VelvetTheme.Palette;
        SearchField.Draw(fieldRect, "##velvetConnectionsSearch", Loc.T(L.Velvet.ConnectionsSearchHint),
            ref connectionsQuery, palette.FieldSurface, palette.MutedInk, palette.TitleInk, SearchClearFill,
            palette.BackdropBottom, ConnectionsSearchMaxLength, false);
        RebuildConnectionMatches();

        var list = new Rect(new Vector2(area.Min.X, fieldRect.Max.Y + SearchFieldTop * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(list))
        {
            if (connectionMatches.Count == 0)
            {
                DrawConnectionsEmpty(list);
                return;
            }

            Gap(2f);
            for (var index = 0; index < connectionMatches.Count; index++)
            {
                if (SocialUserRow.SkipHidden(ConnectionsRowStyle))
                {
                    continue;
                }

                DrawConnectionRow(connectionMatches[index]);
            }

            Gap(40f);
        }
    }

    private void DrawConnectionsEmpty(Rect list)
    {
        if (!store.ConnectionsLoaded)
        {
            DrawEmpty(list, Loc.T(L.Common.Loading), string.Empty);
        }
        else if (connectionsMatchedQuery.Length > 0)
        {
            DrawEmpty(list, Loc.T(L.Velvet.ConnectionsNoMatches), string.Empty);
        }
        else
        {
            DrawEmpty(list, Loc.T(L.Velvet.ConnectionsNone), Loc.T(L.Velvet.ConnectionsNoneHint));
        }
    }

    private void RebuildConnectionMatches()
    {
        var source = store.Connections;
        var query = connectionsQuery.Trim();
        if (ReferenceEquals(source, connectionsMatchedSource)
            && string.Equals(query, connectionsMatchedQuery, StringComparison.Ordinal))
        {
            return;
        }

        connectionsMatchedSource = source;
        connectionsMatchedQuery = query;
        connectionMatches.Clear();
        var handleQuery = query.Length > 1 && query[0] == '@' ? query[1..] : query;
        for (var index = 0; index < source.Length; index++)
        {
            var connection = source[index];
            if (connection.State != VelvetConnectionState.Connected)
            {
                continue;
            }

            if (query.Length == 0
                || connection.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || connection.Handle.Contains(handleQuery, StringComparison.OrdinalIgnoreCase))
            {
                connectionMatches.Add(connection);
            }
        }
    }

    private void DrawConnectionRow(VelvetConnectionDto connection)
    {
        var name = DisplayNameOf(connection.DisplayName, connection.Handle);
        var model = new VRowModel
        {
            Title = name,
            Subtitle = SocialIdentity.ProfileMeta(connection.Handle, string.Empty),
            Height = ConnectionsRowHeight,
            Leading = VRowLeading.Avatar,
            AvatarRadius = ConnectionsAvatarRadius,
            Name = name,
            World = string.Empty,
            AvatarUrl = connection.AvatarUrl,
            Presence = connection.Presence,
            UserId = connection.UserId,
            Chevron = true,
        };
        if (VRow.Cell(in model, theme, images, lodestone) == VRowHit.Body)
        {
            OpenProfile(connection.UserId);
        }
    }

    private int ConnectedCount()
    {
        var source = store.Connections;
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].State == VelvetConnectionState.Connected)
            {
                count++;
            }
        }

        return count;
    }
}
