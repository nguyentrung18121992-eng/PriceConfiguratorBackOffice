using System.ComponentModel.DataAnnotations;
using Nobia.CmsToolkit.ListingPage;

namespace PriceConfiguratorBackoffice.Models;

public class PreviewToken : BaseEntity
{
    [ListingPage(Order = 60)]
    [Display(Name = "Token")]
    public string Token { get; set; } = string.Empty;

    [ListingPage(Order = 70)]
    [Display(Name = "Expires at (UTC)")]
    public DateTime? ExpiresAtUtc { get; set; }

    [ListingPage(Order = 80)]
    [Display(Name = "Revoked")]
    public bool Revoked { get; set; }
}
