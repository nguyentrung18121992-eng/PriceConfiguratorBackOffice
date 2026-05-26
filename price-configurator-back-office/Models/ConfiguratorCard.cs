using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nobia.CmsToolkit.EditingPage;
using Nobia.CmsToolkit.ListingPage;
using Nobia.CmsToolkit.Property.Image;
using Nobia.CmsToolkit.Translation;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Models;

public class ConfiguratorCard : BaseEntity
{
    private const string PayloadGroup = "Card payload";
    private const string OptionsGroup = "Options";

    [ListingPage(Order = 30)]
    [Display(Name = "Section id")]
    public string SectionId { get; set; } = string.Empty;

    [ListingPage(Order = 40)]
    [Display(Name = "Card key (stable id)")]
    public string CardKey { get; set; } = string.Empty;

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Translated]
    public string? Title { get; set; }

    [Translated]
    [EditingPage(EditingFields.Textarea)]
    public string? Description { get; set; }

    [Display(Name = "Cloudinary image")]
    public CloudinaryImage? Image { get; set; }

    [EditingPage("hidden")]
    public string CardDataJson { get; set; } = "{}";

    [DecimalPrice]
    [Display(Name = "Price", GroupName = OptionsGroup, Order = 10, Description = "Optional. Decimals allowed (use . e.g. 1234.50)")]
    public decimal? CardPrice
    {
        get => _cardPrice ?? CardPayloadHelper.GetDecimal(CardDataJson, "price");
        set => _cardPrice = value;
    }

    [DecimalPrice]
    [Display(Name = "Amount", GroupName = OptionsGroup, Order = 20, Description = "Optional. Storage quantity or similar (decimals allowed)")]
    public decimal? CardAmount
    {
        get => _cardAmount ?? CardPayloadHelper.GetDecimal(CardDataJson, "amount");
        set => _cardAmount = value;
    }

    [Display(Name = "Type", GroupName = PayloadGroup, Order = 30, Description = "Card type key (e.g. small, medium, flat-pack)")]
    public string? CardType
    {
        get => _cardType ?? CardPayloadHelper.GetString(CardDataJson, "type");
        set => _cardType = value;
    }

    [Display(Name = "Units", GroupName = PayloadGroup, Order = 40)]
    public int? CardUnits
    {
        get => _cardUnits ?? CardPayloadHelper.GetInt(CardDataJson, "units");
        set => _cardUnits = value;
    }

    [CardKeyValueList]
    [Display(
        Name = "Appliances",
        GroupName = OptionsGroup,
        Order = 30,
        Description = "Brand options shown in the appliance picker (label + numeric key).")]
    public string? AppliancesJson
    {
        get => _appliancesJson ?? CardPayloadHelper.FormatJsonArray(CardDataJson, "appliances");
        set => _appliancesJson = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [CardKeyValueList]
    [Display(
        Name = "Sinks",
        GroupName = OptionsGroup,
        Order = 40,
        Description = "Bowl options (e.g. 1 Bowl, 1.5 Bowl) with a numeric key per row.")]
    public string? SinksJson
    {
        get => _sinksJson ?? CardPayloadHelper.FormatJsonArray(CardDataJson, "sinks");
        set => _sinksJson = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [CardImagesJson]
    [Display(
        Name = "Images",
        GroupName = OptionsGroup,
        Order = 50,
        Description = "One Cloudinary path per sink option (same order as Sinks above).")]
    public string? ImagesJson
    {
        get => _imagesJson ?? CardPayloadHelper.FormatJsonArray(CardDataJson, "images");
        set => _imagesJson = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private decimal? _cardPrice;
    private decimal? _cardAmount;
    private string? _cardType;
    private int? _cardUnits;
    private string? _appliancesJson;
    private string? _sinksJson;
    private string? _imagesJson;

    public void ApplyFromCardDto(ConfiguratorCardDto dto)
    {
        ClearFieldCache();
        _cardPrice = dto.Price;
        _cardUnits = dto.Units;
        _cardType = dto.Type;

        if (dto.ExtensionData is null)
        {
            return;
        }

        if (dto.ExtensionData.TryGetValue("amount", out var amount))
        {
            _cardAmount = ToDecimal(amount);
        }

        if (dto.ExtensionData.TryGetValue("appliances", out var appliances))
        {
            _appliancesJson = ToJsonString(appliances);
        }

        if (dto.ExtensionData.TryGetValue("sinks", out var sinks))
        {
            _sinksJson = ToJsonString(sinks);
        }

        if (dto.ExtensionData.TryGetValue("images", out var images))
        {
            _imagesJson = ToJsonString(images);
        }
    }

    public void SyncCardPayload()
    {
        var appliancesJson = CardPayloadHelper.NormalizeKeyValueListJson(AppliancesJson);
        var sinksJson = CardPayloadHelper.NormalizeKeyValueListJson(SinksJson);
        var imagesJson = CardPayloadHelper.NormalizeImagesJson(ImagesJson, sinksJson);

        CardDataJson = CardPayloadHelper.MergeIntoCardDataJson(
            CardDataJson,
            _cardPrice,
            _cardAmount,
            _cardType,
            _cardUnits,
            appliancesJson,
            sinksJson,
            imagesJson);

        ClearFieldCache();
    }

    public void ClearFieldCache() =>
        (_cardPrice, _cardAmount, _cardType, _cardUnits, _appliancesJson, _sinksJson, _imagesJson) =
        (null, null, null, null, null, null, null);

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        int i => i,
        long l => l,
        double d => (decimal)d,
        float f => (decimal)f,
        JsonElement { ValueKind: JsonValueKind.Number } el when el.TryGetDecimal(out var d) => d,
        JsonElement { ValueKind: JsonValueKind.String } el
            when decimal.TryParse(el.GetString(), out var parsed) => parsed,
        _ when decimal.TryParse(value.ToString(), out var parsed) => parsed,
        _ => null,
    };

    private static string ToJsonString(object? value) =>
        value is null ? "[]" : JsonSerializer.Serialize(value, CardPayloadJsonOptions);

    private static readonly JsonSerializerOptions CardPayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
