using System;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace DotSee.ResponsiveImages
{
    public static class Helpers
    {
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

        public static string CreateAttribute(string title, string value)
        {
            return (string.Concat(" ", title, "=\"", value, "\""));
        }

        public static string GetCacheKey(string ruleSetName, string imageKey)
        {
            return CacheLiteralsRS.CachedImagesClassName + ruleSetName + "_" + imageKey.ToString().Replace("-", "");
        }

        public static string GetRulesetCacheKey(string ruleSetName)
        {
            return CacheLiteralsRS.Ruleset + ruleSetName;
        }
    }
}