namespace PriceConfiguratorBackoffice.Infrastructure;

public static class CloudinaryConfigurationHelper
{
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["CloudinaryName"])
        && !string.IsNullOrWhiteSpace(configuration["CloudinaryKey"])
        && !string.IsNullOrWhiteSpace(configuration["CloudinarySecret"]);

    public static bool UsesKnownInvalidCloudName(IConfiguration configuration) =>
        string.Equals(configuration["CloudinaryName"], "test", StringComparison.OrdinalIgnoreCase);

    public static string GetMissingKeysMessage(IConfiguration configuration)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(configuration["CloudinaryName"]))
        {
            missing.Add("CloudinaryName");
        }

        if (string.IsNullOrWhiteSpace(configuration["CloudinaryKey"]))
        {
            missing.Add("CloudinaryKey");
        }

        if (string.IsNullOrWhiteSpace(configuration["CloudinarySecret"]))
        {
            missing.Add("CloudinarySecret");
        }

        return missing.Count == 0
            ? string.Empty
            : "Cloudinary Media Library is not configured. Set "
              + string.Join(", ", missing)
              + " in appsettings.local.json, user secrets, or .env (Docker). "
              + "Use the same values as kitchen-quiz-backoffice (cloud dgg9enyjv).";
    }

    public static string GetInvalidCloudNameMessage() =>
        "CloudinaryName is \"test\" — that overrides appsettings.json and breaks Browse (Cloudinary returns Not Found). "
        + "Set CloudinaryName to dgg9enyjv and use the API key/secret from kitchen-quiz-backoffice for that cloud.";
}
