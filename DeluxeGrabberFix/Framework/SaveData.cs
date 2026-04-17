using System.Collections.Generic;

namespace DeluxeGrabberFix.Framework;

internal class SaveData
{
    public HashSet<string> AutoSkippedLocations { get; set; } = new();
    public HashSet<string> ManuallyManagedLocations { get; set; } = new();
    public HashSet<string> BlacklistedLocations { get; set; } = new();
}
