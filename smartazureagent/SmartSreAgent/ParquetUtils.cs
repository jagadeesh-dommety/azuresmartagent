using System.Text.Json;
using OpenAI.Embeddings;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

public static class ParquetUtils
{
    private static readonly ParquetSchema MetricsSchema = new ParquetSchema(
        new DataField<string>("ResourceId"),
        new DataField<string>("ResourceName"),
        new DataField<string>("ResourceType"),
        new DataField<string>("SubscriptionId"),
        new DataField<string>("ResourceGroupName"),
        new DataField<DateTime>("Timestamp"),
        new DataField<string>("MetricsJson"),
        new DataField<string>("Embeddings")
    );

    public static async Task WriteMetricsToParquet(Stream outputStream, List<CosmosResource> metrics, OpenAIEmbeddingCollection embeddings)
    {
        var dataFields = MetricsSchema.GetDataFields();

        // Convert each column into Parquet DataColumn objects
        var columns = new List<DataColumn>
        {
            new DataColumn(dataFields[0], metrics.Select(m => m.ResourceId).ToArray()),
            new DataColumn(dataFields[1], metrics.Select(m => m.ResourceName).ToArray()),
            new DataColumn(dataFields[2], metrics.Select(m => m.ResourceType).ToArray()),
            new DataColumn(dataFields[3], metrics.Select(m => m.SubscriptionId).ToArray()),
            new DataColumn(dataFields[4], metrics.Select(m => m.ResourceGroupName).ToArray()),
            new DataColumn(dataFields[5], metrics.Select(m => m.Timestamp).ToArray()),
            new DataColumn(dataFields[6],
                metrics.Select(m => JsonSerializer.Serialize(new
                {
                    m.AvgRequestCharge,
                    m.ThrottledRequests,
                    m.FailedRequests,
                    m.AvgLatencyMs
                })).ToArray()),
            new DataColumn(dataFields[7],
                embeddings.Select(e => JsonSerializer.Serialize(e.ToFloats())).ToArray())
        };

        // Write to Parquet memory stream
        using var parquetWriter = await ParquetWriter.CreateAsync(MetricsSchema, outputStream);
        using var groupWriter = parquetWriter.CreateRowGroup();

        foreach (var column in columns)
        {
           await groupWriter.WriteColumnAsync(column);
        }

        outputStream.Position = 0;
    }
}
