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

        /// <summary>Cap on how much of an error response body is copied into the log.</summary>
        private const int MaxLoggedBodyLength = 500;

        private async Task<CdnPurgeResult> SendAsync(object payload, int urlCount, CancellationToken cancellationToken)
        {
            // One read per operation: IsEnabled validated a snapshot moments ago, and re-reading here
            // could observe a mid-reload value (e.g. a momentarily null token → a guaranteed 401).
            var settings = _settings.CurrentValue;
            if (string.IsNullOrWhiteSpace(settings.ZoneId) || string.IsNullOrWhiteSpace(settings.ApiToken))
            {
                return CdnPurgeResult.Skipped("CDN purging is disabled or not configured.");
            }

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    // Escaped so a mispasted zone id containing '/' cannot silently retarget the request
                    // to a different API path.
                    $"https://api.cloudflare.com/client/v4/zones/{Uri.EscapeDataString(settings.ZoneId)}/purge_cache")
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);

                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Truncated: a WAF or captive portal can answer with a whole HTML page, and copying
                    // it verbatim into the log per failed batch is log flooding.
                    if (body != null && body.Length > MaxLoggedBodyLength) { body = body.Substring(0, MaxLoggedBodyLength) + "…"; }
                    _logger.LogWarning(
                        "Cloudflare purge failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
                    return CdnPurgeResult.Failure($"Cloudflare returned {(int)response.StatusCode}.");
                }

                if (urlCount > 0)
                {
                    _logger.LogInformation("Purged {Count} image URL(s) from Cloudflare.", urlCount);
                }
                else
                {
                    _logger.LogInformation("Purged the entire Cloudflare zone.");
                }
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
        private readonly IOptionsMonitor<ImageCdnSettings> _settings;
        private readonly ILogger<NullCdnPurgeService> _logger;
        private int _warned;

        public NullCdnPurgeService(
            IOptionsMonitor<ImageCdnSettings> settings = null,
            ILogger<NullCdnPurgeService> logger = null)
        {
            _settings = settings;
            _logger = logger;
        }

        public bool IsEnabled
        {
            get
            {
                WarnIfConfiguredButInactive();
                return false;
            }
        }

        public Task<CdnPurgeResult> PurgeAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken = default)
            => Task.FromResult(CdnPurgeResult.Skipped("CDN purging is disabled."));

        public Task<CdnPurgeResult> PurgeEverythingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CdnPurgeResult.Skipped("CDN purging is disabled."));

        /// <summary>
        /// The provider choice is made once at startup, so "Enabled": true reaching this implementation
        /// means either an unrecognised Provider value or a configuration change after startup — both of
        /// which would otherwise fail with no diagnostic at all. Logged once per process.
        /// </summary>
        private void WarnIfConfiguredButInactive()
        {
            if (_settings == null || _logger == null) { return; }
            if (!_settings.CurrentValue.Enabled) { return; }
            if (Interlocked.Exchange(ref _warned, 1) == 1) { return; }

            _logger.LogWarning(
                "DotSee:ImageCdn:Enabled is true, but no purge service is active. Either Provider ('{Provider}') is not a recognised value (only 'Cloudflare' is implemented), or purging was enabled after startup — the provider is selected once at startup, so a restart is required.",
                _settings.CurrentValue.Provider);
        }
    }
}
