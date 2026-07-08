using System;
using System.Collections.Generic;
using MoreQuestsFramework.Dispatch;
using StardewValley;
using StardewValley.GameData.Characters;

namespace MoreQuestsFramework.Quests;

public sealed record TimedTargetCandidate(string Name, string LocationName, bool IsInvisible);

// Picks who a timed package goes to. Rolled at handoff (not at posting) so the
// filters can look at where everyone actually is right now.
public static class TimedDeliveryTargets
{
    // The pure filter, split out for tests. Drops the giver, anyone standing in the
    // giver's location (no trivial next-tile deliveries), anyone invisible (hospital
    // days and the like), and anyone in a location the player has never visited.
    // The visited gate is what keeps Desert and Ginger Island targets out until the
    // player has actually unlocked them.
    public static List<string> Filter(
        IEnumerable<TimedTargetCandidate> candidates,
        string giver,
        string giverLocation,
        Func<string, bool> hasVisited)
    {
        var result = new List<string>();
        if (candidates == null)
            return result;
        foreach (var c in candidates)
        {
            if (c == null || string.IsNullOrEmpty(c.Name))
                continue;
            if (string.Equals(c.Name, giver, StringComparison.OrdinalIgnoreCase))
                continue;
            if (c.IsInvisible)
                continue;
            if (string.IsNullOrEmpty(c.LocationName))
                continue;
            if (string.Equals(c.LocationName, giverLocation, StringComparison.OrdinalIgnoreCase))
                continue;
            if (hasVisited == null || !hasVisited(c.LocationName))
                continue;
            result.Add(c.Name);
        }
        return result;
    }

    // Met, board-eligible, adult human NPCs who can receive gifts, with the reachability
    // filters applied against their current spot in the world. Null when nobody fits.
    public static string? PickTarget(string giver, string giverLocationName)
    {
        var player = Game1.player;
        if (player == null)
            return null;

        var candidates = new List<TimedTargetCandidate>();
        foreach (var name in DispatchRegistry.MetHumanNpcs())
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null)
                continue;
            var data = npc.GetData();
            if (data == null || data.Language == NpcLanguage.Dwarvish)
                continue;
            if (!npc.CanReceiveGifts())
                continue;
            if (!player.friendshipData.TryGetValue(name, out var friendship) || friendship == null || friendship.Points <= 0)
                continue;
            candidates.Add(new TimedTargetCandidate(name, npc.currentLocation?.Name ?? string.Empty, npc.IsInvisible));
        }

        var pool = Filter(candidates, giver, giverLocationName, loc => player.locationsVisited.Contains(loc));
        if (pool.Count == 0)
            return null;
        return pool[Game1.random.Next(pool.Count)];
    }
}
