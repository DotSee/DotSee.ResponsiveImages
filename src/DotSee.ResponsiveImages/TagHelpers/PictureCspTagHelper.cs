using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// CSP-safe version of the picture element tag helper.
    /// Uses nonce-tagged style and script blocks instead of inline attributes for LQIP.
    /// </summary>
    [HtmlTargetElement("ds:picture-csp", Attributes = "image,nonce", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class PictureCspTagHelper(
        SrcSetManager srcSet,
        IGlobalLazyLoadSettings lazyLoadSettings,
        IConfigSource configSource,
        IImageUrlGenerator imageUrlGenerator,
        IPublishedUrlProvider publishedUrlProvider,
        ILogger<PictureCspTagHelper> logger) : TagHelper
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
        /// The CSP nonce value for inline style and script blocks
        /// </summary>
        [HtmlAttributeName("nonce")]
        public string Nonce { get; set; }

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
                    logger.LogError("No image found for ds:picture-csp tag helper. Model Alias: {ModelAlias}, Model Id: {ModelId}", model?.ContentType.Alias, model?.Id);
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

                // Resolve the rule set to determine lazy load and LQIP settings
                var ruleSetConfig = configSource.GetRuleByName(RuleSet);
                var isLazyLoad = ruleSetConfig != null && lazyLoadSettings.IsLazyLoadEnabled(ruleSetConfig);
                var needsLqip = isLazyLoad && !string.IsNullOrWhiteSpace(Nonce);

                // Generate a unique selector for CSP-safe style/script targeting
                var uniqueId = "ds-" + Guid.NewGuid().ToString("N").Substring(0, 8);

                // Merge any caller-supplied attributes with the internal data-ds-id used for LQIP targeting
                var imageAttributes = new Dictionary<string, string>(ImageAttributes);
                if (needsLqip)
                {
                    imageAttributes[SrcSetManager.DsIdAttributeName] = uniqueId;
                }

                // Emit the nonce-tagged <style> block before the picture element
                if (needsLqip)
                {
                    var selector = $"[{SrcSetManager.DsIdAttributeName}=\"{uniqueId}\"]";

                    if (lazyLoadSettings.PreviewType == PreviewType.Blur)
                    {
                        var lqipUrl = Image.GetCropUrl(imageUrlGenerator, null, publishedUrlProvider,
                            width: 40, quality: 20, imageCropMode: ruleSetConfig.CropMode);
                        output.Content.AppendHtml(
                            $"<style nonce=\"{Nonce}\">"
                            + $"{selector}{{background-size:cover;background-repeat:no-repeat;"
                            + $"background-image:url('{lqipUrl}');filter:blur(20px);transition:filter 0.3s}}"
                            + "</style>");
                    }
                    else if (lazyLoadSettings.PreviewType == PreviewType.LowResImage
                        && !string.IsNullOrWhiteSpace(lazyLoadSettings.LowResImagePath))
                    {
                        output.Content.AppendHtml(
                            $"<style nonce=\"{Nonce}\">"
                            + $"{selector}{{background-size:cover;background-repeat:no-repeat;"
                            + $"background-image:url('{lazyLoadSettings.LowResImagePath}')}}"
                            + "</style>");
                    }
                }

                // Generate picture element without inline LQIP (style/onload are handled by nonce blocks)
                output.Content.AppendHtml(srcSet.CreatePictureElement(
                    Image, RuleSet,
                    imageAlt: ImageAlt,
                    imageClass: ImageClass,
                    imageAttributes: imageAttributes.Count > 0 ? imageAttributes : null,
                    optionalQueryStringParameters: QueryString,
                    emitInlineLqip: false));

                // Emit the nonce-tagged <script> block after the picture element
                if (needsLqip)
                {
                    var selector = $"[{SrcSetManager.DsIdAttributeName}=\"{uniqueId}\"]";

                    if (lazyLoadSettings.PreviewType == PreviewType.Blur)
                    {
                        output.Content.AppendHtml(
                            $"<script nonce=\"{Nonce}\">"
                            + $"document.querySelector('{selector}').addEventListener('load',function(){{"
                            + "this.style.filter='none';this.style.backgroundImage='none'"
                            + "});</script>");
                    }
                    else if (lazyLoadSettings.PreviewType == PreviewType.LowResImage
                        && !string.IsNullOrWhiteSpace(lazyLoadSettings.LowResImagePath))
                    {
                        output.Content.AppendHtml(
                            $"<script nonce=\"{Nonce}\">"
                            + $"document.querySelector('{selector}').addEventListener('load',function(){{"
                            + "this.style.backgroundImage='none'"
                            + "});</script>");
                    }
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
