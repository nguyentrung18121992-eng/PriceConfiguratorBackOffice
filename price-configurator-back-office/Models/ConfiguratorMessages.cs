using System.ComponentModel.DataAnnotations;
using CmsToolkit.SingularSupport;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Models;

/// <summary>
/// UI copy keyed like config/{brand}/messages.json (app.header.title, etc.).
/// </summary>
[Singular]
public class ConfiguratorMessages : BaseEntity
{
    [ConfiguratorMessagesEditor]
    [Display(Name = "Messages", Description = "Keys are fixed per brand. Edit the text values only.")]
    public string MessagesJson { get; set; } = "{}";

    public void NormalizeMessages(ConfiguratorMessagesTemplateProvider templates)
    {
        var template = templates.GetTemplate(Brand, Language);
        MessagesJson = ConfiguratorMessagesHelper.NormalizeMessagesJson(MessagesJson, template);
    }
}
