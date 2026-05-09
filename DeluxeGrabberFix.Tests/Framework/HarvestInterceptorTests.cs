using System;
using DeluxeGrabberFix.Framework;
using FluentAssertions;
using Xunit;

namespace DeluxeGrabberFix.Tests.Framework;

// HarvestInterceptor is a static; tests must end every Begin with an End so they don't
// leave the global flag set for the next test in the run order. The reentry-guard test
// catches the throw and then ends the outer frame to clean up.
public class HarvestInterceptorTests
{
    [Fact]
    public void BeginEndCycle_StartsIdle_AndIdleAfterEnd()
    {
        HarvestInterceptor.IsIntercepting.Should().BeFalse();
        HarvestInterceptor.BeginIntercept();
        HarvestInterceptor.IsIntercepting.Should().BeTrue();
        HarvestInterceptor.EndIntercept();
        HarvestInterceptor.IsIntercepting.Should().BeFalse();
    }

    [Fact]
    public void EndIntercept_WithoutBegin_ReturnsEmptyList_DoesNotThrow()
    {
        HarvestInterceptor.IsIntercepting.Should().BeFalse();
        var items = HarvestInterceptor.EndIntercept();
        items.Should().BeEmpty();
        HarvestInterceptor.IsIntercepting.Should().BeFalse();
    }

    [Fact]
    public void BeginIntercept_WhileAlreadyIntercepting_Throws()
    {
        try
        {
            HarvestInterceptor.BeginIntercept();
            Action reentry = () => HarvestInterceptor.BeginIntercept();
            reentry.Should().Throw<InvalidOperationException>()
                .WithMessage("*reentrant harvest cycle*");
        }
        finally
        {
            HarvestInterceptor.EndIntercept();
        }
    }

    [Fact]
    public void CreateItemDebrisPrefix_NotIntercepting_AllowsOriginal()
    {
        HarvestInterceptor.IsIntercepting.Should().BeFalse();
        bool runOriginal = HarvestInterceptor.CreateItemDebris_Prefix(item: null!);
        runOriginal.Should().BeTrue();
    }
}
