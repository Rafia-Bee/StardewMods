using StardewModdingAPI;
using StardewValley;

namespace QuestJournal.Warp;

// One NPC the player can warp to from a quest's detail panel. InternalName is
// the character key we warp against; DisplayName is what the button shows.
public sealed record WarpNpc(string InternalName, string DisplayName);

// Warps the player onto an NPC's current tile. Used by the journal's Warp
// action (gated behind AllowWarpCheat). The NPC's live location and tile are
// read here, at warp time, so it works wherever the NPC has wandered off to.
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

        // Close the journal (and any child popup) so the player lands in the
        // world instead of staring at the menu after the warp.
        if (Game1.activeClickableMenu != null)
            Game1.activeClickableMenu = null;

        // Land right on the NPC's tile, facing down. Same tile dodges getting
        // wedged on an obstacle next to them.
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
