#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace LoanableTractor.Framework;

/// <summary>Shows a speech bubble above Morris or Pierre when the player rides a loaned tractor nearby.</summary>
internal sealed class NpcReactionManager
{
    private const float ReactionTileDistance = 5f;
    private const int ReactionChancePercent = 40;
    private const int BubbleDuration = 3500;
    private const int MorrisVariations = 8;
    private const int PierreVariations = 8;

    private readonly IModHelper _helper;
    private int _lastReactionDay = -1;

    public NpcReactionManager(IModHelper helper)
    {
        _helper = helper;
    }

    /// <summary>Check once per second for NPC proximity and maybe show a reaction bubble.</summary>
    public void Update()
    {
        if (Game1.Date.TotalDays == _lastReactionDay)
            return;

        bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();

        NPC? target = FindTargetNpc(ccComplete);
        if (target == null)
            return;

        float distance = Vector2.Distance(Game1.player.Tile, target.Tile);
        if (distance > ReactionTileDistance)
            return;

        _lastReactionDay = Game1.Date.TotalDays;

        if (Game1.random.Next(100) >= ReactionChancePercent)
            return;

        string key = ccComplete ? "bubble.pierre" : "bubble.morris";
        int count = ccComplete ? PierreVariations : MorrisVariations;
        int index = Game1.random.Next(1, count + 1);
        string text = _helper.Translation.Get($"{key}.{index}");

        target.showTextAboveHead(text, duration: BubbleDuration);
    }

    /// <summary>Find the target NPC in the current location. Handles SVE's "MorrisTod" variant.</summary>
    private static NPC? FindTargetNpc(bool ccComplete)
    {
        var location = Game1.currentLocation;
        if (location == null)
            return null;

        if (ccComplete)
            return location.getCharacterFromName("Pierre");

        // Try vanilla Morris first, then SVE's "MorrisTod"
        return location.getCharacterFromName("Morris")
            ?? location.getCharacterFromName("MorrisTod");
    }

    public void Reset()
    {
        _lastReactionDay = -1;
    }
}
