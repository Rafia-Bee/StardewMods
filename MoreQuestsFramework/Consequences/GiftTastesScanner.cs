using System;
using System.Collections.Generic;
using MoreQuestsFramework.Cache;

namespace MoreQuestsFramework.Consequences;

/// Pre-built per-NPC loved/liked/hated/disliked sets keyed by item id. Backed by
/// `GameDataCache.GiftTastes` so modded NPCs and modded items participate natively
/// (the data source is the live `Data/NPCGiftTastes` asset). Refreshed on the same
/// cadence as `GameDataCache` itself: SaveLoaded + DayStarted + content invalidation.
///
/// `Data/NPCGiftTastes` row format (fields separated by `/`):
///   0: universal love-line
///   1: loved item ids (space-separated bare ids)
///   2: universal like-line
///   3: liked item ids
///   4: universal dislike-line
///   5: disliked item ids
///   6: universal hate-line
///   7: hated item ids
///   8: universal neutral-line
///   9: neutral item ids
public sealed class GiftTastesScanner
{
    private readonly GameDataCache _cache;
    private Dictionary<string, NpcTasteIndex>? _index;
    private string? _signature;

    public GiftTastesScanner(GameDataCache cache)
    {
        _cache = cache;
    }

    /// NPC names whose loved-item set contains `bareItemId`. Bare = strip `(O)` prefix
    /// if present. Empty list when no NPC loves the item.
    public IReadOnlyList<string> NpcsWhoLove(string itemId) => Lookup(itemId, t => t.Loved);

    public IReadOnlyList<string> NpcsWhoLike(string itemId) => Lookup(itemId, t => t.Liked);

    public IReadOnlyList<string> NpcsWhoHate(string itemId) => Lookup(itemId, t => t.Hated);

    public IReadOnlyList<string> NpcsWhoDislike(string itemId) => Lookup(itemId, t => t.Disliked);

    private IReadOnlyList<string> Lookup(string itemId, Func<NpcTasteIndex, HashSet<string>> selector)
    {
        if (string.IsNullOrEmpty(itemId))
            return Array.Empty<string>();

        EnsureBuilt();
        string bare = StripPrefix(itemId);
        var matches = new List<string>();
        foreach (var (npc, taste) in _index!)
        {
            if (selector(taste).Contains(bare))
                matches.Add(npc);
        }
        return matches;
    }

    private void EnsureBuilt()
    {
        var raw = _cache.GiftTastes;
        // GameDataCache returns the same dictionary instance across the day; we cache the
        // built index until the underlying reference changes (DayStarted refresh swaps it).
        string sig = raw.Count.ToString() + ":" + raw.GetHashCode().ToString();
        if (_index != null && _signature == sig)
            return;

        var built = new Dictionary<string, NpcTasteIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var (npc, line) in raw)
        {
            // Vanilla also stores universal taste rows under keys like "Universal_Love";
            // skip them — we want per-NPC indexes only.
            if (npc.StartsWith("Universal_", StringComparison.OrdinalIgnoreCase))
                continue;
            built[npc] = ParseRow(line);
        }
        _index = built;
        _signature = sig;
    }

    private static NpcTasteIndex ParseRow(string row)
    {
        var taste = new NpcTasteIndex();
        if (string.IsNullOrEmpty(row))
            return taste;
        var fields = row.Split('/');
        FillFrom(fields, 1, taste.Loved);
        FillFrom(fields, 3, taste.Liked);
        FillFrom(fields, 5, taste.Disliked);
        FillFrom(fields, 7, taste.Hated);
        return taste;
    }

    private static void FillFrom(string[] fields, int idx, HashSet<string> sink)
    {
        if (idx >= fields.Length || string.IsNullOrWhiteSpace(fields[idx]))
            return;
        foreach (var token in fields[idx].Split(' '))
        {
            if (string.IsNullOrEmpty(token))
                continue;
            sink.Add(StripPrefix(token));
        }
    }

    private static string StripPrefix(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;
        if (id.Length > 3 && id.StartsWith("(O)", StringComparison.Ordinal))
            return id.Substring(3);
        if (id.Length > 0 && id[0] == '(')
        {
            int close = id.IndexOf(')');
            if (close > 0 && close < id.Length - 1)
                return id.Substring(close + 1);
        }
        return id;
    }

    private sealed class NpcTasteIndex
    {
        public HashSet<string> Loved { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Liked { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Disliked { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Hated { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
