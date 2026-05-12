namespace MoreQuestsFramework;

/// Static bridge consumer mods use to supply a runtime "how many animals are following
/// the local player" count to the framework. AdventureQuest's `Visit` step consults
/// this when the step's `Items[]` carries a `$follower-count:N` gate. Wired by the
/// content mod at SaveLoaded after it fetches the LivestockFollowsYou API; null when
/// LFY isn't installed (in which case `CurrentCount` returns 0 and any follower-gated
/// Visit step never auto-completes).
public static class FollowerApiBridge
{
    public static System.Func<int>? AnimalFollowerCount { get; set; }

    public static int CurrentCount() => AnimalFollowerCount?.Invoke() ?? 0;
}
