using System;
using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Rewards;

// Format: Kind|key1=val1|key2=val2. Consequence specs ride on the same list under
// a Consequence|<base64-json> line (lists + multi-line text would blow the flat
// key=val format apart).
public static class RewardCodec
{
    private const string ConsequencePrefix = "Consequence|";

    // Base64 so "|" chars inside the JSON don't collide with the codec separator.
    public static string EncodeConsequence(ConsequenceSpec spec)
    {
        string json = JsonConvert.SerializeObject(spec);
        string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        return ConsequencePrefix + b64;
    }

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
        AnimalPurchaseDiscountReward a => $"AnimalPurchaseDiscount|Percent={a.PercentOff}|Days={a.DurationDays}",
        FestivalBiasReward fb => $"FestivalBias|Festival={fb.Festival}|Magnitude={fb.Magnitude}",
        FairStarTokensReward fst => $"FairStarTokens|Amount={fst.Amount}",
        CustomReward c => $"Custom|Kind={c.Kind}|Payload={EncodeOpaque(c.Payload)}",
        _ => throw new ArgumentException($"Unknown reward spec: {spec.GetType()}")
    };

    // Payload may contain "|" / "=" / line breaks; base64 round-trips arbitrary text
    // through the flat codec without conflicting with the field separator.
    private static string EncodeOpaque(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static string DecodeOpaque(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string JoinAppliesTo(System.Collections.Generic.List<string>? items)
    {
        if (items == null || items.Count == 0)
            return string.Empty;
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

            case "FestivalBias":
                if (fields.TryGetValue("Festival", out var fest)
                    && Enum.TryParse<FestivalKind>(fest, ignoreCase: true, out var festKind))
                {
                    int mag = 1;
                    if (fields.TryGetValue("Magnitude", out var magRaw))
                        int.TryParse(magRaw, out mag);
                    return new FestivalBiasReward(festKind, mag);
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

            case "FairStarTokens":
                if (fields.TryGetValue("Amount", out var fstAmt) && int.TryParse(fstAmt, out int fstAmount))
                    return new FairStarTokensReward(fstAmount);
                return null;

            case "AnimalPurchaseDiscount":
                if (fields.TryGetValue("Percent", out var apdPct) && int.TryParse(apdPct, out int apdPercent)
                    && fields.TryGetValue("Days", out var apdDys) && int.TryParse(apdDys, out int apdDays))
                {
                    return new AnimalPurchaseDiscountReward(apdPercent, apdDays);
                }
                return null;

            case "Custom":
                if (fields.TryGetValue("Kind", out var customKind) && !string.IsNullOrEmpty(customKind))
                {
                    fields.TryGetValue("Payload", out var payloadEncoded);
                    return new CustomReward(customKind, DecodeOpaque(payloadEncoded));
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
