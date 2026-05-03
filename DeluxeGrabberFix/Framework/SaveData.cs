using System.Collections.Generic;

namespace DeluxeGrabberFix.Framework;

internal class SaveData
{
    // Bumped whenever a one-shot per-save migration is introduced. Saves at the
    // current version skip the heavy scans in OnSaveLoaded; older saves run the
    // migrations once and then get bumped to current.
    public const int CurrentSchemaVersion = 1;

    public HashSet<string> AutoSkippedLocations { get; set; } = new();
    public HashSet<string> ManuallyManagedLocations { get; set; } = new();
    public HashSet<string> BlacklistedLocations { get; set; } = new();
    public int SchemaVersion { get; set; }
}
