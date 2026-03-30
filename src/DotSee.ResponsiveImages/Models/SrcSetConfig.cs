using System.Collections.Generic;

namespace DotSee.ResponsiveImages
{
    public class SrcSetConfig
    {
        public SrcSetConfig()
        {
            SrcSetEntries = new List<SrcSetEntry>();
            SizeEntries = new List<string>();
        }

        public List<SrcSetEntry> SrcSetEntries { get; set; }
        public List<string> SizeEntries { get; set; }

    }
}
