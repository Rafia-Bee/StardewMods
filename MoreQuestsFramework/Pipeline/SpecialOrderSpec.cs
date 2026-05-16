using System.Collections.Generic;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Rewards;

namespace MoreQuestsFramework.Pipeline;

// Type = vanilla type name without "Objective" suffix (Ship, Collect, Slay, Fish,
// Donate, Reach, GiftLove, JKHTask). Data carries the per-type fields verbatim.
public sealed class SpecialOrderObjectiveSpec
{
    public string Type { get; set; } = "Ship";
    public string Text { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public Dictionary<string, string> Data { get; set; } = new();
}

// Type = vanilla reward name without "Reward" suffix (Money, Friendship, Object,
// Mail, Gems, ResetEvent). Data carries the per-type fields verbatim.
public sealed class SpecialOrderRewardSpec
{
    public string Type { get; set; } = "Money";
    public Dictionary<string, string> Data { get; set; } = new();
}

public sealed class SpecialOrderSpec
{
    public string Name { get; set; } = "";

    public string Text { get; set; } = "";

    // Drives the portrait shown on the board and the default Friendship reward target.
    public string Requester { get; set; } = "";

    // QuestDuration enum: OneDay/TwoDays/ThreeDays/Week/TwoWeeks/Month.
    public string Duration { get; set; } = "Week";

    // Empty = default board. "Qi" = Mr. Qi's island board.
    public string OrderType { get; set; } = "";

    // Vanilla RequiredTags (comma-AND, slash-OR).
    public string RequiredTags { get; set; } = "";

    public List<SpecialOrderObjectiveSpec> Objectives { get; set; } = new();
    public List<SpecialOrderRewardSpec> Rewards { get; set; } = new();

    // Granted directly by SpecialOrderWriter on Complete; bypasses Data/SpecialOrders.Rewards
    // so third-party packs that overwrite reward entries can't intercept.
    // Money intentionally stays in the vanilla Rewards path so the vanilla reward UI shows it.
    public List<RewardSpec> FrameworkRewards { get; set; } = new();

    // Fired by ConsequenceEngine.Active on completion. Multiple specs supported (e.g.
    // Grand Feast pulls one reaction per requested dish).
    public List<ConsequenceSpec> Consequences { get; set; } = new();
}
