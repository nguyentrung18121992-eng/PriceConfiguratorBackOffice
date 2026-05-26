using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Nobia.CmsToolkit.Context;
using Nobia.CmsToolkit.Versioning;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Infrastructure;

public sealed class ConfiguratorMessagesPreSaveListener(
    ConfiguratorMessagesTemplateProvider templates,
    IHttpContextAccessor httpContextAccessor) : IPreSaveContextChangeListener
{
    public Task PreSave(CmsContext context, IList<EntityChange> changes)
    {
        var postedMessagesJson = ReadPostedFormValue("MessagesJson");

        foreach (var entry in context.ChangeTracker.Entries<ConfiguratorMessages>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(postedMessagesJson))
            {
                entry.Entity.MessagesJson = postedMessagesJson;
            }

            entry.Entity.NormalizeMessages(templates);
            entry.Property(e => e.MessagesJson).IsModified = true;
        }

        return Task.CompletedTask;
    }

    private string? ReadPostedFormValue(string propertyName)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null || !request.HasFormContentType)
        {
            return null;
        }

        var direct = request.Form[propertyName].ToString();
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        foreach (var key in request.Form.Keys)
        {
            if (key.EndsWith(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                var value = request.Form[key].ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
