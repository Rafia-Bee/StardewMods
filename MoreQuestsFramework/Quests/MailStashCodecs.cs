using System;
using System.Collections.Generic;
using System.Globalization;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Length-prefixed string lists for variable-size NetFields. Round-trip is exact for
// the framework's two built-in custom Quest subclasses, so a mail-delivered Adventure
// or Ship quest survives a save+reload before the player opens the letter.
internal static class AdventureQuestStashCodec
{
    public const string Kind = "MoreQuestsFramework.AdventureQuest";

    public static IList<string> Encode(Quest q)
    {
        var a = (AdventureQuest)q;
        var list = new List<string>
        {
            a.giverNpc.Value ?? string.Empty,
            a.completionMessage.Value ?? string.Empty,
            a.stepStates.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int i = 0; i < a.stepStates.Count; i++)
            list.Add(a.stepStates[i] ?? string.Empty);
        list.Add(a.serializedRewards.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < a.serializedRewards.Count; i++)
            list.Add(a.serializedRewards[i] ?? string.Empty);
        return list;
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 4) return null;
        int i = 0;
        var a = new AdventureQuest();
        a.giverNpc.Value = payload[i++];
        a.completionMessage.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stepCount))
            return null;
        for (int j = 0; j < stepCount; j++)
        {
            if (i >= payload.Count) return null;
            a.stepStates.Add(payload[i++]);
        }
        if (i >= payload.Count || !int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rewardCount))
            return null;
        for (int j = 0; j < rewardCount; j++)
        {
            if (i >= payload.Count) return null;
            a.serializedRewards.Add(payload[i++]);
        }
        return a;
    }
}

// Vanilla quest subclass codecs. Needed because the four VanillaXxx wrappers (and
// QuestFactory's Socialize / SlayMonster paths) set PreBuiltQuest to a fresh vanilla
// instance, and a mail redirect (e.g. when the giver is on IneligibleGivers) stashes
// it. The `parts` / `dialogueparts` / `objective` NetDescriptionElementList fields
// aren't encoded: ApplyPostingFields restores questDescription / currentObjective
// from the stash strings and flips _loadedDescription so vanilla doesn't rebuild
// from the netcoded parts. Progress counters are always 0 at stash time (player
// hasn't accepted the quest yet), so they don't need encoding either.
internal static class VanillaItemDeliveryQuestStashCodec
{
    public const string Kind = "Vanilla.ItemDeliveryQuest";

    public static IList<string> Encode(Quest q)
    {
        var i = (ItemDeliveryQuest)q;
        return new List<string>
        {
            i.target.Value ?? string.Empty,
            i.ItemId.Value ?? string.Empty,
            i.number.Value.ToString(CultureInfo.InvariantCulture),
            i.targetMessage ?? string.Empty
        };
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 4) return null;
        var q = new ItemDeliveryQuest();
        q.target.Value = payload[0];
        q.ItemId.Value = payload[1];
        if (!int.TryParse(payload[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            return null;
        q.number.Value = Math.Max(1, number);
        q.targetMessage = payload[3];
        return q;
    }
}

internal static class VanillaFishingQuestStashCodec
{
    public const string Kind = "Vanilla.FishingQuest";

    public static IList<string> Encode(Quest q)
    {
        var f = (FishingQuest)q;
        return new List<string>
        {
            f.target.Value ?? string.Empty,
            f.ItemId.Value ?? string.Empty,
            f.numberToFish.Value.ToString(CultureInfo.InvariantCulture),
            f.reward.Value.ToString(CultureInfo.InvariantCulture),
            f.targetMessage ?? string.Empty
        };
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 5) return null;
        var f = new FishingQuest();
        f.target.Value = payload[0];
        f.ItemId.Value = payload[1];
        if (!int.TryParse(payload[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toFish)) return null;
        f.numberToFish.Value = Math.Max(1, toFish);
        if (!int.TryParse(payload[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int reward)) return null;
        f.reward.Value = reward;
        f.targetMessage = payload[4];
        return f;
    }
}

internal static class VanillaSlayMonsterQuestStashCodec
{
    public const string Kind = "Vanilla.SlayMonsterQuest";

    public static IList<string> Encode(Quest q)
    {
        var s = (SlayMonsterQuest)q;
        return new List<string>
        {
            s.target.Value ?? string.Empty,
            s.monsterName.Value ?? string.Empty,
            s.numberToKill.Value.ToString(CultureInfo.InvariantCulture),
            s.reward.Value.ToString(CultureInfo.InvariantCulture),
            s.ignoreFarmMonsters.Value ? "1" : "0",
            s.targetMessage ?? string.Empty
        };
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 6) return null;
        var s = new SlayMonsterQuest();
        s.target.Value = payload[0];
        s.monsterName.Value = payload[1];
        if (!int.TryParse(payload[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toKill)) return null;
        s.numberToKill.Value = Math.Max(1, toKill);
        if (!int.TryParse(payload[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int reward)) return null;
        s.reward.Value = reward;
        s.ignoreFarmMonsters.Value = payload[4] == "1";
        s.targetMessage = payload[5];
        return s;
    }
}

internal static class VanillaResourceCollectionQuestStashCodec
{
    public const string Kind = "Vanilla.ResourceCollectionQuest";

    public static IList<string> Encode(Quest q)
    {
        var r = (ResourceCollectionQuest)q;
        return new List<string>
        {
            r.target.Value ?? string.Empty,
            r.ItemId.Value ?? string.Empty,
            r.number.Value.ToString(CultureInfo.InvariantCulture),
            r.reward.Value.ToString(CultureInfo.InvariantCulture),
            r.targetMessage.Value ?? string.Empty
        };
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 5) return null;
        var r = new ResourceCollectionQuest();
        r.target.Value = payload[0];
        r.ItemId.Value = payload[1];
        if (!int.TryParse(payload[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return null;
        r.number.Value = Math.Max(1, number);
        if (!int.TryParse(payload[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int reward)) return null;
        r.reward.Value = reward;
        r.targetMessage.Value = payload[4];
        return r;
    }
}

internal static class MoreQuestsEarnMoneyQuestStashCodec
{
    public const string Kind = "MoreQuestsFramework.MoreQuestsEarnMoneyQuest";

    public static IList<string> Encode(Quest q)
    {
        var m = (MoreQuestsEarnMoneyQuest)q;
        var list = new List<string>
        {
            m.target.Value ?? string.Empty,
            m.goldTarget.Value.ToString(CultureInfo.InvariantCulture),
            m.baselineCaptured.Value ? "1" : "0",
            m.baselineEarned.Value.ToString(CultureInfo.InvariantCulture),
            m.earnedSoFar.Value.ToString(CultureInfo.InvariantCulture),
            m.baseObjective.Value ?? string.Empty,
            m.serializedRewards.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int i = 0; i < m.serializedRewards.Count; i++)
            list.Add(m.serializedRewards[i] ?? string.Empty);
        list.Add(m.targetMessage ?? string.Empty);
        return list;
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 7) return null;
        int i = 0;
        var m = new MoreQuestsEarnMoneyQuest();
        m.target.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int target)) return null;
        m.goldTarget.Value = target;
        m.baselineCaptured.Value = payload[i++] == "1";
        if (!long.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out long baseline)) return null;
        m.baselineEarned.Value = baseline;
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int earned)) return null;
        m.earnedSoFar.Value = earned;
        m.baseObjective.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rewardCount)) return null;
        for (int j = 0; j < rewardCount; j++)
        {
            if (i >= payload.Count) return null;
            m.serializedRewards.Add(payload[i++]);
        }
        if (i < payload.Count)
            m.targetMessage = payload[i++];
        return m;
    }
}

internal static class MoreQuestsShipQuestStashCodec
{
    public const string Kind = "MoreQuestsFramework.MoreQuestsShipQuest";

    public static IList<string> Encode(Quest q)
    {
        var s = (MoreQuestsShipQuest)q;
        var list = new List<string>
        {
            s.target.Value ?? string.Empty,
            s.itemId.Value ?? string.Empty,
            s.itemWeight.Value.ToString(CultureInfo.InvariantCulture),
            s.numberToShip.Value.ToString(CultureInfo.InvariantCulture),
            s.numberShipped.Value.ToString(CultureInfo.InvariantCulture),
            s.allowDecorShipping.Value ? "1" : "0",
            s.baseObjective.Value ?? string.Empty,
            s.alternativeItemIds.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int i = 0; i < s.alternativeItemIds.Count; i++)
            list.Add(s.alternativeItemIds[i] ?? string.Empty);
        list.Add(s.alternativeItemWeights.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < s.alternativeItemWeights.Count; i++)
            list.Add(s.alternativeItemWeights[i].ToString(CultureInfo.InvariantCulture));
        list.Add(s.serializedRewards.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < s.serializedRewards.Count; i++)
            list.Add(s.serializedRewards[i] ?? string.Empty);
        list.Add(s.objectiveItemName ?? string.Empty);
        list.Add(s.targetMessage ?? string.Empty);
        return list;
    }

    public static Quest? Decode(IList<string> payload)
    {
        if (payload.Count < 8) return null;
        int i = 0;
        var s = new MoreQuestsShipQuest();
        s.target.Value = payload[i++];
        s.itemId.Value = payload[i++];
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight)) return null;
        s.itemWeight.Value = weight;
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toShip)) return null;
        s.numberToShip.Value = toShip;
        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int shipped)) return null;
        s.numberShipped.Value = shipped;
        s.allowDecorShipping.Value = payload[i++] == "1";
        s.baseObjective.Value = payload[i++];

        if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int altIdCount)) return null;
        for (int j = 0; j < altIdCount; j++)
        {
            if (i >= payload.Count) return null;
            s.alternativeItemIds.Add(payload[i++]);
        }
        if (i >= payload.Count || !int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int altWeightCount)) return null;
        for (int j = 0; j < altWeightCount; j++)
        {
            if (i >= payload.Count) return null;
            if (!int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)) return null;
            s.alternativeItemWeights.Add(w);
        }
        if (i >= payload.Count || !int.TryParse(payload[i++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rewardCount)) return null;
        for (int j = 0; j < rewardCount; j++)
        {
            if (i >= payload.Count) return null;
            s.serializedRewards.Add(payload[i++]);
        }
        // Trailing optional fields. Pre-existing payloads from older saves won't have
        // them, so missing entries fall back to the field defaults (empty strings).
        if (i < payload.Count)
            s.objectiveItemName = payload[i++];
        if (i < payload.Count)
            s.targetMessage = payload[i++];
        return s;
    }
}
