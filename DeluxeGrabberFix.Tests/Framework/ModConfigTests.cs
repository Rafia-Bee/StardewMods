using System.Collections.Generic;
using DeluxeGrabberFix;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class ModConfigTests
{
    public class IsItemExcluded
    {
        private static ModConfig FreshConfig()
        {
            // Default ctor sets every compat-exclusion flag to true and an empty
            // user excludedItems set. Tests below opt out of flags they want off.
            return new ModConfig();
        }

        [Fact]
        public void EmptyUserListAndUnrelatedItem_ReturnsFalse()
        {
            var config = FreshConfig();
            // Turn off every compat exclusion so only the user list is in play.
            config.Compatibility.sunberryVillageExclusions = false;
            config.Compatibility.visitMtVapiusExclusions = false;
            config.Compatibility.baublesExclusions = false;
            config.Compatibility.resourceChickensExclusions = false;
            config.Compatibility.capeStardewExclusions = false;

            config.IsItemExcluded("(O)128").Should().BeFalse();
        }

        [Fact]
        public void UserExcludedItem_ReturnsTrue()
        {
            var config = FreshConfig();
            config.Compatibility.excludedItems = new HashSet<string> { "(O)128" };

            config.IsItemExcluded("(O)128").Should().BeTrue();
        }

        [Fact]
        public void NullExcludedItems_DoesNotThrow_FallsThroughToCompatChecks()
        {
            // The null guard inside IsItemExcluded matters: ConfigManager has run
            // through several reshape passes, and a defensive null check protects
            // against a future Clone or save-load path leaving the set null.
            var config = FreshConfig();
            config.Compatibility.excludedItems = null!;
            config.Compatibility.sunberryVillageExclusions = false;
            config.Compatibility.visitMtVapiusExclusions = false;
            config.Compatibility.baublesExclusions = false;
            config.Compatibility.resourceChickensExclusions = false;
            config.Compatibility.capeStardewExclusions = false;

            config.IsItemExcluded("(O)128").Should().BeFalse();
        }

        [Theory]
        [InlineData("(O)skellady.SBVCP_AnnabergiteNode")]
        [InlineData("(O)skellady.SBVCP_SunsetOrbNode")]
        [InlineData("(O)skellady.SBVCP_SupplyCrate1")]
        public void SunberryFlagOn_ExcludesSunberryItems(string id)
        {
            var config = FreshConfig();

            config.IsItemExcluded(id).Should().BeTrue();
        }

        [Fact]
        public void SunberryFlagOff_DoesNotExcludeSunberryItems()
        {
            var config = FreshConfig();
            config.Compatibility.sunberryVillageExclusions = false;

            config.IsItemExcluded("(O)skellady.SBVCP_AnnabergiteNode").Should().BeFalse();
        }

        [Theory]
        [InlineData("(O)Some_Node_Foo")]
        [InlineData("(O)Visit_Node_Bar")]
        [InlineData("(O)leading_Node_trailing")]
        public void VisitMtVapiusFlagOn_ExcludesAnyIdContainingNodeMarker(string id)
        {
            // Vapius uses substring matching on "_Node_" rather than a fixed list,
            // so any qualified id with that token in its name is excluded. Pin the
            // contract so a future tightening (e.g. exact list) is a deliberate change.
            var config = FreshConfig();

            config.IsItemExcluded(id).Should().BeTrue();
        }

        [Fact]
        public void VisitMtVapiusFlagOff_DoesNotExcludeNodeIds()
        {
            var config = FreshConfig();
            config.Compatibility.visitMtVapiusExclusions = false;
            // Sunberry list contains "_Node_" ids; turn that off so the flag we are
            // testing is the only one active.
            config.Compatibility.sunberryVillageExclusions = false;

            config.IsItemExcluded("(O)Some_Node_Foo").Should().BeFalse();
        }

        [Fact]
        public void VisitMtVapiusSubstring_DoesNotMatchOnIdWithoutMarker()
        {
            var config = FreshConfig();
            // Only Vapius active.
            config.Compatibility.sunberryVillageExclusions = false;
            config.Compatibility.baublesExclusions = false;
            config.Compatibility.resourceChickensExclusions = false;
            config.Compatibility.capeStardewExclusions = false;

            config.IsItemExcluded("(O)NodeFoo").Should().BeFalse(); // missing both underscores
            config.IsItemExcluded("(O)_Nodefoo").Should().BeFalse(); // missing trailing underscore
            config.IsItemExcluded("(O)foo_Node").Should().BeFalse(); // missing leading underscore in pattern
        }

        [Theory]
        [InlineData("(O)appleseed.BCP.CattailNodeOne")]
        [InlineData("(O)appleseed.BCP.PetuntseNode")]
        public void BaublesFlagOn_ExcludesBaublesItems(string id)
        {
            var config = FreshConfig();

            config.IsItemExcluded(id).Should().BeTrue();
        }

        [Fact]
        public void BaublesFlagOff_DoesNotExcludeBaublesItems()
        {
            var config = FreshConfig();
            config.Compatibility.baublesExclusions = false;

            config.IsItemExcluded("(O)appleseed.BCP.PetuntseNode").Should().BeFalse();
        }

        [Theory]
        [InlineData("(O)UncleArya.ResourceChickens.WeedFiberEgg")]
        [InlineData("(O)UncleArya.ResourceChickens.DangerousMineRadioactiveEgg")]
        public void ResourceChickensFlagOn_ExcludesResourceChickensItems(string id)
        {
            var config = FreshConfig();

            config.IsItemExcluded(id).Should().BeTrue();
        }

        [Fact]
        public void ResourceChickensFlagOff_DoesNotExcludeResourceChickensItems()
        {
            var config = FreshConfig();
            config.Compatibility.resourceChickensExclusions = false;

            config.IsItemExcluded("(O)UncleArya.ResourceChickens.WeedFiberEgg").Should().BeFalse();
        }

        [Theory]
        [InlineData("(O)Cape.kimberliteheart")]
        [InlineData("(O)Cape.kimberlitecelestial")]
        public void CapeStardewFlagOn_ExcludesCapeStardewItems(string id)
        {
            var config = FreshConfig();

            config.IsItemExcluded(id).Should().BeTrue();
        }

        [Fact]
        public void CapeStardewFlagOff_DoesNotExcludeCapeStardewItems()
        {
            var config = FreshConfig();
            config.Compatibility.capeStardewExclusions = false;

            config.IsItemExcluded("(O)Cape.kimberliteheart").Should().BeFalse();
        }

        [Fact]
        public void UserListWinsEvenIfAllCompatFlagsOff()
        {
            var config = FreshConfig();
            config.Compatibility.sunberryVillageExclusions = false;
            config.Compatibility.visitMtVapiusExclusions = false;
            config.Compatibility.baublesExclusions = false;
            config.Compatibility.resourceChickensExclusions = false;
            config.Compatibility.capeStardewExclusions = false;
            config.Compatibility.excludedItems = new HashSet<string> { "(O)999" };

            config.IsItemExcluded("(O)999").Should().BeTrue();
            config.IsItemExcluded("(O)128").Should().BeFalse();
        }

        [Fact]
        public void EmptyString_ReturnsFalse_OnDefaultConfig()
        {
            // Empty string isn't in any list and doesn't contain "_Node_". Pin behavior
            // because IsItemExcluded gets called with QualifyItemId(...) results which
            // can theoretically be empty for an unknown id; we don't want it crashing
            // or over-excluding.
            var config = FreshConfig();

            config.IsItemExcluded(string.Empty).Should().BeFalse();
        }
    }

    public class Clone
    {
        [Fact]
        public void Clone_CopiesScalarFields()
        {
            var original = new ModConfig
            {
                grabberMode = ModConfig.GrabberMode.Specialized,
                globalGrabber = ModConfig.GlobalGrabberMode.All,
                grabFrequency = ModConfig.GrabFrequency.Hourly,
                Features = { harvestCropsRange = 7 },
                Specialized = { cropsShippedThreshold = 99 },
                Machines = { automateCompatibility = false }
            };

            var clone = original.Clone();

            clone.grabberMode.Should().Be(ModConfig.GrabberMode.Specialized);
            clone.globalGrabber.Should().Be(ModConfig.GlobalGrabberMode.All);
            clone.grabFrequency.Should().Be(ModConfig.GrabFrequency.Hourly);
            clone.Features.harvestCropsRange.Should().Be(7);
            clone.Specialized.cropsShippedThreshold.Should().Be(99);
            clone.Machines.automateCompatibility.Should().BeFalse();
        }

        [Fact]
        public void Clone_DeepCopiesExcludedItems()
        {
            // Clone is used by PerSaveConfigManager to snapshot per-save state.
            // If excludedItems was a shallow copy, mutating one would mutate the other
            // and you'd get cross-save bleed.
            var original = new ModConfig();
            original.Compatibility.excludedItems = new HashSet<string> { "(O)128" };

            var clone = original.Clone();
            clone.Compatibility.excludedItems.Add("(O)999");

            original.Compatibility.excludedItems.Should().ContainSingle().Which.Should().Be("(O)128");
            clone.Compatibility.excludedItems.Should().HaveCount(2);
        }

        [Fact]
        public void Clone_DeepCopiesSkippedLocations()
        {
            var original = new ModConfig();
            original.Locations.SkippedLocations = new HashSet<string> { "Farm" };

            var clone = original.Clone();
            clone.Locations.SkippedLocations.Add("Town");

            original.Locations.SkippedLocations.Should().ContainSingle().Which.Should().Be("Farm");
            clone.Locations.SkippedLocations.Should().HaveCount(2);
        }

        [Fact]
        public void Clone_NullExcludedItems_BecomesEmptySetOnClone()
        {
            // Defensive: a config in some legacy state could deserialize with null sets.
            // Clone normalizes to a fresh empty HashSet so callers can safely Add.
            var original = new ModConfig();
            original.Compatibility.excludedItems = null!;
            original.Locations.SkippedLocations = null!;

            var clone = original.Clone();

            clone.Compatibility.excludedItems.Should().NotBeNull().And.BeEmpty();
            clone.Locations.SkippedLocations.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Clone_NestedGroups_AreIndependentInstances()
        {
            // Pins the invariant that Clone re-instantiates each nested group rather
            // than aliasing it. If the groups aliased, setting a scalar on the clone
            // would silently mutate the original — the exact bug PerSaveConfigManager's
            // Clone snapshot is meant to prevent.
            var original = new ModConfig();
            var clone = original.Clone();

            clone.Features.Should().NotBeSameAs(original.Features);
            clone.Machines.Should().NotBeSameAs(original.Machines);
            clone.Specialized.Should().NotBeSameAs(original.Specialized);
            clone.Locations.Should().NotBeSameAs(original.Locations);
            clone.GlobalGrab.Should().NotBeSameAs(original.GlobalGrab);
            clone.Compatibility.Should().NotBeSameAs(original.Compatibility);
        }

        [Fact]
        public void Clone_ScalarMutationOnCloneGroup_DoesNotBleedToOriginal()
        {
            var original = new ModConfig();
            original.Features.harvestCrops = false;
            original.Machines.collectKegs = true;

            var clone = original.Clone();
            clone.Features.harvestCrops = true;
            clone.Machines.collectKegs = false;

            original.Features.harvestCrops.Should().BeFalse();
            original.Machines.collectKegs.Should().BeTrue();
        }
    }

    public class Defaults
    {
        [Fact]
        public void DefaultCtor_InitializesAllNestedGroups()
        {
            // SMAPI deserializes config.json onto a freshly constructed ModConfig.
            // If a group is missing from the user's JSON, the field initializer keeps
            // it non-null so callers don't NRE on first access.
            var config = new ModConfig();

            config.Features.Should().NotBeNull();
            config.Machines.Should().NotBeNull();
            config.Specialized.Should().NotBeNull();
            config.Locations.Should().NotBeNull();
            config.GlobalGrab.Should().NotBeNull();
            config.Compatibility.Should().NotBeNull();
        }
    }
}
