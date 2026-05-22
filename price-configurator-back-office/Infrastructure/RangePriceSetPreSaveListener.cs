using Microsoft.EntityFrameworkCore;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Versioning;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Infrastructure;

/// <summary>
/// Persists editor tier fields into JSON arrays before Cosmos save (publish format unchanged).
/// </summary>
public sealed class RangePriceSetPreSaveListener : IPreSaveContextChangeListener
{
    public Task PreSave(CmsContext context, IList<EntityChange> changes)
    {
        foreach (var entry in context.ChangeTracker.Entries<RangePriceSet>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.SyncJsonFromTierFields();
            }
        }

        return Task.CompletedTask;
    }
}
