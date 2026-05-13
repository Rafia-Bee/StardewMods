namespace MoreQuests;

/// Duck-typed mirror of LFY's ILivestockFollowsYouApi. Only the members we consume,
/// so we don't need an assembly reference back to LFY. Must be public so SMAPI's
/// ModRegistry.GetApi<T>() can bind to it.
public interface ILfyApi
{
    int FollowingAnimalCount { get; }
    string GrazingBellQualifiedItemId { get; }
}
