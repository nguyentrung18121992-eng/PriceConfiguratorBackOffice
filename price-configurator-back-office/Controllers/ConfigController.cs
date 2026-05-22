using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.Services;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Controllers;

[ApiController]
public class ConfigController : ControllerBase
{
    [HttpGet("/api/config/v1")]
    [ProducesResponseType<ConfigV1Response>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetConfigV1(
        [FromServices] IPublishedConfigurationQuery publishedQuery,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language)
    {
        var brandKey = LanguageMapper.NormalizeBrand(brand);
        var apiLanguage = language.Trim();

        var published = await publishedQuery.GetLatestAsync(brandKey, apiLanguage);
        if (published is null)
        {
            return NotFound();
        }

        var payload = PayloadJsonParser.Parse(published.PayloadJson);
        if (payload is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-cache";
        return Respond(ToResponse(payload, brandKey, apiLanguage, published.Version, published.PublishedAt ?? DateTime.UtcNow, preview: false));
    }

    /// <summary>
    /// P4: Load draft configuration using a preview token (not cached for public CDN).
    /// </summary>
    [HttpGet("/api/config/v1/preview")]
    [ProducesResponseType<ConfigV1Response>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetConfigPreview(
        [FromServices] IPreviewTokenService previewTokenService,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language,
        [FromQuery][BindRequired] string token)
    {
        if (!await previewTokenService.ValidateTokenAsync(token, brand, language))
        {
            return NotFound();
        }

        var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
        var payload = await previewTokenService.BuildPreviewPayloadAsync(brand, contentLanguage);

        if (payload is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return Respond(ToResponse(
            payload,
            brand,
            language.Trim(),
            version: 0,
            publishedAt: DateTime.UtcNow,
            preview: true));
    }

    private IActionResult Respond(ConfigV1Response response)
    {
        if (!response.Preview)
        {
            Response.Headers.ETag = $"\"{response.Brand}-{response.Language}-v{response.Version}\"";
        }

        return Ok(response);
    }

    private static ConfigV1Response ToResponse(
        ConfigV1Payload payload,
        string brand,
        string language,
        int version,
        DateTime publishedAt,
        bool preview) =>
        new(
            version,
            publishedAt,
            brand,
            language,
            payload.Sections,
            payload.Messages,
            preview);
}
