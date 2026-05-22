using System;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Listens for player-dropped items hitting a location's debris list. When an OBJECT
// debris with DroppedByPlayerID == local player matches an active DropItems step
// (location + item filter, progress not maxed), credits one to the step and yanks
// the Debris back out of the location so the item visibly disappears (eaten by the
// wild critters). Extra drops past the step's count, or non-matching items, stay
// on the ground as normal.
//
// We can't Harmony-patch Farmer.dropItem because vanilla inventory drops call
// Game1.createItemDebris(...) directly (the F-key path goes through the inventory
// menu's drop button, not Farmer.dropItem), then set DroppedByPlayerID on the
// returned Debris. World.DebrisListChanged fires after that field is populated.
internal static class DropItemsPatches
{
    public static void Subscribe(IModHelper helper)
    {
        helper.Events.World.DebrisListChanged += OnDebrisListChanged;
    }

    private static void OnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
    {
        if (e.Added == null)
            return;
        var player = Game1.player;
        if (player == null)
            return;
        var location = e.Location;
        if (location == null)
            return;
        long playerId = player.UniqueMultiplayerID;
        var log = player.questLog;
        if (log == null || log.Count == 0)
            return;

        foreach (var debris in e.Added)
        {
            if (debris == null) continue;
            if (debris.debrisType.Value != Debris.DebrisType.OBJECT) continue;
            if (debris.DroppedByPlayerID.Value != playerId) continue;
            var item = debris.item;
            if (item == null) continue;

            // Vanilla inventory drops bundle the whole stack into one Debris (one chunk,
            // item.Stack carries the count). We tell the quest how many are available and
            // it tells us back how many it actually wanted. If the player dropped more
            // than the step needed, the leftover stays on the ground as a smaller Debris.
            int available = Math.Max(1, item.Stack);
            int consumed = 0;
            foreach (var q in log)
            {
                if (q is AdventureQuest aq)
                {
                    consumed = aq.TryConsumeDroppedItem(location.Name, item, available);
                    if (consumed > 0)
                        break;
                }
            }

            if (consumed <= 0)
                continue;

            if (consumed >= available)
                location.debris.Remove(debris);
            else
                item.Stack = available - consumed;

            Game1.playSound("dwop");
        }
    }
}
