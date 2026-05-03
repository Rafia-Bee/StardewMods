# More Quests Framework

A SMAPI mod for Stardew Valley that provides a quest engine and a multi-slot help-wanted billboard. Other mods register quests through it; the framework handles generation, posting, rendering, completion, and rewards.

This mod powers [More Quests](../MoreQuests/README.md) and ships four configurable wrappers around the vanilla quest types so you get a working billboard out of the box even with no consumer mod installed.

## What this mod provides

- **Multi-slot billboard.** Replaces vanilla's single "Quest of the Day" with a configurable per-day batch, rendered by `MoreQuestsBillboard`.
- **Paginated SpecialOrders board (opt-in).** Vanilla's SpecialOrders board hardcodes two random slots from the eligible pool, which can hide framework-emitted (and other modded) orders behind a 2/N random pick. Setting `SpecialOrdersBoardPages` to 2 or 3 in config adds prev/next arrows to the board so the player can browse every eligible order, two per page. Default is 1 (vanilla behaviour, patches no-op). No orders are evicted; existing modded SpecialOrders flow through the same `availableSpecialOrders` list and appear naturally in the rotation. Vanilla's accept flow + per-week accept limit are unchanged.
- **Daily generation pipeline.** Samples the registry by weight at `DayStarted`, subject to `MaxPerDay`, `CooldownDays`, per-NPC dedup, friendship-cap dedup, and recent history. Triggered (non-board) quests run as a separate pass.
- **Trigger sources.** `DailyBoard`, `Mail`, `Periodic` (every N days), `DateLocked` (specific date, optionally yearly), `DateRange`, `OneShot` (first time a predicate is true), `BuildingBuilt`, `MailReceived`, `WeatherForecast`, `NpcDialogue`, `SpecialOrder` (writes a configurable `Data/SpecialOrders` entry on the matching `StartDate` for `Duration` days; vanilla owns the accept + objective + reward flow from there), and `CustomBoard` (per-day weighted draw routed to a registered `BoardDefinition`'s slot list, filtered by the board's `AllowedCategories` and capped at its `PoolSize`). Source is independent of delivery channel — every non-board source defaults to mail, overridable via `Trigger.Delivery`. Fire history persists per-save in `MoreQuestsFrameworkState`.
- **Quest factory.** Builds the right `Quest` subclass per posting (`ItemDeliveryQuest`, `FishingQuest`, `SlayMonsterQuest`, `ResourceCollectionQuest`, plus the framework's own multistep `AdventureQuest` and shipping-tracked `MoreQuestsShipQuest`).
- **Declarative rewards.** Each `QuestPosting` carries a `List<RewardSpec>` (Money / Friendship / Object / Recipe / Mail). `RewardApplier.ApplyEncoded` decodes and pays at `questComplete`, so vanilla in-person delivery, Mail Services Mod, and any future channel produce the same payout. Vanilla's hidden bonuses (every-3rd-quest prize ticket, default 150/255 friendship bumps) are suppressed.
- **Custom Quest subclasses.** `MoreQuestsItemDeliveryQuest` / `MoreQuestsFishingQuest` implement `IRewardedQuest` (rewards survive save round-trip) and override vanilla turn-in to actually consume requested items. `MoreQuestsShipQuest` is observed at `DayEnding` against the player's shipping bin. `AdventureQuest` is a multistep substrate: each step ships its own kind (`Deliver`, `Talk`, `Gift`, `GiftUniqueNpcs`, `Ship`, `Catch`, `Slay`, `ReachLevel`; the rest of the planned verb set lands as later phases need it) with `Requires[]` ordering. Every active step sees each event in parallel, so independent steps complete in any order. Adventure JSON also accepts `$giver` (resolves to the giver) and `$dispatcher.<role>[N]` (samples N distinct NPCs from a registered dispatch role) tokens in step `Targets[]`. The Items field also accepts `$forage` (any object with the `forage_item` context tag), `$edible-egg` (any non-inedible egg-category object), and `$category:N` (any object with the given vanilla category constant).
- **Custom boards.** `BoardDefinition` registers a per-location pin-board at a tile of your choosing; `LoadBoardsFromMod(helper, "assets/boards.json")` auto-loads a JSON pack. Quest definitions with `Trigger.Source: "CustomBoard"` are routed to the matching board's slot list each `DayStarted` (filtered by the board's `AllowedCategories`, capped at its `PoolSize`). The framework renders the board sprite + a bobbing "!" indicator in-world, opens a cork-board `CustomBoardMenu` on action-button click, and reuses vanilla `Billboard(true)` as the inner accept-quest popup. No Harmony patches; the world renderer + click handler ride pure SMAPI events.
- **Runtime trigger-source overrides.** `IMoreQuestsModApi.OverrideTriggerSource(definitionId, source)` re-routes an already-registered quest to a different `TriggerSource` without re-registration. Useful for content-mod config toggles that flip a quest between the help-wanted board and a custom board (e.g. "enable Adventurer's Guild board: when off, fall the guild quests back to the help-wanted board so they stay reachable"). The pipeline consults the override before reading `def.Source`, so the flip takes effect on the next daily roll.
- **Reward kinds.** `Money`, `Friendship`, `Object`, `Recipe`, `Mail` (`Today` or `Tomorrow` / `NextDay`), `ShopDiscount` (temporarily reduces prices in `Data/Shops/<ShopId>` by `PercentOff` for `DurationDays`; optional `AppliesTo` whitelist scopes it to specific item ids), and `FestivalBias` (Luau or Fair: bumps the governor's reaction tier on the Luau, capped below the Mayor's Shorts gag, or adds a flat bonus to the Fair grange score). Discounts and biases persist per-save, sweep on `DayStarted` once expired, and re-grants merge into the existing entry instead of stacking.
- **Consequence engine.** Each `QuestPosting` can carry a `ConsequenceSpec` (`Tier1`-`Tier3` + `Special`); `SpecialOrderSpec` carries a list (one entry per dish for Grand-Feast-style multi-dish orders). On `questComplete`, the engine resolves loved/hated NPCs via `Data/NPCGiftTastes` (or a static `Targets[]` for Tier 3 ecology chains), filters to met villagers, samples one NPC per spec across the union, and queues a per-NPC dialogue line + friendship delta. The persistent dialogue queue surfaces lines on the next chat with the affected NPC; Tier 3 chains step `EarliestFireDay` so one line surfaces per day. Built-in handlers (`Tier1` = `±FriendshipBasic`, `Tier2` = loved `+FriendshipBasic` / hated `-(FriendshipBasic+FriendshipMid)/2`, `Tier3` = multi-day chain to ecology NPCs, `Special` = gold loss) can be replaced per-tier through `IMoreQuestsModApi.RegisterConsequenceTier`.
- **Four vanilla wrappers.** `VanillaItemDelivery`, `VanillaResourceCollection`, `VanillaSlayMonster`, `VanillaFishing` expose vanilla quest types as configurable `IQuestDefinition`s with their own GMCM weights.
- **Condition evaluator.** `ConditionEvaluator.Evaluate(dict, modRegistry)` covers Season, Date, DayRange, Weather, FriendshipLevel, MailReceived, EventSeen, SkillLevel, BuildingExists, KnownCookingRecipe, KnownCraftingRecipe, StatAtLeast, ShippedAtLeast, HasItemEverObtained, HasMod, Random, plus `GSQ` (1.6 GameStateQuery escape hatch). Top-level keys AND-combine; `not:` prefix negates; `|` inside a value is OR. See `plan.md §2.6` for the full key list.
- **Game-data cache.** `GameDataCache` reads `Data/Crops`, `Data/Fish`, `Data/Locations`, `Data/CookingRecipes`, `Data/NPCGiftTastes` once per day so generators don't pay the load cost per Build call.
- **Item resolver + NPC dispatch.** `ItemResolver` reads cached game data so modded items surface automatically. `DispatchRegistry` is a runtime, role-keyed picker (saloon chefs, ecology-minded, conservation guides, etc.); the built-in vanilla + RSV/ESV/VMV/SVE seeds register through the same public `RegisterDispatchNpc` API third parties use, and authors can define new role strings on the fly.
- **Custom assets.** Pad and pin sprites for the billboard, loaded from `Mods/RafiaBee.MoreQuestsFramework/Pad` and `.../Pin`.

## Dependencies

**Required**

- **SpaceCore** (`spacechase0.SpaceCore`) — registers custom `Quest` subclasses with the serializer so saves round-trip cleanly.

**Optional**

- **Generic Mod Config Menu** — exposes the framework's tunables and per-quest weights as an in-game config page.

## Configuration

`Mods/MoreQuestsFramework/config.json`. Surfaced through GMCM when installed:

- **Quest board** — `QuestsPerDay`, `AllowDuplicateGiverPerDay`, `SkipFriendshipQuestsAtMaxHeart`.
- **Per-quest weights** — one entry per registered `IQuestDefinition` (built dynamically; consumer mods' quests appear too).
- **Vanilla wrappers** — toggle and tune the four bundled vanilla quest types.
- **Friendship reward sizes** — `FriendshipBasic`, `FriendshipMid`, `FriendshipIntermediate`, `FriendshipLarge`, `FriendshipMultiSmall`, `FriendshipMultiHeart`.
- **Gold reward bases** — beginner / basic / intermediate / advanced / expert tiers.
- **Reward multipliers** — `RewardMultiplierBelowSell`, `RewardMultiplierAboveSell`, `RewardMultiplierFishPremium`.
- **Deadlines** — short / medium / long / extended (in-game days).

GMCM registration is deferred until the first `UpdateTicking` so consumer-mod quests that register during their own `OnGameLaunched` appear in the per-quest weight list.

## Registering quests from another mod

Three entry points, depending on whether your mod is a SMAPI content pack, a C# mod with bundled JSON, or a C# mod with imperative quest definitions.

### A. SMAPI content pack (no code)

Drop a folder under `Mods/` with a `manifest.json` declaring `"ContentPackFor": { "UniqueID": "RafiaBee.MoreQuestsFramework" }` and a `quests.json` next to it. The framework auto-loads every owned content pack at startup. See the working example at [docs/example-pack/](docs/example-pack/).

### B. C# mod with bundled JSON + generators

```csharp
private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    var fw = Helper.ModRegistry.GetApi<MoreQuestsFramework.Api.IMoreQuestsApi>(
        "RafiaBee.MoreQuestsFramework");
    if (fw == null) return;

    fw.RegistrationOpen += (_, _) =>
    {
        var scope = fw.GetModApi(ModManifest);

        scope.RegisterCustomQuestType(typeof(MyCustomQuestSubclass));
        scope.RegisterGenerator("MyQuest", ctx => new QuestPosting { /* ... */ });
        scope.LoadQuestsFromMod(Helper, "assets/quests.json");
        scope.RegisterDispatchNpc(DispatchRoles.SaloonChef, "MyChef", "MyMod.UniqueId");
    };
}
```

`quests.json` carries metadata; the C# generator owns runtime randomization. `RegistrationOpen` fires from the framework's first update tick (after every consumer mod's `GameLaunched` runs), so subscribing inside your own `GameLaunched` is the correct timing.

### C. C# mod with `IQuestDefinition` instances

For mods that prefer pure C# without JSON:

```csharp
scope.RegisterQuest(new MyQuestDefinition());
```

### Schema

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
            "Trigger": { "Source": "DailyBoard", "Weight": 10, "MaxPerDay": 1, "CooldownDays": 14 },
            "Giver": "Lewis",
            "Objective": { "Kind": "Deliver", "Item": "(O)628", "Count": 1 },
            "Title": "{i18n:my.quest.title}",
            "Rewards": [ { "Kind": "Money", "Amount": 500 } ]
        },
        {
            "Name": "MyMod.YearlyHarvestFestival",
            "Trigger": { "Source": "DateLocked", "Date": "fall 14", "RepeatYearly": true },
            "Giver": "Lewis",
            "Objective": { "Kind": "Deliver", "Item": "(O)276", "Count": 5 },
            "Rewards": [ { "Kind": "Money", "Amount": 1000 } ]
        },
        {
            "Name": "MyMod.CheckOnFriend",
            "Trigger": { "Source": "DailyBoard", "Weight": 20, "MaxPerDay": 1, "CooldownDays": 7 },
            "Giver": "Lewis",
            "Steps": [
                { "Name": "GiftFriend", "Kind": "Gift", "Targets": [ "Sebastian" ], "Description": "{i18n:my.checkon.gift}" },
                { "Name": "TalkFriend", "Kind": "Talk", "Targets": [ "Sebastian" ], "Requires": [ "GiftFriend" ], "Description": "{i18n:my.checkon.talk}" },
                { "Name": "Report",     "Kind": "Talk", "Targets": [ "$giver" ],    "Requires": [ "TalkFriend" ], "Description": "{i18n:my.checkon.report}" }
            ],
            "Rewards": [ { "Kind": "Friendship", "Npc": "Lewis", "Points": 80 } ]
        },
        {
            "Name": "MyMod.PreservesOrder",
            "Category": "Seasonal",
            "Trigger": { "Source": "SpecialOrder", "StartDate": "fall 1", "Duration": "Month", "CooldownDays": 21 },
            "Generator": "PreservesOrder"
        }
    ]
}
```

Notes on the schema:

- Each `QuestDef` sets either `Generator` (a name registered via `RegisterGenerator`) or a fully-declarative `Objective` (or `Steps[]` for multistep).
- `{i18n:key}` tokens in any string field resolve through the owning pack's translation helper.
- `Available` accepts every key the framework's `ConditionEvaluator` knows about; `not:` prefix negates; `|` inside a value is OR.
- `Trigger.Delivery: "Mail" | "NpcDialogue" | "DailyBoard"` overrides the channel default if needed.
- Adventure (multistep) quests use `Steps[]` instead of `Objective`. Each step has a `Name` (used by other steps' `Requires[]`), a `Kind`, a `Description`, and step-kind-specific targeting fields. `$giver` in `Targets[]` rewrites to the resolved giver name. `$dispatcher.<role>` resolves to one NPC from the named dispatch role; `$dispatcher.<role>[N]` resolves to N distinct NPCs (clamped to whatever the role's pool yields when smaller). Resolution happens once at quest-creation time, so the picked names are stable across save/reload. Step kinds available now: `Deliver`, `Talk`, `Gift`, `Ship`, `Catch`, `Slay`. Independent steps (no `Requires[]`) are all active simultaneously.
- Single-objective `Ship` quests use `"Objective": { "Kind": "Ship", "Item": "(O)787", "Count": 1 }`. `Item` accepts a single string or an array — when an array, any id satisfies the delivery. Observed against `Game1.getFarm().getShippingBin(player)` at `DayEnding`.
- `MailReward` accepts `"When": "Today"` (default), `"Tomorrow"`, or `"NextDay"` (alias for `Tomorrow`).
- **`SpecialOrder` source.** Trigger fires when `today == StartDate` (`<season> <day>`) and the cooldown has elapsed. The framework writes a vanilla `Data/SpecialOrders` entry (key namespaced as `<ownerUniqueId>.<defId>.<dayStamp>` so other mods' SpecialOrders are never disturbed) for `Duration` days (`OneDay`/`TwoDays`/`ThreeDays`/`Week`/`TwoWeeks`/`Month`). The matching `Generator` returns a `QuestPosting` with `Kind = PostingKind.SpecialOrder` and a populated `SpecialOrder` block (`Name`/`Text`/`Requester`/`Duration`/`Objectives[]`/`Rewards[]`); each `SpecialOrderObjectiveSpec.Type`/`SpecialOrderRewardSpec.Type` is the vanilla type name without the `Objective`/`Reward` suffix (e.g. `Ship`, `Money`, `Friendship`). Vanilla owns accept + objective tracking + reward grant from there.

## Public API

`IMoreQuestsApi` ([Api/IMoreQuestsApi.cs](Api/IMoreQuestsApi.cs)) is the public registration seam, marked Beta until framework v1.0. Fetch it once via `helper.ModRegistry.GetApi<IMoreQuestsApi>(...)`, then narrow to a per-mod scope through `GetModApi(ModManifest)`.

Lifecycle events:

- `RegistrationOpen` / `RegistrationClosed` — bracket the window in which `RegisterQuest` is accepted. The registry freezes after `RegistrationClosed`.
- `QuestAccepted` / `QuestCompleted` / `QuestRemoved` — fired only for framework-managed quests. Each event arg carries `Quest`, `OwnerUniqueId`, `DefinitionId`.
- `DayRefreshed(dailyCount, mailCount)` — fires at end of `OnDayStarted` and from `RefreshOffers()`.

Custom `Quest` subclasses must carry a unique `[XmlType("Mods_<owner>_<name>")]` attribute and be registered via `RegisterCustomQuestType` so SpaceCore's serializer factory knows about them.

### Debug

The framework registers an `mq_refresh` SMAPI console command that re-rolls today's daily-board postings without reloading the save.

## Notes for consumer mods

- Declare `RafiaBee.MoreQuestsFramework` as a `Dependencies` entry with `IsRequired: true` so your mod loads after the framework.
- For shared types (`IQuestDefinition`, `QuestPosting`, `QuestContext`), use a `<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` so the framework DLL isn't copied into the consumer mod's deploy folder. SMAPI's `AssemblyResolve` finds the engine types in the framework's loaded assembly at runtime.

## Project layout

```
MoreQuestsFramework/
  ModEntry.cs                         // hooks events, builds the API, runs the daily pipeline
  Api/                                // public framework + per-mod scope handles
  Registry/QuestRegistry.cs           // runtime registry with Register/WithKind/Freeze/Clear
  Pipeline/                           // daily + triggered generation orchestrator and poster
  Posting/QuestFactory.cs             // builds the vanilla Quest subclass for a posting
  Conditions/ConditionEvaluator.cs    // dictionary-driven IsAvailable helpers
  Cache/GameDataCache.cs              // per-day cache of Data/Crops, Data/Fish, etc.
  Rewards/                            // RewardSpec/Codec/Applier + IRewardedQuest marker
  Patches/BillboardPatches.cs         // Harmony patches that reroute the vanilla billboard
  MoreQuestsBillboard.cs              // custom multi-slot billboard UI
  Quests/                             // MoreQuests* subclasses + Vanilla/ wrappers
  Dispatch/                           // role -> NPC entries with public registration
  Config/MoreQuestsFrameworkConfig.cs // engine tunables
  i18n/                               // engine config + reward summary + per-quest weight labels
  assets/                             // pad + pin textures
  manifest.json
```
