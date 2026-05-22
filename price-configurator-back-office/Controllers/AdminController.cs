using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Entity;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Controllers;

[ApiController]
[Authorize]
public class AdminController : ControllerBase
{
    private static readonly (string Brand, string Language)[] AllBrands =
    [
        (Constants.Brands.Magnet, Constants.Languages.Magnet),
        (Constants.Brands.Invita, Constants.Languages.Invita),
        (Constants.Brands.Sigdal, Constants.Languages.Sigdal),
        (Constants.Brands.Norema, Constants.Languages.Norema),
        (Constants.Brands.Novart, Constants.Languages.Novart),
        (Constants.Brands.Marbodal, Constants.Languages.Marbodal),
    ];

    [HttpPost("/api/admin/publish-seed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> PublishFromSeed(
        [FromServices] IPublishService publishService,
        [FromQuery][BindRequired] string brand,
        [FromQuery] string language)
    {
        var lang = language.Trim();
        var contentLanguage = LanguageMapper.ToContentLanguage(brand, lang);
        var result = await publishService.PublishNowAsync(brand, contentLanguage);

        if (!result.Success)
        {
            return BadRequest(new { success = false, error = result.Error });
        }

        return Ok(new { success = true, version = result.Version, brand, language = lang });
    }

    /// <summary>
    /// P2: Publish seed payloads for every brand (requires Data/Seeds/*.payload.json).
    /// </summary>
    [HttpPost("/api/admin/publish-all-seeds")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> PublishAllSeeds([FromServices] IPublishService publishService)
    {
        var results = new List<PublishSeedResult>();

        foreach (var (brand, language) in AllBrands)
        {
            var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
            var result = await publishService.PublishNowAsync(brand, contentLanguage);
            results.Add(new PublishSeedResult(brand, language, result.Success, result.Version, result.Error));
        }

        return Ok(new { success = results.All(r => r.Success), results });
    }

    /// <summary>
    /// Import sections, cards, messages, and settings from Data/Seeds/*.payload.json into CMS draft entities.
    /// Run scripts/sync-seeds-from-frontend.ps1 first to refresh seeds from the Zeus frontend repo.
    /// </summary>
    [HttpPost("/api/admin/import-from-seed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ImportFromSeed(
        [FromServices] ISeedImportService seedImportService,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language,
        [FromQuery] bool replaceExisting = true)
    {
        var result = await seedImportService.ImportFromSeedFileAsync(
            LanguageMapper.NormalizeBrand(brand),
            language.Trim(),
            replaceExisting);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Import all brands from Data/Seeds/*.payload.json (requires sync from frontend first).
    /// </summary>
    [HttpPost("/api/admin/import-all-from-seed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ImportAllFromSeed(
        [FromServices] ISeedImportService seedImportService,
        [FromQuery] bool replaceExisting = true)
    {
        var results = new List<SeedImportResult>();

        foreach (var (brand, language) in AllBrands)
        {
            results.Add(await seedImportService.ImportFromSeedFileAsync(brand, language, replaceExisting));
        }

        return Ok(new { success = results.All(r => r.Success), results });
    }

    /// <summary>
    /// Import all brands from seeds, then publish each (draft → PublishedConfiguration).
    /// </summary>
    [HttpPost("/api/admin/bootstrap-from-seed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> BootstrapFromSeed(
        [FromServices] ISeedImportService seedImportService,
        [FromServices] IPublishService publishService,
        [FromQuery] bool replaceExisting = true)
    {
        var importResults = new List<SeedImportResult>();
        var publishResults = new List<PublishSeedResult>();

        foreach (var (brand, language) in AllBrands)
        {
            var imported = await seedImportService.ImportFromSeedFileAsync(brand, language, replaceExisting);
            importResults.Add(imported);

            if (!imported.Success)
            {
                publishResults.Add(new PublishSeedResult(brand, language, false, 0, imported.Error));
                continue;
            }

            var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
            var published = await publishService.PublishNowAsync(brand, contentLanguage);
            publishResults.Add(new PublishSeedResult(brand, language, published.Success, published.Version, published.Error));
        }

        return Ok(new
        {
            success = importResults.All(r => r.Success) && publishResults.All(r => r.Success),
            import = importResults,
            publish = publishResults,
        });
    }

    /// <summary>
    /// P3: Copy messages from seed payload into CMS ConfiguratorMessages (draft).
    /// </summary>
    [HttpPost("/api/admin/import-messages-from-seed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ImportMessagesFromSeed(
        [FromServices] IConfigSnapshotBuilder snapshotBuilder,
        [FromServices] IEntityLoader loader,
        [FromServices] CmsContext cmsContext,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language)
    {
        var payload = snapshotBuilder.BuildFromSeedFile(brand, language.Trim());
        if (payload?.Messages is null || payload.Messages.Count == 0)
        {
            return BadRequest(new { success = false, error = "No messages in seed file." });
        }

        var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
        var existing = (await loader.Get<ConfiguratorMessages>(brand, contentLanguage)).FirstOrDefault();
        var json = JsonSerializer.Serialize(payload.Messages);

        if (existing is null)
        {
            var entity = new ConfiguratorMessages
            {
                Id = Guid.NewGuid(),
                Name = $"{brand} messages",
                MessagesJson = json,
            };
            LanguageMapper.ApplyCmsScope(entity, brand, contentLanguage);
            cmsContext.Add(entity);
        }
        else
        {
            LanguageMapper.ApplyCmsScope(existing, brand, contentLanguage);
            existing.MessagesJson = json;
            cmsContext.Update(existing);
        }

        await cmsContext.SaveChangesAsync();
        return Ok(new { success = true, brand, language, count = payload.Messages.Count });
    }

    private sealed record PublishSeedResult(
        string Brand,
        string Language,
        bool Success,
        int Version,
        string? Error);
}
