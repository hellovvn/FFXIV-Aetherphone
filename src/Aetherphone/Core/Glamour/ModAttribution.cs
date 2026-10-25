using Aetherphone.Core.Aethernet.Contracts;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Glamour;

internal static class ModAttribution
{
    private const string MetaFile = "meta.json";

    public static string ModFolder(string root, string actualPath)
    {
        if (root.Length == 0 || !Path.IsPathRooted(actualPath))
        {
            return string.Empty;
        }

        var trimmedRoot = root.AsSpan().TrimEnd('\\').TrimEnd('/');
        var actual = actualPath.AsSpan();
        if (actual.Length <= trimmedRoot.Length + 1 || !actual.StartsWith(trimmedRoot, StringComparison.OrdinalIgnoreCase) ||
            actual[trimmedRoot.Length] is not ('\\' or '/'))
        {
            return string.Empty;
        }

        var rest = actual[(trimmedRoot.Length + 1)..];
        var end = rest.IndexOfAny('\\', '/');
        return end <= 0 ? string.Empty : rest[..end].ToString();
    }

    public static void Collect(Dictionary<string, HashSet<string>> paths, string root,
        Dictionary<int, HashSet<string>> foldersBySlot)
    {
        foreach (var (actual, gamePaths) in paths)
        {
            var folder = ModFolder(root, actual);
            if (folder.Length == 0)
            {
                continue;
            }

            var placed = false;
            foreach (var gamePath in gamePaths)
            {
                var slot = GearSlots.FromGamePath(gamePath);
                if (slot == GearSlots.None)
                {
                    continue;
                }

                Add(foldersBySlot, slot, folder);
                placed = true;
            }

            if (!placed)
            {
                Add(foldersBySlot, GearSlots.None, folder);
            }
        }
    }

    public static GlamModDto Describe(string root, string folder, string listedName)
    {
        var name = listedName.Length > 0 ? listedName : folder;
        var metaPath = Path.Combine(root, folder, MetaFile);
        if (!File.Exists(metaPath))
        {
            return new GlamModDto(name);
        }

        try
        {
            var meta = JObject.Parse(File.ReadAllText(metaPath));
            return new GlamModDto(
                Text(meta, "Name", name),
                Text(meta, "Author", string.Empty),
                Text(meta, "Version", string.Empty),
                Text(meta, "Website", string.Empty));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Could not read the Penumbra meta for {folder}: {exception.Message}");
            return new GlamModDto(name);
        }
    }

    public static GlamOptionDto[]? Options(Dictionary<string, List<string>>? settings)
    {
        if (settings is null || settings.Count == 0)
        {
            return null;
        }

        var options = new GlamOptionDto[settings.Count];
        var index = 0;
        foreach (var (group, picked) in settings)
        {
            options[index++] = new GlamOptionDto(group, picked.ToArray());
        }

        return options;
    }

    public static Dictionary<string, IReadOnlyList<string>> Settings(GlamOptionDto[]? options)
    {
        var settings = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (options is null)
        {
            return settings;
        }

        for (var index = 0; index < options.Length; index++)
        {
            var option = options[index];
            if (option.Group.Length > 0 && option.Picked is { } picked)
            {
                settings[option.Group] = picked;
            }
        }

        return settings;
    }

    public static string MatchFolder(Dictionary<string, string> installed, Dictionary<string, string>? links,
        GlamModDto mod)
    {
        var folder = MatchByName(installed, mod);
        return folder.Length > 0 || links is null ? folder : MatchByLink(installed, links, mod);
    }

    public static string MatchByName(Dictionary<string, string> installed, GlamModDto mod)
    {
        var name = mod.Name.AsSpan().Trim();
        if (name.IsEmpty)
        {
            return string.Empty;
        }

        foreach (var (folder, listed) in installed)
        {
            if (SameName(listed, name) || SameName(folder, name))
            {
                return folder;
            }
        }

        return string.Empty;
    }

    public static bool HasLink(GlamModDto mod) => !LinkOf(mod.Link).IsEmpty;

    public static string MatchByLink(Dictionary<string, string> installed, Dictionary<string, string> links,
        GlamModDto mod)
    {
        var link = LinkOf(mod.Link);
        var name = mod.Name.AsSpan().Trim();
        if (link.IsEmpty || name.IsEmpty)
        {
            return string.Empty;
        }

        foreach (var (folder, listed) in installed)
        {
            if (links.TryGetValue(folder, out var website) &&
                LinkOf(website).Equals(link, StringComparison.OrdinalIgnoreCase) &&
                (Prefixed(listed.AsSpan().Trim(), name) || Prefixed(folder.AsSpan().Trim(), name)))
            {
                return folder;
            }
        }

        return string.Empty;
    }

    private static ReadOnlySpan<char> LinkOf(string link) => link.AsSpan().Trim().TrimEnd('/');

    private static bool SameName(string candidate, ReadOnlySpan<char> name) =>
        candidate.AsSpan().Trim().Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool Prefixed(ReadOnlySpan<char> installed, ReadOnlySpan<char> posted)
    {
        if (installed.IsEmpty)
        {
            return false;
        }

        var installedLonger = installed.Length >= posted.Length;
        var longer = installedLonger ? installed : posted;
        var shorter = installedLonger ? posted : installed;
        return longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase) && IsDecoration(longer[shorter.Length..]);
    }

    private static bool IsDecoration(ReadOnlySpan<char> rest)
    {
        var trimmed = rest.TrimStart();
        if (trimmed.IsEmpty)
        {
            return true;
        }

        var first = trimmed[0];
        if (first is '-' or '(' or '[' or '{' or '|' or ':' or '_' or '/' or '~')
        {
            return true;
        }

        var spaced = trimmed.Length < rest.Length;
        if (char.IsDigit(first))
        {
            return spaced;
        }

        return spaced && first is 'v' or 'V' && trimmed.Length > 1 && char.IsDigit(trimmed[1]);
    }

    private static void Add(Dictionary<int, HashSet<string>> foldersBySlot, int slot, string folder)
    {
        if (!foldersBySlot.TryGetValue(slot, out var folders))
        {
            folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foldersBySlot[slot] = folders;
        }

        folders.Add(folder);
    }

    private static string Text(JObject meta, string key, string fallback) =>
        meta.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) && token.Type == JTokenType.String &&
        token.ToString() is { Length: > 0 } value
            ? value
            : fallback;
}
