using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SmartSreAgent;
public record EmbeddingDocument : CosmosItem  // Assumes CosmosItem provides base props like _rid, _self
{
    [JsonPropertyName("resourceid")]
    public string resourceid { get; set; }
    // New: Original text for RAG context (from m.ToString())
    [JsonPropertyName("content")]
    public string content { get; init; } = string.Empty;

    [JsonPropertyName("embedding")]
    public float[] embedding { get; init; } = Array.Empty<float>();  // Vector (validate Length == 1536 in inserter)

    [JsonPropertyName("timestamp")]
    public DateTime timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("resourceType")]
    public string resourceType { get; init; } = string.Empty;  // For /resourceType partition key

    [JsonPropertyName("metricsJson")]
    public string? metricsJson { get; init; }  // e.g., serialized snippet

    // Constructor for 10-minute batching (from your metrics loop)
    public EmbeddingDocument(CosmosResource metric, float[] embedding, string resourceType)
    {
        content = metric.ToString();  // Full text for RAG
        this.resourceid = metric.ResourceId;
        this.embedding = embedding;  // Single vector from batch
        timestamp = metric.Timestamp;  // Per-minute timestamp
        this.resourceType = resourceType;
        metricsJson = System.Text.Json.JsonSerializer.Serialize(new { metric.AvgRequestCharge, metric.ThrottledRequests, metric.FailedRequests, metric.AvgLatencyMs }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });  // Quick metrics snippet
    }
}