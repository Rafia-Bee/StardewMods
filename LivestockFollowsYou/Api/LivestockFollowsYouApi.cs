using LivestockFollowsYou.Framework;
using StardewValley;

namespace LivestockFollowsYou.Api;

/// <summary>
/// Concrete <see cref="ILivestockFollowsYouApi"/> implementation. Must be a public,
/// top-level type because SMAPI's <c>GetApi&lt;T&gt;</c> rejects nested or internal
/// API instances.
/// </summary>
public class LivestockFollowsYouApi : ILivestockFollowsYouApi
{
    private readonly AnimalFollowManager followManager;

    internal LivestockFollowsYouApi(AnimalFollowManager followManager)
    {
        this.followManager = followManager;
    }

    public int FollowingAnimalCount => followManager.Followers.Count;

    public bool IsFollowing(FarmAnimal animal) =>
        animal != null && followManager.IsFollowing(animal);

    public string GrazingBellQualifiedItemId => GrazingBellItem.QualifiedItemId;
}
