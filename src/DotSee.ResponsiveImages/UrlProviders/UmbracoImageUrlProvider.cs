using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages.UrlProviders
{
    /// <summary>
    /// The default provider: Umbraco's <see cref="IImageUrlGenerator"/>, i.e. ImageSharp.Web query strings
    /// resolved by the origin (<c>?width=1200&amp;quality=70&amp;rxy=0.4,0.6</c>).
    /// </summary>
    public class UmbracoImageUrlProvider : IResponsiveImageUrlProvider
    {
        /// <summary>Width of the fallback URL placeholder, in pixels.</summary>
        internal const int PlaceholderWidth = 40;

        /// <summary>Quality of the fallback URL placeholder.</summary>
        internal const int PlaceholderQuality = 20;

        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;

        public UmbracoImageUrlProvider(
              IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider)
        {
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
        }

        public string GetCropUrl(MediaWithCrops image, RuleSet ruleSet, int width, int height, string furtherOptions = null)
        {
            if (image == null) { return null; }

            return image.GetCropUrl(
                _imageUrlGenerator, null, _publishedUrlProvider
                , width: width > 0 ? (int?)width : null
                , height: height > 0 ? (int?)height : null
                // Omitted rather than passed as an explicit 0 — "quality=0" is not a meaningful request,
                // and GetCropUrlForPath below already omitted it, so render and purge URLs disagreed for
                // any rule set that leaves ImageQuality unset.
                , quality: ruleSet.ImageQuality > 0 ? (int?)ruleSet.ImageQuality : null
                , imageCropMode: ruleSet.CropMode
                , preferFocalPoint: ruleSet.UseFocalPoint
                , furtherOptions: furtherOptions);
        }

        public string GetPlaceholderUrl(MediaWithCrops image, RuleSet ruleSet)
        {
            if (image == null) { return null; }

            return image.GetCropUrl(
                _imageUrlGenerator, null, _publishedUrlProvider
                , width: PlaceholderWidth
                , quality: PlaceholderQuality
                , imageCropMode: ruleSet.CropMode);
        }

        public string GetCropUrlForPath(string mediaPath, RuleSet ruleSet, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(mediaPath)) { return null; }

            return _imageUrlGenerator.GetImageUrl(new ImageUrlGenerationOptions(mediaPath)
            {
                Width = width > 0 ? width : null,
                Height = height > 0 ? height : null,
                Quality = ruleSet.ImageQuality > 0 ? ruleSet.ImageQuality : null,
                ImageCropMode = ruleSet.CropMode
            });
        }
    }
}
