using System.Collections.Generic;
using System.Linq;
using MoreQuests.Framework.Quests;
using MoreQuests.Framework.Quests.Vanilla;

namespace MoreQuests.Framework;

internal static class BoardQuestRegistry
{
    public static IReadOnlyList<IQuestDefinition> All { get; } = new IQuestDefinition[]
    {
        new VanillaItemDelivery(),
        new VanillaResourceCollection(),
        new VanillaSlayMonster(),
        new VanillaFishing(),
        new BasicCropDelivery(),
        new SimpleFishingRequest(),
        new BasicSlimeClearing(),
        new BarDelivery(),
        new SeasonalForaging(),
        new ElliottPoemInspiration(),
        new CheckOnGeorge(),
        new HaySupplyRun(),
        new BeachCleanup(),
        new SpringTea(),
        new CravingDish()
    };

    public static IEnumerable<IQuestDefinition> WithKind(PostingKind kind) =>
        All.Where(d => d.Kind == kind);
}
