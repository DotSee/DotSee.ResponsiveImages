namespace DotSee.ResponsiveImages.Models
{
    /// <summary>
    /// One rendered <c>&lt;source&gt;</c> of a <c>&lt;picture&gt;</c>, with its URLs already resolved.
    /// Also what a preload hint is built from, so the two can never disagree about which image the
    /// browser will pick.
    /// </summary>
    public sealed class RenderedSource
    {
        public RenderedSource(string media, string srcSet, int width, int height)
        {
            Media = media;
            SrcSet = srcSet;
            Width = width;
            Height = height;
        }

        public string Media { get; }
        public string SrcSet { get; }
        public int Width { get; }
        public int Height { get; }
    }
}
