using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// Renders a responsive CSS background image. Emits a &lt;style&gt; block with per-breakpoint
    /// background-image media queries (via SrcSetManager.GetBreakPointsCss) and applies the matching
    /// generated class (via SrcSetManager.GetClassName) to the output element, which wraps its inner content.
    /// If no image is found, the element is rendered without a background (its inner content is preserved).
    /// </summary>
    [HtmlTargetElement("ds:background", Attributes = "image", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class BackgroundImageTagHelper(SrcSetManager srcSet,
        ILogger<BackgroundImageTagHelper> logger,
        IConfiguration configuration = null) : TagHelper
    {
        /// <summary>
        /// Whether to stay silent instead of rendering warnings into the page. Controlled globally by
        /// DotSee:ResponsiveImages:SuppressTagHelperWarnings.
        /// </summary>
        private bool SuppressWarnings => ResponsiveImagesConfiguration.GetSuppressTagHelperWarnings(configuration);

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
        /// The HTML element to render and apply the background to. Defaults to "div".
        /// </summary>
        [HtmlAttributeName("element")]
        public string Element { get; set; } = "div";

        /// <summary>
        /// Optional query string parameters appended to every generated image URL (e.g. "bgcolor=fff").
        /// </summary>
        [HtmlAttributeName("query-string")]
        public string QueryString { get; set; }

        /// <summary>
        /// The CSP nonce value for the generated inline style block. Leave empty when no CSP is in use.
        /// </summary>
        [HtmlAttributeName("nonce")]
        public string Nonce { get; set; }

        #endregion

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            try
            {
                var model = ViewContext?.ViewData.Model as IPublishedContent;
                output.TagName = string.IsNullOrWhiteSpace(Element) ? "div" : Element;

                if (Image is null)
                {
                    output.SuppressOutput();
                    logger.LogError("No image found for ds:background tag helper. Model Alias: {ModelAlias}, Model Id: {ModelId}", model?.ContentType.Alias, model?.Id);
                    return;
                }

                if (string.IsNullOrWhiteSpace(RuleSet))
                {
                    Error(output, Constants.PicElErrorRuleSetError, SuppressWarnings);
                    return;
                }

                IHtmlContent nonceAttribute = string.IsNullOrWhiteSpace(Nonce) ? null : new HtmlString(Nonce);

                var css = srcSet.GetBreakPointsCss(Image, RuleSet, QueryString, nonceAttribute);
                var className = srcSet.GetClassName(Image, RuleSet, QueryString);

                if (!string.IsNullOrEmpty(className))
                {
                    output.AddClass(className, HtmlEncoder.Default);
                }

                // Emit the <style> block immediately before the element so the class rules apply to it.
                if (css != null)
                {
                    output.PreElement.AppendHtml(css);
                }

                // Inner content between the tags is preserved automatically.
            }
            catch (Exception e)
            {
                logger.LogError(e, "ds:background failed to render. Image: {ImageId}, RuleSet: {RuleSet}", Image?.Id, RuleSet);
                Error(output, Constants.RenderError, SuppressWarnings);
            }
        }

        private static void Error(TagHelperOutput output, string errorMessage, bool suppressWarnings = false)
        {
            if (suppressWarnings) return;
            output.TagName = "div";
            output.AddClass("container", HtmlEncoder.Default);
            output.AddClass("row", HtmlEncoder.Default);
            output.Attributes.SetAttribute("style", "color: red;");
            output.Content.SetContent(errorMessage);
        }
    }
}
