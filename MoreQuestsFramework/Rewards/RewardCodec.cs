using System;
using System.Collections.Generic;

namespace MoreQuestsFramework.Rewards;

/// Encodes/decodes a `RewardSpec` to/from a single text line so it can ride
/// inside a `NetStringList` on a custom Quest subclass and survive a save
/// round-trip without a polymorphic JSON serializer.
///
/// Format: `Kind|key1=val1|key2=val2`. Order of keys is fixed per kind so the
/// decoder can be a flat switch instead of a key/value parser.
public static class RewardCodec
{
    public static string Encode(RewardSpec spec) => spec switch
    {
        MoneyReward m => $"Money|Amount={m.Amount}",
        FriendshipReward f => $"Friendship|Npc={f.Npc}|Points={f.Points}",
        ObjectReward o => $"Object|ItemId={o.ItemId}|Count={o.Count}",
        RecipeReward r => $"Recipe|Name={r.RecipeName}|Kind={r.Kind}",
        MailReward ml => $"Mail|Letter={ml.LetterKey}|When={ml.When}",
        _ => throw new ArgumentException($"Unknown reward spec: {spec.GetType()}")
    };

    public static RewardSpec? Decode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var parts = line.Split('|');
        if (parts.Length == 0)
            return null;

        var fields = ParseFields(parts);
        switch (parts[0])
        {
            case "Money":
                if (fields.TryGetValue("Amount", out var amt) && int.TryParse(amt, out int amount))
                    return new MoneyReward(amount);
                return null;

            case "Friendship":
                if (fields.TryGetValue("Npc", out var npc) &&
                    fields.TryGetValue("Points", out var pts) &&
                    int.TryParse(pts, out int points))
                    return new FriendshipReward(npc, points);
                return null;

            case "Object":
                if (fields.TryGetValue("ItemId", out var itemId))
                {
                    int count = 1;
                    if (fields.TryGetValue("Count", out var c)) int.TryParse(c, out count);
                    return new ObjectReward(itemId, count);
                }
                return null;

            case "Recipe":
                if (fields.TryGetValue("Name", out var rname))
                {
                    var kind = RecipeKind.Cooking;
                    if (fields.TryGetValue("Kind", out var k))
                        Enum.TryParse(k, ignoreCase: true, out kind);
                    return new RecipeReward(rname, kind);
                }
                return null;

            case "Mail":
                if (fields.TryGetValue("Letter", out var letter))
                {
                    var when = MailWhen.Today;
                    if (fields.TryGetValue("When", out var w))
                        Enum.TryParse(w, ignoreCase: true, out when);
                    return new MailReward(letter, when);
                }
                return null;

            default:
                return null;
        }
    }

    private static Dictionary<string, string> ParseFields(string[] parts)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq <= 0)
                continue;
            dict[parts[i][..eq]] = parts[i][(eq + 1)..];
        }
        return dict;
    }
}
