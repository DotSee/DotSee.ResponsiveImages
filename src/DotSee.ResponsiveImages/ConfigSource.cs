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
            // static string.Equals, so one rule set with a null Name cannot make every lookup throw
            // while scanning past it.
            return AllRuleSets.FirstOrDefault(r => string.Equals(r.Name, name, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
