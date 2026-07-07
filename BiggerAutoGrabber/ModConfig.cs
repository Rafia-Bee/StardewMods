using System.Collections.Generic;

namespace BiggerAutoGrabber;

internal class ModConfig
{
    /// <summary>Size used for any storage type without its own override, including modded chests we haven't seen before.</summary>
    public int DefaultCapacity { get; set; } = 36;

    /// <summary>Per-type size overrides, keyed by type key ("AutoGrabber", "Fridge", or a chest item id like "(BC)130").</summary>
    public Dictionary<string, int> CapacityByType { get; set; } = new();

    /// <summary>Every storage type we've discovered, so the config menu can show a row for each even when it's left at the default.</summary>
    public List<string> KnownTypes { get; set; } = new();

    // Legacy fields from the 3-slider version. Migrated into the fields above on
    // load, then cleared so they stop showing up in config.json.
    public int? Capacity { get; set; }
    public int? ChestCapacity { get; set; }
    public int? FridgeCapacity { get; set; }
}
