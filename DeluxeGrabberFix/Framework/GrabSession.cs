using System;
using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix.Grabbers;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal enum GrabSessionKind
{
    /// Day-start sweep, dirty-locations sweep, day-ending forage sweep.
    /// Sets _isGrabbing + IsForageGrabEnabled + Specialized cache (when applicable).
    AutoSweep,

    /// Hourly poll. Sets _isGrabbing + IsForageGrabEnabled (off in Daily mode)
    /// + Specialized cache OR Classic-global fallback (when designated grabber exists).
    Hourly,

    /// Instant-mode machine-ready sweep. Sets _isGrabbing
    /// + Specialized cache OR Classic-global fallback. No forage flag.
    MachineSweep,

    /// Manual fire keybind (Specialized or Classic) and the deferred Classic auto-fire.
    /// Sets _isGrabbing + IsForageGrabEnabled + the appropriate global state for the
    /// active grabber mode and globalGrabber sub-mode. Caller must filter Off upstream.
    ManualGlobalFire,
}

/// Single entry/exit point for a grab cycle. Constructing the session sets every flag
/// that grab-aware code paths depend on (_isGrabbing, IsGlobalGrabActive,
/// IsForageGrabEnabled, CachedDesignatedGrabbers); Dispose unwinds them symmetrically.
/// This is the only place that should mutate those flags.
internal sealed class GrabSession : IDisposable
{
    private readonly ModEntry _mod;
    private bool _restoreForage;
    private bool _restoreGlobalCache;
    private bool _disposed;

    public GrabSession(ModEntry mod, GrabSessionKind kind)
    {
        _mod = mod;

        switch (kind)
        {
            case GrabSessionKind.AutoSweep:
                EnableForageGrab();
                TrySetupSpecializedCache();
                break;

            case GrabSessionKind.Hourly:
                if (mod.Config.grabFrequency != ModConfig.GrabFrequency.Daily)
                    EnableForageGrab();
                if (!TrySetupSpecializedCache())
                    TrySetupClassicGlobalCache();
                break;

            case GrabSessionKind.MachineSweep:
                if (!TrySetupSpecializedCache())
                    TrySetupClassicGlobalCache();
                break;

            case GrabSessionKind.ManualGlobalFire:
                EnableForageGrab();
                SetupManualFireGlobalState();
                break;
        }

        mod.IsGrabbing = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_restoreForage)
            _mod.IsForageGrabEnabled = false;

        if (_restoreGlobalCache)
        {
            _mod.IsGlobalGrabActive = false;
            _mod.CachedDesignatedGrabbers = null;
            _mod.CachedHoverGrabber = null;
        }

        _mod.IsGrabbing = false;
    }

    private void EnableForageGrab()
    {
        _mod.IsForageGrabEnabled = true;
        _restoreForage = true;
    }

    /// Specialized-mode global cache: every grabber on every location whose held item is a Chest.
    /// Returns true only when the cache was actually populated (Specialized + globalGrabber == All).
    /// Hover is keybind-only by design -- non-keybind sweeps must fall back to per-location range
    /// grabbing, otherwise placed grabbers would silently global-grab without the player ever
    /// hovering and firing (issue #74 bug 1).
    private bool TrySetupSpecializedCache()
    {
        if (_mod.Config.grabberMode != ModConfig.GrabberMode.Specialized)
            return false;
        if (_mod.Config.globalGrabber != ModConfig.GlobalGrabberMode.All)
            return false;

        _mod.IsGlobalGrabActive = true;
        _mod.CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
        foreach (var loc in ModEntry.GetAllLocations())
        {
            _mod.CachedDesignatedGrabbers.AddRange(
                loc.Objects.Pairs
                    .Where(pair => pair.Value != null
                        && GrabberTypeHelper.IsGrabber(pair.Value.QualifiedItemId)
                        && pair.Value.heldObject.Value is StardewValley.Objects.Chest));
        }
        _restoreGlobalCache = true;
        return true;
    }

    /// Classic-mode All fallback: cache built from objects carrying GlobalGrabberModDataKey.
    /// Returns true only when the cache was set up (Classic + All + a designated grabber exists).
    private bool TrySetupClassicGlobalCache()
    {
        if (_mod.Config.grabberMode != ModConfig.GrabberMode.Classic)
            return false;
        if (_mod.Config.globalGrabber != ModConfig.GlobalGrabberMode.All)
            return false;
        if (!_mod.HasDesignatedGrabber())
            return false;

        _mod.IsGlobalGrabActive = true;
        _mod.CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
        foreach (var loc in ModEntry.GetAllLocations())
        {
            _mod.CachedDesignatedGrabbers.AddRange(
                loc.Objects.Pairs
                    .Where(pair => pair.Value != null
                        && pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey)));
        }
        _restoreGlobalCache = true;
        return true;
    }

    /// Manual-fire setup: branch on grabberMode and globalGrabber sub-mode. Hover sub-mode
    /// captures the cursor-targeted grabber at session entry into CachedHoverGrabber, so the
    /// per-iteration path (MapGrabber's Hover branch) reads from a stable session snapshot
    /// rather than re-resolving Game1.lastCursorTile mid-cycle (audit §2.2). Off is
    /// unreachable here (caller filters it).
    private void SetupManualFireGlobalState()
    {
        if (_mod.Config.grabberMode == ModConfig.GrabberMode.Specialized)
        {
            if (_mod.Config.globalGrabber == ModConfig.GlobalGrabberMode.Hover)
            {
                _mod.IsGlobalGrabActive = true;
                CaptureHoverGrabber();
                _restoreGlobalCache = true;
            }
            else
            {
                TrySetupSpecializedCache();
            }
            return;
        }

        _mod.IsGlobalGrabActive = true;
        _restoreGlobalCache = true;
        if (_mod.Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
        {
            _mod.CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
            foreach (var loc in ModEntry.GetAllLocations())
            {
                _mod.CachedDesignatedGrabbers.AddRange(
                    loc.Objects.Pairs
                        .Where(pair => pair.Value != null
                            && pair.Value.modData.ContainsKey(ModEntry.GlobalGrabberModDataKey)));
            }
        }
        else if (_mod.Config.globalGrabber == ModConfig.GlobalGrabberMode.Hover)
        {
            CaptureHoverGrabber();
        }
    }

    private void CaptureHoverGrabber()
    {
        var loc = Game1.player?.currentLocation;
        if (loc == null)
            return;

        var tile = Game1.lastCursorTile;
        var hoverObj = loc.getObjectAtTile((int)tile.X, (int)tile.Y);
        if (hoverObj != null
            && GrabberTypeHelper.IsGrabber(hoverObj.QualifiedItemId)
            && hoverObj.heldObject.Value is StardewValley.Objects.Chest)
        {
            _mod.CachedHoverGrabber = new KeyValuePair<Vector2, Object>(tile, hoverObj);
        }
    }
}
