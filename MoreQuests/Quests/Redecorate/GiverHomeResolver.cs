using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.Locations;

namespace MoreQuests.Quests;

// Picks NPCs who can post a redecoration quest: a met, board-eligible villager who has a
// real home the player can actually walk into and decorate.
//
// We read the home from the character's Data/Characters Home entry (the same call the game
// uses), so modded NPCs go through the exact same path as vanilla ones. But "has a Home
// entry" isn't enough on its own: expansion mods like SVE park homeless NPCs (Claire,
// Martin, etc, the ones who ride in on the bus and vanish at night) in a tiny hidden
// "warp room" that's listed as their home but has no door the player can use. So on top of
// the indoor / not-farmhouse checks we require the home to actually connect to the outdoor
// world through warps. A warp room is a dead end and gets rejected; a real house reaches
// the outside through its door (even a sub-room like Sebastian's, which exits via the
// science house). Married NPCs are skipped since they live in the farmhouse now.
internal static class GiverHomeResolver
{
    public static bool TryResolveHome(string npcName, out GameLocation? home)
    {
        home = null;
        var npc = Game1.getCharacterFromName(npcName);
        if (npc == null)
            return false;

        // Married NPCs (and roommates) moved into the farmhouse; their old home isn't theirs anymore.
        if (npc.isMarried())
            return false;

        CharacterData? data = npc.GetData();
        if (data == null)
            return false;

        // Returns false when the NPC has no Home entry at all.
        if (!NPC.ReadNpcHomeData(data, npc.currentLocation, out string locationName, out Point _, out int _))
            return false;

        var resolved = Game1.getLocationFromName(locationName);
        if (resolved == null || resolved.IsOutdoors)
            return false;
        if (resolved is FarmHouse)
            return false;
        if (!ConnectsToOutdoors(resolved))
            return false;

        home = resolved;
        return true;
    }

    // Walks the warp graph out from the home; true if it can reach any outdoor location,
    // which means the player can walk there. Hidden parking rooms have no exit warps and
    // fail this. Bounded so a pathological map can't spin.
    private static bool ConnectsToOutdoors(GameLocation home)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<GameLocation>();
        queue.Enqueue(home);
        visited.Add(home.NameOrUniqueName);

        int guard = 0;
        while (queue.Count > 0 && guard++ < 200)
        {
            var loc = queue.Dequeue();
            if (loc.IsOutdoors)
                return true;

            var warps = loc.warps;
            if (warps == null)
                continue;
            foreach (var warp in warps)
            {
                if (warp == null || string.IsNullOrEmpty(warp.TargetName))
                    continue;
                var target = Game1.getLocationFromName(warp.TargetName);
                if (target != null && visited.Add(target.NameOrUniqueName))
                    queue.Enqueue(target);
            }
        }
        return false;
    }

    // Met adult human villagers (the same gift-receiver filter the other MoreQuests
    // social/gift quests use) who also have a real home the player can walk into.
    public static List<string> EligibleGivers()
    {
        var result = new List<string>();
        foreach (var name in Generators.MetAdultHumanGiftReceivers())
        {
            if (TryResolveHome(name, out _))
                result.Add(name);
        }
        return result;
    }
}
