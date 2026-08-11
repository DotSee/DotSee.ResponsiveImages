using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotSee.ResponsiveImages.Cdn
{
    /// <summary>
    /// Purges cached images from a CDN.
    /// </summary>
    public interface ICdnPurgeService
    {
        /// <summary>True when the service is configured and permitted to make outbound calls.</summary>
        bool IsEnabled { get; }

        /// <summary>Purges specific absolute URLs.</summary>
        Task<CdnPurgeResult> PurgeAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken = default);

        /// <summary>Purges the entire zone.</summary>
        Task<CdnPurgeResult> PurgeEverythingAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Outcome of a purge attempt. Never throws at the caller; failures are reported here.</summary>
    public sealed class CdnPurgeResult
    {
        private CdnPurgeResult(bool succeeded, bool attempted, string message, int urlCount)
        {
            Succeeded = succeeded;
            Attempted = attempted;
            Message = message;
            UrlCount = urlCount;
        }

        public bool Succeeded { get; }

        /// <summary>False when the purge was deliberately not made (disabled, misconfigured, nothing to do).</summary>
        public bool Attempted { get; }

        public string Message { get; }
        public int UrlCount { get; }

        public static CdnPurgeResult Skipped(string reason) => new CdnPurgeResult(true, false, reason, 0);
        public static CdnPurgeResult Success(int urlCount) => new CdnPurgeResult(true, true, "Purged.", urlCount);
        public static CdnPurgeResult Failure(string message) => new CdnPurgeResult(false, true, message, 0);
    }
}
