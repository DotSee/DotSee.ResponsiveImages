namespace DotSee.ResponsiveImages.Cdn
{
    /// <summary>
    /// Settings for purging a CDN when media changes, bound from the <c>DotSee:ImageCdn</c>
    /// configuration section.
    /// </summary>
    /// <remarks>
    /// Everything here is off unless explicitly switched on. Purging is an outbound call that affects
    /// live infrastructure, so it must never happen because a package was installed — only because
    /// someone configured it to.
    /// </remarks>
    public class ImageCdnSettings
    {
        /// <summary>
        /// Master switch. While false, no CDN call of any kind is made and the rest of this section is
        /// ignored. Defaults to false.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Which CDN to talk to. Currently only <c>Cloudflare</c> is implemented.</summary>
        public string Provider { get; set; } = "Cloudflare";

        /// <summary>Cloudflare zone id for the zone serving the site.</summary>
        public string ZoneId { get; set; }

        /// <summary>
        /// API token with the "Zone / Cache Purge" permission. Keep it out of appsettings.json — use
        /// user secrets or an environment variable.
        /// </summary>
        public string ApiToken { get; set; }

        /// <summary>
        /// Absolute public origin of the site, e.g. <c>https://www.example.com</c>. Required for
        /// <see cref="CdnPurgeMode.Files"/>, which needs absolute URLs. Purging is skipped with a
        /// warning when it is missing.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>Purge when a media item is saved. Defaults to true (still gated by <see cref="Enabled"/>).</summary>
        public bool PurgeOnMediaSave { get; set; } = true;

        /// <summary>Purge when a media item is deleted. Defaults to true (still gated by <see cref="Enabled"/>).</summary>
        public bool PurgeOnMediaDelete { get; set; } = true;

        /// <summary>How much to purge. Defaults to <see cref="CdnPurgeMode.Files"/>.</summary>
        public CdnPurgeMode Mode { get; set; } = CdnPurgeMode.Files;

        /// <summary>
        /// Upper bound on URLs submitted for a single media change, so a rule set with many breakpoints
        /// cannot produce an unbounded request. Defaults to 100.
        /// </summary>
        public int MaxUrlsPerPurge { get; set; } = 100;
    }

    public enum CdnPurgeMode
    {
        /// <summary>
        /// Purge the changed media's own URL and the variant URLs the configured rule sets would ask
        /// for. Targeted, but best effort — see <see cref="CdnPurgeUrlBuilder"/>.
        /// </summary>
        Files = 0,

        /// <summary>
        /// Purge the entire zone on every qualifying media change. Reliable and very blunt: it discards
        /// the cache for the whole site, not just images. Opt in deliberately.
        /// </summary>
        Everything = 1
    }
}
