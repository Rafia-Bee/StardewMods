using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.GameData.Objects;

namespace MoreQuestsFramework;

/// Pool of qualified item ids the framework offers to consumer mods as combat-food
/// rewards. On each `SaveLoaded` the framework auto-scans `Data/Objects` and registers
/// every edible whose buffs grant non-zero Attack or Defense, with the magnitude
/// (max of Attack / Defense levels, floored) recorded alongside the id. Consumer mods
/// can also call `Register(itemId, magnitude)` to add items the scan misses (modded
/// foods that grant attack via a non-standard mechanism, custom rings, etc.).
public sealed class CombatFoodRegistry
{
    private readonly IMonitor _monitor;
    private readonly List<string> _items = new();
    private readonly Dictionary<string, int> _magnitudes = new(StringComparer.OrdinalIgnoreCase);

    public CombatFoodRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string itemId, int? magnitude = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            _monitor.Log("CombatFoodRegistry rejected empty itemId.", LogLevel.Warn);
            return;
        }
        bool exists = false;
        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        if (!exists)
            _items.Add(itemId);

        if (magnitude.HasValue && magnitude.Value > 0)
        {
            if (!_magnitudes.TryGetValue(itemId, out int existing) || magnitude.Value > existing)
                _magnitudes[itemId] = magnitude.Value;
        }
    }

    public IReadOnlyList<string> Pool => _items;

    public int? GetMagnitude(string qualifiedItemId)
        => !string.IsNullOrEmpty(qualifiedItemId) && _magnitudes.TryGetValue(qualifiedItemId, out int m) ? m : null;

    /// Walks `Data/Objects` and registers every edible with a non-zero Attack or Defense
    /// buff. Magnitude = floor(max(Attack, Defense)) across all of the item's buffs.
    /// Re-runs on every SaveLoaded so swapped content packs pick up the right pool.
    /// Manual entries added via `Register(id, magnitude)` survive the rescan (their
    /// magnitudes are merged in, not overwritten downward) so consumers can keep
    /// overrides registered at `RegistrationOpen`.
    internal void RunDataScan(IGameContentHelper content)
    {
        if (content == null)
            return;

        var manualOverrides = new Dictionary<string, int>(_magnitudes, StringComparer.OrdinalIgnoreCase);
        _items.Clear();
        _magnitudes.Clear();

        var data = content.Load<Dictionary<string, ObjectData>>("Data/Objects");
        int registered = 0;
        foreach (var (rawId, obj) in data)
        {
            if (obj == null || obj.Edibility <= 0 || obj.Buffs == null)
                continue;

            int magnitude = 0;
            foreach (var buff in obj.Buffs)
            {
                var attrs = buff?.CustomAttributes;
                if (attrs == null)
                    continue;
                int level = (int)Math.Floor(Math.Max(attrs.Attack, attrs.Defense));
                if (level > magnitude)
                    magnitude = level;
            }
            if (magnitude <= 0)
                continue;

            string qualified = "(O)" + rawId;
            Register(qualified, magnitude);
            registered++;
        }

        // Re-merge any manual overrides registered before the scan ran. Pick the higher
        // of (scan magnitude, override magnitude) so an explicit override never gets
        // downgraded by a weaker buff entry on the same id.
        foreach (var (id, mag) in manualOverrides)
            Register(id, mag);

        _monitor.Log($"Combat-food scan: registered {registered} food(s) from Data/Objects.", LogLevel.Trace);
    }
}
