using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ExampleCSharpGenerators;

/// Pattern B: C# mod that registers JSON quest metadata plus C# generator functions.
///
/// JSON (in `assets/quests.json`) carries the static fields: trigger source, weight,
/// cooldown, category, `Available` conditions, deadline tier. The C# generator named
/// in the JSON's `Generator` field runs at trigger time and fills in the dynamic
/// parts (random giver, random in-season crop, scaled quantity, sell-price-based gold).
///
/// This example also shows the three "escape hatch" registrations that need C# code
/// but can be referenced from JSON: `RegisterCustomTrigger` (a custom trigger source),
/// `RegisterCustomReward` (a custom reward kind), and a `SpecialOrder` generator.
public sealed class ModEntry : Mod
{
    private const string MqfQuestsAsset = "Mods/RafiaBee.MoreQuestsFramework/Quests";
    private static readonly Regex I18nToken = new(@"\{i18n:([^}]+)\}", RegexOptions.Compiled);

    private QuestPackDocument? _cachedQuests;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = this.Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            this.Monitor.Log("More Quests Framework not loaded. Quests disabled.", LogLevel.Warn);
            return;
        }

        // The framework fires RegistrationOpen on its own first update tick (after every
        // consumer mod's GameLaunched runs), so subscribing in our GameLaunched is the
        // right timing. The framework reads the Quests asset shortly after.
        fw.RegistrationOpen += (_, _) =>
        {
            var scope = fw.GetModApi(this.ModManifest);

            // Daily-board generator. JSON entry references this name in its `Generator` field.
            scope.RegisterGenerator("RandomCropDelivery", this.BuildRandomCropDelivery);

            // SpecialOrder generator. Trigger.Source = "SpecialOrder" routes through this
            // and the returned QuestPosting carries a SpecialOrderSpec instead of an
            // Objective. Vanilla owns accept, objective tracking, and reward grant from there.
            scope.RegisterGenerator("MarnieEggDrive", this.BuildMarnieEggDrive);

            // Custom trigger source. The handler is called once per DayStarted; the framework
            // fires the quest the first day it returns true (subject to the def's CooldownDays).
            scope.RegisterCustomTrigger("PlayerOwnsStardrop", _ =>
                Game1.player.maxStamina.Value > 270);

            // Custom reward kind. `apply` runs at questComplete with the raw payload string.
            // `summarize` is optional and renders the reward preview line in the journal.
            scope.RegisterCustomReward(
                "RestoreHealthAndStamina",
                _ =>
                {
                    Game1.player.health = Game1.player.maxHealth;
                    Game1.player.Stamina = Game1.player.MaxStamina;
                },
                summarize: (_, giver, t) =>
                    t.Get("example.customReward.summary", new { npc = giver })
                        .Default($"{giver} will help you rest up").ToString());
        };
    }

    /// Injects this mod's quest definitions into the framework's Quests asset. CP packs
    /// from other authors layer their edits on top at the same priority. The dict key
    /// is the registered quest id, so each entry gets a stable namespaced id derived
    /// from this mod's UniqueID + the quest's short name.
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo(MqfQuestsAsset))
            return;
        e.Edit(asset =>
        {
            var dict = asset.AsDictionary<string, QuestDef>().Data;
            foreach (var def in this.LoadQuestsCached().Quests)
            {
                if (string.IsNullOrWhiteSpace(def.Name))
                    continue;
                // i18n tokens get resolved against this mod's own ITranslationHelper here,
                // since the framework no longer expands {i18n:key} on its behalf.
                this.ResolveI18nTokens(def);
                def.Owner = this.ModManifest.UniqueID;
                dict[def.Name] = def;
            }
        }, AssetEditPriority.Early);
    }

    private QuestPackDocument LoadQuestsCached()
        => this._cachedQuests ??= this.Helper.Data.ReadJsonFile<QuestPackDocument>("assets/quests.json") ?? new QuestPackDocument();

    /// Walks a QuestDef and rewrites every {i18n:key} token to the localized string.
    /// Keeps the JSON shape, lets translators contribute via i18n/<locale>.json the
    /// same way they would in any SMAPI mod.
    private void ResolveI18nTokens(QuestDef def)
    {
        def.Title = this.Resolve(def.Title);
        def.Description = this.Resolve(def.Description);
        def.CurrentObjective = this.Resolve(def.CurrentObjective);
        def.TargetMessage = this.Resolve(def.TargetMessage);
        def.MailBody = this.Resolve(def.MailBody);
        if (def.Objective != null)
            def.Objective.ProgressTemplate = this.Resolve(def.Objective.ProgressTemplate);
        if (def.Steps != null)
        {
            foreach (var step in def.Steps)
                step.Description = this.Resolve(step.Description);
        }
    }

    private string? Resolve(string? input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("{i18n:"))
            return input;
        return I18nToken.Replace(input, m =>
        {
            string key = m.Groups[1].Value.Trim();
            var t = this.Helper.Translation.Get(key);
            return t.HasValue() ? t.ToString() : m.Value;
        });
    }

    /// Generator: a random met villager wants a random in-season crop delivered.
    /// Quantity scales with Farming. Gold reward scales with the crop's sell price.
    /// Returning null cancels this attempt (e.g. no in-season crops, no met NPCs).
    private QuestPosting? BuildRandomCropDelivery(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        var crop = crops[Game1.random.Next(crops.Count)];

        var villagers = ctx.Dispatch.ResolvePool("MetAdultHumans");
        if (villagers.Count == 0)
            return null;
        string giver = villagers[Game1.random.Next(villagers.Count)];

        int qty = Game1.player.FarmingLevel + Game1.random.Next(2, 6);
        int gold = (int)(crop.SellPrice * qty * 1.05f);

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = ctx.Config.DeadlineShort,
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = this.Helper.Translation.Get("example.cropDelivery.title", new { npc = giver }),
            Description = this.Helper.Translation.Get("example.cropDelivery.description", new { npc = giver, qty, item = crop.DisplayName }),
            CurrentObjective = this.Helper.Translation.Get("example.cropDelivery.objective", new { npc = giver, qty, item = crop.DisplayName }),
            TargetMessage = this.Helper.Translation.Get("example.cropDelivery.targetMessage")
        };
    }

    /// Generator for a SpecialOrder: Marnie asks the farmer to ship 20 eggs (any kind,
    /// modded eggs included via the `egg_item` context tag). Money is paid via vanilla's
    /// reward path so the standard reward-box UI shows it; friendship rides the framework
    /// path so third-party Special Order tweak packs can't intercept it.
    private QuestPosting? BuildMarnieEggDrive(QuestContext ctx)
    {
        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Custom,
            Kind = PostingKind.SpecialOrder,
            QuestGiver = "Marnie",
            SpecialOrder = new SpecialOrderSpec
            {
                Name = this.Helper.Translation.Get("example.specialOrder.title"),
                Text = this.Helper.Translation.Get("example.specialOrder.text"),
                Requester = "Marnie",
                Duration = "Week",
                Objectives = new List<SpecialOrderObjectiveSpec>
                {
                    new()
                    {
                        Type = "Ship",
                        Text = this.Helper.Translation.Get("example.specialOrder.objective"),
                        RequiredCount = 20,
                        Data = { ["AcceptedContextTags"] = "egg_item" }
                    }
                },
                Rewards = new List<SpecialOrderRewardSpec>
                {
                    new()
                    {
                        Type = "Money",
                        Data = { ["Amount"] = "5000" }
                    }
                },
                FrameworkRewards = new List<RewardSpec>
                {
                    new FriendshipReward("Marnie", 80)
                }
            }
        };
    }
}
