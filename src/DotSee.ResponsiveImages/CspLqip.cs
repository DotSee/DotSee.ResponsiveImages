using Microsoft.AspNetCore.Html;

namespace DotSee.ResponsiveImages
{
    /// <summary>
    /// The CSP-safe LQIP artifacts for a single image render: the nonce-tagged &lt;style&gt; and
    /// &lt;script&gt; blocks plus the unique <c>data-ds-id</c> that links them to the image element.
    /// <see cref="Active"/> is false when no CSP LQIP should be emitted (no nonce, lazy loading off,
    /// or no preview type configured), in which case the element is rendered with no inline LQIP.
    /// </summary>
    public sealed class CspLqip
    {
        public static readonly CspLqip Inactive = new CspLqip(null, null, null);

        public CspLqip(string dsId, HtmlString styleBlock, HtmlString scriptBlock)
        {
            DsId = dsId;
            StyleBlock = styleBlock;
            ScriptBlock = scriptBlock;
        }

        /// <summary>The unique <c>data-ds-id</c> value to place on the image element.</summary>
        public string DsId { get; }

        /// <summary>Nonce-tagged &lt;style&gt; block to emit before the element.</summary>
        public HtmlString StyleBlock { get; }

        /// <summary>Nonce-tagged &lt;script&gt; block to emit after the element.</summary>
        public HtmlString ScriptBlock { get; }

        public bool Active => DsId != null;
    }
}
