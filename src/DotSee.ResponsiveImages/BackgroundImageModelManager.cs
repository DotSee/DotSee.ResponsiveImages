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
            var ruleSet = _cacheService.GetCachedItem (
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName),
                nullResultTimeout: System.TimeSpan.FromMinutes(2));

            // An unknown rule-set name (or an empty one) has nothing to render against; the caller
            // treats a null model as "emit nothing" instead of the NullReferenceException it used to be.
            if (ruleSet == null || ruleSet.Breakpoints == null || ruleSet.Breakpoints.Count == 0) { return null; }

            ImageModel imageModel = new();
            imageModel.OriginalImage = originalImage;
            imageModel.RuleSet = ruleSet;
            imageModel.QueryString = optionalQueryStringParameters;
            // An SVG has nothing to crop; the CSS renderer emits its bare URL instead of breakpoints.
            imageModel.IsSvg = Helpers.IsSvg(originalImage);
            // Same variant as SrcSetManager.GetClassName / GetBreakPointsCss, so the class name written
            // inside the CSS block matches the one handed to the element.
            imageModel.ImageGuid = Helpers.GetCacheKey(
                ruleSetName, originalImage.Key.ToString(), Helpers.GetCssVariant(originalImage, optionalQueryStringParameters));
            imageModel.OriginalImageId = originalImage.Id;
            var top = originalImage.LocalCrops?.FocalPoint != null ? originalImage.LocalCrops.FocalPoint.Top : (decimal)0.5;
            var left = originalImage.LocalCrops?.FocalPoint != null ? originalImage.LocalCrops.FocalPoint.Left : (decimal)0.5;

            imageModel.ImageTop = (int)(top * 100);
            imageModel.ImageLeft = (int)(left * 100);

            imageModel.BreakPoints = GetImageBreakPointModels(ruleSet, originalImage, optionalQueryStringParameters);

            return (imageModel);
        }

        private IEnumerable<ImageBreakPointModel> GetImageBreakPointModels(RuleSet ruleSet, MediaWithCrops originalImage, string optionalQueryStringParameters)
        {
            // GetOrderedBreakPoints supplies the synthetic 1px breakpoint on a COPY. The rule set
            // instance is a cached singleton shared across requests — appending the synthetic entry to
            // it (as this method once did) raced concurrent renders and permanently leaked a bogus
            // breakpoint into every other ladder built from the same rule set.
            var orderedBreakPoints = CandidateLadder.GetOrderedBreakPoints(ruleSet);
            bool isFirst = true;

            //Get all breakpoints, create from larger to smaller pixel ratio.
            foreach (var b in orderedBreakPoints)
            {
                ImageBreakPointModel imageBreakPointModel = new();
                int myWidth = b.BreakPointWidth;

                int nextBreakPointWidth = orderedBreakPoints.Where(x => x.BreakPointWidth < myWidth).Any()
            ? orderedBreakPoints.Where(x => x.BreakPointWidth < b.BreakPointWidth).First().BreakPointWidth
            : 0;

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