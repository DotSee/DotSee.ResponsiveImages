using System.Collections.Generic;
using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages
{
    public interface IConfigSource
    {
        List<RuleSet> AllRuleSets { get; set; }

        public RuleSet GetRuleByName(string name);
    }
}
