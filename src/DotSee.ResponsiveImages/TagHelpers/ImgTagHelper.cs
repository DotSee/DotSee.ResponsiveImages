using Microsoft.AspNetCore.Html;
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
    /// Renders a single &lt;img&gt; element with srcset and sizes attributes (via SrcSetManager.CreateMarkup).
    /// Use this for responsive images where the browser picks the source from srcset/sizes,
    /// rather than the art-directed &lt;picture&gt; markup produced by ds:picture.
    /// If no image is found, nothing will be rendered.
    /// </summary>
    [HtmlTargetElement("ds:img", Attributes = "image", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class ImgTagHelper(SrcSetManager srcSet,
        ILogger<ImgTagHelper> logger) : TagHelper
    {
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
        /// Provide an optional title attribute for the image
        /// </summary>
        [HtmlAttributeName("image-title")]
        public string ImageTitle { get; set; }

        /// <summary>
        /// Provide an optional image class for the image
        /// </summary>
        [HtmlAttributeName("image-class")]
        public string ImageClass { get; set; }

        /// <summary>
        /// Override the name of the srcset attribute. Defaults to "srcset".
        /// </summary>
        [HtmlAttributeName("srcset-attr-name")]
        public string SrcSetAttrName { get; set; } = "srcset";

        /// <summary>
        /// Provide the HTML element to wrap the image in. If you leave this empty, it will directly output the img element
        /// </summary>
        [HtmlAttributeName("wrapper-element")]
        public string? WrapperElement { get; set; }

        /// <summary>
        /// Provide an optional CSS class for the wrapper element
        /// </summary>
        [HtmlAttributeName("wrapper-class")]
        public string WrapperClass { get; set; }

        /// <summary>
        /// Set this to true to render friendly error messages in the output
        /// </summary>
        [HtmlAttributeName("suppress-warnings")]
        public bool SuppressWarnings { get; set; }

        /// <summary>
        /// Additional HTML attributes to render on the img element.
        /// Supply them individually with the attr- prefix (e.g. attr-fetchpriority="high", attr-id="hero")
        /// or as a whole dictionary via image-attributes.
        /// </summary>
        [HtmlAttributeName("image-attributes", DictionaryAttributePrefix = "attr-")]
        public Dictionary<string, string> ImageAttributes { get; set; } = new Dictionary<string, string>();

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
                    logger.LogError("No image found for ds:img tag helper. Model Alias: {ModelAlias}, Model Id: {ModelId}", model?.ContentType.Alias, model?.Id);
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
                var otherAttributes = ImageAttributes;

                // CSP mode: resolve the nonce-tagged style/script blocks once and link them via data-ds-id.
                var csp = useCsp ? srcSet.GetCspLqip(Image, RuleSet, Nonce, AboveFold) : CspLqip.Inactive;
                if (csp.Active)
                {
                    otherAttributes = new Dictionary<string, string>(ImageAttributes) { [SrcSetManager.DsIdAttributeName] = csp.DsId };
                    output.Content.AppendHtml(csp.StyleBlock);
                }

                HtmlString markup = srcSet.CreateMarkup(
                    Image, RuleSet,
                    alt: ImageAlt,
                    title: ImageTitle,
                    srcSetAttrName: string.IsNullOrWhiteSpace(SrcSetAttrName) ? "srcset" : SrcSetAttrName,
                    imageClass: ImageClass,
                    otherAttributes: otherAttributes.Count > 0 ? otherAttributes : null,
                    emitInlineLqip: !useCsp,
                    aboveFold: AboveFold);

                if (markup != null)
                {
                    output.Content.AppendHtml(markup);
                }

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
