using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages.LazyLoad
{
    public class GlobalLazyLoadSettings : IGlobalLazyLoadSettings
    {
        public PreviewType PreviewType { get; set; }
        public string LowResImagePath { get; set; }
        public bool? EnablelazyLoad { get; set; }
        /// <summary>
        /// performs override check for a specific ruleset against the global lazyload setting
        /// </summary>
        /// <param name="ruleSet"></param>
        /// <returns></returns>
        public virtual bool IsLazyLoadEnabled(RuleSet ruleSet)
        {
            if (this.EnablelazyLoad != null)
            {
               // ruleSet is null-tolerated (an unknown rule-set name resolves to null); a missing rule
               // set inherits the global value rather than throwing.
               return ruleSet?.LazyLoad == null ? this.EnablelazyLoad.Value : ruleSet.LazyLoad.Value;
            }
            return false;
        }
    }
}