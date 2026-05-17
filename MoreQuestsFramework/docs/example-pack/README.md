# Example Content Pack

Pattern A from the framework README. A fully-declarative `quests.json` content pack for the More Quests Framework. No C# code, just JSON and translation strings.

For the C# patterns, see [`../example-csharp-generators/`](../example-csharp-generators/) (C# + JSON) and [`../example-csharp-iquestdef/`](../example-csharp-iquestdef/) (pure C#).

## What it ships

- `manifest.json`, declares the pack as `ContentPackFor: RafiaBee.MoreQuestsFramework`. The framework auto-loads any pack with this declaration.
- `quests.json`, one example quest per step kind / objective kind. Pick the one closest to what you want and crib from it.
- `i18n/default.json`, the translation strings the quests reference via `{i18n:key}` tokens.

## How it loads

At `GameLaunched`, the framework iterates `Helper.ContentPacks.GetOwned()` and calls `QuestPackLoader.LoadContentPack(pack)` on each. Token resolution uses the pack's own `Translation` helper, so each pack's keys are scoped to its own `i18n/` folder.

## How to try it

1. Copy this folder to your `Stardew Valley/Mods/` directory.
2. Launch the game. The quests show up in the daily-board pool subject to their `Available` conditions.

## What each quest demonstrates

Single-step quests use `Objective`. Multi-step quests use `Steps[]` and can wire steps in order with `Requires`.

| Quest | Shape | Step kind | What it shows |
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

For more advanced patterns (NPC dispatch pools, decor-shipping bypass, etc.), look at [`../../../MoreQuests/assets/quests.json`](../../../MoreQuests/assets/quests.json), the production quest list the More Quests content mod ships with.

## Schema reference

A `quests.json` document is:

```jsonc
{
    "Schema": "1.0",
    "Quests": [ /* QuestDef */ ]
}
```

Each `QuestDef` must set either `Generator` (a name registered in C# via `RegisterGenerator`), `Objective` (a fully-declarative single-step quest), or `Steps[]` (a multi-step Adventure quest). Trigger metadata, conditions, and rewards work in all three shapes. See the framework [README](../../README.md) for the full condition-key list.

Reward kinds supported by JSON: `Money`, `Friendship`, `Object`, `Recipe`, `Mail`, `ShopDiscount`, `AnimalPurchaseDiscount`, `FestivalBias`, `FairStarTokens`, `Custom`. The advanced kinds use the same field names as the C# `RewardSpec` records, see the framework README's "Reward kinds" section for the per-kind field list. `Deliver_Apple`, `Ship_Eggs`, and `Steps_FishingTrip` in this pack show the four discount/festival kinds wired up alongside Money and Friendship.
