namespace CoffeeNChill.ApiGateway.Options;

// Microsoft Learn - Use the Azurite emulator for local Azure Storage development
// https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite
public sealed class StorageOptions
{
    public const string SectionName = "AzureStorage";

    // "UseDevelopmentStorage=true" is the connection string Azurite understands.
    public string ConnectionString { get; init; } = "UseDevelopmentStorage=true";

    // Configuration first, then the AzureWebJobsStorage variable shared with the Functions project, then the Azurite default. Reusing that chain means one variable drives both services locally instead of each keeping its own copy of the same string.
    public static StorageOptions Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetSection(SectionName)["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(configured))
            return new StorageOptions { ConnectionString = configured };

        var jobsStorage = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrWhiteSpace(jobsStorage))
            return new StorageOptions { ConnectionString = jobsStorage };

        return new StorageOptions();
    }
}
