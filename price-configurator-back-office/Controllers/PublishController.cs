using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Controllers;

[ApiController]
[Authorize]
public class PublishController : ControllerBase
{
    [HttpPost("/api/publish/v1")]
    public async Task<IActionResult> PublishNow(
        [FromServices] IPublishService publishService,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language)
    {
        var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
        var result = await publishService.PublishNowAsync(brand, contentLanguage);

        if (!result.Success)
        {
            return BadRequest(new { success = false, error = result.Error });
        }

        return Ok(new
        {
            success = true,
            version = result.Version,
            language = LanguageMapper.ToApiLanguageCode(brand, contentLanguage),
        });
    }

    [HttpPost("/api/publish/v1/schedule")]
    public async Task<IActionResult> SchedulePublish(
        [FromServices] IPublishService publishService,
        [FromQuery][BindRequired] string brand,
        [FromQuery][BindRequired] string language,
        [FromQuery][BindRequired] DateTime scheduledAtUtc)
    {
        var contentLanguage = LanguageMapper.ToContentLanguage(brand, language);
        var result = await publishService.SchedulePublishAsync(brand, contentLanguage, scheduledAtUtc);

        if (!result.Success)
        {
            return BadRequest(new { success = false, error = result.Error });
        }

        return Ok(new { success = true, scheduledPublishId = result.ScheduledPublishId });
    }
}
