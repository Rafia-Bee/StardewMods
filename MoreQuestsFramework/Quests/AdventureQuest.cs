using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Active step = first not-Done step whose Requires[] are all Done. Step state rides
// as one encoded line per step inside stepStates.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_AdventureQuest")]
public sealed class AdventureQuest : Quest, IRewardedQuest
{
    public readonly NetStringList stepStates = new();
    public readonly NetStringList serializedRewards = new();
    public readonly NetString giverNpc = new();

    // Spoken by the closing NPC. Gift-step completions never push this so vanilla's
    // gift-reaction line takes priority.
    public readonly NetString completionMessage = new();

    public NetStringList SerializedRewards => serializedRewards;

    private List<AdventureStepState>? _decoded;

    // Per-step baseline ResourceClump count for ClearDebris. Re-baselines on save load.
    private Dictionary<int, int>? _clumpBaselines;

    public bool HasDecorShippingStep
    {
        get
        {
            var steps = Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].AllowDecorShipping)
                    return true;
            }
            return false;
        }
    }

    // Used by the shipping-bin Harmony postfix so vanilla only opens the shipping
    // door for the specific item ids a live decor-shipping step actually wants.
    public bool CanShipForDecor(Item item)
    {
        if (item == null || completed.Value) return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !step.AllowDecorShipping) continue;
            if (ItemMatches(step, item)) return true;
        }
        return false;
    }

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(stepStates, "stepStates")
            .AddField(serializedRewards, "serializedRewards")
            .AddField(giverNpc, "giverNpc")
            .AddField(completionMessage, "completionMessage");
    }

    public void Initialize(IEnumerable<AdventureStepState> steps, string giver, string? completionDialogue = null)
    {
        stepStates.Clear();
        foreach (var s in steps)
            stepStates.Add(AdventureStepCodec.Encode(s));
        giverNpc.Value = giver ?? string.Empty;
        completionMessage.Value = completionDialogue ?? string.Empty;
        _decoded = null;
    }

    private List<AdventureStepState> Steps
    {
        get
        {
            if (_decoded == null || _decoded.Count != stepStates.Count)
                Refresh();
            return _decoded!;
        }
    }

    private void Refresh()
    {
        _decoded = new List<AdventureStepState>(stepStates.Count);
        for (int i = 0; i < stepStates.Count; i++)
        {
            var s = AdventureStepCodec.Decode(stepStates[i]);
            if (s != null)
                _decoded.Add(s);
        }
    }

    private void Persist(int index, AdventureStepState state)
    {
        stepStates[index] = AdventureStepCodec.Encode(state);
        if (_decoded != null && index < _decoded.Count)
            _decoded[index] = state;
    }

    public bool HasActiveStepTargeting(AdventureStepKind kind, string locationName)
    {
        if (completed.Value || string.IsNullOrEmpty(locationName))
            return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done) continue;
            if (step.Kind != kind) continue;
            if (LocationMatches(step, locationName))
                return true;
        }
        return false;
    }

    // Returns -1 when every step is done or no step is reachable (malformed Requires).
    public int ActiveStepIndex()
    {
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Done) continue;
            if (RequiresMet(steps, steps[i]))
                return i;
        }
        return -1;
    }

    private static bool RequiresMet(List<AdventureStepState> steps, AdventureStepState step)
    {
        if (step.Requires.Count == 0) return true;
        foreach (var req in step.Requires)
        {
            bool found = false;
            foreach (var s in steps)
            {
                if (string.Equals(s.Name, req, StringComparison.OrdinalIgnoreCase))
                {
                    if (!s.Done) return false;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    public override void reloadObjective()
    {
        if (completed.Value)
            return;
        // Single-line summary for callers reading currentObjective directly. The journal
        // uses GetObjectiveDescriptions (patched to one entry per active step).
        var lines = BuildActiveObjectiveDescriptions();
        _currentObjective = lines.Count == 0 ? string.Empty : lines[0];
    }

    public List<string> BuildActiveObjectiveDescriptions()
    {
        var lines = new List<string>();
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done) continue;
            if (!RequiresMet(steps, step)) continue;
            string line = step.Description;
            if (step.Count > 1)
                line = $"{line} ({step.Progress}/{step.Count})";
            lines.Add(line);
        }
        if (lines.Count == 0)
            lines.Add(string.Empty);
        return lines;
    }

    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value || npc == null || item == null)
            return false;
        // Walk every active step (not just the first) so the player can hand in items
        // in any order (e.g. "deliver Flour + Sugar + Eggs to Evelyn").
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            bool advanced = step.Kind switch
            {
                AdventureStepKind.Deliver => TryAdvanceDeliver(i, step, npc, item, probe),
                AdventureStepKind.Gift => TryAdvanceGift(i, step, npc, item, probe),
                AdventureStepKind.GiftUniqueNpcs => TryAdvanceGiftUniqueNpcs(i, step, npc, item, probe),
                _ => false
            };
            if (advanced)
                return true;
        }
        return false;
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (completed.Value || npc == null)
            return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Talk) continue;
            if (TryAdvanceTalk(i, step, npc, probe))
                return true;
        }
        return false;
    }

    public override bool OnFishCaught(string fishId, int numberCaught, int size, bool probe = false)
    {
        if (completed.Value || string.IsNullOrEmpty(fishId))
            return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Catch) continue;
            TryAdvanceCatch(i, step, fishId, numberCaught, size, probe);
        }
        return false;
    }

    public override bool OnMonsterSlain(GameLocation location, Monster monster, bool killedByBomb, bool isTameMonster, bool probe = false)
    {
        if (completed.Value || monster == null || isTameMonster)
            return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Slay) continue;
            TryAdvanceSlay(i, step, monster, probe);
        }
        return false;
    }

    // Mine target = min(120, deepest). SkullCavern target = max(0, deepest - 120).
    public void ObserveReachLevel(int deepestMineLevel)
    {
        if (completed.Value)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.ReachLevel) continue;

            string target = step.Targets.Count > 0 ? step.Targets[0] : "Mine";
            int currentFloor = target.Replace(" ", "").ToLowerInvariant() switch
            {
                "skullcavern" => Math.Max(0, deepestMineLevel - 120),
                _ => Math.Min(120, deepestMineLevel)
            };

            if (currentFloor >= step.Count)
            {
                step.Progress = step.Count;
                MarkStepDone(i, step);
            }
            else if (currentFloor != step.Progress)
            {
                step.Progress = currentFloor;
                Persist(i, step);
                reloadObjective();
            }
        }
    }

    public void ObserveShippingBin(IList<Item> bin)
    {
        if (completed.Value || bin == null || bin.Count == 0)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Ship) continue;
            AdvanceShipStep(i, step, bin);
        }
    }

    private void AdvanceShipStep(int idx, AdventureStepState step, IList<Item> bin)
    {
        int matched = 0;
        for (int i = 0; i < bin.Count; i++)
        {
            var item = bin[i];
            if (item == null) continue;
            if (ItemMatches(step, item))
                matched += item.Stack;
        }
        if (matched <= 0)
            return;

        int needed = Math.Max(1, step.Count);
        int credit = Math.Min(matched, needed - step.Progress);
        if (credit <= 0)
            return;
        step.Progress += credit;
        if (step.Progress >= needed)
            MarkStepDone(idx, step);
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
    }

    // Items[] carries "with N following animals" gates (e.g. $follower-count:2).
    public void ObserveVisit(string locationName)
    {
        if (completed.Value || string.IsNullOrEmpty(locationName))
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Visit) continue;
            if (!LocationMatches(step, locationName)) continue;
            if (step.Items.Count > 0 && !VisitGatesMet(step))
                continue;
            MarkStepDone(i, step);
        }
    }

    public void ObserveBuild(IReadOnlyCollection<string> newBuildingTypes)
    {
        if (completed.Value || newBuildingTypes == null || newBuildingTypes.Count == 0)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Build) continue;
            string target = step.Targets.Count > 0 ? step.Targets[0] : string.Empty;
            if (string.IsNullOrEmpty(target)) continue;
            foreach (var type in newBuildingTypes)
            {
                if (string.Equals(type, target, StringComparison.OrdinalIgnoreCase))
                {
                    MarkStepDone(i, step);
                    break;
                }
            }
        }
    }

    public void ObservePlantedTree(string locationName, int delta)
    {
        if (completed.Value || delta <= 0 || string.IsNullOrEmpty(locationName))
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Plant) continue;
            if (!LocationMatches(step, locationName)) continue;
            CreditCount(i, step, delta);
        }
    }

    public void ObserveWeedsCleared(string locationName, int delta)
    {
        if (completed.Value || delta <= 0 || string.IsNullOrEmpty(locationName))
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.ClearWeeds) continue;
            if (!LocationMatches(step, locationName)) continue;
            CreditCount(i, step, delta);
        }
    }

    // Counts Large Stump, Large Log, Boulder, Meteorite, mine rocks, quarry boulder.
    // Green rain bushes and giant crops are filtered out.
    public void PollResourceClumps(GameLocation? location)
    {
        if (completed.Value || location == null)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.ClearDebris) continue;
            if (!LocationMatches(step, location.Name)) continue;

            int current = CountTargetClumps(location);
            _clumpBaselines ??= new Dictionary<int, int>();
            if (!_clumpBaselines.TryGetValue(i, out int baseline))
            {
                _clumpBaselines[i] = current;
                continue;
            }
            if (current < baseline)
            {
                int delta = baseline - current;
                _clumpBaselines[i] = current;
                CreditCount(i, step, delta);
            }
            else if (current > baseline)
            {
                _clumpBaselines[i] = current;
            }
        }
    }

    private static bool IsTargetClumpIndex(int idx) =>
        idx == StardewValley.TerrainFeatures.ResourceClump.stumpIndex
        || idx == StardewValley.TerrainFeatures.ResourceClump.hollowLogIndex
        || idx == StardewValley.TerrainFeatures.ResourceClump.boulderIndex
        || idx == StardewValley.TerrainFeatures.ResourceClump.meteoriteIndex
        || idx == StardewValley.TerrainFeatures.ResourceClump.mineRock1Index
        || idx == StardewValley.TerrainFeatures.ResourceClump.mineRock2Index
        || idx == StardewValley.TerrainFeatures.ResourceClump.mineRock3Index
        || idx == StardewValley.TerrainFeatures.ResourceClump.mineRock4Index
        || idx == StardewValley.TerrainFeatures.ResourceClump.quarryBoulderIndex;

    private static int CountTargetClumps(GameLocation location)
    {
        var clumps = location.resourceClumps;
        if (clumps == null) return 0;
        int total = 0;
        for (int i = 0; i < clumps.Count; i++)
        {
            var c = clumps[i];
            if (c is StardewValley.TerrainFeatures.GiantCrop) continue;
            if (IsTargetClumpIndex(c.parentSheetIndex.Value))
                total++;
        }
        return total;
    }

    // Walks every active Custom step and runs the supplied resolver. The resolver
    // returns the registered handler keyed by step.Targets[0] (or null when unknown);
    // the framework's per-second tick wires this to CustomStepRegistry.
    public void PollCustomSteps(Func<AdventureStepState, Func<CustomStepContext, int>?> resolver)
    {
        if (completed.Value || resolver == null)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Custom) continue;
            var handler = resolver(step);
            if (handler == null) continue;
            int delta;
            try
            {
                delta = handler(new CustomStepContext(this, step));
            }
            catch (Exception)
            {
                continue;
            }
            if (delta > 0)
                CreditCount(i, step, delta);
        }
    }

    public void ObserveDebrisCleared(string locationName, int delta)
    {
        if (completed.Value || delta <= 0 || string.IsNullOrEmpty(locationName))
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.ClearDebris) continue;
            if (!LocationMatches(step, locationName)) continue;
            CreditCount(i, step, delta);
        }
    }

    private void CreditCount(int idx, AdventureStepState step, int delta)
    {
        int needed = Math.Max(1, step.Count);
        int credit = Math.Min(delta, needed - step.Progress);
        if (credit <= 0)
            return;
        step.Progress += credit;
        if (step.Progress >= needed)
            MarkStepDone(idx, step);
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
    }

    // Empty/zero filters pass through.
    private static bool CatchFiltersPass(AdventureStepState step, int size)
    {
        if (!string.IsNullOrEmpty(step.LocationName))
        {
            string current = Game1.currentLocation?.Name ?? string.Empty;
            if (!string.Equals(current, step.LocationName, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        if (step.MinSize > 0 && size < step.MinSize)
            return false;
        if (!string.IsNullOrEmpty(step.Weather))
        {
            if (!CurrentWeatherMatches(step.Weather))
                return false;
        }
        return true;
    }

    private static bool CurrentWeatherMatches(string requested)
    {
        string contextId = Game1.player?.currentLocation?.GetLocationContextId() ?? "Default";
        string actual = Game1.netWorldState?.Value?.GetWeatherForLocation(contextId)?.Weather ?? string.Empty;
        if (string.IsNullOrEmpty(actual))
            return false;
        string norm = requested.ToLowerInvariant() switch
        {
            "sunny" => "Sun",
            "rainy" => "Rain",
            "stormy" => "Storm",
            "snowy" => "Snow",
            "windy" => "Wind",
            _ => requested
        };
        if (string.Equals(norm, "Rain", StringComparison.OrdinalIgnoreCase))
            return string.Equals(actual, "Rain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, "Storm", StringComparison.OrdinalIgnoreCase);
        return string.Equals(actual, norm, StringComparison.OrdinalIgnoreCase);
    }

    // "*" matches any, "!Name" excludes. Targets=["*", "!Farm"] = anywhere except Farm.
    private static bool LocationMatches(AdventureStepState step, string locationName)
    {
        if (step.Targets.Count == 0)
            return false;
        bool anyPositive = false;
        bool positiveMatched = false;
        foreach (var t in step.Targets)
        {
            if (string.IsNullOrEmpty(t))
                continue;
            if (t.StartsWith("!", StringComparison.Ordinal))
            {
                if (string.Equals(t.Substring(1), locationName, StringComparison.OrdinalIgnoreCase))
                    return false;
                continue;
            }
            anyPositive = true;
            if (t == "*" || string.Equals(t, locationName, StringComparison.OrdinalIgnoreCase))
                positiveMatched = true;
        }
        return anyPositive && positiveMatched;
    }

    // Only $follower-count:N today. Unknown tokens fail the gate so future tokens
    // don't auto-complete on saves where they aren't implemented yet.
    private static bool VisitGatesMet(AdventureStepState step)
    {
        foreach (var entry in step.Items)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            if (entry.StartsWith("$follower-count:", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = entry.Substring("$follower-count:".Length).Trim();
                if (!int.TryParse(suffix, out int required))
                    return false;
                if (FollowerApiBridge.CurrentCount() < required)
                    return false;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    private bool TryAdvanceDeliver(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        if (!TargetMatches(step, npc.Name)) return false;
        if (!ItemMatches(step, item)) return false;
        if (step.MinQuality > 0 && (item is not StardewValley.Object obj || obj.Quality < step.MinQuality)) return false;

        int remaining = step.Count - step.Progress;
        if (remaining <= 0) return false;

        if (probe) return true;

        int accept = Math.Min(item.Stack, remaining);
        Game1.player.Items.Reduce(item, accept);
        step.Progress += accept;

        if (step.Progress >= step.Count)
        {
            bool willComplete = WillStepCompleteQuest(idx);
            if (willComplete && !string.IsNullOrEmpty(completionMessage.Value))
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, completionMessage.Value));
                Game1.drawDialogue(npc);
            }
            if (HasMultipleDeliverSteps())
                Game1.playSound("give_gift");
            MarkStepDone(idx, step);
        }
        else
        {
            Game1.playSound("give_gift");
            Persist(idx, step);
            reloadObjective();
            string partial = TryGetPartialDialogue(step.Count - step.Progress);
            if (!string.IsNullOrEmpty(partial))
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, partial));
                Game1.drawDialogue(npc);
            }
        }
        return true;
    }

    private static string TryGetPartialDialogue(int remaining)
    {
        var translation = ModEntry.Translation;
        if (translation == null)
            return string.Empty;
        string text = translation.Get("quest.itemDelivery.partial.thanks", new { remaining }).ToString();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
    }

    // Single-step quests use vanilla's complete fanfare; multi-ingredient asks need
    // a per-handoff cue so the player knows that hand-off counted.
    private bool HasMultipleDeliverSteps()
    {
        var steps = Steps;
        int count = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Kind == AdventureStepKind.Deliver && ++count > 1)
                return true;
        }
        return false;
    }

    // Returns false so vanilla's gift flow (friendship bump, daily counter) still runs.
    private bool TryAdvanceGift(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        if (!TargetMatches(step, npc.Name)) return false;
        // Empty Items list = "any thoughtful item to the target".
        if (step.Items.Count > 0 && !ItemMatches(step, item)) return false;

        int taste = npc.getGiftTasteForThisItem(item);
        bool acceptable = taste == NPC.gift_taste_love
                       || taste == NPC.gift_taste_like
                       || taste == NPC.gift_taste_stardroptea;
        if (!acceptable)
            return false;

        if (probe) return false;

        MarkStepDone(idx, step);
        return false;
    }

    // CreditedKeys dedupes by NPC name. Returns false so vanilla's gift flow still runs.
    private bool TryAdvanceGiftUniqueNpcs(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        if (!npc.IsVillager) return false;
        if (step.Targets.Count > 0)
        {
            bool whitelisted = false;
            foreach (var t in step.Targets)
            {
                if (string.Equals(t, npc.Name, StringComparison.OrdinalIgnoreCase))
                {
                    whitelisted = true;
                    break;
                }
            }
            if (!whitelisted) return false;
        }

        if (step.Items.Count > 0 && !ItemMatches(step, item))
            return false;

        int taste = npc.getGiftTasteForThisItem(item);
        bool acceptable = taste == NPC.gift_taste_love
                       || taste == NPC.gift_taste_like
                       || taste == NPC.gift_taste_stardroptea;
        if (!acceptable) return false;

        if (step.CreditedKeys.Contains(npc.Name, StringComparer.OrdinalIgnoreCase))
            return false;

        if (probe) return false;

        step.CreditedKeys.Add(npc.Name);
        step.Progress++;
        if (step.Progress >= step.Count)
            MarkStepDone(idx, step);
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
        return false;
    }

    // Returns false so vanilla's other quests still see the catch.
    private bool TryAdvanceCatch(int idx, AdventureStepState step, string fishId, int numberCaught, int size, bool probe)
    {
        if (step.Items.Count == 0)
            return false;
        bool matched = false;
        foreach (var entry in step.Items)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            if (string.Equals(entry, fishId, StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                break;
            }
            if (entry.StartsWith("(", StringComparison.Ordinal))
            {
                string bare = entry.Substring(entry.IndexOf(')') + 1);
                if (string.Equals(bare, fishId, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }
        }
        if (!matched) return false;
        if (!CatchFiltersPass(step, size)) return false;
        if (probe) return true;

        int needed = Math.Max(1, step.Count);
        int credit = Math.Min(numberCaught, needed - step.Progress);
        if (credit <= 0)
            return false;
        step.Progress += credit;
        if (step.Progress >= needed)
            MarkStepDone(idx, step);
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
        return false;
    }

    // Empty Targets = any monster. Tame monsters never count; bomb kills do.
    private bool TryAdvanceSlay(int idx, AdventureStepState step, Monster monster, bool probe)
    {
        bool matched = step.Targets.Count == 0;
        if (!matched)
        {
            foreach (var t in step.Targets)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (string.Equals(t, monster.Name, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }
        }
        if (!matched) return false;
        if (probe) return true;

        int needed = Math.Max(1, step.Count);
        if (step.Progress >= needed)
            return false;
        step.Progress++;
        if (step.Progress >= needed)
            MarkStepDone(idx, step);
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
        return false;
    }

    // CreditedKeys dedupes so "talk to 3 different villagers" can't be satisfied by
    // the same NPC three times.
    private bool TryAdvanceTalk(int idx, AdventureStepState step, NPC npc, bool probe)
    {
        if (!TargetMatches(step, npc.Name)) return false;
        if (step.CreditedKeys.Contains(npc.Name, StringComparer.OrdinalIgnoreCase))
            return false;

        if (probe)
            return true;

        step.CreditedKeys.Add(npc.Name);
        step.Progress++;
        if (step.Progress >= step.Count)
        {
            if (WillStepCompleteQuest(idx) && !string.IsNullOrEmpty(completionMessage.Value))
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, completionMessage.Value));
                Game1.drawDialogue(npc);
            }
            MarkStepDone(idx, step);
        }
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
        return true;
    }

    private bool WillStepCompleteQuest(int doneIdx)
    {
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (i == doneIdx) continue;
            if (!steps[i].Done) return false;
        }
        return true;
    }

    private void MarkStepDone(int idx, AdventureStepState step)
    {
        step.Done = true;
        step.Progress = Math.Max(step.Progress, step.Count);
        Persist(idx, step);

        if (ActiveStepIndex() < 0)
            questComplete();
        else
            reloadObjective();
    }

    // Empty Targets matches giverNpc. $giver is rewritten at Initialize.
    private bool TargetMatches(AdventureStepState step, string npcName)
    {
        if (step.Targets.Count == 0)
            return string.Equals(npcName, giverNpc.Value, StringComparison.OrdinalIgnoreCase);
        foreach (var t in step.Targets)
        {
            if (string.Equals(t, npcName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool ItemMatches(AdventureStepState step, Item item)
    {
        if (step.Items.Count == 0)
            return false;
        string qid = item.QualifiedItemId ?? string.Empty;
        string id = item.ItemId ?? string.Empty;
        foreach (var entry in step.Items)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            if (entry.StartsWith("$", StringComparison.Ordinal))
            {
                if (TokenMatches(entry, item)) return true;
                continue;
            }
            if (string.Equals(entry, qid, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(entry, id, StringComparison.OrdinalIgnoreCase)) return true;
            if (entry.StartsWith("(") && string.Equals(entry.Substring(entry.IndexOf(')') + 1), id, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Tokens: $edible-egg, $category:N, $forage (= $tag:forage_item), $furniture-table,
    // $tag:<tag>. $edible-egg excludes Dinosaur Egg (Edibility -300).
    private static bool TokenMatches(string token, Item item)
    {
        const int eggCategory = -5;
        const int inedible = -300;
        if (string.Equals(token, "$edible-egg", StringComparison.OrdinalIgnoreCase))
            return item is StardewValley.Object eggObj && eggObj.Category == eggCategory && eggObj.Edibility != inedible;
        if (string.Equals(token, "$forage", StringComparison.OrdinalIgnoreCase))
            return HasContextTag(item, "forage_item");
        if (string.Equals(token, "$furniture-table", StringComparison.OrdinalIgnoreCase))
        {
            if (item is not StardewValley.Objects.Furniture f) return false;
            int t = f.furniture_type.Value;
            return t == StardewValley.Objects.Furniture.table || t == StardewValley.Objects.Furniture.longTable;
        }
        if (token.StartsWith("$category:", StringComparison.OrdinalIgnoreCase))
        {
            if (item is StardewValley.Object catObj && int.TryParse(token.Substring("$category:".Length), out int cat))
                return catObj.Category == cat;
            return false;
        }
        if (token.StartsWith("$tag:", StringComparison.OrdinalIgnoreCase))
        {
            string tag = token.Substring("$tag:".Length).Trim();
            return !string.IsNullOrEmpty(tag) && HasContextTag(item, tag);
        }
        return false;
    }

    private static bool HasContextTag(Item item, string tag)
    {
        if (item == null)
            return false;
        try
        {
            // HasBaseTag covers authored + runtime tags (preserve_sheet_* etc).
            return StardewValley.ItemContextTagManager.HasBaseTag(item.QualifiedItemId, tag);
        }
        catch
        {
            return false;
        }
    }
}
