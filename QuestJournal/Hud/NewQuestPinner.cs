using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.SpecialOrders;

namespace QuestJournal.Hud;

// Watches for brand new quests and special orders and auto-pins them.
// Primes a list of already-seen quests on load, then checks once a second for
// anything new. Only pins automatically when that config option is on.
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
        _seen.RemoveWhere(k => !current.Contains(k));
    }
}
