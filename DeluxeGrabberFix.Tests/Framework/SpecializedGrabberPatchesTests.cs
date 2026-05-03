using DeluxeGrabberFix.Framework;
using FluentAssertions;
using StardewValley;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

// Audit §3.4: Chest_addItem_Prefix runs on every Chest.addItem call in the game
// (player chests, mini-fridges, Automate IO, ChestsAnywhere reads). The early-out
// on SpecializedGrabberCount == 0 spares the modData lookup in the common case
// where no specialized grabbers are loaded. These tests pin the contract by
// passing a null Chest instance: if the prefix reached `__instance.modData`, we'd
// see an NRE -- the fact that it returns true cleanly proves the early-out fired.
public class SpecializedGrabberPatchesTests
{
    public class Chest_addItem_Prefix_EarlyOut
    {
        [Fact]
        public void CountZero_ReturnsTrue_WithoutTouchingChest()
        {
            int saved = SpecializedGrabberPatches.SpecializedGrabberCount;
            try
            {
                SpecializedGrabberPatches.SpecializedGrabberCount = 0;
                Item result = null!;

                // A null instance would NRE on the modData lookup; the early-out
                // returns true before reaching it.
                bool keep = SpecializedGrabberPatches.Chest_addItem_Prefix(null!, null, ref result);

                keep.Should().BeTrue();
                result.Should().BeNull();
            }
            finally
            {
                SpecializedGrabberPatches.SpecializedGrabberCount = saved;
            }
        }

        [Fact]
        public void CountNegative_ReturnsTrue_WithoutTouchingChest()
        {
            int saved = SpecializedGrabberPatches.SpecializedGrabberCount;
            try
            {
                // Defensive: a counter drifting below zero should still trip the
                // early-out, never the modData lookup with a null/garbage chest.
                SpecializedGrabberPatches.SpecializedGrabberCount = -1;
                Item result = null!;

                bool keep = SpecializedGrabberPatches.Chest_addItem_Prefix(null!, null, ref result);

                keep.Should().BeTrue();
            }
            finally
            {
                SpecializedGrabberPatches.SpecializedGrabberCount = saved;
            }
        }

        [Fact]
        public void CountPositive_FallsThroughToModDataLookup()
        {
            int saved = SpecializedGrabberPatches.SpecializedGrabberCount;
            try
            {
                SpecializedGrabberPatches.SpecializedGrabberCount = 1;
                Item result = null!;

                // With the early-out disabled (count > 0), the prefix must reach
                // `__instance.modData` -- a null instance throws. This pins that
                // the optimization is the *only* thing skipping the lookup.
                System.Action act = () =>
                    SpecializedGrabberPatches.Chest_addItem_Prefix(null!, null, ref result);

                act.Should().Throw<System.NullReferenceException>();
            }
            finally
            {
                SpecializedGrabberPatches.SpecializedGrabberCount = saved;
            }
        }
    }
}
