# Example: C# Mod with Bundled JSON + Generators

Pattern B from the framework [README](../../README.md), which is the main reference for the full API, schema, and features. This page just walks through the example. A C# SMAPI mod that:

1. Subscribes to the framework's `RegistrationOpen` event.
2. Registers one or more named C# generator functions.
3. Subscribes to SMAPI's `AssetRequested` and injects its `assets/quests.json` entries straight into `Mods/RafiaBee.MoreQuestsFramework/Quests` via `e.Edit(...)`. Each JSON entry can reference a registered generator by name, or describe the quest declaratively.

Use this pattern when you want declarative metadata (trigger, weight, cooldown, conditions) handled by JSON, plus runtime randomization done in C#.

Third-party Content Patcher packs can still layer their own `EditData` patches on top of the asset alongside this mod's edits, with normal CP priority rules deciding who wins on conflicts.

## What it ships

- `manifest.json`, declares the mod as a SMAPI mod (not a content pack) with `RafiaBee.MoreQuestsFramework` as a required dependency.
- `ModEntry.cs`, the SMAPI entry point. Fetches the framework API on `GameLaunched`, registers the generators and custom-trigger / custom-reward handlers, and edits the framework's Quests asset on `AssetRequested`. Title/description `{i18n:key}` tokens are resolved against this mod's own `Helper.Translation` before the entries land in the asset.
- `assets/quests.json`, three quest entries (a daily-board crop run, a Custom-trigger one-off, and a SpecialOrder).
- `i18n/default.json`, the strings the generator passes through the translation helper.

This folder doesn't include a `.csproj`. Drop the files into your own SMAPI mod project, point the project at the framework's DLL (`<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` is the recommended setup), and you're ready!

## What each quest demonstrates

| Quest | Trigger | What it shows |
| --- | --- | --- |
| `RandomCropDelivery` | `DailyBoard` | The bread-and-butter pattern B case. JSON sets the trigger source, weight, cooldown, and a `Season` gate. The C# generator picks the giver, picks the crop, scales the quantity off Farming, and computes the gold reward from the crop's sell price. |
| `StardropFollowUp` | `Custom` (handler `PlayerOwnsStardrop`) | A Custom-source trigger registered through `RegisterCustomTrigger`. The handler returns true once the player's max stamina is above the starting 270 (i.e. has eaten or is carrying a Stardrop). Reward list mixes a vanilla `Money` reward with a `Custom` reward (`RestoreHealthAndStamina`) registered through `RegisterCustomReward`. |
| `MarnieEggDrive` | `SpecialOrder` | A SpecialOrder posted on spring 7 for a week. The generator returns a `QuestPosting` with `Kind = PostingKind.SpecialOrder` and a populated `SpecialOrderSpec` (one Ship objective for 20 eggs, plus Money via the vanilla rewards path and Friendship via `FrameworkRewards`). Vanilla owns accept, objective tracking, and reward grant from there. |

## Where each piece lives

| Field | Lives in |
| --- | --- |
| Trigger source, weight, cooldown, `Available` conditions, deadline tier | `quests.json` |
| Giver, item, quantity, gold, description text for the daily-board quest | `ModEntry.BuildRandomCropDelivery` |
| Custom-trigger handler body | `ModEntry.OnGameLaunched` (`scope.RegisterCustomTrigger`) |
| Custom-reward `apply` + `summarize` bodies | `ModEntry.OnGameLaunched` (`scope.RegisterCustomReward`) |
| SpecialOrder objectives, rewards, requester | `ModEntry.BuildMarnieEggDrive` |

## Why split the work this way

- JSON is the only place the framework can read to expose **per-quest GMCM weights**. Anything you want a config slider for has to live in JSON.
- Generators get the live `QuestContext` (current season, met villagers, cached crop pool, player stats), so any logic that depends on save state is much easier to write in C#.

## See also

- [`../example-pack/`](../example-pack/), pattern A. Pure JSON content pack, no C# at all.
- [`../example-csharp-iquestdef/`](../example-csharp-iquestdef/), pattern C. Pure C# with no JSON.
- [`../../README.md`](../../README.md), the framework README with the full API reference.
- [`../../../MoreQuests/`](../../../MoreQuests/), the production content mod that uses pattern B for the majority of its quests.
