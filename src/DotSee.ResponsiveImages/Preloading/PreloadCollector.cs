using System.Collections.Generic;

namespace DotSee.ResponsiveImages.Preloading
{
    /// <summary>
    /// Request-scoped implementation of <see cref="IPreloadCollector"/>. Stock Razor renders a request's
    /// views sequentially, but view components invoked with Task.WhenAll (or any parallel work sharing
    /// the request scope) call in concurrently — and a corrupted HashSet can spin forever on read, which
    /// is a far worse failure than the microseconds a lock costs here.
    /// </summary>
    public class PreloadCollector : IPreloadCollector
    {
        private readonly object _gate = new object();
        private readonly List<string> _links = new List<string>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        public IReadOnlyCollection<string> Links
        {
            get
            {
                lock (_gate)
                {
                    return _links.ToArray();
                }
            }
        }

        public void Add(string linkHtml)
        {
            if (string.IsNullOrWhiteSpace(linkHtml)) { return; }

            lock (_gate)
            {
                if (!_seen.Add(linkHtml)) { return; }
                _links.Add(linkHtml);
            }
        }
    }
}
