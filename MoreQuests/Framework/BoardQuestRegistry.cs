using System.Collections.Generic;
using System.Linq;
using MoreQuests.Framework.Quests;

namespace MoreQuests.Framework;

internal static class BoardQuestRegistry
{
    public static IReadOnlyList<IQuestDefinition> All { get; } = new IQuestDefinition[]
    {
        new BasicCropDelivery(),
        new SimpleFishingRequest(),
        new BasicSlimeClearing(),
        new BarDelivery(),
        new SeasonalForaging(),
        new ElliottPoemInspiration(),
        new CheckOnGeorge(),
        new HaySupplyRun(),
        new BeachCleanup(),
        new PlantingDrive(),
        new CravingDish()
    };

    public static IEnumerable<IQuestDefinition> WithKind(PostingKind kind) =>
        All.Where(d => d.Kind == kind);
}
