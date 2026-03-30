using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages
{
    public static class Helpers
    {
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