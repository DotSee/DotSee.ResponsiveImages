using System.Collections.Generic;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smidge;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace DotSee.ResponsiveImages;

public class ResponsiveImagesServicesComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddTransient<SrcSetManager>();
        var globalLazySettings = new GlobalLazyLoadSettings();
        builder.Config.Bind("lazyload", globalLazySettings);
        builder.Services.AddSingleton<IGlobalLazyLoadSettings>(globalLazySettings);
        builder.Services.AddTransient<BackgroundImageModelManager>();
        
        // read Responsive Images from appSettings
        builder.Services.Configure<List<RuleSet>>(
            builder.Config.GetSection("DotSee:ResponsiveImages")
        );
        builder.Services.AddSingleton<IConfigSource, ConfigSource>();
        builder.Services.AddSingleton<IRuleProvider, ConfigFileJsonRuleProvider>();
        builder.Services.AddTransient<CssRenderer>();
        builder.Services.AddTransient<ImageUrlService>();
        builder.Services.AddTransient<PictureElementRenderer>();
        builder.Services.AddSmidge();
    }
}