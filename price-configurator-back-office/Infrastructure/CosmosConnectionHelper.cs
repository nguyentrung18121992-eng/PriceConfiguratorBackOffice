namespace PriceConfiguratorBackoffice.Infrastructure;

public static class CosmosConnectionHelper
{
    public static string Resolve(IConfiguration configuration) =>
        configuration.GetConnectionString("priceconfigurator")
        ?? configuration.GetConnectionString("PriceConfigurator")
        ?? string.Empty;

    public static bool IsValid(string? connectionString) =>
        !string.IsNullOrWhiteSpace(connectionString)
        && connectionString.Contains("AccountEndpoint=", StringComparison.OrdinalIgnoreCase);

    public static string GetValidationMessage() =>
        "ConnectionStrings:priceconfigurator must be an Azure Cosmos DB connection string " +
        "(AccountEndpoint=http(s)://...;AccountKey=...). " +
        "CmsToolkit does not support MongoDB URLs. " +
        "Local: docker compose up -d (cosmos-emulator + back-office) or appsettings.local.json " +
        "(http://localhost:18081 from host when emulator runs in Docker).";

    public static string DescribeEndpoint(string connectionString)
    {
        const string key = "AccountEndpoint=";
        var start = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "(unknown)";
        }

        start += key.Length;
        var end = connectionString.IndexOf(';', start);
        return end < 0 ? connectionString[start..] : connectionString[start..end];
    }
}
