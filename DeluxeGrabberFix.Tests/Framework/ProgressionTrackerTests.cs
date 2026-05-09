using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class ProgressionTrackerTests
{
    [Fact]
    public void AllRecipeKeysSet_ContainsSameKeysAsArray()
    {
        ProgressionTracker.AllRecipeKeysSet.Should()
            .BeEquivalentTo(ProgressionTracker.AllRecipeKeys);
    }

    [Fact]
    public void AllRecipeKeysSet_IsSingletonReference()
    {
        // PerfectionPatch reads this on every postfix call. The whole point of
        // audit 3.3 is that it must be the same instance across calls, not a
        // fresh allocation. ReferenceEquals across two reads pins that.
        var first = ProgressionTracker.AllRecipeKeysSet;
        var second = ProgressionTracker.AllRecipeKeysSet;
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void AllRecipeKeysSet_HasExpectedFiveRecipes()
    {
        ProgressionTracker.AllRecipeKeysSet.Should().HaveCount(5);
        ProgressionTracker.AllRecipeKeysSet.Should().Contain(new[]
        {
            ProgressionTracker.CropRecipe,
            ProgressionTracker.ForageRecipe,
            ProgressionTracker.TreeRecipe,
            ProgressionTracker.ScavengerRecipe,
            ProgressionTracker.MachineRecipe,
        });
    }

    [Fact]
    public void ProgressionPrerequisiteTypes_HasExpectedFiveTypes()
    {
        // Animal -> Crop -> Forage -> Tree -> Scavenger is the prerequisite chain
        // CheckHintMails / CheckUnlockMails / RetroactiveCheck branches Contains() against.
        // Machine is the leaf (never queried as a prerequisite). All is the wildcard global
        // mode marker, never owned by a player. Both must stay out of this set or the
        // CollectOwnedGrabberTypes early-exit would skip the world walk in cases where it's
        // still needed.
        ProgressionTracker.ProgressionPrerequisiteTypes.Should().HaveCount(5);
        ProgressionTracker.ProgressionPrerequisiteTypes.Should().BeEquivalentTo(new[]
        {
            GrabberType.Animal,
            GrabberType.Crop,
            GrabberType.Forage,
            GrabberType.Tree,
            GrabberType.Scavenger,
        });
        ProgressionTracker.ProgressionPrerequisiteTypes.Should().NotContain(GrabberType.Machine);
        ProgressionTracker.ProgressionPrerequisiteTypes.Should().NotContain(GrabberType.All);
    }

    [Fact]
    public void ProgressionPrerequisiteTypes_IsSingletonReference()
    {
        // Audit 3.2 fix relies on this being one allocation, not a fresh set each call.
        var first = ProgressionTracker.ProgressionPrerequisiteTypes;
        var second = ProgressionTracker.ProgressionPrerequisiteTypes;
        ReferenceEquals(first, second).Should().BeTrue();
    }
}
