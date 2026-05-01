using System.Collections.Generic;

namespace MoreQuestsFramework.Pipeline;

/// One vanilla SpecialOrder objective in framework-neutral form. Translated into a
/// `SpecialOrderObjectiveData` JSON entry by `SpecialOrderWriter` at emit time.
///
/// `Type` is the vanilla type name without the `Objective` suffix — `Ship`, `Collect`,
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
/// `Type` is the vanilla reward name without the `Reward` suffix — `Money`, `Friendship`,
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

    /// Vanilla NPC who "requests" the order — drives the portrait shown on the board and
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
}
