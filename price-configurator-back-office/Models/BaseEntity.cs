using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.EditingPage;
using Nobia.CmsToolkit.Entity;
using Nobia.CmsToolkit.ListingPage;
using Nobia.CmsToolkit.Property.Nameable;
using Nobia.CmsToolkit.Property.VisuallyHidden;
using Nobia.CmsToolkit.Translation;

namespace PriceConfiguratorBackoffice.Models;

public class BaseEntity : IEntity, INameable, ITranslatable
{
    [EditingPage("hidden")]
    public Guid Id { get; set; }

    [ListingPage(Order = 10)]
    [Display(Name = "Brand", Order = 10)]
    [EditingPage("hidden")]
    public string Brand { get; set; } = string.Empty;

    public string? Language { get; set; } = string.Empty;

    /// <summary>Listing-only mirror of <see cref="Language"/> (CmsToolkit hides <c>Language</c> on list pages).</summary>
    [ListingPage(Order = 20)]
    [Display(Name = "Language", Order = 20)]
    [VisuallyHidden]
    public string? LanguageList => Language;

    public IList<string> Languages { get; set; } = [];
    public IDictionary<string, IDictionary<string, string>>? Translations { get; set; }

    [Translated]
    [ListingPage(Order = 50)]
    [Display(Order = 50)]
    [Width("col-md-4")]
    public string? Name { get; set; }
}
