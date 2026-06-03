using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests.Quests;

// Watches for furniture the player places inside a quest giver's home and feeds it to the
// matching RedecorateQuest. Seeds each quest's baseline on warp into the home, then polls
// once a second to credit new placements. Works on the local player's own quest log, so
// it's correct for the host and for a multiplayer farmhand alike (quests and money are
// per-player, furniture in the room is synced so anyone's placement is seen).
internal sealed class FurniturePlacementWatcher
{
    public void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer || e.NewLocation == null)
            return;

        string locName = e.NewLocation.Name ?? string.Empty;
        foreach (var rq in ActiveQuestsForLocation(locName))
            rq.SeedSnapshot(e.NewLocation);
    }

    public void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        var location = Game1.currentLocation;
        if (location == null)
            return;

        string locName = location.Name ?? string.Empty;
        foreach (var rq in ActiveQuestsForLocation(locName))
            rq.ObservePlacements(location);
    }

    private static System.Collections.Generic.IEnumerable<RedecorateQuest> ActiveQuestsForLocation(string locationName)
    {
        if (string.IsNullOrEmpty(locationName))
            yield break;

        var log = Game1.player?.questLog;
        if (log == null)
            yield break;

        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is RedecorateQuest rq
                && !rq.completed.Value
                && string.Equals(rq.homeLocation.Value, locationName, System.StringComparison.OrdinalIgnoreCase))
            {
                yield return rq;
            }
        }
    }
}
