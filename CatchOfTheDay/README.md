# Catch of the Day

A Stardew Valley mod that shows weather-exclusive fish icons on the HUD. Supports rain, storms, sun, snow, wind, and green rain.

## Features

- Displays **fish sprite icons** on the right side of the screen when weather-exclusive fish are available
- Supports **all weather types**: rain, thunderstorms, sunny, snow, wind/debris, and green rain
- **Hover over any icon** to see the fish name, spawn locations (only places you've visited), and catchable time windows
- **Uncaught fish** are marked with "(Uncaught)" in the tooltip -- updates in real time when you catch one
- **Bundle tracking** -- shows which Community Center bundles still need each fish (works with modded bundles too)
- **Lookup Anything integration** -- hover over any fish icon and press F1 to open its Lookup Anything page (requires [Lookup Anything](https://www.nexusmods.com/stardewvalley/mods/541))
- Only shows fish that **require specific weather** to spawn -- not every fish that happens to be available
- Shows all weather-exclusive fish available **at any point during the day**, not just the current time
- Automatically includes fish from **modded locations** (Stardew Valley Expanded, Visit Mount Vapius, East Scarp, etc.)
- Updates dynamically when the day starts or you visit a new location
- **Per-weather toggles** — choose exactly which weather types to track
- Fully configurable via [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional)

## Requirements

- [SMAPI](https://smapi.io) 4.1.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) *(optional, for in-game settings)*

## Configuration

All settings can be changed in-game via Generic Mod Config Menu, or by editing `config.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Show/hide the fish icon overlay entirely |
| `HudX` | `-51` | Horizontal position. Negative values offset from the right edge |
| `HudY` | `300` | Vertical position (pixels from top of screen) |
| `HorizontalLayout` | `false` | Arrange icons in a horizontal row instead of a vertical column |
| `MaxLocations` | `4` | Max spawn locations shown per tooltip (0 = show all) |
| `ShowBundleNeeds` | `true` | Show which Community Center bundles still need each fish |
| `TrackRain` | `true` | Show rain-exclusive fish on rainy days |
| `TrackStorm` | `true` | Show storm-exclusive fish during thunderstorms |
| `TrackSun` | `true` | Show sun-exclusive fish on sunny days |
| `TrackSnow` | `true` | Show snow-exclusive fish when snowing |
| `TrackWind` | `true` | Show wind-exclusive fish during debris weather |
| `TrackGreenRain` | `true` | Show green-rain-exclusive fish during green rain |

## Installation

1. Install SMAPI
2. Drop the `CatchOfTheDay` folder into your `Stardew Valley/Mods` directory
3. Launch the game — fish icons appear automatically when weather-exclusive fish are available

## How It Works

The mod reads fish spawn data from `Data/Locations` at runtime, so it works with any mod that adds fish through the standard game data system. A fish is considered "weather-exclusive" if:

- Its `Data/Fish` entry has a weather restriction (e.g., `"rainy"` or `"sunny"`), or
- Its spawn condition requires specific weather (e.g., `LOCATION_WEATHER Here Rain`, `LOCATION_WEATHER Here Storm`, `LOCATION_WEATHER Here Snow`)

Fish that can be caught in any weather are excluded.

Hovering over a fish icon shows the fish name, which visited locations it spawns in, and the time window(s) it can be caught (e.g., "6pm – 2am").
