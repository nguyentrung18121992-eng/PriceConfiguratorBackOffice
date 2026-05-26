using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.EditingPage;
using Nobia.CmsToolkit.ListingPage;
using Nobia.CmsToolkit.Translation;

namespace PriceConfiguratorBackoffice.Models;

public class ConfiguratorSection : BaseEntity
{
    [ListingPage(Order = 30)]
    [Display(Name = "Section id (e.g. range, units)")]
    public string SectionId { get; set; } = string.Empty;

    [Display(Name = "Section type")]
    public string Type { get; set; } = string.Empty;

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Translated]
    public string? Title { get; set; }

    [Translated]
    public string? ShortTitle { get; set; }

    [Translated]
    [EditingPage(EditingFields.Textarea)]
    public string? Description { get; set; }

    [Translated]
    [Display(Name = "Tooltip title")]
    public string? TooltipTitle { get; set; }

    [Translated]
    [Display(Name = "Tooltip description (one paragraph per line)")]
    [TooltipDescriptionList]
    public IList<string> TooltipDescription { get; set; } = [];
}
