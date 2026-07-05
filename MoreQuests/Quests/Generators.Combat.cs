using System;
using System.Collections.Generic;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewValley;

namespace MoreQuests.Quests;

internal static partial class Generators
{
    /// The 12 Mines reward floors (multiples of 10). Mine is only offered as a location once
    /// the player has reached floor 120, so the elevator can ride straight to any of these.
    private const int MineFloorCount = 12;

    /// Edibility band for valid offerings: spoiled/cursed food the critters will "eat".
    /// Excludes the -300 inedible sentinel (raw wood/stone/ore) and ordinary near-zero foods.
    private const int OfferingEdibilityMin = -200;
    private const int OfferingEdibilityMax = -10;

    /// Step item filter: any object in the edibility band counts, so the player can drop any
    /// such item (vanilla or modded) rather than one fixed item. Matched by the framework's
    /// $edibility token (see AdventureQuest.TokenMatches).
    private static readonly string OfferingItemToken = $"$edibility:{OfferingEdibilityMin}:{OfferingEdibilityMax}";

    /// Glow Ring reward for "Clear the floor". Fixed item per the quest design.
    private const string GlowRingItemId = "(O)517";

    /// Width of the floor band for "Clear the floor". The player slays monsters on any floor in
    /// [start, start + width].
    private const int ClearFloorBandWidth = 5;

    /// "Clear the floor": slay a batch of monsters on a random run of floors, in the Mines or
    /// (once unlocked) the Skull Cavern. The band sits inside what the player has actually
    /// reached, and Skull Cavern respects the deepest-floor config. Posts to the Adventurer's
    /// Guild board (falls back to help-wanted when that's off) from a CombatNpcs giver.
    /// Reward: a Glow Ring.
    private static QuestPosting? ClearTheFloor(QuestContext ctx)
    {
        if (Game1.player.deepestMineLevel < 1)
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.CombatNpcs);
        if (giver == null)
            return null;

        // Skull Cavern only enters the pool once it's unlocked (deepest past floor 120).
        bool skullUnlocked = Game1.player.deepestMineLevel > 120;
        bool useSkull = skullUnlocked && Game1.random.Next(2) == 0;

        int low, high, minFloor, maxFloor;
        string locationLabel;
        if (useSkull)
        {
            int reachable = Math.Min(Game1.player.deepestMineLevel - 120,
                Math.Max(1, ModEntry.Config.SkullCavernMaxLevel));
            int start = Game1.random.Next(1, Math.Max(1, reachable - ClearFloorBandWidth) + 1);
            low = start;
            high = Math.Min(reachable, start + ClearFloorBandWidth);
            minFloor = 120 + low;
            maxFloor = 120 + high;
            locationLabel = ModEntry.I18n.Get("quest.combat.clearFloor.loc.skullCavern");
        }
        else
        {
            int reachable = Math.Min(120, Game1.player.deepestMineLevel);
            int start = Game1.random.Next(1, Math.Max(1, reachable - ClearFloorBandWidth) + 1);
            low = start;
            high = Math.Min(reachable, start + ClearFloorBandWidth);
            minFloor = low;
            maxFloor = high;
            locationLabel = ModEntry.I18n.Get("quest.combat.clearFloor.loc.mines");
        }

        int qty = Difficulty.Scaled(ctx,
            () => Math.Clamp(Game1.random.Next(2, Math.Max(3, 2 * Game1.player.CombatLevel)), 2, 20),
            () => Game1.random.Next(2, 6));

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "SlayInBand",
                Kind = AdventureStepKind.Slay,
                Count = qty,
                MinFloor = minFloor,
                MaxFloor = maxFloor,
                Description = ModEntry.I18n.Get("quest.combat.clearFloor.objective",
                    new { qty, low, high, location = locationLabel })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(GlowRingItemId) },
            Title = ModEntry.I18n.Get("quest.combat.clearFloor.title"),
            Description = ModEntry.I18n.Get("quest.combat.clearFloor.description",
                new { npc = giver, qty, low, high, location = locationLabel }),
            PreBuiltQuest = quest
        };
    }

    /// "The Unseen Offering": collect inedible, spoiled-food items and leave them inside a
    /// ritual circle a magic-themed NPC has marked, to appease the entities that share the
    /// valley. Posts to the Adventurer's Guild board (Mining category); falls back to
    /// help-wanted when the guild board is off. Reward: a combat +2 food in the mail next day.
    private static QuestPosting? UnseenOffering(QuestContext ctx)
    {
        if (Game1.player.CombatLevel < 5)
            return null;
        // willyBoatFixed is the real Ginger Island unlock flag (Visit_Island isn't a thing).
        if (!Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed"))
            return null;

        string? giver = ctx.Dispatch.Pick(DispatchRoles.MagicianNPCs);
        if (giver == null)
            return null;

        var locs = new List<string> { "Forest", "Mountain", "Backwoods" };
        bool mineAvailable = Game1.player.deepestMineLevel >= 120;
        if (mineAvailable) locs.Add("Mine");
        if (Game1.MasterPlayer.mailReceived.Contains("ccVault")) locs.Add("Desert");

        string locName = locs[Game1.random.Next(locs.Count)];

        int radius;
        Microsoft.Xna.Framework.Vector2 center = Microsoft.Xna.Framework.Vector2.Zero;
        string targetName = locName;
        int mineFloor = 0;
        if (locName == "Mine")
        {
            mineFloor = (1 + Game1.random.Next(MineFloorCount)) * 10;
            targetName = $"UndergroundMine{mineFloor}";
            radius = Math.Clamp(ModEntry.Config.MagicianDropRadius, 2, 3);
            // Center left at (0,0); the overlay resolves it lazily on the live mine floor.
        }
        else
        {
            var loc = Game1.getLocationFromName(locName);
            if (loc == null)
                return null;
            radius = Math.Clamp(ModEntry.Config.MagicianDropRadius, 2, 6);
            if (!DropZonePicker_TryPickOverground(loc, radius, out center))
                return null;
        }

        if (!OfferingPoolHasItems(ctx))
            return null;

        int qty = Difficulty.Scaled(ctx,
            () => 2 + Game1.random.Next(2, Math.Max(2, (int)(Game1.player.CombatLevel * 1.5)) + 1),
            () => Game1.random.Next(1, 7));

        string? foodId = PickCombatFood(ctx, preferredMagnitude: 2);
        if (foodId == null)
            return null;
        int rewardCount = Game1.random.Next(1, 5);
        string rawFoodId = foodId.StartsWith("(O)", StringComparison.Ordinal) ? foodId.Substring(3) : foodId;
        string letterKey = $"{ModEntry.UnseenOfferingRewardKeyPrefix}{rawFoodId}.{rewardCount}";

        string locLabel = OfferingLocationLabel(locName, mineFloor);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "LeaveOffering",
                Kind = AdventureStepKind.DropItemsInRadius,
                Targets = new List<string> { targetName },
                Items = new List<string> { OfferingItemToken },
                Count = qty,
                CenterX = (int)center.X,
                CenterY = (int)center.Y,
                Radius = radius,
                Description = ModEntry.I18n.Get("quest.combat.unseenOffering.step",
                    new { count = qty, location = locLabel })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Mining,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MailReward(letterKey, MailWhen.Tomorrow) },
            Title = ModEntry.I18n.Get("quest.combat.unseenOffering.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.combat.unseenOffering.description",
                new { npc = giver, count = qty, location = locLabel }),
            PreBuiltQuest = quest
        };
    }

    /// Wraps the framework picker for overground zones (resolved on accept). Min distance 5
    /// from warps so the circle doesn't land on a doorway or building porch.
    private static bool DropZonePicker_TryPickOverground(GameLocation loc, int radius, out Microsoft.Xna.Framework.Vector2 center)
    {
        center = Microsoft.Xna.Framework.Vector2.Zero;
        if (!DropZonePicker.TryPickZone(loc, radius, minDistFromWarp: 5, attempts: 200, out var point))
            return false;
        center = new Microsoft.Xna.Framework.Vector2(point.X, point.Y);
        return true;
    }

    /// True if at least one offering item exists: a non-blacklisted object whose Edibility is
    /// in the band (spoiled/cursed food the critters will eat). Vanilla always has a few
    /// (Void Mayonnaise, Pufferfish, Red Mushroom, etc.); modded items in the band count too.
    /// The quest accepts any of them via the $edibility token, so this is just a sanity gate.
    private static bool OfferingPoolHasItems(QuestContext ctx)
    {
        var data = ctx.Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.Objects.ObjectData>>("Data/Objects");
        foreach (var (rawId, obj) in data)
        {
            if (obj == null)
                continue;
            if (obj.Edibility < OfferingEdibilityMin || obj.Edibility > OfferingEdibilityMax)
                continue;
            if (QuestItemBlacklist.IsExcluded("(O)" + rawId))
                continue;
            return true;
        }
        return false;
    }

    /// Picks a combat-buff food id at the preferred Attack/Defense magnitude, falling back to
    /// the nearest available magnitude (higher first, then lower) so the quest still posts.
    private static string? PickCombatFood(QuestContext ctx, int preferredMagnitude)
    {
        var fw = ModEntry.Framework;
        var pool = fw?.GetCombatFoodPool() ?? Array.Empty<string>();
        if (pool.Count == 0)
            return null;

        foreach (int magnitude in new[] { preferredMagnitude, 3, 2, 1 })
        {
            var bucket = new List<string>();
            foreach (string id in pool)
            {
                int? m = fw?.GetCombatFoodMagnitude(id);
                if (m.HasValue && m.Value == magnitude)
                    bucket.Add(id);
            }
            if (bucket.Count > 0)
                return bucket[Game1.random.Next(bucket.Count)];
        }
        return null;
    }

    /// Player-facing location name for the quest text. Overground maps get a friendly i18n
    /// name; the Mines name includes the target floor so the player knows where to ride to.
    private static string OfferingLocationLabel(string locName, int mineFloor)
    {
        if (locName == "Mine")
            return ModEntry.I18n.Get("quest.combat.unseenOffering.loc.mine", new { floor = mineFloor });
        return ModEntry.I18n.Get($"quest.combat.unseenOffering.loc.{locName.ToLowerInvariant()}");
    }
}
