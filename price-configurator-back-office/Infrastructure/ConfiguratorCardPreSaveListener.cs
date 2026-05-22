using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Versioning;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Infrastructure;

/// <summary>
/// Persists editor fields into <see cref="ConfiguratorCard.CardDataJson"/> before Cosmos save.
/// </summary>
public sealed class ConfiguratorCardPreSaveListener : IPreSaveContextChangeListener
{
    public Task PreSave(CmsContext context, IList<EntityChange> changes)
    {
        foreach (var entry in context.ChangeTracker.Entries<ConfiguratorCard>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.SyncCardPayload();
            }
        }

        return Task.CompletedTask;
    }
}
