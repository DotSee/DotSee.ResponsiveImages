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
    ILogger<PictureElementTagHelper> logger) : TagHelper
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
        /// Set this to true to render friendly error messages in the output
        /// </summary>
        [HtmlAttributeName("suppress-warnings")]
        public bool SuppressWarnings { get; set; }

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

                // CSP mode: resolve the nonce-tagged style/script blocks once and link them via data-ds-id.
                var csp = useCsp ? srcSet.GetCspLqip(Image, RuleSet, Nonce) : CspLqip.Inactive;
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
                    emitInlineLqip: !useCsp));

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