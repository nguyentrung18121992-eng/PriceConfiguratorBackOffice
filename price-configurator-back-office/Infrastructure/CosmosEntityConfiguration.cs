using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobia.CmsToolkit.Entity;
using Version = Nobia.CmsToolkit.Versioning.Version;

namespace PriceConfiguratorBackoffice.Infrastructure;

/// <summary>
/// EF Cosmos maps partition key to <c>/Brand</c>; JSON property name must match (PascalCase), not camelCase.
/// </summary>
public static class CosmosEntityConfiguration
{
    public static void ConfigureBrandPartitionKey<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IEntity
    {
        builder.Property(e => e.Brand).ToJsonProperty("Brand");
    }

    public static void ConfigureModel(ModelBuilder builder)
    {
        builder.HasDefaultContainer("PriceConfigurator");
        builder.Entity<Version>().Property(v => v.Brand).ToJsonProperty("Brand");
    }
}
