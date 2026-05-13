using System;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;

namespace MoreQuests.Quests;

/// SlayMonsterQuest variant that counts any slime variant (they all derive from GreenSlime).
/// Replaces vanilla's name-based matching to avoid double counting and the "Green Slimes" label.
[XmlType("Mods_RafiaBee_MoreQuests_AnySlimeQuest")]
public sealed class AnySlimeQuest : SlayMonsterQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    public NetStringList SerializedRewards => serializedRewards;

    public AnySlimeQuest()
    {
        // Suppress vanilla's auto-title regen ("Slay X Green Slime") so the posting's title sticks.
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
        if (monster is not GreenSlime)
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

    /// Vanilla would render "x/y Green Slimes" via monster.Value's name. We track all slimes
    /// so the label needs to be generic.
    public override void reloadObjective()
    {
        if (numberKilled.Value < numberToKill.Value)
            currentObjective = $"{numberKilled.Value}/{numberToKill.Value} slimes slain";
        else if (objective.Value != null)
            currentObjective = objective.Value.loadDescriptionElement();
    }

    /// Mirrors vanilla's completion path but pushes targetMessage directly. Vanilla's
    /// reloadDescription() can blank out the message when parts/dialogueparts are empty.
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
