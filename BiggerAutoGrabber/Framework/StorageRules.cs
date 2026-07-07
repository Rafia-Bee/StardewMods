using StardewValley.Objects;

namespace BiggerAutoGrabber.Framework;

internal enum StorageKind
{
    None,
    AutoGrabber,
    Chest,
    Fridge
}

/// <summary>
/// Pure decision logic for the mod: what kind of storage an object is, which
/// config key names its size, which capacity applies, and which chest actually
/// holds its items. Kept free of menu/Harmony code so it can be unit-tested.
/// </summary>
internal static class StorageRules
{
    public const int VanillaCapacity = 36;
    public const int AutoGrabberIndex = 165;

    /// <summary>Config key for auto-grabbers (they aren't a chest item themselves).</summary>
    public const string AutoGrabberKey = "AutoGrabber";

    /// <summary>Config key for the built-in kitchen fridge (it has no clean item id).</summary>
    public const string FridgeKey = "Fridge";

    /// <summary>Classifies a world object from the raw facts about it.</summary>
    public static StorageKind Classify(
        bool isBigCraftable,
        int parentSheetIndex,
        bool hasHeldChest,
        bool isChest,
        bool isFridge,
        bool isPlayerChest,
        Chest.SpecialChestTypes specialType)
    {
        if (isBigCraftable && parentSheetIndex == AutoGrabberIndex && hasHeldChest)
            return StorageKind.AutoGrabber;

        if (isChest)
        {
            if (isFridge)
                return StorageKind.Fridge;

            if (isPlayerChest && IsResizableChestType(specialType))
                return StorageKind.Chest;
        }

        return StorageKind.None;
    }

    /// <summary>Classifies a live game object.</summary>
    public static StorageKind Classify(StardewValley.Object obj)
    {
        if (obj == null)
            return StorageKind.None;

        var chest = obj as Chest;
        return Classify(
            obj.bigCraftable.Value,
            obj.ParentSheetIndex,
            obj.heldObject.Value is Chest,
            chest != null,
            chest?.fridge.Value ?? false,
            chest?.playerChest.Value ?? false,
            chest?.SpecialChestType ?? Chest.SpecialChestTypes.None);
    }

    /// <summary>
    /// The config key for a storage kind. Auto-grabbers share one key; every
    /// other resizable storage is keyed by its item id, so each chest type
    /// (including modded ones) gets its own entry. Returns null when the kind
    /// isn't resizable. Placed mini-fridges come through as a chest item id;
    /// the built-in kitchen fridge is keyed separately with <see cref="FridgeKey"/>.
    /// </summary>
    public static string TypeKeyFor(StorageKind kind, string qualifiedItemId) => kind switch
    {
        StorageKind.AutoGrabber => AutoGrabberKey,
        StorageKind.Chest or StorageKind.Fridge => qualifiedItemId,
        _ => null
    };

    /// <summary>The config key for a live game object, or null if not resizable.</summary>
    public static string GetTypeKey(StardewValley.Object obj)
        => TypeKeyFor(Classify(obj), obj?.QualifiedItemId);

    /// <summary>The configured capacity for a type key, falling back to the default.</summary>
    public static int ResolveCapacity(string key, ModConfig config)
        => key != null && config.CapacityByType.TryGetValue(key, out int v) ? v : config.DefaultCapacity;

    /// <summary>
    /// The capacity to stamp onto a chest, or null when it should stay vanilla.
    /// We only stamp storage that's actually bigger than vanilla so saves stay
    /// clean and normal-sized storage never routes through our resize path.
    /// </summary>
    public static int? ResolveStampCapacity(string key, ModConfig config)
    {
        if (key == null)
            return null;

        int cap = ResolveCapacity(key, config);
        return cap > VanillaCapacity ? cap : null;
    }

    /// <summary>The chest that holds the items for a given storage object.</summary>
    public static Chest GetBackingChest(StardewValley.Object obj, StorageKind kind) => kind switch
    {
        StorageKind.AutoGrabber => obj.heldObject.Value as Chest,
        StorageKind.Chest or StorageKind.Fridge => obj as Chest,
        _ => null
    };

    private static bool IsResizableChestType(Chest.SpecialChestTypes type)
        => type == Chest.SpecialChestTypes.None
        || type == Chest.SpecialChestTypes.BigChest
        || type == Chest.SpecialChestTypes.JunimoChest;
}
