using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Options;

namespace DotSee.ResponsiveImages
{
    public class ConfigSource(IOptions<List<RuleSet>> options) : IConfigSource
    {
        public List<RuleSet> AllRuleSets { get; set; } = options.Value;
        
        public RuleSet GetRuleByName(string name)
        {
            return AllRuleSets.FirstOrDefault(r => r.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
