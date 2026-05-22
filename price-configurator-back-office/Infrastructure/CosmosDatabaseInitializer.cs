using Microsoft.EntityFrameworkCore;
using Nobia.Backend.Configuration;
using Nobia.CmsToolkit.Context;

namespace PriceConfiguratorBackoffice.Infrastructure;

public static class CosmosDatabaseInitializer
{
    public static async Task EnsureCreatedAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!EnvironmentHelper.IsLocal() && !environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsContext>();

        logger.LogInformation("Cosmos EnsureCreated starting (database PriceConfigurator)...");

        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                var created = await context.Database.EnsureCreatedAsync(timeout.Token);
                logger.LogInformation(
                    created
                        ? "Cosmos database and containers were created (EnsureCreated)."
                        : "Cosmos database already exists (EnsureCreated).");
                return;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Cosmos EnsureCreated attempt {Attempt}/{Max} timed out.", attempt, maxAttempts);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Cosmos EnsureCreated attempt {Attempt}/{Max} failed; retrying in 10s.",
                    attempt,
                    maxAttempts);
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            else
            {
                logger.LogError(
                    "Cosmos EnsureCreated failed after {Max} attempts. Import from seed will retry; " +
                    "or restart after `curl http://localhost:18080/ready`.",
                    maxAttempts);
            }
        }
    }

    public static async Task RecreateAsync(
        CmsContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureDeletedAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        logger.LogWarning("Cosmos database PriceConfigurator was recreated (EnsureDeleted + EnsureCreated).");
    }
}
