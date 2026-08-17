using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.Caching;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages
{
    public class BackgroundImageModelManager
    {
        private readonly IRuleProvider _ruleProvider;
        private readonly IGlobalLazyLoadSettings _lazyLoadSettings;
        private readonly ImageUrlService _imageUrlService;
        private readonly ICacheService _cacheService;

        public BackgroundImageModelManager(
            IRuleProvider ruleProvider
            , IGlobalLazyLoadSettings lazyLoadSettings
            , ImageUrlService imageUrlService
            , ICacheService cacheService)
        {
            _ruleProvider = ruleProvider;
            _lazyLoadSettings = lazyLoadSettings;
            _imageUrlService = imageUrlService;
            _cacheService = cacheService;
        }

        public ImageModel GetImageModel(MediaWithCrops originalImage, string ruleSetName, string optionalQueryStringParameters = null)
        {
            //Exit early conditions
            if (originalImage == null) { return null; }
            ImageModel imageModel = new();
            imageModel.OriginalImage = originalImage;
            var ruleSet = _cacheService.GetCachedItem (
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));
            imageModel.RuleSet = ruleSet;
            imageModel.LowResImagePath = _lazyLoadSettings.LowResImagePath;
            imageModel.QueryString = optionalQueryStringParameters;
            imageModel.CacheKey = CacheLiteralsRS.CachedImageCss + originalImage.Id + ruleSet.Name;
            imageModel.ImageGuid = Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString());
            imageModel.OriginalImageId = originalImage.Id;
            var top = originalImage.LocalCrops.FocalPoint != null ? originalImage.LocalCrops.FocalPoint.Top : (decimal)0.5;
            var left = originalImage.LocalCrops.FocalPoint != null ? originalImage.LocalCrops.FocalPoint.Left : (decimal)0.5;

            imageModel.ImageTop = (int)(top * 100);
            imageModel.ImageLeft = (int)(left * 100);
            imageModel.OriginalUrl = originalImage.Url();

            imageModel.BreakPoints = GetImageBreakPointModels(ruleSet, originalImage, optionalQueryStringParameters);

            return (imageModel);
        }

        private IEnumerable<ImageBreakPointModel> GetImageBreakPointModels(RuleSet ruleSet, MediaWithCrops originalImage, string optionalQueryStringParameters)
        {
            int maxBreakPoint = 0;
            var orderedBreakPoints = ruleSet.Breakpoints.OrderByDescending(x => x.BreakPointWidth);
            bool isFirst = true;

            //Construct an artificial last breakpoint to preserve settings below smallest breakpoint
            if (orderedBreakPoints.Last().BreakPointWidth > 1)
            {
                var r = new RuleBreakPoint();
                r.BreakPointWidth = 1;
                r.Width = orderedBreakPoints.Last().Width;
                r.Height = orderedBreakPoints.Last().Height;
                ruleSet.Breakpoints.Add(r);
                orderedBreakPoints = ruleSet.Breakpoints.OrderByDescending(x => x.BreakPointWidth);
            }

            //Get all breakpoints, create from larger to smaller pixel ratio.
            foreach (var b in orderedBreakPoints)
            {
                ImageBreakPointModel imageBreakPointModel = new();
                int myWidth = b.BreakPointWidth;

                int nextBreakPointWidth = orderedBreakPoints.Where(x => x.BreakPointWidth < myWidth).Any()
            ? orderedBreakPoints.Where(x => x.BreakPointWidth < b.BreakPointWidth).First().BreakPointWidth
            : 0;

                maxBreakPoint = b.BreakPointWidth;

                int width = Helpers.GetBreakPointWidth(b, ruleSet);
                int height = Helpers.GetBreakPointHeight(b, ruleSet);

                imageBreakPointModel.ImageUrl = _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, width, height, optionalQueryStringParameters);
                imageBreakPointModel.BreakPointWidth = b.BreakPointWidth;
                imageBreakPointModel.NextBreakPointWidth = nextBreakPointWidth;
                imageBreakPointModel.IsFirst = isFirst;
                imageBreakPointModel.BreakPoint = b;

                isFirst = false;
                yield return imageBreakPointModel;
            }
        }
    }
}