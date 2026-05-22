using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Tests;

public class CardPayloadHelperTests
{
    [Fact]
    public void MergeIntoCardDataJson_ClearsAppliancesWhenEditorSendsEmptyArray()
    {
        var existing = """{"appliances":[{"value":"A","key":0}],"prices":[[1,2]]}""";

        var merged = CardPayloadHelper.MergeIntoCardDataJson(
            existing,
            price: null,
            amount: null,
            type: null,
            units: null,
            appliancesJson: "[]",
            sinksJson: "[]",
            imagesJson: "[]");

        Assert.DoesNotContain("appliances", merged, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prices", merged, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MergeIntoCardDataJson_PreservesPayloadWhenCardDataJsonIsInvalid()
    {
        const string corrupt = "{not valid json";

        var merged = CardPayloadHelper.MergeIntoCardDataJson(
            corrupt,
            price: 10m,
            amount: null,
            type: null,
            units: null,
            appliancesJson: "[]",
            sinksJson: "[]",
            imagesJson: "[]");

        Assert.Equal(corrupt, merged);
    }

    [Fact]
    public void MergeIntoCardDataJson_WritesZeroPrice()
    {
        var merged = CardPayloadHelper.MergeIntoCardDataJson(
            "{}",
            price: 0m,
            amount: null,
            type: null,
            units: null,
            appliancesJson: "[]",
            sinksJson: "[]",
            imagesJson: "[]");

        Assert.Contains("\"price\": 0", merged);
    }

    [Fact]
    public void SyncCardPayload_ClearsPriceWhenFieldCleared()
    {
        var card = new ConfiguratorCard
        {
            CardDataJson = """{"price":99.5,"prices":[[1]]}""",
        };
        card.CardPrice = null;

        card.SyncCardPayload();

        Assert.DoesNotContain("price", card.CardDataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prices", card.CardDataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeKeyValueListJson_StripsBlankLabels()
    {
        var json = """[{"value":"","key":0},{"value":"Beko","key":1}]""";

        var normalized = CardPayloadHelper.NormalizeKeyValueListJson(json);

        Assert.Contains("Beko", normalized);
        Assert.DoesNotContain("\"key\": 0", normalized);
    }

    [Fact]
    public void NormalizeKeyValueListJsonDetailed_ReportsDuplicateKeys()
    {
        var json = """[{"value":"A","key":1},{"value":"B","key":1}]""";

        var result = CardPayloadHelper.NormalizeKeyValueListJsonDetailed(json);

        Assert.Equal([1], result.DuplicateKeys);
    }

    [Fact]
    public void NormalizeImagesJson_StripsEmptySetsAndPaths()
    {
        var json = """[[""], ["a.jpg", ""], []]""";

        var normalized = CardPayloadHelper.NormalizeImagesJson(json);

        Assert.Contains("a.jpg", normalized);
        Assert.DoesNotContain("\"\"", normalized);
    }
}
