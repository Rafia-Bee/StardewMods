using RainyDayFishing.Interfaces;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace RainyDayFishing;

public class ModEntry : Mod
{
    private IToDewApi _todewApi;
    private uint _dataSourceHandle;
    private RainyFishDataSource _dataSource;
    private bool _registered;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnPlayerWarped;
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        _todewApi = Helper.ModRegistry.GetApi<IToDewApi>("jltaylor-us.ToDew");
        if (_todewApi == null)
            Monitor.Log("To-Dew mod not found! Install To-Dew for overlay support.", LogLevel.Error);
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        if (_todewApi == null)
            return;

        _dataSource = new RainyFishDataSource(this);
        _dataSourceHandle = _todewApi.AddOverlayDataSource(_dataSource);
        _registered = true;
        Monitor.Log("Registered rainy day fish data source with To-Dew overlay.", LogLevel.Info);
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        if (_registered)
            _todewApi.RefreshOverlay();
    }

    private void OnTimeChanged(object sender, TimeChangedEventArgs e)
    {
        if (_registered)
            _todewApi.RefreshOverlay();
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (_registered && e.IsLocalPlayer)
            _todewApi.RefreshOverlay();
    }

    private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        if (_registered && _todewApi != null)
        {
            _todewApi.RemoveOverlayDataSource(_dataSourceHandle);
            _registered = false;
            _dataSource = null;
        }
    }
}
