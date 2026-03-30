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