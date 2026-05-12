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

/// Multi-step ("Adventure") quest. Each step ships its own kind (`Deliver`, `Talk`, `Gift`,
/// ...) plus optional `Requires[]` ordering; the active step is the first not-Done step
/// whose prerequisites are all Done. Step state — definition + progress + done flag — rides
/// inside `stepStates`, one encoded line per step, so a single `NetStringList` covers the
/// whole quest. Reward payout follows the same `IRewardedQuest` path the framework's other
/// custom subclasses already use.
///
/// Phase 7a wires `Deliver`, `Talk`, and `Gift` through vanilla `Quest` hooks
/// (`OnItemOfferedToNpc`, `OnNpcSocialized`); the remaining step kinds get their handlers in
/// 7b/7c. The quest never patches the engine — every step uses an existing virtual.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_AdventureQuest")]
public sealed class AdventureQuest : Quest, IRewardedQuest
{
    public readonly NetStringList stepStates = new();
    public readonly NetStringList serializedRewards = new();
    public readonly NetString giverNpc = new();

    /// Spoken by the NPC who completes a `Talk` or `Deliver` step that closes out the quest
    /// (e.g. Evelyn's "thank you for checking on him" line for Check on George). Optional —
    /// when empty, the NPC continues their normal dialogue. Gift-step completions never push
    /// this message so vanilla's gift-reaction line gets the floor.
    public readonly NetString completionMessage = new();

    public NetStringList SerializedRewards => serializedRewards;

    /// Lazily-decoded mirror of `stepStates`. Re-decoded whenever the list count changes or
    /// `Persist` writes a new entry. Kept as a field so step handlers don't allocate a fresh
    /// decoded list per virtual call.
    private List<AdventureStepState>? _decoded;

    /// Per-step transient baseline for `ClearDebris`: the most recently observed
    /// `ResourceClump` count at the step's target location. Reset on save load (the
    /// next `PollResourceClumps` poll re-baselines from current world state). Keyed by
    /// step index. Non-netcoded, non-XML-serialised by virtue of being a private field.
    private Dictionary<int, int>? _clumpBaselines;

    /// True when any step of this quest opts into decor-shipping. Read by the framework's
    /// per-tick log diff to decide whether this quest contributes to the
    /// `Object.canBeShipped` patch's active-quest count. Cheap — the decoded list is
    /// already cached, so this is a single iteration over `Steps`.
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

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(stepStates, "stepStates")
            .AddField(serializedRewards, "serializedRewards")
            .AddField(giverNpc, "giverNpc")
            .AddField(completionMessage, "completionMessage");
    }

    /// Authoring entry point used by generators and the JSON path. Replaces any prior
    /// step list, stamps the giver, and primes the journal text for the first active step.
    /// `completionDialogue` is the line spoken by whoever closes out the quest's final
    /// Talk or Deliver step — pass empty/null when the quest has no scripted thank-you line.
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

    /// First not-Done step whose `Requires[]` are all Done. Returns -1 when every step is
    /// done (the quest will complete on the next state change) or when no step is currently
    /// reachable (shouldn't happen with well-formed Requires graphs).
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
        // Single-line summary for callers that read `currentObjective` directly (external
        // quest trackers, log lines, etc.) — pick the first active step. The vanilla journal
        // path goes through `GetObjectiveDescriptions`, which is patched by
        // `AdventureQuestPatches` to return one entry per active step (proper multi-bullet
        // rendering); without that patch the journal would still only show one bullet.
        var lines = BuildActiveObjectiveDescriptions();
        _currentObjective = lines.Count == 0 ? string.Empty : lines[0];
    }

    /// Active objectives = every not-Done step whose `Requires[]` are all Done. Returned in
    /// declaration order so authors can rely on a stable journal layout. Each line carries
    /// its own `(progress/count)` suffix when `Count > 1`, matching how vanilla
    /// `SlayMonsterQuest` renders "0/2 Rock Crab defeated".
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
        // Walk all currently-active steps (Requires met, not Done) instead of just the first.
        // Lets multi-step Adventure quests like "deliver Flour + Sugar + Eggs to Evelyn" accept
        // each item independently of declaration order — the player can hand them in any order.
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

    /// Called by the framework when the player's deepest reached mine/skull-cavern level
    /// might have advanced (DayStarted, or after warping out of a MineShaft). Each active
    /// `ReachLevel` step compares its target floor against the relevant counter:
    /// `Mine` → `min(120, deepestMineLevel)`; `SkullCavern` → `max(0, deepestMineLevel - 120)`.
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

    /// Called by the framework's DayEnding observer with the farm shipping bin contents.
    /// Each active `Ship` step counts matching items independently.
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

    /// Called by the framework when the player warps into a location. Each active `Visit`
    /// step compares its `Targets[0]` against the entered location name. A Visit step's
    /// `Items[]` is reserved for "with N following animals" gating tied to the Livestock
    /// Follows You follower API; that subfield is deferred until LFY exposes a follower
    /// query, so when `Items[]` is non-empty we log once at Trace and skip credit (the
    /// step stays open until the framework gains a follower-count adapter).
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

    /// Called by the framework at `DayStarted` with the set of building types newly added
    /// to the farm since the previous DayStarted snapshot. Each active `Build` step matches
    /// `Targets[0]` against the new types — first match closes the step.
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

    /// Called by the framework when one or more trees were added to a location's
    /// `terrainFeatures` (planted by the player or by the engine following a player
    /// action). Each active `Plant` step crediting its target location is incremented
    /// by `delta`.
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

    /// Called by the framework when one or more weed `Object`s were removed from a
    /// location's `objects` net collection. Each active `ClearWeeds` step crediting
    /// its target location is incremented by `delta`.
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

    /// Polls the supplied location's `resourceClumps` count once and credits each active
    /// `ClearDebris` step targeting that location with the drop since the previous poll.
    /// On the first poll for a step, the current count is recorded as the baseline (no
    /// credit yet). Cheap when no ClearDebris step is active — early-returns on the
    /// per-step kind check before touching `location.resourceClumps`.
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

            int current = location.resourceClumps?.Count ?? 0;
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

    /// Phase 9.5e Catch step filters. A step with empty / zero filters passes through
    /// unchanged. Set fields gate against the player's current location, the catch's
    /// reported size, and the runtime weather respectively. `Rain` matches both `Rain`
    /// and `Storm` so vanilla rain-only fish data stays consistent across thunderstorms.
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

    /// `*` wildcard matches any location; `!Name` excludes that location. A step passes when
    /// at least one positive target matches AND no `!`-prefixed exclusion matches. Lets a
    /// step say "anywhere except the Farm" via `Targets = ["*", "!Farm"]`.
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

    /// Evaluates the `$`-prefixed gate tokens on a Visit step's `Items[]`. Today the only
    /// recognised token is `$follower-count:N` (consults `FollowerApiBridge` for the
    /// LivestockFollowsYou follower count). Unknown tokens are treated as failed gates so
    /// a future-token authored by content doesn't auto-complete on saves where the gate
    /// isn't yet implemented.
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

    /// Vanilla `ItemDeliveryQuest`-style flow: match the target NPC + item, reduce inventory by
    /// the step's count, mark step done, advance the journal. Returning `true` short-circuits
    /// the gift path (the NPC won't accept the item as a normal gift). When this delivery
    /// completes the whole quest, `completionMessage` is pushed as the NPC's response —
    /// matching the vanilla `ItemDeliveryQuest.OnItemOfferedToNpc` flow that pushes
    /// `targetMessage` on completion.
    private bool TryAdvanceDeliver(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        if (!TargetMatches(step, npc.Name)) return false;
        if (!ItemMatches(step, item)) return false;
        if (step.MinQuality > 0 && (item is not StardewValley.Object obj || obj.Quality < step.MinQuality)) return false;
        if (item.Stack < step.Count) return false;

        if (probe) return true;

        Game1.player.Items.Reduce(item, step.Count);
        bool willComplete = WillStepCompleteQuest(idx);
        if (willComplete && !string.IsNullOrEmpty(completionMessage.Value))
        {
            npc.CurrentDialogue.Push(new Dialogue(npc, null, completionMessage.Value));
            Game1.drawDialogue(npc);
        }
        if (HasMultipleDeliverSteps())
            Game1.playSound("give_gift");
        MarkStepDone(idx, step);
        return true;
    }

    /// True when the quest's step list has at least two `Deliver` steps. Used to gate the
    /// per-step delivery sound cue: single-step Deliver quests get vanilla's quest-complete
    /// fanfare and don't need extra confirmation, but multi-ingredient asks benefit from a
    /// "that one landed" cue between handoffs so players know the step registered.
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

    /// Gift step: matches an offered item that the recipient loves, likes, OR Stardrop Tea
    /// (the special item that's always acceptable regardless of weekly gift cap or taste —
    /// vanilla `getGiftTasteForThisItem` returns `gift_taste_stardroptea` (7) for it). Returns
    /// false so the vanilla gift flow (friendship bump, daily-gift counter) still runs — the
    /// quest observes the gift, it does not consume it.
    private bool TryAdvanceGift(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        if (!TargetMatches(step, npc.Name)) return false;
        // Items list optional for Gift: empty list means "any thoughtful item to the target".
        if (step.Items.Count > 0 && !ItemMatches(step, item)) return false;

        int taste = npc.getGiftTasteForThisItem(item);
        bool acceptable = taste == NPC.gift_taste_love
                       || taste == NPC.gift_taste_like
                       || taste == NPC.gift_taste_stardroptea;
        if (!acceptable)
            return false;

        if (probe) return false; // silence the vanilla "this would advance a quest" cue

        MarkStepDone(idx, step);
        return false;
    }

    /// GiftUniqueNpcs step: counts unique NPC recipients across the run. Each gift must
    /// pass the `Items` filter (e.g. `$forage` for forage-category items) AND be loved or
    /// liked by that specific recipient. The same NPC never counts twice — `CreditedKeys`
    /// dedupes by name. Returns false so the vanilla gift flow (friendship bump, daily-gift
    /// counter) still runs; we observe, we don't consume.
    private bool TryAdvanceGiftUniqueNpcs(int idx, AdventureStepState step, NPC npc, Item item, bool probe)
    {
        // Recipient pool: when Targets is non-empty, treat it as a whitelist; otherwise
        // any villager qualifies. Skip non-villager NPCs (monsters, horses, pets) regardless.
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

        if (probe) return false; // observe only — don't consume the vanilla gift cue

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

    /// Catch step: increments progress when the caught fish matches one of the step's `Items`
    /// AND passes the optional `LocationName` / `MinSize` / `Weather` filters added in 9.5e.
    /// `Targets` stays unused so the existing item-id encoding keeps working without churn.
    /// Always returns false so vanilla's other quests still see the catch.
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

    /// Slay step: matches monsters whose name appears in `Targets`. Empty Targets means
    /// "any monster" (rarely useful but supported). Tame monsters never count, and bomb
    /// kills count by default — vanilla `SlayMonsterQuest` accepts both.
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

    /// Talk step: matches one of the step's target NPCs. When `Count > 1` we credit each NPC
    /// only once via `CreditedKeys` so "talk to 3 different villagers" cannot be satisfied
    /// by speaking to the same NPC three times.
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
            // If this Talk closes out the entire quest, push the configured thank-you line so
            // the NPC says it instead of falling through to their normal greeting / chat.
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

    /// True when every step other than `doneIdx` is already Done. Used by Talk/Deliver to
    /// decide whether to push `completionMessage` as the NPC's response.
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

        // Refresh the journal text for whichever step becomes active next, then complete the
        // quest if the whole step list is now Done.
        if (ActiveStepIndex() < 0)
            questComplete();
        else
            reloadObjective();
    }

    /// `$giver` resolves to the giver NPC's name; otherwise compare the literal target string.
    /// Empty Targets list means "any NPC" for the kind in question (used by the Deliver step
    /// when the giver is the target — the framework rewrites `$giver` to the giver name at
    /// `Initialize` time so we never need to dereference it here).
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
            // `$`-prefixed entries are tokens evaluated by category/property checks
            // rather than literal id comparison — `$edible-egg` covers vanilla + modded
            // eggs (Category -5, Edibility != -300), `$category:N` is a generic escape
            // hatch for any other vanilla object category.
            if (entry.StartsWith("$", StringComparison.Ordinal))
            {
                if (TokenMatches(entry, item)) return true;
                continue;
            }
            if (string.Equals(entry, qid, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(entry, id, StringComparison.OrdinalIgnoreCase)) return true;
            // Author-supplied ids may omit the `(O)` prefix; tolerate both forms.
            if (entry.StartsWith("(") && string.Equals(entry.Substring(entry.IndexOf(')') + 1), id, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// `$`-prefixed Items entries are treated as predicates rather than literal ids.
    /// Recognised tokens:
    /// - `$edible-egg`: any Object with Category == -5 and Edibility != -300. Catches
    ///   vanilla + modded eggs (Hootin' & Hollerin', SVE, VMV all use Category -5);
    ///   excludes Dinosaur Egg (Edibility -300, inedible per vanilla).
    /// - `$category:N`: any Object with the given vanilla category constant.
    /// - `$forage`: any Object whose `Data/Objects` row carries the `forage_item`
    ///   context tag. Convenience alias for `$tag:forage_item`.
    /// - `$tag:<contextTag>`: any item whose qualified id carries the given context tag
    ///   per `ItemContextTagManager.HasBaseTag`. Used for filters like `$tag:color_purple`
    ///   (purple-dye items) or `$tag:furniture_table` (any table furniture). Item ids
    ///   resolved this way can be Furniture / BigCraftable / Object alike — the matcher
    ///   doesn't constrain Item subtype, so authors can request decor items the way they
    ///   request crops.
    private static bool TokenMatches(string token, Item item)
    {
        const int eggCategory = -5;
        const int inedible = -300;
        if (string.Equals(token, "$edible-egg", StringComparison.OrdinalIgnoreCase))
            return item is StardewValley.Object eggObj && eggObj.Category == eggCategory && eggObj.Edibility != inedible;
        if (string.Equals(token, "$forage", StringComparison.OrdinalIgnoreCase))
            return HasContextTag(item, "forage_item");
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
            // ItemContextTagManager.HasBaseTag covers both the authored tags from
            // Data/Objects and any runtime tags injected by Stardew (e.g. preserve_sheet_*),
            // which is exactly the source of truth `Data/Shops` and other vanilla
            // systems use for tag-based filtering.
            return StardewValley.ItemContextTagManager.HasBaseTag(item.QualifiedItemId, tag);
        }
        catch
        {
            return false;
        }
    }
}
