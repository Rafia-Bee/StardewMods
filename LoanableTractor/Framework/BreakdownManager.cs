using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace LoanableTractor.Framework
{
    /// <summary>Manages the daily tractor feature breakdown system and Harmony patches on TractorMod attachments.</summary>
    internal static class BreakdownManager
    {
        /// <summary>Breakable tractor features: i18n key → TractorMod attachment class name.</summary>
        private static readonly KeyValuePair<string, string>[] BreakableFeatures =
        {
            new("hoeing", "HoeAttachment"),
            new("watering", "WateringCanAttachment"),
            new("harvesting", "ScytheAttachment"),
            new("tree_chopping", "AxeAttachment"),
            new("rock_breaking", "PickaxeAttachment"),
            new("seed_planting", "SeedAttachment"),
            new("fertilizing", "FertilizerAttachment")
        };

        /// <summary>The TractorMod attachment type name currently broken for today (null = nothing broken).</summary>
        internal static string BrokenAttachmentTypeName { get; set; }

        /// <summary>The i18n feature key for the pending breakdown (between dialogue shown and player response).</summary>
        internal static string PendingFeatureKey { get; set; }

        /// <summary>The attachment type name for the pending breakdown.</summary>
        internal static string PendingAttachmentTypeName { get; set; }

        /// <summary>Roll a random breakable feature.</summary>
        internal static (string featureKey, string attachmentTypeName) RollRandomFeature()
        {
            var picked = BreakableFeatures[Game1.random.Next(BreakableFeatures.Length)];
            return (picked.Key, picked.Value);
        }

        /// <summary>Apply the pending breakdown (player chose to skip fixing).</summary>
        internal static void ApplyPendingBreakdown()
        {
            BrokenAttachmentTypeName = PendingAttachmentTypeName;
            PendingFeatureKey = null;
            PendingAttachmentTypeName = null;
        }

        /// <summary>Clear pending breakdown state (player chose to fix).</summary>
        internal static void ClearPending()
        {
            PendingFeatureKey = null;
            PendingAttachmentTypeName = null;
        }

        /// <summary>Reset all breakdown state (end of day or full reset).</summary>
        internal static void Reset()
        {
            BrokenAttachmentTypeName = null;
            PendingFeatureKey = null;
            PendingAttachmentTypeName = null;
        }

        /// <summary>Apply Harmony prefix patches on all breakable TractorMod attachment IsEnabled methods.</summary>
        internal static void ApplyPatches(Harmony harmony, IMonitor monitor)
        {
            try
            {
                var tractorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TractorMod");

                if (tractorAssembly == null)
                {
                    monitor.Log("Cannot find TractorMod assembly for breakdown patches.", LogLevel.Warn);
                    return;
                }

                string ns = "Pathoschild.Stardew.TractorMod.Framework.Attachments";
                var prefix = new HarmonyMethod(typeof(BreakdownManager), nameof(IsEnabled_Prefix));
                int patchedCount = 0;

                foreach (var feature in BreakableFeatures)
                {
                    var type = tractorAssembly.GetType($"{ns}.{feature.Value}");
                    if (type == null)
                    {
                        monitor.Log($"Could not find {feature.Value} in TractorMod assembly.", LogLevel.Trace);
                        continue;
                    }

                    var method = AccessTools.Method(type, "IsEnabled");
                    if (method != null)
                    {
                        harmony.Patch(method, prefix: prefix);
                        patchedCount++;
                    }
                }

                monitor.Log($"Applied breakdown patches to {patchedCount} TractorMod attachments.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                monitor.Log($"Error applying breakdown patches: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Harmony prefix for TractorMod attachment IsEnabled methods. Blocks broken attachments on loaned tractors only.</summary>
        internal static bool IsEnabled_Prefix(object __instance, ref bool __result)
        {
            if (BrokenAttachmentTypeName == null)
                return true;

            var mount = Game1.player?.mount;
            if (mount == null || !mount.modData.ContainsKey(TractorLoanManager.LoanedTractorModDataKey))
                return true;

            if (__instance.GetType().Name == BrokenAttachmentTypeName)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
