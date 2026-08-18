using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.UrlProviders;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// Both settings live under DotSee:ResponsiveImages. The original layout — a bare rule set array with
/// lazy loading at the root — is still honoured, since a published package must not invalidate the
/// configuration of sites already using it.
/// </summary>
public class ConfigurationLayoutTests
{
    private static IConfiguration Config(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static readonly Dictionary<string, string?> CurrentLayout = new()
    {
        ["DotSee:ResponsiveImages:LazyLoad:EnablelazyLoad"] = "true",
        ["DotSee:ResponsiveImages:LazyLoad:PreviewType"] = "Blur",
        ["DotSee:ResponsiveImages:LazyLoad:LowResImagePath"] = "/img/new.jpg",
        ["DotSee:ResponsiveImages:UseWebP"] = "true",
        ["DotSee:ResponsiveImages:RuleSets:0:Name"] = "fromNewLayout",
        ["DotSee:ResponsiveImages:RuleSets:0:ImageQuality"] = "70"
    };

    private static readonly Dictionary<string, string?> LegacyLayout = new()
    {
        ["lazyload:EnablelazyLoad"] = "true",
        ["lazyload:PreviewType"] = "LowResImage",
        ["lazyload:LowResImagePath"] = "/img/legacy.jpg",
        ["useWebP"] = "true",
        ["DotSee:ResponsiveImages:0:Name"] = "fromLegacyLayout",
        ["DotSee:ResponsiveImages:0:ImageQuality"] = "70"
    };

    // ---- rule sets ----

    [Fact]
    public void RuleSets_AreReadFromTheNestedRuleSetsArray()
    {
        var section = ResponsiveImagesConfiguration.GetRuleSetsSection(Config(CurrentLayout));

        var ruleSets = new List<RuleSet>();
        section.Bind(ruleSets);

        Assert.Equal("fromNewLayout", Assert.Single(ruleSets).Name);
    }

    [Fact]
    public void RuleSets_StillReadFromABareArrayAtTheOldLocation()
    {
        var section = ResponsiveImagesConfiguration.GetRuleSetsSection(Config(LegacyLayout));

        var ruleSets = new List<RuleSet>();
        section.Bind(ruleSets);

        Assert.Equal("fromLegacyLayout", Assert.Single(ruleSets).Name);
    }

    // ---- lazy load ----

    [Fact]
    public void LazyLoad_IsReadFromTheNestedSection()
    {
        var settings = new GlobalLazyLoadSettings();
        ResponsiveImagesConfiguration.GetLazyLoadSection(Config(CurrentLayout)).Bind(settings);

        Assert.True(settings.EnablelazyLoad);
        Assert.Equal(PreviewType.Blur, settings.PreviewType);
        Assert.Equal("/img/new.jpg", settings.LowResImagePath);
    }

    [Fact]
    public void LazyLoad_StillReadFromTheRootSection()
    {
        var settings = new GlobalLazyLoadSettings();
        ResponsiveImagesConfiguration.GetLazyLoadSection(Config(LegacyLayout)).Bind(settings);

        Assert.True(settings.EnablelazyLoad);
        Assert.Equal(PreviewType.LowResImage, settings.PreviewType);
        Assert.Equal("/img/legacy.jpg", settings.LowResImagePath);
    }

    [Fact]
    public void LazyLoad_NestedSectionWinsWhenBothArePresent()
    {
        var both = CurrentLayout.Concat(LegacyLayout).ToDictionary(x => x.Key, x => x.Value);

        var settings = new GlobalLazyLoadSettings();
        ResponsiveImagesConfiguration.GetLazyLoadSection(Config(both)).Bind(settings);

        Assert.Equal(PreviewType.Blur, settings.PreviewType);
        Assert.Equal("/img/new.jpg", settings.LowResImagePath);
    }

    // ---- WebP ----

    [Fact]
    public void UseWebP_IsReadFromTheNestedKey()
    {
        Assert.True(ResponsiveImagesConfiguration.GetUseWebP(Config(CurrentLayout)));
    }

    [Fact]
    public void UseWebP_StillReadFromTheRootKey()
    {
        Assert.True(ResponsiveImagesConfiguration.GetUseWebP(Config(LegacyLayout)));
    }

    [Fact]
    public void UseWebP_NestedKeyWinsWhenBothArePresent()
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["useWebP"] = "true",
            ["DotSee:ResponsiveImages:UseWebP"] = "false"
        });

        Assert.False(ResponsiveImagesConfiguration.GetUseWebP(config));
    }

    [Fact]
    public void UseWebP_DefaultsToOff()
    {
        Assert.False(ResponsiveImagesConfiguration.GetUseWebP(Config(new Dictionary<string, string?>())));
    }

    // ---- tag helper warnings ----

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void SuppressTagHelperWarnings_IsReadFromConfiguration(string value, bool expected)
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["DotSee:ResponsiveImages:SuppressTagHelperWarnings"] = value
        });

        Assert.Equal(expected, ResponsiveImagesConfiguration.GetSuppressTagHelperWarnings(config));
    }

    [Fact]
    public void SuppressTagHelperWarnings_DefaultsToOff_SoMisconfigurationIsVisible()
    {
        Assert.False(ResponsiveImagesConfiguration.GetSuppressTagHelperWarnings(Config(new Dictionary<string, string?>())));
    }

    [Fact]
    public void SuppressTagHelperWarnings_TreatsMissingConfigurationAsOff()
    {
        //Tag helpers accept a null IConfiguration so they stay constructible outside DI.
        Assert.False(ResponsiveImagesConfiguration.GetSuppressTagHelperWarnings(null));
    }

    // ---- URL provider ----

    [Fact]
    public void UrlProvider_IsReadFromTheNestedKey()
    {
        var config = Config(new Dictionary<string, string?>
        {
            ["DotSee:ResponsiveImages:UrlProvider"] = "Cloudflare"
        });

        Assert.Equal("Cloudflare", ResponsiveImagesConfiguration.GetUrlProvider(config));
    }

    [Fact]
    public void UrlProvider_DefaultsToUmbraco()
    {
        Assert.Equal("Umbraco", ResponsiveImagesConfiguration.GetUrlProvider(Config(new Dictionary<string, string?>())));
        Assert.Equal("Umbraco", ResponsiveImagesConfiguration.GetUrlProvider(null));
    }

    [Fact]
    public void UrlProvider_DefaultsToUmbracoOnTheLegacyBareArrayLayout()
    {
        // The old layout makes DotSee:ResponsiveImages the array itself, so it cannot carry a named key.
        // Such a site keeps working on the default provider; selecting one means nesting the rule sets first.
        Assert.Equal("Umbraco", ResponsiveImagesConfiguration.GetUrlProvider(Config(LegacyLayout)));
    }

    [Fact]
    public void CloudflareSettings_BindFromTheNestedSectionAndDefaultOtherwise()
    {
        var configured = new CloudflareImageSettings();
        ResponsiveImagesConfiguration.GetCloudflareSection(Config(new Dictionary<string, string?>
        {
            ["DotSee:ResponsiveImages:Cloudflare:Prefix"] = "/images/resize",
            ["DotSee:ResponsiveImages:Cloudflare:Format"] = "webp"
        })).Bind(configured);

        Assert.Equal("/images/resize", configured.Prefix);
        Assert.Equal("webp", configured.Format);

        var defaults = new CloudflareImageSettings();
        ResponsiveImagesConfiguration.GetCloudflareSection(Config(new Dictionary<string, string?>())).Bind(defaults);

        Assert.Equal("/cdn-cgi/image", defaults.Prefix);
        Assert.Equal("auto", defaults.Format);
        Assert.Null(defaults.BaseUrl);
        Assert.Null(defaults.Metadata);
        Assert.Equal("redirect", defaults.OnError);
    }

    // ---- layout detection ----

    [Theory]
    [InlineData("DotSee:ResponsiveImages:RuleSets:0:Name", true)]
    [InlineData("DotSee:ResponsiveImages:LazyLoad:PreviewType", true)]
    [InlineData("DotSee:ResponsiveImages:0:Name", false)]
    public void LayoutIsDetectedFromTheChildKeys(string key, bool expectedObjectLayout)
    {
        var config = Config(new Dictionary<string, string?> { [key] = "x" });

        Assert.Equal(expectedObjectLayout, ResponsiveImagesConfiguration.UsesObjectLayout(config));
    }

    [Fact]
    public void MissingConfigurationBindsToNothingRatherThanThrowing()
    {
        var config = Config(new Dictionary<string, string?>());

        var ruleSets = new List<RuleSet>();
        ResponsiveImagesConfiguration.GetRuleSetsSection(config).Bind(ruleSets);

        var settings = new GlobalLazyLoadSettings();
        ResponsiveImagesConfiguration.GetLazyLoadSection(config).Bind(settings);

        Assert.Empty(ruleSets);
        Assert.Null(settings.EnablelazyLoad);
    }
}
