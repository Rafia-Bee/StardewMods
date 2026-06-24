using System;
using System.Collections.Generic;
using MoreQuestsFramework.Content;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Api;

// Authoring spec for a custom pin-board. Registered via IMoreQuestsModApi.RegisterBoard
// or auto-loaded from a pack's boards.json. The owning mod is responsible for serving
// the texture asset; an omitted Texture renders nothing in the world but still leaves
// the anchor tile clickable.
//
// Placement model: the anchor `Tile` is the floor tile the player stands on to interact.
// The sprite renders directly above that tile, horizontally centered on it, scaled to
// `BoardSize` tiles. The same pixel rect drives collision and click hit-testing.
public sealed class BoardDefinition
{
    public string Name { get; set; } = "";

    // GameLocation.Name, case-sensitive.
    public string Location { get; set; } = "";

    // [x, y] floor tile the player stands on to interact. Always walkable.
    public int[] Tile { get; set; } = Array.Empty<int>();

    // In-world sprite (wall painting / sign). Not the menu skin (that's Background).
    public string? Texture { get; set; }

    // Menu skin, sheet layout matches vanilla LooseSprites/Billboard (top 338x198 at 4x).
    public string? Background { get; set; }

    // [width, height] of the rendered board in tiles. Sprite renders this many tiles up
    // from the anchor and is centered horizontally on it. Drives collision and click
    // hit-testing. Defaults to 1x1.
    public int[] BoardSize { get; set; } = Array.Empty<int>();

    public int BoardWidth => BoardSize.Length >= 1 && BoardSize[0] > 0 ? BoardSize[0] : 1;
    public int BoardHeight => BoardSize.Length >= 2 && BoardSize[1] > 0 ? BoardSize[1] : 1;

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

    // Catch-all / aggregator. When set, this board ALSO mirrors CustomBoard quests it
    // isn't the declared target of, on top of its own quests. ["*"] catches every owner;
    // a list of mod UniqueIDs curates which owners it picks up. Absent or empty means
    // normal id-routing only (the default; a private board never needs this).
    public List<string>? AllowedOwners { get; set; }

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
