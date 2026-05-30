using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.SpecialOrders;

namespace QuestJournal.Hud;

// Auto-pins quests the player picks up after a save loads, when
// DefaultPinNewQuests is on. SMAPI has no "quest added" event, so this watches
// the quest log (and special orders, which live in team.specialOrders, not the
// log) on a one-second tick and pins anything it hasn't seen before. On save
// load it primes the seen-set with the existing quests/orders so old ones
// aren't bulk-pinned, only genuinely new ones are.
internal sealed class NewQuestPinner
{
    private readonly IModHelper _helper;
    private readonly HashSet<string> _seen = new();
    private bool _primed;

    public NewQuestPinner(IModHelper helper)
    {
        _helper = helper;
    }

    public void Register()
    {
        _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        _helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        _helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondTick;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => Prime();

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _seen.Clear();
        _primed = false;
    }

    // Snapshot the current quests + special orders as already-seen so they don't
    // get auto-pinned.
    private void Prime()
    {
        _seen.Clear();
        var log = Game1.player?.questLog;
        if (log != null)
        {
            for (int i = 0; i < log.Count; i++)
            {
                var q = log[i];
                if (q == null) continue;
                string key = PinnedObjectivesStore.KeyFor(q);
                if (!string.IsNullOrEmpty(key)) _seen.Add(key);
            }
        }
        var orders = Game1.player?.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null) continue;
                string key = PinnedObjectivesStore.KeyFor(so);
                if (!string.IsNullOrEmpty(key)) _seen.Add(key);
            }
        }
        _primed = true;
    }

    private void OnOneSecondTick(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null) return;
        if (!_primed) { Prime(); return; }

        var log = Game1.player.questLog;
        var current = new HashSet<string>();
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value) continue;
            string key = PinnedObjectivesStore.KeyFor(q);
            if (string.IsNullOrEmpty(key)) continue;
            current.Add(key);
            if (_seen.Add(key) && ModEntry.Config.DefaultPinNewQuests)
                PinnedObjectivesStore.Pin(q);
        }
        // Special orders, accepted from the SO board into team.specialOrders.
        var orders = Game1.player.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null || so.questState.Value != SpecialOrderStatus.InProgress) continue;
                string key = PinnedObjectivesStore.KeyFor(so);
                if (string.IsNullOrEmpty(key)) continue;
                current.Add(key);
                if (_seen.Add(key) && ModEntry.Config.DefaultPinNewQuests)
                    PinnedObjectivesStore.Pin(so);
            }
        }
        // Forget quests/orders that left so re-accepting one pins it again.
        _seen.RemoveWhere(k => !current.Contains(k));
    }
}
