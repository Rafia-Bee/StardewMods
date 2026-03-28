# Livestock Follows You

Animals you buy follow you home instead of teleporting to the barn. Walk them back to the farm yourself and watch them line up at the barn door.

## Features

- **Follow the leader**: Purchased animals trail behind you as you walk, forming a chain if you buy more than one.
- **Outdoor stall support**: Works with outdoor animal vendors too (e.g. Moira's Glimsap Fair stall from Visit Mount Vapius). Animals appear right away.
- **Livestock Bazaar compatible**: Hooks into `AnimalHouse.adoptAnimal`, so any mod that routes through the standard adoption path is supported.
- **Farm arrival**: When you reach the farm, animals spread out in front of their barn/coop and wait for you to enter the building.
- **Auto-delivery**: Animals are delivered to their buildings when you enter them, or automatically at a configurable curfew time (default 8:00 PM).
- **Rubber-banding**: Animals that fall too far behind teleport closer so they don't get lost.
- **Obstacle handling**: If an animal gets stuck on a fence, tree, or rock, it teleports past the obstacle after a short delay.

## Configuration

All options are available through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`.

| Setting | Default | Description |
|---|---|---|
| Enabled | true | Toggle the mod on/off. When off, animals teleport to barns as usual. |
| Follow Speed | 1.0 | Speed multiplier for catch-up when animals fall behind. |
| Catch-up Distance | 12 | Tile distance before an animal rubber-bands to the player. |
| Auto-deliver Time | 2000 (8 PM) | Game time when undelivered animals are sent home automatically. |
| Animal Sounds | true | Whether animals make sounds while following. |
| Sound Interval | 15s | Seconds between animal sounds. |
| Show Notifications | true | HUD messages for follow/delivery events. |
| Debug Logging | true | Log debug messages to the SMAPI console. |

## Requirements

- [SMAPI](https://smapi.io/) 4.1.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional, for in-game config)

## Compatibility

- Works with Livestock Bazaar, Visit Mount Vapius, and other mods that use `AnimalHouse.adoptAnimal`.
- Multiplayer: host only (animals follow the main farmer).

## Install

1. Install SMAPI.
2. Drop the `LivestockFollowsYou` folder into your `Mods` directory.
3. Run the game through SMAPI.
