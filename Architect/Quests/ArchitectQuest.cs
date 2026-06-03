using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;
using Architect.Services;
using MoreQuestsFramework;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Quests;

namespace Architect.Quests;

// A redecoration quest. The giver hands over a budget on accept; the player buys
// furniture and places it inside the giver's home. Each matching placement ticks an
// objective and draws the budget down by that piece's price. On completion the unused
// budget returns to the giver (net player cost = max(0, spent - budget)).
[XmlType("Mods_RafiaBee_Architect_ArchitectQuest")]
public sealed class ArchitectQuest : Quest, IRewardedQuest, IObjectiveLineSource
{
    public readonly NetString giverNpc = new();
    public readonly NetString homeLocation = new();

    // One "category|count|progress" entry per objective.
    public readonly NetStringList objectiveStates = new();

    public readonly NetInt budget = new();
    public readonly NetInt budgetRemaining = new();
    public readonly NetBool budgetGranted = new();
    public readonly NetBool settled = new();
    public readonly NetStringList serializedRewards = new();

    [XmlIgnore]
    public NetStringList SerializedRewards => serializedRewards;

    // Rebuilt at runtime (never net-synced). The high-water snapshot of furniture in the
    // home, keyed by item id + tile, so only genuinely new pieces credit and the persisted
    // progress stays authoritative across reloads and room re-entries.
    [XmlIgnore]
    private Dictionary<(string itemId, int x, int y), Furniture>? _placedSnapshot;
    [XmlIgnore]
    private bool _snapshotSeeded;

    public ArchitectQuest()
    {
        // Keep the posting's title so vanilla doesn't regenerate it.
        _loadedTitle = true;
    }

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(giverNpc, "giverNpc")
            .AddField(homeLocation, "homeLocation")
            .AddField(objectiveStates, "objectiveStates")
            .AddField(budget, "budget")
            .AddField(budgetRemaining, "budgetRemaining")
            .AddField(budgetGranted, "budgetGranted")
            .AddField(settled, "settled")
            .AddField(serializedRewards, "serializedRewards");
    }

    public void Initialize(string giver, string home, IEnumerable<(string category, int count)> objectives, int budgetAmount)
    {
        giverNpc.Value = giver;
        homeLocation.Value = home;
        objectiveStates.Clear();
        foreach (var (category, count) in objectives)
            objectiveStates.Add($"{category}|{count}|0");
        budget.Value = budgetAmount;
        budgetRemaining.Value = budgetAmount;
        budgetGranted.Value = false;
        settled.Value = false;
    }

    // Pays the budget the first time the quest lands in the player's log. Idempotent via
    // the synced flag, so the reload-time re-fire of QuestAccepted can't double-pay.
    public void GrantBudgetOnAccept()
    {
        if (budgetGranted.Value)
            return;
        Game1.player.Money += budget.Value;
        budgetGranted.Value = true;
    }

    // Seed the high-water baseline against whatever's in the room right now, so furniture
    // that was already there when the player walked in never counts. Called on warp into
    // the home so there's no 1-second window where an instant placement gets baselined.
    public void SeedSnapshot(GameLocation? location)
    {
        if (location == null)
            return;
        _placedSnapshot = BuildSnapshot(location);
        _snapshotSeeded = true;
    }

    private static Dictionary<(string, int, int), Furniture> BuildSnapshot(GameLocation location)
    {
        var snapshot = new Dictionary<(string, int, int), Furniture>();
        var furniture = location.furniture;
        if (furniture != null)
        {
            foreach (var f in furniture)
            {
                if (f == null)
                    continue;
                var key = (f.ItemId ?? string.Empty, (int)f.TileLocation.X, (int)f.TileLocation.Y);
                snapshot[key] = f;
            }
        }
        return snapshot;
    }

    public void ObservePlacements(GameLocation? location)
    {
        if (completed.Value || location == null)
            return;

        var cur = BuildSnapshot(location);

        if (!_snapshotSeeded)
        {
            _placedSnapshot = cur;
            _snapshotSeeded = true;
            return;
        }

        var objectives = ReadObjectives();
        bool changed = false;

        foreach (var pair in cur)
        {
            if (_placedSnapshot!.ContainsKey(pair.Key))
                continue;
            // A newly placed piece. Credit the first still-open objective it matches.
            for (int i = 0; i < objectives.Count; i++)
            {
                var o = objectives[i];
                if (o.progress >= o.count)
                    continue;
                if (!FurnitureCategory.Matches(pair.Value, o.category))
                    continue;
                o.progress++;
                objectives[i] = o;
                budgetRemaining.Value -= pair.Value.salePrice();
                changed = true;
                break;
            }
        }

        _placedSnapshot = cur;

        if (!changed)
            return;

        WriteObjectives(objectives);
        reloadObjective();
        Game1.dayTimeMoneyBox.pingQuest(this);

        if (AllObjectivesDone(objectives))
            questComplete();
    }

    private List<(string category, int count, int progress)> ReadObjectives()
    {
        var list = new List<(string, int, int)>();
        foreach (var entry in objectiveStates)
        {
            if (string.IsNullOrEmpty(entry))
                continue;
            var parts = entry.Split('|');
            if (parts.Length < 3)
                continue;
            int count = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) ? c : 0;
            int progress = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 0;
            list.Add((parts[0], count, progress));
        }
        return list;
    }

    private void WriteObjectives(List<(string category, int count, int progress)> objectives)
    {
        objectiveStates.Clear();
        foreach (var o in objectives)
            objectiveStates.Add($"{o.category}|{o.count}|{o.progress}");
    }

    private static bool AllObjectivesDone(List<(string category, int count, int progress)> objectives)
    {
        foreach (var o in objectives)
            if (o.progress < o.count)
                return false;
        return objectives.Count > 0;
    }

    public IReadOnlyList<string> GetObjectiveLines() => BuildObjectiveLines();

    public List<string> BuildObjectiveLines()
    {
        var lines = new List<string>();
        var translation = ModEntry.Translation;
        foreach (var o in ReadObjectives())
        {
            string key = FurnitureCategory.I18nKey(o.category);
            string line = translation != null
                ? translation.Get(key, new { progress = o.progress, count = o.count }).ToString()
                : $"{o.category} {o.progress}/{o.count}";
            lines.Add(line);
        }

        int remaining = budgetRemaining.Value;
        string budgetLine;
        if (remaining >= 0)
            budgetLine = translation != null
                ? translation.Get("objective.budget", new { amount = remaining }).ToString()
                : $"Budget remaining: {remaining}g";
        else
            budgetLine = translation != null
                ? translation.Get("objective.budget.over", new { amount = -remaining }).ToString()
                : $"Over budget by {-remaining}g";
        lines.Add(budgetLine);

        if (lines.Count == 0)
            lines.Add(string.Empty);
        return lines;
    }

    public override void reloadObjective()
    {
        var lines = BuildObjectiveLines();
        currentObjective = lines.Count > 0 ? lines[0] : string.Empty;
    }

    // Returns the unused budget to the giver. Net player cost ends up max(0, spent - budget).
    // Idempotent so the completion path and the cancel/expire path can't both fire it.
    public void Settle()
    {
        if (settled.Value)
            return;
        if (budgetRemaining.Value > 0)
            Game1.player.Money = Math.Max(0, Game1.player.Money - budgetRemaining.Value);
        settled.Value = true;
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        Settle();
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }
}
