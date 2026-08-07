using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages
{
    public class SrcSetManager
    {
        /// <summary>
        /// Attribute used to link a CSP-safe &lt;picture&gt;'s fallback img to its nonce-tagged style/script.
        /// It is unique per request, so it is stripped before caching and injected afterwards.
        /// </summary>
        public const string DsIdAttributeName = "data-ds-id";

        private readonly IRuleProvider _ruleProvider;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;
        private readonly ImageUrlService _imageUrlService;
        private readonly CssRenderer _cssRenderer;
        private readonly IGlobalLazyLoadSettings _lazyLoadSettings;
        private readonly PictureElementRenderer _pictureElementRenderer;
        private readonly BackgroundImageModelManager _backgroundImageModelManager;
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;
        private readonly ILqipService _lqipService;

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
            , ICacheService cacheService
            , IConfiguration configuration
            , ILqipService lqipService = null
            )
        {
            _lqipService = lqipService;
            _ruleProvider = ruleProvider;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
            _imageUrlService = imageUrlService;
            _cssRenderer = cssRenderer;
            _backgroundImageModelManager = backgroundImageModelManager;
            _pictureElementRenderer = pictureElementRenderer;
            _lazyLoadSettings = lazyLoadSettings;
            _cacheService = cacheService;
            _configuration = configuration;
        }

        #endregion ctor

        #region Public Members

        public HtmlString GetBreakPointsCss(MediaWithCrops originalImage, string ruleSetName, string optionalQueryStringParameters = null, IHtmlContent nonceAttribute = null)
        {

            if (_configuration.GetValue<bool>("useWebP"))
            {
                optionalQueryStringParameters = StringUtils.UpdateQueryString(optionalQueryStringParameters, "format", "webp");
            }

            //Exit early conditions
            if (originalImage == null) { return null; }

            // Cache the nonce-less CSS (the nonce is per-request, so keying/caching by it would never hit
            // and would leak an entry per request). The nonce is injected into the cached <style> below.
            var retVal = _cacheService.GetCachedItem(
                Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString())
                , () =>
                {
                    ImageModel imageModel = _backgroundImageModelManager.GetImageModel(originalImage, ruleSetName, optionalQueryStringParameters);
                    return _cssRenderer.RenderCss(imageModel);
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true);

            if (nonceAttribute != null)
            {
                return InjectAfterTag(retVal, "<style", $" nonce='{nonceAttribute}'");
            }

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

            int maxWidth = GetLayoutMaxWidth(config);

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
        public HtmlString CreatePictureElement(MediaWithCrops originalImage, string ruleSetName, string imageAlt = "", string imageClass = "", Dictionary<string, string> imageAttributes = null, string optionalQueryStringParameters = null, bool emitInlineLqip = true, bool aboveFold = false)
        {
            if (_configuration.GetValue<bool>("useWebP"))
            {
                optionalQueryStringParameters = StringUtils.UpdateQueryString(optionalQueryStringParameters, "format", "webp");
            }

            if (!emitInlineLqip)
            {
                // CSP mode carries a unique per-call data-ds-id on the <img>. Cache the markup WITHOUT it
                // (so entries are reusable) and inject the id afterwards, rather than skipping the cache.
                string dsId = null;
                var cspAttributes = imageAttributes;
                if (imageAttributes != null && imageAttributes.TryGetValue(DsIdAttributeName, out dsId))
                {
                    cspAttributes = imageAttributes.Where(x => x.Key != DsIdAttributeName).ToDictionary(x => x.Key, x => x.Value);
                }

                var cspCached = _cacheService.GetCachedItem(
                    BuildPictureCacheKey("pictureelement_csp", originalImage, ruleSetName, imageAlt, imageClass, cspAttributes, optionalQueryStringParameters, aboveFold)
                    , () =>
                    {
                        return _pictureElementRenderer.CreatePictureElement(originalImage, ruleSetName, imageAlt, imageClass, cspAttributes, optionalQueryStringParameters, emitInlineLqip, aboveFold);
                    }, timeout: TimeSpan.FromMinutes(20), isSliding: true);

                return dsId != null
                    ? InjectAfterTag(cspCached, "<img", $" {DsIdAttributeName}=\"{dsId}\"")
                    : cspCached;
            }

            return _cacheService.GetCachedItem(
                BuildPictureCacheKey("pictureelement", originalImage, ruleSetName, imageAlt, imageClass, imageAttributes, optionalQueryStringParameters, aboveFold)
                , () =>
                {
                    return _pictureElementRenderer.CreatePictureElement(originalImage, ruleSetName, imageAlt, imageClass, imageAttributes, optionalQueryStringParameters, emitInlineLqip, aboveFold);
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true);
        }

        public HtmlString CreateMarkup(
            MediaWithCrops originalImage
            , string ruleSetName
            , string alt = ""
            , string title = ""
            , string srcSetAttrName = "srcset"
            , string imageClass = ""
            , Dictionary<string, string> otherAttributes = null
            , bool emitInlineLqip = true
            , bool aboveFold = false)
        {
            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));

            var config = GetConfigSection(originalImage, ruleSet);

            if (originalImage == null || config == null) { return null; }

            int maxWidth = GetLayoutMaxWidth(config);

            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("<img ");
            // format lazyload and override global if exists in rule
            var _overriddenLazyLoad = !aboveFold && _lazyLoadSettings.IsLazyLoadEnabled(ruleSet);
            SetLazyLoadAttributes(srcSetAttrName, imageClass, sb, _overriddenLazyLoad, aboveFold, otherAttributes);

            sb.Append("=\"");
            sb.Append(string.Join(",", config.SrcSetEntries.OrderBy(x => x.Width).Select(x => x.ImageUrl)));
            sb.Append("\"");
            sb.Append(" sizes=\"");
            sb.Append(string.Join(",", config.SizeEntries));
            sb.Append(", " + maxWidth.ToString() + Enum.GetName(typeof(SizeType), SizeType.px));
            sb.Append("\" ");

            sb.Append("src=\"");
            sb.Append(_imageUrlService.GetCropUrl(originalImage, ruleSet, ruleSet.OriginalImageMaxWidth ?? 0, ruleSet.OriginalImageMaxHeight ?? 0));
            sb.Append("\"");

            // Reserving the box up front is what stops the page reflowing as images arrive.
            if (!HasAttribute(otherAttributes, "width") && !HasAttribute(otherAttributes, "height")
                && Helpers.TryGetRenderedSize(originalImage, ruleSet, out int renderedWidth, out int renderedHeight))
            {
                sb.Append(Helpers.CreateAttribute("width", renderedWidth.ToString()));
                sb.Append(Helpers.CreateAttribute("height", renderedHeight.ToString()));
            }

            // Inline LQIP (style/onload). Skipped in CSP mode, where the caller emits nonce-tagged blocks instead.
            if (_overriddenLazyLoad && emitInlineLqip)
            {
                if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
                {
                    var lqipSource = Lqip.BlurSource(_lqipService, originalImage,
                        () => originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: 40, quality: 20, imageCropMode: ruleSet.CropMode));
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
            sb.Append(Helpers.CreateAttribute("alt", alt));
            sb.Append(Helpers.CreateAttribute("title", title));
            if (otherAttributes != null)
            {
                sb.Append(string.Join(" ", otherAttributes.Select(x => Helpers.CreateAttribute(x.Key, x.Value).IfNull(x => ""))));
            }
            sb.Append("/>");
            return new HtmlString(sb.ToString());
        }

        private void SetLazyLoadAttributes(string srcSetAttrName, string imageClass, StringBuilder sb, bool enableLazyLoad, bool aboveFold, Dictionary<string, string> otherAttributes)
        {
            if (!string.IsNullOrEmpty(imageClass))
            {
                sb.Append("class=\"");
                sb.Append(imageClass);
                sb.Append("\" ");
            }

            if (enableLazyLoad)
            {
                sb.Append("loading=\"lazy\" decoding=\"async\" ");
            }
            else if (aboveFold)
            {
                sb.Append("loading=\"eager\" ");
                if (!HasAttribute(otherAttributes, "fetchpriority"))
                {
                    sb.Append("fetchpriority=\"high\" ");
                }
            }

            sb.Append(srcSetAttrName);
        }

        private static bool HasAttribute(Dictionary<string, string> attributes, string name)
        {
            return attributes != null && attributes.Keys.Any(x => x.InvariantEquals(name));
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

        /// <summary>
        /// Builds the CSP-safe LQIP artifacts (nonce-tagged &lt;style&gt;/&lt;script&gt; blocks + a unique
        /// data-ds-id) for an image, so a strict Content Security Policy is satisfied without inline
        /// style/onload attributes. Returns <see cref="CspLqip.Inactive"/> when there is nothing to emit
        /// (no nonce, lazy loading disabled for the rule set, or no preview type configured). Callers add
        /// <see cref="CspLqip.DsId"/> to the image element (via <see cref="DsIdAttributeName"/>) and render
        /// the element with <c>emitInlineLqip: false</c>.
        /// </summary>
        public CspLqip GetCspLqip(MediaWithCrops originalImage, string ruleSetName, string nonce, bool aboveFold = false)
        {
            if (originalImage == null || string.IsNullOrWhiteSpace(nonce) || aboveFold) { return CspLqip.Inactive; }

            var ruleSet = _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () => _ruleProvider.LoadRule(ruleSetName));

            if (ruleSet == null || !_lazyLoadSettings.IsLazyLoadEnabled(ruleSet)) { return CspLqip.Inactive; }

            var uniqueId = "ds-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var selector = $"[{DsIdAttributeName}=\"{uniqueId}\"]";

            if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
            {
                var lqipSource = Lqip.BlurSource(_lqipService, originalImage,
                    () => originalImage.GetCropUrl(_imageUrlGenerator, null, _publishedUrlProvider, width: 40, quality: 20, imageCropMode: ruleSet.CropMode));
                var style = $"<style nonce=\"{nonce}\">{selector}{{background-size:cover;background-repeat:no-repeat;background-image:url('{lqipSource}');filter:blur(20px);transition:filter 0.3s}}</style>";
                var script = $"<script nonce=\"{nonce}\">document.querySelector('{selector}').addEventListener('load',function(){{this.style.filter='none';this.style.backgroundImage='none'}});</script>";
                return new CspLqip(uniqueId, new HtmlString(style), new HtmlString(script));
            }

            if (_lazyLoadSettings.PreviewType == PreviewType.LowResImage && !string.IsNullOrWhiteSpace(_lazyLoadSettings.LowResImagePath))
            {
                var style = $"<style nonce=\"{nonce}\">{selector}{{background-size:cover;background-repeat:no-repeat;background-image:url('{_lazyLoadSettings.LowResImagePath}')}}</style>";
                var script = $"<script nonce=\"{nonce}\">document.querySelector('{selector}').addEventListener('load',function(){{this.style.backgroundImage='none'}});</script>";
                return new CspLqip(uniqueId, new HtmlString(style), new HtmlString(script));
            }

            return CspLqip.Inactive;
        }

        #endregion Public Members

        #region Private Members

        private SrcSetConfig GetConfigSection(MediaWithCrops originalImage, RuleSet ruleSet)
        {
            return GetConfig(originalImage, ruleSet);
        }

        private static string BuildPictureCacheKey(string prefix, MediaWithCrops originalImage, string ruleSetName, string imageAlt, string imageClass, Dictionary<string, string> imageAttributes, string optionalQueryStringParameters, bool aboveFold)
        {
            var attrsKey = imageAttributes != null
                ? string.Join("_", imageAttributes.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value))
                : string.Empty;
            return $"{prefix}_{originalImage.Id}_{ruleSetName}_{imageAlt}_{imageClass}_{attrsKey}_{optionalQueryStringParameters}_{aboveFold}";
        }

        /// <summary>
        /// Inserts <paramref name="attribute"/> immediately after the first occurrence of <paramref name="tag"/>
        /// (e.g. "&lt;style" or "&lt;img"). Used to add a per-request nonce or data-ds-id to cached markup.
        /// </summary>
        private static HtmlString InjectAfterTag(HtmlString html, string tag, string attribute)
        {
            if (html == null) { return null; }
            var s = html.ToString();
            int idx = s.IndexOf(tag, StringComparison.Ordinal);
            if (idx < 0) { return html; }
            return new HtmlString(s.Insert(idx + tag.Length, attribute));
        }

        /// <summary>
        /// Builds the srcset candidates for a rule set.
        /// </summary>
        /// <remarks>
        /// Every candidate is described by its real pixel width ("w"). A srcset may not mix "w" and "x"
        /// descriptors — the HTML spec makes the whole attribute invalid and browsers drop it entirely,
        /// falling back to src — so higher-DPI variants are emitted as additional, wider candidates.
        /// That is also all a "w" srcset needs: the browser resolves the device pixel ratio against
        /// <c>sizes</c> by itself and picks the right candidate.
        /// </remarks>
        private SrcSetConfig GetConfig(MediaWithCrops originalImage, RuleSet rs)
        {
            SrcSetConfig retVal = new SrcSetConfig();

            var factors = new List<int> { 1 };
            if (rs.Use2x) { factors.Add(2); }
            if (rs.Use3x) { factors.Add(3); }

            int maxWidth = rs.OriginalImageMaxWidth ?? 0;
            int maxHeight = rs.OriginalImageMaxHeight ?? 0;

            //Two breakpoints (or a breakpoint and a DPI variant) can resolve to the same pixel width once
            //clamped. Emitting it twice buys nothing and costs an extra variant to generate and cache.
            var emittedWidths = new HashSet<int>();

            foreach (var b in rs.Breakpoints.OrderBy(x => x.BreakPointWidth))
            {
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
                if (maxWidth > 0 && width > maxWidth) { width = maxWidth; }
                if (maxHeight > 0 && height > maxHeight) { height = maxHeight; }

                foreach (int factor in factors)
                {
                    int candidateWidth = width * factor;
                    int candidateHeight = height * factor;

                    if (maxWidth > 0 && candidateWidth > maxWidth)
                    {
                        candidateWidth = maxWidth;
                        if (candidateHeight > 0)
                        {
                            int recalculated = Helpers.CalcHeight(rs, candidateWidth);
                            candidateHeight = recalculated > 0 ? recalculated : candidateHeight;
                        }
                    }
                    if (maxHeight > 0 && candidateHeight > maxHeight) { candidateHeight = maxHeight; }

                    if (candidateWidth <= 0 || !emittedWidths.Add(candidateWidth)) { continue; }

                    retVal.SrcSetEntries.Add(new SrcSetEntry
                    {
                        Breakpoint = b.BreakPointWidth,
                        Width = candidateWidth,
                        Is2x = factor == 2,
                        Is3x = factor == 3,
                        ImageUrl = _imageUrlService.GetAltImageUrlOrDefault(originalImage, rs, candidateWidth, candidateHeight) + " " + candidateWidth + "w"
                    });
                }
            }

            foreach (string size in rs.Sizes)
            {
                retVal.SizeEntries.Add(size);
            }

            return (retVal);
        }

        /// <summary>
        /// The widest layout breakpoint, used as the trailing default in <c>sizes</c>. Deliberately
        /// ignores the extra DPI candidates: sizes describes how wide the image is laid out on the page,
        /// not how many pixels the largest candidate contains.
        /// </summary>
        private static int GetLayoutMaxWidth(SrcSetConfig config)
        {
            var layoutEntries = config.SrcSetEntries.Where(x => !x.Is2x && !x.Is3x).ToList();
            if (layoutEntries.Count == 0) { return 0; }
            return layoutEntries.Max(x => x.Breakpoint);
        }

        #endregion Private Members
    }
}