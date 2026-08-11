using System.Collections.Generic;

namespace DotSee.ResponsiveImages.Preloading
{
    /// <summary>
    /// Request-scoped implementation of <see cref="IPreloadCollector"/>. Not thread safe by design:
    /// a single request renders its views sequentially.
    /// </summary>
    public class PreloadCollector : IPreloadCollector
    {
        private readonly List<string> _links = new List<string>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        public IReadOnlyCollection<string> Links => _links;

        public void Add(string linkHtml)
        {
            if (string.IsNullOrWhiteSpace(linkHtml)) { return; }
            if (!_seen.Add(linkHtml)) { return; }

            _links.Add(linkHtml);
        }
    }
}
