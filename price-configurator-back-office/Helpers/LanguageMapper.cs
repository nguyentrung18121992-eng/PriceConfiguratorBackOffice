using Nobia.CmsToolkit.Options;
using Nobia.CmsToolkit.Translation;
using PriceConfiguratorBackoffice;
using PriceConfiguratorBackoffice.Models;

namespace PriceConfiguratorBackoffice.Helpers;

public static class LanguageMapper
{
    /// <summary>
    /// Cosmos partition key is <see cref="IEntity.Brand"/> (see CmsToolkit HasPartitionKey). Must match <c>AddBrand</c> registration.
    /// </summary>
    public static string NormalizeBrand(string brand) =>
        brand.Trim().ToLowerInvariant();

    /// <summary>
    /// Language code in <see cref="ITranslatable.Languages"/> (and before translation finalize). Matches <see cref="ContentLanguage"/>.
    /// </summary>
    public static string ToCmsLanguage(ContentLanguage contentLanguage) =>
        contentLanguage.ToString();

    public static void ApplyCmsScope(BaseEntity entity, string brand, ContentLanguage contentLanguage)
    {
        entity.Brand = NormalizeBrand(brand);
        if (entity is ITranslatable translatable)
        {
            translatable.Language = ToCmsLanguage(contentLanguage);
        }
    }

    /// <summary>
    /// Matches <see cref="Nobia.CmsToolkit.Entity.EntitySaver"/> / NewEntityViewComponent: set Brand + Language, then <see cref="ITranslationUpdater.UpdateTranslation"/>.
    /// </summary>
    public static void FinalizeForCosmos(
        ITranslatable entity,
        string brand,
        ContentLanguage contentLanguage,
        ITranslationUpdater translationUpdater)
    {
        ApplyCmsScope((BaseEntity)entity, brand, contentLanguage);
        translationUpdater.UpdateTranslation(entity, ToCmsLanguage(contentLanguage));
    }

    public static ContentLanguage ToContentLanguage(string brand, string language)
    {
        var normalizedBrand = NormalizeBrand(brand);
        var normalized = language.Trim().ToLowerInvariant();

        return normalizedBrand switch
        {
            Constants.Brands.Magnet when normalized is "en" or "en-gb" => ContentLanguage.en,
            Constants.Brands.Marbodal when normalized is "sv" or "sv-se" => ContentLanguage.sv,
            Constants.Brands.Invita when normalized is "da" or "da-dk" => ContentLanguage.da,
            Constants.Brands.Sigdal or Constants.Brands.Norema when normalized is "no" or "nb-no" => ContentLanguage.no,
            Constants.Brands.Novart when normalized is "fi" or "fi-fi" => ContentLanguage.fi,
            _ => Enum.TryParse<ContentLanguage>(normalized, true, out var parsed)
                ? parsed
                : ContentLanguage.en,
        };
    }

    public static string ToApiLanguageCode(string brand, ContentLanguage language)
    {
        return NormalizeBrand(brand) switch
        {
            Constants.Brands.Magnet => Constants.Languages.Magnet,
            Constants.Brands.Marbodal => Constants.Languages.Marbodal,
            Constants.Brands.Invita => Constants.Languages.Invita,
            Constants.Brands.Sigdal => Constants.Languages.Sigdal,
            Constants.Brands.Norema => Constants.Languages.Norema,
            Constants.Brands.Novart => Constants.Languages.Novart,
            _ => language.ToString(),
        };
    }

    /// <summary>
    /// Whether a stored entity belongs to the requested public API locale (e.g. en-GB), including legacy CMS codes (e.g. en).
    /// </summary>
    public static bool MatchesApiLocale(
        string brand,
        string? storedLanguage,
        IList<string>? storedLanguages,
        string requestLanguage)
    {
        var brandKey = NormalizeBrand(brand);
        var contentLanguage = ToContentLanguage(brandKey, requestLanguage);
        var apiLanguage = ToApiLanguageCode(brandKey, contentLanguage);
        var cmsLanguage = ToCmsLanguage(contentLanguage);

        if (storedLanguages is { Count: > 0 }
            && !storedLanguages.Any(l =>
                string.Equals(l, cmsLanguage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(l, apiLanguage, StringComparison.OrdinalIgnoreCase)
                || ToContentLanguage(brandKey, l) == contentLanguage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(storedLanguage))
        {
            return true;
        }

        return string.Equals(storedLanguage, apiLanguage, StringComparison.OrdinalIgnoreCase)
            || string.Equals(storedLanguage, cmsLanguage, StringComparison.OrdinalIgnoreCase)
            || ToContentLanguage(brandKey, storedLanguage) == contentLanguage;
    }

    /// <summary>CMS + API language tags written on publish so loader and public API both resolve.</summary>
    public static IList<string> PublishLanguageTags(string brand, ContentLanguage contentLanguage)
    {
        var cms = ToCmsLanguage(contentLanguage);
        var api = ToApiLanguageCode(brand, contentLanguage);
        return string.Equals(cms, api, StringComparison.OrdinalIgnoreCase)
            ? [cms]
            : [cms, api];
    }
}
