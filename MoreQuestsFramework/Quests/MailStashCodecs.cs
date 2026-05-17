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
        return s;
    }
}
