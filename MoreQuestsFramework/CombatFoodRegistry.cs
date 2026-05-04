using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework;

/// Process-lifetime list of combat-buff food item ids consumer mods can offer as a
/// reward pool. The framework seeds nothing, so the pool starts empty; the content
/// mod registers its vanilla defaults at `RegistrationOpen` and other mods can
/// extend the same pool through the public API.
public sealed class CombatFoodRegistry
{
    private readonly IMonitor _monitor;
    private readonly List<string> _items = new();

    public CombatFoodRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            _monitor.Log("CombatFoodRegistry rejected empty itemId.", LogLevel.Warn);
            return;
        }
        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase))
                return;
        }
        _items.Add(itemId);
    }

    public IReadOnlyList<string> Pool => _items;
}
