using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;

namespace Aetherphone.Apps.Prism;

internal static class PostedMods
{
    public static string Key(GlamModDto mod) => string.Concat(mod.Name, "|", mod.Author);

    public static GlamGearDto[] Unmodded(GlamGearDto[] gear)
    {
        var kept = new List<GlamGearDto>(gear.Length);
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.ItemId != 0)
            {
                kept.Add(piece.Mods is null ? piece : piece with { Mods = null });
            }
        }

        return kept.ToArray();
    }

    public static WornMod[] GearMods(GlamGearDto[] capture)
    {
        var listed = new List<WornMod>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < capture.Length; index++)
        {
            var piece = capture[index];
            if (piece.Slot == GearSlots.None || piece.Mods is not { } mods)
            {
                continue;
            }

            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                if (seen.Add(Key(mods[modIndex])))
                {
                    listed.Add(new WornMod(mods[modIndex], piece.Slot));
                }
            }
        }

        return listed.ToArray();
    }

    public static GlamGearDto[]? Credited(GlamGearDto[] capture, HashSet<string> picked)
    {
        var listed = GearMods(capture);
        var credited = new List<GlamModDto>(listed.Length);
        for (var index = 0; index < listed.Length; index++)
        {
            if (picked.Contains(Key(listed[index].Mod)))
            {
                credited.Add(listed[index].Mod);
            }
        }

        return credited.Count == 0 ? null : new[] { new GlamGearDto(GearSlots.None, Mods: credited.ToArray()) };
    }
}
