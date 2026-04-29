# More Quests Framework

A SMAPI mod for Stardew Valley that provides a quest engine and a multi-slot help-wanted billboard. Other mods register quests through it; the framework handles generation, posting, rendering, completion, and rewards.

This mod is the engine that powers [More Quests](../MoreQuests/README.md). It also ships four configurable wrappers around the vanilla quest types so you get a working out-of-the-box billboard even without any consumer mod installed.

## What this mod provides

- **Multi-slot billboard.** Replaces vanilla's single "Quest of the Day" with a configurable per-day batch. Custom UI (`MoreQuestsBillboard`) renders all slots; `BillboardPatches` reroutes vanilla's billboard click + draw paths.
- **Daily generation pipeline.** At `DayStarted`, samples from the registry by weight (subject to `MaxPerDay`, `CooldownDays`, per-NPC dedup, friendship-cap dedup, and a recent-history filter) to fill up to `QuestsPerDay` slots, then runs a separate pass for `PostingKind.Mail` quests.
- **Quest factory.** Builds the right vanilla `Quest` subclass for each posting (`ItemDeliveryQuest`, `FishingQuest`, `SlayMonsterQuest`, `ResourceCollectionQuest`) and stamps a unique ID so external trackers attribute the quest correctly.
- **Declarative reward block.** Each `QuestPosting` carries a `List<RewardSpec>` (Money / Friendship / Object / Recipe / Mail). The poster routes Money into vanilla's `Quest.moneyReward` and encodes the rest into a `NetStringList` on the quest; `RewardApplier.ApplyEncoded` decodes and pays at `questComplete`, so vanilla in-person delivery, Mail Services Mod, and any future delivery channel produce the same payout. Vanilla's hidden bonuses (every-3rd-quest prize ticket, default 150/255 friendship bumps) are suppressed so every reward is explicit.
- **Custom Quest subclasses.** `MoreQuestsItemDeliveryQuest` and `MoreQuestsFishingQuest` implement `IRewardedQuest` (a `serializedRewards` NetStringList that survives save round-trip) and override the vanilla turn-in logic to actually consume the requested items (vanilla's `FishingQuest.OnNpcSocialized` doesn't reduce the stack — you could sell every fish and still claim the reward).
- **Four vanilla wrappers.** `VanillaItemDelivery`, `VanillaResourceCollection`, `VanillaSlayMonster`, `VanillaFishing` expose vanilla quest types as configurable `IQuestDefinition`s with their own GMCM weights, so the billboard has content even with no consumer mod installed.
- **Condition evaluator + game-data cache.** `ConditionEvaluator.Evaluate(dict, modRegistry)` covers every key in plan.md §2.6: `Season`, `Date`, `DayRange`, `DaysOfWeek`, `Year`, `Weather`, `WeatherForecast`, `FriendshipLevel`, `FriendshipStatus`, `MailReceived`, `EventSeen`, `MinDaysPlayed`, `MaxDaysPlayed`, `IsPlayerMarried`, `IsMultiplayer`, `IsCommunityCenterCompleted`, `SkillLevel`, `BuildingExists`, `KnownCookingRecipe`, `KnownCraftingRecipe`, `StatAtLeast`, `ShippedAtLeast`, `HasItemEverObtained`, `HasMod`, `Random`, plus `GSQ` (the 1.6 GameStateQuery escape hatch). Top-level keys are AND-combined; `not:` prefix negates; `|` inside a value is the OR-combinator. `GameDataCache` reads `Data/Crops`, `Data/Fish`, `Data/Locations`, `Data/CookingRecipes`, `Data/NPCGiftTastes` once per day so quest generators don't pay the load cost per Build call.
- **Item resolver + NPC dispatch.** `ItemResolver` reads the cached game data so modded items surface automatically. `NpcDispatch` is a role-based picker (saloon chefs, ecology-minded, conservation guides, etc.) that includes vanilla and modded NPCs and falls back gracefully when a referenced NPC mod is missing.
- **Custom assets.** Pad and pin sprites for the billboard, loaded from `Mods/RafiaBee.MoreQuestsFramework/Pad` and `.../Pin`.

## Dependencies

**Required**

- **SpaceCore** (`spacechase0.SpaceCore`) — used to register custom `Quest` subclasses with the serializer so saves round-trip cleanly. The framework hard-depends on SpaceCore.

**Optional**

- **Generic Mod Config Menu** (`spacechase0.GenericModConfigMenu`) — exposes the framework's tunables and per-quest weights as an in-game config page.

## Configuration

`Mods/MoreQuestsFramework/config.json`. Surfaced through GMCM when installed. Settings include:

- **Quest board** — `QuestsPerDay`, `AllowDuplicateGiverPerDay`, `SkipFriendshipQuestsAtMaxHeart`.
- **Per-quest weights** — one entry per registered `IQuestDefinition` (built dynamically; consumer mods' quests appear here too).
- **Vanilla wrappers** — toggle and tune the four bundled vanilla quest types.
- **Friendship reward sizes** — `FriendshipBasic`, `FriendshipMid`, `FriendshipIntermediate`, `FriendshipLarge`, `FriendshipMultiSmall`, `FriendshipMultiHeart`.
- **Gold reward bases** — beginner / basic / intermediate / advanced / expert tiers.
- **Reward multipliers** — `RewardMultiplierBelowSell`, `RewardMultiplierAboveSell`, `RewardMultiplierFishPremium`.
- **Deadlines** — short / medium / long / extended (in-game days).

GMCM registration is deferred until the first `UpdateTicking` so consumer-mod quests that register during their own `OnGameLaunched` appear in the per-quest weight list.

## Registering quests from another mod

There are three entry points, depending on whether your mod is a SMAPI content pack, a C# mod with bundled JSON, or a C# mod with imperative quest definitions.

### A. SMAPI content pack (no code)

Drop a folder under `Mods/` with a `manifest.json` declaring `"ContentPackFor": { "UniqueID": "RafiaBee.MoreQuestsFramework" }` and a `quests.json` next to it. The framework auto-loads every owned content pack at startup. See the working example at [docs/example-pack/](docs/example-pack/).

### B. C# mod with bundled JSON + generators

```csharp
private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    var fw = Helper.ModRegistry.GetApi<MoreQuestsFramework.Api.IInternalApi>(
        "RafiaBee.MoreQuestsFramework");
    if (fw == null) return;

    // Custom Quest subclasses (must register before quests.json loads).
    fw.RegisterCustomQuestType(typeof(MyCustomQuestSubclass));

    // Named C# generators referenced by quests.json `"Generator": "<name>"`.
    fw.RegisterGenerator(ModManifest, "MyQuest", ctx => new QuestPosting { /* ... */ });

    // Load the JSON pack bundled inside this mod's folder.
    fw.LoadQuestsFromMod(Helper, ModManifest, "assets/quests.json");
}
```

This is the pattern our own `RafiaBee.MoreQuests` content mod uses. `quests.json` carries metadata (Id / Category / Trigger / Available); the C# generator owns runtime randomization.

### C. C# mod with `IQuestDefinition` instances

For mods that prefer pure C# without JSON, the original `RegisterQuest` API still works:

```csharp
fw.RegisterQuest(new MyQuestDefinition());  // implements IQuestDefinition
```

### Schema

`quests.json` shape (Phase 4 subset):

```jsonc
{
    "Schema": "1.0",
    "Quests": [
        {
            "Name": "MyMod.MyQuest",
            "Category": "Farming",
            "Trigger": {
                "Source": "DailyBoard",
                "Weight": 30,
                "MaxPerDay": 1,
                "CooldownDays": 7,
                "Available": { "Season": "spring|fall", "NpcMet": "Lewis" }
            },
            "Generator": "MyQuest"
        },
        {
            "Name": "MyMod.StaticQuest",
            "Category": "Foraging",
            "Tier": "Beginner",
            "Trigger": { "Source": "DailyBoard", "Weight": 10, "MaxPerDay": 1, "CooldownDays": 14 },
            "Giver": "Lewis",
            "Objective": { "Kind": "Deliver", "Item": "(O)628", "Count": 1 },
            "Title": "{i18n:my.quest.title}",
            "Rewards": [ { "Kind": "Money", "Amount": 500 } ]
        }
    ]
}
```

Each `QuestDef` must set either `Generator` (a name registered via `RegisterGenerator`) or a fully-declarative `Objective`. `{i18n:key}` tokens in any string field resolve through the owning pack's translation helper. `Available` accepts every key the framework's `ConditionEvaluator` knows about (Season, NpcExists, NpcMet, MinDeepestMineLevel, SkillLevel, FriendshipLevel, HasMod, GSQ, etc. — see `plan.md §2.6`); `not:` prefix negates; `|` inside a value is OR.

`IInternalApi` is the current registration seam ([Api/IInternalApi.cs](Api/IInternalApi.cs)). It's deliberately minimal until the public `IMoreQuestsApi` lands in Phase 5 (see `docs/plan.md` in the content mod). Consumer mods need a hard SMAPI dependency on `RafiaBee.MoreQuestsFramework` and either a project reference to this assembly or their own copy of the `IQuestDefinition` / `QuestPosting` / `QuestContext` shapes.

### Debug / iteration

The framework registers an `mq_refresh` SMAPI console command that re-rolls today's daily-board postings without reloading the save. Useful while tuning new quests.

## Project layout

```
MoreQuestsFramework/
  ModEntry.cs                         // hooks events, builds the API, runs the daily pipeline
  Api/
    IInternalApi.cs                   // current registration interface for consumer mods
    InternalApi.cs                    // public top-level concrete impl (top-level required by SMAPI)
  Registry/QuestRegistry.cs           // runtime registry with Register/WithKind/Freeze/Clear
  Pipeline/
    QuestPipeline.cs                  // daily + triggered generation orchestrator
    QuestPoster.cs                    // routes postings to the board / mail / asset edits
  Posting/QuestFactory.cs             // builds the vanilla Quest subclass for a posting
  Conditions/ConditionEvaluator.cs    // dictionary-driven IsAvailable helpers
  Cache/GameDataCache.cs              // per-day cache of Data/Crops, Data/Fish, etc.
  Rewards/
    RewardSpec.cs                     // declarative reward records (Money/Friendship/Object/Recipe/Mail)
    RewardCodec.cs                    // encodes specs into single-line NetStringList entries
    RewardApplier.cs                  // single funnel that decodes + pays at questComplete
    IRewardedQuest.cs                 // marker interface custom Quest subclasses implement
  Patches/BillboardPatches.cs         // Harmony patches that reroute the vanilla billboard
  MoreQuestsBillboard.cs              // custom multi-slot billboard UI
  BillboardSlots.cs                   // model behind the billboard UI
  Quests/
    MoreQuestsItemDeliveryQuest.cs    // explicit-rewards subclass with NetFields
    MoreQuestsFishingQuest.cs         // overrides vanilla turn-in to actually consume fish
    Vanilla/                          // four configurable wrappers around vanilla quest types
  IQuestDefinition.cs                 // interface for individual quest generators
  QuestPosting.cs                     // shared DTO + PostingKind / BoardQuestType / consequences
  QuestContext.cs                     // helpers passed to each definition's Build
  ItemResolver.cs                     // resolves items via the cached game data
  NpcDispatch.cs                      // role-based quest-giver picker (mod aware)
  AntiRepetition.cs                   // recent-item / NPC / definition history
  ModCompat.cs                        // mod IDs + IsLoaded helpers
  Difficulty.cs                       // tier + deadline + skill mapping
  ISpaceCoreApi.cs                    // SpaceCore interface
  IGenericModConfigMenuApi.cs         // GMCM interface
  Config/MoreQuestsFrameworkConfig.cs // engine tunables
  i18n/                               // engine config + reward summary + per-quest weight labels
  assets/                             // pad + pin textures
  manifest.json
```

## Notes for consumer mods

- The framework is loaded by SMAPI as its own mod. Consumer mods should declare `RafiaBee.MoreQuestsFramework` as a `Dependencies` entry with `IsRequired: true` so they load after the framework.
- For shared types (`IQuestDefinition`, `QuestPosting`, `QuestContext`), use a `<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` so the framework DLL isn't copied into the consumer mod's deploy folder. SMAPI's `AssemblyResolve` finds the engine types in the framework's loaded assembly at runtime.
- Custom `Quest` subclasses must carry a unique `[XmlType("Mods_<owner>_<name>")]` attribute. Register them via `fw.RegisterCustomQuestType(typeof(...))` so SpaceCore's serializer factory knows about them.
