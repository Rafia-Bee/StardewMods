namespace MoreQuests;

/// One painting in Leah's farm-painting pool. The pool lives in the
/// `Mods/RafiaBee.MoreQuests/LeahPaintings` data asset, so Content Patcher packs can drop in
/// their own paintings with an EditData edit. The quest and the furniture both read the
/// merged pool, so anything added here becomes both placeable furniture and a possible reward.
public sealed class LeahPaintingEntry
{
    /// Asset name of the painting texture. Should be 32x32 (a 2x2 painting). Built-ins point
    /// at `Mods/RafiaBee.MoreQuests/LeahPainting/<animal>_<frame>`. A Content Patcher pack
    /// points this at whatever texture it loaded itself.
    public string Texture { get; set; } = "";

    /// Frame group this painting belongs to (e.g. wood, night, burgundy). The quest only
    /// rewards paintings whose frame matches the one the player picked in the config.
    public string Frame { get; set; } = "";

    /// Name shown on the furniture item. Built-ins keep their animal name; Content Patcher
    /// packs set their own (a CP `{{i18n}}` token works here).
    public string DisplayName { get; set; } = "";
}
