using System.Collections.Generic;
using DeluxeGrabberFix;
using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

public class GrabberManagerTests
{
    public class ShouldFireGlobalGrab
    {
        // GrabberMode and GlobalGrabberMode are internal, so keep them inside object[]
        // arrays (MemberData) rather than expose them on a public [Theory] signature.
        private static ModConfig ConfigFor(ModConfig.GrabberMode grabberMode, ModConfig.GlobalGrabberMode globalGrabber)
        {
            var config = new ModConfig();
            config.grabberMode = grabberMode;
            config.globalGrabber = globalGrabber;
            return config;
        }

        public static IEnumerable<object[]> AllModes => new[]
        {
            new object[] { ModConfig.GrabberMode.Classic },
            new object[] { ModConfig.GrabberMode.Specialized }
        };

        [Theory]
        [MemberData(nameof(AllModes))]
        internal void Off_ReturnsFalse_RegardlessOfMode(ModConfig.GrabberMode mode)
        {
            var config = ConfigFor(mode, ModConfig.GlobalGrabberMode.Off);

            // Off blocks both modes. hasDesignated is irrelevant.
            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: false).Should().BeFalse();
            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: true).Should().BeFalse();
        }

        [Fact]
        public void ClassicAll_NoDesignatedGrabber_ReturnsFalse()
        {
            // Classic + All requires a designated grabber to know where to route items.
            var config = ConfigFor(ModConfig.GrabberMode.Classic, ModConfig.GlobalGrabberMode.All);

            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: false).Should().BeFalse();
        }

        [Fact]
        public void ClassicAll_WithDesignatedGrabber_ReturnsTrue()
        {
            var config = ConfigFor(ModConfig.GrabberMode.Classic, ModConfig.GlobalGrabberMode.All);

            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: true).Should().BeTrue();
        }

        [Fact]
        public void ClassicHover_PassesThrough()
        {
            // Classic + Hover routes through MapGrabber's Hover branch using the cursor
            // target captured by the GrabSession; no designated-grabber requirement.
            var config = ConfigFor(ModConfig.GrabberMode.Classic, ModConfig.GlobalGrabberMode.Hover);

            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: false).Should().BeTrue();
            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: true).Should().BeTrue();
        }

        public static IEnumerable<object[]> SpecializedNonOff => new[]
        {
            new object[] { ModConfig.GlobalGrabberMode.All },
            new object[] { ModConfig.GlobalGrabberMode.Hover }
        };

        [Theory]
        [MemberData(nameof(SpecializedNonOff))]
        internal void Specialized_NonOff_ReturnsTrue(ModConfig.GlobalGrabberMode globalGrabber)
        {
            // Specialized has no per-mode designation requirement at this layer.
            // The designated cache is built by GrabSession; the predicate just
            // gates Off (already covered above) and the Classic+All special case.
            var config = ConfigFor(ModConfig.GrabberMode.Specialized, globalGrabber);

            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: false).Should().BeTrue();
            GrabberManager.ShouldFireGlobalGrab(config, hasDesignatedGrabber: true).Should().BeTrue();
        }
    }
}
