using System;
using System.Collections.Generic;
using System.Text;

namespace MoreQuestsFramework.Quests;

/// One step in an `AdventureQuest`. Encodes both definition (Kind / Items / Count / Requires)
/// and runtime progress (Progress / Done) so the whole step can ride a single `NetStringList`
/// entry on the quest. The list-per-step layout keeps Netcode synchronisation flat — no
/// nested NetCollections — and lets `RewardCodec`-style line-encoded persistence round-trip
/// through SpaceCore's serializer without polymorphic JSON.
///
/// 7a wires up `Deliver`, `Talk`, and `Gift` handlers. Remaining kinds land in 7b/7c.
public enum AdventureStepKind
{
    Deliver,
    Talk,
    Gift,
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
    public int Progress { get; set; }
    public bool Done { get; set; }
    public string Description { get; set; } = string.Empty;

    /// Distinct names of NPCs/items already credited toward this step. Used by Talk and Gift
    /// to enforce uniqueness ("talk to N different NPCs"). Encoded as a comma-separated
    /// list so it round-trips through the same NetStringList entry as the rest of the step.
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
        sb.Append("|Progress=").Append(s.Progress);
        sb.Append("|Done=").Append(s.Done ? "1" : "0");
        sb.Append("|Credited=").Append(JoinList(s.CreditedKeys));
        // Description goes last because the journal text is the only field that may legitimately
        // contain `|`; any pipes are replaced with `/` at encode time so the decoder's flat
        // split-on-`|` keeps working. `\n` is also stripped to keep one step per NetStringList line.
        sb.Append("|Description=").Append((s.Description ?? string.Empty).Replace('|', '/').Replace('\n', ' '));
        return sb.ToString();
    }

    public static AdventureStepState? Decode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var state = new AdventureStepState();
        // Find the description marker first so we can split the rest as flat key=value pairs.
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
