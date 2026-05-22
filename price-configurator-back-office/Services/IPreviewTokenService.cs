using PriceConfiguratorBackoffice.ViewModels;
using Nobia.CmsToolkit.Options;

namespace PriceConfiguratorBackoffice.Services;

public interface IPreviewTokenService
{
    Task<string> CreateTokenAsync(string brand, ContentLanguage language, TimeSpan? lifetime = null, CancellationToken cancellationToken = default);

    Task<bool> ValidateTokenAsync(string token, string brand, string language, CancellationToken cancellationToken = default);

    Task<ConfigV1Payload?> BuildPreviewPayloadAsync(string brand, ContentLanguage language, CancellationToken cancellationToken = default);
}
