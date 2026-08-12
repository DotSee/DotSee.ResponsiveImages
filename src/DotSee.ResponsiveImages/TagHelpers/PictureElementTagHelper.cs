using DotSee.ResponsiveImages.Preloading;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// Renders a responsive &lt;picture&gt; element through the Source Set Manager.
    /// If no image is found, nothing will be rendered.
    /// Supply an optional <c>nonce</c> to render CSP-safe (nonce-tagged style/script blocks instead of
    /// inline style/onload for the LQIP preview); when omitted the LQIP is applied inline.
    /// </summary>
    /// <param name="srcSet"></param>
    /// <param name="logger"></param>
    [HtmlTargetElement("ds:picture", Attributes = "image", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class PictureElementTagHelper(SrcSetManager srcSet,
    ILogger<PictureElementTagHelper> logger,
    IPreloadCollector preloadCollector = null,
    IConfiguration configuration = null) : TagHelper
    {
        /// <summary>
        /// Whether to stay silent instead of rendering warnings into the page. Controlled globally by
        /// DotSee:ResponsiveImages:SuppressTagHelperWarnings.
        /// </summary>
        protected bool SuppressWarnings => ResponsiveImagesConfiguration.GetSuppressTagHelperWarnings(configuration);

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext? ViewContext { get; set; }

        #region Properties

        /// <summary>
        /// The image to be processed by the Source Set Manager. This is a mandatory field
        /// </summary>
        [HtmlAttributeName("image")]
        public MediaWithCrops Image { get; set; }

        /// <summary>
        /// Provide the Rule Set for this image. This is a mandatory field and a warning
        /// will be displayed if this is not set
        /// </summary>
        [HtmlAttributeName("rule-set")]
        public string RuleSet { get; set; }

        /// <summary>
        /// Provide an alt text for the image
        /// </summary>
        [Required]
        [HtmlAttributeName("image-alt")]
        public string ImageAlt { get; set; }

        /// <summary>
        /// Provide the HTML element to wrap the image in. If you leave this empty, it will directly output the picture element
        /// </summary>
        [HtmlAttributeName("wrapper-element")]
        public string? WrapperElement { get; set; }

        /// <summary>
        /// Provide an optional CSS class for the wrapper element
        /// </summary>
        [HtmlAttributeName("wrapper-class")]
        public string WrapperClass { get; set; }

        /// <summary>
        /// Provide an optional image class for the image
        /// </summary>
        [HtmlAttributeName("image-class")]
        public string ImageClass { get; set; }

        /// <summary>
        /// Additional HTML attributes to render on the fallback img element.
        /// Supply them individually with the attr- prefix (e.g. attr-fetchpriority="high", attr-id="hero")
        /// or as a whole dictionary via image-attributes.
        /// </summary>
        [HtmlAttributeName("image-attributes", DictionaryAttributePrefix = "attr-")]
        public Dictionary<string, string> ImageAttributes { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Optional query string parameters appended to every generated image URL (e.g. "bgcolor=fff").
        /// </summary>
        [HtmlAttributeName("query-string")]
        public string QueryString { get; set; }

        /// <summary>
        /// Optional CSP nonce. When provided, the LQIP preview is delivered via nonce-tagged &lt;style&gt;
        /// and &lt;script&gt; blocks (CSP-safe) instead of inline style/onload attributes. When omitted,
        /// the LQIP is applied inline.
        /// </summary>
        [HtmlAttributeName("nonce")]
        public string Nonce { get; set; }

        /// <summary>
        /// Set this for an image that is visible without scrolling — typically the hero, and usually the
        /// page's Largest Contentful Paint element. It is then loaded eagerly at high priority and skips
        /// the placeholder, instead of being deferred like the images further down the page.
        /// </summary>
        [HtmlAttributeName("above-fold")]
        public bool AboveFold { get; set; }

        /// <summary>
        /// Set to false to skip the &lt;link rel="preload"&gt; hint that an above-fold image otherwise
        /// registers. Has no effect unless <see cref="AboveFold"/> is set.
        /// </summary>
        [HtmlAttributeName("preload")]
        public bool Preload { get; set; } = true;

        #endregion

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            try
            {
                var model = ViewContext?.ViewData.Model as IPublishedContent;
                output.TagName = WrapperElement;

                if (Image is null)
                {
                    output.SuppressOutput();
                    logger.LogError("No image found for ds:picture tag helper. Model Alias: {ModelAlias}, Model Id: {ModelId}", model?.ContentType.Alias, model?.Id);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ImageAlt))
                {
                    Error(output, Constants.PicElErrorImageAltError, SuppressWarnings);
                }

                if (!string.IsNullOrWhiteSpace(WrapperClass))
                {
                    output.Attributes.Add("class", WrapperClass);
                }

                if (string.IsNullOrWhiteSpace(RuleSet))
                {
                    Error(output, Constants.PicElErrorRuleSetError, SuppressWarnings);
                }

                var useCsp = !string.IsNullOrWhiteSpace(Nonce);
                var imageAttributes = ImageAttributes;

                // Registered for the layout's <ds:preloads> to emit in <head>, which is early enough to
                // matter; a hint written here beside the image would be discovered no sooner than the
                // image itself.
                if (AboveFold && Preload && preloadCollector != null)
                {
                    var links = srcSet.GetPicturePreloadLinks(Image, RuleSet, QueryString);
                    if (links != null) { preloadCollector.Add(links.ToString()); }
                }

                // CSP mode: resolve the nonce-tagged style/script blocks once and link them via data-ds-id.
                var csp = useCsp ? srcSet.GetCspLqip(Image, RuleSet, Nonce, AboveFold) : CspLqip.Inactive;
                if (csp.Active)
                {
                    imageAttributes = new Dictionary<string, string>(ImageAttributes) { [SrcSetManager.DsIdAttributeName] = csp.DsId };
                    output.Content.AppendHtml(csp.StyleBlock);
                }

                output.Content.AppendHtml(srcSet.CreatePictureElement(
                    Image, RuleSet,
                    imageAlt: ImageAlt,
                    imageClass: ImageClass,
                    imageAttributes: imageAttributes.Count > 0 ? imageAttributes : null,
                    optionalQueryStringParameters: QueryString,
                    emitInlineLqip: !useCsp,
                    aboveFold: AboveFold));

                if (csp.Active)
                {
                    output.Content.AppendHtml(csp.ScriptBlock);
                }
            }
            catch (Exception e)
            {
                Error(output, e.Message, SuppressWarnings);
            }
        }

        private static void Error(TagHelperOutput output, string errorMessage, bool suppressWarnings = false)
        {
            if (suppressWarnings) return;
            output.TagName = "div";
            output.AddClass("container", HtmlEncoder.Default);
            output.AddClass("row", HtmlEncoder.Default);
            output.Attributes.Add("style", "color: red;");
            output.Content.SetContent(errorMessage);
        }
    }
}