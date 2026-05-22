using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.EditingPage;
using Nobia.CmsToolkit.ListingPage;
using PriceConfiguratorBackoffice.Helpers;

namespace PriceConfiguratorBackoffice.Models;

public class RangePriceSet : BaseEntity
{
    private const string MinPrice = "0";
    private const string MaxPrice = "79228162514264337593543950335"; // decimal.MaxValue

    private const string StandardGroup = "Standard prices";
    private const string PrebuiltGroup = "Prebuilt prices";
    private const string HandlelessGroup = "Handleless prices";

    [ListingPage(Order = 45)]
    [Display(Name = "Range key (e.g. ascoli)")]
    public string RangeKey { get; set; } = string.Empty;

    [EditingPage("hidden")]
    public string PricesJson { get; set; } = "[]";

    [EditingPage("hidden")]
    public string? PrebuiltPricesJson { get; set; }

    [EditingPage("hidden")]
    public string? HandlelessPricesJson { get; set; }

    [DecimalPrice]
    [Display(Name = "Small", GroupName = StandardGroup, Order = 10, Description = "Decimals allowed (use . e.g. 1234.50)")]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? StandardSmall
    {
        get => GetTier(PricesJson, 0, ref _standardSmall);
        set => _standardSmall = value;
    }

    [DecimalPrice]
    [Display(Name = "Medium", GroupName = StandardGroup, Order = 20)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? StandardMedium
    {
        get => GetTier(PricesJson, 1, ref _standardMedium);
        set => _standardMedium = value;
    }

    [DecimalPrice]
    [Display(Name = "Large", GroupName = StandardGroup, Order = 30)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? StandardLarge
    {
        get => GetTier(PricesJson, 2, ref _standardLarge);
        set => _standardLarge = value;
    }

    [DecimalPrice]
    [Display(Name = "Extra large", GroupName = StandardGroup, Order = 40)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? StandardExtraLarge
    {
        get => GetTier(PricesJson, 3, ref _standardExtraLarge);
        set => _standardExtraLarge = value;
    }

    [DecimalPrice]
    [Display(Name = "Small", GroupName = PrebuiltGroup, Order = 50)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? PrebuiltSmall
    {
        get => GetTier(PrebuiltPricesJson, 0, ref _prebuiltSmall);
        set => _prebuiltSmall = value;
    }

    [DecimalPrice]
    [Display(Name = "Medium", GroupName = PrebuiltGroup, Order = 60)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? PrebuiltMedium
    {
        get => GetTier(PrebuiltPricesJson, 1, ref _prebuiltMedium);
        set => _prebuiltMedium = value;
    }

    [DecimalPrice]
    [Display(Name = "Large", GroupName = PrebuiltGroup, Order = 70)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? PrebuiltLarge
    {
        get => GetTier(PrebuiltPricesJson, 2, ref _prebuiltLarge);
        set => _prebuiltLarge = value;
    }

    [DecimalPrice]
    [Display(Name = "Extra large", GroupName = PrebuiltGroup, Order = 80)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? PrebuiltExtraLarge
    {
        get => GetTier(PrebuiltPricesJson, 3, ref _prebuiltExtraLarge);
        set => _prebuiltExtraLarge = value;
    }

    [DecimalPrice]
    [Display(Name = "Small", GroupName = HandlelessGroup, Order = 90)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? HandlelessSmall
    {
        get => GetTier(HandlelessPricesJson, 0, ref _handlelessSmall);
        set => _handlelessSmall = value;
    }

    [DecimalPrice]
    [Display(Name = "Medium", GroupName = HandlelessGroup, Order = 100)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? HandlelessMedium
    {
        get => GetTier(HandlelessPricesJson, 1, ref _handlelessMedium);
        set => _handlelessMedium = value;
    }

    [DecimalPrice]
    [Display(Name = "Large", GroupName = HandlelessGroup, Order = 110)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? HandlelessLarge
    {
        get => GetTier(HandlelessPricesJson, 2, ref _handlelessLarge);
        set => _handlelessLarge = value;
    }

    [DecimalPrice]
    [Display(Name = "Extra large", GroupName = HandlelessGroup, Order = 120)]
    [Range(typeof(decimal), MinPrice, MaxPrice)]
    public decimal? HandlelessExtraLarge
    {
        get => GetTier(HandlelessPricesJson, 3, ref _handlelessExtraLarge);
        set => _handlelessExtraLarge = value;
    }

    private decimal? _standardSmall;
    private decimal? _standardMedium;
    private decimal? _standardLarge;
    private decimal? _standardExtraLarge;
    private decimal? _prebuiltSmall;
    private decimal? _prebuiltMedium;
    private decimal? _prebuiltLarge;
    private decimal? _prebuiltExtraLarge;
    private decimal? _handlelessSmall;
    private decimal? _handlelessMedium;
    private decimal? _handlelessLarge;
    private decimal? _handlelessExtraLarge;

    public void SyncJsonFromTierFields()
    {
        PricesJson = RangeTierPrices.ToJsonArray(
            _standardSmall ?? RangeTierPrices.GetTier(PricesJson, 0),
            _standardMedium ?? RangeTierPrices.GetTier(PricesJson, 1),
            _standardLarge ?? RangeTierPrices.GetTier(PricesJson, 2),
            _standardExtraLarge ?? RangeTierPrices.GetTier(PricesJson, 3));

        PrebuiltPricesJson = RangeTierPrices.ToJsonArrayOrNull(
            _prebuiltSmall ?? RangeTierPrices.GetTier(PrebuiltPricesJson, 0),
            _prebuiltMedium ?? RangeTierPrices.GetTier(PrebuiltPricesJson, 1),
            _prebuiltLarge ?? RangeTierPrices.GetTier(PrebuiltPricesJson, 2),
            _prebuiltExtraLarge ?? RangeTierPrices.GetTier(PrebuiltPricesJson, 3));

        HandlelessPricesJson = RangeTierPrices.ToJsonArrayOrNull(
            _handlelessSmall ?? RangeTierPrices.GetTier(HandlelessPricesJson, 0),
            _handlelessMedium ?? RangeTierPrices.GetTier(HandlelessPricesJson, 1),
            _handlelessLarge ?? RangeTierPrices.GetTier(HandlelessPricesJson, 2),
            _handlelessExtraLarge ?? RangeTierPrices.GetTier(HandlelessPricesJson, 3));
    }

    public void ClearTierFieldCache() =>
        (_standardSmall, _standardMedium, _standardLarge, _standardExtraLarge,
            _prebuiltSmall, _prebuiltMedium, _prebuiltLarge, _prebuiltExtraLarge,
            _handlelessSmall, _handlelessMedium, _handlelessLarge, _handlelessExtraLarge) =
        (null, null, null, null, null, null, null, null, null, null, null, null);

    private static decimal? GetTier(string? json, int index, ref decimal? field) =>
        field ?? RangeTierPrices.GetTier(json, index);
}
