using System;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;

namespace DotSee.ResponsiveImages.TagHelpers
{
    /// <summary>
    /// Obsolete. The CSP-safe behaviour is now built into <c>&lt;ds:picture&gt;</c>: supply a <c>nonce</c>
    /// attribute and it renders nonce-tagged style/script blocks instead of inline style/onload.
    /// This element remains as a backwards-compatible alias and will be removed in a future version.
    /// </summary>
    [Obsolete("Use <ds:picture> with a nonce attribute instead. <ds:picture-csp> will be removed in a future version.")]
    [HtmlTargetElement("ds:picture-csp", Attributes = "image", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class PictureCspTagHelper(SrcSetManager srcSet, ILogger<PictureElementTagHelper> logger)
        : PictureElementTagHelper(srcSet, logger)
    {
    }
}
