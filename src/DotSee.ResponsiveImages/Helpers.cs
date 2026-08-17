using System;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace DotSee.ResponsiveImages
{
    public static class Helpers
    {
        /// <summary>
        /// Encoder for attribute values. Allows the full Unicode range so non-Latin alt text stays
        /// readable in the markup instead of becoming numeric entities; the HTML-significant characters
        /// are still encoded regardless of the allowed ranges.
        /// </summary>
        private static readonly HtmlEncoder AttributeEncoder = HtmlEncoder.Create(UnicodeRanges.All);

        /// <summary>
        /// Reads the media item's stored pixel dimensions (umbracoWidth / umbracoHeight).
        /// </summary>
        public static bool TryGetIntrinsicSize(IPublishedContent image, out int width, out int height)
        {
            width = 0;
            height = 0;
            return image != null
                && TryReadInt(image, "umbracoWidth", out width)
                && TryReadInt(image, "umbracoHeight", out height)
                && width > 0
                && height > 0;
        }

        /// <summary>
        /// Works out the pixel dimensions to declare on an image, so the browser can reserve the right
        /// box before it arrives (otherwise the page shifts as it loads).
        /// </summary>
        /// <remarks>
        /// Derived from the largest image the markup can actually deliver, <b>not</b> from the rule set's
        /// <see cref="RuleSet.OriginalImageMaxWidth"/>. That is only a ceiling — a rule set may cap at
        /// 1920 while every breakpoint asks for 400px — and declaring the ceiling would tell the browser
        /// to lay the image out several times larger than anything it will download, upscaling it on any
        /// page without a CSS rule to cap it. The candidate's own height is used when the rule set fixes
        /// one, so the declared aspect ratio matches the delivered crop rather than the source photo's.
        /// </remarks>
        /// <param name="largest">The widest candidate the srcset (or picture sources) will offer.</param>
        public static bool TryGetRenderedSize(IPublishedContent image, ImageCandidate largest, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (largest == null || largest.Width <= 0) { return false; }

            width = largest.Width;

            //The rule set fixed a height, so the delivered crop's own ratio is known.
            if (largest.Height > 0)
            {
                height = largest.Height;
                return true;
            }

            //Height follows the source aspect ratio; borrow it from the media item.
            if (!TryGetIntrinsicSize(image, out int intrinsicWidth, out int intrinsicHeight))
            {
                //A single dimension is not enough to reserve a box, so emit nothing rather than guess.
                width = 0;
                return false;
            }

            height = (int)Math.Round(width * (intrinsicHeight / (double)intrinsicWidth));
            return height > 0;
        }

        private static bool TryReadInt(IPublishedContent content, string alias, out int value)
        {
            value = 0;
            try
            {
                var raw = content.GetProperty(alias)?.GetValue();
                return raw != null && int.TryParse(raw.ToString(), out value);
            }
            catch
            {
                //Property access can throw on partially-initialised or mocked content; treat as unknown.
                return false;
            }
        }

        /// <summary>
        /// Whether the media item is an SVG, which bypasses the whole crop pipeline — there is nothing
        /// to resize and image processors can't transform it. Case-insensitive (".SVG" exports are
        /// common) and tolerant of a query string on the URL, and shared so the renderers and the
        /// preload builders cannot disagree about what counts as an SVG.
        /// </summary>
        public static bool IsSvg(IPublishedContent image)
        {
            string url = image?.Url();
            if (string.IsNullOrEmpty(url)) { return false; }

            int queryStart = url.IndexOf('?');
            if (queryStart >= 0) { url = url.Substring(0, queryStart); }

            return string.Equals(System.IO.Path.GetExtension(url), ".svg", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetBreakPointWidth(RuleBreakPoint b, RuleSet ruleSet)
        {
            return (b.Width > 0)
                    ? b.Width
                    : (ruleSet.OriginalImageMaxWidth != null && b.BreakPointWidth > (int)ruleSet.OriginalImageMaxWidth)
                        ? (int)ruleSet.OriginalImageMaxWidth
                        : ruleSet.UseBreakPointWidthIfNoWidth
                            ? b.BreakPointWidth
                            : CalcWidth(ruleSet, b.Height);
        }

        public static int GetBreakPointHeight(RuleBreakPoint b, RuleSet ruleSet)
        {
            return (ruleSet.OriginalImageMaxHeight != null && b.Height > (int)ruleSet.OriginalImageMaxHeight)
                    ? (int)ruleSet.OriginalImageMaxHeight
                    : b.Height;
        }

        public static int CalcHeight(RuleSet ruleSet, int paramNewWidth)
        {
            //Both max dimensions must be present
            if (ruleSet.OriginalImageMaxHeight == null || ruleSet.OriginalImageMaxWidth == null) { return 0; }

            float oldWidth = (float)ruleSet.OriginalImageMaxWidth;
            float scaleFactor = paramNewWidth / oldWidth;
            if (scaleFactor > 1) { scaleFactor = 1; }
            float newHeight = (int)ruleSet.OriginalImageMaxHeight * scaleFactor;
            
            return ((int)newHeight);
        }

        public static int CalcWidth(RuleSet ruleSet, int paramNewHeight)
        {
            //Both max dimensions must be present
            if (ruleSet.OriginalImageMaxWidth == null || ruleSet.OriginalImageMaxHeight == null) { return 0; }

            float oldHeight = (float)ruleSet.OriginalImageMaxHeight;
            float scaleFactor = paramNewHeight / oldHeight;
            if (scaleFactor > 1) { scaleFactor = 1; }

            float newWidth = (int)ruleSet.OriginalImageMaxWidth * scaleFactor;
            return ((int)newWidth);
        }

        /// <summary>
        /// Renders one HTML attribute. The value is HTML-encoded here — this is the single sink for
        /// alt text, titles, classes, URLs and caller-supplied attribute dictionaries, all of which can
        /// carry editor-entered content, so encoding anywhere else would leave a gap. The name is
        /// restricted to attribute-legal characters so a hostile dictionary key cannot smuggle in a
        /// second attribute.
        /// </summary>
        public static string CreateAttribute(string title, string value)
        {
            return (string.Concat(" ", SanitizeAttributeName(title), "=\"", HtmlAttributeEncode(value), "\""));
        }

        /// <summary>HTML-encodes text for use inside a double-quoted attribute value.</summary>
        public static string HtmlAttributeEncode(string value)
        {
            return string.IsNullOrEmpty(value) ? value ?? string.Empty : AttributeEncoder.Encode(value);
        }

        private static string SanitizeAttributeName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return name ?? string.Empty; }

            Span<char> buffer = name.Length <= 128 ? stackalloc char[name.Length] : new char[name.Length];
            int kept = 0;
            foreach (char c in name)
            {
                if (char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.')
                {
                    buffer[kept++] = c;
                }
            }

            return kept == name.Length ? name : new string(buffer[..kept]);
        }

        /// <summary>
        /// Makes a URL safe to interpolate into a CSS <c>url('…')</c>. Inside a &lt;style&gt; block HTML
        /// entities are NOT decoded, so HTML-encoding is the wrong tool there; percent-encoding the
        /// CSS-significant characters preserves the URL's meaning in both the attribute and the
        /// stylesheet context. Data URIs are our own base64 output and pass through untouched.
        /// </summary>
        public static string SanitizeCssUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) { return url ?? string.Empty; }
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) { return url; }

            return url
                .Replace("\\", "%5C")
                .Replace("'", "%27")
                .Replace("\"", "%22")
                .Replace("<", "%3C")
                .Replace(">", "%3E")
                .Replace("(", "%28")
                .Replace(")", "%29")
                .Replace(" ", "%20");
        }

        /// <summary>
        /// True when the string is a plain HTML attribute name (letter first, then letters, digits or
        /// dashes). Used to whitelist caller-supplied attribute <em>names</em>, which are written into
        /// the tag as markup and therefore cannot be made safe by encoding.
        /// </summary>
        public static bool IsValidAttributeName(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsAsciiLetter(name[0])) { return false; }

            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsAsciiLetterOrDigit(name[i]) && name[i] != '-') { return false; }
            }

            return true;
        }

        /// <summary>
        /// True when the string is safe to interpolate as a CSP nonce attribute value: the base64 /
        /// base64url alphabet browsers actually issue. Anything else is rejected rather than encoded —
        /// a "nonce" that needs escaping is not a nonce.
        /// </summary>
        public static bool IsValidNonce(string nonce)
        {
            if (string.IsNullOrWhiteSpace(nonce)) { return false; }

            foreach (char c in nonce)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '/' && c != '=' && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Key for a rendered background-CSS block, which doubles as the generated CSS class name. The
        /// optional variant carries everything else the CSS depends on (per-call query string, WebP,
        /// focal point) as a short hash — without it two variants share one cache entry AND one class
        /// name, so whichever renders first wins for both. Null/empty variant keeps the historical
        /// key/class shape.
        /// </summary>
        public static string GetCacheKey(string ruleSetName, string imageKey, string variant = null)
        {
            var key = CacheLiteralsRS.CachedImagesClassName + ruleSetName + "_" + imageKey.ToString().Replace("-", "");
            return string.IsNullOrEmpty(variant) ? key : key + "_" + ShortHash(variant);
        }

        /// <summary>
        /// The variant discriminator for background CSS: the effective query string plus the focal
        /// point. Returns null when neither applies, so the common case keeps its historical class name.
        /// </summary>
        public static string GetCssVariant(Umbraco.Cms.Core.Models.MediaWithCrops image, string effectiveQueryString)
        {
            var focalPoint = image?.LocalCrops?.FocalPoint;
            string focalToken = focalPoint == null
                ? null
                : focalPoint.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)
                  + "," + focalPoint.Top.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(effectiveQueryString) && focalToken == null) { return null; }

            return (effectiveQueryString ?? string.Empty) + "|" + focalToken;
        }

        /// <summary>
        /// Deterministic (FNV-1a) short hash. <see cref="string.GetHashCode()"/> is randomised per
        /// process, which would make cache keys and generated class names differ across restarts.
        /// </summary>
        public static string ShortHash(string value)
        {
            if (string.IsNullOrEmpty(value)) { return "0"; }

            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return hash.ToString("x8");
            }
        }

        public static string GetRulesetCacheKey(string ruleSetName)
        {
            return CacheLiteralsRS.Ruleset + ruleSetName;
        }
    }
}