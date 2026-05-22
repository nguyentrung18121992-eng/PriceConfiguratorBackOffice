using Nobia.CmsToolkit.Options;

namespace PriceConfiguratorBackoffice.Services;

public interface IPublishService
{
    Task<PublishResult> PublishNowAsync(string brand, ContentLanguage language, CancellationToken cancellationToken = default);

    Task<ScheduleResult> SchedulePublishAsync(
        string brand,
        ContentLanguage language,
        DateTime scheduledPublishAtUtc,
        CancellationToken cancellationToken = default);
}

public record PublishResult(bool Success, int Version, string? Error);

public record ScheduleResult(bool Success, Guid? ScheduledPublishId, string? Error);
