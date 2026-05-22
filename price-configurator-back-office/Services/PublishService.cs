using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Entity;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Services;

public class PublishService(
    IConfigSnapshotBuilder snapshotBuilder,
    IPublishedConfigurationQuery publishedQuery,
    CmsContext cmsContext,
    ILogger<PublishService> logger) : IPublishService
{
    public async Task<PublishResult> PublishNowAsync(
        string brand,
        ContentLanguage language,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiLanguage = LanguageMapper.ToApiLanguageCode(brand, language);
            var payload = await snapshotBuilder.BuildFromDraftAsync(brand, language, cancellationToken)
                ?? snapshotBuilder.BuildFromSeedFile(brand, apiLanguage);

            if (payload is null)
            {
                return new PublishResult(false, 0, "No draft content and no seed file found.");
            }

            var validationError = ValidatePayload(payload);
            if (validationError is not null)
            {
                return new PublishResult(false, 0, validationError);
            }

            var version = await publishedQuery.GetNextVersionAsync(brand, language, cancellationToken);
            var languageCode = LanguageMapper.ToApiLanguageCode(brand, language);

            var published = new PublishedConfiguration
            {
                Id = Guid.NewGuid(),
                Brand = LanguageMapper.NormalizeBrand(brand),
                Language = languageCode,
                Languages = LanguageMapper.PublishLanguageTags(brand, language),
                Name = $"Published v{version}",
                Version = version,
                PublishedAt = DateTime.UtcNow,
                PayloadJson = PayloadJsonParser.Serialize(payload),
            };

            cmsContext.Add(published);
            await cmsContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Published {Brand} {Language} version {Version}", brand, language, version);
            return new PublishResult(true, version, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Publish failed for {Brand} {Language}", brand, language);
            return new PublishResult(false, 0, ex.Message);
        }
    }

    public async Task<ScheduleResult> SchedulePublishAsync(
        string brand,
        ContentLanguage language,
        DateTime scheduledPublishAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (scheduledPublishAtUtc <= DateTime.UtcNow)
        {
            return new ScheduleResult(false, null, "Scheduled time must be in the future.");
        }

        var scheduled = new ScheduledPublish
        {
            Id = Guid.NewGuid(),
            Brand = LanguageMapper.NormalizeBrand(brand),
            Language = LanguageMapper.ToApiLanguageCode(brand, language),
            Languages = LanguageMapper.PublishLanguageTags(brand, language),
            Name = $"Scheduled {scheduledPublishAtUtc:u}",
            ScheduledPublishAt = scheduledPublishAtUtc,
            Completed = false,
        };

        cmsContext.Add(scheduled);
        await cmsContext.SaveChangesAsync(cancellationToken);

        return new ScheduleResult(true, scheduled.Id, null);
    }

    private static string? ValidatePayload(ConfigV1Payload payload)
    {
        if (payload.Sections.Count == 0)
        {
            return "At least one section is required.";
        }

        foreach (var section in payload.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Id))
            {
                return "Section id is required.";
            }
        }

        return null;
    }
}
