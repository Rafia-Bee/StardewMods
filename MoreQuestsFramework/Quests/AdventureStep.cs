using System;
using System.Collections.Generic;
using System.Text;

namespace MoreQuestsFramework.Quests;

// One step's definition + runtime progress fits in a single NetStringList entry per
// step, keeping Netcode flat (no nested NetCollections) and round-tripping cleanly
// through SpaceCore's serializer.
public enum AdventureStepKind
{
    Deliver,
    Talk,
    Gift,
    GiftUniqueNpcs,
    Catch,
    Slay,
    Ship,
    Visit,
    Build,
    ReachLevel,
    Plant,
    Collect,
    ClearDebris,
    ClearWeeds,
    DropItems,
    Custom
}

public sealed class AdventureStepState
{
    public string Name { get; set; } = string.Empty;
    public AdventureStepKind Kind { get; set; }
    public List<string> Requires { get; set; } = new();
    public List<string> Targets { get; set; } = new();
    public List<string> Items { get; set; } = new();
    public int Count { get; set; } = 1;
    public int MinQuality { get; set; }
    // Ship step opt-in to the decor shipping bypass. See DecorShippingPatches.
    public bool AllowDecorShipping { get; set; }
    public string LocationName { get; set; } = string.Empty;
    // Catch size in inches. Squid/Octopus/pond returns report -1 and fail this gate.
    public int MinSize { get; set; }
    // Sun/Rain/Storm/Snow/Wind (sunny/rainy aliases). "Rain" matches Rain+Storm.
    public string Weather { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool Done { get; set; }
    public string Description { get; set; } = string.Empty;

    // Uniqueness tracking for Talk/Gift ("talk to N different NPCs").
    public List<string> CreditedKeys { get; set; } = new();
}

internal static class AdventureStepCodec
{
    public static string Encode(AdventureStepState s)
    {
        var sb = new StringBuilder();
        sb.Append("Kind=").Append(s.Kind);
        sb.Append("|Name=").Append(Sanitise(s.Name));
        sb.Append("|Requires=").Append(JoinList(s.Requires));
        sb.Append("|Targets=").Append(JoinList(s.Targets));
        sb.Append("|Items=").Append(JoinList(s.Items));
        sb.Append("|Count=").Append(s.Count);
        sb.Append("|MinQuality=").Append(s.MinQuality);
        sb.Append("|AllowDecor=").Append(s.AllowDecorShipping ? "1" : "0");
        sb.Append("|Loc=").Append(Sanitise(s.LocationName));
        sb.Append("|MinSize=").Append(s.MinSize);
        sb.Append("|Weather=").Append(Sanitise(s.Weather));
        sb.Append("|Progress=").Append(s.Progress);
        sb.Append("|Done=").Append(s.Done ? "1" : "0");
        sb.Append("|Credited=").Append(JoinList(s.CreditedKeys));
        // Description last because it's the only field that may contain "|"; sanitised
        // to keep the decoder's flat split working and one step per NetStringList line.
        sb.Append("|Description=").Append((s.Description ?? string.Empty).Replace('|', '/').Replace('\n', ' '));
        return sb.ToString();
    }

    public static AdventureStepState? Decode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var state = new AdventureStepState();
        const string descMarker = "|Description=";
        int descAt = line.IndexOf(descMarker, StringComparison.Ordinal);
        string head;
        string description;
        if (descAt >= 0)
        {
            head = line.Substring(0, descAt);
            description = line.Substring(descAt + descMarker.Length);
        }
        else
        {
            head = line;
            description = string.Empty;
        }
        state.Description = description;

        foreach (var part in head.Split('|'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string key = part.Substring(0, eq);
            string val = part.Substring(eq + 1);
            switch (key)
            {
                case "Kind":
                    if (Enum.TryParse<AdventureStepKind>(val, ignoreCase: true, out var kind))
                        state.Kind = kind;
                    break;
                case "Name":
                    state.Name = val;
                    break;
                case "Requires":
                    state.Requires = SplitList(val);
                    break;
                case "Targets":
                    state.Targets = SplitList(val);
                    break;
                case "Items":
                    state.Items = SplitList(val);
                    break;
                case "Count":
                    int.TryParse(val, out int count);
                    state.Count = count;
                    break;
                case "MinQuality":
                    int.TryParse(val, out int q);
                    state.MinQuality = q;
                    break;
                case "AllowDecor":
                    state.AllowDecorShipping = val == "1" || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "Loc":
                    state.LocationName = val;
                    break;
                case "MinSize":
                    int.TryParse(val, out int minSize);
                    state.MinSize = minSize;
                    break;
                case "Weather":
                    state.Weather = val;
                    break;
                case "Progress":
                    int.TryParse(val, out int p);
                    state.Progress = p;
                    break;
                case "Done":
                    state.Done = val == "1" || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "Credited":
                    state.CreditedKeys = SplitList(val);
                    break;
            }
        }
        return state;
    }

    private static string Sanitise(string s) =>
        (s ?? string.Empty).Replace('|', '/').Replace(',', ' ');

    private static string JoinList(IList<string> list) =>
        list == null || list.Count == 0 ? string.Empty : string.Join(",", list);

    private static List<string> SplitList(string s)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(s))
            return result;
        foreach (var entry in s.Split(','))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length > 0)
                result.Add(trimmed);
        }
        return result;
    }
}
