using System.Linq;
using DotSee.ResponsiveImages.Preloading;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// Emits the <c>&lt;link rel="preload"&gt;</c> hints collected from every <c>above-fold</c> image
    /// rendered this request. Place it in the <c>&lt;head&gt;</c> of your layout.
    /// </summary>
    /// <remarks>
    /// Razor runs a view before its layout, so by the time the layout writes the head, the body has
    /// already registered its hints. Images rendered by the layout itself before this tag will not be
    /// included — there is nothing to collect yet at that point.
    /// </remarks>
    [HtmlTargetElement("ds:preloads", TagStructure = TagStructure.WithoutEndTag)]
    public class PreloadsTagHelper(IPreloadCollector collector) : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = null;

            if (collector.Links.Count == 0)
            {
                output.SuppressOutput();
                return;
            }

            output.Content.SetHtmlContent(string.Join("\n", collector.Links));
        }
    }
}
