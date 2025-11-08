using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.Azure.Cosmos;
using OpenAI.Embeddings;
using SmartSreAgent;

public class AzureDataLakeParquetWriter
{
    private readonly DataLakeServiceClient _dataLakeServiceClient;
    private readonly string _fileSystemName;
    private readonly string _directoryPath;
    private readonly EmbeddingService _embeddingService;
    private readonly RAGCosmos rAGCosmos;

    public AzureDataLakeParquetWriter(
        string storageAccountName,
        string fileSystemName,
        string directoryPath,
        RAGCosmos rAGCosmos)
    {
        _dataLakeServiceClient = new DataLakeServiceClient(
            new Uri($"https://{storageAccountName}.dfs.core.windows.net"),
            new AzureCliCredential());

        _fileSystemName = fileSystemName;
        _directoryPath = directoryPath;
        _embeddingService = new EmbeddingService(
            Constants.OpenAiEndpoint,
            Constants.OpenAiApiKey,
            Constants.OpenAiModel);
        this.rAGCosmos = rAGCosmos;
    }

    public async Task WriteMetricsAsParquetAsync(List<CosmosResource> metrics)
    {
        try
        {
            // Generate embeddings using EmbeddingService
            var embeddings = _embeddingService.GenerateEmbeddings(
                metrics.Select(m => m.ToString()).ToArray());

            // Prepare Parquet stream
            using var stream = new MemoryStream();
            await ParquetUtils.WriteMetricsToParquet(stream, metrics, embeddings);

            // Upload to Data Lake
            await UploadToDataLakeAsync(stream);

            var texts = metrics.Select(m => m.ToString()).ToArray();  // 10 strings

            var docs = new List<EmbeddingDocument>();
            for (int i = 0; i < metrics.Count; i++)
            {
                var doc = new EmbeddingDocument(metrics[i], embeddings.ElementAt(i).ToFloats().ToArray(), "CosmosDB");  // One doc per minute
                docs.Add(doc);
            }

            // Batch insert to Cosmos
            var tasks = docs.Select(doc => rAGCosmos.CreateItemAsync(doc));
            await Task.WhenAll(tasks);  // ~5-10 RU total for batch
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to write metrics to Parquet: {ex.Message}");
            throw;
        }
    }

    private async Task UploadToDataLakeAsync(Stream parquetStream)
    {
        var fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(_fileSystemName);
        await fileSystemClient.CreateIfNotExistsAsync();

        var timestamp = DateTime.UtcNow;
        var hourDirectoryPath = $"{_directoryPath}/{timestamp:yyyyMMddHH}";
        var directoryClient = fileSystemClient.GetDirectoryClient(hourDirectoryPath);
        await directoryClient.CreateIfNotExistsAsync();

        var fileClient = directoryClient.GetFileClient($"{timestamp:mm}.parquet");

        parquetStream.Position = 0;
        await fileClient.UploadAsync(parquetStream, overwrite: true);

        Console.WriteLine($"[Info] Parquet file uploaded to {hourDirectoryPath}/{timestamp:mm}.parquet");
    }
}

public class EmbeddingService
{
    private readonly AzureOpenAIClient _azureOpenAIClient;

    public EmbeddingService(string openAiEndpoint, string openAiApiKey, string openAiModel)
    {
        _azureOpenAIClient = new AzureOpenAIClient(openAiEndpoint, openAiApiKey, openAiModel);
    }

    public OpenAIEmbeddingCollection GenerateEmbeddings(string[] inputs)
    {
        return _azureOpenAIClient.GenerateEmbeddings(inputs);
    }
}
