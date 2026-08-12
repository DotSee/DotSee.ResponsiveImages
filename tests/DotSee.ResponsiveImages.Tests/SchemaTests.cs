using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DotSee.ResponsiveImages.Cdn;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Umbraco.Cms.Core.Models;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// Keeps the hand-authored appsettings JSON schema honest.
/// </summary>
/// <remarks>
/// The schema exists to give editors IntelliSense, so its only real failure mode is silently drifting
/// from the settings classes — a property added in C# and forgotten in the schema produces no error,
/// just a missing suggestion and a spurious "unknown property" warning for whoever uses it. These
/// tests fail the build instead.
/// </remarks>
public class SchemaTests
{
    private static readonly JsonElement Schema = LoadSchema();

    private static JsonElement LoadSchema()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings-schema.DotSee.ResponsiveImages.json");
        return JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement.Clone();
    }

    private static JsonElement Definition(string name) => Schema.GetProperty("definitions").GetProperty(name);

    private static IEnumerable<string> PropertyNamesOf(string definition)
        => Definition(definition).GetProperty("properties").EnumerateObject().Select(x => x.Name);

    /// <summary>
    /// Bindable properties: settable, or supplied through the constructor as RuleSet.Name is.
    /// </summary>
    private static IEnumerable<string> BindablePropertiesOf(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite
                        || type.GetConstructors().Any(c => c.GetParameters()
                            .Any(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase))))
            .Select(p => p.Name);

    [Theory]
    [InlineData(typeof(RuleSet), "RuleSet")]
    [InlineData(typeof(RuleBreakPoint), "RuleBreakPoint")]
    [InlineData(typeof(GlobalLazyLoadSettings), "LazyLoadSettings")]
    [InlineData(typeof(ImageCdnSettings), "ImageCdnSettings")]
    public void EverySettableSettingIsDescribedBySchema(Type settingsType, string definition)
    {
        var documented = PropertyNamesOf(definition).ToHashSet(StringComparer.Ordinal);
        var missing = BindablePropertiesOf(settingsType).Where(x => !documented.Contains(x)).ToList();

        Assert.True(missing.Count == 0,
            $"'{definition}' in appsettings-schema.DotSee.ResponsiveImages.json is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData(typeof(RuleSet), "RuleSet")]
    [InlineData(typeof(RuleBreakPoint), "RuleBreakPoint")]
    [InlineData(typeof(GlobalLazyLoadSettings), "LazyLoadSettings")]
    [InlineData(typeof(ImageCdnSettings), "ImageCdnSettings")]
    public void SchemaDescribesNoSettingThatDoesNotExist(Type settingsType, string definition)
    {
        var bindable = BindablePropertiesOf(settingsType).ToHashSet(StringComparer.Ordinal);
        var invented = PropertyNamesOf(definition).Where(x => !bindable.Contains(x)).ToList();

        Assert.True(invented.Count == 0,
            $"'{definition}' documents settings that no longer exist: {string.Join(", ", invented)}");
    }

    [Theory]
    [InlineData(typeof(ImageCropMode), "RuleSet", "CropMode")]
    [InlineData(typeof(PreviewType), "LazyLoadSettings", "PreviewType")]
    [InlineData(typeof(CdnPurgeMode), "ImageCdnSettings", "Mode")]
    public void EnumValuesMatchTheCode(Type enumType, string definition, string property)
    {
        var documented = Definition(definition).GetProperty("properties").GetProperty(property)
            .GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.Equal(Enum.GetNames(enumType).OrderBy(x => x), documented.OrderBy(x => x));
    }

    [Fact]
    public void EverySettingCarriesADescription()
    {
        // A property without a description still validates but shows an empty tooltip, which is the
        // difference between a schema that helps and one that only nags about typos.
        var undocumented = new List<string>();

        foreach (var definition in Schema.GetProperty("definitions").EnumerateObject())
        {
            if (!definition.Value.TryGetProperty("properties", out var properties)) { continue; }

            foreach (var property in properties.EnumerateObject())
            {
                bool described = property.Value.TryGetProperty("description", out _)
                                 || property.Value.TryGetProperty("$ref", out _);

                if (!described) { undocumented.Add($"{definition.Name}.{property.Name}"); }
            }
        }

        Assert.True(undocumented.Count == 0, "Missing descriptions: " + string.Join(", ", undocumented));
    }

    [Fact]
    public void LegacyConfigurationLocationsAreStillDescribed()
    {
        // Both layouts are honoured at runtime, so a site on the old one must not be shown as invalid.
        var root = Schema.GetProperty("properties");

        Assert.True(root.TryGetProperty("lazyload", out _));
        Assert.True(root.TryGetProperty("useWebP", out _));

        var responsiveImages = Definition("DotSeeDefinition").GetProperty("properties").GetProperty("ResponsiveImages");
        Assert.Equal(2, responsiveImages.GetProperty("oneOf").GetArrayLength());
    }
}
