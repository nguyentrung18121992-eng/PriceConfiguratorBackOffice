using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Services;

/// <summary>
/// Reads latest published config via Cosmos (partition = Brand).
/// Filters with equality only (translatable); picks latest version in memory to avoid Cosmos composite indexes on ORDER BY.
/// Publish writes <see cref="PublishedConfiguration.Language"/> as the API locale (e.g. en-GB).
/// </summary>
public class PublishedConfigurationQuery(CmsContext context) : IPublishedConfigurationQuery
{
    public async Task<PublishedConfiguration?> GetLatestAsync(
        string brand,
        string apiLanguage,
        CancellationToken cancellationToken = default)
    {
        var brandKey = LanguageMapper.NormalizeBrand(brand);
        var cmsLanguage = LanguageMapper.ToCmsLanguage(
            LanguageMapper.ToContentLanguage(brandKey, apiLanguage.Trim()));

        var byApiLocale = await GetLatestByLanguageAsync(brandKey, apiLanguage, cancellationToken);

        if (string.Equals(apiLanguage, cmsLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return byApiLocale;
        }

        var byCmsLocale = await GetLatestByLanguageAsync(brandKey, cmsLanguage, cancellationToken);
        return PickLatest(byApiLocale, byCmsLocale);
    }

    public Task<PublishedConfiguration?> GetLatestAsync(
        string brand,
        ContentLanguage contentLanguage,
        CancellationToken cancellationToken = default)
    {
        var apiLanguage = LanguageMapper.ToApiLanguageCode(brand, contentLanguage);
        return GetLatestAsync(brand, apiLanguage, cancellationToken);
    }

    public async Task<int> GetNextVersionAsync(
        string brand,
        ContentLanguage contentLanguage,
        CancellationToken cancellationToken = default)
    {
        var brandKey = LanguageMapper.NormalizeBrand(brand);
        var apiLanguage = LanguageMapper.ToApiLanguageCode(brand, contentLanguage);
        var cmsLanguage = LanguageMapper.ToCmsLanguage(contentLanguage);

        var maxApi = await MaxVersionByLanguageAsync(brandKey, apiLanguage, cancellationToken);

        if (string.Equals(apiLanguage, cmsLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return (maxApi ?? 0) + 1;
        }

        var maxCms = await MaxVersionByLanguageAsync(brandKey, cmsLanguage, cancellationToken);
        return Math.Max(maxApi ?? 0, maxCms ?? 0) + 1;
    }

    private async Task<PublishedConfiguration?> GetLatestByLanguageAsync(
        string brandKey,
        string language,
        CancellationToken cancellationToken)
    {
        var rows = await context.Set<PublishedConfiguration>()
            .AsNoTracking()
            .Where(p => p.Brand == brandKey && p.Language == language)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(p => p.Version)
            .ThenByDescending(p => p.PublishedAt)
            .FirstOrDefault();
    }

    private Task<int?> MaxVersionByLanguageAsync(
        string brandKey,
        string language,
        CancellationToken cancellationToken) =>
        context.Set<PublishedConfiguration>()
            .AsNoTracking()
            .Where(p => p.Brand == brandKey && p.Language == language)
            .MaxAsync(p => (int?)p.Version, cancellationToken);

    private static PublishedConfiguration? PickLatest(
        PublishedConfiguration? a,
        PublishedConfiguration? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        if (a.Version != b.Version)
        {
            return a.Version > b.Version ? a : b;
        }

        var aAt = a.PublishedAt ?? DateTime.MinValue;
        var bAt = b.PublishedAt ?? DateTime.MinValue;
        return aAt >= bAt ? a : b;
    }
}
