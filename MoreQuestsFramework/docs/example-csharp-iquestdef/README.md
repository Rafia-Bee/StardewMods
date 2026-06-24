# Example: C# Mod with `IQuestDefinition` Instances

Pattern C from the framework [README](../../README.md), which is the main reference for the full API, schema, and features. This page just walks through the example. A C# SMAPI mod that builds quest definitions in code and registers them directly via `scope.RegisterQuest(...)`. No JSON file at all.

## When to use this pattern

- Your quest list is short. Two or three quests in pure C# is less ceremony than spinning up a JSON file plus a generator name plus i18n keys.
- You want compile-time checking on the trigger, category, and reward shape.
- You'd rather not ship a separate `.json` asset alongside your DLL.

**Trade-off:** the framework auto-builds GMCM weight sliders by reading the JSON metadata. With pattern C there's no JSON, so per-quest GMCM sliders won't appear. If you want config sliders, prefer [pattern B](../example-csharp-generators/) (C# + JSON).

## What it ships

- `manifest.json`, declares the mod as a SMAPI mod (not a content pack) with `RafiaBee.MoreQuestsFramework` as a required dependency.
- `ModEntry.cs`, the SMAPI entry point. Fetches the framework API on `GameLaunched` and registers the quest instance on `RegistrationOpen`.
- `AppleForLewisDefinition.cs`, a class that implements `IQuestDefinition`. This is the actual quest. Make sure to rename this to your quest.
- `i18n/default.json`, the translation strings the quest references.

This folder doesn't include a `.csproj`. Drop the files into your own SMAPI mod project.

## What the example quest does

Same shape as the simple Lewis apple quest from the JSON example pack. The quest is daily-board, fires in fall, asks for one Apple, pays 500g plus a friendship bump.

Implementing `IQuestDefinition` means filling in:

| Member | What it's for |
| --- | --- |
| `Id` | Unique definition id. Used for cooldown tracking and event attribution. |
| `Category` | Category id string (use the `QuestCategory` constants for the built-ins, or any custom id). Sets the note's pad/pin colors and the skill it scales against. |
| `Kind` | `PostingKind`, picks the delivery channel (DailyBoard, Mail, NpcDialogue, etc.). |
| `DefaultWeight` | Relative weight in the daily-board pool. 0 disables. |
| `MaxPerDay` | Hard cap on copies of this definition per day. |
| `CooldownDays` | Minimum days between successive postings. |
| `IsAvailable(ctx)` | Cheap pre-check. Return false to skip without spending generator time. Prefer the `ctx` accessors (`Season`, `DayOfMonth`, `Year`, `Data`, etc.) over reading `Game1.*` directly so the same check can be dry-run via `IMoreQuestsApi.IsQuestAvailable`. |
| `Build(ctx)` | Return a `QuestPosting`, or null if generation failed. |

`Source` and `Trigger` are optional. The defaults (`TriggerSource.DailyBoard`, `TriggerInfo.Default`) cover most daily-board quests. Override them when you want `Mail`, `Periodic`, `DateLocked`, etc.

## See also

- [`../example-pack/`](../example-pack/), pattern A. Pure JSON content pack.
- [`../example-csharp-generators/`](../example-csharp-generators/), pattern B. C# + JSON + generators (the recommended pattern for content mods of any size).
- [`../../README.md`](../../README.md), the framework README with the full API reference.
- [`../../Quests/Vanilla/`](../../Quests/Vanilla/), the four bundled vanilla wrappers. Each is an `IQuestDefinition` you can read end to end.
