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

                data[BigCraftableIds.CropGrabberBase] = CreateGrabberData(
                    "Crop Grabber",
                    _helper.Translation.Get("specialized.crop-grabber.name"),
                    _helper.Translation.Get("specialized.crop-grabber.description"),
                    spriteIndex: 0);

                data[BigCraftableIds.ForageGrabberBase] = CreateGrabberData(
                    "Forage Grabber",
                    _helper.Translation.Get("specialized.forage-grabber.name"),
                    _helper.Translation.Get("specialized.forage-grabber.description"),
                    spriteIndex: 2);

                data[BigCraftableIds.TreeGrabberBase] = CreateGrabberData(
                    "Tree Grabber",
                    _helper.Translation.Get("specialized.tree-grabber.name"),
                    _helper.Translation.Get("specialized.tree-grabber.description"),
                    spriteIndex: 4);

                data[BigCraftableIds.ScavengerGrabberBase] = CreateGrabberData(
                    "Scavenger Grabber",
                    _helper.Translation.Get("specialized.scavenger-grabber.name"),
                    _helper.Translation.Get("specialized.scavenger-grabber.description"),
                    spriteIndex: 6);

                data[BigCraftableIds.MachineGrabberBase] = CreateGrabberData(
                    "Machine Grabber",
                    _helper.Translation.Get("specialized.machine-grabber.name"),
                    _helper.Translation.Get("specialized.machine-grabber.description"),
                    spriteIndex: 8);
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            // Only add recipes when Specialized mode is active. In Classic mode these items
            // cannot be crafted and should not count toward the Craft Master achievement or
            // the crafting portion of the perfection score.
            if (_getConfig().grabberMode != ModConfig.GrabberMode.Specialized)
                return;

            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                var c = _getConfig();

                // Format: ingredients/context/output id/isBigCraftable/conditions/display name
                // Condition "none" prevents auto-learning; recipes are taught via mail
                data[ProgressionTracker.CropRecipe] =
                    $"388 {c.recipeCropWood} 336 {c.recipeCropGoldBar} 621 {c.recipeCropQualitySprinkler}/Field/{BigCraftableIds.CropGrabberBase}/true/none/"
                    + _helper.Translation.Get("specialized.crop-grabber.name");

                data[ProgressionTracker.ForageRecipe] =
                    $"388 {c.recipeForageWood} 336 {c.recipeForageGoldBar} 770 {c.recipeForageMixedSeeds} 771 {c.recipeForageFiber}/Field/{BigCraftableIds.ForageGrabberBase}/true/none/"
                    + _helper.Translation.Get("specialized.forage-grabber.name");

                data[ProgressionTracker.TreeRecipe] =
                    $"709 {c.recipeTreeHardwood} 337 {c.recipeTreeIridiumBar} 724 {c.recipeTreeMapleSyrup} 725 {c.recipeTreeOakResin} 726 {c.recipeTreePineTar}/Field/{BigCraftableIds.TreeGrabberBase}/true/none/"
                    + _helper.Translation.Get("specialized.tree-grabber.name");

                data[ProgressionTracker.ScavengerRecipe] =
                    $"709 {c.recipeScavengerHardwood} 337 {c.recipeScavengerIridiumBar} 881 {c.recipeScavengerBoneFragment} 275 {c.recipeScavengerArtifactTrove}/Field/{BigCraftableIds.ScavengerGrabberBase}/true/none/"
                    + _helper.Translation.Get("specialized.scavenger-grabber.name");

                data[ProgressionTracker.MachineRecipe] =
                    $"337 {c.recipeMachineIridiumBar} 787 {c.recipeMachineBatteryPack} 72 {c.recipeMachineDiamond}/Field/{BigCraftableIds.MachineGrabberBase}/true/none/"
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
                    + $"%item craftingRecipe {BigCraftableIds.CropGrabberBase} %%";
                data["Rafia.DGF_UnlockForage"] = _helper.Translation.Get("mail.unlock-forage")
                    + $"%item craftingRecipe {BigCraftableIds.ForageGrabberBase} %%";
                data["Rafia.DGF_UnlockTree"] = _helper.Translation.Get("mail.unlock-tree")
                    + $"%item craftingRecipe {BigCraftableIds.TreeGrabberBase} %%";
                data["Rafia.DGF_UnlockScavenger"] = _helper.Translation.Get("mail.unlock-scavenger")
                    + $"%item craftingRecipe {BigCraftableIds.ScavengerGrabberBase} %%";
                data["Rafia.DGF_UnlockMachine"] = _helper.Translation.Get("mail.unlock-machine")
                    + $"%item craftingRecipe {BigCraftableIds.MachineGrabberBase} %%";
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
