# More Quests Framework

A SMAPI mod for Stardew Valley that provides a quest engine and a roomier help-wanted billboard. Other mods register quests through it. The framework handles generation, posting, rendering, completion, and rewards.

This mod powers [More Quests](../MoreQuests/README.md) and ships four configurable wrappers around the vanilla quest types so you get a working billboard out of the box even with no other mod installed.

## What this mod provides

- **Multi-slot billboard.** Replaces vanilla's single "Quest of the Day" with a configurable per-day batch, rendered by `MoreQuestsBillboard`.
- **Paginated Special Orders board (opt-in).** If you play a heavily modded game, chances are the special orders list is quite long and it'll take a lot of in-game years to do even *see* all the quests. This config lets you see up to 5 pages in the special orders board so you get a bigger selection of orders. Set `SpecialOrdersBoardPages` to anything from 2 to 5 in config and the board grows prev/next arrows so you can browse every eligible order, two per page. Default is 1 (vanilla behavior, patches no-op). You still can only pick 1 order, I didn't change anything related to the special order code itself.
- **Daily generation pipeline.** Samples the registry by weight at `DayStarted`, subject to `MaxPerDay`, cooldowns, per-NPC dedup, friendship-cap dedup, and recent history. This is *only* for the Help Wanted billboard (daily board) quests.
- **Trigger sources.**
  - `DailyBoard`, weighted draw from the daily pool.
  - `Mail`, older path kept around for compatibility. Use `Periodic` instead if you want a quest that fires every N days.
  - `Periodic`, every N in-game days.
  - `DateLocked`, a specific date, optionally yearly.
  - `DateRange`, every day inside a closed date range.
  - `OneShot`, fires once per save when the `When` predicate first comes true.
  - `BuildingBuilt`, the morning after a given farm building finishes construction (with optional `DayDelay`).
  - `MailReceived`, the day a given mail flag enters the player's received list (with optional `DayDelay`).
  - `WeatherForecast`, when tomorrow's weather matches. Handy for rainy-day mail that arrives the night before.
  - `NpcDialogue`, queues the posting until the player next speaks to the named NPC.
  - `SpecialOrder`, writes a `Data/SpecialOrders` entry on the matching `StartDate` for `Duration` days. Vanilla owns the accept and tracking flow from there.
  - `CustomBoard`, per-day weighted draw routed to a registered `BoardDefinition`'s slot list, filtered by the board's `AllowedCategories` and capped at its `PoolSize`.
  - `Custom`, escape hatch for consumer-mod trigger sources. The quest's `Trigger.Custom` field is the handler id registered through `IMoreQuestsModApi.RegisterCustomTrigger`. The framework respects the definition's `CooldownDays` first (so the handler isn't even asked while the cooldown is active), then calls the handler at DayStarted to decide whether the trigger fires today. Bare names resolve under the calling mod's UniqueID; pass `"OtherMod.UniqueID/Name"` to reference another mod's handler. Example:

    ```csharp
    scope.RegisterCustomTrigger("PlayerHasAxeT4", ctx =>
    {
        // fires once after the player has bought the gold axe.
        return Game1.player.toolBeingUpgraded?.Value == null
            && Game1.player.Items.Any(i => i is StardewValley.Tools.Axe a && a.UpgradeLevel >= 3);
    });
    ```

    Then in JSON: `"Trigger": { "Source": "Custom", "Custom": "PlayerHasAxeT4", "CooldownDays": 28 }`. A Custom trigger whose handler isn't registered (e.g. the consumer mod is uninstalled) silently never fires.

  The trigger and the delivery method are picked separately. The trigger says *when* the quest fires, and the delivery says *how* it reaches the player (mail letter, next NPC chat, daily board slot, etc.). For example, a `DateLocked` quest set to Winter 12 doesn't have to arrive as mail, you can set `Trigger.Delivery` to `NpcDialogue` and it'll instead wait until you talk to the giver. Non-board sources default to mail when `Delivery` isn't set. Fire history is saved per-save in `MoreQuestsFrameworkState`.
- **Quest factory.** Builds the right `Quest` subclass per posting (`ItemDeliveryQuest`, `FishingQuest`, `SlayMonsterQuest`, `ResourceCollectionQuest`, plus the framework's own multi-step `AdventureQuest` and shipping-tracked `MoreQuestsShipQuest`).
- **Declarative rewards.** Each `QuestPosting` carries a `List<RewardSpec>`. `RewardApplier.ApplyEncoded` decodes and pays them at `questComplete`, so vanilla in-person delivery, Mail Services Mod, and any future channel all produce the same payout. Vanilla's default 150/255 friendship bump on completion is suppressed so the declarative rewards are the only source. The every-3rd-quest prize ticket and milestone mail flags are kept (we mark accepted billboard quests with `dailyQuest = true` so those side-effects still fire).
- **Custom Quest subclasses.** `MoreQuestsItemDeliveryQuest` and `MoreQuestsFishingQuest` implement `IRewardedQuest` (so rewards survive save round-trip) and override vanilla turn-in to actually consume requested items. `MoreQuestsShipQuest` is observed at `DayEnding` against the player's shipping bin. `AdventureQuest` is the base for any quest with more than one step. You give it a list of steps and each step has its own kind (`Deliver`, `Talk`, `Gift`, `GiftUniqueNpcs`, `Ship`, `Catch`, `Slay`, `ReachLevel`, `Visit`, `Build`, `Plant`, `Collect`, `ClearWeeds`, `ClearDebris`, `Custom`) and uses `Requires[]` for ordering. Every active step sees each event in parallel, so independent steps can complete in any order. Adventure JSON also accepts `$giver` (resolves to the giver) and `$dispatcher.<role>[N]` (samples N distinct NPCs from a registered dispatch role) tokens in step `Targets[]`. The `Items` field also accepts `$forage` (any object tagged `forage_item`), `$edible-egg` (any non-inedible egg-category object), and `$category:N` (any object with that vanilla category constant).
- **Custom boards.** `BoardDefinition` registers a per-location pin-board at a tile of your choosing. `LoadBoardsFromMod(helper, "assets/boards.json")` auto-loads a JSON pack. Quest definitions with `Trigger.Source: "CustomBoard"` route to the matching board's slot list each `DayStarted` (filtered by the board's `AllowedCategories`, capped at its `PoolSize`). The framework renders the board sprite plus a bobbing "!" indicator in-world, opens a cork-board `CustomBoardMenu` on action-button click, and reuses vanilla `Billboard(true)` as the inner accept-quest popup.
- **Runtime trigger-source overrides.** `IMoreQuestsModApi.OverrideTriggerSource(definitionId, source)` re-routes an already-registered quest to a different `TriggerSource` without re-registration. Useful for content-mod toggles that flip a quest between the help-wanted board and a custom board (e.g. guild board on -> off, falls the guild quests back to the help-wanted board so they stay reachable). The pipeline checks the override before reading `def.Source`, so the flip takes effect after you go to sleep. Overrides called during the registration window that target a quest its owner hasn't registered yet are buffered and replayed when the owner registers, so load order between consumer mods doesn't matter as long as both register before `RegistrationClosed`.
- **Runtime unregister.** `IMoreQuestsModApi.Unregister(definitionId)` removes a quest definition from the registry. Useful for "disable this quest" config toggles when re-routing isn't enough. Allowed both before and after the registry freezes. Quests already in the player's journal keep working (their state lives on the Quest instance, not the registry); the def just stops being a draw candidate on the next `DayStarted`. The id is global, any mod can unregister any quest, same posture as `OverrideTriggerSource`.
- **Registry introspection.** `IMoreQuestsApi.RegisteredQuestIds()` returns a snapshot of every registered quest id in registration order. `IMoreQuestsApi.GetQuestInfo(definitionId)` returns a small `QuestInfo` record (`Id`, `OwnerUniqueId`, `Category`, `Kind`, `Source`, `EffectiveSource`) or null when the id is unknown. `IMoreQuestsApi.IsQuestAvailable(definitionId)` runs the def's `IsAvailable(ctx)` check against the current save and returns `true` / `false`, or `null` when the id is unknown or no save is loaded. Useful for consumer-mod debug menus, quest browsers, or GMCM helpers that need to enumerate what's registered or preview which quests would qualify today. Heads up: built-in JSON conditions read live `Game1.*` state, so `IsQuestAvailable` reports what would qualify *right now*, not a hypothetical scenario.
- **Reward kinds.**
  - `Money`, gold dropped into the player's wallet at completion.
  - `Friendship`, a flat friendship-point change for a named NPC.
  - `Object`, a stack of one item id.
  - `Recipe`, a cooking or crafting recipe.
  - `Mail` with `Today` or `Tomorrow` (alias `NextDay`).
  - `ShopDiscount`, lowers prices in `Data/Shops/<ShopId>` by `PercentOff` for `DurationDays`. Optional `AppliesTo` whitelist scopes it to specific item ids. Optional `GuaranteedStock` force-adds any whitelisted item the shop doesn't normally carry.
  - `AnimalPurchaseDiscount`, the same concept but applied to every `Data/FarmAnimals` purchase price. Should be compatible with mods like Livestock Bazaar that patch onto the animal purchase UI, but needs testing.
  - `FestivalBias`, a one-shot bias on the Luau governor reaction or the Fair grange judging score.
  - `FairStarTokens`, adds tokens to the player's `festivalScore` at the start of the vanilla Stardew Valley Fair.
  - `Custom`, escape hatch for consumer-mod reward kinds. The reward's `Custom` field is the handler id registered via `IMoreQuestsModApi.RegisterCustomReward`; `Payload` is an arbitrary string the handler interprets (anything, the framework just round-trips it). Example:

    ```csharp
    scope.RegisterCustomReward(
        "GrantPetSlimeFollower",
        payload => MySlimeMod.AttachFollower(payload),
        summarize: (payload, giver, t) =>
            t.Get("quest.reward.line.slimeFollower", new { color = payload, npc = giver })
                .Default($"{giver} will introduce you to a {payload} slime").ToString());
    ```

    Then in JSON: `{ "Kind": "Custom", "Custom": "GrantPetSlimeFollower", "Payload": "blue" }`. Bare handler names resolve under the calling mod's UniqueID; pass `"OtherMod.UniqueID/Name"` for cross-mod references. A Custom reward with no registered handler is a no-op (no payout, no journal line).

  Discounts and biases persist per save, sweep off on `DayStarted` once expired, and a re-grant merges into the existing entry instead of stacking.
- **Consequence engine.** Each `QuestPosting` can carry a `ConsequenceSpec` (`Tier1` to `Tier3`, or `Special`). `SpecialOrderSpec` carries a list (e.g. one entry per dish for Grand-Feast-style multi-dish orders). On `questComplete`, the engine resolves loved/hated NPCs via `Data/NPCGiftTastes` (or a static `Targets[]` for Tier 3 ecology chains), filters to met villagers, samples one NPC per side (one loved plus one hated for `GiftTastes` source, every static target for `Static` source), and queues a per-NPC dialogue line plus friendship delta. The persistent dialogue queue adds lines on the next chat with the affected NPC, capped at one pop per NPC per in-game day. Tier 3 chains step `EarliestFireDay` so one line shows up per day. If the player doesn't chat with the designated npc that day (or for a number of days), the queue drops earlier stale lines and shows the most recent elligible line so the narration stays immersive (I tried my best). Entries past `ConsequenceGraceDays` (default 7 days after the quest starts) silently expire on `DayStarted`, so if you avoid the affected NPC for the whole week, you won't face the consequence. Built-in handlers (`Tier1` = `±FriendshipBasic`, `Tier2` = loved `+FriendshipBasic` / hated `-(FriendshipBasic+FriendshipMid)/2`, `Tier3` = multi-day chain to ecology NPCs, `Special` = gold loss) can be replaced per-tier through `IMoreQuestsModApi.RegisterConsequenceTier`.
- **Four vanilla wrappers.** `VanillaItemDelivery`, `VanillaResourceCollection`, `VanillaSlayMonster`, `VanillaFishing` expose vanilla quest types as configurable `IQuestDefinition`s with their own GMCM weights. (Just turn them off, my quests are better :>)
- **Condition evaluator.** Powers the JSON `Available { ... }` block. Every key in the block has to be true for the quest to post. Stick `not:` in front of a key to flip it. Use `|` inside a value to mean "any of these". Supported keys (case-insensitive):

  | Key | Notes | Examples |
  | --- | --- | --- |
  | `Season` | One of `spring` / `summer` / `fall` / `winter`. Space-separated list works too. | `"spring"`, `"spring fall"`, `"spring\|fall"` |
  | `Date` | `<season> <day>`, or a closed range like `winter 22-25`. | `"winter 12"`, `"winter 22-25"` |
  | `DayRange` | Day numbers inside the current season. | `"1-7"`, `"21-28"` |
  | `DaysOfWeek` | Full name (`Monday`), short (`Mon`), or number 0-6 (0 = Sunday). Space-separated for multiple. | `"Mon Wed Fri"`, `"Saturday Sunday"` |
  | `Year` | Year number, true when the current year is at or above this. | `"2"`, `"3"` |
  | `Weather` | Today's weather. | `"Rain"`, `"Sun"`, `"Sun\|Wind"` |
  | `WeatherForecast` | Tomorrow's weather. | `"Rain"`, `"Storm"` |
  | `MinDaysPlayed` / `MaxDaysPlayed` | Total in-game days played. | `"28"`, `"112"` |
  | `IsPlayerMarried` / `IsMultiplayer` / `IsCommunityCenterCompleted` | True/false flags. | `"true"`, `"false"` |
  | `SkillLevel` | `<Skill> <minLevel>`. Skill is one of Farming/Fishing/Mining/Foraging/Combat/Cooking. | `"Farming 4"`, `"Fishing 6"` |
  | `MinDeepestMineLevel` | Lowest mine floor the player has reached. | `"40"`, `"120"` |
  | `NpcExists` / `NpcMet` | NPC is loaded in the save / has been spoken to. Space-separated is AND, `\|` is OR. | `"Caroline"`, `"George Evelyn"`, `"Maru\|Sebastian"` |
  | `FriendshipLevel` | `<NPC> <minHearts>`. | `"Caroline 1"`, `"Leah 8"` |
  | `FriendshipStatus` | `<NPC> <status>`. Status is `dating` / `engaged` / `married` / `roommate` / `divorced`. | `"Abigail dating"`, `"Shane married"` |
  | `MailReceived` / `EventSeen` | A specific mail flag is set / event id has been seen. | `"Visit_Island"`, `"60367"` |
  | `BuildingExists` | Farm has the named building. | `"Coop"`, `"Deluxe Barn"`, `"Silo"` |
  | `KnownCookingRecipe` / `KnownCraftingRecipe` | The player learned the recipe. | `"Pizza"`, `"Preserves Jar"` |
  | `StatAtLeast` | `<statName> <minCount>`, reads from `Game1.stats`. | `"ChickenEggsLayed 1"`, `"MonstersKilled 50"` |
  | `ShippedAtLeast` | `<itemId> <minCount>` (qualified `(O)174` or bare `174`). | `"(O)174 10"`, `"184 1"` |
  | `HasItemEverObtained` | The item has been shipped or cooked at least once on this save. | `"176"`, `"305"` |
  | `HasMod` | A mod with the given UniqueID is installed. | `"PathosChild.SkipIntro"`, `"Rafseazz.RidgesideVillage"` |
  | `FollowingAnimalCount` | Animals currently following the player (uses the Livestock Follows You bridge). | `"1"`, `"2"` |
  | `Random` | A 0.0 to 1.0 chance gate rolled each evaluation. | `"0.25"`, `"0.5"` |
  | `GSQ` | A 1.6 `GameStateQuery` string, for anything the keys above don't cover. | `"PLAYER_HAS_PROFESSION Current Rancher"` |

  Consumer mods can add their own condition keys with `IMoreQuestsModApi.RegisterCustomCondition(key, evaluator)`. The evaluator gets the raw value string and returns true / false; the `not:` prefix and `|` OR alternatives are applied above the evaluator, so they keep working for free. Pick a key name that won't collide with built-ins or other mods (prefixing with the mod's short name, e.g. `MyMod_HasFollower`, is a safe bet). Example:

  ```csharp
  scope.RegisterCustomCondition("MyMod_HasFollower", value =>
  {
      // value is the JSON string after the key. Parse however you like.
      return MyMod.Followers.Contains(value);
  });
  ```

  Then in JSON: `"Available": { "MyMod_HasFollower": "Junimo" }` or `"Available": { "not:MyMod_HasFollower": "Krobus" }`.

- **Game-data cache.** `GameDataCache` reads `Data/Crops`, `Data/Fish`, `Data/Locations`, `Data/CookingRecipes`, `Data/NPCGiftTastes` once per day so generators don't pay the load cost on every Build call.
- **Item resolver and NPC dispatch.** `ItemResolver` reads cached game data so modded items show up automatically. `DispatchRegistry` is a runtime role-keyed picker (saloon chefs, ecology-minded, conservation guides, etc.). The built-in vanilla and RSV / ESV / VMV / SVE seeds register through the same public `RegisterDispatchNpc` API that third parties use, and mod authors can define new role strings on the fly.
- **Custom assets.** Pad and pin sprites for the billboard, loaded from `Mods/RafiaBee.MoreQuestsFramework/Pad` and `.../Pin` (resprite of Aedenthorn's Help Wanted pad and pin!).

## For Mod Authors: registering quests from another mod

There are three entry points, depending on whether your mod is a SMAPI content pack, a C# mod with bundled JSON, or a C# mod with imperative quest definitions.

### A. SMAPI content pack (no code)

Drop a folder under `Mods/` with a `manifest.json` declaring `"ContentPackFor": { "UniqueID": "RafiaBee.MoreQuestsFramework" }` and a `quests.json` with it. The framework auto-loads every owned content pack at startup. See the working example at [docs/example-pack/](docs/example-pack/).

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

`quests.json` carries metadata, the C# generator owns runtime randomization. `RegistrationOpen` fires from the framework's first update tick (after every consumer mod's `GameLaunched` runs), so subscribing inside your own `GameLaunched` is the right timing. See the working example at [docs/example-csharp-generators/](docs/example-csharp-generators/).

### C. C# mod with `IQuestDefinition` instances

For mods that prefer pure C# without JSON:

```csharp
scope.RegisterQuest(new MyQuestDefinition());
```

See the working example at [docs/example-csharp-iquestdef/](docs/example-csharp-iquestdef/).

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

### Schema notes

#### The basics

- Each `QuestDef` sets one of: `Generator` (a name registered via `RegisterGenerator`), a declarative `Objective` (single-step), or `Steps[]` (multi-step Adventure quest).
- `{i18n:key}` tokens work in any string field. They resolve through the owning pack's translation helper.
- List fields (`Requires`, `Targets`, `Items`, `Objective.Item`, `RewardDef.AppliesTo`, `Consequence.Targets`) accept either a single string or an array. `"Targets": "Sebastian"` and `"Targets": [ "Sebastian" ]` mean the same thing.

#### The `Available` block

Accepts every key from the condition evaluator (see the table above).

- `not:` prefix negates a key.
- `|` inside a value is OR.
- Values can be strings, plain numbers, or bools: `"MinDaysPlayed": 28` and `"MinDaysPlayed": "28"` both work, same for `"IsPlayerMarried": true`.

#### Delivery

`Trigger.Delivery` overrides the default channel. Accepted values: `"Mail"`, `"NpcDialogue"`, `"DailyBoard"`.

`NpcDialogue` needs a `Giver` set on the quest. The framework can't queue a chat-time post if it doesn't know who the player has to speak to, so a `NpcDialogue` posting with no giver is dropped with a Warn line.

#### Adventure quests (multi-step)

Use `Steps[]` instead of `Objective`. Each step has:

- `Name`, referenced by other steps' `Requires[]`.
- `Kind`, one of: `Deliver`, `Talk`, `Gift`, `GiftUniqueNpcs`, `Ship`, `Catch`, `Slay`, `Visit`, `Build`, `ReachLevel`, `Plant`, `Collect`, `ClearWeeds`, `ClearDebris`, `Custom`.
- `Description`, the journal line.
- Step-kind-specific targeting fields (`Targets[]`, `Items[]`, `Count`, etc.).

Independent steps (no `Requires[]`) are all active at once. Tokens you can use in `Targets[]`:

- `$giver` rewrites to the resolved giver name.
- `$dispatcher.<role>` resolves to one NPC from a named dispatch role.
- `$dispatcher.<role>[N]` resolves to N distinct NPCs (clamped to the pool size).

NPC resolution happens once at quest-creation time, so the picked names are stable across save/reload.

#### Single-objective `Ship`

```jsonc
"Objective": { "Kind": "Ship", "Item": "(O)787", "Count": 1 }
```

`Item` accepts a string or an array; in array form, any listed id satisfies the delivery. Observed against `Game1.getFarm().getShippingBin(player)` at `DayEnding`.

The bin sweep is observe-only: shipped items still sell at full price AND credit the quest, so a "ship 50 stone" quest pays the player the stone sell value on top of the quest reward. Price your rewards with that in mind.

#### Single-objective `Custom`

```jsonc
"Objective": { "Kind": "Custom", "Custom": "<handler>" }
```

The handler is registered via `IMoreQuestsModApi.RegisterCustomBoardQuestType`. It receives a `CustomBoardQuestContext` (definition id, owner, giver, primary / alternative item ids, count, quality gate, target message, deadline) and returns a `Quest` instance. The framework applies title / description / money / reward encoding the same way it does for built-in board kinds.

Bare handler names resolve under the calling mod's UniqueID; `"OtherMod.UniqueID/Name"` works for cross-mod references. A Custom-kind posting whose handler isn't registered (consumer mod uninstalled) drops with the same Warn line as any other failed Build.

#### Optional quest-level fields (single-step quests)

| Field | Type | What it does |
| --- | --- | --- |
| `MailBody` | string, supports `{i18n:key}` | Overrides the auto-generated letter body when the quest is delivered via mail. On Adventure quests it lives at the same `QuestDef` level. Null/empty = use the default body. |
| `DeliveryTarget` | string | For `Deliver` quests where the requester (`Giver`) isn't the NPC who accepts the hand-off (anonymous gift orders). Empty = use `Giver`. |
| `AllowDecorShipping` | bool | Single-step `Ship` quests only. Lifts vanilla's furniture/decor shipping ban while the quest is active. For Adventure quests, set this on the individual `Ship` step instead. |

#### Optional fishing fields (`Objective.Kind` = `Fish` / `Catch`)

| Field | Type | What it does |
| --- | --- | --- |
| `MaxSize` | int (inches) | Upper bound on catch size. 0 = no cap. |
| `AnyFish` | bool | Counter-only mode. Any catch passing the location/size/weather filters counts toward the quota; no specific stack needed at turn-in. |
| `ProgressTemplate` | string, supports `{i18n:key}` | Replaces vanilla's `"0/5 Frog caught"` progress label for `AnyFish` quests. `{0}` is the current count, `{1}` is the quota. |

#### Reward fields per kind

Everything in the "Reward kinds" list above is reachable from a content pack.

| Kind | Required fields | Optional fields |
| --- | --- | --- |
| `Money` | `Amount` | |
| `Friendship` | `Npc`, `Points` | |
| `Object` | `Item`, `Count` | |
| `Recipe` | `Recipe` | `RecipeKind` (`Cooking` or `Crafting`, default `Cooking`) |
| `Mail` | `Letter` | `When` (`"Today"` default / `"Tomorrow"` / `"NextDay"` alias for `Tomorrow`) |
| `ShopDiscount` | `ShopId`, `PercentOff`, `DurationDays` | `AppliesTo` (string or list), `GuaranteedStock` |
| `AnimalPurchaseDiscount` | `PercentOff`, `DurationDays` | |
| `FestivalBias` | `Festival` (`Luau` or `Fair`), `Magnitude` | |
| `FairStarTokens` | `Amount` | |
| `Custom` | `Custom` (handler id) | `Payload` (string the handler unpacks however it wants) |

#### Consequence block

JSON quests can attach a `Consequence` block on the quest.

| Field | Applies to | What it does |
| --- | --- | --- |
| `Tier` | All | `Tier1` / `Tier2` / `Tier3` / `Special`. Omit (or set `Tier0`) to skip. |
| `Source` | All | `GiftTastes` (scan `Data/NPCGiftTastes` for loved/hated picks on `Subject`) or `Static` (use `Targets[]` verbatim). |
| `Subject` | `GiftTastes` source | Item id used for the gift-tastes scan. Ignored when `Source = Static`. |
| `Targets` | All | String or array. Appended to the resolved set for `GiftTastes`, the full affected set for `Static`. |
| `GoldDelta` | `Special` tier only | Negative numbers take gold away from the player. |
| `FriendshipOverride` | All | Replaces the tier's default friendship change. |
| `FriendshipPerDay` | `Tier3` only | Per-chain-day delta, used verbatim with no division. |
| `ChainDays` | `Tier3` only | Defaults to 3 when 0. |
| `LovedLine` / `HatedLine` | All | Dialogue text queued for affected NPCs. `{i18n:key}` tokens resolve. |
| `ChainLinesByDay` | `Tier3` only | One line per chain day, in order (first entry = day 1, second = day 2, etc.). |

#### `SpecialOrder` source

Trigger fires when `today == StartDate` (`<season> <day>`) and the cooldown has elapsed.

The framework writes a vanilla `Data/SpecialOrders` entry (key namespaced as `<ownerUniqueId>.<defId>.<dayStamp>` so other mods' orders are never disturbed) for `Duration` days (`OneDay` / `TwoDays` / `ThreeDays` / `Week` / `TwoWeeks` / `Month`).

The matching `Generator` returns a `QuestPosting` with `Kind = PostingKind.SpecialOrder` and a populated `SpecialOrder` block (`Name` / `Text` / `Requester` / `Duration` / `Objectives[]` / `Rewards[]`). Each `SpecialOrderObjectiveSpec.Type` / `SpecialOrderRewardSpec.Type` is the vanilla type name without the `Objective` / `Reward` suffix (e.g. `Ship`, `Money`, `Friendship`).

Vanilla owns accept, objective tracking, and reward grant from there. See [docs/example-csharp-generators/](docs/example-csharp-generators/) for a working JSON + generator pair.

## Public API

`IMoreQuestsApi` ([Api/IMoreQuestsApi.cs](Api/IMoreQuestsApi.cs)) is the public registration seam, marked Beta until framework v1.0. Fetch it once via `helper.ModRegistry.GetApi<IMoreQuestsApi>(...)`, then narrow to a per-mod scope through `GetModApi(ModManifest)`.

Lifecycle events:

- `RegistrationOpen` / `RegistrationClosed`, bracket the window in which `RegisterQuest` is accepted. Both fire on the framework's first update tick (one tick past every consumer mod's `GameLaunched`). Open handlers run inline in subscription order, then content packs auto-load, then Closed fires in the same tick. Subscribe to `Open` when you're registering your own quests, generators, or dispatch NPCs. Subscribe to `Closed` when you want to read what other mods registered (e.g. a quest browser or GMCM helper that calls `RegisteredQuestIds()`), since the registry is frozen by then and everyone else's Open handlers have already run.
- `QuestAccepted` / `QuestCompleted` / `QuestRemoved`, fired only for framework-managed quests. Each event arg carries `Quest`, `OwnerUniqueId`, `DefinitionId`. `QuestRemovedArgs` also carries `Reason` (`Completed` / `Expired` / `Cancelled`) so reward trackers can tell a deadline timeout apart from a player cancel. `WasCompleted` is kept as a convenience getter for the `Reason == Completed` case.
- `DayRefreshed(dailyCount, mailCount)`, fires at the end of `OnDayStarted` and from `RefreshOffers()`.

Custom `Quest` subclasses must carry a unique `[XmlType("Mods_<owner>_<name>")]` attribute and be registered via `RegisterCustomQuestType` so SpaceCore's serializer factory knows about them.

Mail-delivered postings whose `PreBuiltQuest` is a custom `Quest` subclass also need a mail-stash codec, otherwise the quest is lost if the player saves before opening the letter. The framework ships codecs for its own `AdventureQuest` and `MoreQuestsShipQuest`; consumer mods register their own:

```csharp
scope.RegisterMailStashCodec(
    kind: "MyMod.MyQuestSubclass",
    questType: typeof(MyQuest),
    encode: q => new List<string> { /* serialise variable NetField state */ },
    decode: payload => new MyQuest { /* rebuild from payload */ });
```

`kind` is a stable string id stored alongside the stash, don't rename it after release. The framework re-applies title, description, daysLeft, and the standard reward/consequence wiring on top of whatever `decode` returns, so the codec only has to cover its own extra fields. Subclasses with no codec still post; they just log a Warn at stash time and vanish on reload.

### Quality-aware Deliver

`ObjectiveDef.MinQuality` and `AdventureStep.MinQuality` gate item acceptance on `Object.Quality >= MinQuality` for `MoreQuestsItemDeliveryQuest` and AdventureQuest `Deliver` steps. Quality 0 = base (any quality accepted), 1 = silver, 2 = gold, 4 = iridium (vanilla skips 3). Non-Object items (rings, weapons, etc.) fail any non-zero gate. Quest descriptions render the requirement on the content side, the framework only enforces.

`ObjectiveDef.LocationName` / `MinSize` / `Weather` extend single-objective `Fishing` quests with the same filter set the `Catch` step exposes. Set them on a `QuestPosting` (or via the JSON `Objective.{LocationName,MinSize,Weather}` fields) and the catch only credits when the player is at the named location, the catch's reported size (inches) clears the threshold, and the runtime weather matches. Weather alias and `Rain ∋ Storm` rules mirror the `Catch` step. Empty or zero values disable each gate independently.

### AdventureQuest step kinds

Multi-step Adventure quests carry a `Steps[]` list. Each entry has a `Kind` driving how the step advances:

- `Deliver` / `Talk` / `Gift` / `GiftUniqueNpcs`, vanilla `OnItemOfferedToNpc` and `OnNpcSocialized` virtuals. `Targets[]` = NPC names, `Items[]` = accepted item ids (or `$`-prefixed predicates: `$edible-egg`, `$category:N`, `$tag:<contextTag>`, `$forage` (alias for `$tag:forage_item`)).
- `Catch`, the `OnFishCaught` virtual. `Items[]` = accepted fish ids. Optional `LocationName`, `MinSize`, `Weather` filters gate the catch on current location, reported size in inches (size -1 always fails non-zero gates), and runtime weather (`Sun` / `Rain` / `Storm` / `Snow` / `Wind`. `Rain` matches both Rain and Storm).
- `Slay`, the `OnMonsterSlain` virtual. `Targets[]` = monster type names.
- `Ship`, DayEnding shipping-bin observer. `Items[]` filter, `Count` = stack to credit. Set `AllowDecorShipping: true` on the step to bypass vanilla's furniture / decor shipping ban while the parent quest is in the active log.
- `ReachLevel`, DayStarted plus `Player.Warped` poll of `deepestMineLevel`. `Targets[0]` = `Mine` or `SkullCavern`, `Count` = floor.
- `Visit`, `Player.Warped` observer. `Targets[0]` = location name. `Items[]` accepts `$follower-count:N` so quests can require N animals following the player (uses the Livestock Follows You bridge when present).
- `Build`, DayStarted diff against the previous day's farm-building snapshot. `Targets[0]` = building type.
- `Plant`, `World.TerrainFeatureListChanged` filtered to Tree. `Targets[0]` = location, `Count` = trees planted.
- `ClearWeeds`, `World.ObjectListChanged` removed list filtered by `IsWeeds()`. `Targets[0]` = location, `Count` = weeds cleared.
- `ClearDebris`, per-second poll of `location.resourceClumps`. `Targets[0]` = location, `Count` = clumps removed.
- `Collect`, `Player.InventoryChanged` additions for matching item ids.
- `Custom`, escape hatch for consumer-mod step handlers. `Targets[0]` is the handler id registered through `IMoreQuestsModApi.RegisterCustomAdventureStep`. The framework polls the handler once per second while the step is active; the handler reads `CustomStepContext` and returns an `int` delta to credit against `Step.Progress` (returning enough to reach `Count` marks the step Done). Bare names resolve under the calling mod's UniqueID; pass `"OtherMod.UniqueID/Name"` to reference another mod's handler. Example:

  ```csharp
  scope.RegisterCustomAdventureStep("WateredCropsToday", ctx =>
  {
      // award one tick the first time the player has watered N crops today.
      int watered = Game1.stats.Get("cropsWateredToday");
      return watered >= ctx.Count ? ctx.Count - ctx.Progress : 0;
  });
  ```

  Then in JSON: `{ "Name": "WaterCrops", "Kind": "Custom", "Targets": ["WateredCropsToday"], "Count": 10, "Description": "{i18n:my.step.water}" }`. A Custom step whose handler isn't registered (e.g. the consumer mod is uninstalled mid-save) sits idle, the framework doesn't bomb the save.

  When polling isn't a fit (you want to credit progress off a specific game event instead of checking a counter every second), use `GetActiveCustomSteps(handlerName)` instead. The call returns every active Custom step whose `Targets[0]` resolves to that handler id; push progress into each handle from your event handler or Harmony patch. You don't have to call `RegisterCustomAdventureStep` if you're only using the push path. Example:

  ```csharp
  Helper.Events.World.NpcListChanged += (_, e) =>
  {
      if (!e.Removed.Any(npc => npc is GreenSlime { Name: "Mega Slime" })) return;
      foreach (var handle in scope.GetActiveCustomSteps("MegaSlimeDown"))
          handle.AddProgress(1);
  };
  ```

  Handles are short-lived snapshots: re-query each event tick rather than caching a handle across days, and check `handle.IsActive` if there's any chance the underlying quest changed between query and write.

Step ordering is enforced by `Requires[]` (other step `Name`s that must be Done before the step becomes active). `$giver` in `Targets[]` rewrites to the resolved giver at quest-creation time. None of the step observers add Harmony patches, every kind rides an existing SMAPI event or a framework-owned tick.

### Decor-shipping bypass

This is mainly for peeps who don't use [Ship Anything](https://www.nexusmods.com/stardewvalley/mods/3782) mod.
My festival supply quests often want the player to ship items vanilla refuses to accept (Hay Bales, Wood Lamp-posts, table furniture, custom decor). Set `AllowDecorShipping = true` on a `QuestPosting` (single-step Ship quests) or any `AdventureStep` of `Kind: Ship` (multi-step Adventure quests) and the framework lifts the ban for as long as the quest is active. Implemented as a gated postfix on `Object.canBeShipped`. The override is scoped to the specific item ids each opted-in quest or step declared, so unrelated decor stays blocked from the bin. The postfix recomputes its predicate list whenever a managed quest is accepted, completed, or removed. Off-quest sessions pay one int compare.

### Debug

The framework registers an `mq_refresh` SMAPI console command that re-rolls today's daily-board postings without reloading the save.

### Versioning

The framework follows semver from 1.0 onward.

- **Major** bumps when the public API in `Api/` changes in a backwards-incompatible way, when the `quests.json` schema breaks, or when a built-in reward / objective / trigger kind is renamed or removed. Consumer mods should expect to update.
- **Minor** bumps when new public API, new schema fields, new reward / objective / trigger kinds, or new built-in conditions land. Consumer mods written against the previous minor keep working.
- **Patch** bumps for bug fixes, performance work, or internal refactors with no API-visible change.

The `quests.json` `Schema` field (currently `"1.0"`) is the source of truth for what JSON keys mean. Bumping the framework's major bumps this number; the loader warns when a pack declares a schema it doesn't recognize (rather than refusing the pack) so authors can see the mismatch and update at their own pace.

## Dependencies

**Required**

- **SpaceCore** (`spacechase0.SpaceCore`), registers custom `Quest` subclasses with the serializer so saves round-trip cleanly.

**Optional**

- **Generic Mod Config Menu**

## Configuration

`Mods/MoreQuestsFramework/config.json`. Shows up in GMCM when installed:

- **Quest board:** `QuestsPerDay`, `SpecialOrdersBoardPages`, `AllowDuplicateGiverPerDay`, `SkipFriendshipQuestsAtMaxHeart`.
- **Master toggles:** `DifficultyScaling`, `FishingIgnoresVisitedLocations`, `ForagingIgnoresVisitedLocations`.
- **Per-quest weights:** one entry per registered `IQuestDefinition` (built at runtime, so consumer mods' quests show up too).
- **Vanilla wrappers:** toggle and tune the four bundled vanilla quest types.
- **Friendship reward sizes:** `FriendshipBasic`, `FriendshipMid`, `FriendshipIntermediate`, `FriendshipLarge`, `FriendshipMultiSmall`, `FriendshipMultiHeart`.
- **Gold reward bases:** beginner / basic / intermediate / advanced / expert tiers.
- **Reward multipliers:** `RewardMultiplierBelowSell`, `RewardMultiplierAboveSell`, `RewardMultiplierFishPremium`.
- **Deadlines:** short / medium / long / extended / none (in-game days).
- **Consequences:** `ConsequenceGraceDays` (days past a queued reaction's fire day before it silently expires, default 7). Controls how long an NPC keeps a queued reaction alive when the player avoids them (i.e. how long Demetrious keeps a grudge when you catch too many fish).

GMCM registration is deferred until the first `UpdateTicking` so consumer-mod quests that register during their own `GameLaunched` show up in the per-quest weight list.

## See also

- [docs/integration.md](docs/integration.md) covers the smaller integration caveats: how to declare the dependency, how to share types without copying the DLL, how the framework interacts with `Game1.questOfTheDay`, and how mods that deliver items on the player's behalf (Mail Services Mod, etc.) should call into the framework so gift-step quests still tick.

## Credits

**ConcernedApe** for Stardew Valley.

**Pathoschild** for **[SMAPI](https://www.nexusmods.com/stardewvalley/mods/2400)**.

**spacechase0** for **[SpaceCore](https://www.nexusmods.com/stardewvalley/mods/1348)** and **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)**.

**aedenthorn** for **[Help Wanted](https://www.nexusmods.com/stardewvalley/mods/14640)**, the inspiration for this framework's vanilla quest tuning, and the source the pad and pin sprites were retextured from.

**SiTheGreat1** for **[Si's Extra Crafting Materials](https://www.nexusmods.com/stardewvalley/mods/25467)**

Npcs from these mods added so much variety to my quest giver pools:
- **Rafseazz** for **[Ridgeside Village](https://www.nexusmods.com/stardewvalley/mods/7286)**.
- **lemurkat** for **[East Scarp](https://www.nexusmods.com/stardewvalley/mods/5787)**
- **Lumisteria** for **[Visit Mount Vapius](https://www.nexusmods.com/stardewvalley/mods/9600)**
- **FlashShifter** for **[Stardew Valley Expanded](https://www.nexusmods.com/stardewvalley/mods/3753)**
- **TenebrousNova** for **[Eli and Dylan - Custom NPCs for East Scarp](https://www.nexusmods.com/stardewvalley/mods/13883)**
- **NassilLove** for **[Arumi the Actress](https://www.nexusmods.com/stardewvalley/mods/44286)**
- **7thAxis** for **[Lurking in the Dark - NPC Sen (East Scarp)](https://www.nexusmods.com/stardewvalley/mods/10770)**
- **MadDog** for **[Gunnar from Bear Family Custom NPCs - An Add-On for East Scarp](https://www.nexusmods.com/stardewvalley/mods/16197)**

Finally **the Stardew Valley modding community** for tuning feedback on the custom quests and for the modded NPC info that seeded the framework's NPC dispatch pools.
