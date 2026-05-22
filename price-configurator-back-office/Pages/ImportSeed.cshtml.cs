using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nobia.CmsToolkit.Authorization;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Pages;

[Authorize(Policy = Policy.BrandPolicy)]
public class ImportSeedModel(ISeedImportService seedImportService) : PageModel
{
    private static readonly Dictionary<string, string> BrandApiLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        [Constants.Brands.Magnet] = Constants.Languages.Magnet,
        [Constants.Brands.Marbodal] = Constants.Languages.Marbodal,
        [Constants.Brands.Invita] = Constants.Languages.Invita,
        [Constants.Brands.Sigdal] = Constants.Languages.Sigdal,
        [Constants.Brands.Norema] = Constants.Languages.Norema,
        [Constants.Brands.Novart] = Constants.Languages.Novart,
    };

    [FromRoute]
    public string Brand { get; set; } = string.Empty;

    [FromQuery(Name = "language")]
    public string? LanguageCode { get; set; }

    public string ApiLanguage { get; private set; } = string.Empty;

    public string CmsLanguageCode { get; private set; } = string.Empty;

    public string SeedFileName { get; private set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusSuccess { get; set; }

    public void OnGet()
    {
        ResolveLocale();
    }

    public async Task<IActionResult> OnPostImportAsync(
        [FromForm] bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ResolveLocale();
        var result = await seedImportService.ImportFromSeedFileAsync(Brand, ApiLanguage, replaceExisting, cancellationToken);

        if (result.Success)
        {
            StatusSuccess = true;
            StatusMessage =
                $"Imported {result.SectionsImported} sections, {result.CardsImported} cards, "
                + $"{result.RangePriceSetsImported} range price sets, {result.MessagesImported} messages. "
                + $"Removed {result.SectionsRemoved} sections, {result.CardsRemoved} cards. "
                + "Open Edit → Configurator sections/cards to review, then use Publish.";
        }
        else
        {
            StatusSuccess = false;
            StatusMessage = result.Error ?? "Import failed.";
        }

        return RedirectToPage(new { brand = Brand, language = CmsLanguageCode });
    }

    private void ResolveLocale()
    {
        Brand = LanguageMapper.NormalizeBrand(Brand);
        ApiLanguage = BrandApiLanguages.TryGetValue(Brand, out var api)
            ? api
            : LanguageCode?.Trim() ?? "en-GB";
        CmsLanguageCode = LanguageMapper.ToCmsLanguage(LanguageMapper.ToContentLanguage(Brand, ApiLanguage));
        LanguageCode = CmsLanguageCode;
        SeedFileName = $"{Brand}-{ApiLanguage}.payload.json";
    }
}
