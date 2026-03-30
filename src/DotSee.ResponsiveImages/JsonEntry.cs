using System.Collections.Generic;

namespace DotSee.ResponsiveImages
{
    public class JsonEntry
    {
        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        public class DefaultImage
        {
            public string width { get; set; }
            public string height { get; set; }
            public string blur { get; set; }
        }

        public class Srcsetentry
        {
            public string breakpoint { get; set; }
            public string width { get; set; }
            public string height { get; set; }
        }

        public class Sizeentry
        {
            public string mediacondition { get; set; }
            public string width { get; set; }
            public string type { get; set; }
        }

        public class Sizeentries
        {
            public string defaultwidth { get; set; }
            public List<Sizeentry> sizeentry { get; set; }
        }

        public class Root
        {
            public string name { get; set; }
            public string originalImageMaxWidth { get; set; }
            public string imageQuality { get; set; }
            public DefaultImage defaultImage { get; set; }
            public List<Srcsetentry> srcsetentries { get; set; }
            public Sizeentries sizeentries { get; set; }
            public string use2x { get; set; }
            public string use3x { get; set; }
            public string upscale { get; set; }
            public string cropmode { get; set; }
            public bool? lazyload { get; set; }
            public string originalImageMaxHeight { get; set; }
            public string useBreakPointWidthIfNoWidth { get; set; }
        }


    }
}
