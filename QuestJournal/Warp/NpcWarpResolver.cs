using StardewModdingAPI;
using StardewValley;

namespace QuestJournal.Warp;

// Finds an NPC by name and warps the player to wherever they're standing.
// If the NPC can't be found, it shows a "can't find them" HUD message instead.
public sealed record WarpNpc(string InternalName, string DisplayName);

internal static class NpcWarpResolver
{
    public static void Warp(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName)) return;

        NPC? npc = Game1.getCharacterFromName(internalName);
        if (npc == null)
        {
            try { npc = Utility.fuzzyCharacterSearch(internalName, true); } catch { }
        }

        if (npc?.currentLocation == null)
        {
            Notify(internalName!);
            return;
        }

        var loc = npc.currentLocation;
        var tile = npc.TilePoint;

        if (Game1.activeClickableMenu != null)
            Game1.activeClickableMenu = null;

        Game1.warpFarmer(loc.NameOrUniqueName, tile.X, tile.Y, 2);
    }

    private static void Notify(string name)
    {
        string msg = ModEntry.Instance?.Helper?.Translation
            .Get("journal.warp.notfound", new { npc = name })
            .Default($"Can't find {name} right now.").ToString()
            ?? $"Can't find {name} right now.";
        Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.error_type));
    }
}
