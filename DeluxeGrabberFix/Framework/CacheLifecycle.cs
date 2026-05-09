using DeluxeGrabberFix.Grabbers;

namespace DeluxeGrabberFix.Framework;

// Audit §4.5: single source-of-truth for static-cache invalidation.
//
// Static state in DGF historically grew piecemeal. Each cache picked its own reset
// mechanism (some per-tick, some per-session, some per-grab, some never), and a new
// contributor had no convention to follow. This class centralizes the resets that
// have to happen at lifecycle boundaries and documents the others.
//
// Convention for adding a new static cache:
//   1. Decide the cache's reset boundary:
//        - per-tick           -> compare Game1.ticks in the read path, no entry here
//        - per-grab-cycle     -> managed by GrabSession or the existing flag-guard helpers,
//                                no entry here
//        - per-session        -> add a Reset/Clear method on the owning class and call it
//                                from ResetSessionCaches below
//        - per-title          -> if it differs from per-session, add to ResetTitleCaches
//                                (currently the two are identical -- both are "the world
//                                is gone, drop GameLocation references")
//        - process-lifetime   -> never reset (singleton, delegate-once-at-launch)
//   2. If per-session or per-title, add a doc line under the "Currently tracked caches"
//      list below so the convention stays discoverable.
//
// Currently tracked caches (kept here so a static-state inventory is one Glob away):
//   - per-session reset:
//       TownGarbageCanGrabber.CachedCanIds      -> ClearCache
//       Helpers._cachedBeeHouseLocation         -> ClearBeeHouseCache
//   - per-tick auto-invalidation (no entry needed):
//       AutomateSkipTiles._cachedSkipTiles      -> tick comparison in GetSkipTiles
//       ModEntry._cachedAllLocations            -> tick comparison in GetAllLocations
//   - per-grab-cycle (managed by surrounding control flow):
//       ModEntry._isGrabbing / SpecializedGrabberPatches.IsGrabbingActive
//                                               -> bridged setter (GrabSession owns it)
//       HarvestInterceptor._intercepting        -> Begin/EndIntercept guards
//       ModEntry.IsGlobalGrabActive / IsForageGrabEnabled / Cached*Grabbers
//                                               -> GrabSession owns
//   - process-lifetime (set once, never reset):
//       ModEntry._instance                      -> singleton accessor
//       PerfectionPatch.GetConfig               -> assigned at Entry, lives until shutdown
//       SpecializedGrabberPatches.SpecializedGrabberCount
//                                               -> intentionally NOT reset on save load
//                                                  (Recount runs in OnSaveLoaded after this);
//                                                  reset to 0 only on title return where the
//                                                  next save load will recount from scratch
internal static class CacheLifecycle
{
    // Called from ModEntry.OnSaveLoaded after the active config has been swapped but
    // before per-save migrations / world recount. The prior session's GameLocation
    // references should be released here so the per-save world is the only one alive.
    public static void ResetSessionCaches()
    {
        TownGarbageCanGrabber.ClearCache();
        Helpers.ClearBeeHouseCache();
    }

    // Called from ModEntry.OnReturnedToTitle. Currently identical to ResetSessionCaches;
    // kept as a distinct entry point so a future cache with a different boundary has a
    // clear place to land without re-architecting the call sites.
    public static void ResetTitleCaches()
    {
        ResetSessionCaches();
    }
}
