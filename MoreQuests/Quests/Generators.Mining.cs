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

    /// Prismatic Bar comes from `Si.ExtraCraftingMaterials`. Only used as a bar pool
    /// entry when the mod is loaded AND the player has reached the Ginger Island tier.
    private const string PrismaticBarItemId = "Si.ECM_PrismaticBar";

    /// Geodes + Artifact Trove that can roll as a side reward alongside the gold payout.
    /// Picking one type and stacking `qty / 2` of it keeps the reward tidy in the mail
    /// (a single object stack rather than a hand-of-mixed-geodes).
    private static readonly string[] BarDeliveryGeodeRewards =
    {
        "(O)535", // Geode
        "(O)536", // Frozen Geode
        "(O)537", // Magma Geode
        "(O)749", // Omni Geode
        "(O)275"  // Artifact Trove
    };

    /// CSV row 2. Daily-board bar order routed through the blacksmith pool (Clint
    /// vanilla; MarlonFay SVE, Eli ED, Mariam VMV, Lola/Jio/Daia RSV). Bar tier expands
    /// with mine depth: Copper from the start, Iron at floor 40, Gold at 80, Iridium
    /// once Skull Cavern is unlocked, and Radioactive + (mod-gated) Prismatic once the
    /// player is on Ginger Island. Quantity scales with Mining when DifficultyScaling
    /// is on. Reward = `GoldIntermediateBase` + `qty/2` of one random geode (Geode /
    /// Frozen / Magma / Omni / Artifact Trove). Skill gate: Mining 3.
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

    /// Vanilla ore + stone ids accepted by both deep-dive quests' Deliver step. Stone is
    /// included per the CSV row's "(any type of ore)/stone" wording — Marlon's not picky
    /// about what fills the crate, the bar reward is fixed by quest difficulty rather
    /// than the player's specific haul.
    private static readonly string[] AnyOreOrStone =
    {
        "(O)378", // Copper Ore
        "(O)380", // Iron Ore
        "(O)384", // Gold Ore
        "(O)386", // Iridium Ore
        "(O)390"  // Stone
    };

    /// CSV row 67. Daily-board posting on the Adventurer's Guild custom board. Two-step
    /// Adventure: ReachLevel a target floor in Skull Cavern → ship the ore/stone haul via
    /// the farm shipping bin. Floor target rolls in [50, Config.SkullCavernMaxLevel]; haul
    /// size scales with Mining skill when DifficultyScaling is on. Reward = `GoldAdvancedBase`
    /// + an iridium-bar count proportional to the haul (5 ores ≈ 1 bar, vanilla smelt ratio),
    /// granted directly into inventory + money on quest completion.
    ///
    /// Shipping-step framing keeps the quest playable on vanilla saves: Marlon is only a
    /// shopkeeper sprite without SVE, so `OnItemOfferedToNpc` never fires for him. The
    /// shipping-bin observer (`AdventureQuest.ObserveShippingBin` at DayEnding) doesn't
    /// require an NPC at all.

    /// CSV row 67. Daily-board posting on the Adventurer's Guild custom board. Two-step
    /// Adventure: ReachLevel a target floor in Skull Cavern → ship the ore/stone haul via
    /// the farm shipping bin. Floor target rolls in [50, Config.SkullCavernMaxLevel]; haul
    /// size scales with Mining skill when DifficultyScaling is on. Reward = `GoldAdvancedBase`
    /// + an iridium-bar count proportional to the haul (5 ores ≈ 1 bar, vanilla smelt ratio),
    /// granted directly into inventory + money on quest completion.
    ///
    /// Shipping-step framing keeps the quest playable on vanilla saves: Marlon is only a
    /// shopkeeper sprite without SVE, so `OnItemOfferedToNpc` never fires for him. The
    /// shipping-bin observer (`AdventureQuest.ObserveShippingBin` at DayEnding) doesn't
    /// require an NPC at all.
    private static QuestPosting? SkullCavernDeepDive(QuestContext ctx)
    {
        // Gate on Skull Cavern access. Vanilla unlocks the Cavern after the first iridium
        // ore drops the player past floor 120; reflecting that in availability keeps the
        // quest from posting on saves where Skull Cavern is still locked.
        if (Game1.player.deepestMineLevel <= 120)
            return null;

        int maxFloor = Math.Max(20, ModEntry.Config.SkullCavernMaxLevel);
        int targetFloor = Game1.random.Next(50, maxFloor + 1);

        int haul = ctx.Config.DifficultyScaling
            ? Math.Max(15, 10 + 2 * Game1.player.MiningLevel)
            : 25;

        int bars = Math.Max(2, haul / 5);
        int gold = ctx.Config.GoldAdvancedBase;

        // Marlon is the in-character "giver" the journal references, but no NPC turn-in
        // happens — the Ship step closes the quest on its own at DayEnding. The bar
        // reward arrives the morning after via a parameterised mail key so the player
        // doesn't get items mysteriously appearing in inventory while sleeping.
        const string giver = "Marlon";
        const string barId = "337"; // Iridium Bar (bare id; the mail token uses bare ids)
        string letterKey = BuildDeepDiveRewardLetterKey(barId, bars);
        var oreIds = new List<string>(AnyOreOrStone);

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
                new MoneyReward(gold),
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.title"),
            Description = ModEntry.I18n.Get("quest.mining.skullCavernDeepDive.description", new { floor = targetFloor, count = haul }),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 78. Sibling of `SkullCavernDeepDive` for the regular Mines (capped at 120).
    /// Floor target rolls in a band that scales with Mining skill so a fresh save isn't
    /// asked to reach floor 120 immediately. Bar type reward matches the floor band:
    /// Copper at 1-79, Iron at 80-99, Gold at 100-120. Ship step at DayEnding closes the
    /// quest without requiring an NPC turn-in (vanilla Marlon isn't a giftable villager).

    /// CSV row 78. Sibling of `SkullCavernDeepDive` for the regular Mines (capped at 120).
    /// Floor target rolls in a band that scales with Mining skill so a fresh save isn't
    /// asked to reach floor 120 immediately. Bar type reward matches the floor band:
    /// Copper at 1-79, Iron at 80-99, Gold at 100-120. Ship step at DayEnding closes the
    /// quest without requiring an NPC turn-in (vanilla Marlon isn't a giftable villager).
    private static QuestPosting? MinesDeepDive(QuestContext ctx)
    {
        int currentDeepest = Math.Min(120, Game1.player.deepestMineLevel);
        if (currentDeepest < 5)
            return null; // need at least a few floors of progress before asking for more

        // Target a floor band slightly past where the player has already been so the
        // quest demands genuine progress without being unreachable. Cap at 120.
        int low = Math.Max(20, currentDeepest - 30);
        int high = Math.Min(120, currentDeepest + 30);
        if (high <= low)
            high = low + 1;
        int targetFloor = Game1.random.Next(low, high + 1);

        int haul = ctx.Config.DifficultyScaling
            ? Math.Max(8, 6 + Game1.player.MiningLevel)
            : 15;

        // Bar reward type matches the floor band the quest targets — the Guild scales the
        // payout to whatever's plausible at that depth.
        (string barId, int barCount) = targetFloor switch
        {
            >= 100 => ("336", Math.Max(1, haul / 5)), // Gold Bar
            >= 80 => ("335", Math.Max(1, haul / 5)),  // Iron Bar
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

    /// Builds a parameterised reward letter key that bakes the bar id + count into the
    /// key itself. The content mod's `Data/mail` asset edit parses the suffix on the fly,
    /// so neither the framework nor save state needs to track per-quest reward bodies.
    /// Format: `RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}` — both numeric so the
    /// `%item object {barId} {count} %%` mail token slots in directly.

    /// Builds a parameterised reward letter key that bakes the bar id + count into the
    /// key itself. The content mod's `Data/mail` asset edit parses the suffix on the fly,
    /// so neither the framework nor save state needs to track per-quest reward bodies.
    /// Format: `RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}` — both numeric so the
    /// `%item object {barId} {count} %%` mail token slots in directly.
    internal static string BuildDeepDiveRewardLetterKey(string barId, int count)
        => $"RafiaBee.MoreQuests.DeepDiveReward.{barId}.{count}";

    /// CSV row 54. Daily-board bulk-crop delivery for Pierre's General Store. Picks 3
    /// distinct seasonal crops; player delivers a per-crop quantity that scales with
    /// Farming skill. Reward = `ShopDiscount` on Pierre's `SeedShop` for the matching
    /// seed ids, lasting `SeedShopDiscountDurationDays` in-game days at
    /// `SeedShopDiscountPercent` off.

    /// 1 heart of friendship in vanilla = 250 points. Used as the threshold for
    /// whether an "underground" NPC reacts to the monster-parts trade: at zero
    /// hearts a freshly-met villager is too distant for the consequence dialog to
    /// land without breaking immersion, so we gate on a real relationship existing.
    private const int FriendshipPointsPerHeart = 250;

    /// CSV row 53. Daily-board item delivery. Routed through the `CombatNpcs` pool
    /// (Wizard / Lance / MarlonFay / Mr. Aguar / Jio / Daia / Eli / Mariam) and
    /// asks for a Combat-scaled quantity of one rare monster drop. Reward = one
    /// random gem or artifact, pulled live from `Data/Objects` (Category -2 or
    /// Type "Arch"), with a stack count sized so the headline value clears
    /// `GoldIntermediateBase`. Tier 1 negative consequence routed via `Source:
    /// Static` to Krobus / Dwarf / Sen — but only those at >= 1 heart, and the
    /// quest itself refuses to post if none of the three qualify.
    private static QuestPosting? MonsterParts(QuestContext ctx)
    {
        if (Game1.player.CombatLevel < 2)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatNpcs);
        if (giver == null)
            return null;

        // Quest refuses to post unless at least one underground NPC is at >= 1
        // heart. Re-checked below to build the consequence target list (giver
        // excluded), so the two stay in sync.
        var underground = ResolveUndergroundTargets(exclude: giver);
        if (underground.Count == 0)
            return null;

        // TODO: vanilla monster drops only. Modded drops aren't enumerable without
        // per-mod data, so the pool stays vanilla. Add modded drops manually here
        // when extending mod-pack coverage (e.g. RSV / ESV / SVE custom monsters).
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

    /// Walks `Data/Objects` once and returns a single random (id, count) reward
    /// where `id` is either a gem (Category `Object.GemCategory == -2`) or an
    /// artifact (Type `Arch`), and `count` is sized so `count * sellPrice` clears
    /// `GoldIntermediateBase`. Picks up modded gems and artifacts automatically.
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

    /// Vanilla "rare" monster drops. Modded drops aren't enumerable without per-mod data,
    /// so the pool stays vanilla — the framework's `Data/NPCGiftTastes` consequence path
    /// still picks up modded NPCs who happen to like or hate any of these.

    /// Vanilla "rare" monster drops. Modded drops aren't enumerable without per-mod data,
    /// so the pool stays vanilla — the framework's `Data/NPCGiftTastes` consequence path
    /// still picks up modded NPCs who happen to like or hate any of these.
    private static readonly (string Id, string Name)[] MonsterDropPool =
    {
        ("(O)767", "Bat Wing"),
        ("(O)768", "Solar Essence"),
        ("(O)769", "Void Essence"),
        ("(O)684", "Bug Meat")
    };


    /// Pufferfish carries the Nausea status effect when eaten — the only vanilla "fish"
    /// any reasonable cook would call poisonous. Filtered out of the Seafood Night pool
    /// so the CSV's "edible non-poisonous" framing holds. Modded fish stay in as long as
    /// their Edibility is positive.

    /// Krobus + Dwarf are vanilla; Sen ships with East Scarp. Filters to met NPCs
    /// at >= 1 heart of friendship so the consequence dialog reads as a reaction
    /// from someone the player actually knows. Met-at-0-hearts and never-met both
    /// drop out; missing characters (mod not loaded) also drop out.
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

    // -------------------- Phase 9d: Forage with Linus --------------------

    /// CSV row 27. Daily-board, Linus giver, single-step `GiftUniqueNpcs` objective: gift a
    /// forage-category item that the recipient loves or likes to 5 distinct NPCs. Reward is
    /// `FriendshipLarge` with Linus only — no consequence engine wiring needed since the
    /// gift recipients already get the standard friendship boost from vanilla's gift flow.
    /// Quest gates on Linus being met (no point posting a "deliver gifts on Linus's behalf"
    /// quest before the player has met Linus).

    /// CSV row 52. Daily-board SlayMonster (any monster). Routed through the
    /// `CombatNpcs` dispatch pool (Wizard / Lance / MarlonFay / Mr. Aguar / Jio /
    /// Daia / Eli / Mariam, mod-gated). Reward = `GoldIntermediateBase` + one combat
    /// food sized to the rolled magnitude bucket: +1 when DifficultyScaling is off,
    /// or a random +1 / +2 / +3 when it's on. The combat-food pool is auto-scanned
    /// at save load from every edible item in `Data/Objects` whose `Buffs` grant a
    /// non-zero Attack or Defense; magnitude is `max(Attack, Defense)`.
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

        var pool = ModEntry.Framework?.GetCombatFoodPool() ?? Array.Empty<string>();
        var bucket = new List<string>();
        foreach (string id in pool)
        {
            int? m = ModEntry.GetCombatFoodMagnitude(id);
            if (m.HasValue && m.Value == targetMagnitude)
                bucket.Add(id);
        }
        // Fallback: if the rolled bucket is empty (e.g. no +3 foods in this save's
        // content), drop to the next-lower magnitude that does have entries. Keeps
        // the reward non-null while still honouring the magnitude direction.
        if (bucket.Count == 0)
        {
            for (int m = targetMagnitude - 1; m >= 1 && bucket.Count == 0; m--)
            {
                foreach (string id in pool)
                {
                    int? mag = ModEntry.GetCombatFoodMagnitude(id);
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

    /// Vanilla rare forageables. Modded forage gets folded in via the `forage_item`
    /// context tag so this list is the seed; the resolver appends matching modded ids.

    /// Iridium Bar + vanilla rare gems. The objective rolls one entry; the reward is
    /// independent and can be either Artifact Troves or a stack of the same gem family.
    private static readonly (string Id, string Name)[] RareMaterialPool =
    {
        ("(O)337", "Iridium Bar"),
        ("(O)72", "Diamond"),
        ("(O)64", "Ruby"),
        ("(O)60", "Emerald"),
        ("(O)70", "Jade"),
        ("(O)62", "Aquamarine"),
        ("(O)68", "Topaz"),
        ("(O)66", "Amethyst")
    };

    /// CSV row 63. Daily-board ItemDelivery to Clint. Asks for 1-3 Iridium Bars or 3-5
    /// rare gems. Reward = `GoldAdvancedBase` + either 3 Artifact Troves or 5 random
    /// gems (rolled per-fire). Skill gates on Mining 7 so the quest only surfaces when
    /// the player can plausibly source the requested materials.

    /// CSV row 63. Daily-board ItemDelivery to Clint. Asks for 1-3 Iridium Bars or 3-5
    /// rare gems. Reward = `GoldAdvancedBase` + either 3 Artifact Troves or 5 random
    /// gems (rolled per-fire). Skill gates on Mining 7 so the quest only surfaces when
    /// the player can plausibly source the requested materials.
    private static QuestPosting? RareMaterialRequest(QuestContext ctx)
    {
        if (Game1.player.MiningLevel < 7)
            return null;

        const string giver = "Clint";
        var pick = PickResolved(ctx, RareMaterialPool);
        if (pick == null)
            return null;

        bool isBar = pick.QualifiedItemId == "(O)337";
        int qty = isBar ? Game1.random.Next(1, 4) : Game1.random.Next(3, 6);
        int gold = ctx.Config.GoldAdvancedBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        if (Game1.random.NextDouble() < 0.5)
        {
            // 3 Artifact Troves
            rewards.Add(new ObjectReward("(O)275", 3));
        }
        else
        {
            // 5 of one randomly-picked gem (skip the bar entry from the pool)
            var gem = RareMaterialPool[Game1.random.Next(1, RareMaterialPool.Length)];
            rewards.Add(new ObjectReward(gem.Id, 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.mining.rareMaterial.title"),
            Description = ModEntry.I18n.Get("quest.mining.rareMaterial.description", new { qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.mining.rareMaterial.objective", new { qty, item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.mining.rareMaterial.targetMessage")
        };
    }

    /// CSV row 66. Winter 22 DateLocked. Resolves the player's Winter Star recipient via
    /// `Utility.GetRandomWinterStarParticipant` (the same deterministic random the game
    /// uses to assign secret-santa pairings) and embeds a hint about the recipient's
    /// loved gifts. Player can opt out via `SecretGiftHintEnabled`. Quest is a single
    /// Talk step targeted at the recipient — the festival event surfaces dialogue with
    /// them naturally so the quest closes on Winter 25 without bespoke event hooks.
}
