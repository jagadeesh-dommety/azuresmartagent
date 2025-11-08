using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace SmartSreAgent;

public class CosmosMetricsCollector
{
     private readonly LogsQueryClient _logsClient;
    private readonly AzureCliCredential managedIdentity;
    private readonly AzureDataLakeParquetWriter parquetWriter;
    private const string currentworkspaceId = "/subscriptions/9487cd81-9520-47e2-941d-2cfc4dda3b30/resourcegroups/appsvc_windows_centralus_basic/providers/microsoft.operationalinsights/workspaces/workspace-superfans-net";
    public CosmosMetricsCollector(AzureDataLakeParquetWriter parquetWriter)
    {
        this.parquetWriter = parquetWriter;
        managedIdentity = new AzureCliCredential();
        _logsClient = new LogsQueryClient(managedIdentity);
    }    public async Task InitialSetup()
    {
        // Any initial setup if needed
        var token = managedIdentity.GetToken(
            new Azure.Core.TokenRequestContext(
                new[] { "https://management.azure.com/.default" }
            )
        );
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        var payload = new
        {
            properties = new
            {
                workspaceId = currentworkspaceId,
                logs = new[] {
                            new { category = "DataPlaneRequests", enabled = true },
                            new { category = "ControlPlaneRequests", enabled = true },
                            new { category = "QueryRuntimeStatistics", enabled = true }
                        },
                metrics = new[] {
                            new { category = "AllMetrics", enabled = true }
                        }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        
        var url = "https://management.azure.com/subscriptions/9487cd81-9520-47e2-941d-2cfc4dda3b30/resourceGroups/super-fans/providers/Microsoft.DocumentDb/databaseAccounts/super-fans-users/providers/microsoft.insights/diagnosticSettings/cosmosdatacollection?api-version=2021-05-01-preview";
        var response = await http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        var responseoutput = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"InitialSetup response: {responseoutput}");
    }

    public async Task CollectCosmosDbMetrics()
    {
        //InitialSetup().GetAwaiter().GetResult();
        await FetchMetricsForCosmos();
    }

    private async Task FetchMetricsForCosmos()
    {
        string kql = @"
AzureDiagnostics
| where ResourceProvider == 'MICROSOFT.DOCUMENTDB'
| where Category == 'DataPlaneRequests'
| summarize 
    TotalRequests = count(),
    AvgRequestCharge = avg(todouble(requestCharge_s)),
    ThrottledRequests = countif(tostring(statusCode_s) == '429'),
    FailedRequests = countif(tostring(statusCode_s) startswith '5'),
    AvgLatencyMs = avg(todouble(duration_s))
by bin(TimeGenerated, 1m), Resource";

        try
        {
            Response<LogsQueryResult> response = await _logsClient.QueryWorkspaceAsync(
                workspaceId: "9e123a3b-3d5b-49c9-82a3-e2d54c09f033",
                query: kql,
                timeRange: new QueryTimeRange(TimeSpan.FromMinutes(10))
            );

            List<CosmosResource> cosmosMetrics = new List<CosmosResource>();
            var table = response.Value.Table;
            foreach (var row in table.Rows)
            {
                var time = row["TimeGenerated"];
                var resource = row["Resource"];
                var totalReq = row["TotalRequests"];
                var avgRU = row["AvgRequestCharge"];
                var throttled = row["ThrottledRequests"];
                var failed = row["FailedRequests"];
                var latency = row["AvgLatencyMs"];
                var cosmosResource = new CosmosResource(
                    resourceId: "/subscriptions/9487cd81-9520-47e2-941d-2cfc4dda3b30/resourceGroups/super-fans/providers/Microsoft.DocumentDb/databaseAccounts/super-fans-users",
                    resourceName: "super-fans-users", // Extract from resource if needed
                    resourceType: "Microsoft.DocumentDB/databaseAccounts",
                    subscriptionId: "9487cd81-9520-47e2-941d-2cfc4dda3b30", // Extract from resource if needed
                    resourceGroupName: "super-fans" // Extract from resource if needed
                )
                {
                    Timestamp = DateTime.Parse(time.ToString()),
                    AvgRequestCharge = Convert.ToDouble(avgRU),
                    ThrottledRequests = Convert.ToInt32(throttled),
                    FailedRequests = Convert.ToInt32(failed),
                    AvgLatencyMs = Convert.ToDouble(latency)
                };
                cosmosMetrics.Add(cosmosResource);
            }
            if (cosmosMetrics.Count > 0)
            {
                
                await parquetWriter.WriteMetricsAsParquetAsync(cosmosMetrics);
                Console.WriteLine($"Successfully wrote {cosmosMetrics.Count} Cosmos DB metrics to Parquet.");
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Failed to collect Cosmos DB metrics. {ex}");
            throw;
        }
    }
}