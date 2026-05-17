using System;
using System.Collections.Generic;
using MoreQuestsFramework.Cache;

namespace MoreQuestsFramework.Consequences;

// Data/NPCGiftTastes row format (slash-separated): 0 universal-love, 1 loved ids,
// 2 universal-like, 3 liked ids, 4 universal-dislike, 5 disliked ids, 6 universal-hate,
// 7 hated ids, 8 universal-neutral, 9 neutral ids.
internal sealed class GiftTastesScanner
{
    private readonly GameDataCache _cache;
    private Dictionary<string, NpcTasteIndex>? _index;
    private string? _signature;

    public GiftTastesScanner(GameDataCache cache)
    {
        _cache = cache;
    }

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
        // GameDataCache returns the same dict across the day; rebuild only when swapped.
        string sig = raw.Count.ToString() + ":" + raw.GetHashCode().ToString();
        if (_index != null && _signature == sig)
            return;

        var built = new Dictionary<string, NpcTasteIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var (npc, line) in raw)
        {
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
