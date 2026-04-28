using System;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// `SlayMonsterQuest` variant whose kill counter accepts any slime variant
/// (Green/Blue/Red/Frost/Sludge/Tiger/Squid Kid mode), since they all derive
/// from `GreenSlime`. Replaces the vanilla name-based matching to avoid
/// double counting and the misleading "Green Slimes" label.
[XmlType("Mods_RafiaBee_MoreQuests_AnySlimeQuest")]
public sealed class AnySlimeQuest : SlayMonsterQuest
{
    public AnySlimeQuest()
    {
        // Suppress vanilla's auto-title regeneration (which would print "Slay X Green Slime")
        // so the title we set from the posting is the one that sticks.
        _loadedTitle = true;
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

    /// Vanilla `reloadObjective` would render "x/y Green Slimes" using `monster.Value`'s
    /// display name. We track all slimes, so the label should say "slimes" generically.
    public override void reloadObjective()
    {
        if (numberKilled.Value < numberToKill.Value)
            currentObjective = $"{numberKilled.Value}/{numberToKill.Value} slimes slain";
        else if (objective.Value != null)
            currentObjective = objective.Value.loadDescriptionElement();
    }

    /// Mirror vanilla's completion path explicitly. Vanilla's version calls
    /// `reloadDescription()` which can blank out our manually-set targetMessage if
    /// `parts`/`dialogueparts` are empty; we sidestep that by pushing the message
    /// directly.
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
