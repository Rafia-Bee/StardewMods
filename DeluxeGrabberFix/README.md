# Deluxe Grabber Fix

Adopted the existing [Deluxe Grabber Redux 1.6](https://www.nexusmods.com/stardewvalley/mods/20799) port by Nykal145 and fixed the underlying issues I found in that mod, along with some added features (wip).

## Features

- **Crop harvesting** — Harvests ground crops and indoor pot crops within a configurable range and shape (diamond or square). Fully delegates to the game's own harvest logic, so modded crops, quality overrides, and extra harvest chances all work correctly.
- **Fruit trees** — Picks fruit from mature fruit trees, including quality based on tree age.
- **Berry bushes** — Collects berries from salmonberry and blackberry bushes.
- **Seed trees** — Shakes trees for seeds, hazelnuts, coconuts, and other drops. Uses the game's own shake logic, so modded trees work too.
- **Tree moss** — Strips moss from trees.
- **Animal products** — Collects milk, wool, eggs, feathers, and other animal products from barns and coops, extended to work with the global grabber system.
- **Slime balls** — Collects slime balls in slime hutches.
- **Farm cave mushrooms** — Grabs mushrooms from cave boxes (works anywhere you place them). Configured under the Machine Collection page.
- **Artifact spots** — Digs up artifact spots for loot.
- **Ore panning** — Collects ore from panning sites.
- **Seed spots** — Digs up raccoon seed spots.
- **Hardwood stumps** — Fells large stumps and hollow logs for hardwood in all locations.
- **Town garbage cans** — Searches garbage cans for items.
- **Forage** — Picks up all forageable items (truffles, beach forage, etc.) with quality based on your foraging skill. Also collects modded spawned forage.
- **Green rain weeds** — Breaks apart large green rain bushes for moss, fiber, and mossy seeds.
- **Item debris** — Collects items dropped as ground debris (monster loot, fallen items, modded forage drops).
- **Machine collection** — Automatically collects finished outputs from all vanilla machines: Crab Pots, Bee Houses, Tappers, Heavy Tappers, Mushroom Logs, Leaf Baskets, Fish Ponds, Mushroom Boxes, Kegs, Preserves Jars, Cheese Presses, Mayonnaise Machines, Looms, Oil Makers, Furnaces, Charcoal Kilns, Recycling Machines, Seed Makers, Bone Mills, Geode Crushers, Wood Chippers, Deconstructors, Fish Smokers, Bait Makers, Dehydrators, Crystalariums, Lightning Rods, Worm Bins, Solar Panels, Slime Egg Presses, Coffee Makers, Soda Machines, and statues. Also collects from modded machines. Per-machine toggles available.
- **Exclude quest items** — Prevents the grabber from collecting quest items like the Lucky Purple Shorts. Enabled by default.
- **Item exclusion list** — Specify items by their qualified item ID that the grabber should never collect.
- **Yield reporting** — Logs what each auto-grabber collected to the SMAPI console, with an optional in-game toast showing the total.
- **Chest-full warnings** — Shows a HUD warning when auto-grabber chests are full, so you never silently lose items.
- **Replant reminder** — Notifies you when single-harvest crops are collected, reminding you to replant. A second reminder fires later in the day at a configurable time.
- **Auto-grabber naming** — Name individual auto-grabbers via the in-menu Name button. Named grabbers show their name in HUD warnings and grab summaries. Also reads names from Chests Anywhere as a fallback.
- **Experience gain** — Optionally awards farming/foraging XP as if you did it yourself.
- **Global grabber mode** — Lets all auto-grabbers work across all locations, or fire on demand with a hotkey. Designate via in-menu star button (mobile-friendly) or hover + hotkey.
- **Auto-fire global grabber** — Optionally fires the designated global grabber automatically each morning, perfect for mobile.
- **Per-save config** — Each save file gets its own independent config. Changing settings in one save won't affect your other saves. The global `config.json` is used as the template for new saves.
- **Location filtering** — Skip festival areas automatically, and choose exactly which locations the grabbers should ignore via a config menu page.
- **Select Visited Only** — Enables only locations you've actually visited, skipping the rest. Newly discovered locations are auto-enabled when you walk into them.
- **Specialized Grabbers** — An optional progression system that replaces the "one grabber does everything" model with six specialized grabbers, each collecting a specific category of items.

## Configuration

All options are configurable through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`. Each save file gets its own config stored in `configs/` inside the mod folder, so settings changed in one save won't affect another. The global `config.json` is used as the default template for new saves and for title screen edits.

### Grabber Mode

| Setting | Default | Description |
|---|---|---|
| Grabber Mode | Classic | Classic: one grabber collects everything. Specialized: six grabbers each collect a specific category |
| Count for Perfection | On | When in Specialized mode, whether crafting all five specialized grabbers is required for the Craft Master achievement and perfection score. Disable to use Specialized mode without affecting perfection. |

### Crop Harvesting

| Setting | Default | Description |
|---|---|---|
| Harvest Crops | Off | Harvest crops near auto-grabbers |
| Harvest Crops Inside Pots | On | Also harvest crops in indoor pots |
| Harvest Flowers | Smart | Off/All/Smart. Smart skips flowers near Bee Houses |
| Bee House Flower Range | 5 | Tile range Smart mode uses to detect nearby Bee Houses |
| Harvest Range | -1 | Tile range for crop harvesting (-1 = unlimited) |
| Harvest Range Mode | Walk | Walk = diamond shape, Square = square shape |
| Replant Reminder | On | HUD notification when single-harvest crops are collected |
| Replant Reminder Time | 2000 | In-game time for the repeat reminder (2000 = 8:00 PM) |

### Other Harvesting

| Setting | Default | Description |
|---|---|---|
| Collect Forage Items | On | Picks up forageable items |
| Harvest Fruit Trees | Off | Pick fruit from fruit trees |
| Harvest Berry Bushes | On | Collect berries from bushes |
| Shake Seed Trees | Off | Shake trees for seeds |
| Collect Animal Products | On | Milk, wool, eggs, feathers, and other animal products |
| Grab Slime Balls | On | Collect slime balls in slime hutches |
| Dig Up Artifact Spots | Off | Dig up artifact spots |
| Collect Ore From Panning Sites | Off | Pan for ore |
| Fell Hardwood Stumps | Off | Chops large stumps and hollow logs for hardwood |
| Search Garbage Cans | Off | Search town garbage cans |
| Dig Up Seed Spots | Off | Dig up seed spots |
| Harvest Moss from Trees | Off | Strip moss from trees |
| Harvest Green Rain Weeds | Off | Break apart large green rain bushes |
| Collect Item Debris | Off | Collect items dropped as ground debris |

### Machine Collection

| Setting | Default | Description |
|---|---|---|
| Collect Machine Outputs | Off | Master toggle. Auto-enables when you turn on any individual toggle |
| Collect All Machines | Off | Batch toggle for all machine types at once |

Individual toggles (all default On when machines enabled): Crab Pots, Fish Ponds, Bee Houses, Tappers, Leaf Baskets, Mushroom Boxes, Kegs, Preserves Jars, Cheese Presses, Mayonnaise Machines, Looms, Oil Makers, Furnaces, Charcoal Kilns, Recycling Machines, Seed Makers, Bone Mills, Geode Crushers, Wood Chippers, Deconstructors, Mushroom Logs, Fish Smokers, Bait Makers, Dehydrators, Crystalariums, Lightning Rods, Worm Bins, Solar Panels, Slime Egg Presses, Coffee Makers, Soda Machines, Statues, Other Machines.

### Global Grabber & Misc

| Setting | Default | Description |
|---|---|---|
| Report Yield | On | Log collected items to SMAPI console and show in-game summary |
| Debug Logging | Off | Detailed trace logging for troubleshooting |
| Gain Experience | On | Award XP for auto-grabbed items |
| Grab Frequency | Instant | Instant (every tick), Hourly (batched), or Daily |
| Exclude Quest Items | On | Skip items flagged as quest items |
| Skip Festival Locations | On | Prevents collection in festival/event areas |
| Excluded Items | (empty) | Comma-separated qualified item IDs to never collect |
| Global Grabber Mode | Off | Off / All (every location) / Hover (cursor + hotkey) |
| Auto-Fire Global Grabber | Off | Fire designated global grabber automatically each morning |
| Fire Global Grabber | B | Hotkey for Hover/All mode |
| Designate Global Grabber | G | Hotkey to designate a grabber as the global collection point |
| Global Button X/Y Offset | 0 | Adjust star button position in the auto-grabber menu |
| Name Button X/Y Offset | 0 | Adjust Name button position in the auto-grabber menu |
| Enabled Locations | — | Toggle individual locations on or off |
| Select Visited Only | Off | Only enable locations you've actually visited |

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `DeluxeGrabberFix` folder into your `Mods` directory
3. Run the game

## Compatibility

- Stardew Valley 1.6+
- SMAPI 4.0+
- Works alongside other grabber mods
- Compatible with [Vanilla Plus Professions](https://www.nexusmods.com/stardewvalley/mods/22992) — VPP's Combat-Farm slime drops, Shaker tree drops, and Gleaner/Wayfarer forage are all auto-collected
- Compatible with [Sunberry Village](https://www.nexusmods.com/stardewvalley/mods/22702) — optional config toggle to exclude FTM-spawned ore nodes and supply crates
- Compatible with [Machine Progression System](https://www.nexusmods.com/stardewvalley/mods/15530) — upgraded crab pots, bee houses, tappers, and mushroom logs are collected alongside vanilla machines
- Compatible with [Fishnets](https://www.nexusmods.com/stardewvalley/mods/13819) — fish nets are collected alongside crab pots, with bait consumed on collection
- Compatible with [Cloths and Colors (Textile Expansion)](https://www.nexusmods.com/stardewvalley/mods/39216) — Leaf Baskets are collected as part of machine collection
- Compatible with [Automate](https://www.nexusmods.com/stardewvalley/mods/1063) — DGF skips machines Automate is already managing, preventing duplicate collection. Respects per-chest settings so input-only chests won't block DGF from collecting
- Compatible with [Smart Filtered Hopper](https://github.com/yxlimo/StardewValleyMods) — hoppers are no longer treated as chest connectors during Automate chain scanning
- Compatible with [Wild Flowers](https://www.nexusmods.com/stardewvalley/mods/9535) — harvests fully grown wildflowers from grass tiles. Respects the Harvest Flowers setting
- Compatible with [Custom Bush](https://www.nexusmods.com/stardewvalley/mods/20791) — bushes from Custom Bush produce the correct items with proper quality
- Collects roe and other produce from Fish Ponds automatically
- Provides a mod API (`IDeluxeGrabberFixApi`) for other mods to override mushroom, berry bush, fruit tree, and slime harvest behavior, plus an `OnItemGrabbed` event and `IsGrabberActive` query

## Notes

- The auto-grabber only collects items if there's room in its internal chest. Items that don't fit are left for the next day.
- Crop harvesting range is visualized when placing an auto-grabber (if range > 0).
