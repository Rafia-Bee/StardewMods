using System.Collections.Generic;

namespace DeluxeGrabberFix.Framework;

internal class SaveData
{
    // Bumped whenever a one-shot per-save migration is introduced. Saves at the
    // current version skip the heavy scans in OnSaveLoaded; older saves run the
    // migrations once and then get bumped to current.
    // v2: rerun MigrateSpecializedGrabbers once to heal grabbers corrupted by the
    // pre-fix melee-hit bug (issue #125), which left them with a custom ItemId.
    public const int CurrentSchemaVersion = 2;

    public HashSet<string> AutoSkippedLocations { get; set; } = new();
    public HashSet<string> ManuallyManagedLocations { get; set; } = new();
    public HashSet<string> BlacklistedLocations { get; set; } = new();
    public int SchemaVersion { get; set; }
}
