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
