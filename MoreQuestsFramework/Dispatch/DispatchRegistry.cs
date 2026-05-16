using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Dispatch;

public sealed class DispatchRegistry
{
    public readonly record struct Entry(string Npc, string? RequiredModUniqueId);

    private readonly IModRegistry _modRegistry;
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, List<Entry>> _byRole = new(StringComparer.OrdinalIgnoreCase);

    public DispatchRegistry(IModRegistry modRegistry, IMonitor monitor)
    {
        _modRegistry = modRegistry;
        _monitor = monitor;
    }

    public void Register(string role, string npcName, string? requiredModUniqueId = null)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(npcName))
        {
            _monitor.Log($"DispatchRegistry rejected entry: role='{role}', npc='{npcName}' (both required).", LogLevel.Warn);
            return;
        }
        if (!_byRole.TryGetValue(role, out var list))
        {
            list = new List<Entry>();
            _byRole[role] = list;
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Npc, npcName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(list[i].RequiredModUniqueId, requiredModUniqueId, StringComparison.OrdinalIgnoreCase))
                return;
        }
        list.Add(new Entry(npcName, requiredModUniqueId));
    }

    public string? Pick(string role)
    {
        var pool = ResolvePool(role);
        if (pool.Count == 0)
            return null;
        return pool[Game1.random.Next(pool.Count)];
    }

    public IReadOnlyList<string> ResolvePool(string role)
    {
        if (!_byRole.TryGetValue(role, out var entries))
            return Array.Empty<string>();

        var built = new List<string>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.RequiredModUniqueId != null && !_modRegistry.IsLoaded(e.RequiredModUniqueId))
                continue;
            if (Game1.getCharacterFromName(e.Npc) == null)
                continue;
            built.Add(e.Npc);
        }
        if (built.Count == 0)
            return Array.Empty<string>();

        var met = new List<string>(built.Count);
        for (int i = 0; i < built.Count; i++)
        {
            if (Game1.player.friendshipData.ContainsKey(built[i]))
                met.Add(built[i]);
        }
        // No fallback to `built`: posting to an unmet NPC reads as a bug (player has
        // no way to recognise the giver) and obscures the social-progression gate.
        return met;
    }

    public static List<string> MetHumanNpcs()
    {
        var results = new List<string>();
        foreach (var (name, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster || !npc.IsVillager)
                continue;
            results.Add(name);
        }
        return results;
    }
}
