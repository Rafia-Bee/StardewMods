namespace MoreQuestsFramework;

// Bridge for content mods to supply a live follower-animal count (e.g. via the
// LivestockFollowsYou API). Null when LFY isn't installed; CurrentCount then
// returns 0 and follower-gated Visit steps never auto-complete.
public static class FollowerApiBridge
{
    public static System.Func<int>? AnimalFollowerCount { get; set; }

    public static int CurrentCount() => AnimalFollowerCount?.Invoke() ?? 0;
}
