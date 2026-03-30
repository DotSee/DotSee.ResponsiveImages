using System;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Logging;

namespace DotSee.ResponsiveImages
{
    public class ConfigFileJsonRuleProvider(ILogger<ConfigFileJsonRuleProvider> logger, IConfigSource config)
        : IRuleProvider
    {
        private readonly ILogger _logger = logger;

        public RuleSet LoadRule(string setName)
        {
            try
            {
                return config.GetRuleByName(setName);
            }
            
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load Rule Set Error: There was an error loading the rule from configuration. Tried to load rule with name {RuleName}", setName);
                return null;
            }
        }
    }
}