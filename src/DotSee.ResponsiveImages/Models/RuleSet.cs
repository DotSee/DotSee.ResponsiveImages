using System.Collections.Generic;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.Models
{
    public class RuleSet
    {
        public RuleSet(string name)
        {
            Name = name;
            Breakpoints = new List<RuleBreakPoint>();
            Sizes = new List<string>();
        }
        public string Name { get; }
        public List<RuleBreakPoint> Breakpoints { get; set; }
        public List<string> Sizes { get; set; }
        public int? OriginalImageMaxWidth { get; set; }
        public int? OriginalImageMaxHeight { get; set; }
        public int ImageQuality { get; set; }
        public bool Use2x { get; set; }
        public bool Use3x { get; set; }
        public ImageCropMode CropMode { get; set; }
        public bool UseBreakPointWidthIfNoWidth { get; set; }
        public bool? LazyLoad { get; set; }
    }
}
