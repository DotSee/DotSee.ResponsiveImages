using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// Render any image through the Source Set Manager.
    /// If no image is found, nothing will be rendered.
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

                output.Content.AppendHtml(srcSet.CreatePictureElement(Image, RuleSet, imageAlt: ImageAlt, imageClass: ImageClass));
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