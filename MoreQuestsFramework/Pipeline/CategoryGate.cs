using System.Collections.Generic;

namespace MoreQuestsFramework.Pipeline;

// The per-category on/off rule, pulled out of QuestPipeline so it can be unit tested without
// building a full QuestContext. A category with no entry, or an entry set true, is on. Only an
// explicit false turns it off. An empty or null category reads as on (postings always carry one).
internal static class CategoryGate
{
    public static bool IsEnabled(IReadOnlyDictionary<string, bool> categoryEnabled, string? category)
        => string.IsNullOrEmpty(category)
           || !categoryEnabled.TryGetValue(category, out bool enabled)
           || enabled;
}
