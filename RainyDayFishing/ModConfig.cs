namespace RainyDayFishing;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public int HudX { get; set; } = -51;
    public int HudY { get; set; } = 300;
    public bool HorizontalLayout { get; set; } = false;
    public int MaxLocations { get; set; } = 4;
}
