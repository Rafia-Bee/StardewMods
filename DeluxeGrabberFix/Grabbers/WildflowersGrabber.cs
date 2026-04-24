using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class WildflowersGrabber : TerrainFeaturesMapGrabber
{
    private const string AedenthornWildflowerKey = "aedenthorn.Wildflowers/wild";
    private const string ReimaginedTypeName = "WildFlowersReimagined.FlowerGrass";

    private static PropertyInfo _reimaginedCropProperty;
    private static Type _reimaginedCachedType;

    public WildflowersGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.collectWildflowers)
            return false;

        if (feature is not Grass grass)
            return false;

        if (!PassesFlowerMode(tile))
            return false;

        if (grass.modData.TryGetValue(AedenthornWildflowerKey, out string data))
            return TryHarvestAedenthorn(tile, grass, data);

        if (grass.GetType().FullName == ReimaginedTypeName)
            return TryHarvestReimagined(tile, grass);

        return false;
    }

    private bool PassesFlowerMode(Vector2 tile)
    {
        if (Config.flowers == ModConfig.FlowerHarvestMode.Off)
            return false;

        if (Config.flowers == ModConfig.FlowerHarvestMode.Smart
            && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.beeHouseRange))
            return false;

        return true;
    }

    private bool TryHarvestAedenthorn(Vector2 tile, Grass grass, string data)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(data);
        }
        catch
        {
            return false;
        }

        var root = doc.RootElement;
        bool dead = root.TryGetProperty("dead", out var deadProp) && deadProp.GetBoolean();
        bool fullyGrown = root.TryGetProperty("fullyGrown", out var fgProp) && fgProp.GetBoolean();
        int currentPhase = root.TryGetProperty("currentPhase", out var cpProp) ? cpProp.GetInt32() : 0;
        int dayOfCurrentPhase = root.TryGetProperty("dayOfCurrentPhase", out var dcpProp) ? dcpProp.GetInt32() : 0;
        string seedIndex = root.TryGetProperty("seedIndex", out var siProp) ? siProp.GetString() : null;

        int phaseDaysCount = 0;
        if (root.TryGetProperty("phaseDays", out var pdProp) && pdProp.ValueKind == JsonValueKind.Array)
            phaseDaysCount = pdProp.GetArrayLength();

        if (dead || string.IsNullOrEmpty(seedIndex))
            return false;

        bool harvestReady = (!fullyGrown || dayOfCurrentPhase <= 0)
            && currentPhase >= phaseDaysCount - 1;
        if (!harvestReady)
            return false;

        Crop crop;
        try
        {
            crop = new Crop(seedIndex, (int)tile.X, (int)tile.Y, Location);
            crop.growCompletely();
        }
        catch
        {
            return false;
        }

        var dirt = new HoeDirt(1, crop);
        dirt.modData[AedenthornWildflowerKey] = "T";

        HarvestInterceptor.BeginIntercept();
        crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        if (items.Count == 0)
            return false;

        DistributeItems(tile, items);
        grass.modData.Remove(AedenthornWildflowerKey);
        return true;
    }

    private bool TryHarvestReimagined(Vector2 tile, Grass grass)
    {
        Type type = grass.GetType();
        PropertyInfo cropProperty = GetReimaginedCropProperty(type);
        if (cropProperty == null)
            return false;

        if (cropProperty.GetValue(grass) is not Crop crop)
            return false;

        if (!IsCropHarvestReady(crop))
            return false;

        var dirt = new HoeDirt(1, crop);

        HarvestInterceptor.BeginIntercept();
        bool shouldDestroy = crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        if (items.Count == 0)
            return false;

        DistributeItems(tile, items);

        // For single-harvest flowers, clear the Crop. For regrow flowers, leave it so the mod can regrow.
        if (shouldDestroy)
            cropProperty.SetValue(grass, null);

        return true;
    }

    private static PropertyInfo GetReimaginedCropProperty(Type type)
    {
        if (_reimaginedCachedType == type)
            return _reimaginedCropProperty;

        _reimaginedCachedType = type;
        _reimaginedCropProperty = type.GetProperty("Crop", BindingFlags.Public | BindingFlags.Instance);
        return _reimaginedCropProperty;
    }

    private static bool IsCropHarvestReady(Crop crop)
    {
        int phaseDaysCount = crop.phaseDays?.Count ?? 0;
        if (phaseDaysCount == 0)
            return false;

        int currentPhase = crop.currentPhase.Value;
        bool fullyGrown = crop.fullyGrown.Value;
        int dayOfCurrentPhase = crop.dayOfCurrentPhase.Value;

        return currentPhase >= phaseDaysCount - 1
            && (!fullyGrown || dayOfCurrentPhase <= 0);
    }

    private void DistributeItems(Vector2 tile, List<Item> items)
    {
        foreach (var item in items)
        {
            if (!TryAddItem(item))
                Game1.createItemDebris(item, new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32), -1, Location);
        }
    }
}
