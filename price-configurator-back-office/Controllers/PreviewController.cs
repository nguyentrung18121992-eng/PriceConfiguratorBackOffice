using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Controllers;

[ApiController]
[Authorize]
public class PreviewController : ControllerBase
{
    [HttpPost("/api/preview/v1/token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePreviewToken(
        [FromServices] IPreviewTokenService previewTokenService,
        [FromServices] IConfiguration configuration,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language,
        [FromQuery] int? hours = 8)
    {
        var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
        var lifetime = TimeSpan.FromHours(Math.Clamp(hours ?? 8, 1, 72));
        var token = await previewTokenService.CreateTokenAsync(brand, contentLanguage, lifetime);

        var publicBase = configuration["PublicConfiguratorBaseUrl"]?.TrimEnd('/')
            ?? $"https://price-configurator-{brand}.dev.nobiadigital.com";

        var previewUrl =
            $"{publicBase}?previewToken={token}&brand={Uri.EscapeDataString(brand)}&language={Uri.EscapeDataString(language.Trim())}";

        return Ok(new
        {
            success = true,
            token,
            expiresInHours = lifetime.TotalHours,
            previewUrl,
        });
    }
}
