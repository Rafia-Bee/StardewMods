using System;
using System.Collections.Generic;
using MoreQuestsFramework.Content;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Api;

// Authoring spec for a custom pin-board. Registered via IMoreQuestsModApi.RegisterBoard
// or auto-loaded from a pack's boards.json. Texture/Pin/Pad name content assets the
// registering mod is responsible for serving; omitted ones fall back to framework defaults.
public sealed class BoardDefinition
{
    public string Name { get; set; } = "";

    // GameLocation.Name, case-sensitive.
    public string Location { get; set; } = "";

    // [x, y] anchor tile.
    public int[] Tile { get; set; } = Array.Empty<int>();

    // In-world sprite (wall painting / sign). Not the menu skin (that's Background).
    public string? Texture { get; set; }

    // Menu skin, sheet layout matches vanilla LooseSprites/Billboard (top 338x198 at 4x).
    public string? Background { get; set; }

    // World-render scale for the in-world sprite. Doesn't affect the menu.
    public float WorldScale { get; set; } = 2f;

    // [x, y] pixel offset for the in-world sprite, lets authors park art on a wall
    // while keeping the click-target tile walkable.
    public int[] DrawOffset { get; set; } = Array.Empty<int>();

    public int DrawOffsetX => DrawOffset.Length >= 1 ? DrawOffset[0] : 0;
    public int DrawOffsetY => DrawOffset.Length >= 2 ? DrawOffset[1] : 0;

    // [width, height] in tiles. Drives both collision and click hit-testing so the
    // action button on any tile inside the footprint opens the board.
    public int[] FootprintTiles { get; set; } = Array.Empty<int>();

    public int FootprintWidth => FootprintTiles.Length >= 1 && FootprintTiles[0] > 0 ? FootprintTiles[0] : 1;
    public int FootprintHeight => FootprintTiles.Length >= 2 && FootprintTiles[1] > 0 ? FootprintTiles[1] : 1;

    public string? Title { get; set; }

    // Evaluated by ConditionEvaluator. Empty/null = always available.
    [JsonConverter(typeof(ScalarStringDictionaryConverter))]
    public Dictionary<string, string>? Available { get; set; }

    public BoardIndicator? Indicator { get; set; }

    public BoardSpriteRef? Pin { get; set; }

    public BoardSpriteRef? Pad { get; set; }

    public int PoolSize { get; set; } = 3;

    // "WeightedRandom" (default) or "FirstAvailable".
    public string PoolStrategy { get; set; } = "WeightedRandom";

    public List<string>? AllowedCategories { get; set; }

    public string OwnerUniqueId { get; set; } = "";

    public int TileX => Tile.Length >= 1 ? Tile[0] : 0;
    public int TileY => Tile.Length >= 2 ? Tile[1] : 0;
}

public sealed class BoardIndicator
{
    public bool Show { get; set; } = true;

    public int[] Offset { get; set; } = Array.Empty<int>();

    public int OffsetX => Offset.Length >= 1 ? Offset[0] : 0;
    public int OffsetY => Offset.Length >= 2 ? Offset[1] : 0;
}

public sealed class BoardSpriteRef
{
    public string? Texture { get; set; }
}
