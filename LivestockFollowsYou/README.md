# Livestock Follows You

When you buy an animal, it follows you around instead of teleporting straight to its building. Once you reach the farm and enter the barn or coop, the animal is registered inside and goes back to normal. This only happens on the walk home after purchase -- your existing animals won't start following you around.

## Features

- **Follow the leader**: Purchased animals trail behind you as you walk.
- **Grazing Bell**: Buy the Grazing Bell from Marnie's shop (500g) to take your registered farm animals on walks around town. Walking animals graze nearby grass for a happiness boost when you stand still. Send them home by clicking with the bell (requires 3+ hearts).
- **Idle roaming**: Walking animals don't just stand around -- they wander short distances, pause, sit, and eat naturally when you stop moving.
- **NPC reactions**: Villagers you pass will comment on your animals with speech bubbles -- 131 unique lines ranging from sweet to snarky. Marnie, Shane, and Jas have their own personality-specific dialog, and reactions change based on weather and whether you're on a walk.
- **Outdoor stall support**: Works with outdoor animal vendors too (e.g. Moira's Glimsap Fair stall from Visit Mount Vapius). Animals appear right away.
- **Livestock Bazaar compatible**: Hooks into `AnimalHouse.adoptAnimal`, so any mod that routes through the standard adoption path is supported.
- **Farm arrival**: When you reach the farm, animals spread out in front of their barn/coop and wait for you to enter the building.
- **Auto-delivery**: Animals are delivered to their buildings when you enter them, or automatically at a configurable curfew time (default 8:00 PM).
- **Smart pathfinding**: Animals route around fences, trees, and rocks instead of getting stuck. If something is blocking their path, they back off and try a different route.
- **Formation walking**: When you're walking with multiple animals, they spread into a small group around you instead of stacking on the same tile.
- **Sprint to catch up**: If an animal falls behind, it sprints back to you. Only animals that get really far behind (past twice the catch-up distance) teleport.

## Configuration

All options are available through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`.

| Setting | Default | Description |
|---|---|---|
| Enabled | true | Toggle the mod on/off. When off, animals teleport to barns as usual. |
| Follow Speed | 1.0 | Speed multiplier for catch-up when animals fall behind. |
| Catch-up Distance | 10 | Tile distance before an animal starts sprinting to catch up. If the animal falls behind by more than twice this distance, it teleports to you. |
| Auto-deliver Time | 2000 (8 PM) | Game time when undelivered animals are sent home automatically. |
| Animal Sounds | true | Whether animals make sounds while following. |
| Sound Interval | 15s | Seconds between animal sounds. |
| Show Notifications | true | HUD messages for follow/delivery events. |
| NPC Reactions | true | Nearby villagers react with speech bubbles when you escort animals. |
| Debug Logging | false | Log debug messages to the SMAPI console. |
| Grazing Happiness | 15 | Happiness gained per grass eaten during a walk (0-255 scale). |
| Send Home Friendship | 750 (3 hearts) | Friendship points needed to send an animal home alone via the bell. |
| Grazing Idle Time | 2s | Seconds you must stand still before walking animals start grazing. |

## Requirements

- [SMAPI](https://smapi.io/) 4.1.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional, for in-game config)

## Compatibility

- Works with Livestock Bazaar, Visit Mount Vapius, and other mods that use `AnimalHouse.adoptAnimal`.
- Multiplayer: works for both host and farmhands.

## Install

1. Install SMAPI.
2. Drop the `LivestockFollowsYou` folder into your `Mods` directory.
3. Run the game through SMAPI.
