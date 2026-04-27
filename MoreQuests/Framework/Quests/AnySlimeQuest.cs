using System;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// `SlayMonsterQuest` variant whose kill counter accepts any slime variant
/// (Green/Blue/Red/Frost/Sludge/Tiger/Squid Kid mode), since they all derive
/// from `GreenSlime`. Replaces the vanilla name-based matching to avoid
/// double counting.
internal sealed class AnySlimeQuest : SlayMonsterQuest
{
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
}
