using Netcode;

namespace MoreQuestsFramework.Rewards;

/// Marker for custom Quest subclasses that participate in the declarative reward
/// pipeline. The framework's poster populates `SerializedRewards` from a posting's
/// `RewardSpec` list at delivery time; the subclass's `questComplete` override
/// hands those entries to `RewardApplier.ApplyEncoded`.
///
/// Money rewards bypass this interface - they go straight into vanilla's
/// `Quest.moneyReward` so the base completion path pays them automatically.
public interface IRewardedQuest
{
    NetStringList SerializedRewards { get; }
}
