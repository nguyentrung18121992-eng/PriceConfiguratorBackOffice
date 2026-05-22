using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nobia.CmsToolkit.Authorization;
using Nobia.CmsToolkit.Options;
using Nobia.CmsToolkit.Translation;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Pages;

[Authorize(Policy = Policy.BrandPolicy)]
public class PublishModel(
    IPublishService publishService,
    IPublishedConfigurationQuery publishedQuery,
    ILanguageGetter languageGetter) : PageModel
{
    [FromRoute]
    public string Brand { get; set; } = string.Empty;

    [FromQuery(Name = "language")]
    public string? LanguageCode { get; set; }

    public string ApiLanguage { get; private set; } = string.Empty;

    public string CmsLanguageCode { get; private set; } = string.Empty;

    public LanguageDescriptor? Language { get; private set; }

    public int? LatestPublishedVersion { get; private set; }

    public DateTime? LatestPublishedAt { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusSuccess { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ResolveLocale();
        var latest = await GetLatestPublishedAsync(cancellationToken);
        LatestPublishedVersion = latest?.Version;
        LatestPublishedAt = latest?.PublishedAt;
    }

    public async Task<IActionResult> OnPostPublishNowAsync(CancellationToken cancellationToken)
    {
        ResolveLocale();
        var contentLanguage = LanguageMapper.ToContentLanguage(Brand, LanguageCode ?? CmsLanguageCode);
        var result = await publishService.PublishNowAsync(Brand, contentLanguage, cancellationToken);

        if (result.Success)
        {
            StatusSuccess = true;
            StatusMessage = $"Published version {result.Version} for {Brand} ({ApiLanguage}). The live configurator will use this snapshot.";
        }
        else
        {
            StatusSuccess = false;
            StatusMessage = result.Error ?? "Publish failed.";
        }

        return RedirectToPage(new { brand = Brand, language = CmsLanguageCode });
    }

    public async Task<IActionResult> OnPostScheduleAsync(
        [FromForm] DateTime scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        ResolveLocale();
        var scheduledUtc = scheduledAtUtc.Kind == DateTimeKind.Utc
            ? scheduledAtUtc
            : DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);
        var contentLanguage = LanguageMapper.ToContentLanguage(Brand, LanguageCode ?? CmsLanguageCode);
        var result = await publishService.SchedulePublishAsync(Brand, contentLanguage, scheduledUtc, cancellationToken);

        if (result.Success)
        {
            StatusSuccess = true;
            StatusMessage = $"Scheduled publish at {scheduledUtc:u} for {Brand} ({ApiLanguage}).";
        }
        else
        {
            StatusSuccess = false;
            StatusMessage = result.Error ?? "Could not schedule publish.";
        }

        return RedirectToPage(new { brand = Brand, language = CmsLanguageCode });
    }

    private void ResolveLocale()
    {
        Brand = LanguageMapper.NormalizeBrand(Brand);
        CmsLanguageCode = string.IsNullOrWhiteSpace(LanguageCode)
            ? languageGetter.GetAll(Brand).FirstOrDefault(l => l.Available)?.Code ?? "en"
            : LanguageMapper.ToCmsLanguage(LanguageMapper.ToContentLanguage(Brand, LanguageCode));
        LanguageCode = CmsLanguageCode;
        Language = languageGetter.Get(Brand, CmsLanguageCode);
        var contentLanguage = LanguageMapper.ToContentLanguage(Brand, CmsLanguageCode);
        ApiLanguage = LanguageMapper.ToApiLanguageCode(Brand, contentLanguage);
    }

    private Task<PublishedConfiguration?> GetLatestPublishedAsync(CancellationToken cancellationToken) =>
        publishedQuery.GetLatestAsync(Brand, ApiLanguage, cancellationToken);
}
