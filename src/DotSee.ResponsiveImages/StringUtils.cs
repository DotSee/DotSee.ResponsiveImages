using System;
using System.Web;

namespace DotSee.ResponsiveImages
{
    public static class StringUtils
    {
        public static string UpdateQueryString(string queryString, string key, string value)
        {
            var qs = HttpUtility.ParseQueryString(queryString ?? string.Empty);
            qs[key] = value;
            return qs.ToString();
        }

        /// <summary>
        /// Round-trips a caller-supplied query string through the parser so it comes out in canonical,
        /// URL-encoded form. <see cref="UpdateQueryString"/> already has this effect when WebP is on;
        /// applying it unconditionally means the emitted URLs don't change shape with that switch, and
        /// characters that could break out of an HTML attribute are encoded before the value reaches
        /// any URL.
        /// </summary>
        public static string NormalizeQueryString(string queryString)
        {
            if (string.IsNullOrWhiteSpace(queryString)) { return queryString; }

            return HttpUtility.ParseQueryString(queryString.TrimStart('&', '?')).ToString();
        }

        public static string RemoveQueryStringByKey(string url, string key)
        {
            if (!url.StartsWith("http")) { url = "http://localhost" + url; }
            var uri = new Uri(url, uriKind:UriKind.Absolute);

            // this gets all the query string key value pairs as a collection
            var newQueryString = HttpUtility.ParseQueryString(uri.Query);

            // this removes the key if exists
            newQueryString.Remove(key);

            // this gets the page path from root without QueryString
            string pagePathWithoutQueryString = uri.GetLeftPart(UriPartial.Path);

            var retval = newQueryString.Count > 0
                ? String.Format("{0}?{1}", pagePathWithoutQueryString, newQueryString)
                : pagePathWithoutQueryString;

            return (retval.Replace("http://localhost", ""));
        }
    }
}