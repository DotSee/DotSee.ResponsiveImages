using DotSee.ResponsiveImages.Models;

namespace DotSee.ResponsiveImages
{
    public interface IRuleProvider
    {
        RuleSet LoadRule(string setName);
    }
}
