using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Configuration;
using Smidge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Themelion.Core.Common;
using Themelion.Core.Common.Services.CacheService;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
    public class SrcSetManager
    {
        private readonly IRuleProvider _ruleProvider;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;
        private readonly ImageUrlService _imageUrlService;
        private readonly CssRenderer _cssRenderer;
        private readonly IGlobalLazyLoadSettings _lazyLoadSettings;
        private readonly PictureElementRenderer _pictureElementRenderer;
        private readonly BackgroundImageModelManager _backgroundImageModelManager;
        private readonly SmidgeHelper _smidgeHelper;
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;

        #region ctor

        public SrcSetManager(
             IRuleProvider ruleProvider
            , IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider
            , ImageUrlService imageUrlService
            , CssRenderer cssRenderer
            , PictureElementRenderer pictureElementRenderer
            , BackgroundImageModelManager backgroundImageModelManager
            , IGlobalLazyLoadSettings lazyLoadSettings
            , SmidgeHelper smidgeHelper
            , ICacheService cacheService
            , IConfiguration configuration
            )
        {
            _ruleProvider = ruleProvider;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
            _imageUrlService = imageUrlService;
            _cssRenderer = cssRenderer;
            _backgroundImageModelManager = backgroundImageModelManager;
            _pictureElementRenderer = pictureElementRenderer;
            _lazyLoadSettings = lazyLoadSettings;
            _smidgeHelper = smidgeHelper;
            _smidgeHelper.RequiresJs("/_content/Themelion.CoreViews/scripts/lazysizes.js");
            _smidgeHelper.RequiresJs("/_content/Themelion.CoreViews/scripts/ls.blur-up.min.js");

            _cacheService = cacheService;
            _configuration = configuration;
        }

        #endregion ctor

        #region Public Members

        public HtmlString GetBreakPointsCss(MediaWithCrops originalImage, string ruleSetName, string optionalQueryStringParameters = null, IHtmlContent nonceAttribute = null)
        {

            if (_configuration.GetValue<bool>("useWebP"))
            {
                optionalQueryStringParameters = Utils.UpdateQueryString(optionalQueryStringParameters, "format", "webp");
            }

            //Exit early conditions
            if (originalImage == null) { return null; }

            var retVal = _cacheService.GetCachedItem(
                Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString() + nonceAttribute)
                , () =>
                {
                    ImageModel imageModel = _backgroundImageModelManager.GetImageModel(originalImage, ruleSetName, optionalQueryStringParameters);
                    return _cssRenderer.RenderCss(imageModel, nonceAttribute);
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true);

            return (retVal);
        }

        public HtmlString GetSrcSet(MediaWithCrops originalImage, string ruleSetName)
        {
            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));

            var config = GetConfigSection(originalImage, ruleSet);
            if (config == null) { return null; }

            StringBuilder sb = new StringBuilder(string.Empty);

            //Remove "Center" because it contains a comma! Split the entry in two (image url and viewport) and reconnect them again
            sb.Append(string.Join(",", config.SrcSetEntries.Select(x => StringUtils.RemoveQueryStringByKey(x.ImageUrl.Split(' ')[0], "center") + " " + x.ImageUrl.Split(' ')[1])));
            return new HtmlString(sb.ToString());
        }

        public HtmlString GetSizes(MediaWithCrops originalImage, RuleSet ruleSet)
        {
            var config = GetConfigSection(originalImage, ruleSet);
            if (config == null) { return null; }
            if (config.SizeEntries == null || config.SizeEntries.Count() == 0) { return null; }

            //SrcSetEntries are already sorted.
            int maxWidth = config.SrcSetEntries.Skip(config.SrcSetEntries.Count() - 1).Take(1).First().Breakpoint;

            StringBuilder sb = new StringBuilder(string.Empty);

            sb.Append(string.Join(",", config.SizeEntries));
            sb.Append(", " + maxWidth.ToString() + Enum.GetName(typeof(SizeType), SizeType.px));
            return new HtmlString(sb.ToString());
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="originalImage"></param>
        /// <param name="ruleSetName"></param>
        /// <param name="imageAlt"></param>
        /// <param name="imageClass"></param>
        /// <param name="imageAttributes"></param>
        /// <param name="optionalQueryStringParameters"> optional paramaters will get append to the final image url</param>
        /// <returns></returns>
        public HtmlString CreatePictureElement(MediaWithCrops originalImage, string ruleSetName, string imageAlt = "", string imageClass = "", Dictionary<string, string> imageAttributes = null, string optionalQueryStringParameters = null)
        {
            if (_configuration.GetValue<bool>("useWebP"))
            {
                optionalQueryStringParameters = Utils.UpdateQueryString(optionalQueryStringParameters, "format", "webp");
            }

            if (string.IsNullOrWhiteSpace(imageAlt) && string.IsNullOrEmpty(imageClass) && imageAttributes == null)
            {
                return _cacheService.GetCachedItem(
                    "pictureelement" + originalImage.Id + ruleSetName
                    , () =>
                    {
                        return _pictureElementRenderer.CreatePictureElement(originalImage, ruleSetName, imageAlt, imageClass, imageAttributes, optionalQueryStringParameters);
                    }, timeout: TimeSpan.FromMinutes(20), isSliding: true);
            }
            else
            {
                return _pictureElementRenderer.CreatePictureElement(originalImage, ruleSetName, imageAlt, imageClass, imageAttributes, optionalQueryStringParameters);
            }
        }

        public HtmlString CreateMarkup(
            MediaWithCrops originalImage
            , string ruleSetName
            , string alt = ""
            , string title = ""
            , string srcSetAttrName = "srcset"
            , string imageClass = ""
            , Dictionary<string, string> otherAttributes = null)
        {
            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));

            var config = GetConfigSection(originalImage, ruleSet);

            if (originalImage == null || config == null) { return null; }

            int maxWidth = config.SrcSetEntries
                .OrderByDescending(z => z.Breakpoint)
                .ThenBy(z => z.Is3x)
                .ThenBy(z => z.Is2x)
                .FirstOrDefault()
                .Breakpoint;

            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("<img ");
            // format lazyload and override global if exists in rule
            var _overriddenLazyLoad = _lazyLoadSettings.IsLazyLoadEnabled(ruleSet);
            SetLazyLoadAttributes(srcSetAttrName, imageClass, sb, _overriddenLazyLoad);

            sb.Append("=\"");
            sb.Append(string.Join(",", config.SrcSetEntries.OrderBy(x => x.Breakpoint).Select(x => x.ImageUrl)));
            sb.Append("\"");
            sb.Append(" sizes=\"");
            if (_overriddenLazyLoad)
            {
                sb.Append("auto");
            }
            else
            {
                sb.Append(string.Join(",", config.SizeEntries));
                sb.Append(", " + maxWidth.ToString() + Enum.GetName(typeof(SizeType), SizeType.px));
            }
            sb.Append("\" ");

            sb.Append("src=\"");
            if (_overriddenLazyLoad && _lazyLoadSettings.PreviewType == PreviewType.Blur)
            {
                sb.Append(originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: ruleSet.OriginalImageMaxWidth, height: ruleSet.OriginalImageMaxHeight, quality: 1, imageCropMode: ruleSet.CropMode));
            }
            else if (_overriddenLazyLoad && _lazyLoadSettings.PreviewType == PreviewType.LowResImage)
            {
                sb.Append(_lazyLoadSettings.LowResImagePath);
            }
            else
            {
                sb.Append(originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: ruleSet.OriginalImageMaxWidth, height: ruleSet.OriginalImageMaxHeight, quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode));
            }
            sb.Append("\"");
            sb.Append(Helpers.CreateAttribute("alt", alt));
            sb.Append(Helpers.CreateAttribute("title", title));
            if (otherAttributes != null)
            {
                sb.Append(string.Join(",", otherAttributes.Select(x => Helpers.CreateAttribute(x.Key, x.Value).IfNull(x => ""))));
            }
            sb.Append("/>");
            return new HtmlString(sb.ToString());
        }

        private void SetLazyLoadAttributes(string srcSetAttrName, string imageClass, StringBuilder sb, bool enableLazyLoad)
        {
            if (enableLazyLoad)
            {
                sb.Append("class=\"lazyload");
                if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
                {
                    sb.Append(" blur-up");
                }
                sb.Append(String.IsNullOrWhiteSpace(imageClass) ? "" : " " + imageClass);
                sb.Append("\" ");
                sb.Append(" loading=\"lazy\"");
            }
            else
            {
                if (!string.IsNullOrEmpty(imageClass))
                {
                    sb.Append("class=\"");
                    sb.Append(imageClass);
                    sb.Append("\" ");
                }
            }

            if (enableLazyLoad)
            {
                sb.Append("data-srcset");
            }
            else
            {
                sb.Append(srcSetAttrName);
            }
        }

        public string GetClassName(IPublishedContent originalImage, string ruleSetName)
        {
            if (originalImage != null)
            {
                var styleGuid = Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString());
                if (string.IsNullOrEmpty(styleGuid)) { return null; }
                return string.Concat("media-image-", styleGuid);
            }
            return null;
        }

        #endregion Public Members

        #region Private Members

        private SrcSetConfig GetConfigSection(MediaWithCrops originalImage, RuleSet ruleSet)
        {
            return GetConfig(originalImage, ruleSet);
        }

        private SrcSetConfig GetConfig(MediaWithCrops originalImage, RuleSet rs)
        {
            SrcSetConfig retVal = new SrcSetConfig();

            //Other sizes
            foreach (var b in rs.Breakpoints.OrderBy(x => x.BreakPointWidth))
            {
                SrcSetEntry s = new SrcSetEntry();
                int height = (b.Width > 0 && b.Height == 0)
                    ? Helpers.CalcHeight(rs, b.Width)
                    : (b.Height > 0)
                        ? b.Height
                        : 0;

                int width = (b.Height > 0 && b.Width == 0)
                        ? Helpers.CalcWidth(rs, b.Height)
                        : (b.Width > 0)
                            ? b.Width
                            : b.BreakPointWidth;

                //Respect original image max dimensions
                if (rs.OriginalImageMaxWidth != null && rs.OriginalImageMaxWidth > 0 && width > rs.OriginalImageMaxWidth) { width = (int)rs.OriginalImageMaxWidth; }
                if (rs.OriginalImageMaxHeight != null && rs.OriginalImageMaxHeight > 0 && height > rs.OriginalImageMaxHeight) { height = (int)rs.OriginalImageMaxHeight; }

                s.Breakpoint = b.BreakPointWidth;

                s.Is2x = false;
                s.Is3x = false;

                s.ImageUrl = _imageUrlService.GetAltImageUrlOrDefault(originalImage, rs, width, height) + " " + b.BreakPointWidth + "w";
                //.GetCropUrl(width: width != 0 ? (int?)width : null, height: height != 0 ? (int?)height : null, quality: rs.ImageQuality)
                retVal.SrcSetEntries.Add(s);
            }

            if (rs.Use2x)
            {
                SrcSetEntry s = new SrcSetEntry();
                //Only one entry for breakpoint, max image width
                s.Breakpoint = rs.OriginalImageMaxWidth != null ? (int)rs.OriginalImageMaxWidth : rs.Breakpoints.OrderByDescending(x => x.BreakPointWidth).First().BreakPointWidth;

                //Double any dimension defined
                var width = rs.OriginalImageMaxWidth * 2;
                var height = rs.OriginalImageMaxHeight != null ? (int)rs.OriginalImageMaxHeight * 2 : 0;

                s.Is2x = true;
                s.ImageUrl = originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width != 0 ? (int?)width : null, height: height != 0 ? (int?)height : null, quality: rs.ImageQuality, imageCropMode: rs.CropMode) + " 2x";
                retVal.SrcSetEntries.Add(s);
            }

            if (rs.Use3x)
            {
                SrcSetEntry s = new SrcSetEntry();
                //Only one entry for breakpoint, max image width
                s.Breakpoint = rs.OriginalImageMaxWidth != null ? (int)rs.OriginalImageMaxWidth : rs.Breakpoints.OrderByDescending(x => x.BreakPointWidth).First().BreakPointWidth;

                //Double any dimension defined
                var width = rs.OriginalImageMaxWidth * 3;
                var height = rs.OriginalImageMaxHeight != null ? (int)rs.OriginalImageMaxHeight * 3 : 0;

                s.Is3x = true;
                s.ImageUrl = originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width != 0 ? (int?)width : null, height: height != 0 ? (int?)height : null, quality: rs.ImageQuality, imageCropMode: rs.CropMode) + " 3x";
                retVal.SrcSetEntries.Add(s);
            }

            foreach (string size in rs.Sizes)
            {
                retVal.SizeEntries.Add(size);
            }

            return (retVal);
        }

        #endregion Private Members
    }
}