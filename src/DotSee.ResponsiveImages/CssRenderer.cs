using System.Text;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace DotSee.ResponsiveImages
{
    public class CssRenderer
    {
        private readonly ImageUrlService _imageUrlService;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedUrlProvider _publishedUrlProvider;

        public CssRenderer(
            ImageUrlService imageUrlService
            , IImageUrlGenerator imageUrlGenerator
            , IPublishedUrlProvider publishedUrlProvider)
        {
            _imageUrlService = imageUrlService;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedUrlProvider = publishedUrlProvider;
        }

        /// <summary>
        /// Renders the responsive background-image CSS as a nonce-less &lt;style&gt; block so the result
        /// is cacheable. A per-request CSP nonce (if any) is injected by the caller after caching.
        /// </summary>
        public HtmlString RenderCss(ImageModel imageModel)
        {
            if (imageModel.IsSvg)
            {
                // An SVG scales itself; a single rule with the bare URL is all the background needs.
                // (This branch used to request a hard-coded 450x450 crop of the vector — and was dead
                // anyway, since IsSvg was never assigned.)
                StringBuilder sbSvg = new StringBuilder(string.Empty);
                sbSvg.Append("<style type=\"text/css\">\r\n");
                var svgUrl = Helpers.SanitizeCssUrl(imageModel.OriginalImage.Url());
                sbSvg.Append("\r\n.media-image-" + imageModel.ImageGuid + $" {{\r\nbackground-image:url('{svgUrl}');\r\n }}");
                sbSvg.Append("</style>");
                return (new HtmlString(sbSvg.ToString()));
            }

            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("<style type=\"text/css\">\r\n");

            //First append a dummy element without media query - required for some browsers to work.
            //background-image:none, NOT url(''): an empty url() resolves against the document base URL,
            //so every page carrying a background image would fetch its own HTML a second time as an image.
            sb.Append(".media-image-" + imageModel.ImageGuid + " {background-image:none;}\r\n");

            //Get all breakpoints, create from larger to smaller pixel ratio.
            foreach (var b in imageModel.BreakPoints)
            {
                //int myWidth = b.BreakPointWidth;

                sb.Append("@media only screen");

                string currNextBreakPointQueries = GetCurrentNextBreakPointQuery(b);

                sb.Append(currNextBreakPointQueries);

                sb.Append(" {\r\n.media-image-" + imageModel.ImageGuid + $" {{\r\nbackground-image:url('" + Helpers.SanitizeCssUrl(b.ImageUrl) + "');\r\n ");

                sb.Append(GetClosingElements(imageModel.ImageTop, imageModel.ImageLeft));

                if (imageModel.RuleSet.Use2x)
                {
                    sb.Append(Generate2x(imageModel, b.BreakPoint, currNextBreakPointQueries));
                }

                if (imageModel.RuleSet.Use3x)
                {
                    sb.Append(Generate3x(imageModel, b.BreakPoint, currNextBreakPointQueries));
                }
            }

            sb.Append("</style>");
            return (new HtmlString(sb.ToString()));
        }


        private string Generate2x(ImageModel imageModel, RuleBreakPoint b, string currNextBreakPointQueries)

        {
            //var styleGuid = CacheLiteralsRS.CachedImagesClassName + imageModel.rule + "_" + originalImage.Key.ToString().Replace("-", "");
            StringBuilder sb = new StringBuilder(string.Empty);

            string mediaQueryImage2x = null;

            int width2x = DpiWidth(imageModel.RuleSet, b, 2);
            mediaQueryImage2x = _imageUrlService.GetCropUrl(
                _imageUrlService.GetAltImageOrDefault(imageModel.OriginalImage, Helpers.GetBreakPointWidth(b, imageModel.RuleSet), null)
                , imageModel.RuleSet, width2x, 0, imageModel.QueryString);

            sb.Append("@media ");
            sb.Append("only screen and (-webkit-min-device-pixel-ratio: 5/4)");

            sb.Append(currNextBreakPointQueries);
            sb.Append(",");
            sb.Append("\r\nonly screen and (min--moz-device-pixel-ratio: 1.25)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (-o-min-device-pixel-ratio: 5/4)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (min-device-pixel-ratio: 1.25)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (min-resolution: 1.25dppx)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(" {\r\n.media-image-" + imageModel.ImageGuid + $" {{\r\nbackground-image:url('" + Helpers.SanitizeCssUrl(mediaQueryImage2x) + "'); ");

            sb.Append(GetClosingElements(imageModel.ImageTop, imageModel.ImageLeft));

            return sb.ToString();
        }

        private string Generate3x(ImageModel imageModel, RuleBreakPoint b, string currNextBreakPointQueries)
        {
            //var styleGuid = CacheLiteralsRS.CachedImagesClassName + ruleSet.Name + "_" + originalImage.Key.ToString().Replace("-", "");

            StringBuilder sb = new StringBuilder(string.Empty);

            string mediaQueryImage3x = null;

            int width3x = DpiWidth(imageModel.RuleSet, b, 3);
            mediaQueryImage3x = _imageUrlService.GetCropUrl(
                _imageUrlService.GetAltImageOrDefault(imageModel.OriginalImage, Helpers.GetBreakPointWidth(b, imageModel.RuleSet), null)
                , imageModel.RuleSet, width3x, 0, imageModel.QueryString);

            sb.Append("@media ");
            sb.Append("only screen and (-webkit-min-device-pixel-ratio: 2.25)");

            sb.Append(currNextBreakPointQueries);
            sb.Append(",");
            sb.Append("\r\nonly screen and (min--moz-device-pixel-ratio: 2.25)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (-o-min-device-pixel-ratio: 9/4)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (min-device-pixel-ratio: 2.25)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(",");
            sb.Append("\r\nonly screen and (min-resolution: 2.25dppx)");
            sb.Append(currNextBreakPointQueries);

            sb.Append(" {\r\n.media-image-" + imageModel.ImageGuid + $" {{\r\nbackground-image:url('" + Helpers.SanitizeCssUrl(mediaQueryImage3x) + "'); ");

            sb.Append(GetClosingElements(imageModel.ImageTop, imageModel.ImageLeft));

            return sb.ToString();
        }

        /// <summary>
        /// Width for a DPI variant of this breakpoint: the same resolved 1x width the breakpoint's own
        /// URL uses, multiplied and clamped to <see cref="RuleSet.OriginalImageMaxWidth"/>. The ceiling
        /// applies to the multiplied value — asking for twice the source ceiling only asks the image
        /// processor to upscale.
        /// </summary>
        private static int DpiWidth(RuleSet ruleSet, RuleBreakPoint b, int factor)
        {
            int scaled = Helpers.GetBreakPointWidth(b, ruleSet) * factor;
            int maxWidth = ruleSet.OriginalImageMaxWidth ?? 0;
            return (maxWidth > 0 && scaled > maxWidth) ? maxWidth : scaled;
        }

        private string GetClosingElements(double imageTop, double imageLeft)
        {
            StringBuilder sb = new StringBuilder(string.Empty);
            sb.Append("background-position:");
            sb.Append(imageLeft.ToString());
            sb.Append("% ");
            sb.Append(imageTop.ToString());
            sb.Append("%;");
            sb.Append("}} \r\n");
            return sb.ToString();
        }

        private string GetCurrentNextBreakPointQuery(ImageBreakPointModel bm)
        {
            StringBuilder sb = new StringBuilder(string.Empty);
            if (!bm.IsFirst)
            {
                sb.Append(" and (max-width: " + bm.BreakPointWidth + "px)");
            }
            sb.Append((bm.NextBreakPointWidth != 0) ? " and (min-width: " + (bm.NextBreakPointWidth + 1) + "px)" : string.Empty);
            return (sb.ToString());
        }


    }
}
