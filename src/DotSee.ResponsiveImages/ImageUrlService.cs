using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
    /// <summary>
    /// Central point for generating crop URLs. Every URL the package emits goes through
    /// <see cref="GetCropUrl"/>, so rule-set concerns that must never be forgotten — quality, crop mode
    /// and the editor's focal point — are applied in exactly one place.
    /// </summary>
    public class ImageUrlService
    {
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;

        public ImageUrlService(
              IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider)
        {
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
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

            return image.GetCropUrl(
                _imageUrlGenerator, null, _publishedUrlProvider
                , width: width > 0 ? (int?)width : null
                , height: height > 0 ? (int?)height : null
                , quality: ruleSet.ImageQuality
                , imageCropMode: ruleSet.CropMode
                , preferFocalPoint: ruleSet.UseFocalPoint
                , furtherOptions: queryString);
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
