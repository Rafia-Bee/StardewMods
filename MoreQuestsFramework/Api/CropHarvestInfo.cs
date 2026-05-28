namespace MoreQuestsFramework.Api;

// Read-only payload for the IMoreQuestsApi.CropHarvested event. Fired from a
// Harmony postfix on Crop.harvest, so it covers player + Junimo harvests.
// - CropQualifiedId: the harvest item's qualified id, e.g. "(O)190" for Cauliflower
// - LocationName: NameOrUniqueName of the HoeDirt's containing location
// - TileX / TileY: tile coords on that location
// - ByJunimo: true when a JunimoHarvester triggered the harvest
public readonly record struct CropHarvestInfo(
    string CropQualifiedId,
    string LocationName,
    int TileX,
    int TileY,
    bool ByJunimo);
