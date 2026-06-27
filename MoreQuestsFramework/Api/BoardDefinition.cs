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

    // Player-config bounds for PoolSize. The GMCM slider for this board runs PoolSizeMin to
    // PoolSizeMax and defaults to PoolSize; the player's chosen value is clamped into that
    // range. PoolSizeMin doubles as the floor, so a board never draws fewer than this even
    // if the player drags the slider all the way down. Defaults give a 1 to 10 slider.
    public int PoolSizeMin { get; set; } = 1;
    public int PoolSizeMax { get; set; } = 10;

    // "WeightedRandom" (default) or "FirstAvailable".
    public string PoolStrategy { get; set; } = "WeightedRandom";

    // How many bulletin notices (non-quest text pins) this board shows, separate from the
    // quest PoolSize so notices and quests don't compete for slots. 0 hides notices. Only
    // matters once a notice targets this board. Players get their own GMCM slider running
    // NoticePoolSizeMin to NoticePoolSizeMax. Min can be 0, so a player may hide all notices.
    public int NoticePoolSize { get; set; } = 2;
    public int NoticePoolSizeMin { get; set; } = 0;
    public int NoticePoolSizeMax { get; set; } = 5;

    // Note arrangement on the cork board. "Scatter" (default) = the loose pinned look custom
    // boards have always used, kept as the default so existing boards look unchanged.
    // "TiltedGrid" = the tidy auto-grid with per-note tilts (what the daily board uses).
    // "Zoned" = author-defined regions, each holding a set of categories with its own
    // sub-layout (see Zones).
    public string Layout { get; set; } = "Scatter";

    // Used only when Layout is "Zoned". Each zone carves out a rectangle of the cork area
    // and lays out the notes whose category lands in it. Order matters: the first zone whose
    // Categories list matches a note wins. A zone with an empty/absent Categories list is the
    // catch-all for anything no other zone claimed.
    public List<BoardZone>? Zones { get; set; }

    // Optional author bias for which entries win when more are eligible than there are slots.
    // Absent or empty leaves the existing per-quest weighted RNG untouched.
    public BoardPriority? Priority { get; set; }

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

// One region of a "Zoned" board. Rect is [x, y, width, height] as percentages (0-100) of
// the cork area, measured from its top-left corner. Style is the sub-layout used inside the
// region ("TiltedGrid" or "Scatter"). Categories lists which quest categories live here;
// leave it empty/absent to make this the catch-all region for anything no other zone claims.
public sealed class BoardZone
{
    public int[] Rect { get; set; } = Array.Empty<int>();

    public string Style { get; set; } = "TiltedGrid";

    public List<string>? Categories { get; set; }

    // Reserved for notice / ad / other non-quest entry types once those exist. Unused today.
    public List<string>? Types { get; set; }

    public float RectX => Rect.Length >= 1 ? Rect[0] : 0;
    public float RectY => Rect.Length >= 2 ? Rect[1] : 0;
    public float RectW => Rect.Length >= 3 ? Rect[2] : 100;
    public float RectH => Rect.Length >= 4 ? Rect[3] : 100;
}

// Per-board bias applied on top of the per-quest weights before the weighted draw. Each map
// is category-or-owner -> multiplier (1.0 = no change, 2.0 = twice as likely, 0 = excluded).
// A category/owner not listed keeps multiplier 1.0, so an empty Priority is a no-op and the
// board behaves exactly like the plain weighted RNG.
public sealed class BoardPriority
{
    public Dictionary<string, double>? Categories { get; set; }

    public Dictionary<string, double>? Owners { get; set; }

    // Reserved for quest-vs-notice biasing once notices exist. Unused today.
    public Dictionary<string, double>? Types { get; set; }
}
