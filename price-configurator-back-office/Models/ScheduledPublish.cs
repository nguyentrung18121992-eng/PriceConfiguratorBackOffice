using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.ListingPage;

namespace PriceConfiguratorBackoffice.Models;

public class ScheduledPublish : BaseEntity
{
    [ListingPage(Order = 60)]
    [Display(Name = "Scheduled at (UTC)")]
    public DateTime? ScheduledPublishAt { get; set; }

    [ListingPage(Order = 70)]
    [Display(Name = "Completed")]
    public bool Completed { get; set; }

    [Display(Name = "Error message")]
    public string? ErrorMessage { get; set; }
}
