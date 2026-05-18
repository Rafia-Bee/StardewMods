using StardewModdingAPI;

namespace MoreQuestsFramework.State;

// SMAPI scopes WriteSaveData to the host save, matching our host-only multiplayer stance.
internal sealed class StateStore
{
    private const string Key = "MoreQuestsFrameworkState";

    private readonly IDataHelper _data;
    private readonly IMonitor _monitor;
    private FrameworkState _state = new();

    public StateStore(IDataHelper data, IMonitor monitor)
    {
        _data = data;
        _monitor = monitor;
    }

    public FrameworkState State => _state;

    public void Load()
    {
        try
        {
            _state = _data.ReadSaveData<FrameworkState>(Key) ?? new FrameworkState();
        }
        catch (System.Exception ex)
        {
            _monitor.Log($"Failed to read framework save data; using defaults. Reason: {ex.Message}", LogLevel.Warn);
            _state = new FrameworkState();
        }
        ReconcileSchema();
    }

    // Compares the loaded save's Schema against the framework's current schema and
    // runs migrations (when any exist). A save with a higher schema than this build
    // knows about means someone reverted the framework after writing with a newer
    // version, which can silently drop fields, so we surface a warning.
    private void ReconcileSchema()
    {
        int loaded = _state.Schema;
        if (loaded == FrameworkState.CurrentSchema)
            return;
        if (loaded > FrameworkState.CurrentSchema)
        {
            _monitor.Log(
                $"Framework save data was written by a newer schema (v{loaded}); this build only understands v{FrameworkState.CurrentSchema}. "
                + "Some fields may not have loaded correctly. Updating the framework is recommended.",
                LogLevel.Warn);
            return;
        }
        ModEntry.LogDebug($"Migrating framework save data from schema v{loaded} to v{FrameworkState.CurrentSchema}.");
        _state.Schema = FrameworkState.CurrentSchema;
    }

    public void Save()
    {
        try
        {
            _data.WriteSaveData(Key, _state);
        }
        catch (System.Exception ex)
        {
            _monitor.Log($"Failed to write framework save data: {ex.Message}", LogLevel.Error);
        }
    }
}
