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
    public void NormalizeImagesJson_FlattensLegacyNestedArray()
    {
        var json = """
            [
              [
                "Magnet/Sinks/1.0B/Square.jpg",
                "Magnet/Sinks/1.5B/Square_1.5.jpg"
              ]
            ]
            """;

        var normalized = CardPayloadHelper.NormalizeImagesJson(json);

        Assert.Contains("1.0B/Square.jpg", normalized);
        Assert.Contains("1.5B/Square_1.5.jpg", normalized);
        Assert.DoesNotContain("[[", normalized);
    }

    [Fact]
    public void NormalizeImagesJson_TrimsToSinkCountAndDropsTrailingEmpty()
    {
        var images = """["path-1-bowl", "path-1.5-bowl", "orphan", ""]""";
        var sinks = """
            [
              {"value":"1.5 Bowl","key":1},
              {"value":"1 Bowl","key":0}
            ]
            """;

        var normalized = CardPayloadHelper.NormalizeImagesJson(images, sinks);

        Assert.Contains("path-1-bowl", normalized);
        Assert.Contains("path-1.5-bowl", normalized);
        Assert.DoesNotContain("orphan", normalized);
    }

    [Fact]
    public void ParseImagesToFlatList_ReadsFlatStrings()
    {
        var paths = CardPayloadHelper.ParseImagesToFlatList("""["a.jpg", "b.jpg"]""");

        Assert.Equal(["a.jpg", "b.jpg"], paths);
    }
}
