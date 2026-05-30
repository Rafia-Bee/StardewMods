using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Quests;

namespace QuestJournal.Hud;

// Per-save set of pinned quest ids, kept in Game1.player.modData under a stable
// key so it round-trips with the save (mirrors CompletedQuestStore). The HUD
// resolves these ids back to live quests every frame, so the store only holds
// the keys, not any quest text; the objective shown always reflects the quest's
// current progress.
internal static class PinnedObjectivesStore
{
    private const string ModDataKey = "RafiaBee.QuestJournal/PinnedQuests";

    // Cheap cache so the HUD's per-frame Load doesn't re-parse the same JSON 60
    // times a second. Re-parses only when the stored string actually changes.
    private static string _cachedJson = string.Empty;
    private static HashSet<string> _cached = new();

    // A quest's stable pin key. The data-quest id is preferred (numeric for
    // vanilla, dotted for MQF, and it survives a save reload). Falls back to the
    // title for the rare quest with no id so the pin still works that session.
    public static string KeyFor(Quest q)
    {
        if (q == null) return string.Empty;
        string? id = q.id?.Value;
        if (!string.IsNullOrEmpty(id)) return id!;
        string? title = q.questTitle;
        return string.IsNullOrEmpty(title) ? string.Empty : "title:" + title;
    }

    public static HashSet<string> Load()
    {
        if (Game1.player == null) return new HashSet<string>();
        string json = Game1.player.modData.TryGetValue(ModDataKey, out string? stored) && stored != null
            ? stored
            : string.Empty;
        if (json == _cachedJson) return _cached;

        var set = new HashSet<string>();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<string>>(json);
                if (list != null)
                    foreach (var s in list)
                        if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
            catch { }
        }
        _cachedJson = json;
        _cached = set;
        return set;
    }

    public static bool IsPinned(Quest q)
    {
        string key = KeyFor(q);
        return !string.IsNullOrEmpty(key) && Load().Contains(key);
    }

    // Flips the pin and persists. Returns the new pinned state.
    public static bool Toggle(Quest q)
    {
        string key = KeyFor(q);
        if (string.IsNullOrEmpty(key)) return false;
        var set = new HashSet<string>(Load());
        bool nowPinned;
        if (set.Remove(key)) nowPinned = false;
        else { set.Add(key); nowPinned = true; }
        Save(set);
        return nowPinned;
    }

    public static void Pin(Quest q)
    {
        string key = KeyFor(q);
        if (string.IsNullOrEmpty(key)) return;
        var set = new HashSet<string>(Load());
        if (set.Add(key)) Save(set);
    }

    public static void Unpin(Quest q)
    {
        string key = KeyFor(q);
        if (string.IsNullOrEmpty(key)) return;
        var set = new HashSet<string>(Load());
        if (set.Remove(key)) Save(set);
    }

    private static void Save(HashSet<string> set)
    {
        if (Game1.player == null) return;
        if (set.Count == 0)
            Game1.player.modData.Remove(ModDataKey);
        else
            Game1.player.modData[ModDataKey] = JsonConvert.SerializeObject(new List<string>(set));
    }
}
