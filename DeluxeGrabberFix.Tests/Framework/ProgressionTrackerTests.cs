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
}
