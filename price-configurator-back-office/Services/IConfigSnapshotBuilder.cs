using PriceConfiguratorBackoffice.ViewModels;
using Nobia.CmsToolkit.Options;

namespace PriceConfiguratorBackoffice.Services;

public interface IConfigSnapshotBuilder
{
    Task<ConfigV1Payload?> BuildFromDraftAsync(string brand, ContentLanguage language, CancellationToken cancellationToken = default);

    ConfigV1Payload? BuildFromSeedFile(string brand, string language);
}
