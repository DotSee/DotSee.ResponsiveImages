using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DotSee.ResponsiveImages.UrlProviders;
using Microsoft.Extensions.Logging;
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
        private readonly IResponsiveImageUrlProvider _urlProvider;
        private readonly IConfigSource _configSource;
        private readonly ILogger<CdnPurgeUrlBuilder> _logger;

        /// <param name="urlProvider">
        /// The same provider the renderers use, so the URLs submitted for purging are in whatever format
        /// the site actually rendered — purging Umbraco crop URLs on a site emitting Cloudflare ones would
        /// match nothing at the edge and fail silently.
        /// </param>
        public CdnPurgeUrlBuilder(IResponsiveImageUrlProvider urlProvider, IConfigSource configSource,
            ILogger<CdnPurgeUrlBuilder> logger = null)
        {
            _urlProvider = urlProvider;
            _configSource = configSource;
            _logger = logger;
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

                    var url = _urlProvider.GetCropUrlForPath(mediaPath, ruleSet, width, height);

                    if (!string.IsNullOrWhiteSpace(url)) { relativeUrls.Add(url); }
                }
            }

            foreach (var relative in relativeUrls.Take(maxUrls))
            {
                if (Uri.TryCreate(origin, relative, out var absolute))
                {
                    // Uri.TryCreate ignores the base when the second argument is itself absolute (or
                    // protocol-relative), which is exactly what an external media file system produces.
                    // A Cloudflare zone rejects a purge batch containing another host's URLs outright,
                    // so one blob-storage URL would take every legitimate URL down with it.
                    if (!string.Equals(absolute.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogDebug(
                            "Skipping purge URL {Url}: its host does not match DotSee:ImageCdn:BaseUrl ({BaseHost}). External media cannot be purged through this zone.",
                            absolute, origin.Host);
                        continue;
                    }

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
