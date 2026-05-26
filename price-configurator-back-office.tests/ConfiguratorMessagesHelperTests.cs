using Microsoft.AspNetCore.Hosting;
using Moq;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Tests;

public class ConfiguratorMessagesHelperTests
{
    [Fact]
    public void MergeEntriesToMessagesJson_UsesTemplateKeyOrderAndStoredValues()
    {
        var template = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app.header.title"] = "Default title",
            ["app.country.code"] = "en-GB",
        };

        var entriesJson =
            """[{"key":"app.header.title","value":"Custom title"},{"key":"app.country.code","value":"en-GB"}]""";

        var result = ConfiguratorMessagesHelper.MergeEntriesToMessagesJson(entriesJson, template);
        var parsed = ConfiguratorMessagesHelper.ParseMessagesDictionary(result);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Custom title", parsed["app.header.title"]);
        Assert.Equal("en-GB", parsed["app.country.code"]);
    }

    [Fact]
    public void NormalizeMessagesJson_UsesStoredValuesAndTemplateDefaults()
    {
        var template = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app.header.title"] = "Default title",
            ["app.country.code"] = "en-GB",
        };

        var result = ConfiguratorMessagesHelper.NormalizeMessagesJson(
            """{"app.header.title":"Custom"}""",
            template);
        var parsed = ConfiguratorMessagesHelper.ParseMessagesDictionary(result);

        Assert.Equal("Custom", parsed["app.header.title"]);
        Assert.Equal("en-GB", parsed["app.country.code"]);
    }

    [Fact]
    public void MergeEntriesToMessagesJson_IgnoresKeysNotInTemplate()
    {
        var template = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app.header.title"] = "Default",
        };

        var entriesJson =
            """[{"key":"app.header.title","value":"A"},{"key":"extra.key","value":"B"}]""";

        var parsed = ConfiguratorMessagesHelper.ParseMessagesDictionary(
            ConfiguratorMessagesHelper.MergeEntriesToMessagesJson(entriesJson, template));

        Assert.Single(parsed);
        Assert.Equal("A", parsed["app.header.title"]);
    }

    [Fact]
    public void BuildEntriesJson_PrefersStoredOverTemplateDefault()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(GetContentRoot());
        var provider = new ConfiguratorMessagesTemplateProvider(env.Object);

        var messagesJson = """{"app.header.title":"Stored"}""";
        var entriesJson = ConfiguratorMessagesHelper.BuildEntriesJson(
            "magnet",
            "en-GB",
            messagesJson,
            provider);

        var entries = ConfiguratorMessagesHelper.ParseEntries(entriesJson);
        var header = entries.First(e => e.Key == "app.header.title");

        Assert.Equal("Stored", header.Value);
    }

    private static string GetContentRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "price-configurator-back-office");
            if (Directory.Exists(Path.Combine(candidate, "Data", "Seeds")))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate price-configurator-back-office content root.");
    }
}
