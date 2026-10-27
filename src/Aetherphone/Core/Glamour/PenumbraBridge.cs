using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Aetherphone.Core.Glamour;

internal sealed class PenumbraBridge : IDisposable
{
    public const int PlayerIndex = 0;

    private const string InternalName = "Penumbra";
    private const int SupportedBreakingVersion = 5;
    private const int SettingsKey = 0x41455050;
    private const string SettingsSource = "Aetherphone";
    private const int TemporaryPriority = 100;
    private const int RedrawNow = 0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICallGateSubscriber<(int, int)> apiVersionGate;
    private readonly ICallGateSubscriber<bool> enabledGate;
    private readonly ICallGateSubscriber<string> modDirectoryGate;
    private readonly ICallGateSubscriber<Dictionary<string, string>> modListGate;
    private readonly ICallGateSubscriber<Dictionary<ushort, Dictionary<string, HashSet<string>>>> playerPathsGate;
    private readonly ICallGateSubscriber<int, (bool, bool, (Guid, string))> collectionGate;
    private readonly ICallGateSubscriber<Guid, string, string, bool, (int, (bool, int, Dictionary<string, List<string>>, bool)?)>
        settingsGate;
    private readonly ICallGateSubscriber<Guid, string, string, bool, bool, int,
        (int, (bool, int, Dictionary<string, List<string>>, bool, bool)?)> settingsWithTemporaryGate;
    private readonly ICallGateSubscriber<int, string, string,
        (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>), string, int, int> temporaryGate;
    private readonly ICallGateSubscriber<int, int, int> clearTemporaryGate;
    private readonly ICallGateSubscriber<int, int, object> redrawGate;
    private readonly ICallGateSubscriber<object> initializedGate;
    private readonly ICallGateSubscriber<object> disposedGate;
    private readonly ICallGateSubscriber<string, object> modAddedGate;
    private readonly ICallGateSubscriber<string, object> modDeletedGate;
    private readonly ICallGateSubscriber<string, string, object> modMovedGate;
    private volatile bool readySignaled;
    private volatile bool disposingSignaled;
    private volatile bool temporarySettingsUnreadable;
    private int modsSignaled;

    public PenumbraBridge(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        apiVersionGate = pluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion.V5");
        enabledGate = pluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState");
        modDirectoryGate = pluginInterface.GetIpcSubscriber<string>("Penumbra.GetModDirectory");
        modListGate = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        playerPathsGate = pluginInterface
            .GetIpcSubscriber<Dictionary<ushort, Dictionary<string, HashSet<string>>>>("Penumbra.GetPlayerResourcePaths.V5");
        collectionGate = pluginInterface
            .GetIpcSubscriber<int, (bool, bool, (Guid, string))>("Penumbra.GetCollectionForObject.V5");
        settingsGate = pluginInterface
            .GetIpcSubscriber<Guid, string, string, bool, (int, (bool, int, Dictionary<string, List<string>>, bool)?)>(
                "Penumbra.GetCurrentModSettings.V5");
        settingsWithTemporaryGate = pluginInterface.GetIpcSubscriber<Guid, string, string, bool, bool, int,
            (int, (bool, int, Dictionary<string, List<string>>, bool, bool)?)>("Penumbra.GetCurrentModSettingsWithTemp");
        temporaryGate = pluginInterface.GetIpcSubscriber<int, string, string,
            (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>), string, int, int>(
            "Penumbra.SetTemporaryModSettingsPlayer.V5");
        clearTemporaryGate = pluginInterface.GetIpcSubscriber<int, int, int>("Penumbra.RemoveAllTemporaryModSettingsPlayer.V5");
        redrawGate = pluginInterface.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject.V5");
        initializedGate = pluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
        disposedGate = pluginInterface.GetIpcSubscriber<object>("Penumbra.Disposed");
        modAddedGate = pluginInterface.GetIpcSubscriber<string, object>("Penumbra.ModAdded");
        modDeletedGate = pluginInterface.GetIpcSubscriber<string, object>("Penumbra.ModDeleted");
        modMovedGate = pluginInterface.GetIpcSubscriber<string, string, object>("Penumbra.ModMoved");
        initializedGate.Subscribe(OnReady);
        disposedGate.Subscribe(OnDisposing);
        modAddedGate.Subscribe(OnModChanged);
        modDeletedGate.Subscribe(OnModChanged);
        modMovedGate.Subscribe(OnModMoved);
    }

    public bool ConsumeModsChanged() => Interlocked.Exchange(ref modsSignaled, 0) != 0;

    public bool ConsumeReady()
    {
        if (!readySignaled)
        {
            return false;
        }

        readySignaled = false;
        return true;
    }

    public bool ConsumeDisposing()
    {
        if (!disposingSignaled)
        {
            return false;
        }

        disposingSignaled = false;
        return true;
    }

    public bool Probe()
    {
        if (!GlamourPlugins.IsLoaded(pluginInterface, InternalName))
        {
            return false;
        }

        try
        {
            var (breaking, _) = apiVersionGate.InvokeFunc();
            temporarySettingsUnreadable = false;
            return breaking == SupportedBreakingVersion;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra API version check failed: {exception.Message}");
            return false;
        }
    }

    public bool Enabled() => Call(enabledGate.InvokeFunc, false, "GetEnabledState");

    public string ModDirectory() => Call(modDirectoryGate.InvokeFunc, string.Empty, "GetModDirectory");

    public Dictionary<string, string> ModList() => Call(modListGate.InvokeFunc, new Dictionary<string, string>(), "GetModList");

    public Dictionary<string, HashSet<string>>? PlayerPaths()
    {
        var trees = Call(playerPathsGate.InvokeFunc, null, "GetPlayerResourcePaths");
        return trees is not null && trees.TryGetValue(PlayerIndex, out var paths) ? paths : null;
    }

    public bool TryPlayerCollection(out Guid collection)
    {
        var (valid, _, (id, _)) = Call(() => collectionGate.InvokeFunc(PlayerIndex), default, "GetCollectionForObject");
        collection = id;
        return valid && id != Guid.Empty;
    }

    public Dictionary<string, List<string>>? CurrentOptions(Guid collection, string directory)
    {
        if (!temporarySettingsUnreadable)
        {
            try
            {
                var (_, current) = settingsWithTemporaryGate.InvokeFunc(collection, directory, string.Empty, false,
                    false, SettingsKey);
                return current?.Item3;
            }
            catch (Exception exception) when (exception is IpcNotReadyError or IpcTypeMismatchError)
            {
                temporarySettingsUnreadable = true;
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Penumbra GetCurrentModSettingsWithTemp failed: {exception.Message}");
            }
        }

        var (_, settings) = Call(() => settingsGate.InvokeFunc(collection, directory, string.Empty, false), default,
            "GetCurrentModSettings");
        return settings?.Item3;
    }

    public bool SetTemporary(string directory, IReadOnlyDictionary<string, IReadOnlyList<string>> options)
    {
        var result = Call(() => temporaryGate.InvokeFunc(PlayerIndex, directory, string.Empty,
            (false, true, TemporaryPriority, options), SettingsSource, SettingsKey), -1, "SetTemporaryModSettingsPlayer");
        return result == 0;
    }

    public void ClearTemporary() =>
        Call(() => clearTemporaryGate.InvokeFunc(PlayerIndex, SettingsKey), -1, "RemoveAllTemporaryModSettingsPlayer");

    public void Redraw()
    {
        try
        {
            redrawGate.InvokeAction(PlayerIndex, RedrawNow);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra RedrawObject failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        initializedGate.Unsubscribe(OnReady);
        disposedGate.Unsubscribe(OnDisposing);
        modAddedGate.Unsubscribe(OnModChanged);
        modDeletedGate.Unsubscribe(OnModChanged);
        modMovedGate.Unsubscribe(OnModMoved);
    }

    private static T Call<T>(Func<T> call, T fallback, string name)
    {
        try
        {
            return call();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra {name} failed: {exception.Message}");
            return fallback;
        }
    }

    private void OnReady() => readySignaled = true;

    private void OnDisposing() => disposingSignaled = true;

    private void OnModChanged(string directory) => Interlocked.Exchange(ref modsSignaled, 1);

    private void OnModMoved(string from, string to) => Interlocked.Exchange(ref modsSignaled, 1);
}
