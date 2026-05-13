using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Quests;

internal static partial class Generators
{
    private static QuestPosting? BasicSlimeClearing(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatNpcs);
        if (giver == null)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(6, Game1.player.CombatLevel * 3 + 1);
            qty = Game1.random.Next(5, upper);
        }
        else
        {
            qty = Game1.random.Next(3, 13);
        }
        int gold = ctx.Config.GoldBeginnerBase * Math.Max(1, qty / 2);

        var quest = new AnySlimeQuest
        {
            target = { Value = giver },
            monsterName = { Value = "Green Slime" },
            numberToKill = { Value = qty },
            reward = { Value = gold },
            targetMessage = ModEntry.I18n.Get("quest.mining.slime.targetMessage", new { npc = giver })
        };

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = giver,
            ObjectiveItemId = "Green Slime",
            ObjectiveItemName = "Green Slime",
            ObjectiveQuantity = qty,
            TargetMonster = "Green Slime",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.mining.slime.title"),
            Description = ModEntry.I18n.Get("quest.mining.slime.description", new { qty, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.slime.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.slime.targetMessage", new { npc = giver }),
            PreBuiltQuest = quest
        };
    }

    /// Prismatic Bar from Si.ExtraCraftingMaterials. Only used when the mod is loaded AND
    /// the player has reached Ginger Island.
    private const string PrismaticBarItemId = "Si.ECM_PrismaticBar";

    /// Geodes + Artifact Trove side-reward pool. One type stacked qty/2 keeps the reward
    /// tidy (single stack instead of a mixed-geode hand).
    private static readonly string[] BarDeliveryGeodeRewards =
    {
        "(O)535", // Geode
        "(O)536", // Frozen Geode
        "(O)537", // Magma Geode
        "(O)749", // Omni Geode
        "(O)275"  // Artifact Trove
    };

    /// Bar order from the blacksmith pool. Bar tier expands with mine depth: Copper from
    /// the start, Iron at 40, Gold at 80, Iridium past Skull Cavern, Radioactive (and
    /// optional Prismatic) on Ginger Island. Reward: GoldIntermediateBase + qty/2 of one
    /// random geode. Mining 3 gate.
    private static QuestPosting? BarDelivery(QuestContext ctx)
    {
        if (Game1.player.MiningLevel < 3)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.BlacksmithNpcs);
        if (giver == null)
            return null;

        int deepest = Game1.player.deepestMineLevel;
        bool skullCavernUnlocked = deepest > 120;
        bool gingerIslandUnlocked = Game1.player.hasOrWillReceiveMail("Visit_Island");
        bool hasEcm = ctx.Helper.ModRegistry.IsLoaded(ModCompat.SiExtraCraftingMaterials);

        var pool = new List<string> { "(O)334" }; // Copper
        if (deepest >= 40) pool.Add("(O)335");    // Iron
        if (deepest >= 80) pool.Add("(O)336");    // Gold
        if (skullCavernUnlocked) pool.Add("(O)337"); // Iridium
        if (skullCavernUnlocked && gingerIslandUnlocked)
        {
            pool.Add("(O)910"); // Radioactive
            if (hasEcm)
                pool.Add(PrismaticBarItemId);
        }

        var pick = ctx.Items.TryResolveItem(pool[Game1.random.Next(pool.Count)]);
        if (pick == null)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(3, (int)Math.Floor(Game1.player.MiningLevel * 1.5));
            qty = Game1.random.Next(2, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(2, 7);
        }

        int gold = ctx.Config.GoldIntermediateBase;
        string geodeId = BarDeliveryGeodeRewards[Game1.random.Next(BarDeliveryGeodeRewards.Length)];
        int geodeCount = Math.Max(1, qty / 2);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold), new ObjectReward(geodeId, geodeCount) },
            Title = ModEntry.I18n.Get("quest.mining.bar.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.mining.bar.description", new { qty, item = pick.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.bar.objective", new { qty, item = pick.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.bar.targetMessage")
        };
    }

    /// Ores + stone accepted by Mines Deep Dive. Marlon isn't picky about what fills the
    /// crate; the bar reward is fixed by quest difficulty.
    private static readonly string[] AnyOreOrStone =
    {
        "(O)378", // Copper Ore
        "(O)380", // Iron Ore
        "(O)384", // Gold Ore
        "(O)386", // Iridium Ore
        "(O)390"  // Stone
    };

    /// Ores accepted by Skull Cavern Deep Dive. Stone is excluded since the pitch is
    /// "exceptional ores beyond the target floor".
    private static readonly string[] AnyOreNoStone =
    {
        "(O)378", // Copper Ore
        "(O)380", // Iron Ore
        "(O)384", // Gold Ore
        "(O)386" // Iridium Ore
    };

    /// Guild-board two-step Adventure (falls back to help-wanted if the guild board is off):
    /// ReachLevel target floor in Skull Cavern, then ship an ore haul. Floor target rolls in
    /// [10, SkullCavernMaxLevel]. Reward: a parameterised mail-delivered Radioactive Bar stack
    /// the next morning (no gold; the player already realises the shipping value).
    private static QuestPosting? SkullCavernDeepDive(QuestContext ctx)
    {
        // Skull Cavern unlocks once the player passes floor 120.
        if (Game1.player.deepestMineLevel <= 120)
            return null;

        int maxFloor = Math.Max(11, ModEntry.Config.SkullCavernMaxLevel + 1);
        int targetFloor = Game1.random.Next(10, maxFloor);

        int haul;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(16, Game1.player.MiningLevel * 5 + 1);
            haul = Game1.random.Next(15, upper);
        }
        else
        {
            haul = Game1.random.Next(5, 26);
        }

        int bars = Math.Max(2, haul / 5);

        // Marlon is the in-character giver but there's no NPC turn-in. The Ship step closes
        // the quest at DayEnding. Bar reward arrives by mail the next morning so items don't
        // mysteriously appear in inventory while sleeping.
        const string giver = "Marlon";
        const string barId = "910"; // Radioactive Bar
        string letterKey = BuildDeepDiveRewardLetterKey(barId, bars);
        var oreIds = new List<string>(AnyOreNoStone);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ReachFloor",
                Kind = AdventureStepKind.ReachLevel,
                Targets = new List<string> { "SkullCavern" },
                Count = targetFloor,
                Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.step.reach", new { floor = targetFloor })
            },
            new AdventureStepState
            {
                Name = "ShipHaul",
                Kind = AdventureStepKind.Ship,
                Items = oreIds,
                Count = haul,
                Requires = new List<string> { "ReachFloor" },
                Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.step.ship", new { count = haul })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.title"),
            Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.description", new { floor = targetFloor, count = haul }),
            PreBuiltQuest = quest
        };
    }

    /// Sibling of SkullCavernDeepDive for the regular Mines (cap 120). Scaling on: ceiling
    /// is 120. Scaling off: tracks the player's deepest Mines progress. Bar reward matches
    /// the floor band (same thresholds as BarDelivery: Copper &lt;40, Iron 40-79, Gold 80-119,
    /// Iridium 120). Ship step closes at DayEnding (Marlon isn't giftable).
    private static QuestPosting? MinesDeepDive(QuestContext ctx)
    {
        int deepest = Math.Min(120, Game1.player.deepestMineLevel);
        if (deepest < 5)
            return null;

        int maxLevel = ctx.Config.DifficultyScaling
            ? 120
            : deepest;
        int low = Math.Min(10, maxLevel);
        int targetFloor = Game1.random.Next(low, maxLevel + 1);

        int haul;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = 5 + Game1.player.MiningLevel * 3 + 1;
            haul = Game1.random.Next(5, Math.Max(upper, 6));
        }
        else
        {
            haul = Game1.random.Next(5, 17);
        }

        (string barId, int barCount) = targetFloor switch
        {
            >= 120 => ("337", Math.Max(1, haul / 5)), // Iridium Bar
            >= 80 => ("336", Math.Max(1, haul / 5)),  // Gold Bar
            >= 40 => ("335", Math.Max(1, haul / 5)),  // Iron Bar
            _ => ("334", Math.Max(1, haul / 5))        // Copper Bar
        };
        int gold = ctx.Config.GoldIntermediateBase;
        string letterKey = BuildDeepDiveRewardLetterKey(barId, barCount);

        const string giver = "Marlon";
        var oreIds = new List<string>(AnyOreOrStone);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ReachFloor",
                Kind = AdventureStepKind.ReachLevel,
                Targets = new List<string> { "Mine" },
                Count = targetFloor,
                Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.step.reach", new { floor = targetFloor })
            },
            new AdventureStepState
            {
                Name = "ShipHaul",
                Kind = AdventureStepKind.Ship,
                Items = oreIds,
                Count = haul,
                Requires = new List<string> { "ReachFloor" },
                Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.step.ship", new { count = haul })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.mining.minesDeepDive.title"),
            Description = ModEntry.I18n.Get("quest.mining.minesDeepDive.description", new { floor = targetFloor, count = haul }),
            PreBuiltQuest = quest
        };
    }

    /// Bakes the bar id + count into the mail key. The content mod's Data/mail asset edit
    /// parses the suffix at read time so nothing needs to be persisted per-quest. Format:
    /// `RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}` (slots into `%item object {barId} {count} %%`).
    internal static string BuildDeepDiveRewardLetterKey(string barId, int count)
        => $"RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}";

    /// 1 heart of friendship = 250 points. Threshold for whether an "underground" NPC reacts
    /// to the monster-parts trade. At zero hearts a freshly-met villager feels too distant.
    private const int FriendshipPointsPerHeart = 250;

    /// Item delivery from a CombatNpcs-pool giver for a Combat-scaled qty of one rare
    /// monster drop. Reward: one random gem/artifact from Data/Objects, stack sized so the
    /// headline value clears GoldIntermediateBase. Tier 1 negative consequence on Krobus /
    /// Dwarf / Sen at 1+ hearts. Quest refuses to post when none of the three qualify.
    private static QuestPosting? MonsterParts(QuestContext ctx)
    {
        if (Game1.player.CombatLevel < 2)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatNpcs);
        if (giver == null)
            return null;

        // Quest refuses to post unless at least one underground NPC is at 1+ hearts.
        var underground = ResolveUndergroundTargets(exclude: giver);
        if (underground.Count == 0)
            return null;

        // TODO: vanilla drops only. Modded drops aren't enumerable without per-mod data;
        // extend manually when adding RSV/ESV/SVE monster coverage.
        var drop = MonsterDropPool[Game1.random.Next(MonsterDropPool.Length)];
        var resolved = ctx.Items.TryResolveItem(drop.Id);
        if (resolved == null)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(6, Game1.player.CombatLevel * 2 + 1);
            qty = Game1.random.Next(5, upper);
        }
        else
        {
            qty = Game1.random.Next(3, 12);
        }

        var rewardOpt = PickGemOrArtifactReward(ctx);
        if (rewardOpt == null)
            return null;
        var reward = rewardOpt.Value;

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            consequence = new ConsequenceSpec
            {
                Tier = ConsequenceTier.Tier1,
                Source = ConsequenceSource.Static,
                Targets = underground,
                HatedLine = ModEntry.I18n.Get(
                    "quest.mining.monsterParts.consequence.hated",
                    new { item = resolved.DisplayName, npc = giver })
            };
        }

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = resolved.QualifiedItemId,
            ObjectiveItemName = resolved.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(reward.QualifiedItemId, reward.Count) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.mining.monsterParts.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.mining.monsterParts.description", new { qty, item = resolved.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.monsterParts.objective", new { qty, item = resolved.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.monsterParts.targetMessage")
        };
    }

    /// Random gem or artifact from Data/Objects, stacked so count*sellPrice clears
    /// GoldIntermediateBase. Picks up modded gems/artifacts automatically.
    private static (string QualifiedItemId, int Count)? PickGemOrArtifactReward(QuestContext ctx)
    {
        var data = ctx.Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.Objects.ObjectData>>("Data/Objects");
        var pool = new List<(string Id, int Price)>(data.Count);
        foreach (var (rawId, obj) in data)
        {
            if (obj == null || obj.Price <= 0)
                continue;
            bool isGem = obj.Category == StardewValley.Object.GemCategory;
            bool isArtifact = string.Equals(obj.Type, "Arch", StringComparison.OrdinalIgnoreCase);
            if (!isGem && !isArtifact)
                continue;
            pool.Add(("(O)" + rawId, obj.Price));
        }
        if (pool.Count == 0)
            return null;

        int floor = ctx.Config.GoldIntermediateBase;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var pick = pool[Game1.random.Next(pool.Count)];
            var resolved = ctx.Items.TryResolveItem(pick.Id);
            if (resolved == null)
                continue;
            int count = Math.Max(1, (int)Math.Ceiling(floor / (double)pick.Price));
            return (resolved.QualifiedItemId, count);
        }
        return null;
    }

    /// Vanilla "rare" monster drops. Modded drops aren't enumerable without per-mod data.
    /// The NPCGiftTastes consequence path still picks up modded NPCs reacting to these.
    private static readonly (string Id, string Name)[] MonsterDropPool =
    {
        ("(O)767", "Bat Wing"),
        ("(O)768", "Solar Essence"),
        ("(O)769", "Void Essence"),
        ("(O)684", "Bug Meat")
    };


    /// Krobus + Dwarf are vanilla, Sen ships with Lurking in the Dark - NPC Sen
    /// (East Scarp. Filters to met NPCs at 1+ hearts
    /// so the consequence reads as a reaction from someone the player knows.
    private static List<string> ResolveUndergroundTargets(string exclude)
    {
        string[] candidates = { "Krobus", "Dwarf", "Sen" };
        var targets = new List<string>(candidates.Length);
        foreach (var npc in candidates)
        {
            if (string.Equals(npc, exclude, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Game1.getCharacterFromName(npc) == null)
                continue;
            if (!Game1.player.friendshipData.TryGetValue(npc, out var friendship))
                continue;
            if (friendship == null || friendship.Points < FriendshipPointsPerHeart)
                continue;
            targets.Add(npc);
        }
        return targets;
    }

    /// SlayMonster (any monster) from a CombatNpcs-pool giver. Reward: GoldIntermediateBase
    /// + one combat food sized to the rolled magnitude bucket (+1 when scaling off, random
    /// +1/+2/+3 when on). Combat-food pool is scanned at save load from Data/Objects
    /// edibles whose buffs grant non-zero Attack or Defense.
    private static QuestPosting? MonsterHunt(QuestContext ctx)
    {
        if (Game1.player.deepestMineLevel < 1)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatNpcs);
        if (giver == null)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(8, 6 + 2 * Game1.player.CombatLevel)
            : Game1.random.Next(3, 13);

        int gold = ctx.Config.GoldIntermediateBase;

        int targetMagnitude = ctx.Config.DifficultyScaling ? Game1.random.Next(1, 4) : 1;

        var fw = ModEntry.Framework;
        var pool = fw?.GetCombatFoodPool() ?? Array.Empty<string>();
        var bucket = new List<string>();
        foreach (string id in pool)
        {
            int? m = fw?.GetCombatFoodMagnitude(id);
            if (m.HasValue && m.Value == targetMagnitude)
                bucket.Add(id);
        }
        // Fallback: if the rolled bucket is empty (no +3 foods in this save's content),
        // drop to the next-lower magnitude that has entries.
        if (bucket.Count == 0)
        {
            for (int m = targetMagnitude - 1; m >= 1 && bucket.Count == 0; m--)
            {
                foreach (string id in pool)
                {
                    int? mag = fw?.GetCombatFoodMagnitude(id);
                    if (mag.HasValue && mag.Value == m)
                        bucket.Add(id);
                }
            }
        }

        ResolvedItem? rewardFood = null;
        if (bucket.Count > 0)
        {
            for (int i = 0; i < Math.Min(bucket.Count, 5) && rewardFood == null; i++)
                rewardFood = ctx.Items.TryResolveItem(bucket[Game1.random.Next(bucket.Count)]);
        }

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        if (rewardFood != null)
            rewards.Add(new ObjectReward(rewardFood.QualifiedItemId));

        var quest = new AnyMonsterQuest
        {
            target = { Value = giver },
            monsterName = { Value = "any" },
            numberToKill = { Value = qty },
            reward = { Value = gold },
            targetMessage = ModEntry.I18n.Get("quest.mining.monsterHunt.targetMessage")
        };

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.SlayMonster,
            QuestGiver = giver,
            ObjectiveItemId = "any monster",
            ObjectiveItemName = "any monster",
            ObjectiveQuantity = qty,
            TargetMonster = "any",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.mining.monsterHunt.title"),
            Description = ModEntry.I18n.Get("quest.mining.monsterHunt.description", new { qty, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.monsterHunt.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.monsterHunt.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// ItemDelivery from a blacksmith asking for a Mining-scaled qty of one random gem
    /// (live Data/Objects scan, modded gems included). Reward: GoldAdvancedBase + Artifact
    /// Troves 1:1 with the gem count. Mining 7 gate.
    private static QuestPosting? RareMaterialRequest(QuestContext ctx)
    {
        if (Game1.player.MiningLevel < 7)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.BlacksmithNpcs);
        if (giver == null)
            return null;

        var gem = PickRandomGem(ctx);
        if (gem == null)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = 2 + Game1.player.MiningLevel / 2;
            qty = Game1.random.Next(1, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(1, 5);
        }

        int gold = ctx.Config.GoldAdvancedBase;
        var rewards = new List<RewardSpec>
        {
            new MoneyReward(gold),
            new ObjectReward("(O)275", qty) // Artifact Trove x qty
        };

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = gem.QualifiedItemId,
            ObjectiveItemName = gem.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.mining.rareMaterial.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.mining.rareMaterial.description", new { qty, item = gem.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.rareMaterial.objective", new { qty, item = gem.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.rareMaterial.targetMessage")
        };
    }

    /// Random gem from Data/Objects (Category GemCategory, Price &gt; 0). Vanilla + modded.
    private static ResolvedItem? PickRandomGem(QuestContext ctx)
    {
        var data = ctx.Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.Objects.ObjectData>>("Data/Objects");
        var ids = new List<string>();
        foreach (var (rawId, obj) in data)
        {
            if (obj == null || obj.Price <= 0)
                continue;
            if (obj.Category != StardewValley.Object.GemCategory)
                continue;
            ids.Add("(O)" + rawId);
        }
        if (ids.Count == 0)
            return null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var resolved = ctx.Items.TryResolveItem(ids[Game1.random.Next(ids.Count)]);
            if (resolved != null)
                return resolved;
        }
        return null;
    }

}
