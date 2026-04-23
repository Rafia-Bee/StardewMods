using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class WildflowersGrabber : TerrainFeaturesMapGrabber
{
    private const string WildflowerKey = "aedenthorn.Wildflowers/wild";

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

        if (!grass.modData.TryGetValue(WildflowerKey, out string data))
            return false;

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

        // Respect the flower harvest mode setting (same logic as HarvestableCropHoeDirtGrabber)
        if (Config.flowers == ModConfig.FlowerHarvestMode.Off)
            return false;

        if (Config.flowers == ModConfig.FlowerHarvestMode.Smart
            && Helpers.IsFlowerNearBeeHouse(Location, tile, Config.beeHouseRange))
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
        dirt.modData[WildflowerKey] = "T";

        HarvestInterceptor.BeginIntercept();
        crop.harvest((int)tile.X, (int)tile.Y, dirt, isForcedScytheHarvest: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                if (!TryAddItem(item))
                    Game1.createItemDebris(item, new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32), -1, Location);
            }

            grass.modData.Remove(WildflowerKey);
            return true;
        }
        return false;
    }
}
