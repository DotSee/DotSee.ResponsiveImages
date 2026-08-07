using System;
using System.IO;
using DotSee.ResponsiveImages.Caching;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.LazyLoad
{
    /// <summary>
    /// Builds inline LQIP data URIs by decoding the source media off the media file system and
    /// downscaling it to a handful of pixels. The result is a few hundred bytes, which is small enough
    /// to inline in markup and cheaper than the round trip a URL placeholder would cost.
    /// </summary>
    public class LqipService : ILqipService
    {
        /// <summary>Placeholder width in pixels. Blurred on display, so detail beyond this is wasted bytes.</summary>
        private const int PlaceholderWidth = 20;

        private const int PlaceholderQuality = 40;

        private readonly MediaFileManager _mediaFileManager;
        private readonly ICacheService _cacheService;
        private readonly ILogger<LqipService> _logger;

        public LqipService(MediaFileManager mediaFileManager, ICacheService cacheService, ILogger<LqipService> logger)
        {
            _mediaFileManager = mediaFileManager;
            _cacheService = cacheService;
            _logger = logger;
        }

        public string GetDataUri(MediaWithCrops image)
        {
            if (image == null) { return null; }

            // A null result is cached too: a media item we cannot decode will not become decodable on
            // the next request, and retrying per request would be far more expensive than the miss.
            return _cacheService.GetCachedItem(
                CacheLiteralsRS.Lqip + image.Key.ToString("N")
                , () => Build(image)
                , timeout: TimeSpan.FromMinutes(20)
                , isSliding: true);
        }

        private string Build(MediaWithCrops image)
        {
            var path = ResolveMediaPath(image);
            if (string.IsNullOrWhiteSpace(path)) { return null; }

            try
            {
                if (!_mediaFileManager.FileSystem.FileExists(path)) { return null; }

                using var source = _mediaFileManager.FileSystem.OpenFile(path);
                using var decoded = Image.Load(source);

                decoded.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(PlaceholderWidth, 0),
                    Mode = ResizeMode.Max
                }));

                using var buffer = new MemoryStream();
                decoded.Save(buffer, new WebpEncoder { Quality = PlaceholderQuality });

                return "data:image/webp;base64," + Convert.ToBase64String(buffer.ToArray());
            }
            catch (Exception e)
            {
                // Never let a placeholder take the page down - the caller falls back to a URL placeholder.
                _logger.LogDebug(e, "Could not build an inline LQIP for media {MediaPath}; falling back to a URL placeholder.", path);
                return null;
            }
        }

        /// <summary>
        /// Resolves the media file system path for the image, tolerating absolute URLs and query strings.
        /// </summary>
        private static string ResolveMediaPath(MediaWithCrops image)
        {
            var src = image.LocalCrops?.Src;
            if (string.IsNullOrWhiteSpace(src)) { return null; }

            var queryStart = src.IndexOf('?');
            if (queryStart >= 0) { src = src.Substring(0, queryStart); }

            if (Uri.TryCreate(src, UriKind.Absolute, out var absolute)) { src = absolute.AbsolutePath; }

            return Uri.UnescapeDataString(src);
        }
    }
}
