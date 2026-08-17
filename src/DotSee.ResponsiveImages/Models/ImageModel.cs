using DotSee.ResponsiveImages.LazyLoad;
using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages
{
    public class ImageModel
    {
        public string ImageGuid { get; set; }
        public int OriginalImageId { get; set; }
        public MediaWithCrops OriginalImage { get; set; }
        public string CacheKey { get; set; }
        public int ImageTop { get; set; }
        public int ImageLeft { get; set; }
        public string OriginalUrl { get; set; }
        public bool IsSvg { get; set; }
        public string LowResImagePath { get; set; }

        /// <summary>
        /// The per-call query string the breakpoint URLs were built with, so the 2x/3x URLs the renderer
        /// derives itself carry the same options rather than quietly dropping them.
        /// </summary>
        public string QueryString { get; set; }

        public RuleSet RuleSet { get; set; }
        public IOrderedEnumerable<RuleBreakPoint> OrderedBreakPoints { get; set; }
        public IEnumerable<ImageBreakPointModel> BreakPoints { get; set; }
    }
}