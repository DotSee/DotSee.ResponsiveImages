using System.Collections.Generic;

namespace DotSee.ResponsiveImages
{
    public class SrcSetEntry
    {
        public int Breakpoint { get; set; }
        public bool Is2x { get; set; }
        public bool Is3x { get; set; }
        public string ImageUrl { get; set; }
    }
}
