using Aetherphone.Apps.Prism;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PostedModsTests
{
    private static readonly GlamModDto Coat = new("Starlit Coat", "Bea");
    private static readonly GlamModDto Boots = new("Moon Boots", "Dee");
    private static readonly GlamModDto Body = new("Silhouette Body", "Cee");
    private static readonly GlamModDto Hair = new("Long Braid", "Rin");

    private static GlamGearDto[] Outfit() => new[]
    {
        new GlamGearDto(3, 45123, 12, 0, Mods: new[] { Coat }),
        new GlamGearDto(6, 45124, Mods: new[] { Boots }),
        new GlamGearDto(GearSlots.None, Mods: new[] { Body, Hair }),
    };

    private static HashSet<string> Picked(params GlamModDto[] mods)
    {
        var picked = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < mods.Length; index++)
        {
            picked.Add(PostedMods.Key(mods[index]));
        }

        return picked;
    }

    [Fact]
    public void OnlyGearModsAreListedForAnEntry()
    {
        var listed = PostedMods.GearMods(Outfit());

        Assert.Equal(2, listed.Length);
        Assert.Equal(new WornMod(Coat, 3), listed[0]);
        Assert.Equal(new WornMod(Boots, 6), listed[1]);
    }

    [Fact]
    public void NothingTickedCreditsNothing()
    {
        Assert.Null(PostedMods.Credited(Outfit(), Picked()));
    }

    [Fact]
    public void AnUntickedModNeverLeavesTheClient()
    {
        var credited = PostedMods.Credited(Outfit(), Picked(Coat));

        Assert.NotNull(credited);
        Assert.Single(credited!);
        Assert.Equal(new[] { Coat }, credited[0].Mods);
    }

    [Fact]
    public void ACharacterModIsNeverSentEvenWhenTicked()
    {
        var credited = PostedMods.Credited(Outfit(), Picked(Body, Hair, Boots));

        Assert.NotNull(credited);
        Assert.Equal(new[] { Boots }, credited![0].Mods);
    }

    [Fact]
    public void AModCoveringTwoSlotsIsListedAndSentOnce()
    {
        var gear = new[]
        {
            new GlamGearDto(3, 45123, Mods: new[] { Coat }),
            new GlamGearDto(4, 45125, Mods: new[] { Coat, Hair }),
        };

        var listed = PostedMods.GearMods(gear);
        var credited = PostedMods.Credited(gear, Picked(Coat, Hair));

        Assert.Equal(2, listed.Length);
        Assert.Equal(3, listed[0].Slot);
        Assert.Equal(new[] { Coat, Hair }, credited![0].Mods);
    }
}
