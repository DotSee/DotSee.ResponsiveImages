namespace DotSee.ResponsiveImages.UrlProviders
{
    /// <summary>
    /// Settings for the Cloudflare URL provider, bound from <c>DotSee:ResponsiveImages:Cloudflare</c>.
    /// Only read when <c>DotSee:ResponsiveImages:UrlProvider</c> is <c>Cloudflare</c>.
    /// </summary>
    /// <remarks>
    /// Every value has a working default, so switching provider needs no other configuration.
    /// </remarks>
    public class CloudflareImageSettings
    {
        /// <summary>
        /// Path prefix Cloudflare listens on for transformations. Change it only if the site serves
        /// transformations through a Worker route rather than the built-in endpoint.
        /// </summary>
        public string Prefix { get; set; } = "/cdn-cgi/image";

        /// <summary>
        /// Absolute origin to prefix generated URLs with, e.g. <c>https://images.example.com</c>. Leave
        /// unset (the default) to emit relative URLs, which is correct when the site itself is the
        /// Cloudflare zone doing the transforming.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Cloudflare <c>format</c> option. <c>auto</c> (the default) serves AVIF or WebP by content
        /// negotiation, which is the one thing a CDN does better than this package. Set to an empty string
        /// or <c>none</c> to omit the option and keep the source format. Overridden per call when
        /// <c>UseWebP</c> is on, which forces <c>webp</c>.
        /// </summary>
        public string Format { get; set; } = "auto";

        /// <summary>
        /// Cloudflare <c>metadata</c> option: <c>none</c> strips EXIF entirely, <c>copyright</c> keeps only
        /// the copyright tag, <c>keep</c> preserves everything.
        /// </summary>
        /// <remarks>
        /// Unset by default, which leaves Cloudflare's own behaviour (keep the copyright tag, drop the rest)
        /// in place — so the option is not emitted at all. Deliberately not defaulted to <c>none</c>: on a
        /// photography site silently stripping the copyright tag from every image is the wrong call to make
        /// on someone's behalf, and the option would only lengthen every URL to say what Cloudflare already
        /// does.
        /// </remarks>
        public string Metadata { get; set; }

        /// <summary>
        /// Append the media item's cache buster to the source URL, as Umbraco's own crop URLs do. Defaults
        /// to true, so switching provider does not quietly lose cache busting.
        /// </summary>
        /// <remarks>
        /// Without it, replacing a media file in place at the same path leaves Cloudflare serving the old
        /// image until its TTL expires or the cache is purged. The cost is that each edit produces a fresh
        /// set of URLs, which a per-transformation-billed CDN charges for — turn it off only if media is
        /// never replaced in place and that cost matters.
        /// </remarks>
        public bool CacheBuster { get; set; } = true;

        /// <summary>
        /// Cloudflare <c>onerror</c> option. <c>redirect</c> (the default) serves the untransformed original
        /// when a transformation fails, so an unsupported source degrades to a working image rather than a
        /// broken one. Empty omits the option.
        /// </summary>
        public string OnError { get; set; } = "redirect";
    }
}
