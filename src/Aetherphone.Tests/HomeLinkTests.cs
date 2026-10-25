using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLinkTests
{
    private const string GamesId = "games";
    private const string HarvestRoute = "games.play.harvest";
    private const string ClusterRoute = "games.play.cluster";

    [Fact]
    public void AnAddedLink_IsOnHomeAndSurvivesAReload()
    {
        var apps = new List<IPhoneApp> { new FakeLinkApp(GamesId, HarvestRoute) };
        var configuration = EmptyConfiguration();

        Assert.Equal(HomeLinkAdd.Added, BuildLayout(apps, configuration).AddLink(GamesId, HarvestRoute));

        var reloaded = BuildLayout(apps, configuration);
        Assert.True(reloaded.HasLink(GamesId, HarvestRoute));
        var tile = LinkTile(reloaded, HarvestRoute);
        Assert.True(tile.IsLink);
        Assert.False(tile.IsFolder);
        Assert.False(tile.IsWidget);
        Assert.Null(tile.App);
    }

    [Fact]
    public void AddingTheSameLinkTwice_SaysItIsAlreadyThere()
    {
        var apps = new List<IPhoneApp> { new FakeLinkApp(GamesId, HarvestRoute) };
        var layout = BuildLayout(apps, EmptyConfiguration());

        layout.AddLink(GamesId, HarvestRoute);

        Assert.Equal(HomeLinkAdd.AlreadyThere, layout.AddLink(GamesId, HarvestRoute));
        Assert.Equal(1, CountLinks(layout));
    }

    [Fact]
    public void ALinkTheAppNoLongerKnows_IsDroppedOnLoad()
    {
        var app = new FakeLinkApp(GamesId, HarvestRoute);
        var apps = new List<IPhoneApp> { app };
        var configuration = EmptyConfiguration();
        BuildLayout(apps, configuration).AddLink(GamesId, HarvestRoute);

        app.Known.Clear();

        var reloaded = BuildLayout(apps, configuration);
        Assert.False(reloaded.HasLink(GamesId, HarvestRoute));
        Assert.Equal(0, CountLinks(reloaded));
    }

    [Fact]
    public void ALinkToAnAppThatCannotDescribeIt_IsRefused()
    {
        var apps = new List<IPhoneApp> { new FakeApp(GamesId), new FakeLinkApp("casino", "plinko") };
        var layout = BuildLayout(apps, EmptyConfiguration());

        Assert.Equal(HomeLinkAdd.Unavailable, layout.AddLink(GamesId, HarvestRoute));
        Assert.Equal(HomeLinkAdd.Unavailable, layout.AddLink("casino", "keno"));
        Assert.Equal(0, CountLinks(layout));
    }

    [Fact]
    public void ALinkDroppedIntoAFolder_StaysThereAfterAReload()
    {
        var apps = new List<IPhoneApp> { new FakeApp("notes"), new FakeLinkApp(GamesId, HarvestRoute) };
        var configuration = EmptyConfiguration();
        var layout = BuildLayout(apps, configuration);
        layout.Install("notes");
        layout.AddLink(GamesId, HarvestRoute);

        layout.MakeFolder(AppTile(layout, "notes"), LinkTile(layout, HarvestRoute));

        var reloaded = BuildLayout(apps, configuration);
        Assert.True(reloaded.HasLink(GamesId, HarvestRoute), "A link inside a folder still counts as on Home");
        Assert.Equal(HomeLinkAdd.AlreadyThere, reloaded.AddLink(GamesId, HarvestRoute));
    }

    [Fact]
    public void UninstallingTheApp_TakesItsLinksWithIt()
    {
        var apps = new List<IPhoneApp> { new FakeLinkApp(GamesId, HarvestRoute, ClusterRoute) };
        var configuration = EmptyConfiguration();
        var layout = BuildLayout(apps, configuration);
        layout.Install(GamesId);
        layout.AddLink(GamesId, HarvestRoute);
        layout.AddLink(GamesId, ClusterRoute);

        Assert.True(layout.Uninstall(GamesId));

        Assert.Equal(0, CountLinks(layout));
        Assert.Equal(0, CountLinks(BuildLayout(apps, configuration)));
    }

    [Fact]
    public void ALinkWhoseAppIsSwitchedOff_SurvivesASaveAndComesBack()
    {
        var app = new FakeLinkApp(GamesId, HarvestRoute);
        var apps = new List<IPhoneApp> { app };
        var configuration = EmptyConfiguration();
        BuildLayout(apps, configuration).AddLink(GamesId, HarvestRoute);

        app.IsAvailable = false;
        var switchedOff = BuildLayout(apps, configuration);
        Assert.Equal(0, CountLinks(switchedOff));
        switchedOff.Persist();

        app.IsAvailable = true;
        var restored = BuildLayout(apps, configuration);
        Assert.True(restored.HasLink(GamesId, HarvestRoute), "A switched off app's link must come back with the app");
    }

    [Fact]
    public void ALinkInAFolderWhoseAppIsSwitchedOff_ComesBackOnTheHomeScreen()
    {
        var app = new FakeLinkApp(GamesId, HarvestRoute);
        var apps = new List<IPhoneApp> { new FakeApp("notes"), app };
        var configuration = EmptyConfiguration();
        var layout = BuildLayout(apps, configuration);
        layout.Install("notes");
        layout.AddLink(GamesId, HarvestRoute);
        layout.MakeFolder(AppTile(layout, "notes"), LinkTile(layout, HarvestRoute));

        app.IsAvailable = false;
        BuildLayout(apps, configuration).Persist();

        app.IsAvailable = true;
        Assert.True(BuildLayout(apps, configuration).HasLink(GamesId, HarvestRoute));
    }

    [Fact]
    public void ALinkToAnUninstalledApp_IsDroppedWhenAnotherLookLoadsIt()
    {
        var apps = new List<IPhoneApp> { new FakeApp("notes"), new FakeLinkApp(GamesId, HarvestRoute) };
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Installed = new List<string> { "notes" },
                Pages = new List<HomePage>
                {
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new() { Kind = "app", AppId = "notes" },
                            new() { Kind = "link", LinkAppId = GamesId, LinkArgument = HarvestRoute },
                        },
                    },
                },
            },
        };

        var layout = BuildLayout(apps, configuration);

        Assert.False(layout.HasLink(GamesId, HarvestRoute));
        Assert.Equal(0, CountLinks(layout));
    }

    [Fact]
    public void AddingALinkForAnAppThatIsNotInstalled_InstallsItSoTheLinkSticks()
    {
        var apps = new List<IPhoneApp> { new FakeLinkApp(GamesId, HarvestRoute) };
        var configuration = EmptyConfiguration();
        var layout = BuildLayout(apps, configuration);

        Assert.Equal(HomeLinkAdd.Added, layout.AddLink(GamesId, HarvestRoute));

        Assert.True(layout.IsInstalled(GamesId));
        Assert.True(BuildLayout(apps, configuration).HasLink(GamesId, HarvestRoute));
    }

    [Fact]
    public void AHomeLookKeepsItsLinks()
    {
        var look = new FakeLookConfiguration
        {
            Home = new HomeLayout
            {
                Pages = new List<HomePage>
                {
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new() { Kind = "link", LinkAppId = GamesId, LinkArgument = HarvestRoute },
                        },
                    },
                },
            },
        };
        var captured = new HomeLook();

        HomeLookMirror.Capture(look, captured);
        look.Home = null;
        HomeLookMirror.Restore(look, captured);

        var item = look.Home!.Pages[0].Items[0];
        Assert.Equal("link", item.Kind);
        Assert.Equal(GamesId, item.LinkAppId);
        Assert.Equal(HarvestRoute, item.LinkArgument);
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), new FakeShortcutSource(), configuration);

    private static FakeHomeConfiguration EmptyConfiguration() =>
        new()
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { new() },
            },
        };

    private static int CountLinks(HomeLayoutService layout)
    {
        var count = 0;
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].IsLink)
                {
                    count++;
                }

                for (var memberIndex = 0; memberIndex < tiles[index].Members.Count; memberIndex++)
                {
                    if (tiles[index].Members[memberIndex].IsLink)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static HomeTile LinkTile(HomeLayoutService layout, string argument)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].Link is { } link && string.Equals(link.Argument, argument, StringComparison.Ordinal))
                {
                    return tiles[index];
                }
            }
        }

        throw new InvalidOperationException("The link tile is not on any page.");
    }

    private static HomeTile AppTile(HomeLayoutService layout, string appId)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].App?.Id == appId)
                {
                    return tiles[index];
                }
            }
        }

        throw new InvalidOperationException("The app tile is not on any page.");
    }

    private sealed class FakeLinkApp : IPhoneApp, IHomeLinkTarget
    {
        public FakeLinkApp(string id, params string[] known)
        {
            Id = id;
            Known = new List<string>(known);
        }

        public List<string> Known { get; }
        public bool IsAvailable { get; set; } = true;
        public string Id { get; }
        public string DisplayName => Id;
        public string Glyph => string.Empty;
        public int BadgeCount => 0;

        public bool HasLink(string argument) => Known.Contains(argument);

        public LocString LinkTitle(string argument) => L.Apps.Games;

        public void DrawLinkIcon(string argument, Vector2 min, Vector2 max, bool elevated)
        {
        }

        public void OpenLink(string argument)
        {
        }

        public void OnOpened() { }
        public void OnClosed() { }
        public void Draw(in PhoneContext context) { }
        public void Dispose() { }
    }

    private sealed class FakeLookConfiguration : ILookConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public bool ShowAppNames { get; set; }
        public IconAppearance IconAppearance { get; set; }
        public ThemeMode ThemeMode { get; set; }
        public string AccentName { get; set; } = string.Empty;
        public string AccentCustomHex { get; set; } = string.Empty;
        public string PhoneCaseName { get; set; } = string.Empty;
        public string LightWallpaperId { get; set; } = string.Empty;
        public string DarkWallpaperId { get; set; } = string.Empty;
        public List<HomeLook> Looks { get; } = new();
        public Dictionary<ulong, Guid> LookByCharacter { get; } = new();
        public Guid ActiveLookId { get; set; }
        public void Save() { }
    }
}
