using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.HealthChecks;

namespace DotSee.ResponsiveImages.HealthChecks
{
    /// <summary>
    /// Reports whether each rule set can emit <c>width</c> and <c>height</c> attributes, which is what
    /// lets the browser reserve the right box before an image arrives instead of reflowing the page
    /// around it once it does.
    /// </summary>
    /// <remarks>
    /// A rule set that sets both maximum dimensions always emits them. One that sets only a width has to
    /// borrow the aspect ratio from each media item's own umbracoWidth/umbracoHeight, so it depends on
    /// the media being complete. One that sets neither depends on the media entirely.
    /// </remarks>
    [HealthCheck(
        "C4A97E02-58D9-4A6F-B0D1-2E5F7C3A9B18",
        "Responsive Images: layout stability",
        Description = "Checks which rule sets can emit width/height attributes to prevent layout shift as images load.",
        Group = "Configuration")]
    public class LayoutStabilityHealthCheck : HealthCheck
    {
        private readonly IConfigSource _configSource;

        public LayoutStabilityHealthCheck(IConfigSource configSource) => _configSource = configSource;

        public override Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
        {
            var ruleSets = _configSource?.AllRuleSets;

            if (ruleSets == null || ruleSets.Count == 0)
            {
                return Task.FromResult<IEnumerable<HealthCheckStatus>>(new[]
                {
                    new HealthCheckStatus("No responsive image rule sets are configured.")
                    {
                        ResultType = StatusResultType.Info,
                        Description = "Add rule sets under DotSee:ResponsiveImages:RuleSets in appsettings.json."
                    }
                });
            }

            var always = ruleSets.Where(x => x.OriginalImageMaxWidth > 0 && x.OriginalImageMaxHeight > 0).ToList();
            var mediaDependent = ruleSets.Except(always).ToList();

            var statuses = new List<HealthCheckStatus>
            {
                always.Count == ruleSets.Count
                    ? new HealthCheckStatus($"All {ruleSets.Count} rule sets always emit width and height.")
                    {
                        ResultType = StatusResultType.Success,
                        Description = "Every rule set sets both OriginalImageMaxWidth and OriginalImageMaxHeight, "
                            + "so dimensions are known from configuration alone."
                    }
                    : new HealthCheckStatus(
                        $"{mediaDependent.Count} of {ruleSets.Count} rule sets depend on media dimensions for width/height.")
                    {
                        ResultType = StatusResultType.Info,
                        Description = BuildDescription(always, mediaDependent)
                    }
            };

            return Task.FromResult(statuses.AsEnumerable());
        }

        private static string BuildDescription(IReadOnlyCollection<RuleSet> always, IReadOnlyCollection<RuleSet> mediaDependent)
        {
            var parts = new List<string>();

            if (always.Count > 0)
            {
                parts.Add("Always emit dimensions (both maximums set): "
                    + string.Join(", ", always.Select(x => "'" + x.Name + "'")) + ".");
            }

            var widthOnly = mediaDependent.Where(x => x.OriginalImageMaxWidth > 0).Select(x => x.Name).ToList();
            var heightOnly = mediaDependent.Where(x => x.OriginalImageMaxWidth is null or 0 && x.OriginalImageMaxHeight > 0).Select(x => x.Name).ToList();
            var neither = mediaDependent
                .Where(x => x.OriginalImageMaxWidth is null or 0 && x.OriginalImageMaxHeight is null or 0)
                .Select(x => x.Name).ToList();

            if (widthOnly.Count > 0)
            {
                parts.Add("Only a maximum width is set for " + string.Join(", ", widthOnly.Select(x => "'" + x + "'"))
                    + ", so the height is derived from each media item's umbracoWidth/umbracoHeight. "
                    + "Media missing those properties will render without dimensions and can shift the layout.");
            }

            if (heightOnly.Count > 0)
            {
                parts.Add("Only a maximum height is set for " + string.Join(", ", heightOnly.Select(x => "'" + x + "'"))
                    + ", with the width derived the same way.");
            }

            if (neither.Count > 0)
            {
                parts.Add("Neither maximum is set for " + string.Join(", ", neither.Select(x => "'" + x + "'"))
                    + ", so dimensions come entirely from the media item.");
            }

            parts.Add("Setting both OriginalImageMaxWidth and OriginalImageMaxHeight makes this independent of the media library.");

            return string.Join(" ", parts);
        }
    }
}
