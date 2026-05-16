using Netcode;

namespace MoreQuestsFramework.Rewards;

// Money rewards bypass this and route through Quest.moneyReward instead.
public interface IRewardedQuest
{
    NetStringList SerializedRewards { get; }
}
