using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

public class AzureDataLakeParquetWriter
{
    private readonly DataLakeServiceClient dataLakeServiceClient;
    private readonly string fileSystemName;
    private readonly string directoryPath;
    // Implementation for writing Parquet files to Azure Data Lake
    public AzureDataLakeParquetWriter(string storageAccountName, string fileSystemName, string directoryPath)
    {
        dataLakeServiceClient = new DataLakeServiceClient(new Uri($"https://{storageAccountName}.dfs.core.windows.net"), new DefaultAzureCredential());
        this.fileSystemName = fileSystemName;
        this.directoryPath = directoryPath;
    }

    public async Task WriteMetricsAsParquetAsync(List<CosmosResourceMetrics> metrics)
    {
        try
        {
            var schema = new ParquetSchema(
                new DataField<DateTime>("TimeGenerated"),
                new DataField<double>("AvgRequestCharge"),
                new DataField<int>("ThrottledRequests"),
                new DataField<int>("FailedRequests"),
                new DataField<double>("AvgLatencyMs"),
                new DataField<double>("CpuUsage")
            );
            // Prepare column-wise data for each field
            var timeGeneratedColumn = new DataColumn(
                schema.DataFields[0],
                metrics.Select(m => m.TimeGenerated).ToArray()
            );
            var avgRequestChargeColumn = new DataColumn(
                schema.DataFields[1],
                metrics.Select(m => m.AvgRequestCharge).ToArray()
            );
            var throttledRequestsColumn = new DataColumn(
                schema.DataFields[2],
                metrics.Select(m => m.ThrottledRequests).ToArray()
            );
            var failedRequestsColumn = new DataColumn(
                schema.DataFields[3],
                metrics.Select(m => m.FailedRequests).ToArray()
            );
            var avgLatencyMsColumn = new DataColumn(
                schema.DataFields[4],
                metrics.Select(m => m.AvgLatencyMs).ToArray()
            );
            var cpuUsageColumn = new DataColumn(
                schema.DataFields[5],
                metrics.Select(m => m.CpuUsage).ToArray()
            );

            var fileSystemClient = dataLakeServiceClient.GetFileSystemClient(fileSystemName);
            fileSystemClient.CreateIfNotExists();
            var directoryClient = fileSystemClient.GetDirectoryClient(directoryPath);
            directoryClient.CreateIfNotExists();
            var timestamp = DateTime.UtcNow;
            string filePath = $"{directoryPath}/{timestamp:yyyy/MM/dd/HH}/{timestamp:mm}.parquet";
            var fileClient = directoryClient.GetFileClient($"{timestamp:mm}.parquet");
                
                using (var stream = new MemoryStream())
                {
                    using (var parquetWriter = await ParquetWriter.CreateAsync(schema, stream))
                    {
                        using (var groupWriter = parquetWriter.CreateRowGroup())
                    {
                        await groupWriter.WriteColumnAsync(timeGeneratedColumn);
                        await groupWriter.WriteColumnAsync(avgRequestChargeColumn);
                        await groupWriter.WriteColumnAsync(throttledRequestsColumn);
                        await groupWriter.WriteColumnAsync(failedRequestsColumn);
                        await groupWriter.WriteColumnAsync(avgLatencyMsColumn);
                        await groupWriter.WriteColumnAsync(cpuUsageColumn);
                    }
                    }
                    stream.Position = 0;
                    await fileClient.UploadAsync(stream, overwrite: true);
                }

        }
        catch (System.Exception)
        {

            throw;
        }

       
    }


}

public class CosmosResourceMetrics
{
    public DateTime TimeGenerated { get; set; }
    public double AvgRequestCharge { get; set; }
    public double CpuUsage { get; set; }
    public int ThrottledRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AvgLatencyMs { get; set; }
}