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
        private readonly ILqipService _lqipService;

        public PictureElementRenderer(
            ImageUrlService imageUrlService
            , IRuleProvider ruleProvider
            , IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider
            , IGlobalLazyLoadSettings lazyLoadSettings
            , ICacheService cacheService
            , ILqipService lqipService = null)
        {
            _imageUrlService = imageUrlService;
            _ruleProvider = ruleProvider;
            _lazyLoadSettings = lazyLoadSettings;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
            _cacheService = cacheService;
            _lqipService = lqipService;
        }

        /// <param name="aboveFold">
        /// Marks this image as part of the initial viewport (typically the LCP element). It is then loaded
        /// eagerly at high priority and skips the placeholder, since lazy-loading the largest visible image
        /// delays the very paint the metric measures.
        /// </param>
        public HtmlString CreatePictureElement(MediaWithCrops originalImage, string ruleSetName, string imageAlt = "", string imageClass = "", Dictionary<string, string> imageAttributes = null, string optionalQueryStringParameters = null, bool emitInlineLqip = true, bool aboveFold = false)
        {
            string imageFiletype = Path.GetExtension(originalImage.Url());
            if (imageFiletype == ".svg")
            {
                StringBuilder _sb = new StringBuilder(string.Empty);
                _sb.Append("<img");
                _sb.Append(Helpers.CreateAttribute("src", originalImage.Url()));
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

            var isLazyLoad = !aboveFold && _lazyLoadSettings.IsLazyLoadEnabled(ruleSet);

            // Copy before adding the synthetic breakpoint: the rule set instance is cached and shared
            // across requests, so mutating its list here would leak into every other render.
            var orderedBreakPoints = ruleSet.Breakpoints.OrderByDescending(x => x.BreakPointWidth).ToList();

            //Construct an artificial last breakpoint to preserve settings below smallest breakpoint
            if (orderedBreakPoints.Count > 0 && orderedBreakPoints.Last().BreakPointWidth > 1)
            {
                var smallest = orderedBreakPoints.Last();
                orderedBreakPoints.Add(new RuleBreakPoint
                {
                    BreakPointWidth = 1,
                    Width = smallest.Width,
                    Height = smallest.Height
                });
            }

            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("<picture>");

            foreach (var bp in orderedBreakPoints)
            {
                //Width the alternative-image lookup is keyed on, and whether this breakpoint is height-driven.
                int width = Helpers.GetBreakPointWidth(bp, ruleSet);
                int height = Helpers.GetBreakPointHeight(bp, ruleSet);

                int width1x = ScaledWidth(bp, 1);
                int height1x = ScaledHeight(bp, height, width1x, 1);

                // One source per breakpoint carrying every DPI as an x-descriptor candidate. The browser
                // picks by device pixel ratio on its own, which needs no media queries at all - the older
                // vendor-prefixed -webkit-min-device-pixel-ratio sources tripled both the markup and the
                // number of distinct image variants a CDN has to generate and bill for.
                var candidates = new List<string> { BuildUrl(bp, width, width1x, height1x) };

                if (ruleSet.Use2x)
                {
                    AddDpiCandidate(candidates, bp, width, height, width1x, height1x, 2);
                }
                if (ruleSet.Use3x)
                {
                    AddDpiCandidate(candidates, bp, width, height, width1x, height1x, 3);
                }

                sb.Append("\n<source ");
                sb.Append($"media=\"only screen and (min-width: {bp.BreakPointWidth}px)\" ");
                sb.Append("srcset=\"" + string.Join(", ", candidates) + "\"");
                if (width1x > 0 && height1x > 0)
                {
                    sb.Append($" width=\"{width1x}\" height=\"{height1x}\"");
                }
                sb.Append(" />");
            }

            sb.Append("<img");

            AppendLoadingAttributes(sb, isLazyLoad, aboveFold, imageClass, imageAttributes);

            if (isLazyLoad && emitInlineLqip)
            {
                AppendInlineLqip(sb, originalImage, ruleSet);
            }

            sb.Append(Helpers.CreateAttribute("src", _imageUrlService.GetCropUrl(
                originalImage, ruleSet, ruleSet.OriginalImageMaxWidth ?? 0, ruleSet.OriginalImageMaxHeight ?? 0, optionalQueryStringParameters)));

            // Reserving the box up front is what stops the page reflowing as images arrive.
            if (!HasAttribute(imageAttributes, "width") && !HasAttribute(imageAttributes, "height")
                && Helpers.TryGetRenderedSize(originalImage, ruleSet, out int renderedWidth, out int renderedHeight))
            {
                sb.Append(Helpers.CreateAttribute("width", renderedWidth.ToString()));
                sb.Append(Helpers.CreateAttribute("height", renderedHeight.ToString()));
            }

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

            //Pixel width of the image generated for this breakpoint at the given DPI factor.
            int ScaledWidth(RuleBreakPoint bp, int factor)
            {
                if (bp.Width <= 0) { return 0; }

                return (ruleSet.OriginalImageMaxWidth != null && bp.DefinedImageWidth > (int)ruleSet.OriginalImageMaxWidth)
                    ? (int)ruleSet.OriginalImageMaxWidth * factor
                    : bp.DefinedImageWidth * factor;
            }

            // If height is present make sure that we don't exceed maximum width or height if present.
            // Calculate the new height or use the original one in case calculation returns 0 which means that
            // either max constraints have not been set or some other issue allows us to use original height.
            int ScaledHeight(RuleBreakPoint bp, int breakPointHeight, int scaledWidth, int factor)
            {
                if (breakPointHeight <= 0) { return 0; }

                var calculated = Helpers.CalcHeight(ruleSet, scaledWidth);
                return calculated > 0 ? calculated : breakPointHeight * factor;
            }

            string BuildUrl(RuleBreakPoint bp, int altImageWidth, int targetWidth, int targetHeight)
            {
                return targetHeight > 0
                    ? _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, targetWidth, targetHeight, optionalQueryStringParameters)
                    : _imageUrlService.GetCropUrl(
                        _imageUrlService.GetAltImageOrDefault(originalImage, altImageWidth, null), ruleSet, targetWidth, 0, optionalQueryStringParameters);
            }

            void AddDpiCandidate(List<string> candidates, RuleBreakPoint bp, int altImageWidth, int breakPointHeight, int width1x, int height1x, int factor)
            {
                int scaledWidth = ScaledWidth(bp, factor);
                int scaledHeight = ScaledHeight(bp, breakPointHeight, scaledWidth, factor);

                //A clamped or absent width can collapse onto the 1x candidate; don't pay for a duplicate.
                if (scaledWidth == width1x && scaledHeight == height1x) { return; }

                candidates.Add(BuildUrl(bp, altImageWidth, scaledWidth, scaledHeight) + " " + factor + "x");
            }
        }

        private void AppendLoadingAttributes(StringBuilder sb, bool enableLazyLoad, bool aboveFold, string imageClass, Dictionary<string, string> imageAttributes)
        {
            if (!string.IsNullOrWhiteSpace(imageClass))
            {
                sb.Append(Helpers.CreateAttribute("class", imageClass));
            }

            if (enableLazyLoad)
            {
                sb.Append(" loading=\"lazy\" decoding=\"async\"");
                return;
            }

            if (aboveFold)
            {
                sb.Append(" loading=\"eager\"");
                if (!HasAttribute(imageAttributes, "fetchpriority"))
                {
                    sb.Append(" fetchpriority=\"high\"");
                }
            }
        }

        private void AppendInlineLqip(StringBuilder sb, MediaWithCrops originalImage, RuleSet ruleSet)
        {
            if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
            {
                var lqipSource = Lqip.BlurSource(_lqipService, originalImage,
                    () => originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: 40, quality: 20));

                sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{lqipSource}');filter:blur(20px);transition:filter 0.3s\"");
                sb.Append(" onload=\"this.style.filter='none';this.style.backgroundImage='none'\"");
            }
            else if (_lazyLoadSettings.PreviewType == PreviewType.LowResImage
                && !string.IsNullOrWhiteSpace(_lazyLoadSettings.LowResImagePath))
            {
                sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{_lazyLoadSettings.LowResImagePath}')\"");
                sb.Append(" onload=\"this.style.backgroundImage='none'\"");
            }
        }

        private static bool HasAttribute(Dictionary<string, string> attributes, string name)
        {
            return attributes != null && attributes.Keys.Any(x => x.InvariantEquals(name));
        }
    }
}
