using Nobia.CmsToolkit.EditingPage;

namespace PriceConfiguratorBackoffice.Models;

/// <summary>
/// Renders a number input with <c>step="any"</c> so editors can enter decimal prices.
/// </summary>
public sealed class DecimalPriceAttribute() : EditingPageAttribute("DecimalPrice");
