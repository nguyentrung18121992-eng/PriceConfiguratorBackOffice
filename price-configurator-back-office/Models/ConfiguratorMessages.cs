using System.ComponentModel.DataAnnotations;
using CmsToolkit.SingularSupport;
using Nobia.CmsToolkit.Translation;

namespace PriceConfiguratorBackoffice.Models;

/// <summary>
/// UI copy keyed like config/{brand}/messages.json (app.header.title, etc.).
/// </summary>
[Singular]
public class ConfiguratorMessages : BaseEntity
{
    [Display(Name = "Messages JSON (key → string)")]
    public string MessagesJson { get; set; } = "{}";
}
