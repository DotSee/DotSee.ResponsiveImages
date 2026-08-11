using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages
{
    /// <summary>
    /// Works out which image sizes a rule set will generate, independently of any particular image.
    /// </summary>
    /// <remarks>
    /// The renderers, the health checks and the CDN purge all need to agree on this. Keeping the
    /// arithmetic here means a rule change lands in one place instead of drifting between them.
    ///
    /// The two ladders are deliberately separate because they answer different questions and have
    /// always used different arithmetic: <see cref="GetSrcSetCandidates"/> produces the flat, globally
    /// deduplicated "w" ladder for a single &lt;img srcset&gt;, while <see cref="GetPictureSources"/>
    /// produces per-breakpoint groups for &lt;picture&gt;, where each group is one &lt;source&gt;.
    /// </remarks>
    public static class CandidateLadder
    {
        /// <summary>
        /// Flat list of candidates for an <c>&lt;img srcset&gt;</c>, ordered by width and deduplicated:
        /// one per breakpoint, plus the configured DPI multiples, each clamped to the rule set maximums.
        /// </summary>
        public static IReadOnlyList<ImageCandidate> GetSrcSetCandidates(RuleSet ruleSet)
        {
            var results = new List<ImageCandidate>();
            if (ruleSet?.Breakpoints == null) { return results; }

            var factors = GetDpiFactors(ruleSet);
            int maxWidth = ruleSet.OriginalImageMaxWidth ?? 0;
            int maxHeight = ruleSet.OriginalImageMaxHeight ?? 0;

            //Two breakpoints (or a breakpoint and a DPI variant) can resolve to the same pixel width
            //once clamped. Emitting it twice buys nothing and costs an extra variant to generate.
            var emittedWidths = new HashSet<int>();

            foreach (var b in ruleSet.Breakpoints.OrderBy(x => x.BreakPointWidth))
            {
                int height = (b.Width > 0 && b.Height == 0)
                    ? Helpers.CalcHeight(ruleSet, b.Width)
                    : (b.Height > 0)
                        ? b.Height
                        : 0;

                int width = (b.Height > 0 && b.Width == 0)
                        ? Helpers.CalcWidth(ruleSet, b.Height)
                        : (b.Width > 0)
                            ? b.Width
                            : b.BreakPointWidth;

                //Respect original image max dimensions
                if (maxWidth > 0 && width > maxWidth) { width = maxWidth; }
                if (maxHeight > 0 && height > maxHeight) { height = maxHeight; }

                foreach (int factor in factors)
                {
                    int candidateWidth = width * factor;
                    int candidateHeight = height * factor;

                    if (maxWidth > 0 && candidateWidth > maxWidth)
                    {
                        candidateWidth = maxWidth;
                        if (candidateHeight > 0)
                        {
                            int recalculated = Helpers.CalcHeight(ruleSet, candidateWidth);
                            candidateHeight = recalculated > 0 ? recalculated : candidateHeight;
                        }
                    }
                    if (maxHeight > 0 && candidateHeight > maxHeight) { candidateHeight = maxHeight; }

                    if (candidateWidth <= 0 || !emittedWidths.Add(candidateWidth)) { continue; }

                    results.Add(new ImageCandidate(b.BreakPointWidth, candidateWidth, candidateHeight, factor));
                }
            }

            return results;
        }

        /// <summary>
        /// One entry per <c>&lt;source&gt;</c> a <c>&lt;picture&gt;</c> will contain, largest breakpoint
        /// first, including the synthetic breakpoint that covers viewports below the smallest configured
        /// one. Each entry carries its 1x candidate plus any configured DPI candidates.
        /// </summary>
        public static IReadOnlyList<PictureSource> GetPictureSources(RuleSet ruleSet)
        {
            var results = new List<PictureSource>();
            if (ruleSet?.Breakpoints == null) { return results; }

            foreach (var bp in GetOrderedBreakPoints(ruleSet))
            {
                int height = Helpers.GetBreakPointHeight(bp, ruleSet);

                int width1x = ScaledWidth(ruleSet, bp, 1);
                int height1x = ScaledHeight(ruleSet, height, width1x, 1);

                var candidates = new List<ImageCandidate>
                {
                    new ImageCandidate(bp.BreakPointWidth, width1x, height1x, 1)
                };

                foreach (int factor in GetDpiFactors(ruleSet).Where(x => x > 1))
                {
                    int scaledWidth = ScaledWidth(ruleSet, bp, factor);
                    int scaledHeight = ScaledHeight(ruleSet, height, scaledWidth, factor);

                    //A clamped or absent width can collapse onto the 1x candidate; skip the duplicate.
                    if (scaledWidth == width1x && scaledHeight == height1x) { continue; }

                    candidates.Add(new ImageCandidate(bp.BreakPointWidth, scaledWidth, scaledHeight, factor));
                }

                results.Add(new PictureSource(bp, width1x, height1x, candidates));
            }

            return results;
        }

        /// <summary>
        /// Breakpoints largest first, with an artificial 1px breakpoint appended to preserve the
        /// smallest breakpoint's settings below itself. Returns a copy: the rule set instance is cached
        /// and shared across requests, so the synthetic entry must never be added to it.
        /// </summary>
        public static IReadOnlyList<RuleBreakPoint> GetOrderedBreakPoints(RuleSet ruleSet)
        {
            var ordered = ruleSet.Breakpoints.OrderByDescending(x => x.BreakPointWidth).ToList();

            if (ordered.Count > 0 && ordered.Last().BreakPointWidth > 1)
            {
                var smallest = ordered.Last();
                ordered.Add(new RuleBreakPoint
                {
                    BreakPointWidth = 1,
                    Width = smallest.Width,
                    Height = smallest.Height
                });
            }

            return ordered;
        }

        /// <summary>The device pixel ratios the rule set generates for, always including 1.</summary>
        public static IReadOnlyList<int> GetDpiFactors(RuleSet ruleSet)
        {
            var factors = new List<int> { 1 };
            if (ruleSet.Use2x) { factors.Add(2); }
            if (ruleSet.Use3x) { factors.Add(3); }
            return factors;
        }

        //Pixel width generated for this breakpoint at the given DPI factor.
        private static int ScaledWidth(RuleSet ruleSet, RuleBreakPoint bp, int factor)
        {
            if (bp.Width <= 0) { return 0; }

            return (ruleSet.OriginalImageMaxWidth != null && bp.DefinedImageWidth > (int)ruleSet.OriginalImageMaxWidth)
                ? (int)ruleSet.OriginalImageMaxWidth * factor
                : bp.DefinedImageWidth * factor;
        }

        // If height is present make sure that we don't exceed maximum width or height if present.
        // Calculate the new height or use the original one in case calculation returns 0 which means that
        // either max constraints have not been set or some other issue allows us to use original height.
        private static int ScaledHeight(RuleSet ruleSet, int breakPointHeight, int scaledWidth, int factor)
        {
            if (breakPointHeight <= 0) { return 0; }

            var calculated = Helpers.CalcHeight(ruleSet, scaledWidth);
            return calculated > 0 ? calculated : breakPointHeight * factor;
        }
    }
}
