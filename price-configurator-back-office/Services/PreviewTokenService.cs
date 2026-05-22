using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Services;

public class PreviewTokenService(
    CmsContext cmsContext,
    IConfigSnapshotBuilder snapshotBuilder) : IPreviewTokenService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(8);

    public async Task<string> CreateTokenAsync(
        string brand,
        ContentLanguage language,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var entity = new PreviewToken
        {
            Id = Guid.NewGuid(),
            Brand = LanguageMapper.NormalizeBrand(brand),
            Language = LanguageMapper.ToApiLanguageCode(brand, language),
            Languages = [LanguageMapper.ToCmsLanguage(language)],
            Name = $"Preview {DateTime.UtcNow:u}",
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime ?? DefaultLifetime),
            Revoked = false,
        };

        cmsContext.Add(entity);
        await cmsContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<bool> ValidateTokenAsync(
        string token,
        string brand,
        string language,
        CancellationToken cancellationToken = default)
    {
        var apiLanguage = language.Trim();
        var match = await cmsContext.Set<PreviewToken>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Token == token
                    && t.Brand == brand
                    && t.Language == apiLanguage
                    && !t.Revoked
                    && t.ExpiresAtUtc != null
                    && t.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        return match is not null;
    }

    public async Task<ConfigV1Payload?> BuildPreviewPayloadAsync(
        string brand,
        ContentLanguage language,
        CancellationToken cancellationToken = default)
    {
        return await snapshotBuilder.BuildFromDraftAsync(brand, language, cancellationToken)
            ?? snapshotBuilder.BuildFromSeedFile(brand, LanguageMapper.ToApiLanguageCode(brand, language));
    }
}
