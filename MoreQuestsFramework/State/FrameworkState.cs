using System.Collections.Generic;
using MoreQuestsFramework.Consequences;

namespace MoreQuestsFramework.State;

/// Per-save persistent state owned by the framework. Stored under
/// `Helper.Data.WriteSaveData("MoreQuestsFrameworkState", state)` per plan.md §9.
///
/// All collections are keyed by the registered quest definition id (the JSON
/// `Name` verbatim — auto-prefixing by ownerUniqueId is deferred to Phase 10).
/// Empty/zero values are valid defaults, so a save written before any Phase 6
/// trigger fires deserialises into a clean state.
public sealed class FrameworkState
{
    /// Schema version. Bumped when the shape changes in an incompatible way so
    /// future migrations can branch on it.
    public int Schema { get; set; } = 1;

    /// Absolute `Game1.Date.TotalDays` of the most recent fire for each definition.
    /// Used by `Periodic`, `DateLocked` (yearly), and post-fire bookkeeping.
    public Dictionary<string, int> LastFiredDay { get; set; } = new();

    /// True for every definition whose OneShot `When` predicate has fired this save.
    /// Once set, a definition never fires again on this save.
    public Dictionary<string, bool> OneShotFired { get; set; } = new();

    /// Snapshot of farm-building types as of the last DayStarted. Compared on the
    /// next DayStarted to detect newly built buildings without polling.
    public List<string> LastSeenBuildings { get; set; } = new();

    /// Snapshot of `Game1.player.mailReceived` flags from the last DayStarted. Diffed
    /// to detect newly received mail flags.
    public List<string> LastSeenMailFlags { get; set; } = new();

    /// Definition id → `Game1.Date.TotalDays` on which a deferred (DayDelay) trigger
    /// should actually fire. Cleared once the day arrives.
    public Dictionary<string, int> ScheduledFireDay { get; set; } = new();

    /// Definition id → target NPC name. The DialogueWatcher pushes the quest into
    /// the journal the next time the player speaks with that NPC, then removes the
    /// entry. Survives save/load so a quest queued just before bedtime still fires.
    public Dictionary<string, string> PendingDialogueQuests { get; set; } = new();

    /// Mail-delivered quests waiting for the player to open the letter. Each entry
    /// carries the full posting + body; the framework rebuilds the Quest object via
    /// `QuestFactory` and re-injects the body into `Data/mail` at SaveLoaded so a
    /// letter sitting unread in the mailbox across save/load still resolves to the
    /// same quest.
    public List<StashedMailQuest> PendingMailDeliveries { get; set; } = new();

    /// SpecialOrder entries the framework has emitted into `Data/SpecialOrders` and
    /// not yet retired. Keyed by the order's runtime id. Survives save/load so the
    /// `OnAssetRequested` edit can be re-applied after the cache is wiped, and so
    /// expired entries can be swept on the next DayStarted.
    public List<EmittedSpecialOrder> EmittedSpecialOrders { get; set; } = new();

    /// OrderIds for which the framework has already applied its `FrameworkRewards` block.
    /// Persisted across save/load so a reload after completion doesn't double-grant. Each
    /// entry corresponds to one EmittedSpecialOrder.OrderId; cleared automatically when
    /// `SweepExpired` drops the parent emit record.
    public List<string> FrameworkRewardsGranted { get; set; } = new();

    /// Active shop-price discounts granted by `ShopDiscountReward`. Each entry rewrites
    /// matching `Data/Shops/<ShopId>` items at lookup time and is dropped on `DayStarted`
    /// once the in-game day passes `ExpiresAfterDay`.
    public List<ActiveShopDiscount> ActiveShopDiscounts { get; set; } = new();

    /// Active animal-purchase discounts granted by `AnimalPurchaseDiscountReward`. Each
    /// entry lowers every `Data/FarmAnimals` `PurchasePrice` by `PercentOff` until the
    /// in-game day passes `ExpiresAfterDay`, swept on `DayStarted`.
    public List<ActiveAnimalPurchaseDiscount> ActiveAnimalPurchaseDiscounts { get; set; } = new();

    /// Pending consequence dialogue lines, queued by `ConsequenceEngine.Apply` on quest
    /// completion and popped by `ConsequenceDialogueWatcher` the next time the player
    /// chats with the named NPC. Tier 3 chains span multiple entries with stepping
    /// `EarliestFireDay` values so one line surfaces per day.
    public List<DialogueQueueEntry> PendingConsequenceLines { get; set; } = new();

    /// NPC name → most recent `Game1.Date.TotalDays` on which the consequence pop
    /// fired for that NPC. The dialog watcher + Harmony prefix both consult this so
    /// at most one consequence line surfaces per NPC per day. Without this, a player
    /// who skips ahead with cheats (or just doesn't talk to the NPC for several days)
    /// gets every queued line back-to-back on the next chat.
    public Dictionary<string, int> LastConsequencePoppedDay { get; set; } = new();

    /// Active festival-bias rewards waiting to be consumed at their festival's judging
    /// hook (Luau governor reaction, Fair grange judging). One entry per granted reward;
    /// the Luau / Fair patches read and remove the relevant entry once the bias has been
    /// applied. Expired entries get swept on `DayStarted`.
    public List<ActiveFestivalBias> ActiveFestivalBiases { get; set; } = new();

    /// Pending Stardew Valley Fair star-token grants from `FairStarTokensReward`. Each
    /// entry stamps a flat bonus that gets added to `Game1.player.festivalScore` once
    /// the Fair event is live on Fall 16. Consumed on first apply; expired entries
    /// swept on `DayStarted` like the festival biases above.
    public List<ActiveFairStarTokens> ActiveFairStarTokens { get; set; } = new();
}

/// One in-flight festival bias granted by a `FestivalBiasReward`. Persisted to per-save
/// state so a quest completed days before the festival still nudges the outcome on the
/// festival day.
public sealed class ActiveFestivalBias
{
    /// `"Luau"` or `"Fair"`. Stringly-typed so the enum can move without breaking save
    /// compat — the readers tolerate unknown values by skipping them.
    public string Festival { get; set; } = "";

    /// How strong the bias is. Luau treats this as tier-bump steps (clamped 0..5);
    /// Fair adds it directly to `grangeScore`.
    public int Magnitude { get; set; }

    /// `Game1.Date.TotalDays` after which the bias gets dropped on the next sweep.
    /// Set generously to the festival day plus a small grace so a save loaded mid-festival
    /// still has the entry available.
    public int ExpiresAfterDay { get; set; }
}

/// One in-flight Fair star-token grant. Persisted so the bonus survives save/reload
/// between quest completion and Fall 16. Consumed on the Fair start tick.
public sealed class ActiveFairStarTokens
{
    public int Amount { get; set; }
    public int ExpiresAfterDay { get; set; }
}

/// One in-flight shop discount. Persisted in framework save state so the price reduction
/// survives save/reload across the full duration window.
public sealed class ActiveShopDiscount
{
    public string ShopId { get; set; } = "";
    public int PercentOff { get; set; }
    public int ExpiresAfterDay { get; set; }
    /// Item ids the discount applies to. Empty = discount every entry in the shop.
    public List<string> AppliesTo { get; set; } = new();
    /// When > 0, items in `AppliesTo` that the shop doesn't already sell are added as
    /// temporary entries with this per-visit stock count at the discounted price.
    public int GuaranteedStock { get; set; }
}

/// One in-flight animal-purchase discount. Persisted so the price reduction survives
/// save/reload across the full duration window.
public sealed class ActiveAnimalPurchaseDiscount
{
    public int PercentOff { get; set; }
    public int ExpiresAfterDay { get; set; }
}

/// One SpecialOrder entry the framework has injected into `Data/SpecialOrders`. The
/// `Spec` block carries the framework-neutral SpecialOrder shape; the writer constructs
/// a fresh vanilla `SpecialOrderData` from it on every asset-edit pass so the entry stays
/// in sync with whatever Stardew's strongly-typed asset dictionary expects (avoids the
/// pre-serialised-JSON route, which collided with SMAPI's `AsDictionary<string, T>` cast).
public sealed class EmittedSpecialOrder
{
    /// `Data/SpecialOrders` dict key. Namespaced as `<ownerUniqueId>.<defId>.<dayStamp>`
    /// so colliding with a vanilla or third-party order is essentially impossible.
    public string OrderId { get; set; } = "";

    /// `Game1.Date.TotalDays` when the entry was emitted. Used as the daystamp suffix
    /// in OrderId and for `Repeatable` re-emission gating.
    public int EmittedDay { get; set; }

    /// `Game1.Date.TotalDays` after which the framework drops the entry from the dict
    /// even if the player never accepted it. Set to emit-day + duration-in-days + a
    /// small grace window. Vanilla owns the in-flight order's actual due date once
    /// accepted.
    public int ExpiresAfterDay { get; set; }

    /// Owning content mod's UniqueID; set on the matching `IQuestDefinition`.
    public string OwnerUniqueId { get; set; } = "";

    /// Source definition id. Used to attribute `QuestCompleted` events back to the
    /// definition the order was generated from.
    public string DefinitionId { get; set; } = "";

    /// Framework-neutral SpecialOrder spec. Round-trips through Newtonsoft via
    /// `Helper.Data.WriteSaveData`. The writer translates this to a vanilla
    /// `SpecialOrderData` instance at every `Data/SpecialOrders` edit pass.
    public Pipeline.SpecialOrderSpec Spec { get; set; } = new();
}
