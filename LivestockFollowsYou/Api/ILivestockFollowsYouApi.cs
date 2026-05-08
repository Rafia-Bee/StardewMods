using StardewValley;

namespace LivestockFollowsYou.Api;

/// <summary>
/// Public API for the Livestock Follows You mod. Fetch with
/// <c>helper.ModRegistry.GetApi&lt;ILivestockFollowsYouApi&gt;("RafiaBee.LivestockFollowsYou")</c>.
/// All members reflect the local player's follow state.
/// </summary>
public interface ILivestockFollowsYouApi
{
    /// <summary>Number of farm animals currently following or being walked by the local player.</summary>
    int FollowingAnimalCount { get; }

    /// <summary>Whether the given farm animal is currently following or being walked by the local player.</summary>
    bool IsFollowing(FarmAnimal animal);

    /// <summary>Qualified item id (with <c>(O)</c> prefix) of the Grazing Bell shop item.</summary>
    string GrazingBellQualifiedItemId { get; }
}
