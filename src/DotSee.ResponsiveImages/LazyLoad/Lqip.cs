using System;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.LazyLoad
{
    internal static class Lqip
    {
        /// <summary>
        /// Resolves what a blur placeholder should point at: an inline base64 data URI when one can be
        /// built (no extra request, paints with the HTML), otherwise the supplied URL placeholder.
        /// </summary>
        public static string BlurSource(ILqipService lqipService, MediaWithCrops image, Func<string> urlFallback)
        {
            return lqipService?.GetDataUri(image) ?? urlFallback();
        }
    }
}
