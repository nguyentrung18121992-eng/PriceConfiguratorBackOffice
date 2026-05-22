using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.ListingPage;

namespace PriceConfiguratorBackoffice.Models;

public class PublishedConfiguration : BaseEntity
{
    [ListingPage(Order = 60)]
    [Display(Name = "Version")]
    public int Version { get; set; }

    [ListingPage(Order = 70)]
    [Display(Name = "Published at (UTC)")]
    public DateTime? PublishedAt { get; set; }

    [Display(Name = "Published API payload (JSON)")]
    public string PayloadJson { get; set; } = "[]";
}
