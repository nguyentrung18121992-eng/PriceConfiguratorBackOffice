namespace PriceConfiguratorBackoffice.Helpers;

/// <summary>Best-effort brand/language from CMS URL path (e.g. /magnet/en/...).</summary>
public static class CmsRouteLocale
{
    private static readonly (string Brand, string Language)[] Known =
    [
        (Constants.Brands.Magnet, Constants.Languages.Magnet),
        (Constants.Brands.Marbodal, Constants.Languages.Marbodal),
        (Constants.Brands.Invita, Constants.Languages.Invita),
        (Constants.Brands.Sigdal, Constants.Languages.Sigdal),
        (Constants.Brands.Norema, Constants.Languages.Norema),
        (Constants.Brands.Novart, Constants.Languages.Novart),
    ];

    public static (string Brand, string Language) Resolve(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var (brand, language) in Known)
        {
            if (path.Contains($"/{brand}/", StringComparison.OrdinalIgnoreCase))
            {
                return (brand, language);
            }
        }

        return (Constants.Brands.Magnet, Constants.Languages.Magnet);
    }
}
