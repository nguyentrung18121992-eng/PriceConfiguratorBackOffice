using System.ComponentModel.DataAnnotations;
using CmsToolkit.SingularSupport;
using Nobia.CmsToolkit.EditingPage;

namespace PriceConfiguratorBackoffice.Models;

[Singular]
public class ConfiguratorSettings : BaseEntity
{
    /// <summary>Hidden label; singular settings use the content-type title in the CMS.</summary>
    [EditingPage("hidden")]
    public new string? Name { get; set; }

    [Display(Name = "Section order (comma-separated ids)", Order = 10)]
    public string? SectionOrder { get; set; }
}
