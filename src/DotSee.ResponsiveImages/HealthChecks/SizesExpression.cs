using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotSee.ResponsiveImages.HealthChecks
{
    /// <summary>
    /// A single entry of a CSS <c>sizes</c> attribute — an optional media condition and a length,
    /// e.g. <c>(max-width: 576px) 100vw</c> or plain <c>50vw</c>.
    /// </summary>
    /// <remarks>
    /// Only the subset of the grammar that appears in practice is understood: <c>min-width</c> /
    /// <c>max-width</c> conditions and <c>px</c> / <c>vw</c> / <c>em</c> lengths. Anything else
    /// (<c>calc()</c>, orientation queries, and so on) is reported as unparsed rather than guessed at,
    /// so a check never claims a problem it cannot actually see.
    /// </remarks>
    public sealed class SizesExpression
    {
        private const int AssumedEmPixels = 16;

        private static readonly Regex ConditionPattern = new Regex(
            @"\(\s*(?<kind>min|max)-width\s*:\s*(?<value>\d+(\.\d+)?)\s*(?<unit>px|em)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LengthPattern = new Regex(
            @"(?<value>\d+(\.\d+)?)\s*(?<unit>vw|px|em)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private SizesExpression(string raw) => Raw = raw;

        public string Raw { get; }

        /// <summary>True when the entry could be understood well enough to resolve a width.</summary>
        public bool IsParsed { get; private set; }

        /// <summary>Upper viewport bound from a max-width condition, if any.</summary>
        public double? MaxViewport { get; private set; }

        /// <summary>Lower viewport bound from a min-width condition, if any.</summary>
        public double? MinViewport { get; private set; }

        /// <summary>Fraction of the viewport for a vw length, e.g. 0.5 for 50vw. Null for fixed lengths.</summary>
        public double? ViewportFraction { get; private set; }

        /// <summary>Fixed pixel length, for px/em entries. Null for vw lengths.</summary>
        public double? FixedWidth { get; private set; }

        public static SizesExpression Parse(string entry)
        {
            var result = new SizesExpression(entry?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(result.Raw)) { return result; }

            var remaining = result.Raw;

            foreach (Match condition in ConditionPattern.Matches(remaining))
            {
                double value = ToPixels(condition.Groups["value"].Value, condition.Groups["unit"].Value);
                if (condition.Groups["kind"].Value.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    result.MaxViewport = value;
                }
                else
                {
                    result.MinViewport = value;
                }
                remaining = remaining.Replace(condition.Value, " ");
            }

            //Anything left that isn't the length (e.g. "and", "calc(...)") means we don't fully understand it.
            var length = LengthPattern.Match(remaining.Trim());
            if (!length.Success) { return result; }

            var leftover = remaining.Trim().Substring(0, remaining.Trim().Length - length.Value.Length).Trim();
            if (leftover.Length > 0) { return result; }

            var unit = length.Groups["unit"].Value.ToLowerInvariant();
            if (unit == "vw")
            {
                result.ViewportFraction = double.Parse(length.Groups["value"].Value, CultureInfo.InvariantCulture) / 100d;
            }
            else
            {
                result.FixedWidth = ToPixels(length.Groups["value"].Value, unit);
            }

            result.IsParsed = true;
            return result;
        }

        /// <summary>
        /// The widest this entry can ever resolve to. A vw length with no upper bound is capped by
        /// <paramref name="referenceMaxViewport"/>, since "how wide can the viewport get" is otherwise
        /// unanswerable.
        /// </summary>
        public double ResolveMaxWidth(double referenceMaxViewport)
        {
            if (!IsParsed) { return 0; }
            if (FixedWidth.HasValue) { return FixedWidth.Value; }

            var upperBound = Math.Min(MaxViewport ?? referenceMaxViewport, referenceMaxViewport);

            //A min-width floor above the reference means this entry only applies on wider viewports.
            if (MinViewport.HasValue && MinViewport.Value > upperBound) { upperBound = MinViewport.Value; }

            return upperBound * ViewportFraction.GetValueOrDefault();
        }

        public static IReadOnlyList<SizesExpression> ParseAll(IEnumerable<string> entries)
        {
            return (entries ?? Enumerable.Empty<string>()).Select(Parse).ToList();
        }

        private static double ToPixels(string value, string unit)
        {
            var number = double.Parse(value, CultureInfo.InvariantCulture);
            return unit.Equals("em", StringComparison.OrdinalIgnoreCase) ? number * AssumedEmPixels : number;
        }
    }
}
