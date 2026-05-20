# Example Content Pack (CP-style)

A Content Patcher pack that adds quests to the More Quests Framework via standard CP edits. No C# code, just `content.json` and translation strings.

For the C# patterns, see [`../example-csharp-generators/`](../example-csharp-generators/) (C# + JSON, injected via `IAssetRequested.Edit`) and [`../example-csharp-iquestdef/`](../example-csharp-iquestdef/) (pure C# using `RegisterQuest`).

## What it ships

- `manifest.json`. Declares the pack as `ContentPackFor: Pathoschild.ContentPatcher` and lists `RafiaBee.MoreQuestsFramework` as a required dependency.
- `content.json`. One `EditData` block targeting `Mods/RafiaBee.MoreQuestsFramework/Quests`, with one entry per example quest. Pick the one closest to what you want and crib from it.
- `i18n/default.json`. The translation strings the quests reference via `{{i18n:key}}` tokens. Content Patcher resolves these against this pack's own `i18n/` folder before MQF sees the data.

## How it loads

MQF seeds three empty game-content assets at startup:

- `Mods/RafiaBee.MoreQuestsFramework/Quests` (Dictionary<string, QuestDef>)
- `Mods/RafiaBee.MoreQuestsFramework/Boards` (Dictionary<string, BoardDef>)
- `Mods/RafiaBee.MoreQuestsFramework/CooldownTiers` (Dictionary<string, int>)

Any Content Patcher pack can `EditData` these. MQF reads the assets on the first update tick after `GameLaunched`, so by the time CP has finished registering its patches, the quests land in MQF's registry.

The dict key is the quest id. Using `{{ModId}}_Name` (CP's `{{ModId}}` token expands to the pack's UniqueID) gives every entry a namespaced id automatically, so two packs can't collide even if they pick the same short name.

## How to try it

1. Copy this folder to your `Stardew Valley/Mods/` directory.
2. Launch the game. The quests show up in the daily-board pool subject to their `Available` conditions.
3. To verify the edits landed, run `patch summary` in the SMAPI console and look for `RafiaBee.MoreQuestsFramework.ExamplePack`.

## What each quest demonstrates

Single-step quests use `Objective`. Multi-step quests use `Steps[]` and can wire steps in order with `Requires`.

| Entry id | Shape | Step kind | What it shows |
| --- | --- | --- | --- |
| `Deliver_Apple` | `Objective` | `Deliver` | The simplest case. One item handed to one giver. Also shows `FestivalBias` + `FairStarTokens` rewards tied to the Stardew Valley Fair, and a `Tier1` `GiftTastes` consequence keyed off the apple itself. |
| `Ship_Eggs` | `Objective` | `Ship` | Ship N of an item through the bin (DayEnding observer). Also shows an `AnimalPurchaseDiscount` reward you can layer on top of Money + Friendship. |
| `Catch_LargemouthBass` | `Objective` | `Fish` / `Catch` | Catch N of one fish. Single-objective fishing quest. |
| `Slay_Slimes` | `Objective` | `Slay` | Slay N of one monster type. |
| `Collect_Mushrooms` | `Objective` | `Resource` / `Collect` | Pick up N of one foraged item (also valid as `"Kind": "Collect"`). |
| `Steps_CheckOnGramps` | `Steps[]` | `Gift` + `Talk` + `Talk` | Multi-step quest. Gift George, talk to him, then report to Evelyn (last step uses `Requires` to wait on the first two). Also shows `$giver` resolving to the quest giver. |
| `GiftUnique_ForageRun` | `Steps[]` | `GiftUniqueNpcs` | Gift any forage item (`$forage` predicate) to N different villagers. Each unique recipient ticks the counter once. |
| `Steps_FishingTrip` | `Steps[]` | `Catch` + `Deliver` | Two-step quest with quality gating. Catch the fish, then deliver Gold-quality stacks. The `Deliver` step uses `MinQuality: 2`. Also shows a `ShopDiscount` reward scoped to a couple of items in Willy's shop. |
| `Visit_LeahsHouse` | `Steps[]` | `Visit` | Walk into a named location. `Targets[0]` is the location name. |
| `Build_NewBarn` | `Steps[]` | `Build` | Player builds the named farm building. `Targets[0]` is the building type. |
| `Reach_MinesFloor25` | `Steps[]` | `ReachLevel` + `Ship` | Reach a target floor in the Mines, then ship a small ore haul. `Targets[0]` is `Mine` or `SkullCavern`, `Count` is the floor. |
| `Plant_Trees` | `Steps[]` | `Plant` | Plant N trees at the named location. |
| `ClearWeeds_Town` | `Steps[]` | `ClearWeeds` | Clear N weeds at the named location. |
| `ClearDebris_Backwoods` | `Steps[]` | `ClearDebris` | Clear N resource clumps at the named location. |

The `Custom` step kind is an escape hatch for consumer-mod code, not used in this pack.

### Event-triggered quests (not on the daily board)

Most quests above sit in the daily-board random pool. The four below show the other trigger sources that fire from save-state changes and land directly in the player's mailbox.

| Entry id | Trigger | What it shows |
| --- | --- | --- |
| `OneShot_FirstCabbage` | `OneShot` with `When: "FirstShipped 250"` | Fires once, ever, the first day the player has shipped a cabbage. Other `When` forms include `FirstStat <name> >= N`, `FirstHeldItem`, `FirstCraftingRecipe`, `FirstCookingRecipe`. |
| `MailReceived_GuildFollowUp` | `MailReceived` with `Flag: "guildMember"` | Fires the morning after a specific vanilla (or mod) mail flag lands. Useful for follow-up quests gated on existing letters. |
| `BuildingBuilt_SiloHousewarming` | `BuildingBuilt` with `Building: "Silo"` and `DayDelay: 1` | Fires when a new farm building of the named type appears. `DayDelay` defers the post to N days later so the letter isn't waiting on the same morning the build finishes. |
| `WeatherForecast_RainPrep` | `WeatherForecast` with `Weather: "Rain"` and `CooldownDays: 14` | Fires the evening before rain (the in-game forecast for tomorrow). `CooldownDays` keeps it from spamming during a rainy stretch. |

All four default to mail delivery; the framework auto-builds the letter body from `Description` plus a sign-off so you don't have to set `MailBody` unless you want a custom letter.

For more advanced patterns (NPC dispatch pools, decor-shipping bypass, etc.), look at the More Quests content mod source for the production quest list.

## Schema reference

A `content.json` editing the Quests asset looks like this:

```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "Mods/RafiaBee.MoreQuestsFramework/Quests",
            "Entries": {
                "{{ModId}}_MyQuest": { /* QuestDef */ }
            }
        }
    ]
}
```

Each `QuestDef` must set either `Generator` (a name registered in C# via `RegisterGenerator`), `Objective` (a fully-declarative single-step quest), or `Steps[]` (a multi-step Adventure quest). Trigger metadata, conditions, and rewards work in all three shapes. See the framework [README](../../README.md) for the full condition-key list.

Reward kinds supported by JSON: `Money`, `Friendship`, `Object`, `Recipe`, `Mail`, `ShopDiscount`, `AnimalPurchaseDiscount`, `FestivalBias`, `FairStarTokens`, `Custom`. The advanced kinds use the same field names as the C# `RewardSpec` records, see the framework README's "Reward kinds" section for the per-kind field list. `Deliver_Apple`, `Ship_Eggs`, and `Steps_FishingTrip` in this pack show the four discount/festival kinds wired up alongside Money and Friendship.

## Cooldown tiers (optional)

Quests can reference named cooldown buckets instead of hard-coding `CooldownDays`. Define the buckets in the same `content.json`:

```jsonc
{
    "Action": "EditData",
    "Target": "Mods/RafiaBee.MoreQuestsFramework/CooldownTiers",
    "Entries": {
        "{{ModId}}_short": 3,
        "{{ModId}}_long": 14
    }
}
```

Then in a quest:

```jsonc
"Trigger": {
    "Source": "DailyBoard",
    "CooldownTier": "{{ModId}}_long"
}
```

CP expands `{{ModId}}` on both sides, so the tier reference resolves to the same string as the tier definition. The framework re-reads the asset on every cooldown lookup, so editing the tier days mid-session (via your pack's own GMCM, a `patch reload`, etc.) takes effect immediately, no save reload needed.

## Custom boards (optional)

A second `EditData` block targeting `Mods/RafiaBee.MoreQuestsFramework/Boards` adds an extra cork-board surface in-world that pulls from a category-specific quest pool:

```jsonc
{
    "Action": "EditData",
    "Target": "Mods/RafiaBee.MoreQuestsFramework/Boards",
    "Entries": {
        "{{ModId}}_HuntingBoard": {
            "Location": "AdventurersGuild",
            "Tile": [20, 4],
            "Categories": ["Mining", "Adventure"]
        }
    }
}
```

Quests with `Trigger.Source: "CustomBoard"` are eligible for these boards instead of the help-wanted billboard.
