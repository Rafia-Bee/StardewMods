using System.Collections.Generic;
using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class GrabberTypeHelperTests
{
    // GrabberType is internal, so we can't put it in a public [Theory] signature.
    // Routing through MemberData (which returns object[]) keeps the enum inside
    // the array and out of the test method's public surface.
    public static IEnumerable<object[]> KnownGrabberIds => new[]
    {
        new object[] { BigCraftableIds.AutoGrabber, GrabberType.Animal },
        new object[] { BigCraftableIds.CropGrabber, GrabberType.Crop },
        new object[] { BigCraftableIds.ForageGrabber, GrabberType.Forage },
        new object[] { BigCraftableIds.TreeGrabber, GrabberType.Tree },
        new object[] { BigCraftableIds.ScavengerGrabber, GrabberType.Scavenger },
        new object[] { BigCraftableIds.MachineGrabber, GrabberType.Machine }
    };

    [Theory]
    [MemberData(nameof(KnownGrabberIds))]
    public void GetGrabberType_KnownIds_ReturnsExpectedType(string qualifiedItemId, object expected)
    {
        GrabberTypeHelper.GetGrabberType(qualifiedItemId).Should().Be((GrabberType)expected);
    }

    [Theory]
    [InlineData("(BC)999")]
    [InlineData("(O)128")]
    [InlineData("")]
    [InlineData("not-a-real-id")]
    public void GetGrabberType_UnknownId_ReturnsAll(string qualifiedItemId)
    {
        // Documents the "catch-all" branch contract. If a future change tightens
        // dispatch (e.g. throws on unknown), this test will catch the regression.
        GrabberTypeHelper.GetGrabberType(qualifiedItemId).Should().Be(GrabberType.All);
    }

    [Theory]
    [InlineData(BigCraftableIds.AutoGrabber, true)]
    [InlineData(BigCraftableIds.CropGrabber, true)]
    [InlineData(BigCraftableIds.ForageGrabber, true)]
    [InlineData(BigCraftableIds.TreeGrabber, true)]
    [InlineData(BigCraftableIds.ScavengerGrabber, true)]
    [InlineData(BigCraftableIds.MachineGrabber, true)]
    [InlineData("(BC)999", false)]
    [InlineData("(O)128", false)]
    [InlineData("", false)]
    public void IsGrabber_KnownAndUnknown(string qualifiedItemId, bool expected)
    {
        GrabberTypeHelper.IsGrabber(qualifiedItemId).Should().Be(expected);
    }

    [Theory]
    [InlineData(BigCraftableIds.CropGrabber, true)]
    [InlineData(BigCraftableIds.ForageGrabber, true)]
    [InlineData(BigCraftableIds.TreeGrabber, true)]
    [InlineData(BigCraftableIds.ScavengerGrabber, true)]
    [InlineData(BigCraftableIds.MachineGrabber, true)]
    [InlineData(BigCraftableIds.AutoGrabber, false)] // The vanilla grabber is NOT specialized
    [InlineData("(BC)999", false)]
    [InlineData("", false)]
    public void IsSpecializedGrabberItem_DistinguishesVanillaFromSpecialized(string qualifiedItemId, bool expected)
    {
        GrabberTypeHelper.IsSpecializedGrabberItem(qualifiedItemId).Should().Be(expected);
    }
}
