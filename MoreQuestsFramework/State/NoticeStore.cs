using System;
using System.Collections.Generic;
using StardewValley;

namespace MoreQuestsFramework.State;

// Save-safe persistence for bulletin notices, kept apart from the quest anti-repetition store
// so the riskiest notice state lives in one place. Wires onto FrameworkState at SaveLoaded
// and writes through to its collections (which StateStore persists). Keys are the namespaced
// {owner}/{Name} of a notice def.
internal sealed class NoticeStore
{
    private FrameworkState _state = new();
    private HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public void WireState(FrameworkState state)
    {
        _state = state;
        _seen = new HashSet<string>(state.SeenNotices, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsSeen(string id) => _seen.Contains(id);

    // Records a one-shot notice as shown so it never draws again on this save.
    public void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;
        if (_seen.Add(id))
            _state.SeenNotices.Add(id);
    }

    public bool OnCooldown(string id, int cooldownDays)
    {
        if (cooldownDays <= 0)
            return false;
        if (!_state.NoticeLastPostedDay.TryGetValue(id, out int lastDay))
            return false;
        return Game1.Date.TotalDays - lastDay < cooldownDays;
    }

    // Starts a notice's cooldown clock. Harmless when the notice has no cooldown (the stamp
    // is just never read), so the draw can call it unconditionally on every posted notice.
    public void RecordPosted(string id)
    {
        if (!string.IsNullOrEmpty(id))
            _state.NoticeLastPostedDay[id] = Game1.Date.TotalDays;
    }
}
