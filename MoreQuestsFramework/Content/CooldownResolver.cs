using System;

namespace MoreQuestsFramework.Content;

// Resolves a quest's cooldown in days from its CooldownTier name. Pulled out of
// JsonQuestDefinition.CooldownDays so it can be tested on its own. A tier that resolves via the
// lookup wins. A non-empty tier that doesn't resolve (a typo, or one the asset never defined) sets
// unknownTier so the caller can warn, then falls back to the quest's own CooldownDays. No tier, or
// no lookup, also falls back.
internal static class CooldownResolver
{
    public static int Resolve(string? tier, Func<string, int?>? tierLookup, int fallbackDays, out bool unknownTier)
    {
        unknownTier = false;
        if (!string.IsNullOrEmpty(tier) && tierLookup != null)
        {
            int? resolved = tierLookup(tier);
            if (resolved.HasValue)
                return resolved.Value;
            unknownTier = true;
        }
        return fallbackDays;
    }
}
