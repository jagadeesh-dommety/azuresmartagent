using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using OpenAI;
using OpenAI.Embeddings;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

public class AzureDataLakeParquetWriter
{
    private readonly DataLakeServiceClient dataLakeServiceClient;
    private readonly string fileSystemName;
    private readonly string directoryPath;
    private readonly EmbeddingClient embeddingClient;
    // Implementation for writing Parquet files to Azure Data Lake
    public AzureDataLakeParquetWriter(string storageAccountName, string fileSystemName, string directoryPath)
    {
        dataLakeServiceClient = new DataLakeServiceClient(new Uri($"https://{storageAccountName}.dfs.core.windows.net"), new AzureCliCredential());
        this.fileSystemName = fileSystemName;
        this.directoryPath = directoryPath;
        var endpoint = new Uri("https://ai-jagadeeshdommety5277ai983496962694.cognitiveservices.azure.com/openai/v1/");
        var model = "text-embedding-3-small";

        OpenAIClient client = new(
    new System.ClientModel.ApiKeyCredential("<api key>"),
    new OpenAIClientOptions()
    {
        Endpoint = endpoint
    });
//https://smartmetrics.blob.core.windows.net/metrics/resources/2025102316/36.parquet
   embeddingClient = client.GetEmbeddingClient(model);
    }

    public async Task WriteMetricsAsParquetAsync(List<CosmosResource> metrics)
    {
        try
        {
            var schema = new ParquetSchema(
                new DataField<string>("ResourceId"),
                new DataField<string>("ResourceName"),
                new DataField<string>("ResourceType"),
                new DataField<string>("SubscriptionId"),
                new DataField<string>("ResourceGroupName"),
                new DataField<DateTime>("Timestamp"),
                new DataField<string>("MetricsJson"),
                new DataField<string>("Embeddings")
            );
            // Prepare column-wise data for each field
            var resourceIdColumn = new DataColumn(
                schema.GetDataFields()[0],
                metrics.Select(m => m.ResourceId).ToArray()
            );
            var resourceNameColumn = new DataColumn(
                schema.GetDataFields()[1],
                metrics.Select(m => m.ResourceName).ToArray()
            );
            var resourceTypeColumn = new DataColumn(
                schema.GetDataFields()[2],
                metrics.Select(m => m.ResourceType).ToArray()
            );
            var subscriptionIdColumn = new DataColumn(
                schema.GetDataFields()[3],
                metrics.Select(m => m.SubscriptionId).ToArray()
            );
            var resourceGroupNameColumn = new DataColumn(
                schema.GetDataFields()[4],
                metrics.Select(m => m.ResourceGroupName).ToArray()
            );
            var timestampColumn = new DataColumn(
                schema.GetDataFields()[5],
                metrics.Select(m => m.Timestamp).ToArray()
            );
            var metricsJsonColumn = new DataColumn(
                schema.GetDataFields()[6],
                metrics.Select(m => JsonSerializer.Serialize(new
                {
                    m.AvgRequestCharge,
                    m.ThrottledRequests,
                    m.FailedRequests,
                    m.AvgLatencyMs
                })).ToArray()
            );
            OpenAIEmbeddingCollection embeddings = embeddingClient.GenerateEmbeddings(
                metrics.Select(m => m.ToString()).ToArray()
            );
            var embeddingsColumn = new DataColumn(
                schema.GetDataFields()[7],
                embeddings.Select(e => JsonSerializer.Serialize(e.ToFloats())).ToArray()
            );

            var fileSystemClient = dataLakeServiceClient.GetFileSystemClient(fileSystemName);
            await fileSystemClient.CreateIfNotExistsAsync();
            var timestamp = DateTime.UtcNow;
            var hourDirectoryPath = $"{directoryPath}/{timestamp:yyyyMMddHH}";
            var directoryClient = fileSystemClient.GetDirectoryClient(hourDirectoryPath);
            await directoryClient.CreateIfNotExistsAsync();
            var fileClient = directoryClient.GetFileClient($"{timestamp:mm}.parquet");

            using (var stream = new MemoryStream())
            {
                using (var parquetWriter = await ParquetWriter.CreateAsync(schema, stream))
                {
                    using (var groupWriter = parquetWriter.CreateRowGroup())
                    {
                        await groupWriter.WriteColumnAsync(resourceIdColumn);
                        await groupWriter.WriteColumnAsync(resourceNameColumn);
                        await groupWriter.WriteColumnAsync(resourceTypeColumn);
                        await groupWriter.WriteColumnAsync(subscriptionIdColumn);
                        await groupWriter.WriteColumnAsync(resourceGroupNameColumn);
                        await groupWriter.WriteColumnAsync(timestampColumn);
                        await groupWriter.WriteColumnAsync(metricsJsonColumn);
                        await groupWriter.WriteColumnAsync(embeddingsColumn);
                    }
                }
                stream.Position = 0;
                await fileClient.UploadAsync(stream, overwrite: true);
            }

        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Error writing Parquet file: {ex.Message}");
            throw;
        }


    }

}