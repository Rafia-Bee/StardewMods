using System;
using System.Collections.Generic;

namespace MoreQuestsFramework.Api;

/// Authoring spec for a custom pin-board placed at a tile in a named location. Mirrors
/// plan.md §5.5. Registered via `IMoreQuestsModApi.RegisterBoard(...)` or auto-loaded from
/// a content pack's `boards.json`. Per-day quest postings produced by `TriggerSource.CustomBoard`
/// definitions land on the board whose `Name` matches the definition's target board (Phase 8c).
///
/// Texture / Pin / Pad fields name SMAPI content assets the registering mod is responsible
/// for serving via its own `Helper.Events.Content.AssetRequested` handler. When a Texture
/// is omitted the framework falls back to vanilla's `LooseSprites/Billboard` cork-board so
/// the board still opens cleanly without a custom skin. Pin / Pad similarly fall back to
/// the framework-provided defaults.
public sealed class BoardDefinition
{
    /// Unique board id within the registering mod's scope. Used as the lookup key for
    /// `TriggerSource.CustomBoard` quest definitions targeting this board.
    public string Name { get; set; } = "";

    /// `GameLocation.Name` of the location the board lives in (e.g. `"AdventureGuild"`).
    /// Comparison is case-sensitive to match `Game1.currentLocation.Name`.
    public string Location { get; set; } = "";

    /// `[x, y]` tile coordinate of the board's anchor tile. The action button on this
    /// tile opens the board menu; the in-world board sprite (if any) draws here.
    public int[] Tile { get; set; } = Array.Empty<int>();

    /// Optional content-asset name of the cork-board background texture used by the menu.
    /// Null → fall back to vanilla `LooseSprites/Billboard`.
    public string? Texture { get; set; }

    /// World-render scale multiplier for the in-world board sprite. Defaults to 2 so a
    /// 64×64 placeholder texture renders as a 2-tile-tall wall sign. Authors shipping
    /// pre-scaled board art (e.g. a 32×32 source meant to fill ~3 tiles) can set their
    /// own value. Doesn't affect the menu — the menu always uses the cork-board texture
    /// at vanilla's 4× scale.
    public float WorldScale { get; set; } = 2f;

    /// Optional title shown in the menu's hover/header area. Defaults to `Name` when null.
    public string? Title { get; set; }

    /// Condition dictionary evaluated by `ConditionEvaluator.Evaluate`. The board is only
    /// rendered/clickable when these conditions are met. Empty/null = always available.
    public Dictionary<string, string>? Available { get; set; }

    /// Optional small "!" marker drawn above the board sprite when slots are non-empty.
    public BoardIndicator? Indicator { get; set; }

    /// Optional override for the per-note pin sprite. Null → framework default.
    public BoardSpriteRef? Pin { get; set; }

    /// Optional override for the per-note pad sprite. Null → framework default.
    public BoardSpriteRef? Pad { get; set; }

    /// How many quests render at once. Day-start sampling caps the slot list at this size.
    public int PoolSize { get; set; } = 3;

    /// `WeightedRandom` (default) or `FirstAvailable`. Drives the day-start sampler when
    /// the eligible pool is larger than `PoolSize`.
    public string PoolStrategy { get; set; } = "WeightedRandom";

    /// Optional category whitelist. Quests whose category isn't listed are filtered out
    /// when this board is sampled. Null/empty = accept any category.
    public List<string>? AllowedCategories { get; set; }

    /// UniqueID of the mod that registered this board. Set by the framework at register
    /// time so attribution and asset routing know who owns this entry.
    public string OwnerUniqueId { get; internal set; } = "";

    public int TileX => Tile.Length >= 1 ? Tile[0] : 0;
    public int TileY => Tile.Length >= 2 ? Tile[1] : 0;
}

public sealed class BoardIndicator
{
    public bool Show { get; set; } = true;

    /// `[x, y]` pixel offset from the board's anchor tile, applied at draw time.
    public int[] Offset { get; set; } = Array.Empty<int>();

    public int OffsetX => Offset.Length >= 1 ? Offset[0] : 0;
    public int OffsetY => Offset.Length >= 2 ? Offset[1] : 0;
}

public sealed class BoardSpriteRef
{
    /// Content-asset name (e.g. `"Mods/RafiaBee.MoreQuests/AdventurePin"`). Null → framework default.
    public string? Texture { get; set; }
}
