using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Rewards;

namespace MoreQuestsFramework.Pipeline;

/// One vanilla SpecialOrder objective in framework-neutral form. Translated into a
/// `SpecialOrderObjectiveData` JSON entry by `SpecialOrderWriter` at emit time.
///
/// `Type` is the vanilla type name without the `Objective` suffix, `Ship`, `Collect`,
/// `Slay`, `Fish`, `Donate`, `Reach`, `GiftLove`, `JKHTask`. `Data` carries the per-type
/// fields exactly as vanilla expects them in `Data/SpecialOrders` (e.g. `AcceptedContextTags`
/// for Ship/Collect, `TargetName` for Slay).
public sealed class SpecialOrderObjectiveSpec
{
    public string Type { get; set; } = "Ship";
    public string Text { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public Dictionary<string, string> Data { get; set; } = new();
}

/// One vanilla SpecialOrder reward in framework-neutral form. Translated into a
/// `SpecialOrderRewardData` JSON entry by `SpecialOrderWriter` at emit time.
///
/// `Type` is the vanilla reward name without the `Reward` suffix, `Money`, `Friendship`,
/// `Object`, `Mail`, `Gems`, `ResetEvent`. `Data` carries the per-type fields (e.g.
/// `Amount` for Money, `TargetName` + `Amount` for Friendship).
public sealed class SpecialOrderRewardSpec
{
    public string Type { get; set; } = "Money";
    public Dictionary<string, string> Data { get; set; } = new();
}

/// Header block carried on `QuestPosting` when the posting routes to a vanilla SpecialOrder.
/// Owns every field the framework hands to vanilla's `Data/SpecialOrders` JSON for one entry.
/// Generators populate this directly; the `SpecialOrderWriter` reads it at emit time.
public sealed class SpecialOrderSpec
{
    /// Display name shown on the SpecialOrders board. Player-facing.
    public string Name { get; set; } = "";

    /// Body text shown on the order's accept screen. Player-facing.
    public string Text { get; set; } = "";

    /// Vanilla NPC who "requests" the order, drives the portrait shown on the board and
    /// the default `Friendship` reward target.
    public string Requester { get; set; } = "";

    /// Vanilla `QuestDuration` enum name (`OneDay` / `TwoDays` / `ThreeDays` / `Week` /
    /// `TwoWeeks` / `Month`). Defaults to `Week` when empty.
    public string Duration { get; set; } = "Week";

    /// Optional vanilla `OrderType` discriminator. Empty = default board (the regular
    /// SpecialOrders board in town). `"Qi"` = Mr. Qi's board on the island.
    public string OrderType { get; set; } = "";

    /// Optional vanilla `RequiredTags` (comma-AND, slash-OR) to gate the order on the board
    /// independent of the framework's own `Available` conditions. Most callers leave empty.
    public string RequiredTags { get; set; } = "";

    public List<SpecialOrderObjectiveSpec> Objectives { get; set; } = new();
    public List<SpecialOrderRewardSpec> Rewards { get; set; } = new();

    /// Framework-owned rewards granted directly by `SpecialOrderWriter` once the order
    /// flips to `Complete`. Sits OUTSIDE vanilla's `Data/SpecialOrders` Rewards array, so
    /// third-party content packs that edit the asset to "configure" or "boost" vanilla
    /// rewards (e.g. friendship-tuning packs that overwrite all Friendship `OrderReward`
    /// entries) cannot intercept these. Use this path for any reward whose semantics must
    /// be exactly what the content mod author specified.
    ///
    /// Money is intentionally kept in the vanilla `Rewards` path: vanilla's reward UI
    /// shows it, the player clicks the reward box, money is paid. That UX is worth more
    /// than the small risk of a third-party mod adjusting the displayed gold amount.
    public List<RewardSpec> FrameworkRewards { get; set; } = new();

    /// Consequence specs fired by `ConsequenceEngine.Active` once the order completes.
    /// Multiple specs are supported because a Grand Feast SpecialOrder pulls one taste
    /// reaction per requested dish (CSV row 34: "Tier 2 consequence per dish"). Each
    /// spec is dispatched independently, so the engine's per-spec NPC sampling caps
    /// the total fanfare at one reaction per dish rather than one per save.
    public List<ConsequenceSpec> Consequences { get; set; } = new();
}
