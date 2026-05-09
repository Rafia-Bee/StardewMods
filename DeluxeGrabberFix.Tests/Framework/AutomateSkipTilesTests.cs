using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class AutomateSkipTilesTests
{
    [Theory]
    [InlineData("Tapper", "Tapper")]
    [InlineData("Crab Pot", "CrabPot")]
    [InlineData("Bee House", "BeeHouse")]
    [InlineData("Mayonnaise Machine", "MayonnaiseMachine")]
    [InlineData("Statue Of Endless Fortune", "StatueOfEndlessFortune")]
    [InlineData("J. Cola", "JCola")]
    public void GetCleanedTypeId_StripsNonAlphanumeric(string input, string expected)
    {
        AutomateSkipTiles.GetCleanedTypeId(input).Should().Be(expected);
    }

    [Fact]
    public void GetCleanedTypeId_ReturnsSourceWhenAlreadyClean()
    {
        // No allocation contract: when the name has nothing to strip, the helper
        // hands back the same reference. Pins the fast-path that audit 3.6's fix
        // depends on (so future contributors don't accidentally introduce a
        // .ToCharArray() or substring that defeats the optimization).
        const string clean = "FurnaceXYZ123";
        ReferenceEquals(AutomateSkipTiles.GetCleanedTypeId(clean), clean).Should().BeTrue();
    }

    [Fact]
    public void GetCleanedTypeId_MemoizesAcrossCalls()
    {
        // Second call must return the exact same instance as the first. This is
        // what makes the BFS allocation-free on repeat machine types after the
        // first pass through a network.
        string first = AutomateSkipTiles.GetCleanedTypeId("Bee House");
        string second = AutomateSkipTiles.GetCleanedTypeId("Bee House");
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void GetCleanedTypeId_HandlesEmptyAndAllStripped()
    {
        AutomateSkipTiles.GetCleanedTypeId("").Should().Be("");
        AutomateSkipTiles.GetCleanedTypeId("--").Should().Be("");
        AutomateSkipTiles.GetCleanedTypeId(" . ").Should().Be("");
    }
}
