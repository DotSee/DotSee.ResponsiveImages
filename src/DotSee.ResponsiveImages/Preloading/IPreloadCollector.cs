using System.Collections.Generic;

namespace DotSee.ResponsiveImages.Preloading
{
    /// <summary>
    /// Collects <c>&lt;link rel="preload"&gt;</c> hints for the images rendered during the current
    /// request, so they can be emitted in <c>&lt;head&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Razor executes a view before its layout, so hints registered while the body renders are all
    /// present by the time the layout writes the head. That ordering is the whole reason this works,
    /// and is why the preload has to be collected rather than written where the image appears — a hint
    /// sitting next to its own image is found no earlier than the image itself and buys nothing.
    /// </remarks>
    public interface IPreloadCollector
    {
        /// <summary>
        /// Registers a preload hint. Identical hints are collapsed, so the same hero rendered twice
        /// does not preload twice.
        /// </summary>
        void Add(string linkHtml);

        /// <summary>Every hint registered so far this request, in registration order.</summary>
        IReadOnlyCollection<string> Links { get; }
    }
}
