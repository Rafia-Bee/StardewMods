using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoreQuestsFramework.Conditions;

// Plain case-insensitive name => evaluator map. Authors should pick keys that
// don't collide with built-ins or with other mods (e.g. prefix the key with
// the mod's short name: "MyMod_HasFriend"); collisions are rejected with a Warn
// and the first registration wins.
internal sealed class CustomConditionRegistry
{
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, Func<string, bool>> _evaluators
        = new(StringComparer.OrdinalIgnoreCase);

    public CustomConditionRegistry(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Register(string ownerUniqueId, string key, Func<string, bool> evaluator)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _monitor.Log($"CustomConditionRegistry rejected registration from '{ownerUniqueId}': key is empty.", LogLevel.Warn);
            return;
        }
        if (evaluator == null)
        {
            _monitor.Log($"CustomConditionRegistry rejected '{key}' from '{ownerUniqueId}': evaluator is null.", LogLevel.Warn);
            return;
        }
        if (BuiltInKeys.Contains(key))
        {
            _monitor.Log($"CustomConditionRegistry rejected '{key}' from '{ownerUniqueId}': name collides with a built-in condition key.", LogLevel.Warn);
            return;
        }
        if (_evaluators.ContainsKey(key))
        {
            _monitor.Log($"CustomConditionRegistry rejected duplicate condition key '{key}' (registration from '{ownerUniqueId}').", LogLevel.Warn);
            return;
        }
        _evaluators[key] = evaluator;
    }

    public bool TryEvaluate(string key, string value, out bool result)
    {
        if (_evaluators.TryGetValue(key, out var evaluator))
        {
            try
            {
                result = evaluator(value);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Custom condition '{key}' threw on value '{value}': {ex.Message}", LogLevel.Warn);
                result = false;
            }
            return true;
        }
        result = false;
        return false;
    }

    private static readonly HashSet<string> BuiltInKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "season", "date", "dayrange", "daysofweek", "year",
        "weather", "weatherforecast",
        "mindaysplayed", "maxdaysplayed",
        "isplayermarried", "ismultiplayer", "iscommunitycentercompleted",
        "skilllevel", "mindeepestminelevel",
        "npcexists", "npcmet",
        "friendshiplevel", "friendshipstatus",
        "mailreceived", "eventseen",
        "buildingexists",
        "knowncookingrecipe", "knowncraftingrecipe",
        "statatleast", "shippedatleast",
        "hasitemeverobtained",
        "hasmod",
        "followinganimalcount",
        "random",
        "gsq"
    };
}
