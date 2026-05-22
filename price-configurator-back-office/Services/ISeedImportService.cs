using Nobia.CmsToolkit.Options;

namespace PriceConfiguratorBackoffice.Services;

public interface ISeedImportService
{
    Task<SeedImportResult> ImportFromSeedFileAsync(
        string brand,
        string language,
        bool replaceExisting = true,
        CancellationToken cancellationToken = default);
}

public sealed record SeedImportResult(
    bool Success,
    string Brand,
    string Language,
    int SectionsImported,
    int CardsImported,
    int MessagesImported,
    bool SettingsImported,
    int SectionsRemoved,
    int CardsRemoved,
    int RangePriceSetsImported,
    int RangePriceSetsRemoved,
    string? Error);
