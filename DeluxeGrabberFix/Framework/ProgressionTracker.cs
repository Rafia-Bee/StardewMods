using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace DeluxeGrabberFix.Framework;

internal class ProgressionTracker
{
    private readonly ModEntry _mod;
    private readonly IModHelper _helper;

    // Mail IDs
    private const string HintCropMail = "Rafia.DGF_HintCrop";
    private const string HintForageMail = "Rafia.DGF_HintForage";
    private const string HintTreeMail = "Rafia.DGF_HintTree";
    private const string HintScavengerMail = "Rafia.DGF_HintScavenger";
    private const string HintMachineMail = "Rafia.DGF_HintMachine";

    private const string UnlockCropMail = "Rafia.DGF_UnlockCrop";
    private const string UnlockForageMail = "Rafia.DGF_UnlockForage";
    private const string UnlockTreeMail = "Rafia.DGF_UnlockTree";
    private const string UnlockScavengerMail = "Rafia.DGF_UnlockScavenger";
    private const string UnlockMachineMail = "Rafia.DGF_UnlockMachine";

    // Recipe keys (must match Data/CraftingRecipes keys)
    internal const string CropRecipe = "Rafia.DGF_CropGrabber";
    internal const string ForageRecipe = "Rafia.DGF_ForageGrabber";
    internal const string TreeRecipe = "Rafia.DGF_TreeGrabber";
    internal const string ScavengerRecipe = "Rafia.DGF_ScavengerGrabber";
    internal const string MachineRecipe = "Rafia.DGF_MachineGrabber";

    internal static readonly string[] AllMailIds =
    {
        HintCropMail, HintForageMail, HintTreeMail, HintScavengerMail, HintMachineMail,
        UnlockCropMail, UnlockForageMail, UnlockTreeMail, UnlockScavengerMail, UnlockMachineMail
    };

    internal static readonly string[] AllRecipeKeys =
    {
        CropRecipe, ForageRecipe, TreeRecipe, ScavengerRecipe, MachineRecipe
    };

    public ProgressionTracker(ModEntry mod)
    {
        _mod = mod;
        _helper = mod.Helper;
    }

    /// <summary>
    /// Run on day start. Checks milestones and sends hint/unlock mails.
    /// </summary>
    internal void CheckProgression()
    {
        if (_mod.Config.grabberMode != ModConfig.GrabberMode.Specialized)
            return;

        var farmer = Game1.player;

        CheckHintMails(farmer);
        CheckUnlockMails(farmer);
    }

    /// <summary>
    /// Run on save loaded. Retroactively grants any already-earned unlocks.
    /// </summary>
    internal void RetroactiveCheck()
    {
        if (_mod.Config.grabberMode != ModConfig.GrabberMode.Specialized)
            return;

        var farmer = Game1.player;
        var config = _mod.Config;
        int unlocked = 0;

        // Animal Grabber is always available (bought from Marnie), so skip it.
        // Check each tier in order.

        if (PlayerOwnsGrabberType(GrabberType.Animal))
        {
            // Crop Grabber
            if (!farmer.mailReceived.Contains(UnlockCropMail)
                && Game1.stats.CropsShipped >= (uint)config.cropsShippedThreshold)
            {
                GrantRecipeAndMail(farmer, UnlockCropMail, CropRecipe);
                unlocked++;
            }

            if (!farmer.mailReceived.Contains(HintCropMail))
                farmer.mailForTomorrow.Add(HintCropMail);
        }

        if (PlayerOwnsGrabberType(GrabberType.Crop))
        {
            // Forage Grabber
            if (!farmer.mailReceived.Contains(UnlockForageMail)
                && Game1.stats.ItemsForaged >= (uint)config.itemsForagedThreshold)
            {
                GrantRecipeAndMail(farmer, UnlockForageMail, ForageRecipe);
                unlocked++;
            }

            if (!farmer.mailReceived.Contains(HintForageMail))
                farmer.mailForTomorrow.Add(HintForageMail);
        }

        if (PlayerOwnsGrabberType(GrabberType.Forage))
        {
            // Tree Grabber
            if (!farmer.mailReceived.Contains(UnlockTreeMail)
                && Game1.stats.StumpsChopped >= (uint)config.stumpsChoppedThreshold)
            {
                GrantRecipeAndMail(farmer, UnlockTreeMail, TreeRecipe);
                unlocked++;
            }

            if (!farmer.mailReceived.Contains(HintTreeMail))
                farmer.mailForTomorrow.Add(HintTreeMail);
        }

        if (PlayerOwnsGrabberType(GrabberType.Tree))
        {
            // Scavenger Grabber
            if (!farmer.mailReceived.Contains(UnlockScavengerMail)
                && GetMuseumDonationCount() >= config.museumDonationsThreshold)
            {
                GrantRecipeAndMail(farmer, UnlockScavengerMail, ScavengerRecipe);
                unlocked++;
            }

            if (!farmer.mailReceived.Contains(HintScavengerMail))
                farmer.mailForTomorrow.Add(HintScavengerMail);
        }

        if (PlayerOwnsGrabberType(GrabberType.Scavenger))
        {
            // Machine Grabber
            if (!farmer.mailReceived.Contains(UnlockMachineMail)
                && farmer.totalMoneyEarned >= (uint)config.totalMoneyEarnedThreshold)
            {
                GrantRecipeAndMail(farmer, UnlockMachineMail, MachineRecipe);
                unlocked++;
            }

            if (!farmer.mailReceived.Contains(HintMachineMail))
                farmer.mailForTomorrow.Add(HintMachineMail);
        }

        if (unlocked > 0)
            _mod.LogDebug($"Retroactive progression: granted {unlocked} grabber recipe(s)");
    }

    private void CheckHintMails(Farmer farmer)
    {
        // Hint mails are sent the day after the previous grabber is obtained.
        // We check daily: if player owns the previous grabber and hasn't received the hint, queue it.

        if (PlayerOwnsGrabberType(GrabberType.Animal) && !farmer.mailReceived.Contains(HintCropMail))
            AddMailIfNotQueued(farmer, HintCropMail);

        if (PlayerOwnsGrabberType(GrabberType.Crop) && !farmer.mailReceived.Contains(HintForageMail))
            AddMailIfNotQueued(farmer, HintForageMail);

        if (PlayerOwnsGrabberType(GrabberType.Forage) && !farmer.mailReceived.Contains(HintTreeMail))
            AddMailIfNotQueued(farmer, HintTreeMail);

        if (PlayerOwnsGrabberType(GrabberType.Tree) && !farmer.mailReceived.Contains(HintScavengerMail))
            AddMailIfNotQueued(farmer, HintScavengerMail);

        if (PlayerOwnsGrabberType(GrabberType.Scavenger) && !farmer.mailReceived.Contains(HintMachineMail))
            AddMailIfNotQueued(farmer, HintMachineMail);
    }

    private void CheckUnlockMails(Farmer farmer)
    {
        var config = _mod.Config;

        // Crop Grabber: owns Animal + shipped enough crops
        if (PlayerOwnsGrabberType(GrabberType.Animal)
            && !farmer.mailReceived.Contains(UnlockCropMail)
            && Game1.stats.CropsShipped >= (uint)config.cropsShippedThreshold)
        {
            AddMailIfNotQueued(farmer, UnlockCropMail);
            _mod.LogDebug("Crop Grabber milestone met, unlock mail queued");
        }

        // Forage Grabber: owns Animal + Crop + foraged enough
        if (PlayerOwnsGrabberType(GrabberType.Crop)
            && !farmer.mailReceived.Contains(UnlockForageMail)
            && Game1.stats.ItemsForaged >= (uint)config.itemsForagedThreshold)
        {
            AddMailIfNotQueued(farmer, UnlockForageMail);
            _mod.LogDebug("Forage Grabber milestone met, unlock mail queued");
        }

        // Tree Grabber: owns previous + chopped enough stumps
        if (PlayerOwnsGrabberType(GrabberType.Forage)
            && !farmer.mailReceived.Contains(UnlockTreeMail)
            && Game1.stats.StumpsChopped >= (uint)config.stumpsChoppedThreshold)
        {
            AddMailIfNotQueued(farmer, UnlockTreeMail);
            _mod.LogDebug("Tree Grabber milestone met, unlock mail queued");
        }

        // Scavenger Grabber: owns previous + enough museum donations
        if (PlayerOwnsGrabberType(GrabberType.Tree)
            && !farmer.mailReceived.Contains(UnlockScavengerMail)
            && GetMuseumDonationCount() >= config.museumDonationsThreshold)
        {
            AddMailIfNotQueued(farmer, UnlockScavengerMail);
            _mod.LogDebug("Scavenger Grabber milestone met, unlock mail queued");
        }

        // Machine Grabber: owns previous + earned enough gold
        if (PlayerOwnsGrabberType(GrabberType.Scavenger)
            && !farmer.mailReceived.Contains(UnlockMachineMail)
            && farmer.totalMoneyEarned >= (uint)config.totalMoneyEarnedThreshold)
        {
            AddMailIfNotQueued(farmer, UnlockMachineMail);
            _mod.LogDebug("Machine Grabber milestone met, unlock mail queued");
        }
    }

    /// <summary>
    /// Check if the player owns (placed in world or in inventory) a grabber of the given type.
    /// </summary>
    private bool PlayerOwnsGrabberType(GrabberType type)
    {
        // Animal type: any (BC)165 counts (vanilla auto-grabber)
        if (type == GrabberType.Animal)
        {
            // Check inventory
            foreach (var item in Game1.player.Items)
            {
                if (item?.QualifiedItemId == BigCraftableIds.AutoGrabber)
                    return true;
            }

            // Check placed in world
            foreach (var location in ModEntry.GetAllLocations())
            {
                foreach (var pair in location.Objects.Pairs)
                {
                    if (pair.Value.QualifiedItemId == BigCraftableIds.AutoGrabber)
                        return true;
                }
            }

            return false;
        }

        // For specialized types: check if recipe is known (they've crafted or could craft one)
        // OR if they have one placed/in inventory
        string recipeKey = GetRecipeKey(type);
        if (recipeKey != null && Game1.player.craftingRecipes.ContainsKey(recipeKey))
            return true;

        string itemId = GetItemId(type);
        if (itemId == null)
            return false;

        // Check inventory for the specialized grabber item
        foreach (var item in Game1.player.Items)
        {
            if (item?.QualifiedItemId == itemId)
                return true;
        }

        // Check placed in world: (BC)165 with matching modData
        string typeStr = type.ToString();
        foreach (var location in ModEntry.GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.QualifiedItemId == BigCraftableIds.AutoGrabber
                    && pair.Value.modData.TryGetValue(SpecializedGrabberPatches.ModDataGrabberType, out string t)
                    && t == typeStr)
                    return true;
            }
        }

        return false;
    }

    private static int GetMuseumDonationCount()
    {
        if (Game1.getLocationFromName("ArchaeologyHouse") is LibraryMuseum museum)
            return museum.museumPieces.Length;

        return 0;
    }

    private static void AddMailIfNotQueued(Farmer farmer, string mailId)
    {
        if (!farmer.mailForTomorrow.Contains(mailId) && !farmer.mailbox.Contains(mailId))
            farmer.mailForTomorrow.Add(mailId);
    }

    private static void GrantRecipeAndMail(Farmer farmer, string mailId, string recipeKey)
    {
        // Mark mail as received so it doesn't re-send
        if (!farmer.mailReceived.Contains(mailId))
            farmer.mailReceived.Add(mailId);

        // Teach the recipe directly for retroactive unlocks
        if (!farmer.craftingRecipes.ContainsKey(recipeKey))
            farmer.craftingRecipes.Add(recipeKey, 0);
    }

    private static string GetRecipeKey(GrabberType type)
    {
        return type switch
        {
            GrabberType.Crop => CropRecipe,
            GrabberType.Forage => ForageRecipe,
            GrabberType.Tree => TreeRecipe,
            GrabberType.Scavenger => ScavengerRecipe,
            GrabberType.Machine => MachineRecipe,
            _ => null
        };
    }

    private static string GetItemId(GrabberType type)
    {
        return type switch
        {
            GrabberType.Crop => BigCraftableIds.CropGrabber,
            GrabberType.Forage => BigCraftableIds.ForageGrabber,
            GrabberType.Tree => BigCraftableIds.TreeGrabber,
            GrabberType.Scavenger => BigCraftableIds.ScavengerGrabber,
            GrabberType.Machine => BigCraftableIds.MachineGrabber,
            _ => null
        };
    }
}
