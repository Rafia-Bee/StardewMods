using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// <summary>
/// Tracks player building events and triggers animal-related quests via mailbox letters.
/// </summary>
internal sealed class AnimalQuestTracker
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    private const string SaveKey = "MoreQuests_AnimalTriggers";

    private HashSet<string> _triggeredEvents = new();

    public AnimalQuestTracker(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
        Load();
    }

    public void CheckForTriggers()
    {
        if (!ModEntry.Config.AnimalQuestsEnabled || !Context.IsWorldReady)
            return;

        var farm = Game1.getFarm();
        if (farm == null)
            return;

        foreach (var building in farm.buildings)
        {
            var type = building.buildingType.Value;

            // Coop built for the first time
            if (type == "Coop" && !_triggeredEvents.Contains("coop_built"))
            {
                TriggerEvent("coop_built", "mail.marnie.coopBuilt");
            }

            // Barn built for the first time
            if (type == "Barn" && !_triggeredEvents.Contains("barn_built"))
            {
                TriggerEvent("barn_built", "mail.marnie.barnBuilt");
            }

            // Big Coop upgrade
            if (type == "Big Coop" && !_triggeredEvents.Contains("bigcoop_built"))
            {
                TriggerEvent("bigcoop_built", "mail.robin.bigCoopUpgrade");
            }

            // Deluxe Barn upgrade
            if (type == "Deluxe Barn" && !_triggeredEvents.Contains("deluxebarn_built"))
            {
                TriggerEvent("deluxebarn_built", "mail.marnie.deluxeBarnUpgrade");
            }
        }

        // First egg collected
        if (!_triggeredEvents.Contains("first_egg"))
        {
            if (Game1.player.basicShipped.ContainsKey("176") || // Egg
                Game1.player.basicShipped.ContainsKey("174") || // Large Egg
                HasItemInInventory("(O)176") || HasItemInInventory("(O)174"))
            {
                // TODO: better detection - check if player has ever picked up an egg
            }
        }
    }

    private void TriggerEvent(string eventId, string mailKey)
    {
        _triggeredEvents.Add(eventId);
        Game1.player.mailForTomorrow.Add($"MoreQuests_{eventId}");
        Save();
        _monitor.Log($"Animal quest triggered: {eventId}", LogLevel.Trace);
    }

    private static bool HasItemInInventory(string qualifiedId)
    {
        foreach (var item in Game1.player.Items)
        {
            if (item?.QualifiedItemId == qualifiedId)
                return true;
        }
        return false;
    }

    private void Load()
    {
        _triggeredEvents = _helper.Data.ReadSaveData<HashSet<string>>(SaveKey) ?? new();
    }

    private void Save()
    {
        _helper.Data.WriteSaveData(SaveKey, _triggeredEvents);
    }
}
