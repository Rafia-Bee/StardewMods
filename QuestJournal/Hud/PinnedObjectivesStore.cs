using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;

namespace QuestJournal.Hud;

// Tracks which quests and special orders the player has pinned.
// Stores the pinned keys as JSON in the player's modData, with a small cache so
// we don't reparse every read. Has pin, unpin, toggle, and lookup helpers.
internal static class PinnedObjectivesStore
{
    private const string ModDataKey = "RafiaBee.QuestJournal/PinnedQuests";

    private static string _cachedJson = string.Empty;
    private static HashSet<string> _cached = new();

    public static string KeyFor(Quest q)
    {
        if (q == null) return string.Empty;
        string? id = q.id?.Value;
        if (!string.IsNullOrEmpty(id)) return id!;
        string? title = q.questTitle;
        return string.IsNullOrEmpty(title) ? string.Empty : "title:" + title;
    }

    public static string KeyFor(SpecialOrder so)
    {
        if (so == null) return string.Empty;
        string? key = so.questKey?.Value;
        return string.IsNullOrEmpty(key) ? string.Empty : "so:" + key;
    }

    public static string KeyFor(string ownerId, string key)
        => string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(key)
            ? string.Empty
            : "ext:" + ownerId + "/" + key;

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

    public static bool IsPinned(Quest q) => IsPinnedKey(KeyFor(q));
    public static bool IsPinned(SpecialOrder so) => IsPinnedKey(KeyFor(so));
    public static bool IsPinned(string ownerId, string key) => IsPinnedKey(KeyFor(ownerId, key));

    public static bool ToggleExternal(string ownerId, string key) => ToggleKey(KeyFor(ownerId, key));

    public static void SetExternalPinned(string ownerId, string key, bool pinned)
    {
        string k = KeyFor(ownerId, key);
        if (string.IsNullOrEmpty(k)) return;
        if (pinned) PinKey(k);
        else UnpinKey(k);
    }

    public static bool Toggle(Quest q) => ToggleKey(KeyFor(q));
    public static bool Toggle(SpecialOrder so) => ToggleKey(KeyFor(so));

    public static void Pin(Quest q) => PinKey(KeyFor(q));
    public static void Pin(SpecialOrder so) => PinKey(KeyFor(so));

    public static void Unpin(Quest q) => UnpinKey(KeyFor(q));
    public static void Unpin(SpecialOrder so) => UnpinKey(KeyFor(so));

    public static void UnpinByKey(string key) => UnpinKey(key);

    private static bool IsPinnedKey(string key)
        => !string.IsNullOrEmpty(key) && Load().Contains(key);

    private static bool ToggleKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var set = new HashSet<string>(Load());
        bool nowPinned;
        if (set.Remove(key)) nowPinned = false;
        else { set.Add(key); nowPinned = true; }
        Save(set);
        return nowPinned;
    }

    private static void PinKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var set = new HashSet<string>(Load());
        if (set.Add(key)) Save(set);
    }

    private static void UnpinKey(string key)
    {
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
