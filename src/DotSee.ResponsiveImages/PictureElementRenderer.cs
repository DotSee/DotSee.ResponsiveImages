using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using DotSee.ResponsiveImages.Caching;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
    public class PictureElementRenderer
    {
        private readonly ImageUrlService _imageUrlService;
        private readonly IRuleProvider _ruleProvider;
        private readonly IGlobalLazyLoadSettings _lazyLoadSettings;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;
        private readonly ICacheService _cacheService;

        public PictureElementRenderer(
            ImageUrlService imageUrlService
            , IRuleProvider ruleProvider
            , IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider
            , IGlobalLazyLoadSettings lazyLoadSettings
            , ICacheService cacheService)
        {
            _imageUrlService = imageUrlService;
            _ruleProvider = ruleProvider;
            _lazyLoadSettings = lazyLoadSettings;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
            _cacheService = cacheService;
        }

        public HtmlString CreatePictureElement(MediaWithCrops originalImage, string ruleSetName, string imageAlt = "", string imageClass = "", Dictionary<string, string> imageAttributes = null, string optionalQueryStringParameters = null, bool emitInlineLqip = true)
        {
            string imageFiletype = Path.GetExtension(originalImage.Url());
            if (imageFiletype == ".svg")
            {
                var _svgStyleGuid = Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString());

                StringBuilder _sb = new StringBuilder(string.Empty);
                var aa = originalImage.GetCropUrl(450, 450);
                _sb.Append("<img ");
                //var c = "class=\"";
                _sb.Append($" src =\"{aa}");
                _sb.Append(originalImage.Url());

                _sb.Append("\"");
                if (imageAlt != null)
                {
                    _sb.Append(Helpers.CreateAttribute("alt", imageAlt));
                }
                if (imageClass != null)
                {
                    _sb.Append(Helpers.CreateAttribute("class", imageClass));
                }
                if (imageAttributes != null)
                {
                    _sb.Append(string.Join(" ", imageAttributes
                                                    .Where(x =>
                                                        !(
                                                        (imageAlt != null && x.Key.InvariantEquals("alt"))
                                                        ||
                                                        (imageClass != null && x.Key.InvariantEquals("class")))
                                                        )
                                                    .Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
                }
                _sb.Append("/>");
                return (new HtmlString(_sb.ToString()));
            }

            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));

            var styleGuid = Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString());
            var config = GetPicturesqueConfig(originalImage, ruleSet, optionalQueryStringParameters);
            var orderedBreakPoints = ruleSet.Breakpoints.OrderByDescending(x => x.BreakPointWidth);
            var isLazyLoad = _lazyLoadSettings.IsLazyLoadEnabled(ruleSet);

            {

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

                StringBuilder sb = new StringBuilder(string.Empty);
                sb.Append("<picture>");

                // put media queries with higher specificity first (3x = 2x > regular)
                foreach (var bp in orderedBreakPoints)
                {
                    int width = Helpers.GetBreakPointWidth(bp, ruleSet);
                    int height = Helpers.GetBreakPointHeight(bp, ruleSet);

                    // 3x source
                    if (ruleSet.Use3x)
                    {
                        int width3x = bp.Width > 0
                            ? (ruleSet.OriginalImageMaxWidth != null && bp.DefinedImageWidth > (int)ruleSet.OriginalImageMaxWidth)
                                ? (int)ruleSet.OriginalImageMaxWidth * 3
                                : bp.DefinedImageWidth * 3
                            : 0;

                        // If height is present make sure that we don't exceed maximum width or height if present.
                        // Calculate the new height or use the original one in case calculation returns 0 which means that
                        // either max constraints have not been set or some other issue allows us to use original height.
                        int height3x = height > 0
                            ? Helpers.CalcHeight(ruleSet, width3x) > 0
                                ? Helpers.CalcHeight(ruleSet, width3x)
                                : height * 3
                            : 0;
                        var mediaQueryImage3x = (height > 0)
                            ? _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, width3x, height3x, optionalQueryStringParameters)
                            : _imageUrlService.GetAltImageOrDefault(originalImage, width, null).GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width3x, quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: optionalQueryStringParameters);

                        var mq3x =
                            string.Format(
                        "\n<source media=\"only screen and (-webkit-min-device-pixel-ratio: 2.25) and (min-width: {0}px),"
                        + "only screen and (min--moz-device-pixel-ratio: 2.25) and (min-width: {0}px),"
                        + "only screen and (-o-min-device-pixel-ratio: 9/4) and (min-width: {0}px),"
                        + "only screen and (min-device-pixel-ratio: 2.25) and (min-width: {0}px),"
                        + "only screen and (min-resolution: 2.25dppx) and (min-width: {0}px)\""
                        + " srcset=\"" + mediaQueryImage3x
                        + "\" />"
                        , bp.BreakPointWidth);

                        sb.Append(mq3x);
                    }
                    // 2x source
                    if (ruleSet.Use2x)
                    {
                        int width2x = bp.Width > 0
                            ? (ruleSet.OriginalImageMaxWidth != null && bp.DefinedImageWidth > (int)ruleSet.OriginalImageMaxWidth)
                                ? (int)ruleSet.OriginalImageMaxWidth * 2
                                : bp.DefinedImageWidth * 2
                            : 0;
                        // If height is present make sure that we don't exceed maximum width or height if present.
                        // Calculate the new height or use the original one in case calculation returns 0 which means that
                        // either max constraints have not been set or some other issue allows us to use original height.
                        int height2x = height > 0
                            ? Helpers.CalcHeight(ruleSet, width2x) > 0
                                ? Helpers.CalcHeight(ruleSet, width2x)
                                : height * 2
                            : 0;
                        var mediaQueryImage2x = (height > 0)
                            ? _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, width2x, height2x, optionalQueryStringParameters)
                            : _imageUrlService.GetAltImageOrDefault(originalImage, width, null).GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width2x, quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: optionalQueryStringParameters);

                        var mq2x =
                            string.Format(
                        "\n<source media=\"only screen and (-webkit-min-device-pixel-ratio: 5/4) and (min-width: {0}px),"
                        + "only screen and (min--moz-device-pixel-ratio: 1.25) and (min-width: {0}px),"
                        + "only screen and (-o-min-device-pixel-ratio: 5/4) and (min-width: {0}px),"
                        + "only screen and (min-device-pixel-ratio: 1.25) and (min-width: {0}px),"
                        + "only screen and (min-resolution: 1.25dppx) and (min-width: {0}px)\""
                        + " srcset=\"" + mediaQueryImage2x
                        + "\" />"
                        , bp.BreakPointWidth);

                        sb.Append(mq2x);
                    }

                    // regular source
                    int width1x = bp.Width > 0
                        ? (ruleSet.OriginalImageMaxWidth != null && bp.DefinedImageWidth > (int)ruleSet.OriginalImageMaxWidth)
                            ? (int)ruleSet.OriginalImageMaxWidth
                            : bp.DefinedImageWidth
                        : 0;

                    // If height is present make sure that we don't exceed maximum width or height if present.
                    // Calculate the new height or use the original one in case calculation returns 0 which means that
                    // either max constraints have not been set or some other issue allows us to use original height.
                    int height1x = height > 0
                        ? Helpers.CalcHeight(ruleSet, width1x) > 0
                            ? Helpers.CalcHeight(ruleSet, width1x)
                            : height
                        : 0;

                    var mediaQueryImage1x = (height > 0)
                            ? _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, width1x, height1x, optionalQueryStringParameters)
                            : _imageUrlService.GetAltImageOrDefault(originalImage, width, null).GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: width1x, quality: ruleSet.ImageQuality, imageCropMode: ruleSet.CropMode, furtherOptions: optionalQueryStringParameters);
                    sb.Append("\n<source ");
                    //sb.Append($"data-lowsrc=\"{_originalImage.GetCropUrl(width:bp.BreakPointWidth, quality:5)}\" ");
                    sb.Append($"media=\"only screen and (min-width: {bp.BreakPointWidth}px)\" ");
                    sb.Append("srcset=\"" + mediaQueryImage1x);     //config.SrcSetEntries.Where(x => x.Breakpoint == bp.BreakPointWidth).First().ImageUrl);
                    sb.Append("\" />");
                }

                sb.Append("<img ");

                //format lazyload and override on ruleset if exists
                // set image class if exists
                SetLazyLoadAndCssClasses(sb, isLazyLoad, imageClass, emitInlineLqip);

                sb.Append(" src=\"");
                sb.Append(originalImage.Url());

                sb.Append("\"");
                if (imageAlt != null)
                {
                    sb.Append(Helpers.CreateAttribute("alt", imageAlt));
                }
                if (imageAttributes != null)
                {
                    sb.Append(string.Join(" ", imageAttributes
                                                    .Where(x =>
                                                        !(
                                                        (imageAlt != null && x.Key.InvariantEquals("alt"))
                                                        ||
                                                        (imageClass != null && x.Key.InvariantEquals("class")))
                                                        )
                                                    .Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
                }
                sb.Append("/>");

                sb.Append("\n</picture>");
                return new HtmlString(sb.ToString());
            }



            void SetLazyLoadAndCssClasses(StringBuilder sb, bool enableLazyLoad, string imageClassString, bool emitInline)
            {
                if (!enableLazyLoad && string.IsNullOrWhiteSpace(imageClassString)) return;

                if (!string.IsNullOrWhiteSpace(imageClassString))
                {
                    sb.Append($" class=\"{imageClassString}\"");
                }

                if (enableLazyLoad)
                {
                    sb.Append(" loading=\"lazy\" decoding=\"async\"");

                    if (emitInline)
                    {
                        if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
                        {
                            var lqipUrl = originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: 40, quality: 20);
                            sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{lqipUrl}');filter:blur(20px);transition:filter 0.3s\"");
                            sb.Append(" onload=\"this.style.filter='none';this.style.backgroundImage='none'\"");
                        }
                        else if (_lazyLoadSettings.PreviewType == PreviewType.LowResImage
                            && !string.IsNullOrWhiteSpace(_lazyLoadSettings.LowResImagePath))
                        {
                            sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{_lazyLoadSettings.LowResImagePath}')\"");
                            sb.Append(" onload=\"this.style.backgroundImage='none'\"");
                        }
                    }
                }
            }

            SrcSetConfig GetPicturesqueConfig(MediaWithCrops originalImage, RuleSet rs, string optionalQueryString = null)
            {
                SrcSetConfig retVal = new SrcSetConfig();

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
                    s.ImageUrl = _imageUrlService.GetAltImageUrlOrDefault(originalImage, rs, width, height, optionalQueryString);

                    retVal.SrcSetEntries.Add(s);
                }

                foreach (string size in rs.Sizes)
                {
                    retVal.SizeEntries.Add(size);
                }

                return (retVal);
            }
        }
    }
}