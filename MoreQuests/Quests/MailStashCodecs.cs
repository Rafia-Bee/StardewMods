using System.Collections.Generic;
using System.Globalization;
using StardewValley.Quests;

namespace MoreQuests.Quests;

// Length-prefixed string lists. Round-trip is exact for our custom Quest subclasses
// so a mail-delivered quest survives a save+reload before the letter is opened.
// Framework re-applies title/description/daysLeft/rewards via ApplyPostingFields,
// so we only encode this subclass's own NetFields.
internal static class PurchaseFromShopQuestStashCodec
{
    public const string Kind = "RafiaBee.MoreQuests.PurchaseFromShopQuest";

    public static IList<string> Encode(Quest q)
    {
        var p = (PurchaseFromShopQuest)q;
        var list = new List<string>
        {
            p.itemId.Value ?? string.Empty,
            p.shopOwnerNpc.Value ?? string.Empty,
            p.targetMessage.Value ?? string.Empty,
            p.serializedRewards.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int i = 0; i < p.serializedRewards.Count; i++)
            list.Add(p.serializedRewards[i] ?? string.Empty);
        return list;
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 4) return null;
        int i = 0;
        var p = new PurchaseFromShopQuest();
        p.itemId.Value = payload[i++];
        p.shopOwnerNpc.Value = payload[i++];
        p.targetMessage.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rewardCount))
            return null;
        for (int j = 0; j < rewardCount; j++)
        {
            if (i >= payload.Count) return null;
            p.serializedRewards.Add(payload[i++]);
        }
        return p;
    }
}

internal static class CollectAndReportQuestStashCodec
{
    public const string Kind = "RafiaBee.MoreQuests.CollectAndReportQuest";

    public static IList<string> Encode(Quest q)
    {
        var c = (CollectAndReportQuest)q;
        var list = new List<string>
        {
            c.talkToNpc.Value ?? string.Empty,
            c.requiredCount.Value.ToString(CultureInfo.InvariantCulture),
            c.reportMessage.Value ?? string.Empty,
            c.itemIds.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int i = 0; i < c.itemIds.Count; i++)
            list.Add(c.itemIds[i] ?? string.Empty);
        list.Add(c.serializedRewards.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < c.serializedRewards.Count; i++)
            list.Add(c.serializedRewards[i] ?? string.Empty);
        return list;
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 4) return null;
        int i = 0;
        var c = new CollectAndReportQuest();
        c.talkToNpc.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int required))
            return null;
        c.requiredCount.Value = required;
        c.reportMessage.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int idCount))
            return null;
        for (int j = 0; j < idCount; j++)
        {
            if (i >= payload.Count) return null;
            c.itemIds.Add(payload[i++]);
        }
        if (i >= payload.Count || !int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rewardCount))
            return null;
        for (int j = 0; j < rewardCount; j++)
        {
            if (i >= payload.Count) return null;
            c.serializedRewards.Add(payload[i++]);
        }
        return c;
    }
}
