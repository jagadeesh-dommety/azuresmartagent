using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Advisor;
using Azure.ResourceManager.Advisor.Models;
using Azure;

namespace SmartSreAgent;

public class CosmosConfigCollector
{
    private readonly ArmClient armClient;
    private const string resourceId = "/subscriptions/9487cd81-9520-47e2-941d-2cfc4dda3b30/resourceGroups/super-fans/providers/Microsoft.DocumentDb/databaseAccounts/super-fans-users";
    public CosmosConfigCollector()
    {
        armClient = new ArmClient(new AzureCliCredential());
    }
    public async Task CollectCosmosDbConfig()
    {
        //InitialSetup().GetAwaiter().GetResult();
        try
        {
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            CosmosDBAccountResource cosmosAccount = armClient.GetCosmosDBAccountResource(resourceIdentifier);

            // Fetch account details
            CosmosDBAccountResource cosmosAccountDetails = await cosmosAccount.GetAsync();
            Console.WriteLine($"Cosmos DB Config: {cosmosAccountDetails.Data.Id}, {cosmosAccountDetails.Data.Name}");
            GetThroughPutDetails(cosmosAccount);
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Error collecting Cosmos DB config: {ex.Message}");
            throw;
        }
    }

    private static void GetThroughPutDetails(CosmosDBAccountResource cosmosAccount)
    {
        foreach (var db in cosmosAccount.GetCosmosDBSqlDatabases())
        {
            Console.WriteLine($"Database: {db.Data.Name}");

            // Provisioned throughput (manual or autoscale)
            if (db.Data.Options?.Throughput != null)
            {
                var throughput = db.Data.Options.Throughput;
                Console.WriteLine($" - Throughput: {throughput} RU/s");
            }
            if (db.Data.Options?.AutoscaleMaxThroughput != null)
            {
                var maxThroughput = db.Data.Options.AutoscaleMaxThroughput;
                Console.WriteLine($" - Autoscale Max Throughput: {maxThroughput} RU/s");
            }
        }
    }

    public async Task GetAdvisorRecommendations()
    {
        var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/9487cd81-9520-47e2-941d-2cfc4dda3b30"));
        Pageable<ConfigData> advisorRecommendations = subscription.GetConfigurations();
        foreach (var recommendation in advisorRecommendations)
        {
            Console.WriteLine($"Recommendation: {recommendation.Name}, Type: {recommendation.SystemData}");
        }
    }
}
