using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
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

    // Timed-delivery support, opt-in via ConfigureTimedDelivery. All defaults are off
    // so every existing quest (and old save) behaves exactly as before. The named step
    // becomes "timed": when it activates, the giver hands the player the package item,
    // a limit in in-game minutes is rolled in [timerMinMinutes, timerMaxMinutes], and
    // timerDeadline gets the absolute HHMM time the countdown runs out. deadline > 0
    // means the clock is running. timerFriendshipStakes is the friendship the target
    // NPC gains on success and BOTH giver and target lose on failure.
    public readonly NetString timedStepName = new();
    public readonly NetString timedPackageItemId = new();
    public readonly NetString timedHandoffDialogue = new();
    public readonly NetString timedDeliverDescription = new();
    public readonly NetInt timerMinMinutes = new();
    public readonly NetInt timerMaxMinutes = new();
    public readonly NetInt timerLatestStart = new();
    public readonly NetInt timerLimitMinutes = new();
    public readonly NetInt timerDeadline = new();
    public readonly NetInt timerFriendshipStakes = new();

    // Set when the timer failed this session so the framework's removal diff reports
    // Expired instead of Cancelled. Transient by design (a failed quest never saves).
    internal bool TimedFailed;

    // Refusal-dialogue throttle: the giver explains why they won't hand the package
    // over ONCE per day (or when the reason changes), then goes back to their normal
    // dialogue so the quest doesn't hold their conversation hostage. Transient.
    private int _timedRefusalDay = -1;
    private TimedStartRefusal _timedRefusalReason = TimedStartRefusal.None;

    // [XmlIgnore] prevents XmlSerializer from emitting a second <SerializedRewards>
    // element that aliases the same NetStringList as <serializedRewards>. Without it,
    // every save/load round-trip doubles serializedRewards: XmlSerializer would write
    // both members and on load Add into the same backing list for each one. See
    // RewardApplier.ApplyEncoded for the dedupe that used to mitigate the symptom.
    [System.Xml.Serialization.XmlIgnore]
    public NetStringList SerializedRewards => serializedRewards;

    private List<AdventureStepState>? _decoded;

    // Per-(step, location). A shared baseline across locations would credit progress on
    // every warp into a map with fewer clumps than the previous one.
    private Dictionary<(int StepIndex, string Location), int>? _clumpBaselines;

    // Decorate high-water baselines, per (step, "category@location"). Transient and seeded
    // on the first poll of a session, so furniture already in the room when the quest starts
    // (or already credited before a reload) never counts. Only ever moves up, so moving a
    // piece around can't credit. Only genuinely new placements do.
    [XmlIgnore] private Dictionary<(int StepIndex, string Key), int>? _decorBaselines;

    // Craft step state, per step index. _craftBaselines is the total craft count of matching
    // recipes seen on first poll (transient, same idea as the decorate high-water mark, so
    // crafts before the step went active never count). _craftMatch caches which recipe names
    // produce a matching item, rebuilt only when the player's known-recipe count changes.
    [XmlIgnore] private Dictionary<int, int>? _craftBaselines;
    [XmlIgnore] private Dictionary<int, (int Known, HashSet<string> Recipes)>? _craftMatch;

    // Set while a mailbox turn-in (Mail Services Mod shim) runs with the NPC dialogue
    // disabled, since the NPC isn't actually there. Transient by design.
    [XmlIgnore] private bool _suppressTurnInDialogue;

    // Transient cache for a deferred (mine) DropItemsInRadius zone, keyed by the live floor
    // instance so a new visit (new instance) re-picks. Not netfields, so it never syncs or saves.
    [XmlIgnore] private GameLocation? _zoneLoc;
    [XmlIgnore] private int _zoneStepIndex = -1;
    [XmlIgnore] private Point _zoneCenter;
    [XmlIgnore] private bool _zoneTried;

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
            .AddField(completionMessage, "completionMessage")
            .AddField(timedStepName, "timedStepName")
            .AddField(timedPackageItemId, "timedPackageItemId")
            .AddField(timedHandoffDialogue, "timedHandoffDialogue")
            .AddField(timedDeliverDescription, "timedDeliverDescription")
            .AddField(timerMinMinutes, "timerMinMinutes")
            .AddField(timerMaxMinutes, "timerMaxMinutes")
            .AddField(timerLatestStart, "timerLatestStart")
            .AddField(timerLimitMinutes, "timerLimitMinutes")
            .AddField(timerDeadline, "timerDeadline")
            .AddField(timerFriendshipStakes, "timerFriendshipStakes");
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

    // Marks one step as a timed package delivery. The step should be a Deliver step
    // gated behind a Talk step (the talk is the handoff). deliverDescription is the
    // post-handoff objective text; "[target]" in it and in handoffDialogue is replaced
    // with the rolled NPC's display name, "[time]" with the rolled limit.
    public void ConfigureTimedDelivery(
        string stepName,
        string packageItemId,
        int minMinutes,
        int maxMinutes,
        int latestStartTime,
        int friendshipStakes,
        string handoffDialogue,
        string deliverDescription)
    {
        timedStepName.Value = stepName ?? string.Empty;
        timedPackageItemId.Value = packageItemId ?? string.Empty;
        timerMinMinutes.Value = Math.Max(10, minMinutes);
        timerMaxMinutes.Value = Math.Max(timerMinMinutes.Value, maxMinutes);
        timerLatestStart.Value = latestStartTime;
        timerFriendshipStakes.Value = Math.Max(0, friendshipStakes);
        timedHandoffDialogue.Value = handoffDialogue ?? string.Empty;
        timedDeliverDescription.Value = deliverDescription ?? string.Empty;
    }

    public bool HasTimedStep => !string.IsNullOrEmpty(timedStepName.Value);

    public bool TimerActive => timerDeadline.Value > 0 && !completed.Value;

    private int TimedStepIndex()
    {
        if (!HasTimedStep)
            return -1;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i].Name, timedStepName.Value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private string TimedTargetName()
    {
        int idx = TimedStepIndex();
        if (idx < 0)
            return string.Empty;
        var step = Steps[idx];
        return step.Targets.Count > 0 ? step.Targets[0] : string.Empty;
    }

    // True when finishing the given step would make the timed step active (every
    // other Requires entry is already done).
    private bool WouldActivateTimedStep(int completedIdx)
    {
        int timedIdx = TimedStepIndex();
        if (timedIdx < 0)
            return false;
        var steps = Steps;
        var timed = steps[timedIdx];
        if (timed.Done)
            return false;
        foreach (var req in timed.Requires)
        {
            if (string.Equals(req, steps[completedIdx].Name, StringComparison.OrdinalIgnoreCase))
                continue;
            bool found = false;
            foreach (var s in steps)
            {
                if (string.Equals(s.Name, req, StringComparison.OrdinalIgnoreCase))
                {
                    if (!s.Done)
                        return false;
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    // The handoff. Rolls the timer and the target, hands the package over, and starts
    // the countdown. Returns false when a gate says no (festival day, too late, full
    // bags, nobody reachable); the talk step stays uncredited so the player can try
    // again another time. refusalSpoken reports whether the giver actually said the
    // refusal line this time; it's throttled to once per day per reason so repeat
    // chats (and gifting) get the NPC's normal dialogue instead.
    private bool TryBeginTimedDelivery(NPC giver, out bool refusalSpoken)
    {
        refusalSpoken = false;
        string packageId = timedPackageItemId.Value ?? string.Empty;
        Item? package = packageId.Length == 0
            ? null
            : ItemRegistry.Create(packageId, 1, 0, allowNull: true);
        bool hasRoom = package != null && Game1.player.couldInventoryAcceptThisItem(package);

        string giverLocation = giver?.currentLocation?.Name
            ?? Game1.currentLocation?.Name
            ?? string.Empty;
        string? target = TimedDeliveryTargets.PickTarget(giverNpc.Value ?? string.Empty, giverLocation);

        var refusal = TimedQuestClock.CheckHandoffGate(
            Game1.timeOfDay,
            timerMaxMinutes.Value,
            timerLatestStart.Value,
            Utility.isFestivalDay(),
            hasRoom,
            target != null);
        if (refusal != TimedStartRefusal.None)
        {
            int today = Game1.Date.TotalDays;
            if (_timedRefusalDay != today || _timedRefusalReason != refusal)
            {
                _timedRefusalDay = today;
                _timedRefusalReason = refusal;
                PushTimedDialogue(giver, RefusalText(refusal));
                refusalSpoken = true;
            }
            return false;
        }

        int limit = TimedQuestClock.RollLimitMinutes(timerMinMinutes.Value, timerMaxMinutes.Value, Game1.random.Next);
        timerLimitMinutes.Value = limit;
        timerDeadline.Value = TimedQuestClock.AddMinutes(Game1.timeOfDay, limit);

        string targetDisplay = NpcDisplay.Resolve(target!);
        int timedIdx = TimedStepIndex();
        var timedStep = Steps[timedIdx];
        timedStep.Targets = new List<string> { target! };
        if (!string.IsNullOrEmpty(timedDeliverDescription.Value))
            timedStep.Description = timedDeliverDescription.Value.Replace("[target]", targetDisplay);
        Persist(timedIdx, timedStep);

        if (package is StardewValley.Object packageObj)
            packageObj.questItem.Value = true;
        if (!Game1.player.addItemToInventoryBool(package))
            Game1.createItemDebris(package, Game1.player.getStandingPosition(), 2);
        Game1.playSound("coin");

        // The target's completion reward can't be known at posting time, so it joins
        // the encoded list now.
        if (timerFriendshipStakes.Value > 0)
            serializedRewards.Add(RewardCodec.Encode(new FriendshipReward(target!, timerFriendshipStakes.Value)));

        string dialogue = timedHandoffDialogue.Value ?? string.Empty;
        if (dialogue.Length > 0)
        {
            dialogue = dialogue
                .Replace("[target]", targetDisplay)
                .Replace("[time]", FormatMinutes(limit));
            PushTimedDialogue(giver, dialogue);
        }

        ModEntry.LogDebug(() => $"Timed delivery started on '{questTitle}': target {target}, "
            + $"limit {limit} min, deadline {timerDeadline.Value}.");
        reloadObjective();
        return true;
    }

    // Called once a second for the local player. True when the timer ran out and the
    // quest failed (and removed itself from the log).
    internal bool CheckTimedExpiry()
    {
        if (!TimerActive)
            return false;
        if (Game1.timeOfDay < timerDeadline.Value)
            return false;
        FailTimedDelivery(expired: true, removeFromLog: true);
        return true;
    }

    // Shared failure path for expiry, day-end with the package in hand, and cancelling
    // after the handoff. Pulls the package back, docks friendship with both NPCs, and
    // tells the player what just happened.
    internal void FailTimedDelivery(bool expired, bool removeFromLog)
    {
        if (completed.Value || timerDeadline.Value <= 0)
            return;
        TimedFailed = true;
        timerDeadline.Value = 0;
        RemovePackageFromInventory();

        string giver = giverNpc.Value ?? string.Empty;
        string target = TimedTargetName();
        int stakes = timerFriendshipStakes.Value;
        if (stakes > 0)
        {
            var penalties = new List<Rewards.RewardSpec>(2);
            if (!string.IsNullOrEmpty(giver))
                penalties.Add(new FriendshipReward(giver, -stakes));
            if (!string.IsNullOrEmpty(target))
                penalties.Add(new FriendshipReward(target, -stakes));
            RewardApplier.Apply(penalties);
        }

        string key = expired ? "timedQuest.failed.expired" : "timedQuest.failed.cancelled";
        string text = ModEntry.Translation
            ?.Get(key, new { giver = NpcDisplay.Resolve(giver), target = NpcDisplay.Resolve(target) })
            .ToString()
            ?? "The delivery fell through.";
        Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));
        Game1.playSound("cancel");
        ModEntry.LogDebug(() => $"Timed delivery failed on '{questTitle}' ({(expired ? "expired" : "cancelled")}): "
            + $"giver {giver}, target {target}, stakes {stakes}.");

        if (removeFromLog)
            Game1.player.questLog.Remove(this);
    }

    private void RemovePackageFromInventory()
    {
        string id = timedPackageItemId.Value ?? string.Empty;
        if (id.Length == 0)
            return;
        string qualified = ItemRegistry.QualifyItemId(id) ?? id;
        var items = Game1.player.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && string.Equals(item.QualifiedItemId, qualified, StringComparison.OrdinalIgnoreCase))
                items[i] = null;
        }
    }

    private static void PushTimedDialogue(NPC? npc, string text)
    {
        if (npc == null || string.IsNullOrEmpty(text))
            return;
        npc.CurrentDialogue.Push(new Dialogue(npc, null, text));
        Game1.drawDialogue(npc);
    }

    private static string RefusalText(TimedStartRefusal refusal)
    {
        string key = refusal switch
        {
            TimedStartRefusal.Festival => "timedQuest.refuse.festival",
            TimedStartRefusal.TooLate => "timedQuest.refuse.tooLate",
            TimedStartRefusal.InventoryFull => "timedQuest.refuse.inventoryFull",
            _ => "timedQuest.refuse.noTarget"
        };
        return ModEntry.Translation?.Get(key).ToString() ?? "Come back another time.";
    }

    internal static string FormatMinutes(int minutes)
    {
        minutes = Math.Max(0, minutes);
        int h = minutes / 60;
        int m = minutes % 60;
        var t = ModEntry.Translation;
        if (h > 0 && m > 0)
            return t?.Get("timedQuest.time.hoursMinutes", new { hours = h, minutes = m }).Default($"{h}h {m}m").ToString() ?? $"{h}h {m}m";
        if (h > 0)
            return t?.Get("timedQuest.time.hours", new { hours = h }).Default($"{h}h").ToString() ?? $"{h}h";
        return t?.Get("timedQuest.time.minutes", new { minutes = m }).Default($"{m}m").ToString() ?? $"{m}m";
    }

    // Appends the live countdown to the timed step's objective line. Everything that
    // shows objectives (vanilla journal, QuestJournal HUD and detail page) rebuilds
    // its text through here, so the countdown shows up everywhere without those UIs
    // knowing timers exist. The minute interpolation makes the HUD (which re-reads
    // every frame) tick down smoothly between clock jumps.
    private string AppendTimerSuffix(int stepIndex, string line)
    {
        if (!TimerActive || stepIndex != TimedStepIndex())
            return line;
        int remaining = TimedQuestClock.RemainingMinutes(
            Game1.timeOfDay,
            timerDeadline.Value,
            Game1.gameTimeInterval,
            Game1.realMilliSecondsPerGameTenMinutes);
        string time = FormatMinutes(remaining);
        string suffix = ModEntry.Translation
            ?.Get("timedQuest.timeLeft", new { time }).Default($"{time} left").ToString()
            ?? $"{time} left";
        return $"{line} ({suffix})";
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

    // Resolves the active DropItemsInRadius zone for the given location, if any. Overground
    // zones return the center baked into the step on accept. Mine zones (center left at 0,0)
    // are resolved lazily against the live floor via DropZonePicker and cached per visit.
    // Returns false when there's no matching active step, or the lazy pick found no clear tile.
    // Read-only: it never writes to the location, only queries tiles to choose a center.
    public bool TryGetDropZone(GameLocation? location, out Point center, out int radius)
    {
        center = Point.Zero;
        radius = 0;
        if (completed.Value || location == null)
            return false;
        string locName = location.Name ?? string.Empty;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.DropItemsInRadius) continue;
            if (!LocationMatches(step, locName)) continue;

            radius = step.Radius;
            if (step.CenterX != 0 || step.CenterY != 0)
            {
                center = new Point(step.CenterX, step.CenterY);
                return true;
            }
            if (_zoneTried && ReferenceEquals(_zoneLoc, location) && _zoneStepIndex == i)
            {
                center = _zoneCenter;
                return _zoneCenter != Point.Zero;
            }
            _zoneLoc = location;
            _zoneStepIndex = i;
            _zoneTried = true;
            _zoneCenter = DropZonePicker.TryPickZone(location, Math.Max(1, radius), minDistFromWarp: 3, attempts: 200, out var picked)
                ? picked
                : Point.Zero;
            center = _zoneCenter;
            return _zoneCenter != Point.Zero;
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

    // Read-only step snapshot for the API surface. Returns a fresh list of
    // shallow projections so callers can't mutate internal state.
    internal List<Api.AdventureStepInfo> BuildStepInfos()
    {
        var steps = Steps;
        var infos = new List<Api.AdventureStepInfo>(steps.Count);
        int active = ActiveStepIndex();
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            infos.Add(new Api.AdventureStepInfo(
                s.Name,
                s.Kind.ToString(),
                s.Progress,
                s.Count,
                s.Done,
                i == active,
                s.Done ? s.Description : AppendTimerSuffix(i, s.Description),
                new List<string>(s.Requires),
                new List<string>(s.Targets),
                new List<string>(s.Items),
                s.LocationName,
                s.Weather,
                s.MinSize,
                s.MinQuality));
        }
        return infos;
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
            lines.Add(AppendTimerSuffix(i, line));
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
        // in any order (e.g. "deliver Flour + Sugar + Eggs to Evelyn"). The first matching
        // active step consumes the offered stack and returns; if two steps accept the same
        // item id (e.g. deliver 3 hardwood to Robin AND 3 hardwood to Lewis), the player
        // needs to talk to each recipient separately to advance each step.
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

    // Finds the NPC an active deliver step would accept this item for, so the Mail
    // Services shim can route a mailbox hand-in without an in-person interaction.
    internal bool TryResolveMailDeliveryTarget(Item item, out NPC? npc)
    {
        npc = null;
        if (completed.Value || item == null)
            return false;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Deliver) continue;
            foreach (string name in CandidateTargets(step))
            {
                NPC? candidate = Game1.getCharacterFromName(name);
                if (candidate == null)
                    continue;
                if (TryAdvanceDeliver(i, step, candidate, item, probe: true))
                {
                    npc = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    internal bool TryAcceptByMail(NPC npc, Item item, bool showNpcDialogue)
    {
        _suppressTurnInDialogue = !showNpcDialogue;
        try
        {
            return OnItemOfferedToNpc(npc, item, probe: false);
        }
        finally
        {
            _suppressTurnInDialogue = false;
        }
    }

    private IEnumerable<string> CandidateTargets(AdventureStepState step)
    {
        if (step.Targets.Count == 0)
        {
            if (!string.IsNullOrEmpty(giverNpc.Value))
                yield return giverNpc.Value;
            yield break;
        }
        foreach (var t in step.Targets)
            yield return t;
    }

    // Called from the receiveGift postfix so gifts that skip the in-person quest hook
    // (Mail Services Mod's gift service) still tick gift-kind steps. Deliver steps are
    // skipped on purpose: they consume items, and the gift already consumed one. Safe
    // to run alongside the in-person path too, gift steps check Done and CreditedKeys,
    // so nothing counts twice.
    internal void ObserveGiftGiven(NPC npc, Item item)
    {
        if (completed.Value || npc == null || item == null)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (completed.Value)
                return;
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            bool wasDone = step.Done;
            int wasProgress = step.Progress;
            switch (step.Kind)
            {
                case AdventureStepKind.Gift:
                    TryAdvanceGift(i, step, npc, item, probe: false);
                    break;
                case AdventureStepKind.GiftUniqueNpcs:
                    TryAdvanceGiftUniqueNpcs(i, step, npc, item, probe: false);
                    break;
                default:
                    continue;
            }
            if (step.Done != wasDone || step.Progress != wasProgress)
                ModEntry.LogDebug(() => $"Gift step advanced on '{questTitle}': {item.QualifiedItemId} to {npc.Name}, "
                    + $"progress {step.Progress}/{step.Count}, step done: {step.Done}, quest completed: {completed.Value}.");
        }
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
            TryAdvanceSlay(i, step, location, monster, probe);
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
            _clumpBaselines ??= new Dictionary<(int, string), int>();
            var key = (i, location.Name);
            if (!_clumpBaselines.TryGetValue(key, out int baseline))
            {
                _clumpBaselines[key] = current;
                continue;
            }
            if (current < baseline)
            {
                int delta = baseline - current;
                _clumpBaselines[key] = current;
                CreditCount(i, step, delta);
            }
            else if (current > baseline)
            {
                _clumpBaselines[key] = current;
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

    // Lookup seam for ICustomStepHandle. Walks every Custom step that's currently
    // eligible to advance (not Done, Requires met, parent not completed) and yields
    // the index plus the step's name and Targets[0] (handler id).
    public IEnumerable<(int Index, string StepName, string HandlerName)> ActiveCustomStepInfos()
    {
        if (completed.Value)
            yield break;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done) continue;
            if (step.Kind != AdventureStepKind.Custom) continue;
            if (!RequiresMet(steps, step)) continue;
            string handler = step.Targets.Count > 0 ? step.Targets[0] : string.Empty;
            yield return (i, step.Name, handler);
        }
    }

    // Snapshot read for handle property getters. Returns null for an out-of-range
    // index or a non-Custom step. Callers must treat the result as read-only.
    public AdventureStepState? PeekCustomStep(int index)
    {
        var steps = Steps;
        if (index < 0 || index >= steps.Count) return null;
        var step = steps[index];
        return step.Kind == AdventureStepKind.Custom ? step : null;
    }

    public bool IsCustomStepActive(int index)
    {
        if (completed.Value) return false;
        var steps = Steps;
        if (index < 0 || index >= steps.Count) return false;
        var step = steps[index];
        if (step.Done || step.Kind != AdventureStepKind.Custom) return false;
        return RequiresMet(steps, step);
    }

    // Push-based credit for event-driven Custom steps. Returns the number of credits
    // actually applied (0 when the step isn't active, isn't a Custom step, or is
    // already full). Drives MarkStepDone + questComplete when Progress catches up.
    public int TryAddCustomStepProgress(int index, int delta)
    {
        if (delta <= 0 || completed.Value) return 0;
        var steps = Steps;
        if (index < 0 || index >= steps.Count) return 0;
        var step = steps[index];
        if (step.Done || step.Kind != AdventureStepKind.Custom) return 0;
        if (!RequiresMet(steps, step)) return 0;
        int needed = Math.Max(1, step.Count);
        int credit = Math.Min(delta, needed - step.Progress);
        if (credit <= 0) return 0;
        step.Progress += credit;
        if (step.Progress >= needed)
            MarkStepDone(index, step);
        else
        {
            Persist(index, step);
            reloadObjective();
        }
        return credit;
    }

    // Like TryAddCustomStepProgress, but also dedupes by a caller-supplied key (e.g. a
    // tile coordinate or unique entity id) using the step's CreditedKeys list. Returns 0
    // when the key has already been credited so the same event can't double-count. The
    // key is persisted in the AdventureStepState's CreditedKeys (the same NetStringList
    // Talk/GiftUniqueNpcs steps already use), so dedupe survives save/reload.
    public int TryAddCustomStepProgressOnceForKey(int index, string key, int delta = 1)
    {
        if (string.IsNullOrEmpty(key) || delta <= 0 || completed.Value) return 0;
        var steps = Steps;
        if (index < 0 || index >= steps.Count) return 0;
        var step = steps[index];
        if (step.Done || step.Kind != AdventureStepKind.Custom) return 0;
        if (!RequiresMet(steps, step)) return 0;
        if (step.CreditedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return 0;
        int needed = Math.Max(1, step.Count);
        int credit = Math.Min(delta, needed - step.Progress);
        if (credit <= 0) return 0;
        step.CreditedKeys.Add(key);
        step.Progress += credit;
        if (step.Progress >= needed)
            MarkStepDone(index, step);
        else
        {
            Persist(index, step);
            reloadObjective();
        }
        return credit;
    }

    public bool TryMarkCustomStepDone(int index)
    {
        if (completed.Value) return false;
        var steps = Steps;
        if (index < 0 || index >= steps.Count) return false;
        var step = steps[index];
        if (step.Done || step.Kind != AdventureStepKind.Custom) return false;
        if (!RequiresMet(steps, step)) return false;
        step.Progress = Math.Max(step.Progress, step.Count);
        MarkStepDone(index, step);
        return true;
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

    public void ObserveArtifactSpotDug(string locationName, int delta)
    {
        if (completed.Value || delta <= 0 || string.IsNullOrEmpty(locationName))
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.DigArtifactSpot) continue;
            if (!LocationMatches(step, locationName)) continue;
            CreditCount(i, step, delta);
        }
    }

    // Fired by the framework when the player closes the museum donation menu with a
    // net-positive change in museum piece count. Museum is one fixed location, so no
    // Targets filter is needed; any DonateMuseum step on this quest credits.
    public void ObserveMuseumDonation(int delta)
    {
        if (completed.Value || delta <= 0)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.DonateMuseum) continue;
            CreditCount(i, step, delta);
        }
    }

    // Polled each second on the player's current location. Credits furniture the player
    // places while the step is active, and only that: the baseline is a high-water mark of
    // the matching-furniture count, seeded on the first poll of a session (so what's already
    // in the room never counts) and never lowered. Picking up furniture to move it drops the
    // live count below the high-water mark, so putting it back can't credit, which keeps a
    // rearrange from advancing the quest. The step's category (Items[0]) selects which
    // furniture counts: rug, light, wall, other (none of those three), or any.
    public void ObserveDecorate(GameLocation? location)
    {
        if (completed.Value || location == null)
            return;
        string loc = location.Name ?? string.Empty;
        if (loc.Length == 0)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Decorate) continue;
            if (!LocationMatches(step, loc)) continue;

            string category = step.Items.Count > 0
                ? (NormalizeDecorCategory(step.Items[0]) ?? "any")
                : "any";

            int matching = 0;
            var furniture = location.furniture;
            if (furniture != null)
            {
                foreach (var f in furniture)
                {
                    if (f == null) continue;
                    if (MatchesDecorCategory(f, category)) matching++;
                }
            }

            _decorBaselines ??= new Dictionary<(int, string), int>();
            var key = (i, category + "@" + loc);
            if (!_decorBaselines.TryGetValue(key, out int baseline))
            {
                _decorBaselines[key] = matching;
                continue;
            }
            if (matching <= baseline)
                continue;

            int delta = matching - baseline;
            _decorBaselines[key] = matching;
            int needed = Math.Max(1, step.Count);
            int credit = Math.Min(delta, needed - step.Progress);
            if (credit <= 0)
                continue;
            step.Progress += credit;
            if (step.Progress >= needed)
                MarkStepDone(i, step);
            else
            {
                Persist(i, step);
                reloadObjective();
            }
        }
    }

    // Credits Craft steps by watching the player's per-recipe craft counter
    // (player.craftingRecipes), which both vanilla crafting and Better Crafting bump, so it
    // doesn't matter which crafting menu is used and a spawned-in item never counts. Items
    // lists what counts: item ids (qualified or bare) and/or "tag:<contextTag>" tokens matched
    // against each recipe's output. An empty Items list counts any craft. Polled once a second.
    public void ObserveCraft()
    {
        if (completed.Value || Game1.player?.craftingRecipes == null)
            return;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.Craft) continue;

            int matched = CountMatchingCrafts(i, step);
            _craftBaselines ??= new Dictionary<int, int>();
            if (!_craftBaselines.TryGetValue(i, out int baseline))
            {
                _craftBaselines[i] = matched;
                continue;
            }
            if (matched <= baseline)
                continue;
            int delta = matched - baseline;
            _craftBaselines[i] = matched;
            CreditCount(i, step, delta);
        }
    }

    private int CountMatchingCrafts(int idx, AdventureStepState step)
    {
        var recipes = Game1.player.craftingRecipes;
        _craftMatch ??= new Dictionary<int, (int, HashSet<string>)>();
        int known = recipes.Keys.Count();
        if (!_craftMatch.TryGetValue(idx, out var cached) || cached.Known != known)
        {
            cached = (known, BuildMatchingRecipeSet(step));
            _craftMatch[idx] = cached;
        }
        int total = 0;
        foreach (var name in cached.Recipes)
            if (recipes.ContainsKey(name))
                total += recipes[name];
        return total;
    }

    private static HashSet<string> BuildMatchingRecipeSet(AdventureStepState step)
    {
        var recipes = Game1.player.craftingRecipes;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();
        foreach (var raw in step.Items)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string t = raw.Trim();
            if (t.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                tags.Add(t.Substring(4).Trim());
            else
                ids.Add(StripItemQualifier(t));
        }
        bool matchAny = ids.Count == 0 && tags.Count == 0;

        var set = new HashSet<string>();
        foreach (var name in recipes.Keys)
        {
            if (matchAny) { set.Add(name); continue; }
            Item? output;
            try { output = new CraftingRecipe(name).createItem(); }
            catch { continue; }
            if (output == null) continue;
            if (ids.Contains(output.QualifiedItemId) || ids.Contains(StripItemQualifier(output.ItemId)))
            {
                set.Add(name);
                continue;
            }
            foreach (var tag in tags)
            {
                if (output.HasContextTag(tag)) { set.Add(name); break; }
            }
        }
        return set;
    }

    private static string StripItemQualifier(string id) =>
        id.StartsWith("(") ? id.Substring(id.IndexOf(')') + 1) : id;

    private static string? NormalizeDecorCategory(string raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "rug" => "rug",
            "light" or "lamp" or "lighting" => "light",
            "wall" or "walldecor" or "wall-decoration" => "wall",
            "other" => "other",
            "any" or "" => "any",
            _ => null
        };

    private static bool MatchesDecorCategory(Furniture f, string category) => category switch
    {
        "rug" => IsDecorRug(f),
        "light" => IsDecorLight(f),
        "wall" => IsDecorWall(f),
        "other" => !IsDecorRug(f) && !IsDecorLight(f) && !IsDecorWall(f),
        _ => true
    };

    private static bool IsDecorRug(Furniture f) => f.furniture_type.Value == Furniture.rug;

    private static bool IsDecorLight(Furniture f)
    {
        int t = f.furniture_type.Value;
        return t == Furniture.lamp || t == Furniture.fireplace
            || t == Furniture.sconce || t == Furniture.torch;
    }

    private static bool IsDecorWall(Furniture f) => !f.isGroundFurniture();

    /// DropItemsPatches calls this when a player-dropped Debris hits the ground. Drops
    /// of a stack create one Debris carrying the whole stack, so the listener passes
    /// `available = item.Stack` and gets back how many were actually consumed. The
    /// listener then either removes the Debris (consumed == available) or reduces its
    /// stack by the consumed amount (player wanted to drop more than the step needed,
    /// so the leftover stays on the ground).
    public int TryConsumeDroppedItem(string locationName, Item item, int available)
    {
        if (completed.Value || item == null || string.IsNullOrEmpty(locationName) || available <= 0)
            return 0;
        var steps = Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Done || !RequiresMet(steps, step)) continue;
            if (step.Kind != AdventureStepKind.DropItems && step.Kind != AdventureStepKind.DropItemsInRadius) continue;
            if (!LocationMatches(step, locationName)) continue;
            if (!ItemMatches(step, item)) continue;
            // Ritual-circle gate: the drop only counts inside the zone. Radius 0 = no gate.
            if (step.Kind == AdventureStepKind.DropItemsInRadius && step.Radius > 0)
            {
                if (!TryGetDropZone(Game1.currentLocation, out var c, out _))
                    continue;
                var tile = Game1.player.Tile;
                int dx = (int)tile.X - c.X;
                int dy = (int)tile.Y - c.Y;
                if (dx * dx + dy * dy > step.Radius * step.Radius)
                    continue;
            }
            int needed = Math.Max(1, step.Count);
            int remaining = needed - step.Progress;
            if (remaining <= 0) continue;
            int take = Math.Min(available, remaining);
            CreditCount(i, step, take);
            return take;
        }
        return 0;
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
            bool willComplete = MarkStepDoneInternal(idx, step);
            if (willComplete && !string.IsNullOrEmpty(completionMessage.Value) && !_suppressTurnInDialogue)
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, completionMessage.Value));
                Game1.drawDialogue(npc);
            }
            if (HasMultipleDeliverSteps())
                Game1.playSound("give_gift");
            if (willComplete)
                questComplete();
            else
                reloadObjective();
        }
        else
        {
            Game1.playSound("give_gift");
            Persist(idx, step);
            reloadObjective();
            string partial = TryGetPartialDialogue(step.Count - step.Progress);
            if (!string.IsNullOrEmpty(partial))
            {
                if (_suppressTurnInDialogue)
                {
                    Game1.drawObjectDialogue(partial);
                }
                else
                {
                    npc.CurrentDialogue.Push(new Dialogue(npc, null, partial));
                    Game1.drawDialogue(npc);
                }
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

    // Empty Targets = any monster. Tame monsters never count; bomb kills do. A MinFloor/MaxFloor
    // gate, when set, only counts kills made on a mine floor in that absolute mineLevel range
    // (Mines 1-120, Skull Cavern 121+).
    private bool TryAdvanceSlay(int idx, AdventureStepState step, GameLocation location, Monster monster, bool probe)
    {
        if (step.MinFloor > 0 || step.MaxFloor > 0)
        {
            if (location is not MineShaft shaft) return false;
            int floor = shaft.mineLevel;
            if (floor < step.MinFloor || floor > step.MaxFloor) return false;
        }

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

        // Timed-delivery handoff: when this talk would put the timed step in play,
        // the package changes hands right here (or the giver refuses and the step
        // stays uncredited so the player can come back).
        if (timerDeadline.Value == 0
            && step.Progress + 1 >= step.Count
            && WouldActivateTimedStep(idx))
        {
            // Vanilla fires OnNpcSocialized before the gift path in NPC.checkAction,
            // so a click while holding an item reaches here first. That's a gift
            // attempt, not a chat: stay silent and unhandled so the gift (or normal
            // dialogue) proceeds, and don't start the handoff mid-gift either.
            if (Game1.player.ActiveObject != null)
                return false;
            if (!TryBeginTimedDelivery(npc, out bool refusalSpoken))
            {
                // Spoken refusal consumes the interaction; a silent one (already
                // explained today) falls through to the giver's normal dialogue.
                return refusalSpoken;
            }
        }

        step.CreditedKeys.Add(npc.Name);
        step.Progress++;
        if (step.Progress >= step.Count)
        {
            bool willComplete = MarkStepDoneInternal(idx, step);
            if (willComplete && !string.IsNullOrEmpty(completionMessage.Value))
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, completionMessage.Value));
                Game1.drawDialogue(npc);
            }
            if (willComplete)
                questComplete();
            else
                reloadObjective();
        }
        else
        {
            Persist(idx, step);
            reloadObjective();
        }
        return true;
    }

    // Sets the step's Done flag, persists it, and reports whether the quest is now
    // complete. Callers that need to inject completion-time dialogue before the
    // questComplete() UI fires should use this directly; everyone else can stay on
    // the convenience wrapper below.
    private bool MarkStepDoneInternal(int idx, AdventureStepState step)
    {
        step.Done = true;
        step.Progress = Math.Max(step.Progress, step.Count);
        Persist(idx, step);
        return ActiveStepIndex() < 0;
    }

    private void MarkStepDone(int idx, AdventureStepState step)
    {
        if (MarkStepDoneInternal(idx, step))
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
    // $tag:<tag>, $edibility:<min>:<max>. $edible-egg excludes Dinosaur Egg (Edibility -300).
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
        // Matches any object with Edibility in [min, max] inclusive (-300 is the inedible sentinel).
        if (token.StartsWith("$edibility:", StringComparison.OrdinalIgnoreCase))
        {
            if (item is not StardewValley.Object edObj)
                return false;
            var bounds = token.Substring("$edibility:".Length).Split(':');
            if (bounds.Length != 2
                || !int.TryParse(bounds[0], out int lo)
                || !int.TryParse(bounds[1], out int hi))
                return false;
            int e = edObj.Edibility;
            return e >= lo && e <= hi;
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
