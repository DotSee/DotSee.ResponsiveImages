using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.HealthChecks;

namespace DotSee.ResponsiveImages.HealthChecks
{
    /// <summary>
    /// Compares each rule set's generated srcset candidates against the widths its <c>sizes</c>
    /// attribute can actually resolve to.
    /// </summary>
    /// <remarks>
    /// A candidate wider than any slot <c>sizes</c> can produce (allowing for device pixel ratio) will
    /// never be chosen by a browser — it is an image variant generated, stored and billed for nothing.
    /// This is pure configuration analysis, so it needs no media and no traffic.
    /// </remarks>
    [HealthCheck(
        "3F1B6A5E-1E38-4C0B-9E7C-6C8B1D2F5A41",
        "Responsive Images: sizes vs. srcset candidates",
        Description = "Finds generated image widths that no viewport can ever select, and rule sets with a missing or unreadable sizes attribute.",
        Group = "Configuration")]
    public class SizesCoverageHealthCheck : HealthCheck
    {
        /// <summary>
        /// Widest viewport assumed when a sizes entry has no upper bound. Wider screens exist, but
        /// treating them as the norm would excuse candidates almost nobody downloads.
        /// </summary>
        private const int DefaultReferenceViewport = 1920;

        /// <summary>Candidates within this margin of the largest usable width are left alone.</summary>
        private const double ToleranceFactor = 1.05;

        private readonly IConfigSource _configSource;

        public SizesCoverageHealthCheck(IConfigSource configSource) => _configSource = configSource;

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
                        Description = "Add rule sets under DotSee:ResponsiveImages in appsettings.json."
                    }
                });
            }

            return Task.FromResult(ruleSets.Select(Check).ToList().AsEnumerable());
        }

        private HealthCheckStatus Check(RuleSet ruleSet)
        {
            var candidates = CandidateLadder.GetSrcSetCandidates(ruleSet);
            if (candidates.Count == 0)
            {
                return new HealthCheckStatus($"'{ruleSet.Name}': no breakpoints configured.")
                {
                    ResultType = StatusResultType.Warning,
                    Description = "This rule set generates no srcset candidates at all."
                };
            }

            if (ruleSet.Sizes == null || ruleSet.Sizes.Count == 0)
            {
                return new HealthCheckStatus($"'{ruleSet.Name}': no 'sizes' configured.")
                {
                    ResultType = StatusResultType.Warning,
                    Description =
                        "&lt;ds:img&gt; falls back to a single fixed size equal to the widest breakpoint, so the browser "
                        + "assumes the image fills that width at every viewport and downloads more than it needs on small screens. "
                        + "Add a Sizes array describing how wide the image is laid out. (&lt;ds:picture&gt; is unaffected — it uses media queries.)"
                };
            }

            var parsed = SizesExpression.ParseAll(ruleSet.Sizes);
            var unreadable = parsed.Where(x => !x.IsParsed).ToList();

            if (parsed.All(x => !x.IsParsed))
            {
                return new HealthCheckStatus($"'{ruleSet.Name}': none of the 'sizes' entries could be read.")
                {
                    ResultType = StatusResultType.Info,
                    Description = "Could not analyse: " + string.Join("; ", unreadable.Select(x => "'" + x.Raw + "'"))
                        + ". Only min-width/max-width conditions and px/em/vw lengths are understood, so this may be fine."
                };
            }

            double referenceViewport = Math.Max(
                ruleSet.Breakpoints.Max(x => x.BreakPointWidth),
                DefaultReferenceViewport);

            double widestSlot = parsed.Where(x => x.IsParsed).Max(x => x.ResolveMaxWidth(referenceViewport));

            // The package appends a trailing default equal to the widest 1x candidate. A sizes list is
            // evaluated in order and the first match wins, so that default is only ever reached when no
            // earlier entry is unconditional — otherwise it is dead and cannot rescue a wide candidate.
            bool trailingDefaultIsReachable = !parsed.Any(x => x.IsParsed && x.MinViewport is null && x.MaxViewport is null);
            if (trailingDefaultIsReachable)
            {
                widestSlot = Math.Max(widestSlot, candidates.Where(c => c.DpiFactor == 1).Max(c => c.Width));
            }

            int maxDpi = CandidateLadder.GetDpiFactors(ruleSet).Max();
            double widestUseful = widestSlot * maxDpi;

            var unreachable = candidates.Where(c => c.Width > widestUseful * ToleranceFactor).ToList();

            var notes = new List<string>();
            if (unreadable.Count > 0)
            {
                notes.Add("Ignored unreadable entries: " + string.Join("; ", unreadable.Select(x => "'" + x.Raw + "'")) + ".");
            }

            if (unreachable.Count == 0)
            {
                return new HealthCheckStatus($"'{ruleSet.Name}': all {candidates.Count} candidates are reachable.")
                {
                    ResultType = StatusResultType.Success,
                    Description = string.Join(" ", new[]
                    {
                        $"Widest slot 'sizes' can resolve to is {Math.Round(widestSlot)}px"
                            + (maxDpi > 1 ? $", or {Math.Round(widestUseful)}px at {maxDpi}x." : ".")
                    }.Concat(notes))
                };
            }

            return new HealthCheckStatus(
                $"'{ruleSet.Name}': {unreachable.Count} of {candidates.Count} candidates can never be selected.")
            {
                ResultType = StatusResultType.Warning,
                Description = string.Join(" ", new[]
                {
                    $"The widest slot 'sizes' can resolve to is {Math.Round(widestSlot)}px"
                        + (maxDpi > 1 ? $" ({Math.Round(widestUseful)}px at {maxDpi}x)" : string.Empty)
                        + ", but these candidates are wider: "
                        + string.Join(", ", unreachable.Select(c => c.Width + "w")) + ". "
                        + "They are generated, stored and billed for, and no browser will ever download them. "
                        + "Either widen the Sizes entries or drop the breakpoints that produce them."
                }.Concat(notes))
            };
        }
    }
}
