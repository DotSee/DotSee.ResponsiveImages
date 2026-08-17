using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages.UrlProviders
{
    /// <summary>
    /// Emits Cloudflare image-transformation URLs —
    /// <c>/cdn-cgi/image/width=1200,fit=cover,gravity=0.4x0.6,quality=70,format=auto/media/abc/photo.jpg</c>
    /// — so the resizing happens at the edge instead of on the origin.
    /// </summary>
    /// <remarks>
    /// The package still decides <em>which</em> sizes to ask for and writes the markup; Cloudflare only
    /// produces the pixels. The editor's focal point is carried across as Cloudflare's <c>gravity</c>
    /// option, which is what keeps the one piece of information a CDN can never guess at — where the
    /// subject of the image is — in charge of the crop.
    ///
    /// Transformations must be enabled on the zone serving the site, and <c>/cdn-cgi/image/</c> does not
    /// resolve on localhost, so local testing verifies the markup rather than the delivered image.
    /// </remarks>
    public class CloudflareImageUrlProvider : IResponsiveImageUrlProvider
    {
        /// <summary>Width of the fallback URL placeholder, in pixels.</summary>
        internal const int PlaceholderWidth = 40;

        /// <summary>Quality of the fallback URL placeholder.</summary>
        internal const int PlaceholderQuality = 20;

        /// <summary>Cloudflare <c>blur</c> strength (0-250) for the fallback URL placeholder.</summary>
        internal const int PlaceholderBlur = 64;

        /// <summary>
        /// Options the package owns or replaces, so a caller-supplied query string cannot fight the
        /// candidate ladder over sizing or the rule set over the focal point.
        /// </summary>
        private static readonly HashSet<string> IgnoredFurtherOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "width", "height", "w", "h", "mode", "rxy", "center", "anchor"
        };

        private readonly IOptionsMonitor<CloudflareImageSettings> _settings;
        private readonly IPublishedUrlProvider _publishedUrlProvider;

        public CloudflareImageUrlProvider(
              IOptionsMonitor<CloudflareImageSettings> settings
            , IPublishedUrlProvider publishedUrlProvider)
        {
            _settings = settings;
            _publishedUrlProvider = publishedUrlProvider;
        }

        private CloudflareImageSettings Settings => _settings?.CurrentValue ?? new CloudflareImageSettings();

        public string GetCropUrl(MediaWithCrops image, RuleSet ruleSet, int width, int height, string furtherOptions = null)
        {
            if (image == null) { return null; }

            var source = ResolveSourcePath(image);
            if (string.IsNullOrWhiteSpace(source)) { return null; }

            var extras = ParseFurtherOptions(furtherOptions, out string formatOverride, out string qualityOverride);

            var options = new List<string>();
            AddIfPositive(options, "width", width);
            AddIfPositive(options, "height", height);

            string fit = ToFit(ruleSet.CropMode);
            options.Add("fit=" + fit);

            string gravity = ToGravity(image, ruleSet, fit);
            if (gravity != null) { options.Add("gravity=" + gravity); }

            AddQuality(options, ruleSet, qualityOverride);
            AddFormat(options, formatOverride);
            AddSetting(options, "metadata", Settings.Metadata);
            AddSetting(options, "onerror", Settings.OnError);

            return Compose(options, source, extras);
        }

        public string GetPlaceholderUrl(MediaWithCrops image, RuleSet ruleSet)
        {
            if (image == null) { return null; }

            var source = ResolveSourcePath(image);
            if (string.IsNullOrWhiteSpace(source)) { return null; }

            // Width only, so nothing is cropped - no fit or gravity needed, and one fewer distinct variant
            // for a per-transformation-billed CDN to generate.
            var options = new List<string>
            {
                "width=" + PlaceholderWidth.ToString(CultureInfo.InvariantCulture),
                "quality=" + PlaceholderQuality.ToString(CultureInfo.InvariantCulture),
                "blur=" + PlaceholderBlur.ToString(CultureInfo.InvariantCulture)
            };
            AddFormat(options, null);
            AddSetting(options, "metadata", Settings.Metadata);
            AddSetting(options, "onerror", Settings.OnError);

            return Compose(options, source, null);
        }

        /// <summary>
        /// The purge path. Necessarily without a focal point: a media-save notification carries the file,
        /// not the published content the crop was rendered from.
        /// </summary>
        public string GetCropUrlForPath(string mediaPath, RuleSet ruleSet, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(mediaPath)) { return null; }

            var options = new List<string>();
            AddIfPositive(options, "width", width);
            AddIfPositive(options, "height", height);
            options.Add("fit=" + ToFit(ruleSet.CropMode));
            AddQuality(options, ruleSet, null);
            AddFormat(options, null);
            AddSetting(options, "metadata", Settings.Metadata);
            AddSetting(options, "onerror", Settings.OnError);

            return Compose(options, NormaliseSource(mediaPath), null);
        }

        /// <summary>
        /// <c>{BaseUrl}{Prefix}/{options}/{source}</c>, per Cloudflare's URL format. The commas separating
        /// the options sit in the path, which is safe inside a <c>srcset</c> — the HTML parser only treats
        /// a comma as a candidate separator after whitespace or a descriptor.
        /// </summary>
        private string Compose(List<string> options, string source, string sourceQueryString)
        {
            var settings = Settings;

            string prefix = string.IsNullOrWhiteSpace(settings.Prefix) ? "/cdn-cgi/image" : settings.Prefix.Trim();
            prefix = "/" + prefix.Trim('/');

            string baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? string.Empty : settings.BaseUrl.TrimEnd('/');

            if (!string.IsNullOrEmpty(sourceQueryString))
            {
                source += (source.IndexOf('?') >= 0 ? "&" : "?") + sourceQueryString;
            }

            return baseUrl + prefix + "/" + string.Join(",", options) + "/" + source;
        }

        /// <summary>
        /// The URL Cloudflare should fetch the original from. Relative for media served off the site
        /// itself; an absolute URL when the media file system is external, which Cloudflare also accepts.
        /// </summary>
        private string ResolveSourcePath(MediaWithCrops image)
        {
            string url = null;

            try
            {
                url = _publishedUrlProvider?.GetMediaUrl(
                    image, UrlMode.Relative, null, Umbraco.Cms.Core.Constants.Conventions.Media.File, null);
            }
            catch (Exception)
            {
                // Fall through to the cropper value below; a URL provider that cannot resolve the media
                // must not take the whole page down.
            }

            if (string.IsNullOrWhiteSpace(url)) { url = image.LocalCrops?.Src; }

            url = NormaliseSource(url);

            if (url == null || !Settings.CacheBuster) { return url; }

            var cacheBuster = GetCacheBusterValue(image);

            return cacheBuster == null
                ? url
                : url + (url.IndexOf('?') >= 0 ? "&" : "?") + "v=" + cacheBuster;
        }

        /// <summary>
        /// The same cache buster Umbraco appends to its own crop URLs — the media item's update date as a
        /// hex file time — so replacing a file in place produces new URLs at the edge too.
        /// </summary>
        private static string GetCacheBusterValue(MediaWithCrops image)
        {
            try
            {
                var updated = image.UpdateDate;

                // A media item with no meaningful update date (an unsaved or mocked one) has nothing to bust
                // against, and ToFileTimeUtc throws below the FileTime epoch.
                if (updated.Year <= 1601) { return null; }

                return updated.ToFileTimeUtc().ToString("x", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NormaliseSource(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) { return null; }

            url = url.Trim();

            // Absolute and protocol-relative sources are passed through verbatim — an external media
            // file system (blob storage behind a CDN) emits both forms, and trimming the "//" off a
            // protocol-relative URL would silently turn it into a broken local path. Only a
            // site-relative source loses its leading slash, since it already follows one in the
            // composed path.
            if (url.StartsWith("//", StringComparison.Ordinal)) { return url; }
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile) { return url; }

            return url.TrimStart('/');
        }

        /// <summary>
        /// Maps Umbraco's crop mode onto Cloudflare's <c>fit</c>.
        /// </summary>
        /// <remarks>
        /// <c>Min</c> and <c>Stretch</c> are approximations — Cloudflare has no shortest-side-constrained
        /// mode and no distort mode, and <c>cover</c> is the closest behaviour in both cases.
        /// </remarks>
        public static string ToFit(ImageCropMode cropMode)
        {
            switch (cropMode)
            {
                case ImageCropMode.Max:
                    return "scale-down";
                case ImageCropMode.Pad:
                case ImageCropMode.BoxPad:
                    return "pad";
                case ImageCropMode.Crop:
                case ImageCropMode.Min:
                case ImageCropMode.Stretch:
                default:
                    return "cover";
            }
        }

        /// <summary>
        /// The editor's focal point as Cloudflare's <c>gravity=XxY</c>, or null when there is nothing to
        /// anchor. Cloudflare ignores gravity unless the fit actually crops, so it is omitted otherwise
        /// rather than emitted as a dead option that would only fragment the CDN's cache.
        /// </summary>
        private static string ToGravity(MediaWithCrops image, RuleSet ruleSet, string fit)
        {
            if (!ruleSet.UseFocalPoint) { return null; }
            if (fit != "cover" && fit != "crop") { return null; }

            var focalPoint = ResolveFocalPoint(image);
            if (focalPoint == null) { return null; }

            decimal left = focalPoint.Left;
            decimal top = focalPoint.Top;

            if (left < 0m || left > 1m || top < 0m || top > 1m) { return null; }

            return Format(left) + "x" + Format(top);

            static string Format(decimal value)
                => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The focal point from the property's own cropper value, falling back to the one saved on the
        /// media item — the same two places Umbraco's own <c>GetCropUrl</c> looks.
        /// </summary>
        private static ImageCropperValue.ImageCropperFocalPoint ResolveFocalPoint(MediaWithCrops image)
        {
            var local = image.LocalCrops?.FocalPoint;
            if (local != null) { return local; }

            try
            {
                var value = image.Content?
                    .GetProperty(Umbraco.Cms.Core.Constants.Conventions.Media.File)?
                    .GetValue() as ImageCropperValue;

                return value?.FocalPoint;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddIfPositive(List<string> options, string name, int value)
        {
            if (value > 0) { options.Add(name + "=" + value.ToString(CultureInfo.InvariantCulture)); }
        }

        /// <summary>The formats Cloudflare accepts, plus "none" meaning "keep the source format".</summary>
        private static readonly HashSet<string> KnownFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auto", "avif", "webp", "jpeg", "baseline-jpeg", "json", "none"
        };

        /// <summary>The keyword quality values Cloudflare accepts alongside 1-100.</summary>
        private static readonly HashSet<string> KnownQualityKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "high", "medium-high", "medium-low", "low"
        };

        private static void AddQuality(List<string> options, RuleSet ruleSet, string qualityOverride)
        {
            // The override arrives via the per-call query string, so it is validated rather than
            // trusted: interpolated into the URL path, a "quality" of "1,fit=pad" or "1/.." would
            // inject an extra Cloudflare option or rewrite the source path.
            if (!string.IsNullOrWhiteSpace(qualityOverride))
            {
                var candidate = qualityOverride.Trim();
                if (int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= 1 && parsed <= 100)
                {
                    options.Add("quality=" + parsed.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                if (KnownQualityKeywords.Contains(candidate))
                {
                    options.Add("quality=" + candidate.ToLowerInvariant());
                    return;
                }
                // Unrecognised override: fall through to the rule set's own quality.
            }

            if (ruleSet.ImageQuality > 0)
            {
                options.Add("quality=" + ruleSet.ImageQuality.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// A per-call <c>format</c> (which is how <c>UseWebP</c> reaches here) wins over the configured
        /// default, so a site that explicitly asked for WebP still gets it. Both values are validated
        /// against Cloudflare's own format list — they end up in the URL path, where a stray comma or
        /// slash would change the request's meaning.
        /// </summary>
        private void AddFormat(List<string> options, string formatOverride)
        {
            // A present-but-empty per-call format ("format=") means "omit the option", matching what an
            // empty configured Format does — only an absent override falls back to the configured value.
            string format = formatOverride != null ? formatOverride.Trim() : Settings.Format?.Trim();

            if (string.IsNullOrEmpty(format) || !KnownFormats.Contains(format)) { return; }

            // "none" is not a Cloudflare format, so it reads as "leave the source format alone".
            if (format.InvariantEquals("none")) { return; }

            options.Add("format=" + format.ToLowerInvariant());
        }

        /// <summary>
        /// Adds a configured option, omitting it when the setting is blank. Note <c>metadata=none</c> is a
        /// real Cloudflare value (strip EXIF), not an instruction to omit the option. Values containing
        /// the option-list separators are dropped: a comma or slash inside a value would splice extra
        /// options into the URL path.
        /// </summary>
        private static void AddSetting(List<string> options, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return; }

            value = value.Trim();
            if (value.IndexOfAny(new[] { ',', '/', '=', ' ' }) >= 0) { return; }

            options.Add(name + "=" + value);
        }

        /// <summary>
        /// Splits a caller-supplied query string into the parts Cloudflare understands as options and the
        /// rest, which is re-attached to the source URL and so reaches the origin unchanged.
        /// </summary>
        private static string ParseFurtherOptions(string furtherOptions, out string format, out string quality)
        {
            format = null;
            quality = null;

            if (string.IsNullOrWhiteSpace(furtherOptions)) { return null; }

            var parsed = HttpUtility.ParseQueryString(furtherOptions.TrimStart('&', '?'));
            var passThrough = new List<string>();

            foreach (string key in parsed.AllKeys)
            {
                if (key == null) { continue; }

                string value = parsed[key];

                if (key.InvariantEquals("format")) { format = value; continue; }
                if (key.InvariantEquals("quality")) { quality = value; continue; }
                if (IgnoredFurtherOptions.Contains(key)) { continue; }

                passThrough.Add(HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(value));
            }

            return passThrough.Count > 0 ? string.Join("&", passThrough) : null;
        }
    }
}
