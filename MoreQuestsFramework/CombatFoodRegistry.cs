using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.GameData.Objects;

namespace MoreQuestsFramework;

// Pool of qualified item ids offered as combat-food rewards. SaveLoaded auto-scans
// Data/Objects for edibles with non-zero Attack/Defense buffs; consumers can also
// call Register() to add items the scan misses.
internal sealed class CombatFoodRegistry
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

    // Re-runs on every SaveLoaded so swapped content packs pick up the right pool.
    // Manual entries merged back in afterward; an explicit override never gets
    // downgraded by a weaker scan result on the same id.
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

        foreach (var (id, mag) in manualOverrides)
            Register(id, mag);

        _monitor.Log($"Combat-food scan: registered {registered} food(s) from Data/Objects.", LogLevel.Trace);
    }
}
