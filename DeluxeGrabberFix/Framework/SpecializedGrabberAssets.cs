using System;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.BigCraftables;

namespace DeluxeGrabberFix.Framework;

internal class SpecializedGrabberAssets
{
    private const string TexturePath = "Mods/Rafia.DeluxeGrabberFix/SpecializedGrabbers";

    private readonly IModHelper _helper;
    private readonly Func<ModConfig> _getConfig;

    public SpecializedGrabberAssets(IModHelper helper, Func<ModConfig> getConfig)
    {
        _helper = helper;
        _getConfig = getConfig;
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
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                var c = _getConfig();

                // Format: ingredients/context/output id/isBigCraftable/conditions/display name
                // Condition "none" prevents auto-learning; recipes are taught via mail
                data[ProgressionTracker.CropRecipe] =
                    $"388 {c.recipeCropWood} 336 {c.recipeCropGoldBar} 621 {c.recipeCropQualitySprinkler}/Field/Rafia.DGF_CropGrabber/true/none/"
                    + _helper.Translation.Get("specialized.crop-grabber.name");

                data[ProgressionTracker.ForageRecipe] =
                    $"388 {c.recipeForageWood} 336 {c.recipeForageGoldBar} 770 {c.recipeForageMixedSeeds} 771 {c.recipeForageFiber}/Field/Rafia.DGF_ForageGrabber/true/none/"
                    + _helper.Translation.Get("specialized.forage-grabber.name");

                data[ProgressionTracker.TreeRecipe] =
                    $"709 {c.recipeTreeHardwood} 337 {c.recipeTreeIridiumBar} 724 {c.recipeTreeMapleSyrup} 725 {c.recipeTreeOakResin} 726 {c.recipeTreePineTar}/Field/Rafia.DGF_TreeGrabber/true/none/"
                    + _helper.Translation.Get("specialized.tree-grabber.name");

                data[ProgressionTracker.ScavengerRecipe] =
                    $"709 {c.recipeScavengerHardwood} 337 {c.recipeScavengerIridiumBar} 881 {c.recipeScavengerBoneFragment} 275 {c.recipeScavengerArtifactTrove}/Field/Rafia.DGF_ScavengerGrabber/true/none/"
                    + _helper.Translation.Get("specialized.scavenger-grabber.name");

                data[ProgressionTracker.MachineRecipe] =
                    $"337 {c.recipeMachineIridiumBar} 787 {c.recipeMachineBatteryPack} 72 {c.recipeMachineDiamond}/Field/Rafia.DGF_MachineGrabber/true/none/"
                    + _helper.Translation.Get("specialized.machine-grabber.name");
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;

                // Hint mails (no attachments, just flavor text)
                data["Rafia.DGF_HintCrop"] = _helper.Translation.Get("mail.hint-crop");
                data["Rafia.DGF_HintForage"] = _helper.Translation.Get("mail.hint-forage");
                data["Rafia.DGF_HintTree"] = _helper.Translation.Get("mail.hint-tree");
                data["Rafia.DGF_HintScavenger"] = _helper.Translation.Get("mail.hint-scavenger");
                data["Rafia.DGF_HintMachine"] = _helper.Translation.Get("mail.hint-machine");

                // Unlock mails (teach crafting recipe)
                data["Rafia.DGF_UnlockCrop"] = _helper.Translation.Get("mail.unlock-crop")
                    + "%item craftingRecipe Rafia.DGF_CropGrabber %%";
                data["Rafia.DGF_UnlockForage"] = _helper.Translation.Get("mail.unlock-forage")
                    + "%item craftingRecipe Rafia.DGF_ForageGrabber %%";
                data["Rafia.DGF_UnlockTree"] = _helper.Translation.Get("mail.unlock-tree")
                    + "%item craftingRecipe Rafia.DGF_TreeGrabber %%";
                data["Rafia.DGF_UnlockScavenger"] = _helper.Translation.Get("mail.unlock-scavenger")
                    + "%item craftingRecipe Rafia.DGF_ScavengerGrabber %%";
                data["Rafia.DGF_UnlockMachine"] = _helper.Translation.Get("mail.unlock-machine")
                    + "%item craftingRecipe Rafia.DGF_MachineGrabber %%";
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
