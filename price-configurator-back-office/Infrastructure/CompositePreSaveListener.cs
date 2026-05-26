using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Versioning;

namespace PriceConfiguratorBackoffice.Infrastructure;

/// <summary>
/// ASP.NET Core DI only keeps one <see cref="IPreSaveContextChangeListener"/> unless registered as a composite.
/// </summary>
public sealed class CompositePreSaveListener(
    RangePriceSetPreSaveListener rangePriceSet,
    ConfiguratorCardPreSaveListener configuratorCard,
    ConfiguratorMessagesPreSaveListener configuratorMessages) : IPreSaveContextChangeListener
{
    public async Task PreSave(CmsContext context, IList<EntityChange> changes)
    {
        await rangePriceSet.PreSave(context, changes);
        await configuratorCard.PreSave(context, changes);
        await configuratorMessages.PreSave(context, changes);
    }
}
