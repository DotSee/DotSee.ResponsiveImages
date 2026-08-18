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
            if (originalImage == null) { return null; }

            if (Helpers.IsSvg(originalImage))
            {
                StringBuilder _sb = new StringBuilder(string.Empty);
                _sb.Append("<img");
                _sb.Append(Helpers.CreateAttribute("src", originalImage.Url()));
                if (imageAlt != null)
                {
                    _sb.Append(Helpers.CreateAttribute("alt", imageAlt));
                }
                // Same non-whitespace condition as the emission and the filter below - the parameter
                // defaults to "", which is not a class.
                if (!string.IsNullOrWhiteSpace(imageClass))
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
                                                        (!string.IsNullOrWhiteSpace(imageClass) && x.Key.InvariantEquals("class")))
                                                        )
                                                    .Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
                }
                _sb.Append("/>");
                return (new HtmlString(_sb.ToString()));
            }

            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName),
                nullResultTimeout: TimeSpan.FromMinutes(2));

            // Unknown rule-set name: emit nothing rather than the NullReferenceException this used to
            // be. SrcSetManager logs the miss; SVGs above never needed the rule set and still render.
            if (ruleSet == null) { return null; }

            var isLazyLoad = !aboveFold && _lazyLoadSettings.IsLazyLoadEnabled(ruleSet);

            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("<picture>");

            var sources = BuildSources(originalImage, ruleSet, optionalQueryStringParameters);

            foreach (var source in sources)
            {
                sb.Append("\n<source ");
                sb.Append($"media=\"{source.Media}\" ");
                sb.Append($"srcset=\"{source.SrcSet}\"");
                if (source.Width > 0 && source.Height > 0)
                {
                    sb.Append($" width=\"{source.Width}\" height=\"{source.Height}\"");
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

            // Reserving the box up front is what stops the page reflowing as images arrive. Sized from
            // the largest source the picture offers, since that is what a browser will actually display -
            // the fallback src is generated at the rule set's ceiling and can be far larger.
            if (!HasAttribute(imageAttributes, "width") && !HasAttribute(imageAttributes, "height")
                && Helpers.TryGetRenderedSize(originalImage, GetLargestCandidate(sources), out int renderedWidth, out int renderedHeight))
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
                // Drop a dictionary "class" only when a class attribute was actually rendered - the
                // imageClass parameter defaults to "", and filtering on != null used to silently
                // discard a caller's attr-class even though no class had been emitted at all.
                sb.Append(string.Join(" ", imageAttributes
                                                .Where(x =>
                                                    !(
                                                    (imageAlt != null && x.Key.InvariantEquals("alt"))
                                                    ||
                                                    (!string.IsNullOrWhiteSpace(imageClass) && x.Key.InvariantEquals("class")))
                                                    )
                                                .Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
            }
            sb.Append("/>");

            sb.Append("\n</picture>");
            return new HtmlString(sb.ToString());

        }

        /// <summary>
        /// Builds the media query and srcset for every <c>&lt;source&gt;</c> the picture will contain.
        /// </summary>
        /// <remarks>
        /// One source per breakpoint carrying every DPI as an x-descriptor candidate. The browser picks
        /// by device pixel ratio on its own, which needs no media queries at all — the older
        /// vendor-prefixed -webkit-min-device-pixel-ratio sources tripled both the markup and the number
        /// of distinct image variants a CDN has to generate and bill for.
        ///
        /// Shared with the preload hints, so a preloaded URL is always one the picture actually asks for.
        /// </remarks>
        public IReadOnlyList<RenderedSource> BuildSources(MediaWithCrops originalImage, RuleSet ruleSet, string optionalQueryStringParameters = null)
        {
            var results = new List<RenderedSource>();

            foreach (var source in CandidateLadder.GetPictureSources(ruleSet))
            {
                //Width the alternative-image lookup is keyed on.
                int altImageWidth = Helpers.GetBreakPointWidth(source.BreakPoint, ruleSet);

                var candidates = source.Candidates
                    .Select(c => c.DpiFactor == 1
                        ? BuildUrl(c.Width, c.Height)
                        : BuildUrl(c.Width, c.Height) + " " + c.DpiFactor + "x");

                results.Add(new RenderedSource(
                    $"only screen and (min-width: {source.BreakPoint.BreakPointWidth}px)",
                    string.Join(", ", candidates),
                    source.Width,
                    source.Height));

                string BuildUrl(int targetWidth, int targetHeight)
                {
                    return targetHeight > 0
                        ? _imageUrlService.GetAltImageUrlOrDefault(originalImage, ruleSet, targetWidth, targetHeight, optionalQueryStringParameters)
                        : _imageUrlService.GetCropUrl(
                            _imageUrlService.GetAltImageOrDefault(originalImage, altImageWidth, null), ruleSet, targetWidth, 0, optionalQueryStringParameters);
                }
            }

            return results;
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
                var lqipSource = Helpers.SanitizeCssUrl(Lqip.BlurSource(_lqipService, originalImage,
                    () => _imageUrlService.GetPlaceholderUrl(originalImage, ruleSet)));

                sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{lqipSource}');filter:blur(20px);transition:filter 0.3s\"");
                sb.Append(" onload=\"this.style.filter='none';this.style.backgroundImage='none'\"");
            }
            else if (_lazyLoadSettings.PreviewType == PreviewType.LowResImage
                && !string.IsNullOrWhiteSpace(_lazyLoadSettings.LowResImagePath))
            {
                sb.Append($" style=\"background-size:cover;background-repeat:no-repeat;background-image:url('{Helpers.SanitizeCssUrl(_lazyLoadSettings.LowResImagePath)}')\"");
                sb.Append(" onload=\"this.style.backgroundImage='none'\"");
            }
        }

        /// <summary>
        /// The widest 1x image any source offers — the largest thing the picture can put on screen.
        /// </summary>
        private static ImageCandidate GetLargestCandidate(IReadOnlyList<RenderedSource> sources)
        {
            var widest = sources.Where(x => x.Width > 0).OrderByDescending(x => x.Width).FirstOrDefault();
            return widest == null ? null : new ImageCandidate(0, widest.Width, widest.Height, 1);
        }

        private static bool HasAttribute(Dictionary<string, string> attributes, string name)
        {
            return attributes != null && attributes.Keys.Any(x => x.InvariantEquals(name));
        }
    }
}
