using System;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// "Earn N gold" objective. There's nothing to deliver or turn in: the quest snapshots
// the player's lifetime earnings the first time it's polled, then completes once they've
// earned goldTarget more from that point. Polled once a second off the quest log so it
// closes almost the moment the threshold is crossed.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_EarnMoneyQuest")]
public sealed class MoreQuestsEarnMoneyQuest : Quest, IRewardedQuest
{
    public readonly NetString target = new();
    public readonly NetInt goldTarget = new();
    public readonly NetBool baselineCaptured = new();
    public readonly NetLong baselineEarned = new();
    public readonly NetInt earnedSoFar = new();
    public readonly NetStringList serializedRewards = new();
    public readonly NetString baseObjective = new();

    [XmlIgnore]
    public NetStringList SerializedRewards => serializedRewards;

    public string targetMessage = string.Empty;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(target, "target")
            .AddField(goldTarget, "goldTarget")
            .AddField(baselineCaptured, "baselineCaptured")
            .AddField(baselineEarned, "baselineEarned")
            .AddField(earnedSoFar, "earnedSoFar")
            .AddField(serializedRewards, "serializedRewards")
            .AddField(baseObjective, "baseObjective");
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    public override void reloadObjective()
    {
        if (completed.Value)
            return;
        if (string.IsNullOrEmpty(baseObjective.Value) && !string.IsNullOrEmpty(_currentObjective))
            baseObjective.Value = _currentObjective;
        if (string.IsNullOrEmpty(baseObjective.Value))
            return;

        _currentObjective = $"{baseObjective.Value} ({earnedSoFar.Value}/{goldTarget.Value}g)";
    }

    public void ObserveMoney()
    {
        if (completed.Value || Game1.player == null)
            return;

        long earnedNow = (long)Game1.player.totalMoneyEarned;
        if (!baselineCaptured.Value)
        {
            baselineEarned.Value = earnedNow;
            baselineCaptured.Value = true;
        }

        long delta = Math.Max(0, earnedNow - baselineEarned.Value);
        int target = Math.Max(1, goldTarget.Value);
        int shown = (int)Math.Min(delta, target);
        if (shown != earnedSoFar.Value)
        {
            earnedSoFar.Value = shown;
            reloadObjective();
        }

        if (delta >= target)
            questComplete();
    }
}
