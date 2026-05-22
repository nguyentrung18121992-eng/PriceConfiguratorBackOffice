using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Services;

public interface IPublishedConfigurationQuery
{
    Task<PublishedConfiguration?> GetLatestAsync(
        string brand,
        string apiLanguage,
        CancellationToken cancellationToken = default);

    Task<PublishedConfiguration?> GetLatestAsync(
        string brand,
        ContentLanguage contentLanguage,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionAsync(
        string brand,
        ContentLanguage contentLanguage,
        CancellationToken cancellationToken = default);
}
