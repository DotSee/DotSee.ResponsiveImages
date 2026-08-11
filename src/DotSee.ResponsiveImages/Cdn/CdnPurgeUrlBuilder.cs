using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;

namespace DotSee.ResponsiveImages.Cdn
{
    /// <summary>
    /// Works out which absolute URLs to ask the CDN to drop when a media item changes.
    /// </summary>
    /// <remarks>
    /// Best effort by nature. The exact URL a page requested also depends on the focal point, the cache
    /// buster and any per-call query string, none of which are known here — so this covers the media's
    /// own URL plus the plain variant URLs the configured rule sets ask for. In practice the cache
    /// buster is the stronger guarantee: replacing a media item changes its URLs, so the stale ones are
    /// simply never requested again. Purging is for the cases that does not cover, such as a CDN
    /// configured to ignore query strings.
    /// </remarks>
    public class CdnPurgeUrlBuilder
    {
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IConfigSource _configSource;

        public CdnPurgeUrlBuilder(IImageUrlGenerator imageUrlGenerator, IConfigSource configSource)
        {
            _imageUrlGenerator = imageUrlGenerator;
            _configSource = configSource;
        }

        /// <summary>
        /// Absolute URLs for the given media path: the original, plus one per distinct size any rule set
        /// generates. Capped at <paramref name="maxUrls"/>.
        /// </summary>
        public IReadOnlyCollection<string> Build(string mediaPath, string baseUrl, int maxUrls)
        {
            var results = new List<string>();

            if (string.IsNullOrWhiteSpace(mediaPath) || string.IsNullOrWhiteSpace(baseUrl)) { return results; }
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin)) { return results; }

            var relativeUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mediaPath };

            foreach (var ruleSet in _configSource?.AllRuleSets ?? new List<Models.RuleSet>())
            {
                //Both ladders, since a site may render the same media through <ds:img> and <ds:picture>.
                var sizes = CandidateLadder.GetSrcSetCandidates(ruleSet)
                    .Concat(CandidateLadder.GetPictureSources(ruleSet).SelectMany(x => x.Candidates))
                    .Select(c => (c.Width, c.Height))
                    .Distinct();

                foreach (var (width, height) in sizes)
                {
                    if (width <= 0) { continue; }

                    var url = _imageUrlGenerator.GetImageUrl(new ImageUrlGenerationOptions(mediaPath)
                    {
                        Width = width,
                        Height = height > 0 ? height : null,
                        Quality = ruleSet.ImageQuality > 0 ? ruleSet.ImageQuality : null,
                        ImageCropMode = ruleSet.CropMode
                    });

                    if (!string.IsNullOrWhiteSpace(url)) { relativeUrls.Add(url); }
                }
            }

            foreach (var relative in relativeUrls.Take(maxUrls))
            {
                if (Uri.TryCreate(origin, relative, out var absolute))
                {
                    results.Add(absolute.ToString());
                }
            }

            return results;
        }

        /// <summary>
        /// Pulls the file path out of a media item's umbracoFile value, which is either an image cropper
        /// JSON object or a bare path depending on the property editor.
        /// </summary>
        public static string GetMediaPath(IMedia media)
        {
            var raw = media?.GetValue<string>(Umbraco.Cms.Core.Constants.Conventions.Media.File);
            if (string.IsNullOrWhiteSpace(raw)) { return null; }

            raw = raw.Trim();
            if (!raw.StartsWith("{", StringComparison.Ordinal)) { return raw; }

            try
            {
                using var document = JsonDocument.Parse(raw);
                return document.RootElement.TryGetProperty("src", out var src) ? src.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
