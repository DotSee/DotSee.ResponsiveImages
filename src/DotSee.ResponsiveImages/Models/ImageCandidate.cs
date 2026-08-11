using System.Collections.Generic;

namespace DotSee.ResponsiveImages.Models
{
    /// <summary>
    /// One image the package will ask the image processor to generate: a specific pixel size, and the
    /// breakpoint and device pixel ratio it exists to serve.
    /// </summary>
    public sealed class ImageCandidate
    {
        public ImageCandidate(int breakPointWidth, int width, int height, int dpiFactor)
        {
            BreakPointWidth = breakPointWidth;
            Width = width;
            Height = height;
            DpiFactor = dpiFactor;
        }

        /// <summary>The layout breakpoint this candidate serves.</summary>
        public int BreakPointWidth { get; }

        /// <summary>Generated pixel width. This is what the srcset "w" descriptor must advertise.</summary>
        public int Width { get; }

        /// <summary>Generated pixel height, or 0 when height is left to the source aspect ratio.</summary>
        public int Height { get; }

        /// <summary>Device pixel ratio this candidate targets: 1, 2 or 3.</summary>
        public int DpiFactor { get; }
    }

    /// <summary>
    /// One <c>&lt;source&gt;</c> of a <c>&lt;picture&gt;</c>: a breakpoint and every DPI candidate for it.
    /// </summary>
    public sealed class PictureSource
    {
        public PictureSource(RuleBreakPoint breakPoint, int width, int height, IReadOnlyList<ImageCandidate> candidates)
        {
            BreakPoint = breakPoint;
            Width = width;
            Height = height;
            Candidates = candidates;
        }

        public RuleBreakPoint BreakPoint { get; }

        /// <summary>1x width, also used for the source's width attribute.</summary>
        public int Width { get; }

        /// <summary>1x height, also used for the source's height attribute.</summary>
        public int Height { get; }

        /// <summary>The 1x candidate first, then any 2x/3x candidates.</summary>
        public IReadOnlyList<ImageCandidate> Candidates { get; }
    }
}
