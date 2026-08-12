using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.HealthChecks;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.HealthChecks;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

public class HealthCheckTests
{
    private static IConfigSource Source(params RuleSet[] ruleSets)
        => new ConfigSource(Options.Create(ruleSets.ToList()));

    private static HealthCheckStatus Single(IEnumerable<HealthCheckStatus> statuses) => statuses.Single();

    private static RuleSet RuleSet(string name, int maxWidth, int? maxHeight, params int[] breakpoints)
    {
        var rs = new RuleSet(name) { ImageQuality = 70, OriginalImageMaxWidth = maxWidth, OriginalImageMaxHeight = maxHeight };
        foreach (var b in breakpoints)
        {
            rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = b, Width = b, Height = 0 });
        }
        return rs;
    }

    // ---- sizes coverage ----

    [Fact]
    public async Task SizesCheck_AllCandidatesReachable_IsSuccess()
    {
        var rs = RuleSet("ok", 1920, null, 576, 1200);
        rs.Sizes.Add("100vw");

        var status = Single(await new SizesCoverageHealthCheck(Source(rs)).GetStatusAsync());

        Assert.Equal(StatusResultType.Success, status.ResultType);
    }

    [Fact]
    public async Task SizesCheck_CandidateWiderThanAnySlot_IsWarning()
    {
        // An unconditional 25vw slot caps the image at 480px on a 1920 reference viewport, and being
        // unconditional it also shadows the trailing default. The configured 1600px image is unreachable.
        var rs = new RuleSet("oversized") { ImageQuality = 70, OriginalImageMaxWidth = 4000 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 600, Width = 1600, Height = 0 });
        rs.Sizes.Add("25vw");

        var status = Single(await new SizesCoverageHealthCheck(Source(rs)).GetStatusAsync());

        Assert.Equal(StatusResultType.Warning, status.ResultType);
        Assert.Contains("can never be selected", status.Message);
        Assert.Contains("1600w", status.Description);
    }

    [Fact]
    public async Task SizesCheck_ConditionalEntriesOnly_TrailingDefaultKeepsCandidatesReachable()
    {
        // Every entry is conditional, so the trailing default (the widest 1x candidate) is reachable
        // and nothing is orphaned.
        var rs = new RuleSet("conditional") { ImageQuality = 70, OriginalImageMaxWidth = 4000 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 600, Width = 1600, Height = 0 });
        rs.Sizes.Add("(max-width: 600px) 25vw");

        var status = Single(await new SizesCoverageHealthCheck(Source(rs)).GetStatusAsync());

        Assert.Equal(StatusResultType.Success, status.ResultType);
    }

    [Fact]
    public async Task SizesCheck_DpiCandidatesAreNotReportedAsUnreachable()
    {
        // With Use2x the browser legitimately wants a candidate twice the slot width.
        var rs = RuleSet("retina", 4000, null, 1200);
        rs.Use2x = true;
        rs.Sizes.Add("100vw");

        var status = Single(await new SizesCoverageHealthCheck(Source(rs)).GetStatusAsync());

        Assert.Equal(StatusResultType.Success, status.ResultType);
    }

    [Fact]
    public async Task SizesCheck_NoSizesConfigured_IsWarning()
    {
        var status = Single(await new SizesCoverageHealthCheck(Source(RuleSet("nosizes", 1920, null, 576, 1200))).GetStatusAsync());

        Assert.Equal(StatusResultType.Warning, status.ResultType);
        Assert.Contains("no 'sizes' configured", status.Message);
    }

    [Fact]
    public async Task SizesCheck_UnreadableEntries_AreReportedNotGuessedAt()
    {
        var rs = RuleSet("calc", 1920, null, 1200);
        rs.Sizes.Add("calc(100vw - 2rem)");

        var status = Single(await new SizesCoverageHealthCheck(Source(rs)).GetStatusAsync());

        Assert.Equal(StatusResultType.Info, status.ResultType);
        Assert.Contains("could be read", status.Message);
    }

    [Fact]
    public async Task SizesCheck_NoRuleSets_IsInfo()
    {
        var status = Single(await new SizesCoverageHealthCheck(Source()).GetStatusAsync());

        Assert.Equal(StatusResultType.Info, status.ResultType);
    }

    // ---- layout stability ----

    [Fact]
    public async Task DimensionsCheck_BothMaximumsSet_IsSuccess()
    {
        var status = Single(await new LayoutStabilityHealthCheck(Source(RuleSet("full", 1920, 1080, 1200))).GetStatusAsync());

        Assert.Equal(StatusResultType.Success, status.ResultType);
    }

    [Fact]
    public async Task DimensionsCheck_WidthOnly_ReportsMediaDependency()
    {
        var status = Single(await new LayoutStabilityHealthCheck(Source(RuleSet("widthonly", 1920, null, 1200))).GetStatusAsync());

        Assert.Equal(StatusResultType.Info, status.ResultType);
        Assert.Contains("umbracoWidth", status.Description);
        Assert.Contains("'widthonly'", status.Description);
    }
}
