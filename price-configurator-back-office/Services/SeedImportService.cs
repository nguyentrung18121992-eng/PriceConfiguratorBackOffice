using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Entity;
using Nobia.CmsToolkit.Options;
using Nobia.CmsToolkit.Property.Image;
using Nobia.CmsToolkit.Translation;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Infrastructure;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Services;

public partial class SeedImportService(
    IConfigSnapshotBuilder snapshotBuilder,
    IEntityLoader loader,
    CmsContext cmsContext,
    ITranslationUpdater translationUpdater,
    ILogger<SeedImportService> logger) : ISeedImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly HashSet<string> CardScalarKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "description",
        "image",
        "imageSrc",
        "price",
        "amount",
        "type",
        "units",
        "appliances",
        "sinks",
        "images",
        "prices",
        "prebuiltPrices",
        "handlelessPrices",
    };

    public async Task<SeedImportResult> ImportFromSeedFileAsync(
        string brand,
        string language,
        bool replaceExisting = true,
        CancellationToken cancellationToken = default)
    {
        var apiLanguage = language.Trim();
        brand = LanguageMapper.NormalizeBrand(brand);
        var contentLanguage = LanguageMapper.ToContentLanguage(brand, apiLanguage);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await ImportCoreAsync(
                    brand,
                    apiLanguage,
                    contentLanguage,
                    replaceExisting,
                    cancellationToken);
            }
            catch (Exception ex) when (IsPartitionKeyMismatch(ex) && attempt == 0)
            {
                logger.LogWarning(
                    ex,
                    "Partition key mismatch — recreating Cosmos database {Database} and retrying import",
                    "PriceConfigurator");
                cmsContext.ChangeTracker.Clear();
                await CosmosDatabaseInitializer.RecreateAsync(cmsContext, logger, cancellationToken);
            }
        }

        return Fail(
            brand,
            apiLanguage,
            "Cosmos partition key mismatch persisted after recreating the database. "
            + "Stop the API, delete database PriceConfigurator in Cosmos Data Explorer, restart, and import again.");
    }

    private async Task<SeedImportResult> ImportCoreAsync(
        string brand,
        string apiLanguage,
        ContentLanguage contentLanguage,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        try
        {
            await cmsContext.Database.EnsureCreatedAsync(cancellationToken);

            var payload = snapshotBuilder.BuildFromSeedFile(brand, apiLanguage);
            if (payload is null || payload.Sections.Count == 0)
            {
                return Fail(brand, apiLanguage, "Seed file not found or has no sections. Run scripts/sync-seeds-from-frontend.ps1 first.");
            }

            var existingSections = (await loader.Get<ConfiguratorSection>(brand, contentLanguage)).ToList();
            var existingCards = (await loader.Get<ConfiguratorCard>(brand, contentLanguage)).ToList();
            var sectionByKey = existingSections.ToDictionary(s => s.SectionId, StringComparer.OrdinalIgnoreCase);
            var cardByKey = existingCards.ToDictionary(
                c => (c.SectionId, c.CardKey),
                c => c,
                new SectionCardKeyComparer());

            var (importedSectionIds, importedCardKeys, sectionOrderIds) = BuildImportKeys(payload);
            var sectionOrder = 0;
            var cardsImported = 0;
            var sectionsRemoved = 0;
            var cardsRemoved = 0;

            if (replaceExisting)
            {
                foreach (var section in existingSections.Where(s => !importedSectionIds.Contains(s.SectionId)))
                {
                    cmsContext.Set<ConfiguratorSection>().Remove(section);
                    sectionsRemoved++;
                }

                foreach (var card in existingCards.Where(c => !importedCardKeys.Contains((c.SectionId, c.CardKey))))
                {
                    cmsContext.Set<ConfiguratorCard>().Remove(card);
                    cardsRemoved++;
                }

                if (sectionsRemoved > 0 || cardsRemoved > 0)
                {
                    await SaveAndClearAsync(cancellationToken);
                    existingSections = (await loader.Get<ConfiguratorSection>(brand, contentLanguage)).ToList();
                    existingCards = (await loader.Get<ConfiguratorCard>(brand, contentLanguage)).ToList();
                    sectionByKey = existingSections.ToDictionary(s => s.SectionId, StringComparer.OrdinalIgnoreCase);
                    cardByKey = existingCards.ToDictionary(
                        c => (c.SectionId, c.CardKey),
                        c => c,
                        new SectionCardKeyComparer());
                }
            }

            foreach (var sectionDto in payload.Sections)
            {
                if (string.IsNullOrWhiteSpace(sectionDto.Id))
                {
                    continue;
                }

                var (tooltipTitle, tooltipDescription) = ParseTooltip(sectionDto.Tooltip);

                if (sectionByKey.TryGetValue(sectionDto.Id, out var sectionEntity))
                {
                    sectionEntity.Type = sectionDto.Type ?? string.Empty;
                    sectionEntity.SortOrder = sectionOrder;
                    sectionEntity.Name = sectionDto.Title ?? sectionDto.Id;
                    sectionEntity.Title = sectionDto.Title;
                    sectionEntity.ShortTitle = sectionDto.ShortTitle;
                    sectionEntity.Description = sectionDto.Description;
                    sectionEntity.TooltipTitle = tooltipTitle;
                    sectionEntity.TooltipDescription = tooltipDescription;
                    LanguageMapper.FinalizeForCosmos(sectionEntity, brand, contentLanguage, translationUpdater);
                    cmsContext.Update(sectionEntity);
                }
                else
                {
                    sectionEntity = new ConfiguratorSection
                    {
                        Id = Guid.NewGuid(),
                        SectionId = sectionDto.Id,
                        Type = sectionDto.Type ?? string.Empty,
                        SortOrder = sectionOrder,
                        Name = sectionDto.Title ?? sectionDto.Id,
                        Title = sectionDto.Title,
                        ShortTitle = sectionDto.ShortTitle,
                        Description = sectionDto.Description,
                        TooltipTitle = tooltipTitle,
                        TooltipDescription = tooltipDescription,
                    };
                    LanguageMapper.FinalizeForCosmos(sectionEntity, brand, contentLanguage, translationUpdater);
                    cmsContext.Add(sectionEntity);
                    sectionByKey[sectionDto.Id] = sectionEntity;
                }

                var cardIndex = 0;
                foreach (var cardDto in sectionDto.Cards ?? [])
                {
                    var cardKey = ResolveCardKey(cardDto, cardIndex++);
                    var key = (sectionDto.Id, cardKey);

                    var imageId = cardDto.Image ?? cardDto.ImageSrc;
                    var cardDataJson = SerializeCardPayload(cardDto);

                    if (cardByKey.TryGetValue(key, out var cardEntity))
                    {
                        cardEntity.SortOrder = cardIndex - 1;
                        cardEntity.Name = cardDto.Title ?? cardKey;
                        cardEntity.Title = cardDto.Title;
                        cardEntity.Description = cardDto.Description;
                        cardEntity.Image = ToCloudinaryImage(imageId);
                        cardEntity.CardDataJson = cardDataJson;
                        cardEntity.ApplyFromCardDto(cardDto);
                        cardEntity.SyncCardPayload();
                        LanguageMapper.FinalizeForCosmos(cardEntity, brand, contentLanguage, translationUpdater);
                        cmsContext.Update(cardEntity);
                    }
                    else
                    {
                        cardEntity = new ConfiguratorCard
                        {
                            Id = Guid.NewGuid(),
                            SectionId = sectionDto.Id,
                            CardKey = cardKey,
                            SortOrder = cardIndex - 1,
                            Name = cardDto.Title ?? cardKey,
                            Title = cardDto.Title,
                            Description = cardDto.Description,
                            Image = ToCloudinaryImage(imageId),
                            CardDataJson = cardDataJson,
                        };
                        cardEntity.ApplyFromCardDto(cardDto);
                        cardEntity.SyncCardPayload();
                        LanguageMapper.FinalizeForCosmos(cardEntity, brand, contentLanguage, translationUpdater);
                        cmsContext.Add(cardEntity);
                        cardByKey[key] = cardEntity;
                    }

                    cardsImported++;
                }

                sectionOrder++;
                await SaveAndClearAsync(cancellationToken);
            }

            var (rangePriceSetsImported, rangePriceSetsRemoved) = await UpsertRangePriceSetsAsync(
                brand,
                contentLanguage,
                payload,
                replaceExisting,
                cancellationToken);

            var messagesImported = await UpsertMessagesAsync(brand, apiLanguage, contentLanguage, payload.Messages, cancellationToken);

            var settingsImported = await UpsertSettingsAsync(
                brand,
                apiLanguage,
                contentLanguage,
                sectionOrderIds,
                cancellationToken);

            logger.LogInformation(
                "Imported seed for {Brand} {Language}: {Sections} sections, {Cards} cards, {RangePriceSets} range price sets, {Messages} messages",
                brand,
                apiLanguage,
                importedSectionIds.Count,
                cardsImported,
                rangePriceSetsImported,
                messagesImported);

            return new SeedImportResult(
                true,
                brand,
                apiLanguage,
                importedSectionIds.Count,
                cardsImported,
                messagesImported,
                settingsImported,
                sectionsRemoved,
                cardsRemoved,
                rangePriceSetsImported,
                rangePriceSetsRemoved,
                null);
        }
        catch (Exception ex) when (IsCosmosNotFound(ex))
        {
            logger.LogError(ex, "Seed import failed for {Brand} {Language} (Cosmos database missing)", brand, apiLanguage);
            return Fail(
                brand,
                apiLanguage,
                "Cosmos database or container does not exist. Restart the API (local/dev runs EnsureCreated on startup), "
                + "or open the CMS once, then retry import. If you wiped the emulator, wait until it is ready and restart.");
        }
        catch (Exception ex) when (IsPartitionKeyMismatch(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed import failed for {Brand} {Language}", brand, apiLanguage);
            return Fail(brand, apiLanguage, ex.Message);
        }
    }

    private async Task<(int Imported, int Removed)> UpsertRangePriceSetsAsync(
        string brand,
        ContentLanguage contentLanguage,
        ConfigV1Payload payload,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        var rangeSection = payload.Sections.FirstOrDefault(s =>
            string.Equals(s.Type, "range", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Id, "range", StringComparison.OrdinalIgnoreCase));

        if (rangeSection?.Cards is null || rangeSection.Cards.Count == 0)
        {
            return (0, 0);
        }

        var apiLanguage = LanguageMapper.ToApiLanguageCode(brand, contentLanguage);
        var existing = await ListRangePriceSetsForLocaleAsync(brand, apiLanguage);
        var duplicateRemovals = new List<RangePriceSet>();
        var byRangeKey = IndexUniqueByKey(
            existing,
            r => r.RangeKey,
            duplicateRemovals);

        var removed = 0;
        foreach (var duplicate in duplicateRemovals)
        {
            cmsContext.Set<RangePriceSet>().Remove(duplicate);
            removed++;
        }

        var importedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var cardIndex = 0;

        foreach (var card in rangeSection.Cards)
        {
            var rangeKey = ResolveCardKey(card, cardIndex++);
            if (card.Prices is null && card.PrebuiltPrices is null && card.HandlelessPrices is null)
            {
                continue;
            }

            importedKeys.Add(rangeKey);
            var pricesJson = RangeTierPrices.FromDictionary(card.Prices);
            var prebuiltJson = card.PrebuiltPrices is null ? null : RangeTierPrices.FromDictionary(card.PrebuiltPrices);
            var handlelessJson = card.HandlelessPrices is null ? null : RangeTierPrices.FromDictionary(card.HandlelessPrices);

            if (byRangeKey.TryGetValue(rangeKey, out var entity))
            {
                entity.PricesJson = pricesJson;
                entity.PrebuiltPricesJson = prebuiltJson;
                entity.HandlelessPricesJson = handlelessJson;
                LanguageMapper.FinalizeForCosmos(entity, brand, contentLanguage, translationUpdater);
                cmsContext.Update(entity);
            }
            else
            {
                entity = new RangePriceSet
                {
                    Id = Guid.NewGuid(),
                    Name = $"{rangeKey} prices",
                    RangeKey = rangeKey,
                    PricesJson = pricesJson,
                    PrebuiltPricesJson = prebuiltJson,
                    HandlelessPricesJson = handlelessJson,
                };
                LanguageMapper.FinalizeForCosmos(entity, brand, contentLanguage, translationUpdater);
                cmsContext.Add(entity);
                byRangeKey[rangeKey] = entity;
            }

            imported++;
        }

        if (replaceExisting)
        {
            foreach (var entity in byRangeKey.Values.Where(r => !importedKeys.Contains(r.RangeKey)))
            {
                cmsContext.Set<RangePriceSet>().Remove(entity);
                removed++;
            }
        }

        if (imported > 0 || removed > 0)
        {
            await SaveAndClearAsync(cancellationToken);
        }

        return (imported, removed);
    }

    private async Task<int> UpsertMessagesAsync(
        string brand,
        string apiLanguage,
        ContentLanguage contentLanguage,
        IReadOnlyDictionary<string, string>? messages,
        CancellationToken cancellationToken)
    {
        if (messages is null || messages.Count == 0)
        {
            return 0;
        }

        var json = JsonSerializer.Serialize(messages, JsonOptions);
        var existing = await FindSingletonForLocaleAsync<ConfiguratorMessages>(brand, apiLanguage);

        if (existing is null)
        {
            var entity = new ConfiguratorMessages
            {
                Id = Guid.NewGuid(),
                Name = $"{brand} messages",
                MessagesJson = json,
            };
            LanguageMapper.FinalizeForCosmos(entity, brand, contentLanguage, translationUpdater);
            cmsContext.Add(entity);
        }
        else
        {
            existing.MessagesJson = json;
            LanguageMapper.FinalizeForCosmos(existing, brand, contentLanguage, translationUpdater);
            cmsContext.Update(existing);
        }

        await SaveAndClearAsync(cancellationToken);
        return messages.Count;
    }

    private async Task<bool> UpsertSettingsAsync(
        string brand,
        string apiLanguage,
        ContentLanguage contentLanguage,
        IReadOnlyList<string> sectionIdsInOrder,
        CancellationToken cancellationToken)
    {
        if (sectionIdsInOrder.Count == 0)
        {
            return false;
        }

        var sectionOrder = string.Join(",", sectionIdsInOrder);
        var existing = await FindSingletonForLocaleAsync<ConfiguratorSettings>(brand, apiLanguage);

        if (existing is null)
        {
            var entity = new ConfiguratorSettings
            {
                Id = Guid.NewGuid(),
                Name = $"{brand} settings",
                SectionOrder = sectionOrder,
            };
            LanguageMapper.FinalizeForCosmos(entity, brand, contentLanguage, translationUpdater);
            cmsContext.Add(entity);
        }
        else
        {
            existing.SectionOrder = sectionOrder;
            LanguageMapper.FinalizeForCosmos(existing, brand, contentLanguage, translationUpdater);
            cmsContext.Update(existing);
        }

        await SaveAndClearAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Loader filters by <see cref="ITranslatable.Languages"/>; entities saved with only <see cref="ITranslatable.Language"/> set are invisible to that filter.
    /// </summary>
    private async Task<List<RangePriceSet>> ListRangePriceSetsForLocaleAsync(string brand, string apiLanguage)
    {
        var brandKey = LanguageMapper.NormalizeBrand(brand);
        var all = await loader.Get<RangePriceSet>(brandKey, (ContentLanguage?)null);
        return all
            .Where(r => LanguageMapper.MatchesApiLocale(brandKey, r.Language, r.Languages, apiLanguage))
            .ToList();
    }

    private async Task<T?> FindSingletonForLocaleAsync<T>(string brand, string apiLanguage)
        where T : BaseEntity
    {
        var brandKey = LanguageMapper.NormalizeBrand(brand);
        var matches = (await loader.Get<T>(brandKey, (ContentLanguage?)null))
            .Where(e => LanguageMapper.MatchesApiLocale(brandKey, e.Language, e.Languages, apiLanguage))
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        foreach (var duplicate in matches.Skip(1))
        {
            cmsContext.Set<T>().Remove(duplicate);
        }

        return matches[0];
    }

    private static Dictionary<string, T> IndexUniqueByKey<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector,
        IList<T> duplicatesToRemove)
    {
        var index = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in items.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase))
        {
            var keeper = group.First();
            index[group.Key] = keeper;
            foreach (var duplicate in group.Skip(1))
            {
                duplicatesToRemove.Add(duplicate);
            }
        }

        return index;
    }

    private static CloudinaryImage? ToCloudinaryImage(string? publicId) =>
        string.IsNullOrWhiteSpace(publicId)
            ? null
            : new CloudinaryImage { PublicId = publicId.Trim() };

    private static (HashSet<string> SectionIds, HashSet<(string SectionId, string CardKey)> CardKeys, List<string> SectionOrderIds)
        BuildImportKeys(ConfigV1Payload payload)
    {
        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cardKeys = new HashSet<(string SectionId, string CardKey)>(new SectionCardKeyComparer());
        var sectionOrderIds = new List<string>();

        foreach (var sectionDto in payload.Sections)
        {
            if (string.IsNullOrWhiteSpace(sectionDto.Id))
            {
                continue;
            }

            sectionIds.Add(sectionDto.Id);
            sectionOrderIds.Add(sectionDto.Id);

            var cardIndex = 0;
            foreach (var cardDto in sectionDto.Cards ?? [])
            {
                cardKeys.Add((sectionDto.Id, ResolveCardKey(cardDto, cardIndex++)));
            }
        }

        return (sectionIds, cardKeys, sectionOrderIds);
    }

    private static (string? Title, IList<string> Description) ParseTooltip(object? tooltip)
    {
        if (tooltip is null)
        {
            return (null, []);
        }

        var element = tooltip is JsonElement json
            ? json
            : JsonSerializer.SerializeToElement(tooltip, JsonOptions);

        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, []);
        }

        string? title = null;
        if (element.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
        {
            title = titleEl.GetString();
        }

        var lines = new List<string>();
        if (element.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in descEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var line = item.GetString();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }
        }

        return (title, lines);
    }

    private static string SerializeCardPayload(ConfiguratorCardDto card)
    {
        var node = JsonSerializer.SerializeToNode(card, JsonOptions) as JsonObject;
        if (node is null)
        {
            return "{}";
        }

        foreach (var key in CardScalarKeys)
        {
            node.Remove(key);
        }

        PruneNulls(node);
        return node.Count == 0 ? "{}" : node.ToJsonString(JsonOptions);
    }

    private static void PruneNulls(JsonObject obj)
    {
        var remove = new List<string>();
        foreach (var (key, value) in obj)
        {
            if (value is null)
            {
                remove.Add(key);
            }
        }

        foreach (var key in remove)
        {
            obj.Remove(key);
        }
    }

    private static string ResolveCardKey(ConfiguratorCardDto card, int index)
    {
        if (!string.IsNullOrWhiteSpace(card.Type))
        {
            return card.Type.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(card.Title))
        {
            return Slugify(card.Title);
        }

        return $"card-{index}";
    }

    private static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var slug = SlugNonAlphanumeric().Replace(lower, "-");
        return slug.Trim('-');
    }

    private async Task SaveAndClearAsync(CancellationToken cancellationToken)
    {
        await cmsContext.SaveChangesAsync(cancellationToken);
        cmsContext.ChangeTracker.Clear();
    }

    private static bool IsPartitionKeyMismatch(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is Microsoft.Azure.Cosmos.CosmosException { SubStatusCode: 1001 })
            {
                return true;
            }

            if (current.Message.Contains("PartitionKey", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("wrong-pk-value", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCosmosNotFound(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is Microsoft.Azure.Cosmos.CosmosException { StatusCode: System.Net.HttpStatusCode.NotFound })
            {
                return true;
            }

            if (current.Message.Contains("Owner resource does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static SeedImportResult Fail(string brand, string language, string error) =>
        new(false, brand, language, 0, 0, 0, false, 0, 0, 0, 0, error);

    private sealed class SectionCardKeyComparer : IEqualityComparer<(string SectionId, string CardKey)>
    {
        public bool Equals((string SectionId, string CardKey) x, (string SectionId, string CardKey) y) =>
            string.Equals(x.SectionId, y.SectionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.CardKey, y.CardKey, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string SectionId, string CardKey) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SectionId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.CardKey));
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex SlugNonAlphanumeric();
}
