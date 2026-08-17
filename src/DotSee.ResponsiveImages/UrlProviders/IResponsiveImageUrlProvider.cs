using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.UrlProviders
{
    /// <summary>
    /// Turns a rule set and a target size into an image URL. The one seam between "which pixel dimensions
    /// should this image be delivered at" — which the package owns — and "who actually resizes it".
    /// </summary>
    /// <remarks>
    /// <see cref="UmbracoImageUrlProvider"/> is the default and produces the ImageSharp.Web query strings
    /// the package has always emitted. <see cref="CloudflareImageUrlProvider"/> emits
    /// <c>/cdn-cgi/image/…</c> URLs so the resizing happens at the edge instead of on the origin.
    ///
    /// Every URL the package renders goes through one of these, so rule-set concerns that must never be
    /// forgotten — quality, crop mode and the editor's focal point — are applied in exactly one place per
    /// provider.
    /// </remarks>
    public interface IResponsiveImageUrlProvider
    {
        /// <summary>
        /// A URL for the media item at the given size. Non-positive width or height is omitted so the
        /// source aspect ratio is preserved.
        /// </summary>
        /// <param name="furtherOptions">
        /// Extra options supplied per call (the tag helpers' <c>query-string</c> attribute, and
        /// <c>format=webp</c> when <c>UseWebP</c> is on), in query-string form.
        /// </param>
        string GetCropUrl(MediaWithCrops image, RuleSet ruleSet, int width, int height, string furtherOptions = null);

        /// <summary>
        /// A URL for a tiny, low-quality placeholder of the media item — the fallback used when the inline
        /// base64 LQIP cannot be built (unreadable or undecodable media file).
        /// </summary>
        string GetPlaceholderUrl(MediaWithCrops image, RuleSet ruleSet);

        /// <summary>
        /// A URL for a media <em>path</em> at the given size, for callers that never have the published
        /// media item — currently only <see cref="Cdn.CdnPurgeUrlBuilder"/>, which works from a media-save
        /// notification. Necessarily without focal point, since that lives on the published content.
        /// </summary>
        string GetCropUrlForPath(string mediaPath, RuleSet ruleSet, int width, int height);
    }
}
