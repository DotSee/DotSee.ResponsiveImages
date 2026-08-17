using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<SrcSetManager> _logger;

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
            , ILogger<SrcSetManager> logger = null
            )
        {
            _logger = logger;
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

        /// <summary>
        /// Whether to append <c>&amp;format=webp</c> to generated URLs. Read per call rather than cached,
        /// so the switch responds to configuration reloads.
        /// </summary>
        private bool UseWebP => ResponsiveImagesConfiguration.GetUseWebP(_configuration);

        #region Public Members

        public HtmlString GetBreakPointsCss(MediaWithCrops originalImage, string ruleSetName, string optionalQueryStringParameters = null, IHtmlContent nonceAttribute = null)
        {
            optionalQueryStringParameters = ApplyGlobalUrlOptions(optionalQueryStringParameters);

            //Exit early conditions
            if (originalImage == null) { return null; }

            // The variant folds the query string, WebP and focal point into the key AND the generated
            // class name (both derive from GetCacheKey) — without it, two <ds:background> for the same
            // image with different query strings would share one cache entry and one class, and
            // whichever rendered first would win for both.
            var variant = Helpers.GetCssVariant(originalImage, optionalQueryStringParameters);

            // Cache the nonce-less CSS (the nonce is per-request, so keying/caching by it would never hit
            // and would leak an entry per request). The nonce is injected into the cached <style> below.
            var retVal = _cacheService.GetCachedItem(
                Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString(), variant)
                , () =>
                {
                    ImageModel imageModel = _backgroundImageModelManager.GetImageModel(originalImage, ruleSetName, optionalQueryStringParameters);
                    return imageModel == null ? null : _cssRenderer.RenderCss(imageModel);
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true
                , nullResultTimeout: TimeSpan.FromMinutes(2));

            if (nonceAttribute != null)
            {
                // The nonce lands inside an attribute of cached markup, so it is validated rather than
                // encoded: a "nonce" that would need escaping is not a nonce, and injecting it would
                // let whatever produced it break out of the <style> tag.
                var nonce = StringifyHtmlContent(nonceAttribute);
                return Helpers.IsValidNonce(nonce)
                    ? InjectAfterTag(retVal, "<style", $" nonce='{nonce}'")
                    : retVal;
            }

            return (retVal);
        }

        private static string StringifyHtmlContent(IHtmlContent content)
        {
            if (content is HtmlString htmlString) { return htmlString.Value; }

            using var writer = new System.IO.StringWriter();
            content.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
            return writer.ToString();
        }

        /// <summary>
        /// Applies the site-wide URL options to a per-call query string: <c>format=webp</c> when
        /// <see cref="UseWebP"/> is on, and canonical query-string form either way — so the emitted URLs
        /// (and the cache keys built from them) do not change shape with the WebP switch, and a hostile
        /// value is neutralised before it reaches any URL.
        /// </summary>
        private string ApplyGlobalUrlOptions(string queryString)
        {
            return UseWebP
                ? StringUtils.UpdateQueryString(queryString, "format", "webp")
                : StringUtils.NormalizeQueryString(queryString);
        }

        public HtmlString GetSrcSet(MediaWithCrops originalImage, string ruleSetName)
        {
            if (originalImage == null) { return null; }

            var ruleSet = LoadRuleSet(ruleSetName);
            if (ruleSet == null) { return null; }

            var config = GetConfigSection(originalImage, ruleSet);
            if (config == null) { return null; }

            StringBuilder sb = new StringBuilder(string.Empty);

            //Remove "Center" because it contains a comma! Split the entry at its LAST space (the URL
            //itself may contain spaces), fix up the URL half and reconnect the width descriptor.
            sb.Append(string.Join(",", config.SrcSetEntries.Select(x =>
            {
                int descriptorStart = x.ImageUrl.LastIndexOf(' ');
                if (descriptorStart < 0) { return x.ImageUrl; }
                return StringUtils.RemoveQueryStringByKey(x.ImageUrl.Substring(0, descriptorStart), "center")
                       + x.ImageUrl.Substring(descriptorStart);
            })));
            return new HtmlString(sb.ToString());
        }

        public HtmlString GetSizes(MediaWithCrops originalImage, RuleSet ruleSet)
        {
            if (originalImage == null || ruleSet == null) { return null; }

            var config = GetConfigSection(originalImage, ruleSet);
            if (config == null) { return null; }
            if (config.SizeEntries == null || config.SizeEntries.Count() == 0) { return null; }

            int maxWidth = GetLayoutMaxWidth(config);

            StringBuilder sb = new StringBuilder(string.Empty);

            sb.Append(string.Join(",", config.SizeEntries));
            sb.Append(", " + maxWidth.ToString() + "px");
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
            // Same contract as every other method here: a null image renders nothing. This one used to
            // NRE building its cache key instead.
            if (originalImage == null) { return null; }

            optionalQueryStringParameters = ApplyGlobalUrlOptions(optionalQueryStringParameters);

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
            // The srcset attribute name is written into the tag as markup, not as a value, so it is
            // whitelisted rather than encoded — anything that is not a plain attribute name falls back.
            if (!Helpers.IsValidAttributeName(srcSetAttrName)) { srcSetAttrName = "srcset"; }

            if (originalImage == null) { return null; }

            // SVGs bypass the crop pipeline entirely, exactly as CreatePictureElement does — there is
            // nothing to resize, and the preload hint already points at the bare URL, so building crop
            // URLs here made the browser fetch the file twice under two different URLs.
            if (IsSvg(originalImage))
            {
                return CreateSvgMarkup(originalImage, alt, title, imageClass, otherAttributes);
            }

            var ruleSet = LoadRuleSet(ruleSetName);
            if (ruleSet == null) { return null; }

            // UseWebP was documented as applying to "all generated URLs" but never reached this path.
            var globalQueryString = ApplyGlobalUrlOptions(null);

            var config = GetConfig(originalImage, ruleSet, globalQueryString);
            if (config == null) { return null; }

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
            sb.Append(BuildSizesValue(config, maxWidth));
            sb.Append("\" ");

            sb.Append("src=\"");
            sb.Append(Helpers.HtmlAttributeEncode(_imageUrlService.GetCropUrl(originalImage, ruleSet, ruleSet.OriginalImageMaxWidth ?? 0, ruleSet.OriginalImageMaxHeight ?? 0, globalQueryString)));
            sb.Append("\"");

            // Reserving the box up front is what stops the page reflowing as images arrive. Sized from
            // the widest srcset candidate, since that is the largest image this markup can deliver - the
            // fallback src is generated at the rule set's ceiling and can be far larger than any of them.
            if (!HasAttribute(otherAttributes, "width") && !HasAttribute(otherAttributes, "height")
                && Helpers.TryGetRenderedSize(originalImage, GetLargestCandidate(ruleSet), out int renderedWidth, out int renderedHeight))
            {
                sb.Append(Helpers.CreateAttribute("width", renderedWidth.ToString()));
                sb.Append(Helpers.CreateAttribute("height", renderedHeight.ToString()));
            }

            // A dictionary "class" would duplicate the class SetLazyLoadAttributes already wrote, and a
            // duplicated attribute is invalid HTML the browser resolves by silently dropping the second;
            // alt/title are always written below, so their dictionary twins are always dropped.
            if (otherAttributes != null)
            {
                otherAttributes = otherAttributes
                    .Where(x => !x.Key.InvariantEquals("alt")
                                && !x.Key.InvariantEquals("title")
                                && !(x.Key.InvariantEquals("class") && !string.IsNullOrEmpty(imageClass)))
                    .ToDictionary(x => x.Key, x => x.Value);
                if (otherAttributes.Count == 0) { otherAttributes = null; }
            }

            // Inline LQIP (style/onload). Skipped in CSP mode, where the caller emits nonce-tagged blocks instead.
            if (_overriddenLazyLoad && emitInlineLqip)
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
            sb.Append(Helpers.CreateAttribute("alt", alt));
            sb.Append(Helpers.CreateAttribute("title", title));
            if (otherAttributes != null)
            {
                sb.Append(string.Join(" ", otherAttributes.Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
            }
            sb.Append("/>");
            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// The plain-&lt;img&gt; markup for an SVG: the bare media URL, no srcset, no LQIP — the same
        /// shape <see cref="PictureElementRenderer"/> emits for its SVG case, and the same URL the
        /// preload hint points at.
        /// </summary>
        private static HtmlString CreateSvgMarkup(MediaWithCrops originalImage, string alt, string title, string imageClass, Dictionary<string, string> otherAttributes)
        {
            var sb = new StringBuilder("<img");
            sb.Append(Helpers.CreateAttribute("src", originalImage.Url()));
            if (!string.IsNullOrWhiteSpace(imageClass)) { sb.Append(Helpers.CreateAttribute("class", imageClass)); }
            sb.Append(Helpers.CreateAttribute("alt", alt));
            sb.Append(Helpers.CreateAttribute("title", title));
            if (otherAttributes != null)
            {
                sb.Append(string.Join(" ", otherAttributes
                    .Where(x => !x.Key.InvariantEquals("alt")
                                && !x.Key.InvariantEquals("title")
                                && !(x.Key.InvariantEquals("class") && !string.IsNullOrWhiteSpace(imageClass)))
                    .Select(x => Helpers.CreateAttribute(x.Key, x.Value))));
            }
            sb.Append("/>");
            return new HtmlString(sb.ToString());
        }

        private void SetLazyLoadAttributes(string srcSetAttrName, string imageClass, StringBuilder sb, bool enableLazyLoad, bool aboveFold, Dictionary<string, string> otherAttributes)
        {
            if (!string.IsNullOrEmpty(imageClass))
            {
                sb.Append("class=\"");
                sb.Append(Helpers.HtmlAttributeEncode(imageClass));
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

        /// <summary>
        /// The widest candidate the srcset offers — the largest image this markup can deliver.
        /// </summary>
        private static ImageCandidate GetLargestCandidate(RuleSet ruleSet)
        {
            return CandidateLadder.GetSrcSetCandidates(ruleSet)
                .OrderByDescending(x => x.Width)
                .FirstOrDefault();
        }

        private static bool HasAttribute(Dictionary<string, string> attributes, string name)
        {
            return attributes != null && attributes.Keys.Any(x => x.InvariantEquals(name));
        }

        /// <param name="optionalQueryStringParameters">
        /// Pass the same value given to <see cref="GetBreakPointsCss"/>: the query string is part of the
        /// generated class name, so the two calls must agree for the class to match its CSS block.
        /// </param>
        public string GetClassName(IPublishedContent originalImage, string ruleSetName, string optionalQueryStringParameters = null)
        {
            if (originalImage != null)
            {
                var variant = Helpers.GetCssVariant(originalImage as MediaWithCrops, ApplyGlobalUrlOptions(optionalQueryStringParameters));
                var styleGuid = Helpers.GetCacheKey(ruleSetName, originalImage.Key.ToString(), variant);
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
            // An invalid nonce is treated as no nonce: it goes into both a <style> and a <script> tag
            // and could otherwise break out of them (see Helpers.IsValidNonce).
            if (originalImage == null || !Helpers.IsValidNonce(nonce) || aboveFold) { return CspLqip.Inactive; }

            var ruleSet = LoadRuleSet(ruleSetName);

            if (ruleSet == null || !_lazyLoadSettings.IsLazyLoadEnabled(ruleSet)) { return CspLqip.Inactive; }

            var uniqueId = "ds-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var selector = $"[{DsIdAttributeName}=\"{uniqueId}\"]";

            if (_lazyLoadSettings.PreviewType == PreviewType.Blur)
            {
                var lqipSource = Helpers.SanitizeCssUrl(Lqip.BlurSource(_lqipService, originalImage,
                    () => _imageUrlService.GetPlaceholderUrl(originalImage, ruleSet)));
                var style = $"<style nonce=\"{nonce}\">{selector}{{background-size:cover;background-repeat:no-repeat;background-image:url('{lqipSource}');filter:blur(20px);transition:filter 0.3s}}</style>";
                var script = $"<script nonce=\"{nonce}\">document.querySelector('{selector}').addEventListener('load',function(){{this.style.filter='none';this.style.backgroundImage='none'}});</script>";
                return new CspLqip(uniqueId, new HtmlString(style), new HtmlString(script));
            }

            if (_lazyLoadSettings.PreviewType == PreviewType.LowResImage && !string.IsNullOrWhiteSpace(_lazyLoadSettings.LowResImagePath))
            {
                var style = $"<style nonce=\"{nonce}\">{selector}{{background-size:cover;background-repeat:no-repeat;background-image:url('{Helpers.SanitizeCssUrl(_lazyLoadSettings.LowResImagePath)}')}}</style>";
                var script = $"<script nonce=\"{nonce}\">document.querySelector('{selector}').addEventListener('load',function(){{this.style.backgroundImage='none'}});</script>";
                return new CspLqip(uniqueId, new HtmlString(style), new HtmlString(script));
            }

            return CspLqip.Inactive;
        }

        /// <summary>
        /// Builds <c>&lt;link rel="preload" as="image"&gt;</c> hints for a <c>&lt;picture&gt;</c>: one per
        /// breakpoint, carrying the same media query and srcset the picture itself will render, so the
        /// browser evaluates them identically and preloads exactly the one it is going to use.
        /// </summary>
        /// <remarks>
        /// Worth emitting only for an image visible without scrolling. The hint lets the browser start
        /// fetching before it has parsed the markup, which is the difference the Largest Contentful Paint
        /// measurement sees. Preloading anything below the fold competes with it and makes things worse.
        /// </remarks>
        public HtmlString GetPicturePreloadLinks(MediaWithCrops originalImage, string ruleSetName, string optionalQueryStringParameters = null)
        {
            if (originalImage == null) { return null; }

            var queryString = ApplyGlobalUrlOptions(optionalQueryStringParameters);

            return _cacheService.GetCachedItem(
                string.Join(KeySeparator, "preload_picture", originalImage.Id, ruleSetName, queryString, FocalPointKey(originalImage))
                , () =>
                {
                    var ruleSet = LoadRuleSet(ruleSetName);

                    if (ruleSet == null) { return null; }

                    //SVGs render as a plain img, so there is a single URL to point at.
                    if (IsSvg(originalImage))
                    {
                        return new HtmlString(BuildLink(href: originalImage.Url()));
                    }

                    var sb = new StringBuilder();
                    foreach (var source in _pictureElementRenderer.BuildSources(originalImage, ruleSet, queryString))
                    {
                        //A hint with nothing to fetch would only cost the browser a wasted evaluation.
                        if (string.IsNullOrWhiteSpace(source.SrcSet)) { continue; }

                        sb.Append(BuildLink(imageSrcSet: source.SrcSet, media: source.Media));
                    }

                    return new HtmlString(sb.ToString());
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true);
        }

        /// <summary>
        /// Builds a single <c>&lt;link rel="preload" as="image"&gt;</c> hint carrying the same srcset and
        /// sizes a <c>&lt;ds:img&gt;</c> would render. See <see cref="GetPicturePreloadLinks"/>.
        /// </summary>
        public HtmlString GetImagePreloadLink(MediaWithCrops originalImage, string ruleSetName)
        {
            if (originalImage == null) { return null; }

            return _cacheService.GetCachedItem(
                string.Join(KeySeparator, "preload_img", originalImage.Id, ruleSetName, FocalPointKey(originalImage), ApplyGlobalUrlOptions(null))
                , () =>
                {
                    var ruleSet = LoadRuleSet(ruleSetName);

                    if (ruleSet == null) { return null; }

                    if (IsSvg(originalImage))
                    {
                        return new HtmlString(BuildLink(href: originalImage.Url()));
                    }

                    var config = GetConfigSection(originalImage, ruleSet);
                    if (config == null || config.SrcSetEntries.Count == 0) { return null; }

                    var srcSet = string.Join(",", config.SrcSetEntries.OrderBy(x => x.Width).Select(x => x.ImageUrl));
                    var sizes = BuildSizesValue(config, GetLayoutMaxWidth(config));

                    return new HtmlString(BuildLink(imageSrcSet: srcSet, imageSizes: sizes));
                }, timeout: TimeSpan.FromMinutes(20), isSliding: true);
        }

        #endregion Public Members

        #region Private Members

        private SrcSetConfig GetConfigSection(MediaWithCrops originalImage, RuleSet ruleSet)
        {
            // The srcset attribute value paths (GetSrcSet / GetSizes / preloads) carry the same global
            // URL options as the rendered markup, so UseWebP means what the documentation says: every
            // generated URL.
            return GetConfig(originalImage, ruleSet, ApplyGlobalUrlOptions(null));
        }

        /// <summary>
        /// Resolves a rule set through the cache, logging when the name matches nothing — a typo'd
        /// rule-set name in a view used to surface as a NullReferenceException several layers down.
        /// Null results (unknown name, or a transient failure inside the provider) are cached briefly
        /// rather than for the full window, so a hiccup doesn't pin every image to the error path.
        /// </summary>
        /// <remarks>
        /// The warning is emitted inside the factory, i.e. only when the provider is actually consulted.
        /// Logging it on the way out instead would repeat it for every cache hit on the negative entry —
        /// one typo'd rule set on a page of fifty images meant fifty identical warnings per request. Once
        /// per negative-cache window still surfaces the misconfiguration, without the flood.
        /// </remarks>
        private RuleSet LoadRuleSet(string ruleSetName)
        {
            return _cacheService.GetCachedItem(
                Helpers.GetRulesetCacheKey(ruleSetName),
                () =>
                {
                    var loaded = _ruleProvider.LoadRule(ruleSetName);

                    if (loaded == null)
                    {
                        _logger?.LogWarning("Rule set '{RuleSetName}' was not found in DotSee:ResponsiveImages; nothing will be rendered for it.", ruleSetName);
                    }

                    return loaded;
                },
                nullResultTimeout: TimeSpan.FromMinutes(2));
        }

        /// <summary>
        /// Separator for cache-key parts. A control character, because the parts include free text —
        /// with "_" as both separator and legal payload, alt "Spring_Sale" + class "" collided with
        /// alt "Spring" + class "Sale" and served one element's cached markup for the other.
        /// </summary>
        private const char KeySeparator = '\u001f';

        private static string BuildPictureCacheKey(string prefix, MediaWithCrops originalImage, string ruleSetName, string imageAlt, string imageClass, Dictionary<string, string> imageAttributes, string optionalQueryStringParameters, bool aboveFold)
        {
            var attrsKey = imageAttributes != null
                ? string.Join(KeySeparator, imageAttributes.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value))
                : string.Empty;
            return string.Join(KeySeparator,
                prefix, originalImage.Id, ruleSetName, imageAlt, imageClass, attrsKey,
                optionalQueryStringParameters, aboveFold, FocalPointKey(originalImage));
        }

        /// <summary>
        /// The picker's focal point as a key component. The rendered URLs depend on it (rxy / gravity),
        /// so two content nodes picking the same media with different focal points must not share an
        /// entry.
        /// </summary>
        private static string FocalPointKey(MediaWithCrops image)
        {
            var focalPoint = image?.LocalCrops?.FocalPoint;
            return focalPoint == null
                ? string.Empty
                : focalPoint.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)
                  + "x" + focalPoint.Top.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        private SrcSetConfig GetConfig(MediaWithCrops originalImage, RuleSet rs, string queryString = null)
        {
            SrcSetConfig retVal = new SrcSetConfig();

            foreach (var candidate in CandidateLadder.GetSrcSetCandidates(rs))
            {
                retVal.SrcSetEntries.Add(new SrcSetEntry
                {
                    Breakpoint = candidate.BreakPointWidth,
                    Width = candidate.Width,
                    Is2x = candidate.DpiFactor == 2,
                    Is3x = candidate.DpiFactor == 3,
                    ImageUrl = _imageUrlService.GetAltImageUrlOrDefault(originalImage, rs, candidate.Width, candidate.Height, queryString) + " " + candidate.Width + "w"
                });
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
        /// <summary>
        /// Joins the configured sizes entries with the trailing fixed default. Skips the join when no
        /// entries are configured, which would otherwise emit a leading empty entry (", 1200px") and
        /// make the whole attribute invalid.
        /// </summary>
        private static string BuildSizesValue(SrcSetConfig config, int maxWidth)
        {
            var trailingDefault = maxWidth.ToString() + "px";

            return config.SizeEntries == null || config.SizeEntries.Count == 0
                ? trailingDefault
                : string.Join(",", config.SizeEntries) + ", " + trailingDefault;
        }

        private static bool IsSvg(MediaWithCrops image) => Helpers.IsSvg(image);

        /// <summary>
        /// Renders one preload hint. fetchpriority="high" is what actually moves it ahead of the rest of
        /// the page's fetches; without it the hint only removes the discovery delay.
        /// </summary>
        private static string BuildLink(string href = null, string imageSrcSet = null, string imageSizes = null, string media = null)
        {
            var sb = new StringBuilder("<link rel=\"preload\" as=\"image\"");

            if (!string.IsNullOrWhiteSpace(href)) { sb.Append(Helpers.CreateAttribute("href", href)); }
            if (!string.IsNullOrWhiteSpace(imageSrcSet)) { sb.Append(Helpers.CreateAttribute("imagesrcset", imageSrcSet)); }
            if (!string.IsNullOrWhiteSpace(imageSizes)) { sb.Append(Helpers.CreateAttribute("imagesizes", imageSizes)); }
            if (!string.IsNullOrWhiteSpace(media)) { sb.Append(Helpers.CreateAttribute("media", media)); }

            sb.Append(" fetchpriority=\"high\" />");
            return sb.ToString();
        }

        /// <summary>
        /// The trailing default for <c>sizes</c>: the widest image the rule set actually lays out, i.e.
        /// its widest 1x candidate.
        /// </summary>
        /// <remarks>
        /// Deliberately not the widest <i>breakpoint</i>. A breakpoint is a viewport threshold, not a
        /// display width — a rule set can switch at 1920px while only ever asking for a 400px image, and
        /// claiming a 1920px slot makes the browser pick the largest candidate on every screen, phones
        /// included. DPI candidates are excluded for the same reason: they are more pixels for the same
        /// slot, not a wider slot.
        /// </remarks>
        private static int GetLayoutMaxWidth(SrcSetConfig config)
        {
            var layoutEntries = config.SrcSetEntries.Where(x => !x.Is2x && !x.Is3x).ToList();
            if (layoutEntries.Count == 0) { return 0; }
            return layoutEntries.Max(x => x.Width);
        }

        #endregion Private Members
    }
}