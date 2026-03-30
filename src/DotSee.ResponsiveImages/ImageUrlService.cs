using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
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

        public string GetAltImageUrlOrDefault(MediaWithCrops originalImage, RuleSet ruleSet, int width, int height, string queryString = null)
        {
            string imageUrl = null;
            MediaWithCrops image = GetAltImageOrDefault(originalImage, width, null);

            if (height > 0 && width > 0)
            {
                imageUrl = image.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width, height: height
                    , quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: queryString);
            }
            else if (height > 0)
            {
                imageUrl = image.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, height: height
                    , quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: queryString);
            }
            else if (width > 0)
            {
                imageUrl = image.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width
                    , quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: queryString);
            }
            
            return imageUrl;
        }
    }
}
