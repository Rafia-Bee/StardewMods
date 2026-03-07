# Deluxe Grabber Fix

Adopted the existing [Deluxe Grabber Redux 1.6](https://www.nexusmods.com/stardewvalley/mods/20799) port by Nykal145 and fixed the underlying issues I found in that mod, along with some added features (wip).

## Features

- **Crop harvesting** — Harvests crops (including indoor pots) within a configurable range and shape (square or walking distance)
- **Fruit trees** — Picks fruit from mature fruit trees
- **Berry bushes** — Collects berries from salmonberry and blackberry bushes
- **Seed trees** — Shakes regular trees for seeds
- **Tree moss** — Strips moss from trees
- **Slime balls** — Collects slime balls in slime hutches
- **Farm cave mushrooms** — Grabs mushrooms from farm cave boxes (works anywhere)
- **Artifact spots** — Digs up artifact spots
- **Ore panning** — Collects ore from panning sites
- **Seed spots** — Digs up seed spots
- **Secret Woods stumps** — Fells hardwood stumps in the Secret Woods
- **Town garbage cans** — Searches garbage cans
- **Forage** — Picks up forageable items
- **Item debris** — Collects items dropped as ground debris (monster loot, fallen items, modded forage drops)
- **Yield reporting** — Logs what each auto-grabber collected to the SMAPI console
- **Experience gain** — Optionally awards farming/foraging XP as if you did it yourself
- **Global grabber mode** — Lets all auto-grabbers work across all locations, or fire on demand with a hotkey. Designate via in-menu star button (mobile-friendly) or hover + hotkey
- **Auto-fire global grabber** — Optionally fires the designated global grabber automatically each morning — no hotkey needed, perfect for mobile

## Configuration

All options are configurable through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`.

| Setting | Default | Description |
|---|---|---|
| Harvest Crops | Off | Harvest crops near auto-grabbers |
| Harvest Crops Inside Pots | On | Also harvest crops in indoor pots |
| Harvest Flowers | Off | Include flowers when harvesting crops |
| Harvest Range | -1 | Tile range for crop harvesting (-1 = unlimited) |
| Harvest Range Mode | Walk | Shape of the range (Walk = diamond, Square = square) |
| Collect Forage Items | On | Collect forageable items like leeks, daffodils, and spring onions |
| Harvest Fruit Trees | Off | Pick fruit from fruit trees |
| Harvest Berry Bushes | On | Collect berries from bushes |
| Shake Seed Trees | Off | Shake trees for seeds |
| Grab Slime Balls | On | Collect slime balls in slime hutches |
| Grab Farm Cave Mushrooms | On | Collect from mushroom boxes |
| Dig Up Artifact Spots | Off | Dig up artifact spots |
| Collect Ore From Panning Sites | Off | Pan for ore |
| Dig Up Seed Spots | Off | Dig up seed spots |
| Fell Stumps in Secret Woods | Off | Chop stumps for hardwood |
| Search Garbage Cans | Off | Search town garbage cans |
| Harvest Moss from Trees | Off | Strip moss from trees |
| Collect Item Debris | Off | Collect items dropped as ground debris (monster loot, modded forage drops) |
| Excluded Items | (empty) | Comma-separated qualified item IDs the grabber should never collect (e.g. `(O)859` for Lucky Ring) |
| Report Yield | On | Log collected items to SMAPI console |
| Gain Experience | On | Award XP for auto-grabbed items |
| Global Grabber Mode | Off | Off / All (every location) / Hover (cursor + hotkey) |
| Auto-Fire Global Grabber | Off | Fire designated global grabber automatically each morning |
| Fire Global Grabber | B | Hotkey for Hover/All mode |
| Global Button X Offset | 0 | Horizontal offset for the star button in the auto-grabber menu |
| Global Button Y Offset | 0 | Vertical offset for the star button in the auto-grabber menu |

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `DeluxeGrabberFix` folder into your `Mods` directory
3. Run the game

## Compatibility

- Stardew Valley 1.6+
- SMAPI 4.0+
- Works alongside other grabber mods
- Provides a mod API (`IDeluxeGrabberFixApi`) for other mods to override mushroom/berry bush harvest behavior

## Notes

- The auto-grabber only collects items if there's room in its internal chest. Items that don't fit are left for the next day.
- Crop harvesting range is visualized when placing an auto-grabber (if range > 0).
