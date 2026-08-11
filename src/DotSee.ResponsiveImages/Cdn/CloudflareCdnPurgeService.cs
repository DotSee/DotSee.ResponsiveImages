using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotSee.ResponsiveImages.Cdn
{
    /// <summary>
    /// Purges Cloudflare's cache through the zone purge API.
    /// </summary>
    /// <remarks>
    /// Makes no outbound call unless <see cref="ImageCdnSettings.Enabled"/> is true and the zone id and
    /// API token are both present. A failure is logged and reported, never thrown: an editor saving a
    /// media item should not see an error because a CDN was unreachable.
    /// </remarks>
    public class CloudflareCdnPurgeService : ICdnPurgeService
    {
        public const string HttpClientName = "DotSee.ResponsiveImages.Cdn";

        /// <summary>Cloudflare rejects purge requests carrying more than 30 files.</summary>
        private const int CloudflareFilesPerRequest = 30;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<ImageCdnSettings> _settings;
        private readonly ILogger<CloudflareCdnPurgeService> _logger;

        public CloudflareCdnPurgeService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<ImageCdnSettings> settings,
            ILogger<CloudflareCdnPurgeService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings;
            _logger = logger;
        }

        public bool IsEnabled
        {
            get
            {
                var s = _settings.CurrentValue;
                return s.Enabled
                    && !string.IsNullOrWhiteSpace(s.ZoneId)
                    && !string.IsNullOrWhiteSpace(s.ApiToken);
            }
        }

        public async Task<CdnPurgeResult> PurgeAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) { return CdnPurgeResult.Skipped("CDN purging is disabled or not configured."); }
            if (urls == null || urls.Count == 0) { return CdnPurgeResult.Skipped("No URLs to purge."); }

            var batches = urls.Distinct()
                .Select((url, index) => new { url, index })
                .GroupBy(x => x.index / CloudflareFilesPerRequest)
                .Select(g => g.Select(x => x.url).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var result = await SendAsync(new { files = batch }, batch.Count, cancellationToken);
                if (!result.Succeeded) { return result; }
            }

            return CdnPurgeResult.Success(urls.Count);
        }

        public Task<CdnPurgeResult> PurgeEverythingAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) { return Task.FromResult(CdnPurgeResult.Skipped("CDN purging is disabled or not configured.")); }

            return SendAsync(new { purge_everything = true }, 0, cancellationToken);
        }

        private async Task<CdnPurgeResult> SendAsync(object payload, int urlCount, CancellationToken cancellationToken)
        {
            var settings = _settings.CurrentValue;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://api.cloudflare.com/client/v4/zones/{settings.ZoneId}/purge_cache")
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);

                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Cloudflare purge failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
                    return CdnPurgeResult.Failure($"Cloudflare returned {(int)response.StatusCode}.");
                }

                _logger.LogInformation("Purged {Count} image URL(s) from Cloudflare.", urlCount);
                return CdnPurgeResult.Success(urlCount);
            }
            catch (Exception e)
            {
                //A CDN being unreachable must not surface as a failed media save.
                _logger.LogWarning(e, "Cloudflare purge request could not be sent.");
                return CdnPurgeResult.Failure(e.Message);
            }
        }
    }

    /// <summary>
    /// Used when CDN purging is switched off or the provider is unrecognised. Exists so callers never
    /// have to null-check, and so nothing can be purged by accident.
    /// </summary>
    public class NullCdnPurgeService : ICdnPurgeService
    {
        public bool IsEnabled => false;

        public Task<CdnPurgeResult> PurgeAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken = default)
            => Task.FromResult(CdnPurgeResult.Skipped("CDN purging is disabled."));

        public Task<CdnPurgeResult> PurgeEverythingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CdnPurgeResult.Skipped("CDN purging is disabled."));
    }
}
