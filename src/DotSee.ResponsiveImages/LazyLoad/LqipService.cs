using System;
using System.IO;
using DotSee.ResponsiveImages.Caching;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

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

        /// <summary>
        /// Upper bound on the source's declared pixel count before we refuse to decode it. Decoding
        /// happens on the request thread, and a small file can declare enormous dimensions — a 200 KB
        /// PNG claiming 40000×40000 would decode to several GB of pixel buffers. 100 MP comfortably
        /// covers real photography (a 6000×4000 hero is 24 MP) while stopping the bombs.
        /// </summary>
        private const long MaxSourcePixels = 100_000_000;

        /// <summary>
        /// How long a failed placeholder build is remembered. Deliberately short and absolute — the
        /// common causes (file still being written by the upload, locked by a scanner) are transient,
        /// and a sliding negative entry on a busy page would never expire.
        /// </summary>
        private static readonly TimeSpan FailureCacheTimeout = TimeSpan.FromMinutes(2);

        private readonly MediaFileManager _mediaFileManager;
        private readonly ICacheService _cacheService;
        private readonly ILogger<LqipService> _logger;
        private readonly IPublishedUrlProvider _publishedUrlProvider;

        public LqipService(
            MediaFileManager mediaFileManager,
            ICacheService cacheService,
            ILogger<LqipService> logger,
            IPublishedUrlProvider publishedUrlProvider = null)
        {
            _mediaFileManager = mediaFileManager;
            _cacheService = cacheService;
            _logger = logger;
            _publishedUrlProvider = publishedUrlProvider;
        }

        public string GetDataUri(MediaWithCrops image)
        {
            if (image == null) { return null; }

            // The update date is part of the key so replacing a media file in place produces a fresh
            // placeholder without waiting for a publish to clear the whole cache.
            return _cacheService.GetCachedItem(
                CacheLiteralsRS.Lqip + image.Key.ToString("N") + "_" + GetVersionToken(image)
                , () => Build(image)
                , timeout: TimeSpan.FromMinutes(20)
                , isSliding: true
                , nullResultTimeout: FailureCacheTimeout);
        }

        private static string GetVersionToken(MediaWithCrops image)
        {
            try
            {
                var updated = image.UpdateDate;
                return updated.Year <= 1601 ? "0" : updated.ToFileTimeUtc().ToString("x");
            }
            catch (Exception)
            {
                return "0";
            }
        }

        private string Build(MediaWithCrops image)
        {
            var path = ResolveMediaPath(image);
            if (string.IsNullOrWhiteSpace(path)) { return null; }

            try
            {
                if (!_mediaFileManager.FileSystem.FileExists(path)) { return null; }

                // Identify first: it reads only the header, so a hostile file's declared dimensions are
                // known before any pixel buffer is allocated.
                ImageInfo info;
                using (var headerStream = _mediaFileManager.FileSystem.OpenFile(path))
                {
                    info = Image.Identify(headerStream);
                }

                if ((long)info.Width * info.Height > MaxSourcePixels)
                {
                    _logger.LogWarning(
                        "Media {MediaPath} declares {Width}x{Height} pixels, above the {Max} pixel LQIP decode budget; falling back to a URL placeholder.",
                        path, info.Width, info.Height, MaxSourcePixels);
                    return null;
                }

                using var source = _mediaFileManager.FileSystem.OpenFile(path);
                using var decoded = Image.Load(new DecoderOptions { MaxFrames = 1 }, source);

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
        /// Prefers the published URL provider — the cropper value's <c>Src</c> is not populated by every
        /// picker — and falls back to it otherwise.
        /// </summary>
        private string ResolveMediaPath(MediaWithCrops image)
        {
            string src = null;

            try
            {
                src = _publishedUrlProvider?.GetMediaUrl(
                    image, UrlMode.Relative, null, Umbraco.Cms.Core.Constants.Conventions.Media.File, null);
            }
            catch (Exception)
            {
                // Fall through to the cropper value; an unresolvable URL must not take the page down.
            }

            if (string.IsNullOrWhiteSpace(src)) { src = image.LocalCrops?.Src; }
            if (string.IsNullOrWhiteSpace(src)) { return null; }

            var queryStart = src.IndexOf('?');
            if (queryStart >= 0) { src = src.Substring(0, queryStart); }

            if (Uri.TryCreate(src, UriKind.Absolute, out var absolute)) { src = absolute.AbsolutePath; }

            src = Uri.UnescapeDataString(src);

            // The traversal check runs on the final, fully-decoded value — checking any earlier would
            // let an encoded "..%2f" materialise after the check. The path is handed to the media
            // IFileSystem; whether that implementation confines itself to the media root is its
            // business, but a relative traversal never has a legitimate reason to appear here.
            if (src.Contains("..")) { return null; }

            return src;
        }
    }
}
