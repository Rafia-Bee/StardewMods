using System;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;

namespace MoreQuests.Quests;

/// `SlayMonsterQuest` variant whose kill counter accepts any non-tame, non-farm
/// monster. Used by Monster Hunt (CSV row 52) where the giver doesn't care what
/// the player slays, only that the count is met. Title and objective render as
/// "monsters" rather than the vanilla per-kind label.
[XmlType("Mods_RafiaBee_MoreQuests_AnyMonsterQuest")]
public sealed class AnyMonsterQuest : SlayMonsterQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    public NetStringList SerializedRewards => serializedRewards;

    public AnyMonsterQuest()
    {
        // Suppress vanilla's auto-title regeneration so the title set from the
        // posting sticks.
        _loadedTitle = true;
    }

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(serializedRewards, "serializedRewards");
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    public override bool OnMonsterSlain(GameLocation location, Monster monster, bool killedByBomb, bool isTameMonster, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (monster == null || isTameMonster)
            return false;
        if (ignoreFarmMonsters.Value && location?.IsFarm == true)
            return false;
        if (numberKilled.Value >= numberToKill.Value)
            return false;

        if (probe)
            return true;

        numberKilled.Value = Math.Min(numberToKill.Value, numberKilled.Value + 1);
        Game1.dayTimeMoneyBox.pingQuest(this);

        if (numberKilled.Value >= numberToKill.Value)
        {
            if (string.IsNullOrEmpty(target.Value) || target.Value.Equals("null"))
            {
                questComplete();
            }
            else
            {
                NPC npc = Game1.getCharacterFromName(target.Value);
                objective.Value = new DescriptionElement("Strings\\StringsFromCSFiles:FishingQuest.cs.13277", npc);
                Game1.playSound("jingle1");
            }
        }
        else if (this.monster.Value == null)
        {
            this.monster.Value = new Monster("Green Slime", Vector2.Zero);
        }
        return true;
    }

    public override void reloadObjective()
    {
        if (numberKilled.Value < numberToKill.Value)
            currentObjective = $"{numberKilled.Value}/{numberToKill.Value} monsters slain";
        else if (objective.Value != null)
            currentObjective = objective.Value.loadDescriptionElement();
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (npc == null || string.IsNullOrEmpty(target.Value) || target.Value == "null")
            return false;
        if (numberKilled.Value < numberToKill.Value)
            return false;
        if (!string.Equals(npc.Name, target.Value, StringComparison.OrdinalIgnoreCase) || !npc.IsVillager)
            return false;
        if (probe)
            return true;

        if (!string.IsNullOrEmpty(targetMessage))
        {
            npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
            Game1.drawDialogue(npc);
        }
        moneyReward.Value = Math.Max(moneyReward.Value, reward.Value);
        questComplete();
        return true;
    }
}
