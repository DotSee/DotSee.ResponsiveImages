using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.UrlProviders;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
    /// <summary>
    /// Central point for generating crop URLs. Every URL the package emits goes through
    /// <see cref="GetCropUrl"/> or <see cref="GetPlaceholderUrl"/>, so rule-set concerns that must never be
    /// forgotten — quality, crop mode and the editor's focal point — are applied in exactly one place.
    /// </summary>
    /// <remarks>
    /// The URL <em>format</em> is decided by the injected <see cref="IResponsiveImageUrlProvider"/>: Umbraco
    /// crop query strings by default, Cloudflare <c>/cdn-cgi/image/…</c> URLs when configured. This class
    /// owns which sizes are asked for; the provider owns who resizes.
    /// </remarks>
    public class ImageUrlService
    {
        private readonly IResponsiveImageUrlProvider _urlProvider;

        /// <param name="urlProvider">
        /// Optional so the service stays constructible from just the two Umbraco boundary services. When
        /// null it falls back to <see cref="UmbracoImageUrlProvider"/>, i.e. the original behaviour.
        /// </param>
        public ImageUrlService(
              IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider
            , IResponsiveImageUrlProvider urlProvider = null)
        {
            _urlProvider = urlProvider ?? new UmbracoImageUrlProvider(imageUrlGenerator, publishedUrlProvider);
        }

        public MediaWithCrops GetAltImageOrDefault(MediaWithCrops originalImage, int width, Dictionary<int, MediaWithCrops> altImages)
        {
            bool useAltImages = false;
            KeyValuePair<int, MediaWithCrops> altImage = new();
            if (altImages != null)
            {
                altImage = altImages.OrderByDescending(x => x.Key).Where(x => x.Key >= width).Select(x => new KeyValuePair<int, MediaWithCrops>(x.Key, x.Value)).FirstOrDefault();

                // If not default keyvaluepair
                if (altImage.Key != 0 && altImage.Value != null)
                {
                    useAltImages = true;
                }
            }

            return (useAltImages) ? altImage.Value : originalImage; ;
        }

        /// <summary>
        /// Builds a crop URL for the given dimensions. Non-positive width/height are omitted so the
        /// image processor keeps the source aspect ratio. Honours <see cref="RuleSet.UseFocalPoint"/>,
        /// which anchors the crop on the focal point the editor chose in the backoffice — information a
        /// CDN doing the resizing downstream does not have.
        /// </summary>
        public string GetCropUrl(MediaWithCrops image, RuleSet ruleSet, int width, int height, string queryString = null)
        {
            if (image == null) { return null; }

            return _urlProvider.GetCropUrl(image, ruleSet, width, height, queryString);
        }

        /// <summary>
        /// A tiny, low-quality URL placeholder for the image — the fallback the LQIP previews use when the
        /// inline base64 placeholder cannot be built from the media file.
        /// </summary>
        public string GetPlaceholderUrl(MediaWithCrops image, RuleSet ruleSet)
        {
            if (image == null) { return null; }

            return _urlProvider.GetPlaceholderUrl(image, ruleSet);
        }

        public string GetAltImageUrlOrDefault(MediaWithCrops originalImage, RuleSet ruleSet, int width, int height, string queryString = null)
        {
            MediaWithCrops image = GetAltImageOrDefault(originalImage, width, null);

            //Nothing to size by - preserve the previous behaviour of returning no URL at all.
            if (width <= 0 && height <= 0) { return null; }

            return GetCropUrl(image, ruleSet, width, height, queryString);
        }
    }
}
