using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Services;

public class ScheduledPublishHostedService(
    IServiceProvider serviceProvider,
    ILogger<ScheduledPublishHostedService> logger) : BackgroundService
{
    private int _consecutiveFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Emulator / Cosmos can take 1–2 minutes to accept requests after container start.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        logger.LogInformation("Scheduled publish worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
                _consecutiveFailures = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures == 1 || _consecutiveFailures % 5 == 0)
                {
                    logger.LogError(
                        ex,
                        "Scheduled publish worker failed (Cosmos unavailable?). " +
                        "Start Cosmos: docker compose up -d in price-configurator-back-office, " +
                        "or Windows Azure Cosmos DB Emulator on https://localhost:8081");
                }
            }

            var delay = _consecutiveFailures > 0
                ? TimeSpan.FromMinutes(2)
                : TimeSpan.FromMinutes(1);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var cmsContext = scope.ServiceProvider.GetRequiredService<CmsContext>();
        var publishService = scope.ServiceProvider.GetRequiredService<IPublishService>();

        var due = await cmsContext.Set<ScheduledPublish>()
            .Where(s => !s.Completed
                && s.ScheduledPublishAt != null
                && s.ScheduledPublishAt <= DateTime.UtcNow)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var item in due)
        {
            var language = LanguageMapper.ToContentLanguage(item.Brand, item.Language ?? "en-GB");
            var result = await publishService.PublishNowAsync(item.Brand, language, cancellationToken);
            item.Completed = true;
            item.ErrorMessage = result.Success ? null : result.Error;

            logger.LogInformation(
                "Scheduled publish for {Brand} {Language}: {Success}",
                item.Brand,
                item.Language,
                result.Success);
        }

        if (due.Count > 0)
        {
            await cmsContext.SaveChangesAsync(cancellationToken);
        }
    }
}
