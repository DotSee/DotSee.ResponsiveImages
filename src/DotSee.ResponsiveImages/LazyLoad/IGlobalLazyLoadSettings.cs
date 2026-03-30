using System;
using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages.LazyLoad
{
    public interface IGlobalLazyLoadSettings
    {
        public bool? EnablelazyLoad { get; set; }
        public PreviewType PreviewType { get; set; }
        public String LowResImagePath { get; set; }
        /// <summary>
        /// performs override check for a specific ruleset against the global lazyload setting
        /// </summary>
        /// <param name="ruleSet"></param>
        /// <returns></returns>
        public bool IsLazyLoadEnabled(RuleSet ruleSet);
    }

    public enum PreviewType
    {
        Blur = 1,
        LowResImage = 2
    }
}