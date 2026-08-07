using System.Collections.Generic;

namespace DotSee.ResponsiveImages
{
    public class SrcSetEntry
    {
        /// <summary>
        /// The layout breakpoint this candidate was derived from. Drives the "sizes" fallback,
        /// which describes layout width and so must ignore the extra DPI candidates.
        /// </summary>
        public int Breakpoint { get; set; }

        /// <summary>
        /// Actual pixel width of the generated image — the value advertised by the srcset "w" descriptor.
        /// </summary>
        public int Width { get; set; }

        public bool Is2x { get; set; }
        public bool Is3x { get; set; }
        public string ImageUrl { get; set; }
    }
}
