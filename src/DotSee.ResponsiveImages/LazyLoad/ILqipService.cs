using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.LazyLoad
{
    /// <summary>
    /// Produces the Low Quality Image Placeholder shown while a lazy-loaded image downloads.
    /// </summary>
    /// <remarks>
    /// The placeholder is returned as a self-contained base64 <c>data:</c> URI rather than a URL, so it
    /// costs no extra HTTP request and paints immediately with the HTML — a URL-based placeholder cannot
    /// appear until its own round trip completes, which is most of the point of a placeholder gone. It
    /// also means one less transformation per image for sites billed per image transformation by a CDN.
    /// </remarks>
    public interface ILqipService
    {
        /// <summary>
        /// Returns a base64 data URI for the image, or <c>null</c> when one cannot be produced
        /// (unreadable, unsupported or missing source file). Callers must fall back to a URL placeholder.
        /// </summary>
        string GetDataUri(MediaWithCrops image);
    }
}
