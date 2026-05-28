using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Lookup handle returned by IMoreQuestsModApi.GetActiveCustomSteps. Lets a consumer
// mod push progress into an AdventureQuest's Custom step from an external event
// (Harmony patch, SMAPI event, IPC) without going through the framework's per-second
// polling path. Handles are snapshots: re-query each event tick rather than caching
// a handle across days. AddProgress / MarkDone are no-ops once IsActive returns false.
public interface ICustomStepHandle
{
    Quest Quest { get; }

    // The Custom step's authored Name (matches the JSON Steps[].Name field).
    string StepName { get; }

    // Fully-qualified handler id ({ownerUniqueId}/{name}), useful when the caller
    // looked up by bare name and wants to know which scope matched.
    string HandlerName { get; }

    IReadOnlyList<string> Targets { get; }
    IReadOnlyList<string> Items { get; }
    int Count { get; }
    int Progress { get; }

    // False once the step is Done, the parent quest is completed, the step's Requires
    // are no longer met, or the underlying state was reshuffled out from under the
    // handle.
    bool IsActive { get; }

    // Returns the number of credits actually applied. 0 means the step wasn't active
    // or was already full. Reaching Count completes the step and, if it was the last
    // step, calls questComplete().
    int AddProgress(int delta);

    // Like AddProgress, but also dedupes by a caller-supplied key (e.g. a tile coord
    // or unique entity id). Returns 0 when the key has already been credited on this
    // step. Keys persist in the step's CreditedKeys list and survive save/reload, so
    // the same event source (e.g. a Harmony patch firing on the same tile twice)
    // can't double-count across a reload.
    int AddProgressOnceForKey(string key, int delta = 1);

    // Force the step to Done regardless of remaining count. Returns false when the
    // step isn't currently active.
    bool MarkDone();
}
