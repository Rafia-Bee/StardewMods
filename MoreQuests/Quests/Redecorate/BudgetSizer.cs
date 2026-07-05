using System.Collections.Generic;

namespace MoreQuests.Quests;

// Sizes the budget from the objective set so a careful player can stay under it. Budget
// is the sum of each category's reference price times its count, scaled by a generosity
// multiplier, rounded to the nearest 100.
internal static class BudgetSizer
{
    // Reference price for a furniture category that isn't one of the four configured ones.
    // Shouldn't happen in practice, but keeps the budget sane if a new category slips in.
    private const int UncategorizedReferencePrice = 1000;

    public static int Compute(IEnumerable<(string category, int count)> objectives, ModConfig config)
    {
        double raw = 0;
        foreach (var (category, count) in objectives)
            raw += ReferencePrice(category, config) * count;

        raw *= config.RedecorateBudgetGenerosity;
        int rounded = (int)System.Math.Round(raw / 100.0) * 100;
        return System.Math.Max(100, rounded);
    }

    private static int ReferencePrice(string category, ModConfig config) => category switch
    {
        FurnitureCategory.Lamp => config.RedecorateReferenceLampPrice,
        FurnitureCategory.Rug => config.RedecorateReferenceRugPrice,
        FurnitureCategory.Chair => config.RedecorateReferenceChairPrice,
        FurnitureCategory.Table => config.RedecorateReferenceTablePrice,
        _ => UncategorizedReferencePrice
    };
}
