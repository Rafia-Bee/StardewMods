using System;
using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Rewards;

/// Encodes/decodes a `RewardSpec` to/from a single text line so it can ride
/// inside a `NetStringList` on a custom Quest subclass and survive a save
/// round-trip without a polymorphic JSON serializer.
///
/// Format: `Kind|key1=val1|key2=val2`. Order of keys is fixed per kind so the
/// decoder can be a flat switch instead of a key/value parser.
///
/// Consequence specs ride on the same list under a `Consequence|<base64-json>` line
/// (lists + multi-line text would blow the flat key=val format apart). One spec per
/// quest — extra `Consequence|` lines are ignored by `DecodeConsequence`.
public static class RewardCodec
{
    private const string ConsequencePrefix = "Consequence|";

    /// Encode a consequence spec into the same `NetStringList` slot as rewards. JSON
    /// keeps the lists + arbitrary string fields intact; base64 keeps `|` characters
    /// inside lines from colliding with the codec's own field separator.
    public static string EncodeConsequence(ConsequenceSpec spec)
    {
        string json = JsonConvert.SerializeObject(spec);
        string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        return ConsequencePrefix + b64;
    }

    /// Returns the first consequence line decoded from `lines`, or null if there isn't
    /// one. The encoded spec rides alongside reward entries in the same NetStringList.
    public static ConsequenceSpec? DecodeConsequence(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith(ConsequencePrefix, StringComparison.Ordinal))
                continue;
            try
            {
                string b64 = line.Substring(ConsequencePrefix.Length);
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                return JsonConvert.DeserializeObject<ConsequenceSpec>(json);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public static bool IsConsequenceLine(string line)
        => !string.IsNullOrEmpty(line) && line.StartsWith(ConsequencePrefix, StringComparison.Ordinal);


    public static string Encode(RewardSpec spec) => spec switch
    {
        MoneyReward m => $"Money|Amount={m.Amount}",
        FriendshipReward f => $"Friendship|Npc={f.Npc}|Points={f.Points}",
        ObjectReward o => $"Object|ItemId={o.ItemId}|Count={o.Count}",
        RecipeReward r => $"Recipe|Name={r.RecipeName}|Kind={r.Kind}",
        MailReward ml => $"Mail|Letter={ml.LetterKey}|When={ml.When}",
        ShopDiscountReward s => $"ShopDiscount|ShopId={s.ShopId}|Percent={s.PercentOff}|Days={s.DurationDays}|Items={JoinAppliesTo(s.AppliesTo)}|Stock={s.GuaranteedStock}",
        _ => throw new ArgumentException($"Unknown reward spec: {spec.GetType()}")
    };

    private static string JoinAppliesTo(System.Collections.Generic.List<string>? items)
    {
        if (items == null || items.Count == 0)
            return string.Empty;
        // Sanitise: id list never contains `|` (the codec separator) or `,` (our sub-delimiter)
        // for vanilla / modded item ids, so a plain `,` join round-trips cleanly.
        return string.Join(",", items);
    }

    private static System.Collections.Generic.List<string>? SplitAppliesTo(string s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        var list = new System.Collections.Generic.List<string>();
        foreach (var part in s.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0) list.Add(trimmed);
        }
        return list.Count == 0 ? null : list;
    }

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

            case "ShopDiscount":
                if (fields.TryGetValue("ShopId", out var shopId)
                    && fields.TryGetValue("Percent", out var pct) && int.TryParse(pct, out int percent)
                    && fields.TryGetValue("Days", out var dys) && int.TryParse(dys, out int days))
                {
                    fields.TryGetValue("Items", out var itemsRaw);
                    int stock = 0;
                    if (fields.TryGetValue("Stock", out var stk))
                        int.TryParse(stk, out stock);
                    return new ShopDiscountReward(shopId, percent, days, SplitAppliesTo(itemsRaw ?? string.Empty), stock);
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
