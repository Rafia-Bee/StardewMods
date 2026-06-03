using System.Collections.Generic;

namespace Architect.Services;

// Sizes the budget from the objective set so a careful player can stay under it. Budget
// is the sum of each category's reference price times its count, scaled by a generosity
// multiplier, rounded to the nearest 100.
internal static class BudgetSizer
{
    public static int Compute(IEnumerable<(string category, int count)> objectives, ModConfig config)
    {
        double raw = 0;
        foreach (var (category, count) in objectives)
            raw += ReferencePrice(category, config) * count;

        raw *= config.BudgetGenerosity;
        int rounded = (int)System.Math.Round(raw / 100.0) * 100;
        return System.Math.Max(100, rounded);
    }

    private static int ReferencePrice(string category, ModConfig config) => category switch
    {
        FurnitureCategory.Light => config.ReferenceLightPrice,
        FurnitureCategory.Rug => config.ReferenceRugPrice,
        FurnitureCategory.Chair => config.ReferenceChairPrice,
        FurnitureCategory.Table => config.ReferenceTablePrice,
        _ => 1000
    };
}
