using System.Linq;
using Microsoft.Extensions.Configuration;

namespace DotSee.ResponsiveImages
{
    /// <summary>
    /// Resolves where the package's settings live in <c>appsettings.json</c>.
    /// </summary>
    /// <remarks>
    /// Everything now sits under <c>DotSee:ResponsiveImages</c>, which is an object with a
    /// <c>RuleSets</c> array and a <c>LazyLoad</c> object:
    ///
    /// <code>
    /// "DotSee": {
    ///   "ResponsiveImages": {
    ///     "LazyLoad": { "EnablelazyLoad": true, "PreviewType": "Blur" },
    ///     "RuleSets": [ { "Name": "default", ... } ]
    ///   }
    /// }
    /// </code>
    ///
    /// The original layout — <c>DotSee:ResponsiveImages</c> as a bare array, with lazy loading under a
    /// root-level <c>lazyload</c> section — still works. A published package cannot silently invalidate
    /// the configuration of every site already using it, so both shapes are read and the new one wins
    /// where both are present.
    /// </remarks>
    public static class ResponsiveImagesConfiguration
    {
        public const string RootSection = "DotSee:ResponsiveImages";
        public const string RuleSetsKey = "RuleSets";
        public const string LazyLoadKey = "LazyLoad";
        public const string UseWebPKey = "UseWebP";
        public const string SuppressTagHelperWarningsKey = "SuppressTagHelperWarnings";

        /// <summary>Where lazy-load settings used to live, at the root of appsettings.json.</summary>
        public const string LegacyLazyLoadSection = "lazyload";

        /// <summary>Where the WebP switch used to live, at the root of appsettings.json.</summary>
        public const string LegacyUseWebPSection = "useWebP";

        /// <summary>
        /// The section holding the rule set array. An array binds with numeric keys ("0", "1", …), so a
        /// named child is what distinguishes the object layout from the original bare array.
        /// </summary>
        public static IConfigurationSection GetRuleSetsSection(IConfiguration configuration)
        {
            var root = configuration.GetSection(RootSection);

            return UsesObjectLayout(root)
                ? root.GetSection(RuleSetsKey)
                : root;
        }

        /// <summary>
        /// The section holding the global lazy-load settings, preferring the new location and falling
        /// back to the root-level <c>lazyload</c> section.
        /// </summary>
        public static IConfigurationSection GetLazyLoadSection(IConfiguration configuration)
        {
            var nested = configuration.GetSection(RootSection).GetSection(LazyLoadKey);

            return nested.GetChildren().Any()
                ? nested
                : configuration.GetSection(LegacyLazyLoadSection);
        }

        /// <summary>
        /// Whether <c>&amp;format=webp</c> should be appended to generated URLs, preferring the nested
        /// setting and falling back to the root-level <c>useWebP</c> switch.
        /// </summary>
        public static bool GetUseWebP(IConfiguration configuration)
        {
            var nested = configuration.GetSection(RootSection).GetSection(UseWebPKey);

            return !string.IsNullOrWhiteSpace(nested.Value) && bool.TryParse(nested.Value, out bool enabled)
                ? enabled
                : configuration.GetValue<bool>(LegacyUseWebPSection);
        }

        /// <summary>
        /// Whether the tag helpers should stay silent instead of rendering their red warning messages
        /// into the page. Defaults to <c>false</c>, so misconfiguration is loud in development.
        /// </summary>
        /// <remarks>
        /// A single switch rather than a per-element attribute, so the same views can go from noisy in
        /// development to silent in production with one environment-specific setting — and so nobody has
        /// to remember to suppress a warning on the one new image added just before a release.
        /// </remarks>
        public static bool GetSuppressTagHelperWarnings(IConfiguration configuration)
        {
            if (configuration == null) { return false; }

            var value = configuration.GetSection(RootSection).GetSection(SuppressTagHelperWarningsKey).Value;

            return !string.IsNullOrWhiteSpace(value) && bool.TryParse(value, out bool suppress) && suppress;
        }

        /// <summary>
        /// True when the settings use the current object layout rather than the original bare array.
        /// </summary>
        public static bool UsesObjectLayout(IConfiguration configuration)
        {
            return UsesObjectLayout(configuration.GetSection(RootSection));
        }

        private static bool UsesObjectLayout(IConfigurationSection root)
        {
            return root.GetChildren().Any(x =>
                x.Key.InvariantEquals(RuleSetsKey) || x.Key.InvariantEquals(LazyLoadKey));
        }
    }
}
