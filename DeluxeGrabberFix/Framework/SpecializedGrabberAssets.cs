using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.BigCraftables;

namespace DeluxeGrabberFix.Framework;

internal class SpecializedGrabberAssets
{
    private const string TexturePath = "Mods/Rafia.DeluxeGrabberFix/SpecializedGrabbers";

    private readonly IModHelper _helper;

    public SpecializedGrabberAssets(IModHelper helper)
    {
        _helper = helper;
    }

    public void Register()
    {
        _helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, BigCraftableData>().Data;

                data["Rafia.DGF_CropGrabber"] = CreateGrabberData(
                    "Crop Grabber",
                    _helper.Translation.Get("specialized.crop-grabber.name"),
                    _helper.Translation.Get("specialized.crop-grabber.description"),
                    spriteIndex: 0);

                data["Rafia.DGF_ForageGrabber"] = CreateGrabberData(
                    "Forage Grabber",
                    _helper.Translation.Get("specialized.forage-grabber.name"),
                    _helper.Translation.Get("specialized.forage-grabber.description"),
                    spriteIndex: 2);

                data["Rafia.DGF_TreeGrabber"] = CreateGrabberData(
                    "Tree Grabber",
                    _helper.Translation.Get("specialized.tree-grabber.name"),
                    _helper.Translation.Get("specialized.tree-grabber.description"),
                    spriteIndex: 4);

                data["Rafia.DGF_ScavengerGrabber"] = CreateGrabberData(
                    "Scavenger Grabber",
                    _helper.Translation.Get("specialized.scavenger-grabber.name"),
                    _helper.Translation.Get("specialized.scavenger-grabber.description"),
                    spriteIndex: 6);

                data["Rafia.DGF_MachineGrabber"] = CreateGrabberData(
                    "Machine Grabber",
                    _helper.Translation.Get("specialized.machine-grabber.name"),
                    _helper.Translation.Get("specialized.machine-grabber.description"),
                    spriteIndex: 8);
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(TexturePath))
        {
            e.LoadFromModFile<Texture2D>("assets/specialized_grabbers.png", AssetLoadPriority.Exclusive);
        }
    }

    private static BigCraftableData CreateGrabberData(string name, string displayName, string description, int spriteIndex)
    {
        return new BigCraftableData
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Price = 0,
            Texture = TexturePath,
            SpriteIndex = spriteIndex,
            CanBePlacedOutdoors = true,
            CanBePlacedIndoors = true,
            IsLamp = false
        };
    }
}
