namespace CatchOfTheDay;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public int HudX { get; set; } = -51;
    public int HudY { get; set; } = 300;
    public bool HorizontalLayout { get; set; } = false;
    public float IconScale { get; set; } = 1.0f;
    public string CatchableNowColor { get; set; } = "#00000000";
    public string NightFishColor { get; set; } = "#00000000";
    public int MaxLocations { get; set; } = 4;
    public bool ShowBundleNeeds { get; set; } = true;
    public bool HideAlreadyCaught { get; set; } = false;
    public int MinSellPrice { get; set; } = 0;
    public bool ShowNightFish { get; set; } = false;

    // Weather types to track
    public bool TrackRain { get; set; } = true;
    public bool TrackStorm { get; set; } = true;
    public bool TrackSun { get; set; } = true;
    public bool TrackSnow { get; set; } = true;
    public bool TrackWind { get; set; } = true;
    public bool TrackGreenRain { get; set; } = true;
}
